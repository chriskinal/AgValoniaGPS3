# Configuration Settings Audit Plan

## Executive Summary - CRITICAL ISSUES FOUND

### High Priority Fixes - ALL COMPLETE:
1. **Antenna Position FIXED** - `AntennaPivot` and `AntennaOffset` now transform GPS position to vehicle pivot in `GpsService.TransformAntennaToPivot()`. `AntennaHeight` not used (terrain compensation low priority).

2. **Tool Width FIXED** - `MainViewModel.cs:1453` now uses `Tool.Width` (was using `Vehicle.TrackWidth`).

3. **Hitch Lengths FIXED** - `ToolPositionService` now uses all hitch settings:
   - `HitchLength`, `TrailingHitchLength`, `TankTrailingHitchLength`, `TrailingToolToPivotLength`
   - `IsToolTrailing`, `IsToolTBT`, `IsToolRearFixed`, `IsToolFrontFixed`

4. **Section Control IMPLEMENTED** - `SectionControlService` now uses all section settings:
   - `LookAheadOnSetting`, `LookAheadOffSetting`, `TurnOffDelay`, `SlowSpeedCutoff`
   - `IsSectionOffWhenOut`, `GetSectionWidth()`, `GetZoneEndSection()`

5. **U-Turn SkipWidth FIXED** - Was using wrong property, now fixed to use `UTurnSkipRows`.

6. **Tram Lines IMPLEMENTED** - `TramLineService` uses `TramConfig` settings:
   - `TramWidth`, `Passes`, `IsOuterInverted`, plus `Vehicle.TrackWidth`, `Tool.Width`

### Settings Status Summary:
| Tab | Status | Critical Issues |
|-----|--------|----------------|
| Vehicle | ✅ Complete | AntennaHeight unused (terrain compensation low priority) |
| Tool | ✅ Complete | All settings wired via ToolPositionService |
| Sections | ✅ Complete | All settings wired via SectionControlService |
| U-Turn | ✅ Complete | Skip width fixed |
| Hardware | ✅ Audited | AhrsConfig duplicates exist, roll/slope compensation not implemented |
| Machine Control | ✅ Complete | Wired via ModuleCommunicationService |
| Display | ✅ Audited | Core settings wired, many visibility options config-ready but not rendered |
| Tram | ✅ Complete | Wired via TramLineService + TramConfig |
| Additional | ✅ Audited | All config-ready, sounds/keyboard/auto-day-night not implemented |
| Sources | ✅ Audited | Core GPS settings wired, RTK alarm/dual GPS switching not implemented |

---

## Problem Statement

When wiring up configuration panel settings, we discovered that some UI controls update the ConfigurationStore but the services/algorithms continue using local MainViewModel properties or hardcoded values. This creates a disconnect where changing a setting in the config panel has no effect on actual behavior.

**Example found:** The U-Turn skip rows setting in the config panel updated `Guidance.UTurnSkipWidth` in ConfigurationStore, but the U-turn creation logic was reading `UTurnSkipRows` (a separate MainViewModel property controlled by the bottom nav button).

## Audit Scope

### Configuration Tabs to Audit

| Tab | Config File | Status |
|-----|-------------|--------|
| Vehicle | VehicleConfig.cs | Pending |
| Tool | ToolConfig.cs | Pending |
| Sections | (SectionControlConfig?) | Pending |
| U-Turn | GuidanceConfig.cs | **Partially Fixed** |
| Hardware | ConnectionConfig.cs | Pending |
| Machine Control | MachineConfig.cs | Pending |
| Display | DisplayConfig.cs | Pending |
| Tram | GuidanceConfig.cs? | Pending |
| Additional Options | Various | Pending |
| Sources | ConnectionConfig.cs | Pending |

## Audit Process for Each Setting

For each configurable value:

1. **Identify the binding path** - What property does the UI bind to?
   - Example: `{Binding Guidance.UTurnRadius}`

2. **Trace to ConfigurationStore** - Verify the property exists in ConfigurationStore
   - Example: `ConfigurationStore.Instance.Guidance.UTurnRadius`

3. **Find all usages** - Search codebase for where this value SHOULD be used
   - Services, ViewModels, algorithms that need this setting

4. **Verify correct source** - Each usage should read from ConfigurationStore, not:
   - Local ViewModel properties
   - Hardcoded values
   - Duplicate properties with different names

