// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Windows.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the Zero-WAS button semantics in the wizard's WAS calibration
/// step. <c>ActualSteerAngle</c> from PGN 253 is the *post-calibration*
/// angle (raw counts have already had <c>WasOffset</c> subtracted and
/// <c>InvertWas</c> applied), so a second Zero-WAS press must
/// accumulate against the prior offset rather than replace it.
///
/// Pre-fix bugs (bench-test on top of #381):
/// - F: <c>ZeroWas</c> wrote <c>(actualAngle * cpd)</c>, throwing away
///   the prior calibration.
/// - G: it ignored <c>InvertWas</c> entirely, so a second press in
///   inverted mode drove the offset the wrong direction.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class WasCalibrationStepViewModelTests
{
    private IConfigurationService _configService = null!;
    private ConfigurationStore _store = null!;
    private IAutoSteerService _autoSteer = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new ConfigurationStore();
        ConfigurationStore.SetInstance(_store);

        _configService = Substitute.For<IConfigurationService>();
        _configService.Store.Returns(_store);

        _autoSteer = Substitute.For<IAutoSteerService>();
    }

    /// <summary>
    /// Stub <c>LastSteerData</c> to return a fresh
    /// <see cref="SteerModuleData"/> with the given post-calibration
    /// angle. <c>NSubstitute.Returns</c> requires us to redo this every
    /// time the simulated angle changes between presses.
    /// </summary>
    private void GivenReportedAngle(double angleDeg)
    {
        _autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: angleDeg, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false,
            PwmDisplay: 0));
    }

    [Test]
    public void ZeroWas_AppliedTwiceAtDifferentAngles_LandsAtZero()
    {
        // Setup: no prior calibration, CPD=100, wheel at 5°.
        _store.AutoSteer.CountsPerDegree = 100;
        _store.AutoSteer.WasOffset = 0;
        _store.AutoSteer.InvertWas = false;
        GivenReportedAngle(5.0);

        var step = new WasCalibrationStepViewModel(_configService, _autoSteer);
        ((ICommand)step.ZeroWasCommand).Execute(null);

        // Press 1: offset = 0 + 1 * 5 * 100 = 500.
        Assert.That(step.WasOffset, Is.EqualTo(500),
            "First Zero-WAS press should drive offset to raw-counts equivalent of the live angle");

        // Simulate the host pushing the new offset to the module and the
        // operator moving the wheel a further 5°. The module's next
        // PGN 253 reports the new post-calibration angle of 5°.
        _store.AutoSteer.WasOffset = step.WasOffset;
        GivenReportedAngle(5.0);

        ((ICommand)step.ZeroWasCommand).Execute(null);

        // Press 2: offset = 500 + 1 * 5 * 100 = 1000.
        Assert.That(step.WasOffset, Is.EqualTo(1000),
            "Second Zero-WAS press must accumulate against the prior offset, not replace it");
    }

    [Test]
    public void ZeroWas_WithInvertOn_AppliedTwice_LandsAtZero()
    {
        // With InvertWas=true the module reports negated angles. The
        // accumulator must subtract instead of add so the second press
        // still drives the displayed value to zero.
        _store.AutoSteer.CountsPerDegree = 100;
        _store.AutoSteer.WasOffset = 0;
        _store.AutoSteer.InvertWas = true;
        GivenReportedAngle(-5.0);

        var step = new WasCalibrationStepViewModel(_configService, _autoSteer);
        ((ICommand)step.ZeroWasCommand).Execute(null);

        // Press 1: offset = 0 + (-1) * (-5 * 100) = 500.
        Assert.That(step.WasOffset, Is.EqualTo(500));

        // Module now reports -5° again (wheel moved 5° further in the
        // physical direction the inversion treats as negative display).
        _store.AutoSteer.WasOffset = step.WasOffset;
        GivenReportedAngle(-5.0);

        ((ICommand)step.ZeroWasCommand).Execute(null);

        // Press 2: offset = 500 + (-1) * (-5 * 100) = 1000.
        Assert.That(step.WasOffset, Is.EqualTo(1000),
            "Inverted accumulator must move the offset in the same monotonic " +
            "direction so the live reading lands at zero after each press");
    }

    [Test]
    public void ZeroWas_NoInvert_NoMotion_KeepsOffsetStable()
    {
        // Edge case: if the operator is already at the zero angle (display
        // = 0°), Zero-WAS must be a no-op. Past behaviour overwrote the
        // offset to zero in this case, which would *undo* any prior
        // calibration the operator was happy with.
        _store.AutoSteer.CountsPerDegree = 100;
        _store.AutoSteer.WasOffset = 700;
        _store.AutoSteer.InvertWas = false;
        GivenReportedAngle(0.0);

        var step = new WasCalibrationStepViewModel(_configService, _autoSteer);
        ((ICommand)step.ZeroWasCommand).Execute(null);

        Assert.That(step.WasOffset, Is.EqualTo(700),
            "Zero-WAS at zero angle must preserve the existing offset");
    }
}
