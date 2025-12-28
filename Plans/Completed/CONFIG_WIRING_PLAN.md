# Configuration Wiring Plan

This document tracks the wiring of UI configuration settings to AgValonia backend services.

## ✅ Architecture Status (Complete)

The core wiring architecture is **fully implemented**:

1. **ConfigurationStore** - Singleton with reactive config models (VehicleConfig, ToolConfig, GuidanceConfig, etc.)
2. **Config Models** - All use `ReactiveObject` with `RaiseAndSetIfChanged` for automatic UI updates
3. **ConfigurationViewModel** - Exposes config via `Config => _configService.Store` with convenience accessors
4. **MainViewModel** - Reads from `ConfigurationStore.Instance` for guidance calculations
5. **ConfigurationService** - Handles profile load/save via `ApplyProfileToStore()` / `CreateProfileFromStore()`

**Data flow:**
```
UI Tap → EditCommand → ShowNumericInput → Callback sets property →
RaiseAndSetIfChanged → UI updates automatically → Services read on next tick
```

---

## UI Reorganization Notes

**Data I/O Dialog Reorganization** (Planned):
- Current `DataIODialogPanel` will be split:
  - UDP Communication + Module Connections → **Module Monitoring Panel** (status bar access)
  - GPS Data → **GPS Data Panel** (status bar access)
  - NTRIP Configuration → **Data Sources Tab** in config dialog (implemented)
- Status bar will provide quick access to monitoring panels via clickable indicators

**Status Bar** (Planned):
- RTK Fix indicator → opens NTRIP quick panel / reconnect
- Module status → opens Module Monitoring panel
- GPS quality/HDOP → opens GPS Data panel
- Area worked → opens stats

## Architecture Overview

All configuration flows through **ConfigurationStore** (singleton):
```
ConfigurationStore.Instance
├── Vehicle      (VehicleConfig)
├── Tool         (ToolConfig)
├── Guidance     (GuidanceConfig)
├── Display      (DisplayConfig)
├── Connection   (ConnectionConfig)
├── Machine      (MachineConfig)
├── Ahrs         (AhrsConfig)
└── Simulator    (SimulatorConfig)
```

Services access configuration via `ConfigurationStore.Instance.SubConfig.Property`.

---

## Tab-by-Tab Wiring Checklist

