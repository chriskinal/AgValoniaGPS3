# Configuration Refactoring Plan: Single Source of Truth

**Date:** December 11, 2025
**Goal:** Establish a single source of truth for all configuration, eliminating duplicate models and property mapping boilerplate.

## Executive Summary

The current configuration system has multiple overlapping models, duplicated properties, and a 685-line ViewModel that manually maps properties between layers. By establishing a **single source of truth** pattern, we can:

- Eliminate ~560 lines of boilerplate
- Remove duplicate/conflicting configuration
- Ensure all components see the same values
- Simplify persistence and synchronization

**Current state:** 1,458 lines across 10 files
**Estimated after refactor:** ~600 lines across 6 files
**Reduction:** ~60%

## Current Architecture Problems

### Problem 1: Duplicate Vehicle Models

```
Vehicle.cs (51 lines)           VehicleConfiguration.cs (69 lines)
─────────────────────           ────────────────────────────────────
Name                            (not present)
ToolWidth                       (in ToolConfiguration)
ToolOffset                      (in ToolConfiguration)
AntennaHeight          ←───→    AntennaHeight
AntennaOffset          ←───→    AntennaOffset (different meaning?)
Wheelbase              ←───→    Wheelbase
MinTurningRadius                (not present)
NumberOfSections                (in ToolConfiguration)
IsSectionControlEnabled         (in ToolConfiguration)
                                AntennaPivot
                                TrackWidth
                                MaxSteerAngle
                                Type (VehicleType enum)
                                + all steering algorithm params
```

**Impact:** Confusion about which to use, potential for values to diverge.

### Problem 2: Simulator Settings in Two Places

```csharp
// AppSettings.cs
public bool SimulatorEnabled { get; set; } = false;
public double SimulatorLatitude { get; set; } = 40.7128;
public double SimulatorLongitude { get; set; } = -74.0060;
public double SimulatorSpeed { get; set; } = 0.0;
public double SimulatorSteerAngle { get; set; } = 0.0;

// VehicleProfile.cs
public bool IsSimulatorOn { get; set; } = true;
public double SimLatitude { get; set; } = 32.5904315166667;
public double SimLongitude { get; set; } = -87.1804217333333;
```

**Impact:** Different defaults, unclear which is authoritative.

### Problem 3: ConfigurationViewModel Property Explosion (685 lines)

The ViewModel duplicates every single property from the models:

```csharp
// For EACH of 50+ properties:
private double _wheelbase = 2.5;
public double Wheelbase
{
    get => _wheelbase;
    set { this.RaiseAndSetIfChanged(ref _wheelbase, value); MarkChanged(); }
}

// Then manual mapping in LoadFromProfile():
Wheelbase = profile.Vehicle.Wheelbase;
AntennaHeight = profile.Vehicle.AntennaHeight;
// ... 48 more lines

// And reverse mapping in SaveToProfile():
profile.Vehicle.Wheelbase = Wheelbase;
profile.Vehicle.AntennaHeight = AntennaHeight;
// ... 48 more lines
```

**Impact:**
- Maintenance nightmare - add property to model, must add to ViewModel + Load + Save
- Easy to miss a property during copy
- 500+ lines of pure boilerplate

### Problem 4: AhrsConfiguration Mixes Concerns

```csharp
public class AhrsConfiguration
{
    // Runtime sensor values (updated every GPS frame)
    public double ImuHeading { get; set; } = 99999;
    public double ImuRoll { get; set; } = 0;
    public double ImuPitch { get; set; } = 0;
    public double ImuYawRate { get; set; } = 0;
    public short AngularVelocity { get; set; }

    // Configuration values (changed in settings dialog)
    public double RollZero { get; set; }
    public double RollFilter { get; set; }
    public bool IsRollInvert { get; set; }
    // ...
}
```

**Impact:** Confusing API, runtime state mixed with persisted config.

### Problem 5: Display Settings Not Persisted

```csharp
// DisplaySettingsService.cs
public void SaveSettings()
{
    // TODO: Implement settings persistence
}
```

