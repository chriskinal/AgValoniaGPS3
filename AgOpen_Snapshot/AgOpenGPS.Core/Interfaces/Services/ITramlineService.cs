using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for generating tramline offset paths from boundary fence lines.
    /// </summary>
    public interface ITramlineService
    {
        /// <summary>
        /// Generate inner tramline offset from boundary fence line.
        /// Inner tramline is offset inward by (tramWidth * 0.5) + halfWheelTrack.
        /// </summary>
        /// <param name="fenceLine">Boundary fence line points with headings</param>
        /// <param name="tramWidth">Width of tram passes</param>
        /// <param name="halfWheelTrack">Half of vehicle wheel track width</param>
        /// <returns>List of inner tramline points</returns>
        List<Vec2> GenerateInnerTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack);

        /// <summary>
        /// Generate outer tramline offset from boundary fence line.
        /// Outer tramline is offset inward by (tramWidth * 0.5) - halfWheelTrack.
        /// </summary>
        /// <param name="fenceLine">Boundary fence line points with headings</param>
        /// <param name="tramWidth">Width of tram passes</param>
        /// <param name="halfWheelTrack">Half of vehicle wheel track width</param>
        /// <returns>List of outer tramline points</returns>
        List<Vec2> GenerateOuterTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack);
    }
}
