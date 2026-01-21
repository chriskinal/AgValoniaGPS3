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
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.Geometry;

namespace AgValoniaGPS.Services.Headland;

/// <summary>
/// Service for building headland lines from field boundaries
/// </summary>
public interface IHeadlandBuilderService
{
    /// <summary>
    /// Build headland from a boundary with specified options
    /// </summary>
    /// <param name="boundary">The field boundary</param>
    /// <param name="options">Build options (distance, passes, etc.)</param>
    /// <returns>Result containing headland points or error</returns>
    HeadlandBuildResult BuildHeadland(Boundary boundary, HeadlandBuildOptions options);

    /// <summary>
    /// Preview headland without saving - for real-time UI preview
    /// </summary>
    /// <param name="boundaryPoints">Boundary points to offset</param>
    /// <param name="distance">Offset distance in meters</param>
    /// <param name="joinType">Corner join type</param>
    /// <returns>Preview headland points, or null if invalid</returns>
    List<Vec2>? PreviewHeadland(List<Vec2> boundaryPoints, double distance, OffsetJoinType joinType = OffsetJoinType.Round);

    /// <summary>
    /// Build headland for all boundaries in a field (outer + inner)
    /// </summary>
    /// <param name="boundary">The complete field boundary with inners</param>
    /// <param name="options">Build options</param>
    /// <returns>Result containing all headland lines</returns>
    HeadlandBuildResult BuildAllHeadlands(Boundary boundary, HeadlandBuildOptions options);
}

/// <summary>
/// Options for headland building
/// </summary>
public class HeadlandBuildOptions
{
    /// <summary>
    /// Offset distance in meters
    /// </summary>
    public double Distance { get; set; } = 10.0;

    /// <summary>
    /// Number of headland passes (concentric rings)
    /// </summary>
    public int Passes { get; set; } = 1;

    /// <summary>
    /// Corner join type
    /// </summary>
    public OffsetJoinType JoinType { get; set; } = OffsetJoinType.Round;

    /// <summary>
    /// If true, also build headlands around inner boundaries (islands)
    /// </summary>
    public bool IncludeInnerBoundaries { get; set; } = true;
}

/// <summary>
/// Result of headland building operation
/// </summary>
public class HeadlandBuildResult
{
    /// <summary>
    /// Whether the build was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The outer headland line points (with headings)
    /// </summary>
    public List<Vec3>? OuterHeadlandLine { get; set; }

    /// <summary>
    /// Headland lines for inner boundaries (islands) - outward offset
    /// </summary>
    public List<List<Vec3>>? InnerHeadlandLines { get; set; }

    /// <summary>
    /// All headland passes if multi-pass was requested
    /// </summary>
    public List<List<Vec3>>? AllPasses { get; set; }

    /// <summary>
    /// Error message if build failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static HeadlandBuildResult Ok(List<Vec3> outerHeadland)
    {
        return new HeadlandBuildResult
        {
            Success = true,
            OuterHeadlandLine = outerHeadland
        };
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static HeadlandBuildResult Fail(string message)
    {
        return new HeadlandBuildResult
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
