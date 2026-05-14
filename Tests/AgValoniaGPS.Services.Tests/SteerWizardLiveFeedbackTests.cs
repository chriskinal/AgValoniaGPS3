// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using CommunityToolkit.Mvvm.Input;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the live-feedback wiring added across the Roll / WAS / Motor
/// calibration wizard steps. The operator's perception bug was "I move
/// the slider but the gauge doesn't change": pre-fix, the steps only
/// wrote their settings into ConfigurationStore on OnLeaving, so the
/// downstream NMEA roll post-process and the PGN 251/252 emit cycle
/// didn't see the operator's changes until the next step was visited.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class SteerWizardLiveFeedbackTests
{
    private IConfigurationService _configService = null!;

    [SetUp]
    public void SetUp()
    {
        // Each test starts with a fresh store so the assertions about
        // "store flipped to X" are independent of any prior test's
        // leftover state.
        ConfigurationStore.SetInstance(new ConfigurationStore());
        _configService = Substitute.For<IConfigurationService>();
        _configService.Store.Returns(ConfigurationStore.Instance);
    }

    // ── Area 2: Roll calibration step pushes to store live ─────────────

    [Test]
    public void RollStep_SettingIsRollInvert_WritesToStoreImmediately()
    {
        var step = new RollCalibrationStepViewModel(_configService);
        Assume.That(_configService.Store.Ahrs.IsRollInvert, Is.False);

        step.IsRollInvert = true;

        Assert.That(_configService.Store.Ahrs.IsRollInvert, Is.True,
            "IsRollInvert setter must push to ConfigStore immediately so "
            + "NmeaParserServiceFast's roll post-process picks up the new "
            + "value on the next parse — not at OnLeaving.");
    }

    [Test]
    public void RollStep_SettingRollZero_WritesToStoreImmediately()
    {
        var step = new RollCalibrationStepViewModel(_configService);
        Assume.That(_configService.Store.Ahrs.RollZero, Is.EqualTo(0));

        step.RollZero = 2.5;

        Assert.That(_configService.Store.Ahrs.RollZero, Is.EqualTo(2.5),
            "RollZero setter must push to ConfigStore so the next NMEA "
            + "parse subtracts it from raw roll.");
    }

    [Test]
    public void RollStep_ZeroRollCommand_PushesLiveRollIntoStore()
    {
        // ZeroRollCommand captures LiveRoll into the setter. Combined with
        // the live-push, the operator's tap on Zero Roll lands in the
        // store within the same cycle.
        var step = new RollCalibrationStepViewModel(_configService);
        // Seed a live reading from the IMU feed (simulate a snapshot).
        InvokeNonPublic(step, "OnStateUpdated",
            new object?[] { this, BuildSnapshot(roll: 3.2) });

        ((IRelayCommand)step.ZeroRollCommand).Execute(null);

        Assert.That(step.RollZero, Is.EqualTo(3.2).Within(1e-6));
        Assert.That(_configService.Store.Ahrs.RollZero, Is.EqualTo(3.2).Within(1e-6),
            "Zero Roll button captures LiveRoll into the RollZero setter, "
            + "which forwards to the store — the gauge centres on the next "
            + "tick rather than waiting for the wizard step to be left.");
    }

    // ── Area 3: WAS calibration step pushes to store live ──────────────

    [Test]
    public void WasStep_SettingInvertWas_WritesToStoreImmediately()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        Assume.That(_configService.Store.AutoSteer.InvertWas, Is.False);

        step.InvertWas = true;

        Assert.That(_configService.Store.AutoSteer.InvertWas, Is.True,
            "InvertWas setter must push to ConfigStore so the next PGN 251 "
            + "the host emits carries the new flag — not at OnLeaving.");
    }

    [Test]
    public void WasStep_SettingWasOffset_WritesToStoreImmediately()
    {
        var step = new WasCalibrationStepViewModel(_configService);
        Assume.That(_configService.Store.AutoSteer.WasOffset, Is.EqualTo(0));

        step.WasOffset = 42;

        Assert.That(_configService.Store.AutoSteer.WasOffset, Is.EqualTo(42),
            "WasOffset setter must push to ConfigStore so PGN 252 carries "
            + "the new offset — Zero WAS button picks the angle * CPD and "
            + "the module starts subtracting it from raw counts.");
    }

    // ── Area 4: Motor calibration gates on physical switch engagement ──

    [Test]
    public void MotorCalStep_NoPhysicalSwitchConfigured_StartsImmediately()
    {
        _configService.Store.Tool.IsSteerSwitchEnabled = false;
        var autoSteer = Substitute.For<IAutoSteerService>();
        autoSteer.LastSteerData.Returns(default(SteerModuleData));
        var step = new AutoMotorCalibrationStepViewModel(_configService, autoSteer);

        InvokeNonPublic(step, "OnEntering", null);

        Assert.That(step.WaitingForPhysicalSwitch, Is.False,
            "Without a physical AutoSteer switch configured, the wizard "
            + "must not gate on engagement — engagement is managed by the "
            + "host's AutoSteer button alone.");
        Assert.That(step.CanStartTest, Is.True);
    }

    [Test]
    public void MotorCalStep_PhysicalSwitchConfiguredButOff_BlocksCalibration()
    {
        _configService.Store.Tool.IsSteerSwitchEnabled = true;
        var autoSteer = Substitute.For<IAutoSteerService>();
        // Module reports SteerSwitchActive=false → physical switch is OFF.
        autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0,
            ImuHeading: 0,
            ImuRoll: 0,
            WorkSwitchActive: false,
            SteerSwitchActive: false,
            RemoteButtonPressed: false,
            VwasFusionActive: false,
            PwmDisplay: 0));
        var step = new AutoMotorCalibrationStepViewModel(_configService, autoSteer);

        InvokeNonPublic(step, "OnEntering", null);

        Assert.That(step.WaitingForPhysicalSwitch, Is.True,
            "Physical switch is configured but module reports it OFF: "
            + "calibration must wait so the operator-flips-switch prompt "
            + "is visible and StartTestCommand's CanExecute is false.");
        Assert.That(step.CanStartTest, Is.False);
        Assert.That(step.PhysicalSwitchPromptText,
            Does.Contain("AutoSteer switch"));
    }

    [Test]
    public void MotorCalStep_PhysicalSwitchFlipsOn_UnblocksCalibration()
    {
        _configService.Store.Tool.IsSteerSwitchEnabled = true;
        var autoSteer = Substitute.For<IAutoSteerService>();
        autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0,
            ImuHeading: 0,
            ImuRoll: 0,
            WorkSwitchActive: false,
            SteerSwitchActive: false,
            RemoteButtonPressed: false,
            VwasFusionActive: false,
            PwmDisplay: 0));
        var step = new AutoMotorCalibrationStepViewModel(_configService, autoSteer);

        InvokeNonPublic(step, "OnEntering", null);
        Assume.That(step.WaitingForPhysicalSwitch, Is.True);

        // Operator flips the physical switch — module's next PGN 253
        // reports SteerSwitchActive=true.
        autoSteer.LastSteerData.Returns(new SteerModuleData(
            ActualSteerAngle: 0,
            ImuHeading: 0,
            ImuRoll: 0,
            WorkSwitchActive: false,
            SteerSwitchActive: true,
            RemoteButtonPressed: false,
            VwasFusionActive: false,
            PwmDisplay: 0));
        // VehicleStateSnapshot is a plain struct (not EventArgs), so
        // EventWith<T> won't accept it — use the generic-delegate form
        // and invoke with positional args.
        autoSteer.StateUpdated += Raise.Event<EventHandler<VehicleStateSnapshot>>(
            autoSteer, BuildSnapshot(isAutoSteerEngaged: true));

        Assert.That(step.WaitingForPhysicalSwitch, Is.False,
            "After the module's PGN 253 reports SteerSwitchActive=true, "
            + "the gate clears and calibration can start.");
        Assert.That(step.CanStartTest, Is.True);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static VehicleStateSnapshot BuildSnapshot(
        double roll = 0, bool isAutoSteerEngaged = false) => new()
    {
        Roll = roll,
        IsAutoSteerEngaged = isAutoSteerEngaged,
    };

    private static void InvokeNonPublic(object target, string name, object?[]? args)
    {
        var m = target.GetType().GetMethod(name,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        m!.Invoke(target, args);
    }
}
