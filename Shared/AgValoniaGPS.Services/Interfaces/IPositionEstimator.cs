// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
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

using AgValoniaGPS.Models.Pipeline;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Holds the latest GPS-anchored pose snapshot and produces interpolated
/// poses on demand. Lets the host control loop run at a fixed rate
/// independent of the GPS arrival rate.
///
/// Single writer (the GPS receive thread, via <see cref="UpdateFromGps"/>),
/// many readers (control loop thread, renderer, anyone else needing pose).
/// Implementations swap an immutable snapshot atomically; readers see a
/// fully-consistent record with no locking.
/// </summary>
public interface IPositionEstimator
{
    /// <summary>
    /// Replace the latest snapshot. Called on the UDP receive thread when
    /// a fresh GPS frame arrives.
    /// </summary>
    void UpdateFromGps(PoseSnapshot snapshot);

    /// <summary>
    /// Latest snapshot, or null if no GPS sample has been observed yet.
    /// </summary>
    PoseSnapshot? GetLatestSnapshot();

    /// <summary>
    /// Pose dead-reckoned forward from the latest snapshot to the supplied
    /// timestamp. Heading advances by the snapshot's yaw rate; position
    /// advances by the velocity vector at the predicted heading. If no
    /// snapshot has been observed yet, returns the default pose.
    /// </summary>
    /// <param name="nowTicks">High-resolution timestamp from <c>Clock.Current</c>.</param>
    /// <returns>Interpolated pose, or default if no snapshot yet.</returns>
    InterpolatedPose GetPose(long nowTicks);

    /// <summary>
    /// Maximum age (seconds) of the latest snapshot before
    /// <see cref="GetPose"/> stops dead-reckoning forward and clamps to
    /// the snapshot pose. Protects against runaway prediction during
    /// extended GPS dropouts.
    /// </summary>
    double MaxStaleSeconds { get; set; }
}
