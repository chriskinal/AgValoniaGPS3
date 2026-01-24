using System.Collections.Generic;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Services.Geometry
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
