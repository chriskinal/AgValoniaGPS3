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
    public struct GeoBoundingBox
    {
        private GeoCoord _minCoord;
        private GeoCoord _maxCoord;

        static public GeoBoundingBox CreateEmpty()
        {
            GeoCoord minCoord = new GeoCoord(double.MaxValue, double.MaxValue);
            GeoCoord maxCoord = new GeoCoord(double.MinValue, double.MinValue);
            return new GeoBoundingBox(minCoord, maxCoord);
        }

        public GeoBoundingBox(GeoCoord minCoord, GeoCoord maxCoord)
        {
            _minCoord = minCoord;
            _maxCoord = maxCoord;
        }

        public bool IsEmpty =>
            _maxCoord.Northing < _minCoord.Northing &&
            _maxCoord.Easting < _minCoord.Easting;
        public double MinNorthing => _minCoord.Northing;
        public double MaxNorthing => _maxCoord.Northing;
        public double MinEasting => _minCoord.Easting;
        public double MaxEasting => _maxCoord.Easting;

        public void Include(GeoCoord geoCoord)
        {
            _minCoord = _minCoord.Min(geoCoord);
            _maxCoord = _maxCoord.Max(geoCoord);
        }

        public bool IsInside(GeoCoord testCoord)
        {
            return
                _minCoord.Northing <= testCoord.Northing && testCoord.Northing <= _maxCoord.Northing &&
                _minCoord.Easting <= testCoord.Easting && testCoord.Easting <= _maxCoord.Easting;
        }
    }

}
