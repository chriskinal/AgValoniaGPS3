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
/// Snapshot of the device's battery state. Used by the strip's battery icon.
/// <see cref="IsAvailable"/> is false on machines without a battery (most
/// desktops) or when the platform API didn't return a usable reading; the
/// icon hides in that case.
/// </summary>
public readonly record struct BatteryStatus
{
    /// <summary>True when the platform exposes a real battery reading.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Charge fraction, 0.0 to 1.0. Undefined when <see cref="IsAvailable"/> is false.</summary>
    public double Level { get; init; }

    /// <summary>True when the device is plugged in / charging.</summary>
    public bool IsCharging { get; init; }

    public static BatteryStatus Unavailable => default;
}
