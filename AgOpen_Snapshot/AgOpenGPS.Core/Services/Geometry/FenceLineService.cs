using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Services.Geometry
{
    /// <summary>
    /// Core fence line geometry service.
    /// Handles boundary fence line calculations: headings, spacing, winding, and area.
    /// </summary>
    public class FenceLineService : IFenceLineService
    {
        private const double TwoPI = Math.PI * 2.0;

        /// <summary>
        /// Calculate headings for each fence line point based on neighboring points.
        /// First and last points use wrap-around neighbors.
        /// </summary>
        public List<Vec3> CalculateHeadings(List<Vec3> fenceLine)
        {
            if (fenceLine == null || fenceLine.Count < 3)
                return fenceLine;

            int cnt = fenceLine.Count;
            Vec3[] arr = new Vec3[cnt];
            for (int i = 0; i < cnt; i++)
            {
                arr[i] = fenceLine[i];
            }

            List<Vec3> result = new List<Vec3>(cnt);

            // First point uses last, first, second points
            Vec3 pt3 = arr[0];
            pt3.Heading = Math.Atan2(arr[1].Easting - arr[cnt - 1].Easting, arr[1].Northing - arr[cnt - 1].Northing);
            if (pt3.Heading < 0) pt3.Heading += TwoPI;
            result.Add(pt3);

            // Middle points
            for (int i = 1; i < cnt - 1; i++)
            {
                pt3 = arr[i];
                pt3.Heading = Math.Atan2(arr[i + 1].Easting - arr[i - 1].Easting, arr[i + 1].Northing - arr[i - 1].Northing);
                if (pt3.Heading < 0) pt3.Heading += TwoPI;
                result.Add(pt3);
            }

            // Last point uses last, first, second-to-last points
            pt3 = arr[cnt - 1];
            pt3.Heading = Math.Atan2(arr[0].Easting - arr[cnt - 2].Easting, arr[0].Northing - arr[cnt - 2].Northing);
            if (pt3.Heading < 0) pt3.Heading += TwoPI;
            result.Add(pt3);

            return result;
        }

        /// <summary>
        /// Fix fence line spacing by adding/removing points.
        /// Also creates simplified line for ear clipping triangulation.
        /// </summary>
        /// <param name="fenceLine">Fence line points</param>
        /// <param name="area">Boundary area (affects spacing)</param>
        /// <param name="boundaryIndex">0 for outer, >0 for inner</param>
        /// <param name="fenceLineEar">Output: simplified line for triangulation</param>
        public List<Vec3> FixSpacing(List<Vec3> fenceLine, double area, int boundaryIndex, out List<Vec2> fenceLineEar)
        {
            fenceLineEar = new List<Vec2>();

            if (fenceLine == null || fenceLine.Count < 3)
                return fenceLine;

            // Determine spacing based on area
            double spacing;
            if (area < 200000) spacing = 1.1;
            else if (area < 400000) spacing = 2.2;
            else spacing = 3.3;

            if (boundaryIndex > 0) spacing *= 0.5;

            List<Vec3> result = new List<Vec3>(fenceLine);
            int bndCount = result.Count;
            double distance;

            // Add points where spacing is too large (first pass: 1.5x spacing)
            for (int i = 0; i < bndCount; i++)
            {
                int j = i + 1;
                if (j == bndCount) j = 0;

                distance = Distance(result[i], result[j]);
                if (distance > spacing * 1.5)
                {
                    Vec3 pointB = new Vec3(
                        (result[i].Easting + result[j].Easting) / 2.0,
                        (result[i].Northing + result[j].Northing) / 2.0,
                        result[i].Heading);

                    result.Insert(j, pointB);
                    bndCount = result.Count;
                    i--;
                }
            }

            // Add points where spacing is too large (second pass: 1.6x spacing)
            bndCount = result.Count;
            for (int i = 0; i < bndCount; i++)
            {
                int j = i + 1;
                if (j == bndCount) j = 0;

                distance = Distance(result[i], result[j]);
                if (distance > spacing * 1.6)
                {
                    Vec3 pointB = new Vec3(
                        (result[i].Easting + result[j].Easting) / 2.0,
                        (result[i].Northing + result[j].Northing) / 2.0,
                        result[i].Heading);

                    result.Insert(j, pointB);
                    bndCount = result.Count;
                    i--;
                }
            }

            // Remove points where spacing is too small
            spacing *= 0.9;
            bndCount = result.Count;
            for (int i = 0; i < bndCount - 1; i++)
            {
                distance = Distance(result[i], result[i + 1]);
                if (distance < spacing)
                {
                    result.RemoveAt(i + 1);
                    bndCount = result.Count;
                    i--;
                }
            }

            // Recalculate headings
            result = CalculateHeadings(result);

            // Create simplified line for ear clipping triangulation
            double delta = 0;
            for (int i = 0; i < result.Count; i++)
            {
                if (i == 0)
                {
                    fenceLineEar.Add(new Vec2(result[i].Easting, result[i].Northing));
                    continue;
                }
                delta += (result[i - 1].Heading - result[i].Heading);
                if (Math.Abs(delta) > 0.005)
                {
                    fenceLineEar.Add(new Vec2(result[i].Easting, result[i].Northing));
                    delta = 0;
                }
            }

            return result;
        }

        /// <summary>
        /// Reverse fence line winding direction.
        /// </summary>
        public List<Vec3> ReverseWinding(List<Vec3> fenceLine)
        {
            if (fenceLine == null || fenceLine.Count < 2)
                return fenceLine;

            int cnt = fenceLine.Count;
            Vec3[] arr = new Vec3[cnt];
            for (int i = 0; i < cnt; i++)
            {
                arr[i] = fenceLine[i];
            }

            List<Vec3> result = new List<Vec3>(cnt);
            for (int i = cnt - 1; i >= 0; i--)
            {
                Vec3 pt = arr[i];
                pt.Heading -= Math.PI;
                if (pt.Heading < 0) pt.Heading += TwoPI;
                result.Add(pt);
            }

            return result;
        }

        /// <summary>
        /// Calculate fence area and ensure correct winding.
        /// Outer boundaries (idx=0) should be counter-clockwise.
        /// Inner boundaries (idx>0) should be clockwise.
        /// </summary>
        /// <param name="fenceLine">Fence line points</param>
        /// <param name="boundaryIndex">0 for outer, >0 for inner</param>
        /// <param name="area">Output: calculated area</param>
        /// <returns>Modified fence line with correct winding</returns>
        public List<Vec3> CalculateAreaAndFixWinding(List<Vec3> fenceLine, int boundaryIndex, out double area)
        {
            area = 0;

            if (fenceLine == null || fenceLine.Count < 3)
                return fenceLine;

            int ptCount = fenceLine.Count;
            int j = ptCount - 1;

            // Calculate signed area
            for (int i = 0; i < ptCount; j = i++)
            {
                area += (fenceLine[j].Easting + fenceLine[i].Easting) * (fenceLine[j].Northing - fenceLine[i].Northing);
            }

            bool isClockwise = area >= 0;
            area = Math.Abs(area / 2);

            // Fix winding if needed
            // Outer boundary (idx=0) should be counter-clockwise (negative signed area before abs)
            // Inner boundary (idx>0) should be clockwise (positive signed area before abs)
            if ((boundaryIndex == 0 && isClockwise) || (boundaryIndex > 0 && !isClockwise))
            {
                return ReverseWinding(fenceLine);
            }

            return fenceLine;
        }

        /// <summary>
        /// Calculate distance between two Vec3 points.
        /// </summary>
        private double Distance(Vec3 a, Vec3 b)
        {
            double dEast = a.Easting - b.Easting;
            double dNorth = a.Northing - b.Northing;
            return Math.Sqrt(dEast * dEast + dNorth * dNorth);
        }
    }
}
