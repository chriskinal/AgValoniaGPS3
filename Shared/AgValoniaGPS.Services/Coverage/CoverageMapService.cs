// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Coverage;

/// <summary>
/// Tracks and manages coverage (worked area) as the tool moves across the field.
/// Coverage is stored as triangle strips for efficient rendering.
///
/// Based on AgOpenGPS CPatches implementation with Torriem's area calculation.
/// </summary>
public class CoverageMapService(IWorkedAreaService workedAreaService) : ICoverageMapService
{
    // All coverage patches (completed and active)
    private readonly List<CoveragePatch> _patches = new();

    // Active patches per zone (index = zone index)
    private readonly Dictionary<int, CoveragePatch> _activePatches = new();

    // Track indices of active patches for O(1) lookup
    private readonly Dictionary<int, int> _activePatchIndices = new(); // zone -> patch index

    // Patches pending save to file
    private readonly List<CoveragePatch> _patchSaveList = new();

    // Area totals
    private double _totalWorkedArea;
    private double _totalWorkedAreaUser;

    // Triangle count threshold for splitting patches (for rendering efficiency)
    private const int MAX_TRIANGLES_PER_PATCH = 62;

    // Dirty flag to track if coverage has changed since last flush
    private bool _coverageDirty;
    private double _pendingAreaAdded;

    // Spatial index for fast coverage lookup
    private const double COVERAGE_GRID_CELL_SIZE = 50.0; // meters per cell
    private readonly Dictionary<(int, int), List<int>> _coverageSpatialIndex = new();
    private int _gridOffsetE; // offset to make grid indices non-negative
    private int _gridOffsetN;
    private bool _spatialIndexNeedsRebuild = true;

    // Cached patch bounding boxes for fast rejection
    private readonly List<(double MinE, double MaxE, double MinN, double MaxN)> _patchBounds = new();

    public double TotalWorkedArea => _totalWorkedArea;
    public double TotalWorkedAreaUser => _totalWorkedAreaUser;
    public int PatchCount => _patches.Count;
    public bool IsAnyZoneMapping => _activePatches.Count > 0;

    public event EventHandler<CoverageUpdatedEventArgs>? CoverageUpdated;

    public void StartMapping(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge, CoverageColor? color = null)
    {
        // If already mapping this zone, just continue
        if (_activePatches.ContainsKey(zoneIndex))
            return;

        // Create new patch
        var patch = new CoveragePatch
        {
            ZoneIndex = zoneIndex,
            Color = color ?? GetZoneColor(zoneIndex),
            IsActive = true,
            Vertices = new List<Vec3>(64)
        };

        // First vertex is the color
        patch.Vertices.Add(patch.Color.ToVec3());

        // Add initial edge points
        patch.Vertices.Add(new Vec3(leftEdge.Easting, leftEdge.Northing, 0));
        patch.Vertices.Add(new Vec3(rightEdge.Easting, rightEdge.Northing, 0));

        _patches.Add(patch);
        int patchIndex = _patches.Count - 1;
        _activePatches[zoneIndex] = patch;
        _activePatchIndices[zoneIndex] = patchIndex;
        // Don't invalidate - active patches are always checked via _activePatchIndices
        // Index will be updated when patch becomes inactive (StopMapping/SplitPatch)
    }

    public void StopMapping(int zoneIndex)
    {
        if (!_activePatches.TryGetValue(zoneIndex, out var patch))
            return;

        // Get patch index before removing from active tracking
        int patchIndex = _activePatchIndices.GetValueOrDefault(zoneIndex, -1);

        // Mark patch as complete
        patch.IsActive = false;
        _activePatches.Remove(zoneIndex);
        _activePatchIndices.Remove(zoneIndex);

        // Save patch if it has enough points
        // Require at least 9 vertices (1 color + 8 geometry = 4 edge pairs = 3 quads minimum)
        // This filters out tiny glitch patches from section flickering at boundaries
        if (patch.Vertices.Count >= 9)
        {
            _patchSaveList.Add(patch);
            // Add to spatial index now that it's finalized
            if (patchIndex >= 0)
                AddPatchToSpatialIndex(patchIndex);
        }
        else
        {
            // Remove incomplete/glitch patches - need full rebuild since we're removing
            _patches.Remove(patch);
            InvalidateSpatialIndex();
        }
    }

