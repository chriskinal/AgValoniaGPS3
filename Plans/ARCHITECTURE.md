# AgValoniaGPS3 Architecture

This document describes the current architecture of AgValoniaGPS3, including service communication patterns, state management, and data flow.

**Last Updated:** December 2025

---

## Overview

AgValoniaGPS3 is a cross-platform agricultural GPS guidance application built with:
- **Avalonia UI 11.3** - Cross-platform UI framework
- **ReactiveUI 20.1** - MVVM with reactive extensions
- **.NET 10.0** - Target framework
- **Microsoft.Extensions.DependencyInjection** - Dependency injection

The architecture achieves **91.7% shared code** across platforms (Windows, macOS, Linux, iOS, Android).

---

## Project Structure

```
AgValoniaGPS3/
├── Shared/                              # 91.7% - Platform-agnostic code
│   ├── AgValoniaGPS.Models/            # Data models, geometry, state, configuration
│   ├── AgValoniaGPS.Services/          # Business logic, GPS, NTRIP, UDP, guidance
│   ├── AgValoniaGPS.ViewModels/        # MVVM ViewModels (ReactiveUI)
│   └── AgValoniaGPS.Views/             # Shared UI controls, panels, dialogs
│
├── Platforms/                           # 8.3% - Platform-specific code
│   ├── AgValoniaGPS.Desktop/           # Windows/macOS/Linux
│   ├── AgValoniaGPS.iOS/               # iOS/iPadOS
│   └── AgValoniaGPS.Android/           # Android
│
├── TestRunner/                          # Test harness for guidance algorithms
└── Plans/                               # Architecture and implementation plans
```

---

## Dependency Injection

All services are registered as **singletons** via `ServiceCollectionExtensions.AddAgValoniaServices()`:

```csharp
public static IServiceCollection AddAgValoniaServices(this IServiceCollection services)
{
    // Centralized application state
    services.AddSingleton<ApplicationState>();

    // Core services (20+ singletons)
    services.AddSingleton<IUdpCommunicationService, UdpCommunicationService>();
    services.AddSingleton<IGpsService, GpsService>();
    services.AddSingleton<IAutoSteerService, AutoSteerService>();
    services.AddSingleton<INtripClientService, NtripClientService>();
    services.AddSingleton<ITrackGuidanceService, TrackGuidanceService>();
    services.AddSingleton<IFieldService, FieldService>();
    services.AddSingleton<IBoundaryRecordingService, BoundaryRecordingService>();
    services.AddSingleton<IModuleCommunicationService, ModuleCommunicationService>();
    services.AddSingleton<IConfigurationService, ConfigurationService>();
    // ... additional services

    // ViewModels are Transient
    services.AddTransient<MainViewModel>();
    services.AddTransient<ConfigurationViewModel>();
}

public static void WireUpServices(this IServiceProvider serviceProvider)
{
    // Post-container wiring for zero-copy dependencies
    var udpService = serviceProvider.GetRequiredService<IUdpCommunicationService>() as UdpCommunicationService;
    var autoSteerService = serviceProvider.GetRequiredService<IAutoSteerService>();
    udpService?.SetAutoSteerService(autoSteerService);
}
```

**Platform-specific services:**
- `IDialogService` - Platform file/folder dialogs
- `IMapService` - Map control registration

---

## Service Communication Patterns

### Pattern 1: Event-Based Communication

Services expose events that subscribers (primarily MainViewModel) handle:

| Service | Events |
|---------|--------|
| `UdpCommunicationService` | `DataReceived`, `ModuleConnectionChanged` |
| `GpsService` | `GpsDataUpdated` |
| `AutoSteerService` | `StateUpdated` |
| `NtripClientService` | `ConnectionStatusChanged`, `RtcmDataReceived` |
| `BoundaryRecordingService` | `StateChanged`, `PointAdded` |
| `ModuleCommunicationService` | `AutoSteerToggleRequested`, `SectionMasterToggleRequested` |
| `ConfigurationService` | `ProfileLoaded`, `ProfileSaved` |
| `GpsSimulationService` | `GpsDataUpdated` |
| `FieldService` | `ActiveFieldChanged` |

**MainViewModel subscribes to 12+ events:**
```csharp
_gpsService.GpsDataUpdated += OnGpsDataUpdated;
_udpService.DataReceived += OnUdpDataReceived;
_autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
_udpService.ModuleConnectionChanged += OnModuleConnectionChanged;
_ntripService.ConnectionStatusChanged += OnNtripConnectionChanged;
_ntripService.RtcmDataReceived += OnRtcmDataReceived;
_fieldService.ActiveFieldChanged += OnActiveFieldChanged;
_simulatorService.GpsDataUpdated += OnSimulatorGpsDataUpdated;
_boundaryRecordingService.PointAdded += OnBoundaryPointAdded;
_boundaryRecordingService.StateChanged += OnBoundaryStateChanged;
_moduleCommunicationService.AutoSteerToggleRequested += OnAutoSteerToggleRequested;
_moduleCommunicationService.SectionMasterToggleRequested += OnSectionMasterToggleRequested;
```

