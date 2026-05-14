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
/// Pins the auto motor calibration step's physical-switch gate. The
/// host requires Tool.IsSteerSwitchEnabled + a live PGN 253 with
/// SteerSwitchActive bit set before motor tests can run; the wizard
/// has to reflect that, both so the operator can't fire the test
/// without the safety toggle and so the operator can see *why* the
/// Start button is greyed.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class AutoMotorCalibrationGateTests
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

    private static AutoMotorCalibrationStepViewModel CreateEnteredStep(
        IConfigurationService configService,
        IAutoSteerService? autoSteer)
    {
        var step = new AutoMotorCalibrationStepViewModel(configService, autoSteer);
        // Force OnEntering via the public IsActive setter (reflection
        // because the setter is internal).
        var prop = typeof(WizardStepViewModel).GetProperty(nameof(WizardStepViewModel.IsActive));
        prop!.SetValue(step, true);
        return step;
    }

    [Test]
    public void GateOpen_WhenSteerSwitchNotRequired()
    {
        // If the operator hasn't enabled the steer-switch requirement,
        // the gate is irrelevant and the Start button must be enabled
        // regardless of the physical switch state.
        _store.Tool.IsSteerSwitchEnabled = false;

        var autoSteer = Substitute.For<IAutoSteerService>();
        autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false, PwmDisplay: 0));

        var step = CreateEnteredStep(_configService, autoSteer);

        Assert.That(step.WaitingForPhysicalSwitch, Is.False);
        Assert.That(step.CanStartTest, Is.True);
        Assert.That(step.PhysicalSwitchPromptText, Is.Empty);
    }

    [Test]
    public void GateClosed_WhenSwitchRequiredButInactive()
    {
        // Operator wants the safety toggle (IsSteerSwitchEnabled = true)
        // but the live PGN 253 says the physical switch is OFF. The gate
        // must close so the operator can't fire the ramp.
        _store.Tool.IsSteerSwitchEnabled = true;

        var autoSteer = Substitute.For<IAutoSteerService>();
        autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false, PwmDisplay: 0));

        var step = CreateEnteredStep(_configService, autoSteer);

        Assert.That(step.WaitingForPhysicalSwitch, Is.True);
        Assert.That(step.CanStartTest, Is.False);
        Assert.That(step.PhysicalSwitchPromptText, Does.Contain("Steer Switch"));
    }

    [Test]
    public void GateOpens_WhenLastSteerDataSteerSwitchActiveFlipsTrue()
    {
        // The operator flips the physical switch ON. The wizard sees a
        // new PGN 253 via StateUpdated, re-evaluates the gate, and
        // unblocks the Start button. Matches lead spec
        // 'AutoMotorCalibrationStep_GateFollowsLastSteerDataSteerSwitchActive'.
        _store.Tool.IsSteerSwitchEnabled = true;

        var autoSteer = Substitute.For<IAutoSteerService>();
        var off = new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false, PwmDisplay: 0);
        autoSteer.LastSteerData.Returns(off);

        var step = CreateEnteredStep(_configService, autoSteer);
        Assert.That(step.WaitingForPhysicalSwitch, Is.True, "starts closed");

        // Flip the simulated PGN 253 to switch-ON, then raise the event
        // the way AutoSteerService would after a fresh packet.
        var on = new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: true,
            RemoteButtonPressed: false, VwasFusionActive: false, PwmDisplay: 0);
        autoSteer.LastSteerData.Returns(on);
        autoSteer.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteer, new VehicleStateSnapshot());

        Assert.That(step.WaitingForPhysicalSwitch, Is.False);
        Assert.That(step.CanStartTest, Is.True);
        Assert.That(step.PhysicalSwitchPromptText, Is.Empty);
    }

    [Test]
    public void GateReacts_WhenOperatorTogglesIsSteerSwitchEnabledLive()
    {
        // Operator flips Tool.IsSteerSwitchEnabled in the config dialog
        // while the wizard is on-screen. Without subscribing to Tool's
        // PropertyChanged, the wizard would only re-evaluate the gate
        // on the next PGN 253. Verify the immediate response.
        _store.Tool.IsSteerSwitchEnabled = false;

        var autoSteer = Substitute.For<IAutoSteerService>();
        autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: false,
            RemoteButtonPressed: false, VwasFusionActive: false, PwmDisplay: 0));

        var step = CreateEnteredStep(_configService, autoSteer);
        Assert.That(step.WaitingForPhysicalSwitch, Is.False);

        // Operator turns the requirement on; gate must close immediately.
        _store.Tool.IsSteerSwitchEnabled = true;
        Assert.That(step.WaitingForPhysicalSwitch, Is.True);

        // Operator turns it back off; gate reopens.
        _store.Tool.IsSteerSwitchEnabled = false;
        Assert.That(step.WaitingForPhysicalSwitch, Is.False);
    }
}
