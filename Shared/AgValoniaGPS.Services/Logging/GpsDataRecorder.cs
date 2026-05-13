// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.Services.Logging;

/// <summary>
/// Ring buffer that records GPS cycle data for debugging.
/// Captures position, heading, guidance, YouTurn state every cycle.
/// Included in debug dump for comparing real-app behavior to test scenarios.
/// </summary>
public sealed class GpsDataRecorder
{
    private static readonly Lazy<GpsDataRecorder> _instance = new(() => new GpsDataRecorder());
    public static GpsDataRecorder Instance => _instance.Value;

    private readonly GpsRecord[] _buffer;
    private int _writeIndex;
    private int _count;
    private readonly object _lock = new();

    /// <summary>Number of records to keep (at 10Hz = 60 seconds).</summary>
    public const int BufferSize = 600;

    /// <summary>
    /// Last captured TurnPath, retained for the debug-dump sidecar. Cleared
    /// to <c>null</c> when a turn completes or the field closes. Replaces
    /// the legacy "we drop the in-flight path on completion" assumption
    /// with explicit retention for one bug-report cycle so the operator
    /// can capture a dump immediately after a drive-over without losing
    /// the path that caused it.
    /// </summary>
    private IReadOnlyList<Vec3>? _lastTurnPath;

    public GpsDataRecorder()
    {
        _buffer = new GpsRecord[BufferSize];
    }

    /// <summary>
    /// Record one GPS cycle result. Called from ApplyGpsCycleResult on the UI thread.
    /// </summary>
    public void Record(GpsCycleResult result)
    {
        // Goal point comes off the Guidance snapshot. It's (0,0) on cycles
        // where guidance didn't publish — the recorder treats those as
        // "no goal this cycle" (CSV columns blank) so the trace is
        // unambiguous about which cycles had a real lookahead carrot.
        var goal = result.Guidance?.GoalPoint;
        bool goalPublished = goal.HasValue
            && (goal.Value.Easting != 0 || goal.Value.Northing != 0);

        // Capture the most-recent non-null TurnPath. Retained for the
        // dump sidecar so a forensic replay has the exact production
        // geometry that produced the failure window.
        if (result.YouTurn?.TurnPath is { Count: > 0 } tp)
            _lastTurnPath = tp;

        var record = new GpsRecord
        {
            Timestamp = DateTime.UtcNow,
            Easting = result.Easting,
            Northing = result.Northing,
            Heading = result.Heading,
            Speed = result.Speed,
            FixQuality = result.FixQuality,
            RollDegrees = result.RollDegrees,
            ToolEasting = result.ToolEasting,
            ToolNorthing = result.ToolNorthing,
            ToolHeadingRad = result.ToolHeadingRadians,
            SteerAngle = result.Guidance?.SteerAngle ?? 0,
            CrossTrackError = result.Guidance?.CrossTrackError ?? 0,
            HasGuidance = result.Guidance?.HasGuidance ?? false,
            HowManyPathsAway = result.Guidance?.HowManyPathsAway ?? 0,
            IsAutoSteerEngaged = result.IsAutoSteerEngaged,
            YouTurnTriggered = result.YouTurn?.IsTriggered ?? false,
            YouTurnExecuting = result.YouTurn?.IsExecuting ?? false,
            IsTurnLeft = result.YouTurn?.IsTurnLeft ?? false,
            HeadlandDistance = result.HeadlandProximityDistance,
            GoalEasting = goalPublished ? goal!.Value.Easting : (double?)null,
            GoalNorthing = goalPublished ? goal!.Value.Northing : (double?)null,
            PathAnchorA = result.Guidance?.PathAnchorA ?? 0,
            PathAnchorB = result.Guidance?.PathAnchorB ?? 0,
            TurnPathPointCount = result.Guidance?.TurnPathPointCount ?? 0,
            AntiTangentGuardFired = result.Guidance?.AntiTangentGuardFired ?? false,
        };

        lock (_lock)
        {
            _buffer[_writeIndex] = record;
            _writeIndex = (_writeIndex + 1) % BufferSize;
            if (_count < BufferSize) _count++;
        }
    }

