using System;
using AgOpenGPS.Core.Models;

namespace AgOpenGPS.Core.Models.Base
{
    /// <summary>
    /// 3D vector with easting (X), northing (Y), and heading components
    /// </summary>
    public struct Vec3
    {
        public double Easting { get; set; }
        public double Northing { get; set; }
        public double Heading { get; set; }

        public Vec3(double easting, double northing, double heading)
        {
            Easting = easting;
            Northing = northing;
            Heading = heading;
        }

        public Vec3(GeoCoord geoCoord, double heading = 0)
        {
            Easting = geoCoord.Easting;
            Northing = geoCoord.Northing;
            Heading = heading;
        }

        public GeoCoord ToGeoCoord()
        {
            return new GeoCoord(Northing, Easting);
        }

        public Vec2 ToVec2()
        {
            return new Vec2(Easting, Northing);
        }

        public static Vec3 operator +(Vec3 lhs, Vec3 rhs)
        {
            return new Vec3(lhs.Easting + rhs.Easting, lhs.Northing + rhs.Northing, lhs.Heading);
        }

        public static Vec3 operator -(Vec3 lhs, Vec3 rhs)
        {
            return new Vec3(lhs.Easting - rhs.Easting, lhs.Northing - rhs.Northing, lhs.Heading);
        }

        public static bool operator ==(Vec3 lhs, Vec3 rhs)
        {
            return lhs.Easting == rhs.Easting && lhs.Northing == rhs.Northing && lhs.Heading == rhs.Heading;
        }

        public static bool operator !=(Vec3 lhs, Vec3 rhs)
        {
            return !(lhs == rhs);
        }

        public double GetLength()
        {
            return Math.Sqrt(Easting * Easting + Northing * Northing);
        }

        public override bool Equals(object obj)
        {
            if (obj is Vec3 other)
            {
                return this == other;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Easting.GetHashCode() ^ (Northing.GetHashCode() << 16) ^ (Heading.GetHashCode() << 8);
        }

        public override string ToString()
        {
            return $"({Easting:F3}, {Northing:F3}, {Heading:F3})";
        }
    }

    /// <summary>
    /// Vector for fix-to-fix calculations with distance and set flag
    /// </summary>
    public struct VecFix2Fix
    {
        public double Easting { get; set; }
        public double Distance { get; set; }
        public double Northing { get; set; }
        public bool IsSet { get; set; }

        public VecFix2Fix(double easting, double distance, double northing, bool isSet)
        {
            Easting = easting;
            Distance = distance;
            Northing = northing;
            IsSet = isSet;
        }

        public override string ToString()
        {
            return $"({Easting:F3}, {Northing:F3}, dist={Distance:F3}, set={IsSet})";
        }
    }
}
