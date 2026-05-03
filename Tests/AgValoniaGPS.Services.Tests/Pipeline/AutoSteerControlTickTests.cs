// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Pipeline;
using AgValoniaGPS.Services.Track;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests.Pipeline;

/// <summary>
/// Verifies commit 4 of #313: SendPgnsForControlTick exists on
/// IAutoSteerService and emits PGN 254 + PGN 239 to the UDP transport
/// when the control loop ticks.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class AutoSteerControlTickTests
{
    private IUdpCommunicationService _udp = null!;
    private AutoSteerService _autoSteer = null!;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());
        var appState = new ApplicationState();
        _udp = Substitute.For<IUdpCommunicationService>();
        var gps = new GpsService();
        gps.Start();
        _autoSteer = new AutoSteerService(new TrackGuidanceService(), _udp, gps, appState);
        _autoSteer.Start();
    }

    [Test]
    public void ControlLoopTick_TriggersTwoPgnSends()
    {
        var loop = new ManualSteerMachineLoop();
        loop.Ticked += _ => _autoSteer.SendPgnsForControlTick();
        loop.Start();

        loop.Tick(0);

        // PGN 254 (autosteer) + PGN 239 (machine) = 2 sends per tick.
        _udp.Received(2).SendToModules(Arg.Any<byte[]>());
    }

    [Test]
    public void ControlLoopTick_NotEnabled_SuppressesSends()
    {
        var loop = new ManualSteerMachineLoop();
        loop.Ticked += _ => _autoSteer.SendPgnsForControlTick();
        loop.Start();
        _autoSteer.Stop();

        loop.Tick(0);

        _udp.DidNotReceive().SendToModules(Arg.Any<byte[]>());
    }

    [Test]
    public void MultipleTicks_FireProportionalSendCount()
    {
        var loop = new ManualSteerMachineLoop();
        loop.Ticked += _ => _autoSteer.SendPgnsForControlTick();
        loop.Start();

        for (int i = 0; i < 5; i++)
            loop.Tick(i);

        // 5 ticks × 2 PGNs each = 10 sends.
        _udp.Received(10).SendToModules(Arg.Any<byte[]>());
    }
}
