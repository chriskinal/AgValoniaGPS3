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

using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Pipeline;

/// <summary>
/// Pose interpolated from the latest <see cref="PoseSnapshot"/> using
/// dead reckoning: heading is advanced by the snapshot's yaw rate and
/// position is advanced by the velocity vector at the predicted heading.
/// Returned by <c>IPositionEstimator.GetPose(now)</c>.
/// </summary>
public readonly record struct InterpolatedPose(
    Vec2 Position,
    double Heading,
    double SpeedMps,
    double Roll);
