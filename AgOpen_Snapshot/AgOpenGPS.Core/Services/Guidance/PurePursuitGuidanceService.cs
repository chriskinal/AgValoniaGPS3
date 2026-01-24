using System;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Services.Guidance
{
    /// <summary>
    /// Core Pure Pursuit guidance algorithm implementation.
    /// Provides path tracking for precision agriculture using the Pure Pursuit controller.
    /// </summary>
    public class PurePursuitGuidanceService : IPurePursuitGuidanceService
    {
        private const double PIBy2 = Math.PI / 2.0;
        private const double TwoPI = Math.PI * 2.0;

        /// <summary>
        /// Calculate steering guidance using Pure Pursuit algorithm for AB line.
        /// </summary>
        /// <param name="input">Pure Pursuit algorithm input parameters</param>
        /// <returns>Pure Pursuit guidance output</returns>
        public PurePursuitGuidanceOutput CalculateGuidanceABLine(PurePursuitGuidanceInput input)
        {
            var output = new PurePursuitGuidanceOutput
            {
                GoalPoint = new Vec2(),
                RadiusPoint = new Vec2()
            };

            // Calculate distance from AB line
            double dx = input.CurrentLinePtB.Easting - input.CurrentLinePtA.Easting;
            double dy = input.CurrentLinePtB.Northing - input.CurrentLinePtA.Northing;

            // Distance from current AB Line
            output.DistanceFromCurrentLinePivot = ((dy * input.PivotPosition.Easting) - (dx * input.PivotPosition.Northing)
                + (input.CurrentLinePtB.Easting * input.CurrentLinePtA.Northing)
                - (input.CurrentLinePtB.Northing * input.CurrentLinePtA.Easting))
                / Math.Sqrt((dy * dy) + (dx * dx));

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
                if (input.IsAutoSteerOn
                    && Math.Abs(output.PivotDerivative) < 0.1
                    && input.AvgSpeed > 2.5)
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

            // Pure pursuit - calc point on ABLine closest to current position
            double U = (((input.PivotPosition.Easting - input.CurrentLinePtA.Easting) * dx)
                + ((input.PivotPosition.Northing - input.CurrentLinePtA.Northing) * dy))
                / ((dx * dx) + (dy * dy));

            // Point on AB line closest to pivot axle point
            output.REast = input.CurrentLinePtA.Easting + (U * dx);
            output.RNorth = input.CurrentLinePtA.Northing + (U * dy);

            // Calculate goal point based on heading direction
            if (input.IsReverse ^ input.IsHeadingSameWay)
            {
                output.GoalPoint = new Vec2(
                    output.REast + (Math.Sin(input.ABHeading) * input.GoalPointDistance),
                    output.RNorth + (Math.Cos(input.ABHeading) * input.GoalPointDistance));
            }
            else
            {
                output.GoalPoint = new Vec2(
                    output.REast - (Math.Sin(input.ABHeading) * input.GoalPointDistance),
                    output.RNorth - (Math.Cos(input.ABHeading) * input.GoalPointDistance));
            }

            // Calculate "D" the distance from pivot axle to lookahead point
            double goalPointDistanceDSquared = DistanceSquared(
                output.GoalPoint.Northing,
                output.GoalPoint.Easting,
                input.PivotPosition.Northing,
                input.PivotPosition.Easting);

            // Calculate the new x in local coordinates and steering angle degrees based on wheelbase
            double localHeading;
            if (input.IsHeadingSameWay)
                localHeading = TwoPI - input.FixHeading + output.Integral;
            else
                localHeading = TwoPI - input.FixHeading - output.Integral;

            // Pure Pursuit radius calculation
            output.PurePursuitRadius = goalPointDistanceDSquared / (2 * (
                ((output.GoalPoint.Easting - input.PivotPosition.Easting) * Math.Cos(localHeading))
                + ((output.GoalPoint.Northing - input.PivotPosition.Northing) * Math.Sin(localHeading))));

            // Steer angle calculation using Pure Pursuit formula
            output.SteerAngle = ToDegrees(Math.Atan(2 * (
                ((output.GoalPoint.Easting - input.PivotPosition.Easting) * Math.Cos(localHeading))
                + ((output.GoalPoint.Northing - input.PivotPosition.Northing) * Math.Sin(localHeading)))
                * input.Wheelbase / goalPointDistanceDSquared));

            // Side hill compensation from roll angle
            if (input.ImuRoll != 88888)
                output.SteerAngle += input.ImuRoll * -input.SideHillCompFactor;

            // Limit steer angle to vehicle maximum
            if (output.SteerAngle < -input.MaxSteerAngle)
                output.SteerAngle = -input.MaxSteerAngle;
            if (output.SteerAngle > input.MaxSteerAngle)
                output.SteerAngle = input.MaxSteerAngle;

            // Limit circle size for display purpose
            if (output.PurePursuitRadius < -500) output.PurePursuitRadius = -500;
            if (output.PurePursuitRadius > 500) output.PurePursuitRadius = 500;

            // Calculate radius point (center of turning circle)
            output.RadiusPoint = new Vec2(
                input.PivotPosition.Easting + (output.PurePursuitRadius * Math.Cos(localHeading)),
                input.PivotPosition.Northing + (output.PurePursuitRadius * Math.Sin(localHeading)));

            // Distance is negative if on left, positive if on right
            if (!input.IsHeadingSameWay)
                output.DistanceFromCurrentLinePivot *= -1.0;

            // Used for acquire/hold mode
            output.ModeActualXTE = output.DistanceFromCurrentLinePivot;

            // Calculate heading error
            double steerHeadingError = input.PivotPosition.Heading - input.ABHeading;

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

            // Convert to millimeters and prepare for transmission
            output.GuidanceLineDistanceOff = (short)Math.Round(output.DistanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(output.SteerAngle * 100);

            return output;
        }

        private double DistanceSquared(double northing1, double easting1, double northing2, double easting2)
        {
            double dNorth = northing1 - northing2;
            double dEast = easting1 - easting2;
            return (dNorth * dNorth) + (dEast * dEast);
        }

        private double ToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
