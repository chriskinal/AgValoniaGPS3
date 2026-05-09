// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Periodic autosave for in-progress field state. Today only the coverage
// map is persisted on the timer — that's the highest-value data because
// it's what drives the "this row is done" map paint and the operator
// can't reproduce it without re-driving the field. Tracks, tram lines,
// tram systems, and job metadata still only persist on CloseFieldAsync;
// crash recovery for those is follow-up work.

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Autosave interval. 30 s balances "operator hasn't lost much on a
    /// crash" against "RLE compression cost on a large coverage map".
    /// At 30 s the worst case is ~30 s of paint loss; at faster intervals
    /// the background save thread risks overlapping with itself on slow
    /// disks (mitigated by the in-flight guard, but still wasteful).
    /// </summary>
    private const int CoverageAutosaveIntervalSeconds = 30;

    private DispatcherTimer? _coverageAutosaveTimer;

    /// <summary>
    /// Re-entrancy guard: if a save is already running on the background
    /// thread when the next tick fires we skip rather than queue. A long
    /// save followed by another long save would otherwise pile up.
    /// </summary>
    private int _coverageSaveInFlight;

    /// <summary>
    /// Start the periodic coverage autosave. Safe to call repeatedly —
    /// idempotent. Called from OpenFieldAsync after coverage is loaded.
    /// </summary>
    private void StartCoverageAutosave()
    {
        if (_coverageAutosaveTimer != null)
            return;

        _coverageAutosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(CoverageAutosaveIntervalSeconds)
        };
        _coverageAutosaveTimer.Tick += OnCoverageAutosaveTick;
        _coverageAutosaveTimer.Start();
        _logger.LogDebug("[Coverage] Autosave timer started ({Interval}s)", CoverageAutosaveIntervalSeconds);
    }

    /// <summary>
    /// Stop the autosave timer. Called from CloseFieldAsync (before the
    /// final close-save flushes coverage) and from ClearFieldState. Safe
    /// to call when the timer was never started.
    /// </summary>
    private void StopCoverageAutosave()
    {
        if (_coverageAutosaveTimer == null)
            return;

        _coverageAutosaveTimer.Stop();
        _coverageAutosaveTimer.Tick -= OnCoverageAutosaveTick;
        _coverageAutosaveTimer = null;
        _logger.LogDebug("[Coverage] Autosave timer stopped");
    }

    private void OnCoverageAutosaveTick(object? sender, EventArgs e)
    {
        // Fire-and-forget on the background thread. The timer callback
        // itself returns immediately so the dispatcher isn't blocked.
        _ = TryAutosaveCoverageAsync();
    }

    /// <summary>
    /// Persist the active job's coverage to disk if a field+job is open
    /// and no save is currently in flight. Internal so UI tests can drive
    /// the autosave path directly without poking the timer.
    /// </summary>
    internal async Task TryAutosaveCoverageAsync()
    {
        // Pre-conditions, in order of cheapest-first.
        if (!IsFieldOpen)
            return;

        var field = ActiveField;
        if (field == null || string.IsNullOrEmpty(field.DirectoryPath))
            return;

        var activeJob = _jobService.ActiveJob;
        if (activeJob == null)
            return;

        // Re-entrancy guard. Interlocked CAS so two ticks racing on the
        // dispatcher (shouldn't happen, but cheap insurance) can't both
        // proceed.
        if (Interlocked.CompareExchange(ref _coverageSaveInFlight, 1, 0) != 0)
        {
            _logger.LogDebug("[Coverage] Autosave skipped: previous save still in flight");
            return;
        }

        var savePath = field.DirectoryPath;
        var taskName = activeJob.TaskName;
        try
        {
            // Same call shape as the close-save path (MainViewModel.cs
            // CloseFieldAsync). RLE compression can take seconds on a
            // large field; the GPS cycle must not block on it.
            await Task.Run(() => _coverageMapService.SaveToFile(savePath, taskName));
            _logger.LogDebug("[Coverage] Autosaved coverage to {Path} job={Task}", savePath, taskName);
        }
        catch (Exception ex)
        {
            // Never throw out of the timer callback. A failed autosave is
            // a warning, not a fatal — the close-save will try again.
            _logger.LogWarning(ex, "[Coverage] Autosave failed");
        }
        finally
        {
            Interlocked.Exchange(ref _coverageSaveInFlight, 0);
        }
    }
}
