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
using System.Diagnostics;

namespace AgValoniaGPS.Models
{
    public class DebugAsserts
    {
        public static void AreEqual(double a, double b, double epsilon = 0.0001)
        {
            Debug.Assert(b < a + epsilon);
            Debug.Assert(a < b + epsilon);
        }

        public static void AreEqual(bool a, bool b)
        {
            Debug.Assert(a == b);
        }

        public static void AreEqual(GeoCoord a, GeoCoord b)
        {
            AreEqual(a.Northing, b.Northing);
            AreEqual(a.Easting, b.Easting);
        }

        public static void AreEqual(GeoDir a, GeoDir b)
        {
            AreEqual(Math.Cos(a.AngleInRadians), Math.Cos(b.AngleInRadians));
            AreEqual(Math.Sin(a.AngleInRadians), Math.Sin(b.AngleInRadians));
        }

        public static void AreEqual(GeoPath a, GeoPath b)
        {
            DebugAsserts.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                DebugAsserts.AreEqual(a[i], b[i]);
            }
        }

        public static void AreEqual(GeoPolygon a, GeoPolygon b)
        {
            DebugAsserts.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                DebugAsserts.AreEqual(a[i], b[i]);
            }
        }
    }
}
