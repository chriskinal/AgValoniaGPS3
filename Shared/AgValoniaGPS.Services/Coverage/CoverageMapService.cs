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
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Coverage;

/// <summary>
/// Tracks and manages coverage (worked area) as the tool moves across the field.
///
/// Architecture (dual-layer design):
/// - VISUAL LAYER: One StreamGeometry polygon per section (16 max) for efficient rendering
/// - DETECTION LAYER: HashSet bitmap with 0.5m cells for O(1) coverage detection
///
/// This replaces the old patch-based system which stored 50,000+ triangle strips
/// and iterated through them for coverage detection (280-330ms per check).
/// The new bitmap approach provides coverage detection in ~0.04ms.
/// </summary>
public class CoverageMapService(IWorkedAreaService workedAreaService) : ICoverageMapService
{
    // ========== VISUAL LAYER ==========
    // Visual polygons per section - multiple passes per section (one polygon per pass)
    // Each pass tracks left/right edge points to form a filled polygon
    // When sections turn off at headland and back on, a new pass/polygon is started
    private readonly Dictionary<int, List<SectionVisualPolygon>> _sectionPolygons = new();

    // ========== DETECTION LAYER (Bitmap) ==========
    // Coverage bitmap for O(1) coverage detection (like AgOpenGPS GPU pixel readback)
    // Each cell represents a small area; if present in the set, that cell is covered
    private const double BITMAP_CELL_SIZE = 0.5; // meters per cell (0.5m = ~1.6ft resolution)
    private readonly HashSet<(int CellE, int CellN)> _coverageBitmap = new();

    // ========== TRACKING STATE ==========
    // Track which sections are actively mapping
    private readonly HashSet<int> _activeSections = new();

    // Track last edges per section for area calculation and bitmap rasterization
    private readonly Dictionary<int, ((double E, double N) Left, (double E, double N) Right)> _lastEdgesPerSection = new();

    // Track last VISUAL edges per section for decimation (only add points if far enough apart)
    // This is separate from _lastEdgesPerSection which tracks every point for bitmap rasterization
    private readonly Dictionary<int, ((double E, double N) Left, (double E, double N) Right)> _lastVisualEdgesPerSection = new();
    private const double VISUAL_DECIMATION_THRESHOLD_SQ = 25.0; // 5m minimum spacing squared for visual polygon

    // Area totals (calculated incrementally)
    private double _totalWorkedArea;
    private double _totalWorkedAreaUser;

    // Dirty flag to track if coverage has changed since last flush
    private bool _coverageDirty;
    private double _pendingAreaAdded;

    /// <summary>
    /// Tracks the visual polygon for a single section - left edge points forward,
    /// right edge points will be reversed to close the polygon.
    /// </summary>
    private class SectionVisualPolygon
    {
        public List<(double E, double N)> LeftEdge { get; } = new();
        public List<(double E, double N)> RightEdge { get; } = new();
        public CoverageColor Color { get; set; }
        public bool IsDirty { get; set; } = true;
    }

    public double TotalWorkedArea => _totalWorkedArea;
    public double TotalWorkedAreaUser => _totalWorkedAreaUser;
    public int PatchCount => _coverageBitmap.Count; // Now represents bitmap cell count
    public bool IsAnyZoneMapping => _activeSections.Count > 0;

    // Debug: total polygon points across all sections (sum across all passes)
    public int TotalPolygonPoints => _sectionPolygons.Values.Sum(passes => passes.Sum(p => p.LeftEdge.Count + p.RightEdge.Count));
    public int ActiveSectionCount => _activeSections.Count;

    // Track the current active polygon for each section (last polygon in the list)
    private SectionVisualPolygon? GetCurrentPolygon(int zoneIndex) =>
        _sectionPolygons.TryGetValue(zoneIndex, out var passes) && passes.Count > 0 ? passes[^1] : null;

    public event EventHandler<CoverageUpdatedEventArgs>? CoverageUpdated;

