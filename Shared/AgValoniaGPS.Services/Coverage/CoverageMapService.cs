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
public class CoverageMapService : ICoverageMapService
{
    // All coverage patches (completed and active)
    private readonly List<CoveragePatch> _patches = new();

    // Active patches per zone (index = zone index)
    private readonly Dictionary<int, CoveragePatch> _activePatches = new();

    // Patches pending save to file
    private readonly List<CoveragePatch> _patchSaveList = new();

    // Area calculation service
    private readonly IWorkedAreaService _workedAreaService;

    // Area totals
    private double _totalWorkedArea;
    private double _totalWorkedAreaUser;

    // Triangle count threshold for splitting patches (for rendering efficiency)
    private const int MAX_TRIANGLES_PER_PATCH = 62;

    public double TotalWorkedArea => _totalWorkedArea;
    public double TotalWorkedAreaUser => _totalWorkedAreaUser;
    public int PatchCount => _patches.Count;
    public bool IsAnyZoneMapping => _activePatches.Count > 0;

    public event EventHandler<CoverageUpdatedEventArgs>? CoverageUpdated;

    public CoverageMapService(IWorkedAreaService workedAreaService)
    {
        _workedAreaService = workedAreaService;
    }

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
        _activePatches[zoneIndex] = patch;
    }

    public void StopMapping(int zoneIndex)
    {
        if (!_activePatches.TryGetValue(zoneIndex, out var patch))
            return;

        // Mark patch as complete
        patch.IsActive = false;
        _activePatches.Remove(zoneIndex);

        // Save patch if it has enough points
        if (patch.Vertices.Count > 4)
        {
            _patchSaveList.Add(patch);
        }
        else
        {
            // Remove incomplete patches
            _patches.Remove(patch);
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

            double area = _workedAreaService.CalculateTriangleStripArea(points, c - 3);
            _totalWorkedArea += area;
            _totalWorkedAreaUser += area;

            // Fire event
            CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
            {
                TotalArea = _totalWorkedArea,
                PatchCount = _patches.Count,
                AreaAdded = area
            });
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
        // Save the old patch
        oldPatch.IsActive = false;
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
        _activePatches[zoneIndex] = newPatch;
    }

    public bool IsZoneMapping(int zoneIndex)
    {
        return _activePatches.ContainsKey(zoneIndex);
    }

    public bool IsPointCovered(double easting, double northing)
    {
        // Simple bounding box check for each patch
        // TODO: Implement proper point-in-polygon for triangle strips
        foreach (var patch in _patches)
        {
            if (!patch.IsRenderable) continue;

            // Quick bounding box check
            double minE = double.MaxValue, maxE = double.MinValue;
            double minN = double.MaxValue, maxN = double.MinValue;

            for (int i = 1; i < patch.Vertices.Count; i++) // Skip color vertex
            {
                var v = patch.Vertices[i];
                if (v.Easting < minE) minE = v.Easting;
                if (v.Easting > maxE) maxE = v.Easting;
                if (v.Northing < minN) minN = v.Northing;
                if (v.Northing > maxN) maxN = v.Northing;
            }

            if (easting >= minE && easting <= maxE && northing >= minN && northing <= maxN)
            {
                // Point is in bounding box - for now, consider it covered
                // A more precise check would test against actual triangles
                return true;
            }
        }

        return false;
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
        _patchSaveList.Clear();
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
                        double area = _workedAreaService.CalculateTriangleStripArea(points, i - 3);
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

        CoverageUpdated?.Invoke(this, new CoverageUpdatedEventArgs
        {
            TotalArea = _totalWorkedArea,
            PatchCount = _patches.Count,
            AreaAdded = 0
        });
    }

    /// <summary>
    /// Get default color for a zone (supports multi-colored sections)
    /// </summary>
    private CoverageColor GetZoneColor(int zoneIndex)
    {
        var tool = ConfigurationStore.Instance.Tool;

        if (!tool.IsMultiColoredSections)
        {
            return CoverageColor.Default;
        }

        // Predefined section colors (matching AgOpenGPS)
        var colors = new CoverageColor[]
        {
            new(0, 255, 0),     // Green
            new(255, 0, 0),     // Red
            new(0, 0, 255),     // Blue
            new(255, 255, 0),   // Yellow
            new(255, 0, 255),   // Magenta
            new(0, 255, 255),   // Cyan
            new(255, 128, 0),   // Orange
            new(128, 0, 255),   // Purple
        };

        return colors[zoneIndex % colors.Length];
    }
}
