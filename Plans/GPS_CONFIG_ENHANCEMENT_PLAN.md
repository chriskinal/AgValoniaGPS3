# GPS/Data Sources Configuration Enhancement Plan

**Date:** December 22, 2025
**Goal:** Implement the GPS/Data Sources configuration tab with full AgOpenGPS feature parity

## Executive Summary

The current "Data Sources" tab is a placeholder with no functionality. AgOpenGPS has a comprehensive GPS configuration dialog covering antenna setup, heading source selection, and RTK monitoring.

**Current state:** Placeholder text only
**Target state:** Full GPS configuration with 4 sub-sections matching AgOpenGPS

---

## AgOpenGPS GPS Configuration Analysis

Based on available graphics and codebase analysis, the GPS/Sources configuration covers:

### Sub-Sections (4 total)

| Section | Graphic | Purpose |
|---------|---------|---------|
| **GPS Mode** | `Con_SourcesGPSSingle.png` / `Con_SourcesGPSDual.png` | Single vs Dual antenna selection |
| **Heading Source** | `Con_SourcesHead.png` | Where vehicle heading comes from |
| **RTK Monitoring** | `Con_SourcesRTKAlarm.png` | Fix quality alarms and thresholds |
| **Data Rate** | - | GPS update rate configuration |

---

## Sub-Section 1: GPS Mode

### Purpose
Select between single antenna GPS or dual antenna GPS (for heading from GPS).

### Options
| Mode | Description | Graphic |
|------|-------------|---------|
| **Single Antenna** | One GPS receiver, heading from IMU or calculated | `Con_SourcesGPSSingle.png` |
| **Dual Antenna** | Two GPS receivers, heading from antenna baseline | `Con_SourcesGPSDual.png` |

### Settings
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `IsDualGps` | bool | false | Use dual GPS antennas for heading |

### UI Elements
- Two large tappable cards with graphics
- Visual selection indicator (border highlight)
- Description text for each mode

### Behavior
- When Dual GPS selected, enables dual-specific settings in Heading Source section
- Single GPS relies on IMU or calculated heading

---

## Sub-Section 2: Heading Source

### Purpose
Configure where the vehicle heading comes from. Critical for accurate guidance.

### Heading Sources
| Source | Description | When Available |
|--------|-------------|----------------|
| **Single GPS** | Heading calculated from GPS position delta | Always |
| **Dual GPS** | Heading from dual antenna baseline | When IsDualGps=true |
| **IMU** | Heading from inertial measurement unit | When IMU connected |
| **Dual as IMU** | Use dual GPS heading as virtual IMU | When IsDualGps=true |

### Settings (from AhrsConfig)
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `IsDualAsIMU` | bool | false | Use dual GPS heading as IMU source |
| `AutoSwitchDualFixOn` | bool | false | Auto-switch to dual when fix quality improves |
| `AutoSwitchDualFixSpeed` | double | 0.5 | Speed threshold for auto-switch (km/h) |
| `FusionWeight` | double | 0.5 | GPS/IMU heading fusion weight (0=GPS only, 1=IMU only) |

### UI Elements
- Radio button group for primary heading source
- Fusion weight slider (when IMU available)
- Auto-switch toggle and speed threshold (when dual GPS)
- Graphic showing antenna positioning (`Con_SourcesHead.png`)

### Validation
- Dual GPS options only enabled when GPS Mode is Dual
- IMU options only shown when IMU hardware detected

---

## Sub-Section 3: RTK Monitoring

### Purpose
Configure RTK fix quality monitoring and alarms for precision agriculture.

### Fix Quality Levels
| Code | Name | Typical Accuracy | Suitable For |
|------|------|------------------|--------------|
| 0 | Invalid | - | Nothing |
| 1 | GPS Fix | 2-5m | Field recording only |
| 2 | DGPS | 0.5-2m | Basic guidance |
| 4 | RTK Fixed | 1-2cm | Precision planting/spraying |
| 5 | RTK Float | 10-50cm | Moderate precision work |

