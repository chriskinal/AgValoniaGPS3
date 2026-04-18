# AutoSteer Setup Wizard v2 - Feature Parity+ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a mature AutoSteer Setup Wizard with full legacy feature parity plus smart improvements: interactive calibration tests, prerequisites checking, live feedback, and auto-detection of inversions.

**Architecture:** The wizard uses WizardViewModel (step navigation) + WizardStepViewModel (per-step logic) + AXAML views. Live hardware data flows through IAutoSteerService (StateUpdated event, LatestSnapshot, LastSteerData). Steps are registered in WizardHost.axaml.cs switch. Conditional step skipping requires adding dynamic filtering to GoNextAsync/GoBack.

**Tech Stack:** Avalonia UI, ReactiveUI/CommunityToolkit.Mvvm, NUnit, NSubstitute, .NET 10

---

## Phase 1: Infrastructure Improvements

### Task 1: Add conditional step skipping to WizardViewModel

The base wizard currently navigates linearly through all steps. We need it to skip steps based on the HardwareInstalled selection (GPS Only skips autosteer steps 6-10).

**Files:**
- Modify: `Shared/AgValoniaGPS.ViewModels/Wizards/WizardViewModel.cs`
- Modify: `Shared/AgValoniaGPS.ViewModels/Wizards/WizardStepViewModel.cs`
- Test: `Tests/AgValoniaGPS.Services.Tests/SteerWizardE2ETests.cs`

- [ ] **Step 1: Add ShouldSkip to WizardStepViewModel**

```csharp
// In WizardStepViewModel.cs, add:
/// <summary>
/// When true, GoNext/GoBack will skip this step entirely.
/// Override in derived classes to implement conditional visibility.
/// </summary>
public virtual bool ShouldSkip => false;
```

- [ ] **Step 2: Modify GoNextAsync to skip steps**

In WizardViewModel.cs, change `GoNextAsync()`:
```csharp
private async Task GoNextAsync()
{
    if (CurrentStep == null || CurrentStepIndex >= Steps.Count - 1)
        return;

    var isValid = await CurrentStep.ValidateAsync();
    if (!isValid)
        return;

    // Find next non-skipped step
    int nextIndex = CurrentStepIndex + 1;
    while (nextIndex < Steps.Count && Steps[nextIndex].ShouldSkip)
        nextIndex++;

    if (nextIndex < Steps.Count)
    {
        CurrentStepIndex = nextIndex;
        CurrentStep = Steps[CurrentStepIndex];
    }
}
```

- [ ] **Step 3: Modify GoBack to skip steps**

```csharp
private void GoBack()
{
    if (CurrentStepIndex <= 0)
        return;

    int prevIndex = CurrentStepIndex - 1;
    while (prevIndex > 0 && Steps[prevIndex].ShouldSkip)
        prevIndex--;

    CurrentStepIndex = prevIndex;
    CurrentStep = Steps[CurrentStepIndex];
}
```

- [ ] **Step 4: Build and verify**

Run: `~/.dotnet/dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add Shared/AgValoniaGPS.ViewModels/Wizards/WizardViewModel.cs Shared/AgValoniaGPS.ViewModels/Wizards/WizardStepViewModel.cs
git commit -m "Add ShouldSkip to WizardStepViewModel for conditional step navigation"
```

### Task 2: Wire HardwareInstalled to skip autosteer steps

Steps 6-10 (HardwareConfig through SteeringGains) should be skipped when GPS Only is selected. The HardwareInstalledStepViewModel needs to propagate its selection to downstream steps.

**Files:**
- Modify: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/SteerWizardViewModel.cs`
- Modify: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/HardwareInstalledStepViewModel.cs`
- Modify: All autosteer step ViewModels (HardwareConfig, RollCalibration, WasCalibration, PwmCalibration, SteeringGains)

- [ ] **Step 1: Add hardware level reference to SteerWizardViewModel**

```csharp
// In SteerWizardViewModel constructor, after creating steps:
private HardwareInstalledStepViewModel _hardwareStep;

// In constructor:
_hardwareStep = new HardwareInstalledStepViewModel();
AddStep(_hardwareStep);
// ... other steps ...

// Pass reference to autosteer steps:
var hwConfig = new HardwareConfigStepViewModel(configService);
hwConfig.SetHardwareStep(_hardwareStep);
// etc for all autosteer steps
```

- [ ] **Step 2: Add ShouldSkip override to autosteer steps**

Create a base class or add to each:
```csharp
// In each autosteer step (HardwareConfig, Roll, WAS, PWM, Gains):
private HardwareInstalledStepViewModel? _hardwareStep;
public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;
public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0; // Skip for GPS Only
```

- [ ] **Step 3: Build and test**

