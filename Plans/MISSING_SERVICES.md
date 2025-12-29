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

## 2. ToolPositionService (Config Settings Support)

**Purpose:** Calculate tool/implement position relative to vehicle pivot point based on hitch configuration.

**Config Settings Used:**
- `Tool.HitchLength` - Distance from pivot to hitch point
- `Tool.TrailingHitchLength` - For trailing implements
- `Tool.TankTrailingHitchLength` - For tank trailer configurations
- `Tool.TrailingToolToPivotLength`
- `Tool.IsToolTrailing`, `IsToolTBT`, `IsToolRearFixed`, `IsToolFrontFixed` - Tool type flags

**Interface:**
```csharp
public interface IToolPositionService
{
    Vec3 CalculateToolPosition(Vec3 vehiclePivot, double vehicleHeading);
    double CalculateToolHeading(double vehicleHeading);
    void UpdateTrailingState(Vec3 vehiclePivot, double vehicleHeading);
    (Vec3 left, Vec3 right) GetToolEdgePositions(Vec3 toolCenter, double toolHeading);
}
```

**Key Algorithms:**
1. **Fixed Tool**: Simple offset from pivot by hitch length
2. **Trailing Tool**: Track hitch point, calculate tool angle from movement history
3. **TBT (Tow-Between-Tractor)**: Two-stage trailing calculation

**Priority:** Medium - Needed for accurate coverage mapping and section control

---

## 3. SectionControlService (Config Settings Support)

**Purpose:** Manage automatic section on/off based on coverage, boundaries, and headlands.

**Config Settings Used:**
- `Config.NumSections`
- `Tool.SectionWidths[]`
- `Tool.LookAheadOnSetting`
- `Tool.LookAheadOffSetting`
- `Tool.TurnOffDelay`
- `Tool.MinCoverage`
- `Tool.IsSectionOffWhenOut`
- `Tool.SlowSpeedCutoff`

**Interface:**
```csharp
public interface ISectionControlService
{
    SectionState[] SectionStates { get; }
    void UpdateSections(Vec3 toolPosition, double toolHeading, double speed);
    bool IsPointCovered(Vec3 point);
    bool IsSectionInWorkArea(int sectionIndex, Vec3 toolPosition, double toolHeading);
    void SetSectionManual(int sectionIndex, bool on);
    void SetAllAuto();
    bool MasterOn { get; set; }
}
```

**Key Algorithms:**
1. Look-ahead calculation: Project section position forward by speed × look-ahead time
2. Coverage check: Query coverage map for overlap
3. Boundary check: Ensure section is inside field boundary
4. Headland check: Turn off in headland zone if configured
5. Delay logic: Implement turn-off delay timer

**Priority:** Medium-High - Core functionality for sprayers/planters

---

## 4. CoverageMapService (Config Settings Support)

**Purpose:** Track and render coverage (where tool has been active).

**Config Settings Used:**
- `Tool.Width`
- `Tool.SectionWidths[]`
- `Config.NumSections`
- `Tool.IsMultiColoredSections`

**Interface:**
```csharp
public interface ICoverageMapService
{
    void AddCoverage(Vec3 toolPosition, double toolHeading, bool[] activeSections);
    bool IsPointCovered(double easting, double northing);
    double GetCoveragePercentage();
    void Clear();
    IEnumerable<CoveragePolygon> GetCoveragePolygons();
}
```

**Priority:** Medium - Needed for visual feedback and section control

---

## 5. AntennaTransformService (Config Settings Support)

**Purpose:** Transform GPS antenna position to vehicle pivot point. Currently GPS position is used directly without applying antenna offset.

**Config Settings Used:**
- `Vehicle.AntennaPivot` - Distance from antenna to pivot (along centerline)
- `Vehicle.AntennaOffset` - Lateral offset of antenna from centerline
- `Vehicle.AntennaHeight` - Height of antenna (terrain compensation)

**Interface:**
```csharp
public interface IAntennaTransformService
{
    Position TransformToPivot(GpsData antennaPosition);
    (double pivot, double lateral, double height) GetOffsets();
}
```

**Implementation Notes:**
This could alternatively be integrated into GpsService directly rather than a separate service. The key requirement is that GPS antenna position must be transformed to vehicle pivot point before being used in guidance calculations.

**Priority:** HIGH - Critical for guidance accuracy. Currently guidance uses antenna position, not pivot point.

---

## 6. TramLineService (Config Settings Support)

**Purpose:** Generate and display tram lines for controlled traffic farming.

**Config Settings Used:**
- Tram configuration settings from TramConfigTab

**Interface:**
```csharp
public interface ITramLineService
{
    void GenerateTramLines(Track referenceTrack);
    bool IsOnTramLine(Vec3 position);
    IEnumerable<Track> GetTramLines();
}
```

**Priority:** Low - Advanced feature

---

## Notes

- Services should be added to `Shared/AgValoniaGPS.Services/`
- Interfaces should be in `Shared/AgValoniaGPS.Services/Interfaces/`
- Models should be in `Shared/AgValoniaGPS.Models/`
- UI panels should be in `Shared/AgValoniaGPS.Views/Controls/Dialogs/` or `Controls/Panels/`