Meanwhile, `AppSettings` has overlapping display properties:
```csharp
public bool GridVisible { get; set; } = true;
public bool CompassVisible { get; set; } = true;
```

**Impact:** Settings lost on restart, duplication between service and AppSettings.

## Proposed Architecture: Single Source of Truth

### Core Principle

```
┌─────────────────────────────────────────────────────────────────┐
│                    ConfigurationStore                            │
│  (Single source of truth for ALL configuration)                  │
├─────────────────────────────────────────────────────────────────┤
│  AppConfig          │ Per-installation settings (NTRIP, paths)  │
│  VehicleConfig      │ Physical vehicle (wheelbase, antenna)     │
│  ToolConfig         │ Implement (width, sections, hitch)        │
│  GuidanceConfig     │ Steering algorithm parameters             │
│  DisplayConfig      │ UI preferences (grid, units, camera)      │
│  SimulatorConfig    │ Simulator settings (ONE place only)       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ References (not copies)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  ViewModels, Services, Renderers                                 │
│  All access ConfigurationStore directly                          │
│  No private copies, no property mapping                          │
└─────────────────────────────────────────────────────────────────┘
```

### New Model Structure

#### 1. `ConfigurationStore.cs` - The Single Source of Truth

```csharp
/// <summary>
/// Central configuration store - the ONLY place configuration lives.
/// All components access this directly; no private copies.
/// Implements INotifyPropertyChanged for UI binding.
/// </summary>
public class ConfigurationStore : ReactiveObject
{
    private static ConfigurationStore? _instance;
    public static ConfigurationStore Instance => _instance ??= new ConfigurationStore();

    // Sub-configurations (each is a ReactiveObject for binding)
    public VehicleConfig Vehicle { get; } = new();
    public ToolConfig Tool { get; } = new();
    public GuidanceConfig Guidance { get; } = new();
    public DisplayConfig Display { get; } = new();
    public SimulatorConfig Simulator { get; } = new();
    public ConnectionConfig Connections { get; } = new();

    // Profile management
    private string _activeProfileName = "Default";
    public string ActiveProfileName
    {
        get => _activeProfileName;
        set => this.RaiseAndSetIfChanged(ref _activeProfileName, value);
    }

    // Dirty tracking for save prompts
    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    // Events for significant changes
    public event EventHandler? ProfileLoaded;
    public event EventHandler? ProfileSaved;
}
```

#### 2. `VehicleConfig.cs` - Consolidated Vehicle Settings

```csharp
/// <summary>
/// Vehicle physical configuration.
/// Replaces: Vehicle.cs, VehicleConfiguration.cs (physical parts)
/// </summary>
public class VehicleConfig : ReactiveObject
{
    // Identity
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    // Vehicle type
    private VehicleType _type = VehicleType.Tractor;
    public VehicleType Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    // Physical dimensions
    private double _wheelbase = 2.5;
    public double Wheelbase
    {
        get => _wheelbase;
        set => this.RaiseAndSetIfChanged(ref _wheelbase, value);
    }

    private double _trackWidth = 1.8;
    public double TrackWidth
    {
        get => _trackWidth;
        set => this.RaiseAndSetIfChanged(ref _trackWidth, value);
    }

    // Antenna position
    private double _antennaHeight = 3.0;
    public double AntennaHeight
    {
        get => _antennaHeight;
        set => this.RaiseAndSetIfChanged(ref _antennaHeight, value);
    }

    private double _antennaPivot = 0.0;
    public double AntennaPivot
    {
        get => _antennaPivot;
        set => this.RaiseAndSetIfChanged(ref _antennaPivot, value);
    }

    private double _antennaOffset = 0.0;
    public double AntennaOffset
    {
        get => _antennaOffset;
        set => this.RaiseAndSetIfChanged(ref _antennaOffset, value);
    }

    // Steering limits
    private double _maxSteerAngle = 35.0;
    public double MaxSteerAngle
    {
        get => _maxSteerAngle;
        set => this.RaiseAndSetIfChanged(ref _maxSteerAngle, value);
    }

    private double _maxAngularVelocity = 35.0;
    public double MaxAngularVelocity
    {
        get => _maxAngularVelocity;
        set => this.RaiseAndSetIfChanged(ref _maxAngularVelocity, value);
    }

    // Computed properties
    public double MinTurningRadius => Wheelbase / Math.Tan(MaxSteerAngle * Math.PI / 180.0);
}
```

