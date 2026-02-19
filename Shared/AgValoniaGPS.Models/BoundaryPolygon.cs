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
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;

namespace AgValoniaGPS.Models;

/// <summary>
/// Represents a single boundary polygon (outer or inner)
/// Points are in local coordinates (meters from field origin)
/// </summary>
public class BoundaryPolygon
{
    /// <summary>
    /// Boundary points (Easting, Northing, Heading in local coordinates)
    /// Minimum 3 points required for valid polygon
    /// </summary>
    public List<BoundaryPoint> Points { get; set; } = new List<BoundaryPoint>();

    // Bounding box cache for fast rejection/acceptance
    private double _minEasting = double.MaxValue;
    private double _maxEasting = double.MinValue;
    private double _minNorthing = double.MaxValue;
    private double _maxNorthing = double.MinValue;
    private bool _boundsDirty = true;

    // Spatial index for fast segment lookup
    private const double GRID_CELL_SIZE = 50.0; // meters per cell
    private Dictionary<(int, int), List<int>>? _spatialIndex; // cell -> list of segment indices
    private int _gridOffsetE; // offset to make grid indices non-negative
    private int _gridOffsetN;

    /// <summary>
    /// Call this after modifying Points to update the bounding box cache and spatial index
    /// </summary>
    public void UpdateBounds()
    {
        if (Points.Count == 0)
        {
            _minEasting = _maxEasting = _minNorthing = _maxNorthing = 0;
            _boundsDirty = false;
            _spatialIndex = null;
            return;
        }

        _minEasting = double.MaxValue;
        _maxEasting = double.MinValue;
        _minNorthing = double.MaxValue;
        _maxNorthing = double.MinValue;

        foreach (var pt in Points)
        {
            if (pt.Easting < _minEasting) _minEasting = pt.Easting;
            if (pt.Easting > _maxEasting) _maxEasting = pt.Easting;
            if (pt.Northing < _minNorthing) _minNorthing = pt.Northing;
            if (pt.Northing > _maxNorthing) _maxNorthing = pt.Northing;
        }
        _boundsDirty = false;

        // Build spatial index
        BuildSpatialIndex();
    }

    /// <summary>
    /// Build grid-based spatial index for fast segment lookup
    /// </summary>
    private void BuildSpatialIndex()
    {
        _spatialIndex = new Dictionary<(int, int), List<int>>();

        // Calculate grid offset to handle negative coordinates
        _gridOffsetE = (int)Math.Floor(_minEasting / GRID_CELL_SIZE);
        _gridOffsetN = (int)Math.Floor(_minNorthing / GRID_CELL_SIZE);

        // Index each segment (edge between consecutive points)
        for (int i = 0; i < Points.Count; i++)
        {
            int j = (i + 1) % Points.Count;

            var p1 = Points[i];
            var p2 = Points[j];

            // Find all cells this segment touches
            double minE = Math.Min(p1.Easting, p2.Easting);
            double maxE = Math.Max(p1.Easting, p2.Easting);
            double minN = Math.Min(p1.Northing, p2.Northing);
            double maxN = Math.Max(p1.Northing, p2.Northing);

            int cellMinE = (int)Math.Floor(minE / GRID_CELL_SIZE) - _gridOffsetE;
            int cellMaxE = (int)Math.Floor(maxE / GRID_CELL_SIZE) - _gridOffsetE;
            int cellMinN = (int)Math.Floor(minN / GRID_CELL_SIZE) - _gridOffsetN;
            int cellMaxN = (int)Math.Floor(maxN / GRID_CELL_SIZE) - _gridOffsetN;

            // Add segment index to all cells it touches
            for (int ce = cellMinE; ce <= cellMaxE; ce++)
            {
                for (int cn = cellMinN; cn <= cellMaxN; cn++)
                {
                    var key = (ce, cn);
                    if (!_spatialIndex.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        _spatialIndex[key] = list;
                    }
                    list.Add(i);
                }
            }
        }
    }