### 1. Vehicle Tab → VehicleConfig ✅ Wired
**File**: `VehicleConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Vehicle Type | `Vehicle.Type` | Diagram display | ✅ |
| Wheelbase | `Vehicle.Wheelbase` | TrackGuidanceService, YouTurnGuidanceService | ✅ |
| Track Width | `Vehicle.TrackWidth` | Geometry calculations | ✅ |
| Antenna Height | `Vehicle.AntennaHeight` | GPS offset corrections | ✅ |
| Antenna Pivot | `Vehicle.AntennaPivot` | GPS position projection | ✅ |
| Antenna Offset | `Vehicle.AntennaOffset` | GPS lateral correction | ✅ |
| Max Steer Angle | `Vehicle.MaxSteerAngle` | TrackGuidanceService steering limits | 🔶 No UI |
| Max Angular Velocity | `Vehicle.MaxAngularVelocity` | Yaw rate limiting | 🔶 No UI |

**Wiring Notes**:
- ✅ All bindings use ReactiveUI - changes propagate automatically
- ✅ MainViewModel reads `Vehicle.Wheelbase`, `Vehicle.MaxSteerAngle` for guidance input
- ✅ ConfigurationService saves/loads all vehicle properties to profile
- 🔶 MaxSteerAngle and MaxAngularVelocity need edit commands and UI (advanced settings)

---

### 2. Tool Tab → ToolConfig ✅ Wired
**File**: `ToolConfigTab.axaml` (with sub-tabs: Type, Hitch, Timing, Offset, Pivot, Sections, Switches)

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Tool Width | `Tool.Width` | Section control, Tramline | ✅ |
| Overlap | `Tool.Overlap` | Section overlap compensation | ✅ |
| Lateral Offset | `Tool.Offset` | Tool lateral positioning | ✅ |
| Tool Type (4 modes) | `Tool.IsToolTrailing`, etc. | Hitch geometry | ✅ |
| Hitch Length | `Tool.HitchLength` | Tool position tracking | ✅ |
| Trailing Hitch | `Tool.TrailingHitchLength` | TBT tool geometry | ✅ |
| Look Ahead On | `Tool.LookAheadOnSetting` | Section auto-on distance | ✅ |
| Look Ahead Off | `Tool.LookAheadOffSetting` | Section auto-off distance | ✅ |
| Turn Off Delay | `Tool.TurnOffDelay` | Section shutoff timing | ✅ |
| Number of Sections | `NumSections` | Section control | ✅ |
| Section Widths | `Tool.SectionWidths[]` | Individual section sizes | ✅ |
| Zone Ranges | `Tool.ZoneRanges[]` | Zone grouping | ✅ |

**Wiring Notes**:
- ✅ Tool type selection uses RadioButtons with two-way binding
- ✅ All numeric values have edit commands (ShowNumericInput pattern)
- ✅ ConfigurationService saves/loads all tool properties to profile

---

### 3. U-Turn Tab → GuidanceConfig ✅ Wired
**File**: `UTurnConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Turn Radius | `Guidance.UTurnRadius` | YouTurnGuidanceService | ✅ |
| Extension Length | `Guidance.UTurnExtension` | Entry/exit leg length | ✅ |
| Distance from Boundary | `Guidance.UTurnDistanceFromBoundary` | YouTurnCreationService | ✅ |
| U-Turn Style | `Guidance.UTurnStyle` | Path generation (0=normal, 1=K) | 🔶 No UI |
| Smoothing | `Guidance.UTurnSmoothing` | Spline smoothing (1-50) | ✅ |
| Compensation | `Guidance.UTurnCompensation` | Steering compensation | 🔶 No UI |
| Skip Width | `Guidance.UTurnSkipWidth` | Row skip on return | 🔶 Command exists |

**Wiring Notes**:
- ✅ Edit commands exist for all settings in ConfigurationViewModel
- ✅ UI shows Radius, Extension, Distance, Smoothing with inline graphics
- 🔶 UTurnStyle, UTurnCompensation, UTurnSkipWidth commands exist but not exposed in UI

---

### 4. Machine Control Tab → MachineConfig ✅ Fully Wired
**Files**: `MachineControlConfigTab.axaml`, `MachineModuleSubTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Machine Module On/Off | `Machine.MachineModuleEnabled` | ModuleCommunicationService | 🔶 UI Needed |
| Raise Time | `Machine.RaiseTime` | ModuleCommunicationService | ✅ |
| Lower Time | `Machine.LowerTime` | ModuleCommunicationService | ✅ |
| Look Ahead | `Machine.LookAhead` | ModuleCommunicationService | ✅ |
| Invert Relay | `Machine.InvertRelay` | ModuleCommunicationService | ✅ |
| Pin Assignments (24) | `Machine.PinAssignments[]` | ConfigurationViewModel | ✅ |
| User Values (1-4) | `Machine.User1Value`, etc. | ModuleCommunicationService | ✅ |
| Alarm Stops AutoSteer | `Ahrs.AlarmStopsAutoSteer` | ModuleCommunicationService | ✅ |

**Verification Notes** (2024-12):
- ✅ MachineConfig model exists with all properties
- ✅ ModuleCommunicationService.cs exists (migrated from AgOpenGPS)
- ✅ IModuleCommunicationService interface exists
- ✅ Service registered in DI container (Desktop, iOS, Android)
- ✅ Service injected into MainViewModel
- ✅ Service reads ToolConfig (work switch settings) from ConfigurationStore
- ✅ Service reads MachineConfig (hydraulic settings, user values) from ConfigurationStore
- ✅ Service reads AhrsConfig (AlarmStopsAutoSteer) from ConfigurationStore
- ✅ Event handlers connected for AutoSteerToggle and SectionMasterToggle
- ✅ Pin assignments wired via ConfigurationViewModel (Pin1Function through Pin24Function)
- ✅ User values (1-4) accessible via ModuleCommunicationService

---

### 5. Tram Lines Tab → GuidanceConfig ✅ Pattern OK
**File**: `TramConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Tram Lines Enabled | `Guidance.TramLinesEnabled` | TramlineService | ✅ |
| Tram Line Style | `Guidance.TramLineStyle` | Rendering style | ✅ |
| Tram Passes | `Guidance.TramPasses` | Pass count between trams | ✅ |
| Seed Tram | `Guidance.SeedTram` | Seed drill mode | ✅ |
| Half Width Mode | `Guidance.TramHalfWidth` | Half-width tram mode | ✅ |
| Outer Tram | `Guidance.TramOuter` | Outer tram offset | ✅ |

