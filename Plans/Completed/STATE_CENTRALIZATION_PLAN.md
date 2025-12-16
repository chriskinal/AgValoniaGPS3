# State Centralization Plan: ApplicationState

**Date:** December 11, 2025
**Goal:** Extract the 207 private fields from MainViewModel into a centralized, observable ApplicationState, eliminating the God Object anti-pattern.

## Executive Summary

MainViewModel has grown to **7,045 lines** with **207 private fields**, making it a classic God Object. By extracting state into a centralized `ApplicationState` with domain-specific sub-states, we can:

- Reduce MainViewModel from 7,045 to ~1,500 lines
- Make state accessible from any component without prop drilling
- Enable state snapshots for debugging and testing
- Eliminate duplicate state between ViewModel and Services
- Simplify unit testing

**Current state:** 7,045 lines in MainViewModel + scattered service state
**Estimated after refactor:** ~1,500 lines in MainViewModel + ~800 lines in state classes
**Net reduction:** ~5,000 lines, cleaner architecture

## Current Problems

### Problem 1: God Object (MainViewModel = 7,045 lines)

```
MainViewModel.cs
├── 207 private fields
├── 25+ services injected
├── 100+ public properties
├── 50+ commands
├── Dialog management for 20+ dialogs
├── GPS data processing
├── Guidance calculations
├── YouTurn state machine
├── Section control
├── Boundary recording
├── ... and more
```

**Symptoms:**
- Takes 30+ parameters in constructor
- Any change risks breaking unrelated functionality
- Impossible to unit test in isolation
- Hard to find where state lives

### Problem 2: Duplicate State

GPS position exists in multiple places:
```csharp
// GpsService.cs
public GpsData CurrentData { get; private set; }

// MainViewModel.cs
private double _latitude;
private double _longitude;
private double _easting;
private double _northing;
private double _heading;
private double _speed;

// GpsSimulationService.cs
private double _latitude;
private double _longitude;
// ...
```

When GPS updates, multiple copies must be synchronized.

### Problem 3: State Scattered Across Services

```
UdpCommunicationService  → 16 private fields (connection state)
NtripClientService       → 15 private fields (NTRIP state)
GpsSimulationService     → 8 private fields (simulator state)
DisplaySettingsService   → 6 private fields (display state)
BoundaryRecordingService → 5 private fields (recording state)
```

To know "is the system connected?", you must query multiple services.

### Problem 4: Dialog State Explosion

```csharp
// 25+ dialog visibility flags in MainViewModel
private bool _isFieldSelectionDialogVisible;
private bool _isTracksDialogVisible;
private bool _isQuickABSelectorVisible;
private bool _isDrawABDialogVisible;
private bool _isNewFieldDialogVisible;
private bool _isFromExistingFieldDialogVisible;
private bool _isKmlImportDialogVisible;
private bool _isIsoXmlImportDialogVisible;
private bool _isBoundaryMapDialogVisible;
private bool _isNumericInputDialogVisible;
private bool _isAgShareSettingsDialogVisible;
private bool _isAgShareUploadDialogVisible;
private bool _isAgShareDownloadDialogVisible;
private bool _isDataIODialogVisible;
private bool _isConfigurationDialogVisible;
private bool _isHeadlandBuilderDialogVisible;
private bool _isHeadlandDialogVisible;
private bool _isSimCoordsDialogVisible;
// ... plus associated data fields for each dialog
```

## Proposed Architecture: Centralized ApplicationState

### Core Design

```
┌──────────────────────────────────────────────────────────────────┐
│                      ApplicationState                             │
│  (Single source of truth for ALL runtime state)                   │
│  Singleton, Observable, Injectable                                │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐               │
│  │VehicleState │  │GuidanceState│  │FieldState   │               │
│  ├─────────────┤  ├─────────────┤  ├─────────────┤               │
│  │Position     │  │CrossTrackErr│  │ActiveField  │               │
│  │Heading      │  │SteerAngle   │  │Boundaries   │               │
│  │Speed        │  │GoalPoint    │  │Tracks       │               │
│  │IMU data     │  │PP/Stanley   │  │Headlands    │               │
│  └─────────────┘  └─────────────┘  └─────────────┘               │
│                                                                   │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐               │
│  │SectionState │  │ConnState    │  │YouTurnState │               │
│  ├─────────────┤  ├─────────────┤  ├─────────────┤               │
│  │Sections[]   │  │GPS status   │  │IsTriggered  │               │
│  │MasterOn     │  │NTRIP status │  │TurnPath     │               │
│  │ManualMode   │  │AutoSteer    │  │Direction    │               │
│  │Coverage     │  │Machine      │  │PathsAway    │               │
│  └─────────────┘  └─────────────┘  └─────────────┘               │
│                                                                   │
│  ┌─────────────┐  ┌─────────────┐                                │
│  │UIState      │  │SimState     │                                │
│  ├─────────────┤  ├─────────────┤                                │
│  │ActiveDialog │  │Enabled      │                                │
│  │DialogStack  │  │Position     │                                │
│  │PanelVisible │  │Speed        │                                │
│  │SelectedItem │  │SteerAngle   │                                │
│  └─────────────┘  └─────────────┘                                │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
        MainViewModel    Services      Renderers
        (thin layer)    (update state) (read state)
```

