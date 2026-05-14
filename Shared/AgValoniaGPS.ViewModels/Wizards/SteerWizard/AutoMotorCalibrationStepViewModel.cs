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

using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Calibration phases for the auto motor calibration step.
/// </summary>
public enum CalibrationPhase
{
    /// <summary>Phase A0: Waiting for the operator to start the Kp-ramp test.</summary>
    WaitingToStart,

    /// <summary>Phase A1: Ramping Kp at a fixed +5° setpoint, watching for first WAS motion.</summary>
    RampingPWM,

    /// <summary>Phase A result: Motor direction + observed MinPWM captured.</summary>
    RampResult,

    /// <summary>Phase B0: Waiting for the operator to start max-angle measurement.</summary>
    WaitingForMaxAngle,

    /// <summary>Phase B1: Driving full lock both ways to measure max angles.</summary>
    MeasuringMaxAngle,

    /// <summary>All calibration complete, showing final results.</summary>
    Complete
}

/// <summary>
/// Combined auto motor calibration step. Phase A holds a small fixed
/// angle setpoint (start + 5°) and ramps the module's proportional gain
/// (Kp) until the wheel breaks static friction. The PWM the module is
/// reporting at the moment of first motion is the real MinPWM; we don't
/// guess it from a synthetic loop counter. If the wheel turns the wrong
/// way, we flip InvertMotor in ConfigStore (which re-emits PGN 251 via
/// the existing emit-on-change subscription) and re-run the ramp.
/// Phase B is unchanged from the prior design.
/// </summary>
public class AutoMotorCalibrationStepViewModel : SwitchGatedWizardStep
{
    private const int KpRampStartInclusive = 5;
    private const int KpRampEndInclusive = 200;
    private const int KpRampStep = 5;
    private const int KpRampSettleMs = 200;
    private const double TargetAngleDeltaDeg = 5.0;
    private const double MotionThresholdDeg = 1.0;
    private const double RunawayLimitDeg = 15.0;

    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Injectable delay function for testing. Production uses Task.Delay.
    /// </summary>
    internal Func<int, CancellationToken, Task> DelayFunc { get; set; } = Task.Delay;

    /// <summary>
    /// Injectable accessor for the live module feedback so tests can
    /// drive the ramp deterministically without standing up a UDP loop.
    /// Production reads from <see cref="IAutoSteerService.LastSteerData"/>.
    /// </summary>
    internal Func<SteerModuleData>? ReadModuleData { get; set; }

    public override string Title => "Auto Motor Calibration";

    public override string Description =>
        "Automatically detects motor direction and minimum PWM by holding a small " +
        "setpoint and increasing steering strength until the wheels respond. " +
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
            "We'll command a small turn and gradually increase steering strength " +
            "until the wheels respond. This finds the minimum drive level and the " +
            "motor direction.\n\n" +
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
    /// <summary>
    /// PWM the module reported at the moment the WAS first registered
    /// motion during the Kp ramp. This is the duty cycle the firmware
    /// is actually running at when the motor breaks static friction —
    /// the value persisted into <c>AutoSteerConfig.MinPwm</c>.
    /// </summary>
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

    private int _currentKp;
    /// <summary>
    /// Current Kp value driven into ConfigStore during the ramp. The
    /// progress bar tracks this. Note: the host's PGN 252 builder
    /// clamps the wire-level value to 1..100, so display values past
    /// 100 reflect the wizard's intent but the firmware sees the
    /// saturation.
    /// </summary>
    public int CurrentKp
    {
        get => _currentKp;
        set => SetProperty(ref _currentKp, value);
    }

    private int _reportedModulePwm;
    /// <summary>
    /// Live PWM from the module's PGN 253 byte 7 (data-payload offset),
    /// updated on every <c>StateUpdated</c>. Distinct from
    /// <see cref="DetectedMinPwm"/>: this one moves second-to-second
    /// during the ramp, the other is the captured snapshot at trigger.
    /// </summary>
    public int ReportedModulePwm
    {
        get => _reportedModulePwm;
        set => SetProperty(ref _reportedModulePwm, value);
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
        : base(configService, autoSteerService)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;

        StartTestCommand = new AsyncRelayCommand(RunKpRampAsync, () => CanStartTest);
        ContinueToMaxAngleCommand = new AsyncRelayCommand(RunMaxAngleMeasurementAsync);
        RedoPhaseACommand = new AsyncRelayCommand(RedoPhaseA);
        RedoPhaseBCommand = new AsyncRelayCommand(RedoPhaseB);
    }

    // =========================================================================
    // Phase A: Kp-ramp motor detection
    // =========================================================================

