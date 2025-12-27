# Configuration Settings Audit Plan

## Executive Summary - CRITICAL ISSUES FOUND

### High Priority Fixes Needed:
1. **Antenna Position FIXED** - `AntennaPivot` and `AntennaOffset` now transform GPS position to vehicle pivot in `GpsService.TransformAntennaToPivot()`. `AntennaHeight` not used (terrain compensation low priority).

2. **Tool Width FIXED** - `MainViewModel.cs:1453` now uses `Tool.Width` (was using `Vehicle.TrackWidth`).

3. **Hitch Lengths NOT USED** - `HitchLength`, `TrailingHitchLength`, `TankTrailingHitchLength` stored but never applied. Tool position relative to vehicle not calculated.

4. **Section Control NOT IMPLEMENTED** - All section timing settings (`LookAheadOnSetting`, `LookAheadOffSetting`, `TurnOffDelay`, `NumSections`) stored but no SectionControlService exists.

5. **U-Turn SkipWidth FIXED** - Was using wrong property, now fixed to use `UTurnSkipRows`.

### Settings Status Summary:
| Tab | Status | Critical Issues |
|-----|--------|----------------|
| Vehicle | Audited | 3 antenna settings unused |
| Tool | Audited | Width wrong, hitch unused |
| Sections | Audited | All unused (not implemented) |
| U-Turn | Fixed | Skip width now correct |
| Hardware | Partial | Needs deeper audit |
| Machine Control | Audited | Mostly wired correctly |
| Display | Partial | Needs UI audit |
| Tram | Partial | Needs verification |
| Additional | Partial | Needs verification |
| Sources | Partial | NTRIP OK, others need audit |

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

### 2. Tool Config Tab - AUDITED
- [x] Tool Width - **FIXED** - MainViewModel line 1453 now uses `Tool.Width` (was `Vehicle.TrackWidth`)
- [x] Tool Overlap - OK - Passed correctly to YouTurnCreationInput as `Tool.Overlap`
- [x] Tool Offset - OK - Passed correctly to YouTurnCreationInput as `Tool.Offset`
- [ ] HitchLength - **NOT USED** - Only loaded/saved, never applied to tool position calculations
- [ ] TrailingHitchLength - **NOT USED** - Only loaded/saved
- [ ] TankTrailingHitchLength - **NOT USED** - Only loaded/saved
- [ ] IsToolTrailing/IsToolTBT/etc - **NOT USED** - Tool type flags stored but not used in calculations
- [ ] LookAheadOnSetting/LookAheadOffSetting - Need to verify usage in section control

**ISSUES FOUND:**
1. `toolWidth = Vehicle.TrackWidth` should be `toolWidth = Tool.Width` (line 1453)
2. Hitch lengths not used - tool position not calculated from hitch
3. Tool type flags not affecting behavior

**Key consumers:** YouTurnCreationService (partially), SectionControlService (needs audit)

### 3. Sections Config Tab - AUDITED
- [ ] NumSections - **NOT USED** - Only loaded/saved (section control not implemented)
- [ ] LookAheadOnSetting - **NOT USED** - Only loaded/saved
- [ ] LookAheadOffSetting - **NOT USED** - Only loaded/saved
- [ ] TurnOffDelay - **NOT USED** - Only loaded/saved
- [ ] Section Widths array - **NOT USED** - Only loaded/saved

**ISSUES FOUND:**
Section control is not yet implemented - all section settings are stored but unused.
No SectionControlService exists in the codebase.

**Key consumers:** None currently - SectionControlService needed

### 4. U-Turn Config Tab (PARTIALLY DONE)
- [x] UTurnRadius - Verified used in YouTurnCreationService
- [x] UTurnExtension - Verified used in CreateSimpleUTurnPath
- [x] UTurnDistanceFromBoundary - Verified used in CreateSimpleUTurnPath
- [x] UTurnSmoothing - Verified used in CreateSimpleUTurnPath
- [ ] UTurnSkipWidth - **ISSUE FOUND/FIXED**: Was not being used, runtime UTurnSkipRows used instead

**Key consumers:** YouTurnCreationService, MainViewModel.CreateSimpleUTurnPath

### 5. Hardware Config Tab - AUDITED
- [ ] GPS Port/Baud settings - Need to verify usage in serial comms
- [ ] AHRS settings - Partially used (AlarmStopsAutoSteer via ModuleCommunicationService)
- [ ] Steering hardware settings - Need to verify PID values usage

**Status:** Needs deeper audit of serial/hardware communication

### 6. Machine Control Config Tab - AUDITED
- [x] HydraulicLiftEnabled - Exposed via ModuleCommunicationService (but hydraulic control not fully implemented)
- [x] RaiseTime/LowerTime - Exposed via ModuleCommunicationService
- [x] Work Switch settings - Used in ModuleCommunicationService.CheckSwitches()
- [x] Steer Switch settings - Used in ModuleCommunicationService.CheckSwitches()
- [ ] Pin Assignments - Stored but usage unclear (needs UDP message building)

**Status:** Settings wired to ModuleCommunicationService, but full hydraulic control logic may be incomplete

### 7. Display Config Tab - AUDITED
- [x] IsMetric - Used for unit display conversions
- [ ] Map colors/styles - Need to verify DrawingContextMapControl usage
- [ ] Grid/overlay visibility - Need to verify

**Status:** Needs UI-specific audit

### 8. Tram Config Tab - AUDITED
- [ ] Tram settings - Need to verify TramLineService or guidance usage

**Status:** Tram feature implementation needs verification

### 9. Additional Options Config Tab - AUDITED
- [ ] Sound settings - Need to verify audio playback
- [ ] Screen button visibility - Need to verify UI binding
- [ ] Auto-steer settings - Need to verify guidance usage

**Status:** Needs feature-specific audit

### 10. Sources Config Tab - AUDITED
- [x] NTRIP settings - Used in NtripClientService (Host, Port, MountPoint, Username, Password)
- [ ] UDP ports - Need to verify UdpCommunicationService usage
- [ ] GPS source settings - Need to verify

**Status:** NTRIP verified, other sources need audit

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
