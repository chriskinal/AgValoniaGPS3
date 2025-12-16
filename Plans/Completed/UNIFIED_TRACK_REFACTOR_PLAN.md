# Unified Track Class Refactoring Plan

**Date:** December 11, 2025
**Origin:** Tip from Brian (AgOpenGPS creator): "If you tweak the curves class you can use that for a track class and completely remove ABLine, CCurve and about 87 different guidance functions."

## Executive Summary

The current codebase has significant duplication across guidance-related code. By recognizing that **an AB line is just a 2-point curve**, we can unify all track types into a single `Track` class and consolidate multiple guidance services into one.

**Current state:** 3,393 lines across 32 files
**Estimated after refactor:** ~1,200 lines across 10-12 files
**Reduction:** ~65% fewer lines, ~60% fewer files

## Current Architecture Analysis

### Files by Category (3,393 total lines)

| Category | Files | Lines |
|----------|-------|-------|
| **Guidance Services** | 5 | 1,741 |
| PurePursuitGuidanceService.cs | | 207 |
| CurvePurePursuitGuidanceService.cs | | 476 |
| ContourPurePursuitGuidanceService.cs | | 262 |
| StanleyGuidanceService.cs | | 459 |
| YouTurnGuidanceService.cs | | 337 |
| **Guidance Interfaces** | 6 | 169 |
| **Guidance Models (Input/Output)** | 14 | 943 |
| **Track Models** | 5 | 187 |
| **Other (GuidanceService, TrackNudging)** | 2 | 383 |

### Duplication Analysis

#### 1. Integral Term Calculation (Duplicated 4x)
The PID integral calculation appears nearly identically in:
- `PurePursuitGuidanceService.cs` (lines 41-96)
- `CurvePurePursuitGuidanceService.cs` (lines 177-231)
- `ContourPurePursuitGuidanceService.cs` (lines 93-151)
- `StanleyGuidanceService.cs` (lines 404-431)

```csharp
// This pattern repeats in every guidance service:
if (input.PurePursuitIntegralGain != 0 && !input.IsReverse)
{
    output.PivotDistanceError = output.DistanceFromCurrentLinePivot * 0.2 + input.PreviousPivotDistanceError * 0.8;
    output.Counter = input.PreviousCounter + 1;
    // ... 40+ more lines identical or near-identical
}
```

#### 2. Steer Angle Limiting (Duplicated 5x)
```csharp
// Appears in every guidance service:
if (output.SteerAngle < -input.MaxSteerAngle)
    output.SteerAngle = -input.MaxSteerAngle;
if (output.SteerAngle > input.MaxSteerAngle)
    output.SteerAngle = input.MaxSteerAngle;
```

#### 3. Helper Methods (Duplicated 5x)
Every service has its own copies of:
- `DistanceSquared(Vec3, Vec3)`
- `DistanceSquared(double, double, double, double)`
- `Distance(Vec3, Vec3)`
- `ToDegrees(double)`

#### 4. Find Nearest Segment (Duplicated 3x)
Similar "find closest 2 points" logic in:
- `CurvePurePursuitGuidanceService.cs`
- `ContourPurePursuitGuidanceService.cs`
- `YouTurnGuidanceService.cs`

#### 5. Input/Output Models (90% similar)
`PurePursuitGuidanceInput` and `CurvePurePursuitGuidanceInput` share 90% of properties:
- Same: PivotPosition, Wheelbase, MaxSteerAngle, FixHeading, AvgSpeed, IsReverse, etc.
- Different: AB line has `CurrentLinePtA/PtB`, Curve has `CurvePoints` list

## The Key Insight

**An AB line is just a curve with 2 points.**

| Track Type | Representation |
|------------|----------------|
| AB Line | `List<Vec3>` with 2 points (A, B) |
| Curve | `List<Vec3>` with N points |
| Contour | `List<Vec3>` with N points |
| Boundary Track (outer) | `List<Vec3>` with N points (from boundary offset) |
| Boundary Track (inner) | `List<Vec3>` with N points (from boundary offset) |
| Water Pivot | `List<Vec3>` with N points (circular, closed) |

The curve guidance algorithm already handles the 2-point case correctly. The only special-casing needed is for closed loops (water pivot).

## Proposed New Architecture

### New Model: `Track.cs`

