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
    public struct GeoCoord
    {
        public GeoCoord(double northing, double easting)
        {
            Northing = northing;
            Easting = easting;
        }

        public double Northing { get; }
        public double Easting { get; }

        public double DistanceSquared(GeoCoord coord2)
        {
            return new GeoDelta(this, coord2).LengthSquared;
        }

        public double Distance(GeoCoord coord2)
        {
            return new GeoDelta(this, coord2).Length;
        }

        public GeoCoord Min(GeoCoord bCoord)
        {
            return new GeoCoord(Math.Min(this.Northing, bCoord.Northing), Math.Min(this.Easting, bCoord.Easting));
        }

        public GeoCoord Max(GeoCoord bCoord)
        {
            return new GeoCoord(Math.Max(this.Northing, bCoord.Northing), Math.Max(this.Easting, bCoord.Easting));
        }

        public GeoArea TriangleArea(GeoCoord b, GeoCoord c)
        {
            // AbsoluteValue of (Ax(By-Cy) + Bx(Cy-Ay) + Cx(Ay-By)/2)

            double area2 =
                Easting * (b.Northing - c.Northing) +
                b.Easting * (c.Northing - Northing) +
                c.Easting * (Northing - b.Northing);
            return new GeoArea(Math.Abs(0.5 * area2));
        }

        public GeoArea QuadArea(GeoCoord q2, GeoCoord q3, GeoCoord q4)
        {
            return TriangleArea(q2, q3) + TriangleArea(q3, q4);
        }

        public static GeoDelta operator -(GeoCoord aCoord, GeoCoord bCoord)
        {
            return new GeoDelta(aCoord.Northing - bCoord.Northing, aCoord.Easting - bCoord.Easting);
        }

        public static GeoCoord operator +(GeoCoord coord, GeoDelta delta)
        {
            return new GeoCoord(coord.Northing + delta.NorthingDelta, coord.Easting + delta.EastingDelta);
        }

        public static GeoCoord operator -(GeoCoord coord, GeoDelta delta)
        {
            return new GeoCoord(coord.Northing - delta.NorthingDelta, coord.Easting - delta.EastingDelta);
        }

    }
}
