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

namespace AgValoniaGPS.Services.Geometry;

/// <summary>
/// Interface for polygon offset operations
/// </summary>
public interface IPolygonOffsetService
{
    /// <summary>
    /// Create an inward offset polygon from a boundary.
    /// </summary>
    /// <param name="boundaryPoints">Outer boundary points</param>
    /// <param name="offsetDistance">Inward offset distance in meters</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>Offset polygon points, or null if offset collapses</returns>
    List<Vec2>? CreateInwardOffset(List<Vec2> boundaryPoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round);

    /// <summary>
    /// Create an outward offset polygon from a boundary.
    /// </summary>
    /// <param name="boundaryPoints">Inner boundary points</param>
    /// <param name="offsetDistance">Outward offset distance in meters</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>Offset polygon points</returns>
    List<Vec2>? CreateOutwardOffset(List<Vec2> boundaryPoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round);

    /// <summary>
    /// Create multiple concentric offset polygons (for multi-pass headlands).
    /// </summary>
    /// <param name="boundaryPoints">Outer boundary points</param>
    /// <param name="offsetDistance">Distance per pass in meters</param>
    /// <param name="passes">Number of passes</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>List of offset polygons from outermost to innermost</returns>
    List<List<Vec2>> CreateMultiPassOffset(List<Vec2> boundaryPoints, double offsetDistance, int passes, OffsetJoinType joinType = OffsetJoinType.Round);

    /// <summary>
    /// Calculate headings for each point in the polygon based on adjacent points.
    /// </summary>
    /// <param name="points">Polygon points</param>
    /// <returns>Points with headings as Vec3 (X, Y, Heading in radians)</returns>
    List<Vec3> CalculatePointHeadings(List<Vec2> points);

    /// <summary>
    /// Create an offset of an open polyline (not closed polygon).
    /// Used for headland segments from boundary clips.
    /// </summary>
    /// <param name="linePoints">Open polyline points</param>
    /// <param name="offsetDistance">Offset distance in meters (positive = left of travel direction)</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>Offset line points, or null if offset fails</returns>
    List<Vec2>? CreateLineOffset(List<Vec2> linePoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round);
}
