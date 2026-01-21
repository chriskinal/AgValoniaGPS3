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

namespace AgValoniaGPS.Models.Base
{
    /// <summary>
    /// Geometry and mathematical utility functions for agricultural applications
    /// </summary>
    public static class GeometryMath
    {
        #region Unit Conversion Constants

        // Inches to meters
        public const double in2m = 0.0254;

        // Meters to inches
        public const double m2in = 39.3701;

        // Meters to feet
        public const double m2ft = 3.28084;

        // Feet to meters
        public const double ft2m = 0.3048;

        // Hectare to Acres
        public const double ha2ac = 2.47105;

        // Acres to Hectare
        public const double ac2ha = 0.404686;

        // Meters to Acres
        public const double m2ac = 0.000247105;

        // Meters to Hectare
        public const double m2ha = 0.0001;

        // Liters per hectare to US gal per acre
        public const double galAc2Lha = 9.35396;

        // US gal per acre to liters per hectare
        public const double LHa2galAc = 0.106907;

        // Liters to Gallons
        public const double L2Gal = 0.264172;

        // Gallons to Liters
        public const double Gal2L = 3.785412534258;

        // The pi's
        public const double twoPI = 6.28318530717958647692;
        public const double PIBy2 = 1.57079632679489661923;

        #endregion

        #region Angle Conversions

        public static double ToDegrees(double radians)
        {
            return radians * 57.295779513082325225835265587528;
        }

        public static double ToRadians(double degrees)
        {
            return degrees * 0.01745329251994329576923690768489;
        }

        /// <summary>
        /// Absolute angle difference (range 0–π)
        /// </summary>
        public static double AngleDiff(double angle1, double angle2)
        {
            double diff = Math.Abs(angle1 - angle2);
            if (diff > Math.PI) diff = twoPI - diff;
            return diff;
        }

        #endregion

        #region Point and Range Tests

        public static bool InRangeBetweenAB(double start_x, double start_y, double end_x, double end_y,
            double point_x, double point_y)
        {
            double dx = end_x - start_x;
            double dy = end_y - start_y;
            double innerProduct = (point_x - start_x) * dx + (point_y - start_y) * dy;
            return 0 <= innerProduct && innerProduct <= dx * dx + dy * dy;
        }

