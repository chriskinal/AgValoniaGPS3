using AgOpenGPS.Core.Models.Base;
using System.Collections.Generic;

namespace AgOpenGPS.Core.Models.Track
{
    /// <summary>
    /// Output from curve nudging calculation.
    /// </summary>
    public class CurveNudgeOutput
    {
        /// <summary>
        /// New curve points after nudging, filtering, and smoothing.
        /// Returns empty list if curve is too short (< 6 points after filtering).
        /// </summary>
        public List<Vec3> NewCurvePoints { get; set; }
    }
}
