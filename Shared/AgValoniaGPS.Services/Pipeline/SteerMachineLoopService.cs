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
using System.Threading.Tasks;
using AgValoniaGPS.Models.Timing;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Pipeline;

/// <summary>
/// Production control loop. Runs a <see cref="PeriodicTimer"/> at the
/// configured frequency on a dedicated background task. Each tick
/// raises <see cref="Ticked"/> synchronously on the loop thread.
///
/// 100 Hz is the default and matches the firmware autosteer cadence so
/// PGN 254 sends never starve the firmware loop.
/// </summary>
public sealed class SteerMachineLoopService : ISteerMachineLoopService, IDisposable, IAsyncDisposable
{
    private readonly object _lifecycleLock = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public SteerMachineLoopService(double frequencyHz = 100.0)
    {
        if (frequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequencyHz),
                "FrequencyHz must be positive");
        FrequencyHz = frequencyHz;
    }

    public bool IsRunning
    {
        get
        {
            lock (_lifecycleLock)
                return _cts is not null && !_cts.IsCancellationRequested;
        }
    }

    public double FrequencyHz { get; }

    public event Action<long>? Ticked;

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_cts is not null && !_cts.IsCancellationRequested)
                return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loopTask = Task.Run(() => RunLoopAsync(token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_lifecycleLock)
        {
            cts = _cts;
            task = _loopTask;
            _cts = null;
            _loopTask = null;
        }
        if (cts is null) return;
        cts.Cancel();
        try { task?.Wait(TimeSpan.FromSeconds(1)); }
        catch (AggregateException) { /* expected on cancellation */ }
        cts.Dispose();
    }

    public void Tick(long timestampTicks)
    {
        // Production timer drives ticks internally, but allow manual fire
        // so callers can synthesize a tick at startup or for diagnostics.
        Ticked?.Invoke(timestampTicks);
    }

    public void Dispose() => Stop();

    public async ValueTask DisposeAsync()
    {
        Stop();
        await Task.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        var period = TimeSpan.FromSeconds(1.0 / FrequencyHz);
        using var timer = new PeriodicTimer(period);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                Ticked?.Invoke(Clock.Current.GetTimestamp());
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
    }
}
