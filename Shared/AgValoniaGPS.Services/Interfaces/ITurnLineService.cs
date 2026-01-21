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
    /// Service for turn line (headland) geometry calculations.
    /// </summary>
    public interface ITurnLineService
    {
        /// <summary>
        /// Calculate headings for turn line points.
        /// Adds duplicate first/last points with forward-looking headings.
        /// </summary>
        List<Vec3> CalculateHeadings(List<Vec3> turnLine);

        /// <summary>
        /// Fix turn line spacing by removing points too close to fence line
        /// and optimizing spacing between remaining points.
        /// </summary>
        List<Vec3> FixSpacing(List<Vec3> turnLine, List<Vec3> fenceLine, double totalHeadWidth, double spacing);
    }
}
