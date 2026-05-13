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
/// Fence for the closest-segment selection at omega-shaped U-turn paths.
///
/// Production U-turns are built as omega turns (cumulative rotation
/// ≥ 270° in the logs of the v8 drive-over dump): tractor enters one
/// way, sweeps around a near-full loop, exits the opposite way along
/// a leg only a tool-width away from the entry leg. The path folds
/// back on itself.
///
/// In the fold region the pure-pursuit closest-point search returns
/// two non-adjacent indices — one on the entry leg, one on the exit
/// leg — because both legs run physically close to the pivot. The
/// legacy disambiguation (force B = A + 1, lower-index wins) picks
/// the entry leg whenever the pivot is on the exit leg. The
/// controller then steers anti-tangent to the pivot's actual heading
/// and slams the wheels into the opposite lock: the user-reported
/// "drive over the path" symptom at end of U-turn.
///
/// These tests construct an omega path with a tight fold and pivot
/// the tractor on each leg in turn, asserting the goal point is
/// always forward of the pivot's actual heading.
/// </summary>
[TestFixture]
public class YouTurnOmegaPathFoldingTests
{
    private const double Lookahead = 3.0;
    private const double Wheelbase = 2.5;

    /// <summary>
    /// Build a tight-fold omega path: entry leg north, half-circle arc
    /// of radius <paramref name="foldGap"/>/2, exit leg south. The exit
    /// leg is <paramref name="foldGap"/> metres east of the entry leg.
    /// 1 m point spacing.
    /// </summary>
    private static List<Vec3> BuildTightFoldPath(double foldGap, double legLength)
    {
        var path = new List<Vec3>();

        // Entry leg: from (0, -legLength) heading north (0 rad) to (0, 0).
        for (double y = -legLength; y < 0; y += 1.0)
            path.Add(new Vec3(0, y, 0));
        path.Add(new Vec3(0, 0, 0));

        // Half-circle arc, radius = foldGap / 2, centred at (foldGap/2, 0),
        // sweeping from (0, 0) heading north to (foldGap, 0) heading south.
        double r = foldGap / 2.0;
        int arcSteps = 16;
        for (int i = 1; i <= arcSteps; i++)
        {
            double angle = i * Math.PI / arcSteps;
            double e = r - r * Math.Cos(angle);
            double n = r * Math.Sin(angle);
            path.Add(new Vec3(e, n, angle));
        }

        // Exit leg: from (foldGap, 0) heading south (π rad) to (foldGap, -legLength).
        for (double y = -1.0; y >= -legLength; y -= 1.0)
            path.Add(new Vec3(foldGap, y, Math.PI));

        return path;
    }

