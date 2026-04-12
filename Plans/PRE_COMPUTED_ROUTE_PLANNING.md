# Pre-Computed Route Planning: Phased Implementation Plan

## Issue #128 -- AgValoniaGPS

### Date: 2026-04-03
### Status: Draft

---

## 1. Current Architecture Summary

The current guidance system is a "white-cane" reactive approach:

- **Track model** (`Track.cs`): Unified AB line / curve representation with `List<Vec3> Points`
- **Guidance loop** (`MainViewModel.Guidance.cs`): Each frame, computes `_howManyPathsAway` offset from the reference track, creates a real-time offset curve via `CurveProcessing.CreateOffsetCurve`, and feeds it to `TrackGuidanceService.CalculateGuidance()` which computes Pure Pursuit or Stanley steering
- **U-turn system** (`MainViewModel.YouTurn.cs`): Detects headland proximity via raycast, creates a single U-turn arc via `YouTurnCreationService` (which internally uses `DubinsPathService`), and follows it via `YouTurnGuidanceService`
- **Snake ordering** (`SwathOrderingService`): Pre-computes a traversal sequence (snake, spiral, boustrophedon) that the YouTurn logic consumes one index at a time, but the actual swath geometry is computed reactively per-turn
- **Polygon operations**: Clipper2 library available for polygon offset/clipping. `PolygonOffsetService` handles headland inward offsets. `BoundaryPolygon` has point-in-polygon and spatial indexing
- **Map rendering**: `DrawingContextMapControl` (4200 lines) draws everything via Avalonia `DrawingContext`. Already renders YouTurn paths, active tracks, next tracks, headland lines

**Key observation**: The existing `YouTurnGuidanceService` already does waypoint-following along a `List<Vec3>` path using Pure Pursuit / Stanley. This is directly reusable for route-following guidance.

---

## 2. Design Principles

1. **Additive, not destructive** -- White-cane mode stays intact and functional throughout. Route mode is a parallel code path selected by a `GuidanceMode` enum.
2. **Reuse existing services** -- `TrackGuidanceService` for swath following, `DubinsPathService` for turn generation, `SwathOrderingService` for ordering, `CurveProcessing` for offset curves.
3. **Thin new services** -- The route planner is a new service that orchestrates existing primitives. It does not reinvent guidance algorithms.
4. **Separation of planning and execution** -- Route computation produces an immutable `RoutePlan` data structure. The guidance loop consumes it. Planning can happen on a background thread.
5. **Incremental rendering** -- The route preview is a new rendering layer in `DrawingContextMapControl`, not a replacement for existing layers.

---

## 3. Data Model Design

### 3.1 New Models (in `Shared/AgValoniaGPS.Models/RoutePlanning/`)

```csharp
RoutePlan                    // Top-level immutable plan
  List<RouteSegment>         // Ordered sequence of segments
  RoutePlanMetadata          // Total distance, estimated time, swath count
  SwathPattern               // Which ordering was used
  DateTime CreatedAt

RouteSegment                 // One swath or one turn
  RouteSegmentType           // Swath | Turn
  int SwathIndex             // Which swath number (for swaths)
  List<Vec3> Waypoints       // Dense waypoint list with headings
  double Length              // Segment length in meters
  bool IsReverse             // Direction of travel

RouteSwath                   // A single clipped swath (pre-computation intermediate)
  int Index                  // Original swath index
  List<Vec3> Points          // Clipped to cultivated area boundary
  double Heading             // Swath heading
  Vec3 EntryPoint            // Start of swath (direction-aware)
  Vec3 ExitPoint             // End of swath (direction-aware)

RoutePlanProgress            // Runtime tracking state
  int CurrentSegmentIndex
  int CurrentWaypointIndex
  double DistanceCompleted
  double DistanceRemaining
  double PercentComplete
  TimeSpan EstimatedTimeRemaining
  RouteSegmentType CurrentSegmentType

GuidanceMode (enum)          // WhiteCane | PreComputedRoute
```

### 3.2 New State (in `Shared/AgValoniaGPS.Models/State/`)