```csharp
/// <summary>
/// Unified track representation for all guidance types.
/// Replaces ABLine, separate curve models, and track mode switching.
/// </summary>
public class Track
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Track points with Easting, Northing, Heading.
    /// AB Lines have exactly 2 points. Curves have N points.
    /// </summary>
    public List<Vec3> Points { get; set; } = new();

    /// <summary>
    /// Track type for behavior variations.
    /// </summary>
    public TrackType Type { get; set; } = TrackType.ABLine;

    /// <summary>
    /// Whether this track forms a closed loop (water pivot, boundary tracks).
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Whether this track is currently active for guidance.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Visibility on map.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Accumulated nudge offset in meters.
    /// </summary>
    public double NudgeDistance { get; set; }

    // Computed properties
    public bool IsABLine => Points.Count == 2;
    public bool IsCurve => Points.Count > 2 && !IsClosed;
    public double Heading => Points.Count >= 2
        ? Math.Atan2(Points[1].Easting - Points[0].Easting,
                     Points[1].Northing - Points[0].Northing)
        : 0;
}

public enum TrackType
{
    ABLine,           // 2 points, infinite extension
    Curve,            // N points, finite
    BoundaryOuter,    // Derived from boundary, offset outward
    BoundaryInner,    // Derived from boundary, offset inward
    Contour,          // Recorded while driving
    WaterPivot        // Circular, closed loop
}
```

### New Service: `TrackGuidanceService.cs`

Single service replacing 4 Pure Pursuit services + 1 Stanley service:

```csharp
public interface ITrackGuidanceService
{
    TrackGuidanceOutput CalculateGuidance(TrackGuidanceInput input);
}

public class TrackGuidanceService : ITrackGuidanceService
{
    // Shared algorithm selection
    public TrackGuidanceOutput CalculateGuidance(TrackGuidanceInput input)
    {
        // Find nearest segment (works for 2 points or N points)
        var segment = FindNearestSegment(input.Track.Points, input.PivotPosition,
                                          input.Track.IsClosed, input.CurrentLocationIndex);

        // Calculate cross-track error (same formula for all)
        double xte = CalculateCrossTrackError(segment, input.PivotPosition);

        // Calculate goal point (same algorithm, just walks point list)
        var goalPoint = CalculateGoalPoint(input.Track.Points, segment,
                                           input.GoalPointDistance, input.IsReverse);

        // Apply chosen algorithm
        if (input.UseStanley)
            return CalculateStanleyOutput(input, segment, xte, goalPoint);
        else
            return CalculatePurePursuitOutput(input, segment, xte, goalPoint);
    }

    // Shared helper: integral term calculation (single implementation)
    private void UpdateIntegral(TrackGuidanceInput input, TrackGuidanceOutput output) { ... }

    // Shared helper: steer angle limiting
    private double ClampSteerAngle(double angle, double max) => Math.Clamp(angle, -max, max);
}
```

### Unified Input/Output Models

```csharp
public class TrackGuidanceInput
{
    // Vehicle position (steer axle for Stanley, pivot for Pure Pursuit)
    public Vec3 Position { get; set; }
    public Vec3 SteerPosition { get; set; }  // Only used for Stanley

    // Track reference
    public Track Track { get; set; }

    // Algorithm selection
    public bool UseStanley { get; set; }

    // Vehicle configuration (shared)
    public double Wheelbase { get; set; }
    public double MaxSteerAngle { get; set; }
    public double GoalPointDistance { get; set; }
    public double SideHillCompFactor { get; set; }

    // Gains (both algorithms)
    public double IntegralGain { get; set; }          // PP: PurePursuitIntegralGain, Stanley: StanleyIntegralGainAB
    public double HeadingErrorGain { get; set; }      // Stanley only
    public double DistanceErrorGain { get; set; }     // Stanley only

    // State
    public double FixHeading { get; set; }
    public double AvgSpeed { get; set; }
    public bool IsReverse { get; set; }
    public bool IsAutoSteerOn { get; set; }
    public double ImuRoll { get; set; }

    // Previous state for filtering
    public TrackGuidanceState PreviousState { get; set; }

    // Tracking optimization
    public int CurrentLocationIndex { get; set; }
    public bool FindGlobalNearest { get; set; }
}

public class TrackGuidanceOutput
{
    // Primary outputs (used by all)
    public double SteerAngle { get; set; }
    public short GuidanceLineDistanceOff { get; set; }  // mm
    public short GuidanceLineSteerAngle { get; set; }   // angle * 100

    // Cross-track error
    public double CrossTrackError { get; set; }

    // Visualization
    public Vec2 GoalPoint { get; set; }
    public Vec2 ClosestPoint { get; set; }
    public Vec2 RadiusPoint { get; set; }        // Pure Pursuit
    public double PurePursuitRadius { get; set; } // Pure Pursuit

    // State for next iteration
    public TrackGuidanceState State { get; set; }

    // Tracking
    public int CurrentLocationIndex { get; set; }
    public bool IsAtEndOfTrack { get; set; }
}

// Encapsulate filter state
public class TrackGuidanceState
{
    public double Integral { get; set; }
    public double PivotDistanceError { get; set; }
    public double PivotDistanceErrorLast { get; set; }
    public double PivotDerivative { get; set; }
    public int Counter { get; set; }
    // Stanley-specific
    public double XTrackSteerCorrection { get; set; }
    public double DistSteerError { get; set; }
    public double LastDistSteerError { get; set; }
}
```

