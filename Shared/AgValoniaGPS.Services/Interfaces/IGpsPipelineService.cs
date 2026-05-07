// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;

using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Orchestrates the GPS processing pipeline on a background thread:
///   GPS data -> drift compensation -> tool position -> guidance -> section control -> coverage -> GpsCycleResult.
/// The ViewModel only receives the finished result via <see cref="CycleCompleted"/>.
/// </summary>
public interface IGpsPipelineService
{
    /// <summary>
    /// Fires on a background thread with computed results for one GPS cycle.
    /// </summary>
    event Action<GpsCycleResult>? CycleCompleted;

    /// <summary>
    /// Start listening for GPS data and processing cycles.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop the pipeline and unsubscribe from GPS events.
    /// </summary>
    void Stop();

    // ── Operational state (set by ViewModel on user actions) ─────────────

    /// <summary>
    /// Tell the pipeline whether autosteer is engaged.
    /// </summary>
    void SetAutoSteerEngaged(bool engaged);

    /// <summary>
    /// Set the active track and current pass info.
    /// </summary>
    void SetActiveTrack(Models.Track.Track? track, int passNumber, double nudgeOffset, bool isOnBoundary);

    /// <summary>
    /// Enable/disable YouTurn processing.
    /// </summary>
    void SetYouTurnEnabled(bool enabled);

    /// <summary>
    /// Provide the headland polygon for YouTurn zone detection.
    /// </summary>
    void SetHeadlandLine(IReadOnlyList<Vec3>? headlandLine);

    /// <summary>
    /// Provide the current boundary for out-of-bounds detection.
    /// </summary>
    void SetBoundary(Models.Boundary? boundary);

    /// <summary>
    /// Set GPS drift compensation offsets (from user editing).
    /// </summary>
    void SetDriftCompensation(double driftE, double driftN);

    /// <summary>
    /// Push YouTurn configuration (skip-rows, skip-worked mode, headland
    /// geometry) so the cycle worker can build its own TickContext.
    /// </summary>
    void SetYouTurnConfig(int uTurnSkipRows, bool isSkipWorkedMode, double headlandCalculatedWidth, double headlandDistance);

    /// <summary>
    /// Tell the pipeline whether a real field is currently loaded. Used by the
    /// cycle worker to choose between silently re-anchoring the temporary
    /// origin (no field) and emitting a far-from-field warning (field loaded)
    /// when the live GPS position drifts beyond the configured threshold.
    /// </summary>
    void SetHasActiveField(bool hasActiveField);

    // ── Read-back state the ViewModel needs for commands ─────────────────

    /// <summary>Whether autosteer is currently engaged.</summary>
    bool IsAutoSteerEngaged { get; }

    /// <summary>Latest simulator steer angle (from guidance).</summary>
    double SimulatorSteerAngle { get; }
}
