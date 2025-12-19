# Configuration Panel - Functional Completion Checklist

## Current State
The first 4 tabs (Vehicle, Tool, Sections, U-Turn) have good graphical design but are **display-only**. Values cannot be edited because:
1. Value boxes are TextBlocks (read-only), not tappable buttons
2. No commands exist to trigger numeric input dialogs
3. No callback mechanism to update ConfigurationStore properties

## Architecture Overview

**Existing Pattern** (from MainViewModel for boundary offset):
```csharp
// 1. Set dialog properties
NumericInputDialogTitle = "Boundary Offset (cm)";
NumericInputDialogValue = (decimal)BoundaryOffset;
NumericInputDialogIntegerOnly = true;
NumericInputDialogAllowNegative = false;

// 2. Set callback
_numericInputDialogCallback = (value) => {
    BoundaryOffset = value;
    StatusMessage = $"Boundary offset set to {value:F0} cm";
};

// 3. Show dialog
State.UI.ShowDialog(DialogType.NumericInput);
```

**Problem**: NumericInputDialog is on MainViewModel, but config tabs use ConfigurationViewModel.

## Solution Options

### Option A: Add numeric input to ConfigurationViewModel (Recommended)
- Add NumericInput properties/commands to ConfigurationViewModel
- Add NumericInputDialogPanel to ConfigurationDialog.axaml (overlay within the dialog)
- Keep everything self-contained within the config dialog

### Option B: Bridge to MainViewModel
- ConfigurationViewModel fires events that MainViewModel handles
- More complex, creates coupling

---

## Phase 1: Infrastructure

### 1.1 Add Numeric Input Support to ConfigurationViewModel
- [ ] Add `NumericInputDialogTitle` property
- [ ] Add `NumericInputDialogValue` property
- [ ] Add `NumericInputDialogDisplayText` property
- [ ] Add `NumericInputDialogIntegerOnly` property
- [ ] Add `NumericInputDialogAllowNegative` property
- [ ] Add `NumericInputDialogMinValue` property
- [ ] Add `NumericInputDialogMaxValue` property
- [ ] Add `NumericInputDialogUnit` property (for display)
- [ ] Add `IsNumericInputDialogVisible` property
- [ ] Add `_numericInputDialogCallback` field (Action<double>)
- [ ] Add `ConfirmNumericInputCommand`
- [ ] Add `CancelNumericInputCommand`

### 1.2 Add NumericInputDialogPanel to ConfigurationDialog
- [ ] Add NumericInputDialogPanel.axaml/cs to Configuration folder (or reuse existing)
- [ ] Add overlay in ConfigurationDialog.axaml above the TabControl
- [ ] Bind visibility to `IsNumericInputDialogVisible`

### 1.3 Create Helper Method for Edit Commands
```csharp
private void ShowNumericInput(
    string title,
    double currentValue,
    Action<double> onConfirm,
    string unit = "m",
    bool integerOnly = false,
    bool allowNegative = true,
    double? min = null,
    double? max = null)
```

---

## Phase 2: Vehicle Tab Edit Commands

### 2.1 Hitch/Wheelbase/Track Sub-tab
- [ ] `EditHitchLengthCommand` - Tool.HitchLength (cm, allow negative)
- [ ] `EditWheelbaseCommand` - Vehicle.Wheelbase (cm)
- [ ] `EditTrackWidthCommand` - Vehicle.TrackWidth (cm)

### 2.2 Antenna Sub-tab
- [ ] `EditAntennaPivotCommand` - Vehicle.AntennaPivot (cm, allow negative)
- [ ] `EditAntennaHeightCommand` - Vehicle.AntennaHeight (cm)
- [ ] `EditAntennaOffsetCommand` - Vehicle.AntennaOffset (cm, allow negative)

### 2.3 Update XAML - Replace TextBlocks with Buttons
Change from:
```xaml
<Border Classes="ValueBox">
    <TextBlock Text="{Binding Vehicle.Wheelbase, StringFormat='{}{0:F0}'}" .../>
</Border>
```
To:
```xaml
<Button Classes="ValueBox" Command="{Binding EditWheelbaseCommand}">
    <TextBlock Text="{Binding Vehicle.Wheelbase, StringFormat='{}{0:F0}'}" .../>
</Button>
```

---

## Phase 3: Tool Tab Edit Commands