#### 3. `GuidanceConfig.cs` - All Steering Algorithm Settings

```csharp
/// <summary>
/// Guidance and steering algorithm configuration.
/// Replaces: steering parts of VehicleConfiguration.cs
/// </summary>
public class GuidanceConfig : ReactiveObject
{
    // Algorithm selection
    private bool _usePurePursuit = true;
    public bool UsePurePursuit
    {
        get => _usePurePursuit;
        set => this.RaiseAndSetIfChanged(ref _usePurePursuit, value);
    }

    public bool UseStanley => !UsePurePursuit;

    // Look-ahead parameters (both algorithms)
    private double _goalPointLookAheadHold = 4.0;
    public double GoalPointLookAheadHold
    {
        get => _goalPointLookAheadHold;
        set => this.RaiseAndSetIfChanged(ref _goalPointLookAheadHold, value);
    }

    private double _goalPointLookAheadMult = 1.4;
    public double GoalPointLookAheadMult
    {
        get => _goalPointLookAheadMult;
        set => this.RaiseAndSetIfChanged(ref _goalPointLookAheadMult, value);
    }

    private double _goalPointAcquireFactor = 1.5;
    public double GoalPointAcquireFactor
    {
        get => _goalPointAcquireFactor;
        set => this.RaiseAndSetIfChanged(ref _goalPointAcquireFactor, value);
    }

    private double _minLookAheadDistance = 2.0;
    public double MinLookAheadDistance
    {
        get => _minLookAheadDistance;
        set => this.RaiseAndSetIfChanged(ref _minLookAheadDistance, value);
    }

    // Pure Pursuit specific
    private double _purePursuitIntegralGain = 0.0;
    public double PurePursuitIntegralGain
    {
        get => _purePursuitIntegralGain;
        set => this.RaiseAndSetIfChanged(ref _purePursuitIntegralGain, value);
    }

    // Stanley specific
    private double _stanleyDistanceErrorGain = 0.8;
    public double StanleyDistanceErrorGain
    {
        get => _stanleyDistanceErrorGain;
        set => this.RaiseAndSetIfChanged(ref _stanleyDistanceErrorGain, value);
    }

    private double _stanleyHeadingErrorGain = 1.0;
    public double StanleyHeadingErrorGain
    {
        get => _stanleyHeadingErrorGain;
        set => this.RaiseAndSetIfChanged(ref _stanleyHeadingErrorGain, value);
    }

    private double _stanleyIntegralGain = 0.0;
    public double StanleyIntegralGain
    {
        get => _stanleyIntegralGain;
        set => this.RaiseAndSetIfChanged(ref _stanleyIntegralGain, value);
    }

    // Dead zone
    private double _deadZoneHeading = 0.5;
    public double DeadZoneHeading
    {
        get => _deadZoneHeading;
        set => this.RaiseAndSetIfChanged(ref _deadZoneHeading, value);
    }

    private int _deadZoneDelay = 10;
    public int DeadZoneDelay
    {
        get => _deadZoneDelay;
        set => this.RaiseAndSetIfChanged(ref _deadZoneDelay, value);
    }

    // U-Turn (merged from YouTurnConfiguration)
    private double _uTurnRadius = 8.0;
    public double UTurnRadius
    {
        get => _uTurnRadius;
        set => this.RaiseAndSetIfChanged(ref _uTurnRadius, value);
    }

    private double _uTurnExtension = 20.0;
    public double UTurnExtension
    {
        get => _uTurnExtension;
        set => this.RaiseAndSetIfChanged(ref _uTurnExtension, value);
    }

    private double _uTurnDistanceFromBoundary = 2.0;
    public double UTurnDistanceFromBoundary
    {
        get => _uTurnDistanceFromBoundary;
        set => this.RaiseAndSetIfChanged(ref _uTurnDistanceFromBoundary, value);
    }

    private int _uTurnSkipWidth = 1;
    public int UTurnSkipWidth
    {
        get => _uTurnSkipWidth;
        set => this.RaiseAndSetIfChanged(ref _uTurnSkipWidth, value);
    }

    private double _uTurnCompensation = 1.0;
    public double UTurnCompensation
    {
        get => _uTurnCompensation;
        set => this.RaiseAndSetIfChanged(ref _uTurnCompensation, value);
    }

    private int _uTurnSmoothing = 14;
    public int UTurnSmoothing
    {
        get => _uTurnSmoothing;
        set => this.RaiseAndSetIfChanged(ref _uTurnSmoothing, value);
    }
}
```

