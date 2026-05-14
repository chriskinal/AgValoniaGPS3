// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Phase progression for the motor direction + MinPWM step. The legacy
/// CalibrationPhase enum carried a Phase B branch for max steering
/// angle; that lives in <see cref="MaxSteeringAngleStepViewModel"/>
/// now, so this enum only tracks the discovery flow.
/// </summary>
public enum CalibrationPhase
{
    /// <summary>Waiting for the operator to start the ramp.</summary>
    WaitingToStart,

    /// <summary>Ramping the motor command, watching for first WAS motion.</summary>
    RampingPWM,

    /// <summary>Ramp result: motor direction + observed MinPWM.</summary>
    RampResult
}

/// <summary>
/// Motor Direction + MinPWM auto-detection step. Sweeps a small motor
/// command until the wheels respond, captures the firmware-reported
/// PWM at first motion as MinPWM, and infers motor direction from the
/// sign of WAS movement. The max-steering-angle measurement that used
/// to live here has moved to <see cref="MaxSteeringAngleStepViewModel"/>
/// so the two operations — quick discovery vs. full-lock stress test —
/// can be presented to the operator as distinct, linear wizard pages.
/// </summary>
public class AutoMotorCalibrationStepViewModel : SwitchGatedWizardStep
{
    private HardwareInstalledStepViewModel? _hardwareStep;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Injectable delay function for testing. Production uses Task.Delay.
    /// </summary>
    internal Func<int, CancellationToken, Task> DelayFunc { get; set; } = Task.Delay;

    /// <summary>
    /// Injectable function to read current WAS angle. Production reads
    /// from <see cref="IAutoSteerService.LastSteerData"/>.
    /// </summary>
    internal Func<double>? ReadWasAngle { get; set; }

    public override string Title => "Motor Direction + MinPWM";

    public override string Description =>
        "Automatically detects motor direction and the minimum PWM duty " +
        "needed to move the wheels. Keep hands clear of the steering wheel " +
        "during testing.";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    public AutoMotorCalibrationStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
        : base(configService, autoSteerService)
    {
        StartTestCommand = new AsyncRelayCommand(RunPwmRampAsync);
        RedoCommand = new AsyncRelayCommand(RedoRamp);
    }

    // =========================================================================
    // State
    // =========================================================================

