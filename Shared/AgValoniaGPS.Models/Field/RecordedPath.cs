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

    public class RecordedPath : GeoPathBase
    {
        public List<RecordedPoint> PointList = new List<RecordedPoint>();

        public override int Count
        {
            get { return PointList.Count; }
        }

        public override GeoCoord this[int index]
        {
            get { return PointList[index].AsGeoCoord; }
        }

        public void Clear()
        {
            PointList.Clear();
        }

        public void Add(RecordedPoint point)
        {
            PointList.Add(point);
        }
    }

}
