using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Services.Guidance
{
    /// <summary>
    /// Core Stanley guidance algorithm implementation.
    /// Provides path tracking for precision agriculture using the Stanley controller.
    /// </summary>
    public class StanleyGuidanceService : IStanleyGuidanceService
    {
        private const double PIBy2 = Math.PI / 2.0;
        private const double TwoPI = Math.PI * 2.0;

        /// <summary>
        /// Calculate steering guidance for a straight AB line.
        /// </summary>
        /// <param name="curPtA">Start point of AB line segment</param>
        /// <param name="curPtB">End point of AB line segment</param>
        /// <param name="input">Stanley algorithm input parameters</param>
        /// <param name="isHeadingSameWay">True if heading same direction as AB line</param>
        /// <returns>Stanley guidance output</returns>
        public StanleyGuidanceOutput CalculateGuidanceABLine(
            Vec3 curPtA,
            Vec3 curPtB,
            StanleyGuidanceInput input,
            bool isHeadingSameWay)
        {
            var output = new StanleyGuidanceOutput();

            // Get the pivot distance from currently active AB segment
            double dx = curPtB.Easting - curPtA.Easting;
            double dy = curPtB.Northing - curPtA.Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                // Invalid segment
                output.DistanceFromCurrentLineSteer = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

            // Calculate pivot distance from AB line
            output.DistanceFromCurrentLinePivot = ((dy * input.PivotPosition.Easting) - (dx * input.PivotPosition.Northing)
                + (curPtB.Easting * curPtA.Northing) - (curPtB.Northing * curPtA.Easting))
                / Math.Sqrt((dy * dy) + (dx * dx));

            if (!isHeadingSameWay)
                output.DistanceFromCurrentLinePivot *= -1.0;

            // Calculate closest point on AB line to pivot
            double U = (((input.PivotPosition.Easting - curPtA.Easting) * dx)
                + ((input.PivotPosition.Northing - curPtA.Northing) * dy))
                / ((dx * dx) + (dy * dy));

            output.REastPivot = curPtA.Easting + (U * dx);
            output.RNorthPivot = curPtA.Northing + (U * dy);

            // Get the distance from AB segment for steer axle
            // Apply integral offset to create offset AB line
            Vec3 steerA = new Vec3(
                curPtA.Easting + (Math.Sin(curPtA.Heading + PIBy2) * input.PreviousIntegral),
                curPtA.Northing + (Math.Cos(curPtA.Heading + PIBy2) * input.PreviousIntegral),
                curPtA.Heading);

            Vec3 steerB = new Vec3(
                curPtB.Easting + (Math.Sin(curPtB.Heading + PIBy2) * input.PreviousIntegral),
                curPtB.Northing + (Math.Cos(curPtB.Heading + PIBy2) * input.PreviousIntegral),
                curPtB.Heading);

            dx = steerB.Easting - steerA.Easting;
            dy = steerB.Northing - steerA.Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                output.DistanceFromCurrentLineSteer = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

            // Calculate steer axle distance from offset AB line
            output.DistanceFromCurrentLineSteer = ((dy * input.SteerPosition.Easting) - (dx * input.SteerPosition.Northing)
                + (steerB.Easting * steerA.Northing) - (steerB.Northing * steerA.Easting))
                / Math.Sqrt((dy * dy) + (dx * dx));

            if (!isHeadingSameWay)
                output.DistanceFromCurrentLineSteer *= -1.0;

            // Calculate closest point on AB line to steer position
            U = (((input.SteerPosition.Easting - steerA.Easting) * dx)
                + ((input.SteerPosition.Northing - steerA.Northing) * dy))
                / ((dx * dx) + (dy * dy));

            output.REastSteer = steerA.Easting + (U * dx);
            output.RNorthSteer = steerA.Northing + (U * dy);

            // Calculate heading error
            double steerErr = Math.Atan2(output.REastSteer - output.REastPivot, output.RNorthSteer - output.RNorthPivot);
            output.SteerHeadingError = input.SteerPosition.Heading - steerErr;

            // Fix circular error
            if (output.SteerHeadingError > Math.PI)
                output.SteerHeadingError -= Math.PI;
            else if (output.SteerHeadingError < -Math.PI)
                output.SteerHeadingError += Math.PI;

            if (output.SteerHeadingError > PIBy2)
                output.SteerHeadingError -= Math.PI;
            else if (output.SteerHeadingError < -PIBy2)
                output.SteerHeadingError += Math.PI;

            output.ModeActualHeadingError = ToDegrees(output.SteerHeadingError);

            // Calculate final steer angle using Stanley algorithm
            CalculateSteerAngle(input, output);

            return output;
        }

        /// <summary>
        /// Calculate steering guidance for a curved path.
        /// </summary>
        /// <param name="curvePoints">List of points defining the curve</param>
        /// <param name="input">Stanley algorithm input parameters</param>
        /// <param name="isHeadingSameWay">True if heading same direction as curve</param>
        /// <returns>Stanley guidance output with curve-specific data</returns>
        public StanleyGuidanceCurveOutput CalculateGuidanceCurve(
            List<Vec3> curvePoints,
            StanleyGuidanceInput input,
            bool isHeadingSameWay)
        {
            var output = new StanleyGuidanceCurveOutput();
            int ptCount = curvePoints.Count;

            if (ptCount <= 5)
            {
                // Invalid curve - not enough points
                output.DistanceFromCurrentLineSteer = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

            // Find closest point roughly (coarse search every 10 points)
            int cc = 0;
            double minDistA = 1000000;

            for (int j = 0; j < ptCount; j += 10)
            {
                double dist = ((input.SteerPosition.Easting - curvePoints[j].Easting) * (input.SteerPosition.Easting - curvePoints[j].Easting))
                    + ((input.SteerPosition.Northing - curvePoints[j].Northing) * (input.SteerPosition.Northing - curvePoints[j].Northing));
                if (dist < minDistA)
                {
                    minDistA = dist;
                    cc = j;
                }
            }

            // Fine search around coarse result
            minDistA = 1000000;
            double minDistB = 1000000;
            int dd = cc + 7;
            if (dd > ptCount - 1) dd = ptCount;
            cc -= 7;
            if (cc < 0) cc = 0;

            int sA = 0, sB = 0;
            // Find the closest 2 points to steer position
            for (int j = cc; j < dd; j++)
            {
                double dist = ((input.SteerPosition.Easting - curvePoints[j].Easting) * (input.SteerPosition.Easting - curvePoints[j].Easting))
                    + ((input.SteerPosition.Northing - curvePoints[j].Northing) * (input.SteerPosition.Northing - curvePoints[j].Northing));
                if (dist < minDistA)
                {
                    minDistB = minDistA;
                    sB = sA;
                    minDistA = dist;
                    sA = j;
                }
                else if (dist < minDistB)
                {
                    minDistB = dist;
                    sB = j;
                }
            }

            // Ensure points are in ascending order
            if (sA > sB)
            {
                int temp = sA;
                sA = sB;
                sB = temp;
            }

            if (sA > ptCount - 1 || sB > ptCount - 1)
                return output;

            // Find closest 2 points for pivot (back from steer position)
            minDistA = minDistB = 1000000;
            int pA = 0, pB = 0;

            if (isHeadingSameWay)
            {
                dd = sB;
                cc = dd - 12;
                if (cc < 0) cc = 0;
            }
            else
            {
                cc = sA;
                dd = sA + 12;
                if (dd >= ptCount) dd = ptCount - 1;
            }

            for (int j = cc; j < dd; j++)
            {
                double dist = ((input.SteerPosition.Easting - curvePoints[j].Easting) * (input.SteerPosition.Easting - curvePoints[j].Easting))
                    + ((input.SteerPosition.Northing - curvePoints[j].Northing) * (input.SteerPosition.Northing - curvePoints[j].Northing));
                if (dist < minDistA)
                {
                    minDistB = minDistA;
                    pB = pA;
                    minDistA = dist;
                    pA = j;
                }
                else if (dist < minDistB)
                {
                    minDistB = dist;
                    pB = j;
                }
            }

            // Ensure points are in ascending order
            if (pA > pB)
            {
                int temp = pA;
                pA = pB;
                pB = temp;
            }

            if (pA > ptCount - 1 || pB > ptCount - 1)
            {
                pA = ptCount - 2;
                pB = ptCount - 1;
            }

            output.CurrentLocationIndex = pA;

            Vec3 pivA = new Vec3(curvePoints[pA].Easting, curvePoints[pA].Northing, curvePoints[pA].Heading);
            Vec3 pivB = new Vec3(curvePoints[pB].Easting, curvePoints[pB].Northing, curvePoints[pB].Heading);

            if (!isHeadingSameWay)
            {
                pivA = new Vec3(curvePoints[pB].Easting, curvePoints[pB].Northing, curvePoints[pB].Heading);
                pivB = new Vec3(curvePoints[pA].Easting, curvePoints[pA].Northing, curvePoints[pA].Heading);

                pivA.Heading += Math.PI;
                if (pivA.Heading > TwoPI) pivA.Heading -= TwoPI;
            }

            output.ManualUturnHeading = pivA.Heading;

            // Calculate pivot distance from curve segment
            double dx = pivB.Easting - pivA.Easting;
            double dz = pivB.Northing - pivA.Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            {
                output.DistanceFromCurrentLineSteer = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

            output.DistanceFromCurrentLinePivot = ((dz * input.SteerPosition.Easting) - (dx * input.SteerPosition.Northing)
                + (pivB.Easting * pivA.Northing) - (pivB.Northing * pivA.Easting))
                / Math.Sqrt((dz * dz) + (dx * dx));

            double U = (((input.SteerPosition.Easting - pivA.Easting) * dx)
                + ((input.SteerPosition.Northing - pivA.Northing) * dz))
                / ((dx * dx) + (dz * dz));

            output.REastPivot = pivA.Easting + (U * dx);
            output.RNorthPivot = pivA.Northing + (U * dz);

            // Get steer points on curve
            Vec3 steerA = new Vec3(curvePoints[sA].Easting, curvePoints[sA].Northing, curvePoints[sA].Heading);
            Vec3 steerB = new Vec3(curvePoints[sB].Easting, curvePoints[sB].Northing, curvePoints[sB].Heading);

            if (!isHeadingSameWay)
            {
                steerA = new Vec3(curvePoints[sB].Easting, curvePoints[sB].Northing, curvePoints[sB].Heading);
                steerA.Heading += Math.PI;
                if (steerA.Heading > TwoPI) steerA.Heading -= TwoPI;

                steerB = new Vec3(curvePoints[sA].Easting, curvePoints[sA].Northing, curvePoints[sA].Heading);
                steerB.Heading += Math.PI;
                if (steerB.Heading > TwoPI) steerB.Heading -= TwoPI;
            }

            // Apply integral offset
            steerA.Easting += (Math.Sin(steerA.Heading + PIBy2) * input.PreviousIntegral);
            steerA.Northing += (Math.Cos(steerA.Heading + PIBy2) * input.PreviousIntegral);

            steerB.Easting += (Math.Sin(steerB.Heading + PIBy2) * input.PreviousIntegral);
            steerB.Northing += (Math.Cos(steerB.Heading + PIBy2) * input.PreviousIntegral);

            dx = steerB.Easting - steerA.Easting;
            dz = steerB.Northing - steerA.Northing;

            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            {
                output.DistanceFromCurrentLineSteer = 32000;
                output.GuidanceLineDistanceOff = 32000;
                return output;
            }

            // Calculate steer distance from curve
            output.DistanceFromCurrentLineSteer = ((dz * input.SteerPosition.Easting) - (dx * input.SteerPosition.Northing)
                + (steerB.Easting * steerA.Northing) - (steerB.Northing * steerA.Easting))
                / Math.Sqrt((dz * dz) + (dx * dx));

            // Calculate closest point on curve to steer position
            U = (((input.SteerPosition.Easting - steerA.Easting) * dx)
                + ((input.SteerPosition.Northing - steerA.Northing) * dz))
                / ((dx * dx) + (dz * dz));

            output.REastSteer = steerA.Easting + (U * dx);
            output.RNorthSteer = steerA.Northing + (U * dz);

            // Calculate heading error
            output.SteerHeadingError = input.SteerPosition.Heading - steerB.Heading;

            // Fix circular error
            if (output.SteerHeadingError > Math.PI)
                output.SteerHeadingError -= Math.PI;
            else if (output.SteerHeadingError < Math.PI)
                output.SteerHeadingError += Math.PI;

            if (output.SteerHeadingError > PIBy2)
                output.SteerHeadingError -= Math.PI;
            else if (output.SteerHeadingError < -PIBy2)
                output.SteerHeadingError += Math.PI;

            // Calculate final steer angle using Stanley algorithm
            CalculateSteerAngle(input, output);

            return output;
        }

        /// <summary>
        /// Core Stanley controller algorithm.
        /// Calculates final steer angle from cross-track error and heading error.
        /// </summary>
        private void CalculateSteerAngle(StanleyGuidanceInput input, StanleyGuidanceOutput output)
        {
            double steerHeadingError = output.SteerHeadingError;

            if (input.IsReverse)
                steerHeadingError *= -1;

            // Apply heading error gain (overshoot setting)
            steerHeadingError *= input.StanleyHeadingErrorGain;

            // Speed-dependent damping
            double sped = Math.Abs(input.AvgSpeed);
            if (sped > 1)
                sped = 1 + 0.277 * (sped - 1);
            else
                sped = 1;

            // Cross-track error correction using atan
            double XTEc = Math.Atan((output.DistanceFromCurrentLineSteer * input.StanleyDistanceErrorGain) / sped);

            // Filter cross-track correction
            output.XTrackSteerCorrection = (input.PreviousXTrackSteerCorrection * 0.5) + XTEc * 0.5;

            // Derivative of steer distance error
            output.DistSteerError = (input.PreviousDistSteerError * 0.95) + ((output.XTrackSteerCorrection * 60) * 0.05);
            output.Counter = input.PreviousCounter + 1;

            if (output.Counter > 5)
            {
                output.DerivativeDistError = output.DistSteerError - input.PreviousLastDistSteerError;
                output.LastDistSteerError = output.DistSteerError;
                output.Counter = 0;
            }
            else
            {
                output.DerivativeDistError = 0;
                output.LastDistSteerError = input.PreviousLastDistSteerError;
            }

            // Calculate base steer angle
            output.SteerAngle = ToDegrees((output.XTrackSteerCorrection + steerHeadingError) * -1.0);

            // Distance-based damping
            if (Math.Abs(output.DistanceFromCurrentLineSteer) > 0.5)
                output.SteerAngle *= 0.5;
            else
                output.SteerAngle *= (1 - Math.Abs(output.DistanceFromCurrentLineSteer));

            // Pivot PID (integral term)
            output.PivotDistanceError = (input.PreviousPivotDistanceError * 0.6) + (output.DistanceFromCurrentLinePivot * 0.4);

            // Update integral term
            if (input.AvgSpeed > 1
                && input.IsAutoSteerOn
                && Math.Abs(output.DerivativeDistError) < 1
                && Math.Abs(output.PivotDistanceError) < 0.25)
            {
                // If over the line heading wrong way, rapidly decrease integral
                if ((input.PreviousIntegral < 0 && output.DistanceFromCurrentLinePivot < 0)
                    || (input.PreviousIntegral > 0 && output.DistanceFromCurrentLinePivot > 0))
                {
                    output.Integral = input.PreviousIntegral + output.PivotDistanceError * input.StanleyIntegralGainAB * -0.03;
                }
                else
                {
                    output.Integral = input.PreviousIntegral + output.PivotDistanceError * input.StanleyIntegralGainAB * -0.01;
                }

                // Integral slider is set to 0
                if (input.StanleyIntegralGainAB == 0)
                    output.Integral = 0;
            }
            else
            {
                output.Integral = input.PreviousIntegral * 0.7;
            }

            if (input.IsReverse)
                output.Integral = 0;

            // Side hill compensation from roll angle
            if (input.ImuRoll != 88888)
                output.SteerAngle += input.ImuRoll * -input.SideHillCompFactor;

            // Limit steer angle to vehicle maximum
            if (output.SteerAngle < -input.MaxSteerAngle)
                output.SteerAngle = -input.MaxSteerAngle;
            else if (output.SteerAngle > input.MaxSteerAngle)
                output.SteerAngle = input.MaxSteerAngle;

            // Used for smooth mode
            output.ModeActualXTE = output.DistanceFromCurrentLinePivot;

            // Convert to millimeters and prepare for transmission
            output.GuidanceLineDistanceOff = (short)Math.Round(output.DistanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);
            output.GuidanceLineSteerAngle = (short)(output.SteerAngle * 100);
        }

        private double ToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
