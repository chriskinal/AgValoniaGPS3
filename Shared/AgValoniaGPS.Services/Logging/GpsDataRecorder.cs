// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

    public GpsDataRecorder()
    {
        _buffer = new GpsRecord[BufferSize];
    }

    /// <summary>
    /// Record one GPS cycle result. Called from ApplyGpsCycleResult on the UI thread.
    /// </summary>
    public void Record(GpsCycleResult result)
    {
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
            HeadlandDistance = result.HeadlandProximityDistance,
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
            "autosteer,yt_triggered,yt_executing,headland_dist");

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
            sb.AppendLine(r.HeadlandDistance?.ToString("F2", ci) ?? "");
        }

        return sb.ToString();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _writeIndex = 0;
            _count = 0;
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
        public bool YouTurnTriggered, YouTurnExecuting;
        public double? HeadlandDistance;
    }
}