5. **Test the setting** - Change the value in config panel and verify behavior changes

## Settings to Audit by Tab

### 1. Vehicle Config Tab - AUDITED & FIXED
- [ ] Antenna Height - NOT USED (terrain compensation not implemented - low priority)
- [x] Antenna Pivot Distance - **FIXED** - Now transforms GPS position to pivot point in GpsService
- [x] Antenna Offset - **FIXED** - Now applied as lateral offset in GpsService
- [x] Wheelbase - OK - Used via `Vehicle.Wheelbase` which points to ConfigurationStore
- [x] MaxSteerAngle - OK - Used in guidance calculations
- [x] Track Width - OK - Used via `Vehicle.TrackWidth` for U-turn offset calculations
- [x] Vehicle Type - OK - Used for display image selection

**FIXES APPLIED:**
1. `AntennaPivot` - Now transforms GPS antenna position to vehicle pivot point in GpsService.TransformAntennaToPivot()
2. `AntennaOffset` - Now applies lateral offset in GpsService.TransformAntennaToPivot()
3. `AntennaHeight` - Terrain compensation not implemented (low priority - requires roll/pitch data)

**Key consumers:** MainViewModel (guidance), TrackGuidanceService, YouTurnCreationService

### 2. Tool Config Tab - ✅ COMPLETE
- [x] Tool Width - **FIXED** - MainViewModel line 1453 now uses `Tool.Width` (was `Vehicle.TrackWidth`)
- [x] Tool Overlap - OK - Passed correctly to YouTurnCreationInput as `Tool.Overlap`
- [x] Tool Offset - OK - Passed correctly to YouTurnCreationInput as `Tool.Offset`
- [x] HitchLength - **FIXED** - Used by ToolPositionService for fixed tools
- [x] TrailingHitchLength - **FIXED** - Used by ToolPositionService for trailing/TBT
- [x] TankTrailingHitchLength - **FIXED** - Used by ToolPositionService for TBT tank position
- [x] TrailingToolToPivotLength - **FIXED** - Used by ToolPositionService for pivot offset
- [x] IsToolTrailing/IsToolTBT/IsToolRearFixed/IsToolFrontFixed - **FIXED** - All used by ToolPositionService
- [x] LookAheadOnSetting/LookAheadOffSetting - **FIXED** - Used by SectionControlService
- [x] TurnOffDelay - **FIXED** - Used by SectionControlService
- [x] SlowSpeedCutoff - **FIXED** - Used by SectionControlService
- [x] IsSectionOffWhenOut - **FIXED** - Used by SectionControlService
- [x] GetSectionWidth() - **FIXED** - Used by SectionControlService
- [x] GetZoneEndSection() - **FIXED** - Used by SectionControlService

**FIXES APPLIED:**
1. `ToolPositionService` now calculates tool position using all hitch settings
2. `SectionControlService` uses all section timing/lookahead settings

**Key consumers:** ToolPositionService, SectionControlService, YouTurnCreationService

### 3. Sections Config Tab - ✅ COMPLETE
- [x] NumSections - **FIXED** - Used by SectionControlService via `ConfigurationStore.Instance.NumSections`
- [x] LookAheadOnSetting - **FIXED** - Used by SectionControlService for predictive section on
- [x] LookAheadOffSetting - **FIXED** - Used by SectionControlService for predictive section off
- [x] TurnOffDelay - **FIXED** - Used by SectionControlService for section off timing
- [x] Section Widths array - **FIXED** - Used by SectionControlService via `GetSectionWidth()`
- [x] SlowSpeedCutoff - **FIXED** - Used by SectionControlService to disable at low speeds
- [x] IsSectionOffWhenOut - **FIXED** - Used by SectionControlService for boundary behavior
- [x] Zones/ZoneRanges - **FIXED** - Used by SectionControlService via `GetZoneEndSection()`

**FIXES APPLIED:**
`SectionControlService` implemented in `Services/Section/SectionControlService.cs`
All section control settings now properly wired.

**Key consumers:** SectionControlService

### 4. U-Turn Config Tab (PARTIALLY DONE)
- [x] UTurnRadius - Verified used in YouTurnCreationService
- [x] UTurnExtension - Verified used in CreateSimpleUTurnPath
- [x] UTurnDistanceFromBoundary - Verified used in CreateSimpleUTurnPath
- [x] UTurnSmoothing - Verified used in CreateSimpleUTurnPath
- [ ] UTurnSkipWidth - **ISSUE FOUND/FIXED**: Was not being used, runtime UTurnSkipRows used instead

