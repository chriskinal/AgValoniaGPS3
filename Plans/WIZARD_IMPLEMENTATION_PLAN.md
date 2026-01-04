# Wizard Implementation Plan - AgValoniaGPS3

## Overview

This plan covers the implementation of wizard-style configuration dialogs for AgValoniaGPS3, based on analysis of the AgOpenGPS `FormSteerWiz` and `FormConfig` implementations.

## AgOpenGPS Wizard Analysis

### FormSteerWiz (Primary Wizard)
- **Location**: `SourceCode/GPS/Forms/Settings/FormSteerWiz.cs` (1,318 lines)
- **Pattern**: Hidden TabControl with invisible tabs, button-based navigation
- **Pages**: 26 wizard steps covering AutoSteer configuration

#### Key UI Patterns
```
1. Hidden TabControl with invisible tabs
   - tabWiz.Appearance = TabAppearance.FlatButtons
   - tabWiz.ItemSize = new Size(0, 1)  // Makes tabs invisible
   - Navigation via: tabWiz.SelectedIndex++

2. Progress indicator
   - ProgressBar showing current step / total steps

3. Navigation buttons
   - "Next" / "Previous" / "Skip" buttons on each page
   - Conditional navigation based on user choices

4. Real-time feedback
   - Timer updates (250ms) for live sensor data
   - Visual feedback during calibration

5. Sidebar with current values
   - Shows WAS Offset, CPD, Ackermann values
```

#### Wizard Steps (26 total)
| Step | Name | Purpose |
|------|------|---------|
| 1 | tabStart | Wizard introduction |
| 2 | tabLoadDef | Load default parameters |
| 3 | tabWheelBase | Enter wheelbase dimension |
| 4 | tabWheelTrack | Enter vehicle track width |
| 5 | tabAntennaDistance | Antenna pivot distance |
| 6 | tabAntennaHeight | Antenna height |
| 7 | tabAntennaOffset | Antenna lateral offset |
| 8 | tabButtonSwitch | Switch configuration |
| 9 | tabA2DConv | Analog-to-Digital conversion |
| 10 | tabMotorDriver | Motor driver type (IBT2/Cytron) |
| 11 | tabInvertRelays | Relay inversion |
| 12 | tabDanfoss | Danfoss valve support |
| 13 | tabRollInv | IMU roll axis inversion |
| 14 | tabRollZero | Roll sensor zero calibration |
| 15 | tabWAS | WAS inversion |
| 16 | tabWAS_Zero | WAS zero offset calibration |
| 17 | tabMotorDirection | Motor direction test (Free Drive) |
| 18 | tabCPD_Setup | CPD setup intro |
| 19 | tabCountsPerDeg | CPD calibration via circle test |
| 20 | tabAckCPD | Ackermann factor calibration |
| 21 | tabMaxSteerAngle | Maximum steering angle detection |
| 22 | tabCancelGuidance | Sensor type selection |
| 23 | tabPanicStop | Emergency stop settings |
| 24 | tab_MinimumGain | Minimum movement (dead band) |
| 25 | tabPGain | Proportional gain tuning |
| 26 | tabEnd | Wizard completion |

### FormConfig (Multi-Tab Configuration)
- **Pattern**: Left-side menu buttons + hidden tab control
- **Purpose**: Master settings organizer (not sequential wizard)
- **Tabs**: 20+ categories (Vehicle, Tool, Heading, U-Turn, etc.)

---

## Implementation Plan for AgValoniaGPS3

### Phase 1: Base Wizard Infrastructure

#### 1.1 Create Wizard Base Classes

**File: `Shared/AgValoniaGPS.ViewModels/Wizards/WizardStepViewModel.cs`**
```csharp
public abstract class WizardStepViewModel : ViewModelBase
{
    public abstract string Title { get; }
    public abstract string Description { get; }
    public virtual string IconPath { get; } = null;

    public virtual bool CanGoNext { get; } = true;
    public virtual bool CanGoBack { get; } = true;
    public virtual bool CanSkip { get; } = false;

    public virtual void OnEntering() { }  // Called when step becomes active
    public virtual void OnLeaving() { }   // Called when leaving step
    public virtual Task<bool> ValidateAsync() => Task.FromResult(true);
}
```

**File: `Shared/AgValoniaGPS.ViewModels/Wizards/WizardViewModel.cs`**
```csharp
public abstract class WizardViewModel : ViewModelBase
{
    public ObservableCollection<WizardStepViewModel> Steps { get; }
    public WizardStepViewModel CurrentStep { get; set; }
    public int CurrentStepIndex { get; set; }
    public int TotalSteps => Steps.Count;
    public double Progress => (CurrentStepIndex + 1.0) / TotalSteps;

    public IRelayCommand NextCommand { get; }
    public IRelayCommand BackCommand { get; }
    public IRelayCommand SkipCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand FinishCommand { get; }
}
```

#### 1.2 Create Wizard Host Control

