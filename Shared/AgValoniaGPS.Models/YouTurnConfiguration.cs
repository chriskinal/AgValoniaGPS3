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

namespace AgValoniaGPS.Models;

/// <summary>
/// LEGACY: YouTurn configuration for AgOpenGPS profile compatibility.
/// New code should use ConfigurationStore.Instance.Guidance.UTurn* properties.
/// This class is retained only for reading/writing AgOpenGPS vehicle XML files.
/// </summary>
[Obsolete("Use ConfigurationStore.Instance.Guidance instead. Retained for AgOpenGPS profile compatibility.")]
public class YouTurnConfiguration
{
    /// <summary>
    /// Radius of the U-turn arc in meters
    /// Maps to: set_youTurnRadius
    /// </summary>
    public double TurnRadius { get; set; } = 8.0;

    /// <summary>
    /// Extension length beyond headland boundary in meters
    /// Maps to: set_youTurnExtensionLength
    /// </summary>
    public double ExtensionLength { get; set; } = 20.0;

    /// <summary>
    /// Distance from boundary to start turn in meters
    /// Maps to: set_youTurnDistanceFromBoundary
    /// </summary>
    public double DistanceFromBoundary { get; set; } = 2.0;

    /// <summary>
    /// Skip width multiplier (how many passes to skip)
    /// Maps to: set_youSkipWidth
    /// </summary>
    public int SkipWidth { get; set; } = 1;

    /// <summary>
    /// U-turn style (0 = standard U-turn)
    /// Maps to: set_uTurnStyle
    /// </summary>
    public int Style { get; set; } = 0;

    /// <summary>
    /// Smoothing factor for U-turn path
    /// Maps to: setAS_uTurnSmoothing
    /// </summary>
    public int Smoothing { get; set; } = 14;

    /// <summary>
    /// Compensation factor for U-turn steering
    /// Maps to: setAS_uTurnCompensation
    /// </summary>
    public double UTurnCompensation { get; set; } = 1.0;
}
