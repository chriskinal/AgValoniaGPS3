using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Output data from Contour Pure Pursuit guidance algorithm calculations.
    /// </summary>
    public class ContourPurePursuitGuidanceOutput
    {
        // Primary outputs
        public double SteerAngle { get; set; } // Calculated steer angle in degrees
        public short GuidanceLineDistanceOff { get; set; } // Distance from line in millimeters
        public short GuidanceLineSteerAngle { get; set; } // Steer angle * 100 for transmission

        // Distance measurements
        public double DistanceFromCurrentLinePivot { get; set; } // Pivot distance from line (m)

        // Pure pursuit specific outputs
        public Vec2 GoalPoint { get; set; } // Look-ahead goal point
        public double REast { get; set; } // Closest point on contour segment (east)
        public double RNorth { get; set; } // Closest point on contour segment (north)

        // Contour tracking state
        public bool IsHeadingSameWay { get; set; }
        public bool IsLocked { get; set; } // Lock state (may change if at boundaries)

        // Mode tracking
        public double ModeActualXTE { get; set; } // Cross-track error

        // State for next iteration (filters/integrators)
        public double Integral { get; set; }
        public double PivotDistanceError { get; set; }
        public double PivotDistanceErrorLast { get; set; }
        public double PivotDerivative { get; set; }
        public int Counter { get; set; }
    }
}
