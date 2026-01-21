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

        // Precompute transform
        double cos = Math.Cos(-heading);
        double sin = Math.Sin(-heading);

        // Find where boundary edges cross Y=0 (the section line) in local coords
        var crossings = new List<double>();

        for (int i = 0; i < Points.Count; i++)
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

        // Check endpoints
        bool leftInside = IsPointInside(
            sectionCenter.Easting - Math.Cos(heading) * halfWidth + Math.Sin(heading) * 0,
            sectionCenter.Northing + Math.Sin(heading) * halfWidth + Math.Cos(heading) * 0);
        // Correct: perpendicular to heading
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
