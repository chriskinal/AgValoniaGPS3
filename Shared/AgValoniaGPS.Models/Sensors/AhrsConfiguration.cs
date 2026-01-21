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

namespace AgValoniaGPS.Models.Sensors
{
    /// <summary>
    /// Core model for IMU/AHRS (Attitude and Heading Reference System) sensor configuration and runtime data
    /// </summary>
    public class AhrsConfiguration
    {
        // Runtime sensor values
        public double ImuHeading { get; set; } = 99999;
        public double ImuRoll { get; set; } = 0;
        public double ImuPitch { get; set; } = 0;
        public double ImuYawRate { get; set; } = 0;
        public short AngularVelocity { get; set; }

        // Configuration values
        public double RollZero { get; set; }
        public double RollFilter { get; set; }
        public double FusionWeight { get; set; }
        public bool IsAutoSteerAuto { get; set; } = true;
        public double ForwardCompensation { get; set; }
        public double ReverseCompensation { get; set; }
        public bool IsRollInvert { get; set; }
        public bool IsReverseOn { get; set; }
        public bool IsDualAsIMU { get; set; }
        public bool AutoSwitchDualFixOn { get; set; }
        public double AutoSwitchDualFixSpeed { get; set; }
    }
}