    public void StartMapping(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge, CoverageColor? color = null)
    {
        // If already mapping this zone, just continue
        if (_activeSections.Contains(zoneIndex))
            return;

        Console.WriteLine($"[Timing] CovStart zone {zoneIndex} at ({leftEdge.Easting:F1}, {leftEdge.Northing:F1})");

        _activeSections.Add(zoneIndex);

        // Initialize pass list for this section if needed
        if (!_sectionPolygons.TryGetValue(zoneIndex, out var passes))
        {
            passes = new List<SectionVisualPolygon>();
            _sectionPolygons[zoneIndex] = passes;
        }

        // Always create a NEW polygon for each pass (prevents fold-back artifacts at U-turns)
        var polygon = new SectionVisualPolygon { Color = color ?? GetZoneColor(zoneIndex) };
        passes.Add(polygon);

        // Store initial edge for area calculation
        _lastEdgesPerSection[zoneIndex] = (
            (leftEdge.Easting, leftEdge.Northing),
            (rightEdge.Easting, rightEdge.Northing));

        // Add initial points to visual polygon
        polygon.LeftEdge.Add((leftEdge.Easting, leftEdge.Northing));
        polygon.RightEdge.Add((rightEdge.Easting, rightEdge.Northing));
        polygon.IsDirty = true;
    }

    public void StopMapping(int zoneIndex)
    {
        if (!_activeSections.Contains(zoneIndex))
            return;

        var currentPolygon = GetCurrentPolygon(zoneIndex);
        int passCount = _sectionPolygons.TryGetValue(zoneIndex, out var passes) ? passes.Count : 0;
        Console.WriteLine($"[Timing] CovStop zone {zoneIndex}, pass {passCount} has {currentPolygon?.LeftEdge.Count ?? 0} pts");

        _activeSections.Remove(zoneIndex);
        _lastEdgesPerSection.Remove(zoneIndex);
        _lastVisualEdgesPerSection.Remove(zoneIndex);
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
            // Also store as last visual edge
            _lastVisualEdgesPerSection[zoneIndex] = (
                (leftEdge.Easting, leftEdge.Northing),
                (rightEdge.Easting, rightEdge.Northing));
            return;
        }

        // Add to visual polygon only if far enough from last visual point (decimation)
        // This prevents unbounded polygon growth while keeping bitmap accurate
        var currentPolygon = GetCurrentPolygon(zoneIndex);
        if (currentPolygon != null)
        {
            bool shouldAddVisual = true;
            if (_lastVisualEdgesPerSection.TryGetValue(zoneIndex, out var lastVisualEdges))
            {
                // Check distance from last visual point (use left edge as reference)
                double dx = leftEdge.Easting - lastVisualEdges.Left.E;
                double dy = leftEdge.Northing - lastVisualEdges.Left.N;
                double distSq = dx * dx + dy * dy;
                shouldAddVisual = distSq >= VISUAL_DECIMATION_THRESHOLD_SQ;
            }

            if (shouldAddVisual)
            {
                currentPolygon.LeftEdge.Add((leftEdge.Easting, leftEdge.Northing));
                currentPolygon.RightEdge.Add((rightEdge.Easting, rightEdge.Northing));
                currentPolygon.IsDirty = true;
                _lastVisualEdgesPerSection[zoneIndex] = (
                    (leftEdge.Easting, leftEdge.Northing),
                    (rightEdge.Easting, rightEdge.Northing));
            }
        }

        // Rasterize the quad to the coverage bitmap for O(1) detection (every point!)
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
                    _coverageBitmap.Add((ce, cn));
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
            PatchCount = _coverageBitmap.Count,
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
        // O(1) bitmap lookup - convert to cell coordinates and check if covered
        int cellE = (int)Math.Floor(easting / BITMAP_CELL_SIZE);
        int cellN = (int)Math.Floor(northing / BITMAP_CELL_SIZE);
        return _coverageBitmap.Contains((cellE, cellN));
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