### State Classes

#### 1. `ApplicationState.cs` - The Container

```csharp
/// <summary>
/// Central application state container.
/// Single source of truth for ALL runtime state.
/// </summary>
public class ApplicationState : ReactiveObject
{
    private static ApplicationState? _instance;
    public static ApplicationState Instance => _instance ??= new ApplicationState();

    // Domain state objects
    public VehicleState Vehicle { get; } = new();
    public GuidanceState Guidance { get; } = new();
    public SectionState Sections { get; } = new();
    public ConnectionState Connections { get; } = new();
    public FieldState Field { get; } = new();
    public YouTurnState YouTurn { get; } = new();
    public BoundaryState Boundary { get; } = new();
    public SimulatorState Simulator { get; } = new();
    public UIState UI { get; } = new();

    // Global events
    public event EventHandler? StateReset;

    /// <summary>
    /// Reset all state (e.g., when closing a field)
    /// </summary>
    public void Reset()
    {
        Vehicle.Reset();
        Guidance.Reset();
        Sections.Reset();
        YouTurn.Reset();
        Boundary.Reset();
        // Field and Connections typically persist
        StateReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Create a snapshot for debugging/logging
    /// </summary>
    public StateSnapshot CreateSnapshot() => new StateSnapshot(this);
}
```

#### 2. `VehicleState.cs` - Position and Motion

```csharp
/// <summary>
/// Current vehicle position, heading, and motion state.
/// Updated by GPS service every frame.
/// </summary>
public class VehicleState : ReactiveObject
{
    // GPS Position (WGS84)
    private double _latitude;
    public double Latitude
    {
        get => _latitude;
        set => this.RaiseAndSetIfChanged(ref _latitude, value);
    }

    private double _longitude;
    public double Longitude
    {
        get => _longitude;
        set => this.RaiseAndSetIfChanged(ref _longitude, value);
    }

    private double _altitude;
    public double Altitude
    {
        get => _altitude;
        set => this.RaiseAndSetIfChanged(ref _altitude, value);
    }

    // Local coordinates (UTM/field plane)
    private double _easting;
    public double Easting
    {
        get => _easting;
        set => this.RaiseAndSetIfChanged(ref _easting, value);
    }

    private double _northing;
    public double Northing
    {
        get => _northing;
        set => this.RaiseAndSetIfChanged(ref _northing, value);
    }

    // Motion
    private double _heading;
    public double Heading
    {
        get => _heading;
        set => this.RaiseAndSetIfChanged(ref _heading, value);
    }

    private double _speed;
    public double Speed
    {
        get => _speed;
        set => this.RaiseAndSetIfChanged(ref _speed, value);
    }

    // GPS quality
    private int _fixQuality;
    public int FixQuality
    {
        get => _fixQuality;
        set => this.RaiseAndSetIfChanged(ref _fixQuality, value);
    }

    private int _satelliteCount;
    public int SatelliteCount
    {
        get => _satelliteCount;
        set => this.RaiseAndSetIfChanged(ref _satelliteCount, value);
    }

    private double _hdop;
    public double Hdop
    {
        get => _hdop;
        set => this.RaiseAndSetIfChanged(ref _hdop, value);
    }

    private double _age;
    public double Age
    {
        get => _age;
        set => this.RaiseAndSetIfChanged(ref _age, value);
    }

    // IMU data
    private double _imuRoll;
    public double ImuRoll
    {
        get => _imuRoll;
        set => this.RaiseAndSetIfChanged(ref _imuRoll, value);
    }

    private double _imuPitch;
    public double ImuPitch
    {
        get => _imuPitch;
        set => this.RaiseAndSetIfChanged(ref _imuPitch, value);
    }

    private double _imuYawRate;
    public double ImuYawRate
    {
        get => _imuYawRate;
        set => this.RaiseAndSetIfChanged(ref _imuYawRate, value);
    }

    // Computed properties
    public string FixQualityText => FixQuality switch
    {
        0 => "No Fix",
        1 => "GPS",
        2 => "DGPS",
        4 => "RTK Fix",
        5 => "RTK Float",
        _ => $"Unknown ({FixQuality})"
    };

    public bool HasValidFix => FixQuality > 0 && SatelliteCount >= 4;
    public bool HasRtkFix => FixQuality == 4;

    // Vec3 representation for guidance calculations
    public Vec3 PivotPosition => new Vec3(Easting, Northing, Heading);

    public void Reset()
    {
        Latitude = Longitude = Altitude = 0;
        Easting = Northing = Heading = Speed = 0;
        FixQuality = SatelliteCount = 0;
        Hdop = Age = 0;
        ImuRoll = ImuPitch = ImuYawRate = 0;
    }

    /// <summary>
    /// Update from GPS data (called by GpsService)
    /// </summary>
    public void UpdateFromGps(GpsData data)
    {
        Latitude = data.CurrentPosition.Latitude;
        Longitude = data.CurrentPosition.Longitude;
        Altitude = data.CurrentPosition.Altitude;
        Easting = data.CurrentPosition.Easting;
        Northing = data.CurrentPosition.Northing;
        Heading = data.CurrentPosition.Heading;
        Speed = data.CurrentPosition.Speed;
        FixQuality = data.FixQuality;
        SatelliteCount = data.SatellitesInUse;
        Hdop = data.Hdop;
        Age = data.DifferentialAge;
    }
}
```

