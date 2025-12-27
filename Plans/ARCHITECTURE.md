# AgValoniaGPS3 Architecture

This document describes the current architecture of AgValoniaGPS3, including service communication patterns, state management, domain models, and data flow.

**Last Updated:** December 2025

---

## Table of Contents

1. [Overview](#overview)
2. [Project Structure](#project-structure)
3. [Domain Models](#domain-models)
4. [Configuration System](#configuration-system)
5. [State Management](#state-management)
6. [Service Architecture](#service-architecture)
7. [Guidance Algorithms](#guidance-algorithms)
8. [UDP Protocol & PGN Format](#udp-protocol--pgn-format)
9. [View Layer](#view-layer)
10. [Data Flow Diagrams](#data-flow-diagrams)
11. [Performance Characteristics](#performance-characteristics)
12. [Coupling Analysis](#coupling-analysis)

---

## Overview

AgValoniaGPS3 is a cross-platform agricultural GPS guidance application built with:
- **Avalonia UI 11.3** - Cross-platform UI framework
- **ReactiveUI 20.1** - MVVM with reactive extensions
- **.NET 10.0** - Target framework
- **Microsoft.Extensions.DependencyInjection** - Dependency injection

The architecture achieves **91.7% shared code** across platforms (Windows, macOS, Linux, iOS, Android).

### Design Principles

1. **Unified Track Model** - "An AB line is just a curve with 2 points" (Brian, AgOpenGPS creator)
2. **Zero-Copy Hot Path** - GPS→Guidance→PGN pipeline has ~0.1ms latency
3. **Reactive State** - All state objects use ReactiveUI for automatic UI binding
4. **Platform Abstraction** - Shared views with platform-specific services only where necessary

---

## Project Structure

```
AgValoniaGPS3/
├── Shared/                              # 91.7% - Platform-agnostic code
│   ├── AgValoniaGPS.Models/            # Data models, geometry, state, configuration
│   │   ├── Base/                       # Core types: Vec2, Vec3, GeometryMath
│   │   ├── Configuration/              # Config models: VehicleConfig, ToolConfig, etc.
│   │   ├── State/                      # Runtime state: VehicleState, GuidanceState
│   │   ├── Track/                      # Unified Track model, guidance I/O
│   │   ├── YouTurn/                    # U-turn path models
│   │   ├── Field/                      # Field, boundaries, flags
│   │   └── Communication/              # UDP/PGN message models
│   │
│   ├── AgValoniaGPS.Services/          # Business logic
│   │   ├── AutoSteer/                  # AutoSteerService, PgnBuilder
│   │   ├── Track/                      # TrackGuidanceService, TrackNudgingService
│   │   ├── YouTurn/                    # YouTurnCreationService, YouTurnGuidanceService
│   │   ├── Geometry/                   # PolygonOffset, FenceArea, WorkedArea
│   │   ├── Headland/                   # HeadlandBuilderService
│   │   ├── AgShare/                    # Cloud sync services
│   │   ├── IsoXml/                     # ISOBUS XML import/export
│   │   └── Interfaces/                 # Service interfaces
│   │
│   ├── AgValoniaGPS.ViewModels/        # MVVM ViewModels
│   │   ├── MainViewModel.cs            # Main orchestrator (26+ dependencies)
│   │   └── ConfigurationViewModel.cs   # Configuration dialog logic
│   │
│   └── AgValoniaGPS.Views/             # Shared UI
│       ├── Controls/
│       │   ├── DrawingContextMapControl.cs  # Map rendering (30 FPS)
│       │   ├── Panels/                 # Navigation, simulator, section controls
│       │   └── Dialogs/                # Configuration, field selection, etc.
│       ├── Converters/                 # Value converters for XAML
│       └── Assets/                     # Icons, images
│
├── Platforms/                           # 8.3% - Platform-specific code
│   ├── AgValoniaGPS.Desktop/           # Windows/macOS/Linux
│   │   ├── Views/MainWindow.axaml      # Desktop window with drag handlers
│   │   ├── Services/                   # DialogService, MapService
│   │   └── DependencyInjection/        # DI registration
│   │
│   ├── AgValoniaGPS.iOS/               # iOS/iPadOS
│   │   ├── Views/MainView.axaml        # iOS view with drag handlers
│   │   ├── Services/                   # Platform services
│   │   └── DependencyInjection/        # DI registration
│   │
│   └── AgValoniaGPS.Android/           # Android
│
├── TestRunner/                          # Test harness for guidance algorithms
├── Reference/                           # PGN specs, protocol documentation
└── Plans/                               # Architecture and implementation plans
```

---

## Domain Models

### Core Geometry Types

Located in `Shared/AgValoniaGPS.Models/Base/`:

#### Vec2 - 2D Point
```csharp
public struct Vec2
{
    public double Easting { get; set; }   // X coordinate (meters)
    public double Northing { get; set; }  // Y coordinate (meters)
}
```

#### Vec3 - 2D Point with Heading
```csharp
public struct Vec3
{
    public double Easting { get; set; }   // X coordinate (meters)
    public double Northing { get; set; }  // Y coordinate (meters)
    public double Heading { get; set; }   // Heading in radians

    public Vec2 ToVec2() => new Vec2(Easting, Northing);
    public double GetLength() => Math.Sqrt(Easting * Easting + Northing * Northing);
}
```

#### GeometryMath - Utility Functions
```csharp
public static class GeometryMath
{
    // Unit conversion constants
    public const double m2ft = 3.28084;
    public const double ft2m = 0.3048;
    public const double ha2ac = 2.47105;
    public const double twoPI = 6.28318530717958647692;
    public const double PIBy2 = 1.57079632679489661923;

    // Angle conversions
    public static double ToDegrees(double radians);
    public static double ToRadians(double degrees);
    public static double AngleDiff(double angle1, double angle2);  // Range 0–π

    // Distance calculations
    public static double Distance(Vec2 first, Vec2 second);
    public static double Distance(Vec3 first, Vec3 second);
    public static double DistanceSquared(Vec3 first, Vec3 second);  // Faster for comparisons

    // Polygon operations
    public static bool IsPointInPolygon(IReadOnlyList<Vec3> polygon, Vec3 testPoint);
    public static Vec2? RaycastToPolygon(Vec3 origin, IReadOnlyList<Vec3> polygon);

    // Splines
    public static Vec3 Catmull(double t, Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3);
}
```

### Unified Track Model

The key architectural insight: **"An AB line is just a curve with 2 points."**

Located in `Shared/AgValoniaGPS.Models/Track/Track.cs`:

```csharp
public enum TrackType
{
    ABLine = 2,         // 2 points, infinite extension
    Curve = 4,          // N points, finite
    BoundaryOuter = 8,  // Boundary offset outward
    BoundaryInner = 16, // Boundary offset inward
    BoundaryCurve = 32, // Boundary curve
    WaterPivot = 64     // Circular, closed loop
}

public class Track
{
    public string Name { get; set; }
    public List<Vec3> Points { get; set; }    // AB lines have 2, curves have N
    public TrackType Type { get; set; }
    public bool IsClosed { get; set; }        // Water pivot, boundary tracks
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public double NudgeDistance { get; set; } // Accumulated offset (+ = right)

    // Computed properties
    public bool IsABLine => Points.Count == 2 && Type == TrackType.ABLine;
    public bool IsCurve => Points.Count > 2 || Type == TrackType.Curve;
    public double Heading => Math.Atan2(Points[1].Easting - Points[0].Easting,
                                         Points[1].Northing - Points[0].Northing);

    // Factory methods
    public static Track FromABLine(string name, Vec3 pointA, Vec3 pointB);
    public static Track FromCurve(string name, List<Vec3> points, bool isClosed = false);
    public static Track FromABLine(ABLine legacy);  // Migration helper
    public ABLine ToABLine();                       // File compatibility
}
```

**Benefits of unified track:**
- Single `TrackGuidanceService` handles all track types
- Same Pure Pursuit/Stanley algorithms work for AB lines and curves
- Reduced ~2,100 lines of duplicated guidance code
- Simplified file I/O and state management

---

## Configuration System

### ConfigurationStore (Static Singleton)

Central repository for all user-configurable settings. Located in `Shared/AgValoniaGPS.Models/Configuration/ConfigurationStore.cs`:

```csharp
public class ConfigurationStore : ReactiveObject
{
    public static ConfigurationStore Instance { get; }

    public VehicleConfig Vehicle { get; }      // Physical dimensions, steering limits
    public ToolConfig Tool { get; }            // Implement width, sections, hitch
    public GuidanceConfig Guidance { get; }    // U-turn params, tram lines, algorithms
    public DisplayConfig Display { get; }      // Grid, day/night, camera settings
    public ConnectionConfig Connection { get; } // NTRIP, GPS quality, UDP ports
    public MachineConfig Machine { get; }      // Hydraulics, pin assignments
    public AhrsConfig Ahrs { get; }            // IMU settings, roll compensation
    public SimulatorConfig Simulator { get; }  // Simulation parameters

    public event EventHandler? ProfileLoaded;
    public event EventHandler? ProfileSaved;
}
```

### Configuration Sub-Models

All inherit from `ReactiveObject` for automatic UI binding:

#### VehicleConfig
```csharp
public class VehicleConfig : ReactiveObject
{
    public string Name { get; set; }
    public VehicleType Type { get; set; }        // Tractor, Harvester, FourWD
    public double Wheelbase { get; set; }        // meters
    public double TrackWidth { get; set; }       // meters
    public double AntennaHeight { get; set; }    // meters
    public double AntennaPivot { get; set; }     // meters (fore/aft offset)
    public double AntennaOffset { get; set; }    // meters (lateral offset)
    public double MaxSteerAngle { get; set; }    // degrees
    public double MaxAngularVelocity { get; set; }

    // Computed
    public double MinTurningRadius => Wheelbase / Math.Tan(MaxSteerAngle * π/180);
}
```

#### ToolConfig
```csharp
public class ToolConfig : ReactiveObject
{
    public double Width { get; set; }            // meters
    public double Overlap { get; set; }          // meters
    public double Offset { get; set; }           // lateral offset
    public double HitchLength { get; set; }      // meters to hitch point
    public double TrailingHitchLength { get; set; }
    public double LookAheadOnSetting { get; set; }
    public double LookAheadOffSetting { get; set; }
    public double TurnOffDelay { get; set; }
    public int NumSections { get; set; }
    public double[] SectionWidths { get; set; }
    public bool IsToolTrailing { get; set; }
    public bool IsWorkSwitchEnabled { get; set; }
    public bool IsWorkSwitchActiveLow { get; set; }
}
```

#### GuidanceConfig
```csharp
public class GuidanceConfig : ReactiveObject
{
    // Pure Pursuit
    public double GoalPointDistance { get; set; }
    public double PurePursuitIntegralGain { get; set; }

    // Stanley
    public double StanleyHeadingErrorGain { get; set; }
    public double StanleyDistanceErrorGain { get; set; }
    public double StanleyIntegralGain { get; set; }

    // U-Turn
    public double UTurnRadius { get; set; }
    public double UTurnExtension { get; set; }
    public double UTurnDistanceFromBoundary { get; set; }
    public double UTurnSmoothing { get; set; }

    // Tram Lines
    public bool TramLinesEnabled { get; set; }
    public int TramPasses { get; set; }
}
```

#### ConnectionConfig
```csharp
public class ConnectionConfig : ReactiveObject
{
    // NTRIP
    public string NtripCasterHost { get; set; }
    public int NtripCasterPort { get; set; }
    public string NtripMountPoint { get; set; }
    public string NtripUsername { get; set; }
    public string NtripPassword { get; set; }
    public bool NtripAutoConnect { get; set; }

    // GPS Quality
    public int MinFixQuality { get; set; }       // 0=None, 1=GPS, 2=DGPS, 4=RTK Fix
    public double MaxHdop { get; set; }
    public double MaxDifferentialAge { get; set; }

    // Dual GPS
    public bool IsDualGps { get; set; }
    public double DualHeadingOffset { get; set; }
    public double DualSwitchSpeed { get; set; }

    // Heading
    public double HeadingFusionWeight { get; set; }  // GPS vs IMU blend
}
```

### Configuration Data Flow

```
User Input (UI) → ConfigurationViewModel → ConfigurationStore.Instance
                                                    ↓
                                           RaiseAndSetIfChanged()
                                                    ↓
                              ┌─────────────────────┴─────────────────────┐
                              ↓                                           ↓
                    UI Binding Updates                          Services Read On Use
                    (automatic via ReactiveUI)          (ConfigurationStore.Instance.X.Y)
```

---

## State Management

### ApplicationState (Static Singleton)

Runtime state that is **not** persisted. Located in `Shared/AgValoniaGPS.Models/State/ApplicationState.cs`:

```csharp
public class ApplicationState : ReactiveObject
{
    public static ApplicationState Instance { get; }

    public VehicleState Vehicle { get; }       // Position, heading, speed, GPS quality
    public GuidanceState Guidance { get; }     // Active track, XTE, steer angle
    public SectionState Sections { get; }      // Section on/off states
    public ConnectionState Connections { get; } // Module connection status
    public FieldState Field { get; }           // Active field, boundaries
    public YouTurnState YouTurn { get; }       // U-turn path, state
    public BoundaryRecState BoundaryRec { get; } // Recording state
    public SimulatorState Simulator { get; }   // Simulator mode state
    public UIState UI { get; }                 // Dialog visibility, panel states

    public event EventHandler? StateReset;
}
```

### State Sub-Models

#### VehicleState
```csharp
public class VehicleState : ReactiveObject
{
    // Position
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Easting { get; set; }
    public double Northing { get; set; }
    public double Heading { get; set; }        // radians
    public double Speed { get; set; }          // m/s

    // GPS Quality
    public int FixQuality { get; set; }
    public int Satellites { get; set; }
    public double Hdop { get; set; }
    public double DifferentialAge { get; set; }

    // IMU
    public double Roll { get; set; }
    public double Pitch { get; set; }
    public double YawRate { get; set; }
}
```

#### GuidanceState
```csharp
public class GuidanceState : ReactiveObject
{
    public Track? ActiveTrack { get; set; }
    public double CrossTrackError { get; set; }    // meters
    public double SteerAngle { get; set; }         // degrees
    public double HeadingError { get; set; }       // degrees
    public bool IsOnTrack { get; set; }
    public int CurrentTrackIndex { get; set; }
    public double DistanceToTurn { get; set; }
}
```

### ReactiveUI Property Change Pattern

All state objects use consistent pattern:

```csharp
public class ExampleState : ReactiveObject
{
    private double _value;
    public double Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }
}
```

This enables:
- Automatic UI binding updates via Avalonia
- Observable sequences via `WhenAnyValue()`
- Computed property dependencies via `RaisePropertyChanged()`

---

## Service Architecture

### Dependency Injection Setup

All services registered as **singletons** in `ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddAgValoniaServices(this IServiceCollection services)
{
    // State
    services.AddSingleton<ApplicationState>();

    // Communication
    services.AddSingleton<IUdpCommunicationService, UdpCommunicationService>();
    services.AddSingleton<INtripClientService, NtripClientService>();

    // GPS & AutoSteer
    services.AddSingleton<IGpsService, GpsService>();
    services.AddSingleton<IAutoSteerService, AutoSteerService>();

    // Guidance
    services.AddSingleton<ITrackGuidanceService, TrackGuidanceService>();
    services.AddSingleton<IYouTurnGuidanceService, YouTurnGuidanceService>();
    services.AddSingleton<ITrackNudgingService, TrackNudgingService>();

    // Field & Boundary
    services.AddSingleton<IFieldService, FieldService>();
    services.AddSingleton<IBoundaryRecordingService, BoundaryRecordingService>();
    services.AddSingleton<IHeadlandBuilderService, HeadlandBuilderService>();

    // Geometry
    services.AddSingleton<IPolygonOffsetService, PolygonOffsetService>();
    services.AddSingleton<IWorkedAreaService, WorkedAreaService>();
    services.AddSingleton<ITramlineService, TramlineService>();

    // Configuration
    services.AddSingleton<IConfigurationService, ConfigurationService>();
    services.AddSingleton<IVehicleProfileService, VehicleProfileService>();
    services.AddSingleton<IDisplaySettingsService, DisplaySettingsService>();

    // Hardware
    services.AddSingleton<IModuleCommunicationService, ModuleCommunicationService>();
    services.AddSingleton<IGpsSimulationService, GpsSimulationService>();

    // ViewModels
    services.AddTransient<MainViewModel>();
    services.AddTransient<ConfigurationViewModel>();

    return services;
}

public static void WireUpServices(this IServiceProvider serviceProvider)
{
    // Post-container wiring for zero-copy dependencies
    var udpService = serviceProvider.GetRequiredService<IUdpCommunicationService>()
                     as UdpCommunicationService;
    var autoSteerService = serviceProvider.GetRequiredService<IAutoSteerService>();
    udpService?.SetAutoSteerService(autoSteerService);
}
```

### Service Communication Patterns

#### Pattern 1: Event-Based Communication

| Service | Events |
|---------|--------|
| `UdpCommunicationService` | `DataReceived`, `ModuleConnectionChanged` |
| `GpsService` | `GpsDataUpdated` |
| `AutoSteerService` | `StateUpdated` |
| `NtripClientService` | `ConnectionStatusChanged`, `RtcmDataReceived` |
| `BoundaryRecordingService` | `StateChanged`, `PointAdded` |
| `ModuleCommunicationService` | `AutoSteerToggleRequested`, `SectionMasterToggleRequested` |
| `ConfigurationService` | `ProfileLoaded`, `ProfileSaved` |
| `FieldService` | `ActiveFieldChanged` |

#### Pattern 2: Direct Service Calls (Hot Path)

For latency-critical operations:

```csharp
// UdpCommunicationService.cs
if (_receiveBuffer[0] == (byte)'$' && _autoSteerService != null)
{
    // Direct call - no event, no copy, ~0.1ms total
    _autoSteerService.ProcessGpsBuffer(_receiveBuffer, bytesReceived);
}
```

#### Pattern 3: Static Singleton Access

Services read configuration directly:

```csharp
// Any service
var wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
var minFix = ConfigurationStore.Instance.Connection.MinFixQuality;
```

### Service Inventory

#### Core Services

| Service | Purpose |
|---------|---------|
| `UdpCommunicationService` | UDP send/receive on port 9999, module broadcast on 8888 |
| `AutoSteerService` | Zero-copy GPS→Guidance→PGN pipeline |
| `GpsService` | GPS data processing, event firing |
| `NtripClientService` | NTRIP RTK corrections via HTTP/1.1 |

#### Guidance Services

| Service | Purpose |
|---------|---------|
| `TrackGuidanceService` | Pure Pursuit + Stanley algorithms for all track types |
| `YouTurnGuidanceService` | U-turn path following |
| `YouTurnCreationService` | Generate U-turn paths from boundaries |
| `TrackNudgingService` | Shift tracks left/right |
| `TramlineService` | Tramline calculations |

#### Field Services

| Service | Purpose |
|---------|---------|
| `FieldService` | Field loading/saving/management |
| `BoundaryRecordingService` | Record field boundaries from GPS |
| `HeadlandBuilderService` | Generate headland zones |
| `HeadlandDetectionService` | Detect when entering/exiting headland |

#### Geometry Services

| Service | Purpose |
|---------|---------|
| `PolygonOffsetService` | Offset boundaries inward/outward |
| `WorkedAreaService` | Track coverage area |
| `FenceAreaService` | Calculate enclosed areas |
| `FenceLineService` | Fence/boundary line operations |

---

## Guidance Algorithms

### TrackGuidanceService

Located in `Shared/AgValoniaGPS.Services/Track/TrackGuidanceService.cs`.

Handles both **Pure Pursuit** and **Stanley** algorithms for all track types (AB lines and curves).

#### Input/Output Models

```csharp
public class TrackGuidanceInput
{
    public Track Track { get; set; }
    public Vec3 PivotPosition { get; set; }      // Vehicle pivot point
    public Vec3 SteerPosition { get; set; }      // Front axle position
    public double FixHeading { get; set; }       // Current heading (radians)
    public double Wheelbase { get; set; }
    public double MaxSteerAngle { get; set; }
    public double GoalPointDistance { get; set; } // Pure Pursuit lookahead
    public double AvgSpeed { get; set; }         // km/h
    public double ImuRoll { get; set; }          // Roll angle for sidehill comp
    public double SideHillCompFactor { get; set; }
    public bool UseStanley { get; set; }
    public bool IsReverse { get; set; }
    public bool IsHeadingSameWay { get; set; }
    public bool IsAutoSteerOn { get; set; }
    public TrackGuidanceState? PreviousState { get; set; }

    // Stanley-specific
    public double StanleyHeadingErrorGain { get; set; }
    public double StanleyDistanceErrorGain { get; set; }
    public double StanleyIntegralGain { get; set; }

    // Pure Pursuit-specific
    public double PurePursuitIntegralGain { get; set; }
}

public class TrackGuidanceOutput
{
    public double SteerAngle { get; set; }          // degrees
    public double CrossTrackError { get; set; }     // meters
    public double DistanceFromLinePivot { get; set; }
    public double HeadingErrorDegrees { get; set; }
    public Vec2 GoalPoint { get; set; }             // Pure Pursuit target
    public Vec2 ClosestPointPivot { get; set; }
    public double PurePursuitRadius { get; set; }
    public short GuidanceLineDistanceOff { get; set; }  // For PGN (mm)
    public short GuidanceLineSteerAngle { get; set; }   // For PGN (degrees*100)
    public bool IsAtEndOfTrack { get; set; }
    public TrackGuidanceState State { get; set; }
}
```

#### Pure Pursuit Algorithm

```
1. Find nearest segment on track (indexA, indexB)
2. Calculate cross-track error (perpendicular distance to line)
3. Find closest point on segment
4. Project goal point ahead by GoalPointDistance:
   - AB Line: project along infinite line
   - Curve: walk along curve points
5. Calculate lateral offset to goal point
6. Apply Pure Pursuit formula:
   SteerAngle = atan(2 * lateralOffset * wheelbase / goalPointDistSq)
7. Apply integral correction for steady-state error
8. Apply sidehill compensation (if IMU roll available)
9. Clamp to MaxSteerAngle
```

#### Stanley Algorithm

```
1. Find nearest segment on track (indexA, indexB)
2. Calculate cross-track error at steer axle position
3. Apply integral offset to create virtual offset line
4. Calculate heading error (vehicle heading vs track heading)
5. Apply Stanley formula:
   headingComponent = headingError * headingGain
   xteComponent = atan(distanceGain * XTE / speed)
   SteerAngle = -(headingComponent + xteComponent)
6. Apply sidehill compensation
7. Clamp to MaxSteerAngle
```

#### Key Formulas

**Cross-Track Error (signed perpendicular distance):**
```
XTE = ((dz * point.Easting) - (dx * point.Northing)
       + (B.Easting * A.Northing) - (B.Northing * A.Easting))
      / sqrt(dz² + dx²)
```

**Pure Pursuit Radius:**
```
radius = goalPointDistSq / (2 * lateralOffset)
```

**Pure Pursuit Steer Angle:**
```
steerAngle = atan(2 * wheelbase * sin(α) / lookAheadDistance)
           = atan(2 * lateralOffset * wheelbase / goalPointDistSq)
```

---

## UDP Protocol & PGN Format

### Network Configuration

| Port | Direction | Purpose |
|------|-----------|---------|
| 9999 | Receive | GPS/PANDA sentences from AiO board |
| 8888 | Send | PGN messages to modules (broadcast 192.168.5.255) |

### PGN Message Format

All messages follow AgOpenGPS standard:

```
[0x80, 0x81, Source, PGN, Length, Data..., CRC]
```

| Byte | Value | Description |
|------|-------|-------------|
| 0 | 0x80 | Header 1 |
| 1 | 0x81 | Header 2 |
| 2 | 0x7F | Source (AgIO/AgOpenGPS) |
| 3 | PGN | Message type |
| 4 | Length | Data bytes count |
| 5-N | Data | Payload |
| N+1 | CRC | Sum of bytes 2 through N |

### PGN 254 (0xFE) - AutoSteer Data

Sent at 10Hz (every GPS update):

```
Byte 5-6:  Speed (high/low, km/h × 10)
Byte 7:    Status
           Bit 0: Steer switch active
           Bit 1: Work switch active
           Bit 2: AutoSteer engaged
           Bit 3: GPS valid
           Bit 4: Guidance valid
Byte 8-9:  SteerAngle × 100 (high/low, signed)
Byte 10:   XTE (cross-track error, cm, signed byte)
Byte 11:   SC1to8 (sections 1-8 bitmask)
Byte 12:   SC9to16 (sections 9-16 bitmask)
Byte 13:   CRC
```

### PGN 239 (0xEF) - Machine Data

```
Byte 5:  U-turn state
Byte 6:  Speed × 10 (single byte)
Byte 7:  Hydraulic lift state
Byte 8:  Tramline state
Byte 9:  Geo Stop
Byte 10: Reserved
Byte 11: SC1to8
Byte 12: SC9to16
Byte 13: CRC
```

### PgnBuilder (Zero-Allocation)

Located in `Shared/AgValoniaGPS.Services/AutoSteer/PgnBuilder.cs`:

```csharp
public static class PgnBuilder
{
    // Thread-local buffers for zero allocation
    [ThreadStatic]
    private static byte[]? _autoSteerBuffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] BuildAutoSteerPgn(ref VehicleState state)
    {
        _autoSteerBuffer ??= new byte[14];
        var buf = _autoSteerBuffer;

        // Build packet directly into reused buffer
        buf[0] = 0x80; buf[1] = 0x81; buf[2] = 0x7F;
        buf[3] = 0xFE; buf[4] = 8;
        // ... populate data ...
        buf[13] = CalculateCrc(buf, 2, 11);

        return buf;
    }
}
```

---

## View Layer

### View Hierarchy

```
Platform MainWindow/MainView
├── DrawingContextMapControl          # Map rendering (30 FPS)
├── LeftNavigationPanel               # Main navigation sidebar
│   ├── FileMenuPanel
│   ├── JobMenuPanel
│   ├── FieldToolsPanel
│   ├── SimulatorPanel
│   └── ViewSettingsPanel
├── RightNavigationPanel              # Section and status controls
│   └── SectionControlPanel
├── BottomNavigationPanel             # Bottom toolbar
├── DialogOverlayHost                 # Modal dialog container
│   ├── FieldSelectionDialogPanel
│   ├── ConfigurationDialog
│   │   ├── VehicleConfigTab
│   │   ├── ToolConfigTab (with sub-tabs)
│   │   ├── UTurnConfigTab
│   │   ├── MachineControlConfigTab
│   │   ├── TramConfigTab
│   │   ├── SourcesConfigTab (GPS, NTRIP, Roll sub-tabs)
│   │   ├── DisplayConfigTab
│   │   └── AdditionalOptionsConfigTab
│   ├── TracksDialogPanel
│   ├── HeadlandBuilderDialogPanel
│   ├── NewFieldDialogPanel
│   ├── DrawABDialogPanel
│   ├── NumericInputDialogPanel
│   └── ... (20+ dialogs)
└── BoundaryRecordingPanel            # Floating panel
```

### Map Rendering

`DrawingContextMapControl` uses Avalonia's `DrawingContext` (not OpenGL):

```csharp
public class DrawingContextMapControl : Control
{
    private readonly DispatcherTimer _renderTimer;

    public DrawingContextMapControl()
    {
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)  // 30 FPS
        };
        _renderTimer.Tick += (s, e) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // Draw field boundaries
        // Draw tracks
        // Draw vehicle
        // Draw coverage
        // Draw U-turn path
        // etc.
    }
}
```

### Dialog Pattern

Dialogs are overlay panels controlled by ViewModel visibility:

```xml
<!-- In MainWindow.axaml -->
<dialogs:ConfigurationDialog IsVisible="{Binding IsConfigurationDialogVisible}"/>
<dialogs:FieldSelectionDialogPanel IsVisible="{Binding IsFieldSelectionDialogVisible}"/>
```

```csharp
// In MainViewModel
public bool IsConfigurationDialogVisible
{
    get => _isConfigurationDialogVisible;
    set => this.RaiseAndSetIfChanged(ref _isConfigurationDialogVisible, value);
}

public void ShowConfigurationDialog() => IsConfigurationDialogVisible = true;
public void HideConfigurationDialog() => IsConfigurationDialogVisible = false;
```

### Draggable Panels

Panels use `DraggableRotatablePanel` base class or Canvas positioning:

```csharp
// In platform MainWindow.axaml.cs or MainView.axaml.cs
private void OnPanelPointerPressed(object sender, PointerPressedEventArgs e)
{
    _isDragging = true;
    _dragStart = e.GetPosition(MainCanvas);
    _panelStart = new Point(Canvas.GetLeft(panel), Canvas.GetTop(panel));
}

private void OnPanelPointerMoved(object sender, PointerEventArgs e)
{
    if (_isDragging)
    {
        var current = e.GetPosition(MainCanvas);
        Canvas.SetLeft(panel, _panelStart.X + current.X - _dragStart.X);
        Canvas.SetTop(panel, _panelStart.Y + current.Y - _dragStart.Y);
    }
}
```

---

## Data Flow Diagrams

### GPS Data Flow (Hot Path)

```
UDP Port 9999 (GPS/PANDA from AiO)
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│              UdpCommunicationService.ReceiveCallback()          │
│                                                                 │
│  if (buffer[0] == '$')  // NMEA/PANDA sentence                 │
│      _autoSteerService.ProcessGpsBuffer(buffer, length)        │
└─────────────────────────────────────────────────────────────────┘
         │ Direct call (zero-copy)
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                   AutoSteerService.ProcessGpsBuffer()           │
│                                                                 │
│  1. NmeaParserServiceFast.ParseIntoState() → VehicleState      │
│     - Span<byte> parsing, no string allocation                 │
│     - 0.024µs per parse (13x faster than string-based)         │
│                                                                 │
│  2. Update local coordinates (Easting/Northing)                │
│     - WGS84 → local plane projection                           │
│                                                                 │
│  3. TrackGuidanceService.CalculateGuidance()                   │
│     - Pure Pursuit or Stanley algorithm                        │
│     - Returns SteerAngle, XTE                                  │
│                                                                 │
│  4. PgnBuilder.BuildAutoSteerPgn(ref state)                    │
│     - Thread-local buffer, zero allocation                     │
│                                                                 │
│  5. UdpCommunicationService.SendToModules(pgn)                 │
│     - Async UDP send to 192.168.5.255:8888                     │
│                                                                 │
│  Total latency: ~0.1ms                                         │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
UDP Port 8888 (PGN 254 to AutoSteer module)
```

### Module Communication Flow

```
UDP Port 9999 (Module responses)
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│              UdpCommunicationService.ReceiveCallback()          │
│                                                                 │
│  Check PGN header (0x80, 0x81)                                 │
│  Parse source, PGN number, length                              │
│  Fire DataReceived event                                        │
│  Update module connection timestamps                            │
└─────────────────────────────────────────────────────────────────┘
         │ Event
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MainViewModel.OnUdpDataReceived()              │
│                                                                 │
│  Route to ModuleCommunicationService.CheckSwitches()           │
│  Update ApplicationState.Connections                            │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│              ModuleCommunicationService.CheckSwitches()         │
│                                                                 │
│  Parse work switch, steer switch states from PGN              │
│  Compare with previous state                                   │
│  Fire AutoSteerToggleRequested if steer switch changed         │
│  Fire SectionMasterToggleRequested if work switch changed      │
└─────────────────────────────────────────────────────────────────┘
         │ Event
         ▼
┌─────────────────────────────────────────────────────────────────┐
│            MainViewModel.OnAutoSteerToggleRequested()           │
│                                                                 │
│  Toggle AutoSteer engaged state                                │
│  Update ApplicationState.Guidance                               │
│  Update UI                                                      │
└─────────────────────────────────────────────────────────────────┘
```

### NTRIP Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      NtripClientService                          │
│                                                                 │
│  HTTP/1.1 GET /mountpoint                                      │
│  Authorization: Basic base64(user:pass)                        │
│  Ntrip-Version: Ntrip/2.0                                      │
└─────────────────────────────────────────────────────────────────┘
         │ TCP stream
         ▼
┌─────────────────────────────────────────────────────────────────┐
│              RTCM3 correction data (continuous)                 │
│                                                                 │
│  Fire RtcmDataReceived event with byte[]                       │
│  Fire ConnectionStatusChanged on connect/disconnect            │
└─────────────────────────────────────────────────────────────────┘
         │ Event
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MainViewModel.OnRtcmDataReceived()             │
│                                                                 │
│  Forward to UdpCommunicationService.SendToModules()            │
│  → Broadcasts RTCM to AiO board on 192.168.5.255:8888         │
│  → AiO forwards to GPS receiver                                │
└─────────────────────────────────────────────────────────────────┘
```

---

## Performance Characteristics

### Zero-Copy GPS Pipeline

| Stage | Typical | Max | Allocations |
|-------|---------|-----|-------------|
| UDP Receive | <1ms | 2ms | 0 (reused buffer) |
| NMEA Parse | 0.024µs | 0.1µs | 0 (Span-based) |
| Guidance Calc | ~50µs | 200µs | 0 (stack) |
| PGN Build | <1µs | 2µs | 0 (ThreadStatic) |
| UDP Send | <1ms | 2ms | 0 (async) |
| **Total** | **~0.1ms** | **~5ms** | **0 bytes** |

### Compared to Original Target

- **Target:** <20ms GPS-to-PGN latency
- **Achieved:** ~0.1ms (100x better)
- **Method:** Zero-copy path, no events, no dispatcher

### UI Performance

| Metric | Value |
|--------|-------|
| Map render rate | 30 FPS (configurable) |
| State updates | Async via `Dispatcher.UIThread.Post()` |
| UI binding | ReactiveUI property change notifications |

### Memory Profile

| Component | Lifetime | Pattern |
|-----------|----------|---------|
| Singleton services | Application | ~20 instances |
| State objects | Application | Mutable, in-place updates |
| PGN buffers | Thread | ThreadStatic, reused |
| Guidance state | Per-calculation | Stack allocated |

---

## Coupling Analysis

### Tight Coupling Points

| Coupling | Location | Reason |
|----------|----------|--------|
| ConfigurationStore static | All services | Historical, needs refactor |
| ApplicationState static | All services | Historical, needs refactor |
| MainViewModel (26+ deps) | Constructor | Acts as orchestrator |
| UDP↔AutoSteer | WireUpServices() | Required for zero-copy performance |

### Loose Coupling Points

| Pattern | Used For |
|---------|----------|
| Event-based | Non-critical service communication |
| Interface-based DI | All service dependencies |
| ReactiveUI bindings | UI ↔ ViewModel |

### MainViewModel Responsibilities (Current)

1. **Event aggregation** - Subscribes to 12+ service events
2. **State coordination** - Updates ApplicationState from events
3. **Command routing** - Exposes 50+ commands for UI actions
4. **UI property exposure** - Mirrors state for XAML binding
5. **Service orchestration** - Coordinates multi-service operations

---

## Future Architecture Considerations

See `MICROKERNEL_MIGRATION_PLAN.md` for proposed evolution toward:

- **Message Bus** - Replace direct event subscriptions with pub/sub
- **Plugin Architecture** - Services as plugins with defined lifecycle
- **Injectable Configuration** - Replace static singleton with DI
- **Reduced MainViewModel** - Extract orchestration to separate services

**Key Constraint:** Preserve the **~0.1ms zero-copy hot path** for real-time steering control. This path should remain direct method calls, not go through a message bus.

---

## File Reference

| Area | Key Files |
|------|-----------|
| **DI Setup** | `Platforms/*/DependencyInjection/ServiceCollectionExtensions.cs` |
| **State** | `Shared/AgValoniaGPS.Models/State/ApplicationState.cs` |
| **Config** | `Shared/AgValoniaGPS.Models/Configuration/ConfigurationStore.cs` |
| **MainViewModel** | `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` |
| **AutoSteer** | `Shared/AgValoniaGPS.Services/AutoSteer/AutoSteerService.cs` |
| **PGN Builder** | `Shared/AgValoniaGPS.Services/AutoSteer/PgnBuilder.cs` |
| **UDP** | `Shared/AgValoniaGPS.Services/UdpCommunicationService.cs` |
| **Guidance** | `Shared/AgValoniaGPS.Services/Track/TrackGuidanceService.cs` |
| **Track Model** | `Shared/AgValoniaGPS.Models/Track/Track.cs` |
| **Geometry** | `Shared/AgValoniaGPS.Models/Base/GeometryMath.cs` |
| **Vec Types** | `Shared/AgValoniaGPS.Models/Base/Vec2.cs`, `Vec3.cs` |
| **Map Render** | `Shared/AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs` |
| **Desktop Main** | `Platforms/AgValoniaGPS.Desktop/Views/MainWindow.axaml` |
| **iOS Main** | `Platforms/AgValoniaGPS.iOS/Views/MainView.axaml` |
