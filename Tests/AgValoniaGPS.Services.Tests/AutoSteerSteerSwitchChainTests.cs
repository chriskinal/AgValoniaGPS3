// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Track;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the PGN 253 -> AutoSteerService -> StateUpdated chain so the
/// wizard's physical-switch gate sees the operator console toggle the
/// moment a fresh PGN 253 arrives, not after the next GPS packet.
///
/// Pre-fix bug surfaced on bench-test of #381: <c>ProcessSteerData</c>
/// updated <c>_lastSteerData</c> and the switch mirrors but never fired
/// <c>StateUpdated</c>, so the wizard's <c>OnStateUpdated</c> handler
/// stayed asleep on switch-only changes.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class AutoSteerSteerSwitchChainTests
{
    private IUdpCommunicationService _udp = null!;
    private AutoSteerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _udp = Substitute.For<IUdpCommunicationService>();
        var guidance = Substitute.For<ITrackGuidanceService>();
        var gps = Substitute.For<IGpsService>();
        var appState = new ApplicationState();
        _service = new AutoSteerService(guidance, _udp, gps, appState);
        _service.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _service.Stop();
    }

    /// <summary>
    /// Build a wire-valid PGN 253 packet (steer angle + switch + PWM)
    /// matching the format the simulator's VirtualSteerModule emits. We
    /// hand-construct rather than reuse a builder so the test stays
    /// self-contained and the bit layout is documented inline.
    ///
    /// Format: [0x80, 0x81, Source(0x7F), 0xFD, 8, AngleLo, AngleHi,
    ///          HeadingLo, HeadingHi, RollLo, RollHi, Switches, PWM, CRC]
    /// </summary>
    private static byte[] BuildPgn253(short angleX100, bool steerSwitch,
        bool workSwitchActive, byte pwm)
    {
        var packet = new byte[14];
        packet[0] = 0x80;
        packet[1] = 0x81;
        packet[2] = 0x7F;        // source = steer module
        packet[3] = 0xFD;        // PGN 253
        packet[4] = 8;           // payload length
        packet[5] = (byte)(angleX100 & 0xFF);
        packet[6] = (byte)((angleX100 >> 8) & 0xFF);
        packet[7] = 0; packet[8] = 0;  // deprecated heading
        packet[9] = 0; packet[10] = 0; // deprecated roll
        // Switch byte: bit 0 work (INVERTED: 0=ON), bit 1 steer, bit 2 remote.
        byte switches = 0;
        if (!workSwitchActive) switches |= 0x01;  // inverted
        if (steerSwitch) switches |= 0x02;
        packet[11] = switches;
        packet[12] = pwm;
        // CRC: sum of bytes 2 through 12.
        byte crc = 0;
        for (int i = 2; i <= 12; i++) crc += packet[i];
        packet[13] = crc;
        return packet;
    }

    private void RaiseSteerDataPgn(byte[] packet)
    {
        _udp.DataReceived += Raise.Event<EventHandler<UdpDataReceivedEventArgs>>(
            _udp,
            new UdpDataReceivedEventArgs { Data = packet, PGN = 0xFD });
    }

    [Test]
    public void Pgn253WithSteerSwitchBit_UpdatesLastSteerData()
    {
        // SteerSwitchActive on the parsed packet must flow into
        // LastSteerData so the wizard's OnStateUpdated handler — which
        // reads LastSteerData.SteerSwitchActive — can gate correctly.
        RaiseSteerDataPgn(BuildPgn253(angleX100: 1000, steerSwitch: true,
            workSwitchActive: false, pwm: 42));

        Assert.That(_service.LastSteerData.SteerSwitchActive, Is.True);
        Assert.That(_service.LastSteerData.PwmDisplay, Is.EqualTo(42));
        Assert.That(_service.LastSteerData.ActualSteerAngle, Is.EqualTo(10.0).Within(0.01));
    }

    [Test]
    public void Pgn253SwitchTransition_FiresStateUpdated()
    {
        // First packet establishes the baseline; the second flips the
        // steer switch and must drive StateUpdated so wizard gates wake
        // up without waiting for the next GPS packet.
        RaiseSteerDataPgn(BuildPgn253(0, steerSwitch: false,
            workSwitchActive: false, pwm: 0));

        int callCount = 0;
        _service.StateUpdated += (_, _) => callCount++;

        RaiseSteerDataPgn(BuildPgn253(0, steerSwitch: true,
            workSwitchActive: false, pwm: 0));

        Assert.That(callCount, Is.EqualTo(1),
            "StateUpdated must fire when the steer-switch bit changes");
        Assert.That(_service.LastSteerData.SteerSwitchActive, Is.True);
    }

    [Test]
    public void Pgn253SteadyState_DoesNotSpamStateUpdated()
    {
        // The PGN 253 stream runs at 10 Hz; if every packet fired
        // StateUpdated, UI subscribers would re-render constantly even
        // when nothing useful changed. Guard against future regression
        // where someone moves the NotifyStateUpdated call out of the
        // 'switchChanged' branch.
        RaiseSteerDataPgn(BuildPgn253(0, steerSwitch: true,
            workSwitchActive: false, pwm: 0));

        int callCount = 0;
        _service.StateUpdated += (_, _) => callCount++;

        // Three identical packets — only the angle is changing, switches
        // and remote stay the same.
        RaiseSteerDataPgn(BuildPgn253(100, steerSwitch: true,
            workSwitchActive: false, pwm: 50));
        RaiseSteerDataPgn(BuildPgn253(200, steerSwitch: true,
            workSwitchActive: false, pwm: 60));
        RaiseSteerDataPgn(BuildPgn253(300, steerSwitch: true,
            workSwitchActive: false, pwm: 70));

        Assert.That(callCount, Is.EqualTo(0),
            "Angle/PWM-only updates must not fire StateUpdated " +
            "(GPS-receive path handles per-cycle notifications)");
    }
}