    private CalibrationPhase _phase = CalibrationPhase.WaitingToStart;
    public CalibrationPhase Phase
    {
        get => _phase;
        set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(IsPhaseA0));
                OnPropertyChanged(nameof(IsPhaseA1));
                OnPropertyChanged(nameof(IsPhaseAResult));
                OnPropertyChanged(nameof(PhaseDescription));
            }
        }
    }

    public bool IsPhaseA0 => Phase == CalibrationPhase.WaitingToStart;
    public bool IsPhaseA1 => Phase == CalibrationPhase.RampingPWM;
    public bool IsPhaseAResult => Phase == CalibrationPhase.RampResult;

    public string PhaseDescription => Phase switch
    {
        CalibrationPhase.WaitingToStart =>
            "We'll command a small turn and gradually increase steering strength " +
            "until the wheels respond. This finds the minimum drive level and " +
            "the motor direction.\n\n" +
            "WARNING: Keep hands clear of the steering wheel.",
        CalibrationPhase.RampingPWM =>
            "Testing motor response... Keep hands clear.",
        CalibrationPhase.RampResult =>
            "Motor direction and minimum PWM detected.",
        _ => ""
    };

    private string _phaseResult = "";
    public string PhaseResult
    {
        get => _phaseResult;
        set => SetProperty(ref _phaseResult, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    // =========================================================================
    // Results
    // =========================================================================

    private int _detectedMinPwm;
    public int DetectedMinPwm
    {
        get => _detectedMinPwm;
        set => SetProperty(ref _detectedMinPwm, value);
    }

    private bool _detectedInvertMotor;
    public bool DetectedInvertMotor
    {
        get => _detectedInvertMotor;
        set => SetProperty(ref _detectedInvertMotor, value);
    }

    private bool _noMovementDetected;
    public bool NoMovementDetected
    {
        get => _noMovementDetected;
        set => SetProperty(ref _noMovementDetected, value);
    }

    private bool _calibrationCompleted;
    /// <summary>True once the ramp has produced a usable MinPWM result.</summary>
    public bool CalibrationCompleted
    {
        get => _calibrationCompleted;
        set => SetProperty(ref _calibrationCompleted, value);
    }

    // =========================================================================
    // Live feedback
    // =========================================================================

    private double _liveSteerAngle;
    public double LiveSteerAngle
    {
        get => _liveSteerAngle;
        set => SetProperty(ref _liveSteerAngle, value);
    }

    private int _currentPwm;
    public int CurrentPwm
    {
        get => _currentPwm;
        set => SetProperty(ref _currentPwm, value);
    }

    // =========================================================================
    // Commands
    // =========================================================================

    public ICommand StartTestCommand { get; }
    public ICommand RedoCommand { get; }

    // =========================================================================
    // Phase A1: PWM Ramp
    // =========================================================================

    internal async Task RunPwmRampAsync()
    {
        Phase = CalibrationPhase.RampingPWM;
        NoMovementDetected = false;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        double startAngle = GetCurrentWasAngle();

        AutoSteerService?.EnableFreeDrive();

        try
        {
            for (int pwm = 0; pwm <= 255; pwm += 5)
            {
                token.ThrowIfCancellationRequested();

                double testAngle = pwm * 0.15;
                AutoSteerService?.SetFreeDriveAngle(testAngle);
                await DelayFunc(200, token);

                double currentAngle = GetCurrentWasAngle();
                double moved = currentAngle - startAngle;
                CurrentPwm = pwm;
                Progress = pwm / 255.0;
                LiveSteerAngle = Math.Round(currentAngle, 1);

                if (Math.Abs(moved) >= 10.0)
                {
                    DetectedInvertMotor = moved < 0;
                    DetectedMinPwm = (int)(pwm * 1.1);

                    AutoSteerService?.SetFreeDriveAngle(0);
                    await DelayFunc(500, token);
                    AutoSteerService?.DisableFreeDrive();

                    CalibrationCompleted = true;
                    Phase = CalibrationPhase.RampResult;
                    PhaseResult = $"Motor direction: {(DetectedInvertMotor ? "Inverted" : "Normal")}\n" +
                                  $"Minimum PWM: {DetectedMinPwm}";
                    return;
                }
            }

            // No movement detected at max PWM
            AutoSteerService?.SetFreeDriveAngle(0);
            AutoSteerService?.DisableFreeDrive();
            NoMovementDetected = true;
            Phase = CalibrationPhase.RampResult;
            PhaseResult = "Warning: No wheel movement detected. Check motor connection.";
        }
        catch (OperationCanceledException)
        {
            AutoSteerService?.SetFreeDriveAngle(0);
            AutoSteerService?.DisableFreeDrive();
        }
    }

    private Task RedoRamp()
    {
        Phase = CalibrationPhase.WaitingToStart;
        PhaseResult = "";
        Progress = 0;
        CurrentPwm = 0;
        DetectedMinPwm = 0;
        DetectedInvertMotor = false;
        NoMovementDetected = false;
        CalibrationCompleted = false;
        return Task.CompletedTask;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private double GetCurrentWasAngle()
    {
        if (ReadWasAngle != null)
            return ReadWasAngle();
        return AutoSteerService?.LastSteerData.ActualSteerAngle ?? 0;
    }

    // =========================================================================
    // Lifecycle
    // =========================================================================

    protected override void OnEntering()
    {
        Phase = CalibrationPhase.WaitingToStart;
        PhaseResult = "";
        Progress = 0;
        CalibrationCompleted = false;

        var autoSteer = ConfigService.Store.AutoSteer;
        DetectedMinPwm = autoSteer.MinPwm;
        DetectedInvertMotor = autoSteer.InvertMotor;

        if (AutoSteerService != null)
            AutoSteerService.StateUpdated += OnStateUpdated;

        SubscribeToSwitchGate();
    }

    protected override void OnLeaving()
    {
        // Cancel any running calibration
        _cancellationTokenSource?.Cancel();

        if (AutoSteerService != null)
        {
            AutoSteerService.StateUpdated -= OnStateUpdated;

            // Ensure free drive is off
            if (AutoSteerService.IsInFreeDriveMode)
            {
                AutoSteerService.SetFreeDriveAngle(0);
                AutoSteerService.DisableFreeDrive();
            }
        }

        UnsubscribeFromSwitchGate();

        // Save results if calibration was completed
        if (CalibrationCompleted)
        {
            var autoSteer = ConfigService.Store.AutoSteer;
            autoSteer.InvertMotor = DetectedInvertMotor;
            autoSteer.MinPwm = DetectedMinPwm;
        }
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
