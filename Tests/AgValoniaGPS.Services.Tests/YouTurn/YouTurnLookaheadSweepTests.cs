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
/// Parameterized sweep of the Pure Pursuit lookahead invariant across a
/// 5×5 matrix of (goalPointDistance, arcRadius). Tight-arc + small-lookahead
/// combinations (radius 2-3 m, lookahead 1-2 m) are where the user's
/// drive-over symptom most plausibly lives — if the goal point collapses
/// onto the pivot anywhere in this matrix, that is the bug reproduced as
/// a unit-level failure rather than an end-to-end symptom.
///
/// Sampling: half-circle arc generated at fixed angular resolution; pivot
/// sampled at 5 evenly-spaced positions along the arc. At each sample the
/// invariant is:
///   • Distance(goal, pivot) ≥ goalPointDistance × 0.4   (lookahead floor)
///   • dot(goal − pivot, heading) > 0                    (goal forward of pivot)
/// </summary>
[TestFixture]
public class YouTurnLookaheadSweepTests
{
    private static readonly double[] GoalPointDistances = { 1.0, 2.0, 5.0, 10.0, 15.0 };
    private static readonly double[] ArcRadii = { 2.0, 3.0, 4.0, 6.0, 10.0 };
    private const int ArcPoints = 60;          // ~3° resolution
    private const int PivotSamples = 5;

    public static IEnumerable<object[]> SweepCases()
    {
        foreach (var gpd in GoalPointDistances)
            foreach (var r in ArcRadii)
                for (int s = 0; s < PivotSamples; s++)
                    yield return new object[] { gpd, r, s };
    }

    /// <summary>
    /// Build a half-circle arc of radius <paramref name="radius"/> centered
    /// at (radius, 0), starting at the origin heading north (0 rad) and
    /// ending at (2·radius, 0) heading south (π rad). Heading at each point
    /// is the tangent direction along the arc.
    /// </summary>
    private static List<Vec3> BuildHalfCircleArc(double radius, int points)
    {
        var path = new List<Vec3>(points + 1);
        for (int i = 0; i <= points; i++)
        {
            double angle = i * Math.PI / points;          // 0..π
            double e = radius - radius * Math.Cos(angle); // 0..2r
            double n = radius * Math.Sin(angle);          // 0..r..0
            double heading = angle;                       // 0=N, π=S in (sin,cos) convention
            path.Add(new Vec3(e, n, heading));
        }
        return path;
    }

    [TestCaseSource(nameof(SweepCases))]
    public void Lookahead_GoalStaysAheadOfPivot(double goalPointDistance, double arcRadius, int pivotSampleIdx)
    {
        var path = BuildHalfCircleArc(arcRadius, ArcPoints);

        // Sample pivot at five evenly-spaced angles in (0, π). Place the
        // pivot ON the arc and oriented along the local tangent — this is
        // the "perfectly tracking" case, the most favorable for the
        // controller. If the invariant fails here, it fails for any tractor
        // motion close to the path.
        double angle = (pivotSampleIdx + 1) * Math.PI / (PivotSamples + 1);
        double pivotE = arcRadius - arcRadius * Math.Cos(angle);
        double pivotN = arcRadius * Math.Sin(angle);
        double heading = angle;

        var input = new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = new Vec3(pivotE, pivotN, heading),
            SteerPosition = new Vec3(
                pivotE + Math.Sin(heading) * 2.5,
                pivotN + Math.Cos(heading) * 2.5,
                heading),
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            UseStanley = false,
            GoalPointDistance = goalPointDistance,
            UTurnCompensation = 1.0,
            FixHeading = heading,
            AvgSpeed = 10,
            IsReverse = false,
            UTurnStyle = 0,
        };

        var service = new YouTurnGuidanceService();
        var output = service.CalculateGuidance(input);

        // Treat IsTurnComplete as a sweep failure here too — the test sweeps
        // the *middle* of the arc, so completion is wrong by construction.
        Assert.That(output.IsTurnComplete, Is.False,
            $"r={arcRadius} lookahead={goalPointDistance} sample={pivotSampleIdx}: "
            + "service returned IsTurnComplete mid-arc — should be following.");

        var goal = output.GoalPoint;
        double dx = goal.Easting - pivotE;
        double dy = goal.Northing - pivotN;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // Lookahead floor.
        double floor = goalPointDistance * 0.4;
        Assert.That(dist, Is.GreaterThanOrEqualTo(floor),
            $"r={arcRadius} lookahead={goalPointDistance} sample={pivotSampleIdx}: "
            + $"|goal − pivot| = {dist:F3} m < {floor:F3} m (drive-over symptom).");

        // Forward of pivot.
        double dot = Math.Sin(heading) * dx + Math.Cos(heading) * dy;
        Assert.That(dot, Is.GreaterThan(0),
            $"r={arcRadius} lookahead={goalPointDistance} sample={pivotSampleIdx}: "
            + $"goal behind pivot (forward dot = {dot:F3}).");
    }
}
