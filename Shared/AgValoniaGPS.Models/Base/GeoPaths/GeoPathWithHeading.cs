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
    public class GeoPathWithHeading
    {
        private readonly GeoPath _path;
        private readonly List<GeoDir> _headings;

        public GeoPathWithHeading()
        {
            _path = new GeoPath();
            _headings = new List<GeoDir>();
        }

        public int Count => _path.Count;

        public GeoCoord this[int index]
        {
            get { return _path[index]; }
        }

        public void Add(GeoCoord geoCoord, GeoDir heading)
        {
            _path.Add(geoCoord);
            _headings.Add(heading);
        }

        public GeoDir GetHeading(int i)
        {
            return _headings[i];
        }

        public bool IsFarAwayFromPath(GeoCoord coord, double minDistSquare)
        {
            return _path.IsFarAwayFromPath(coord, minDistSquare);
        }
    }
}
