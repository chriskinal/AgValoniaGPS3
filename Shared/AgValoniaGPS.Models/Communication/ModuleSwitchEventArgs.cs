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

using System;

namespace AgValoniaGPS.Models.Communication
{
    /// <summary>
    /// Event args for AutoSteer toggle requests
    /// </summary>
    public class AutoSteerToggleEventArgs : EventArgs
    {
    }

    /// <summary>
    /// Event args for section master button toggle requests
    /// </summary>
    public class SectionMasterToggleEventArgs : EventArgs
    {
        /// <summary>
        /// Which button to toggle: Auto or Manual
        /// </summary>
        public SectionMasterButton Button { get; set; }

        public enum SectionMasterButton
        {
            Auto,
            Manual
        }
    }
}
