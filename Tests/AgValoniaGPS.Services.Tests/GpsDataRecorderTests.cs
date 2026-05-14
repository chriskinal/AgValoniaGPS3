// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Globalization;
using System.Linq;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Logging;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Locks the CSV format of the debug-dump recorder. The four
/// goal-trajectory columns — <c>goal_e</c>, <c>goal_n</c>,
/// <c>goal_dist</c>, <c>forward_dot</c> — are load-bearing for
/// diagnosing U-turn drive-over: prior bug reports without them
/// were unfalsifiable (no way to tell if a steering swerve was
/// caused by a goal-point flip or a path-tangent flip).
/// </summary>
[TestFixture]
public class GpsDataRecorderTests
{
    private static GpsCycleResult MakeResult(
        double pivotE, double pivotN, double headingDeg,
        Vec2? goal, bool hasGuidance = true)
    {
        return new GpsCycleResult
        {
            Easting = pivotE,
            Northing = pivotN,
            Heading = headingDeg,
            Speed = 5,
            FixQuality = 4,
            IsAutoSteerEngaged = true,
            Guidance = new GuidanceSnapshot
            {
                GoalPoint = goal ?? new Vec2(),
                HasGuidance = hasGuidance,
                CrossTrackError = 0.05,
                SteerAngle = 1.0,
                HowManyPathsAway = 3,
            },
            YouTurn = new YouTurnSnapshot
            {
                IsExecuting = true,
                IsTriggered = true,
            },
        };
    }

    private static string[] LastDataLineFields(GpsDataRecorder rec)
    {
        var csv = rec.ExportCsv();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Last non-header line, trimmed for trailing CR on Windows.
        return lines[^1].TrimEnd('\r').Split(',');
    }

    [Test]
    public void Header_includes_four_new_goal_columns_in_order()
    {
        var rec = new GpsDataRecorder();
        var csv = rec.ExportCsv();
        var header = csv.Split('\n')[0].TrimEnd('\r');

        // The four columns must appear, in this order, at the tail of the
        // header. Order matters: the diagnostic awk pipelines in
        // forensic playbooks index by column number.
        var cols = header.Split(',');
        Assert.That(cols, Does.Contain("goal_e"));
        Assert.That(cols, Does.Contain("goal_n"));
        Assert.That(cols, Does.Contain("goal_dist"));
        Assert.That(cols, Does.Contain("forward_dot"));
        Assert.That(Array.IndexOf(cols, "goal_e"), Is.LessThan(Array.IndexOf(cols, "goal_n")));
        Assert.That(Array.IndexOf(cols, "goal_n"), Is.LessThan(Array.IndexOf(cols, "goal_dist")));
        Assert.That(Array.IndexOf(cols, "goal_dist"), Is.LessThan(Array.IndexOf(cols, "forward_dot")));
    }

    [Test]
    public void Goal_forward_of_pivot_writes_positive_forward_dot()
    {
        // Pivot at origin heading north (0°). Goal at (0, 3) — straight ahead.
        // Expected: goal_dist = 3, forward_dot = 3.
        var rec = new GpsDataRecorder();
        rec.Record(MakeResult(0, 0, 0, new Vec2 { Easting = 0, Northing = 3 }));

        var fields = LastDataLineFields(rec);
        // Column tail: ... headland_dist, goal_e, goal_n, goal_dist, forward_dot,
        //              A, B, ptCount, is_turn_left, anti_tangent_guard_fired
        // The goal-trajectory block is the last-9 .. last-6 columns.
        double goalE = double.Parse(fields[^9], CultureInfo.InvariantCulture);
        double goalN = double.Parse(fields[^8], CultureInfo.InvariantCulture);
        double dist = double.Parse(fields[^7], CultureInfo.InvariantCulture);
        double dot = double.Parse(fields[^6], CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(goalE, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(goalN, Is.EqualTo(3.0).Within(1e-6));
            Assert.That(dist, Is.EqualTo(3.0).Within(1e-3));
            Assert.That(dot, Is.EqualTo(3.0).Within(1e-3),
                "Goal directly ahead of a north-heading pivot must yield "
                + "forward_dot equal to the distance.");
        });
    }

    [Test]
    public void Goal_behind_pivot_writes_negative_forward_dot()
    {
        // Pivot heading north (0°). Goal at (0, -2) — directly behind.
        // The drive-over signature: forward_dot < 0 → goal behind pivot.
        var rec = new GpsDataRecorder();
        rec.Record(MakeResult(0, 0, 0, new Vec2 { Easting = 0, Northing = -2 }));

        var fields = LastDataLineFields(rec);
        double dot = double.Parse(fields[^6], CultureInfo.InvariantCulture);
        Assert.That(dot, Is.EqualTo(-2.0).Within(1e-3),
            "Anti-tangent goal (the drive-over signature) must record a "
            + "negative forward_dot so forensic filters can grep for it.");
    }

    [Test]
    public void Zero_goal_writes_blank_columns_not_zeros()
    {
        // When the cycle didn't publish a goal (Guidance off, or goal=(0,0)),
        // emit empty fields. A 0/0 row would falsely register as a
        // drive-over by any analyzer that filters on forward_dot < 0
        // (or 0) — the blanks make non-goal cycles unambiguous.
        var rec = new GpsDataRecorder();
        rec.Record(MakeResult(10, 20, 45, new Vec2()));

        var fields = LastDataLineFields(rec);
        Assert.Multiple(() =>
        {
            Assert.That(fields[^9], Is.Empty);
            Assert.That(fields[^8], Is.Empty);
            Assert.That(fields[^7], Is.Empty);
            Assert.That(fields[^6], Is.Empty);
        });
    }

    [Test]
    public void Heading_in_degrees_is_converted_to_radians_for_forward_dot()
    {
        // Pivot at origin heading 90° (east). Goal at (5, 0) — straight east.
        // sin(90°)=1, cos(90°)=0 → forward_dot = 5*1 + 0*0 = 5.
        var rec = new GpsDataRecorder();
        rec.Record(MakeResult(0, 0, 90, new Vec2 { Easting = 5, Northing = 0 }));

        var fields = LastDataLineFields(rec);
        double dot = double.Parse(fields[^6], CultureInfo.InvariantCulture);
        Assert.That(dot, Is.EqualTo(5.0).Within(1e-3),
            "Heading column is degrees; forward_dot computation must convert "
            + "to radians before sin/cos.");
    }

    [Test]
    public void Goal_dist_is_euclidean_distance_to_pivot()
    {
        // Pivot at (10, 10), goal at (13, 14). Distance = 5.
        var rec = new GpsDataRecorder();
        rec.Record(MakeResult(10, 10, 0, new Vec2 { Easting = 13, Northing = 14 }));

        var fields = LastDataLineFields(rec);
        double dist = double.Parse(fields[^7], CultureInfo.InvariantCulture);
        Assert.That(dist, Is.EqualTo(5.0).Within(1e-3));
    }

    [Test]
    public void Null_guidance_snapshot_writes_blank_goal_columns()
    {
        // No Guidance snapshot at all (e.g., cycle was non-guidance) — the
        // recorder must not crash, must emit blanks.
        var rec = new GpsDataRecorder();
        var result = new GpsCycleResult
        {
            Easting = 1,
            Northing = 2,
            Heading = 0,
            Guidance = null,
            YouTurn = null,
            HeadlandProximityDistance = null,
        };
        Assert.DoesNotThrow(() => rec.Record(result));
        var fields = LastDataLineFields(rec);
        Assert.That(fields[^9], Is.Empty);
        Assert.That(fields[^6], Is.Empty);
    }
}
