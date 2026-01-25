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
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Guidance
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
        /// Smooths a curve using Catmull-Rom spline interpolation.
        /// Creates a smooth curve that passes through all control points.
        /// </summary>
        /// <param name="controlPoints">Control points to smooth through</param>
        /// <param name="pointsPerSegment">Number of interpolated points per segment (default 10)</param>
        /// <returns>Smoothed curve points</returns>
        public static List<Vec3> SmoothWithCatmullRom(IReadOnlyList<Vec3> controlPoints, int pointsPerSegment = 10)
        {
            if (controlPoints == null || controlPoints.Count < 2)
                return controlPoints != null ? new List<Vec3>(controlPoints) : new List<Vec3>();

            // Need at least 2 points for a curve
            if (controlPoints.Count == 2)
                return new List<Vec3>(controlPoints);

            var result = new List<Vec3>();

            // For Catmull-Rom, we need 4 points per segment (p0, p1, p2, p3)
            // The curve is drawn between p1 and p2
            // For endpoints, we extrapolate virtual points

            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                // Get the 4 control points for this segment
                Vec3 p0 = i > 0 ? controlPoints[i - 1] : ExtrapolatePoint(controlPoints[1], controlPoints[0]);
                Vec3 p1 = controlPoints[i];
                Vec3 p2 = controlPoints[i + 1];
                Vec3 p3 = i + 2 < controlPoints.Count ? controlPoints[i + 2] : ExtrapolatePoint(controlPoints[controlPoints.Count - 2], controlPoints[controlPoints.Count - 1]);

                // Add interpolated points for this segment
                for (int j = 0; j < pointsPerSegment; j++)
                {
                    double t = (double)j / pointsPerSegment;
                    var point = CatmullRomPoint(p0, p1, p2, p3, t);
                    result.Add(point);
                }
            }

            // Add the final point
            result.Add(controlPoints[controlPoints.Count - 1]);

            return result;
        }

        /// <summary>
        /// Extrapolates a virtual point beyond the endpoint for Catmull-Rom.
        /// </summary>
        private static Vec3 ExtrapolatePoint(Vec3 from, Vec3 to)
        {
            double dx = to.Easting - from.Easting;
            double dy = to.Northing - from.Northing;
            return new Vec3(to.Easting + dx, to.Northing + dy, to.Heading);
        }

        /// <summary>
        /// Calculates a point on a Catmull-Rom spline segment.
        /// </summary>
        /// <param name="p0">Control point before segment start</param>
        /// <param name="p1">Segment start point</param>
        /// <param name="p2">Segment end point</param>
        /// <param name="p3">Control point after segment end</param>
        /// <param name="t">Parameter (0 to 1)</param>
        /// <returns>Interpolated point on the curve</returns>
        private static Vec3 CatmullRomPoint(Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3, double t)
        {
            double t2 = t * t;
            double t3 = t2 * t;

            // Catmull-Rom basis functions
            double b0 = -0.5 * t3 + t2 - 0.5 * t;
            double b1 = 1.5 * t3 - 2.5 * t2 + 1.0;
            double b2 = -1.5 * t3 + 2.0 * t2 + 0.5 * t;
            double b3 = 0.5 * t3 - 0.5 * t2;

            double x = b0 * p0.Easting + b1 * p1.Easting + b2 * p2.Easting + b3 * p3.Easting;
            double y = b0 * p0.Northing + b1 * p1.Northing + b2 * p2.Northing + b3 * p3.Northing;

            return new Vec3(x, y, 0);
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

        /// <summary>
        /// Creates a clean offset curve by offsetting points and removing self-intersections.
        /// Use this instead of manually offsetting and calling CalculateHeadings.
        /// </summary>
        /// <param name="originalPoints">Original curve points with headings</param>
        /// <param name="offsetDistance">Perpendicular offset distance (positive = left of heading)</param>
        /// <returns>Clean offset curve without self-intersections</returns>
        public static List<Vec3> CreateOffsetCurve(IReadOnlyList<Vec3> originalPoints, double offsetDistance)
        {
            var (result, _) = CreateOffsetCurveWithInfo(originalPoints, offsetDistance);
            return result;
        }

        /// <summary>
        /// Creates a clean offset curve and returns information about whether points were removed.
        /// Uses AgOpenGPS collision detection: if an offset point is within offset² distance
        /// of ANY original curve point, it indicates self-intersection and the point is skipped.
        /// </summary>
        /// <param name="originalPoints">Original curve points with headings</param>
        /// <param name="offsetDistance">Perpendicular offset distance (positive = left of heading)</param>
        /// <returns>Tuple of (cleaned curve, percentage of points removed due to self-intersection)</returns>
        public static (List<Vec3> Points, double PercentRemoved) CreateOffsetCurveWithInfo(
            IReadOnlyList<Vec3> originalPoints, double offsetDistance)
        {
            if (originalPoints == null || originalPoints.Count < 2)
                return (originalPoints != null ? new List<Vec3>(originalPoints) : new List<Vec3>(), 0);

            if (Math.Abs(offsetDistance) < 0.01)
                return (new List<Vec3>(originalPoints), 0);

            // AgOpenGPS collision detection threshold: offset² - small margin
            // If offset point is closer than this to ANY original point, it's a self-intersection
            double distSqAway = (offsetDistance * offsetDistance) - 0.01;
            int refCount = originalPoints.Count;
            int skippedCount = 0;

            var offsetPoints = new List<Vec3>(originalPoints.Count);

            for (int i = 0; i < refCount; i++)
            {
                var pt = originalPoints[i];

                // Calculate offset point position
                double perpAngle = pt.Heading + Math.PI / 2;
                double offsetE = pt.Easting + Math.Sin(perpAngle) * offsetDistance;
                double offsetN = pt.Northing + Math.Cos(perpAngle) * offsetDistance;

                // AgOpenGPS collision detection: check if offset point is too close
                // to ANY original curve point (indicates self-intersection)
                bool addPoint = true;
                for (int t = 0; t < refCount; t++)
                {
                    double dx = offsetE - originalPoints[t].Easting;
                    double dy = offsetN - originalPoints[t].Northing;
                    double distSq = dx * dx + dy * dy;

                    if (distSq < distSqAway)
                    {
                        // Offset point is too close to original curve - self-intersection detected
                        addPoint = false;
                        skippedCount++;
                        break;
                    }
                }

                if (addPoint)
                {
                    // Also check minimum spacing from previous offset point (AgOpenGPS uses ~0.48 * tool width)
                    // We use a simpler 1m minimum spacing check
                    if (offsetPoints.Count > 0)
                    {
                        var lastPt = offsetPoints[offsetPoints.Count - 1];
                        double dx = offsetE - lastPt.Easting;
                        double dy = offsetN - lastPt.Northing;
                        double distSq = dx * dx + dy * dy;

                        if (distSq < 1.0) // Less than 1m from previous point
                        {
                            continue; // Skip to maintain spacing
                        }
                    }

                    offsetPoints.Add(new Vec3(offsetE, offsetN, pt.Heading));
                }
            }

            // Need at least 2 points for a valid track
            if (offsetPoints.Count < 2)
            {
                // Fallback: return first and last original points offset
                var first = originalPoints[0];
                var last = originalPoints[refCount - 1];

                double perpAngle1 = first.Heading + Math.PI / 2;
                double perpAngle2 = last.Heading + Math.PI / 2;

                return (new List<Vec3>
                {
                    new Vec3(
                        first.Easting + Math.Sin(perpAngle1) * offsetDistance,
                        first.Northing + Math.Cos(perpAngle1) * offsetDistance,
                        first.Heading),
                    new Vec3(
                        last.Easting + Math.Sin(perpAngle2) * offsetDistance,
                        last.Northing + Math.Cos(perpAngle2) * offsetDistance,
                        last.Heading)
                }, skippedCount * 100.0 / refCount);
            }

            // Recalculate headings based on actual offset point positions
            CalculateHeadings(offsetPoints);

            double percentRemoved = refCount > 0 ? skippedCount * 100.0 / refCount : 0;
            return (offsetPoints, percentRemoved);
        }

        /// <summary>
        /// Calculates the minimum radius of curvature along a curve.
        /// This determines the maximum inward offset before self-intersection occurs.
        /// </summary>
        /// <param name="points">Curve points with headings</param>
        /// <returns>Minimum radius in meters, or double.MaxValue for straight lines</returns>
        public static double CalculateMinRadiusOfCurvature(IReadOnlyList<Vec3> points)
        {
            if (points == null || points.Count < 3)
                return double.MaxValue; // Straight line or too few points

            double minRadius = double.MaxValue;

            for (int i = 0; i < points.Count - 1; i++)
            {
                // Calculate distance to next point
                double dx = points[i + 1].Easting - points[i].Easting;
                double dy = points[i + 1].Northing - points[i].Northing;
                double segmentLength = Math.Sqrt(dx * dx + dy * dy);

                if (segmentLength < 0.01) continue; // Skip very short segments

                // Calculate heading change
                double headingChange = points[i + 1].Heading - points[i].Heading;

                // Normalize to [-PI, PI]
                while (headingChange > Math.PI) headingChange -= GeometryMath.twoPI;
                while (headingChange < -Math.PI) headingChange += GeometryMath.twoPI;

                double absHeadingChange = Math.Abs(headingChange);
                if (absHeadingChange < 0.001) continue; // Nearly straight segment

                // Radius = arc_length / angle (in radians)
                // For small angles, segment length ≈ arc length
                double radius = segmentLength / absHeadingChange;

                if (radius < minRadius)
                    minRadius = radius;
            }

            return minRadius;
        }

        /// <summary>
        /// Calculates how many inward passes can be made before hitting the curve's geometric limit.
        /// </summary>
        /// <param name="points">Curve points with headings</param>
        /// <param name="passWidth">Width of each pass (tool width minus overlap)</param>
        /// <returns>Maximum number of inward passes possible, or int.MaxValue for straight lines</returns>
        public static int CalculateMaxInwardPasses(IReadOnlyList<Vec3> points, double passWidth)
        {
            if (passWidth <= 0) return int.MaxValue;

            double minRadius = CalculateMinRadiusOfCurvature(points);
            if (minRadius >= double.MaxValue - 1) return int.MaxValue;

            // Leave some margin (80% of min radius) to avoid artifacts
            return (int)(minRadius * 0.8 / passWidth);
        }
    }
}