```csharp
RoutePlanState : ReactiveObject    // Added to ApplicationState
  RoutePlan? ActivePlan
  RoutePlanProgress? Progress
  bool IsRouteActive
  bool IsPlanningInProgress
  GuidanceMode Mode
```

---

## 4. Service Architecture

### 4.1 New Services (in `Shared/AgValoniaGPS.Services/RoutePlanning/`)

**`IRoutePlanningService` / `RoutePlanningService`** -- The orchestrator. Public API:

- `Task<RoutePlan> GenerateRoutePlan(RoutePlanRequest request)` -- Async computation
- `RoutePlan? ActivePlan { get; }`
- `void ClearPlan()`

Internally composes:

1. `SwathGenerationService` -- Generates all parallel swaths from a reference track and boundary
2. `SwathClippingService` -- Clips swaths to cultivated area polygon using Clipper2
3. `SwathOrderingService` -- Already exists, orders the clipped swaths
4. `TurnPathService` -- Generates Dubins turn paths between consecutive swath endpoints
5. `RouteStitchingService` -- Assembles ordered swaths and turns into a single `RoutePlan`

**`SwathGenerationService`** -- New. Generates N offset curves covering the field:

- Input: reference `Track`, tool width, overlap, boundary bounding box
- Output: `List<RouteSwath>` (all swaths that have any portion inside the field)
- Uses `CurveProcessing.CreateOffsetCurve` for each offset

**`SwathClippingService`** -- New. Clips each swath line to the cultivated area polygon:

- Input: `RouteSwath`, cultivated area boundary
- Output: `RouteSwath` with `Points` trimmed to boundary intersections
- Uses Clipper2 line-polygon intersection
- Handles concave fields producing multiple disjoint segments per swath

**`TurnPathService`** -- New wrapper around `DubinsPathService`:

- Input: exit point/heading of swath N, entry point/heading of swath N+1, turning radius, boundary
- Output: `List<Vec3>` turn waypoints
- Validates turn stays within headland zone
- Falls back to wider arc or 3-point turn if Dubins path exits boundary

**`RouteGuidanceService`** -- New. Follows a `RoutePlan`:

- Manages segment transitions: detects when vehicle reaches end of current segment, advances to next
- Delegates to existing `TrackGuidanceService` for swath segments, existing `YouTurnGuidanceService` for turn segments
- Handles "rejoin" logic: if vehicle deviates, finds nearest point on current or adjacent segment

### 4.2 Modified Existing Services

**`IMapService`** -- Add new methods:

- `SetRoutePlan(RoutePlanRenderData? plan)` -- Full route preview
- `SetRouteProgress(int currentSegmentIndex, int currentWaypointIndex)` -- Highlight progress
- `ClearRoutePlan()`

**No changes to**: `TrackGuidanceService`, `YouTurnGuidanceService`, `SwathOrderingService`, `DubinsPathService`

---

## 5. Phased Implementation

### Phase 1: Swath Generation and Clipping (Foundation)

**Goal**: Generate all parallel swaths clipped to field boundary, display on map.

**Deliverables**:
1. `RoutePlanRequest` model
2. `RouteSwath` model
3. `SwathGenerationService` -- generate N offset curves covering the field
4. `SwathClippingService` -- clip to cultivated area polygon using Clipper2
5. `IMapService.SetRoutePlan()` and rendering in `DrawingContextMapControl`
6. "Generate Swaths" button that shows preview of all swaths colored by sequence order

**Key technical details**:
- Determine swath count from field bounding box width / tool-effective-width, with margin
- For AB lines, offset is trivial: translate perpendicular by `N * widthMinusOverlap`
- For curves, use `CurveProcessing.CreateOffsetCurve` (already handles self-intersections)
- Clipper2 line-polygon intersection for clipping
- Handle multi-segment results for concave fields

**Estimated effort**: 3-5 days

**Risk**: None -- visualization only, white-cane mode untouched.

### Phase 2: Swath Ordering and Turn Path Generation

**Goal**: Order swaths and generate kinematically-feasible turns between consecutive swaths.

**Deliverables**:
1. Integration with `SwathOrderingService.GenerateSequence()` to order clipped swaths
2. `TurnPathService` wrapping `DubinsPathService` for inter-swath turns
3. Direction assignment (boustrophedon alternates, snake/spiral follows sequence)
4. `RouteStitchingService` -- combine swaths and turns into `RoutePlan`
5. `RoutePlan` and `RouteSegment` models
6. Enhanced map rendering: turns in different color, segment numbering

