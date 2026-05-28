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

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Glanceable aggregate of the four hardware modules (GPS / IMU / AutoSteer / Machine),
/// shown as a single coloured button in the top status strip. The "configured set" is
/// the modules the user has marked as expected via the Network panel (defaults: all).
/// </summary>
public enum ModuleStatusKind
{
    /// <summary>Every configured module is currently producing data.</summary>
    AllPresent,

    /// <summary>At least one configured module is OK, at least one is absent.</summary>
    PartiallyPresent,

    /// <summary>No configured module is currently producing data.</summary>
    NonePresent,
}
