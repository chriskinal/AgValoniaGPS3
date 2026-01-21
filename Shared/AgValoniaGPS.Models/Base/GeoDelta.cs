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
    public struct GeoDelta
    {
        public GeoDelta(GeoCoord fromCoord, GeoCoord toCoord)
        {
            NorthingDelta = toCoord.Northing - fromCoord.Northing;
            EastingDelta = toCoord.Easting - fromCoord.Easting;
        }

        public GeoDelta(double northingDelta, double eastingDelta)
        {
            NorthingDelta = northingDelta;
            EastingDelta = eastingDelta;
        }

        public double NorthingDelta { get; }
        public double EastingDelta { get; }

        public double LengthSquared => NorthingDelta * NorthingDelta + EastingDelta * EastingDelta;
        public double Length => Math.Sqrt(LengthSquared);

        public static GeoDelta operator *(double factor, GeoDelta delta)
        {
            return new GeoDelta(factor * delta.NorthingDelta, factor * delta.EastingDelta);
        }
    }
}
