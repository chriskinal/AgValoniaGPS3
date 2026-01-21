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

namespace AgValoniaGPS.Services.Interfaces
{
    /// <summary>
    /// Service for fence area (field boundary) polygon testing.
    /// </summary>
    public interface IFenceAreaService
    {
        /// <summary>
        /// Determine if a point is inside the fence area (field boundary).
        /// Point must be inside outer boundary and outside all inner boundaries.
        /// </summary>
        /// <param name="fenceLines">List of fence line polygons (outer boundary at index 0, inner boundaries at index > 0)</param>
        /// <param name="point">Point to test (Vec3)</param>
        /// <returns>True if point is safely inside outer boundary and outside all inner boundaries</returns>
        bool IsPointInsideFenceArea(List<List<Vec2>> fenceLines, Vec3 point);

        /// <summary>
        /// Determine if a point is inside the fence area (field boundary).
        /// Point must be inside outer boundary and outside all inner boundaries.
        /// </summary>
        /// <param name="fenceLines">List of fence line polygons (outer boundary at index 0, inner boundaries at index > 0)</param>
        /// <param name="point">Point to test (Vec2)</param>
        /// <returns>True if point is safely inside outer boundary and outside all inner boundaries</returns>
        bool IsPointInsideFenceArea(List<List<Vec2>> fenceLines, Vec2 point);
    }
}
