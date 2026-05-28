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
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Battery;

/// <summary>
/// Fallback used in tests and on platforms where no battery is present.
/// Always reports <see cref="BatteryStatus.Unavailable"/>; the strip's icon
/// is hidden when bound to this.
/// </summary>
public sealed class NullBatteryService : IBatteryService
{
    public BatteryStatus CurrentStatus => BatteryStatus.Unavailable;
    public event EventHandler<BatteryStatus>? StatusChanged { add { } remove { } }
    public void Start() { }
    public void Stop() { }
}
