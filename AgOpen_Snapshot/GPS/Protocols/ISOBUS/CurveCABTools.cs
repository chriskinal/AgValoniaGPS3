using System;
using System.Collections.Generic;
using System.Linq;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for CurveProcessing from AgOpenGPS.Core.
    /// Delegates to Core implementation using implicit type conversions.
    /// </summary>
    public static class CurveCABTools
    {
        /// <summary>
        /// Full preprocessing pipeline: minimum spacing → interpolation → heading calculation
        /// </summary>
        public static List<vec3> Preprocess(List<vec3> points, double minSpacing, double interpolationSpacing)
        {
            // Convert to Core Vec3 using implicit conversion
            var corePoints = points.Select(p => (Vec3)p).ToList();

            // Use Core implementation
            var result = CurveProcessing.Preprocess(corePoints, minSpacing, interpolationSpacing);

            // Convert back to WinForms vec3
            return result.Select(p => (vec3)p).ToList();
        }

        /// <summary>
        /// Ensures minimum spacing between consecutive points
        /// </summary>
        public static List<vec3> MakePointMinimumSpacing(List<vec3> points, double minSpacing)
        {
            var corePoints = points.Select(p => (Vec3)p).ToList();
            var result = CurveProcessing.EnsureMinimumSpacing(corePoints, minSpacing);
            return result.Select(p => (vec3)p).ToList();
        }

        /// <summary>
        /// Interpolates additional points at fixed spacing
        /// </summary>
        public static List<vec3> InterpolatePoints(List<vec3> points, double spacingMeters)
        {
            var corePoints = points.Select(p => (Vec3)p).ToList();
            var result = CurveProcessing.InterpolatePoints(corePoints, spacingMeters);
            return result.Select(p => (vec3)p).ToList();
        }

        /// <summary>
        /// Calculates heading angles for each point
        /// </summary>
        public static List<vec3> CalculateHeadings(List<vec3> points)
        {
            // Core method modifies in place, so we need to work with a mutable list
            var corePoints = points.Select(p => (Vec3)p).ToList();
            var result = CurveProcessing.CalculateHeadings(corePoints);
            return result.Select(p => (vec3)p).ToList();
        }

        /// <summary>
        /// Computes circular mean of heading angles
        /// </summary>
        public static double ComputeAverageHeading(List<vec3> points)
        {
            var corePoints = points.Select(p => (Vec3)p).ToList();
            return CurveProcessing.ComputeAverageHeading(corePoints);
        }
    }
}
