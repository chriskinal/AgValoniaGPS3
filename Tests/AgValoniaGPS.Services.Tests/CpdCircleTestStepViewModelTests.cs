// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Targeted regressions for the wizard's CPD circle-test step. The
/// broader <c>SteerWizardStepTests</c> file is currently excluded from
/// compilation while a handful of older wizard step types are pending
/// removal, so the assertions here are scoped tightly to behavior the
/// step-9/10 polish bundle changed:
///
/// - RTK gate requires <c>FixQuality == 4</c> (strict RTK Fixed; Float
///   is excluded because it still drifts at the centimeter scale and
///   would skew the measured circle diameter).
/// - <see cref="CpdCircleTestStepViewModel.FixQualityLabel"/> maps raw
///   NMEA GGA values to human text so the operator can see *why* the
///   gate is closed (e.g. "RTK Float" vs. "No Fix").
/// - <see cref="CpdCircleTestStepViewModel.IsAtRecommendedSpeed"/>
///   tracks a 3..7 km/h window around the documented ~5 km/h target.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class CpdCircleTestStepViewModelTests
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

    private CpdCircleTestStepViewModel CreateStep(IAutoSteerService? autoSteer = null)
    {
        var step = new CpdCircleTestStepViewModel(_configService, autoSteer);
        // Force OnEntering via reflection so the StateUpdated subscription
        // wires up. Mirrors the helper inside SteerWizardStepTests.cs.
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        prop!.SetValue(step, true);
        return step;
    }

    [Test]
    public void IsRtkFixed_RequiresFixQualityExactlyFour()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(SteerModuleData.Empty);
        var step = CreateStep(autoSteerService);

        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 1.5 });
        Assert.That(step.IsRtkFixed, Is.True, "FixQuality 4 must enable recording");
        Assert.That(step.FixQuality, Is.EqualTo(4));

        // RTK Float should NOT pass the gate even though it used to under
        // the loose 'FixQuality >= 4' check.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 5, Speed = 1.5 });
        Assert.That(step.IsRtkFixed, Is.False, "RTK Float must NOT pass the strict gate");
        Assert.That(step.FixQuality, Is.EqualTo(5));

        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 2, Speed = 1.5 });
        Assert.That(step.IsRtkFixed, Is.False);
    }

    [Test]
    public void FixQualityLabel_MapsNmeaCodesToHumanText()
    {
        var step = new CpdCircleTestStepViewModel(_configService);

        step.FixQuality = 0;
        Assert.That(step.FixQualityLabel, Is.EqualTo("No Fix"));

        step.FixQuality = 4;
        Assert.That(step.FixQualityLabel, Is.EqualTo("RTK Fixed"));

        step.FixQuality = 5;
        Assert.That(step.FixQualityLabel, Is.EqualTo("RTK Float"));

        step.FixQuality = 99;
        Assert.That(step.FixQualityLabel, Does.StartWith("Unknown"));
    }

    [Test]
    public void IsAtRecommendedSpeed_TracksFiveKmhWindow()
    {
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(SteerModuleData.Empty);
        var step = CreateStep(autoSteerService);

        // The snapshot's Speed field is m/s; the wizard converts via SpeedKmh.
        // 2 km/h: below the 3..7 window.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 2.0 / 3.6 });
        Assert.That(step.IsAtRecommendedSpeed, Is.False, "2 km/h is below the window");

        // 5 km/h: bullseye.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 5.0 / 3.6 });
        Assert.That(step.IsAtRecommendedSpeed, Is.True, "5 km/h is in the window");

        // 10 km/h: above the window.
        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4, Speed = 10.0 / 3.6 });
        Assert.That(step.IsAtRecommendedSpeed, Is.False, "10 km/h is above the window");
    }
}