**Verification Notes** (2024-12):
- ✅ GuidanceConfig has all tram properties
- ✅ TramlineService is pure computation - caller passes config values
- ✅ EditTramPassesCommand etc. exist in ConfigurationViewModel
- ✅ Pattern is intentional: service receives params, doesn't read config directly

---

### 6. Data Sources Tab → ConnectionConfig
**Files**: `SourcesConfigTab.axaml`, `GpsSubTab.axaml`, `NtripSubTab.axaml`, `RollSubTab.axaml`

#### GPS Settings (GpsSubTab) ✅ Fully Wired
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Dual GPS Mode | `Connection.IsDualGps` | NmeaParserService | ✅ |
| GPS Update Rate | `Connection.GpsUpdateRate` | UI display only | ✅ |
| Min Fix Quality | `Connection.MinFixQuality` | NmeaParserService | ✅ |
| Max HDOP | `Connection.MaxHdop` | NmeaParserService | ✅ |
| Max Differential Age | `Connection.MaxDifferentialAge` | NmeaParserService | ✅ |
| Dual Heading Offset | `Connection.DualHeadingOffset` | NmeaParserService | ✅ |
| Dual Switch Speed | `Connection.DualSwitchSpeed` | NmeaParserService | ✅ |
| Single Min Step | `Connection.MinGpsStep` | NmeaParserService | ✅ |
| Fix-to-Fix Distance | `Connection.FixToFixDistance` | NmeaParserService | ✅ |
| Heading Fusion Weight | `Connection.HeadingFusionWeight` | NmeaParserService | ✅ |

**Verification Notes** (2024-12):
- ✅ ConnectionConfig has all GPS properties defined
- ✅ GpsService.cs exists (minimal - receives data and fires events)
- ✅ NmeaParserService.cs reads from ConfigurationStore.Instance.Connections
- ✅ MinFixQuality, MaxHdop, MaxDifferentialAge filtering implemented
- ✅ FixQualityBelowMinimum event raised for UI notification
- ✅ ConsecutiveBadFixes counter tracks rejected fixes
- ✅ GpsData.IsValid can be overridden by parser for quality filtering
- ✅ Dual GPS heading with DualHeadingOffset applied
- ✅ DualSwitchSpeed threshold for using fix-to-fix at low speed
- ✅ Single antenna fix-to-fix heading calculation
- ✅ HeadingFusionWeight blending with IMU heading (SensorState)
- ✅ GpsUpdateRate available for UI display (not rate limiting)
- ✅ IMU data (roll, pitch, yaw rate) parsed to SensorState

#### NTRIP Settings (NtripSubTab) ✅ Fully Wired
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Caster Host | `Connection.NtripCasterHost` | NtripClientService | ✅ |
| Caster Port | `Connection.NtripCasterPort` | NtripClientService | ✅ |
| Mount Point | `Connection.NtripMountPoint` | NtripClientService | ✅ |
| Username | `Connection.NtripUsername` | NtripClientService | ✅ |
| Password | `Connection.NtripPassword` | NtripClientService | ✅ |
| Auto Connect | `Connection.NtripAutoConnect` | App startup | ✅ |
| Connect/Disconnect | N/A | NtripClientService | ✅ |
| Connection Status | `IsNtripConnected` | Live from service | ✅ |
| RTCM Bytes | `NtripBytesReceived` | Live from service | ✅ |

**NTRIP Wiring Notes**:
- ✅ Text input overlay for string fields (host, mount, user, password)
- ✅ Numeric input for port
- ✅ Live connection with Connect/Disconnect buttons
- ✅ Real-time status indicator and RTCM byte counter

