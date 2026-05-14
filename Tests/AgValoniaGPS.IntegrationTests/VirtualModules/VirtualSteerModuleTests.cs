// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;

namespace AgValoniaGPS.IntegrationTests.VirtualModules;

/// <summary>
/// Unit tests for VirtualSteerModule — verifies that the simulator emulates
/// a real Teensy steer module end-to-end (periodic PGN 253 emission, PGN 251/252
/// parsing, WAS calibration, synthetic PWM loop, switch handling).
/// </summary>
[TestFixture]
[NonParallelizable] // Tests open UDP sockets on fixed ports
public class VirtualSteerModuleTests
{
    private const int HostPort = 19999;
    private const int ModulePort = 18888;
    private const string LoopbackIp = "127.0.0.1";

    /// <summary>
    /// Lightweight UDP listener that captures PGN 253 packets sent to the host port.
    /// </summary>
    private sealed class HostListener : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly CancellationTokenSource _cts = new();
        public List<byte[]> SteerPackets { get; } = new();

        public HostListener(int port)
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            _ = ReceiveLoopAsync();
        }

        private async System.Threading.Tasks.Task ReceiveLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var res = await _udp.ReceiveAsync(_cts.Token);
                    if (res.Buffer.Length >= 6
                        && res.Buffer[0] == 0x80 && res.Buffer[1] == 0x81
                        && res.Buffer[3] == PgnProtocol.PGN_STEER_DATA)
                    {
                        lock (SteerPackets) SteerPackets.Add(res.Buffer);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { break; }
            }
        }

        public byte[]? LatestSteerPacket()
        {
            lock (SteerPackets)
            {
                return SteerPackets.Count == 0 ? null : SteerPackets[^1];
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _udp.Dispose();
            _cts.Dispose();
        }
    }

    private static SteerSettingsPacket DefaultSettings(byte cpd = 100, short wasOffset = 0)
    {
        return new SteerSettingsPacket
        {
            Kp = 40, HighPWM = 160, LowPWM = 30, MinPWM = 25,
            CountsPerDegree = cpd, WasOffset = wasOffset, AckermannFix = 1.0,
        };
    }

    private static SteerConfigPacket DefaultConfig(bool invertWas = false, bool steerSwitch = false, bool workSwitch = false)
    {
        return new SteerConfigPacket
        {
            InvertWas = invertWas,
            SingleInputWas = true,
            CytronDriver = true,
            SteerSwitchEnabled = steerSwitch,
            // PGN 251 has no WorkSwitchEnabled bit; tests set it directly on the module.
        };
    }

    [Test]
    public void WasGroundTruth_OperatorChangesVirtualCpd_ReportedAngleScales()
    {
        // Misconfigured WAS: real hardware has 85 counts/deg, but the host
        // believes it's 100 counts/deg. Wheel at 10° → 850 raw counts →
        // host applies its (wrong) calibration: 850 / 100 = 8.5° reported.
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 11, hostPort: HostPort + 11, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings(cpd: 100, wasOffset: 0));
        steer.ApplySteerConfig(DefaultConfig());
        steer.VirtualCountsPerDegree = 85.0;
        steer.VirtualWasOffset = 0;
        steer.ActualSteerAngleDeg = 10.0;

        Assert.That(steer.ReportedSteerAngleDeg, Is.EqualTo(8.5).Within(1e-9));
    }

    [Test]
    public void WasGroundTruth_CalibrationMatch_ReportedEqualsTruth()
    {
        // Host's guess matches reality — reported angle equals the wheel angle.
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 12, hostPort: HostPort + 12, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings(cpd: 85, wasOffset: 0));
        steer.ApplySteerConfig(DefaultConfig());
        steer.VirtualCountsPerDegree = 85.0;
        steer.VirtualWasOffset = 0;
        steer.ActualSteerAngleDeg = 10.0;

        Assert.That(steer.ReportedSteerAngleDeg, Is.EqualTo(10.0).Within(1e-9));
    }

    [Test]
    public void WasGroundTruth_OffsetApplied()
    {
        // Real WAS is off-centre by 20 counts; host hasn't run Zero WAS yet
        // (applied offset = 0). At 5° wheel angle: rawCounts = 5*100 + 20 = 520,
        // host's inverse: (520 - 0) / 100 = 5.2° — apparent bias the operator
        // is supposed to dial out with the wizard's Zero WAS step.
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 13, hostPort: HostPort + 13, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings(cpd: 100, wasOffset: 0));
        steer.ApplySteerConfig(DefaultConfig());
        steer.VirtualCountsPerDegree = 100.0;
        steer.VirtualWasOffset = 20;
        steer.ActualSteerAngleDeg = 5.0;

        Assert.That(steer.ReportedSteerAngleDeg, Is.EqualTo(5.2).Within(1e-9));
    }

    [Test]
    public void AppliesWasOffsetToReportedAngle()
    {
        // Slider at 1.0° with CountsPerDegree=100 → 100 raw WAS counts.
        // After offset of 20: (100 − 20) / 100 = 0.8°.
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 1, hostPort: HostPort + 1, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings(cpd: 100, wasOffset: 20));
        steer.ApplySteerConfig(DefaultConfig());
        steer.ActualSteerAngleDeg = 1.0;

        Assert.That(steer.ReportedSteerAngleDeg, Is.EqualTo(0.8).Within(1e-9));
    }

    [Test]
    public void AppliesInvertWasToReportedAngle()
    {
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 2, hostPort: HostPort + 2, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings());
        steer.ApplySteerConfig(DefaultConfig(invertWas: true));
        steer.ActualSteerAngleDeg = 5.0;

        Assert.That(steer.ReportedSteerAngleDeg, Is.EqualTo(-5.0).Within(1e-9));
    }

    private static AutoSteerCommand SteerCommand(double angleDeg, bool engaged)
    {
        byte status = engaged ? (byte)0x0C : (byte)0x00; // bit 2 = engaged, bit 3 = gps valid
        return new AutoSteerCommand
        {
            SpeedKmh = 10.0,
            Status = status,
            SteerAngleDeg = angleDeg,
            IsEngaged = engaged,
            IsGpsValid = true,
        };
    }

    [Test]
    public void ProportionalControl_ConvergesToCommand()
    {
        // 10 deg command + IsEngaged, tick the synthetic PWM control loop for
        // ~5 simulated seconds. ActualSteerAngleDeg should converge to within
        // 0.5 deg of the command.
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 3, hostPort: HostPort + 3, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings());
        steer.ApplySteerConfig(DefaultConfig());
        steer.ActualSteerAngleDeg = 0;
        steer.ApplyAutoSteerCommand(SteerCommand(angleDeg: 10.0, engaged: true));

        for (int i = 0; i < 50; i++)
        {
            steer.UpdateSteerControl(0.1);
        }

        Assert.That(steer.ActualSteerAngleDeg, Is.EqualTo(10.0).Within(0.5),
            $"Expected PWM loop to converge to 10 deg within 5 s, got {steer.ActualSteerAngleDeg}");
    }

    /// <summary>
    /// Decode PGN 253 byte 6 (switch status) from a captured packet.
    /// Bit 1 = steer switch active; bit 0 = work switch inverted (1 = off).
    /// </summary>
    private static (bool steerActive, bool workActive) DecodeSwitchByte(byte[] packet)
    {
        byte b = packet[11]; // header(5) + data[6] = byte 6 of payload
        bool steer = (b & 0x02) != 0;
        bool work = (b & 0x01) == 0; // bit 0 inverted
        return (steer, work);
    }

    [Test]
    public void SwitchConfigured_RespectsUiToggle()
    {
        using var listener = new HostListener(HostPort + 4);
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 4, hostPort: HostPort + 4, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings());
        steer.ApplySteerConfig(DefaultConfig(steerSwitch: true));
        // Operator has the physical switch wired and has flipped it off.
        steer.SteerSwitchActive = false;

        steer.Tick();
        Thread.Sleep(30);

        var packet = listener.LatestSteerPacket();
        Assert.That(packet, Is.Not.Null, "Expected a PGN 253 packet");
        var (steerBit, _) = DecodeSwitchByte(packet!);
        Assert.That(steerBit, Is.False,
            "When SteerSwitch is wired and operator has toggled OFF, PGN 253 should report SteerSwitchActive=false.");
    }

    [Test]
    public void SwitchNotConfigured_AlwaysEngaged()
    {
        using var listener = new HostListener(HostPort + 5);
        using var steer = new VirtualSteerModule(listenPort: ModulePort + 5, hostPort: HostPort + 5, hostIp: LoopbackIp);
        steer.ApplySteerSettings(DefaultSettings());
        steer.ApplySteerConfig(DefaultConfig(steerSwitch: false));
        // Even if the UI toggle says "off", with no physical switch wired the
        // module should report it as continuously engaged.
        steer.SteerSwitchActive = false;

        steer.Tick();
        Thread.Sleep(30);

        var packet = listener.LatestSteerPacket();
        Assert.That(packet, Is.Not.Null, "Expected a PGN 253 packet");
        var (steerBit, _) = DecodeSwitchByte(packet!);
        Assert.That(steerBit, Is.True,
            "When SteerSwitch is not wired, PGN 253 should always report SteerSwitchActive=true regardless of UI toggle.");
    }

    [Test]
    public void PeriodicEmission_FiresAt10Hz()
    {
        using var listener = new HostListener(HostPort);
        using var steer = new VirtualSteerModule(listenPort: ModulePort, hostPort: HostPort, hostIp: LoopbackIp);
        steer.Start();
        try
        {
            // Wait 600 ms with no incoming PGN 254. Expect >= 5 PGN 253 packets
            // (real Teensy hardware streams at ~10 Hz unconditionally).
            Thread.Sleep(650);
        }
        finally
        {
            steer.Stop();
        }

        int count;
        lock (listener.SteerPackets) count = listener.SteerPackets.Count;
        Assert.That(count, Is.GreaterThanOrEqualTo(5),
            $"Expected periodic PGN 253 emission at ~10 Hz, got {count} packets in 650 ms.");
    }
}
