using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for worked area calculations.
    /// </summary>
    public interface IWorkedAreaService
    {
        /// <summary>
        /// Calculate the area of two triangles in a triangle strip.
        /// </summary>
        /// <param name="points">Array of points (must have at least startIndex + 4 elements)</param>
        /// <param name="startIndex">Starting index in the points array</param>
        /// <returns>Total area of the two triangles in square meters</returns>
        double CalculateTriangleStripArea(Vec3[] points, int startIndex);

        /// <summary>
        /// Calculate the area of a single triangle using three points.
        /// </summary>
        /// <param name="p1">First point</param>
        /// <param name="p2">Second point</param>
        /// <param name="p3">Third point</param>
        /// <returns>Area of the triangle in square meters</returns>
        double CalculateTriangleArea(Vec3 p1, Vec3 p2, Vec3 p3);
    }
}