#### RTK Monitoring
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| RTK Lost Alarm | `Connection.RtkLostAlarm` | Alert system | ⬜ |
| RTK Lost Action | `Connection.RtkLostAction` | AutoSteerService | ⬜ |
| Max Differential Age | `Connection.MaxDifferentialAge` | RTK quality check | ⬜ |
| Max HDOP | `Connection.MaxHdop` | Position quality filter | ⬜ |

#### AgShare Cloud
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| AgShare Server | `Connection.AgShareServer` | Cloud sync | ⬜ |
| AgShare API Key | `Connection.AgShareApiKey` | Authentication | ⬜ |
| AgShare Enabled | `Connection.AgShareEnabled` | Cloud sync toggle | ⬜ |

**Wiring Notes**:
- NTRIP settings build `NtripConfiguration` object for service
- RTK Lost Action: 0=Warn only, 1=Pause steering, 2=Stop steering
- GPS update rate affects guidance responsiveness

---

### 7. Display Tab → DisplayConfig ✅ Config Wiring Complete
**File**: `DisplayConfigTab.axaml`

| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Grid Visible | `Display.GridVisible` | DisplaySettingsService → Map | ✅ Wired |
| Day/Night Mode | `Display.IsDayMode` | DisplaySettingsService → UI | ✅ Wired |
| Camera Pitch | `Display.CameraPitch` | DisplaySettingsService → Map | ✅ Wired |
| 2D/3D Mode | `Display.Is2DMode` | DisplaySettingsService → Map | ✅ Wired |
| North Up | `Display.IsNorthUp` | DisplaySettingsService → Map | ✅ Wired |
| Camera Zoom | `Display.CameraZoom` | Window state persistence | ✅ Wired |
| Polygons Visible | `Display.PolygonsVisible` | Future map feature | ✅ Config ready |
| Speedometer Visible | `Display.SpeedometerVisible` | Future UI overlay | ✅ Config ready |
| Keyboard Enabled | `Display.KeyboardEnabled` | Future input handling | ✅ Config ready |
| Headland Distance | `Display.HeadlandDistanceVisible` | Future UI overlay | ✅ Config ready |
| Auto Day/Night | `Display.AutoDayNight` | Future time-based theme | ✅ Config ready |
| Svenn Arrow | `Display.SvennArrowVisible` | Future map feature | ✅ Config ready |
| Start Fullscreen | `Display.StartFullscreen` | Future window startup | ✅ Config ready |
| Elevation Log | `Display.ElevationLogEnabled` | Future data logging | ✅ Config ready |
| Field Texture | `Display.FieldTextureVisible` | Future map feature | ✅ Config ready |
| Extra Guidelines | `Display.ExtraGuidelines` | Future map feature | ✅ Config ready |
| Guidelines Count | `Display.ExtraGuidelinesCount` | Future map feature | ✅ Config ready |
| Line Smooth | `Display.LineSmoothEnabled` | Future map feature | ✅ Config ready |
| Direction Markers | `Display.DirectionMarkersVisible` | Future map feature | ✅ Config ready |
| Section Lines | `Display.SectionLinesVisible` | Future map feature | ✅ Config ready |
| Units (Metric/Imperial) | `IsMetric` | All display conversions | ✅ Wired |

**Verification Notes** (2024-12):
- ✅ DisplaySettingsService delegates to ConfigurationStore.Instance.Display
- ✅ MainViewModel forwards display properties to/from DisplaySettingsService
- ✅ Grid, Day/Night, Camera, View mode fully wired and functional
- ✅ All settings have UI bindings (DisplayConfigTab.axaml)
- ✅ All settings have toggle commands in ConfigurationViewModel
- ✅ All settings persist via ConfigurationService (AppSettings)
- ⬜ Future features: Map rendering features (ExtraGuidelines, DirectionMarkers, etc.) are not yet implemented in DrawingContextMapControl

---

### 8. Additional Options Tab → DisplayConfig ✅ Config Wiring Complete
**File**: `AdditionalOptionsConfigTab.axaml`

#### Screen Buttons
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| U-Turn Button | `Display.UTurnButtonVisible` | Future main UI | ✅ Config ready |
| Lateral Button | `Display.LateralButtonVisible` | Future main UI | ✅ Config ready |

