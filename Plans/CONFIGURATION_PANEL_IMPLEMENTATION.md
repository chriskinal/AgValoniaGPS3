# Configuration Panel Implementation Plan

## Overview

The Configuration Panel is the most complex UI component in AgOpenGPS, providing comprehensive vehicle, tool, and system configuration. This document outlines the implementation plan for AgValoniaGPS3, following the established patterns in the codebase.

## AgOpenGPS Reference Structure

Based on research of the AgOpenGPS FormConfig, the configuration system has **8 main categories** with multiple sub-tabs:

### Tab Structure

```
Configuration Dialog
├── 1. Summary (Overview of all settings)
├── 2. Vehicle
│   ├── Config (Vehicle type selection)
│   ├── Antenna (Height, Offset, Pivot Distance)
│   ├── Dimensions (Wheelbase, Track Width)
│   └── Guidance (Steering parameters)
├── 3. Tool
│   ├── Config (Attachment style: Front/Rear/TBT/Trailing)
│   ├── Hitch (Hitch lengths, Drawbar, Tank)
│   ├── Offset (Tool offset, Overlap/Gap)
│   └── Pivot (Pivot length for trailing tools)
├── 4. Sections
│   ├── Sections (Number, widths, positions)
│   ├── Switches (Work/Steer switch config)
│   └── Timing (Look-ahead, Turn-on/off delays)
├── 5. Data Sources
│   ├── Heading (GPS/IMU source, Dual antenna)
│   └── Roll (Roll offset, IMU fusion)
├── 6. U-Turn
│   └── Settings (Radius, Distance, Smoothing)
├── 7. Hardware
│   ├── Relay (Pin configuration)
│   └── Machine (Hydraulic lift, User parameters)
└── 8. Display
    ├── Buttons (Feature toggles)
    └── Display (Units, Visual preferences)
```

## Implementation Architecture

### File Structure

```
Shared/AgValoniaGPS.Views/Controls/Dialogs/
├── ConfigurationDialog.axaml           # Main dialog container
├── ConfigurationDialog.axaml.cs        # Dialog code-behind
└── ConfigTabs/                         # Individual tab controls
    ├── ConfigSummaryTab.axaml
    ├── ConfigVehicleTab.axaml
    ├── ConfigToolTab.axaml
    ├── ConfigSectionsTab.axaml
    ├── ConfigDataSourcesTab.axaml
    ├── ConfigUTurnTab.axaml
    ├── ConfigHardwareTab.axaml
    └── ConfigDisplayTab.axaml

Shared/AgValoniaGPS.ViewModels/
└── ConfigurationViewModel.cs           # Dedicated ViewModel for configuration
```

### Design Decisions

1. **Dedicated ViewModel**: Create `ConfigurationViewModel` instead of bloating `MainViewModel`
2. **Tab Controls as UserControls**: Each major tab is a separate UserControl for maintainability
3. **Modal Dialog Pattern**: Full-screen overlay with centered content panel
4. **Profile Integration**: Direct integration with `IVehicleProfileService`
5. **Two-way Binding**: All settings use TwoWay binding for immediate updates
6. **Cancel/Apply Pattern**: Support discarding changes or applying them

---

## Phase 1: Core Infrastructure

### 1.1 Create ConfigurationViewModel

**File**: `Shared/AgValoniaGPS.ViewModels/ConfigurationViewModel.cs`