    /// <summary>
    /// Export all recorded data as CSV string for inclusion in debug dump.
    /// </summary>
    public string ExportCsv()
    {
        GpsRecord[] snapshot;
        int count, start;

        lock (_lock)
        {
            count = _count;
            start = count < BufferSize ? 0 : _writeIndex;
            snapshot = new GpsRecord[count];
            for (int i = 0; i < count; i++)
                snapshot[i] = _buffer[(start + i) % BufferSize];
        }

        var sb = new StringBuilder();
        sb.AppendLine("timestamp,easting,northing,heading,speed,fix,roll," +
            "tool_e,tool_n,tool_h_rad,steer_angle,xte,has_guidance,paths_away," +
            "autosteer,yt_triggered,yt_executing,headland_dist," +
            "goal_e,goal_n,goal_dist,forward_dot," +
            "A,B,ptCount,is_turn_left,anti_tangent_guard_fired");

        var ci = CultureInfo.InvariantCulture;
        foreach (var r in snapshot)
        {
            sb.Append(r.Timestamp.ToString("HH:mm:ss.fff", ci)); sb.Append(',');
            sb.Append(r.Easting.ToString("F3", ci)); sb.Append(',');
            sb.Append(r.Northing.ToString("F3", ci)); sb.Append(',');
            sb.Append(r.Heading.ToString("F2", ci)); sb.Append(',');
            sb.Append(r.Speed.ToString("F2", ci)); sb.Append(',');
            sb.Append(r.FixQuality); sb.Append(',');
            sb.Append(r.RollDegrees.ToString("F2", ci)); sb.Append(',');
            sb.Append(r.ToolEasting.ToString("F3", ci)); sb.Append(',');
            sb.Append(r.ToolNorthing.ToString("F3", ci)); sb.Append(',');
            sb.Append(r.ToolHeadingRad.ToString("F4", ci)); sb.Append(',');
            sb.Append(r.SteerAngle.ToString("F3", ci)); sb.Append(',');
            sb.Append(r.CrossTrackError.ToString("F4", ci)); sb.Append(',');
            sb.Append(r.HasGuidance ? "1" : "0"); sb.Append(',');
            sb.Append(r.HowManyPathsAway); sb.Append(',');
            sb.Append(r.IsAutoSteerEngaged ? "1" : "0"); sb.Append(',');
            sb.Append(r.YouTurnTriggered ? "1" : "0"); sb.Append(',');
            sb.Append(r.YouTurnExecuting ? "1" : "0"); sb.Append(',');
            sb.Append(r.HeadlandDistance?.ToString("F2", ci) ?? ""); sb.Append(',');

            // Goal-trajectory columns. Blank when no goal was published.
            if (r.GoalEasting.HasValue && r.GoalNorthing.HasValue)
            {
                double gE = r.GoalEasting.Value;
                double gN = r.GoalNorthing.Value;
                double dx = gE - r.Easting;
                double dy = gN - r.Northing;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double headingRad = r.Heading * Math.PI / 180.0;
                double forwardDot = dx * Math.Sin(headingRad) + dy * Math.Cos(headingRad);

                sb.Append(gE.ToString("F3", ci)); sb.Append(',');
                sb.Append(gN.ToString("F3", ci)); sb.Append(',');
                sb.Append(dist.ToString("F3", ci)); sb.Append(',');
                sb.Append(forwardDot.ToString("F3", ci)); sb.Append(',');
            }
            else
            {
                sb.Append(",,,,");
            }

            // Path-anchor / direction / guard diagnostic columns.
            sb.Append(r.PathAnchorA); sb.Append(',');
            sb.Append(r.PathAnchorB); sb.Append(',');
            sb.Append(r.TurnPathPointCount); sb.Append(',');
            sb.Append(r.IsTurnLeft ? "1" : "0"); sb.Append(',');
            sb.AppendLine(r.AntiTangentGuardFired ? "1" : "0");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Return the most-recently observed non-null TurnPath, or <c>null</c>
    /// if none has been seen since the recorder cleared. Consumed by
    /// <c>DebugDumpService</c> to attach <c>turn_path.json</c> as a
    /// forensic sidecar.
    /// </summary>
    public IReadOnlyList<Vec3>? GetLastTurnPath()
    {
        lock (_lock) return _lastTurnPath;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _writeIndex = 0;
            _count = 0;
            _lastTurnPath = null;
        }
    }

    public int Count { get { lock (_lock) return _count; } }

    private struct GpsRecord
    {
        public DateTime Timestamp;
        public double Easting, Northing, Heading, Speed;
        public int FixQuality;
        public double RollDegrees;
        public double ToolEasting, ToolNorthing, ToolHeadingRad;
        public double SteerAngle, CrossTrackError;
        public bool HasGuidance;
        public int HowManyPathsAway;
        public bool IsAutoSteerEngaged;
        public bool YouTurnTriggered, YouTurnExecuting, IsTurnLeft;
        public double? HeadlandDistance;
        public double? GoalEasting, GoalNorthing;
        public int PathAnchorA, PathAnchorB, TurnPathPointCount;
        public bool AntiTangentGuardFired;
    }
}