#### Sounds
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Auto Steer Sound | `Display.AutoSteerSound` | Future audio service | ✅ Config ready |
| U-Turn Sound | `Display.UTurnSound` | Future audio service | ✅ Config ready |
| Hydraulic Sound | `Display.HydraulicSound` | Future audio service | ✅ Config ready |
| Sections Sound | `Display.SectionsSound` | Future audio service | ✅ Config ready |

#### Hardware
| Setting | Property | Service(s) | Status |
|---------|----------|------------|--------|
| Hardware Messages | `Display.HardwareMessagesEnabled` | Future status display | ✅ Config ready |

**Verification Notes** (2024-12):
- ✅ All settings have UI bindings (AdditionalOptionsConfigTab.axaml)
- ✅ All settings have toggle commands in ConfigurationViewModel
- ✅ All settings persist via ConfigurationService (AppSettings)
- ⬜ Audio service not yet implemented (sounds cannot play)
- ⬜ Button visibility not yet controlling actual main UI buttons

---

## Implementation Priority

### Phase 1: Core Guidance (Critical for field operation) ✅ Complete
1. ✅ Vehicle Tab → VehicleConfig (wheelbase, antenna)
2. ✅ Tool Tab → ToolConfig (width, sections)
3. ✅ U-Turn Tab → GuidanceConfig (turn parameters)

### Phase 2: Data Sources (Required for GPS/RTK) ✅ Complete
4. ✅ NTRIP → ConnectionConfig (fully wired with live connection)
5. ✅ GPS Quality Filtering → ConnectionConfig (MinFixQuality, MaxHdop, MaxDifferentialAge)
6. ✅ GPS Heading Processing → ConnectionConfig (Dual GPS, fix-to-fix, heading fusion)
7. ✅ GPS Update Rate → ConnectionConfig (display/informational)
8. ⬜ RTK Monitoring → ConnectionConfig (UI for RTK lost alarm/action)

### Phase 3: Machine Control (Hardware integration) ✅ Complete
8. ✅ Machine Control Tab → MachineConfig (hydraulics via ModuleCommunicationService)
9. ✅ Work Switch / Steer Switch → ToolConfig (via ModuleCommunicationService)
10. ✅ Pin Assignments → MachineConfig (via ConfigurationViewModel)
11. ✅ User Values → MachineConfig (via ModuleCommunicationService)
12. ✅ AlarmStopsAutoSteer → AhrsConfig (via ModuleCommunicationService)
13. ✅ Tram Lines Tab → GuidanceConfig (pure computation pattern)

### Phase 4: Display & Polish ✅ Config Wiring Complete
14. ✅ Display Tab → DisplayConfig (core settings wired, future features ready)
15. ✅ Additional Options Tab → DisplayConfig (sounds, buttons config ready)

**Phase 4 Notes** (2024-12):
- All display settings have complete UI bindings and toggle commands
- Core settings (Grid, Day/Night, Camera, View mode) fully functional
- Future map features (ExtraGuidelines, DirectionMarkers, etc.) have config infrastructure ready
- Future features (audio service, UI button visibility) have config infrastructure ready

---

## Wiring Pattern

For each setting, the wiring involves:

### 1. ViewModel Property
Ensure ConfigurationViewModel has accessor:
```csharp
// Direct access via ConfigurationStore
public VehicleConfig Vehicle => ConfigurationStore.Instance.Vehicle;
public double Wheelbase => Vehicle.Wheelbase;
```

### 2. XAML Binding
Bind control to property with numeric input support:
```xml
<Button Command="{Binding OpenNumericInputCommand}"
        CommandParameter="Vehicle.Wheelbase|Wheelbase|m|0.5|10|2"/>
```

### 3. Service Access
Services read from ConfigurationStore:
```csharp
var wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
```

### 4. Profile Persistence
VehicleProfileService saves/loads config:
```csharp
profile.Vehicle.Wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
```

---

## Current State Summary (Updated 2024-12)