    public void AddCoveragePoint(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge)
    {
        if (!_activePatches.TryGetValue(zoneIndex, out var patch))
            return;

        // Add two vertices for next quad
        patch.Vertices.Add(new Vec3(leftEdge.Easting, leftEdge.Northing, 0));
        patch.Vertices.Add(new Vec3(rightEdge.Easting, rightEdge.Northing, 0));

        // Calculate area of new triangles
        int c = patch.Vertices.Count - 1;
        if (c >= 4) // Need at least 5 vertices (1 color + 4 positions = 2 triangles)
        {
            // Convert to array for area calculation
            var points = new Vec3[c + 1];
            for (int i = 0; i <= c; i++)
            {
                points[i] = patch.Vertices[i];
            }

            double area = workedAreaService.CalculateTriangleStripArea(points, c - 3);
            _totalWorkedArea += area;
            _totalWorkedAreaUser += area;
            _pendingAreaAdded += area;

            // Mark as dirty - event will fire on FlushCoverageUpdate()
            _coverageDirty = true;
        }

        // Break into chunks for rendering efficiency
        if (patch.TriangleCount > MAX_TRIANGLES_PER_PATCH)
        {
            SplitPatch(patch, zoneIndex, leftEdge, rightEdge);
        }
    }

    /// <summary>
    /// Split a patch that has too many triangles.
    /// Creates a new patch continuing from the last two points.
    /// </summary>
    private void SplitPatch(CoveragePatch oldPatch, int zoneIndex, Vec2 leftEdge, Vec2 rightEdge)
    {
        // Save the old patch and get its index before adding new patch
        oldPatch.IsActive = false;
        int oldPatchIndex = _patches.IndexOf(oldPatch);
        _patchSaveList.Add(oldPatch);

        // Create new patch
        var newPatch = new CoveragePatch
        {
            ZoneIndex = zoneIndex,
            Color = oldPatch.Color,
            IsActive = true,
            Vertices = new List<Vec3>(64)
        };

        // Color vertex
        newPatch.Vertices.Add(oldPatch.Color.ToVec3());

        // Start with last two points (to maintain continuity)
        newPatch.Vertices.Add(new Vec3(leftEdge.Easting, leftEdge.Northing, 0));
        newPatch.Vertices.Add(new Vec3(rightEdge.Easting, rightEdge.Northing, 0));

        _patches.Add(newPatch);
        int newPatchIndex = _patches.Count - 1;
        _activePatches[zoneIndex] = newPatch;
        _activePatchIndices[zoneIndex] = newPatchIndex;

        // Add old patch to spatial index incrementally (it's now finalized)
        if (oldPatchIndex >= 0)
            AddPatchToSpatialIndex(oldPatchIndex);
    }

    /// <summary>
    /// Fire the CoverageUpdated event if coverage has changed since last flush.
    /// Call this once per GPS update cycle to avoid firing 16 events for 16 sections.
    /// </summary>
    public void FlushCoverageUpdate()
    {
        if (!_coverageDirty) return;

        CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
        {
            TotalArea = _totalWorkedArea,
            PatchCount = _patches.Count,
            AreaAdded = _pendingAreaAdded
        });

