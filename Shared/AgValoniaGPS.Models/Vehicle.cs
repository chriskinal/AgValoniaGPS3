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
/// Represents the vehicle configuration (tractor, tool dimensions, etc.)
/// </summary>
public class Vehicle
{
    /// <summary>
    /// Vehicle name/description
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tool width in meters
    /// </summary>
    public double ToolWidth { get; set; }

    /// <summary>
    /// Tool offset from vehicle center in meters
    /// </summary>
    public double ToolOffset { get; set; }

    /// <summary>
    /// Antenna height above ground in meters
    /// </summary>
    public double AntennaHeight { get; set; }

    /// <summary>
    /// Antenna offset forward/backward from pivot in meters
    /// </summary>
    public double AntennaOffset { get; set; }

    /// <summary>
    /// Number of sections for section control
    /// </summary>
    public int NumberOfSections { get; set; }

    /// <summary>
    /// Wheelbase in meters
    /// </summary>
    public double Wheelbase { get; set; }

    /// <summary>
    /// Minimum turning radius in meters
    /// </summary>
    public double MinTurningRadius { get; set; }

    /// <summary>
    /// Whether this vehicle uses section control
    /// </summary>
    public bool IsSectionControlEnabled { get; set; }
}