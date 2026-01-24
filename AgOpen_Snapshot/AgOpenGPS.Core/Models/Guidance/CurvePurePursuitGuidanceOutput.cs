using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Output data from Curve Pure Pursuit guidance algorithm calculations.
    /// </summary>
    public class CurvePurePursuitGuidanceOutput
    {
        // Primary outputs
        public double SteerAngle { get; set; } // Calculated steer angle in degrees
        public short GuidanceLineDistanceOff { get; set; } // Distance from line in millimeters
        public short GuidanceLineSteerAngle { get; set; } // Steer angle * 100 for transmission

        // Distance measurements
        public double DistanceFromCurrentLinePivot { get; set; } // Pivot distance from line (m)

        // Pure pursuit specific outputs
        public Vec2 GoalPoint { get; set; } // Look-ahead goal point
        public Vec2 RadiusPoint { get; set; } // Center point of turning radius
        public double PurePursuitRadius { get; set; } // Turning radius for pure pursuit
        public double REast { get; set; } // Closest point on curve segment (east)
        public double RNorth { get; set; } // Closest point on curve segment (north)
        public double ManualUturnHeading { get; set; } // Heading at closest point

        // Curve tracking state (for next iteration)
        public int CurrentLocationIndex { get; set; }
        public bool FindGlobalNearestPoint { get; set; }

        // Heading and error tracking
        public double ModeActualHeadingError { get; set; } // Heading error in degrees
        public double ModeActualXTE { get; set; } // Cross-track error

        // State for next iteration (filters/integrators)
        public double Integral { get; set; }
        public double PivotDistanceError { get; set; }
        public double PivotDistanceErrorLast { get; set; }
        public double PivotDerivative { get; set; }
        public int Counter { get; set; }

        // End of curve detection
        public bool IsAtEndOfCurve { get; set; }
    }
}