**File: `Shared/AgValoniaGPS.Views/Controls/Wizards/WizardHost.axaml`**
```xml
<UserControl>
    <Grid RowDefinitions="Auto,*,Auto,Auto">
        <!-- Progress Bar -->
        <ProgressBar Grid.Row="0" Value="{Binding Progress}" />

        <!-- Step Content (ContentControl with DataTemplates) -->
        <ContentControl Grid.Row="1" Content="{Binding CurrentStep}" />

        <!-- Step Indicator (circles/dots) -->
        <ItemsControl Grid.Row="2" Items="{Binding Steps}" />

        <!-- Navigation Buttons -->
        <StackPanel Grid.Row="3" Orientation="Horizontal">
            <Button Content="Back" Command="{Binding BackCommand}" />
            <Button Content="Skip" Command="{Binding SkipCommand}" />
            <Button Content="Next" Command="{Binding NextCommand}" />
            <Button Content="Finish" Command="{Binding FinishCommand}" />
        </StackPanel>
    </Grid>
</UserControl>
```

---

### Phase 2: Steer Wizard Implementation

The Steer Wizard is the primary wizard users rely on. It will be implemented in phases.

#### 2.1 SteerWizard ViewModel Structure

**File: `Shared/AgValoniaGPS.ViewModels/Wizards/SteerWizard/SteerWizardViewModel.cs`**

#### 2.2 Step ViewModels (Grouped by Category)

**Group A: Introduction**
- `WelcomeStepViewModel` - Introduction, start wizard
- `LoadDefaultsStepViewModel` - Option to load default parameters

**Group B: Vehicle Dimensions**
- `WheelbaseStepViewModel` - Wheelbase measurement
- `TrackWidthStepViewModel` - Track width measurement
- `AntennaPivotStepViewModel` - Antenna to pivot distance
- `AntennaHeightStepViewModel` - Antenna height
- `AntennaOffsetStepViewModel` - Antenna lateral offset

**Group C: Hardware Configuration**
- `SteerEnableStepViewModel` - Switch/Button/None
- `ADConverterStepViewModel` - Differential/Single
- `MotorDriverStepViewModel` - IBT2/Cytron
- `InvertRelaysStepViewModel` - Relay inversion
- `DanfossValveStepViewModel` - Danfoss support

**Group D: IMU/Roll Calibration**
- `RollInversionStepViewModel` - Invert roll axis
- `RollZeroStepViewModel` - Zero roll calibration

**Group E: WAS Calibration**
- `WASInversionStepViewModel` - Invert WAS
- `WASZeroStepViewModel` - Zero WAS offset
- `MotorDirectionStepViewModel` - Free drive test
- `CPDSetupStepViewModel` - CPD introduction
- `CPDCalibrationStepViewModel` - Circle test for CPD
- `AckermannStepViewModel` - Ackermann factor calibration

**Group F: Limits & Tuning**
- `MaxSteerAngleStepViewModel` - Maximum steer angle
- `CancelGuidanceStepViewModel` - Sensor type selection
- `PanicStopStepViewModel` - Emergency stop threshold
- `MinimumGainStepViewModel` - Dead band adjustment
- `ProportionalGainStepViewModel` - P-gain tuning

**Group G: Completion**
- `FinishStepViewModel` - Summary, restart option

#### 2.3 Step Views

**File Structure:**
```
Shared/AgValoniaGPS.Views/Controls/Wizards/SteerWizard/
    SteerWizardView.axaml
    Steps/
        WelcomeStepView.axaml
        WheelbaseStepView.axaml
        TrackWidthStepView.axaml
        AntennaPivotStepView.axaml
        AntennaHeightStepView.axaml
        AntennaOffsetStepView.axaml
        SteerEnableStepView.axaml
        ADConverterStepView.axaml
        MotorDriverStepView.axaml
        InvertRelaysStepView.axaml
        DanfossValveStepView.axaml
        RollInversionStepView.axaml
        RollZeroStepView.axaml
        WASInversionStepView.axaml
        WASZeroStepView.axaml
        MotorDirectionStepView.axaml
        CPDCalibrationStepView.axaml
        AckermannStepView.axaml
        MaxSteerAngleStepView.axaml
        CancelGuidanceStepView.axaml
        PanicStopStepView.axaml
        MinimumGainStepView.axaml
        ProportionalGainStepView.axaml
        FinishStepView.axaml
```

---

### Phase 3: Vehicle Setup Wizard

A simpler wizard for basic vehicle setup (subset of FormConfig).

**Steps:**
1. Vehicle Type (Tractor, Harvester, etc.)
2. Vehicle Dimensions (Wheelbase, Track Width)
3. Antenna Position (Pivot, Height, Offset)
4. Tool Type (Simple, Trailing, Fixed)
5. Tool Dimensions (Width, Offset, Overlap)
6. Section Configuration (Number, Widths)
7. Summary

---

### Phase 4: Tool Configuration Wizard

Focused wizard for implement/tool setup.

**Steps:**
1. Tool Type Selection
2. Hitch Configuration
3. Tool Dimensions
4. Section Layout
5. Section Switches
6. Overlap/Boundary Settings
7. Summary

---

## UI Design Guidelines

### Avalonia Implementation Notes

