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
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Pipeline;

/// <summary>
/// Test implementation of <see cref="ISteerMachineLoopService"/>. No timer;
/// tests call <see cref="Tick"/> directly to advance the loop.
/// </summary>
public sealed class ManualSteerMachineLoop : ISteerMachineLoopService
{
    public ManualSteerMachineLoop(double frequencyHz = 100.0)
    {
        FrequencyHz = frequencyHz;
    }

    public bool IsRunning { get; private set; }

    public double FrequencyHz { get; }

    public event Action<long>? Ticked;

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Tick(long timestampTicks)
    {
        if (!IsRunning)
            return;
        Ticked?.Invoke(timestampTicks);
    }
}
