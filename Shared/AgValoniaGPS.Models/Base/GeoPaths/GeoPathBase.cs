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

﻿namespace AgValoniaGPS.Models
{
    public abstract class GeoPathBase
    {
        public abstract int Count { get; }

        public abstract GeoCoord this[int i] { get; }

        public int GetClosestPointIndex(GeoCoord coord)
        {
            double closestDistanceSquared = double.MaxValue;
            int closestIndex = -1;
            for (int i = 0; i < Count; i++)
            {
                double distanceSquared = coord.DistanceSquared(this[i]);
                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestIndex = i;
                }
            }
            return closestIndex;
        }

    }
}
