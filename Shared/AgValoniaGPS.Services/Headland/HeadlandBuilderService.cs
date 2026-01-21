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
using System.Linq;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.Geometry;

namespace AgValoniaGPS.Services.Headland;

/// <summary>
/// Service for building headland lines from field boundaries
/// </summary>
public class HeadlandBuilderService(IPolygonOffsetService polygonOffsetService) : IHeadlandBuilderService
{
    /// <inheritdoc/>
    public HeadlandBuildResult BuildHeadland(Boundary boundary, HeadlandBuildOptions options)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            return HeadlandBuildResult.Fail("No valid boundary available");
        }

        if (options.Distance <= 0)
        {
            return HeadlandBuildResult.Fail("Headland distance must be greater than 0");
        }

        // Convert boundary points to Vec2 list
        var boundaryPoints = boundary.OuterBoundary.Points
            .Select(p => new Vec2(p.Easting, p.Northing))
            .ToList();

        if (boundaryPoints.Count < 3)
        {
            return HeadlandBuildResult.Fail("Boundary must have at least 3 points");
        }

        // Create the offset polygon
        List<Vec2>? offsetPoints;

        if (options.Passes == 1)
        {
            offsetPoints = polygonOffsetService.CreateInwardOffset(
                boundaryPoints,
                options.Distance,
                options.JoinType);
        }
        else
        {
            // For multi-pass, get the innermost pass
            var allPasses = polygonOffsetService.CreateMultiPassOffset(
                boundaryPoints,
                options.Distance,
                options.Passes,
                options.JoinType);

            if (allPasses.Count == 0)
            {
                return HeadlandBuildResult.Fail("Headland distance too large for this boundary");
            }

            // Use innermost pass as the headland line
            offsetPoints = allPasses[allPasses.Count - 1];

            // Store all passes in result
            var result = new HeadlandBuildResult
            {
                Success = true,
                OuterHeadlandLine = polygonOffsetService.CalculatePointHeadings(offsetPoints),
                AllPasses = allPasses.Select(p => polygonOffsetService.CalculatePointHeadings(p)).ToList()
            };

            return result;
        }

        if (offsetPoints == null || offsetPoints.Count < 3)
        {
            return HeadlandBuildResult.Fail("Headland distance too large - polygon collapsed");
        }

        // Calculate headings for each point
        var headlandWithHeadings = polygonOffsetService.CalculatePointHeadings(offsetPoints);

        return HeadlandBuildResult.Ok(headlandWithHeadings);
    }

    /// <inheritdoc/>
    public List<Vec2>? PreviewHeadland(List<Vec2> boundaryPoints, double distance, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        if (boundaryPoints == null || boundaryPoints.Count < 3 || distance <= 0)
        {
            return null;
        }

        return polygonOffsetService.CreateInwardOffset(boundaryPoints, distance, joinType);
    }

    /// <inheritdoc/>
    public HeadlandBuildResult BuildAllHeadlands(Boundary boundary, HeadlandBuildOptions options)
    {
        // First build outer headland
        var outerResult = BuildHeadland(boundary, options);

        if (!outerResult.Success)
        {
            return outerResult;
        }

        // Build inner boundary headlands (outward offset for islands)
        if (options.IncludeInnerBoundaries && boundary.InnerBoundaries.Count > 0)
        {
            var innerHeadlands = new List<List<Vec3>>();

            foreach (var innerBoundary in boundary.InnerBoundaries)
            {
                if (!innerBoundary.IsValid || innerBoundary.IsDriveThrough)
                    continue;

                var innerPoints = innerBoundary.Points
                    .Select(p => new Vec2(p.Easting, p.Northing))
                    .ToList();

                // Outward offset for inner boundaries (headland goes around the island)
                var offsetPoints = polygonOffsetService.CreateOutwardOffset(
                    innerPoints,
                    options.Distance,
                    options.JoinType);

                if (offsetPoints != null && offsetPoints.Count >= 3)
                {
                    var withHeadings = polygonOffsetService.CalculatePointHeadings(offsetPoints);
                    innerHeadlands.Add(withHeadings);
                }
            }

            outerResult.InnerHeadlandLines = innerHeadlands;
        }

        return outerResult;
    }
}
