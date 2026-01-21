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

﻿using System;

namespace AgValoniaGPS.Models
{
    // Represents a coordinate in the World Geodetic System 1984
    public struct Wgs84
    {
        const double EarthRadiusInMeters = 6371 * 1000.0;
        public Wgs84(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; }
        public double Longitude { get; }

        public double DistanceInMeters(Wgs84 b)
        {
            double aLatRad = Units.DegreesToRadians(Latitude);
            double aLongRad = Units.DegreesToRadians(Longitude);
            double bLatRad = Units.DegreesToRadians(b.Latitude);
            double bLongRad = Units.DegreesToRadians(b.Longitude);
            double sinHalfLongDelta = Math.Sin(0.5 * (bLongRad - aLongRad));
            double sinHalfLatDelta = Math.Sin(0.5 * (bLatRad - aLatRad));

            double d3 = sinHalfLatDelta * sinHalfLatDelta + Math.Cos(aLatRad) * Math.Cos(bLatRad) * sinHalfLongDelta * sinHalfLongDelta;
            return EarthRadiusInMeters * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
        }

        public double DistanceInKiloMeters(Wgs84 b)
        {
            return 0.001 * DistanceInMeters(b);
        }

        public Wgs84 CalculateNewPostionFromBearingDistance(double bearing, double distanceInMeters)
        {
            double latRadians = Units.DegreesToRadians(Latitude);
            double lonRadians = Units.DegreesToRadians(Longitude);

            double R = distanceInMeters / EarthRadiusInMeters;

            double lat2 = Math.Asin((Math.Sin(latRadians) * Math.Cos(R)) + (Math.Cos(latRadians) * Math.Sin(R) * Math.Cos(bearing)));
            double lon2 = lonRadians + Math.Atan2(Math.Sin(bearing) * Math.Sin(R) * Math.Cos(latRadians), Math.Cos(R) - (Math.Sin(latRadians) * Math.Sin(lat2)));

            return new Wgs84(Units.RadiansToDegrees(lat2), Units.RadiansToDegrees(lon2));
        }
    }

}
