using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;
using AgOpenGPS.Core.Services.Guidance;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for Stanley guidance calculations.
    /// Delegates algorithms to Core StanleyGuidanceService.
    /// </summary>
    public class CGuidance
    {
        private static readonly StanleyGuidanceService _coreStanleyService = new StanleyGuidanceService();
        private readonly FormGPS mf;

        //steer, pivot, and ref indexes
        private int sA, sB, C, pA, pB;

        //private int rA, rB;

        public double distanceFromCurrentLineSteer, distanceFromCurrentLinePivot;
        public double steerAngleGu, rEastSteer, rNorthSteer, rEastPivot, rNorthPivot;

        public double inty, xTrackSteerCorrection = 0;
        public double steerHeadingError;

        public double distSteerError, lastDistSteerError, derivativeDistError;

        public double pivotDistanceError;

        //public int modeTimeCounter = 0;

        //for adding steering angle based on side slope hill
        public double sideHillCompFactor;

        //derivative counter
        private int counter;

        public CGuidance(FormGPS _f)
        {
            //constructor
            mf = _f;
            sideHillCompFactor = Properties.Settings.Default.setAS_sideHillComp;
        }

        #region Stanley

        /// <summary>
        /// Function to calculate steer angle for AB Line Segment only
        /// No curvature calc on straight line
        /// </summary>
        public void StanleyGuidanceABLine(vec3 curPtA, vec3 curPtB, vec3 pivot, vec3 steer)
        {
            // Create input DTO from current state
            var input = new StanleyGuidanceInput
            {
                PivotPosition = new Vec3(pivot.easting, pivot.northing, pivot.heading),
                SteerPosition = new Vec3(steer.easting, steer.northing, steer.heading),
                StanleyHeadingErrorGain = mf.vehicle.stanleyHeadingErrorGain,
                StanleyDistanceErrorGain = mf.vehicle.stanleyDistanceErrorGain,
                StanleyIntegralGainAB = mf.vehicle.stanleyIntegralGainAB,
                MaxSteerAngle = mf.vehicle.maxSteerAngle,
                SideHillCompFactor = sideHillCompFactor,
                AvgSpeed = mf.avgSpeed,
                IsReverse = mf.isReverse,
                IsAutoSteerOn = mf.isBtnAutoSteerOn,
                ImuRoll = mf.ahrs.imuRoll,
                PreviousIntegral = inty,
                PreviousXTrackSteerCorrection = xTrackSteerCorrection,
                PreviousDistSteerError = distSteerError,
                PreviousLastDistSteerError = lastDistSteerError,
                PreviousCounter = counter,
                PreviousPivotDistanceError = pivotDistanceError
            };

            // Convert WinForms points to Core format
            Vec3 corePtA = new Vec3(curPtA.easting, curPtA.northing, curPtA.heading);
            Vec3 corePtB = new Vec3(curPtB.easting, curPtB.northing, curPtB.heading);

            // Delegate to Core service
            var output = _coreStanleyService.CalculateGuidanceABLine(corePtA, corePtB, input, mf.ABLine.isHeadingSameWay);

            // Unpack output DTO back to WinForms fields
            distanceFromCurrentLineSteer = output.DistanceFromCurrentLineSteer;
            distanceFromCurrentLinePivot = output.DistanceFromCurrentLinePivot;
            rEastSteer = output.REastSteer;
            rNorthSteer = output.RNorthSteer;
            rEastPivot = output.REastPivot;
            rNorthPivot = output.RNorthPivot;
            steerHeadingError = output.SteerHeadingError;
            steerAngleGu = output.SteerAngle;
            inty = output.Integral;
            xTrackSteerCorrection = output.XTrackSteerCorrection;
            distSteerError = output.DistSteerError;
            lastDistSteerError = output.LastDistSteerError;
            counter = output.Counter;
            pivotDistanceError = output.PivotDistanceError;
            derivativeDistError = output.DerivativeDistError;

            // Update FormGPS properties
            mf.ABLine.distanceFromCurrentLinePivot = distanceFromCurrentLinePivot;
            mf.ABLine.rEastAB = rEastPivot;
            mf.ABLine.rNorthAB = rNorthPivot;
            mf.vehicle.modeActualHeadingError = output.ModeActualHeadingError;
            mf.vehicle.modeActualXTE = output.ModeActualXTE;
            mf.guidanceLineDistanceOff = output.GuidanceLineDistanceOff;
            mf.guidanceLineSteerAngle = output.GuidanceLineSteerAngle;
        }

        /// <summary>
        /// Find the steer angle for a curve list, curvature and integral
        /// </summary>
        /// <param name="pivot">Pivot position vector</param>
        /// <param name="steer">Steer position vector</param>
        /// <param name="curList">the current list of guidance points</param>
        public void StanleyGuidanceCurve(vec3 pivot, vec3 steer, ref List<vec3> curList)
        {
            // Create input DTO from current state
            var input = new StanleyGuidanceInput
            {
                PivotPosition = new Vec3(pivot.easting, pivot.northing, pivot.heading),
                SteerPosition = new Vec3(steer.easting, steer.northing, steer.heading),
                StanleyHeadingErrorGain = mf.vehicle.stanleyHeadingErrorGain,
                StanleyDistanceErrorGain = mf.vehicle.stanleyDistanceErrorGain,
                StanleyIntegralGainAB = mf.vehicle.stanleyIntegralGainAB,
                MaxSteerAngle = mf.vehicle.maxSteerAngle,
                SideHillCompFactor = sideHillCompFactor,
                AvgSpeed = mf.avgSpeed,
                IsReverse = mf.isReverse,
                IsAutoSteerOn = mf.isBtnAutoSteerOn,
                ImuRoll = mf.ahrs.imuRoll,
                PreviousIntegral = inty,
                PreviousXTrackSteerCorrection = xTrackSteerCorrection,
                PreviousDistSteerError = distSteerError,
                PreviousLastDistSteerError = lastDistSteerError,
                PreviousCounter = counter,
                PreviousPivotDistanceError = pivotDistanceError
            };

            // Convert WinForms curve list to Core format
            List<Vec3> coreCurvePoints = new List<Vec3>(curList.Count);
            foreach (vec3 point in curList)
            {
                coreCurvePoints.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            var output = _coreStanleyService.CalculateGuidanceCurve(coreCurvePoints, input, mf.curve.isHeadingSameWay);

            // Unpack output DTO back to WinForms fields
            distanceFromCurrentLineSteer = output.DistanceFromCurrentLineSteer;
            distanceFromCurrentLinePivot = output.DistanceFromCurrentLinePivot;
            rEastSteer = output.REastSteer;
            rNorthSteer = output.RNorthSteer;
            rEastPivot = output.REastPivot;
            rNorthPivot = output.RNorthPivot;
            steerHeadingError = output.SteerHeadingError;
            steerAngleGu = output.SteerAngle;
            inty = output.Integral;
            xTrackSteerCorrection = output.XTrackSteerCorrection;
            distSteerError = output.DistSteerError;
            lastDistSteerError = output.LastDistSteerError;
            counter = output.Counter;
            pivotDistanceError = output.PivotDistanceError;
            derivativeDistError = output.DerivativeDistError;

            // Update FormGPS properties with curve-specific outputs
            mf.curve.distanceFromCurrentLinePivot = distanceFromCurrentLinePivot;
            mf.curve.rEastCu = rEastPivot;
            mf.curve.rNorthCu = rNorthPivot;
            mf.curve.currentLocationIndex = output.CurrentLocationIndex;
            mf.curve.manualUturnHeading = output.ManualUturnHeading;
            mf.vehicle.modeActualXTE = output.ModeActualXTE;
            mf.guidanceLineDistanceOff = output.GuidanceLineDistanceOff;
            mf.guidanceLineSteerAngle = output.GuidanceLineSteerAngle;
        }

        #endregion Stanley
    }
}