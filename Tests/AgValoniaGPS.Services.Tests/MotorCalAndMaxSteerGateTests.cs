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
/// Both calibration steps (Motor Direction + MinPWM, and Maximum
/// Steering Angle) carry the physical-switch safety gate. When the
/// operator has <c>Tool.IsSteerSwitchEnabled</c> set but the live
/// PGN 253 reports the physical switch is OFF, the Start button must
/// stay disabled and the operator must see a prompt. When the gate
/// flag is false, the gate is bypassed entirely.
///
/// One fixture exercises both VMs with parameterised type so future
/// gate-bearing steps can be added without duplicating the suite.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class MotorCalAndMaxSteerGateTests
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

    private void GivenSwitch(bool active)
    {
        _autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: active,
            RemoteButtonPressed: false, VwasFusionActive: false,
            PwmDisplay: 0));
    }

    private static void EnterStep(WizardStepViewModel step)
    {
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        prop!.SetValue(step, true);
    }

    private SwitchGatedWizardStep CreateStep<T>() where T : SwitchGatedWizardStep
    {
        if (typeof(T) == typeof(AutoMotorCalibrationStepViewModel))
            return new AutoMotorCalibrationStepViewModel(_configService, _autoSteer);
        if (typeof(T) == typeof(MaxSteeringAngleStepViewModel))
            return new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        throw new InvalidOperationException("Unknown step type for fixture");
    }

    [TestCase(typeof(AutoMotorCalibrationStepViewModel))]
    [TestCase(typeof(MaxSteeringAngleStepViewModel))]
    public void Gate_SwitchDisabled_AlwaysAllowsStart(Type stepType)
    {
        // No operator-console switch configured -> gate bypassed.
        _store.Tool.IsSteerSwitchEnabled = false;
        GivenSwitch(active: false);

        SwitchGatedWizardStep step = stepType == typeof(AutoMotorCalibrationStepViewModel)
            ? new AutoMotorCalibrationStepViewModel(_configService, _autoSteer)
            : new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        EnterStep(step);

        Assert.That(step.WaitingForPhysicalSwitch, Is.False);
        Assert.That(step.CanStartTest, Is.True);
        Assert.That(step.PhysicalSwitchPromptText, Is.Empty);
    }

    [TestCase(typeof(AutoMotorCalibrationStepViewModel))]
    [TestCase(typeof(MaxSteeringAngleStepViewModel))]
    public void Gate_SwitchEnabledAndPgn253SwitchOff_BlocksStart(Type stepType)
    {
        _store.Tool.IsSteerSwitchEnabled = true;
        GivenSwitch(active: false);

        SwitchGatedWizardStep step = stepType == typeof(AutoMotorCalibrationStepViewModel)
            ? new AutoMotorCalibrationStepViewModel(_configService, _autoSteer)
            : new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        EnterStep(step);

        Assert.That(step.WaitingForPhysicalSwitch, Is.True);
        Assert.That(step.CanStartTest, Is.False);
        Assert.That(step.PhysicalSwitchPromptText, Does.Contain("Steer Switch"));
    }

    [TestCase(typeof(AutoMotorCalibrationStepViewModel))]
    [TestCase(typeof(MaxSteeringAngleStepViewModel))]
    public void Gate_SwitchEnabledAndPgn253SwitchOn_AllowsStart(Type stepType)
    {
        _store.Tool.IsSteerSwitchEnabled = true;
        GivenSwitch(active: true);

        SwitchGatedWizardStep step = stepType == typeof(AutoMotorCalibrationStepViewModel)
            ? new AutoMotorCalibrationStepViewModel(_configService, _autoSteer)
            : new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        EnterStep(step);

        Assert.That(step.WaitingForPhysicalSwitch, Is.False);
        Assert.That(step.CanStartTest, Is.True);
    }

    [TestCase(typeof(AutoMotorCalibrationStepViewModel))]
    [TestCase(typeof(MaxSteeringAngleStepViewModel))]
    public void Gate_TransitionFromOffToOn_RefreshesImmediately(Type stepType)
    {
        // Operator enters with switch off, then flips it on at the
        // console. The live PGN 253 transition must unblock the Start
        // button immediately, not after a step navigation away/back.
        _store.Tool.IsSteerSwitchEnabled = true;
        GivenSwitch(active: false);

        SwitchGatedWizardStep step = stepType == typeof(AutoMotorCalibrationStepViewModel)
            ? new AutoMotorCalibrationStepViewModel(_configService, _autoSteer)
            : new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        EnterStep(step);
        Assert.That(step.CanStartTest, Is.False);

        // Flip switch on; fire StateUpdated like AutoSteerService would.
        GivenSwitch(active: true);
        _autoSteer.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            _autoSteer, new VehicleStateSnapshot());

        Assert.That(step.WaitingForPhysicalSwitch, Is.False);
        Assert.That(step.CanStartTest, Is.True);
    }

    [TestCase(typeof(AutoMotorCalibrationStepViewModel))]
    [TestCase(typeof(MaxSteeringAngleStepViewModel))]
    public void Gate_OperatorTogglesIsSteerSwitchEnabledLive_Refreshes(Type stepType)
    {
        // Symmetric path: operator flips the requirement on/off in the
        // config dialog while the step is on-screen. Tool's
        // PropertyChanged must wake the gate without needing a fresh
        // PGN 253.
        _store.Tool.IsSteerSwitchEnabled = false;
        GivenSwitch(active: false);

        SwitchGatedWizardStep step = stepType == typeof(AutoMotorCalibrationStepViewModel)
            ? new AutoMotorCalibrationStepViewModel(_configService, _autoSteer)
            : new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        EnterStep(step);
        Assert.That(step.WaitingForPhysicalSwitch, Is.False);

        _store.Tool.IsSteerSwitchEnabled = true;
        Assert.That(step.WaitingForPhysicalSwitch, Is.True,
            "Toggling IsSteerSwitchEnabled on must close the gate immediately");

        _store.Tool.IsSteerSwitchEnabled = false;
        Assert.That(step.WaitingForPhysicalSwitch, Is.False,
            "Toggling IsSteerSwitchEnabled off must reopen the gate");
    }
}
