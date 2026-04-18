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

namespace AgValoniaGPS.VehicleSimulator.Modules;

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
    public double SteerResponseRate { get; set; } = 0.5; // How fast WAS follows command (0-1)
    public bool SimulateSteerResponse { get; set; } = true;

    // Counters
    public long ReceivedCommandCount { get; private set; }
    public long SentFeedbackCount { get; private set; }
    public long SentHelloCount { get; private set; }
    public AutoSteerCommand? LastCommand { get; private set; }

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
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _receiveTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { _helloTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }

    /// <summary>
    /// Send a single PGN 253 steer feedback packet.
    /// </summary>
    public void SendSteerFeedback()
    {
        var data = new byte[8];

        // Bytes 0-1: Actual steer angle (degrees * 100, int16 LE)
        short angleRaw = (short)(ActualSteerAngleDeg * 100);
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

        // Byte 6: Switch status
        byte switches = 0;
        if (!WorkSwitchActive) switches |= 0x01; // bit 0: work switch (inverted: 1=OFF)
        if (SteerSwitchActive) switches |= 0x02;  // bit 1: steer switch
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
                var cmd = PgnProtocol.ParseAutoSteerCommand(data);
                CommandedSteerAngleDeg = cmd.SteerAngleDeg;
                LastCommand = cmd;
                ReceivedCommandCount++;

                // Simulate WAS response
                if (SimulateSteerResponse)
                {
                    ActualSteerAngleDeg += (CommandedSteerAngleDeg - ActualSteerAngleDeg) * SteerResponseRate;
                }

                // Send feedback after receiving command
                SendSteerFeedback();
                break;

            case PgnProtocol.PGN_HELLO_FROM_HOST:
                // Respond with steer module hello
                SendHello();
                break;

            case PgnProtocol.PGN_STEER_SETTINGS:
            case PgnProtocol.PGN_STEER_CONFIG:
                // Acknowledge config packets (no response needed per protocol)
                break;
        }
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

    public void Dispose()
    {
        Stop();
        _udp.Dispose();
        _cts?.Dispose();
    }
}
