// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Phase progression for the max-steering-angle measurement step.
/// </summary>
public enum MaxSteeringAnglePhase
{
    /// <summary>Waiting for the operator to start the test.</summary>
    WaitingToStart,

    /// <summary>Driving the wheels to full right lock and reading the plateau.</summary>
    MeasuringRight,

    /// <summary>Driving the wheels to full left lock and reading the plateau.</summary>
    MeasuringLeft,

    /// <summary>Final results captured, awaiting operator confirmation.</summary>
    Complete
}

/// <summary>
/// Drives the steering to full lock both directions and captures the
/// natural plateau angle from the WAS feedback. Split out from the
/// motor-direction step so the operator gets a distinct page for the
/// stress-style test — full lock pulses the hydraulics harder than
/// the small Phase A discovery sweep and deserves its own warnings.
///
/// The plateau detector watches WAS deltas between samples; once the
/// magnitude of change drops below <see cref="PlateauThresholdDeg"/>
/// for <see cref="PlateauStableSamples"/> consecutive samples, we
/// snapshot the angle. This works with both real-world steering
/// (which plateaus on hydraulic stops) and the simulator (which clamps
/// the simulated wheel angle at a configurable max — see Issue L).
/// </summary>
public class MaxSteeringAngleStepViewModel : SwitchGatedWizardStep
{
    private const double CommandedFullLockDeg = 60.0;
    private const double PlateauThresholdDeg = 0.2;
    private const int PlateauStableSamples = 5;
    private const int PollIntervalMs = 100;
    private const int PlateauTimeoutMs = 6000;
    private const int CenterReturnSettleMs = 800;

    private HardwareInstalledStepViewModel? _hardwareStep;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Injectable delay function for testing.
    /// </summary>
    internal Func<int, CancellationToken, Task> DelayFunc { get; set; } = Task.Delay;

    /// <summary>
    /// Injectable WAS reader. Production reads from
    /// <see cref="IAutoSteerService.LastSteerData"/>.
    /// </summary>
    internal Func<double>? ReadWasAngle { get; set; }

    public override string Title => "Maximum Steering Angle";

    public override string Description =>
        "Find the physical limits of the steering by briefly driving the " +
        "wheels to full lock in both directions. If your vehicle has " +
        "power steering that resists stationary turning, do this on level " +
        "ground at idle. Keep hands clear of the steering wheel.";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    public MaxSteeringAngleStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
        : base(configService, autoSteerService)
    {
        StartTestCommand = new AsyncRelayCommand(RunMaxAngleMeasurementAsync);
        RedoCommand = new AsyncRelayCommand(Redo);
    }

