using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Input data for Stanley guidance algorithm calculations.
    /// </summary>
    public class StanleyGuidanceInput
    {
        // Vehicle position points
        public Vec3 PivotPosition { get; set; }
        public Vec3 SteerPosition { get; set; }

        // Vehicle configuration
        public double StanleyHeadingErrorGain { get; set; }
        public double StanleyDistanceErrorGain { get; set; }
        public double StanleyIntegralGainAB { get; set; }
        public double MaxSteerAngle { get; set; }
        public double SideHillCompFactor { get; set; }

        // Vehicle state
        public double AvgSpeed { get; set; }
        public bool IsReverse { get; set; }
        public bool IsAutoSteerOn { get; set; }

        // AHRS data
        public double ImuRoll { get; set; } // 88888 = invalid

        // Previous state for filtering/integration
        public double PreviousIntegral { get; set; }
        public double PreviousXTrackSteerCorrection { get; set; }
        public double PreviousDistSteerError { get; set; }
        public double PreviousLastDistSteerError { get; set; }
        public int PreviousCounter { get; set; }
        public double PreviousPivotDistanceError { get; set; }
    }
}
