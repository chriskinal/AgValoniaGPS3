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

﻿using System.Collections.Generic;

namespace AgValoniaGPS.Models
{
    public class RecordedPoint
    {

        public RecordedPoint(GeoCoordDir geoCoordDir, double speed, bool autoButtonState)
        {
            GeoCoordDir = geoCoordDir;
            Speed = speed;
            AutoButtonState = autoButtonState;
        }

        public GeoCoordDir GeoCoordDir { get; set; }
        public double Speed { get; set; }
        public bool AutoButtonState { get; set; }

        public GeoCoord AsGeoCoord => GeoCoordDir.Coord;
    }

}
