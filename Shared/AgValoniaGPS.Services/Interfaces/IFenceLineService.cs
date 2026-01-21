// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Interfaces
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