### Settings
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MinFixQuality` | int | 4 | Minimum acceptable fix quality |
| `RtkLostAlarm` | bool | true | Sound alarm when RTK lost |
| `RtkLostAction` | enum | Warn | Action when RTK lost (Warn/Pause/Stop) |
| `MaxAge` | double | 5.0 | Max differential age in seconds |
| `MaxHdop` | double | 2.0 | Max horizontal dilution of precision |

### UI Elements
- Fix quality threshold selector (dropdown or radio)
- Alarm enable toggle
- RTK lost action selector
- Max age input
- Max HDOP input
- Current status display (satellites, fix quality, age)

### Graphic
- `Con_SourcesRTKAlarm.png` - RTK alarm indicator

---

## Sub-Section 4: Data Rate & Connection

### Purpose
Configure GPS data rate and connection monitoring.

### Settings (from ConnectionConfig)
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `GpsUpdateRate` | int | 10 | Expected GPS update rate in Hz |
| `UseRtk` | bool | true | Whether RTK corrections are expected |

### Additional Settings to Add
| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `GpsTimeoutMs` | int | 300 | Timeout before marking GPS as lost |
| `ImuTimeoutMs` | int | 300 | Timeout before marking IMU as lost |

### UI Elements
- GPS update rate selector (5Hz, 10Hz, 20Hz options)
- RTK expected toggle
- Timeout configuration (advanced)
- Connection status indicators

---

## UI Layout Design

Following the established pattern (graphics on left, settings on right), organized as a scrollable single page or sub-tabs:

```
┌─────────────────────────────────────────────────────────────────┐
│ GPS / Data Sources Configuration                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  GPS MODE                                                       │
│  ┌────────────────────┐  ┌────────────────────┐                │
│  │ [Single Antenna]   │  │ [Dual Antenna]     │                │
│  │ ┌──────────────┐   │  │ ┌──────────────┐   │                │
│  │ │   📡         │   │  │ │  📡    📡   │   │                │
│  │ │   🚜         │   │  │ │     🚜      │   │                │
│  │ └──────────────┘   │  │ └──────────────┘   │                │
│  │ Heading from IMU   │  │ Heading from GPS   │                │
│  │ or calculation     │  │ antenna baseline   │                │
│  └────────────────────┘  └────────────────────┘                │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  HEADING SOURCE                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ [160x120 Head Graphic]  │  Primary Heading Source       │   │
│  │                         │  ○ GPS (calculated)           │   │
│  │                         │  ○ Dual GPS (baseline)        │   │
│  │                         │  ○ IMU (inertial)             │   │
│  │                         │                               │   │
│  │                         │  Fusion Weight: [███░░] 0.5   │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ☑ Use dual GPS as virtual IMU                                 │
│  ☑ Auto-switch on fix quality     Speed: [0.5 km/h]           │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  RTK MONITORING                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ [RTK Alarm Graphic]  │  Minimum Fix Quality             │   │
│  │                      │  [RTK Fixed (4) ▼]               │   │
│  │                      │                                  │   │
│  │                      │  ☑ Alarm when RTK lost           │   │
│  │                      │  Action: [Warn ▼]                │   │
│  │                      │                                  │   │
│  │                      │  Max Diff Age: [5.0 sec]         │   │
│  │                      │  Max HDOP: [2.0]                 │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  DATA RATE                                                      │
│  GPS Update Rate: [● 5Hz  ○ 10Hz  ○ 20Hz]                      │
│  ☑ RTK corrections expected                                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Implementation Changes

### 1. Model Updates

#### Add to ConnectionConfig.cs
```csharp
// GPS Mode
private bool _isDualGps;
public bool IsDualGps
{
    get => _isDualGps;
    set => this.RaiseAndSetIfChanged(ref _isDualGps, value);
}

// RTK Monitoring
private int _minFixQuality = 4;
public int MinFixQuality
{
    get => _minFixQuality;
    set => this.RaiseAndSetIfChanged(ref _minFixQuality, value);
}

private bool _rtkLostAlarm = true;
public bool RtkLostAlarm
{
    get => _rtkLostAlarm;
    set => this.RaiseAndSetIfChanged(ref _rtkLostAlarm, value);
}

private int _rtkLostAction; // 0=Warn, 1=Pause, 2=Stop
public int RtkLostAction
{
    get => _rtkLostAction;
    set => this.RaiseAndSetIfChanged(ref _rtkLostAction, value);
}

private double _maxDifferentialAge = 5.0;
public double MaxDifferentialAge
{
    get => _maxDifferentialAge;
    set => this.RaiseAndSetIfChanged(ref _maxDifferentialAge, value);
}

private double _maxHdop = 2.0;
public double MaxHdop
{
    get => _maxHdop;
    set => this.RaiseAndSetIfChanged(ref _maxHdop, value);
}

private int _gpsTimeoutMs = 300;
public int GpsTimeoutMs
{
    get => _gpsTimeoutMs;
    set => this.RaiseAndSetIfChanged(ref _gpsTimeoutMs, value);
}
```

### 2. ConfigurationViewModel Commands

Add edit commands:
- `ToggleDualGpsCommand`
- `SelectHeadingSourceCommand` (with source parameter)
- `EditFusionWeightCommand`
- `EditMinFixQualityCommand`
- `ToggleRtkAlarmCommand`
- `EditRtkLostActionCommand`
- `EditMaxDiffAgeCommand`
- `EditMaxHdopCommand`
- `EditGpsUpdateRateCommand`

### 3. UI Implementation

