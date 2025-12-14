# CLAUDE.md - AgValoniaGPS3

This file provides guidance to Claude Code when working with this repository.

## Project Overview

AgValoniaGPS3 is a cross-platform agricultural GPS guidance application built with Avalonia UI. It's a clean rewrite achieving **91.7% shared code** across platforms.

**What it does:**
- Real-time GPS guidance for agricultural equipment
- Field boundary management and recording
- Unified track guidance (AB lines and curves use same system)
- U-turn path generation and following
- Section control for sprayers/planters
- NTRIP RTK corrections support
- Integration with AgOpenGPS ecosystem via UDP

## Architecture

```
AgValoniaGPS3/
├── Shared/                              # 91.7% - Platform-agnostic code
│   ├── AgValoniaGPS.Models/            # Data models, geometry, DTOs
│   ├── AgValoniaGPS.Services/          # Business logic, GPS, NTRIP, UDP
│   ├── AgValoniaGPS.ViewModels/        # MVVM ViewModels (ReactiveUI)
│   └── AgValoniaGPS.Views/             # Shared UI controls, panels, dialogs
│
├── Platforms/                           # 8.3% - Platform-specific code
│   ├── AgValoniaGPS.Desktop/           # Windows/macOS/Linux (5.7%)
│   └── AgValoniaGPS.iOS/               # iOS/iPadOS (2.6%)
│
└── AgValoniaGPS.sln                    # Solution file
```

### Platform Support

| Platform | Project | Notes |
|----------|---------|-------|
| Windows | AgValoniaGPS.Desktop | Same codebase as macOS/Linux |
| macOS | AgValoniaGPS.Desktop | Same codebase as Windows/Linux |
| Linux | AgValoniaGPS.Desktop | Same codebase as Windows/macOS |
| iOS/iPadOS | AgValoniaGPS.iOS | Requires Xcode, runs on ARM64 simulator |
| Android | Future | Not yet implemented |

## Build Commands

```bash
# Build and run Desktop (works on Windows, macOS, Linux)
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj

# Build iOS (requires macOS with Xcode)
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj -c Debug -f net10.0-ios -r iossimulator-arm64

# Deploy and run iOS on simulator
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj -c Debug -f net10.0-ios -r iossimulator-arm64 -t:Run

# Alternative iOS deployment (if -t:Run doesn't work)
xcrun simctl install booted Platforms/AgValoniaGPS.iOS/bin/Debug/net10.0-ios/iossimulator-arm64/AgValoniaGPS.iOS.app
xcrun simctl launch booted com.agvaloniaagps.ios

# Build entire solution
dotnet build AgValoniaGPS.sln
```

## Key Design Decisions

### Rendering: DrawingContext (not OpenGL/SkiaSharp)
Both Desktop and iOS use Avalonia's `DrawingContext` for map rendering via `DrawingContextMapControl`. This provides:
- Consistent rendering across all platforms
- No platform-specific graphics APIs needed
- 30 FPS default (configurable in `DrawingContextMapControl.cs`)

### Shared UI Components
All panels, dialogs, and controls live in `AgValoniaGPS.Views`:
- `Controls/DrawingContextMapControl.cs` - Main map rendering
- `Controls/Panels/` - LeftNavigationPanel, SimulatorPanel, SectionControlPanel, etc.
- `Controls/Dialogs/` - All modal dialogs (FieldSelection, DataIO, AgShare, etc.)
- `Converters/` - Shared value converters (BoolToColor, FixQualityToColor, etc.)

### Dialog Pattern
Dialogs use overlay panels controlled by ViewModel visibility properties:
```xml
<dialogs:FieldSelectionDialogPanel/>  <!-- Visibility bound to IsFieldSelectionDialogVisible -->
```

### Draggable Panels
Panels use Canvas positioning with pointer event handlers for dragging:
- Desktop: Handlers in `MainWindow.axaml.cs`
- iOS: Handlers in `MainView.axaml.cs`
- LeftNavigationPanel has built-in drag support for sub-panels

### Unified Track Architecture
**Key insight from AgOpenGPS creator Brian:** "An AB line is just a curve with 2 points."

All guidance track types use a single `Track` model (`Shared/AgValoniaGPS.Models/Track/Track.cs`):

```csharp
public class Track
{
    public string Name { get; set; }
    public List<Vec3> Points { get; set; }  // AB lines have 2 points, curves have N
    public TrackMode Mode { get; set; }
    public bool IsVisible { get; set; }
    public double NudgeDistance { get; set; }

    // Computed properties
    public bool IsABLine => Points.Count == 2;
    public bool IsCurve => Points.Count > 2;
}
```

**Single guidance service** (`TrackGuidanceService`) handles both Pure Pursuit and Stanley algorithms for all track types. This replaced 4 separate guidance services and reduced ~2,100 lines of duplicated code.

**Shared utilities** in `GeometryMath.cs`:
- `Distance()`, `DistanceSquared()` - various overloads for Vec2/Vec3
- `ToDegrees()`, `ToRadians()` - angle conversion
- `IsPointInPolygon()` - boundary checks
- `PIBy2`, `twoPI` - common constants

## Technology Stack

- **.NET 10.0** - Target framework
- **Avalonia 11.3.9** - Cross-platform UI framework
- **ReactiveUI 20.1.1** - MVVM framework with reactive extensions
- **Microsoft.Extensions.DependencyInjection** - Dependency injection

## Key Files Reference