#### 4. `DisplayConfig.cs` - All Display Settings (Consolidated)

```csharp
/// <summary>
/// Display and UI configuration.
/// Replaces: display parts of AppSettings, DisplaySettingsService state
/// </summary>
public class DisplayConfig : ReactiveObject
{
    // Units
    private bool _isMetric = true;
    public bool IsMetric
    {
        get => _isMetric;
        set => this.RaiseAndSetIfChanged(ref _isMetric, value);
    }

    // Map display
    private bool _gridVisible = true;
    public bool GridVisible
    {
        get => _gridVisible;
        set => this.RaiseAndSetIfChanged(ref _gridVisible, value);
    }

    private bool _compassVisible = true;
    public bool CompassVisible
    {
        get => _compassVisible;
        set => this.RaiseAndSetIfChanged(ref _compassVisible, value);
    }

    private bool _speedVisible = true;
    public bool SpeedVisible
    {
        get => _speedVisible;
        set => this.RaiseAndSetIfChanged(ref _speedVisible, value);
    }

    // Camera
    private double _cameraZoom = 100.0;
    public double CameraZoom
    {
        get => _cameraZoom;
        set => this.RaiseAndSetIfChanged(ref _cameraZoom, value);
    }

    private double _cameraPitch = -62.0;
    public double CameraPitch
    {
        get => _cameraPitch;
        set => this.RaiseAndSetIfChanged(ref _cameraPitch, Math.Clamp(value, -90, -10));
    }

    private bool _is2DMode = false;
    public bool Is2DMode
    {
        get => _is2DMode;
        set => this.RaiseAndSetIfChanged(ref _is2DMode, value);
    }

    private bool _isNorthUp = true;
    public bool IsNorthUp
    {
        get => _isNorthUp;
        set => this.RaiseAndSetIfChanged(ref _isNorthUp, value);
    }

    private bool _isDayMode = true;
    public bool IsDayMode
    {
        get => _isDayMode;
        set => this.RaiseAndSetIfChanged(ref _isDayMode, value);
    }

    // Window (Desktop only, ignored on iOS)
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public double WindowX { get; set; } = 100;
    public double WindowY { get; set; } = 100;
    public bool WindowMaximized { get; set; } = false;

    // Panel positions
    public double SimulatorPanelX { get; set; } = double.NaN;
    public double SimulatorPanelY { get; set; } = double.NaN;
    public bool SimulatorPanelVisible { get; set; } = false;
}
```

#### 5. `SimulatorConfig.cs` - Single Location for Simulator Settings

