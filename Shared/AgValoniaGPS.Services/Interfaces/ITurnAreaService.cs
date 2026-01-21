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
    /// Service for turn area polygon testing.
    /// </summary>
    public interface ITurnAreaService
    {
        /// <summary>
        /// Determine which turn area boundary a point is inside.
        /// </summary>
        /// <param name="turnLines">List of turn line polygons (outer boundary at index 0, inner boundaries at index > 0)</param>
        /// <param name="isDriveThru">List of drive-through flags for each boundary</param>
        /// <param name="point">Point to test</param>
        /// <returns>Boundary index (0 = outer, >0 = inner), or -1 if outside all boundaries</returns>
        int IsPointInsideTurnArea(List<List<Vec3>> turnLines, List<bool> isDriveThru, Vec3 point);
    }
}
