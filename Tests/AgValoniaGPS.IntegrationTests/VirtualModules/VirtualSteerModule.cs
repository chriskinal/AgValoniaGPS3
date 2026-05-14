// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AgValoniaGPS.IntegrationTests.VirtualModules;

/// <summary>
/// Virtual steer module (Teensy). Listens for PGN 254 (AutoSteer) commands,
/// responds with PGN 253 (steer feedback) and PGN 126 (hello).
/// Simulates a wheel angle sensor (WAS) and steer motor.
/// </summary>
public class VirtualSteerModule : IDisposable
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _hostEndpoint;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _helloTask;
    private Task? _tickTask;

    // Real Teensy steer modules stream PGN 253 continuously at ~10 Hz regardless
    // of host activity. The host's autosteer control loop relies on that cadence.
    public int TickIntervalMs { get; set; } = 100;

    // Simulated WAS (Wheel Angle Sensor) state
    public double ActualSteerAngleDeg { get; set; }
    public double CommandedSteerAngleDeg { get; private set; }
    public bool SteerSwitchActive { get; set; } = true;
    public bool WorkSwitchActive { get; set; }
    public byte PwmDisplay { get; set; }

    // IMU (if separate module, typically embedded in $PANDA now)
    public double ImuHeadingDeg { get; set; }
    public double ImuRollDeg { get; set; }

    // Simulation behavior
    // Legacy WAS-follow path used by older Services.Tests fixtures: applies a
    // fraction of the command-vs-actual gap on every PGN 254 receive. The
    // current default is the PWM-driven tick loop below; this flag stays for
    // back-compat with tests that opt into the simpler model.
    public double SteerResponseRate { get; set; } = 0.5;
    public bool SimulateSteerResponse { get; set; } = false;

    // === Synthetic PWM control loop ===
    // Modelled after the Teensy AutoSteer firmware: proportional control with
    // MinPWM dead-band promotion (so the motor can overcome static friction)
    // and a small commanded-vs-actual deadband to keep the wheel still at the
    // target. Internal PWM is signed (-HighPWM .. +HighPWM); PGN 253 byte 7
    // reports magnitude only, with direction handled by the driver pin on real
    // hardware.
    public int CurrentPwm { get; private set; }

    /// <summary>Maps PWM duty (1..HighPWM) to deg/sec of WAS movement.</summary>
    public double PwmToDegPerSec { get; set; } = 0.2;

    /// <summary>Below this absolute error, drive pwm to zero so the wheel can rest.</summary>
    public double AngleDeadbandDeg { get; set; } = 0.05;

    // Counters
    public long ReceivedCommandCount { get; private set; }
    public long SentFeedbackCount { get; private set; }
    public long SentHelloCount { get; private set; }
    public AutoSteerCommand? LastCommand { get; private set; }

    // === Applied settings from PGN 252 (SteerSettings) ===
    // Defaults mirror AgOpenGPS Teensy firmware boot defaults so the simulator
    // behaves sanely before any host configuration arrives.
    public byte Kp { get; private set; } = 40;
    public byte HighPWM { get; private set; } = 160;
    public byte LowPWM { get; private set; } = 30;
    public byte MinPWM { get; private set; } = 25;
    public byte CountsPerDegree { get; private set; } = 100;
    public short WasOffset { get; private set; }
    public double AckermannFix { get; private set; } = 1.0;

    // === Applied config from PGN 251 (SteerConfig) ===
    public bool InvertWas { get; private set; }
    public bool IsRelayActiveHigh { get; private set; }
    public bool MotorDriveDirection { get; private set; }
    public bool SingleInputWas { get; private set; } = true;
    public bool CytronDriver { get; private set; } = true;
    public bool SteerSwitchEnabled { get; private set; }
    public bool SteerButtonEnabled { get; private set; }
    public bool ShaftEncoderEnabled { get; private set; }
    public byte PulseCountMax { get; private set; } = 5;
    public byte MinSpeed { get; private set; }
    public bool IsDanfoss { get; private set; }
    public bool PressureSensorEnabled { get; private set; }
    public bool CurrentSensorEnabled { get; private set; }
    public bool IsUseYAxis { get; private set; }

    // Whether the operator has wired a physical work switch. Reported via
    // PGN 253 byte 6 bit 0; not configurable through PGN 251 directly, so the
    // simulator exposes it as a settable flag.
    public bool WorkSwitchEnabled { get; set; }

    public VirtualSteerModule(int listenPort = 8888, int hostPort = 9999, string hostIp = "127.0.0.1")
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));
        _hostEndpoint = new IPEndPoint(IPAddress.Parse(hostIp), hostPort);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));
        _helloTask = Task.Run(() => HelloLoop(_cts.Token));
        _tickTask = Task.Run(() => TickLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _receiveTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _helloTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _tickTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }

    /// <summary>
    /// Calibrated WAS angle as a real Teensy would report it in PGN 253.
    /// Treats ActualSteerAngleDeg as the physical wheel position, converts to
    /// raw WAS counts, then applies the host's WasOffset / CountsPerDegree /
    /// InvertWas calibration so any wizard re-zero shows up immediately.
    /// </summary>
    public double ReportedSteerAngleDeg
    {
        get
        {
            double cpd = CountsPerDegree > 0 ? CountsPerDegree : 1.0;
            double rawCounts = ActualSteerAngleDeg * cpd;
            double calibrated = (rawCounts - WasOffset) / cpd;
            return InvertWas ? -calibrated : calibrated;
        }
    }

    /// <summary>
    /// Send a single PGN 253 steer feedback packet.
    /// </summary>
    public void SendSteerFeedback()
    {
        var data = new byte[8];

        // Bytes 0-1: Calibrated WAS angle (degrees * 100, int16 LE)
        short angleRaw = (short)(ReportedSteerAngleDeg * 100);
        data[0] = (byte)(angleRaw & 0xFF);
        data[1] = (byte)((angleRaw >> 8) & 0xFF);

        // Bytes 2-3: IMU heading (degrees * 10, int16 LE)
        short headingRaw = (short)(ImuHeadingDeg * 10);
        data[2] = (byte)(headingRaw & 0xFF);
        data[3] = (byte)((headingRaw >> 8) & 0xFF);

        // Bytes 4-5: IMU roll (degrees * 10, int16 LE)
        short rollRaw = (short)(ImuRollDeg * 10);
        data[4] = (byte)(rollRaw & 0xFF);
        data[5] = (byte)((rollRaw >> 8) & 0xFF);

        // Byte 6: Switch status. When the operator has not wired a physical
        // switch (the matching config flag is OFF), the simulator always
        // reports "active" — matching the legacy Teensy convention where an
        // unwired input is treated as continuously engaged so the host's
        // autosteer can still arm. When the flag is ON, the UI toggle drives
        // the bit.
        bool steerActive = SteerSwitchEnabled ? SteerSwitchActive : true;
        bool workActive = WorkSwitchEnabled ? WorkSwitchActive : true;
        byte switches = 0;
        if (!workActive) switches |= 0x01;  // bit 0: work switch (inverted: 1=OFF)
        if (steerActive) switches |= 0x02;  // bit 1: steer switch
        data[6] = switches;

        // Byte 7: PWM display
        data[7] = PwmDisplay;

        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_STEER_DATA, data);
        _udp.Send(packet, packet.Length, _hostEndpoint);
        SentFeedbackCount++;
    }

    /// <summary>
    /// Send PGN 250 sensor data (pressure/current).
    /// </summary>
    public void SendSensorData(byte sensorValue)
    {
        var data = new byte[8];
        data[0] = sensorValue;
        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_SENSOR_DATA, data);
        _udp.Send(packet, packet.Length, _hostEndpoint);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udp.ReceiveAsync(ct);
                ProcessPacket(result.Buffer);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
        }
    }

    private void ProcessPacket(byte[] data)
    {
        if (!PgnProtocol.IsValidPacket(data, data.Length))
            return;

        byte pgn = PgnProtocol.GetPgn(data);

        switch (pgn)
        {
            case PgnProtocol.PGN_AUTOSTEER_TO_MODULE:
                ApplyAutoSteerCommand(PgnProtocol.ParseAutoSteerCommand(data));
                // PGN 253 is emitted by the periodic TickLoop, not in response to PGN 254.
                break;

            case PgnProtocol.PGN_HELLO_FROM_HOST:
                // Respond with steer module hello
                SendHello();
                break;

            case PgnProtocol.PGN_STEER_SETTINGS:
                ApplySteerSettings(PgnProtocol.ParseSteerSettings(data));
                break;

            case PgnProtocol.PGN_STEER_CONFIG:
                ApplySteerConfig(PgnProtocol.ParseSteerConfig(data));
                break;
        }
    }

    /// <summary>
    /// Apply a parsed PGN 254 (AutoSteer command) payload.
    /// Public so tests can drive the control loop deterministically without
    /// going over UDP and relying on receive-task timing.
    /// </summary>
    public void ApplyAutoSteerCommand(AutoSteerCommand cmd)
    {
        CommandedSteerAngleDeg = cmd.SteerAngleDeg;
        LastCommand = cmd;
        ReceivedCommandCount++;

        // Legacy WAS-follow: applies a fraction of the gap on every receive.
        // Disabled by default — the PWM tick loop is the real model.
        if (SimulateSteerResponse)
        {
            ActualSteerAngleDeg += (CommandedSteerAngleDeg - ActualSteerAngleDeg) * SteerResponseRate;
        }
    }

    /// <summary>
    /// Apply a freshly received PGN 252 (SteerSettings) payload.
    /// Public so tests can drive the parse/apply path without going over UDP.
    /// </summary>
    public void ApplySteerSettings(SteerSettingsPacket s)
    {
        Kp = s.Kp;
        HighPWM = s.HighPWM;
        LowPWM = s.LowPWM;
        MinPWM = s.MinPWM;
        CountsPerDegree = s.CountsPerDegree;
        WasOffset = s.WasOffset;
        AckermannFix = s.AckermannFix;
    }

    /// <summary>
    /// Apply a freshly received PGN 251 (SteerConfig) payload.
    /// Public so tests can drive the parse/apply path without going over UDP.
    /// </summary>
    public void ApplySteerConfig(SteerConfigPacket c)
    {
        InvertWas = c.InvertWas;
        IsRelayActiveHigh = c.IsRelayActiveHigh;
        MotorDriveDirection = c.MotorDriveDirection;
        SingleInputWas = c.SingleInputWas;
        CytronDriver = c.CytronDriver;
        SteerSwitchEnabled = c.SteerSwitchEnabled;
        SteerButtonEnabled = c.SteerButtonEnabled;
        ShaftEncoderEnabled = c.ShaftEncoderEnabled;
        PulseCountMax = c.PulseCountMax;
        MinSpeed = c.MinSpeed;
        IsDanfoss = c.IsDanfoss;
        PressureSensorEnabled = c.PressureSensorEnabled;
        CurrentSensorEnabled = c.CurrentSensorEnabled;
        IsUseYAxis = c.IsUseYAxis;
    }

    private void SendHello()
    {
        var packet = PgnProtocol.BuildHelloPacket(PgnProtocol.PGN_HELLO_AUTOSTEER);
        _udp.Send(packet, packet.Length, _hostEndpoint);
        SentHelloCount++;
    }

    private async Task HelloLoop(CancellationToken ct)
    {
        // Send periodic hello even without host hello (for initial discovery)
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SendHello();
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Tick();
                await Task.Delay(TickIntervalMs, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
        }
    }

    /// <summary>
    /// Single periodic tick. Public so deterministic tests can drive emission
    /// without relying on Task.Delay timing.
    /// </summary>
    public void Tick()
    {
        UpdateSteerControl(TickIntervalMs / 1000.0);
        SendSteerFeedback();
    }

    /// <summary>
    /// Synthetic PWM control + WAS integration step.
    /// Proportional law: pwm = error * Kp / 10, clamped to [-HighPWM, +HighPWM].
    /// Below MinPWM but non-zero, magnitude is promoted to MinPWM (real motors
    /// need that to break static friction). Below AngleDeadbandDeg of error,
    /// pwm is forced to zero so the wheel can rest at the commanded angle.
    /// The WAS is then moved by pwm × PwmToDegPerSec × dt, clamped so a single
    /// tick can never overshoot the commanded angle. Runs only when the host
    /// has IsEngaged set in PGN 254 — otherwise the wheel is freely operator-
    /// controlled and the simulator just holds whatever ActualSteerAngleDeg is.
    /// </summary>
    public void UpdateSteerControl(double dt)
    {
        bool engaged = LastCommand?.IsEngaged ?? false;
        double error = CommandedSteerAngleDeg - ActualSteerAngleDeg;

        int pwm;
        if (!engaged || Math.Abs(error) < AngleDeadbandDeg)
        {
            pwm = 0;
        }
        else
        {
            double raw = error * Kp / 10.0;
            pwm = (int)raw;
            if (pwm > HighPWM) pwm = HighPWM;
            else if (pwm < -HighPWM) pwm = -HighPWM;
            int absPwm = Math.Abs(pwm);
            if (absPwm > 0 && absPwm < MinPWM)
            {
                pwm = pwm > 0 ? MinPWM : -MinPWM;
            }
        }

        CurrentPwm = pwm;
        PwmDisplay = (byte)Math.Min(255, Math.Abs(pwm));

        if (engaged && pwm != 0)
        {
            double dAngle = pwm * PwmToDegPerSec * dt;
            if (Math.Abs(dAngle) > Math.Abs(error)) dAngle = error;
            ActualSteerAngleDeg += dAngle;
        }
    }

    public void Dispose()
    {
        Stop();
        _udp.Dispose();
        _cts?.Dispose();
    }
}