**Key consumers:** YouTurnCreationService, MainViewModel.CreateSimpleUTurnPath

### 5. Hardware Config Tab - ✅ AUDITED
- [x] GPS Port/Baud settings - N/A for UDP-based communication (AgOpenGPS ecosystem uses UDP)
- [x] AlarmStopsAutoSteer - **OK** - Used by ModuleCommunicationService
- [ ] RollZero, RollFilter, IsRollInvert - UI config only, not used by services (roll correction not implemented)
- [ ] FusionWeight (AhrsConfig) - **DUPLICATE** - ConnectionConfig.HeadingFusionWeight is the one being used
- [ ] ForwardCompensation, ReverseCompensation - NOT USED - slope compensation not implemented
- [ ] IsAutoSteerAuto - **MISMATCH** - ModuleCommunicationService uses ModuleSwitchState.IsAutoSteerAuto, not AhrsConfig
- [ ] IsReverseOn, IsDualAsIMU, AutoSwitchDualFixOn, AutoSwitchDualFixSpeed - NOT USED - dual GPS switching not implemented

**Status:** Core alarm setting wired. Roll/slope compensation and dual GPS switching are future features.

### 6. Machine Control Config Tab - AUDITED
- [x] HydraulicLiftEnabled - Exposed via ModuleCommunicationService (but hydraulic control not fully implemented)
- [x] RaiseTime/LowerTime - Exposed via ModuleCommunicationService
- [x] Work Switch settings - Used in ModuleCommunicationService.CheckSwitches()
- [x] Steer Switch settings - Used in ModuleCommunicationService.CheckSwitches()
- [ ] Pin Assignments - Stored but usage unclear (needs UDP message building)

**Status:** Settings wired to ModuleCommunicationService, but full hydraulic control logic may be incomplete

### 7. Display Config Tab - ✅ AUDITED
- [x] GridVisible, IsDayMode, CameraPitch, Is2DMode, IsNorthUp - **OK** - Used by DisplaySettingsService
- [x] Window properties (X, Y, Width, Height, Maximized) - **OK** - Used by ConfigurationService for persistence
- [x] SimulatorPanel properties (X, Y, Visible) - **OK** - Used by ConfigurationService for persistence
- [x] IsMetric - Used for unit display conversions
- [ ] PolygonsVisible, SpeedometerVisible, HeadlandDistanceVisible - Config ready, UI rendering not implemented
- [ ] SvennArrowVisible, DirectionMarkersVisible, SectionLinesVisible - Config ready, UI rendering not implemented
- [ ] ExtraGuidelines, ExtraGuidelinesCount - Config ready, UI rendering not implemented
- [ ] FieldTextureVisible, LineSmoothEnabled - Config ready, UI rendering not implemented
- [ ] UTurnButtonVisible, LateralButtonVisible - Config ready, button visibility not bound

**Status:** Core display settings wired via DisplaySettingsService. Many visibility options are config-ready but not yet rendering in DrawingContextMapControl.

### 8. Tram Config Tab - ✅ COMPLETE
- [x] TramWidth - **FIXED** - Used by TramLineService for tram spacing
- [x] Passes - **FIXED** - Used by TramLineService for pass interval calculation
- [x] IsOuterInverted - **FIXED** - Used by TramLineService for outer/inner swap
- [x] DisplayMode - Available in TramConfig (UI rendering not yet implemented)
- [x] Alpha - Available in TramConfig (UI rendering not yet implemented)
- [x] IsEnabled - Available in TramConfig
- [x] CurrentPass - Available in TramConfig for pass tracking

**FIXES APPLIED:**
1. `TramConfig` added to `ConfigurationStore`
2. `TramLineService` implemented in `Services/Tram/TramLineService.cs`
3. Uses `Vehicle.TrackWidth` and `Tool.Width` from config

**Key consumers:** TramLineService

### 9. Additional Options Config Tab - ✅ AUDITED
- [ ] AutoSteerSound, UTurnSound, HydraulicSound, SectionsSound - Config ready, audio service not implemented
- [ ] KeyboardEnabled - Config ready, keyboard input handling not implemented
- [ ] AutoDayNight - Config ready, time-based theme switching not implemented
- [ ] StartFullscreen - Config ready, window startup logic not implemented
- [ ] ElevationLogEnabled - Config ready, elevation logging not implemented
- [ ] HardwareMessagesEnabled - Config ready, status display not implemented

