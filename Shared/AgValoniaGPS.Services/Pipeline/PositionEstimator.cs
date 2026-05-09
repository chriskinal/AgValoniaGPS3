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

using System;
using System.Threading;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.Timing;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Pipeline;

/// <summary>
/// Lock-free position estimator. The latest <see cref="PoseSnapshot"/> is
/// stored as a single atomic reference; the GPS receive thread overwrites
/// it via <see cref="Interlocked.Exchange{T}"/> and any reader gets a
/// fully-consistent record without coordinating.
/// </summary>
public sealed class PositionEstimator : IPositionEstimator
{
    private PoseSnapshot? _latest;

    public double MaxStaleSeconds { get; set; } = 1.0;

    public void UpdateFromGps(PoseSnapshot snapshot)
    {
        // Derive yaw rate from the heading delta versus the previous snapshot
        // when none was supplied (e.g., the internal simulator has no IMU and
        // passes YawRate=0). Without this, GetPose dead-reckons heading as
        // constant between samples and snaps to the new value at each
        // arrival. That snap propagates into the hitch's lateral position
        // (which depends on heading × hitch length) and from there into the
        // dead-reckoned tool, producing visible per-sample jitter while
        // turning. Using the heading-delta-derived yaw rate makes heading
        // ramp smoothly across the inter-sample interval, killing the snap.
        var prev = Volatile.Read(ref _latest);
        if (prev is not null && Math.Abs(snapshot.YawRateRadPerSec) < 1e-9)
        {
            double dt = Clock.Current.ElapsedSeconds(prev.TimestampTicks, snapshot.TimestampTicks);
            if (dt > 1e-6)
            {
                double dh = snapshot.Heading - prev.Heading;
                while (dh > Math.PI) dh -= 2 * Math.PI;
                while (dh < -Math.PI) dh += 2 * Math.PI;
                snapshot = snapshot with { YawRateRadPerSec = dh / dt };
            }
        }
        Interlocked.Exchange(ref _latest, snapshot);
    }

    public PoseSnapshot? GetLatestSnapshot() => Volatile.Read(ref _latest);

    public InterpolatedPose GetPose(long nowTicks)
    {
        var snapshot = Volatile.Read(ref _latest);
        if (snapshot is null)
            return default;

        double dt = Clock.Current.ElapsedSeconds(snapshot.TimestampTicks, nowTicks);

        // Clamp to non-negative — a clock skew or out-of-order read shouldn't
        // rewind the prediction.
        if (dt < 0) dt = 0;

        // Cap dead reckoning to MaxStaleSeconds so long GPS dropouts don't
        // produce wild predictions; the watchdog upstream is the real safety.
        if (dt > MaxStaleSeconds) dt = MaxStaleSeconds;

        double heading = snapshot.Heading + snapshot.YawRateRadPerSec * dt;
        double dx = Math.Sin(heading) * snapshot.SpeedMps * dt;
        double dy = Math.Cos(heading) * snapshot.SpeedMps * dt;

        return new InterpolatedPose(
            new Vec2(snapshot.Position.Easting + dx, snapshot.Position.Northing + dy),
            heading,
            snapshot.SpeedMps,
            snapshot.Roll);
    }
}