```csharp
public class ConfigurationViewModel : ReactiveObject
{
    private readonly IVehicleProfileService _profileService;
    private readonly ISettingsService _settingsService;

    // Current profile being edited (working copy)
    private VehicleProfile? _workingProfile;

    // Tab navigation
    private int _selectedTabIndex;
    private int _selectedSubTabIndex;

    // Profile management
    public ObservableCollection<string> AvailableProfiles { get; }
    public string? SelectedProfileName { get; set; }

    // Vehicle settings (bound to UI)
    public double AntennaHeight { get; set; }
    public double AntennaOffset { get; set; }
    public double AntennaPivot { get; set; }
    public double Wheelbase { get; set; }
    public double TrackWidth { get; set; }
    public VehicleType VehicleType { get; set; }

    // Tool settings
    public double ToolWidth { get; set; }
    public double ToolOverlap { get; set; }
    public double ToolOffset { get; set; }
    public double HitchLength { get; set; }
    public bool IsToolTrailing { get; set; }
    public bool IsToolTBT { get; set; }
    public bool IsToolRearFixed { get; set; }
    public bool IsToolFrontFixed { get; set; }

    // Section settings
    public int NumSections { get; set; }
    public double[] SectionPositions { get; set; } = new double[17];
    public double LookAheadOn { get; set; }
    public double LookAheadOff { get; set; }
    public double TurnOffDelay { get; set; }

    // U-Turn settings
    public double UTurnRadius { get; set; }
    public double UTurnExtension { get; set; }
    public double UTurnDistanceFromBoundary { get; set; }
    public int UTurnSkipWidth { get; set; }

    // Commands
    public ICommand LoadProfileCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand CancelCommand { get; }
}
```

### 1.2 Register Services

**File**: Both `Desktop/DependencyInjection/ServiceCollectionExtensions.cs` and `iOS/DependencyInjection/ServiceCollectionExtensions.cs`

```csharp
services.AddTransient<ConfigurationViewModel>();
```

### 1.3 Create Main Dialog Container

**File**: `Shared/AgValoniaGPS.Views/Controls/Dialogs/ConfigurationDialog.axaml`

Key features:
- Full-screen modal overlay with semi-transparent backdrop
- Left sidebar with main tab buttons (icons + labels)
- Right content area showing selected tab
- Bottom bar with Cancel/Apply buttons
- Profile selector dropdown at top

---

## Phase 2: Tab Implementation (Priority Order)

### 2.1 Vehicle Tab (Highest Priority)

Essential for basic operation. Sub-tabs:

#### 2.1.1 Vehicle Config Sub-tab
- Vehicle type selector (Tractor, Harvester, 4WD, etc.)
- Vehicle icon display based on selection

#### 2.1.2 Antenna Sub-tab
Settings:
| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| Antenna Height | double | 0-10m | 3.0 | Height above ground |
| Antenna Offset | double | -5 to +5m | 0.0 | Left(-)/Right(+) offset |
| Antenna Pivot | double | -10 to +10m | 0.6 | Distance to pivot point |

Visual: Include diagram showing antenna position on vehicle

#### 2.1.3 Dimensions Sub-tab
Settings:
| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| Wheelbase | double | 0.5-10m | 2.5 | Front to rear axle |
| Track Width | double | 0.5-5m | 1.8 | Wheel center to center |

Visual: Include diagram showing measurements

#### 2.1.4 Guidance Sub-tab
Settings:
| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| Max Steer Angle | double | 10-90° | 35 | Maximum steering angle |
| Look Ahead Hold | double | 1-20m | 4.0 | PP look ahead distance |
| Look Ahead Mult | double | 0.5-3.0 | 1.4 | PP multiplier |
| Acquire Factor | double | 1.0-3.0 | 1.5 | Line acquisition factor |

---

### 2.2 Tool Tab (High Priority)

Essential for section control. Sub-tabs:

#### 2.2.1 Tool Config Sub-tab
- Attachment style radio buttons:
  - Front Fixed
  - Rear Fixed
  - TBT (Tow Between Tractor)
  - Trailing

Visual: Diagram showing each attachment style

#### 2.2.2 Hitch Sub-tab
Settings:
| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| Hitch Length | double | -10 to +5m | -1.8 | Distance to hitch point |
| Trailing Hitch | double | -10 to 0m | -2.5 | Trailing hitch length |
| Tank Trailing | double | 0-10m | 3.0 | Tank trailing length |
| Tool to Pivot | double | 0-10m | 0.0 | Trailing tool pivot |

