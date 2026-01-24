using AgOpenGPS.Core.Models.Base;
using System.Collections.Generic;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Input data for Curve Pure Pursuit guidance algorithm calculations.
    /// </summary>
    public class CurvePurePursuitGuidanceInput
    {
        // Vehicle position
        public Vec3 PivotPosition { get; set; }

        // Curve points list
        public List<Vec3> CurvePoints { get; set; }

        // Curve tracking state
        public int CurrentLocationIndex { get; set; }
        public bool FindGlobalNearestPoint { get; set; }
        public bool IsHeadingSameWay { get; set; }
        public TrackMode TrackMode { get; set; }

        // Vehicle configuration
        public double Wheelbase { get; set; }
        public double MaxSteerAngle { get; set; }
        public double PurePursuitIntegralGain { get; set; }
        public double GoalPointDistance { get; set; }
        public double SideHillCompFactor { get; set; }

        // Vehicle state
        public double FixHeading { get; set; }
        public double AvgSpeed { get; set; }
        public bool IsReverse { get; set; }
        public bool IsAutoSteerOn { get; set; }
        public bool IsYouTurnTriggered { get; set; }

        // AHRS data
        public double ImuRoll { get; set; } // 88888 = invalid

        // Previous state for filtering/integration
        public double PreviousIntegral { get; set; }
        public double PreviousPivotDistanceError { get; set; }
        public double PreviousPivotDistanceErrorLast { get; set; }
        public int PreviousCounter { get; set; }
    }

    /// <summary>
    /// Track mode for curve guidance.
    /// Matches WinForms TrackMode enum values for compatibility.
    /// </summary>
    public enum TrackMode
    {
        None = 0,
        AB = 2,
        Curve = 4,
        bndTrackOuter = 8,
        bndTrackInner = 16,
        bndCurve = 32,
        waterPivot = 64
    }
}