    /// <summary>
    /// Ramp the module's Kp at a fixed +5° setpoint until the WAS
    /// registers motion. The PWM the module is reporting at that moment
    /// is the observed MinPWM. If the wheel turns the wrong direction
    /// (negative delta), flip <see cref="AutoSteerConfig.InvertMotor"/>
    /// and re-run the ramp once. Restore the operator's original Kp on
    /// every exit path.
    /// </summary>
    internal async Task RunKpRampAsync()
    {
        Phase = CalibrationPhase.RampingPWM;
        NoMovementDetected = false;
        PhaseResult = "";
        Progress = 0;
        CurrentKp = 0;

        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        var autoSteerConfig = _configService.Store.AutoSteer;
        int originalKp = autoSteerConfig.ProportionalGain;

        _autoSteerService?.EnableFreeDrive();

        try
        {
            // Two-pass: first try the configured direction; if the wheel
            // turns the wrong way at first motion, flip InvertMotor and
            // restart the ramp from the bottom with the freshly settled
            // start angle. A second wrong-way detection means the motor
            // reverses in both modes (hardware/wiring fault).
            bool invertTried = false;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                // Re-anchor on entry: the previous pass may have left the
                // wheel slightly off-center, and the operator may have
                // touched it between passes.
                double startAngle = GetCurrentSteerAngle();
                double target = startAngle + TargetAngleDeltaDeg;
                _autoSteerService?.SetFreeDriveAngle(target);

                bool detected = false;
                bool wrongDirection = false;

                for (int kp = KpRampStartInclusive; kp <= KpRampEndInclusive; kp += KpRampStep)
                {
                    token.ThrowIfCancellationRequested();

                    // Writing to ConfigStore.AutoSteer.ProportionalGain
                    // triggers the AutoSteerService emit-on-change
                    // subscription, which re-issues PGN 252 with the new
                    // Kp. The module's PID picks up the new gain on the
                    // next firmware tick and the duty cycle climbs.
                    autoSteerConfig.ProportionalGain = kp;
                    CurrentKp = kp;
                    Progress = (double)(kp - KpRampStartInclusive)
                               / (KpRampEndInclusive - KpRampStartInclusive);

                    await DelayFunc(KpRampSettleMs, token);

                    var module = GetCurrentModuleData();
                    LiveSteerAngle = Math.Round(module.ActualSteerAngle, 1);
                    ReportedModulePwm = module.PwmDisplay;

                    double delta = module.ActualSteerAngle - startAngle;

                    // Runaway guard: if we've drifted way past the small
                    // setpoint, kill free-drive immediately rather than
                    // let the loop keep ramping Kp.
                    if (Math.Abs(delta) > RunawayLimitDeg)
                    {
                        NoMovementDetected = true;
                        PhaseResult =
                            "Runaway detected — the wheel moved more than " +
                            $"{RunawayLimitDeg:F0}° from the start position. " +
                            "Stopping for safety. Check WAS calibration and motor wiring.";
                        Phase = CalibrationPhase.RampResult;
                        return;
                    }

                    if (Math.Abs(delta) < MotionThresholdDeg)
                        continue;

                    // First motion. Snapshot the PWM the module is
                    // currently driving — that's the observed MinPWM.
                    if (delta > 0)
                    {
                        DetectedMinPwm = module.PwmDisplay;
                        DetectedInvertMotor = autoSteerConfig.InvertMotor;
                        detected = true;
                    }
                    else
                    {
                        wrongDirection = true;
                    }
                    break;
                }

                if (detected)
                {
                    PhaseResult =
                        $"Motor direction: {(DetectedInvertMotor ? "Inverted" : "Normal")}\n" +
                        $"Minimum PWM: {DetectedMinPwm}";
                    Phase = CalibrationPhase.RampResult;
                    return;
                }

                if (wrongDirection && !invertTried)
                {
                    // Flip InvertMotor in ConfigStore — emit-on-change
                    // pushes PGN 251 to the module, which re-engages
                    // the PID with the opposite sign convention on the
                    // next tick. Settle, then restart the Kp ramp from
                    // the bottom with a fresh start anchor.
                    autoSteerConfig.InvertMotor = true;
                    DetectedInvertMotor = true;
                    invertTried = true;
                    autoSteerConfig.ProportionalGain = originalKp;
                    await DelayFunc(500, token);
                    continue;
                }

                if (wrongDirection)
                {
                    NoMovementDetected = true;
                    PhaseResult =
                        "The motor turns the wrong direction in both modes. " +
                        "This usually means the motor leads are swapped or the " +
                        "WAS direction (Invert WAS) is wrong. Re-check wiring " +
                        "and the WAS step.";
                    Phase = CalibrationPhase.RampResult;
                    return;
                }

                // Ramp completed with no motion at all.
                NoMovementDetected = true;
                PhaseResult =
                    $"No wheel movement detected up to Kp={KpRampEndInclusive}. " +
                    "Check motor wiring, MaxPWM, and supply voltage.";
                Phase = CalibrationPhase.RampResult;
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancel; the finally block restores Kp and
            // disables free drive.
        }
        finally
        {
            _autoSteerService?.SetFreeDriveAngle(0);
            _autoSteerService?.DisableFreeDrive();
            autoSteerConfig.ProportionalGain = originalKp;
        }
    }

    // =========================================================================
    // Phase B: Max angle measurement (unchanged)
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
            DetectedMaxAngleRight = Math.Abs(GetCurrentSteerAngle());
            Progress = 0.33;

            // Return to center
            _autoSteerService?.SetFreeDriveAngle(0);
            await DelayFunc(800, token);
            Progress = 0.5;

            // Full left - brief hold, read angle
            _autoSteerService?.SetFreeDriveAngle(-45);
            await DelayFunc(1500, token);
            DetectedMaxAngleLeft = Math.Abs(GetCurrentSteerAngle());
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
        CurrentKp = 0;
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

    private SteerModuleData GetCurrentModuleData()
    {
        if (ReadModuleData != null)
            return ReadModuleData();
        return _autoSteerService?.LastSteerData ?? SteerModuleData.Empty;
    }

    private double GetCurrentSteerAngle() => GetCurrentModuleData().ActualSteerAngle;

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

        SubscribeToSwitchGate();
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnStateUpdated;
    }

    protected override void OnLeaving()
    {
        // Cancel any running calibration
        _cancellationTokenSource?.Cancel();

        UnsubscribeFromSwitchGate();
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
        if (_autoSteerService == null)
            return;
        var steerData = _autoSteerService.LastSteerData;
        LiveSteerAngle = Math.Round(steerData.ActualSteerAngle, 1);
        ReportedModulePwm = steerData.PwmDisplay;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
