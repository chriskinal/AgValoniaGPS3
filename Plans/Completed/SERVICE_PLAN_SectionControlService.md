# Feature: SectionControlService

## Overview

Manage automatic section on/off based on coverage, boundaries, headlands, and look-ahead calculations. This is core functionality for sprayers, planters, and other implements that need per-section control.

## WinForms Reference

### Files
- `GPS/Forms/Sections.Designer.cs` - Section button handling, state management
- `GPS/Classes/CSection.cs` - WinForms wrapper for SectionControl
- `AgOpenGPS.Core/Models/Sections/SectionControl.cs` - Core section state model
- `AgOpenGPS.Core/Models/Tool/ToolConfiguration.cs` - Tool settings including sections

### Key State Variables
```csharp
// From Sections.Designer.cs
public btnStates manualBtnState = btnStates.Off;  // Manual master state
public btnStates autoBtnState = btnStates.Off;    // Auto master state

// From SectionControl.cs (per section)
public bool IsSectionOn { get; set; }
public bool IsSectionRequiredOn { get; set; }
public bool SectionOnRequest { get; set; }
public bool SectionOffRequest { get; set; }
public int SectionOnTimer { get; set; }
public int SectionOffTimer { get; set; }
public bool IsMappingOn { get; set; }
public double PositionLeft { get; set; }
public double PositionRight { get; set; }
public bool IsInBoundary { get; set; }
public bool IsInHeadlandArea { get; set; }
public SectionButtonState SectionButtonState { get; set; }  // Off, Auto, On
```

### Key Algorithms

1. **Section Position Calculation** (`SectionSetPosition()`, `SectionCalcWidths()`):
   - Each section has left/right position relative to tool center
   - Positions come from settings + tool offset
   - Tool width calculated from outermost section positions

2. **Look-Ahead Logic**:
   - `lookAheadOnSetting` - Time in seconds to look ahead for turning section ON
   - `lookAheadOffSetting` - Time in seconds to look ahead for turning section OFF
   - Look-ahead distance = speed × look-ahead time
   - Projects section position forward to check coverage/boundaries

3. **State Transitions**:
   - Off → Auto: Section controlled automatically by coverage/boundary
   - Auto → On: Manual override, section always on
   - On → Off: Manual override, section always off

## Config Settings Used

From `ConfigurationStore.Instance.Tool`:
- `NumSections` - Number of active sections (1-16)
- `SectionWidths[]` - Individual section widths
- `LookAheadOnSetting` - Look-ahead time for section ON
- `LookAheadOffSetting` - Look-ahead time for section OFF
- `TurnOffDelay` - Delay before turning section off
- `MinCoverage` - Minimum coverage percentage to trigger off
- `IsSectionOffWhenOut` - Turn off sections outside boundary
- `SlowSpeedCutoff` - Speed below which sections turn off
- `Offset` - Tool lateral offset from centerline
- `IsSectionsNotZones` - Use individual sections vs zones

## Proposed Interface

```csharp
public interface ISectionControlService
{
    // Section state array (one per section, up to 16)
    SectionState[] SectionStates { get; }

    // Master control
    SectionMasterState MasterState { get; set; }  // Off, Auto, Manual

    // Update loop - call each GPS update
    void Update(Vec3 toolPosition, double toolHeading, double speed);

    // Check if a world point is covered (for coverage queries)
    bool IsPointCovered(Vec3 point);

    // Check if section is in valid work area
    bool IsSectionInWorkArea(int sectionIndex, Vec3 toolPosition, double toolHeading);

    // Manual section control
    void SetSectionState(int sectionIndex, SectionButtonState state);
    void SetAllSections(SectionButtonState state);

    // Section position queries
    (double left, double right) GetSectionWorldPosition(int sectionIndex, Vec3 toolPosition, double toolHeading);

    // Events
    event EventHandler<SectionStateChangedEventArgs> SectionStateChanged;
}

public enum SectionMasterState { Off, Auto, Manual }

public class SectionState
{
    public bool IsOn { get; set; }
    public bool IsMappingOn { get; set; }  // For coverage recording
    public SectionButtonState ButtonState { get; set; }
    public double PositionLeft { get; set; }
    public double PositionRight { get; set; }
    public bool IsInBoundary { get; set; }
    public bool IsInHeadland { get; set; }
}
```

## Dependencies

### Required Services
- `IToolPositionService` - Calculate tool position from vehicle position
- `ICoverageMapService` - Query if points are already covered
- `IFieldService` - Access boundary and headland polygons

### Required Models
- `SectionControl` from AgOpenGPS.Core (already exists)
- `ToolConfiguration` from AgOpenGPS.Core (already exists)

## Implementation Steps

1. [ ] Create `ISectionControlService` interface in `Services/Interfaces/`
2. [ ] Create `SectionControlService` class in `Services/Section/`
3. [ ] Add section state models if not using Core models directly
4. [ ] Implement section position calculation from config
5. [ ] Implement look-ahead projection logic
6. [ ] Implement boundary/headland checking
7. [ ] Implement coverage checking (depends on CoverageMapService)
8. [ ] Implement manual override logic
9. [ ] Add DI registration
10. [ ] Create UI for section buttons (SectionControlPanel)
11. [ ] Wire up to MainViewModel
12. [ ] Test with simulator

## UI Requirements

### Section Control Panel
- Master Auto/Manual buttons
- Individual section buttons (colored by state: red=off, green=auto, yellow=manual)
- Section buttons resize based on number of sections
- Support for zones (groups of sections)

### Visual Indicators
- Look-ahead lines on map (green=on line, red=off line)
- Section state shown on tool rendering

## Look-Ahead Algorithm Detail

```csharp
// Pseudo-code for look-ahead
void UpdateSection(int index, Vec3 toolPos, double heading, double speed)
{
    var section = SectionStates[index];

    // Calculate look-ahead distance based on speed
    double lookAheadOnDist = speed * Tool.LookAheadOnSetting;
    double lookAheadOffDist = speed * Tool.LookAheadOffSetting;

    // Project section position forward
    var (sectionLeft, sectionRight) = GetSectionWorldPosition(index, toolPos, heading);
    var sectionCenter = (sectionLeft + sectionRight) / 2;

    // Project forward for ON check
    var onCheckPoint = ProjectForward(sectionCenter, heading, lookAheadOnDist);
    bool shouldBeOn = !IsPointCovered(onCheckPoint)
                   && IsPointInBoundary(onCheckPoint)
                   && !IsPointInHeadland(onCheckPoint);

    // Project forward for OFF check
    var offCheckPoint = ProjectForward(sectionCenter, heading, lookAheadOffDist);
    bool shouldBeOff = IsPointCovered(offCheckPoint)
                    || !IsPointInBoundary(offCheckPoint)
                    || IsPointInHeadland(offCheckPoint);

    // Apply state with timing
    if (shouldBeOn && !section.IsOn)
    {
        section.SectionOnTimer++;
        if (section.SectionOnTimer > threshold)
            section.IsOn = true;
    }
    else if (shouldBeOff && section.IsOn)
    {
        section.SectionOffTimer++;
        if (section.SectionOffTimer > Tool.TurnOffDelay)
            section.IsOn = false;
    }
}
```

## Priority

**Medium-High** - Core functionality for sprayers/planters. Required for production use of section control hardware.

## Notes

- AgOpenGPS.Core already has `SectionControl` model - consider using directly
- Section state needs to be sent to hardware via ModuleCommunicationService
- Coverage mapping (green patches) depends on CoverageMapService
