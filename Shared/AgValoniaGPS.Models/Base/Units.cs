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
    public static class Units
    {
        public static double DegreesToRadians(double degrees)
        {
            const double degreesToRadians = Math.PI / 180.0;
            return degrees * degreesToRadians;
        }

        public static double RadiansToDegrees(double radians)
        {
            const double radiansToDegrees = 180.0 / Math.PI;
            return radians * radiansToDegrees;
        }

    }

    public class Distance
    {
        private const double metersToKilometers = 0.001;
        private const double milesToKilometers = 1.609344;
        private const double kilometersToMiles = 1 / milesToKilometers;
        private const double metersToFeet = 3.28;
        private const double metersToMiles = metersToKilometers * kilometersToMiles;

        private double _distanceInMeters;
        public Distance(double distanceInMeters)
        {
            _distanceInMeters = distanceInMeters;
        }

        public double InMeters => _distanceInMeters;
        public double InKilometers => _distanceInMeters * metersToKilometers;
        public double InMiles => _distanceInMeters * metersToMiles;
        public double InFeet => _distanceInMeters * metersToFeet;
    }

    public class Area
    {
        private const double _squareMetersToHectares = 0.0001;
        private const double _squareMetersToAcres = 0.000247105;

        private double _areaInSquareMeters;
        public Area(double areaInSquareMeters)
        {
            _areaInSquareMeters = areaInSquareMeters;
        }

        public double InSquareMeters => _areaInSquareMeters;
        public double InHectares => _squareMetersToHectares * _areaInSquareMeters;
        public double InAcres => _squareMetersToAcres * _areaInSquareMeters;
    }

}
