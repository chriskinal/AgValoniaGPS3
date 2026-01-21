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
    public struct ColorRgb
    {
        public ColorRgb(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public ColorRgb(float red, float green, float blue)
        {
            if (red < 0.0f || 1.0f < red) throw new ArgumentOutOfRangeException(nameof(red), "Argument out of range");
            if (green < 0.0f || 1.0f < green) throw new ArgumentOutOfRangeException(nameof(green), "Argument out of range");
            if (blue < 0.0f || 1.0f < blue) throw new ArgumentOutOfRangeException(nameof(blue), "Argument out of range");
            Red = ColorRgb.FloatToByte(red);
            Green = ColorRgb.FloatToByte(green);
            Blue = ColorRgb.FloatToByte(blue);
        }

        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }

        static private byte FloatToByte(float fraction)
        {
            return (byte)(255 * fraction);
        }
    }

}