#### 3. `GuidanceState.cs` - Steering and Path Following

```csharp
/// <summary>
/// Guidance calculation state - cross-track error, steering, goal points.
/// Updated by guidance service every frame when guidance is active.
/// </summary>
public class GuidanceState : ReactiveObject
{
    // Active track
    private ABLine? _activeTrack;
    public ABLine? ActiveTrack
    {
        get => _activeTrack;
        set => this.RaiseAndSetIfChanged(ref _activeTrack, value);
    }

    private bool _isGuidanceActive;
    public bool IsGuidanceActive
    {
        get => _isGuidanceActive;
        set => this.RaiseAndSetIfChanged(ref _isGuidanceActive, value);
    }

    // Cross-track error
    private double _crossTrackError;
    public double CrossTrackError
    {
        get => _crossTrackError;
        set => this.RaiseAndSetIfChanged(ref _crossTrackError, value);
    }

    private double _headingError;
    public double HeadingError
    {
        get => _headingError;
        set => this.RaiseAndSetIfChanged(ref _headingError, value);
    }

    // Steering output
    private double _steerAngle;
    public double SteerAngle
    {
        get => _steerAngle;
        set => this.RaiseAndSetIfChanged(ref _steerAngle, value);
    }

    private short _steerAngleRaw; // For UDP transmission
    public short SteerAngleRaw
    {
        get => _steerAngleRaw;
        set => this.RaiseAndSetIfChanged(ref _steerAngleRaw, value);
    }

    private short _distanceOffRaw; // For UDP transmission (mm)
    public short DistanceOffRaw
    {
        get => _distanceOffRaw;
        set => this.RaiseAndSetIfChanged(ref _distanceOffRaw, value);
    }

    // Pure Pursuit state (persisted between frames)
    private double _ppIntegral;
    public double PpIntegral
    {
        get => _ppIntegral;
        set => this.RaiseAndSetIfChanged(ref _ppIntegral, value);
    }

    private double _ppPivotDistanceError;
    public double PpPivotDistanceError
    {
        get => _ppPivotDistanceError;
        set => this.RaiseAndSetIfChanged(ref _ppPivotDistanceError, value);
    }

    private double _ppPivotDistanceErrorLast;
    public double PpPivotDistanceErrorLast
    {
        get => _ppPivotDistanceErrorLast;
        set => this.RaiseAndSetIfChanged(ref _ppPivotDistanceErrorLast, value);
    }

    private int _ppCounter;
    public int PpCounter
    {
        get => _ppCounter;
        set => this.RaiseAndSetIfChanged(ref _ppCounter, value);
    }

    // Visualization
    private Vec2 _goalPoint;
    public Vec2 GoalPoint
    {
        get => _goalPoint;
        set => this.RaiseAndSetIfChanged(ref _goalPoint, value);
    }

    private Vec2 _radiusPoint;
    public Vec2 RadiusPoint
    {
        get => _radiusPoint;
        set => this.RaiseAndSetIfChanged(ref _radiusPoint, value);
    }

    private double _purePursuitRadius;
    public double PurePursuitRadius
    {
        get => _purePursuitRadius;
        set => this.RaiseAndSetIfChanged(ref _purePursuitRadius, value);
    }

    // Direction
    private bool _isHeadingSameWay;
    public bool IsHeadingSameWay
    {
        get => _isHeadingSameWay;
        set => this.RaiseAndSetIfChanged(ref _isHeadingSameWay, value);
    }

    private bool _isReverse;
    public bool IsReverse
    {
        get => _isReverse;
        set => this.RaiseAndSetIfChanged(ref _isReverse, value);
    }

    // Line offset (how many passes from original)
    private int _howManyPathsAway;
    public int HowManyPathsAway
    {
        get => _howManyPathsAway;
        set => this.RaiseAndSetIfChanged(ref _howManyPathsAway, value);
    }

    private string _currentLineLabel = "1L";
    public string CurrentLineLabel
    {
        get => _currentLineLabel;
        set => this.RaiseAndSetIfChanged(ref _currentLineLabel, value);
    }

    // Contour mode
    private bool _isContourMode;
    public bool IsContourMode
    {
        get => _isContourMode;
        set => this.RaiseAndSetIfChanged(ref _isContourMode, value);
    }

    public void Reset()
    {
        ActiveTrack = null;
        IsGuidanceActive = false;
        CrossTrackError = HeadingError = SteerAngle = 0;
        PpIntegral = PpPivotDistanceError = PpPivotDistanceErrorLast = 0;
        PpCounter = 0;
        HowManyPathsAway = 0;
        CurrentLineLabel = "1L";
        IsContourMode = false;
    }

    /// <summary>
    /// Update from Pure Pursuit output
    /// </summary>
    public void UpdateFromPurePursuit(PurePursuitGuidanceOutput output)
    {
        CrossTrackError = output.DistanceFromCurrentLinePivot;
        HeadingError = output.ModeActualHeadingError;
        SteerAngle = output.SteerAngle;
        SteerAngleRaw = output.GuidanceLineSteerAngle;
        DistanceOffRaw = output.GuidanceLineDistanceOff;
        GoalPoint = output.GoalPoint;
        RadiusPoint = output.RadiusPoint;
        PurePursuitRadius = output.PurePursuitRadius;

        // Persist state for next iteration
        PpIntegral = output.Integral;
        PpPivotDistanceError = output.PivotDistanceError;
        PpPivotDistanceErrorLast = output.PivotDistanceErrorLast;
        PpCounter = output.Counter;
    }
}
```

