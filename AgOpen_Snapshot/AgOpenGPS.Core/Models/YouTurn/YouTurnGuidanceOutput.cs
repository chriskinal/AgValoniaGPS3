using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.YouTurn
{
    /// <summary>
    /// Output data from U-turn guidance calculation.
    /// </summary>
    public class YouTurnGuidanceOutput
    {
        /// <summary>
        /// True if turn is complete (reached end of path).
        /// </summary>
        public bool IsTurnComplete { get; set; }

        /// <summary>
        /// Calculated steering angle in degrees.
        /// </summary>
        public double SteerAngle { get; set; }

        /// <summary>
        /// Distance from current line in meters (+ = right, - = left).
        /// </summary>
        public double DistanceFromCurrentLine { get; set; }

        /// <summary>
        /// Reference point on turn path closest to vehicle (easting).
        /// </summary>
        public double REast { get; set; }

        /// <summary>
        /// Reference point on turn path closest to vehicle (northing).
        /// </summary>
        public double RNorth { get; set; }

        // Pure Pursuit specific outputs
        /// <summary>
        /// Goal point for Pure Pursuit (lookahead point).
        /// </summary>
        public Vec2 GoalPoint { get; set; }

        /// <summary>
        /// Radius point for Pure Pursuit visualization.
        /// </summary>
        public Vec2 RadiusPoint { get; set; }

        /// <summary>
        /// Pure Pursuit radius value.
        /// </summary>
        public double PPRadius { get; set; }

        /// <summary>
        /// Closest point indices (for path tracking).
        /// </summary>
        public int PointA { get; set; }
        public int PointB { get; set; }

        /// <summary>
        /// Distance from current line in millimeters (for UI display).
        /// </summary>
        public short GuidanceLineDistanceOff { get; set; }

        /// <summary>
        /// Steering angle in hundredths of degrees (for UI display).
        /// </summary>
        public short GuidanceLineSteerAngle { get; set; }

        /// <summary>
        /// Mode actual cross-track error (for smooth mode).
        /// </summary>
        public double ModeActualXTE { get; set; }

        /// <summary>
        /// Remaining path count.
        /// </summary>
        public int PathCount { get; set; }
    }
}
