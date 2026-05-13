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
/// Regression fence for the v10 omega-path drive-over (dump
/// 20260513_225413, recorder commit 07d70927). The closest-segment
/// search returns adjacent indices throughout — so the v8 omega-fold
/// segment-selection guard does not trigger. The bug is instead in
/// the lookahead walk itself: walking <c>GoalPointDistance</c> along
/// a tight-loop omega path wraps past ~90° of cumulative rotation,
/// and the resulting goal point lands on a fold of the path that is
/// physically forward in Euclidean terms but anti-tangent to the
/// pivot's heading. The pure-pursuit controller then steers the
/// tractor full-lock toward the wrong side — the visible drive-over.
///
/// The fix is a post-walk forward-dot guard: if the goal sits behind
/// the pivot's heading vector, replace it with a pivot-heading
/// projection at the configured lookahead distance.
/// </summary>
[TestFixture]
public class YouTurnOmegaLookaheadWrapTests
{
    private const double Lookahead = 4.0;
    private const double Wheelbase = 2.5;
    private const double OmegaRadius = 4.0;

    /// <summary>
    /// Construct an omega-shaped path mimicking the v10 production
    /// turn (logs report "Path created with 56 points,
    /// net=180° cumulative=356°"): 4 m entry leg → near-full-circle
    /// loop of radius <see cref="OmegaRadius"/> → 4 m exit leg, with
    /// 1 m point spacing on the legs and ~3° spacing on the arc.
    /// </summary>
    private static List<Vec3> BuildOmegaPath()
    {
        var path = new List<Vec3>();

        // Entry leg: from (-4, 0) to (0, 0) heading east (π/2 rad in
        // (sin, cos) frame: east = (sin=1, cos=0) so heading = π/2).
        double entryHeading = Math.PI / 2;
        for (double x = -4.0; x < 0; x += 1.0)
            path.Add(new Vec3(x, 0, entryHeading));
        path.Add(new Vec3(0, 0, entryHeading));

        // Omega loop: 356° of rotation around centre at (0, OmegaRadius).
        // Start tangent = east (heading π/2). The loop curves left (CCW),
        // so after 356° the tangent is rotated 356° CCW from east.
        // 3° per step → 119 arc points.
        int arcSteps = 119;
        double totalSweep = 356.0 * Math.PI / 180.0;
        double cx = 0;
        double cy = OmegaRadius;
        for (int i = 1; i <= arcSteps; i++)
        {
            double theta = i * totalSweep / arcSteps; // 0 .. 6.213 rad
            // Position: start at (0, 0) below centre, rotate CCW.
            //   (cx + OmegaRadius * sin(-π/2 + theta), cy + OmegaRadius * cos(-π/2 + theta))
            double pe = cx + OmegaRadius * Math.Sin(-Math.PI / 2 + theta);
            double pn = cy + OmegaRadius * Math.Cos(-Math.PI / 2 + theta);
            // Tangent at this point: rotated theta CCW from the initial
            // tangent (east, heading π/2 in (sin, cos) frame).
            double heading = entryHeading + theta;
            // Wrap to (-π, π] for clarity (not strictly required).
            while (heading > Math.PI) heading -= 2 * Math.PI;
            path.Add(new Vec3(pe, pn, heading));
        }

        // Exit leg: 4 m straight along the final tangent direction.
        var last = path[^1];
        double exitDirE = Math.Sin(last.Heading);
        double exitDirN = Math.Cos(last.Heading);
        for (int i = 1; i <= 4; i++)
            path.Add(new Vec3(
                last.Easting + exitDirE * i,
                last.Northing + exitDirN * i,
                last.Heading));

        return path;
    }