            if (_coverageBitmap.Contains((cellE, cellN)))
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
    /// Rasterize a quad directly to the bitmap (used during file loading).
    /// </summary>
    private void RasterizeQuadToBitmapDirect(
        (double E, double N) p0, (double E, double N) p1,
        (double E, double N) p2, (double E, double N) p3)
    {
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
                double cellCenterE = (ce + 0.5) * BITMAP_CELL_SIZE;
                double cellCenterN = (cn + 0.5) * BITMAP_CELL_SIZE;

                if (IsPointInQuad(cellCenterE, cellCenterN, p0, p1, p2, p3))
                {
                    _coverageBitmap.Add((ce, cn));
                }
            }
        }
    }

    /// <summary>
    /// Get visual polygon data for rendering.
    /// Returns section index, color, and edge points for each pass of each section.
    /// Multiple passes per section are returned as separate polygons.
    /// </summary>
    public IEnumerable<(int SectionIndex, CoverageColor Color, IReadOnlyList<(double E, double N)> LeftEdge, IReadOnlyList<(double E, double N)> RightEdge)> GetSectionPolygons()
    {
        foreach (var (sectionIndex, passes) in _sectionPolygons)
        {
            foreach (var polygon in passes)
            {
                if (polygon.LeftEdge.Count >= 2)
                {
                    yield return (sectionIndex, polygon.Color, polygon.LeftEdge, polygon.RightEdge);
                }
            }
        }
    }

    /// <summary>
    /// Get the number of visual polygons (one per section with coverage)
    /// </summary>
    // Total number of polygon passes across all sections
    public int VisualPolygonCount => _sectionPolygons.Values.Sum(passes => passes.Count);

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
        // Clear visual polygons
        _sectionPolygons.Clear();

        // Clear coverage bitmap
        _coverageBitmap.Clear();

        // Clear tracking state
        _activeSections.Clear();
        _lastEdgesPerSection.Clear();
        _lastVisualEdgesPerSection.Clear();

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

    public void ResetUserArea()
    {
        _totalWorkedAreaUser = 0;
    }

    public void SaveToFile(string fieldDirectory)
    {
        var filename = Path.Combine(fieldDirectory, "Sections.txt");

        // Save visual polygons in new format
        // Format: section count, then for each section: index, color, point count, points
        using var writer = new StreamWriter(filename, false);

        // Write header with format version
        writer.WriteLine("V3"); // Version 3 = multi-pass polygon format

        foreach (var (sectionIndex, passes) in _sectionPolygons)
        {
            foreach (var polygon in passes)
            {
                if (polygon.LeftEdge.Count < 2) continue;

                // Write section header: index, R, G, B, point count
                // Note: same section index can appear multiple times (multiple passes)
                writer.WriteLine($"{sectionIndex},{polygon.Color.R},{polygon.Color.G},{polygon.Color.B},{polygon.LeftEdge.Count}");

                // Write left/right edge pairs
                for (int i = 0; i < polygon.LeftEdge.Count; i++)
                {
                    var left = polygon.LeftEdge[i];
                    var right = polygon.RightEdge[i];
                    writer.WriteLine($"{left.E:F3},{left.N:F3},{right.E:F3},{right.N:F3}");
                }
            }
        }

        // Write total worked area at end for quick loading
        writer.WriteLine($"AREA,{_totalWorkedArea:F2}");
    }

    public void LoadFromFile(string fieldDirectory)
    {
        var path = Path.Combine(fieldDirectory, "Sections.txt");
        if (!File.Exists(path)) return;

        ClearAll();

        using var reader = new StreamReader(path);

        // Check format version
        var firstLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(firstLine)) return;

        if (firstLine == "V2" || firstLine == "V3")
        {
            // V2/V3 format - load visual polygons directly
            // V3 supports multiple passes per section (same section index multiple times)
            LoadNewFormat(reader);
        }
        else
        {
            // Legacy format - convert patches to visual polygons + bitmap
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            reader.DiscardBufferedData();
            LoadLegacyFormat(reader);
        }

        _totalWorkedAreaUser = _totalWorkedArea;

        CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
        {
            TotalArea = _totalWorkedArea,
            PatchCount = _coverageBitmap.Count,
            AreaAdded = 0
        });
    }

    /// <summary>
    /// Load coverage from new V2 format (visual polygons).
    /// </summary>
    private void LoadNewFormat(StreamReader reader)
    {
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Check for area line at end
            if (line.StartsWith("AREA,"))
            {
                if (double.TryParse(line.Substring(5), NumberStyles.Float, CultureInfo.InvariantCulture, out double area))
                    _totalWorkedArea = area;
                continue;
            }

            // Parse section header: index, R, G, B, point count
            var parts = line.Split(',');
            if (parts.Length < 5) continue;

            if (!int.TryParse(parts[0], out int sectionIndex) ||
                !byte.TryParse(parts[1], out byte r) ||
                !byte.TryParse(parts[2], out byte g) ||
                !byte.TryParse(parts[3], out byte b) ||
                !int.TryParse(parts[4], out int pointCount))
                continue;

            var polygon = new SectionVisualPolygon
            {
                Color = new CoverageColor(r, g, b),
                IsDirty = true
            };

            (double E, double N) prevLeft = (0, 0);
            (double E, double N) prevRight = (0, 0);

            // Read edge pairs
            for (int i = 0; i < pointCount && !reader.EndOfStream; i++)
            {
                var edgeLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(edgeLine)) continue;

                var edgeParts = edgeLine.Split(',');
                if (edgeParts.Length >= 4 &&
                    double.TryParse(edgeParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double leftE) &&
                    double.TryParse(edgeParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double leftN) &&
                    double.TryParse(edgeParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double rightE) &&
                    double.TryParse(edgeParts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double rightN))
                {
                    polygon.LeftEdge.Add((leftE, leftN));
                    polygon.RightEdge.Add((rightE, rightN));

                    // Rasterize quad to bitmap (skip first point, need two for quad)
                    if (i > 0)
                    {
                        RasterizeQuadToBitmapDirect(
                            prevLeft, prevRight,
                            (rightE, rightN), (leftE, leftN));
                    }

                    prevLeft = (leftE, leftN);
                    prevRight = (rightE, rightN);
                }
            }

            if (polygon.LeftEdge.Count >= 2)
            {
                // Add polygon as a new pass for this section
                if (!_sectionPolygons.TryGetValue(sectionIndex, out var passes))
                {
                    passes = new List<SectionVisualPolygon>();
                    _sectionPolygons[sectionIndex] = passes;
                }
                passes.Add(polygon);
            }
        }
    }

    /// <summary>
    /// Load coverage from legacy patch format and convert to new structures.
    /// </summary>
    private void LoadLegacyFormat(StreamReader reader)
    {
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (!int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int vertCount))
                continue;

            var vertices = new List<Vec3>(vertCount);

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
                    vertices.Add(new Vec3(e, n, h));
                }
            }

            if (vertices.Count < 4) continue;

            // First vertex is color
            var color = CoverageColor.FromVec3(vertices[0]);

            // Use section 0 for legacy patches (they don't have section info)
            // Each legacy patch becomes a separate polygon (pass) for section 0
            if (!_sectionPolygons.TryGetValue(0, out var passes))
            {
                passes = new List<SectionVisualPolygon>();
                _sectionPolygons[0] = passes;
            }
            var polygon = new SectionVisualPolygon { Color = color, IsDirty = true };
            passes.Add(polygon);

            // Extract left/right edges and rasterize to bitmap
            // Vertices: [color, left1, right1, left2, right2, ...]
            for (int i = 1; i < vertices.Count - 1; i += 2)
            {
                var left = vertices[i];
                var right = vertices[i + 1];
                polygon.LeftEdge.Add((left.Easting, left.Northing));
                polygon.RightEdge.Add((right.Easting, right.Northing));

                // Rasterize quad to bitmap (need previous pair)
                if (i >= 3)
                {
                    var prevLeft = vertices[i - 2];
                    var prevRight = vertices[i - 1];
                    RasterizeQuadToBitmapDirect(
                        (prevLeft.Easting, prevLeft.Northing),
                        (prevRight.Easting, prevRight.Northing),
                        (right.Easting, right.Northing),
                        (left.Easting, left.Northing));
                }
            }

            // Calculate area for loaded patch
            if (vertices.Count >= 5)
            {
                var points = vertices.ToArray();
                for (int i = 4; i < points.Length; i += 2)
                {
                    double area = workedAreaService.CalculateTriangleStripArea(points, i - 3);
                    _totalWorkedArea += area;
                }
            }
        }
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