**Key technical details**:
- Turn radius from `ConfigStore.Vehicle` (minimum turning radius)
- Try all 6 Dubins path types, filter by boundary containment
- If no valid path fits, flag as "manual intervention required"
- For snake pattern, some turns span 2+ swath widths -- Dubins handles this

**Estimated effort**: 4-6 days

**Risk**: None -- still visualization only.

### Phase 3: Route Following Guidance Mode

**Goal**: Follow the pre-computed route instead of reactive offset computation.

**Deliverables**:
1. `GuidanceMode` enum and `RoutePlanState` in `ApplicationState`
2. `RouteGuidanceService` -- segment-by-segment guidance
3. `MainViewModel.RoutePlanning.cs` partial class
4. `MainViewModel.Guidance.cs` modifications -- mode switch
5. Segment transition logic (swath-to-turn, turn-to-swath)
6. `RoutePlanProgress` tracking

**Key guidance loop change**:

```csharp
// In CalculateAutoSteerGuidance:
if (State.RoutePlan.Mode == GuidanceMode.PreComputedRoute)
{
    var segment = routePlan.Segments[progress.CurrentSegmentIndex];
    if (segment.Type == RouteSegmentType.Swath)
        // Use TrackGuidanceService with segment.Waypoints
    else
        // Use YouTurnGuidanceService with segment.Waypoints
    // Check segment transition
}
else
{
    // Existing white-cane code (unchanged)
}
```

**Segment transition**: Use hysteresis -- advance when >2m past end AND heading aligned with next segment.

**Estimated effort**: 5-7 days

**Risk**: First guidance change. Mitigated by `GuidanceMode` switch -- white-cane is default.

### Phase 4: UI/UX Polish and Progress Tracking

**Goal**: Full user-facing workflow for route planning.

**Deliverables**:
1. Route planning dialog/panel
2. Route progress display (current swath N of M, % complete, ETA)
3. "Start from here" -- find nearest segment to current vehicle position
4. Route plan save/load to JSON
5. Cancel/restart mid-field
6. Completed segments dimmed, current highlighted, upcoming normal