### Shared Geometry Utilities

Extract duplicated helpers to `GeometryMath.cs` (may already have some):

```csharp
public static class GeometryMath
{
    // Already exists, add if missing:
    public static double DistanceSquared(Vec3 a, Vec3 b) { ... }
    public static double Distance(Vec3 a, Vec3 b) { ... }
    public static double ToDegrees(double radians) { ... }
    public static double ToRadians(double degrees) { ... }

    // New: segment distance calculation
    public static double DistanceToSegment(Vec3 point, Vec3 segA, Vec3 segB) { ... }

    // New: closest point on segment
    public static Vec2 ClosestPointOnSegment(Vec3 point, Vec3 segA, Vec3 segB) { ... }
}
```

### Unified Track Nudging

```csharp
public interface ITrackNudgingService
{
    Track NudgeTrack(Track track, double distance);
}

public class TrackNudgingService : ITrackNudgingService
{
    public Track NudgeTrack(Track track, double distance)
    {
        // Same algorithm for 2 points or N points
        var newPoints = new List<Vec3>(track.Points.Count);

        foreach (var point in track.Points)
        {
            newPoints.Add(new Vec3(
                point.Easting + Math.Sin(point.Heading + PIBy2) * distance,
                point.Northing + Math.Cos(point.Heading + PIBy2) * distance,
                point.Heading
            ));
        }

        // If N > 2, apply smoothing
        if (newPoints.Count > 2)
            newPoints = SmoothCurve(newPoints);

        return new Track { Points = newPoints, /* copy other properties */ };
    }
}
```

## Migration Strategy

### Phase 1: Create Unified Track Model
1. Create `Track.cs` with all properties
2. Create factory methods: `Track.FromABLine(pointA, pointB, heading)`
3. Update `TrackFilesService` to convert to/from unified Track
4. Keep `ABLine.cs` temporarily for compatibility

### Phase 2: Extract Shared Utilities
1. Move all `DistanceSquared`, `Distance`, `ToDegrees` to `GeometryMath.cs`
2. Create `IntegralCalculator` helper class for PID logic
3. Update existing services to use shared utilities (reduces duplication immediately)

### Phase 3: Create Unified Guidance Service
1. Create `TrackGuidanceInput`, `TrackGuidanceOutput`, `TrackGuidanceState`
2. Implement `TrackGuidanceService` with both Pure Pursuit and Stanley algorithms
3. Write unit tests comparing outputs to existing services

### Phase 4: Migrate Consumers
1. Update `MainViewModel` to use `Track` instead of `ABLine`
2. Update guidance calculation calls to use `TrackGuidanceService`
3. Update rendering to work with unified `Track`

### Phase 5: Cleanup
1. Remove deprecated: `ABLine.cs`, separate guidance services
2. Remove duplicate input/output models
3. Remove duplicate interfaces

## Files to Delete After Refactor

### Services (5 files, 1,741 lines)
- [x] `PurePursuitGuidanceService.cs` (207 lines)
- [x] `CurvePurePursuitGuidanceService.cs` (476 lines)
- [x] `ContourPurePursuitGuidanceService.cs` (262 lines)
- [x] `StanleyGuidanceService.cs` (459 lines)
- [x] `YouTurnGuidanceService.cs` - Simplified to use GeometryMath shared utilities

### Interfaces (5 files, 144 lines)
- [x] `IPurePursuitGuidanceService.cs`
- [x] `ICurvePurePursuitGuidanceService.cs`
- [x] `IContourPurePursuitGuidanceService.cs`
- [x] `IStanleyGuidanceService.cs`
- [x] `IGuidanceService.cs` - Removed (was unused)

