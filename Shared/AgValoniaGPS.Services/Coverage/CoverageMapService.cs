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
using System.IO;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Coverage;

/// <summary>
/// Tracks and manages coverage (worked area) as the tool moves across the field.
///
/// Architecture:
/// - DETECTION LAYER: Bit array with 0.1m cells for O(1) coverage detection (~65MB for 520ha)
/// - DISPLAY LAYER: WriteableBitmap rendered from bit array (handled by DrawingContextMapControl)
///
/// This replaces the old patch-based system which stored 50,000+ triangle strips
/// and iterated through them for coverage detection (280-330ms per check).
/// The new bitmap approach provides coverage detection in ~0.04ms.
/// </summary>
public class CoverageMapService : ICoverageMapService
{
    // ========== UNIFIED BITMAP LAYER ==========
    // WriteableBitmap owned by map control is the single source of truth
    // Service accesses it via callbacks - no separate storage here
    private const double BITMAP_CELL_SIZE = 0.1; // meters per cell (10cm = ~4in resolution)

    // Pixel access callbacks (provided by map control)
    private Func<int, int, ushort>? _getPixel;      // (localX, localY) -> rgb565
    private Action<int, int, ushort>? _setPixel;    // (localX, localY, rgb565) -> void
    private Action? _clearAllPixels;

    // Bitmap dimensions (set when field bounds are established)
    private int _bitmapWidth;   // Number of cells in E direction
    private int _bitmapHeight;  // Number of cells in N direction
    private int _bitmapOriginE; // Cell coordinate of bitmap origin (E)
    private int _bitmapOriginN; // Cell coordinate of bitmap origin (N)

    // Per-zone cell counters for acreage calculation (zone index -> cell count)
    private readonly Dictionary<int, long> _cellCountPerZone = new();

    // Bit array for fast detection - 1 bit per cell, fixed size regardless of coverage
    // 582ha @ 0.1m = 582M cells / 8 = 72MB (much better than HashSet at high coverage)
    private byte[]? _detectionBits;

    // Track newly added cells since last GetNewCoverageBitmapCells call
    // Still use HashSet for new cells (small, cleared frequently)
    private readonly HashSet<(int CellE, int CellN, int Zone)> _newCells = new();

    // Reusable buffers for GetNewCoverageBitmapCells to avoid allocations
    private readonly List<(int CellX, int CellY, CoverageColor Color)> _newCellsResult = new();
    private readonly HashSet<(int, int)> _newCellsDedup = new();

    // Track bounds of coverage for reporting
    private int _minCellE = int.MaxValue;
    private int _maxCellE = int.MinValue;
    private int _minCellN = int.MaxValue;
    private int _maxCellN = int.MinValue;
    private bool _boundsValid;

    // Fixed field bounds for stable bitmap coordinates (set when field is loaded)
    private double _fieldMinE;
    private double _fieldMaxE;
    private double _fieldMinN;
    private double _fieldMaxN;
    private bool _fieldBoundsSet;

    // ========== TRACKING STATE ==========
    // Track which sections are actively mapping
    private readonly HashSet<int> _activeSections = new();

    // Track last edges per section for area calculation and bitmap rasterization
    private readonly Dictionary<int, ((double E, double N) Left, (double E, double N) Right)> _lastEdgesPerSection = new();

    // Area totals (calculated incrementally)
    private double _totalWorkedArea;
    private double _totalWorkedAreaUser;

    // Dirty flag to track if coverage has changed since last flush
    private bool _coverageDirty;
    private double _pendingAreaAdded;

    public double TotalWorkedArea => _totalWorkedArea;
    public double TotalWorkedAreaUser => _totalWorkedAreaUser;
    public int PatchCount => (int)GetTotalCellCount(); // Total covered cells across all zones
    public bool IsAnyZoneMapping => _activeSections.Count > 0;
    public int ActiveSectionCount => _activeSections.Count;

    public event EventHandler<CoverageUpdatedEventArgs>? CoverageUpdated;

    // Callbacks for save/load operations on the bitmap buffer
    public Func<ushort[]?>? GetPixelBufferCallback { get; set; }
    public Action<ushort[]>? SetPixelBufferCallback { get; set; }

    // Expose bitmap dimensions for coordinate calculations
    public (int Width, int Height, int OriginE, int OriginN)? BitmapDimensions =>
        _fieldBoundsSet ? (_bitmapWidth, _bitmapHeight, _bitmapOriginE, _bitmapOriginN) : null;

