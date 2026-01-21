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

using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Unified track guidance service interface.
/// Handles both Pure Pursuit and Stanley algorithms.
/// Works with both AB lines (2 points) and curves (N points).
/// </summary>
public interface ITrackGuidanceService
{
    /// <summary>
    /// Calculate steering guidance for a track.
    /// </summary>
    /// <param name="input">Guidance input parameters including track, position, and configuration</param>
    /// <returns>Guidance output with steering angle, cross-track error, and updated state</returns>
    TrackGuidanceOutput CalculateGuidance(TrackGuidanceInput input);
}