### Models (10 files, ~400 lines)
- [x] `PurePursuitGuidanceInput.cs`
- [x] `PurePursuitGuidanceOutput.cs`
- [x] `CurvePurePursuitGuidanceInput.cs`
- [x] `CurvePurePursuitGuidanceOutput.cs`
- [x] `ContourPurePursuitGuidanceInput.cs`
- [x] `ContourPurePursuitGuidanceOutput.cs`
- [x] `StanleyGuidanceInput.cs`
- [x] `StanleyGuidanceOutput.cs`
- [x] `StanleyGuidanceCurveOutput.cs`
- [x] `ABLineNudgeInput.cs`, `ABLineNudgeOutput.cs` (merge into TrackNudge)

### Models to Rename/Modify
- `ABLine.cs` -> Delete, replaced by `Track.cs`
- `CurveNudgeInput/Output.cs` -> Merge into unified nudge

## Files to Create

| File | Estimated Lines | Purpose |
|------|-----------------|---------|
| `Track.cs` | ~80 | Unified track model |
| `TrackGuidanceService.cs` | ~400 | Single guidance service |
| `ITrackGuidanceService.cs` | ~20 | Interface |
| `TrackGuidanceInput.cs` | ~60 | Unified input |
| `TrackGuidanceOutput.cs` | ~50 | Unified output |
| `TrackGuidanceState.cs` | ~30 | Filter state |
| `IntegralCalculator.cs` | ~80 | Shared PID logic |

**Total new:** ~720 lines in 7 files

## Expected Outcome

| Metric | Before | After | Reduction |
|--------|--------|-------|-----------|
| Service files | 5 | 1 (+YouTurn) | 80% |
| Interface files | 6 | 2 | 67% |
| Model files | 14+ | 5 | 64% |
| Total lines | 3,393 | ~1,200 | 65% |
| Duplicated code | ~800 lines | ~0 | 100% |

## Compatibility Notes

### AgOpenGPS File Format
The `TrackLines.txt` format compatibility must be maintained:
```
$TrackName
Mode,Heading,Easting,Northing,IsVisible
[CurvePoints if Mode is curve]
```

The unified `Track` class can serialize to this format. `TrackFilesService` handles conversion.

### TrackMode Enum
Keep `TrackMode` enum for file compatibility, but `Track.Type` determines behavior internally.

## Testing Strategy

1. **Unit tests**: Compare `TrackGuidanceService` output to existing services with same inputs
2. **Integration tests**: Load existing track files, verify guidance calculations match
3. **Visual tests**: Verify rendered guidance lines look identical
4. **Edge cases**:
   - 2-point tracks (AB lines)
   - Very short curves (< 6 points)
   - Closed loops (water pivot)
   - End-of-track behavior

## Answers from Brian (AgOpenGPS Creator)

**Q1: Are there any edge cases where AB line and curve guidance diverge?**

> No, as long as all your lines are built out of segments. The quirk with AgOpenGPS is that the distance between the points need to be closer than the distance to the next line. So there is no difference between a curve and line from segmentation point of view.

**Implementation note:** Point spacing should be ~40% of tool width. The algorithm:
1. Add lots of points (interpolate/fill in the blanks)
2. Step through by segment distance, eliminating intermediate points
3. Smooth curves with Catmull-Rom splines

For AB lines (2 points), interpolate additional points if needed for long lines.

**Performance note:** Segments > 1 meter cause noticeable "chunky turns" on tight curves. But too many points slows down point searching. The solution is **local vs global search**:
- Global search: Find nearest point on entire track (used when acquiring line or after U-turn)
- Local search: Only search a few points ahead/behind current index (used during normal guidance)

This is already implemented in `CurvePurePursuitGuidanceService` via `CurrentLocationIndex` and `FindGlobalNearestPoint` parameters.

**Q2: How does water pivot mode differ?**

> Water pivot is just a closed loop, however it can be created via 3 points or a single point and radius. Otherwise it is just a looping segmented line.

**Implementation note:** `Track.IsClosed = true` handles this. Optionally store `CreationMethod` (ThreePoint, CenterRadius) for UI/recreation purposes.

**Q3: Any hidden complexities in the "87 different guidance functions"?**

> Well there are - but only because each line type as they were added over the years required their own guidance function to match the structure of the line. Using a single line type - collection of segments - there is no need for multiple guidance functions, drawing functions, closest point functions, storage, or line classes.

**Conclusion:** The duplication was historical accident, not technical necessity. A unified `Track` class with `List<Vec3>` points eliminates ALL of it.

## References

- AgOpenGPS source: Track/curve handling
- Current file: `ABLine.cs`, `CurveProcessing.cs`
- Brian's tip: Curves class can become Track class
