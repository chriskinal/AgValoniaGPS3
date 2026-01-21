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
    public struct ColorRgba
    {
        public ColorRgba(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public ColorRgba(ColorRgb colorRgb, float alpha)
        {
            if (alpha < 0.0f || 1.0f < alpha) throw new ArgumentOutOfRangeException(nameof(alpha), "Argument out of range");
            Red = colorRgb.Red;
            Green = colorRgb.Green;
            Blue = colorRgb.Blue;
            Alpha = ColorRgba.FloatToByte(alpha);
        }

        public ColorRgba(float red, float green, float blue, float alpha)
        {
            if (red < 0.0f || 1.0f < red) throw new ArgumentOutOfRangeException(nameof(red), "Argument out of range");
            if (green < 0.0f || 1.0f < green) throw new ArgumentOutOfRangeException(nameof(green), "Argument out of range");
            if (blue < 0.0f || 1.0f < blue) throw new ArgumentOutOfRangeException(nameof(blue), "Argument out of range");
            if (alpha < 0.0f || 1.0f < alpha) throw new ArgumentOutOfRangeException(nameof(alpha), "Argument out of range");
            Red = ColorRgba.FloatToByte(red);
            Green = ColorRgba.FloatToByte(green);
            Blue = ColorRgba.FloatToByte(blue);
            Alpha = ColorRgba.FloatToByte(alpha);
        }

        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
        public byte Alpha { get; }

        static private byte FloatToByte(float fraction)
        {
            return (byte)(255 * fraction);
        }

    }

}