| Tab | UI Complete | Bindings | Services | Profile Save |
|-----|-------------|----------|----------|--------------|
| Vehicle | ✅ | ✅ | ✅ | ✅ |
| Tool | ✅ | ✅ | ✅ | ✅ |
| U-Turn | ✅ | ✅ | ✅ | ✅ |
| Machine Control | ✅ | ✅ | ✅ Fully wired | ✅ |
| Tram Lines | ✅ | ✅ | ✅ Pure compute | ✅ |
| Data Sources (NTRIP) | ✅ | ✅ | ✅ | ✅ |
| Data Sources (GPS) | ✅ | ✅ | ✅ Fully wired | ✅ |
| Display | ✅ | ✅ | ✅ Core wired | ✅ Core |
| Additional Options | ✅ | 🔶 | ⬜ | ⬜ |

**Legend**: ✅ Complete | 🔶 Partial | ⬜ Not Started/Missing

---

## Remaining Work (Updated 2024-12)

### ✅ Complete - Core Guidance (Phase 1)
- [x] Vehicle Tab - fully wired, MainViewModel reads from ConfigurationStore
- [x] Tool Tab - fully wired, all edit commands exist
- [x] U-Turn Tab - core settings wired (Style/Compensation/SkipWidth commands exist but no UI)
- [x] NTRIP - fully wired with live connection, status, byte counter

### ✅ Complete - GPS Processing (All Features)
- [x] NmeaParserService reads MinFixQuality, MaxHdop, MaxDifferentialAge from ConfigurationStore
- [x] Fixes rejected if quality below minimum, HDOP too high, or differential age too old
- [x] FixQualityBelowMinimum event for UI notification
- [x] ConsecutiveBadFixes counter tracks rejected fixes
- [x] GpsData.IsValid can be overridden by parser for quality filtering
- [x] Dual GPS heading with DualHeadingOffset applied
- [x] DualSwitchSpeed threshold switches to fix-to-fix at low speeds
- [x] Single antenna fix-to-fix heading calculation using MinGpsStep and FixToFixDistance
- [x] HeadingFusionWeight blending between GPS and IMU headings
- [x] GpsUpdateRate available for UI display
- [x] IMU data (roll, pitch, yaw rate) parsed from PANDA sentence to SensorState

### ✅ Complete - Module Communication Service (All Features)
- [x] Service file exists: `ModuleCommunicationService.cs`
- [x] Interface exists: `IModuleCommunicationService.cs`
- [x] Registered in DI container (Desktop, iOS, Android)
- [x] Injected into MainViewModel
- [x] Reads work switch settings from ToolConfig (IsWorkSwitchActiveLow, IsWorkSwitchEnabled, etc.)
- [x] Reads hydraulic timing from MachineConfig (RaiseTime, LowerTime, LookAhead, InvertRelay)
- [x] Reads user values from MachineConfig (User1Value through User4Value)
- [x] Reads AlarmStopsAutoSteer from AhrsConfig
- [x] AutoSteerToggleRequested and SectionMasterToggleRequested events connected
- [x] Pin assignments wired via ConfigurationViewModel (Pin1Function through Pin24Function)

### ✅ OK - Tram Lines (Pure Computation Pattern)
TramlineService intentionally receives parameters rather than reading config - caller passes values

### ✅ OK - Display Settings (Core)
DisplaySettingsService properly delegates to ConfigurationStore.Instance.Display

---

## Notes

- ✅ All config models use ReactiveUI (`RaiseAndSetIfChanged`) for automatic UI updates
- ✅ ConfigurationViewModel properly exposes config via `Config => _configService.Store`
- ✅ MainViewModel uses `ConfigurationStore.Instance` for guidance input
- ✅ Profile persistence via ConfigurationService `ApplyProfileToStore()` / `CreateProfileFromStore()`
- ✅ NmeaParserService reads all GPS config (quality, dual, fusion)
- ✅ ModuleCommunicationService reads ToolConfig, MachineConfig, and AhrsConfig
- ✅ Pin assignments (24) fully wired via ConfigurationViewModel
- ✅ User values (1-4) and AlarmStopsAutoSteer wired to ModuleCommunicationService
- ✅ IMU data parsed from PANDA sentences to SensorState singleton
- ✅ Heading fusion blends GPS with IMU when both available