Run: `~/.dotnet/dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj`
Run: `~/.dotnet/dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "SteerWizard"`

- [ ] **Step 4: Commit**

### Task 3: Add global status bar to WizardHost

A persistent bottom bar showing live hardware data across all steps: WAS angle, Roll, GPS fix quality, Module connection, PWM output.

**Files:**
- Modify: `Shared/AgValoniaGPS.Views/Controls/Wizards/WizardHost.axaml`
- Create: `Shared/AgValoniaGPS.ViewModels/Wizards/WizardStatusBarViewModel.cs`

- [ ] **Step 1: Create WizardStatusBarViewModel**

```csharp
public class WizardStatusBarViewModel : ObservableObject
{
    private readonly IAutoSteerService? _autoSteerService;

    public double WasAngle { get; set; }
    public double RollAngle { get; set; }
    public string GpsStatus { get; set; } = "No GPS";
    public bool IsModuleConnected { get; set; }
    public int PwmOutput { get; set; }
    public double Speed { get; set; }

    // Subscribe to StateUpdated, update properties from LatestSnapshot + LastSteerData
}
```

- [ ] **Step 2: Add status bar to WizardHost.axaml**

Between step dots and navigation buttons, add a compact row:
```xml
<!-- Status bar -->
<Border Grid.Row="3" Background="{DynamicResource SystemControlBackgroundAltHighBrush}" Padding="8,4">
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Spacing="16">
        <TextBlock Text="{Binding StatusBar.WasAngle, StringFormat='WAS: {0:F1} deg'}"/>
        <TextBlock Text="{Binding StatusBar.RollAngle, StringFormat='Roll: {0:F1} deg'}"/>
        <TextBlock Text="{Binding StatusBar.GpsStatus}"/>
        <!-- etc -->
    </StackPanel>
</Border>
```

- [ ] **Step 3: Build, verify, commit**

---

## Phase 2: Interactive Calibration Steps

### Task 4: Motor Direction Test step

Legacy step: "Drive slowly and enable Auto Steer. Invert Direction if steer motor turns the wrong direction." Our improvement: pulse buttons that send a 0.5s motor burst, user verifies direction visually.

**Files:**
- Create: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/MotorDirectionTestStepViewModel.cs`
- Create: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/MotorDirectionTestStepView.axaml`
- Create: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/MotorDirectionTestStepView.axaml.cs`
- Modify: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/SteerWizardViewModel.cs`
- Modify: `Shared/AgValoniaGPS.Views/Controls/Wizards/WizardHost.axaml.cs`

- [ ] **Step 1: Create MotorDirectionTestStepViewModel**

Properties:
- `InvertMotor: bool` (load/save from AutoSteerConfig)
- `LiveSteerAngle: double` (from LastSteerData.ActualSteerAngle)
- `IsPulsing: bool` (true during 0.5s pulse)
- `PulseLeftCommand: ICommand` - Enable free-drive at -20 deg for 0.5s, then disable
- `PulseRightCommand: ICommand` - Enable free-drive at +20 deg for 0.5s, then disable

Pulse implementation:
```csharp
private async Task PulseMotor(double angle)
{
    if (_autoSteerService == null || IsPulsing) return;
    IsPulsing = true;
    _autoSteerService.EnableFreeDrive();
    _autoSteerService.SetFreeDriveAngle(angle);
    await Task.Delay(500);
    _autoSteerService.SetFreeDriveAngle(0);
    _autoSteerService.DisableFreeDrive();
    IsPulsing = false;
}
```

- [ ] **Step 2: Create AXAML view**

Layout:
- Title + description: "Press Left or Right to briefly pulse the motor. Verify the wheels move in the correct direction. If backwards, enable Invert Motor."
- Two large icon buttons: SteerLeft.png + SteerRight.png (each 100x80)
- ConSt_InvertDirection.png + Invert Motor toggle
- Live WAS angle bar (reuse pattern from WAS step)
- Status text: "Pulsing..." during motor test

- [ ] **Step 3: Register in SteerWizardViewModel (after PWM step) and WizardHost switch**

- [ ] **Step 4: Build, test, commit**

### Task 5: CPD Circle Test step (Steer Circle RIGHT)

Legacy: "Turn steering wheel to RIGHT about 20 degrees. While driving in a steady circle, Press Rec and wait."

Prerequisites: RTK Fixed, vehicle moving, module connected.

