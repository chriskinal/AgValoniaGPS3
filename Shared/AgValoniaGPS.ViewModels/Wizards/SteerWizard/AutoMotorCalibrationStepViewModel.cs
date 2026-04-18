// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
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
/// Calibration phases for the auto motor calibration step.
/// </summary>
public enum CalibrationPhase
{
    /// <summary>Phase A0: Waiting for user to start PWM ramp test.</summary>
    WaitingToStart,

    /// <summary>Phase A1: Auto-ramping PWM, detecting motor direction and MinPWM.</summary>
    RampingPWM,

    /// <summary>Phase A result: Shows detected motor direction and MinPWM.</summary>
    RampResult,

    /// <summary>Phase B0: Waiting for user to start max angle measurement.</summary>
    WaitingForMaxAngle,

    /// <summary>Phase B1: Driving full lock both ways to measure max angles.</summary>
    MeasuringMaxAngle,

    /// <summary>All calibration complete, showing final results.</summary>
    Complete
}

/// <summary>
/// Combined auto motor calibration step that replaces the separate Motor Direction Test
/// and PWM Calibration steps. Automatically detects motor direction, minimum PWM,
/// and maximum steering angles.
///
/// Phase A: Auto-ramp PWM to detect motor direction + MinPWM
/// Phase B: Drive full lock both ways to measure max steering angles
/// </summary>
public class AutoMotorCalibrationStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Injectable delay function for testing. Production uses Task.Delay.
    /// </summary>
    internal Func<int, CancellationToken, Task> DelayFunc { get; set; } = Task.Delay;

    /// <summary>
    /// Injectable function to read current WAS angle. Production reads from IAutoSteerService.
    /// </summary>
    internal Func<double>? ReadWasAngle { get; set; }

    public override string Title => "Auto Motor Calibration";

    public override string Description =>
        "Automatically detects motor direction, minimum PWM, and maximum steering angles. " +
        "Keep hands clear of the steering wheel during testing.";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

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
                OnPropertyChanged(nameof(IsPhaseB0));
                OnPropertyChanged(nameof(IsPhaseB1));
                OnPropertyChanged(nameof(IsComplete));
                OnPropertyChanged(nameof(PhaseDescription));
            }
        }
    }

    public bool IsPhaseA0 => Phase == CalibrationPhase.WaitingToStart;
    public bool IsPhaseA1 => Phase == CalibrationPhase.RampingPWM;
    public bool IsPhaseAResult => Phase == CalibrationPhase.RampResult;
    public bool IsPhaseB0 => Phase == CalibrationPhase.WaitingForMaxAngle;
    public bool IsPhaseB1 => Phase == CalibrationPhase.MeasuringMaxAngle;
    public bool IsComplete => Phase == CalibrationPhase.Complete;

    public string PhaseDescription => Phase switch
    {
        CalibrationPhase.WaitingToStart =>
            "This test will slowly increase motor power to detect the minimum PWM " +
            "needed to move the wheels and which direction the motor drives.\n\n" +
            "WARNING: Keep hands clear of the steering wheel.",
        CalibrationPhase.RampingPWM =>
            "Testing motor response... Keep hands clear.",
        CalibrationPhase.RampResult =>
            "Motor direction and minimum PWM detected.",
        CalibrationPhase.WaitingForMaxAngle =>
            "This test will briefly drive the wheels to full lock in both directions " +
            "to measure the maximum steering angle.\n\n" +
            "If your vehicle has power steering that resists stationary turning, " +
            "drive slowly during this test.\n\n" +
            "WARNING: Keep hands clear of the steering wheel.",
        CalibrationPhase.MeasuringMaxAngle =>
            "Measuring maximum steering angles... Keep hands clear.",
        CalibrationPhase.Complete =>
            "Calibration complete. Review the results below.",
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
    /// Final max steer angle as raw WAS value (not degrees - CPD not calibrated yet).
    /// Calculated as min(left, right) * 0.9.
    /// </summary>
    public int MaxSteerAngle
    {
        get => _maxSteerAngle;
        set => SetProperty(ref _maxSteerAngle, value);
    }

    private bool _noMovementDetected;
    public bool NoMovementDetected
    {
        get => _noMovementDetected;
        set => SetProperty(ref _noMovementDetected, value);
    }

    private bool _calibrationCompleted;
    /// <summary>Whether a full calibration has been completed (both phases).</summary>
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

    /// <summary>True when hardware is connected and sending data.</summary>
    public bool HasHardware => _autoSteerService != null;

    // =========================================================================
    // Commands
    // =========================================================================

    public ICommand StartTestCommand { get; }
    public ICommand ContinueToMaxAngleCommand { get; }
    public ICommand RedoPhaseACommand { get; }
    public ICommand RedoPhaseBCommand { get; }

    public AutoMotorCalibrationStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;

        StartTestCommand = new AsyncRelayCommand(RunPwmRampAsync);
        ContinueToMaxAngleCommand = new AsyncRelayCommand(RunMaxAngleMeasurementAsync);
        RedoPhaseACommand = new AsyncRelayCommand(RedoPhaseA);
        RedoPhaseBCommand = new AsyncRelayCommand(RedoPhaseB);
    }

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

        _autoSteerService?.EnableFreeDrive();

        try
        {
            for (int pwm = 0; pwm <= 255; pwm += 5)
            {
                token.ThrowIfCancellationRequested();

                double testAngle = pwm * 0.15;
                _autoSteerService?.SetFreeDriveAngle(testAngle);
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

                    _autoSteerService?.SetFreeDriveAngle(0);
                    await DelayFunc(500, token);
                    _autoSteerService?.DisableFreeDrive();

                    Phase = CalibrationPhase.RampResult;
                    PhaseResult = $"Motor direction: {(DetectedInvertMotor ? "Inverted" : "Normal")}\n" +
                                  $"Minimum PWM: {DetectedMinPwm}";
                    return;
                }
            }

            // No movement detected at max PWM
            _autoSteerService?.SetFreeDriveAngle(0);
            _autoSteerService?.DisableFreeDrive();
            NoMovementDetected = true;
            Phase = CalibrationPhase.RampResult;
            PhaseResult = "Warning: No wheel movement detected. Check motor connection.";
        }
        catch (OperationCanceledException)
        {
            _autoSteerService?.SetFreeDriveAngle(0);
            _autoSteerService?.DisableFreeDrive();
        }
    }

    // =========================================================================
    // Phase B1: Max Angle Measurement
    // =========================================================================

    internal async Task RunMaxAngleMeasurementAsync()
    {
        Phase = CalibrationPhase.MeasuringMaxAngle;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        _autoSteerService?.EnableFreeDrive();

        try
        {
            // Full right - brief hold, read angle
            _autoSteerService?.SetFreeDriveAngle(45);
            await DelayFunc(1500, token);
            DetectedMaxAngleRight = Math.Abs(GetCurrentWasAngle());
            Progress = 0.33;

            // Return to center
            _autoSteerService?.SetFreeDriveAngle(0);
            await DelayFunc(800, token);
            Progress = 0.5;

            // Full left - brief hold, read angle
            _autoSteerService?.SetFreeDriveAngle(-45);
            await DelayFunc(1500, token);
            DetectedMaxAngleLeft = Math.Abs(GetCurrentWasAngle());
            Progress = 0.83;

            // Return to center
            _autoSteerService?.SetFreeDriveAngle(0);
            await DelayFunc(500, token);
            _autoSteerService?.DisableFreeDrive();
            Progress = 1.0;

            // Calculate max angle (conservative) - raw WAS value
            MaxSteerAngle = (int)(Math.Min(DetectedMaxAngleRight, DetectedMaxAngleLeft) * 0.9);

            CalibrationCompleted = true;
            Phase = CalibrationPhase.Complete;
            PhaseResult = $"Right max: {DetectedMaxAngleRight:F1}\n" +
                          $"Left max: {DetectedMaxAngleLeft:F1}\n" +
                          $"Max steer angle (raw WAS): {MaxSteerAngle}";
        }
        catch (OperationCanceledException)
        {
            _autoSteerService?.SetFreeDriveAngle(0);
            _autoSteerService?.DisableFreeDrive();
        }
    }

    // =========================================================================
    // Redo commands
    // =========================================================================

    private Task RedoPhaseA()
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

    private Task RedoPhaseB()
    {
        Phase = CalibrationPhase.WaitingForMaxAngle;
        Progress = 0;
        DetectedMaxAngleRight = 0;
        DetectedMaxAngleLeft = 0;
        MaxSteerAngle = 0;
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
        return _autoSteerService?.LastSteerData.ActualSteerAngle ?? 0;
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

        var autoSteer = _configService.Store.AutoSteer;
        DetectedMinPwm = autoSteer.MinPwm;
        DetectedInvertMotor = autoSteer.InvertMotor;
        MaxSteerAngle = autoSteer.MaxSteerAngle;

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnStateUpdated;
    }

    protected override void OnLeaving()
    {
        // Cancel any running calibration
        _cancellationTokenSource?.Cancel();

        if (_autoSteerService != null)
        {
            _autoSteerService.StateUpdated -= OnStateUpdated;

            // Ensure free drive is off
            if (_autoSteerService.IsInFreeDriveMode)
            {
                _autoSteerService.SetFreeDriveAngle(0);
                _autoSteerService.DisableFreeDrive();
            }
        }

        // Save results if calibration was completed
        if (CalibrationCompleted)
        {
            var autoSteer = _configService.Store.AutoSteer;
            autoSteer.InvertMotor = DetectedInvertMotor;
            autoSteer.MinPwm = DetectedMinPwm;
            autoSteer.MaxSteerAngle = MaxSteerAngle;
        }
    }

    private void OnStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        LiveSteerAngle = Math.Round(_autoSteerService!.LastSteerData.ActualSteerAngle, 1);
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
