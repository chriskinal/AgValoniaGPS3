// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Diagnostics;

namespace AgValoniaGPS.Models.Timing;

/// <summary>
/// Global timing source abstraction. Use this instead of DateTime.Now or Stopwatch
/// so tests can control time progression.
///
/// Normal operation: SystemClock (real time).
/// Tests: TestClock (manually advanced, or running at Nx speed).
/// </summary>
public interface IClock
{
    /// <summary>Current local time.</summary>
    DateTime Now { get; }

    /// <summary>Current UTC time.</summary>
    DateTime UtcNow { get; }

    /// <summary>High-resolution timestamp (like Stopwatch.GetTimestamp).</summary>
    long GetTimestamp();

    /// <summary>Ticks per second for GetTimestamp (like Stopwatch.Frequency).</summary>
    long Frequency { get; }

    /// <summary>Elapsed seconds between two timestamps.</summary>
    double ElapsedSeconds(long startTimestamp, long endTimestamp);

    /// <summary>Elapsed milliseconds between two timestamps.</summary>
    double ElapsedMs(long startTimestamp, long endTimestamp);

    /// <summary>Time multiplier. 1.0 = real time, 10.0 = 10x speed.</summary>
    double TimeScale { get; set; }
}

/// <summary>
/// Real-time clock using system DateTime and Stopwatch. Default for production.
/// </summary>
public class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
    public long GetTimestamp() => Stopwatch.GetTimestamp();
    public long Frequency => Stopwatch.Frequency;

    public double ElapsedSeconds(long start, long end)
        => (double)(end - start) / Stopwatch.Frequency;

    public double ElapsedMs(long start, long end)
        => (double)(end - start) / Stopwatch.Frequency * 1000.0;

    public double TimeScale { get; set; } = 1.0;
}

/// <summary>
/// Test clock with manual time control. Advance time explicitly or set a speed multiplier.
/// </summary>
public class TestClock : IClock
{
    private DateTime _now;
    private long _ticks;

    public TestClock(DateTime? startTime = null)
    {
        _now = startTime ?? new DateTime(2026, 6, 15, 12, 0, 0);
        _ticks = 0;
    }

    public DateTime Now => _now;
    public DateTime UtcNow => _now.ToUniversalTime();
    public long GetTimestamp() => _ticks;
    public long Frequency => Stopwatch.Frequency;

    public double ElapsedSeconds(long start, long end)
        => (double)(end - start) / Stopwatch.Frequency;

    public double ElapsedMs(long start, long end)
        => (double)(end - start) / Stopwatch.Frequency * 1000.0;

    public double TimeScale { get; set; } = 1.0;

    /// <summary>Advance time by a specific duration.</summary>
    public void Advance(TimeSpan duration)
    {
        _now = _now.Add(duration);
        _ticks += (long)(duration.TotalSeconds * Stopwatch.Frequency);
    }

    /// <summary>Advance time by milliseconds.</summary>
    public void AdvanceMs(double ms)
        => Advance(TimeSpan.FromMilliseconds(ms));

    /// <summary>Advance time by seconds.</summary>
    public void AdvanceSeconds(double seconds)
        => Advance(TimeSpan.FromSeconds(seconds));

    /// <summary>Set absolute time.</summary>
    public void SetTime(DateTime time)
    {
        var diff = time - _now;
        _now = time;
        _ticks += (long)(diff.TotalSeconds * Stopwatch.Frequency);
    }
}