#### 2.2.3 Offset Sub-tab
Settings:
| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| Tool Width | double | 0.5-50m | 6.0 | Working width |
| Tool Offset | double | -5 to +5m | 0.0 | Left/Right offset |
| Overlap | double | -1 to +1m | 0.0 | Overlap (neg = gap) |

---

### 2.3 Sections Tab (High Priority)

For section control functionality. Sub-tabs:

#### 2.3.1 Sections Sub-tab
- Number of sections selector (1-16)
- Section width inputs for each section
- Visual section layout preview
- Total width calculation display

#### 2.3.2 Switches Sub-tab
Settings:
| Setting | Type | Description |
|---------|------|-------------|
| Work Switch | bool | Enable work switch input |
| Steer Switch | bool | Enable steer switch input |
| Section Mode | enum | Auto/Manual mode |

#### 2.3.3 Timing Sub-tab
Settings:
| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| Look Ahead On | double | 0-5s | 1.0 | Time to turn on early |
| Look Ahead Off | double | 0-5s | 0.5 | Time to turn off early |
| Turn Off Delay | double | 0-5s | 0.0 | Delay before turning off |

---

### 2.4 U-Turn Tab (Medium Priority)

For YouTurn functionality:

Settings:
| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| Turn Radius | double | 2-30m | 8.0 | U-turn radius |
| Extension | double | 0-50m | 20.0 | Extension past headland |
| Boundary Distance | double | 0-10m | 2.0 | Distance from boundary |
| Skip Width | int | 1-10 | 1 | Tracks to skip |
| Style | enum | 0-3 | 0 | U-turn style |
| Smoothing | int | 1-50 | 14 | Path smoothing factor |

---

### 2.5 Data Sources Tab (Medium Priority)

#### 2.5.1 Heading Sub-tab
Settings:
| Setting | Type | Description |
|---------|------|-------------|
| Heading Source | enum | GPS/IMU/Dual |
| Dual Antenna | bool | Enable dual antenna |
| Heading Offset | double | Correction offset |
| GPS Step Fix | bool | Enable step fix |

#### 2.5.2 Roll Sub-tab
Settings:
| Setting | Type | Description |
|---------|------|-------------|
| Roll Offset | double | Zero calibration |
| Invert Roll | bool | Reverse roll direction |
| Roll Filter | int | Filter strength |
| IMU Fusion | double | IMU fusion weight |

---

### 2.6 Hardware Tab (Lower Priority)

#### 2.6.1 Relay Sub-tab
- Pin assignment for 24 Arduino pins
- Function selection per pin

#### 2.6.2 Machine Sub-tab
Settings:
| Setting | Type | Description |
|---------|------|-------------|
| Hydraulic Lift | bool | Enable hyd lift |
| Raise Time | double | Lift raise time |
| Lower Time | double | Lift lower time |
| User Param 1-4 | double | Custom values |

---

### 2.7 Display Tab (Lower Priority)

#### 2.7.1 Buttons Sub-tab
Feature toggles for UI elements:
- Field Menu visible
- Tools Menu visible
- Screen Buttons visible
- Tramlines visible
- Headland visible
- Boundary visible
- etc.

#### 2.7.2 Display Sub-tab
Settings:
| Setting | Type | Description |
|---------|------|-------------|
| Units | enum | Metric/Imperial |
| Grid Visible | bool | Show grid |
| Compass Visible | bool | Show compass |
| Speed Visible | bool | Show speedometer |

---

### 2.8 Summary Tab (Lowest Priority)

Read-only overview showing:
- Current profile name
- Vehicle type and dimensions
- Tool configuration summary
- Section count and total width
- Key guidance parameters

---

## Phase 3: Model Updates

### 3.1 Extend VehicleProfile Model

