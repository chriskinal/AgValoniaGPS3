using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for Stanley guidance algorithm calculations.
    /// </summary>
    public interface IStanleyGuidanceService
    {
        /// <summary>
        /// Calculate steering guidance for a straight AB line.
        /// </summary>
        /// <param name="curPtA">Start point of AB line segment</param>
        /// <param name="curPtB">End point of AB line segment</param>
        /// <param name="input">Stanley algorithm input parameters</param>
        /// <param name="isHeadingSameWay">True if heading same direction as AB line</param>
        /// <returns>Stanley guidance output</returns>
        StanleyGuidanceOutput CalculateGuidanceABLine(
            Vec3 curPtA,
            Vec3 curPtB,
            StanleyGuidanceInput input,
            bool isHeadingSameWay);

        /// <summary>
        /// Calculate steering guidance for a curved path.
        /// </summary>
        /// <param name="curvePoints">List of points defining the curve</param>
        /// <param name="input">Stanley algorithm input parameters</param>
        /// <param name="isHeadingSameWay">True if heading same direction as curve</param>
        /// <returns>Stanley guidance output with curve-specific data</returns>
        StanleyGuidanceCurveOutput CalculateGuidanceCurve(
            List<Vec3> curvePoints,
            StanleyGuidanceInput input,
            bool isHeadingSameWay);
    }
}
