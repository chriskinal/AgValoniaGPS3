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

﻿using AgValoniaGPS.Models;

using System.Collections.Generic;

namespace AgValoniaGPS.Models
{
    public class GeoPolygonWithHeading
    {
        private readonly GeoPolygon _polygon;
        private readonly List<GeoDir> _headings;

        public GeoPolygonWithHeading()
        {
            _polygon = new GeoPolygon();
            _headings = new List<GeoDir>();
        }

        public int Count => _polygon.Count;
        public double Area => _polygon.Area;

        public GeoCoord this[int index]
        {
            get { return _polygon[index]; }
        }

        public void Add(GeoCoord geoCoord, GeoDir heading)
        {
            _polygon.Add(geoCoord);
            _headings.Add(heading);
        }

        public GeoDir GetHeading(int i)
        {
            return _headings[i];
        }

        public bool IsInside(GeoCoord coord)
        {
            return _polygon.IsInside(coord);
        }

        public bool IsFarAwayFromPath(GeoCoord coord, double dstSquared)
        {
            return _polygon.IsFarAwayFromPath(coord, dstSquared);
        }

    }

}