    private MaxSteeringAnglePhase _phase = MaxSteeringAnglePhase.WaitingToStart;
    public MaxSteeringAnglePhase Phase
    {
        get => _phase;
        set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(IsWaiting));
                OnPropertyChanged(nameof(IsMeasuring));
                OnPropertyChanged(nameof(IsComplete));
                OnPropertyChanged(nameof(PhaseDescription));
            }
        }
    }

    public bool IsWaiting => Phase == MaxSteeringAnglePhase.WaitingToStart;
    public bool IsMeasuring => Phase == MaxSteeringAnglePhase.MeasuringRight
                            || Phase == MaxSteeringAnglePhase.MeasuringLeft;
    public bool IsComplete => Phase == MaxSteeringAnglePhase.Complete;

    public string PhaseDescription => Phase switch
    {
        MaxSteeringAnglePhase.WaitingToStart =>
            "When you press Start, the wheels will turn fully right then " +
            "fully left. Each direction holds for a moment so the angle " +
            "can settle, then we capture the steady-state reading.",
        MaxSteeringAnglePhase.MeasuringRight =>
            "Measuring right lock... Keep hands clear.",
        MaxSteeringAnglePhase.MeasuringLeft =>
            "Measuring left lock... Keep hands clear.",
        MaxSteeringAnglePhase.Complete =>
            "Maximum steering angle captured.",
        _ => ""
    };

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private string _phaseResult = "";
    public string PhaseResult
    {
        get => _phaseResult;
        set => SetProperty(ref _phaseResult, value);
    }

    private double _detectedMaxAngleRight;
    public double DetectedMaxAngleRight
    {
        get => _detectedMaxAngleRight;
        set => SetProperty(ref _detectedMaxAngleRight, value);
    }

    private double _detectedMaxAngleLeft;
    public double DetectedMaxAngleLeft
    {
        get => _detectedMaxAngleLeft;
        set => SetProperty(ref _detectedMaxAngleLeft, value);
    }

    private int _maxSteerAngle;
    /// <summary>
    /// Final max steer angle as a raw WAS value. Conservative:
    /// <c>min(left, right) * 0.9</c>.
    /// </summary>
    public int MaxSteerAngle
    {
        get => _maxSteerAngle;
        set => SetProperty(ref _maxSteerAngle, value);
    }

    private bool _calibrationCompleted;
    public bool CalibrationCompleted
    {
        get => _calibrationCompleted;
        set => SetProperty(ref _calibrationCompleted, value);
    }

    private double _liveSteerAngle;
    public double LiveSteerAngle
    {
        get => _liveSteerAngle;
        set => SetProperty(ref _liveSteerAngle, value);
    }

    public ICommand StartTestCommand { get; }
    public ICommand RedoCommand { get; }

    /// <summary>
    /// Run a full-lock pulse in both directions, capturing each side's
    /// natural plateau. Restores center and disables free-drive on
    /// every exit path.
    /// </summary>
    internal async Task RunMaxAngleMeasurementAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        AutoSteerService?.EnableFreeDrive();
        Progress = 0;

        try
        {
            Phase = MaxSteeringAnglePhase.MeasuringRight;
            DetectedMaxAngleRight = Math.Abs(await DriveToPlateauAsync(+CommandedFullLockDeg, token));
            Progress = 0.5;

            // Return through center with a short settle so the next
            // direction starts from a stable anchor.
            AutoSteerService?.SetFreeDriveAngle(0);
            await DelayFunc(CenterReturnSettleMs, token);

            Phase = MaxSteeringAnglePhase.MeasuringLeft;
            DetectedMaxAngleLeft = Math.Abs(await DriveToPlateauAsync(-CommandedFullLockDeg, token));
            Progress = 1.0;

            AutoSteerService?.SetFreeDriveAngle(0);
            await DelayFunc(CenterReturnSettleMs, token);

            // Conservative: 90 % of the smaller side. Treating asymmetric
            // mechanical limits as if they were symmetric would push past
            // the tighter stop on every guidance correction.
            MaxSteerAngle = (int)(Math.Min(DetectedMaxAngleRight, DetectedMaxAngleLeft) * 0.9);

            CalibrationCompleted = true;
            Phase = MaxSteeringAnglePhase.Complete;
            PhaseResult =
                $"Right max: {DetectedMaxAngleRight:F1}\n" +
                $"Left max: {DetectedMaxAngleLeft:F1}\n" +
                $"Max steer angle (raw WAS): {MaxSteerAngle}";
        }
        catch (OperationCanceledException)
        {
            // finally restores free-drive state.
        }
        finally
        {
            AutoSteerService?.SetFreeDriveAngle(0);
            AutoSteerService?.DisableFreeDrive();
        }
    }

    /// <summary>
    /// Command full lock in the requested direction and poll the WAS
    /// at <see cref="PollIntervalMs"/> until the angle change between
    /// successive samples stays below <see cref="PlateauThresholdDeg"/>
    /// for <see cref="PlateauStableSamples"/> consecutive samples. If
    /// no plateau materialises within <see cref="PlateauTimeoutMs"/>
    /// the last sample is returned — partial information beats no
    /// information.
    /// </summary>
    private async Task<double> DriveToPlateauAsync(double commandedAngleDeg, CancellationToken token)
    {
        AutoSteerService?.SetFreeDriveAngle(commandedAngleDeg);

        double previous = GetCurrentWasAngle();
        int stableCount = 0;
        int elapsed = 0;

        while (elapsed < PlateauTimeoutMs)
        {
            token.ThrowIfCancellationRequested();
            await DelayFunc(PollIntervalMs, token);
            elapsed += PollIntervalMs;

            double sample = GetCurrentWasAngle();
            LiveSteerAngle = Math.Round(sample, 1);

            if (Math.Abs(sample - previous) < PlateauThresholdDeg)
            {
                if (++stableCount >= PlateauStableSamples)
                    return sample;
            }
            else
            {
                stableCount = 0;
            }
            previous = sample;
        }

        return previous;
    }

    private Task Redo()
    {
        Phase = MaxSteeringAnglePhase.WaitingToStart;
        Progress = 0;
        DetectedMaxAngleRight = 0;
        DetectedMaxAngleLeft = 0;
        MaxSteerAngle = 0;
        PhaseResult = "";
        CalibrationCompleted = false;
        return Task.CompletedTask;
    }

    private double GetCurrentWasAngle()
    {
        if (ReadWasAngle != null)
            return ReadWasAngle();
        return AutoSteerService?.LastSteerData.ActualSteerAngle ?? 0;
    }

    protected override void OnEntering()
    {
        Phase = MaxSteeringAnglePhase.WaitingToStart;
        Progress = 0;
        PhaseResult = "";
        CalibrationCompleted = false;

        var autoSteer = ConfigService.Store.AutoSteer;
        MaxSteerAngle = autoSteer.MaxSteerAngle;

        if (AutoSteerService != null)
            AutoSteerService.StateUpdated += OnStateUpdated;

        SubscribeToSwitchGate();
    }

    protected override void OnLeaving()
    {
        _cancellationTokenSource?.Cancel();

        if (AutoSteerService != null)
        {
            AutoSteerService.StateUpdated -= OnStateUpdated;

            if (AutoSteerService.IsInFreeDriveMode)
            {
                AutoSteerService.SetFreeDriveAngle(0);
                AutoSteerService.DisableFreeDrive();
            }
        }

        UnsubscribeFromSwitchGate();

        if (CalibrationCompleted)
            ConfigService.Store.AutoSteer.MaxSteerAngle = MaxSteerAngle;
    }

    private void OnStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        if (AutoSteerService != null)
            LiveSteerAngle = Math.Round(AutoSteerService.LastSteerData.ActualSteerAngle, 1);
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
