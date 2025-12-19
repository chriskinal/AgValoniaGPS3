# Tool/Implement Configuration Enhancement Plan

**Date:** December 17, 2025
**Goal:** Bring Tool Configuration tab to full AgOpenGPS feature parity with 7 sub-tabs

## Executive Summary

The current AgValoniaGPS3 Tool Configuration is a simplified single-page implementation. The original AgOpenGPS has **7 distinct sub-tabs** with extensive functionality including dynamic diagrams, section width editors, zone configuration, and work switch settings.

**Current state:** Basic tool type, dimensions, and hitch settings on one page
**Target state:** Full 7 sub-tab implementation matching AgOpenGPS functionality

---

## Original AgOpenGPS Tool Configuration Structure

Based on analysis of `/SourceCode/GPS/Forms/Settings/ConfigTool.Designer.cs`:

```
Tool/Implement Configuration
├── 1. Config      - Tool attachment type selection with diagrams
├── 2. Hitch       - Hitch length settings (dynamic based on tool type)
├── 3. Settings    - Section timing (look-ahead on/off, turn-off delay)
├── 4. Offset      - Tool offset (left/right) and overlap/gap
├── 5. Pivot       - Trailing tool pivot distance
├── 6. Sections    - Section widths, zones, boundary control
└── 7. Switches    - Work switch and steer switch configuration
```

---

## Sub-Tab 1: Config (Tool Type)

### Purpose
Select the tool attachment style. Different types have different hitch configurations.

### Tool Types
| Type | Description | Hitch Behavior |
|------|-------------|----------------|
| **Front Fixed** | Front-mounted implement (harvester header) | Positive hitch length |
| **Rear Fixed** | 3-point hitch mounted | Negative hitch length |
| **TBT** | Tow Between Tanks (sprayer with tank trailer) | Trailing + Tank hitch |
| **Trailing** | Towed implement (drill, planter) | Trailing hitch only |

### UI Elements
- Radio button group for tool type selection
- Dynamic diagram showing selected configuration
- Harvester mode hides all options (uses Front Fixed)

### Diagrams (from btnImages/)
- `ToolChkFront.png` - Front fixed diagram
- `ToolChkRear.png` - Rear fixed diagram
- `ToolChkTBT.png` - TBT diagram
- `ToolChkTrailing.png` - Trailing diagram

### Logic
```csharp
// When tool type changes, hitch sign must change
if (IsToolFrontFixed && HitchLength < 0)
    HitchLength *= -1;
else if (!IsToolFrontFixed && HitchLength > 0)
    HitchLength *= -1;
```

---

## Sub-Tab 2: Hitch

### Purpose
Configure hitch lengths. UI changes dynamically based on tool type.

### Settings by Tool Type

| Tool Type | Visible Controls | Diagram |
|-----------|------------------|---------|
| Front Fixed | Drawbar Length only | `ToolHitchPageFront.png` |
| Rear Fixed | Drawbar Length only | `ToolHitchPageRear.png` |
| TBT | Trailing Hitch + Tank Hitch | `ToolHitchPageTBT.png` |
| Trailing | Trailing Hitch only | `ToolHitchPageTrailing.png` |
| Harvester | Drawbar Length only | `ToolHitchPageFrontHarvester.png` |

### Settings
| Setting | Type | Unit | Range | Description |
|---------|------|------|-------|-------------|
| Drawbar Length | double | cm | 0-1000 | Hitch to rear axle (absolute, sign auto-applied) |
| Trailing Hitch Length | double | cm | 0-1000 | Length to trailing tool |
| Tank Hitch Length | double | cm | 0-1000 | TBT tank trailer hitch |

### Key Behavior
- Values entered as **positive** (absolute)
- System applies correct sign based on tool type
- Diagram updates position of numeric input based on type

---

## Sub-Tab 3: Settings (Timing)

### Purpose
Configure section look-ahead timing for auto section control.