    /// <summary>
    /// Set pixel access callbacks for unified WriteableBitmap storage.
    /// </summary>
    public void SetPixelAccessCallbacks(
        Func<int, int, ushort>? getPixel,
        Action<int, int, ushort>? setPixel,
        Action? clearAll)
    {
        _getPixel = getPixel;
        _setPixel = setPixel;
        _clearAllPixels = clearAll;
    }

    public void StartMapping(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge, CoverageColor? color = null)
    {
        // If already mapping this zone, just continue
        if (_activeSections.Contains(zoneIndex))
            return;

        _activeSections.Add(zoneIndex);

        // Store initial edge for area calculation and bitmap rasterization
        _lastEdgesPerSection[zoneIndex] = (
            (leftEdge.Easting, leftEdge.Northing),
            (rightEdge.Easting, rightEdge.Northing));
    }

    public void StopMapping(int zoneIndex)
    {
        if (!_activeSections.Contains(zoneIndex))
            return;

        _activeSections.Remove(zoneIndex);
        _lastEdgesPerSection.Remove(zoneIndex);
    }

    public void AddCoveragePoint(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge)
    {
        if (!_activeSections.Contains(zoneIndex))
            return;

        // Get last edges for this section (used for bitmap rasterization and area calc)
        if (!_lastEdgesPerSection.TryGetValue(zoneIndex, out var lastEdges))
        {
            // First point - just store edges
            _lastEdgesPerSection[zoneIndex] = (
                (leftEdge.Easting, leftEdge.Northing),
                (rightEdge.Easting, rightEdge.Northing));
            return;
        }

        // Rasterize the quad to the coverage bitmap for O(1) detection
        RasterizeQuadToBitmap(zoneIndex, leftEdge, rightEdge);

        // Calculate area of the quad (two triangles)
        double area = CalculateQuadArea(
            lastEdges.Left, lastEdges.Right,
            (rightEdge.Easting, rightEdge.Northing),
            (leftEdge.Easting, leftEdge.Northing));

        _totalWorkedArea += area;
        _totalWorkedAreaUser += area;
        _pendingAreaAdded += area;
        _coverageDirty = true;

        // Update last edges for next quad
        _lastEdgesPerSection[zoneIndex] = (
            (leftEdge.Easting, leftEdge.Northing),
            (rightEdge.Easting, rightEdge.Northing));
    }

    /// <summary>
    /// Calculate the area of a quad using the shoelace formula.
    /// Points are in order: p0 -> p1 -> p2 -> p3 -> back to p0
    /// </summary>
    private static double CalculateQuadArea(
        (double E, double N) p0, (double E, double N) p1,
        (double E, double N) p2, (double E, double N) p3)
    {
        // Shoelace formula for quadrilateral area
        double area = Math.Abs(
            (p0.E * p1.N - p1.E * p0.N) +
            (p1.E * p2.N - p2.E * p1.N) +
            (p2.E * p3.N - p3.E * p2.N) +
            (p3.E * p0.N - p0.E * p3.N)) / 2.0;
        return area;
    }


    /// <summary>
    /// Rasterize a quad (from previous to current edges) to the coverage bitmap.
    /// This provides O(1) coverage lookup similar to AgOpenGPS GPU pixel readback.
    /// </summary>
    private void RasterizeQuadToBitmap(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge)
    {
        var currLeft = (E: leftEdge.Easting, N: leftEdge.Northing);
        var currRight = (E: rightEdge.Easting, N: rightEdge.Northing);

        // Need previous edges to form a quad
        if (!_lastEdgesPerSection.TryGetValue(zoneIndex, out var lastEdges))
        {
            _lastEdgesPerSection[zoneIndex] = (currLeft, currRight);
            return;
        }

        // Form quad: prevLeft -> prevRight -> currRight -> currLeft
        var p0 = lastEdges.Left;
        var p1 = lastEdges.Right;
        var p2 = currRight;
        var p3 = currLeft;

        // Find bounding box
        double minE = Math.Min(Math.Min(p0.E, p1.E), Math.Min(p2.E, p3.E));
        double maxE = Math.Max(Math.Max(p0.E, p1.E), Math.Max(p2.E, p3.E));
        double minN = Math.Min(Math.Min(p0.N, p1.N), Math.Min(p2.N, p3.N));
        double maxN = Math.Max(Math.Max(p0.N, p1.N), Math.Max(p2.N, p3.N));

        // Convert to cell coordinates
        int cellMinE = (int)Math.Floor(minE / BITMAP_CELL_SIZE);
        int cellMaxE = (int)Math.Floor(maxE / BITMAP_CELL_SIZE);
        int cellMinN = (int)Math.Floor(minN / BITMAP_CELL_SIZE);
        int cellMaxN = (int)Math.Floor(maxN / BITMAP_CELL_SIZE);

        // Mark all cells in bounding box that are inside the quad
        for (int ce = cellMinE; ce <= cellMaxE; ce++)
        {
            for (int cn = cellMinN; cn <= cellMaxN; cn++)
            {
                // Cell center
                double cellCenterE = (ce + 0.5) * BITMAP_CELL_SIZE;
                double cellCenterN = (cn + 0.5) * BITMAP_CELL_SIZE;

                // Check if cell center is inside quad (point-in-polygon test)
                if (IsPointInQuad(cellCenterE, cellCenterN, p0, p1, p2, p3))
                {
                    if (MarkCellCovered(ce, cn, zoneIndex))
                    {
                        // New cell - track it for incremental display update
                        _newCells.Add((ce, cn, zoneIndex));
                        UpdateBounds(ce, cn);
                    }
                }
            }
        }

        // Update last edges for next quad
        _lastEdgesPerSection[zoneIndex] = (currLeft, currRight);
    }

