using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.AgShare
{
    /// <summary>
    /// Local representation of a field after geo-conversion from WGS84.
    /// Contains field boundaries and guidance lines in a local coordinate system.
    /// </summary>
    public class LocalFieldModel
    {
        /// <summary>
        /// Unique identifier for the field
        /// </summary>
        public Guid FieldId { get; set; }

        /// <summary>
        /// Field name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Field origin point (StartFix) in WGS84 coordinates
        /// </summary>
        public Wgs84 Origin { get; set; }

        /// <summary>
        /// Field boundaries in local plane coordinates.
        /// First list is outer boundary, subsequent lists are holes.
        /// </summary>
        public List<List<LocalPoint>> Boundaries { get; set; } = new List<List<LocalPoint>>();

        /// <summary>
        /// AB guidance lines in local plane coordinates
        /// </summary>
        public List<AbLineLocal> AbLines { get; set; } = new List<AbLineLocal>();
    }

    /// <summary>
    /// Point in local plane coordinate system (easting/northing).
    /// Similar to Vec3 but specifically for field representation.
    /// </summary>
    public struct LocalPoint
    {
        /// <summary>
        /// Easting coordinate in meters
        /// </summary>
        public double Easting { get; set; }

        /// <summary>
        /// Northing coordinate in meters
        /// </summary>
        public double Northing { get; set; }

        /// <summary>
        /// Heading in radians (0 to 2π)
        /// </summary>
        public double Heading { get; set; }

        public LocalPoint(double easting, double northing, double heading = 0)
        {
            Easting = easting;
            Northing = northing;
            Heading = heading;
        }

        /// <summary>
        /// Converts LocalPoint to Vec3 for geometric calculations
        /// </summary>
        public Vec3 ToVec3()
        {
            return new Vec3(Easting, Northing, Heading);
        }

        /// <summary>
        /// Creates LocalPoint from Vec3
        /// </summary>
        public static LocalPoint FromVec3(Vec3 vec)
        {
            return new LocalPoint(vec.Easting, vec.Northing, vec.Heading);
        }

        /// <summary>
        /// Implicit conversion to Vec3 for seamless interoperability
        /// </summary>
        public static implicit operator Vec3(LocalPoint point)
        {
            return new Vec3(point.Easting, point.Northing, point.Heading);
        }

        /// <summary>
        /// Implicit conversion from Vec3
        /// </summary>
        public static implicit operator LocalPoint(Vec3 vec)
        {
            return new LocalPoint(vec.Easting, vec.Northing, vec.Heading);
        }
    }

    /// <summary>
    /// AB guidance line representation in local plane coordinates
    /// </summary>
    public class AbLineLocal
    {
        /// <summary>
        /// Line name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Point A in local coordinates
        /// </summary>
        public LocalPoint PtA { get; set; }

        /// <summary>
        /// Point B in local coordinates
        /// </summary>
        public LocalPoint PtB { get; set; }

        /// <summary>
        /// Line heading in radians (0 to 2π)
        /// </summary>
        public double Heading { get; set; }

        /// <summary>
        /// Optional curve points (only populated for curved AB lines)
        /// </summary>
        public List<LocalPoint> CurvePoints { get; set; } = new List<LocalPoint>();
    }
}
