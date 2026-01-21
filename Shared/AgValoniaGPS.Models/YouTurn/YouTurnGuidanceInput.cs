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
    /// Input data for U-turn guidance (following) calculation.
    /// </summary>
    public class YouTurnGuidanceInput
    {
        // Turn path to follow
        public List<Vec3> TurnPath { get; set; } = new List<Vec3>();

        // Vehicle position
        public Vec3 PivotPosition { get; set; }      // For Pure Pursuit
        public Vec3 SteerPosition { get; set; }      // For Stanley

        // Vehicle configuration
        public double Wheelbase { get; set; }
        public double MaxSteerAngle { get; set; }

        // Guidance algorithm selection
        public bool UseStanley { get; set; }         // true = Stanley, false = Pure Pursuit

        // Stanley-specific parameters
        public double StanleyHeadingErrorGain { get; set; }
        public double StanleyDistanceErrorGain { get; set; }

        // Pure Pursuit-specific parameters
        public double GoalPointDistance { get; set; }
        public double UTurnCompensation { get; set; }   // Multiplier for steer angle

        // Vehicle state
        public double FixHeading { get; set; }
        public double AvgSpeed { get; set; }
        public bool IsReverse { get; set; }

        // U-turn style
        public int UTurnStyle { get; set; }          // For determining completion logic
    }
}