    private static YouTurnGuidanceInput MakeInput(List<Vec3> path, Vec3 pivotPos, double headingRad)
    {
        return new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = pivotPos,
            SteerPosition = new Vec3(
                pivotPos.Easting + Math.Sin(headingRad) * Wheelbase,
                pivotPos.Northing + Math.Cos(headingRad) * Wheelbase,
                headingRad),
            Wheelbase = Wheelbase,
            MaxSteerAngle = 35,
            UseStanley = false,
            GoalPointDistance = Lookahead,
            UTurnCompensation = 1.0,
            FixHeading = headingRad,
            AvgSpeed = 5,
            IsReverse = false,
            UTurnStyle = 0,
        };
    }

    private static double ForwardDot(Vec2 goal, Vec3 pivot, double headingRad)
    {
        double dx = goal.Easting - pivot.Easting;
        double dy = goal.Northing - pivot.Northing;
        return dx * Math.Sin(headingRad) + dy * Math.Cos(headingRad);
    }

    [Test]
    public void Pivot_on_ExitLeg_NearFold_GoalIsForward()
    {
        // Tight 1 m gap between entry and exit legs. Pivot on the EXIT leg
        // mid-leg, heading south (the actual travel direction on the exit
        // leg). The entry leg (heading north) is 1 m due west.
        var path = BuildTightFoldPath(foldGap: 1.0, legLength: 10);
        var pivot = new Vec3(1.0, -5.0, Math.PI);

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(MakeInput(path, pivot, Math.PI));

        Assert.That(output.IsTurnComplete, Is.False,
            "Pivot is ON the exit leg — service should be following, not completing.");
        Assert.That(ForwardDot(output.GoalPoint, pivot, Math.PI), Is.GreaterThan(0),
            "Pivot heading south on exit leg, exit leg runs south — goal must be "
            + "forward of pivot. Anti-tangent here is the drive-over symptom.");
    }

    [Test]
    public void Pivot_on_EntryLeg_NearFold_GoalIsForward()
    {
        // Mirror case: pivot on the ENTRY leg, heading north — the entry
        // leg's natural travel direction. The exit leg (heading south) is
        // 1 m due east.
        var path = BuildTightFoldPath(foldGap: 1.0, legLength: 10);
        var pivot = new Vec3(0.0, -5.0, 0.0);

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(MakeInput(path, pivot, 0.0));

        Assert.That(output.IsTurnComplete, Is.False);
        Assert.That(ForwardDot(output.GoalPoint, pivot, 0.0), Is.GreaterThan(0),
            "Pivot heading north on entry leg — goal must lie forward. "
            + "Picking the exit leg here would be anti-tangent.");
    }

    [Test]
    public void Pivot_AtFoldMidpoint_HeadingPicksSegmentByTangent()
    {
        // Pivot exactly equidistant from both legs (E = foldGap/2). The
        // closest-point search will return one point from each leg. The
        // disambiguation should prefer the leg whose tangent best matches
        // the pivot's heading.
        var path = BuildTightFoldPath(foldGap: 1.0, legLength: 10);
        var pivot = new Vec3(0.5, -5.0, Math.PI); // heading south

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(MakeInput(path, pivot, Math.PI));

        Assert.That(output.IsTurnComplete, Is.False);
        Assert.That(ForwardDot(output.GoalPoint, pivot, Math.PI), Is.GreaterThan(0),
            "At the fold midpoint the heading-aware tiebreak should pick the "
            + "exit leg (heading south, matches pivot heading), placing goal forward.");
    }

    /// <summary>
    /// Sweep the pivot along the LEG INTERIORS (the parts of each leg
    /// far enough from the arc that the lookahead-walk doesn't wrap
    /// around the U-turn naturally — that's a separate pure-pursuit
    /// degeneracy independent of the omega-fold segment-selection bug
    /// this fixture targets). The asserted invariant: in those regions,
    /// where closest-segment ambiguity is the dominant failure mode,
    /// no cycle places the goal behind pivot heading.
    ///
    /// Parameterised over fold-gap so increasingly tight folds are
    /// stressed.
    /// </summary>
    [TestCase(0.5)]
    [TestCase(1.0)]
    [TestCase(2.0)]
    [TestCase(4.0)]
    public void PivotSweep_OnLegInteriors_GoalAlwaysForward(double foldGap)
    {
        const double legLength = 20.0;
        var path = BuildTightFoldPath(foldGap, legLength);
        var svc = new YouTurnGuidanceService();

        // Leg interior = indices at least Lookahead + arc-length away from
        // the arc on each side. With 1 m point spacing and arc length
        // ≈ π × foldGap/2, the safety margin is ~Lookahead + 1.6 m.
        const int margin = 5;
        int entryStart = 0;
        int entryEnd = (int)legLength - margin; // last entry-leg index to use
        int exitStart = (int)legLength + 1 + 16 + margin; // first exit-leg index
        int exitEnd = path.Count - 1;

        void SweepRange(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                var p = path[i];
                double heading = p.Heading;
                var pivot = new Vec3(
                    p.Easting - Math.Sin(heading) * 0.05,
                    p.Northing - Math.Cos(heading) * 0.05,
                    heading);

                var output = svc.CalculateGuidance(MakeInput(path, pivot, heading));
                if (output.IsTurnComplete) continue;

                double dot = ForwardDot(output.GoalPoint, pivot, heading);
                Assert.That(dot, Is.GreaterThan(0),
                    $"foldGap={foldGap} m, path index {i}: forward_dot={dot:F3}. "
                    + "Goal landed behind pivot heading inside leg interior — "
                    + "closest-segment search picked the opposite leg.");
            }
        }

        SweepRange(entryStart, entryEnd);
        SweepRange(exitStart, exitEnd);
    }
}
