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
    public enum TurnType { Straight, Left, Right }

    public struct GeoCircle
    {

        public GeoCircle(GeoCoord center, double radius)
        {
            Center = center;
            Radius = radius;
        }

        public GeoCoord Center { get; }
        public double Radius { get; }

        public GeoCoord PointOnCircle(GeoDir dir)
        {
            return Center + Radius * dir;
        }

        public double GetArcLength(
            GeoCoord startPos,
            GeoCoord goalPos,
            TurnType turnType)
        {
            Debug.Assert(turnType != TurnType.Straight);
            GeoDir startDir = new GeoDir(startPos - Center);
            GeoDir goalDir = new GeoDir(goalPos - Center);

            double theta = goalDir.AngleInRadians - startDir.AngleInRadians;

            if (TurnType.Right == turnType)
            {
                if (theta < 0.0) theta += 2.0 * Math.PI;
            }
            else if (TurnType.Left == turnType)
            {
                if (theta > 0.0) theta -= 2.0 * Math.PI;
            }
            return Math.Abs(theta * Radius);
        }

    }
}
