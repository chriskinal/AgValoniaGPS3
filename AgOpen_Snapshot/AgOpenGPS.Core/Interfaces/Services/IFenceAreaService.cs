using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Interfaces.Services
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
