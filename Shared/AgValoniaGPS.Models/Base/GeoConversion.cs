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
    /// Converts between WGS84 lat/lon and local NE coordinates using CNMEA projection
    /// </summary>
    public class GeoConversion
    {
        private readonly double lat0Rad;
        private readonly double lat0Deg;
        private readonly double lon0;
        private readonly double metersPerDegLat;
        private readonly double metersPerDegLon;

        public double OriginLatitude => lat0Deg;
        public double OriginLongitude => lon0;

        public GeoConversion(double originLat, double originLon)
        {
            lat0Deg = originLat;
            lat0Rad = GeometryMath.ToRadians(originLat);
            lon0 = originLon;

            // CNMEA meters-per-degree conversion formulas
            metersPerDegLat = 111132.92
                            - 559.82 * Math.Cos(2 * lat0Rad)
                            + 1.175 * Math.Cos(4 * lat0Rad)
                            - 0.0023 * Math.Cos(6 * lat0Rad);

            metersPerDegLon = 111412.84 * Math.Cos(lat0Rad)
                            - 93.5 * Math.Cos(3 * lat0Rad)
                            + 0.118 * Math.Cos(5 * lat0Rad);
        }

        /// <summary>
        /// Convert WGS84 lat/lon to local easting/northing
        /// </summary>
        public Vec2 ToLocal(double lat, double lon)
        {
            double deltaLat = lat - lat0Deg;
            double deltaLon = lon - lon0;

            double northing = deltaLat * metersPerDegLat;
            double easting = deltaLon * metersPerDegLon;

            return new Vec2(easting, northing);
        }

        /// <summary>
        /// Convert local easting/northing back to WGS84 lat/lon
        /// </summary>
        public (double lat, double lon) ToWgs84(Vec2 local)
        {
            double lat = lat0Deg + (local.Northing / metersPerDegLat);
            double lon = lon0 + (local.Easting / metersPerDegLon);
            return (lat, lon);
        }

        /// <summary>
        /// Calculate heading from two local coordinates (in radians, 0-2π)
        /// </summary>
        public static double HeadingFromPoints(Vec2 a, Vec2 b)
        {
            double angle = Math.Atan2(b.Easting - a.Easting, b.Northing - a.Northing);
            return (angle + GeometryMath.twoPI) % GeometryMath.twoPI;
        }
    }

    /// <summary>
    /// Helper utilities for boundary operations
    /// </summary>
    public static class BoundaryUtils
    {
        /// <summary>
        /// Calculate heading for each boundary point based on the direction to the next point
        /// (last → first is closed loop)
        /// </summary>
        public static List<Vec3> WithHeadings(IReadOnlyList<Vec2> points)
        {
            var result = new List<Vec3>();
            if (points == null || points.Count < 2) return result;

            for (int i = 0; i < points.Count; i++)
            {
                var curr = points[i];
                var next = points[(i + 1) % points.Count]; // closed ring
                double dx = next.Easting - curr.Easting;
                double dy = next.Northing - curr.Northing;
                double heading = Math.Atan2(dx, dy);
                result.Add(new Vec3(curr.Easting, curr.Northing, heading));
            }

            return result;
        }

        /// <summary>
        /// Calculate heading for each boundary point based on the direction to the next point
        /// Updates existing Vec3 list with calculated headings
        /// </summary>
        public static List<Vec3> WithHeadings(IReadOnlyList<Vec3> points)
        {
            var result = new List<Vec3>();
            if (points == null || points.Count < 2) return result;

            for (int i = 0; i < points.Count; i++)
            {
                var curr = points[i];
                var next = points[(i + 1) % points.Count]; // closed ring
                double dx = next.Easting - curr.Easting;
                double dy = next.Northing - curr.Northing;
                double heading = Math.Atan2(dx, dy);
                result.Add(new Vec3(curr.Easting, curr.Northing, heading));
            }

            return result;
        }
    }

    /// <summary>
    /// Helper utilities for curve operations
    /// </summary>
    public static class CurveUtils
    {
        /// <summary>
        /// Calculate heading for curve points based on direction to next point
        /// Last point uses heading from previous segment
        /// </summary>
        public static List<Vec3> CalculateHeadings(IReadOnlyList<Vec2> inputPoints)
        {
            var output = new List<Vec3>();

            if (inputPoints == null || inputPoints.Count < 2)
                return output;

            for (int i = 0; i < inputPoints.Count - 1; i++)
            {
                var p1 = inputPoints[i];
                var p2 = inputPoints[i + 1];

                var dx = p2.Easting - p1.Easting;
                var dy = p2.Northing - p1.Northing;

                var heading = Math.Atan2(dx, dy);

                output.Add(new Vec3(p1.Easting, p1.Northing, heading));
            }

            // Last point uses heading from previous segment
            var last = inputPoints[inputPoints.Count - 1];
            var lastHeading = output[output.Count - 1].Heading;
            output.Add(new Vec3(last.Easting, last.Northing, lastHeading));

            return output;
        }

        /// <summary>
        /// Calculate heading for curve points (Vec3) based on direction to next point
        /// Last point uses heading from previous segment
        /// </summary>
        public static List<Vec3> CalculateHeadings(IReadOnlyList<Vec3> inputPoints)
        {
            var output = new List<Vec3>();

            if (inputPoints == null || inputPoints.Count < 2)
                return output;

            for (int i = 0; i < inputPoints.Count - 1; i++)
            {
                var p1 = inputPoints[i];
                var p2 = inputPoints[i + 1];

                var dx = p2.Easting - p1.Easting;
                var dy = p2.Northing - p1.Northing;

                var heading = Math.Atan2(dx, dy);

                output.Add(new Vec3(p1.Easting, p1.Northing, heading));
            }

            // Last point uses heading from previous segment
            var last = inputPoints[inputPoints.Count - 1];
            var lastHeading = output[output.Count - 1].Heading;
            output.Add(new Vec3(last.Easting, last.Northing, lastHeading));

            return output;
        }
    }

    /// <summary>
    /// Geographic calculation utilities
    /// </summary>
    public static class GeoCalculations
    {
        /// <summary>
        /// Calculates approximate area of a lat/lon polygon in hectares
        /// Uses the shoelace formula with lat/lon approximation
        /// </summary>
        public static double CalculateAreaInHectares(IReadOnlyList<(double lat, double lon)> coordinates)
        {
            if (coordinates == null || coordinates.Count < 3)
                return 0;

            double area = 0;
            for (int i = 0, j = coordinates.Count - 1; i < coordinates.Count; j = i++)
            {
                double xi = coordinates[i].lon;
                double yi = coordinates[i].lat;
                double xj = coordinates[j].lon;
                double yj = coordinates[j].lat;
                area += (xj + xi) * (yj - yi);
            }

            // Convert to hectares (111.32 km per degree latitude, squared for area)
            return Math.Abs(area * 111.32 * 111.32 / 2.0) / 10000.0;
        }

        /// <summary>
        /// Calculates area of a local plane polygon in hectares
        /// Coordinates should be in meters (easting, northing)
        /// </summary>
        public static double CalculateAreaInHectares(IReadOnlyList<Vec2> coordinates)
        {
            if (coordinates == null || coordinates.Count < 3)
                return 0;

            double area = 0;
            for (int i = 0, j = coordinates.Count - 1; i < coordinates.Count; j = i++)
            {
                area += (coordinates[j].Easting + coordinates[i].Easting)
                      * (coordinates[j].Northing - coordinates[i].Northing);
            }

            // Convert square meters to hectares
            return Math.Abs(area / 2.0) * 0.0001;
        }
    }
}
