using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for Curve Pure Pursuit guidance algorithm calculations.
    /// </summary>
    public interface ICurvePurePursuitGuidanceService
    {
        /// <summary>
        /// Calculate steering guidance using Pure Pursuit algorithm for curved path.
        /// </summary>
        /// <param name="input">Pure Pursuit algorithm input parameters</param>
        /// <returns>Pure Pursuit guidance output</returns>
        CurvePurePursuitGuidanceOutput CalculateGuidanceCurve(CurvePurePursuitGuidanceInput input);
    }
}