1. **No Hidden TabControl Pattern**
   - Use `ContentControl` with `DataTemplates` instead
   - Wizard steps are swapped via ContentControl.Content binding

2. **Progress Indicator**
   - Use `ProgressBar` for completion percentage
   - Consider step dots/circles for visual step indicator

3. **Touch-Friendly**
   - Large buttons (minimum 44x44px touch target)
   - Clear visual hierarchy
   - Works on tablets (iOS/Android)

4. **Consistent Layout**
   - Each step: Title, Description, Main Content, Navigation
   - Use consistent margins and spacing

### Example Step Layout

```xml
<Grid RowDefinitions="Auto,Auto,*,Auto">
    <!-- Title -->
    <TextBlock Grid.Row="0" Text="{Binding Title}"
               Classes="h2" Margin="0,0,0,8" />

    <!-- Description -->
    <TextBlock Grid.Row="1" Text="{Binding Description}"
               TextWrapping="Wrap" Margin="0,0,0,16" />

    <!-- Main Content (specific to each step) -->
    <StackPanel Grid.Row="2">
        <!-- Step-specific controls -->
    </StackPanel>

    <!-- Validation Messages -->
    <TextBlock Grid.Row="3" Text="{Binding ValidationMessage}"
               Foreground="Red" IsVisible="{Binding HasValidationError}" />
</Grid>
```

---

## Implementation Order

### Sprint 1: Infrastructure
1. [x] Analyze AgOpenGPS wizards
2. [ ] Create `WizardStepViewModel` base class
3. [ ] Create `WizardViewModel` base class
4. [ ] Create `WizardHost` control
5. [ ] Create step indicator control
6. [ ] Add wizard dialog support to MainViewModel

### Sprint 2: Steer Wizard - Basic Steps
7. [ ] Welcome/Intro step
8. [ ] Load Defaults step
9. [ ] Vehicle Dimensions steps (Wheelbase, Track Width)
10. [ ] Antenna Position steps (Pivot, Height, Offset)

### Sprint 3: Steer Wizard - Hardware Config
11. [ ] Steer Enable step
12. [ ] Motor Driver step
13. [ ] Invert Relays step
14. [ ] Danfoss Valve step

### Sprint 4: Steer Wizard - Calibration
15. [ ] Roll Inversion step
16. [ ] Roll Zero step
17. [ ] WAS Inversion step
18. [ ] WAS Zero step
19. [ ] Motor Direction (Free Drive) step

### Sprint 5: Steer Wizard - Advanced Calibration
20. [ ] CPD Setup step
21. [ ] CPD Calibration (circle test) step
22. [ ] Ackermann step
23. [ ] Max Steer Angle step

### Sprint 6: Steer Wizard - Tuning
24. [ ] Cancel Guidance step
25. [ ] Panic Stop step
26. [ ] Minimum Gain step
27. [ ] Proportional Gain step
28. [ ] Finish step

### Sprint 7: Additional Wizards
29. [ ] Vehicle Setup Wizard
30. [ ] Tool Configuration Wizard

---

## Key Controls to Implement

### Numeric Entry with Units
```xml
<Grid ColumnDefinitions="*,Auto,Auto">
    <NumericUpDown Grid.Column="0" Value="{Binding Value}" />
    <TextBlock Grid.Column="1" Text="{Binding Unit}" />
    <TextBlock Grid.Column="2" Text="{Binding MetricEquivalent}" />
</Grid>
```

### Real-time Sensor Display
```xml
<StackPanel>
    <TextBlock Text="Current Value:" />
    <TextBlock Text="{Binding SensorValue}" FontSize="24" FontWeight="Bold" />
    <ProgressBar Value="{Binding SensorValue}" Minimum="-50" Maximum="50" />
</StackPanel>
```

### Checkbox Option
```xml
<CheckBox Content="{Binding OptionText}" IsChecked="{Binding IsEnabled}" />
<TextBlock Text="{Binding HelpText}" TextWrapping="Wrap" Opacity="0.7" />
```

### Radio Button Group
```xml
<StackPanel>
    <RadioButton Content="Option A" IsChecked="{Binding IsOptionA}" />
    <RadioButton Content="Option B" IsChecked="{Binding IsOptionB}" />
</StackPanel>
```

---

## Settings Persistence

Wizard settings should be saved to:
1. **Vehicle Profile** - Vehicle dimensions, antenna positions
2. **Steer Settings** - Motor driver, inversions, calibration values
3. **Tool Profile** - Tool dimensions, sections, hitch config

Use existing `IProfileService` and settings infrastructure.

---

## Testing Considerations

1. **Unit Tests**
   - Wizard navigation logic
   - Step validation
   - Settings persistence

2. **Integration Tests**
   - Full wizard flow
   - Settings applied correctly

3. **UI Tests**
   - Touch/click navigation
   - Form validation feedback
   - Cross-platform consistency

---

## Dependencies

- CommunityToolkit.Mvvm (existing)
- Avalonia 11.x (existing)
- No additional packages required

---

## Next Steps

1. Review this plan
2. Create base wizard infrastructure
3. Implement Steer Wizard step by step
4. Test on Desktop and iOS
