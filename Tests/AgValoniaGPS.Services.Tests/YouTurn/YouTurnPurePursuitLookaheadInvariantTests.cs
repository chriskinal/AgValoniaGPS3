// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;

using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.YouTurn;

namespace AgValoniaGPS.Services.Tests.YouTurn;

/// <summary>
/// Lookahead invariant fence for the YouTurn pure-pursuit goal point.
///
/// User-reported symptom: while a U-turn is being followed, the tractor
/// "drives over the goal point" — meaning the visible distance from the
/// pivot to the rendered goal collapses, the controller has nothing to
/// chase, and steering accuracy drops.
///
/// Pure pursuit's contract is that the goal stays at the configured
/// lookahead distance ahead of the pivot. This fixture asserts that
/// invariant on representative geometries:
///
///   1) Straight path — Distance(goal, pivot) ≈ GoalPointDistance.
///   2) Tight U-turn arc — Distance(goal, pivot) stays at least half the
///      lookahead (allowing for path-curvature shrink — pure pursuit walks
///      along-path, so euclidean distance is bounded below by the chord
///      length, which approaches half the arc length on a half-circle).
///   3) Pivot advances → goal advances forward (positive dot product with
///      the local path forward direction).
///
/// If the underlying math regresses such that the goal closes on the
/// pivot (the user's symptom), one of these assertions fails and points
/// at the offending case.
/// </summary>
[TestFixture]
public class YouTurnPurePursuitLookaheadInvariantTests
{
    private const double Lookahead = 5.0;

    [Test]
    public void StraightPath_GoalIsApproximatelyLookaheadFromPivot()
    {
        // 30 m straight path north along x=0.
        var path = BuildStraightNorthPath(length: 30, step: 1.0);

        // Pivot mid-path, pointing north.
        var pivot = new Vec3(0, 10, 0);
        var output = RunPurePursuit(path, pivot, headingRad: 0);

        double dist = Math.Sqrt(
            (output.GoalPoint.Easting - pivot.Easting) * (output.GoalPoint.Easting - pivot.Easting) +
            (output.GoalPoint.Northing - pivot.Northing) * (output.GoalPoint.Northing - pivot.Northing));

        Assert.That(dist, Is.EqualTo(Lookahead).Within(0.5),
            "On a straight path the goal must sit lookahead-distance from the pivot.");
    }

    [Test]
    public void StraightPath_GoalAdvancesAsPivotAdvances()
    {
        var path = BuildStraightNorthPath(length: 30, step: 1.0);

        // Two pivots 1 m apart along the path.
        var pivotA = new Vec3(0, 5, 0);
        var pivotB = new Vec3(0, 6, 0);

        var outA = RunPurePursuit(path, pivotA, headingRad: 0);
        var outB = RunPurePursuit(path, pivotB, headingRad: 0);

        // Forward direction is +Northing on this path.
        double goalAdvance = outB.GoalPoint.Northing - outA.GoalPoint.Northing;

        Assert.That(goalAdvance, Is.GreaterThan(0.5),
            "When the pivot moves 1 m forward, the goal must advance forward too — " +
            "if it advances less than ~0.5 m, the lookahead is collapsing and the " +
            "tractor will drive over the goal point.");
    }

    [Test]
    public void HalfCircleArc_GoalStaysAtLeastHalfLookaheadFromPivot()
    {
        // Half-circle of radius 4 m starting at (0,0) heading north, sweeping
        // east — 30 sample points so segments are short relative to lookahead.
        var path = BuildHalfCircleArc(radius: 4, points: 30);

        // Walk the pivot along the arc, sampling every quarter, and check the
        // invariant at each. Use the per-point heading the arc was built with.
        for (int i = 5; i < path.Count - 5; i += 5)
        {
            var pivot = path[i];
            var output = RunPurePursuit(path, pivot, headingRad: pivot.Heading);

            double dist = Math.Sqrt(
                (output.GoalPoint.Easting - pivot.Easting) * (output.GoalPoint.Easting - pivot.Easting) +
                (output.GoalPoint.Northing - pivot.Northing) * (output.GoalPoint.Northing - pivot.Northing));

            // On a tight arc the euclidean distance is shorter than the
            // along-path distance, but should still be at least half the
            // lookahead. Anything smaller indicates the goal is collapsing
            // toward the pivot — the user-visible drive-over.
            Assert.That(dist, Is.GreaterThan(Lookahead * 0.5),
                $"Arc sample i={i}: Distance(goal, pivot)={dist:F2} m; " +
                $"with lookahead={Lookahead} m, geometry should keep them " +
                $"at least {Lookahead * 0.5:F2} m apart — goal is collapsing onto pivot.");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static List<Vec3> BuildStraightNorthPath(double length, double step)
    {
        var path = new List<Vec3>();
        for (double y = 0; y <= length; y += step)
        {
            path.Add(new Vec3(0, y, 0)); // heading north
        }
        return path;
    }

    private static List<Vec3> BuildHalfCircleArc(double radius, int points)
    {
        var path = new List<Vec3>();
        for (int i = 0; i <= points; i++)
        {
            double angle = i * Math.PI / points; // 0..π sweep
            double x = radius * (1 - Math.Cos(angle));
            double y = radius * Math.Sin(angle);
            // Heading is tangent to the arc; at angle=0 → +N; at angle=π/2 → +E; at angle=π → -N.
            double heading = angle;
            path.Add(new Vec3(x, y, heading));
        }
        return path;
    }

    private static YouTurnGuidanceOutput RunPurePursuit(List<Vec3> path, Vec3 pivot, double headingRad)
    {
        var input = new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = pivot,
            SteerPosition = pivot,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            UseStanley = false,
            GoalPointDistance = Lookahead,
            UTurnCompensation = 1.0,
            StanleyHeadingErrorGain = 1.0,
            StanleyDistanceErrorGain = 0.8,
            FixHeading = headingRad,
            AvgSpeed = 5,
            IsReverse = false,
            UTurnStyle = 0,
        };

        var service = new YouTurnGuidanceService();
        return service.CalculateGuidance(input);
    }
}
