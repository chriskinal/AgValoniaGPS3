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

namespace AgValoniaGPS.Models.Headland
{
    /// <summary>
    /// Input data for headland detection operations.
    /// </summary>
    public class HeadlandDetectionInput
    {
        /// <summary>
        /// List of boundaries with headland lines.
        /// First boundary is the outer boundary, rest are holes.
        /// </summary>
        public List<BoundaryData> Boundaries { get; set; } = new List<BoundaryData>();

        /// <summary>
        /// Vehicle/tool pivot position for proximity calculations.
        /// </summary>
        public Vec3 VehiclePosition { get; set; }

        /// <summary>
        /// Section corner points for detection.
        /// </summary>
        public List<SectionCornerData> Sections { get; set; } = new List<SectionCornerData>();

        /// <summary>
        /// Look-ahead distance configuration for section control.
        /// </summary>
        public LookAheadConfig LookAhead { get; set; } = new LookAheadConfig();

        /// <summary>
        /// Whether headland is active.
        /// </summary>
        public bool IsHeadlandOn { get; set; }
    }

    /// <summary>
    /// Boundary data with headland line.
    /// </summary>
    public class BoundaryData
    {
        /// <summary>
        /// Headland line points.
        /// </summary>
        public List<Vec3> HeadlandLine { get; set; } = new List<Vec3>();

        /// <summary>
        /// Whether this is a drive-through boundary (hole).
        /// </summary>
        public bool IsDriveThru { get; set; }
    }

    /// <summary>
    /// Section corner point data.
    /// </summary>
    public class SectionCornerData
    {
        /// <summary>
        /// Left corner point of section.
        /// </summary>
        public Vec2 LeftPoint { get; set; }

        /// <summary>
        /// Right corner point of section.
        /// </summary>
        public Vec2 RightPoint { get; set; }

        /// <summary>
        /// Section width in meters.
        /// </summary>
        public double SectionWidth { get; set; }
    }

    /// <summary>
    /// Look-ahead configuration for headland detection.
    /// </summary>
    public class LookAheadConfig
    {
        /// <summary>
        /// Look-ahead distance on left side in pixels.
        /// </summary>
        public double LookAheadDistanceOnPixelsLeft { get; set; }

        /// <summary>
        /// Look-ahead distance on right side in pixels.
        /// </summary>
        public double LookAheadDistanceOnPixelsRight { get; set; }

        /// <summary>
        /// Total tool width in meters.
        /// </summary>
        public double TotalWidth { get; set; }
    }
}
