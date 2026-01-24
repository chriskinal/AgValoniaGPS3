using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Curve preprocessing utilities for guidance line preparation.
    /// Provides algorithms for spacing, interpolation, and heading calculation.
    /// </summary>
    public static class CurveProcessing
    {
        /// <summary>
        /// Full preprocessing pipeline: minimum spacing → interpolation → heading calculation
        /// </summary>
        /// <param name="points">Input curve points</param>
        /// <param name="minSpacing">Minimum distance between points (meters)</param>
        /// <param name="interpolationSpacing">Target spacing for interpolated points (meters)</param>
        /// <returns>Processed curve points with calculated headings</returns>
        public static List<Vec3> Preprocess(IReadOnlyList<Vec3> points, double minSpacing, double interpolationSpacing)
        {
            var result = EnsureMinimumSpacing(points, minSpacing);
            result = InterpolatePoints(result, interpolationSpacing);
            result = CalculateHeadings(result);
            return result;
        }

        /// <summary>
        /// Ensures minimum spacing between consecutive points by removing points that are too close.
        /// Always preserves the first and last points.
        /// </summary>
        /// <param name="points">Input points</param>
        /// <param name="minSpacing">Minimum allowed distance between points (meters)</param>
        /// <returns>Points with minimum spacing enforced</returns>
        public static List<Vec3> EnsureMinimumSpacing(IReadOnlyList<Vec3> points, double minSpacing)
        {
            if (points == null || points.Count < 2)
                return points != null ? new List<Vec3>(points) : new List<Vec3>();

            var spaced = new List<Vec3>(points.Count);
            Vec3 last = points[0];
            spaced.Add(last);

            double minSq = minSpacing * minSpacing;

            for (int i = 1; i < points.Count; i++)
            {
                double dx = points[i].Easting - last.Easting;
                double dy = points[i].Northing - last.Northing;
                if ((dx * dx + dy * dy) >= minSq)
                {
                    spaced.Add(points[i]);
                    last = points[i];
                }
            }

            // Always add the original last point if it's not already there
            Vec3 final = points[points.Count - 1];
            Vec3 compare = spaced[spaced.Count - 1];
            if (GeometryMath.DistanceSquared(final, compare) > 1e-10)
                spaced.Add(final);

            return spaced;
        }

        /// <summary>
        /// Interpolates additional points between existing points at fixed spacing.
        /// Creates a smoother curve with consistent point density.
        /// </summary>
        /// <param name="points">Input points</param>
        /// <param name="spacingMeters">Target spacing between interpolated points (meters)</param>
        /// <returns>Interpolated points with consistent spacing</returns>
        public static List<Vec3> InterpolatePoints(IReadOnlyList<Vec3> points, double spacingMeters)
        {
            if (points == null || points.Count < 2)
                return points != null ? new List<Vec3>(points) : new List<Vec3>();

            var result = new List<Vec3>(points.Count * 2);

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vec3 a = points[i];
                Vec3 b = points[i + 1];
                result.Add(a);

                double dx = b.Easting - a.Easting;
                double dy = b.Northing - a.Northing;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                int steps = (int)(distance / spacingMeters);
                for (int j = 1; j < steps; j++)
                {
                    double t = (double)j / steps;
                    double x = a.Easting + dx * t;
                    double y = a.Northing + dy * t;
                    result.Add(new Vec3(x, y, 0));
                }
            }

            result.Add(points[points.Count - 1]);
            return result;
        }

        /// <summary>
        /// Calculates heading angles for each point based on direction to next point.
        /// Last point uses the heading from the second-to-last segment.
        /// </summary>
        /// <param name="points">Input points (modified in place)</param>
        /// <returns>Points with calculated headings</returns>
        public static List<Vec3> CalculateHeadings(List<Vec3> points)
        {
            if (points == null || points.Count < 2) return points;

            for (int i = 0; i < points.Count - 1; i++)
            {
                double dx = points[i + 1].Easting - points[i].Easting;
                double dy = points[i + 1].Northing - points[i].Northing;
                double heading = Math.Atan2(dx, dy);
                if (heading < 0) heading += GeometryMath.twoPI;

                points[i] = new Vec3(points[i].Easting, points[i].Northing, heading);
            }

            // Copy last heading from the second-to-last point
            var last = points[points.Count - 1];
            double lastHeading = points[points.Count - 2].Heading;
            points[points.Count - 1] = new Vec3(last.Easting, last.Northing, lastHeading);

            return points;
        }

        /// <summary>
        /// Computes the circular mean of heading angles.
        /// Uses vector averaging to handle the circular nature of angles correctly.
        /// </summary>
        /// <param name="points">Points with heading values</param>
        /// <returns>Average heading in radians (0 to 2π)</returns>
        public static double ComputeAverageHeading(IReadOnlyList<Vec3> points)
        {
            if (points == null || points.Count == 0) return 0;

            double cx = 0, sy = 0;
            for (int i = 0; i < points.Count; i++)
            {
                cx += Math.Cos(points[i].Heading);
                sy += Math.Sin(points[i].Heading);
            }
            cx /= points.Count;
            sy /= points.Count;

            double avg = Math.Atan2(sy, cx);
            if (avg < 0) avg += GeometryMath.twoPI;
            return avg;
        }
    }
}
