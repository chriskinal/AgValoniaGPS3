# Feature: Vehicle Profile Service

## Overview

The Vehicle Profile Service provides the navigation system with detailed information about the tractor, implements, and tools attached to the vehicle. This enables proper YouTurn path creation, section control positioning, and guidance calculations.

## AgOpenGPS Reference Format

Vehicle profiles are stored as XML files in `~/Documents/AgValoniaGPS/Vehicles/` directory using the AgOpenGPS format:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <userSettings>
        <AgOpenGPS.Properties.Settings>
            <setting name="settingName" serializeAs="String">
                <value>settingValue</value>
            </setting>
            ...
        </AgOpenGPS.Properties.Settings>
    </userSettings>
</configuration>
```

## Key Settings Categories

### 1. Vehicle Physical Dimensions
| Setting Name | Type | Description | Maps To |
|-------------|------|-------------|---------|
| `setVehicle_wheelbase` | double | Distance between axles (meters) | `VehicleConfiguration.Wheelbase` |
| `setVehicle_trackWidth` | double | Width between wheels (meters) | `VehicleConfiguration.TrackWidth` |
| `setVehicle_antennaHeight` | double | GPS antenna height (meters) | `VehicleConfiguration.AntennaHeight` |
| `setVehicle_antennaPivot` | double | Distance from pivot to antenna (meters) | `VehicleConfiguration.AntennaPivot` |
| `setVehicle_antennaOffset` | double | Lateral offset from centerline (meters) | `VehicleConfiguration.AntennaOffset` |
| `setVehicle_vehicleType` | int | 0=Tractor, 1=Harvester, 2=4WD | `VehicleConfiguration.Type` |

### 2. Steering Limits
| Setting Name | Type | Description | Maps To |
|-------------|------|-------------|---------|
| `setVehicle_maxSteerAngle` | double | Maximum steering angle (degrees) | `VehicleConfiguration.MaxSteerAngle` |
| `setVehicle_maxAngularVelocity` | double | Max angular velocity (deg/sec) | `VehicleConfiguration.MaxAngularVelocity` |

### 3. Tool Configuration
| Setting Name | Type | Description | Maps To |
|-------------|------|-------------|---------|
| `setVehicle_toolWidth` | double | Implement width (meters) | `ToolConfiguration.Width` |
| `setVehicle_toolOverlap` | double | Section overlap (meters) | `ToolConfiguration.Overlap` |
| `setVehicle_toolOffset` | double | Lateral tool offset (meters) | `ToolConfiguration.Offset` |
| `setVehicle_hitchLength` | double | Hitch to rear axle (meters) | `ToolConfiguration.HitchLength` |
| `setTool_toolTrailingHitchLength` | double | Trailing hitch length (meters) | `ToolConfiguration.TrailingHitchLength` |
| `setVehicle_tankTrailingHitchLength` | double | Tank trailer hitch (meters) | `ToolConfiguration.TankTrailingHitchLength` |
| `setTool_trailingToolToPivotLength` | double | Tool pivot offset (meters) | `ToolConfiguration.TrailingToolToPivotLength` |
| `setTool_isToolTrailing` | bool | Is tool trailing? | `ToolConfiguration.IsToolTrailing` |
| `setTool_isToolTBT` | bool | Tool Between Tanks? | `ToolConfiguration.IsToolTBT` |
| `setTool_isToolRearFixed` | bool | Rear-mounted fixed? | `ToolConfiguration.IsToolRearFixed` |
| `setTool_isToolFront` | bool | Front-mounted? | `ToolConfiguration.IsToolFrontFixed` |

### 4. Section Control
| Setting Name | Type | Description | Maps To |
|-------------|------|-------------|---------|
| `setVehicle_numSections` | int | Number of boom sections | `ToolConfiguration.NumOfSections` |
| `setSection_position1` - `setSection_position17` | double | Section edge positions (meters) | `Section.Position` array |
| `setVehicle_minCoverage` | int | Min coverage % | `ToolConfiguration.MinCoverage` |
| `setTool_isSectionsNotZones` | bool | Use sections not zones | `ToolConfiguration.IsSectionsNotZones` |
| `setTool_isSectionOffWhenOut` | bool | Turn off outside boundary | `ToolConfiguration.IsSectionOffWhenOut` |

### 5. Look-Ahead Settings
| Setting Name | Type | Description | Maps To |
|-------------|------|-------------|---------|
| `setVehicle_toolLookAheadOn` | double | Look-ahead for section on (meters) | `ToolConfiguration.LookAheadOnSetting` |
| `setVehicle_toolLookAheadOff` | double | Look-ahead for section off (meters) | `ToolConfiguration.LookAheadOffSetting` |
| `setVehicle_toolOffDelay` | double | Delay before turning off | `ToolConfiguration.TurnOffDelay` |

### 6. Guidance Parameters
| Setting Name | Type | Description | Maps To |
|-------------|------|-------------|---------|
| `setVehicle_goalPointLookAheadHold` | double | Hold distance when on-line | `VehicleConfiguration.GoalPointLookAheadHold` |
| `setVehicle_goalPointLookAheadMult` | double | Speed multiplier | `VehicleConfiguration.GoalPointLookAheadMult` |
| `setVehicle_goalPointAcquireFactor` | double | Factor when acquiring line | `VehicleConfiguration.GoalPointAcquireFactor` |
| `setVehicle_isStanleyUsed` | bool | Use Stanley algorithm | Determines steering algorithm |
| `stanleyDistanceErrorGain` | double | Stanley distance gain | `VehicleConfiguration.StanleyDistanceErrorGain` |
| `stanleyHeadingErrorGain` | double | Stanley heading gain | `VehicleConfiguration.StanleyHeadingErrorGain` |
| `stanleyIntegralGainAB` | double | Stanley integral gain | `VehicleConfiguration.StanleyIntegralGainAB` |
| `purePursuitIntegralGainAB` | double | Pure pursuit integral | `VehicleConfiguration.PurePursuitIntegralGain` |

### 7. YouTurn Settings
| Setting Name | Type | Description | Maps To |
|-------------|------|-------------|---------|
| `set_youTurnRadius` | double | U-turn radius (meters) | `YouTurnConfiguration.TurnRadius` |
| `set_youTurnExtensionLength` | double | Extension beyond headland | `YouTurnConfiguration.ExtensionLength` |
| `set_youTurnDistanceFromBoundary` | double | Distance from boundary | `YouTurnConfiguration.DistanceFromBoundary` |
| `set_youSkipWidth` | double | Skip width multiplier | `YouTurnConfiguration.SkipWidth` |
| `set_uTurnStyle` | int | 0=U-turn style | `YouTurnConfiguration.Style` |
| `setAS_uTurnCompensation` | double | U-turn compensation | `VehicleConfiguration.UTurnCompensation` |
| `setAS_uTurnSmoothing` | int | Smoothing factor | `YouTurnConfiguration.Smoothing` |

## Existing Models Analysis

### Current `Vehicle.cs` - Needs Enhancement
```csharp
public class Vehicle
{
    public string Name { get; set; }
    public double ToolWidth { get; set; }       // Limited
    public double ToolOffset { get; set; }
    public double AntennaHeight { get; set; }
    public double AntennaOffset { get; set; }
    public int NumberOfSections { get; set; }
    public double Wheelbase { get; set; }
    public double MinTurningRadius { get; set; }
    public bool IsSectionControlEnabled { get; set; }
}
```

### Current `VehicleConfiguration.cs` - Well Structured
Already contains most vehicle physical and steering parameters.

### Current `ToolConfiguration.cs` - Well Structured
Already contains most tool/implement parameters.

### Missing: `YouTurnConfiguration` Model
Need to create this for U-turn specific settings.

## Service Interface Design

```csharp
public interface IVehicleProfileService
{
    /// <summary>
    /// Gets the list of available vehicle profiles
    /// </summary>
    List<string> GetAvailableProfiles();

