using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Track
{
    /// <summary>
    /// Input for AB line nudging calculation.
    /// </summary>
    public class ABLineNudgeInput
    {
        /// <summary>
        /// Point A of the AB line.
        /// </summary>
        public Vec2 PointA { get; set; }

        /// <summary>
        /// Point B of the AB line.
        /// </summary>
        public Vec2 PointB { get; set; }

        /// <summary>
        /// Heading of the AB line in radians.
        /// </summary>
        public double Heading { get; set; }

        /// <summary>
        /// Distance to nudge perpendicular to the line (positive = right, negative = left).
        /// </summary>
        public double Distance { get; set; }
    }
}
