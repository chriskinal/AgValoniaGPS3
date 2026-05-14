// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Threading.Tasks;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Pins the M / N redesign of the steer-wizard calibration outputs:
///
/// M. The max-steering-angle measurement is stored as raw WAS counts
///    on <see cref="AutoSteerConfig"/> so it stays correct even if
///    the operator runs the CPD circle test before or after the
///    max-steer step. <see cref="AutoSteerConfig.MaxSteerAngle"/>
///    becomes a computed view of <c>MaxSteerRawCounts / CountsPerDegree</c>.
///
/// N. After the motor-cal Kp ramp captures MinPwm, the step also
///    seeds <see cref="AutoSteerConfig.MaxPwm"/> to
///    <c>min(2 × MinPwm, 255)</c> — a sensible default the operator
///    can tune in the AutoSteer config dialog.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class MaxSteerRawCountsAndMaxPwmTests
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

    private static SteerModuleData ModuleAt(double angleDeg, byte pwm) =>
        new SteerModuleData(
            ActualSteerAngle: angleDeg, ImuHeading: 0, ImuRoll: 0,
            WorkSwitchActive: false, SteerSwitchActive: true,
            RemoteButtonPressed: false, VwasFusionActive: false,
            PwmDisplay: pwm);

    // =====================================================================
    // Issue M — MaxSteerAngle is CPD-invariant when stored as raw counts.
    // =====================================================================

    [Test]
    public void MaxSteer_RawCountsInvariantUnderCpdChange()
    {
        // Operator measures at default CPD=100. Capture 35° -> 3500 raw counts.
        _store.AutoSteer.CountsPerDegree = 100;
        _store.AutoSteer.MaxSteerRawCounts = 3500;
        Assert.That(_store.AutoSteer.MaxSteerAngle, Is.EqualTo(35),
            "At CPD=100, 3500 raw counts must show as 35°");

        // CPD calibration drops it to 85. The stored measurement is the
        // same physical limit; only the degree view should adjust.
        _store.AutoSteer.CountsPerDegree = 85;
        Assert.That(_store.AutoSteer.MaxSteerAngle, Is.EqualTo(41),
            "At CPD=85, 3500 raw counts must show as ~41° (rounded from 41.18)");

        // CPD up to 120 in a different setup; same raw counts.
        _store.AutoSteer.CountsPerDegree = 120;
        Assert.That(_store.AutoSteer.MaxSteerAngle, Is.EqualTo(29),
            "At CPD=120, 3500 raw counts must show as ~29° (rounded from 29.17)");

        Assert.That(_store.AutoSteer.MaxSteerRawCounts, Is.EqualTo(3500),
            "Raw counts are the source of truth; CPD changes never touch them");
    }

    [Test]
    public void MaxSteer_SetByDegrees_BackConvertsToRawCounts()
    {
        // Legacy callers (JSON profile load, config dialog spinner) set
        // MaxSteerAngle in degrees. The setter must back-convert using
        // the current CPD so subsequent CPD changes still re-meaning the
        // value correctly.
        _store.AutoSteer.CountsPerDegree = 100;
        _store.AutoSteer.MaxSteerAngle = 45;
        Assert.That(_store.AutoSteer.MaxSteerRawCounts, Is.EqualTo(4500));

        _store.AutoSteer.CountsPerDegree = 90;
        // 4500 raw / 90 cpd = 50°.
        Assert.That(_store.AutoSteer.MaxSteerAngle, Is.EqualTo(50));
    }

    [Test]
    public async Task MaxSteer_CapturesPlateauAsRawCounts()
    {
        // The wizard step measures degrees from PGN 253 and persists
        // raw counts. Drive a simulated WAS that ramps to ±35° and
        // plateaus; verify the captured rawCounts equals
        // chosenDeg × currentCPD.
        _store.AutoSteer.CountsPerDegree = 80;
        var step = new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        step.DelayFunc = (_, _) => Task.CompletedTask;

        // Symmetric plateau at ±35°. min(35,35) * 0.9 = 31.5 -> rounds to 32.
        // rawCounts = 32 × 80 = 2520.
        double[] plateau = { 10, 20, 30, 35, 35, 35, 35, 35, 35, 35 };
        int call = 0;
        bool flipped = false;
        step.ReadWasAngle = () =>
        {
            // First pass: ramp up to +35. Switch to negative after.
            double v = plateau[System.Math.Min(call, plateau.Length - 1)];
            call++;
            if (call == plateau.Length && !flipped)
            {
                flipped = true;
                call = 0;
            }
            return flipped ? -v : v;
        };

        // Enter the step BEFORE running the measurement so OnEntering's
        // state reset doesn't trample the captured values; then run the
        // measurement; then leave so OnLeaving persists.
        var prop = typeof(AgValoniaGPS.ViewModels.Wizards.WizardStepViewModel)
            .GetProperty(nameof(AgValoniaGPS.ViewModels.Wizards.WizardStepViewModel.IsActive));
        prop!.SetValue(step, true);
        await step.RunMaxAngleMeasurementAsync();
        prop!.SetValue(step, false);

        // chosenDeg = min(35, 35) * 0.9 = 31.5 -> Math.Round -> 32 -> ×80 = 2560.
        Assert.That(_store.AutoSteer.MaxSteerRawCounts, Is.EqualTo(2520).Within(50),
            "Persisted raw counts must equal chosenDeg × currentCPD at capture time");
        Assert.That(step.CalibrationCompleted, Is.True);
    }

    // =====================================================================
    // Issue N — Motor cal seeds MaxPwm = min(2 × MinPwm, 255) at capture.
    // =====================================================================

    [Test]
    public async Task MotorCal_AfterCapture_SetsMaxPwmToDoubleMinPwm()
    {
        // Captured MinPwm = 35 from PGN 253 byte 7 at first motion.
        // Wizard must also write MaxPwm = 70 (2 × 35, well under 255).
        _store.AutoSteer.ProportionalGain = 10;
        var step = new AutoMotorCalibrationStepViewModel(_configService, _autoSteer);
        step.DelayFunc = (_, _) => Task.CompletedTask;
        step.ReadModuleData = () =>
        {
            int kp = _store.AutoSteer.ProportionalGain;
            // Below Kp=40, no motion. At Kp >= 40, the wheel has moved 2°
            // and the firmware reports PWM=35 — the threshold duty cycle.
            return kp < 40
                ? ModuleAt(0.0, (byte)System.Math.Min(kp, 30))
                : ModuleAt(2.0, 35);
        };

        await step.RunKpRampAsync();

        Assert.That(step.DetectedMinPwm, Is.EqualTo(35),
            "DetectedMinPwm must mirror PGN 253 PwmDisplay at the moment of motion");
        Assert.That(_store.AutoSteer.MinPwm, Is.EqualTo(35),
            "ConfigStore MinPwm is the captured firmware duty cycle");
        Assert.That(_store.AutoSteer.MaxPwm, Is.EqualTo(70),
            "MaxPwm seeded to 2 × MinPwm right after capture");
        Assert.That(step.CalibrationCompleted, Is.True);
    }

    [Test]
    public async Task MotorCal_AfterCapture_CapsMaxPwmAt255()
    {
        // Edge case: a high-friction setup pushes MinPwm into the
        // 130+ range where 2 × MinPwm would exceed 255. The default
        // must clamp at the byte ceiling.
        _store.AutoSteer.ProportionalGain = 10;
        var step = new AutoMotorCalibrationStepViewModel(_configService, _autoSteer);
        step.DelayFunc = (_, _) => Task.CompletedTask;
        step.ReadModuleData = () =>
        {
            int kp = _store.AutoSteer.ProportionalGain;
            return kp < 80
                ? ModuleAt(0.0, 0)
                : ModuleAt(2.0, 200);
        };

        await step.RunKpRampAsync();

        Assert.That(step.DetectedMinPwm, Is.EqualTo(200));
        Assert.That(_store.AutoSteer.MaxPwm, Is.EqualTo(255),
            "MaxPwm must clamp at 255 when 2 × MinPwm would overflow the byte");
    }
}