```csharp
/// <summary>
/// Simulator configuration - ONE place only.
/// Replaces: simulator fields in both AppSettings and VehicleProfile
/// </summary>
public class SimulatorConfig : ReactiveObject
{
    private bool _enabled = false;
    public bool Enabled
    {
        get => _enabled;
        set => this.RaiseAndSetIfChanged(ref _enabled, value);
    }

    private double _latitude = 40.7128;
    public double Latitude
    {
        get => _latitude;
        set => this.RaiseAndSetIfChanged(ref _latitude, value);
    }

    private double _longitude = -74.0060;
    public double Longitude
    {
        get => _longitude;
        set => this.RaiseAndSetIfChanged(ref _longitude, value);
    }

    private double _speed = 0.0;
    public double Speed
    {
        get => _speed;
        set => this.RaiseAndSetIfChanged(ref _speed, value);
    }

    private double _steerAngle = 0.0;
    public double SteerAngle
    {
        get => _steerAngle;
        set => this.RaiseAndSetIfChanged(ref _steerAngle, value);
    }
}
```

#### 6. `ConnectionConfig.cs` - Network/Communication Settings

```csharp
/// <summary>
/// Network and communication configuration.
/// Replaces: NTRIP and AgShare parts of AppSettings
/// </summary>
public class ConnectionConfig : ReactiveObject
{
    // NTRIP
    public string NtripCasterHost { get; set; } = string.Empty;
    public int NtripCasterPort { get; set; } = 2101;
    public string NtripMountPoint { get; set; } = string.Empty;
    public string NtripUsername { get; set; } = string.Empty;
    public string NtripPassword { get; set; } = string.Empty;
    public bool NtripAutoConnect { get; set; } = false;

    // AgShare
    public string AgShareServer { get; set; } = "https://agshare.agopengps.com";
    public string AgShareApiKey { get; set; } = string.Empty;
    public bool AgShareEnabled { get; set; } = false;

    // GPS
    public int GpsUpdateRate { get; set; } = 10;
    public bool UseRtk { get; set; } = true;
}
```

### Simplified ConfigurationViewModel

With the single source of truth pattern, the ViewModel becomes trivial:

```csharp
/// <summary>
/// ViewModel for Configuration Dialog.
/// NO property mapping - binds directly to ConfigurationStore.
/// </summary>
public class ConfigurationViewModel : ReactiveObject
{
    private readonly IConfigurationService _configService;

    public ConfigurationViewModel(IConfigurationService configService)
    {
        _configService = configService;

        // Commands
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(Cancel);
        LoadProfileCommand = ReactiveCommand.Create<string>(LoadProfile);
        NewProfileCommand = ReactiveCommand.Create<string>(CreateNewProfile);
    }

    // Direct access to configuration - NO COPYING
    public ConfigurationStore Config => ConfigurationStore.Instance;

    // Convenience accessors for XAML binding
    public VehicleConfig Vehicle => Config.Vehicle;
    public ToolConfig Tool => Config.Tool;
    public GuidanceConfig Guidance => Config.Guidance;
    public DisplayConfig Display => Config.Display;
    public SimulatorConfig Simulator => Config.Simulator;

    // Profile management
    public ObservableCollection<string> AvailableProfiles { get; } = new();

    private string? _selectedProfileName;
    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedProfileName, value);
            if (value != null) LoadProfile(value);
        }
    }

    // Dialog visibility
    private bool _isDialogVisible;
    public bool IsDialogVisible
    {
        get => _isDialogVisible;
        set => this.RaiseAndSetIfChanged(ref _isDialogVisible, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<string, Unit> LoadProfileCommand { get; }
    public ReactiveCommand<string, Unit> NewProfileCommand { get; }

    // Image sources based on vehicle type (computed)
    public string WheelbaseImageSource => Vehicle.Type switch
    {
        VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBaseHarvester.png",
        VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBaseArticulated.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBase.png"
    };

    private void Save()
    {
        _configService.SaveProfile(Config.ActiveProfileName);
        IsDialogVisible = false;
    }

    private void Cancel()
    {
        _configService.ReloadProfile(Config.ActiveProfileName);
        IsDialogVisible = false;
    }

    private void LoadProfile(string name)
    {
        _configService.LoadProfile(name);
    }

    private void CreateNewProfile(string name)
    {
        _configService.CreateProfile(name);
        AvailableProfiles.Add(name);
        SelectedProfileName = name;
    }
}
```

**Result: ~80 lines instead of 685 lines**

