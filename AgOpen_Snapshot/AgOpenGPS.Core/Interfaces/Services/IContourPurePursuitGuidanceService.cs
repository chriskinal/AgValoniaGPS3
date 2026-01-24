using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for Contour Pure Pursuit guidance algorithm calculations.
    /// </summary>
    public interface IContourPurePursuitGuidanceService
    {
        /// <summary>
        /// Calculate steering guidance using Pure Pursuit algorithm for contour path.
        /// </summary>
        /// <param name="input">Pure Pursuit algorithm input parameters</param>
        /// <returns>Pure Pursuit guidance output</returns>
        ContourPurePursuitGuidanceOutput CalculateGuidanceContour(ContourPurePursuitGuidanceInput input);
    }
}