### Settings
| Setting | Type | Unit | Range | Default | Description |
|---------|------|------|-------|---------|-------------|
| Look Ahead On | double | seconds | 0-10 | 1.0 | Time before boundary to turn ON |
| Look Ahead Off | double | seconds | 0-10 | 0.5 | Time before boundary to turn OFF |
| Turn Off Delay | double | seconds | 0-10 | 0.0 | Delay after leaving coverage |

### Validation Rules
- `LookAheadOff` cannot exceed `LookAheadOn * 0.8`
- If `TurnOffDelay > 0`, then `LookAheadOff` is set to 0 (mutually exclusive)

### Diagrams
- `SectionLookAheadDelay.gif` - Animation showing delay
- `SectionLookAheadOff.gif` - Animation showing off timing
- `SectionOnLookAhead.gif` - Animation showing on timing

---

## Sub-Tab 4: Offset

### Purpose
Configure lateral tool offset and overlap/gap between passes.

### Settings
| Setting | Type | Unit | Range | Description |
|---------|------|------|-------|-------------|
| Tool Offset | double | cm | 0-500 | Lateral offset amount |
| Offset Direction | enum | - | Left/Right | Which side tool is offset |
| Overlap/Gap | double | cm | 0-100 | Pass overlap amount |
| Overlap Type | enum | - | Overlap/Gap | Overlap or gap between passes |

### UI Elements
- Numeric input for offset value
- Radio buttons: Left (negative) / Right (positive)
- "Zero" button to reset offset
- Numeric input for overlap value
- Radio buttons: Overlap (positive) / Gap (negative)
- "Zero" button to reset overlap

### Diagrams
- `ToolOffsetNegativeLeft.png` - Left offset indicator
- `ToolOffsetPositiveRight.png` - Right offset indicator
- `ToolOverlap.png` - Overlap diagram
- `ToolGap.png` - Gap diagram

---

## Sub-Tab 5: Pivot

### Purpose
Configure pivot distance for trailing tools (distance from hitch to working point).

### When Visible
Only relevant for **Trailing** and **TBT** tool types.

### Settings
| Setting | Type | Unit | Range | Description |
|---------|------|------|-------|-------------|
| Pivot Distance | double | cm | 0-1000 | Distance from pivot to tool |
| Pivot Direction | enum | - | Behind/Ahead | Tool position relative to pivot |

### UI Elements
- Numeric input for pivot distance
- Radio buttons: Behind Pivot (positive) / Ahead of Pivot (negative)
- "Zero" button to reset

### Diagrams
- `ToolHitchPivotOffsetPos.png` - Pivot behind (positive)
- `ToolHitchPivotOffsetNeg.png` - Pivot ahead (negative)

---

## Sub-Tab 6: Sections

### Purpose
Configure section widths, positions, and zone groupings. This is the **most complex** sub-tab.

### Two Modes

#### Mode 1: Individual Sections (`isSectionsNotZones = true`)
- Each section has its own width
- Up to 16 sections supported
- Individual section control

#### Mode 2: Symmetric Zones (`isSectionsNotZones = false`)
- Sections grouped into 2-8 zones
- All sections same width
- Zone-based control (simpler for sprayers)

### Mode 1 Settings (Individual Sections)
| Setting | Type | Range | Description |
|---------|------|-------|-------------|
| Number of Sections | int | 1-16 | Total section count |
| Default Section Width | double | 10-500 cm | Default width for new sections |
| Section 1-16 Width | double | 1-500 cm | Individual section widths |
| Section Off When Out | bool | - | Turn off sections outside boundary |
| Min Coverage | int | 0-100% | Minimum coverage before section turns on |
| Cutoff Speed | double | 0-10 km/h | Speed below which sections turn off |

### Mode 2 Settings (Symmetric Zones)
| Setting | Type | Range | Description |
|---------|------|-------|-------------|
| Number of Sections | int | 1-256 | Total micro-section count |
| Section Width | double | 0.1-50 cm | Width per micro-section |
| Number of Zones | int | 2-8 | Zone groupings |
| Zone 1-8 End Section | int | - | Which section each zone ends at |