#### 4. `SectionState.cs` - Section Control

```csharp
/// <summary>
/// Section control state - which sections are on, coverage mode.
/// </summary>
public class SectionState : ReactiveObject
{
    public const int MaxSections = 16;

    // Section on/off states
    private readonly bool[] _sectionActive = new bool[MaxSections];

    public bool GetSectionActive(int index) =>
        index >= 0 && index < MaxSections && _sectionActive[index];

    public void SetSectionActive(int index, bool active)
    {
        if (index >= 0 && index < MaxSections && _sectionActive[index] != active)
        {
            _sectionActive[index] = active;
            this.RaisePropertyChanged($"Section{index + 1}Active");
            UpdateActiveSectionCount();
        }
    }

    // Convenience properties for common section counts
    public bool Section1Active => GetSectionActive(0);
    public bool Section2Active => GetSectionActive(1);
    public bool Section3Active => GetSectionActive(2);
    public bool Section4Active => GetSectionActive(3);
    public bool Section5Active => GetSectionActive(4);
    public bool Section6Active => GetSectionActive(5);
    public bool Section7Active => GetSectionActive(6);
    public bool Section8Active => GetSectionActive(7);

    // Count
    private int _activeSectionCount;
    public int ActiveSectionCount
    {
        get => _activeSectionCount;
        private set => this.RaiseAndSetIfChanged(ref _activeSectionCount, value);
    }

    private void UpdateActiveSectionCount()
    {
        ActiveSectionCount = _sectionActive.Count(s => s);
    }

    // Master control
    private bool _isMasterOn;
    public bool IsMasterOn
    {
        get => _isMasterOn;
        set => this.RaiseAndSetIfChanged(ref _isMasterOn, value);
    }

    private bool _isManualMode;
    public bool IsManualMode
    {
        get => _isManualMode;
        set => this.RaiseAndSetIfChanged(ref _isManualMode, value);
    }

    private bool _isAutoMode;
    public bool IsAutoMode
    {
        get => _isAutoMode;
        set => this.RaiseAndSetIfChanged(ref _isAutoMode, value);
    }

    // Headland control
    private bool _isSectionControlInHeadland;
    public bool IsSectionControlInHeadland
    {
        get => _isSectionControlInHeadland;
        set => this.RaiseAndSetIfChanged(ref _isSectionControlInHeadland, value);
    }

    public void Reset()
    {
        for (int i = 0; i < MaxSections; i++)
            _sectionActive[i] = false;
        ActiveSectionCount = 0;
        IsMasterOn = false;
        IsManualMode = false;
        IsAutoMode = false;
    }

    /// <summary>
    /// Set all sections at once (from UDP message)
    /// </summary>
    public void SetAllSections(ushort sectionBits)
    {
        for (int i = 0; i < MaxSections; i++)
        {
            SetSectionActive(i, (sectionBits & (1 << i)) != 0);
        }
    }
}
```

#### 5. `ConnectionState.cs` - System Connectivity

