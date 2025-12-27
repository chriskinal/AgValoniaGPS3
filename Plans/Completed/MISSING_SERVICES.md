# Missing Services from AgOpenGPS

This document tracks services and features from the original AgOpenGPS that were not extracted into AgOpenGPS.Core and need to be ported to AgValoniaGPS3.

## 1. BoundaryBuilder Service (Build Boundary From Tracks)

**Original Location:** `/SourceCodeLatest/GPS/Classes/BoundaryBuilder.cs`

**Purpose:** Creates a field boundary polygon from AB lines and curve tracks by finding their intersections and connecting them.

**How it works:**
1. Takes AB lines and/or curve guidance tracks as input
2. Extends track endpoints to ensure they intersect with other tracks
3. Finds intersection points between different tracks
4. Trims tracks to their intersection points
5. Connects trimmed segments into a closed polygon boundary
6. Saves the boundary to Boundary.txt

**Key Components:**
- `BoundaryBuilder` class with:
  - `SetTracks(List<CTrk> tracks)` - Input tracks
  - `ExtendAllTracks(double extendMeters)` - Auto-extend to find intersections
  - `BuildSegments()` - Convert tracks to line segments
  - `FindIntersections()` - Find where tracks cross
  - `TrimSegmentsToIntersections()` - Cut tracks at intersection points
  - `BuildTrimmedBoundary()` - Create final polygon
  - `SaveToBoundaryFile(string fieldDirectory)` - Persist to file

**Dependencies:**
- `CTrk` - Track model (AB line or Curve)
- `TrackMode` enum (AB, Curve)
- `vec2`, `vec3` - Vector types
- `CBoundaryList` - Boundary polygon model
- Track file loading (`FileLoadTracks()`)

**UI Requirements:**
- Track selection list with checkboxes
- Preview map showing tracks and generated boundary
- Track adjustment controls (extend/shrink endpoints)
- "Auto-find intersections" button
- "Build Boundary" and "Save" buttons

**Source Files to Reference:**
- `/SourceCodeLatest/GPS/Classes/BoundaryBuilder.cs` - Core logic
- `/SourceCodeLatest/GPS/Forms/Field/FormBuildBoundaryFromTracks.cs` - UI/Form
- `/AgValoniaGPS/AgValoniaGPS.ViewModels/Dialogs/FieldManagement/BuildBoundaryFromTracksViewModel.cs` - Partial Avalonia port (stub)

**Priority:** Medium - Useful for creating boundaries from guidance lines

---

## 2. ToolPositionService (Config Settings Support) ✅ COMPLETE

**Status:** Implemented in `Services/Tool/ToolPositionService.cs` (December 2024)

**Implementation:**
- Interface: `IToolPositionService` in `Services/Interfaces/`
- Service: `ToolPositionService` in `Services/Tool/`
- Supports Fixed (front/rear), Trailing (Torriem algorithm), and TBT configurations
- Integrated with MainViewModel GPS update loop
- DI registered in Desktop and iOS platforms

**Purpose:** Calculate tool/implement position relative to vehicle pivot point based on hitch configuration.

**Priority:** Medium - Needed for accurate coverage mapping and section control

---

## 3. SectionControlService (Config Settings Support) ✅ COMPLETE

**Status:** Implemented in `Services/Section/SectionControlService.cs` (December 2024)

**Implementation:**
- Interface: `ISectionControlService` in `Services/Interfaces/`
- Service: `SectionControlService` in `Services/Section/`
- State model: `SectionControlState`, `SectionMasterState`, `SectionButtonState` enums in interface
- Look-ahead logic for predictive section on/off
- Boundary/headland checking via ApplicationState
- Manual override support (Off/Auto/On per section)
- Coverage integration via ICoverageMapService
- DI registered in Desktop and iOS platforms

**Purpose:** Manage automatic section on/off based on coverage, boundaries, and headlands.

**Priority:** Medium-High - Core functionality for sprayers/planters

---

## 4. CoverageMapService (Config Settings Support) ✅ COMPLETE

**Status:** Implemented in `Services/Coverage/CoverageMapService.cs` (December 2024)

**Implementation:**
- Interface: `ICoverageMapService` in `Services/Interfaces/`
- Service: `CoverageMapService` in `Services/Coverage/`
- Model: `CoveragePatch` in `Models/Coverage/`
- Triangle strip storage for efficient rendering
- File I/O for Sections.txt format
- Area calculation using WorkedAreaService
- DI registered in Desktop and iOS platforms

**Purpose:** Track and render coverage (where tool has been active).

**Priority:** Medium - Needed for visual feedback and section control

---

## 5. AntennaTransformService (Config Settings Support) ✅ COMPLETE

**Status:** Implemented in `GpsService.TransformAntennaToPivot()` (December 2024)

**Implementation:**
- Integrated directly into GpsService rather than a separate service
- `TransformAntennaToPivot()` method applies both fore/aft (AntennaPivot) and lateral (AntennaOffset) offsets
- Sign conventions: Negative offset = antenna LEFT of center, Positive = RIGHT
- All guidance calculations now use the transformed pivot position

**Config Settings Used:**
- `Vehicle.AntennaPivot` - Distance from antenna to pivot (along centerline)
- `Vehicle.AntennaOffset` - Lateral offset of antenna from centerline
- `Vehicle.AntennaHeight` - NOT USED (terrain compensation - low priority)

---

## 6. TramLineService (Config Settings Support) ✅ COMPLETE

**Status:** Implemented in `Services/Tram/TramLineService.cs` (December 2024)

**Implementation:**
- Interface: `ITramLineService` in `Services/Interfaces/`
- Offset service: `ITramLineOffsetService` / `TramLineOffsetService` in `Services/`
- Main service: `TramLineService` in `Services/Tram/`
- Config: `TramConfig` added to `ConfigurationStore`
- Boundary tram track generation (inner/outer wheel paths)
- Parallel tram line generation from guidance tracks
- On-tram-line query for section control integration
- File I/O (TramLines.txt)
- DI registered in Desktop and iOS platforms

**Purpose:** Generate and display tram lines for controlled traffic farming.

**Priority:** Low - Advanced feature

---

## Notes

- Services should be added to `Shared/AgValoniaGPS.Services/`
- Interfaces should be in `Shared/AgValoniaGPS.Services/Interfaces/`
- Models should be in `Shared/AgValoniaGPS.Models/`
- UI panels should be in `Shared/AgValoniaGPS.Views/Controls/Dialogs/` or `Controls/Panels/`
