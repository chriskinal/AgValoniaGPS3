// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Windows.Input;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the Zero-Roll button semantics in the wizard's roll calibration
/// step. <c>LiveRoll</c> is the post-calibration angle published by the
/// NMEA parser (raw is already inverted if needed and the existing
/// <c>RollZero</c> has already been subtracted). A second Zero-Roll
/// press at a non-zero tilt must accumulate against the prior offset,
/// not replace it — same shape as the WAS bug from PR #385 F/G.
///
/// Unlike WAS, the invert sign is already baked into LiveRoll upstream,
/// so the Zero formula is plain addition rather than the WAS sign-aware
/// accumulator.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class RollCalibrationStepViewModelTests
{
    private IConfigurationService _configService = null!;
    private ConfigurationStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new ConfigurationStore();
        ConfigurationStore.SetInstance(_store);

        _configService = Substitute.For<IConfigurationService>();
        _configService.Store.Returns(_store);
    }

    [Test]
    public void ZeroRoll_AppliedTwiceAtDifferentTilts_LandsAtZero()
    {
        // Press 1: raw=3°, IsRollInvert=false, RollZero=0.
        // LiveRoll reads (3 - 0) = 3°. Press should set RollZero=3, so
        // next LiveRoll would read (3 - 3) = 0°.
        _store.Ahrs.IsRollInvert = false;
        _store.Ahrs.RollZero = 0;
        var step = new RollCalibrationStepViewModel(_configService);
        step.LiveRoll = 3.0;

        ((ICommand)step.ZeroRollCommand).Execute(null);

        Assert.That(step.RollZero, Is.EqualTo(3.0));

        // Simulate the host applying the new RollZero (live propagation
        // writes RollZero into ConfigStore.Ahrs immediately in the
        // production code path) and the operator tilting another 2°:
        // raw=5°, parser publishes LiveRoll = (5 - 3) = 2°.
        _store.Ahrs.RollZero = step.RollZero;
        step.LiveRoll = 2.0;

        ((ICommand)step.ZeroRollCommand).Execute(null);

        // Accumulator: 3 + 2 = 5. The naive replacement would have left
        // 2 here and the operator would still see a 3° drift on screen.
        Assert.That(step.RollZero, Is.EqualTo(5.0),
            "Second Zero-Roll press must accumulate against the prior offset");
    }

    [Test]
    public void ZeroRoll_WithInvertOn_AppliedTwice_LandsAtZero()
    {
        // Confirms the invert sign is baked into LiveRoll upstream:
        // ZeroRoll itself doesn't need a sign flip; it adds whatever
        // LiveRoll says, and the parser's invert handling keeps the
        // arithmetic correct.
        _store.Ahrs.IsRollInvert = true;
        _store.Ahrs.RollZero = 0;
        var step = new RollCalibrationStepViewModel(_configService);
        // Raw=-3° with invert -> live = (-(-3)) - 0 = 3°.
        step.LiveRoll = 3.0;

        ((ICommand)step.ZeroRollCommand).Execute(null);
        Assert.That(step.RollZero, Is.EqualTo(3.0));

        // Operator tilts further; parser publishes 2°.
        _store.Ahrs.RollZero = step.RollZero;
        step.LiveRoll = 2.0;

        ((ICommand)step.ZeroRollCommand).Execute(null);

        Assert.That(step.RollZero, Is.EqualTo(5.0),
            "Invert is handled upstream — accumulator must still be plain addition");
    }

    [Test]
    public void ZeroRoll_OnLevelGround_KeepsOffsetStable()
    {
        // Edge case: pressing Zero when already level (LiveRoll == 0)
        // must preserve the existing calibration rather than wipe it.
        _store.Ahrs.IsRollInvert = false;
        _store.Ahrs.RollZero = 1.5;
        var step = new RollCalibrationStepViewModel(_configService);
        step.RollZero = 1.5;
        step.LiveRoll = 0.0;

        ((ICommand)step.ZeroRollCommand).Execute(null);

        Assert.That(step.RollZero, Is.EqualTo(1.5),
            "Zero-Roll at level must preserve the existing offset, not zero it");
    }
}
