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

using System;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Models.Base
{
    /// <summary>
    /// 2D vector with easting (X) and northing (Y) components
    /// </summary>
    public struct Vec2
    {
        public double Easting { get; set; }
        public double Northing { get; set; }

        public Vec2(double easting, double northing)
        {
            Easting = easting;
            Northing = northing;
        }

        public Vec2(GeoCoord geoCoord)
        {
            Easting = geoCoord.Easting;
            Northing = geoCoord.Northing;
        }

        public GeoCoord ToGeoCoord()
        {
            return new GeoCoord(Northing, Easting);
        }

        // Operators
        public static Vec2 operator +(Vec2 lhs, Vec2 rhs)
        {
            return new Vec2(lhs.Easting + rhs.Easting, lhs.Northing + rhs.Northing);
        }

        public static Vec2 operator -(Vec2 lhs, Vec2 rhs)
        {
            return new Vec2(lhs.Easting - rhs.Easting, lhs.Northing - rhs.Northing);
        }

        public static Vec2 operator -(Vec2 vec)
        {
            return new Vec2(-vec.Easting, -vec.Northing);
        }

        public static Vec2 operator *(Vec2 vec, double scalar)
        {
            return new Vec2(vec.Easting * scalar, vec.Northing * scalar);
        }

        public static Vec2 operator *(double scalar, Vec2 vec)
        {
            return new Vec2(vec.Easting * scalar, vec.Northing * scalar);
        }

        public static Vec2 operator /(Vec2 vec, double scalar)
        {
            return new Vec2(vec.Easting / scalar, vec.Northing / scalar);
        }

        public static bool operator ==(Vec2 lhs, Vec2 rhs)
        {
            return lhs.Easting == rhs.Easting && lhs.Northing == rhs.Northing;
        }

        public static bool operator !=(Vec2 lhs, Vec2 rhs)
        {
            return !(lhs == rhs);
        }

        // Vector operations
        public Vec2 Normalize()
        {
            double length = GetLength();
            if (length > 0)
            {
                return new Vec2(Easting / length, Northing / length);
            }
            return new Vec2(0, 0);
        }

        public double GetLength()
        {
            return Math.Sqrt(Easting * Easting + Northing * Northing);
        }

        public double GetLengthSquared()
        {
            return Easting * Easting + Northing * Northing;
        }

        public static double Dot(Vec2 a, Vec2 b)
        {
            return a.Easting * b.Easting + a.Northing * b.Northing;
        }

        public static double Cross(Vec2 a, Vec2 b)
        {
            return a.Easting * b.Northing - a.Northing * b.Easting;
        }

        public static Vec2 Lerp(Vec2 a, Vec2 b, double t)
        {
            return new Vec2(
                a.Easting + (b.Easting - a.Easting) * t,
                a.Northing + (b.Northing - a.Northing) * t);
        }

        /// <summary>
        /// Project this point onto a line segment defined by two points
        /// </summary>
        public static Vec2 ProjectOnSegment(Vec2 point, Vec2 segmentStart, Vec2 segmentEnd)
        {
            Vec2 segment = segmentEnd - segmentStart;
            double segmentLengthSquared = segment.GetLengthSquared();

            if (segmentLengthSquared == 0)
            {
                return segmentStart;
            }

            double t = Dot(point - segmentStart, segment) / segmentLengthSquared;
            t = Math.Max(0, Math.Min(1, t));

            return segmentStart + segment * t;
        }

        public override bool Equals(object obj)
        {
            if (obj is Vec2 other)
            {
                return this == other;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Easting.GetHashCode() ^ (Northing.GetHashCode() << 16);
        }

        public override string ToString()
        {
            return $"({Easting:F3}, {Northing:F3})";
        }
    }
}
