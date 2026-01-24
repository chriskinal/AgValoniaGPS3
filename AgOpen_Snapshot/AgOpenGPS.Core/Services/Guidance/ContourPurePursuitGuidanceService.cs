using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Services.Guidance
{
    /// <summary>
    /// Core Contour Pure Pursuit guidance algorithm implementation.
    /// Provides path tracking for contour following using the Pure Pursuit controller.
    /// </summary>
    public class ContourPurePursuitGuidanceService : IContourPurePursuitGuidanceService
    {
        private const double PIBy2 = Math.PI / 2.0;
        private const double TwoPI = Math.PI * 2.0;

        /// <summary>
        /// Calculate steering guidance using Pure Pursuit algorithm for contour path.
        /// </summary>
        /// <param name="input">Pure Pursuit algorithm input parameters</param>
        /// <returns>Pure Pursuit guidance output</returns>
        public ContourPurePursuitGuidanceOutput CalculateGuidanceContour(ContourPurePursuitGuidanceInput input)
        {
            var output = new ContourPurePursuitGuidanceOutput
            {
                GoalPoint = new Vec2(),
                IsLocked = input.IsLocked
            };

            if (input.ContourPoints == null || input.ContourPoints.Count <= 8)
            {
                output.DistanceFromCurrentLinePivot = 0;
                output.GuidanceLineDistanceOff = 0;
                return output;
            }

            int ptCount = input.ContourPoints.Count;

            // Find the closest 2 points to current fix
            double minDistA = 1000000, minDistB = 1000000;
            int A = 0, B = 0, C;

            for (int t = 0; t < ptCount; t++)
            {
                double dist = DistanceSquared(input.PivotPosition, input.ContourPoints[t]);
                if (dist < minDistA)
                {
                    minDistB = minDistA;
                    B = A;
                    minDistA = dist;
                    A = t;
                }
                else if (dist < minDistB)
                {
                    minDistB = dist;
                    B = t;
                }
            }

            // Make sure the points continue ascending in list order
            if (A > B)
            {
                C = A;
                A = B;
                B = C;
            }

            // Check lock boundaries - unlock if at beginning or end
            if (output.IsLocked && (A < 2 || B > ptCount - 3))
            {
                output.IsLocked = false;
                return output;
            }

            // Get the distance from currently active segment
            double dx = input.ContourPoints[B].Easting - input.ContourPoints[A].Easting;
            double dy = input.ContourPoints[B].Northing - input.ContourPoints[A].Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                output.DistanceFromCurrentLinePivot = 0;
                output.GuidanceLineDistanceOff = 0;
                return output;
            }

            // Distance from current segment - using fix position
            output.DistanceFromCurrentLinePivot = ((dy * input.FixPosition.Easting) - (dx * input.FixPosition.Northing)
                + (input.ContourPoints[B].Easting * input.ContourPoints[A].Northing)
                - (input.ContourPoints[B].Northing * input.ContourPoints[A].Easting))
                / Math.Sqrt((dy * dy) + (dx * dx));

            // Integral term calculation
            if (input.PurePursuitIntegralGain != 0)
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
                if (input.IsAutoSteerOn
                    && Math.Abs(output.PivotDerivative) < 0.1
                    && input.AvgSpeed > 2.5
                    && !input.IsYouTurnTriggered)
                {
                    // If over the line heading wrong way, rapidly decrease integral
                    // Note: Contour uses -0.06 instead of -0.04 for rapid correction
                    if ((input.PreviousIntegral < 0 && output.DistanceFromCurrentLinePivot < 0)
                        || (input.PreviousIntegral > 0 && output.DistanceFromCurrentLinePivot > 0))
                    {
                        output.Integral = input.PreviousIntegral + output.PivotDistanceError * input.PurePursuitIntegralGain * -0.06;
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

            // Set integral to 0 if reversing (contour-specific)
            if (input.IsReverse)
                output.Integral = 0;

            // Determine heading direction
            output.IsHeadingSameWay = Math.PI - Math.Abs(Math.Abs(input.PivotPosition.Heading - input.ContourPoints[A].Heading) - Math.PI) < PIBy2;

            // Distance is negative if on left, positive if on right
            if (!output.IsHeadingSameWay)
                output.DistanceFromCurrentLinePivot *= -1.0;

            // Calculate closest point on segment
            double U = (((input.PivotPosition.Easting - input.ContourPoints[A].Easting) * dx)
                + ((input.PivotPosition.Northing - input.ContourPoints[A].Northing) * dy))
                / ((dx * dx) + (dy * dy));

            output.REast = input.ContourPoints[A].Easting + (U * dx);
            output.RNorth = input.ContourPoints[A].Northing + (U * dy);

            // Calculate goal point by walking along contour
            bool reverseHeading = input.IsReverse ? !output.IsHeadingSameWay : output.IsHeadingSameWay;
            int count = reverseHeading ? 1 : -1;
            Vec3 start = new Vec3(output.REast, output.RNorth, 0);
            double distSoFar = 0;

            for (int i = reverseHeading ? B : A; i < ptCount && i >= 0; i += count)
            {
                double tempDist = Distance(start, input.ContourPoints[i]);

                // Will we go too far?
                if ((tempDist + distSoFar) > input.GoalPointDistance)
                {
                    double j = (input.GoalPointDistance - distSoFar) / tempDist;
                    output.GoalPoint = new Vec2(
                        (((1 - j) * start.Easting) + (j * input.ContourPoints[i].Easting)),
                        (((1 - j) * start.Northing) + (j * input.ContourPoints[i].Northing)));
                    break;
                }
                else
                {
                    distSoFar += tempDist;
                }

                start = input.ContourPoints[i];
            }

            // Calculate distance to goal point
            double goalPointDistanceSquared = DistanceSquared(
                output.GoalPoint.Northing,
                output.GoalPoint.Easting,
                input.PivotPosition.Northing,
                input.PivotPosition.Easting);

            // Calculate heading and steer angle
            double localHeading;
            if (output.IsHeadingSameWay)
                localHeading = TwoPI - input.FixHeading + output.Integral;
            else
                localHeading = TwoPI - input.FixHeading - output.Integral;

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

            // Used for acquire/hold mode
            output.ModeActualXTE = output.DistanceFromCurrentLinePivot;

            // Convert to millimeters and prepare for transmission
            output.GuidanceLineDistanceOff = (short)Math.Round(output.DistanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(output.SteerAngle * 100);

            return output;
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

        private double ToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