        public static bool IsPointInPolygon(IReadOnlyList<Vec3> polygon, Vec3 testPoint)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                if ((polygon[i].Easting < testPoint.Easting && polygon[j].Easting >= testPoint.Easting)
                    || (polygon[j].Easting < testPoint.Easting && polygon[i].Easting >= testPoint.Easting))
                {
                    if (polygon[i].Northing + (testPoint.Easting - polygon[i].Easting)
                        / (polygon[j].Easting - polygon[i].Easting) * (polygon[j].Northing - polygon[i].Northing)
                        < testPoint.Northing)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public static bool IsPointInPolygon(IReadOnlyList<Vec3> polygon, Vec2 testPoint)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                if ((polygon[i].Easting < testPoint.Easting && polygon[j].Easting >= testPoint.Easting)
                    || (polygon[j].Easting < testPoint.Easting && polygon[i].Easting >= testPoint.Easting))
                {
                    if (polygon[i].Northing + (testPoint.Easting - polygon[i].Easting)
                        / (polygon[j].Easting - polygon[i].Easting) * (polygon[j].Northing - polygon[i].Northing)
                        < testPoint.Northing)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public static bool IsPointInPolygon(IReadOnlyList<Vec2> polygon, Vec2 testPoint)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                if ((polygon[i].Easting < testPoint.Easting && polygon[j].Easting >= testPoint.Easting)
                    || (polygon[j].Easting < testPoint.Easting && polygon[i].Easting >= testPoint.Easting))
                {
                    if (polygon[i].Northing + (testPoint.Easting - polygon[i].Easting)
                        / (polygon[j].Easting - polygon[i].Easting) * (polygon[j].Northing - polygon[i].Northing)
                        < testPoint.Northing)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        public static bool IsPointInPolygon(IReadOnlyList<Vec2> polygon, Vec3 testPoint)
        {
            bool result = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                if ((polygon[i].Easting < testPoint.Easting && polygon[j].Easting >= testPoint.Easting)
                    || (polygon[j].Easting < testPoint.Easting && polygon[i].Easting >= testPoint.Easting))
                {
                    if (polygon[i].Northing + (testPoint.Easting - polygon[i].Easting)
                        / (polygon[j].Easting - polygon[i].Easting) * (polygon[j].Northing - polygon[i].Northing)
                        < testPoint.Northing)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        #endregion

        #region Catmull-Rom Spline

        /// <summary>
        /// Catmull-Rom interpoint spline calculation
        /// </summary>
        public static Vec3 Catmull(double t, Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3)
        {
            double tt = t * t;
            double ttt = tt * t;

            double q1 = -ttt + 2.0f * tt - t;
            double q2 = 3.0f * ttt - 5.0f * tt + 2.0f;
            double q3 = -3.0f * ttt + 4.0f * tt + t;
            double q4 = ttt - tt;

            double tx = 0.5f * (p0.Easting * q1 + p1.Easting * q2 + p2.Easting * q3 + p3.Easting * q4);
            double ty = 0.5f * (p0.Northing * q1 + p1.Northing * q2 + p2.Northing * q3 + p3.Northing * q4);

            return new Vec3(tx, ty, 0);
        }

        #endregion

        #region Distance Calculations

        public static double Distance(double east1, double north1, double east2, double north2)
        {
            return Math.Sqrt(
                Math.Pow(east1 - east2, 2)
                + Math.Pow(north1 - north2, 2));
        }

        public static double Distance(Vec2 first, Vec2 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(Vec2 first, Vec3 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(Vec3 first, Vec2 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(Vec3 first, Vec3 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(Vec2 first, double east, double north)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - east, 2)
                + Math.Pow(first.Northing - north, 2));
        }

        public static double Distance(Vec3 first, double east, double north)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - east, 2)
                + Math.Pow(first.Northing - north, 2));
        }

        public static double Distance(VecFix2Fix first, Vec2 second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        public static double Distance(VecFix2Fix first, VecFix2Fix second)
        {
            return Math.Sqrt(
                Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2));
        }

        #endregion

        #region Distance Squared (Optimized)

        /// <summary>
        /// Not normalized distance, no square root - faster for comparisons
        /// </summary>
        public static double DistanceSquared(double northing1, double easting1, double northing2, double easting2)
        {
            return Math.Pow(easting1 - easting2, 2) + Math.Pow(northing1 - northing2, 2);
        }

        public static double DistanceSquared(Vec3 first, Vec2 second)
        {
            return Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2);
        }

        public static double DistanceSquared(Vec2 first, Vec3 second)
        {
            return Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2);
        }

        public static double DistanceSquared(Vec3 first, Vec3 second)
        {
            return Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2);
        }

        public static double DistanceSquared(Vec2 first, Vec2 second)
        {
            return Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2);
        }

        public static double DistanceSquared(VecFix2Fix first, Vec2 second)
        {
            return Math.Pow(first.Easting - second.Easting, 2)
                + Math.Pow(first.Northing - second.Northing, 2);
        }

        #endregion

        #region Raycasting

