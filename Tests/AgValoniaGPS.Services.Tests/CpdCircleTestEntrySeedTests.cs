// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins that the CPD circle-test step seeds its gate inputs from the
/// AutoSteerService cached <c>LatestSnapshot</c> on entry, so the
/// Record button reflects the current GPS state immediately rather
/// than staying greyed until the next <c>StateUpdated</c> publish.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class CpdCircleTestEntrySeedTests
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
        _autoSteer.LastSteerData.Returns(SteerModuleData.Empty);
    }

    private static void EnterStep(WizardStepViewModel step)
    {
        // IsActive setter is internal; flip via reflection so OnEntering fires.
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        prop!.SetValue(step, true);
    }

    [Test]
    public void OnEntering_WithCachedRtkFixedSnapshot_EnablesRecordImmediately()
    {
        // Service cache reports RTK Fixed + ~5 km/h *before* the step is
        // entered. Without the seed, the Record button stays disabled
        // until the next StateUpdated publish ticks the gate inputs.
        _autoSteer.LatestSnapshot.Returns(new VehicleStateSnapshot
        {
            FixQuality = 4,
            Speed = 5.0 / 3.6,  // m/s -> SpeedKmh = 5
        });

        var step = new CpdCircleTestStepViewModel(_configService, _autoSteer);
        EnterStep(step);

        Assert.That(step.IsRtkFixed, Is.True,
            "IsRtkFixed must be seeded from LatestSnapshot.FixQuality");
        Assert.That(step.Speed, Is.EqualTo(5.0).Within(0.01),
            "Speed must be seeded (SpeedKmh from LatestSnapshot)");
        Assert.That(step.CanRecord, Is.True,
            "Record gate (RTK + speed>0.5 + !IsRecording) must open immediately on entry");
    }

    [Test]
    public void OnEntering_WithoutCachedSnapshot_LeavesGateClosed()
    {
        // No cached snapshot — the service is freshly started. The gate
        // should stay closed; the next StateUpdated publishes will
        // populate the inputs.
        _autoSteer.LatestSnapshot.Returns((VehicleStateSnapshot?)null);

        var step = new CpdCircleTestStepViewModel(_configService, _autoSteer);
        EnterStep(step);

        Assert.That(step.IsRtkFixed, Is.False);
        Assert.That(step.Speed, Is.EqualTo(0.0));
        Assert.That(step.CanRecord, Is.False);
    }
}
