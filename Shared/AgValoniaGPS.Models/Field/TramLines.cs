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
    public class TramLines
    {
        public TramLines()
        {
            TramList = new List<GeoPath>();
        }

        public GeoPolygon OuterTrack { get; set; }

        public GeoPolygon InnerTrack { get; set; }

        public List<GeoPath> TramList { get; set; }

        public void Clear()
        {
            OuterTrack = null;
            InnerTrack = null;
            TramList.Clear();
        }

        public bool IsEmpty => 0 == TramList.Count && null == OuterTrack;

    }

}
