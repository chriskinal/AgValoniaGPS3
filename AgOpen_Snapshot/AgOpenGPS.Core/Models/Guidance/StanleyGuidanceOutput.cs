namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Output data from Stanley guidance algorithm calculations.
    /// </summary>
    public class StanleyGuidanceOutput
    {
        // Primary outputs
        public double SteerAngle { get; set; } // Calculated steer angle in degrees
        public short GuidanceLineDistanceOff { get; set; } // Distance from line in millimeters
        public short GuidanceLineSteerAngle { get; set; } // Steer angle * 100 for transmission

        // Distance measurements
        public double DistanceFromCurrentLineSteer { get; set; } // Steer axle distance from line (m)
        public double DistanceFromCurrentLinePivot { get; set; } // Pivot distance from line (m)

        // Closest points on guidance line
        public double REastSteer { get; set; }
        public double RNorthSteer { get; set; }
        public double REastPivot { get; set; }
        public double RNorthPivot { get; set; }

        // Heading and error tracking
        public double SteerHeadingError { get; set; } // Heading error in radians
        public double ModeActualHeadingError { get; set; } // Heading error in degrees
        public double ModeActualXTE { get; set; } // Cross-track error

        // State for next iteration (filters/integrators)
        public double Integral { get; set; }
        public double XTrackSteerCorrection { get; set; }
        public double DistSteerError { get; set; }
        public double LastDistSteerError { get; set; }
        public int Counter { get; set; }
        public double PivotDistanceError { get; set; }
        public double DerivativeDistError { get; set; }
    }
}
