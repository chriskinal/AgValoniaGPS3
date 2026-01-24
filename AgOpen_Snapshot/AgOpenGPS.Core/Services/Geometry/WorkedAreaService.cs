using System;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Services.Geometry
{
    /// <summary>
    /// Core worked area calculation service.
    /// Provides triangle area calculations for coverage mapping.
    /// </summary>
    public class WorkedAreaService : Interfaces.Services.IWorkedAreaService
    {
        /// <summary>
        /// Calculate the area of two triangles in a triangle strip.
        /// Uses the shoelace formula: Area = |Ax(By-Cy) + Bx(Cy-Ay) + Cx(Ay-By)| / 2
        /// </summary>
        /// <param name="points">Array of points (must have at least startIndex + 4 elements)</param>
        /// <param name="startIndex">Starting index in the points array (typically points.Count - 3)</param>
        /// <returns>Total area of the two triangles in square meters</returns>
        public double CalculateTriangleStripArea(Vec3[] points, int startIndex)
        {
            if (points == null || points.Length < startIndex + 4)
                return 0;

            int c = startIndex + 3; // Last point index (c-3, c-2, c-1, c form 4 points = 2 triangles)

            // Calculate area of first triangle (points: c, c-1, c-2)
            double area1 = Math.Abs(
                (points[c].Easting * (points[c - 1].Northing - points[c - 2].Northing))
                + (points[c - 1].Easting * (points[c - 2].Northing - points[c].Northing))
                + (points[c - 2].Easting * (points[c].Northing - points[c - 1].Northing)));

            // Calculate area of second triangle (points: c-1, c-2, c-3)
            double area2 = Math.Abs(
                (points[c - 1].Easting * (points[c - 2].Northing - points[c - 3].Northing))
                + (points[c - 2].Easting * (points[c - 3].Northing - points[c - 1].Northing))
                + (points[c - 3].Easting * (points[c - 1].Northing - points[c - 2].Northing)));

            // Return total area (formula includes * 0.5, but we apply it once to the sum)
            return (area1 + area2) * 0.5;
        }

        /// <summary>
        /// Calculate the area of a single triangle using three points.
        /// Uses the shoelace formula: Area = |Ax(By-Cy) + Bx(Cy-Ay) + Cx(Ay-By)| / 2
        /// </summary>
        /// <param name="p1">First point</param>
        /// <param name="p2">Second point</param>
        /// <param name="p3">Third point</param>
        /// <returns>Area of the triangle in square meters</returns>
        public double CalculateTriangleArea(Vec3 p1, Vec3 p2, Vec3 p3)
        {
            double area = Math.Abs(
                (p1.Easting * (p2.Northing - p3.Northing))
                + (p2.Easting * (p3.Northing - p1.Northing))
                + (p3.Easting * (p1.Northing - p2.Northing)));

            return area * 0.5;
        }
    }
}
