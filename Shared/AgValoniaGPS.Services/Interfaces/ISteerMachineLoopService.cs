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

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Fixed-rate host control loop. Runs at a target frequency
/// (default 100 Hz, matching the firmware autosteer cadence) on a
/// dedicated thread. Each tick raises <see cref="Ticked"/> with the
/// timestamp of the tick; subscribers (section control, autosteer,
/// guidance) do their work in response.
///
/// Production impl uses <c>PeriodicTimer</c>; test impl is driven
/// manually so unit tests can advance virtual time deterministically.
/// </summary>
public interface ISteerMachineLoopService
{
    /// <summary>
    /// Begin ticking. No-op if already running.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop ticking. No-op if already stopped. Subscribers may still
    /// see one tick already in flight.
    /// </summary>
    void Stop();

    bool IsRunning { get; }

    /// <summary>
    /// Target tick frequency in Hz. 100 Hz by default.
    /// </summary>
    double FrequencyHz { get; }

    /// <summary>
    /// Manually fire a tick. Test hook only — production timer drives
    /// this internally.
    /// </summary>
    /// <param name="timestampTicks">
    /// Timestamp to publish with the tick. Production passes
    /// <c>Clock.Current.GetTimestamp()</c>.
    /// </param>
    void Tick(long timestampTicks);

    /// <summary>
    /// Raised on every tick. The argument is the tick timestamp from
    /// <c>Clock.Current</c>. Subscribers run synchronously on the
    /// loop thread.
    /// </summary>
    event Action<long>? Ticked;
}
