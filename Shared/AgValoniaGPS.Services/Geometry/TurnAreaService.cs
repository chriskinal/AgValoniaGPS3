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
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Geometry
{
    /// <summary>
    /// Core turn area service.
    /// Handles turn area polygon testing - determines which boundary a point is inside.
    /// </summary>
    public class TurnAreaService : ITurnAreaService
    {
        /// <summary>
        /// Determine which turn area boundary a point is inside.
        /// </summary>
        /// <param name="turnLines">List of turn line polygons (outer boundary at index 0, inner boundaries at index > 0)</param>
        /// <param name="isDriveThru">List of drive-through flags for each boundary</param>
        /// <param name="point">Point to test</param>
        /// <returns>Boundary index (0 = outer, >0 = inner), or -1 if outside all boundaries</returns>
        public int IsPointInsideTurnArea(List<List<Vec3>> turnLines, List<bool> isDriveThru, Vec3 point)
        {
            if (turnLines == null || turnLines.Count == 0)
                return -1;

            // Check if point is inside outer boundary (index 0)
            if (GeometryMath.IsPointInPolygon(turnLines[0], point))
            {
                // Check inner boundaries (index > 0)
                for (int i = 1; i < turnLines.Count; i++)
                {
                    // Skip drive-through boundaries
                    if (isDriveThru != null && i < isDriveThru.Count && isDriveThru[i])
                        continue;

                    // If point is inside inner boundary, return that index
                    if (GeometryMath.IsPointInPolygon(turnLines[i], point))
                    {
                        return i;
                    }
                }

                // Point is inside outer boundary but not inside any inner boundaries
                return 0;
            }

            // Point is outside outer boundary
            return -1;
        }
    }
}
