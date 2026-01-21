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
    public struct GeoDir
    {
        public GeoDir(double angleInRadians)
        {
            AngleInRadians = angleInRadians;
        }

        public GeoDir(GeoDelta delta)
        {
            AngleInRadians = Math.Atan2(delta.EastingDelta, delta.NorthingDelta);
        }

        public double AngleInRadians { get; }
        public double AngleInDegrees => Units.RadiansToDegrees(AngleInRadians);

        public GeoDir PerpendicularLeft => new GeoDir(AngleInRadians - 0.5 * Math.PI);

        public GeoDir PerpendicularRight => new GeoDir(AngleInRadians + 0.5 * Math.PI);
        public GeoDir Inverted => new GeoDir(AngleInRadians + Math.PI);

        public static GeoDelta operator *(double distance, GeoDir dir)
        {
            return new GeoDelta(distance * Math.Cos(dir.AngleInRadians), distance * Math.Sin(dir.AngleInRadians));
        }

        public static GeoDir operator +(GeoDir dir, double angleInRadians)
        {
            return new GeoDir(dir.AngleInRadians + angleInRadians);
        }

        public static GeoDir operator -(GeoDir dir, double angleInRadians)
        {
            return new GeoDir(dir.AngleInRadians - angleInRadians);
        }

    }
}