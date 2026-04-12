// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

namespace AgValoniaGPS.Models.Timing;

/// <summary>
/// Static accessor for the global clock.
/// Default: SystemClock (real time).
/// Tests call Clock.Set(testClock) to swap in a controllable clock.
/// </summary>
public static class Clock
{
    private static IClock _instance = SystemClock.Instance;

    /// <summary>The current global clock instance.</summary>
    public static IClock Current => _instance;

    /// <summary>Replace the global clock (call from test setup).</summary>
    public static void Set(IClock clock) => _instance = clock;

    /// <summary>Reset to real-time system clock.</summary>
    public static void Reset() => _instance = SystemClock.Instance;
}