```csharp
/// <summary>
/// Connection status for all external systems.
/// Updated by communication services.
/// </summary>
public class ConnectionState : ReactiveObject
{
    // GPS
    private bool _isGpsConnected;
    public bool IsGpsConnected
    {
        get => _isGpsConnected;
        set => this.RaiseAndSetIfChanged(ref _isGpsConnected, value);
    }

    private bool _isGpsDataOk;
    public bool IsGpsDataOk
    {
        get => _isGpsDataOk;
        set => this.RaiseAndSetIfChanged(ref _isGpsDataOk, value);
    }

    // NTRIP
    private bool _isNtripConnected;
    public bool IsNtripConnected
    {
        get => _isNtripConnected;
        set => this.RaiseAndSetIfChanged(ref _isNtripConnected, value);
    }

    private string _ntripStatus = "Not Connected";
    public string NtripStatus
    {
        get => _ntripStatus;
        set => this.RaiseAndSetIfChanged(ref _ntripStatus, value);
    }

    private ulong _ntripBytesReceived;
    public ulong NtripBytesReceived
    {
        get => _ntripBytesReceived;
        set => this.RaiseAndSetIfChanged(ref _ntripBytesReceived, value);
    }

    // AutoSteer module
    private bool _isAutoSteerConnected;
    public bool IsAutoSteerConnected
    {
        get => _isAutoSteerConnected;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerConnected, value);
    }

    private bool _isAutoSteerDataOk;
    public bool IsAutoSteerDataOk
    {
        get => _isAutoSteerDataOk;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerDataOk, value);
    }

    private bool _isAutoSteerEngaged;
    public bool IsAutoSteerEngaged
    {
        get => _isAutoSteerEngaged;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerEngaged, value);
    }

    // Machine module
    private bool _isMachineConnected;
    public bool IsMachineConnected
    {
        get => _isMachineConnected;
        set => this.RaiseAndSetIfChanged(ref _isMachineConnected, value);
    }

    private bool _isMachineDataOk;
    public bool IsMachineDataOk
    {
        get => _isMachineDataOk;
        set => this.RaiseAndSetIfChanged(ref _isMachineDataOk, value);
    }

    // IMU
    private bool _isImuConnected;
    public bool IsImuConnected
    {
        get => _isImuConnected;
        set => this.RaiseAndSetIfChanged(ref _isImuConnected, value);
    }

    private bool _isImuDataOk;
    public bool IsImuDataOk
    {
        get => _isImuDataOk;
        set => this.RaiseAndSetIfChanged(ref _isImuDataOk, value);
    }

    // Overall status
    public bool IsFullyConnected =>
        IsGpsConnected && IsAutoSteerConnected && IsMachineConnected;

    public string OverallStatus
    {
        get
        {
            if (!IsGpsConnected) return "No GPS";
            if (!IsAutoSteerConnected) return "No AutoSteer";
            if (!IsMachineConnected) return "No Machine";
            if (!IsNtripConnected) return "No RTK";
            return "Connected";
        }
    }
}
```

#### 6. `YouTurnState.cs` - U-Turn State Machine

```csharp
/// <summary>
/// YouTurn (automatic U-turn) state machine.
/// </summary>
public class YouTurnState : ReactiveObject
{
    // Enable/trigger
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    private bool _isTriggered;
    public bool IsTriggered
    {
        get => _isTriggered;
        set => this.RaiseAndSetIfChanged(ref _isTriggered, value);
    }

    private bool _isExecuting;
    public bool IsExecuting
    {
        get => _isExecuting;
        set => this.RaiseAndSetIfChanged(ref _isExecuting, value);
    }

    // Turn path
    private List<Vec3>? _turnPath;
    public List<Vec3>? TurnPath
    {
        get => _turnPath;
        set => this.RaiseAndSetIfChanged(ref _turnPath, value);
    }

    private int _pathIndex;
    public int PathIndex
    {
        get => _pathIndex;
        set => this.RaiseAndSetIfChanged(ref _pathIndex, value);
    }

    // Direction
    private bool _isTurnLeft;
    public bool IsTurnLeft
    {
        get => _isTurnLeft;
        set => this.RaiseAndSetIfChanged(ref _isTurnLeft, value);
    }

    private bool _lastTurnWasLeft;
    public bool LastTurnWasLeft
    {
        get => _lastTurnWasLeft;
        set => this.RaiseAndSetIfChanged(ref _lastTurnWasLeft, value);
    }

    // Distance tracking
    private double _distanceToHeadland;
    public double DistanceToHeadland
    {
        get => _distanceToHeadland;
        set => this.RaiseAndSetIfChanged(ref _distanceToHeadland, value);
    }

    private double _distanceToTrigger;
    public double DistanceToTrigger
    {
        get => _distanceToTrigger;
        set => this.RaiseAndSetIfChanged(ref _distanceToTrigger, value);
    }

    // Next track after turn
    private ABLine? _nextTrack;
    public ABLine? NextTrack
    {
        get => _nextTrack;
        set => this.RaiseAndSetIfChanged(ref _nextTrack, value);
    }

    // Completion tracking
    private Vec2? _lastCompletionPosition;
    public Vec2? LastCompletionPosition
    {
        get => _lastCompletionPosition;
        set => this.RaiseAndSetIfChanged(ref _lastCompletionPosition, value);
    }

    private bool _hasCompletedFirstTurn;
    public bool HasCompletedFirstTurn
    {
        get => _hasCompletedFirstTurn;
        set => this.RaiseAndSetIfChanged(ref _hasCompletedFirstTurn, value);
    }

    public void Reset()
    {
        IsTriggered = false;
        IsExecuting = false;
        TurnPath = null;
        PathIndex = 0;
        DistanceToHeadland = 0;
        NextTrack = null;
        HasCompletedFirstTurn = false;
    }

    public void CompleteTurn()
    {
        IsExecuting = false;
        IsTriggered = false;
        TurnPath = null;
        LastTurnWasLeft = IsTurnLeft;
        HasCompletedFirstTurn = true;
    }
}
```