### 3.1 Tool Dimensions
- [ ] `EditToolWidthCommand` - Tool.Width (m)
- [ ] `EditToolOverlapCommand` - Tool.Overlap (m, allow negative)
- [ ] `EditToolOffsetCommand` - Tool.Offset (m, allow negative)

### 3.2 Hitch Settings
- [ ] `EditToolHitchLengthCommand` - Tool.HitchLength (m, allow negative)
- [ ] `EditTrailingHitchLengthCommand` - Tool.TrailingHitchLength (m, allow negative)

### 3.3 Update XAML - Make value boxes tappable

---

## Phase 4: Sections Tab Edit Commands

### 4.1 Section Config
- [ ] `EditNumSectionsCommand` - Config.NumSections (integer, 1-16)
- [ ] `EditSectionWidthCommand` - For individual section widths (future)

### 4.2 Section Timing
- [ ] `EditLookAheadOnCommand` - Tool.LookAheadOnSetting (seconds)
- [ ] `EditLookAheadOffCommand` - Tool.LookAheadOffSetting (seconds)
- [ ] `EditTurnOffDelayCommand` - Tool.TurnOffDelay (seconds)

### 4.3 Update XAML - Make value boxes tappable

---

## Phase 5: U-Turn Tab Edit Commands

### 5.1 U-Turn Settings
- [ ] `EditUTurnRadiusCommand` - Guidance.UTurnRadius (m)
- [ ] `EditUTurnExtensionCommand` - Guidance.UTurnExtension (m)
- [ ] `EditUTurnDistanceFromBoundaryCommand` - Guidance.UTurnDistanceFromBoundary (m)
- [ ] `EditUTurnSkipWidthCommand` - Guidance.UTurnSkipWidth (integer, 1-10)
- [ ] `EditUTurnSmoothingCommand` - Guidance.UTurnSmoothing (integer, 1-50)

### 5.2 Update XAML - Make value boxes tappable

---

## Phase 6: Service Integration

### 6.1 Mark Changes on Edit
- [ ] Each edit command calls `Config.MarkChanged()` after updating value
- [ ] This enables the "unsaved changes" indicator

### 6.2 Verify Apply/Cancel
- [ ] Apply button saves via `_configService.SaveProfile()`
- [ ] Cancel button reloads via `_configService.ReloadCurrentProfile()`

### 6.3 Verify Profile Persistence
- [ ] Changes persist after closing and reopening dialog
- [ ] Changes persist after app restart
- [ ] Profile XML files are updated correctly

---

## Phase 7: Polish

### 7.1 Visual Feedback
- [ ] Add hover/press states to value boxes (cursor: hand, background change)
- [ ] Show unit in value display (e.g., "250 cm" not just "250")
- [ ] Consider adding +/- buttons for quick adjustments

### 7.2 Validation
- [ ] Add min/max validation for each field
- [ ] Show validation errors in numeric input dialog
- [ ] Prevent invalid values from being saved

### 7.3 Unit Conversion (if IsMetric toggle exists)
- [ ] Display values in current unit system
- [ ] Convert on input if imperial mode

---

## Files to Modify

1. **ConfigurationViewModel.cs** - Add numeric input properties and ~20 edit commands
2. **ConfigurationDialog.axaml** - Add numeric input overlay
3. **VehicleConfigTab.axaml** - Replace TextBlocks with Buttons (~6 places)
4. **ToolConfigTab.axaml** - Replace TextBlocks with Buttons (~5 places)
5. **SectionsConfigTab.axaml** - Replace TextBlocks with Buttons (~4 places)
6. **UTurnConfigTab.axaml** - Replace TextBlocks with Buttons (~5 places)

---

## Estimated Work

| Phase | Description | Estimated Items |
|-------|-------------|-----------------|
| 1 | Infrastructure | ~15 properties/methods |
| 2 | Vehicle Tab | 6 commands + XAML |
| 3 | Tool Tab | 5 commands + XAML |
| 4 | Sections Tab | 4 commands + XAML |
| 5 | U-Turn Tab | 5 commands + XAML |
| 6 | Service Integration | Testing/verification |
| 7 | Polish | Visual/validation |

**Total**: ~20 edit commands, 1 dialog overlay, 4 XAML file updates

---

## Testing Checklist

- [ ] Tap each value box → numeric keypad appears
- [ ] Enter value → updates display immediately
- [ ] Click Apply → changes persist
- [ ] Click Cancel → changes revert
- [ ] Close/reopen dialog → values preserved
- [ ] Restart app → values loaded from profile
- [ ] Edit profile XML manually → changes reflected in UI