### Pattern 2: Direct Service Calls (Zero-Copy Hot Path)

For latency-critical operations, services call each other directly:

```csharp
// UdpCommunicationService.cs - Direct call, no event overhead
if (_receiveBuffer[0] == (byte)'$' && _autoSteerService != null)
{
    _autoSteerService.ProcessGpsBuffer(_receiveBuffer, bytesReceived);
}
```

This achieves **~0.1ms GPS-to-PGN latency** (100x better than the 20ms target).

### Pattern 3: Static Singleton Access

Services access shared state via static singletons:

```csharp
// Any service can read configuration
var wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
var isMetric = ConfigurationStore.Instance.Display.IsMetric;

// Any service can read/write application state
ApplicationState.Instance.Vehicle.Position = newPosition;
```

---

## State Management

### ConfigurationStore (Static Singleton)

Holds all user-configurable settings. Persisted to profiles.

```csharp
public class ConfigurationStore : ReactiveObject
{
    public static ConfigurationStore Instance { get; }

    public VehicleConfig Vehicle { get; }      // Wheelbase, antenna offsets, max steer angle
    public ToolConfig Tool { get; }            // Width, sections, hitch geometry
    public GuidanceConfig Guidance { get; }    // U-turn params, tram lines, Pure Pursuit
    public DisplayConfig Display { get; }      // Grid, day/night, camera settings
    public ConnectionConfig Connection { get; } // NTRIP, GPS quality, UDP ports
    public MachineConfig Machine { get; }      // Hydraulics, pin assignments
    public AhrsConfig Ahrs { get; }            // IMU settings, roll compensation
    public SimulatorConfig Simulator { get; }  // Simulation parameters

    public event EventHandler? ProfileLoaded;
    public event EventHandler? ProfileSaved;
}
```

### ApplicationState (Static Singleton)

Holds runtime state. Not persisted.

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

### ReactiveUI Property Change

All state objects inherit from `ReactiveObject` and use `RaiseAndSetIfChanged`:

```csharp
public class VehicleState : ReactiveObject
{
    private double _heading;
    public double Heading
    {
        get => _heading;
        set => this.RaiseAndSetIfChanged(ref _heading, value);
    }
}
```

This enables automatic UI binding updates via Avalonia's binding system.

---

## Data Flow Diagrams

### GPS Data Flow (Hot Path)

```
UDP Port 9999 (GPS/PANDA sentences from AiO board)
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
│  2. Update local coordinates (Easting/Northing)                │
│  3. TrackGuidanceService.CalculateGuidance() → SteerAngle      │
│  4. PgnBuilder.BuildSteerPgn() → byte[]                        │
│  5. UdpCommunicationService.SendToModules()                    │
│                                                                 │
│  Total latency: ~0.1ms                                         │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
UDP Port 8888 (PGN 254 to AutoSteer module)
```

### Module Communication Flow

```
UDP Port 8888 (Module messages)
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│              UdpCommunicationService.ReceiveCallback()          │
│                                                                 │
│  Parse PGN header (0x80, 0x81, src, pgn, len)                  │
│  Fire DataReceived event                                        │
└─────────────────────────────────────────────────────────────────┘
         │ Event
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MainViewModel.OnUdpDataReceived()              │
│                                                                 │
│  _moduleCommunicationService.CheckSwitches(data)               │
│  Update connection status                                       │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│              ModuleCommunicationService.CheckSwitches()         │
│                                                                 │
│  Parse work switch, steer switch states                        │
│  Fire AutoSteerToggleRequested if switch changed               │
└─────────────────────────────────────────────────────────────────┘
         │ Event
         ▼
┌─────────────────────────────────────────────────────────────────┐
│            MainViewModel.OnAutoSteerToggleRequested()           │
│                                                                 │
│  Toggle AutoSteer engaged state                                │
│  Update UI                                                      │
└─────────────────────────────────────────────────────────────────┘
```

### Configuration Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    ConfigurationViewModel                        │
│                                                                 │
│  User taps "Edit Wheelbase" button                             │
│  ShowNumericInput(current, min, max, callback)                 │
└─────────────────────────────────────────────────────────────────┘
         │ Callback with new value
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    ConfigurationStore.Instance                   │
│                                                                 │
│  Vehicle.Wheelbase = newValue                                  │
│  RaiseAndSetIfChanged() triggers UI update                     │
└─────────────────────────────────────────────────────────────────┘
         │ Services read on next use
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    TrackGuidanceService                          │
│                                                                 │
│  var wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase │
│  (reads current value each calculation cycle)                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Service Inventory

### Core Services