#### 7. `UIState.cs` - UI and Dialog State

```csharp
/// <summary>
/// UI state - active dialogs, panels, selections.
/// Replaces 25+ dialog visibility flags with a proper dialog stack.
/// </summary>
public class UIState : ReactiveObject
{
    // Active dialog (only one modal at a time)
    private DialogType _activeDialog = DialogType.None;
    public DialogType ActiveDialog
    {
        get => _activeDialog;
        set
        {
            if (_activeDialog != value)
            {
                var previous = _activeDialog;
                this.RaiseAndSetIfChanged(ref _activeDialog, value);
                DialogChanged?.Invoke(this, new DialogChangedEventArgs(previous, value));
            }
        }
    }

    public bool IsDialogOpen => ActiveDialog != DialogType.None;

    // Convenience properties for XAML binding
    public bool IsFieldSelectionDialogVisible => ActiveDialog == DialogType.FieldSelection;
    public bool IsTracksDialogVisible => ActiveDialog == DialogType.Tracks;
    public bool IsConfigurationDialogVisible => ActiveDialog == DialogType.Configuration;
    public bool IsNewFieldDialogVisible => ActiveDialog == DialogType.NewField;
    public bool IsKmlImportDialogVisible => ActiveDialog == DialogType.KmlImport;
    public bool IsDataIODialogVisible => ActiveDialog == DialogType.DataIO;
    public bool IsHeadlandDialogVisible => ActiveDialog == DialogType.Headland;
    public bool IsAgShareSettingsDialogVisible => ActiveDialog == DialogType.AgShareSettings;
    // ... etc for all dialogs

    // Panel visibility (non-modal, can have multiple)
    private bool _isSimulatorPanelVisible;
    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isSimulatorPanelVisible, value);
    }

    private bool _isBoundaryPanelVisible;
    public bool IsBoundaryPanelVisible
    {
        get => _isBoundaryPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBoundaryPanelVisible, value);
    }

    private bool _isViewSettingsPanelVisible;
    public bool IsViewSettingsPanelVisible
    {
        get => _isViewSettingsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isViewSettingsPanelVisible, value);
    }

    // Selection state (shared across dialogs)
    private object? _selectedItem;
    public object? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    // Dialog events
    public event EventHandler<DialogChangedEventArgs>? DialogChanged;

    // Methods
    public void ShowDialog(DialogType dialog)
    {
        ActiveDialog = dialog;
    }

    public void CloseDialog()
    {
        ActiveDialog = DialogType.None;
        SelectedItem = null;
    }

    public void CloseAllPanels()
    {
        IsSimulatorPanelVisible = false;
        IsBoundaryPanelVisible = false;
        IsViewSettingsPanelVisible = false;
    }
}

public enum DialogType
{
    None,
    FieldSelection,
    Tracks,
    Configuration,
    NewField,
    FromExistingField,
    KmlImport,
    IsoXmlImport,
    BoundaryMap,
    NumericInput,
    AgShareSettings,
    AgShareUpload,
    AgShareDownload,
    DataIO,
    Headland,
    HeadlandBuilder,
    SimCoords,
    QuickABSelector,
    DrawAB
}

public class DialogChangedEventArgs : EventArgs
{
    public DialogType Previous { get; }
    public DialogType Current { get; }

    public DialogChangedEventArgs(DialogType previous, DialogType current)
    {
        Previous = previous;
        Current = current;
    }
}
```

#### 8. `FieldState.cs` - Active Field Data