### Section Position Calculation
```csharp
// Convert section widths to positions along toolbar
// Positions are relative to tool centerline
// Left side is negative, right side is positive

decimal setWidth = 0;
for (int j = 0; j < numSections; j++)
    setWidth += sectionWidths[j];

// Leftmost position (negative)
setWidth *= -0.5M;
sectionPositions[0] = setWidth;

// Calculate each section edge position
for (int j = 1; j <= numSections; j++)
    sectionPositions[j] = sectionPositions[j-1] + sectionWidths[j-1];
```

### UI Elements
- Mode toggle: Individual Sections / Symmetric Zones
- Number of sections selector (dropdown for individual, numeric for zones)
- Default width input
- Grid of section width inputs (1-16) - visibility based on count
- Total width display (calculated)
- Zone configuration panel (Mode 2 only)
- Section off when out boundary checkbox
- Min coverage input
- Cutoff speed input

### Validation
- Total tool width cannot exceed 50m (metric) or 164ft (imperial)
- Zone count cannot exceed section count
- Zone end sections must be sequential

---

## Sub-Tab 7: Switches

### Purpose
Configure work switch and steer switch inputs from Arduino/hardware.

### Settings
| Setting | Type | Description |
|---------|------|-------------|
| Work Switch Enabled | bool | Enable work switch input |
| Work Switch Active Low | bool | Switch active when closed (low) |
| Work Switch Controls | enum | Auto Sections / Manual Sections |
| Steer Switch Enabled | bool | Enable steer work switch |
| Steer Switch Controls | enum | Auto Sections / Manual Sections |

### UI Elements
- Checkbox: Enable Work Switch
- Toggle: Active High / Active Low (with diagram)
- Radio buttons: Controls Auto Sections / Controls Manual Sections
- Checkbox: Enable Steer Switch
- Radio buttons: Controls Auto Sections / Controls Manual Sections

### Diagrams
- `SwitchActiveOpen.png` - Active high diagram
- `SwitchActiveClosed.png` - Active low diagram

---

## Implementation Plan

### Phase 1: Restructure Tool Tab with Sub-Tabs

1. Create `ToolConfigSubTabs` folder under Configuration/
2. Create 7 sub-tab UserControls:
   - `ToolTypeSubTab.axaml`
   - `ToolHitchSubTab.axaml`
   - `ToolTimingSubTab.axaml`
   - `ToolOffsetSubTab.axaml`
   - `ToolPivotSubTab.axaml`
   - `ToolSectionsSubTab.axaml`
   - `ToolSwitchesSubTab.axaml`
3. Update `ToolConfigTab.axaml` to use TabControl with sub-tabs

### Phase 2: Model Updates

Add missing properties to `ToolConfig.cs`:
```csharp
// Section configuration
public bool IsSectionsNotZones { get; set; } = true;
public double DefaultSectionWidth { get; set; } = 100; // cm
public double[] SectionWidths { get; set; } = new double[16];
public double SlowSpeedCutoff { get; set; } = 0.5;

// Zone configuration
public int Zones { get; set; } = 2;
public int[] ZoneRanges { get; set; } = new int[9];

// Switch configuration
public bool IsWorkSwitchEnabled { get; set; }
public bool IsWorkSwitchActiveLow { get; set; }
public bool IsWorkSwitchManualSections { get; set; }
public bool IsSteerSwitchEnabled { get; set; }
public bool IsSteerSwitchManualSections { get; set; }
```

Add to `ConfigurationStore.cs`:
```csharp
public double[] SectionWidths { get; set; } = new double[16];
```

### Phase 3: ConfigurationViewModel Commands

Add edit commands:
- `EditDrawbarLengthCommand`
- `EditTrailingHitchCommand`
- `EditTankHitchCommand`
- `EditToolPivotCommand`
- `EditSectionWidthCommand` (with section index parameter)
- `EditDefaultSectionWidthCommand`
- `EditMinCoverageCommand`
- `EditCutoffSpeedCommand`
- `EditZoneEndCommand` (with zone index parameter)