Current `VehicleProfile` needs these additions for full AgOpenGPS compatibility:

```csharp
// Already exists - verify completeness
public class VehicleProfile
{
    // Existing properties...

    // May need to add:
    public bool WorkSwitchEnabled { get; set; }
    public bool SteerSwitchEnabled { get; set; }
    public int SectionMode { get; set; }  // 0=Auto, 1=Manual

    // Hardware settings
    public int[] PinAssignments { get; set; } = new int[24];
    public bool HydraulicLiftEnabled { get; set; }
    public double HydRaiseTime { get; set; }
    public double HydLowerTime { get; set; }
    public double[] UserParameters { get; set; } = new double[4];

    // Data source settings
    public int HeadingSource { get; set; }
    public bool DualAntennaEnabled { get; set; }
    public double HeadingOffset { get; set; }
    public double RollOffset { get; set; }
    public bool InvertRoll { get; set; }
    public int RollFilter { get; set; }
    public double ImuFusionWeight { get; set; }
}
```

### 3.2 Extend VehicleProfileService

Update XML parsing/saving to handle new properties while maintaining AgOpenGPS compatibility.

---

## Phase 4: UI Patterns

### 4.1 Numeric Input Pattern

Use the existing `NumericInputDialogPanel` for tap-to-edit numeric fields:

```xaml
<Button Classes="SettingValueButton"
        Content="{Binding AntennaHeight, StringFormat='{}{0:F2} m'}"
        Command="{Binding EditAntennaHeightCommand}"/>
```

### 4.2 Tab Navigation Pattern

Main tabs as vertical button list:
```xaml
<StackPanel Spacing="4">
    <Button Classes="TabButton"
            Classes.Selected="{Binding SelectedTabIndex, Converter={...}, ConverterParameter=0}"
            Command="{Binding SelectTabCommand}" CommandParameter="0">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Image Source="..." Width="24" Height="24"/>
            <TextBlock Text="Vehicle"/>
        </StackPanel>
    </Button>
    <!-- More tabs... -->
</StackPanel>
```

### 4.3 Sub-tab Navigation Pattern

Horizontal tab strip within content area:
```xaml
<TabControl SelectedIndex="{Binding SelectedSubTabIndex}">
    <TabItem Header="Config"><!-- Content --></TabItem>
    <TabItem Header="Antenna"><!-- Content --></TabItem>
    <!-- More sub-tabs... -->
</TabControl>
```

### 4.4 Settings Row Pattern

Consistent layout for settings:
```xaml
<Grid ColumnDefinitions="*,Auto" Margin="0,4">
    <TextBlock Text="Antenna Height" VerticalAlignment="Center"/>
    <Button Grid.Column="1" Classes="SettingValueButton"
            Content="{Binding AntennaHeight, StringFormat='{}{0:F2} m'}"
            Command="{Binding EditAntennaHeightCommand}"/>
</Grid>
```

---

## Phase 5: Integration Points

### 5.1 MainViewModel Integration

```csharp
// Add to MainViewModel
private bool _isConfigurationDialogVisible;
public bool IsConfigurationDialogVisible
{
    get => _isConfigurationDialogVisible;
    set => this.RaiseAndSetIfChanged(ref _isConfigurationDialogVisible, value);
}

public ICommand ShowConfigurationDialogCommand { get; private set; }
```

### 5.2 ConfigurationPanel Button Wiring

Update existing `ConfigurationPanel.axaml` to launch the dialog:

```xaml
<Button Classes="ModernButton"
        Command="{Binding ShowConfigurationDialogCommand}">
    <StackPanel>
        <Image Source="avares://AgValoniaGPS.Views/Assets/Icons/Settings48.png"/>
        <TextBlock Text="Configuration"/>
    </StackPanel>
</Button>
```

### 5.3 Platform Integration

Add to both platform main views:
- `Platforms/AgValoniaGPS.Desktop/Views/MainWindow.axaml`
- `Platforms/AgValoniaGPS.iOS/Views/MainView.axaml`