```csharp
/// <summary>
/// Active field state - boundaries, tracks, headlands.
/// </summary>
public class FieldState : ReactiveObject
{
    private Field? _activeField;
    public Field? ActiveField
    {
        get => _activeField;
        set
        {
            this.RaiseAndSetIfChanged(ref _activeField, value);
            this.RaisePropertyChanged(nameof(HasActiveField));
            this.RaisePropertyChanged(nameof(FieldName));
        }
    }

    public bool HasActiveField => ActiveField != null;
    public string FieldName => ActiveField?.Name ?? "No Field";

    // Boundaries
    public ObservableCollection<Boundary> Boundaries { get; } = new();

    private Boundary? _currentBoundary;
    public Boundary? CurrentBoundary
    {
        get => _currentBoundary;
        set => this.RaiseAndSetIfChanged(ref _currentBoundary, value);
    }

    public bool HasBoundary => Boundaries.Count > 0;

    // Tracks
    public ObservableCollection<ABLine> Tracks { get; } = new();

    private ABLine? _activeTrack;
    public ABLine? ActiveTrack
    {
        get => _activeTrack;
        set => this.RaiseAndSetIfChanged(ref _activeTrack, value);
    }

    public bool HasActiveTrack => ActiveTrack != null;

    // Headlands
    private List<Vec3>? _headlandLine;
    public List<Vec3>? HeadlandLine
    {
        get => _headlandLine;
        set
        {
            this.RaiseAndSetIfChanged(ref _headlandLine, value);
            this.RaisePropertyChanged(nameof(HasHeadland));
        }
    }

    public bool HasHeadland => HeadlandLine != null && HeadlandLine.Count > 0;

    // Field origin
    private double _originLatitude;
    public double OriginLatitude
    {
        get => _originLatitude;
        set => this.RaiseAndSetIfChanged(ref _originLatitude, value);
    }

    private double _originLongitude;
    public double OriginLongitude
    {
        get => _originLongitude;
        set => this.RaiseAndSetIfChanged(ref _originLongitude, value);
    }

    public void Reset()
    {
        ActiveField = null;
        Boundaries.Clear();
        CurrentBoundary = null;
        Tracks.Clear();
        ActiveTrack = null;
        HeadlandLine = null;
        OriginLatitude = OriginLongitude = 0;
    }

    public void LoadFromField(Field field)
    {
        ActiveField = field;
        // Populate boundaries, tracks, etc. from field data
    }
}
```

#### 9. `BoundaryState.cs` - Boundary Recording

```csharp
/// <summary>
/// Boundary recording state.
/// </summary>
public class BoundaryState : ReactiveObject
{
    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set => this.RaiseAndSetIfChanged(ref _isRecording, value);
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    private int _pointCount;
    public int PointCount
    {
        get => _pointCount;
        set => this.RaiseAndSetIfChanged(ref _pointCount, value);
    }

    private double _areaHectares;
    public double AreaHectares
    {
        get => _areaHectares;
        set => this.RaiseAndSetIfChanged(ref _areaHectares, value);
    }

    private bool _isDrawRightSide = true;
    public bool IsDrawRightSide
    {
        get => _isDrawRightSide;
        set => this.RaiseAndSetIfChanged(ref _isDrawRightSide, value);
    }

    private bool _isDrawAtPivot;
    public bool IsDrawAtPivot
    {
        get => _isDrawAtPivot;
        set => this.RaiseAndSetIfChanged(ref _isDrawAtPivot, value);
    }

    private double _boundaryOffset;
    public double BoundaryOffset
    {
        get => _boundaryOffset;
        set => this.RaiseAndSetIfChanged(ref _boundaryOffset, value);
    }

    // Current recording points
    public ObservableCollection<Vec2> RecordingPoints { get; } = new();

    public void Reset()
    {
        IsRecording = false;
        IsPaused = false;
        PointCount = 0;
        AreaHectares = 0;
        RecordingPoints.Clear();
    }
}
```

### Simplified MainViewModel

With state centralized, MainViewModel becomes a thin coordination layer:

```csharp
/// <summary>
/// Main view model - thin layer that coordinates between state and UI.
/// NO business logic, NO state storage.
/// </summary>
public class MainViewModel : ReactiveObject
{
    // State access (single source of truth)
    public ApplicationState State => ApplicationState.Instance;

    // Convenience accessors
    public VehicleState Vehicle => State.Vehicle;
    public GuidanceState Guidance => State.Guidance;
    public SectionState Sections => State.Sections;
    public ConnectionState Connections => State.Connections;
    public FieldState Field => State.Field;
    public YouTurnState YouTurn => State.YouTurn;
    public UIState UI => State.UI;

    // Services (for commands to invoke)
    private readonly IGpsService _gpsService;
    private readonly IFieldService _fieldService;
    private readonly INtripClientService _ntripService;
    // ... minimal set of services

    // Commands
    public ICommand OpenFieldCommand { get; }
    public ICommand CloseFieldCommand { get; }
    public ICommand StartBoundaryRecordingCommand { get; }
    public ICommand ToggleAutoSteerCommand { get; }
    public ICommand ShowDialogCommand { get; }
    public ICommand NudgeTrackCommand { get; }
    // ... etc

    public MainViewModel(
        IGpsService gpsService,
        IFieldService fieldService,
        INtripClientService ntripService)
    {
        _gpsService = gpsService;
        _fieldService = fieldService;
        _ntripService = ntripService;

        // Subscribe to service events that update state
        _gpsService.GpsDataUpdated += (s, data) => State.Vehicle.UpdateFromGps(data);
        _ntripService.ConnectionStatusChanged += (s, e) => State.Connections.IsNtripConnected = e.IsConnected;

        // Initialize commands
        OpenFieldCommand = ReactiveCommand.Create<string>(OpenField);
        CloseFieldCommand = ReactiveCommand.Create(CloseField);
        ShowDialogCommand = ReactiveCommand.Create<DialogType>(d => UI.ShowDialog(d));
        // ... etc
    }

    private void OpenField(string fieldName)
    {
        var field = _fieldService.Load(fieldName);
        if (field != null)
        {
            State.Field.LoadFromField(field);
        }
    }

    private void CloseField()
    {
        _fieldService.Close();
        State.Reset();
    }
}
```

**Result: ~300-500 lines instead of 7,045 lines**

## Service Changes

Services update ApplicationState instead of raising events:

```csharp
// Before (event-based)
public class GpsService : IGpsService
{
    public event EventHandler<GpsData>? GpsDataUpdated;

    private void OnGpsReceived(GpsData data)
    {
        CurrentData = data;
        GpsDataUpdated?.Invoke(this, data);
    }
}

// After (state-based)
public class GpsService : IGpsService
{
    private readonly ApplicationState _state;

    public GpsService(ApplicationState state)
    {
        _state = state;
    }

    private void OnGpsReceived(GpsData data)
    {
        _state.Vehicle.UpdateFromGps(data);
    }
}
```

## Migration Strategy

### Phase 1: Create State Infrastructure
1. Create `ApplicationState.cs` container
2. Create individual state classes (VehicleState, GuidanceState, etc.)
3. Register as singleton in DI
4. Keep MainViewModel unchanged initially

### Phase 2: Migrate State Reads
1. Change UI bindings from `{Binding Latitude}` to `{Binding State.Vehicle.Latitude}`
2. Update services to read from ApplicationState
3. MainViewModel proxies to State temporarily

### Phase 3: Migrate State Writes
1. Update GpsService to write to ApplicationState.Vehicle
2. Update guidance calculation to write to ApplicationState.Guidance
3. Update other services similarly
4. Remove duplicate state from MainViewModel

### Phase 4: Migrate Dialogs to UIState
1. Replace 25+ visibility flags with `UIState.ActiveDialog`
2. Update dialog XAML bindings
3. Extract dialog-specific data to appropriate state classes

### Phase 5: Cleanup
1. Remove unused fields from MainViewModel
2. Remove unused events
3. Remove redundant service state

## Files Summary

### Files to Create (~800 lines total)

| File | Est. Lines | Purpose |
|------|------------|---------|
| `ApplicationState.cs` | 50 | Container |
| `VehicleState.cs` | 120 | Position/motion |
| `GuidanceState.cs` | 150 | Steering/guidance |
| `SectionState.cs` | 80 | Section control |
| `ConnectionState.cs` | 100 | Connectivity |
| `YouTurnState.cs` | 100 | U-turn state machine |
| `FieldState.cs` | 100 | Field data |
| `BoundaryState.cs` | 60 | Boundary recording |
| `UIState.cs` | 80 | Dialogs/panels |

### Files to Modify

| File | Before | After | Reduction |
|------|--------|-------|-----------|
| `MainViewModel.cs` | 7,045 | ~500 | **93%** |
| `GpsService.cs` | Minor changes | | |
| `NtripClientService.cs` | Minor changes | | |
| Various services | Update to use state | | |

## Expected Outcome

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| MainViewModel lines | 7,045 | ~500 | **-93%** |
| Private fields in MainViewModel | 207 | ~10 | **-95%** |
| Dialog visibility flags | 25+ | 1 enum | **-96%** |
| Duplicate GPS state | 3 places | 1 place | **-67%** |
| State classes | 0 | 9 | Organized |
| Total state lines | Scattered | ~800 | Centralized |

## Benefits

1. **Single Source of Truth**: Any component can access current state
2. **No God Object**: MainViewModel becomes a thin coordinator
3. **Testable**: State classes can be tested in isolation
4. **Debuggable**: `ApplicationState.CreateSnapshot()` for logging
5. **Observable**: ReactiveUI bindings work seamlessly
6. **Maintainable**: Clear ownership of each state domain
7. **Scalable**: Easy to add new state without bloating MainViewModel
