using AgOpenGPS.Core.Models.Base;
using System.Collections.Generic;

namespace AgOpenGPS.Core.Models.Track
{
    /// <summary>
    /// Input for curve nudging calculation.
    /// </summary>
    public class CurveNudgeInput
    {
        /// <summary>
        /// Original curve points to nudge.
        /// </summary>
        public List<Vec3> CurvePoints { get; set; }

        /// <summary>
        /// Distance to nudge perpendicular to curve (positive = right, negative = left).
        /// </summary>
        public double Distance { get; set; }
    }
}