### XAML Binding Changes

Before:
```xml
<TextBox Text="{Binding Wheelbase}" />
```

After:
```xml
<TextBox Text="{Binding Vehicle.Wheelbase}" />
```

Or using the convenience accessor:
```xml
<TextBox Text="{Binding Vehicle.Wheelbase}" />
```

### Runtime State Separation

#### `SensorState.cs` - Runtime Sensor Values (NOT persisted)

```csharp
/// <summary>
/// Runtime sensor state - updated every GPS frame.
/// NOT persisted, NOT part of ConfigurationStore.
/// </summary>
public class SensorState : ReactiveObject
{
    // IMU/AHRS
    private double _imuHeading = 99999;
    public double ImuHeading
    {
        get => _imuHeading;
        set => this.RaiseAndSetIfChanged(ref _imuHeading, value);
    }

    private double _imuRoll = 0;
    public double ImuRoll
    {
        get => _imuRoll;
        set => this.RaiseAndSetIfChanged(ref _imuRoll, value);
    }

    private double _imuPitch = 0;
    public double ImuPitch
    {
        get => _imuPitch;
        set => this.RaiseAndSetIfChanged(ref _imuPitch, value);
    }

    private double _imuYawRate = 0;
    public double ImuYawRate
    {
        get => _imuYawRate;
        set => this.RaiseAndSetIfChanged(ref _imuYawRate, value);
    }

    // Validity
    public bool HasValidImu => ImuHeading != 99999;
}
```

#### `AhrsConfig.cs` - IMU Configuration (IS persisted)

```csharp
/// <summary>
/// AHRS/IMU configuration settings.
/// Part of ConfigurationStore, persisted with profile.
/// </summary>
public class AhrsConfig : ReactiveObject
{
    public double RollZero { get; set; }
    public double RollFilter { get; set; }
    public double FusionWeight { get; set; }
    public bool IsRollInvert { get; set; }
    public double ForwardCompensation { get; set; }
    public double ReverseCompensation { get; set; }
    // ... other config-only properties
}
```

## Service Changes

### `IConfigurationService.cs`

```csharp
public interface IConfigurationService
{
    ConfigurationStore Store { get; }

    // Profile management
    IReadOnlyList<string> GetAvailableProfiles();
    void LoadProfile(string name);
    void SaveProfile(string name);
    void CreateProfile(string name);
    void DeleteProfile(string name);
    void ReloadProfile(string name);

    // App settings (not profile-specific)
    void LoadAppSettings();
    void SaveAppSettings();
}
```

### `ConfigurationService.cs`

```csharp
public class ConfigurationService : IConfigurationService
{
    private readonly string _profilesDirectory;
    private readonly string _appSettingsPath;

    public ConfigurationStore Store => ConfigurationStore.Instance;

    public void LoadProfile(string name)
    {
        var path = Path.Combine(_profilesDirectory, $"{name}.json");
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<ProfileDto>(json);

        // Apply to store (direct assignment, no mapping)
        ApplyProfile(profile);
        Store.ActiveProfileName = name;
        Store.HasUnsavedChanges = false;
    }

    public void SaveProfile(string name)
    {
        var profile = CreateProfileDto();
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(_profilesDirectory, $"{name}.json");
        File.WriteAllText(path, json);
        Store.HasUnsavedChanges = false;
    }

    // ... implementation
}
```

## Files to Delete

| File | Lines | Reason |
|------|-------|--------|
| `Vehicle.cs` | 51 | Merged into VehicleConfig |
| `VehicleConfiguration.cs` | 69 | Split into VehicleConfig + GuidanceConfig |
| `YouTurnConfiguration.cs` | 49 | Merged into GuidanceConfig |
| `AhrsConfiguration.cs` | 28 | Split into AhrsConfig + SensorState |
| `DisplaySettingsService.cs` | 185 | Functionality in DisplayConfig + ConfigurationStore |

**Total deleted:** 382 lines

## Files to Create