| File | Purpose |
|------|---------|
| `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` | Main application state, commands, GPS data |
| `Shared/AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs` | Map rendering (30 FPS) |
| `Shared/AgValoniaGPS.Views/Controls/Panels/LeftNavigationPanel.axaml` | Main navigation sidebar |
| `Shared/AgValoniaGPS.Models/Track/Track.cs` | Unified track model (AB lines + curves) |
| `Shared/AgValoniaGPS.Models/Base/GeometryMath.cs` | Shared geometry utilities |
| `Shared/AgValoniaGPS.Services/Track/TrackGuidanceService.cs` | Pure Pursuit + Stanley guidance |
| `Shared/AgValoniaGPS.Services/YouTurn/YouTurnGuidanceService.cs` | U-turn path following |
| `Shared/AgValoniaGPS.Services/NtripClientService.cs` | NTRIP RTK corrections |
| `Shared/AgValoniaGPS.Services/GpsService.cs` | GPS data processing |
| `Platforms/AgValoniaGPS.Desktop/Views/MainWindow.axaml` | Desktop main window |
| `Platforms/AgValoniaGPS.iOS/Views/MainView.axaml` | iOS main view |
| `TestRunner/Program.cs` | Test harness for guidance algorithms |

## Service Interfaces

Services use interface-based design in `Shared/AgValoniaGPS.Services/Interfaces/`:
- `ITrackGuidanceService` - Unified guidance (Pure Pursuit + Stanley) for all track types
- `IGpsService` - GPS data processing and position updates
- `IUdpCommunicationService` - UDP communication with AgOpenGPS modules
- `INtripClientService` - NTRIP caster connections for RTK
- `IFieldService` - Field loading/saving/management
- `IBoundaryRecordingService` - Recording field boundaries
- `IMapService` - Map control registration and track/boundary rendering
- `IDialogService` - Platform dialog abstractions

## Platform-Specific Code

### Desktop (1,681 lines)
- `App.axaml/cs` - Application entry point
- `Program.cs` - Main entry point
- `MainWindow.axaml/cs` - Window with drag handlers, styles
- `Services/DialogService.cs` - Stub (dialogs handled by shared overlays)
- `Services/MapService.cs` - Map control registration
- `DependencyInjection/ServiceCollectionExtensions.cs` - DI setup
- `ViewLocator.cs` - View resolution

### iOS (765 lines)
- `App.axaml/cs` - Application entry point
- `AppDelegate.cs` - iOS app delegate
- `MainView.axaml/cs` - Main view with drag handlers
- `Services/` - Platform service implementations
- `Info.plist` - iOS app configuration

## Common Tasks

### Adding a New Dialog
1. Create `YourDialogPanel.axaml/cs` in `Shared/AgValoniaGPS.Views/Controls/Dialogs/`
2. Add visibility property to `MainViewModel.cs`: `IsYourDialogVisible`
3. Add command to show dialog: `ShowYourDialogCommand`
4. Add `<dialogs:YourDialogPanel/>` to both `MainWindow.axaml` and `MainView.axaml`

### Adding a New Panel
1. Create `YourPanel.axaml/cs` in `Shared/AgValoniaGPS.Views/Controls/Panels/`
2. Add to `LeftNavigationPanel.axaml` if it's a sub-panel
3. Or add directly to platform views if standalone

### Modifying Frame Rate
Edit `DrawingContextMapControl.cs`:
```csharp
_renderTimer = new DispatcherTimer
{
    Interval = TimeSpan.FromMilliseconds(33)  // 33ms = 30 FPS
};
```

## NTRIP Connection Format
The NTRIP client uses HTTP/1.1 format:
```
GET /mountpoint HTTP/1.1
Host: caster.example.com
Ntrip-Version: Ntrip/2.0
Authorization: Basic base64(username:password)
User-Agent: NTRIP AgValoniaGPS
```

## Debugging Tips

1. **iOS simulator issues**: Use `xcrun simctl` commands directly if `dotnet build -t:Run` fails
2. **Frame rate**: ARM64 Macs handle 60 FPS fine; Intel Macs may need 10-15 FPS due to emulation
3. **Dialog not showing**: Check ViewModel visibility property binding
4. **Panel not dragging**: Verify Canvas positioning and pointer event handlers

## Code Style

- Use `Classes.Active` binding for state-based styling instead of converters where possible
- Keep platform code minimal - prefer shared code
- Dialogs are overlay panels, not separate windows
- Use dependency injection for services
- Use shared `GeometryMath` utilities instead of duplicating distance/angle calculations

## Testing

Run guidance algorithm tests:
```bash
dotnet run --project TestRunner/TestRunner.csproj
```

Tests verify:
- AB Line Pure Pursuit steering
- Curve Pure Pursuit steering
- AB Line Stanley steering
- Vehicle on-line behavior (XTE ≈ 0)
- Track model conversion (ABLine ↔ Track)

## U-Turn System

U-turns are generated in `MainViewModel.CreateSimpleUTurnPath()`:
- Entry leg: straight line from cultivated area into headland
- Arc: semicircle positioned so it fits within headland zone
- Exit leg: straight line back to next track

Key parameters:
- `HeadlandDistance` - width of headland zone (green to yellow line)
- `turnRadius` - half of track offset (based on implement width × row skip)
- Arc positioning: `headlandLegLength = max(HeadlandDistance - turnRadius, 2.0)`

The arc must fit between the headland boundary (green line) and outer boundary (yellow line). If the headland is too narrow for the turn radius, the arc will extend past the outer boundary.

## Legacy Code

- `ABLine.cs` - Marked `[Obsolete]`, retained only for AgOpenGPS file I/O compatibility
- Use `Track` model for all new guidance code
