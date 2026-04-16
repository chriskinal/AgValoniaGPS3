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

namespace AgValoniaGPS.Models;

/// <summary>
/// Mode for creating AB lines/curves
/// </summary>
public enum ABCreationMode
{
    None,           // Not creating an AB line
    DriveAB,        // Drive from A to B - uses current position when tapping
    DrawAB,         // Draw on map - tap to place 2 points for straight line
    APlusLine,      // Create from current position + heading
    Curve,          // Record curve while driving
    DrawCurve       // Draw on map - tap to place multiple points for curve
}

/// <summary>
/// Which point is being set in AB creation
/// </summary>
public enum ABPointStep
{
    None,
    SettingPointA,
    SettingPointB
}