    private static YouTurnGuidanceInput MakeInput(
        List<Vec3> path, Vec3 pivotPos, double pivotHeadingRad)
    {
        return new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = pivotPos,
            SteerPosition = new Vec3(
                pivotPos.Easting + Math.Sin(pivotHeadingRad) * Wheelbase,
                pivotPos.Northing + Math.Cos(pivotHeadingRad) * Wheelbase,
                pivotHeadingRad),
            Wheelbase = Wheelbase,
            MaxSteerAngle = 35,
            UseStanley = false,
            GoalPointDistance = Lookahead,
            UTurnCompensation = 1.0,
            FixHeading = pivotHeadingRad,
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

    /// <summary>
    /// Pivot at the very start of the omega path (origin, heading east).
    /// On a 4 m-radius arc with 4 m lookahead, walking 4 m of arc-length
    /// rotates the path tangent ~57° from the start. The goal must
    /// still be forward of the pivot's heading — anything else is the
    /// drive-over signature.
    /// </summary>
    [Test]
    public void OmegaLoop_PivotAtStart_GoalIsForward()
    {
        var path = BuildOmegaPath();
        var pivot = new Vec3(0, 0, Math.PI / 2);

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(MakeInput(path, pivot, Math.PI / 2));

        Assert.That(output.IsTurnComplete, Is.False);
        Assert.That(ForwardDot(output.GoalPoint, pivot, Math.PI / 2),
            Is.GreaterThan(0),
            "Pivot at start of omega path, heading east. Goal must be "
            + "forward of pivot heading — anti-tangent here is the v10 "
            + "drive-over symptom.");
    }

    /// <summary>
    /// Replays the dump's failure window (rows 333-348 of v10 dump):
    /// pivot on the arc with heading rotating ~26° during the run-in.
    /// Five sample positions; at every one the goal must be forward.
    /// Without the post-walk guard, every sample fails with forward_dot
    /// growing more negative as the pivot heading rotates further into
    /// the loop.
    /// </summary>
    [TestCase(0, Math.PI / 2)]                  // entry-end, heading east
    [TestCase(15, Math.PI / 2 + 0.4)]           // ~23° into arc
    [TestCase(30, Math.PI / 2 + 0.8)]           // ~46° into arc
    [TestCase(45, Math.PI / 2 + 1.2)]           // ~69° into arc
    [TestCase(60, Math.PI / 2 + 1.6)]           // ~92° into arc
    public void OmegaLoop_PivotMidArc_GoalIsForward(int arcOffsetIdx, double pivotHeadingRad)
    {
        var path = BuildOmegaPath();
        // Pivot sits on the path at an arc-index offset from the start
        // of the loop (entry leg is 5 points; arc starts at index 5).
        int pivotIdx = Math.Min(5 + arcOffsetIdx, path.Count - 2);
        var p = path[pivotIdx];
        var pivot = new Vec3(p.Easting, p.Northing, pivotHeadingRad);

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(MakeInput(path, pivot, pivotHeadingRad));

        Assert.That(output.IsTurnComplete, Is.False,
            $"arc offset {arcOffsetIdx}: service flagged turn complete mid-arc.");
        double fwd = ForwardDot(output.GoalPoint, pivot, pivotHeadingRad);
        Assert.That(fwd, Is.GreaterThan(0),
            $"arc offset {arcOffsetIdx}, pivot heading {pivotHeadingRad:F3} rad: "
            + $"forward_dot={fwd:F3}. Drive-over reproduces.");
    }

    /// <summary>
    /// Closest-segment search returns adjacent indices on a smooth
    /// omega path — verifying the bug isn't an artefact of the
    /// segment selector. If this fails, my fix is necessary but not
    /// sufficient and the original v8 closest-segment guard should
    /// also have caught the case.
    /// </summary>
    [Test]
    public void OmegaLoop_MidArc_ClosestSegmentSearchReturnsAdjacent()
    {
        var path = BuildOmegaPath();
        // Pivot inside the arc.
        int pivotIdx = 35;
        var p = path[pivotIdx];
        var pivot = new Vec3(p.Easting, p.Northing, p.Heading);

        // Replicate the closest-point search done at the top of
        // CalculatePurePursuitGuidance.
        double minDistA = double.MaxValue;
        double minDistB = double.MaxValue;
        int A = 0, B = 0;
        for (int t = 0; t < path.Count; t++)
        {
            double d = (pivot.Easting - path[t].Easting) * (pivot.Easting - path[t].Easting)
                + (pivot.Northing - path[t].Northing) * (pivot.Northing - path[t].Northing);
            if (d < minDistA)
            {
                minDistB = minDistA; B = A;
                minDistA = d; A = t;
            }
            else if (d < minDistB)
            {
                minDistB = d; B = t;
            }
        }
        if (A > B) (A, B) = (B, A);

        Assert.That(B - A, Is.EqualTo(1),
            $"closest-segment search returned non-adjacent indices "
            + $"(A={A}, B={B}). The bug is then in selection, not the "
            + "walk — different failure mode than the v10 dump reported.");
    }
}