    /// <summary>
    /// Check if a point is inside a quad (4-point polygon).
    /// Uses cross product sign test - point is inside if all cross products have same sign.
    /// </summary>
    private static bool IsPointInQuad(double px, double py,
        (double E, double N) p0, (double E, double N) p1,
        (double E, double N) p2, (double E, double N) p3)
    {
        // Check each edge - point should be on same side of all edges
        double d0 = CrossProductSign(px, py, p0.E, p0.N, p1.E, p1.N);
        double d1 = CrossProductSign(px, py, p1.E, p1.N, p2.E, p2.N);
        double d2 = CrossProductSign(px, py, p2.E, p2.N, p3.E, p3.N);
        double d3 = CrossProductSign(px, py, p3.E, p3.N, p0.E, p0.N);

        bool hasNeg = (d0 < 0) || (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d0 > 0) || (d1 > 0) || (d2 > 0) || (d3 > 0);

        // Inside if all same sign (all positive or all negative)
        return !(hasNeg && hasPos);
    }

    /// <summary>
    /// Cross product sign for point vs edge.
    /// </summary>
    private static double CrossProductSign(double px, double py,
        double x1, double y1, double x2, double y2)
    {
        return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
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
            PatchCount = (int)GetTotalCellCount(),
            AreaAdded = _pendingAreaAdded
        });

        _coverageDirty = false;
        _pendingAreaAdded = 0;
    }

    public bool IsZoneMapping(int zoneIndex)
    {
        return _activeSections.Contains(zoneIndex);
    }

    public bool IsPointCovered(double easting, double northing)
    {
        // O(1) bit array lookup - convert to cell coordinates and check if covered
        int cellE = (int)Math.Floor(easting / BITMAP_CELL_SIZE);
        int cellN = (int)Math.Floor(northing / BITMAP_CELL_SIZE);
        return IsCellCovered(cellE, cellN);
    }

    public CoverageResult GetSegmentCoverage(Vec2 sectionCenter, double heading, double halfWidth, double lookAheadDistance = 0)
    {
        // Adjust center for look-ahead
        Vec2 checkCenter = lookAheadDistance == 0
            ? sectionCenter
            : new Vec2(
                sectionCenter.Easting + Math.Sin(heading) * lookAheadDistance,
                sectionCenter.Northing + Math.Cos(heading) * lookAheadDistance);

        return GetSegmentCoverageBitmap(checkCenter, heading, halfWidth);
    }

    /// <summary>
    /// Check segment coverage using bitmap - O(width/cellSize) lookups.
    /// Sample points along the section width perpendicular to heading.
    /// </summary>
    private CoverageResult GetSegmentCoverageBitmap(Vec2 center, double heading, double halfWidth)
    {
        // Perpendicular direction (90 degrees to heading)
        double perpSin = Math.Cos(heading);  // sin(heading + 90) = cos(heading)
        double perpCos = -Math.Sin(heading); // cos(heading + 90) = -sin(heading)

        // Sample points along the section width at cell-size intervals
        int numSamples = Math.Max(3, (int)Math.Ceiling(halfWidth * 2 / BITMAP_CELL_SIZE));
        double step = halfWidth * 2 / (numSamples - 1);

        int coveredCount = 0;
        for (int i = 0; i < numSamples; i++)
        {
            double offset = -halfWidth + i * step;
            double sampleE = center.Easting + perpSin * offset;
            double sampleN = center.Northing + perpCos * offset;

            int cellE = (int)Math.Floor(sampleE / BITMAP_CELL_SIZE);
            int cellN = (int)Math.Floor(sampleN / BITMAP_CELL_SIZE);

            if (IsCellCovered(cellE, cellN))
                coveredCount++;
        }

        double coveragePercent = (double)coveredCount / numSamples;
        double uncoveredLength = (numSamples - coveredCount) * (halfWidth * 2 / numSamples);
        return new CoverageResult(
            coveragePercent,
            coveredCount > 0,           // HasAnyOverlap
            coveragePercent >= 0.95,    // IsFullyCovered (95%+ threshold)
            uncoveredLength);
    }

    public (CoverageResult Current, CoverageResult LookOn, CoverageResult LookOff) GetSegmentCoverageMulti(
        Vec2 sectionCenter, double heading, double halfWidth,
        double lookOnDistance, double lookOffDistance)
    {
        // Calculate all three check centers
        Vec2 currentCenter = sectionCenter;
        Vec2 lookOnCenter = new Vec2(
            sectionCenter.Easting + Math.Sin(heading) * lookOnDistance,
            sectionCenter.Northing + Math.Cos(heading) * lookOnDistance);
        Vec2 lookOffCenter = new Vec2(
            sectionCenter.Easting + Math.Sin(heading) * lookOffDistance,
            sectionCenter.Northing + Math.Cos(heading) * lookOffDistance);

        // Use bitmap-based coverage detection - O(width/cellSize) per position
        return (
            GetSegmentCoverageBitmap(currentCenter, heading, halfWidth),
            GetSegmentCoverageBitmap(lookOnCenter, heading, halfWidth),
            GetSegmentCoverageBitmap(lookOffCenter, heading, halfWidth)
        );
    }

    /// <summary>
    /// Update coverage bounds when a new cell is added.
    /// </summary>
    private void UpdateBounds(int cellE, int cellN)
    {
        if (cellE < _minCellE) _minCellE = cellE;
        if (cellE > _maxCellE) _maxCellE = cellE;
        if (cellN < _minCellN) _minCellN = cellN;
        if (cellN > _maxCellN) _maxCellN = cellN;
        _boundsValid = true;
    }

    /// <summary>
    /// Mark a cell as covered. Returns true if cell was newly covered.
    /// Uses bit array for fast detection, batched write via _newCells for rendering.
    /// </summary>
    private bool MarkCellCovered(int cellE, int cellN, int zone)
    {
        if (_detectionBits == null || !_fieldBoundsSet)
            return false;

        // Convert to local coordinates
        int localE = cellE - _bitmapOriginE;
        int localN = cellN - _bitmapOriginN;

        // Bounds check
        if (localE < 0 || localE >= _bitmapWidth || localN < 0 || localN >= _bitmapHeight)
            return false;

        // Calculate bit position in detection array
        long bitIndex = (long)localN * _bitmapWidth + localE;
        int byteIndex = (int)(bitIndex / 8);
        int bitOffset = (int)(bitIndex % 8);
        byte mask = (byte)(1 << bitOffset);

        // Check if already covered using bit array (O(1), no bitmap lock)
        if ((_detectionBits[byteIndex] & mask) != 0)
            return false; // Already covered

        // Mark as covered in detection array
        _detectionBits[byteIndex] |= mask;

        // Track for batched write by map control (via GetNewCoverageBitmapCells)
        _newCells.Add((cellE, cellN, zone));

        // Update per-zone counter
        if (!_cellCountPerZone.TryGetValue(zone, out long count))
            count = 0;
        _cellCountPerZone[zone] = count + 1;

        return true;
    }

    /// <summary>
    /// Check if a cell is covered.
    /// </summary>
    private bool IsCellCovered(int cellE, int cellN)
    {
        if (_detectionBits == null || !_fieldBoundsSet)
            return false;

        // Convert to local coordinates
        int localE = cellE - _bitmapOriginE;
        int localN = cellN - _bitmapOriginN;

        // Bounds check
        if (localE < 0 || localE >= _bitmapWidth || localN < 0 || localN >= _bitmapHeight)
            return false;

        // Calculate bit position
        long bitIndex = (long)localN * _bitmapWidth + localE;
        int byteIndex = (int)(bitIndex / 8);
        int bitOffset = (int)(bitIndex % 8);
        byte mask = (byte)(1 << bitOffset);

        return (_detectionBits[byteIndex] & mask) != 0;
    }

    /// <summary>
    /// Get area covered by a specific zone in hectares.
    /// </summary>
    public double GetZoneArea(int zone)
    {
        if (!_cellCountPerZone.TryGetValue(zone, out long count))
            return 0;
        // Each cell is BITMAP_CELL_SIZE x BITMAP_CELL_SIZE meters
        double cellAreaM2 = BITMAP_CELL_SIZE * BITMAP_CELL_SIZE;
        return count * cellAreaM2 / 10000.0; // Convert to hectares
    }

    /// <summary>
    /// Get total cell count across all zones (for statistics).
    /// </summary>
    public long GetTotalCellCount()
    {
        long total = 0;
        foreach (var count in _cellCountPerZone.Values)
            total += count;
        return total;
    }


    /// <summary>
    /// Get coverage bitmap bounds in world coordinates.
    /// Returns fixed field bounds if set, otherwise coverage bounds.
    /// Returns null if no bounds available.
    /// </summary>
    public (double MinE, double MaxE, double MinN, double MaxN)? GetCoverageBounds()
    {
        // Return null if no coverage exists (even if field bounds are set)
        if (GetTotalCellCount() == 0)
            return null;

        // Use fixed field bounds if set (stable coordinate system)
        if (_fieldBoundsSet)
            return (_fieldMinE, _fieldMaxE, _fieldMinN, _fieldMaxN);

        // Fall back to coverage bounds (dynamic, can drift)
        if (!_boundsValid)
            return null;

        // Convert cell coordinates to world coordinates
        // Cell (x,y) covers from x*cellSize to (x+1)*cellSize
        double minE = _minCellE * BITMAP_CELL_SIZE;
        double maxE = (_maxCellE + 1) * BITMAP_CELL_SIZE;
        double minN = _minCellN * BITMAP_CELL_SIZE;
        double maxN = (_maxCellN + 1) * BITMAP_CELL_SIZE;

        return (minE, maxE, minN, maxN);
    }

    /// <summary>
    /// Get coverage cells within viewport bounds for bitmap rendering.
    /// Only iterates cells within the specified world coordinate bounds.
    /// Time complexity: O(viewport area), not O(total coverage).
    /// </summary>
    public IEnumerable<(int CellX, int CellY, CoverageColor Color)> GetCoverageBitmapCells(
        double cellSize, double viewMinE, double viewMaxE, double viewMinN, double viewMaxN)
    {
        if (_getPixel == null || GetTotalCellCount() == 0)
            yield break;

        // Determine origin for coordinate calculations
        double originE, originN;
        if (_fieldBoundsSet)
        {
            originE = _fieldMinE;
            originN = _fieldMinN;
        }
        else
        {
            if (!_boundsValid) yield break;
            originE = _minCellE * BITMAP_CELL_SIZE;
            originN = _minCellN * BITMAP_CELL_SIZE;
        }

        // Default color for legacy compatibility (actual colors are in the bitmap)
        var defaultColor = GetZoneColor(0);

        // Convert viewport bounds to internal cell coordinates
        int internalMinCellE = (int)Math.Floor(viewMinE / BITMAP_CELL_SIZE);
        int internalMaxCellE = (int)Math.Ceiling(viewMaxE / BITMAP_CELL_SIZE);
        int internalMinCellN = (int)Math.Floor(viewMinN / BITMAP_CELL_SIZE);
        int internalMaxCellN = (int)Math.Ceiling(viewMaxN / BITMAP_CELL_SIZE);

        // Track output cells to avoid duplicates when downsampling
        var outputCells = new HashSet<(int, int)>();

        // Iterate only over cells within viewport bounds - O(viewport) not O(coverage)
        for (int cellE = internalMinCellE; cellE <= internalMaxCellE; cellE++)
        {
            for (int cellN = internalMinCellN; cellN <= internalMaxCellN; cellN++)
            {
                // O(1) HashSet lookup
                if (IsCellCovered(cellE, cellN))
                {
                    // Convert to output cell coordinates
                    double worldE = (cellE + 0.5) * BITMAP_CELL_SIZE;
                    double worldN = (cellN + 0.5) * BITMAP_CELL_SIZE;
                    int outCellX = (int)Math.Floor((worldE - originE) / cellSize);
                    int outCellY = (int)Math.Floor((worldN - originN) / cellSize);

                    if (outputCells.Add((outCellX, outCellY)))
                    {
                        yield return (outCellX, outCellY, defaultColor);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get newly added coverage cells since last call.
    /// Clears the pending list after returning.
    /// Uses fixed field bounds if set, otherwise coverage bounds.
    /// </summary>
    public IEnumerable<(int CellX, int CellY, CoverageColor Color)> GetNewCoverageBitmapCells(double cellSize)
    {
        if (_newCells.Count == 0)
            return Array.Empty<(int, int, CoverageColor)>();

        // Determine origin for coordinate calculations
        double minE, minN;
        if (_fieldBoundsSet)
        {
            // Use fixed field bounds (stable coordinate system)
            minE = _fieldMinE;
            minN = _fieldMinN;
        }
        else
        {
            // Fall back to coverage bounds (can drift)
            if (!_boundsValid)
            {
                _newCells.Clear();
                return Array.Empty<(int, int, CoverageColor)>();
            }
            minE = _minCellE * BITMAP_CELL_SIZE;
            minN = _minCellN * BITMAP_CELL_SIZE;
        }

        // Reuse buffers to avoid allocations (clear first)
        _newCellsDedup.Clear();
        _newCellsResult.Clear();

        // Convert each new cell to the requested cell size
        foreach (var (cellE, cellN, zone) in _newCells)
        {
            double worldE = (cellE + 0.5) * BITMAP_CELL_SIZE;
            double worldN = (cellN + 0.5) * BITMAP_CELL_SIZE;

            int outCellX = (int)Math.Floor((worldE - minE) / cellSize);
            int outCellY = (int)Math.Floor((worldN - minN) / cellSize);

            if (_newCellsDedup.Add((outCellX, outCellY)))
            {
                var color = GetZoneColor(zone);
                _newCellsResult.Add((outCellX, outCellY, color));
            }
        }

        // Clear source cells
        _newCells.Clear();

        return _newCellsResult;
    }

    public IReadOnlyList<CoveragePatch> GetPatches()
    {
        // Legacy compatibility - patches no longer used, return empty list
        return Array.Empty<CoveragePatch>();
    }

    public IReadOnlyList<CoveragePatch> GetPatchesForZone(int zoneIndex)
    {
        // Legacy compatibility - patches no longer used, return empty list
        return Array.Empty<CoveragePatch>();
    }

    public void ClearAll()
    {
        // Clear bitmap via callback
        _clearAllPixels?.Invoke();
        _newCells.Clear();
        if (_detectionBits != null)
            Array.Clear(_detectionBits, 0, _detectionBits.Length);
        _cellCountPerZone.Clear();

        // Reset bounds
        _minCellE = int.MaxValue;
        _maxCellE = int.MinValue;
        _minCellN = int.MaxValue;
        _maxCellN = int.MinValue;
        _boundsValid = false;

        // Clear tracking state
        _activeSections.Clear();
        _lastEdgesPerSection.Clear();

        // Reset totals
        _totalWorkedArea = 0;
        _totalWorkedAreaUser = 0;
        _coverageDirty = false;
        _pendingAreaAdded = 0;

        CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
        {
            TotalArea = 0,
            PatchCount = 0,
            AreaAdded = 0
        });
    }

    /// <summary>
    /// Set fixed field bounds for stable bitmap coordinate calculations.
    /// Allocates the bit array for memory-efficient coverage detection.
    /// </summary>
    public void SetFieldBounds(double minE, double maxE, double minN, double maxN)
    {
        // Skip if bounds unchanged
        if (_fieldBoundsSet &&
            Math.Abs(_fieldMinE - minE) < 0.01 &&
            Math.Abs(_fieldMaxE - maxE) < 0.01 &&
            Math.Abs(_fieldMinN - minN) < 0.01 &&
            Math.Abs(_fieldMaxN - maxN) < 0.01)
        {
            Console.WriteLine($"[Coverage] SetFieldBounds: bounds unchanged");
            return;
        }

        _fieldMinE = minE;
        _fieldMaxE = maxE;
        _fieldMinN = minN;
        _fieldMaxN = maxN;
        _fieldBoundsSet = true;

        // Calculate bitmap dimensions (actual allocation done by map control)
        _bitmapOriginE = (int)Math.Floor(minE / BITMAP_CELL_SIZE);
        _bitmapOriginN = (int)Math.Floor(minN / BITMAP_CELL_SIZE);
        int maxCellE = (int)Math.Ceiling(maxE / BITMAP_CELL_SIZE);
        int maxCellN = (int)Math.Ceiling(maxN / BITMAP_CELL_SIZE);
        _bitmapWidth = maxCellE - _bitmapOriginE + 1;
        _bitmapHeight = maxCellN - _bitmapOriginN + 1;

        long totalPixels = (long)_bitmapWidth * _bitmapHeight;

        // Allocate bit array for detection: 1 bit per cell
        long totalBits = totalPixels;
        long totalBytes = (totalBits + 7) / 8;
        _detectionBits = new byte[totalBytes];

        double areaMSq = (maxE - minE) * (maxN - minN);
        double areaHa = areaMSq / 10000.0;
        double bitmapMB = totalPixels * 2 / (1024.0 * 1024.0); // 2 bytes per pixel (Rgb565)
        double detectionMB = totalBytes / (1024.0 * 1024.0);
        Console.WriteLine($"[Coverage] Field bounds set: E[{minE:F1}, {maxE:F1}] N[{minN:F1}, {maxN:F1}]");
        Console.WriteLine($"[Coverage] {_bitmapWidth}x{_bitmapHeight} = {totalPixels:N0} cells, detection={detectionMB:F1}MB, bitmap=~{bitmapMB:F0}MB for {areaHa:F0}ha");
    }

    /// <summary>
    /// Clear field bounds (when field is closed).
    /// </summary>
    public void ClearFieldBounds()
    {
        _fieldBoundsSet = false;
        _bitmapWidth = 0;
        _bitmapHeight = 0;
        _cellCountPerZone.Clear();
        _detectionBits = null;
        _clearAllPixels?.Invoke();
        Console.WriteLine("[Coverage] Field bounds cleared");
    }

    public void ResetUserArea()
    {
        _totalWorkedAreaUser = 0;
    }

    public void SaveToFile(string fieldDirectory)
    {
        if (!_fieldBoundsSet || GetPixelBufferCallback == null)
            return;

        var pixels = GetPixelBufferCallback();
        if (pixels == null || pixels.Length == 0)
            return;

        var filename = Path.Combine(fieldDirectory, "Coverage.bin");

        using var stream = new FileStream(filename, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        // Write header - COV2 format (Rgb565 pixels)
        writer.Write("COV2".ToCharArray()); // Magic + version
        writer.Write(_fieldMinE);
        writer.Write(_fieldMaxE);
        writer.Write(_fieldMinN);
        writer.Write(_fieldMaxN);
        writer.Write(BITMAP_CELL_SIZE);
        writer.Write(_bitmapWidth);
        writer.Write(_bitmapHeight);
        writer.Write(_totalWorkedArea);

        // Write pixel array with RLE compression (ushort values)
        // Format: [count:ushort][value:ushort] pairs
        // Max run length is 65535
        long i = 0;
        long compressedSize = 0;
        while (i < pixels.Length)
        {
            ushort value = pixels[i];
            int runLength = 1;
            while (i + runLength < pixels.Length &&
                   pixels[i + runLength] == value &&
                   runLength < 65535)
            {
                runLength++;
            }
            writer.Write((ushort)runLength);
            writer.Write(value);
            compressedSize += 4;
            i += runLength;
        }

        Console.WriteLine($"[Coverage] Saved {pixels.Length} pixels ({pixels.Length * 2 / 1024 / 1024}MB) -> {compressedSize / 1024}KB compressed to {filename}");
    }

    public void LoadFromFile(string fieldDirectory)
    {
        var path = Path.Combine(fieldDirectory, "Coverage.bin");
        if (!File.Exists(path)) return;

        if (SetPixelBufferCallback == null)
        {
            Console.WriteLine("[Coverage] LoadFromFile: SetPixelBufferCallback not set");
            return;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open);
            using var reader = new BinaryReader(stream);

            // Read and verify header
            var magic = new string(reader.ReadChars(4));

            double minE = reader.ReadDouble();
            double maxE = reader.ReadDouble();
            double minN = reader.ReadDouble();
            double maxN = reader.ReadDouble();
            double cellSize = reader.ReadDouble();
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            double area = reader.ReadDouble();

            Console.WriteLine($"[Coverage] File header ({magic}): E[{minE:F1}, {maxE:F1}] N[{minN:F1}, {maxN:F1}] cellSize={cellSize} {width}x{height} area={area:F2}m²");

            // Verify cell size matches
            if (Math.Abs(cellSize - BITMAP_CELL_SIZE) > 0.001)
            {
                Console.WriteLine($"[Coverage] Cell size mismatch: file={cellSize}, expected={BITMAP_CELL_SIZE}");
                return;
            }

            // Set field bounds (sets dimensions, map control allocates bitmap)
            SetFieldBounds(minE, maxE, minN, maxN);

            // Allocate pixel buffer for loading
            long totalPixels = (long)width * height;
            var pixels = new ushort[totalPixels];

            if (magic == "COV2")
            {
                // COV2 format: RLE-compressed Rgb565 pixels
                long i = 0;
                long nonZeroPixels = 0;
                while (i < pixels.Length && stream.Position < stream.Length)
                {
                    ushort runLength = reader.ReadUInt16();
                    ushort value = reader.ReadUInt16();
                    if (value != 0) nonZeroPixels += runLength;
                    for (int j = 0; j < runLength && i < pixels.Length; j++, i++)
                    {
                        pixels[i] = value;
                    }
                }
                Console.WriteLine($"[Coverage] COV2 RLE: read {i:N0} pixels, {nonZeroPixels:N0} non-zero");
                _cellCountPerZone[0] = nonZeroPixels;
            }
            else if (magic == "COV1")
            {
                // Legacy COV1 format: RLE-compressed bit array - convert to green pixels
                // Each bit becomes a pixel (0 or default green color)
                ushort greenRgb565 = (ushort)(((0 >> 3) << 11) | ((255 >> 2) << 5) | (0 >> 3)); // Pure green
                long bitIndex = 0;
                long nonZeroBits = 0;
                long totalBits = (long)width * height;
                long totalBytes = (totalBits + 7) / 8;

                while (stream.Position < stream.Length && bitIndex < totalBits)
                {
                    byte runLength = reader.ReadByte();
                    byte value = reader.ReadByte();

                    for (int j = 0; j < runLength && bitIndex / 8 < totalBytes; j++)
                    {
                        // Expand byte to 8 pixels
                        for (int bit = 0; bit < 8 && bitIndex < totalBits; bit++, bitIndex++)
                        {
                            if ((value & (1 << bit)) != 0)
                            {
                                pixels[bitIndex] = greenRgb565;
                                nonZeroBits++;
                            }
                        }
                    }
                }
                Console.WriteLine($"[Coverage] COV1 legacy: converted {nonZeroBits:N0} bits to green pixels");
                _cellCountPerZone[0] = nonZeroBits;
            }
            else
            {
                Console.WriteLine($"[Coverage] Unknown file format: {magic}");
                return;
            }

            // Pass pixel buffer to map control
            SetPixelBufferCallback(pixels);

            // Populate detection bit array from loaded pixels
            if (_detectionBits != null)
            {
                Array.Clear(_detectionBits, 0, _detectionBits.Length);
                long coveredCount = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        long idx = (long)y * width + x;
                        if (pixels[idx] != 0)
                        {
                            // Set bit in detection array
                            long bitIndex = (long)y * width + x;
                            int byteIndex = (int)(bitIndex / 8);
                            int bitOffset = (int)(bitIndex % 8);
                            _detectionBits[byteIndex] |= (byte)(1 << bitOffset);
                            coveredCount++;
                        }
                    }
                }
                Console.WriteLine($"[Coverage] Populated detection bits with {coveredCount:N0} cells");
            }

            _totalWorkedArea = area;
            _totalWorkedAreaUser = area;

            // Update bounds from loaded data
            long cellCount = _cellCountPerZone.GetValueOrDefault(0, 0);
            _boundsValid = cellCount > 0;
            if (_boundsValid)
            {
                _minCellE = _bitmapOriginE;
                _maxCellE = _bitmapOriginE + _bitmapWidth - 1;
                _minCellN = _bitmapOriginN;
                _maxCellN = _bitmapOriginN + _bitmapHeight - 1;
            }

            Console.WriteLine($"[Coverage] Loaded {cellCount:N0} cells, {area:F2} m² from {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Coverage] Failed to load: {ex.Message}");
        }

        Console.WriteLine($"[Coverage] Firing CoverageUpdated with IsFullReload=true");
        CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
        {
            TotalArea = _totalWorkedArea,
            PatchCount = (int)GetTotalCellCount(),
            AreaAdded = 0,
            IsFullReload = true
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
