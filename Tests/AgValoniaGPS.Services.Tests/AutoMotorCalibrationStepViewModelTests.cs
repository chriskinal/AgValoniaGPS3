// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Threading.Tasks;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Targeted regressions for the auto motor-calibration wizard step. The
/// broader <c>SteerWizardStepTests</c> file is excluded from compilation
/// while older wizard types are pending removal, so these assertions are
/// scoped tightly to the honest-labels bundle:
///
/// - <c>CurrentPwm</c> renamed to <c>TestAngleStep</c> (the loop iterator
///   that drives the angle ramp via <c>angle = step * 0.15</c>).
/// - <c>DetectedMinPwm</c> renamed to <c>DetectedMinAngle</c>.
/// - <c>ReportedModulePwm</c> tracks the firmware's PWM via PGN 253
///   <see cref="SteerModuleData.PwmDisplay"/>.
/// - <c>CapturedMinPwm</c> snapshots <c>ReportedModulePwm</c> at the
///   moment the ramp first detects movement, and is what OnLeaving
///   persists to <see cref="AutoSteerConfig.MinPwm"/>.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class AutoMotorCalibrationStepViewModelTests
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

    private AutoMotorCalibrationStepViewModel CreateStep(IAutoSteerService autoSteer)
    {
        var step = new AutoMotorCalibrationStepViewModel(_configService, autoSteer);
        // Force OnEntering via reflection (matches SteerWizardStepTests helper).
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        prop!.SetValue(step, true);
        // Stub DelayFunc so the ramp loop doesn't actually wait 200 ms per step.
        step.DelayFunc = (_, _) => Task.CompletedTask;
        return step;
    }

    [Test]
    public void ReportedModulePwm_UpdatesFromPgn253PwmDisplay()
    {
        // The wizard's live PWM readout is fed by the StateUpdated event,
        // not by polling. PGN 253 byte 7 (data-payload offset) lands in
        // SteerModuleData.PwmDisplay; the wizard must mirror it.
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false,
            PwmDisplay: 142));

        var step = CreateStep(autoSteerService);

        autoSteerService.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteerService,
            new VehicleStateSnapshot { FixQuality = 4 });

        Assert.That(step.ReportedModulePwm, Is.EqualTo(142),
            "ReportedModulePwm must mirror SteerModuleData.PwmDisplay");
    }

    [Test]
    public async Task RunPwmRamp_OnTrigger_CapturesReportedPwmIntoCapturedMinPwm()
    {
        // The ramp's "MinPwm" output used to be derived from the loop
        // iterator (an angle step), not from a real PWM. Now we snapshot
        // ReportedModulePwm at the moment movement is detected, so the
        // saved value is genuinely the duty cycle the firmware is driving.
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false,
            PwmDisplay: 77));

        var step = CreateStep(autoSteerService);
        // Pre-set the live mirror as if a PGN 253 had already arrived;
        // the ramp captures whatever's currently in ReportedModulePwm.
        step.ReportedModulePwm = 77;

        // First five samples report no movement; subsequent reads show >=10° deflection.
        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount <= 5 ? 0.0 : 12.0;
        };

        await step.RunPwmRampAsync();

        Assert.That(step.CapturedMinPwm, Is.EqualTo(77),
            "CapturedMinPwm must snapshot the live ReportedModulePwm at trigger");
    }

    [Test]
    public async Task OnLeaving_PersistsCapturedMinPwm_NotAngleStep()
    {
        // OnLeaving used to save DetectedMinPwm (the angle-step iterator
        // times 1.1) into AutoSteerConfig.MinPwm. That was nonsense — the
        // slot is a PWM. Verify the rewrite saves the captured firmware
        // PWM instead.
        var autoSteerService = Substitute.For<IAutoSteerService>();
        autoSteerService.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false,
            PwmDisplay: 91));

        var step = CreateStep(autoSteerService);
        step.ReportedModulePwm = 91;

        int callCount = 0;
        step.ReadWasAngle = () =>
        {
            callCount++;
            return callCount <= 5 ? 0.0 : 15.0;
        };
        await step.RunPwmRampAsync();

        // Mark complete and complete Phase B so OnLeaving saves results.
        // Drive max-angle synchronously by stubbing ReadWasAngle.
        callCount = 0;
        step.ReadWasAngle = () => callCount++ switch
        {
            0 => 30.0,
            1 => -30.0,
            _ => 0.0
        };
        await step.RunMaxAngleMeasurementAsync();

        // Force IsActive false to trigger OnLeaving.
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        prop!.SetValue(step, false);

        Assert.That(_store.AutoSteer.MinPwm, Is.EqualTo(91),
            "AutoSteerConfig.MinPwm must be set from CapturedMinPwm, not the angle step");
    }
}