        _coverageDirty = false;
        _pendingAreaAdded = 0;
    }

    public bool IsZoneMapping(int zoneIndex)
    {
        return _activePatches.ContainsKey(zoneIndex);
    }

    public bool IsPointCovered(double easting, double northing)
    {
        // Use spatial index to only check nearby patches
        foreach (int patchIndex in GetNearbyPatchIndices(easting, northing, COVERAGE_GRID_CELL_SIZE))
        {
            var patch = _patches[patchIndex];
            if (!patch.IsRenderable) continue;

            // Use cached bounding box for quick rejection
            if (patchIndex < _patchBounds.Count)
            {
                var bounds = _patchBounds[patchIndex];
                if (easting < bounds.MinE || easting > bounds.MaxE ||
                    northing < bounds.MinN || northing > bounds.MaxN)
                    continue;
            }

            // Check actual triangles in the strip
            // Triangle strip: vertices 1,2,3 form triangle 1; 2,3,4 form triangle 2; etc.
            // (vertex 0 is color)
            if (IsPointInTriangleStrip(patch.Vertices, easting, northing))
                return true;
        }

        return false;
    }

    public CoverageResult GetSegmentCoverage(Vec2 sectionCenter, double heading, double halfWidth, double lookAheadDistance = 0)
    {
        // Precompute transform for heading
        double cos = Math.Cos(-heading);
        double sin = Math.Sin(-heading);

        // Adjust center for look-ahead
        Vec2 checkCenter = lookAheadDistance == 0
            ? sectionCenter
            : new Vec2(
                sectionCenter.Easting + Math.Sin(heading) * lookAheadDistance,
                sectionCenter.Northing + Math.Cos(heading) * lookAheadDistance);

        // Collect coverage intervals along X-axis
        var intervals = new List<(double Start, double End)>();

        // Search radius for spatial index
        double searchRadius = halfWidth + lookAheadDistance + COVERAGE_GRID_CELL_SIZE;

        // Use spatial index to only check nearby patches
        foreach (int patchIndex in GetNearbyPatchIndices(checkCenter.Easting, checkCenter.Northing, searchRadius))
        {
            var patch = _patches[patchIndex];
            if (!patch.IsRenderable) continue;

            // Check each triangle in the strip (skip color vertex at index 0)
            for (int i = 1; i < patch.Vertices.Count - 2; i++)
            {
                var interval = GetTriangleXInterval(
                    patch.Vertices[i],
                    patch.Vertices[i + 1],
                    patch.Vertices[i + 2],
                    checkCenter, cos, sin, halfWidth);

                if (interval.HasValue)
                    intervals.Add(interval.Value);
            }
        }

        return CalculateCoverageFromIntervals(intervals, halfWidth);
    }

    public (CoverageResult Current, CoverageResult LookOn, CoverageResult LookOff) GetSegmentCoverageMulti(
        Vec2 sectionCenter, double heading, double halfWidth,
        double lookOnDistance, double lookOffDistance)
    {
        // Precompute transform for heading
        double cos = Math.Cos(-heading);
        double sin = Math.Sin(-heading);

        // Calculate all three check centers
        Vec2 currentCenter = sectionCenter;
        Vec2 lookOnCenter = new Vec2(
            sectionCenter.Easting + Math.Sin(heading) * lookOnDistance,
            sectionCenter.Northing + Math.Cos(heading) * lookOnDistance);
        Vec2 lookOffCenter = new Vec2(
            sectionCenter.Easting + Math.Sin(heading) * lookOffDistance,
            sectionCenter.Northing + Math.Cos(heading) * lookOffDistance);

        // Max radius to consider for any of the three positions
        double maxLookDist = Math.Max(lookOnDistance, lookOffDistance);
        double searchRadius = halfWidth + maxLookDist + COVERAGE_GRID_CELL_SIZE;

        var currentIntervals = new List<(double Start, double End)>();
        var lookOnIntervals = new List<(double Start, double End)>();
        var lookOffIntervals = new List<(double Start, double End)>();

        // Use spatial index to only check nearby patches
        foreach (int patchIndex in GetNearbyPatchIndices(sectionCenter.Easting, sectionCenter.Northing, searchRadius))
        {
            var patch = _patches[patchIndex];
            if (!patch.IsRenderable) continue;

            // Check each triangle in the strip
            for (int i = 1; i < patch.Vertices.Count - 2; i++)
            {
                var v0 = patch.Vertices[i];
                var v1 = patch.Vertices[i + 1];
                var v2 = patch.Vertices[i + 2];

                // Check current position
                var currInterval = GetTriangleXInterval(v0, v1, v2, currentCenter, cos, sin, halfWidth);
                if (currInterval.HasValue)
                    currentIntervals.Add(currInterval.Value);

                // Check look-on position
                var onInterval = GetTriangleXInterval(v0, v1, v2, lookOnCenter, cos, sin, halfWidth);
                if (onInterval.HasValue)
                    lookOnIntervals.Add(onInterval.Value);

                // Check look-off position
                var offInterval = GetTriangleXInterval(v0, v1, v2, lookOffCenter, cos, sin, halfWidth);
                if (offInterval.HasValue)
                    lookOffIntervals.Add(offInterval.Value);
            }
        }

        return (
            CalculateCoverageFromIntervals(currentIntervals, halfWidth),
            CalculateCoverageFromIntervals(lookOnIntervals, halfWidth),
            CalculateCoverageFromIntervals(lookOffIntervals, halfWidth)
        );
    }

    /// <summary>
    /// Get X interval where a triangle crosses Y=0 in local coordinates.
    /// </summary>
    private (double Start, double End)? GetTriangleXInterval(
        Vec3 v0, Vec3 v1, Vec3 v2,
        Vec2 center, double cos, double sin, double halfWidth)
    {
        // Transform to local coords
        var a = GeometryMath.ToLocalCoords(new Vec2(v0.Easting, v0.Northing), center, cos, sin);
        var b = GeometryMath.ToLocalCoords(new Vec2(v1.Easting, v1.Northing), center, cos, sin);
        var c = GeometryMath.ToLocalCoords(new Vec2(v2.Easting, v2.Northing), center, cos, sin);

        // Quick reject: all above or all below X-axis (Y=0)?
        if ((a.Northing > 0 && b.Northing > 0 && c.Northing > 0) ||
            (a.Northing < 0 && b.Northing < 0 && c.Northing < 0))
            return null;

        // Find X intercepts where edges cross Y=0
        var xIntercepts = new List<double>(6);

        var x1 = GeometryMath.GetXInterceptAtYZero(a, b);
        var x2 = GeometryMath.GetXInterceptAtYZero(b, c);
        var x3 = GeometryMath.GetXInterceptAtYZero(c, a);

        if (x1.HasValue) xIntercepts.Add(x1.Value);
        if (x2.HasValue) xIntercepts.Add(x2.Value);
        if (x3.HasValue) xIntercepts.Add(x3.Value);

        // Handle vertices exactly on the axis
        const double epsilon = 0.001;
        if (Math.Abs(a.Northing) < epsilon) xIntercepts.Add(a.Easting);
        if (Math.Abs(b.Northing) < epsilon) xIntercepts.Add(b.Easting);
        if (Math.Abs(c.Northing) < epsilon) xIntercepts.Add(c.Easting);

        if (xIntercepts.Count < 2)
            return null;

        double xMin = xIntercepts.Min();
        double xMax = xIntercepts.Max();

        // Clip to section bounds
        xMin = Math.Max(xMin, -halfWidth);
        xMax = Math.Min(xMax, halfWidth);

        if (xMax <= xMin)
            return null;

        return (xMin, xMax);
    }

    /// <summary>
    /// Calculate coverage result from X intervals.
    /// </summary>
    private CoverageResult CalculateCoverageFromIntervals(List<(double Start, double End)> intervals, double halfWidth)
    {
        if (intervals.Count == 0)
            return new CoverageResult(0, false, false, halfWidth * 2);

        // Merge overlapping intervals
        var merged = GeometryMath.MergeIntervals(intervals);

        // Calculate total coverage
        double totalWidth = halfWidth * 2;
        double coveredWidth = merged.Sum(i => i.End - i.Start);
        double coveragePercent = Math.Min(1.0, coveredWidth / totalWidth);

        return new CoverageResult(
            CoveragePercent: coveragePercent,
            HasAnyOverlap: coveredWidth > 0.001,
            IsFullyCovered: coveragePercent > 0.99,
            UncoveredLength: totalWidth - coveredWidth
        );
    }

    /// <summary>
    /// Quick bounding box check if patch is near a point (squared distance).
    /// </summary>
    private bool IsPatchNearPointSq(CoveragePatch patch, Vec2 point, double radiusSq)
    {
        // Calculate approximate center and check distance
        if (patch.Vertices.Count < 4) return false;

        // Use first and last geometry vertices to approximate center
        var first = patch.Vertices[1];
        var last = patch.Vertices[patch.Vertices.Count - 1];
        double centerE = (first.Easting + last.Easting) / 2;
        double centerN = (first.Northing + last.Northing) / 2;

        double dx = point.Easting - centerE;
        double dy = point.Northing - centerN;

        // Use larger radius to account for patch extent
        double patchExtentSq = GeometryMath.DistanceSquared(first, last) / 4;
        return (dx * dx + dy * dy) < (radiusSq + patchExtentSq);
    }

    /// <summary>
    /// Rebuild the entire spatial index from all patches.
    /// </summary>
    private void RebuildSpatialIndex()
    {
        _coverageSpatialIndex.Clear();
        _patchBounds.Clear();

        if (_patches.Count == 0)
        {
            _spatialIndexNeedsRebuild = false;
            return;
        }

        // First pass: calculate all bounds and find min coordinates for grid offset
        double globalMinE = double.MaxValue;
        double globalMinN = double.MaxValue;

        for (int p = 0; p < _patches.Count; p++)
        {
            var patch = _patches[p];
            if (patch.Vertices.Count < 4)
            {
                _patchBounds.Add((0, 0, 0, 0));
                continue;
            }

            double minE = double.MaxValue, maxE = double.MinValue;
            double minN = double.MaxValue, maxN = double.MinValue;

            // Skip color vertex at index 0
            for (int i = 1; i < patch.Vertices.Count; i++)
            {
                var v = patch.Vertices[i];
                if (v.Easting < minE) minE = v.Easting;
                if (v.Easting > maxE) maxE = v.Easting;
                if (v.Northing < minN) minN = v.Northing;
                if (v.Northing > maxN) maxN = v.Northing;
            }

            _patchBounds.Add((minE, maxE, minN, maxN));

            if (minE < globalMinE) globalMinE = minE;
            if (minN < globalMinN) globalMinN = minN;
        }

        // Calculate grid offset to handle negative coordinates
        _gridOffsetE = (int)Math.Floor(globalMinE / COVERAGE_GRID_CELL_SIZE);
        _gridOffsetN = (int)Math.Floor(globalMinN / COVERAGE_GRID_CELL_SIZE);

        // Second pass: add each patch to all grid cells it touches
        for (int p = 0; p < _patches.Count; p++)
        {
            var bounds = _patchBounds[p];
            if (bounds.MinE == 0 && bounds.MaxE == 0) continue; // Skip invalid patches

            int cellMinE = (int)Math.Floor(bounds.MinE / COVERAGE_GRID_CELL_SIZE) - _gridOffsetE;
            int cellMaxE = (int)Math.Floor(bounds.MaxE / COVERAGE_GRID_CELL_SIZE) - _gridOffsetE;
            int cellMinN = (int)Math.Floor(bounds.MinN / COVERAGE_GRID_CELL_SIZE) - _gridOffsetN;
            int cellMaxN = (int)Math.Floor(bounds.MaxN / COVERAGE_GRID_CELL_SIZE) - _gridOffsetN;

            for (int ce = cellMinE; ce <= cellMaxE; ce++)
            {
                for (int cn = cellMinN; cn <= cellMaxN; cn++)
                {
                    var key = (ce, cn);
                    if (!_coverageSpatialIndex.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        _coverageSpatialIndex[key] = list;
                    }
                    list.Add(p);
                }
            }
        }

        _spatialIndexNeedsRebuild = false;
    }

    /// <summary>
    /// Get patch indices near a point within the given radius.
    /// Always includes active patches (which may have grown beyond their indexed bounds).
    /// </summary>
    private IEnumerable<int> GetNearbyPatchIndices(double easting, double northing, double radius)
    {
        if (_spatialIndexNeedsRebuild)
            RebuildSpatialIndex();

        var seen = new HashSet<int>();

        // Always include active patches (they may have grown beyond indexed bounds)
        foreach (int idx in _activePatchIndices.Values)
        {
            if (seen.Add(idx))
                yield return idx;
        }

        if (_coverageSpatialIndex.Count == 0)
            yield break;

        // Find cells within radius
        int cellE = (int)Math.Floor(easting / COVERAGE_GRID_CELL_SIZE) - _gridOffsetE;
        int cellN = (int)Math.Floor(northing / COVERAGE_GRID_CELL_SIZE) - _gridOffsetN;
        int cellRadius = (int)Math.Ceiling(radius / COVERAGE_GRID_CELL_SIZE);

        for (int ce = cellE - cellRadius; ce <= cellE + cellRadius; ce++)
        {
            for (int cn = cellN - cellRadius; cn <= cellN + cellRadius; cn++)
            {
                if (_coverageSpatialIndex.TryGetValue((ce, cn), out var patchIndices))
                {
                    foreach (int idx in patchIndices)
                    {
                        if (seen.Add(idx)) // Only yield each patch once
                            yield return idx;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Mark spatial index as needing rebuild (call when patches change significantly).
    /// </summary>
    private void InvalidateSpatialIndex()
    {
        _spatialIndexNeedsRebuild = true;
    }

    /// <summary>
    /// Add a single patch to the spatial index incrementally.
    /// </summary>
    private void AddPatchToSpatialIndex(int patchIndex)
    {
        // If index needs rebuild, this patch will be included in the full rebuild
        // Also skip if index was never built (grid offsets not set)
        if (_spatialIndexNeedsRebuild || _coverageSpatialIndex.Count == 0) return;

        var patch = _patches[patchIndex];
        if (patch.Vertices.Count < 4) return;

        // Calculate bounds
        double minE = double.MaxValue, maxE = double.MinValue;
        double minN = double.MaxValue, maxN = double.MinValue;

        for (int i = 1; i < patch.Vertices.Count; i++)
        {
            var v = patch.Vertices[i];
            if (v.Easting < minE) minE = v.Easting;
            if (v.Easting > maxE) maxE = v.Easting;
            if (v.Northing < minN) minN = v.Northing;
            if (v.Northing > maxN) maxN = v.Northing;
        }

        // Ensure _patchBounds is large enough
        while (_patchBounds.Count <= patchIndex)
            _patchBounds.Add((0, 0, 0, 0));
        _patchBounds[patchIndex] = (minE, maxE, minN, maxN);

        // Add to spatial index cells
        int cellMinE = (int)Math.Floor(minE / COVERAGE_GRID_CELL_SIZE) - _gridOffsetE;
        int cellMaxE = (int)Math.Floor(maxE / COVERAGE_GRID_CELL_SIZE) - _gridOffsetE;
        int cellMinN = (int)Math.Floor(minN / COVERAGE_GRID_CELL_SIZE) - _gridOffsetN;
        int cellMaxN = (int)Math.Floor(maxN / COVERAGE_GRID_CELL_SIZE) - _gridOffsetN;

        for (int ce = cellMinE; ce <= cellMaxE; ce++)
        {
            for (int cn = cellMinN; cn <= cellMaxN; cn++)
            {
                var key = (ce, cn);
                if (!_coverageSpatialIndex.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    _coverageSpatialIndex[key] = list;
                }
                if (!list.Contains(patchIndex))
                    list.Add(patchIndex);
            }
        }
    }

    /// <summary>
    /// Check if a point is inside any triangle in a triangle strip.
    /// Vertices[0] is color, actual geometry starts at index 1.
    /// </summary>
    private bool IsPointInTriangleStrip(List<Vec3> vertices, double easting, double northing)
    {
        // Need at least 3 geometry vertices (indices 1,2,3) to form a triangle
        if (vertices.Count < 4) return false;

        // Check each triangle in the strip
        for (int i = 1; i < vertices.Count - 2; i++)
        {
            var v0 = vertices[i];
            var v1 = vertices[i + 1];
            var v2 = vertices[i + 2];

            if (IsPointInTriangle(easting, northing,
                v0.Easting, v0.Northing,
                v1.Easting, v1.Northing,
                v2.Easting, v2.Northing))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if point (px,py) is inside triangle (ax,ay)-(bx,by)-(cx,cy)
    /// using barycentric coordinate method.
    /// </summary>
    private bool IsPointInTriangle(double px, double py,
        double ax, double ay, double bx, double by, double cx, double cy)
    {
        // Compute vectors
        double v0x = cx - ax;
        double v0y = cy - ay;
        double v1x = bx - ax;
        double v1y = by - ay;
        double v2x = px - ax;
        double v2y = py - ay;

        // Compute dot products
        double dot00 = v0x * v0x + v0y * v0y;
        double dot01 = v0x * v1x + v0y * v1y;
        double dot02 = v0x * v2x + v0y * v2y;
        double dot11 = v1x * v1x + v1y * v1y;
        double dot12 = v1x * v2x + v1y * v2y;

        // Compute barycentric coordinates
        double denom = dot00 * dot11 - dot01 * dot01;
        if (Math.Abs(denom) < 1e-10) return false; // Degenerate triangle

        double invDenom = 1.0 / denom;
        double u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        double v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        // Check if point is in triangle (with small tolerance for edge cases)
        const double tolerance = 0.001;
        return (u >= -tolerance) && (v >= -tolerance) && (u + v <= 1.0 + tolerance);
    }

    public IReadOnlyList<CoveragePatch> GetPatches()
    {
        return _patches.AsReadOnly();
    }

    public IReadOnlyList<CoveragePatch> GetPatchesForZone(int zoneIndex)
    {
        return _patches.Where(p => p.ZoneIndex == zoneIndex).ToList().AsReadOnly();
    }

    public void ClearAll()
    {
        _patches.Clear();
        _activePatches.Clear();
        _activePatchIndices.Clear();
        _patchSaveList.Clear();
        _coverageSpatialIndex.Clear();
        _patchBounds.Clear();
        _spatialIndexNeedsRebuild = true;
        _totalWorkedArea = 0;
        _totalWorkedAreaUser = 0;

        CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
        {
            TotalArea = 0,
            PatchCount = 0,
            AreaAdded = 0
        });
    }

    public void ResetUserArea()
    {
        _totalWorkedAreaUser = 0;
    }

    public void SaveToFile(string fieldDirectory)
    {
        var filename = Path.Combine(fieldDirectory, "Sections.txt");

        // First, finalize any active patches (sections still painting when field closes)
        foreach (var kvp in _activePatches.ToList())
        {
            var patch = kvp.Value;
            patch.IsActive = false;
            if (patch.Vertices.Count > 4)
            {
                _patchSaveList.Add(patch);
            }
        }
        _activePatches.Clear();

        // Append new patches to file
        using var writer = new StreamWriter(filename, true);

        foreach (var patch in _patchSaveList)
        {
            if (patch.Vertices.Count < 4) continue;

            // Write vertex count
            writer.WriteLine(patch.Vertices.Count.ToString(CultureInfo.InvariantCulture));

            // Write vertices
            foreach (var v in patch.Vertices)
            {
                writer.WriteLine($"{v.Easting:F3},{v.Northing:F3},{v.Heading:F5}");
            }
        }

        _patchSaveList.Clear();
    }

    public void LoadFromFile(string fieldDirectory)
    {
        var path = Path.Combine(fieldDirectory, "Sections.txt");
        if (!File.Exists(path)) return;

        ClearAll();

        using var reader = new StreamReader(path);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (!int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int vertCount))
                continue;

            var patch = new CoveragePatch
            {
                ZoneIndex = 0,
                IsActive = false,
                Vertices = new List<Vec3>(vertCount)
            };

            // Read vertices
            for (int i = 0; i < vertCount && !reader.EndOfStream; i++)
            {
                var vertLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(vertLine)) continue;

                var parts = vertLine.Split(',');
                if (parts.Length >= 3 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double e) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double n) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
                {
                    patch.Vertices.Add(new Vec3(e, n, h));
                }
            }

            // First vertex is color
            if (patch.Vertices.Count > 0)
            {
                patch.Color = CoverageColor.FromVec3(patch.Vertices[0]);
            }

            // Calculate area for loaded patch
            if (patch.Vertices.Count >= 5)
            {
                var points = patch.Vertices.ToArray();
                for (int i = 4; i < points.Length; i += 2)
                {
                    if (i >= 4)
                    {
                        double area = workedAreaService.CalculateTriangleStripArea(points, i - 3);
                        _totalWorkedArea += area;
                    }
                }
            }

            if (patch.IsRenderable)
            {
                _patches.Add(patch);
            }
        }

        _totalWorkedAreaUser = _totalWorkedArea;

        // Rebuild spatial index for loaded patches
        InvalidateSpatialIndex();

        CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
        {
            TotalArea = _totalWorkedArea,
            PatchCount = _patches.Count,
            AreaAdded = 0
        });
    }

    /// <summary>
    /// Get color for a zone/section from configuration.
    /// Uses single color or per-section colors based on IsMultiColoredSections setting.
    /// </summary>
    private CoverageColor GetZoneColor(int zoneIndex)
    {
        var tool = ConfigurationStore.Instance.Tool;

        if (!tool.IsMultiColoredSections)
        {
            // Use single coverage color
            uint color = tool.SingleCoverageColor;
            return new CoverageColor(
                (byte)((color >> 16) & 0xFF),
                (byte)((color >> 8) & 0xFF),
                (byte)(color & 0xFF)
            );
        }

        // Use per-section color from configuration
        uint sectionColor = tool.GetSectionColor(zoneIndex);
        return new CoverageColor(
            (byte)((sectionColor >> 16) & 0xFF),
            (byte)((sectionColor >> 8) & 0xFF),
            (byte)(sectionColor & 0xFF)
        );
    }
}
