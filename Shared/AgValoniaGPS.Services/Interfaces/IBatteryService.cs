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
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Reads the host device's battery state on a slow (~15 s) polling cadence.
/// Each platform contributes its own implementation; the Null variant is used
/// on platforms where no battery is present so callers can bind unconditionally.
/// </summary>
public interface IBatteryService
{
    /// <summary>Latest cached reading. Safe to read from any thread.</summary>
    BatteryStatus CurrentStatus { get; }

    /// <summary>Fires on the dispatcher thread when the reading changes.</summary>
    event EventHandler<BatteryStatus>? StatusChanged;

    /// <summary>Begin polling. Idempotent.</summary>
    void Start();

    /// <summary>Stop polling and release resources.</summary>
    void Stop();
}