```xaml
<dialogs:ConfigurationDialog
    DataContext="{Binding ConfigurationViewModel}"
    IsVisible="{Binding IsConfigurationDialogVisible}"/>
```

---

## Implementation Timeline

### Sprint 1: Foundation
- [ ] Create ConfigurationViewModel
- [ ] Create main ConfigurationDialog container
- [ ] Implement tab navigation
- [ ] Wire up to MainViewModel

### Sprint 2: Vehicle Tab
- [ ] Vehicle Config sub-tab
- [ ] Antenna sub-tab with diagram
- [ ] Dimensions sub-tab with diagram
- [ ] Guidance sub-tab

### Sprint 3: Tool Tab
- [ ] Tool Config sub-tab with diagrams
- [ ] Hitch sub-tab
- [ ] Offset sub-tab

### Sprint 4: Sections Tab
- [ ] Sections sub-tab with visual editor
- [ ] Switches sub-tab
- [ ] Timing sub-tab

### Sprint 5: U-Turn & Data Sources
- [ ] U-Turn settings tab
- [ ] Heading sub-tab
- [ ] Roll sub-tab

### Sprint 6: Hardware & Display
- [ ] Relay configuration
- [ ] Machine settings
- [ ] Button toggles
- [ ] Display preferences

### Sprint 7: Summary & Polish
- [ ] Summary tab
- [ ] Profile management UI polish
- [ ] Testing with real profiles
- [ ] Cross-platform validation

---

## Testing Strategy

1. **Unit Tests**: Test VehicleProfileService XML parsing with various profiles
2. **Integration Tests**: Test profile load/save cycle
3. **UI Tests**: Verify all bindings work correctly
4. **Cross-Platform**: Test on Desktop and iOS
5. **Compatibility**: Verify profiles work with original AgOpenGPS

---

## Dependencies

### Existing Services to Use
- `IVehicleProfileService` - Profile management
- `ISettingsService` - App settings persistence
- `NumericInputDialogPanel` - Numeric input UI

### AgOpenGPS Icon Library

The project includes a comprehensive icon library at `/btnImages/` with **240 icons** including a dedicated `Config/` subfolder. These should be copied to `Shared/AgValoniaGPS.Views/Assets/Icons/` and referenced in the Configuration Panel.

**Source Location**: `/Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/btnImages/`

The btnImages folder is already synchronized with AgOpenGPS source. Additional resources can be found at:
- **Config icons** (90 icons): `/btnImages/Config/` - Tab icons, sub-tab icons, setting diagrams
- **Steer icons**: `/btnImages/Steer/` - Pure Pursuit, Stanley, gain settings
- **Runtime images**: `/Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/btnImages/Images/` - Compass, speedo, floor texture, etc.
- **Sound files**: `/Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/Resources/` - Audio feedback (.wav files)

#### Main Tab Icons (from /btnImages/)
| Tab | Icon | File |
|-----|------|------|
| Summary | Settings | `Settings48.png` |
| Vehicle | Vehicle | `Con_VehicleMenu.png` (Config folder) |
| Tool | Implement | `Con_ImplementMenu.png` (Config folder) |
| Sections | Sections | `ConS_ImplementSection.png` (Config folder) |
| Data Sources | Sources | `Con_SourcesMenu.png` (Config folder) |
| U-Turn | U-Turn | `Con_UTurnMenu.png` (Config folder) |
| Hardware | Modules | `Con_ModulesMenu.png` (Config folder) |
| Display | Display | `Con_Display.png` (Config folder) |

#### Vehicle Tab Icons (from /btnImages/Config/)
| Sub-tab | Icon | File |
|---------|------|------|
| Config | Config | `ConS_VehicleConfig.png` |
| Antenna | Antenna | `ConS_ImplementAntenna.png` |
| Dimensions | - | Vehicle diagrams: `vehiclePageTractor.png`, `vehiclePageHarvester.png`, `vehiclePageArticulated.png` |
| Guidance | Look-ahead | `ConV_GuidanceLookAhead.png` |