| Service | Interface | Purpose |
|---------|-----------|---------|
| `UdpCommunicationService` | `IUdpCommunicationService` | UDP send/receive, module communication |
| `GpsService` | `IGpsService` | GPS data processing, position updates |
| `AutoSteerService` | `IAutoSteerService` | Zero-copy GPS→PGN pipeline |
| `NtripClientService` | `INtripClientService` | NTRIP RTK corrections |
| `TrackGuidanceService` | `ITrackGuidanceService` | Pure Pursuit + Stanley guidance |
| `YouTurnGuidanceService` | `IYouTurnGuidanceService` | U-turn path following |

### Field & Boundary Services

| Service | Interface | Purpose |
|---------|-----------|---------|
| `FieldService` | `IFieldService` | Field loading/saving/management |
| `BoundaryRecordingService` | `IBoundaryRecordingService` | Record field boundaries |
| `HeadlandService` | `IHeadlandService` | Headland generation |

### Configuration & Profile Services

| Service | Interface | Purpose |
|---------|-----------|---------|
| `ConfigurationService` | `IConfigurationService` | Profile load/save |
| `VehicleProfileService` | `IVehicleProfileService` | Vehicle profile management |
| `DisplaySettingsService` | `IDisplaySettingsService` | Display settings |

### Hardware Integration

| Service | Interface | Purpose |
|---------|-----------|---------|
| `ModuleCommunicationService` | `IModuleCommunicationService` | Module switch/relay handling |
| `GpsSimulationService` | `IGpsSimulationService` | GPS simulation for testing |

---

## MainViewModel Responsibilities

`MainViewModel` currently acts as the central orchestrator with **26+ injected dependencies**:

### Current Responsibilities
1. **Event aggregation** - Subscribes to 12+ service events
2. **State coordination** - Updates ApplicationState from events
3. **Command routing** - Exposes commands for UI actions
4. **UI property exposure** - Mirrors state for XAML binding
5. **Service orchestration** - Coordinates multi-service operations

### Injected Dependencies
```csharp
public MainViewModel(
    IUdpCommunicationService udpService,
    IGpsService gpsService,
    IAutoSteerService autoSteerService,
    INtripClientService ntripService,
    ITrackGuidanceService trackGuidanceService,
    IYouTurnGuidanceService youTurnService,
    IFieldService fieldService,
    IBoundaryRecordingService boundaryRecordingService,
    IHeadlandService headlandService,
    IConfigurationService configurationService,
    IVehicleProfileService vehicleProfileService,
    IDisplaySettingsService displaySettingsService,
    IModuleCommunicationService moduleCommunicationService,
    IGpsSimulationService simulatorService,
    IMapService mapService,
    IDialogService dialogService,
    // ... additional services
)
```

---

## Coupling Analysis

### Tight Coupling Points

1. **ConfigurationStore static access**
   - All services reference `ConfigurationStore.Instance` directly
   - Not injected through DI, harder to test

2. **ApplicationState static access**
   - Services read/write `ApplicationState.Instance` directly
   - State mutations scattered across codebase

3. **MainViewModel as god object**
   - 26+ dependencies, 12+ event subscriptions
   - Mixes orchestration with UI concerns

4. **UdpCommunicationService ↔ AutoSteerService**
   - Manual wiring via `SetAutoSteerService()`
   - Required for zero-copy performance

### Loose Coupling Points

1. **Event-based communication** for non-critical paths
2. **Interface-based DI** for all services
3. **ReactiveUI bindings** for UI updates

---

## Performance Characteristics

### Zero-Copy GPS Pipeline
- **Latency:** ~0.1ms (GPS receive → PGN transmit)
- **Allocations:** 0 bytes per cycle
- **Rate:** 10Hz (matches GPS update rate)

### UI Updates
- **Render rate:** 30 FPS (configurable in DrawingContextMapControl)
- **State updates:** Async via `Dispatcher.UIThread.Post()`
- **Binding:** ReactiveUI property change notifications

### Memory
- **Singleton services:** ~20 instances, long-lived
- **State objects:** Mutable, updated in-place
- **PGN buffers:** Thread-local, reused (ArrayPool)

---

## Future Architecture Considerations

See `MICROKERNEL_MIGRATION_PLAN.md` for proposed evolution toward:
- Message bus for loose coupling
- Plugin architecture for extensibility
- Injectable configuration provider
- Reduced MainViewModel responsibilities

The key constraint is preserving the **~0.1ms zero-copy hot path** for real-time steering control.

---

## File Reference

| Area | Key Files |
|------|-----------|
| DI Setup | `Platforms/*/DependencyInjection/ServiceCollectionExtensions.cs` |
| State | `Shared/AgValoniaGPS.Models/State/ApplicationState.cs` |
| Config | `Shared/AgValoniaGPS.Models/Configuration/ConfigurationStore.cs` |
| MainViewModel | `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` |
| AutoSteer | `Shared/AgValoniaGPS.Services/AutoSteerService.cs` |
| UDP | `Shared/AgValoniaGPS.Services/UdpCommunicationService.cs` |
| Guidance | `Shared/AgValoniaGPS.Services/Track/TrackGuidanceService.cs` |
| Map Render | `Shared/AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs` |
