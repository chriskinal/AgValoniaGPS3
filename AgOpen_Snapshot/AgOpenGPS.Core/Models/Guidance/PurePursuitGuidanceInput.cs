using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Input data for Pure Pursuit guidance algorithm calculations.
    /// </summary>
    public class PurePursuitGuidanceInput
    {
        // Vehicle position
        public Vec3 PivotPosition { get; set; }

        // Current AB line endpoints
        public Vec3 CurrentLinePtA { get; set; }
        public Vec3 CurrentLinePtB { get; set; }

        // AB line properties
        public double ABHeading { get; set; }
        public bool IsHeadingSameWay { get; set; }

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

        // AHRS data
        public double ImuRoll { get; set; } // 88888 = invalid

        // Previous state for filtering/integration
        public double PreviousIntegral { get; set; }
        public double PreviousPivotDistanceError { get; set; }
        public double PreviousPivotDistanceErrorLast { get; set; }
        public int PreviousCounter { get; set; }
    }
}
