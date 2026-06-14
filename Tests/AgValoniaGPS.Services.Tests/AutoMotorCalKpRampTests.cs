// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Threading;
using System.Threading.Tasks;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the redesigned Phase A behaviour of the auto motor calibration
/// step. Phase A now ramps <c>ConfigStore.AutoSteer.ProportionalGain</c>
/// at a fixed +5° setpoint and snapshots <c>PwmDisplay</c> from the
/// module at first motion. The original <c>ProportionalGain</c> must
/// always be restored on exit so the operator's tuning isn't lost.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class AutoMotorCalKpRampTests
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
    /// Build the step with a mocked module-data accessor. The closure
    /// captures the latest <c>ProportionalGain</c> written by the wizard
    /// so the simulated motion responds to the ramp, not just to time.
    /// </summary>
    private AutoMotorCalibrationStepViewModel CreateStep(System.Func<int, SteerModuleData> moduleFor)
    {
        var step = new AutoMotorCalibrationStepViewModel(_configService, new AgValoniaGPS.Services.Threading.InlineUiDispatcher(), _autoSteer);
        step.DelayFunc = (_, _) => Task.CompletedTask;
        step.ReadModuleData = () => moduleFor(_store.AutoSteer.ProportionalGain);
        return step;
    }

    private static SteerModuleData ModuleAt(double angleDeg, byte pwm) =>
        new SteerModuleData(
            ActualSteerAngle: angleDeg, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: true,
            RemoteButtonPressed: false, VwasFusionActive: false,
            PwmDisplay: pwm);

    [Test]
    public async Task MotorCal_FirstMotionAtKp40_CapturesPwmAtThatMoment()
    {
        // Simulator's PID is approximately pwm = error * Kp / 10 with a
        // MinPWM floor. We're not modelling that exactly here — what we
        // care about is the *capture* semantic: when motion first
        // appears, DetectedMinPwm must equal LastSteerData.PwmDisplay at
        // that moment, not a derived number. Below Kp=40 the simulator
        // reports the wheel still at start; at Kp>=40 it has moved 2°
        // and is driving a duty cycle of 25.
        _store.AutoSteer.ProportionalGain = 12; // operator's tuning
        var step = CreateStep(kp =>
        {
            if (kp < 40) return ModuleAt(angleDeg: 0.0, pwm: (byte)(kp / 2));
            return ModuleAt(angleDeg: 2.0, pwm: 25);
        });

        await step.RunKpRampAsync();

        Assert.That(step.DetectedMinPwm, Is.EqualTo(25),
            "MinPWM must be the firmware-reported PWM at the moment of first motion");
        Assert.That(step.DetectedInvertMotor, Is.False);
        Assert.That(step.NoMovementDetected, Is.False);
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));

        // OriginalKp restored on exit.
        Assert.That(_store.AutoSteer.ProportionalGain, Is.EqualTo(12));
    }

    [Test]
    public async Task MotorCal_WrongDirectionFirstTry_FlipsInvertAndSucceeds()
    {
        // First-pass simulator: motor turns the wrong way at Kp>=40.
        // Second pass (after the wizard flips InvertMotor): motor now
        // turns correctly at Kp>=40 with PWM=30.
        _store.AutoSteer.ProportionalGain = 8;
        _store.AutoSteer.InvertMotor = false;

        var step = CreateStep(kp =>
        {
            if (!_store.AutoSteer.InvertMotor)
            {
                if (kp < 40) return ModuleAt(0.0, 0);
                return ModuleAt(-2.0, 25);  // wrong direction
            }
            else
            {
                if (kp < 40) return ModuleAt(0.0, 0);
                return ModuleAt(+2.5, 30);  // correct direction after flip
            }
        });

        await step.RunKpRampAsync();

        Assert.That(_store.AutoSteer.InvertMotor, Is.True,
            "ConfigStore.AutoSteer.InvertMotor must be flipped after a wrong-direction first pass");
        Assert.That(step.DetectedInvertMotor, Is.True);
        Assert.That(step.DetectedMinPwm, Is.EqualTo(30));
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));
        Assert.That(step.NoMovementDetected, Is.False);

        // OriginalKp restored.
        Assert.That(_store.AutoSteer.ProportionalGain, Is.EqualTo(8));
    }

    [Test]
    public async Task MotorCal_NoMotionAtMaxKp_RaisesFailure()
    {
        // Module reports the wheel stationary at every Kp setting —
        // hardware fault, low MaxPWM, or unsupplied motor. Wizard must
        // mark the failure and restore the operator's Kp.
        _store.AutoSteer.ProportionalGain = 42;
        var step = CreateStep(_ => ModuleAt(0.0, 0));

        await step.RunKpRampAsync();

        Assert.That(step.NoMovementDetected, Is.True);
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));
        Assert.That(step.PhaseResult, Does.Contain("No wheel movement"));
        Assert.That(_store.AutoSteer.ProportionalGain, Is.EqualTo(42),
            "OriginalKp must survive a failed ramp");
    }

    [Test]
    public async Task MotorCal_OnCancel_RestoresOriginalKp()
    {
        // Operator hits cancel mid-ramp. The internal CTS is exposed
        // via the OnLeaving hook, but at the unit-test level we trigger
        // cancel by overriding DelayFunc to throw OperationCanceled on
        // the second delay invocation.
        _store.AutoSteer.ProportionalGain = 17;
        var step = new AutoMotorCalibrationStepViewModel(_configService, new AgValoniaGPS.Services.Threading.InlineUiDispatcher(), _autoSteer);
        step.ReadModuleData = () => ModuleAt(0.0, 0);

        int delayCalls = 0;
        step.DelayFunc = (_, _) =>
        {
            delayCalls++;
            if (delayCalls >= 3)
                throw new System.OperationCanceledException();
            return Task.CompletedTask;
        };

        await step.RunKpRampAsync();

        Assert.That(_store.AutoSteer.ProportionalGain, Is.EqualTo(17),
            "Cancellation must restore the operator's originalKp via the finally block");
    }

    [Test]
    public async Task MotorCal_DetectsRunaway_AbortsBeforeRampCompletes()
    {
        // Defensive: if the wheel races more than 15° from the start
        // anchor during the ramp (e.g. WAS misconfigured), the wizard
        // must abort early rather than keep ramping Kp.
        //
        // Simulate: at the very first sample the start anchor reads 0°.
        // Once the ramp engages (a Kp has been written), the wheel has
        // shot to 20° — well past the 15° runaway limit.
        _store.AutoSteer.ProportionalGain = 0;
        var step = CreateStep(kp =>
        {
            if (kp >= 5) return ModuleAt(angleDeg: 20.0, pwm: 80);
            return ModuleAt(0.0, 0);
        });

        await step.RunKpRampAsync();

        Assert.That(step.NoMovementDetected, Is.True);
        Assert.That(step.PhaseResult, Does.Contain("Runaway"));
        Assert.That(step.Phase, Is.EqualTo(CalibrationPhase.RampResult));
        Assert.That(_store.AutoSteer.ProportionalGain, Is.EqualTo(0),
            "Runaway abort still restores originalKp");
    }
}
