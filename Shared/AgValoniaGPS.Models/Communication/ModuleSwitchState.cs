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

namespace AgValoniaGPS.Models.Communication
{
    /// <summary>
    /// Current UI state needed for module communication switch logic.
    /// Passed to Core service to determine what actions should be taken.
    /// </summary>
    public class ModuleSwitchState
    {
        /// <summary>
        /// Is AutoSteer currently enabled in UI?
        /// </summary>
        public bool IsAutoSteerOn { get; set; }

        /// <summary>
        /// Is AutoSteer in "Auto" mode (remote control)?
        /// </summary>
        public bool IsAutoSteerAuto { get; set; }

        /// <summary>
        /// Current state of Auto section button (Off, Auto, On)
        /// </summary>
        public int AutoButtonState { get; set; }

        /// <summary>
        /// Current state of Manual section button (Off, On)
        /// </summary>
        public int ManualButtonState { get; set; }
    }

    /// <summary>
    /// Button states enum (matches WinForms btnStates)
    /// </summary>
    public static class ButtonStates
    {
        public const int Off = 0;
        public const int Auto = 1;
        public const int On = 2;
    }
}
