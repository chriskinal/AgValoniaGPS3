using AgOpenGPS.Core.Models.Base;
using System.Collections.Generic;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Input data for Contour Pure Pursuit guidance algorithm calculations.
    /// </summary>
    public class ContourPurePursuitGuidanceInput
    {
        // Vehicle position
        public Vec3 PivotPosition { get; set; }
        public Vec3 FixPosition { get; set; } // Used for distance calculation

        // Contour points list
        public List<Vec3> ContourPoints { get; set; }

        // Lock state
        public bool IsLocked { get; set; }

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
}
