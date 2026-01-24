using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.YouTurn;
using System;

namespace AgOpenGPS.Core.Services.YouTurn
{
    /// <summary>
    /// Service for calculating steering guidance while following a U-turn path.
    /// Supports both Stanley (steer axle) and Pure Pursuit (pivot axle) algorithms.
    /// </summary>
    public class YouTurnGuidanceService
    {
        private const double TWO_PI = Math.PI * 2.0;
        private const double PI_BY_2 = Math.PI / 2.0;

        /// <summary>
        /// Calculate steering guidance for following a U-turn path.
        /// </summary>
        public YouTurnGuidanceOutput CalculateGuidance(YouTurnGuidanceInput input)
        {
            var output = new YouTurnGuidanceOutput();

            int ptCount = input.TurnPath.Count;
            if (ptCount == 0)
            {
                output.IsTurnComplete = true;
                return output;
            }

            if (input.UseStanley)
            {
                CalculateStanleyGuidance(input, output, ptCount);
            }
            else
            {
                CalculatePurePursuitGuidance(input, output, ptCount);
            }

            return output;
        }

        private void CalculateStanleyGuidance(YouTurnGuidanceInput input, YouTurnGuidanceOutput output, int ptCount)
        {
            Vec3 pivot = input.SteerPosition;

            // Find the closest 2 points to current fix
            double minDistA = double.MaxValue;
            double minDistB = double.MaxValue;
            int A = 0, B = 0;

            for (int t = 0; t < ptCount; t++)
            {
                double dist = ((pivot.Easting - input.TurnPath[t].Easting) * (pivot.Easting - input.TurnPath[t].Easting))
                                + ((pivot.Northing - input.TurnPath[t].Northing) * (pivot.Northing - input.TurnPath[t].Northing));
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

            // Check if too far away - turn complete
            if (minDistA > 16)
            {
                output.IsTurnComplete = true;
                return;
            }

            // Make sure points continue ascending
            if (A > B)
            {
                (B, A) = (A, B);
            }

            // Bounds check
            if (A < 0) A = 0;
            B = A + 1;

            // Return and reset if too far away or end of the line
            if (B >= ptCount - 1)
            {
                output.IsTurnComplete = true;
                return;
            }

            // K-style turn in reverse completes immediately
            if (input.UTurnStyle == 1 && input.IsReverse)
            {
                output.IsTurnComplete = true;
                return;
            }

            // Get the distance from currently active line
            double dx = input.TurnPath[B].Easting - input.TurnPath[A].Easting;
            double dz = input.TurnPath[B].Northing - input.TurnPath[A].Northing;
            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            {
                output.IsTurnComplete = false;
                return;
            }

            double abHeading = input.TurnPath[A].Heading;

            // How far from current line is steer point (90 degrees from steer position)
            double distanceFromCurrentLine = ((dz * pivot.Easting) - (dx * pivot.Northing) + (input.TurnPath[B].Easting
                        * input.TurnPath[A].Northing) - (input.TurnPath[B].Northing * input.TurnPath[A].Easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            // Calc point on line closest to current position and 90 degrees to segment heading
            double U = (((pivot.Easting - input.TurnPath[A].Easting) * dx)
                        + ((pivot.Northing - input.TurnPath[A].Northing) * dz))
                        / ((dx * dx) + (dz * dz));

            // Critical point used as start for the uturn path
            double rEast = input.TurnPath[A].Easting + (U * dx);
            double rNorth = input.TurnPath[A].Northing + (U * dz);

            // The first part of stanley is to extract heading error
            double abFixHeadingDelta = (pivot.Heading - abHeading);

            // Fix the circular error - get it from -Pi/2 to Pi/2
            if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < Math.PI) abFixHeadingDelta += Math.PI;
            if (abFixHeadingDelta > PI_BY_2) abFixHeadingDelta -= Math.PI;
            else if (abFixHeadingDelta < -PI_BY_2) abFixHeadingDelta += Math.PI;

            if (input.IsReverse) abFixHeadingDelta *= -1;

            // Normally set to 1, less than unity gives less heading error
            abFixHeadingDelta *= input.StanleyHeadingErrorGain;
            if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
            if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

            // The non linear distance error part of stanley
            double steerAngle = Math.Atan((distanceFromCurrentLine * input.StanleyDistanceErrorGain) / ((input.AvgSpeed * 0.277777) + 1));

            // Clamp it to max 42 degrees
            if (steerAngle > 0.74) steerAngle = 0.74;
            if (steerAngle < -0.74) steerAngle = -0.74;

            // Add them up and clamp to max in vehicle settings
            steerAngle = ToDegrees((steerAngle + abFixHeadingDelta * input.UTurnCompensation) * -1.0);
            if (steerAngle < -input.MaxSteerAngle) steerAngle = -input.MaxSteerAngle;
            if (steerAngle > input.MaxSteerAngle) steerAngle = input.MaxSteerAngle;

            // Output
            output.IsTurnComplete = false;
            output.DistanceFromCurrentLine = distanceFromCurrentLine;
            output.REast = rEast;
            output.RNorth = rNorth;
            output.SteerAngle = steerAngle;
            output.PointA = A;
            output.PointB = B;
            output.ModeActualXTE = distanceFromCurrentLine;
            output.GuidanceLineDistanceOff = (short)Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(steerAngle * 100);
            output.PathCount = ptCount - B;
        }

        private void CalculatePurePursuitGuidance(YouTurnGuidanceInput input, YouTurnGuidanceOutput output, int ptCount)
        {
            Vec3 pivot = input.PivotPosition;

            // Find the closest 2 points to current fix
            double minDistA = double.MaxValue;
            double minDistB = double.MaxValue;
            int A = 0, B = 0;

            for (int t = 0; t < ptCount; t++)
            {
                double dist = ((pivot.Easting - input.TurnPath[t].Easting) * (pivot.Easting - input.TurnPath[t].Easting))
                                + ((pivot.Northing - input.TurnPath[t].Northing) * (pivot.Northing - input.TurnPath[t].Northing));
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

            // Make sure points continue ascending
            if (A > B)
            {
                (B, A) = (A, B);
            }

            double distancePiv = Distance(input.TurnPath[A], pivot);

            if ((A > 0 && distancePiv > 2) || (B >= ptCount - 1))
            {
                output.IsTurnComplete = true;
                return;
            }

            // Get the distance from currently active line
            double dx = input.TurnPath[B].Easting - input.TurnPath[A].Easting;
            double dz = input.TurnPath[B].Northing - input.TurnPath[A].Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            {
                output.IsTurnComplete = false;
                return;
            }

            // How far from current line is fix
            double distanceFromCurrentLine = ((dz * pivot.Easting) - (dx * pivot.Northing) + (input.TurnPath[B].Easting
                        * input.TurnPath[A].Northing) - (input.TurnPath[B].Northing * input.TurnPath[A].Easting))
                            / Math.Sqrt((dz * dz) + (dx * dx));

            // Calc point on line closest to current position
            double U = (((pivot.Easting - input.TurnPath[A].Easting) * dx)
                        + ((pivot.Northing - input.TurnPath[A].Northing) * dz))
                        / ((dx * dx) + (dz * dz));

            double rEast = input.TurnPath[A].Easting + (U * dx);
            double rNorth = input.TurnPath[A].Northing + (U * dz);

            // Sharp turns on you turn - update based on autosteer settings and distance from line
            double goalPointDistance = input.GoalPointDistance;

            bool isHeadingSameWay = true;
            bool reverseHeading = !input.IsReverse;

            int count = reverseHeading ? 1 : -1;
            Vec3 start = new Vec3(rEast, rNorth, 0);
            double distSoFar = 0;
            Vec2 goalPoint = new Vec2();

            for (int i = reverseHeading ? B : A; i < ptCount && i >= 0; i += count)
            {
                // Used for calculating the length squared of next segment
                double tempDist = Distance(start, input.TurnPath[i]);

                // Will we go too far?
                if ((tempDist + distSoFar) > goalPointDistance)
                {
                    double j = (goalPointDistance - distSoFar) / tempDist; // The remainder to yet travel

                    goalPoint.Easting = (((1 - j) * start.Easting) + (j * input.TurnPath[i].Easting));
                    goalPoint.Northing = (((1 - j) * start.Northing) + (j * input.TurnPath[i].Northing));
                    break;
                }
                else distSoFar += tempDist;

                start = input.TurnPath[i];

                if (i == ptCount - 1) // goalPointDistance is longer than remaining u-turn
                {
                    output.IsTurnComplete = true;
                    return;
                }

                if (input.UTurnStyle == 1 && input.IsReverse)
                {
                    output.IsTurnComplete = true;
                    return;
                }
            }

            // Calc "D" the distance from pivot axle to lookahead point
            double goalPointDistanceSquared = DistanceSquared(goalPoint.Northing, goalPoint.Easting, pivot.Northing, pivot.Easting);

            // Calculate the delta x in local coordinates and steering angle degrees based on wheelbase
            double localHeading = TWO_PI - input.FixHeading;
            double ppRadius = goalPointDistanceSquared / (2 * (((goalPoint.Easting - pivot.Easting) * Math.Cos(localHeading)) + ((goalPoint.Northing - pivot.Northing) * Math.Sin(localHeading))));

            double steerAngle = ToDegrees(Math.Atan(2 * (((goalPoint.Easting - pivot.Easting) * Math.Cos(localHeading))
                + ((goalPoint.Northing - pivot.Northing) * Math.Sin(localHeading))) * input.Wheelbase / goalPointDistanceSquared));

            steerAngle *= input.UTurnCompensation;

            if (steerAngle < -input.MaxSteerAngle) steerAngle = -input.MaxSteerAngle;
            if (steerAngle > input.MaxSteerAngle) steerAngle = input.MaxSteerAngle;

            if (ppRadius < -500) ppRadius = -500;
            if (ppRadius > 500) ppRadius = 500;

            Vec2 radiusPoint = new Vec2
            {
                Easting = pivot.Easting + (ppRadius * Math.Cos(localHeading)),
                Northing = pivot.Northing + (ppRadius * Math.Sin(localHeading))
            };

            // Distance is negative if on left, positive if on right
            if (!isHeadingSameWay)
                distanceFromCurrentLine *= -1.0;

            // Output
            output.IsTurnComplete = false;
            output.DistanceFromCurrentLine = distanceFromCurrentLine;
            output.REast = rEast;
            output.RNorth = rNorth;
            output.SteerAngle = steerAngle;
            output.GoalPoint = goalPoint;
            output.RadiusPoint = radiusPoint;
            output.PPRadius = ppRadius;
            output.PointA = A;
            output.PointB = B;
            output.ModeActualXTE = distanceFromCurrentLine;
            output.GuidanceLineDistanceOff = (short)Math.Round(distanceFromCurrentLine * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(steerAngle * 100);
            output.PathCount = ptCount - B;
        }

        private static double Distance(Vec3 a, Vec3 b)
        {
            double dx = a.Easting - b.Easting;
            double dz = a.Northing - b.Northing;
            return Math.Sqrt((dx * dx) + (dz * dz));
        }

        private static double DistanceSquared(double aN, double aE, double bN, double bE)
        {
            double dx = aE - bE;
            double dz = aN - bN;
            return (dx * dx) + (dz * dz);
        }

        private static double ToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
