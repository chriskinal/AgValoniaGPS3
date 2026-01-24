using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Services.Guidance
{
    /// <summary>
    /// Core Curve Pure Pursuit guidance algorithm implementation.
    /// Provides path tracking for curved guidance lines using the Pure Pursuit controller.
    /// </summary>
    public class CurvePurePursuitGuidanceService : ICurvePurePursuitGuidanceService
    {
        private const double PIBy2 = Math.PI / 2.0;
        private const double TwoPI = Math.PI * 2.0;

        /// <summary>
        /// Calculate steering guidance using Pure Pursuit algorithm for curved path.
        /// </summary>
        /// <param name="input">Pure Pursuit algorithm input parameters</param>
        /// <returns>Pure Pursuit guidance output</returns>
        public CurvePurePursuitGuidanceOutput CalculateGuidanceCurve(CurvePurePursuitGuidanceInput input)
        {
            var output = new CurvePurePursuitGuidanceOutput
            {
                GoalPoint = new Vec2(),
                RadiusPoint = new Vec2(),
                FindGlobalNearestPoint = input.FindGlobalNearestPoint,
                CurrentLocationIndex = input.CurrentLocationIndex
            };

            if (input.CurvePoints == null || input.CurvePoints.Count == 0)
            {
                output.DistanceFromCurrentLinePivot = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

            bool reverseHeading = input.IsReverse ? !input.IsHeadingSameWay : input.IsHeadingSameWay;

            int A, B, C;

            // Find nearest segment on curve
            if (input.TrackMode <= TrackMode.Curve)
            {
                // Standard curve or boundary curve mode
                int cc;
                if (output.FindGlobalNearestPoint)
                {
                    cc = FindNearestGlobalCurvePoint(input.PivotPosition, input.CurvePoints, 10);
                    output.FindGlobalNearestPoint = false;
                }
                else
                {
                    cc = FindNearestLocalCurvePoint(input.PivotPosition, input.CurvePoints,
                        output.CurrentLocationIndex, input.GoalPointDistance, reverseHeading);
                }

                double minDistA = double.MaxValue;
                double minDistB = double.MaxValue;
                A = 0;
                B = 0;

                int dd = cc + 8;
                if (dd > input.CurvePoints.Count - 1) dd = input.CurvePoints.Count;
                cc -= 8;
                if (cc < 0) cc = 0;

                // Find the closest 2 points to current close call
                for (int j = cc; j < dd; j++)
                {
                    double dist = DistanceSquared(input.PivotPosition, input.CurvePoints[j]);
                    if (dist < minDistA)
                    {
                        minDistB = minDistA;
                        B = A;
                        minDistA = dist;
                        A = j;
                    }
                    else if (dist < minDistB)
                    {
                        minDistB = dist;
                        B = j;
                    }
                }

                // Make sure points continue ascending
                if (A > B)
                {
                    C = A;
                    A = B;
                    B = C;
                }

                output.CurrentLocationIndex = A;

                if (A > input.CurvePoints.Count - 1 || B > input.CurvePoints.Count - 1)
                {
                    output.DistanceFromCurrentLinePivot = 32000;
                    output.GuidanceLineDistanceOff = 32000;
                    return output;
                }
            }
            else
            {
                // Water pivot or other mode
                if (output.FindGlobalNearestPoint)
                {
                    A = FindNearestGlobalCurvePoint(input.PivotPosition, input.CurvePoints);
                    output.FindGlobalNearestPoint = false;
                }
                else
                {
                    A = FindNearestLocalCurvePoint(input.PivotPosition, input.CurvePoints,
                        output.CurrentLocationIndex, input.GoalPointDistance, reverseHeading);
                }

                output.CurrentLocationIndex = A;

                if (A > input.CurvePoints.Count - 1)
                {
                    output.DistanceFromCurrentLinePivot = 32000;
                    output.GuidanceLineDistanceOff = 32000;
                    return output;
                }

                // Initial forward test if pivot in range AB
                if (A == input.CurvePoints.Count - 1)
                    B = 0;
                else
                    B = A + 1;

                if (IsInRangeBetweenAB(input.CurvePoints[A], input.CurvePoints[B], input.PivotPosition))
                    goto SegmentFound;

                // Step back one
                if (A == 0)
                {
                    A = input.CurvePoints.Count - 1;
                    B = 0;
                }
                else
                {
                    A--;
                    B = A + 1;
                }

                if (IsInRangeBetweenAB(input.CurvePoints[A], input.CurvePoints[B], input.PivotPosition))
                    goto SegmentFound;

                // Really really lost
                output.DistanceFromCurrentLinePivot = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

        SegmentFound:

            // Get distance from currently active segment
            double dx = input.CurvePoints[B].Easting - input.CurvePoints[A].Easting;
            double dz = input.CurvePoints[B].Northing - input.CurvePoints[A].Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            {
                output.DistanceFromCurrentLinePivot = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

            // Distance from current segment
            output.DistanceFromCurrentLinePivot = ((dz * input.PivotPosition.Easting) - (dx * input.PivotPosition.Northing)
                + (input.CurvePoints[B].Easting * input.CurvePoints[A].Northing)
                - (input.CurvePoints[B].Northing * input.CurvePoints[A].Easting))
                / Math.Sqrt((dz * dz) + (dx * dx));

            // Integral term calculation
            if (input.PurePursuitIntegralGain != 0 && !input.IsReverse)
            {
                output.PivotDistanceError = output.DistanceFromCurrentLinePivot * 0.2 + input.PreviousPivotDistanceError * 0.8;
                output.Counter = input.PreviousCounter + 1;

                if (output.Counter > 4)
                {
                    output.PivotDerivative = output.PivotDistanceError - input.PreviousPivotDistanceErrorLast;
                    output.PivotDistanceErrorLast = output.PivotDistanceError;
                    output.Counter = 0;
                    output.PivotDerivative *= 2;
                }
                else
                {
                    output.PivotDerivative = 0;
                    output.PivotDistanceErrorLast = input.PreviousPivotDistanceErrorLast;
                }

                // Integral update conditions
                if (input.IsAutoSteerOn && input.AvgSpeed > 2.5 && Math.Abs(output.PivotDerivative) < 0.1 && !input.IsYouTurnTriggered)
                {
                    // If over the line heading wrong way, rapidly decrease integral
                    if ((input.PreviousIntegral < 0 && output.DistanceFromCurrentLinePivot < 0)
                        || (input.PreviousIntegral > 0 && output.DistanceFromCurrentLinePivot > 0))
                    {
                        output.Integral = input.PreviousIntegral + output.PivotDistanceError * input.PurePursuitIntegralGain * -0.04;
                    }
                    else
                    {
                        if (Math.Abs(output.DistanceFromCurrentLinePivot) > 0.02)
                        {
                            output.Integral = input.PreviousIntegral + output.PivotDistanceError * input.PurePursuitIntegralGain * -0.02;
                            if (output.Integral > 0.2) output.Integral = 0.2;
                            else if (output.Integral < -0.2) output.Integral = -0.2;
                        }
                        else
                        {
                            output.Integral = input.PreviousIntegral;
                        }
                    }
                }
                else
                {
                    output.Integral = input.PreviousIntegral * 0.95;
                }
            }
            else
            {
                output.Integral = 0;
                output.PivotDistanceError = 0;
                output.PivotDistanceErrorLast = input.PreviousPivotDistanceErrorLast;
                output.Counter = input.PreviousCounter;
                output.PivotDerivative = 0;
            }

            // Calculate closest point on segment
            double U = (((input.PivotPosition.Easting - input.CurvePoints[A].Easting) * dx)
                + ((input.PivotPosition.Northing - input.CurvePoints[A].Northing) * dz))
                / ((dx * dx) + (dz * dz));

            output.REast = input.CurvePoints[A].Easting + (U * dx);
            output.RNorth = input.CurvePoints[A].Northing + (U * dz);
            output.ManualUturnHeading = input.CurvePoints[A].Heading;

            // Calculate goal point by walking along curve
            int count = reverseHeading ? 1 : -1;
            Vec3 start = new Vec3(output.REast, output.RNorth, 0);
            double distSoFar = 0;

            for (int i = reverseHeading ? B : A; i < input.CurvePoints.Count && i >= 0;)
            {
                double tempDist = Distance(start, input.CurvePoints[i]);

                // Will we go too far?
                if ((tempDist + distSoFar) > input.GoalPointDistance)
                {
                    double j = (input.GoalPointDistance - distSoFar) / tempDist;
                    output.GoalPoint = new Vec2(
                        (((1 - j) * start.Easting) + (j * input.CurvePoints[i].Easting)),
                        (((1 - j) * start.Northing) + (j * input.CurvePoints[i].Northing)));
                    break;
                }
                else
                {
                    distSoFar += tempDist;
                }

                start = input.CurvePoints[i];
                i += count;
                if (i < 0) i = input.CurvePoints.Count - 1;
                if (i > input.CurvePoints.Count - 1) i = 0;
            }

            // Check for end of curve (only for standard curve mode)
            if (input.TrackMode <= TrackMode.Curve && input.IsAutoSteerOn && !input.IsReverse)
            {
                if (input.IsHeadingSameWay)
                {
                    if (Distance(output.GoalPoint, input.CurvePoints[input.CurvePoints.Count - 1]) < 0.5)
                    {
                        output.IsAtEndOfCurve = true;
                    }
                }
                else
                {
                    if (Distance(output.GoalPoint, input.CurvePoints[0]) < 0.5)
                    {
                        output.IsAtEndOfCurve = true;
                    }
                }
            }

            // Calculate distance to goal point
            double goalPointDistanceSquared = DistanceSquared(
                output.GoalPoint.Northing,
                output.GoalPoint.Easting,
                input.PivotPosition.Northing,
                input.PivotPosition.Easting);

            // Calculate heading and radius
            double localHeading;
            if (reverseHeading)
                localHeading = TwoPI - input.FixHeading + output.Integral;
            else
                localHeading = TwoPI - input.FixHeading - output.Integral;

            output.PurePursuitRadius = goalPointDistanceSquared / (2 * (
                ((output.GoalPoint.Easting - input.PivotPosition.Easting) * Math.Cos(localHeading))
                + ((output.GoalPoint.Northing - input.PivotPosition.Northing) * Math.Sin(localHeading))));

            output.SteerAngle = ToDegrees(Math.Atan(2 * (
                ((output.GoalPoint.Easting - input.PivotPosition.Easting) * Math.Cos(localHeading))
                + ((output.GoalPoint.Northing - input.PivotPosition.Northing) * Math.Sin(localHeading)))
                * input.Wheelbase / goalPointDistanceSquared));

            // Side hill compensation from roll angle
            if (input.ImuRoll != 88888)
                output.SteerAngle += input.ImuRoll * -input.SideHillCompFactor;

            // Limit steer angle to vehicle maximum
            if (output.SteerAngle < -input.MaxSteerAngle)
                output.SteerAngle = -input.MaxSteerAngle;
            if (output.SteerAngle > input.MaxSteerAngle)
                output.SteerAngle = input.MaxSteerAngle;

            // Distance is negative if on left, positive if on right
            if (!input.IsHeadingSameWay)
                output.DistanceFromCurrentLinePivot *= -1.0;

            // Used for acquire/hold mode
            output.ModeActualXTE = output.DistanceFromCurrentLinePivot;

            // Calculate heading error
            double steerHeadingError = input.PivotPosition.Heading - input.CurvePoints[A].Heading;

            // Fix the circular error
            if (steerHeadingError > Math.PI)
                steerHeadingError -= Math.PI;
            else if (steerHeadingError < -Math.PI)
                steerHeadingError += Math.PI;

            if (steerHeadingError > PIBy2)
                steerHeadingError -= Math.PI;
            else if (steerHeadingError < -PIBy2)
                steerHeadingError += Math.PI;

            output.ModeActualHeadingError = ToDegrees(steerHeadingError);

            // Calculate radius point (no limiting for curves like AB lines)
            output.RadiusPoint = new Vec2(
                input.PivotPosition.Easting + (output.PurePursuitRadius * Math.Cos(localHeading)),
                input.PivotPosition.Northing + (output.PurePursuitRadius * Math.Sin(localHeading)));

            // Convert to millimeters and prepare for transmission
            output.GuidanceLineDistanceOff = (short)Math.Round(output.DistanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(output.SteerAngle * 100);

            return output;
        }

        private int FindNearestGlobalCurvePoint(Vec3 refPoint, List<Vec3> curvePoints, int increment = 1)
        {
            double minDist = double.MaxValue;
            int minDistIndex = 0;

            for (int i = 0; i < curvePoints.Count; i += increment)
            {
                double dist = DistanceSquared(refPoint, curvePoints[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    minDistIndex = i;
                }
            }
            return minDistIndex;
        }

        private int FindNearestLocalCurvePoint(Vec3 refPoint, List<Vec3> curvePoints,
            int startIndex, double minSearchDistance, bool reverseSearchDirection)
        {
            double minDist = DistanceSquared(refPoint, curvePoints[(startIndex + curvePoints.Count) % curvePoints.Count]);
            int minDistIndex = startIndex;

            int directionMultiplier = reverseSearchDirection ? 1 : -1;
            double distSoFar = 0;
            Vec3 start = curvePoints[startIndex];

            // Check all points' distances from the pivot inside the "look ahead"-distance and find the nearest
            int offset = 1;

            while (offset < curvePoints.Count)
            {
                int pointIndex = (startIndex + (offset * directionMultiplier) + curvePoints.Count) % curvePoints.Count;
                double dist = DistanceSquared(refPoint, curvePoints[pointIndex]);

                if (dist < minDist)
                {
                    minDist = dist;
                    minDistIndex = pointIndex;
                }

                distSoFar += Distance(start, curvePoints[pointIndex]);
                start = curvePoints[pointIndex];

                offset++;

                if (distSoFar > minSearchDistance)
                {
                    break;
                }
            }

            // Continue traversing until the distance starts growing
            while (offset < curvePoints.Count)
            {
                int pointIndex = (startIndex + (offset * directionMultiplier) + curvePoints.Count) % curvePoints.Count;
                double dist = DistanceSquared(refPoint, curvePoints[pointIndex]);
                if (dist < minDist)
                {
                    minDist = dist;
                    minDistIndex = pointIndex;
                }
                else
                {
                    // Distance is growing - we've found the minimum
                    break;
                }

                offset++;
            }

            return minDistIndex;
        }

        private bool IsInRangeBetweenAB(Vec3 ptA, Vec3 ptB, Vec3 pivot)
        {
            double dx = ptB.Easting - ptA.Easting;
            double dz = ptB.Northing - ptA.Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
                return false;

            double t = ((pivot.Easting - ptA.Easting) * dx + (pivot.Northing - ptA.Northing) * dz) / (dx * dx + dz * dz);

            return t >= 0 && t <= 1;
        }

        private double DistanceSquared(Vec3 pt1, Vec3 pt2)
        {
            double dEast = pt1.Easting - pt2.Easting;
            double dNorth = pt1.Northing - pt2.Northing;
            return (dEast * dEast) + (dNorth * dNorth);
        }

        private double DistanceSquared(double northing1, double easting1, double northing2, double easting2)
        {
            double dNorth = northing1 - northing2;
            double dEast = easting1 - easting2;
            return (dNorth * dNorth) + (dEast * dEast);
        }

        private double Distance(Vec3 pt1, Vec3 pt2)
        {
            return Math.Sqrt(DistanceSquared(pt1, pt2));
        }

        private double Distance(Vec2 pt1, Vec3 pt2)
        {
            double dEast = pt1.Easting - pt2.Easting;
            double dNorth = pt1.Northing - pt2.Northing;
            return Math.Sqrt((dEast * dEast) + (dNorth * dNorth));
        }

        private double ToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