        /// <summary>
        /// Performs a raycast from the origin point in the direction of the heading.
        /// Used for HeadlandProximity and the distance to the headland.
        /// </summary>
        public static Vec2? RaycastToPolygon(Vec3 origin, IReadOnlyList<Vec3> polygon)
        {
            Vec2 from = origin.ToVec2();
            Vec2 dir = new Vec2(Math.Sin(origin.Heading), Math.Cos(origin.Heading));

            double minDist = double.MaxValue;
            Vec2? closest = null;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vec2 p1 = polygon[i].ToVec2();
                Vec2 p2 = polygon[(i + 1) % polygon.Count].ToVec2();

                if (TryRaySegmentIntersection(from, dir, p1, p2, out Vec2 intersection))
                {
                    double dist = Distance(from, intersection);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = intersection;
                    }
                }
            }

            return closest;
        }

        public static bool TryRaySegmentIntersection(Vec2 rayOrigin, Vec2 rayDir, Vec2 segA, Vec2 segB, out Vec2 intersection)
        {
            intersection = new Vec2();

            double dx = segB.Easting - segA.Easting;
            double dy = segB.Northing - segA.Northing;

            double det = (-rayDir.Easting * dy + dx * rayDir.Northing);
            if (Math.Abs(det) < 1e-8) return false; // parallel

            double s = (-dy * (segA.Easting - rayOrigin.Easting) + dx * (segA.Northing - rayOrigin.Northing)) / det;
            double t = (rayDir.Easting * (segA.Northing - rayOrigin.Northing) - rayDir.Northing * (segA.Easting - rayOrigin.Easting)) / det;

            if (s >= 0 && t >= 0 && t <= 1)
            {
                intersection = new Vec2(rayOrigin.Easting + s * rayDir.Easting, rayOrigin.Northing + s * rayDir.Northing);
                return true;
            }

            return false;
        }

        #endregion

        #region Coordinate Transform (for Segment-Based Coverage)

        /// <summary>
        /// Transform a world point to local coordinates where the section
        /// center is at origin and section heading aligns with positive Y-axis.
        /// X-axis represents the section width (left = negative, right = positive).
        /// </summary>
        /// <param name="worldPoint">Point in world coordinates.</param>
        /// <param name="sectionCenter">Center of the section in world coordinates.</param>
        /// <param name="sectionHeading">Section heading in radians.</param>
        /// <returns>Point in local coordinates (X = across section, Y = along heading).</returns>
        public static Vec2 ToLocalCoords(Vec2 worldPoint, Vec2 sectionCenter, double sectionHeading)
        {
            double cos = Math.Cos(-sectionHeading);
            double sin = Math.Sin(-sectionHeading);

            double dx = worldPoint.Easting - sectionCenter.Easting;
            double dy = worldPoint.Northing - sectionCenter.Northing;

            return new Vec2(
                dx * cos - dy * sin,  // X in local coords (across section)
                dx * sin + dy * cos   // Y in local coords (along heading)
            );
        }

        /// <summary>
        /// Optimized transform using precomputed sin/cos values.
        /// Use this when transforming many points with the same heading.
        /// </summary>
        public static Vec2 ToLocalCoords(Vec2 worldPoint, Vec2 sectionCenter, double cos, double sin)
        {
            double dx = worldPoint.Easting - sectionCenter.Easting;
            double dy = worldPoint.Northing - sectionCenter.Northing;

            return new Vec2(dx * cos - dy * sin, dx * sin + dy * cos);
        }

        /// <summary>
        /// Find X coordinate where edge from p1 to p2 crosses a given Y threshold.
        /// Returns null if edge doesn't cross the threshold.
        /// Points are in local coordinates where Y is the heading direction.
        /// </summary>
        /// <param name="p1">First point of edge (in local coords).</param>
        /// <param name="p2">Second point of edge (in local coords).</param>
        /// <param name="yThreshold">Y value to check (0 = current position, positive = look-ahead).</param>
        /// <returns>X intercept or null if edge doesn't cross threshold.</returns>
        public static double? GetXInterceptAtY(Vec2 p1, Vec2 p2, double yThreshold = 0)
        {
            // Shift Y values relative to threshold
            double y1 = p1.Northing - yThreshold;
            double y2 = p2.Northing - yThreshold;

            // Both above or both below threshold? No crossing.
            if ((y1 > 0) == (y2 > 0) && y1 != 0 && y2 != 0)
                return null;

            // Avoid division by zero for horizontal edges
            double dy = y2 - y1;
            if (Math.Abs(dy) < 1e-10)
                return null;

            // Linear interpolation: find t where Y = yThreshold
            double t = -y1 / dy;
            return p1.Easting + t * (p2.Easting - p1.Easting);
        }

        /// <summary>
        /// Convenience overload for Y=0 (current section position).
        /// </summary>
        public static double? GetXInterceptAtYZero(Vec2 p1, Vec2 p2)
            => GetXInterceptAtY(p1, p2, 0);

        #endregion

        #region Interval Merging

        /// <summary>
        /// Merge overlapping or adjacent intervals into non-overlapping set.
        /// </summary>
        /// <param name="intervals">List of (start, end) intervals.</param>
        /// <returns>Merged non-overlapping intervals sorted by start.</returns>
        public static List<(double Start, double End)> MergeIntervals(List<(double Start, double End)> intervals)
        {
            if (intervals.Count == 0)
                return new List<(double, double)>();

            // Sort by start position
            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));

            var merged = new List<(double Start, double End)>();
            var current = intervals[0];

            for (int i = 1; i < intervals.Count; i++)
            {
                if (intervals[i].Start <= current.End)
                {
                    // Overlapping or adjacent - extend current
                    current.End = Math.Max(current.End, intervals[i].End);
                }
                else
                {
                    // Gap - save current, start new
                    merged.Add(current);
                    current = intervals[i];
                }
            }

            merged.Add(current);
            return merged;
        }

        #endregion
    }
}
