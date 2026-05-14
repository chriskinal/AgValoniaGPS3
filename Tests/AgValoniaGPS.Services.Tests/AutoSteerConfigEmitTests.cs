// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Threading;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the plumbing that lets the wizard's live setters and the
/// AutoSteer config dialog's writes both reach the module/simulator
/// without depending on the dialog being open. The service is the
/// single owner of PGN 251 + PGN 252 emission: subscribes to
/// ConfigurationStore.AutoSteer + Tool PropertyChanged, debounces, and
/// re-emits both packets. On Start() it also emits a baseline so the
/// module sees current settings without waiting for the operator to
/// touch anything.
///
/// Pre-fix bug surfaced on PR #380's bench-test: wizard step 8 wrote
/// InvertWas / WasOffset into ConfigStore.AutoSteer live, but nothing
/// in the host pushed PGN 251/252 to the module. Simulator never
/// received the updated config; Zero WAS / Invert WAS appeared to do
/// nothing; the simulator's Steer Switch toggle stayed greyed because
/// IsSteerSwitchEnabled never flowed through PGN 251.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class AutoSteerConfigEmitTests
{
    private IUdpCommunicationService _udp = null!;
    private AutoSteerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Fresh ConfigurationStore so PropertyChanged subscriptions hit
        // a clean baseline, and so the [NonParallelizable] mutations
        // from prior tests don't bleed in.
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _udp = Substitute.For<IUdpCommunicationService>();
        var guidance = Substitute.For<ITrackGuidanceService>();
        var gps = Substitute.For<IGpsService>();
        var appState = new ApplicationState();
        _service = new AutoSteerService(guidance, _udp, gps, appState);

        // Shorten the debounce so tests don't have to sleep 150 ms
        // wall-clock per assertion. 20 ms is well over the timer's
        // resolution and well under any sensible test budget.
        _service.ConfigEmitDebounceMilliseconds = 20;
    }

    [TearDown]
    public void TearDown()
    {
        _service.Stop();
    }

    [Test]
    public void Start_EmitsBaselinePgn251AndPgn252()
    {
        // Start() must push the current ConfigStore.AutoSteer values
        // immediately so the module sees them without waiting for the
        // operator to mutate something.
        _service.Start();

        // PGN 251 is 0xFB, PGN 252 is 0xFC (byte 3 in the wire format).
        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFB));
        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFC));
    }

    [Test]
    public void AutoSteerPropertyChange_ReemitsBothPgns_AfterDebounce()
    {
        _service.Start();
        _udp.ClearReceivedCalls();

        ConfigurationStore.Instance.AutoSteer.InvertWas = true;

        // Wait past the debounce window. Tight upper bound — the timer
        // fires once the storm ends; 100 ms is plenty even on a loaded
        // CI runner with a 20 ms debounce.
        WaitForEmit();

        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFB));
        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFC));
    }

    [Test]
    public void RapidPropertyChanges_CoalesceIntoSingleEmit()
    {
        _service.Start();
        _udp.ClearReceivedCalls();

        // Burst-mutate five properties tightly. With a 20 ms debounce
        // and back-to-back sets the timer must coalesce these into a
        // single emit pair.
        var auto = ConfigurationStore.Instance.AutoSteer;
        auto.InvertWas = true;
        auto.WasOffset = 100;
        auto.CountsPerDegree = 50;
        auto.MinPwm = 35;
        auto.MaxPwm = 240;

        WaitForEmit();

        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFB));
        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFC));
    }

    [Test]
    public void ToolPropertyChange_ReemitsConfig()
    {
        _service.Start();
        _udp.ClearReceivedCalls();

        // Tool.IsSteerSwitchEnabled is the operator-console wiring flag
        // the lead called out specifically. Per spec, flipping it must
        // re-emit so the module sees the new switch-mode in PGN 251.
        ConfigurationStore.Instance.Tool.IsSteerSwitchEnabled =
            !ConfigurationStore.Instance.Tool.IsSteerSwitchEnabled;

        WaitForEmit();

        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFB));
        _udp.Received(1).SendToModules(Arg.Is<byte[]>(b => b.Length > 4 && b[3] == 0xFC));
    }

    [Test]
    public void PropertyChangeAfterStop_DoesNotEmit()
    {
        // Sanity guard: a property write that races past Stop() must
        // not cause a phantom emit. Unsubscribed handlers + the
        // _isEnabled gate inside EmitSteerConfigPgns both contribute.
        _service.Start();
        _service.Stop();
        _udp.ClearReceivedCalls();

        ConfigurationStore.Instance.AutoSteer.InvertWas = true;
        WaitForEmit();

        _udp.DidNotReceiveWithAnyArgs().SendToModules(default!);
    }

    /// <summary>
    /// Spin until either the debounce should have fired or we've
    /// waited well past it. Faster than a fixed sleep when the timer
    /// fires earlier; bounded so a stuck timer doesn't hang the suite.
    /// </summary>
    private void WaitForEmit()
    {
        // 200 ms is ~10× the configured debounce; tests fail fast if
        // the timer never fires.
        Thread.Sleep(200);
    }
}
