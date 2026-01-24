using AgOpenGPS.Core.Models.Base;
using System.Collections.Generic;

namespace AgOpenGPS.Core.Models.YouTurn
{
    /// <summary>
    /// Input data for U-turn guidance (following) calculation.
    /// </summary>
    public class YouTurnGuidanceInput
    {
        // Turn path to follow
        public List<Vec3> TurnPath { get; set; } = new List<Vec3>();

        // Vehicle position
        public Vec3 PivotPosition { get; set; }      // For Pure Pursuit
        public Vec3 SteerPosition { get; set; }      // For Stanley

        // Vehicle configuration
        public double Wheelbase { get; set; }
        public double MaxSteerAngle { get; set; }

        // Guidance algorithm selection
        public bool UseStanley { get; set; }         // true = Stanley, false = Pure Pursuit

        // Stanley-specific parameters
        public double StanleyHeadingErrorGain { get; set; }
        public double StanleyDistanceErrorGain { get; set; }

        // Pure Pursuit-specific parameters
        public double GoalPointDistance { get; set; }
        public double UTurnCompensation { get; set; }   // Multiplier for steer angle

        // Vehicle state
        public double FixHeading { get; set; }
        public double AvgSpeed { get; set; }
        public bool IsReverse { get; set; }

        // U-turn style
        public int UTurnStyle { get; set; }          // For determining completion logic
    }
}