### Phase 4: Dynamic UI Behavior

1. **Hitch sub-tab**: Show/hide controls based on tool type
2. **Pivot sub-tab**: Only show for Trailing/TBT types
3. **Sections sub-tab**: Toggle between Individual/Zone modes
4. **Section width inputs**: Show/hide based on section count

### Phase 5: Copy Required Icons

From `/btnImages/` to `Assets/Icons/`:
```
ToolChkFront.png, ToolChkRear.png, ToolChkTBT.png, ToolChkTrailing.png
ToolHitchPageFront.png, ToolHitchPageRear.png, ToolHitchPageTBT.png, ToolHitchPageTrailing.png
ToolHitchPageFrontHarvester.png
ToolOffsetNegativeLeft.png, ToolOffsetPositiveRight.png
ToolOverlap.png, ToolGap.png
ToolHitchPivotOffsetPos.png, ToolHitchPivotOffsetNeg.png
SwitchActiveOpen.png, SwitchActiveClosed.png
```

### Phase 6: Validation & Polish

1. Implement validation rules (look-ahead constraints, width limits)
2. Add unit conversion support (metric/imperial)
3. Add section position calculation
4. Test with real AgOpenGPS profiles

---

## Files to Create

| File | Purpose |
|------|---------|
| `ToolTypeSubTab.axaml` | Tool type selection with diagrams |
| `ToolHitchSubTab.axaml` | Dynamic hitch configuration |
| `ToolTimingSubTab.axaml` | Section timing settings |
| `ToolOffsetSubTab.axaml` | Offset and overlap settings |
| `ToolPivotSubTab.axaml` | Pivot distance for trailing |
| `ToolSectionsSubTab.axaml` | Section/zone configuration |
| `ToolSwitchesSubTab.axaml` | Work/steer switch settings |

## Files to Modify

| File | Changes |
|------|---------|
| `ToolConfigTab.axaml` | Add TabControl with 7 sub-tabs |
| `ToolConfig.cs` | Add missing properties |
| `ConfigurationStore.cs` | Add section widths array |
| `ConfigurationViewModel.cs` | Add edit commands |
| `ConfigurationService.cs` | Handle new properties in save/load |

---

## Estimated Complexity

| Sub-Tab | Complexity | Notes |
|---------|------------|-------|
| Config | Low | Radio buttons + static images |
| Hitch | Medium | Dynamic visibility based on type |
| Settings | Low | 3 numeric inputs with validation |
| Offset | Medium | Direction toggles + zero buttons |
| Pivot | Low | Simple when visible |
| Sections | **High** | Two modes, dynamic section grid, zone config |
| Switches | Medium | Multiple toggles with dependencies |

**Sections sub-tab is the most complex** due to:
- Two distinct operational modes
- Dynamic section width input grid (1-16 inputs)
- Zone range configuration
- Section position calculation
- Width validation and totaling

---

## AgOpenGPS Compatibility

All settings must map to AgOpenGPS XML profile format:
- `setTool_isToolFront`, `setTool_isToolTBT`, etc.
- `setVehicle_hitchLength`, `setTool_toolTrailingHitchLength`
- `setSection_position1` through `setSection_position17`
- `setTool_isSectionsNotZones`, `setTool_zones`
- `setF_isWorkSwitchEnabled`, `setF_isWorkSwitchActiveLow`, etc.

---

## Testing Checklist

- [ ] Tool type selection updates hitch sign correctly
- [ ] Hitch sub-tab shows correct controls for each tool type
- [ ] Timing validation prevents invalid look-ahead combinations
- [ ] Offset direction buttons work correctly
- [ ] Pivot sub-tab only visible for trailing/TBT
- [ ] Section mode toggle switches between individual/zone
- [ ] Section width grid shows correct number of inputs
- [ ] Total width calculates correctly
- [ ] Zone configuration works properly
- [ ] Switch settings save/load correctly
- [ ] Profile loads from AgOpenGPS XML correctly
- [ ] Profile saves compatible with AgOpenGPS
