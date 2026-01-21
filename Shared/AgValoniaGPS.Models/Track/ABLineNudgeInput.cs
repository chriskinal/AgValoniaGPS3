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

using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Track
{
    /// <summary>
    /// Input for AB line nudging calculation.
    /// </summary>
    public class ABLineNudgeInput
    {
        /// <summary>
        /// Point A of the AB line.
        /// </summary>
        public Vec2 PointA { get; set; }

        /// <summary>
        /// Point B of the AB line.
        /// </summary>
        public Vec2 PointB { get; set; }

        /// <summary>
        /// Heading of the AB line in radians.
        /// </summary>
        public double Heading { get; set; }

        /// <summary>
        /// Distance to nudge perpendicular to the line (positive = right, negative = left).
        /// </summary>
        public double Distance { get; set; }
    }
}