**Files:**
- Create: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/CpdCircleTestStepViewModel.cs`
- Create: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/CpdCircleTestStepView.axaml`
- Create: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/CpdCircleTestStepView.axaml.cs`

- [ ] **Step 1: Create CpdCircleTestStepViewModel**

Properties:
- `IsRecording: bool`
- `Diameter: double` (max GPS distance from start)
- `CalculatedSteerAngle: double` (from atan formula)
- `ActualSteerAngle: double` (from WAS)
- `CalculatedCpd: double` (result)
- `CountsPerDegree: double` (editable, auto-set from test)
- `Ackermann: int` (editable)
- `MaxSteerAngle: int`
- `IsRtkFixed: bool` (prerequisite)
- `Speed: double` (prerequisite - must be > 0)
- `StartRecordingCommand: ICommand`
- `StopRecordingCommand: ICommand`

Algorithm (from legacy):
```csharp
// During recording, on each GPS update:
double dist = Distance(startPosition, currentPosition);
if (dist > diameter) { diameter = dist; stableCounter = 0; }
stableCounter++;

if (stableCounter > 9) // diameter stabilized
{
    double calcAngle = Math.Atan(wheelbase / ((diameter - trackWidth * 0.5) / 2));
    calcAngle = calcAngle * 180.0 / Math.PI;
    double cpd = (actualSteerAngle / calcAngle) * currentCpd * 0.9; // conservative
    CountsPerDegree = (int)cpd;
    IsRecording = false;
}
```

- [ ] **Step 2: Create view with prerequisite indicators**

```xml
<!-- Prerequisites section -->
<StackPanel>
    <Grid> <!-- RTK status: green dot if fixed, red if not -->
    <Grid> <!-- Speed: show current, warn if 0 -->
    <Grid> <!-- Module: connected indicator -->
</StackPanel>

<!-- Instructions -->
<TextBlock Text="Turn steering wheel to RIGHT about 20 degrees..."/>

<!-- Record button (disabled unless prerequisites met) -->
<Button Content="Record" IsEnabled="{Binding CanRecord}"/>

<!-- Results -->
<TextBlock Text="{Binding Diameter, StringFormat='Turning diameter: {0:F2} m'}"/>
<TextBlock Text="{Binding CalculatedCpd, StringFormat='Calculated CPD: {0:F0}'}"/>

<!-- Manual override -->
<Slider Value="{Binding CountsPerDegree}" Minimum="1" Maximum="255"/>
```

- [ ] **Step 3: Register, build, test, commit**

### Task 6: Ackermann Circle Test step (Steer Circle LEFT)

Same as CPD but driving left circle. Only available after CPD test (Ackermann != 100 blocks this).

**Files:**
- Create: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/AckermannTestStepViewModel.cs`
- Create: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/AckermannTestStepView.axaml`

- [ ] **Step 1: Create AckermannTestStepViewModel**

Similar to CPD but:
- Drives LEFT circle
- Formula: `ackermann = (leftAngle / startAngle) * 100`
- Stores result in `AutoSteerConfig.Ackermann`
- CanSkip = true (Ackermann is optional)

- [ ] **Step 2: Create view, register, build, commit**

---

## Phase 3: UI Polish & Text Improvements

### Task 7: Increase text sizes and fix contrast

All wizard steps need larger text and fixed contrast in light mode.

**Files:**
- Modify: All step AXAML views (12+ files)
- Modify: `Shared/AgValoniaGPS.Views/Controls/Wizards/WizardHost.axaml`

- [ ] **Step 1: Increase base text sizes**

In WizardHost.axaml, update styles:
- Title: 24 -> 28
- Description: 14 -> 16
- Field labels: 14 -> 16
- Input text: 18 -> 22
- Navigation buttons: 18 (already set)

- [ ] **Step 2: Fix selected button contrast in light mode**

Add explicit Foreground to selected state:
```xml
<Style Selector="Button.option-button.selected">
    <Setter Property="Background" Value="{DynamicResource SystemControlHighlightAccentBrush}"/>
    <Setter Property="Foreground" Value="White"/>
</Style>
```

- [ ] **Step 3: Include descriptions inside option buttons (Vehicle Type, Hardware Installed)**

Already done for VehicleType. Verify HardwareInstalled also has descriptions inside buttons.

- [ ] **Step 4: Build, capture screenshots in both themes, verify, commit**

### Task 8: Improve WAS step with units, slider, guide text

**Files:**
- Modify: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/WasCalibrationStepView.axaml`
- Modify: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/WasCalibrationStepViewModel.cs`

- [ ] **Step 1: Add units to all fields**

- WAS Offset: "counts"
- Counts Per Degree: "counts/deg"
- Max Steer Angle: "deg"

- [ ] **Step 2: Replace CPD TextBox with Slider + value display**

```xml
<Slider Value="{Binding CountsPerDegree}" Minimum="1" Maximum="255" TickFrequency="1"/>
<TextBlock Text="{Binding CountsPerDegree, StringFormat='{}{0:F0} counts/deg'}"/>
```

- [ ] **Step 3: Improve guide text**

```
"1. Point wheels straight ahead
2. Press 'Zero WAS' to set the zero position
3. Turn wheels RIGHT - the angle should read POSITIVE
4. If it reads negative, enable 'Invert WAS'
5. Set Counts Per Degree (higher = more sensitive, typical: 80-120)
6. Set Max Steer Angle to your vehicle's physical steering limit"
```

- [ ] **Step 4: Build, verify, commit**

### Task 9: Improve Roll step ordering and guide text

**Files:**
- Modify: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/RollCalibrationStepView.axaml`

