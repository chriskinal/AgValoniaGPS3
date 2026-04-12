// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;

using AgValoniaGPS.Models.Base;
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
    /// Provide the active YouTurn path and triggered state.
    /// Called by the ViewModel when a U-turn is created or completed.
    /// </summary>
    void SetYouTurnState(bool isTriggered, bool isInYouTurn, List<Vec3>? youTurnPath);

    // ── Read-back state the ViewModel needs for commands ─────────────────

    /// <summary>Whether autosteer is currently engaged.</summary>
    bool IsAutoSteerEngaged { get; }

    /// <summary>Whether we are executing a U-turn.</summary>
    bool IsInYouTurn { get; }

    /// <summary>Latest simulator steer angle (from guidance).</summary>
    double SimulatorSteerAngle { get; }
}
