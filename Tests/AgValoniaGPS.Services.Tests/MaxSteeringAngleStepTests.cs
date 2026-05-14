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
/// Pins the new max-steering-angle wizard step. The split-out step
/// must:
///   1. Detect a WAS plateau (consecutive samples within
///      PlateauThresholdDeg) and capture that angle, rather than read
///      a fixed-time snapshot mid-motion.
///   2. Capture both sides of the lock and persist
///      <c>min(left, right) * 0.9</c> as MaxSteerAngle on completion.
///   3. Honour cooperative cancellation by exiting the loop and
///      restoring free-drive state.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class MaxSteeringAngleStepTests
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

    [Test]
    public async Task PlateauDetector_FiresOnStableSamples_AndPersistsMaxAngle()
    {
        // Simulator-style WAS: ramp up to ±35° then plateau. The
        // detector should fire once it sees PlateauStableSamples
        // consecutive samples within PlateauThresholdDeg of each other.
        var step = new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        step.DelayFunc = (_, _) => Task.CompletedTask;

        // Right-lock sequence: 10, 20, 30, 35, 35, 35, 35, 35, 35, 35
        // then center, then left: -10, -25, -30, -30, -30, -30, -30, -30
        var rightSequence = new[] { 10.0, 20.0, 30.0, 35.0, 35.0, 35.0, 35.0, 35.0, 35.0, 35.0 };
        var leftSequence = new[] { -10.0, -25.0, -30.0, -30.0, -30.0, -30.0, -30.0, -30.0 };

        int call = 0;
        bool measuringLeft = false;
        step.ReadWasAngle = () =>
        {
            // Transition to "left" sequence the first time we hit
            // Phase.MeasuringLeft. (See the SetFreeDriveAngle stub below.)
            if (measuringLeft)
            {
                var v = leftSequence[System.Math.Min(call - rightSequence.Length, leftSequence.Length - 1)];
                call++;
                return v;
            }
            else
            {
                var v = rightSequence[System.Math.Min(call, rightSequence.Length - 1)];
                call++;
                if (call >= rightSequence.Length)
                {
                    // Reset call counter for the left-side polling.
                    measuringLeft = true;
                    call = rightSequence.Length;
                }
                return v;
            }
        };

        await step.RunMaxAngleMeasurementAsync();

        Assert.That(step.DetectedMaxAngleRight, Is.EqualTo(35.0).Within(0.5));
        Assert.That(step.DetectedMaxAngleLeft, Is.EqualTo(30.0).Within(0.5));
        // min(35, 30) * 0.9 = 27 (truncated).
        Assert.That(step.MaxSteerAngle, Is.EqualTo(27));
        Assert.That(step.CalibrationCompleted, Is.True);
        Assert.That(step.Phase, Is.EqualTo(MaxSteeringAnglePhase.Complete));
    }

    [Test]
    public async Task NoPlateau_FallsBackToLastSample_WithoutHanging()
    {
        // If the WAS never settles (e.g. simulator with no clamp, runaway
        // hydraulics), the detector must time out and return the last
        // sample rather than hang forever. The wizard caps the poll loop
        // at PlateauTimeoutMs internally.
        var step = new MaxSteeringAngleStepViewModel(_configService, _autoSteer);
        step.DelayFunc = (_, _) => Task.CompletedTask;

        double angle = 0;
        bool right = true;
        step.ReadWasAngle = () =>
        {
            angle += right ? 0.5 : -0.5;  // never plateaus, just drifts
            if (System.Math.Abs(angle) > 50)
            {
                right = !right;
                angle = right ? 0 : 0;
            }
            return angle;
        };

        await step.RunMaxAngleMeasurementAsync();

        // We don't assert exact values — the timeout-fallback path is
        // operational, not metric. The point: it returned without
        // hanging, and CalibrationCompleted is true so OnLeaving will
        // still persist *something*.
        Assert.That(step.Phase, Is.EqualTo(MaxSteeringAnglePhase.Complete));
        Assert.That(step.CalibrationCompleted, Is.True);
    }
}
