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

namespace AgValoniaGPS.Models.Headland;

/// <summary>
/// Result of segment-based boundary analysis.
/// </summary>
/// <param name="IsFullyInside">Entire segment is inside boundary.</param>
/// <param name="IsFullyOutside">Entire segment is outside boundary.</param>
/// <param name="CrossesBoundary">Segment crosses boundary edge.</param>
/// <param name="InsidePercent">Percentage of segment inside boundary (0.0 to 1.0).</param>
public readonly record struct BoundaryResult(
    bool IsFullyInside,
    bool IsFullyOutside,
    bool CrossesBoundary,
    double InsidePercent)
{
    /// <summary>
    /// Fully inside boundary result.
    /// </summary>
    public static BoundaryResult FullyInside => new(true, false, false, 1.0);

    /// <summary>
    /// Fully outside boundary result.
    /// </summary>
    public static BoundaryResult FullyOutside => new(false, true, false, 0.0);
}
