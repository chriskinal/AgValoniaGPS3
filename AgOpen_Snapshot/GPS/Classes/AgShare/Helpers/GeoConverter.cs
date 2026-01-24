using System;
using System.Collections.Generic;
using System.Linq;
using AgOpenGPS.Core.Models.AgShare;
using AgOpenGPS.Core.Models.Base;
using GeoCore = AgOpenGPS.Core.Models.Base.GeoConversion;
using BoundaryCore = AgOpenGPS.Core.Models.Base.BoundaryUtils;
using CurveCore = AgOpenGPS.Core.Models.Base.CurveUtils;
using GeoCalc = AgOpenGPS.Core.Models.Base.GeoCalculations;

namespace AgOpenGPS.Classes.AgShare.Helpers
{
    /// <summary>
    /// WinForms wrapper for GeoConversion from AgOpenGPS.Core
    /// All coordinate conversion functions delegate to Core
    /// </summary>
    public class GeoConverter
    {
        private readonly GeoCore _coreConverter;

        public GeoConverter(double originLat, double originLon)
        {
            _coreConverter = new GeoCore(originLat, originLon);
        }

        /// <summary>
        /// Convert WGS84 lat/lon to local easting/northing
        /// Returns a simple struct for backward compatibility
        /// </summary>
        public (double Easting, double Northing) ToLocal(double lat, double lon)
        {
            var result = _coreConverter.ToLocal(lat, lon);
            return (result.Easting, result.Northing);
        }

        /// <summary>
        /// Calculate heading from two local coordinates (in radians, 0-2π)
        /// </summary>
        public static double HeadingFromPoints((double Easting, double Northing) a, (double Easting, double Northing) b)
        {
            var vecA = new Vec2(a.Easting, a.Northing);
            var vecB = new Vec2(b.Easting, b.Northing);
            return GeoCore.HeadingFromPoints(vecA, vecB);
        }
    }
    /// <summary>
    /// WinForms wrapper for BoundaryUtils from AgOpenGPS.Core
    /// </summary>
    public static class BoundaryHelper
    {
        /// <summary>
        /// Calculate heading for each boundary point based on the direction to the next point (last → first is closed loop)
        /// Delegates to Core BoundaryUtils
        /// </summary>
        public static List<LocalPoint> WithHeadings(List<LocalPoint> points)
        {
            if (points == null || points.Count < 2) return new List<LocalPoint>();

            // Convert to Core Vec3 and delegate
            var corePoints = points.Select(p => (Vec3)p).ToList();
            var result = BoundaryCore.WithHeadings(corePoints);

            // Convert back to LocalPoint
            return result.Select(v => (LocalPoint)v).ToList();
        }
    }

    /// <summary>
    /// WinForms wrapper for CurveUtils from AgOpenGPS.Core
    /// </summary>
    public static class CurveHelper
    {
        /// <summary>
        /// Calculate Heading for CurvePoints
        /// Delegates to Core CurveUtils
        /// </summary>
        public static List<vec3> CalculateHeadings(List<vec3> inputPoints)
        {
            if (inputPoints == null || inputPoints.Count < 2)
                return new List<vec3>();

            // Convert to Core Vec3 and delegate
            var corePoints = inputPoints.Select(p => (Vec3)p).ToList();
            var result = CurveCore.CalculateHeadings(corePoints);

            // Convert back to WinForms vec3
            return result.Select(v => (vec3)v).ToList();
        }
    }
    /// <summary>
    /// WinForms wrapper for GeoCalculations from AgOpenGPS.Core
    /// </summary>
    public static class GeoUtils
    {
        /// <summary>
        /// Calculates approximate area of a lat/lon polygon in hectares
        /// Delegates to Core GeoCalculations
        /// </summary>
        public static double CalculateAreaInHa(List<CoordinateDto> coords)
        {
            if (coords == null || coords.Count < 3)
                return 0;

            // Convert to Core coordinate format and delegate
            var coreCoords = coords.Select(c => (c.Latitude, c.Longitude)).ToList();
            return GeoCalc.CalculateAreaInHectares(coreCoords);
        }
    }
}