#### Tool Tab Icons (from /btnImages/ and Config/)
| Sub-tab | Icon | File |
|---------|------|------|
| Config | Config | `ConS_ImplementConfig.png` |
| Hitch | Hitch | `ConS_ImplementHitch.png` |
| Offset | Offset | `ConS_ImplementOffset.png` |
| Pivot | Pivot | `ConS_ImplementPivot.png` |

Tool Type Diagrams:
- `ToolChkFront.png` - Front fixed
- `ToolChkRear.png` - Rear fixed
- `ToolChkTBT.png` - TBT (Tow Between Tractor)
- `ToolChkTrailing.png` - Trailing
- `ToolHitchPageFront.png`, `ToolHitchPageRear.png`, `ToolHitchPageTBT.png`, `ToolHitchPageTrailing.png` - Hitch diagrams

#### Sections Tab Icons (from /btnImages/Config/)
| Sub-tab | Icon | File |
|---------|------|------|
| Sections | Section | `ConS_ImplementSection.png` |
| Switches | Switch | `ConS_ImplementSwitch.png` |
| Timing | Settings | `ConS_ImplementSettings.png` |

Section Timing Animations (GIF):
- `SectionLookAheadDelay.gif`
- `SectionLookAheadOff.gif`
- `SectionOnLookAhead.gif`

#### Data Sources Tab Icons (from /btnImages/Config/)
| Sub-tab | Icon | File |
|---------|------|------|
| Heading | Heading | `ConS_SourcesHeading.png` |
| Roll | Roll | `ConS_SourcesRoll.png` |

GPS Source Icons:
- `Con_SourcesGPSDual.png` - Dual antenna
- `Con_SourcesGPSSingle.png` - Single antenna
- `Con_SourcesHead.png` - Heading source diagram
- `Con_SourcesRTKAlarm.png` - RTK alarm

Roll Icons:
- `ConDa_InvertRoll.png` - Invert roll
- `ConDa_RollSetZero.png` - Zero roll
- `ConD_RollHelper.png` - Roll helper diagram

#### U-Turn Tab Icons (from /btnImages/ and Config/)
| Setting | Icon | File |
|---------|------|------|
| Turn Radius | Radius | `ConU_UturnRadius.png` |
| Extension | Length | `ConU_UturnLength.png` |
| Distance | Distance | `ConU_UturnDistance.png` |
| Smoothing | Smooth | `ConU_UturnSmooth.png` |

U-Turn Style Icons:
- `YouTurnU.png` - U-turn style
- `YouTurnH.png` - H-turn style (hairpin)
- `YouTurnReverse.png` - Reverse turn

#### Hardware Tab Icons (from /btnImages/Config/)
| Sub-tab | Icon | File |
|---------|------|------|
| Relay | Pins | `ConS_Pins.png` |
| Machine | Machine | `ConS_ModulesMachine.png` |

Hydraulic Lift Icons:
- `ConMa_LiftRaiseTime.png` - Lift raise
- `ConMa_LiftLowerTime.png` - Lift lower
- `HydraulicLiftOn.png`, `HydraulicLiftOff.png`

#### Display Tab Icons (from /btnImages/Config/)
| Sub-tab | Icon | File |
|---------|------|------|
| Features | Features | `Con_FeatureMenu.png` |
| Display | Display | `Con_Display.png` |

Display Setting Icons:
- `ConD_Metric.png` - Metric units
- `ConD_Imperial.png` - Imperial units
- `ConD_Grid.png` - Grid toggle
- `ConD_Speedometer.png` - Speedometer
- `ConD_LightBar.png` - Light bar
- `ConD_LineSmooth.png` - Line smoothing
- `WindowDayMode.png`, `WindowNightMode.png` - Day/Night mode

