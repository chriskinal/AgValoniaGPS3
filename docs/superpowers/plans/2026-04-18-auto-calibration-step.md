# Auto Motor Calibration Step - Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development to implement.

**Goal:** Replace 3 manual wizard steps (Motor Direction, PWM Settings, Max Steer Angle) with a single automated calibration step that auto-detects motor direction, minimum PWM, and maximum steering angle.

**Architecture:** Single ViewModel with state machine (phases A0->A1->B0->B1->Done). Uses IAutoSteerService free-drive mode to control motor, LastSteerData.ActualSteerAngle to read WAS. All testable without UI via mocked service.

---

## Task 1: AutoMotorCalibrationStepViewModel

**Files:**
- Create: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/AutoMotorCalibrationStepViewModel.cs`
- Test: `Tests/AgValoniaGPS.Services.Tests/SteerWizardStepTests.cs`

### State Machine

```
Phase A0 (WaitingToStart) -> user clicks Start
Phase A1 (RampingPWM) -> auto-ramp, detect movement -> result
Phase B0 (WaitingForMaxAngle) -> user clicks Continue  
Phase B1 (MeasuringMaxAngle) -> drive full lock both ways -> result
Done (CalibrationComplete) -> show summary
```

### Properties

```csharp
// State
CalibrationPhase Phase (enum: WaitingToStart, RampingPWM, RampResult, 
                        WaitingForMaxAngle, MeasuringMaxAngle, Complete)
string PhaseDescription  // explains current phase to user
string PhaseResult       // shows result after each phase
double Progress          // 0-1 for progress bar

// Results
int DetectedMinPwm
bool DetectedInvertMotor
double DetectedMaxAngleRight  // raw degrees
double DetectedMaxAngleLeft
int MaxSteerAngle             // final: min(L,R) * 0.9

// Live feedback
double LiveSteerAngle
int CurrentPwm

// Commands
ICommand StartTestCommand      // begins Phase A1
ICommand ContinueCommand       // begins Phase B1
ICommand AcceptResultsCommand  // saves to config

// Config load/save
OnEntering: load current MinPwm, InvertMotor, MaxSteerAngle
OnLeaving: save detected values if calibration completed
```

### Phase A1 Algorithm (PWM Ramp)

```csharp
private async Task RunPwmRamp()
{
    Phase = CalibrationPhase.RampingPWM;
    double startAngle = _autoSteerService.LastSteerData.ActualSteerAngle;
    
    _autoSteerService.EnableFreeDrive();
    
    for (int pwm = 0; pwm <= 255; pwm += 5)
    {
        // Convert PWM to approximate angle for free-drive
        double testAngle = pwm * 0.15; // rough mapping
        _autoSteerService.SetFreeDriveAngle(testAngle);
        await Task.Delay(200);
        
        double currentAngle = _autoSteerService.LastSteerData.ActualSteerAngle;
        double moved = currentAngle - startAngle;
        CurrentPwm = pwm;
        Progress = pwm / 255.0;
        
        if (Math.Abs(moved) >= 10.0)
        {
            // Detected movement!
            DetectedInvertMotor = moved < 0; // negative = inverted
            DetectedMinPwm = (int)(pwm * 1.1); // add 10%
            
            // Return to center
            _autoSteerService.SetFreeDriveAngle(0);
            await Task.Delay(500);
            _autoSteerService.DisableFreeDrive();
            
            Phase = CalibrationPhase.RampResult;
            PhaseResult = $"Motor direction: {(DetectedInvertMotor ? "Inverted" : "Normal")}\n" +
                         $"Minimum PWM: {DetectedMinPwm}";
            return;
        }
    }
    
    // No movement detected at max PWM
    _autoSteerService.DisableFreeDrive();
    Phase = CalibrationPhase.RampResult;
    PhaseResult = "Warning: No wheel movement detected. Check motor connection.";
}
```

### Phase B1 Algorithm (Max Angle)

```csharp
private async Task RunMaxAngleMeasurement()
{
    Phase = CalibrationPhase.MeasuringMaxAngle;
    _autoSteerService.EnableFreeDrive();
    
    // Full right - brief hold, read angle
    _autoSteerService.SetFreeDriveAngle(45); // request max right
    await Task.Delay(1500); // allow time to reach lock
    DetectedMaxAngleRight = Math.Abs(_autoSteerService.LastSteerData.ActualSteerAngle);
    Progress = 0.33;
    
    // Return to center
    _autoSteerService.SetFreeDriveAngle(0);
    await Task.Delay(800);
    Progress = 0.5;
    
    // Full left - brief hold, read angle
    _autoSteerService.SetFreeDriveAngle(-45);
    await Task.Delay(1500);
    DetectedMaxAngleLeft = Math.Abs(_autoSteerService.LastSteerData.ActualSteerAngle);
    Progress = 0.83;
    
    // Return to center
    _autoSteerService.SetFreeDriveAngle(0);
    await Task.Delay(500);
    _autoSteerService.DisableFreeDrive();
    Progress = 1.0;
    
    // Calculate max angle (conservative)
    MaxSteerAngle = (int)(Math.Min(DetectedMaxAngleRight, DetectedMaxAngleLeft) * 0.9);
    
    Phase = CalibrationPhase.Complete;
    PhaseResult = $"Right max: {DetectedMaxAngleRight:F1} deg\n" +
                 $"Left max: {DetectedMaxAngleLeft:F1} deg\n" +
                 $"Max steer angle: {MaxSteerAngle} deg";
}
```

### Tests (write first)

- `AutoCalibration_PhaseA_DetectsMotorDirection` - Feed increasing WAS angle after PWM ramp -> InvertMotor=false
- `AutoCalibration_PhaseA_DetectsInvertedMotor` - Feed decreasing WAS angle -> InvertMotor=true
- `AutoCalibration_PhaseA_CalculatesMinPwm` - Verify MinPwm = threshold * 1.1
- `AutoCalibration_PhaseA_NoMovement_WarnsUser` - WAS stays at 0 -> warning message
- `AutoCalibration_PhaseB_MeasuresMaxAngles` - Feed known angles -> verify MaxSteerAngle = min * 0.9
- `AutoCalibration_OnLeaving_SavesResults` - Verify config updated
- `AutoCalibration_ShouldSkip_WhenGpsOnly`

---

## Task 2: AXAML View

**Files:**
- Create: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/AutoMotorCalibrationStepView.axaml`
- Create: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/AutoMotorCalibrationStepView.axaml.cs`

Layout changes per phase (use IsVisible bindings on Phase):

**Phase A0:** Title + explanation + Start Test button
**Phase A1:** Progress bar + current PWM + live WAS angle
**Phase B0:** Result from A + explanation for B + Continue button  
**Phase B1:** Progress bar + live angle + "Testing right..." / "Testing left..."
**Complete:** All results + Accept button

---

## Task 3: Replace old steps in SteerWizardViewModel

Remove: PwmCalibrationStepViewModel, MotorDirectionTestStepViewModel
Add: AutoMotorCalibrationStepViewModel (after WAS, before CPD circle test)
Update WizardHost switch.

---

## Task 4: Update tests for new step count
