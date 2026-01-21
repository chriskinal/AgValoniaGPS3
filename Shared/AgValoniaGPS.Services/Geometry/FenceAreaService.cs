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
    /// Core fence area service.
    /// Handles fence area (field boundary) polygon testing - determines if a point is inside the valid field area.
    /// </summary>
    public class FenceAreaService : IFenceAreaService
    {
        /// <summary>
        /// Determine if a point is inside the fence area.
        /// Point must be inside outer boundary (index 0) and outside all inner boundaries (index > 0).
        /// </summary>
        /// <param name="fenceLines">List of fence line polygons (outer at index 0, inner at index > 0)</param>
        /// <param name="point">Point to test (Vec3)</param>
        /// <returns>True if point is safely inside outer and outside all inner boundaries</returns>
        public bool IsPointInsideFenceArea(List<List<Vec2>> fenceLines, Vec3 point)
        {
            if (fenceLines == null || fenceLines.Count == 0)
                return false;

            // Check if outer boundary has points
            if (fenceLines[0] == null || fenceLines[0].Count < 3)
                return false;

            // Must be inside outer boundary (index 0)
            if (GeometryMath.IsPointInPolygon(fenceLines[0], point))
            {
                // Check inner boundaries (index > 0)
                for (int i = 1; i < fenceLines.Count; i++)
                {
                    // If point is inside any inner boundary, it's not in valid fence area
                    if (GeometryMath.IsPointInPolygon(fenceLines[i], point))
                    {
                        return false;
                    }
                }
                // Point is inside outer boundary and outside all inner boundaries
                return true;
            }
            // Point is outside outer boundary
            return false;
        }

        /// <summary>
        /// Determine if a point is inside the fence area.
        /// Point must be inside outer boundary (index 0) and outside all inner boundaries (index > 0).
        /// </summary>
        /// <param name="fenceLines">List of fence line polygons (outer at index 0, inner at index > 0)</param>
        /// <param name="point">Point to test (Vec2)</param>
        /// <returns>True if point is safely inside outer and outside all inner boundaries</returns>
        public bool IsPointInsideFenceArea(List<List<Vec2>> fenceLines, Vec2 point)
        {
            if (fenceLines == null || fenceLines.Count == 0)
                return false;

            // Check if outer boundary has points
            if (fenceLines[0] == null || fenceLines[0].Count < 3)
                return false;

            // Must be inside outer boundary (index 0)
            if (GeometryMath.IsPointInPolygon(fenceLines[0], point))
            {
                // Check inner boundaries (index > 0)
                for (int i = 1; i < fenceLines.Count; i++)
                {
                    // If point is inside any inner boundary, it's not in valid fence area
                    if (GeometryMath.IsPointInPolygon(fenceLines[i], point))
                    {
                        return false;
                    }
                }
                // Point is inside outer boundary and outside all inner boundaries
                return true;
            }
            // Point is outside outer boundary
            return false;
        }
    }
}