#### Antenna Position Diagrams (from /btnImages/)
- `AntennaTractor.png` - Tractor antenna diagram
- `AntennaHarvester.png` - Harvester antenna diagram
- `AntennaArticulated.png` - Articulated vehicle diagram
- `AntennaLeftOffset.png` - Left offset indicator
- `AntennaRightOffset.png` - Right offset indicator
- `AntennaNoOffset.png` - No offset indicator

#### Steering/Guidance Icons (from /btnImages/ and Steer/)
- `AutoSteerConf.png` - Autosteer config
- `ModePurePursuit.png` - Pure Pursuit mode
- `ModeStanley.png` - Stanley mode
- `Steer/Sf_PP.png` - Pure Pursuit settings
- `Steer/Sf_Stanley.png` - Stanley settings
- `Steer/Sf_GainTab.png` - Gain settings

### Icon Integration Steps

1. **Copy icons to shared assets**:
   ```bash
   cp -r btnImages/Config/* Shared/AgValoniaGPS.Views/Assets/Icons/Config/
   cp btnImages/Settings48.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/vehiclePage*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/ToolChk*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/ToolHitchPage*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/Antenna*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/YouTurn*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/Mode*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/HydraulicLift*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   cp btnImages/Window*.png Shared/AgValoniaGPS.Views/Assets/Icons/
   ```

2. **Update .csproj to include icons as AvaloniaResource**:
   ```xml
   <ItemGroup>
     <AvaloniaResource Include="Assets\Icons\Config\**\*" />
   </ItemGroup>
   ```

3. **Reference in XAML**:
   ```xml
   <Image Source="avares://AgValoniaGPS.Views/Assets/Icons/Config/Con_VehicleMenu.png"
          Width="32" Height="32"/>
   ```

### Models to Verify
- `VehicleConfiguration`
- `ToolConfiguration`
- `YouTurnConfiguration`
- `VehicleProfile`

---

## Notes

1. **AgOpenGPS Compatibility**: All profile XML must remain compatible with AgOpenGPS
2. **Touch-Friendly**: Design for both mouse and touch input (iOS)
3. **Responsive**: Handle different screen sizes gracefully
4. **Validation**: Add input validation for all numeric fields
5. **Undo Support**: Consider supporting undo for configuration changes

---

## Icon Verification

All diagram images needed for the Configuration Panel **already exist** in `/btnImages/`:

### Antenna Diagrams (all present)
| File | Size | Description |
|------|------|-------------|
| `AntennaTractor.png` | 68KB | Tractor antenna position diagram |
| `AntennaHarvester.png` | 45KB | Harvester antenna position diagram |
| `AntennaArticulated.png` | 74KB | Articulated vehicle antenna diagram |
| `AntennaLeftOffset.png` | - | Left offset indicator |
| `AntennaRightOffset.png` | - | Right offset indicator |
| `AntennaNoOffset.png` | - | No offset indicator |

### Vehicle Page Diagrams (all present)
| File | Description |
|------|-------------|
| `vehiclePageTractor.png` | Tractor dimensions diagram |
| `vehiclePageHarvester.png` | Harvester dimensions diagram |
| `vehiclePageArticulated.png` | Articulated dimensions diagram |

### Hitch Diagrams (all present)
| File | Size | Description |
|------|------|-------------|
| `ToolHitchPageFront.png` | 71KB | Front hitch configuration |
| `ToolHitchPageFrontHarvester.png` | - | Harvester front hitch |
| `ToolHitchPageRear.png` | 76KB | Rear hitch configuration |
| `ToolHitchPageTBT.png` | - | TBT (Tow Between Tractor) |
| `ToolHitchPageTrailing.png` | - | Trailing hitch configuration |

**No additional icons need to be created** - the AgOpenGPS icon library is complete for Configuration Panel implementation.