    /// <summary>
    /// Loads a vehicle profile by name (filename without .XML)
    /// </summary>
    VehicleProfile Load(string profileName);

    /// <summary>
    /// Saves a vehicle profile
    /// </summary>
    void Save(VehicleProfile profile);

    /// <summary>
    /// Gets the currently active profile
    /// </summary>
    VehicleProfile ActiveProfile { get; }

    /// <summary>
    /// Sets the active profile
    /// </summary>
    void SetActiveProfile(string profileName);

    /// <summary>
    /// Gets the vehicles directory path
    /// </summary>
    string VehiclesDirectory { get; }

    /// <summary>
    /// Event fired when active profile changes
    /// </summary>
    event EventHandler<VehicleProfile> ActiveProfileChanged;
}
```

## VehicleProfile Model Design

```csharp
public class VehicleProfile
{
    public string Name { get; set; }
    public string FilePath { get; set; }

    // Composed of existing models
    public VehicleConfiguration Vehicle { get; set; }
    public ToolConfiguration Tool { get; set; }
    public YouTurnConfiguration YouTurn { get; set; }
    public SectionConfiguration[] Sections { get; set; }

    // Display/Feature settings
    public DisplaySettings Display { get; set; }
    public FeatureSettings Features { get; set; }
}
```

## Implementation Steps

### Step 1: Create New Model Classes
1. [ ] Create `YouTurnConfiguration.cs` in `AgValoniaGPS.Models`
2. [ ] Create `SectionConfiguration.cs` for per-section settings
3. [ ] Create `VehicleProfile.cs` as the aggregate model

### Step 2: Create Service Interface
1. [ ] Create `IVehicleProfileService.cs` in `AgValoniaGPS.Services/Interfaces/`

### Step 3: Implement Service
1. [ ] Create `VehicleProfileService.cs` in `AgValoniaGPS.Services/`
2. [ ] Implement XML parsing for AgOpenGPS format
3. [ ] Implement save functionality maintaining AgOpenGPS format
4. [ ] Handle profile listing and directory management

### Step 4: Wire Up Dependencies
1. [ ] Register `IVehicleProfileService` in both platform DI containers
2. [ ] Inject into `MainViewModel`
3. [ ] Load active profile on startup
4. [ ] Expose profile settings to components that need them

### Step 5: Update Consumers
1. [ ] Update YouTurn service to read from profile
2. [ ] Update section control to use profile section positions
3. [ ] Update guidance service to use profile steering parameters

### Step 6: Add UI for Profile Selection
1. [ ] Create vehicle selection dialog
2. [ ] Add profile selector to settings

## File Locations

| Component | Path |
|-----------|------|
| Models | `Shared/AgValoniaGPS.Models/VehicleProfile.cs` |
| YouTurn Config | `Shared/AgValoniaGPS.Models/YouTurnConfiguration.cs` |
| Interface | `Shared/AgValoniaGPS.Services/Interfaces/IVehicleProfileService.cs` |
| Service | `Shared/AgValoniaGPS.Services/VehicleProfileService.cs` |
| Vehicles Dir | `~/Documents/AgValoniaGPS/Vehicles/` |

## XML Parsing Strategy

Use `System.Xml.Linq` for parsing:

```csharp
public VehicleProfile Load(string profileName)
{
    var filePath = Path.Combine(VehiclesDirectory, $"{profileName}.XML");
    var doc = XDocument.Load(filePath);

    var settings = doc.Descendants("setting")
        .ToDictionary(
            s => s.Attribute("name")?.Value ?? "",
            s => s.Element("value")?.Value ?? ""
        );

    return new VehicleProfile
    {
        Name = profileName,
        FilePath = filePath,
        Vehicle = ParseVehicleConfiguration(settings),
        Tool = ParseToolConfiguration(settings),
        YouTurn = ParseYouTurnConfiguration(settings),
        Sections = ParseSections(settings)
    };
}
```

## Expected Behavior

1. **On Startup**: Load last-used profile or "Default Vehicle"
2. **Profile Switch**: Update all dependent services with new configuration
3. **Profile Save**: Preserve AgOpenGPS format for cross-compatibility
4. **Missing File**: Create default profile if none exist

## Test Scenarios

1. Load existing AgOpenGPS vehicle file (Deere 5055e.XML)
2. Verify all settings correctly mapped
3. Save modified profile
4. Verify saved file readable by AgOpenGPS
5. Switch profiles and verify services update
6. Handle missing/corrupt files gracefully