**Status:** All settings are config-ready but features not yet implemented. Sound playback, keyboard shortcuts, and auto day/night are future features.

### 10. Sources Config Tab - ✅ AUDITED
- [x] NTRIP settings (Host, Port, MountPoint, Username, Password, AutoConnect) - **OK** - Used by NtripClientService via ConfigurationViewModel
- [x] MinFixQuality, MaxHdop, MaxDifferentialAge - **OK** - Used by NmeaParserService for quality filtering
- [x] DualHeadingOffset - **OK** - Used by NmeaParserService for dual antenna heading calculation
- [x] HeadingFusionWeight - **OK** - Used by NmeaParserService for single antenna heading fusion
- [ ] RtkLostAlarm, RtkLostAction - Config ready, RTK alarm/action handling NOT implemented
- [ ] IsDualGps, ReverseDetection - Config ready, not wired to services
- [ ] MinGpsStep, FixToFixDistance - Config ready, filtering not implemented
- [ ] AutoDualFix, DualSwitchSpeed, DualReverseDistance - Config ready, dual GPS switching not implemented
- [x] AgShare settings (Server, ApiKey, Enabled) - **OK** - Used by ConfigurationService for persistence

**Status:** Core GPS quality settings are wired. RTK alarm handling and dual GPS switching are future features.

## Common Issues to Look For

### Issue 1: Duplicate Properties
```csharp
// BAD: MainViewModel has its own property
public double TurnRadius { get; set; }  // Local copy

// GOOD: Read from ConfigurationStore
double turnRadius = ConfigurationStore.Instance.Guidance.UTurnRadius;
```

### Issue 2: Stale Property References
```csharp
// BAD: Using old property that was never wired
RowSkipsWidth = Guidance.UTurnSkipWidth,  // Config panel sets this

// But elsewhere using different property
int rowSkipWidth = UTurnSkipRows;  // Bottom nav sets this - MISMATCH!
```

### Issue 3: Missing ConfigurationStore Access
```csharp
// BAD: Hardcoded or default value
double wheelbase = 2.5;

// GOOD: Read from configuration
double wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
```

### Issue 4: One-Time Read vs Live Update
```csharp
// BAD: Only reads config at startup
_turnRadius = ConfigurationStore.Instance.Guidance.UTurnRadius;

// GOOD: Reads config each time (or subscribes to changes)
double turnRadius = ConfigurationStore.Instance.Guidance.UTurnRadius;
```

## Verification Commands

Search for potential duplicate properties:
```bash
grep -r "public.*TurnRadius" Shared/
grep -r "public.*TrackWidth" Shared/
grep -r "public.*ToolWidth" Shared/
```

Search for hardcoded values that should be configurable:
```bash
grep -rn "= 2.5" Shared/  # Common default values
grep -rn "= 5.0" Shared/
```

Find all ConfigurationStore usages:
```bash
grep -r "ConfigurationStore.Instance" Shared/
```

## Implementation Steps

1. **Create tracking checklist** - Copy settings list above, mark each as verified
2. **Tab-by-tab audit** - Work through one tab at a time
3. **Document findings** - Note any mismatches found
4. **Fix issues** - Update services to read from ConfigurationStore
5. **Test each fix** - Verify config changes affect behavior
6. **Update this document** - Mark settings as verified

## Priority Order

1. **High Priority** - Settings that affect guidance/steering
   - Vehicle dimensions (wheelbase, antenna position)
   - Tool dimensions (width, offset)
   - U-Turn settings
   - Steer PID values

2. **Medium Priority** - Settings that affect field operations
   - Section control settings
   - Headland settings
   - Tram settings

3. **Lower Priority** - Display and convenience settings
   - Units (metric/imperial)
   - UI visibility options
   - Sound settings

## Notes

- The ConfigurationViewModel provides `Vehicle`, `Tool`, `Guidance`, `Display`, `Connection`, `Machine` property groups that map to ConfigurationStore sections
- Services should generally read from ConfigurationStore.Instance directly, not through ViewModel properties
- Some settings may legitimately need runtime overrides (like UTurnSkipRows from bottom nav) - document these cases