    /// <summary>
    /// Get segment indices near a point (from spatial index)
    /// </summary>
    private IEnumerable<int> GetNearbySegments(double easting, double northing, double radius)
    {
        if (_spatialIndex == null) BuildSpatialIndex();
        if (_spatialIndex == null) yield break;

        // Find cells within radius
        int cellE = (int)Math.Floor(easting / GRID_CELL_SIZE) - _gridOffsetE;
        int cellN = (int)Math.Floor(northing / GRID_CELL_SIZE) - _gridOffsetN;
        int cellRadius = (int)Math.Ceiling(radius / GRID_CELL_SIZE);

        var seen = new HashSet<int>();

        for (int ce = cellE - cellRadius; ce <= cellE + cellRadius; ce++)
        {
            for (int cn = cellN - cellRadius; cn <= cellN + cellRadius; cn++)
            {
                if (_spatialIndex.TryGetValue((ce, cn), out var segments))
                {
                    foreach (int idx in segments)
                    {
                        if (seen.Add(idx)) // Only yield each segment once
                            yield return idx;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if point is definitely outside the bounding box (fast rejection)
    /// </summary>
    private bool IsOutsideBounds(double easting, double northing, double margin = 0)
    {
        if (_boundsDirty) UpdateBounds();
        return easting < _minEasting - margin || easting > _maxEasting + margin ||
               northing < _minNorthing - margin || northing > _maxNorthing + margin;
    }

    /// <summary>
    /// Check if point is definitely inside the bounding box with margin (potential fast accept)
    /// </summary>
    private bool IsDeepInsideBounds(double easting, double northing, double margin)
    {
        if (_boundsDirty) UpdateBounds();
        return easting > _minEasting + margin && easting < _maxEasting - margin &&
               northing > _minNorthing + margin && northing < _maxNorthing - margin;
    }

    /// <summary>
    /// Whether this is a drive-through boundary (true) or avoid boundary (false)
    /// </summary>
    public bool IsDriveThrough { get; set; } = false;

    /// <summary>
    /// Area in square meters (calculated from points)
    /// </summary>
    public double AreaSquareMeters
    {
        get
        {
            if (Points.Count < 3) return 0;

            // Shoelace formula for polygon area
            double area = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                int j = (i + 1) % Points.Count;
                area += Points[i].Easting * Points[j].Northing;
                area -= Points[j].Easting * Points[i].Northing;
            }
            return Math.Abs(area) / 2.0;
        }
    }

    /// <summary>
    /// Area in hectares (10,000 square meters = 1 hectare)
    /// </summary>
    public double AreaHectares => AreaSquareMeters / 10000.0;

    /// <summary>
    /// Area in acres (4046.86 square meters = 1 acre)
    /// </summary>
    public double AreaAcres => AreaSquareMeters / 4046.86;

    /// <summary>
    /// Check if this polygon is valid
    /// </summary>
    public bool IsValid => Points.Count >= 3;

    /// <summary>
    /// Check if a point is inside this polygon using ray-casting algorithm
    /// Ported from AOG_Dev CPolygon.IsPointInPolygon()
    /// </summary>
    /// <param name="easting">Point easting coordinate</param>
    /// <param name="northing">Point northing coordinate</param>
    /// <returns>True if point is inside the polygon</returns>
    public bool IsPointInside(double easting, double northing)
    {
        if (Points.Count < 3) return false;

        // Fast rejection: if outside bounding box, definitely not inside
        if (IsOutsideBounds(easting, northing))
            return false;

        bool isInside = false;
        int j = Points.Count - 1;

        for (int i = 0; i < Points.Count; i++)
        {
            // Ray-casting algorithm: cast ray from point to infinity
            // Count intersections with polygon edges
            if ((Points[i].Northing > northing) != (Points[j].Northing > northing) &&
                (easting < (Points[j].Easting - Points[i].Easting) *
                (northing - Points[i].Northing) /
                (Points[j].Northing - Points[i].Northing) + Points[i].Easting))
            {
                isInside = !isInside;
            }
            j = i;
        }

        return isInside;
    }

    /// <summary>
    /// Check boundary status for a section segment using coordinate transform method.
    /// More accurate than point-based check as it detects when section edges cross boundary.
    /// </summary>
    /// <param name="sectionCenter">Center point of section in world coords.</param>
    /// <param name="heading">Section heading in radians.</param>
    /// <param name="halfWidth">Half the section width in meters.</param>
    /// <returns>Boundary result with inside percentage and crossing info.</returns>
    public BoundaryResult GetSegmentBoundaryStatus(Vec2 sectionCenter, double heading, double halfWidth)
    {
        if (Points.Count < 3)
            return BoundaryResult.FullyInside; // No boundary = always inside

        // FAST PATH: If section center is deep inside bounding box (50m+ from any edge),
        // skip expensive polygon intersection tests - we're definitely fully inside
        const double DEEP_INSIDE_MARGIN = 50.0; // meters from bounding box edge
        if (IsDeepInsideBounds(sectionCenter.Easting, sectionCenter.Northing, DEEP_INSIDE_MARGIN + halfWidth))
        {
            return BoundaryResult.FullyInside;
        }

        // Precompute transform
        double cos = Math.Cos(-heading);
        double sin = Math.Sin(-heading);

        // Find where boundary edges cross Y=0 (the section line) in local coords
        var crossings = new List<double>();

        // Use spatial index to only check nearby segments (within search radius)
        double searchRadius = halfWidth + GRID_CELL_SIZE; // Section width + one cell margin

        foreach (int i in GetNearbySegments(sectionCenter.Easting, sectionCenter.Northing, searchRadius))
        {
            int j = (i + 1) % Points.Count;

            // Transform boundary points to local coordinates
            var p1 = GeometryMath.ToLocalCoords(
                new Vec2(Points[i].Easting, Points[i].Northing),
                sectionCenter, cos, sin);
            var p2 = GeometryMath.ToLocalCoords(
                new Vec2(Points[j].Easting, Points[j].Northing),
                sectionCenter, cos, sin);

            // Find X intercept where edge crosses Y=0
            var intercept = GeometryMath.GetXInterceptAtYZero(p1, p2);
            if (intercept.HasValue)
            {
                // Only count crossings within section width (with margin)
                if (intercept.Value >= -halfWidth - 1.0 && intercept.Value <= halfWidth + 1.0)
                {
                    crossings.Add(intercept.Value);
                }
            }
        }

        // Check endpoints: perpendicular to heading
        double perpHeading = heading + Math.PI / 2.0;
        bool leftEdgeInside = IsPointInside(
            sectionCenter.Easting + Math.Sin(perpHeading) * (-halfWidth),
            sectionCenter.Northing + Math.Cos(perpHeading) * (-halfWidth));
        bool rightEdgeInside = IsPointInside(
            sectionCenter.Easting + Math.Sin(perpHeading) * halfWidth,
            sectionCenter.Northing + Math.Cos(perpHeading) * halfWidth);

        // No crossings - entire segment is either inside or outside
        if (crossings.Count == 0)
        {
            // Check center point to determine
            bool centerInside = IsPointInside(sectionCenter.Easting, sectionCenter.Northing);
            if (centerInside)
                return BoundaryResult.FullyInside;
            else
                return BoundaryResult.FullyOutside;
        }

        // Sort crossings from left to right
        crossings.Sort();

        // Calculate inside percentage by walking along section
        // Start from left edge, toggle inside/outside at each crossing
        double totalWidth = halfWidth * 2;
        double insideLength = 0;

        // Determine starting state (is left edge inside?)
        bool currentlyInside = leftEdgeInside;
        double lastX = -halfWidth;

        foreach (double crossX in crossings)
        {
            // Clip to section bounds
            double clippedX = Math.Max(-halfWidth, Math.Min(halfWidth, crossX));

            if (currentlyInside)
            {
                insideLength += clippedX - lastX;
            }

            lastX = clippedX;
            currentlyInside = !currentlyInside;
        }

        // Handle final segment to right edge
        if (currentlyInside)
        {
            insideLength += halfWidth - lastX;
        }

        double insidePercent = Math.Max(0, Math.Min(1, insideLength / totalWidth));

        return new BoundaryResult(
            IsFullyInside: insidePercent > 0.99,
            IsFullyOutside: insidePercent < 0.01,
            CrossesBoundary: crossings.Count > 0,
            InsidePercent: insidePercent
        );
    }
}

/// <summary>
/// Represents a single point in a boundary polygon
/// Coordinates are in local system (meters from field origin)
/// </summary>
public class BoundaryPoint
{
    /// <summary>
    /// Easting (X coordinate) in meters
    /// </summary>
    public double Easting { get; set; }

    /// <summary>
    /// Northing (Y coordinate) in meters
    /// </summary>
    public double Northing { get; set; }

    /// <summary>
    /// Heading/direction at this point in radians
    /// </summary>
    public double Heading { get; set; }

    public BoundaryPoint() { }

    public BoundaryPoint(double easting, double northing, double heading)
    {
        Easting = easting;
        Northing = northing;
        Heading = heading;
    }
}
