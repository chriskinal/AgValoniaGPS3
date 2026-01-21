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
using AgValoniaGPS.Models.Tool;

namespace AgValoniaGPS.Models;

/// <summary>
/// LEGACY: Complete vehicle profile DTO for AgOpenGPS vehicle XML format.
/// Used only for loading/saving AgOpenGPS profile files.
/// Runtime configuration is stored in ConfigurationStore.Instance.
/// </summary>
[Obsolete("Use ConfigurationStore.Instance for runtime configuration. This class is for AgOpenGPS XML I/O only.")]
public class VehicleProfile
{
    /// <summary>
    /// Profile name (filename without .XML extension)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the profile file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Vehicle physical configuration (wheelbase, antenna, steering limits)
    /// </summary>
    public VehicleConfiguration Vehicle { get; set; } = new();

    /// <summary>
    /// Tool/implement configuration (width, sections, hitch)
    /// </summary>
    public ToolConfiguration Tool { get; set; } = new();

    /// <summary>
    /// YouTurn configuration (turn radius, extension, boundary distance)
    /// </summary>
    public YouTurnConfiguration YouTurn { get; set; } = new();

    /// <summary>
    /// Section positions (edge positions for each section in meters)
    /// Index 0 = leftmost edge, increasing indices move right
    /// </summary>
    public double[] SectionPositions { get; set; } = new double[17];

    /// <summary>
    /// Number of sections configured
    /// </summary>
    public int NumSections { get; set; } = 1;

    /// <summary>
    /// Whether metric units are used
    /// </summary>
    public bool IsMetric { get; set; } = false;

    /// <summary>
    /// Whether Pure Pursuit steering is used (vs Stanley)
    /// </summary>
    public bool IsPurePursuit { get; set; } = true;

    /// <summary>
    /// Simulator mode enabled
    /// </summary>
    public bool IsSimulatorOn { get; set; } = true;

    /// <summary>
    /// Simulator latitude
    /// </summary>
    public double SimLatitude { get; set; } = 32.5904315166667;

    /// <summary>
    /// Simulator longitude
    /// </summary>
    public double SimLongitude { get; set; } = -87.1804217333333;
}
