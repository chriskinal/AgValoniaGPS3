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
using System.Collections.Generic;

namespace AgValoniaGPS.Models.YouTurn
{
    /// <summary>
    /// Output data from U-turn path creation.
    /// </summary>
    public class YouTurnCreationOutput
    {
        /// <summary>
        /// True if turn was successfully created.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Reason for failure (if Success = false).
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Generated U-turn path points.
        /// </summary>
        public List<Vec3> TurnPath { get; set; } = new List<Vec3>();

        /// <summary>
        /// True if turn goes to same curve/line (doubling back).
        /// False if turn goes to next offset line.
        /// </summary>
        public bool IsOutSameCurve { get; set; }

        /// <summary>
        /// True if vehicle continues same direction (straight through headland).
        /// </summary>
        public bool IsGoingStraightThrough { get; set; }

        /// <summary>
        /// True if turn is out of bounds (intersects boundaries).
        /// </summary>
        public bool IsOutOfBounds { get; set; }

        /// <summary>
        /// Distance from pivot to turn line at start of turn.
        /// </summary>
        public double DistancePivotToTurnLine { get; set; }

        /// <summary>
        /// Closest point on turn line where turn starts.
        /// </summary>
        public Vec3 InClosestTurnPoint { get; set; }

        /// <summary>
        /// Closest point on turn line where turn ends.
        /// </summary>
        public Vec3 OutClosestTurnPoint { get; set; }
    }
}
