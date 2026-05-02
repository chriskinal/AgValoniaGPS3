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
/// Immutable snapshot of vehicle pose + motion state at a single GPS sample
/// instant. Published by GpsService each time a GPS frame arrives, swapped
/// atomically into the position estimator. Readers receive a fully-consistent
/// record without locking.
///
/// Used by the host control loop (running at 100 Hz) to interpolate vehicle
/// pose between GPS samples (typically 10 Hz) so that section control,
/// guidance, and PGN sends can run at sub-frame resolution.
/// </summary>
public sealed record PoseSnapshot(
    Vec2 Position,
    double Heading,
    double SpeedMps,
    double YawRateRadPerSec,
    double Roll,
    long TimestampTicks);
