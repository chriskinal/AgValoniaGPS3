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
using System.Collections.ObjectModel;

namespace AgValoniaGPS.Models
{
    public class GeoPath : GeoPathBase
    {
        protected readonly List<GeoCoord> _coords;
        public GeoPath()
        {
            _coords = new List<GeoCoord>();
        }

        public override int Count => _coords.Count;
        public GeoCoord Last => _coords[_coords.Count - 1];
        public ReadOnlyCollection<GeoCoord> Coords => _coords.AsReadOnly();

        public override GeoCoord this[int index]
        {
            get { return _coords[index]; }
        }

        public void Clear()
        {
            _coords.Clear();
        }

        public virtual void Add(GeoCoord coord)
        {
            _coords.Add(coord);
        }

        public bool IsFarAwayFromPath(GeoCoord testCoord, double minimumDistanceSquared)
        {
            return _coords.TrueForAll(coordOnPath => coordOnPath.DistanceSquared(testCoord) >= minimumDistanceSquared);
        }

    }
}