**"Start from here"**: Find nearest waypoint across all segments. Skip to nearest swath segment (don't start mid-turn).

**Estimated effort**: 4-6 days

### Phase 5: Robustness and Edge Cases

**Goal**: Handle real-world complications.

**Deliverables**:
1. Off-route recovery (>10m deviation warning, re-acquire nearest segment)
2. Obstacle avoidance (inner boundaries)
3. Headland circuit passes (before/after interior swaths)
4. Multi-segment swaths for concave fields
5. Performance validation (<500ms for 100ha)

**Estimated effort**: 4-6 days

### Phase 6: Advanced Features (Future)

Each is an independent PR:

1. **Optimal swath angle** -- Brute-force sweep at 1-degree, evaluate total turn distance
2. **Trapezoidal decomposition** -- For complex/concave fields
3. **Reeds-Shepp turns** -- Reverse-capable for tight headlands
4. **NURBS path smoothing** -- Smoother turns, less data
5. **Cost function optimization** -- Total time, fuel, off-target application
6. **GTSP cell ordering** -- For decomposed fields (Hoffmann approach)

**Estimated effort**: 2-4 weeks total, variable per feature.

---

## 6. File Organization

New files:

```
AgValoniaGPS.Models/RoutePlanning/
  GuidanceMode.cs
  RoutePlan.cs
  RouteSegment.cs
  RouteSwath.cs
  RoutePlanRequest.cs
  RoutePlanProgress.cs

AgValoniaGPS.Models/State/
  RoutePlanState.cs

AgValoniaGPS.Services/RoutePlanning/
  IRoutePlanningService.cs
  RoutePlanningService.cs
  SwathGenerationService.cs
  SwathClippingService.cs
  TurnPathService.cs
  RouteStitchingService.cs
  RouteGuidanceService.cs

AgValoniaGPS.ViewModels/
  MainViewModel.RoutePlanning.cs

AgValoniaGPS.Views/Controls/Panels/
  RoutePlanningPanel.axaml/cs

Tests/AgValoniaGPS.Services.Tests/RoutePlanning/
  SwathGenerationServiceTests.cs
  SwathClippingServiceTests.cs
  TurnPathServiceTests.cs
  RoutePlanningServiceTests.cs
  RouteGuidanceServiceTests.cs
```

Modified files:

```
AgValoniaGPS.Models/State/ApplicationState.cs
AgValoniaGPS.Services/Interfaces/IMapService.cs
AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs
AgValoniaGPS.ViewModels/MainViewModel.Guidance.cs
AgValoniaGPS.ViewModels/MainViewModel.cs
Platforms/*/DependencyInjection/ServiceCollectionExtensions.cs
```

---

## 7. Testing Strategy

### Unit Tests (per phase)

**Phase 1**: Swath count for known field width. Offset distances. Convex/concave clipping. Swath entirely outside (dropped).

**Phase 2**: Dubins path between adjacent/non-adjacent swaths. Boundary validation. Narrow headland fallback. Segment continuity.

**Phase 3**: Segment transitions. Progress tracking. Off-route detection. Mode switching.

### Integration Tests

- Generate route for test field, simulate driving with `GpsSimulationService`, verify all segments traversed
- White-cane mode regression test (unaffected by route code)
- Route following convergence test: vehicle starts 5m off planned swath, must converge within 1m

### CI Pipeline

All route planning tests run in the existing CI `test` job (gates all builds). No workflow changes needed — tests in `Tests/AgValoniaGPS.Services.Tests/RoutePlanning/` are automatically discovered.

Critical regression guards:
- **Route generation round-trip**: generate plan → serialize → deserialize → verify identical
- **Full route simulation**: generate plan for test field → simulate driving all segments → verify 100% coverage
- **White-cane isolation**: verify no route planning code executes when `GuidanceMode.WhiteCane` is active

---

## 8. Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| Segment transition jitter | High | Hysteresis: >2m past end AND heading aligned |
| Clipper2 open-path clipping issues | Medium | Extensive unit tests, fallback to manual intersection |
| Dubins turns exit boundary | Medium | Try all 6 path types, fall back to 3-point turn |
| Performance regression on large curved fields | Medium | Profile early, consider parallel computation |
| Breaking white-cane mode | Critical | GuidanceMode switch is single if/else, no existing code modified |
| Complex concave fields | Medium | Phase 5 decomposition. Until then, warn user |

---

## 9. Total Estimated Effort

| Phase | Days | Cumulative | Status |
|-------|------|------------|--------|
| Phase 1: Swath Generation + Clipping | 3-5 | 3-5 | Planned |
| Phase 2: Ordering + Turns | 4-6 | 7-11 | Planned |
| Phase 3: Route Guidance Mode | 5-7 | 12-18 | Planned |
| Phase 4: UI/UX Polish | 4-6 | 16-24 | Planned |
| Phase 5: Robustness | 4-6 | 20-30 | Planned |
| Phase 6: Advanced (each) | 3-5 each | Variable | Future |

**MVP** (through Phase 3): 12-18 working days
**Production-ready** (through Phase 5): 20-30 working days

---

## 10. References

- Fields2Cover library: https://github.com/Fields2Cover/Fields2Cover
- Hoffmann et al. 2023 -- GTSP cell ordering with entry/exit optimization
- Driscoll 2011 -- Trapezoidal decomposition + cost functions
- Oksanen 2007 -- Path planning algorithms for agricultural field machines
- Bakhtiari et al. 2011 -- B-patterns via Ant Colony Optimization
- Choton & Hsu 2024 -- Multi-robot coverage path planning
- Zhou et al. 2024 -- Hybrid A* + annealing for agricultural vehicles
- Chatzisavvas 2025 -- Enhanced A* with Bezier smoothing
- Nilsson & Zhou 2020 (AGCO) -- Decision support for field operations
- Zhou et al. 2020 (AGCO/Aarhus) -- Metric map generation
- Khosravani et al. 2020 -- TSP benchmarking for coverage planning
- Vahdanjoo et al. 2020 -- Multi-depot route planning
