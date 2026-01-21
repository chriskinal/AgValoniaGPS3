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

using System;
using System.Collections.Generic;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Geometry
{
    /// <summary>
    /// Core turn line geometry service.
    /// Handles turn/headland line calculations: headings and spacing optimization.
    /// </summary>
    public class TurnLineService : ITurnLineService
    {
        private const double TwoPI = Math.PI * 2.0;

        /// <summary>
        /// Calculate headings for turn line points.
        /// Middle points use neighbors, first/last points use adjacent points.
        /// Adds duplicate first and last points with forward-looking headings.
        /// </summary>
        public List<Vec3> CalculateHeadings(List<Vec3> turnLine)
        {
            if (turnLine == null || turnLine.Count < 2)
                return turnLine;

            int cnt = turnLine.Count;
            Vec3[] arr = new Vec3[cnt];
            for (int i = 0; i < cnt; i++)
            {
                arr[i] = turnLine[i];
            }

            List<Vec3> result = new List<Vec3>();

            // First point (duplicate) - uses first->second heading
            Vec3 pt2 = arr[0];
            pt2.Heading = Math.Atan2(arr[1].Easting - arr[0].Easting, arr[1].Northing - arr[0].Northing);
            if (pt2.Heading < 0) pt2.Heading += TwoPI;
            result.Add(pt2);

            // First point (averaged) - uses last, first, second points
            Vec3 pt3 = arr[0];
            pt3.Heading = Math.Atan2(arr[1].Easting - arr[cnt - 1].Easting, arr[1].Northing - arr[cnt - 1].Northing);
            if (pt3.Heading < 0) pt3.Heading += TwoPI;
            result.Add(pt3);

            // Middle points - use neighbor averaging
            for (int i = 1; i < cnt - 1; i++)
            {
                pt3 = arr[i];
                pt3.Heading = Math.Atan2(arr[i + 1].Easting - arr[i - 1].Easting, arr[i + 1].Northing - arr[i - 1].Northing);
                if (pt3.Heading < 0) pt3.Heading += TwoPI;
                result.Add(pt3);
            }

            // Last point - uses last->previous heading
            pt2 = arr[cnt - 1];
            pt2.Heading = Math.Atan2(arr[cnt - 1].Easting - arr[cnt - 2].Easting, arr[cnt - 1].Northing - arr[cnt - 2].Northing);
            if (pt2.Heading < 0) pt2.Heading += TwoPI;
            result.Add(pt2);

            return result;
        }

        /// <summary>
        /// Fix turn line spacing:
        /// 1. Remove points too close to fence line
        /// 2. Add points where spacing is too large
        /// 3. Remove points where spacing is too small
        /// 4. Recalculate headings
        /// </summary>
        /// <param name="turnLine">Turn line points</param>
        /// <param name="fenceLine">Fence line points to check distance against</param>
        /// <param name="totalHeadWidth">Minimum distance from fence line</param>
        /// <param name="spacing">Target spacing between points</param>
        public List<Vec3> FixSpacing(List<Vec3> turnLine, List<Vec3> fenceLine, double totalHeadWidth, double spacing)
        {
            if (turnLine == null || turnLine.Count == 0)
                return turnLine;

            List<Vec3> result = new List<Vec3>(turnLine);

            double totalHeadWidthSq = totalHeadWidth * totalHeadWidth;
            double spacingSq = spacing * spacing;

            // Remove points too close to fence line
            if (fenceLine != null && fenceLine.Count > 0)
            {
                int lineCount = result.Count;
                for (int i = 0; i < fenceLine.Count; i++)
                {
                    for (int j = 0; j < lineCount; j++)
                    {
                        double distance = DistanceSquared(fenceLine[i], result[j]);
                        if (distance < (totalHeadWidthSq * 0.99))
                        {
                            result.RemoveAt(j);
                            lineCount = result.Count;
                            j = -1; // Restart inner loop
                        }
                    }
                }
            }

            // Add points where spacing is too large
            int bndCount = result.Count;
            for (int i = 0; i < bndCount; i++)
            {
                int j = i + 1;
                if (j == bndCount) j = 0;

                double distance = DistanceSquared(result[i], result[j]);
                if (distance > (spacingSq * 1.8))
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
            bndCount = result.Count;
            for (int i = 0; i < bndCount - 1; i++)
            {
                double distance = DistanceSquared(result[i], result[i + 1]);
                if (distance < spacingSq)
                {
                    result.RemoveAt(i + 1);
                    bndCount = result.Count;
                    i--;
                }
            }

            // Recalculate headings if points remain
            if (result.Count > 0)
            {
                result = CalculateHeadings(result);
            }

            return result;
        }

        /// <summary>
        /// Calculate squared distance between two Vec3 points.
        /// </summary>
        private double DistanceSquared(Vec3 a, Vec3 b)
        {
            double dEast = a.Easting - b.Easting;
            double dNorth = a.Northing - b.Northing;
            return (dEast * dEast) + (dNorth * dNorth);
        }
    }
}
