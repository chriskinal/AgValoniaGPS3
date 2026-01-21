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
    /// Output data from headland detection operations.
    /// </summary>
    public class HeadlandDetectionOutput
    {
        /// <summary>
        /// Whether the tool is in the headland area (based on outer points).
        /// </summary>
        public bool IsToolOuterPointsInHeadland { get; set; }

        /// <summary>
        /// Whether the left side of the tool is in headland.
        /// </summary>
        public bool IsLeftSideInHeadland { get; set; }

        /// <summary>
        /// Whether the right side of the tool is in headland.
        /// </summary>
        public bool IsRightSideInHeadland { get; set; }

        /// <summary>
        /// Headland status for each section (corner-based).
        /// </summary>
        public List<SectionHeadlandStatus> SectionStatus { get; set; } = new List<SectionHeadlandStatus>();

        /// <summary>
        /// Nearest point on headland boundary to vehicle.
        /// </summary>
        public Vec2? HeadlandNearestPoint { get; set; }

        /// <summary>
        /// Distance to nearest headland boundary point in meters.
        /// </summary>
        public double? HeadlandDistance { get; set; }

        /// <summary>
        /// Whether the proximity warning should be triggered.
        /// </summary>
        public bool ShouldTriggerWarning { get; set; }
    }

/// <summary>
/// Headland status for a single section.
/// </summary>
/// <param name="IsInHeadlandArea">Whether the section corners are in headland area.</param>
/// <param name="IsLookOnInHeadland">Whether the look-ahead points are in headland area.</param>
public record SectionHeadlandStatus(bool IsInHeadlandArea, bool IsLookOnInHeadland);
}
