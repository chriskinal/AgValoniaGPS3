using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Track
{
    /// <summary>
    /// Output from AB line nudging calculation.
    /// </summary>
    public class ABLineNudgeOutput
    {
        /// <summary>
        /// New Point A after nudging.
        /// </summary>
        public Vec2 NewPointA { get; set; }

        /// <summary>
        /// New Point B after nudging.
        /// </summary>
        public Vec2 NewPointB { get; set; }
    }
}