Rewrite `SourcesConfigTab.axaml` with:
- GPS Mode card selection (single/dual)
- Heading source radio group
- RTK monitoring settings with graphic
- Data rate configuration

---

## Graphics Available

All graphics already exist in `Assets/Icons/Config/`:

| File | Purpose | Display Size |
|------|---------|--------------|
| `Con_SourcesGPSSingle.png` | Single antenna mode | 120x80 (card) |
| `Con_SourcesGPSDual.png` | Dual antenna mode | 120x80 (card) |
| `Con_SourcesHead.png` | Heading/antenna positioning | 160x120 |
| `Con_SourcesRTKAlarm.png` | RTK alarm indicator | 80x80 |
| `Con_SourcesMenu.png` | Menu icon (not needed) | - |

---

## Settings Specification

### GPS Mode
- **Type:** bool (IsDualGps)
- **Default:** false (single antenna)
- **Description:** Whether dual GPS antennas are used for heading determination

### Heading Source (computed from settings)
- Single GPS: IsDualGps=false, no IMU
- Dual GPS: IsDualGps=true
- IMU: Has IMU hardware
- Fusion: FusionWeight between 0-1

### Minimum Fix Quality
- **Type:** int
- **Range:** 1-5
- **Default:** 4 (RTK Fixed)
- **Options:** GPS(1), DGPS(2), RTK Float(5), RTK Fixed(4)

### RTK Lost Action
- **Type:** enum
- **Options:** Warn (0), Pause AutoSteer (1), Stop Sections (2)
- **Default:** Warn

### GPS Update Rate
- **Type:** int
- **Options:** 5, 10, 20 Hz
- **Default:** 10

---

## AgOpenGPS XML Mapping

| Setting | XML Key | Notes |
|---------|---------|-------|
| IsDualGps | `setGPS_isDualAsDingle` | Note: typo in original |
| FusionWeight | `setIMU_fusionWeight` | 0.0-1.0 |
| MinFixQuality | `setGPS_minFixQuality` | 1-5 |
| MaxDiffAge | `setGPS_maxAge` | seconds |
| GpsUpdateRate | `setGPS_Hz` | 5/10/20 |

---

## Files to Modify

| File | Changes |
|------|---------|
| `SourcesConfigTab.axaml` | Complete rewrite with new layout |
| `ConnectionConfig.cs` | Add GPS mode, RTK monitoring settings |
| `ConfigurationViewModel.cs` | Add edit commands |
| `VehicleProfileService.cs` | Parse/save GPS settings from XML |

---

## Implementation Phases

### Phase 1: Model Updates
1. Add new properties to ConnectionConfig.cs
2. Add XML parsing/saving to VehicleProfileService.cs

### Phase 2: ViewModel Commands
1. Add toggle/edit commands to ConfigurationViewModel.cs
2. Implement command handlers

### Phase 3: UI Implementation
1. Rewrite SourcesConfigTab.axaml
2. GPS mode card selection
3. Heading source configuration
4. RTK monitoring section
5. Data rate section

### Phase 4: Rename Tab
1. Consider renaming from "Data Sources" to "GPS" to match AgOpenGPS
2. Update any references

---

## Complexity Assessment

| Section | Complexity | Notes |
|---------|------------|-------|
| GPS Mode | Low | Two-card toggle |
| Heading Source | Medium | Radio group + conditional options |
| RTK Monitoring | Medium | Multiple inputs + dropdown |
| Data Rate | Low | Radio buttons + toggle |

**Overall:** Medium complexity - straightforward settings without complex interdependencies like Tool Sections.

---

## Testing Checklist

- [ ] GPS mode toggle updates IsDualGps correctly
- [ ] Dual GPS options only enabled when dual mode selected
- [ ] Heading source selection persists
- [ ] Fusion weight slider works (0.0-1.0 range)
- [ ] RTK alarm toggle works
- [ ] Fix quality dropdown works
- [ ] RTK lost action dropdown works
- [ ] Max diff age editable
- [ ] Max HDOP editable
- [ ] GPS update rate selection works
- [ ] Settings save/load from profile
- [ ] Settings compatible with AgOpenGPS XML format
- [ ] Graphics display correctly

---

## Notes

### Tab Naming
The tab is currently called "Data Sources" but AgOpenGPS calls it "GPS". Consider renaming to "GPS" for consistency. The graphics use "Sources" prefix suggesting the original AgOpen name may have been "Sources".

### Connection vs Configuration
NTRIP connection settings are handled separately in the NTRIP dialog. This tab focuses on GPS receiver configuration, not network connections.

### Future Enhancement
Consider adding a live status display showing current:
- Fix quality
- Satellite count
- HDOP
- Differential age
- GPS/IMU data flow indicators

This would make the tab both configuration AND monitoring.
