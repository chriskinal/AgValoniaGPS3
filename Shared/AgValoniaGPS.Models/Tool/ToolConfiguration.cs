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

using AgValoniaGPS.Models;

namespace AgValoniaGPS.Models.Tool
{
    /// <summary>
    /// Core model for implement/tool configuration and runtime state
    /// </summary>
    public class ToolConfiguration
    {
        // Tool dimensions
        public double Width { get; set; }
        public double HalfWidth { get; set; }
        public double ContourWidth { get; set; }
        public double Overlap { get; set; }
        public double Offset { get; set; }

        // Position and speed tracking
        public double FarLeftPosition { get; set; } = 0;
        public double FarLeftSpeed { get; set; } = 0;
        public double FarRightPosition { get; set; } = 0;
        public double FarRightSpeed { get; set; } = 0;

        // Hitch configuration
        public double TrailingHitchLength { get; set; }
        public double TankTrailingHitchLength { get; set; }
        public double TrailingToolToPivotLength { get; set; }
        public double HitchLength { get; set; }

        // Lookahead settings
        public double LookAheadOffSetting { get; set; }
        public double LookAheadOnSetting { get; set; }
        public double TurnOffDelay { get; set; }

        public double LookAheadDistanceOnPixelsLeft { get; set; }
        public double LookAheadDistanceOnPixelsRight { get; set; }
        public double LookAheadDistanceOffPixelsLeft { get; set; }
        public double LookAheadDistanceOffPixelsRight { get; set; }

        // Tool type flags
        public bool IsToolTrailing { get; set; }
        public bool IsToolTBT { get; set; }  // Tool Between Tanks
        public bool IsToolRearFixed { get; set; }
        public bool IsToolFrontFixed { get; set; }

        // Section configuration
        public int NumOfSections { get; set; }
        public int MinCoverage { get; set; }
        public bool IsMultiColoredSections { get; set; }
        public bool IsSectionOffWhenOut { get; set; }
        public bool IsHeadlandSectionControl { get; set; } = true;
        public bool IsSectionsNotZones { get; set; }

        // Headland detection
        public bool IsLeftSideInHeadland { get; set; } = true;
        public bool IsRightSideInHeadland { get; set; } = true;

        // Read pixel parameters
        public int RpXPosition { get; set; }
        public int RpWidth { get; set; }

        // Zone configuration
        public int Zones { get; set; }
        public int[] ZoneRanges { get; set; } = new int[9];

        // Display settings
        public bool IsDisplayTramControl { get; set; }

        // Section colors (16 sections max)
        public ColorRgb[] SectionColors { get; set; } = new ColorRgb[16];
    }
}