- [ ] **Step 1: Reorder - Invert above Zero**

Move the Invert Roll toggle above the Zero Roll button.

- [ ] **Step 2: Add step-by-step guide text**

```
"1. Tilt your vehicle slightly to the RIGHT
2. The gauge should move to the RIGHT (positive)
3. If it moves LEFT (negative), enable 'Invert Roll'
4. Park on LEVEL GROUND
5. Press 'Zero Roll' to calibrate the zero position"
```

- [ ] **Step 3: Build, verify, commit**

### Task 10: Motor test pulse buttons (0.5s one-shot)

Replace the current hold-to-steer buttons with single-press 0.5s pulse buttons matching legacy behavior.

**Files:**
- Modify: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/PwmCalibrationStepViewModel.cs`
- Modify: `Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/PwmCalibrationStepView.axaml`

- [ ] **Step 1: Add pulse commands to PwmCalibrationStepViewModel**

```csharp
public ICommand PulseLeftCommand { get; }
public ICommand PulseRightCommand { get; }

private async Task PulseMotor(double angle)
{
    if (_autoSteerService == null || _isPulsing) return;
    _isPulsing = true;
    _autoSteerService.EnableFreeDrive();
    _autoSteerService.SetFreeDriveAngle(angle);
    await Task.Delay(500);
    _autoSteerService.SetFreeDriveAngle(0);
    _autoSteerService.DisableFreeDrive();
    _isPulsing = false;
}
```

- [ ] **Step 2: Update view with SteerLeft/SteerRight icon buttons**

Replace current FreeDriveLeft/Right/Center with two large pulse buttons + center reset.

- [ ] **Step 3: Build, verify, commit**

---

## Phase 4: Missing Legacy Settings

### Task 11: Add Ackermann and MaxSteerAngle to appropriate steps

- Move MaxSteerAngle from WAS step to a more logical position (or keep in WAS)
- Add Ackermann slider to CpdCircleTest step (or new step)

### Task 12: Add SideHill Compensation

Add `SideHillCompensation` (0-1.0) to the Steering Gains step.

### Task 13: Add Cancel Guidance / Safety settings

Create a Safety step or add to Speed & Sensors:
- ManualTurnsEnabled + ManualTurnsSpeed
- SteerInReverse toggle
- DeadzoneHeading + DeadzoneDelay

---

## Phase 5: Testing & Screenshots

### Task 14: Update all tests for new step count and new steps

### Task 15: Capture integration test screenshots with emulated data

Use the integration test harness with simulated GPS + IMU data to capture screenshots showing:
- WAS at different positions (left/center/right)
- WAS before/after zero
- WAS before/after invert
- Roll gauge with non-zero value
- Roll before/after zero
- Roll before/after invert
- CPD circle test in progress
- Motor pulse test

---

## Step Order (Final 16-step wizard)

1. Welcome
2. Vehicle Type (Tractor/Harvester/4WD)
3. Hardware Installed (GPS Only/AutoSteer/Full) - controls step skipping
4. Vehicle Dimensions (wheelbase + track width, two columns)
5. Antenna Position (pivot + height + offset, two columns)
6. Hardware Config (enable, motor, AD, relays, Danfoss) [skip if GPS Only]
7. Roll Calibration (invert + zero + gauge) [skip if GPS Only]
8. WAS Calibration (invert + zero + CPD slider) [skip if GPS Only]
9. Motor Direction Test (pulse L/R + invert) [skip if GPS Only]
10. Motor PWM Settings (max/min PWM + free-drive) [skip if GPS Only]
11. CPD Circle Test RIGHT (auto-calculate CPD) [skip if GPS Only, CanSkip=true]
12. Ackermann Circle Test LEFT (auto-calculate) [skip if GPS Only, CanSkip=true]
13. Steering Gains + Algorithm (PP/Stanley, Kp, Ki, sidehill) [skip if GPS Only]
14. Speed & Safety (speed limits, deadzone, sensors, steer-in-reverse)
15. Finish (summary + save)

GPS Only path: 1 -> 2 -> 3 -> 4 -> 5 -> 14 -> 15 (7 steps)
AutoSteer path: 1 -> 2 -> 3 -> 4 -> 5 -> 6-13 -> 14 -> 15 (15 steps)