| File | Est. Lines | Purpose |
|------|------------|---------|
| `ConfigurationStore.cs` | ~60 | Single source of truth container |
| `VehicleConfig.cs` | ~80 | Vehicle physical settings |
| `ToolConfig.cs` | ~100 | Tool/implement settings (minor refactor of existing) |
| `GuidanceConfig.cs` | ~120 | All steering/guidance params |
| `DisplayConfig.cs` | ~80 | All display settings |
| `SimulatorConfig.cs` | ~40 | Simulator settings |
| `ConnectionConfig.cs` | ~30 | NTRIP, AgShare, GPS |
| `SensorState.cs` | ~40 | Runtime sensor values |
| `AhrsConfig.cs` | ~30 | IMU configuration |

**Total new:** ~580 lines

## Files to Modify

| File | Change |
|------|--------|
| `ConfigurationViewModel.cs` | 685 → ~80 lines (remove property mapping) |
| `AppSettings.cs` | Remove duplicated fields, keep paths only |
| `VehicleProfile.cs` | Becomes a DTO for serialization only |
| All services using config | Change to use `ConfigurationStore.Instance` |

## Migration Strategy

### Phase 1: Create ConfigurationStore Infrastructure
1. Create `ConfigurationStore.cs` as container
2. Create new config classes (VehicleConfig, GuidanceConfig, etc.)
3. Create `IConfigurationService` and implementation
4. Keep old models temporarily

### Phase 2: Migrate ConfigurationViewModel
1. Change ViewModel to reference ConfigurationStore
2. Update XAML bindings to use `{Binding Vehicle.Wheelbase}` style
3. Remove 600+ lines of property mapping
4. Test configuration dialog thoroughly

### Phase 3: Migrate Services
1. Update GuidanceService to use `ConfigurationStore.Instance.Guidance`
2. Update all other services
3. Remove old model references

### Phase 4: Cleanup
1. Delete old model files
2. Delete DisplaySettingsService
3. Update persistence to use new structure

### Phase 5: AgOpenGPS Compatibility
1. Ensure profile import/export still works with AOG XML format
2. Create DTOs for serialization if needed

## Usage Examples

### Before (service accessing config):

```csharp
public class GuidanceService
{
    private readonly VehicleConfiguration _vehicleConfig;

    public GuidanceService(VehicleConfiguration vehicleConfig)
    {
        _vehicleConfig = vehicleConfig;
    }

    public void Calculate()
    {
        var wheelbase = _vehicleConfig.Wheelbase;
        var lookAhead = _vehicleConfig.GoalPointLookAheadHold;
    }
}
```

### After (single source of truth):

```csharp
public class GuidanceService
{
    public void Calculate()
    {
        var config = ConfigurationStore.Instance;
        var wheelbase = config.Vehicle.Wheelbase;
        var lookAhead = config.Guidance.GoalPointLookAheadHold;
    }
}
```

Or with DI:

```csharp
public class GuidanceService
{
    private readonly ConfigurationStore _config;

    public GuidanceService(ConfigurationStore config)
    {
        _config = config;
    }

    public void Calculate()
    {
        var wheelbase = _config.Vehicle.Wheelbase;
        var lookAhead = _config.Guidance.GoalPointLookAheadHold;
    }
}
```

## Expected Outcome

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Configuration files | 10 | 6 | -40% |
| Total lines | 1,458 | ~600 | -59% |
| ConfigurationViewModel | 685 | ~80 | -88% |
| Duplicate properties | ~20 | 0 | -100% |
| Property mapping code | ~200 | 0 | -100% |

## Benefits

1. **Single Source of Truth**: One place for each setting, no divergence possible
2. **No Property Mapping**: Direct binding eliminates copy/sync bugs
3. **Easier Maintenance**: Add property to model, automatically available everywhere
4. **Better Testability**: Mock ConfigurationStore for testing
5. **Cleaner Separation**: Runtime state (SensorState) vs config (ConfigurationStore)
6. **Reactive Updates**: Changes propagate automatically via INotifyPropertyChanged
