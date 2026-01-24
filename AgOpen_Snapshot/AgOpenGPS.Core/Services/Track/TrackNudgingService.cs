using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Track;

namespace AgOpenGPS.Core.Services.Track
{
    /// <summary>
    /// Core track nudging geometric algorithms.
    /// Provides perpendicular offset calculations for AB lines and curves.
    /// </summary>
    public class TrackNudgingService : ITrackNudgingService
    {
        /// <summary>
        /// Nudge an AB line by perpendicular distance.
        /// </summary>
        /// <param name="input">AB line nudge input parameters</param>
        /// <returns>New AB line points</returns>
        public ABLineNudgeOutput NudgeABLine(ABLineNudgeInput input)
        {
            // Calculate perpendicular offset using heading + 90 degrees
            double perpHeading = input.Heading + GeometryMath.PIBy2;

            return new ABLineNudgeOutput
            {
                NewPointA = new Vec2
                {
                    Easting = input.PointA.Easting + (Math.Sin(perpHeading) * input.Distance),
                    Northing = input.PointA.Northing + (Math.Cos(perpHeading) * input.Distance)
                },
                NewPointB = new Vec2
                {
                    Easting = input.PointB.Easting + (Math.Sin(perpHeading) * input.Distance),
                    Northing = input.PointB.Northing + (Math.Cos(perpHeading) * input.Distance)
                }
            };
        }

        /// <summary>
        /// Nudge a curve by perpendicular distance with filtering and smoothing.
        /// </summary>
        /// <param name="input">Curve nudge input parameters</param>
        /// <returns>New curve points</returns>
        public CurveNudgeOutput NudgeCurve(CurveNudgeInput input)
        {
            var output = new CurveNudgeOutput
            {
                NewCurvePoints = new List<Vec3>()
            };

            if (input.CurvePoints == null || input.CurvePoints.Count < 6)
            {
                return output;
            }

            List<Vec3> curList = new List<Vec3>();
            double distSqAway = (input.Distance * input.Distance) - 0.01;

            // Step 1: Move all points perpendicular by distance and filter duplicates
            for (int i = 0; i < input.CurvePoints.Count; i++)
            {
                Vec3 point = new Vec3(
                    input.CurvePoints[i].Easting + (Math.Sin(GeometryMath.PIBy2 + input.CurvePoints[i].Heading) * input.Distance),
                    input.CurvePoints[i].Northing + (Math.Cos(GeometryMath.PIBy2 + input.CurvePoints[i].Heading) * input.Distance),
                    input.CurvePoints[i].Heading);

                bool add = true;

                // Check if too close to any original point (anti-collision)
                for (int t = 0; t < input.CurvePoints.Count; t++)
                {
                    double dist = ((point.Easting - input.CurvePoints[t].Easting) * (point.Easting - input.CurvePoints[t].Easting))
                        + ((point.Northing - input.CurvePoints[t].Northing) * (point.Northing - input.CurvePoints[t].Northing));
                    if (dist < distSqAway)
                    {
                        add = false;
                        break;
                    }
                }

                if (add)
                {
                    if (curList.Count > 0)
                    {
                        // Check minimum spacing between consecutive points
                        double dist = ((point.Easting - curList[curList.Count - 1].Easting) * (point.Easting - curList[curList.Count - 1].Easting))
                            + ((point.Northing - curList[curList.Count - 1].Northing) * (point.Northing - curList[curList.Count - 1].Northing));
                        if (dist > 1.0)
                            curList.Add(point);
                    }
                    else
                    {
                        curList.Add(point);
                    }
                }
            }

            int cnt = curList.Count;
            if (cnt < 6)
            {
                return output;
            }

            // Step 2: Recalculate headings for the nudged points
            Vec3[] arr = new Vec3[cnt];
            curList.CopyTo(arr);
            curList.Clear();

            for (int i = 0; i < (arr.Length - 1); i++)
            {
                arr[i].Heading = Math.Atan2(arr[i + 1].Easting - arr[i].Easting, arr[i + 1].Northing - arr[i].Northing);
                if (arr[i].Heading < 0) arr[i].Heading += GeometryMath.twoPI;
                if (arr[i].Heading >= GeometryMath.twoPI) arr[i].Heading -= GeometryMath.twoPI;
            }

            arr[arr.Length - 1].Heading = arr[arr.Length - 2].Heading;

            // Step 3: Apply Catmull-Rom spline smoothing with spacing control
            cnt = arr.Length;
            double spacing = 1.2;

            // Add first point
            curList.Add(arr[0]);

            for (int i = 0; i < cnt - 3; i++)
            {
                // Add p2
                curList.Add(arr[i + 1]);

                double distance = GeometryMath.Distance(arr[i + 1], arr[i + 2]);

                if (distance > spacing)
                {
                    int loopTimes = (int)(distance / spacing + 1);
                    for (int j = 1; j < loopTimes; j++)
                    {
                        Vec3 pos = GeometryMath.Catmull(j / (double)(loopTimes), arr[i], arr[i + 1], arr[i + 2], arr[i + 3]);
                        curList.Add(pos);
                    }
                }
            }

            curList.Add(arr[cnt - 2]);
            curList.Add(arr[cnt - 1]);

            // Step 4: Final heading calculation for smoothed curve
            CalculateHeadings(ref curList);

            output.NewCurvePoints = curList;
            return output;
        }

        /// <summary>
        /// Calculate headings for curve points based on adjacent points.
        /// First point uses forward difference, last uses backward, middle points use centered difference.
        /// </summary>
        private void CalculateHeadings(ref List<Vec3> xList)
        {
            int cnt = xList.Count;
            if (cnt <= 3)
            {
                return;
            }

            Vec3[] arr = new Vec3[cnt];
            cnt--;
            xList.CopyTo(arr);
            xList.Clear();

            // First point - forward difference
            Vec3 pt3 = arr[0];
            pt3.Heading = Math.Atan2(arr[1].Easting - arr[0].Easting, arr[1].Northing - arr[0].Northing);
            if (pt3.Heading < 0) pt3.Heading += GeometryMath.twoPI;
            xList.Add(pt3);

            // Middle points - centered difference
            for (int i = 1; i < cnt; i++)
            {
                pt3 = arr[i];
                pt3.Heading = Math.Atan2(arr[i + 1].Easting - arr[i - 1].Easting, arr[i + 1].Northing - arr[i - 1].Northing);
                if (pt3.Heading < 0) pt3.Heading += GeometryMath.twoPI;
                xList.Add(pt3);
            }

            // Last point - backward difference
            pt3 = arr[arr.Length - 1];
            pt3.Heading = Math.Atan2(arr[arr.Length - 1].Easting - arr[arr.Length - 2].Easting,
                arr[arr.Length - 1].Northing - arr[arr.Length - 2].Northing);
            if (pt3.Heading < 0) pt3.Heading += GeometryMath.twoPI;
            xList.Add(pt3);
        }
    }
}
