# Segment-Based Track Architecture Plan

**Date:** March 25, 2026
**Status:** Draft
**Priority:** Medium-High
**Related Issues:** Performance optimization for large tracks, smoother guidance

## Executive Summary

The current guidance system is a hybrid: **point-based storage** with **segment-based logic**. While functionally correct, this creates inefficiencies:

- **Segment metadata is recalculated every guidance cycle** (heading, direction, length)
- **Linear search O(n)** for nearest segment (acceptable for <100 points, slow for 500+)
- **No variable segment density** - curves need more points than straight lines
- **No spatial indexing** - every search scans all segments

**Proposal:** Migrate to explicit **segment-based track architecture** with pre-computed metadata, optional spatial indexing, and variable density support.

**Expected improvements:**
- **20-30% faster guidance calculations** (pre-computed segment data)
- **10-100x faster nearest segment lookup** for large tracks (with spatial index)
- **Smaller file sizes** (variable density: fewer points on straight sections)
- **Smoother Stanley guidance** (curvature-aware segments)

**Cost:** ~2-3 weeks development, moderate risk (guidance algorithm changes)

---

## Table of Contents

1. [Current State Analysis](#current-state-analysis)
2. [The Problem](#the-problem)
3. [Proposed Solution](#proposed-solution)
4. [Architecture Design](#architecture-design)
5. [Implementation Phases](#implementation-phases)
6. [Performance Analysis](#performance-analysis)
7. [GIS MCP Integration](#gis-mcp-integration)
8. [Migration Strategy](#migration-strategy)
9. [Testing Strategy](#testing-strategy)
10. [Risks and Mitigations](#risks-and-mitigations)

---

## Current State Analysis

### Track Model (Point-Based Storage)

```csharp
// Shared/AgValoniaGPS.Models/Track/Track.cs
public class Track
{
    public string Name { get; set; }
    public List<Vec3> Points { get; set; }  // Point-based storage
    public TrackType Type { get; set; }
    public bool IsClosed { get; set; }
    // ...
}
```

**Current storage characteristics:**
- Each point: Easting, Northing, Heading (3 doubles = 24 bytes)
- 100-point track = 2,400 bytes
- 1000-point track = 24,000 bytes

### TrackGuidanceService (Segment-Based Logic)

Despite point storage, guidance works with segments:

```csharp
// L68-91: Find nearest segment FIRST
(int indexA, int indexB) = FindNearestSegment(...);

// L101-102: Get segment endpoints
Vec3 ptA = points[indexA];
Vec3 ptB = points[indexB];

// L116-117: Calculate segment direction (EVERY CYCLE)
double dx = ptB.Easting - ptA.Easting;
double dz = ptB.Northing - ptA.Northing;

// L540-561: Perpendicular distance to segment
private double PerpendicularDistanceToSegment(Vec3 point, Vec3 segA, Vec3 segB)
{
    double segDx = segB.Easting - segA.Easting;  // Repeated calculation
    double segDy = segB.Northing - segA.Northing;
    double segLenSq = segDx * segDx + segDy * segDy;  // Repeated
    // ...
}
```

**Key findings:**
1. **Segment discovery happens every guidance cycle** (10 Hz = 10x/second)
2. **dx, dz, segLenSq are recalculated for every segment checked**
3. **No caching of segment metadata**

### Performance Profile

| Operation | Current Complexity | Typical Time (100 pts) | Typical Time (1000 pts) |
|-----------|-------------------|------------------------|-------------------------|
| Find nearest segment | O(n) linear search | ~5-10 µs | ~50-100 µs |
| Perpendicular distance | O(1) per segment | ~2-3 µs | ~2-3 µs |
| Goal point calculation | O(k) where k = goal dist / pt spacing | ~10-20 µs | ~50-100 µs |
| **Total guidance** | **O(n)** | **~20-40 µs** | **~120-250 µs** |

**At 10 Hz guidance rate:**
- 100 points: 0.02% CPU (negligible)
- 1000 points: 0.1-0.25% CPU (still negligible)
- **BUT** with multiple guidance services (Pure Pursuit + Stanley + YouTurn), cumulative overhead increases

---

## The Problem

### Problem 1: Redundant Calculations

Every guidance cycle (10 Hz), for every segment checked during nearest segment search:

```csharp
// These are calculated repeatedly:
double dx = ptB.Easting - ptA.Easting;        // Subtraction
double dz = ptB.Northing - ptA.Northing;      // Subtraction
double segLenSq = dx * dx + dz * dz;          // Multiplication + addition
double segLen = Math.Sqrt(segLenSq);          // Expensive sqrt
double segHeading = Math.Atan2(dx, dz);       // Expensive atan2
```

**For a 100-point track with ±25 segment search radius:**
- 50 segments checked per cycle
- 5 sqrt + 5 atan2 per cycle
- **50 sqrt + 50 atan2 per second**

### Problem 2: No Spatial Locality

Current search always checks ±N segments from current index:

```csharp
// L507-531: Local search radius
int searchRadius = (int)(searchDistance / 2) + 8;
for (int offset = -searchRadius; offset <= searchRadius; offset++)
{
    // Check EVERY segment in range, even if far away
}
```

**Issue:** On a long straight track, we check many segments that are spatially distant in the perpendicular direction.

### Problem 3: Uniform Point Density

Current curves use uniform point spacing:

```csharp
// Curve recording adds points at fixed distance intervals
// Straight sections get same density as curves
```

**Waste:** A 500m straight section with 1m spacing = 500 points (mostly redundant)

### Problem 4: No Curvature Awareness

Stanley guidance would benefit from curvature information:

```csharp
// Stanley handles curves, but doesn't know "how sharp"
// Could pre-compute curvature for smoother control
```

---

## Proposed Solution

### Overview: Explicit Segment Model

Replace implicit segments (adjacent point pairs) with explicit `TrackSegment` objects:

```csharp
/// <summary>
/// Explicit segment with pre-computed metadata
/// </summary>
public class TrackSegment
{
    // Endpoints
    public Vec3 Start { get; set; }
    public Vec3 End { get; set; }

    // Pre-computed metadata (calculated once on track creation)
    public double Length { get; set; }
    public double Heading { get; set; }           // Radians
    public double LengthSquared { get; set; }     // For faster distance calcs
    public Vec2 Direction { get; set; }           // Normalized (dx, dy)
    public double Curvature { get; set; }         // Optional: 1/radius (0 = straight)

    // Spatial indexing
    public Vec2 Midpoint { get; set; }            // For R-tree or grid index
    public BoundingBox Bounds { get; set; }       // Segment bounding box

    // Navigation
    public int Index { get; set; }                // Position in track
    public TrackSegment? Next { get; set; }       // Linked list for fast traversal
    public TrackSegment? Previous { get; set; }
}
```

### New Track Model

```csharp
/// <summary>
/// Segment-based track for efficient guidance
/// </summary>
public class SegmentTrack
{
    public string Name { get; set; } = string.Empty;
    public TrackType Type { get; set; }
    public bool IsClosed { get; set; }

    // Core storage
    public List<TrackSegment> Segments { get; set; } = new();

    // Spatial index (optional, for large tracks)
    private ISpatialIndex? _spatialIndex;

    // Computed properties
    public int SegmentCount => Segments.Count;
    public double TotalLength => Segments.Sum(s => s.Length);
    public bool IsABLine => Segments.Count == 1 && Type == TrackType.ABLine;

    // Factory methods
    public static SegmentTrack FromPoints(List<Vec3> points, TrackType type, bool isClosed);
    public static SegmentTrack FromABLine(Vec3 pointA, Vec3 pointB);

    // Compatibility
    public List<Vec3> ToPoints() => Segments.SelectMany(s => new[] { s.Start, s.End })
                                          .Distinct()
                                          .ToList();
    public Track ToLegacyTrack();
}
```

### Spatial Index Interface

```csharp
/// <summary>
/// Spatial index for fast nearest segment lookup
/// </summary>
public interface ISpatialIndex
{
    void Build(List<TrackSegment> segments);
    TrackSegment? FindNearest(Vec2 position, double maxDistance = double.MaxValue);
    List<TrackSegment> FindInRange(Vec2 position, double radius);
}

/// <summary>
/// Simple grid-based spatial index
/// O(1) average case lookup
/// </summary>
public class GridSpatialIndex : ISpatialIndex
{
    private readonly Dictionary<(int, int), List<TrackSegment>> _grid;
    private readonly double _cellSize;

    public GridSpatialIndex(double cellSize = 10.0)  // 10m grid cells
    {
        _grid = new Dictionary<(int, int), List<TrackSegment>>();
        _cellSize = cellSize;
    }

    public void Build(List<TrackSegment> segments)
    {
        foreach (var segment in segments)
        {
            var cell = GetCell(segment.Midpoint);
            if (!_grid.ContainsKey(cell))
                _grid[cell] = new List<TrackSegment>();
            _grid[cell].Add(segment);
        }
    }

    public TrackSegment? FindNearest(Vec2 position, double maxDistance = double.MaxValue)
    {
        var cell = GetCell(position);
        if (!_grid.ContainsKey(cell)) return null;

        // Only check segments in this cell (and 8 neighbors for edge cases)
        // Typically 1-10 segments instead of 100+
        return _grid[cell]
            .Concat(GetNeighborCells(cell).SelectMany(c => _grid.GetValueOrDefault(c, Enumerable.Empty<TrackSegment>())))
            .MinBy(s => s.DistanceToPoint(position));
    }

    private (int, int) GetCell(Vec2 point) =>
        ((int)(point.Easting / _cellSize), (int)(point.Northing / _cellSize));
}
```

---

## Architecture Design

### Class Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        SegmentTrack                             │
├─────────────────────────────────────────────────────────────────┤
│ + Name: string                                                  │
│ + Type: TrackType                                              │
│ + IsClosed: bool                                               │
│ + Segments: List<TrackSegment>                                 │
│ - _spatialIndex: ISpatialIndex?                                │
│                                                                 │
│ + FromPoints(points, type, isClosed): SegmentTrack             │
│ + FindNearestSegment(position): TrackSegment                   │
│ + ToPoints(): List<Vec3>                                       │
│ + ToLegacyTrack(): Track                                       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ 1
                              │
                              │ *
┌─────────────────────────────────────────────────────────────────┐
│                      TrackSegment                               │
├─────────────────────────────────────────────────────────────────┤
│ + Start: Vec3                                                  │
│ + End: Vec3                                                    │
│ + Length: double                                               │
│ + Heading: double                                              │
│ + LengthSquared: double                                        │
│ + Direction: Vec2                                              │
│ + Curvature: double                                            │
│ + Midpoint: Vec2                                               │
│ + Bounds: BoundingBox                                          │
│ + Index: int                                                   │
│ + Next: TrackSegment?                                          │
│ + Previous: TrackSegment?                                      │
│                                                                 │
│ + DistanceToPoint(point): double                               │
│ + ClosestPoint(point): Vec2                                    │
│ + Interpolate(distance): Vec2                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ implements
                              │
┌─────────────────────────────────────────────────────────────────┐
│                      ISpatialIndex                              │
├─────────────────────────────────────────────────────────────────┤
│ + Build(segments): void                                        │
│ + FindNearest(position, maxDistance): TrackSegment?            │
│ + FindInRange(position, radius): List<TrackSegment>            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │
          ┌───────────────────┴───────────────────┐
          │                                       │
┌─────────────────────────┐         ┌─────────────────────────┐
│   GridSpatialIndex      │         │   RTreeSpatialIndex     │
│ (Simple, fast enough)   │         │   (Advanced, O(log n))  │
└─────────────────────────┘         └─────────────────────────┘
```

### Data Flow: Guidance Calculation

```
GPS Update (10 Hz)
        │
        ▼
┌─────────────────────────────────────────────────────────────────┐
│              SegmentTrackGuidanceService.CalculateGuidance()    │
│                                                                 │
│  1. Find nearest segment:                                      │
│     if (_spatialIndex != null)                                 │
│         segment = _spatialIndex.FindNearest(pivotPosition);    │
│     else                                                       │
│         segment = FindNearestLinear(pivotPosition, index);     │
│                                                                 │
│  2. Get segment properties (NO CALCULATION):                   │
│     dx = segment.Direction.X;  // Already normalized           │
│     dz = segment.Direction.Y;                                 │
│     segLen = segment.Length;   // Pre-computed                 │
│                                                                 │
│  3. Calculate cross-track error:                               │
│     XTE = segment.DistanceToPoint(pivotPosition);              │
│                                                                 │
│  4. Calculate goal point:                                      │
│     Walk segments using Next links (O(k), not O(n*k))          │
│     goalPoint = InterpolateGoalPoint(...);                     │
│                                                                 │
│  5. Apply Pure Pursuit or Stanley:                             │
│     steerAngle = CalculateSteerAngle(...);                      │
└─────────────────────────────────────────────────────────────────┘
        │
        ▼
   Steer Output
```

---

## Implementation Phases

### Phase 1: Core Segment Model (Week 1)

**Files to create:**
```
Shared/AgValoniaGPS.Models/Track/
├── TrackSegment.cs                    (New)
├── SegmentTrack.cs                     (New)
└── ISpatialIndex.cs                    (New)
```

**Tasks:**
1. Create `TrackSegment` class with all properties
2. Implement `DistanceToPoint()`, `ClosestPoint()`, `Interpolate()`
3. Create `SegmentTrack` class with basic CRUD
4. Implement `FromPoints()` factory method
5. Add unit tests for segment math

**Acceptance criteria:**
- TrackSegment correctly computes all metadata
- SegmentTrack can be created from point list
- ToPoints() produces identical output (round-trip test)
- All existing geometry tests pass

### Phase 2: Spatial Indexing (Week 1-2)

**Files to create:**
```
Shared/AgValoniaGPS.Models/Track/
├── Spatial/
│   ├── GridSpatialIndex.cs            (New)
│   ├── RTreeSpatialIndex.cs            (New, optional)
│   └── BoundingBox.cs                  (New or move from existing)
```

**Tasks:**
1. Implement `GridSpatialIndex` with 10m cell size
2. Implement `RTreeSpatialIndex` (consider external lib)
3. Add benchmarking harness
4. Compare performance vs linear search

**Acceptance criteria:**
- Grid index finds correct nearest segment
- Performance: 10x faster for 500+ point tracks
- No measurable overhead for <100 point tracks

### Phase 3: Guidance Service (Week 2)

**Files to modify:**
```
Shared/AgValoniaGPS.Services/Track/
├── TrackGuidanceService.cs             (Modify - add overload)
└── SegmentTrackGuidanceService.cs      (New, or merge into existing)
```

**Tasks:**
1. Create `SegmentTrackGuidanceService` (or extend existing)
2. Implement guidance using pre-computed segment data
3. Use spatial index when available
4. Maintain identical output to current service

**Acceptance criteria:**
- Guidance output matches current service (within 1e-6)
- 20-30% faster for 100+ point tracks
- All existing guidance tests pass

### Phase 4: Variable Density (Week 2-3)

**Files to create:**
```
Shared/AgValoniaGPS.Services/Track/
├── TrackSimplificationService.cs       (New)
└── CurveUtils.cs                       (Extend existing)
```

**Tasks:**
1. Implement Douglas-Peucker simplification
2. Add curvature-based point insertion for sharp curves
3. Create adaptive resampling algorithm
4. Integrate GIS MCP `simplify` for initial proof-of-concept

**Acceptance criteria:**
- 500-point curve reduces to ~100-200 points (2-5x reduction)
- Maximum deviation < tolerance (configurable, default 0.5m)
- Smoother Stanley guidance (measurable heading error reduction)

### Phase 5: Integration (Week 3)

**Files to modify:**
```
Shared/AgValoniaGPS.ViewModels/
├── MainViewModel.Commands.Track.cs     (Modify)
└── MainViewModel.cs                     (Minor updates)

Shared/AgValoniaGPS.Services/
├── TrackFilesService.cs                (Modify - add format detection)
└── ConfigurationService.cs             (Modify - add segment track option)
```

**Tasks:**
1. Add configuration option: "Use segment-based tracks"
2. Update track loading to detect format
3. Maintain backward compatibility (auto-convert)
4. Update UI to show segment count vs point count

**Acceptance criteria:**
- Old track files load correctly
- New format saves with .segment extension
- User can toggle between formats
- No breaking changes to file format

---

## Performance Analysis

### Expected Improvements

| Track Size | Current (guidance) | With Segments | With Spatial Index | Speedup |
|------------|-------------------|---------------|-------------------|---------|
| 50 points | 15 µs | 12 µs (20%) | 12 µs | 1.25x |
| 100 points | 25 µs | 18 µs (28%) | 15 µs | 1.7x |
| 500 points | 120 µs | 85 µs (29%) | 25 µs | 4.8x |
| 1000 points | 240 µs | 170 µs (29%) | 30 µs | 8x |

### Memory Overhead

| Component | Per Track | Notes |
|-----------|-----------|-------|
| TrackSegment (vs 2 Vec3) | +64 bytes | Length², Direction, Curvature, Midpoint, Bounds, links |
| Grid index | ~1KB | For 100-point track |
| Total overhead (100 pts) | ~7.4KB | From 2.4KB to 9.8KB (4x) |
| Total overhead (1000 pts) | ~65KB | From 24KB to 89KB (3.7x) |

**Analysis:** Memory tradeoff is acceptable. Modern devices have GB of RAM; 65KB is negligible.

### File Size Impact

With variable density (Douglas-Peucker simplification):

| Track Type | Original | Simplified | Reduction |
|------------|----------|------------|-----------|
| AB line | 2 pts (48 bytes) | 2 pts (48 bytes) | 0% |
| Gentle curve | 200 pts (4.8KB) | ~50 pts (1.2KB) | 75% |
| Complex curve | 500 pts (12KB) | ~150 pts (3.6KB) | 70% |
| Field boundary | 1000 pts (24KB) | ~200 pts (4.8KB) | 80% |

---

## GIS MCP Integration

### Douglas-Peucker Simplification

```csharp
/// <summary>
/// Simplify a track using Douglas-Peucker algorithm
/// </summary>
public class TrackSimplificationService
{
    // Can use GIS MCP simplify tool via CLI for initial testing
    // Then implement native C# version for production

    public List<Vec3> SimplifyDouglasPeucker(List<Vec3> points, double tolerance)
    {
        if (points.Count <= 2) return points;

        // Find point with maximum distance from line Start-End
        double maxDistance = 0;
        int maxIndex = 0;
        Vec3 start = points[0];
        Vec3 end = points[^1];

        for (int i = 1; i < points.Count - 1; i++)
        {
            double dist = PerpendicularDistance(points[i], start, end);
            if (dist > maxDistance)
            {
                maxDistance = dist;
                maxIndex = i;
            }
        }

        // Recursively simplify
        if (maxDistance > tolerance)
        {
            var left = SimplifyDouglasPeucker(points.Take(maxIndex + 1).ToList(), tolerance);
            var right = SimplifyDouglasPeucker(points.Skip(maxIndex).ToList(), tolerance);

            // Merge (remove duplicate point at maxIndex)
            return left.Concat(right.Skip(1)).ToList();
        }
        else
        {
            return new List<Vec3> { start, end };
        }
    }

    private double PerpendicularDistance(Vec3 point, Vec3 lineStart, Vec3 lineEnd)
    {
        // Using GeometryMath or segment-based calculation
        double dx = lineEnd.Easting - lineStart.Easting;
        double dz = lineEnd.Northing - lineStart.Northing;
        double lenSq = dx * dx + dz * dz;

        if (lenSq < double.Epsilon)
            return GeometryMath.Distance(point, lineStart);

        double t = Math.Max(0, Math.Min(1, ((point.Easting - lineStart.Easting) * dx +
                                            (point.Northing - lineStart.Northing) * dz) / lenSq));

        double projX = lineStart.Easting + t * dx;
        double projZ = lineStart.Northing + t * dz;

        return GeometryMath.Distance(point.Easting, point.Northing, projX, projZ);
    }
}
```

### Curvature Calculation

```csharp
/// <summary>
/// Calculate segment curvature for Stanley guidance
/// </summary>
public static class SegmentCurvature
{
    public static double CalculateCurvature(Vec3 p0, Vec3 p1, Vec3 p2)
    {
        // Menger curvature k = 4*Area / (a*b*c)
        // For small arcs, approximate as 1/radius

        double a = GeometryMath.Distance(p0, p1);
        double b = GeometryMath.Distance(p1, p2);
        double c = GeometryMath.Distance(p0, p2);

        if (c < 0.01) return 0;  // Collinear

        // Using cross product for area
        double area = Math.Abs((p1.Easting - p0.Easting) * (p2.Northing - p0.Northing) -
                              (p1.Northing - p0.Northing) * (p2.Easting - p0.Easting)) / 2.0;

        return 4 * area / (a * b * c);
    }
}
```

---

## Migration Strategy

### Backward Compatibility

**Option A: Dual Format (Recommended)**
- Save both formats (auto-detect on load)
- Phase 1-2: Read point-based, convert to segment internally
- Phase 3-4: Write both formats
- Phase 5: Default to segment, point as fallback

**Option B: Conversion Utility**
- One-time conversion tool
- User runs when ready
- Simpler code, more user effort

### File Format

```
// New format: .track.json
{
  "format": "segment-v1",
  "name": "My Track",
  "type": 4,  // TrackType.Curve
  "isClosed": false,
  "segments": [
    {
      "start": { "easting": 100.0, "northing": 200.0, "heading": 0.78 },
      "end": { "easting": 105.0, "northing": 205.0, "heading": 0.78 },
      "length": 7.07,
      "lengthSquared": 50.0,
      "heading": 0.785398,
      "direction": { "easting": 0.707, "northing": 0.707 },
      "curvature": 0.0,
      "index": 0
    },
    // ...
  ],
  "spatialIndex": null  // Optional, can be regenerated
}
```

**Legacy format compatibility:**
```csharp
public static class TrackConverter
{
    public static SegmentTrack FromLegacyTrack(Track legacy)
    {
        return SegmentTrack.FromPoints(legacy.Points, legacy.Type, legacy.IsClosed);
    }

    public static Track ToLegacyTrack(SegmentTrack modern)
    {
        return new Track
        {
            Name = modern.Name,
            Type = modern.Type,
            IsClosed = modern.IsClosed,
            Points = modern.ToPoints()
        };
    }
}
```

---

## Testing Strategy

### Unit Tests

```csharp
// Tests/AgValoniaGPS.Models.Tests/SegmentTrackTests.cs
[TestFixture]
public class SegmentTrackTests
{
    [Test]
    public void FromPoints_RoundTrip_ProducesIdenticalPoints()
    {
        var original = new List<Vec3>
        {
            new Vec3(0, 0, 0),
            new Vec3(10, 0, 0),
            new Vec3(10, 10, Math.PI/2)
        };

        var segmentTrack = SegmentTrack.FromPoints(original, TrackType.Curve, false);
        var result = segmentTrack.ToPoints();

        Assert.That(result, Is.EqualTo(original).Within(1e-6));
    }

    [Test]
    public void DistanceToPoint_MatchesGeometryMath()
    {
        var segment = new TrackSegment
        {
            Start = new Vec3(0, 0, 0),
            End = new Vec3(10, 0, 0)
        };

        var point = new Vec2(5, 3);

        double segmentDist = segment.DistanceToPoint(point);
        double geometryDist = GeometryMath.Distance(point, new Vec2(5, 0));

        Assert.That(segmentDist, Is.EqualTo(geometryDist).Within(1e-6));
    }
}
```

### Integration Tests

```csharp
// Tests/AgValoniaGPS.Services.Tests/SegmentGuidanceTests.cs
[TestFixture]
public class SegmentGuidanceTests
{
    [Test]
    public void CalculateGuidance_OutputsMatchCurrentImplementation()
    {
        // Arrange
        var points = GenerateTestCurve(100);  // 100-point sine wave
        var legacyTrack = new Track { Points = points, Type = TrackType.Curve };
        var segmentTrack = SegmentTrack.FromPoints(points, TrackType.Curve, false);

        var input = new TrackGuidanceInput
        {
            Track = legacyTrack,
            PivotPosition = new Vec3(50, 5, 0),
            // ... other params
        };

        var segmentInput = CreateSegmentInput(input, segmentTrack);

        // Act
        var legacyOutput = _legacyService.CalculateGuidance(input);
        var segmentOutput = _segmentService.CalculateGuidance(segmentInput);

        // Assert
        Assert.That(segmentOutput.SteerAngle,
            Is.EqualTo(legacyOutput.SteerAngle).Within(0.001));  // 0.001 degree tolerance
        Assert.That(segmentOutput.CrossTrackError,
            Is.EqualTo(legacyOutput.CrossTrackError).Within(0.001));  // 1mm tolerance
    }
}
```

### Performance Benchmarks

```csharp
[TestFixture]
public class SegmentPerformanceTests
{
    [Test]
    [TestCase(50)]
    [TestCase(100)]
    [TestCase(500)]
    [TestCase(1000)]
    public void GuidancePerformance_Benchmark(int pointCount)
    {
        var track = GenerateTestTrack(pointCount);
        var input = CreateTestInput(track);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            _service.CalculateGuidance(input);
        }
        sw.Stop();

        var avgMicroseconds = sw.Elapsed.TotalMicroseconds / 10000;
        Console.WriteLine($"{pointCount} points: {avgMicroseconds:F2} µs per guidance");

        // Assert acceptable performance
        Assert.That(avgMicroseconds, Is.LessThan(100));  // < 100 µs = > 10 kHz guidance
    }
}
```

---

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Guidance output differs** | Medium | High | Extensive A/B testing, tolerance-based assertions |
| **Spatial index bugs** | Low | High | Fallback to linear search, comprehensive unit tests |
| **File format breaking changes** | Low | Medium | Dual format support, migration utility |
| **Memory overhead too high** | Low | Low | Configurable spatial index, lazy loading |
| **Performance regression on small tracks** | Low | Low | Benchmark-based thresholds, disable index for <50 segments |
| **Curve simplification too aggressive** | Medium | Medium | Configurable tolerance, user preview mode |

### Rollback Plan

If issues arise:
1. **Phase 1-2:** Can be abandoned; segment model is additive
2. **Phase 3:** Keep both services; add feature flag
3. **Phase 4:** Simplification is optional; can disable
4. **Phase 5:** Format detection allows seamless fallback

---

## Success Criteria

### Performance

- [ ] Guidance calculation: 20% faster for 100+ point tracks
- [ ] Nearest segment lookup: 10x faster for 500+ point tracks with spatial index
- [ ] Memory overhead: < 5x (acceptable)

### Functionality

- [ ] Guidance output matches current implementation (within 1mm/0.001°)
- [ ] All existing tests pass
- [ ] File format backward compatible

### Code Quality

- [ ] No increase in code complexity (maintainability index)
- [ ] Test coverage > 80% for new code
- [ ] Documentation updated

---

## Open Questions

1. **Spatial index library:** Use existing C# R-tree implementation or build simple grid?
2. **Curvature storage:** Is per-segment curvature useful, or compute on-demand?
3. **File format:** JSON (readable) or binary (compact)? Hybrid option?
4. **Simplification tolerance:** Fixed (0.5m) or configurable per track?
5. **AB line representation:** Still 2 segments, or single infinite segment?

---

## References

- Current `Track.cs` and `TrackGuidanceService.cs`
- AgOpenGPS curve handling insights (Brian's comments)
- Douglas-Peucker algorithm: [Wikipedia](https://en.wikipedia.org/wiki/Ramer–Douglas–Peucker_algorithm)
- Spatial indexing: R-tree, Grid, Quadtree trade-offs
- GIS MCP tools: `simplify`, `nearest_point_on_geometry`
- Completed: `UNIFIED_TRACK_REFACTOR_PLAN.md` (related work)

---

## Appendix: Sample Code

### TrackSegment Implementation

```csharp
// Shared/AgValoniaGPS.Models/Track/TrackSegment.cs
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Track;

public class TrackSegment
{
    public Vec3 Start { get; set; }
    public Vec3 End { get; set; }

    // Pre-computed metadata
    private double _length;
    private double _lengthSquared;
    private double _heading;
    private Vec2 _direction;

    public double Length => _length;
    public double LengthSquared => _lengthSquared;
    public double Heading => _heading;
    public Vec2 Direction => _direction;

    // Optional advanced features
    public double Curvature { get; set; } = 0.0;
    public Vec2 Midpoint { get; set; }
    public BoundingBox Bounds { get; set; }

    // Navigation
    public int Index { get; set; }
    public TrackSegment? Next { get; set; }
    public TrackSegment? Previous { get; set; }

    public TrackSegment(Vec3 start, Vec3 end, int index = 0)
    {
        Start = start;
        End = end;
        Index = index;

        // Pre-compute all metadata
        double dx = end.Easting - start.Easting;
        double dz = end.Northing - start.Northing;
        _lengthSquared = dx * dx + dz * dz;
        _length = Math.Sqrt(_lengthSquared);
        _heading = Math.Atan2(dx, dz);

        if (_length > 1e-10)
        {
            _direction = new Vec2(dx / _length, dz / _length);
        }
        else
        {
            _direction = new Vec2(1, 0);  // Degenerate segment
        }

        Midpoint = new Vec2((start.Easting + end.Easting) / 2,
                           (start.Northing + end.Northing) / 2);
    }

    /// <summary>
    /// Calculate perpendicular distance from point to segment
    /// </summary>
    public double DistanceToPoint(Vec2 point)
    {
        double t = ((point.Easting - Start.Easting) * _direction.Easting +
                   (point.Northing - Start.Northing) * _direction.Northing);

        // Clamp to segment
        if (t < 0) return GeometryMath.Distance(point, Start.ToVec2());
        if (t > _length) return GeometryMath.Distance(point, End.ToVec2());

        // Perpendicular distance to line
        Vec2 closest = new Vec2(
            Start.Easting + t * _direction.Easting,
            Start.Northing + t * _direction.Northing
        );

        return GeometryMath.Distance(point, closest);
    }

    /// <summary>
    /// Find closest point on segment
    /// </summary>
    public Vec2 ClosestPoint(Vec2 point)
    {
        double t = ((point.Easting - Start.Easting) * _direction.Easting +
                   (point.Northing - Start.Northing) * _direction.Northing);

        // Clamp to segment
        t = Math.Max(0, Math.Min(_length, t));

        return new Vec2(
            Start.Easting + t * _direction.Easting,
            Start.Northing + t * _direction.Northing
        );
    }

    /// <summary>
    /// Interpolate point at distance along segment
    /// </summary>
    public Vec2 Interpolate(double distance)
    {
        distance = Math.Max(0, Math.Min(_length, distance));
        return new Vec2(
            Start.Easting + distance * _direction.Easting,
            Start.Northing + distance * _direction.Northing
        );
    }
}

public class BoundingBox
{
    public double MinEasting { get; set; }
    public double MaxEasting { get; set; }
    public double MinNorthing { get; set; }
    public double MaxNorthing { get; set; }

    public bool Contains(Vec2 point)
    {
        return point.Easting >= MinEasting && point.Easting <= MaxEasting &&
               point.Northing >= MinNorthing && point.Northing <= MaxNorthing;
    }

    public bool Intersects(BoundingBox other)
    {
        return !(MaxEasting < other.MinEasting || MinEasting > other.MaxEasting ||
                 MaxNorthing < other.MinNorthing || MinNorthing > other.MaxNorthing);
    }
}
```

### SegmentTrack Implementation

```csharp
// Shared/AgValoniaGPS.Models/Track/SegmentTrack.cs
using System.Collections.Generic;
using System.Linq;

namespace AgValoniaGPS.Models.Track;

public class SegmentTrack
{
    public string Name { get; set; } = string.Empty;
    public TrackType Type { get; set; }
    public bool IsClosed { get; set; }

    public List<TrackSegment> Segments { get; set; } = new();

    private ISpatialIndex? _spatialIndex;

    public int SegmentCount => Segments.Count;
    public double TotalLength => Segments.Sum(s => s.Length);
    public bool IsABLine => Segments.Count == 1 && Type == TrackType.ABLine;

    /// <summary>
    /// Create segment track from point list
    /// </summary>
    public static SegmentTrack FromPoints(List<Vec3> points, TrackType type, bool isClosed)
    {
        if (points == null || points.Count < 2)
            throw new ArgumentException("Need at least 2 points");

        var track = new SegmentTrack
        {
            Type = type,
            IsClosed = isClosed
        };

        // Create segments from adjacent points
        int count = isClosed ? points.Count : points.Count - 1;
        for (int i = 0; i < count; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];

            // Use segment heading if available, otherwise calculate from positions
            if (start.Heading == 0 && end.Heading == 0)
            {
                double heading = Math.Atan2(end.Easting - start.Easting,
                                           end.Northing - start.Northing);
                start = new Vec3(start.Easting, start.Northing, heading);
                end = new Vec3(end.Easting, end.Northing, heading);
            }

            track.Segments.Add(new TrackSegment(start, end, i));
        }

        // Link segments for fast traversal
        for (int i = 0; i < track.Segments.Count; i++)
        {
            track.Segments[i].Previous = i > 0 ? track.Segments[i - 1] :
                                         (isClosed ? track.Segments[^1] : null);
            track.Segments[i].Next = i < track.Segments.Count - 1 ? track.Segments[i + 1] :
                                     (isClosed ? track.Segments[0] : null);
        }

        return track;
    }

    /// <summary>
    /// Find nearest segment (uses spatial index if available)
    /// </summary>
    public TrackSegment? FindNearestSegment(Vec2 position, int currentIndex = -1,
                                           bool searchLocal = true, double maxDistance = 50)
    {
        if (_spatialIndex != null)
        {
            return _spatialIndex.FindNearest(position, maxDistance);
        }

        // Fallback to linear search with current index hint
        if (searchLocal && currentIndex >= 0 && currentIndex < Segments.Count)
        {
            int searchRadius = Math.Min(Segments.Count, (int)(maxDistance / 2) + 8);
            double minDist = double.MaxValue;
            TrackSegment? nearest = null;

            for (int offset = -searchRadius; offset <= searchRadius; offset++)
            {
                int idx = (currentIndex + offset + Segments.Count) % Segments.Count;
                var segment = Segments[idx];
                double dist = segment.DistanceToPoint(position);

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = segment;
                }
            }

            return nearest;
        }

        // Global linear search
        return Segments.MinBy(s => s.DistanceToPoint(position));
    }

    /// <summary>
    /// Convert back to point list for compatibility
    /// </summary>
    public List<Vec3> ToPoints()
    {
        var points = new List<Vec3>(Segments.Count + 1);
        foreach (var segment in Segments)
        {
            points.Add(segment.Start);
        }
        if (!IsClosed && Segments.Count > 0)
        {
            points.Add(Segments[^1].End);
        }
        return points;
    }

    /// <summary>
    /// Convert to legacy Track model
    /// </summary>
    public Track ToLegacyTrack()
    {
        return new Track
        {
            Name = Name,
            Type = Type,
            IsClosed = IsClosed,
            Points = ToPoints()
        };
    }

    /// <summary>
    /// Enable spatial indexing for large tracks
    /// </summary>
    public void EnableSpatialIndex(ISpatialIndex? index = null)
    {
        _spatialIndex = index ?? new GridSpatialIndex(cellSize: 10.0);
        _spatialIndex.Build(Segments);
    }

    /// <summary>
    /// Disable spatial indexing (saves memory)
    /// </summary>
    public void DisableSpatialIndex()
    {
        _spatialIndex = null;
    }
}
```

---

**Document Status:** Draft - Ready for review
**Next Steps:** Discuss with team, prioritize phases, begin Phase 1 implementation
