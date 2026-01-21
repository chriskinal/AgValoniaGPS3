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
    public class HeadPath
    {
        public HeadPath(string name, double moveDistance, int mode, int aPoint)
        {
            Path = new GeoPathWithHeading();
            Name = name;
            MoveDistance = moveDistance;
            Mode = mode;
            APoint = aPoint;
        }

        public GeoPathWithHeading Path { get; }
        public string Name { get; }
        public double MoveDistance { get; }
        public int Mode { get; }
        public int APoint { get; }
    }

}
