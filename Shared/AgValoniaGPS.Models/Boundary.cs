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

using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;

namespace AgValoniaGPS.Models;

/// <summary>
/// Represents a field boundary with outer boundary and optional inner boundaries (holes)
/// Matches AgOpenGPS Boundary.txt format
/// </summary>
public class Boundary
{
    /// <summary>
    /// Outer boundary polygon (required)
    /// </summary>
    public BoundaryPolygon? OuterBoundary { get; set; }

    /// <summary>
    /// Inner boundary polygons (holes/exclusions like ponds)
    /// </summary>
    public List<BoundaryPolygon> InnerBoundaries { get; set; } = new List<BoundaryPolygon>();

    /// <summary>
    /// Headland polygon - defines the inner working area boundary.
    /// Points INSIDE this polygon are the work area (sections ON).
    /// Points OUTSIDE (but inside outer boundary) are the headland zone (sections OFF).
    /// This is separate from InnerBoundaries which are holes to avoid.
    /// </summary>
    public BoundaryPolygon? HeadlandPolygon { get; set; }

    /// <summary>
    /// Whether this boundary is turned on for display/guidance
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Total area in hectares (calculated from outer boundary minus inner boundaries)
    /// </summary>
    public double AreaHectares
    {
        get
        {
            double area = OuterBoundary?.AreaHectares ?? 0;
            foreach (var inner in InnerBoundaries)
            {
                area -= inner.AreaHectares;
            }
            return area;
        }
    }

    /// <summary>
    /// Check if this boundary is valid (has a valid outer boundary)
    /// </summary>
    public bool IsValid => OuterBoundary?.IsValid ?? false;

    /// <summary>
    /// Check if a point is inside the boundary area (inside outer, outside inner holes)
    /// Ported from AOG_Dev CBoundary.IsPointInsideFenceArea()
    /// </summary>
    /// <param name="easting">Point easting coordinate</param>
    /// <param name="northing">Point northing coordinate</param>
    /// <returns>True if point is inside the usable boundary area</returns>
    public bool IsPointInside(double easting, double northing)
    {
        // First check if inside outer boundary
        if (OuterBoundary == null || !OuterBoundary.IsPointInside(easting, northing))
        {
            return false;
        }

        // Check if inside any inner boundary (hole) - if so, it's outside the usable area
        foreach (var innerBoundary in InnerBoundaries)
        {
            if (!innerBoundary.IsDriveThrough && innerBoundary.IsPointInside(easting, northing))
            {
                return false;
            }
        }

        // Inside outer, outside all inner holes
        return true;
    }

    /// <summary>
    /// Check if a position is inside the boundary
    /// </summary>
    public bool IsPointInside(Position position)
    {
        return IsPointInside(position.Easting, position.Northing);
    }

    /// <summary>
    /// Get segment-based boundary status for a section.
    /// Checks if section segment is inside outer boundary and outside inner holes.
    /// </summary>
    /// <param name="sectionCenter">Center point of section in world coords.</param>
    /// <param name="heading">Section heading in radians.</param>
    /// <param name="halfWidth">Half the section width in meters.</param>
    /// <returns>Boundary result with inside percentage.</returns>
    public BoundaryResult GetSegmentBoundaryStatus(Vec2 sectionCenter, double heading, double halfWidth)
    {
        // Check outer boundary first
        if (OuterBoundary == null || !OuterBoundary.IsValid)
            return BoundaryResult.FullyInside; // No boundary = always in

        var outerResult = OuterBoundary.GetSegmentBoundaryStatus(sectionCenter, heading, halfWidth);

        // If fully outside outer boundary, we're done
        if (outerResult.IsFullyOutside)
            return outerResult;

        // Check inner boundaries (holes) - subtract their inside portions
        double effectiveInsidePercent = outerResult.InsidePercent;

        foreach (var innerBoundary in InnerBoundaries)
        {
            if (innerBoundary.IsDriveThrough) continue; // Skip drive-through boundaries

            var innerResult = innerBoundary.GetSegmentBoundaryStatus(sectionCenter, heading, halfWidth);

            // Portion inside inner boundary is "outside" the usable area
            effectiveInsidePercent -= innerResult.InsidePercent;
        }

        effectiveInsidePercent = System.Math.Max(0, effectiveInsidePercent);

        return new BoundaryResult(
            IsFullyInside: effectiveInsidePercent > 0.99,
            IsFullyOutside: effectiveInsidePercent < 0.01,
            CrossesBoundary: outerResult.CrossesBoundary || effectiveInsidePercent != outerResult.InsidePercent,
            InsidePercent: effectiveInsidePercent
        );
    }
}
