using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for fence line geometry calculations.
    /// </summary>
    public interface IFenceLineService
    {
        /// <summary>
        /// Calculate headings for each fence line point based on neighboring points.
        /// </summary>
        List<Vec3> CalculateHeadings(List<Vec3> fenceLine);

        /// <summary>
        /// Fix fence line spacing by adding/removing points.
        /// Also creates simplified line for ear clipping triangulation.
        /// </summary>
        List<Vec3> FixSpacing(List<Vec3> fenceLine, double area, int boundaryIndex, out List<Vec2> fenceLineEar);

        /// <summary>
        /// Reverse fence line winding direction.
        /// </summary>
        List<Vec3> ReverseWinding(List<Vec3> fenceLine);

        /// <summary>
        /// Calculate fence area and ensure correct winding.
        /// Outer boundaries should be counter-clockwise, inner boundaries clockwise.
        /// </summary>
        List<Vec3> CalculateAreaAndFixWinding(List<Vec3> fenceLine, int boundaryIndex, out double area);
    }
}
