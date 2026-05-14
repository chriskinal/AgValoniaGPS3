// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgValoniaGPS.Services.Tests.YouTurn;

/// <summary>
/// End-to-end simulation driving a synthetic tractor through a full U-turn,
/// capturing per-cycle state and asserting invariants that would catch the
/// U-turn bug class — drive-over, exit-seam jerk, freeze, off-by-one pass
/// number transitions — that pass when probed cycle-locally with
/// "math correct in normal cases" but fail when observed across the full
/// approach→arc→exit trajectory.
///
/// The fixture invokes the YouTurnStateMachine + YouTurnGuidanceService on
/// a hand-stepped trajectory rather than booting GpsPipelineService — the
/// state-machine + guidance contract is the one that produces the observable
/// drive-over / jerk / freeze symptoms, and isolating it keeps each failure
/// pointing to a single algorithm rather than a pipeline assembly problem.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class YouTurnEndToEndSimulationTests
{
    private const double PivotStepMeters = 0.28;     // ~10 km/h at 10 Hz
    private const double GoalPointDistance = 3.0;
    private const double Wheelbase = 2.5;
    private const int MaxCycles = 200;

    private YouTurnStateMachine _stateMachine = null!;
    private YouTurnGuidanceService _guidance = null!;
    private Boundary _boundary = null!;
    private List<Vec3> _headlandLine = null!;
    private Models.Track.Track _track = null!;

    private List<CycleRecord> _records = null!;
    private GuidanceWorkingState _guidanceState = null!;
    private YouTurnWorkingState _turnState = null!;

    private sealed record CycleRecord(
        int Cycle,
        Vec2 Pivot,
        double HeadingRad,
        bool IsTriggered,
        bool IsExecuting,
        int TurnPathCount,
        int PathsAway,
        Vec2 GoalPoint,
        bool GoalComputed);

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());
        var config = ConfigurationStore.Instance;
        config.Vehicle.AntennaPivot = 0;
        config.Vehicle.AntennaOffset = 0;
        config.Vehicle.AntennaHeight = 0;
        config.Vehicle.Wheelbase = Wheelbase;
        config.Tool.Width = 6;
        config.Tool.Overlap = 0;
        config.NumSections = 1;
        config.Tool.SetSectionWidth(0, 600);

        var polygonOffset = new PolygonOffsetService();
        var creation = new YouTurnCreationService(
            NullLogger<YouTurnCreationService>.Instance, polygonOffset);
        var pathing = new YouTurnPathingService(
            NullLogger<YouTurnPathingService>.Instance);
        _stateMachine = new YouTurnStateMachine(
            creation, pathing, NullLogger<YouTurnStateMachine>.Instance);
        _guidance = new YouTurnGuidanceService();

        // 100×100 m boundary centered on origin; headland line inset 10 m.
        // Cultivated area is (-40, -40) .. (40, 40). AB line at x = 0,
        // north-south. Tractor seeds in the cultivated area heading north.
        var outerPoints = new List<Vec2>
        {
            new(-50, -50), new(50, -50), new(50, 50), new(-50, 50),
        };
        _boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = outerPoints
                    .Select(p => new BoundaryPoint(p.Easting, p.Northing, 0))
                    .ToList(),
            },
        };
        _headlandLine = new List<Vec3>
        {
            new(-40, -40, 0), new(40, -40, 0), new(40, 40, 0), new(-40, 40, 0),
        };
        _track = Models.Track.Track.FromABLine(
            "AB-sim", new Vec3(0, -100, 0), new Vec3(0, 100, 0));

        _guidanceState = new GuidanceWorkingState();
        _turnState = new YouTurnWorkingState { IsEnabled = true };

        _records = new List<CycleRecord>();
        RunSimulation();
    }

    private void RunSimulation()
    {
        // Start ~30 m south of the north headland edge, heading north.
        Vec2 pivot = new(0, 10);
        double heading = 0; // north, radians

        var phase = Phase.Approach;
        double cumulativePathDist = 0;
        bool executingObservedThisRun = false;

        for (int i = 0; i < MaxCycles; i++)
        {
            // Tick the state machine.
            var pos = new Position
            {
                Easting = pivot.Easting,
                Northing = pivot.Northing,
                Heading = heading * 180.0 / Math.PI,
            };
            var ctx = new YouTurnStateMachine.TickContext(
                pos, _track, _boundary, _headlandLine,
                UTurnSkipRows: 0,
                IsSkipWorkedMode: false,
                HeadlandCalculatedWidth: 10.0,
                HeadlandDistance: 5.0);

            _turnState.YouTurnCounter++; // pipeline parity
            _stateMachine.Tick(in ctx, _guidanceState, _turnState);

            // If executing, also compute Pure Pursuit goal point (mirroring
            // what GpsPipelineService does for a U-turn-following cycle).
            var goalPt = new Vec2();
            bool goalComputed = false;
            if (_turnState.IsExecuting
                && _turnState.TurnPath != null
                && _turnState.TurnPath.Count > 2)
            {
                var input = new YouTurnGuidanceInput
                {
                    TurnPath = _turnState.TurnPath,
                    PivotPosition = new Vec3(pivot.Easting, pivot.Northing, heading),
                    SteerPosition = new Vec3(
                        pivot.Easting + Math.Sin(heading) * Wheelbase,
                        pivot.Northing + Math.Cos(heading) * Wheelbase,
                        heading),
                    Wheelbase = Wheelbase,
                    MaxSteerAngle = 35,
                    UseStanley = false,
                    GoalPointDistance = GoalPointDistance,
                    UTurnCompensation = 1.0,
                    FixHeading = heading,
                    AvgSpeed = 10,
                    IsReverse = false,
                    UTurnStyle = 0,
                };
                var output = _guidance.CalculateGuidance(input);
                goalPt = output.GoalPoint;
                goalComputed = !output.IsTurnComplete
                    && (Math.Abs(goalPt.Easting) > 0 || Math.Abs(goalPt.Northing) > 0);
            }

            _records.Add(new CycleRecord(
                Cycle: i,
                Pivot: pivot,
                HeadingRad: heading,
                IsTriggered: _turnState.IsTriggered,
                IsExecuting: _turnState.IsExecuting,
                TurnPathCount: _turnState.TurnPath?.Count ?? 0,
                PathsAway: _guidanceState.HowManyPathsAway,
                GoalPoint: goalPt,
                GoalComputed: goalComputed));

            // Advance the synthetic tractor.
            if (_turnState.IsExecuting
                && _turnState.TurnPath != null
                && _turnState.TurnPath.Count > 2)
            {
                phase = Phase.Executing;
                executingObservedThisRun = true;
                cumulativePathDist += PivotStepMeters;
                (pivot, heading) = WalkAlongTurnPath(
                    _turnState.TurnPath, cumulativePathDist);
            }
            else if (phase == Phase.Executing) // just exited
            {
                phase = Phase.Exit;
                // After 180° U-turn the path heading at the end is ~south.
                // Step in current heading direction.
                pivot = new Vec2(
                    pivot.Easting + Math.Sin(heading) * PivotStepMeters,
                    pivot.Northing + Math.Cos(heading) * PivotStepMeters);
            }
            else if (phase == Phase.Approach)
            {
                pivot = new Vec2(
                    pivot.Easting + Math.Sin(heading) * PivotStepMeters,
                    pivot.Northing + Math.Cos(heading) * PivotStepMeters);
            }
            else // Phase.Exit
            {
                pivot = new Vec2(
                    pivot.Easting + Math.Sin(heading) * PivotStepMeters,
                    pivot.Northing + Math.Cos(heading) * PivotStepMeters);
            }

            // End the simulation a few cycles after exit so we capture the
            // post-turn paths-away change.
            if (executingObservedThisRun && phase == Phase.Exit
                && _records.Count > 0
                && _records[^1].Cycle - LastExecutingCycle() > 10)
            {
                break;
            }
        }
    }

    private int LastExecutingCycle() => _records
        .Where(r => r.IsExecuting)
        .Select(r => r.Cycle)
        .DefaultIfEmpty(-1)
        .Max();

    private enum Phase { Approach, Executing, Exit }

    /// <summary>
    /// Walk along the turn path by <paramref name="cumulativeDist"/> meters
    /// from path[0]. If <paramref name="cumulativeDist"/> exceeds the total
    /// path length, project past the endpoint along the endpoint heading so
    /// the synthetic tractor overshoots the turn — this is what triggers
    /// the state machine's closest-approach completion check.
    /// </summary>
    private static (Vec2 Pos, double Heading) WalkAlongTurnPath(
        List<Vec3> path, double cumulativeDist)
    {
        if (path.Count == 0) return (new Vec2(), 0);

        double traveled = 0;
        for (int i = 1; i < path.Count; i++)
        {
            double dx = path[i].Easting - path[i - 1].Easting;
            double dy = path[i].Northing - path[i - 1].Northing;
            double segLen = Math.Sqrt(dx * dx + dy * dy);
            if (segLen < 1e-9) continue;

            if (traveled + segLen >= cumulativeDist)
            {
                double j = (cumulativeDist - traveled) / segLen;
                double e = path[i - 1].Easting + j * dx;
                double n = path[i - 1].Northing + j * dy;
                double h = path[i].Heading;
                return (new Vec2(e, n), h);
            }
            traveled += segLen;
        }

        // Past the last point — overshoot in endpoint heading direction.
        var end = path[^1];
        double remainder = cumulativeDist - traveled;
        double oe = end.Easting + Math.Sin(end.Heading) * remainder;
        double on = end.Northing + Math.Cos(end.Heading) * remainder;
        return (new Vec2(oe, on), end.Heading);
    }

    // ── Assertions ──────────────────────────────────────────────────────

    [Test]
    public void EndToEnd_TurnExecutes_AtLeastOnce()
    {
        // Precondition: this fixture is meaningful only when the simulation
        // actually drives the state machine into the executing branch. If
        // this fails, the rest of the assertions in this class are stuck
        // testing only the approach phase and need their seed adjusted.
        Assert.That(_records.Any(r => r.IsExecuting), Is.True,
            "Simulation never entered IsExecuting — boundary/headland/seed "
            + "is wrong for this fixture to reproduce U-turn bugs.");
    }

    [Test]
    public void EndToEnd_NoIllegalStateTransitions()
    {
        // Flags must progress monotonically forward through the lifecycle.
        // Specifically: once IsExecuting flips false after a turn, it must
        // not flip true again in the same simulation run; once IsTriggered
        // is cleared post-execution, it stays cleared.
        bool sawExecuting = false;
        bool executionEnded = false;
        bool triggerCleared = false;

        for (int i = 0; i < _records.Count; i++)
        {
            var r = _records[i];
            if (r.IsExecuting)
            {
                sawExecuting = true;
                if (executionEnded)
                    Assert.Fail($"Cycle {r.Cycle}: IsExecuting flipped back to true "
                        + "after a prior completion — illegal lifecycle re-entry.");
            }
            else if (sawExecuting)
            {
                executionEnded = true;
            }

            if (!r.IsTriggered && executionEnded) triggerCleared = true;
            if (triggerCleared && r.IsTriggered)
                Assert.Fail($"Cycle {r.Cycle}: IsTriggered re-armed after the "
                    + "execution completed — pass-counter would double-fire.");
        }
    }

    [Test]
    [Ignore("Deferred: synthetic Fixture-1 path doesn't reproduce the production omega-fold geometry. Real-path drive-over is covered by YouTurnRealPathDriveOverTests / YouTurnEarlyCompletionTests.")]
    public void EndToEnd_GoalNeverJumpsMoreThan2m()
    {
        // The goal point is the visible "carrot" the steering controller
        // follows; an inter-cycle jump greater than 2 m at 10 Hz is a visible
        // steering jerk. Catches the exit-seam jerk and the end-of-turn
        // freeze-then-snap pattern. Only compare cycles where both sides
        // produced a real goal point (zero/zero means "not computed").
        const double MaxJumpMeters = 2.0;
        for (int i = 1; i < _records.Count; i++)
        {
            var prev = _records[i - 1];
            var cur = _records[i];
            if (!prev.GoalComputed || !cur.GoalComputed) continue;

            double dx = cur.GoalPoint.Easting - prev.GoalPoint.Easting;
            double dy = cur.GoalPoint.Northing - prev.GoalPoint.Northing;
            double d = Math.Sqrt(dx * dx + dy * dy);
            Assert.That(d, Is.LessThan(MaxJumpMeters),
                $"Cycle {cur.Cycle}: goal point jumped {d:F2} m from "
                + $"({prev.GoalPoint.Easting:F2},{prev.GoalPoint.Northing:F2}) "
                + $"to ({cur.GoalPoint.Easting:F2},{cur.GoalPoint.Northing:F2}). "
                + "This is the steering-jerk / freeze-and-snap symptom class.");
        }
    }

    [Test]
    public void EndToEnd_LookaheadInvariantHolds()
    {
        // Pure Pursuit lookahead invariant: when actively chasing a goal
        // point, the goal must stay at least a meaningful fraction of the
        // configured lookahead in front of the pivot. The 0.4× floor is
        // the threshold the user's "drive-over" symptom corresponds to —
        // if the goal collapses onto the pivot, the steering controller's
        // angle calc divides by ~zero and the tractor either freezes or
        // chases a stale carrot.
        const double Floor = GoalPointDistance * 0.4;
        for (int i = 0; i < _records.Count; i++)
        {
            var r = _records[i];
            if (!r.IsExecuting || !r.GoalComputed) continue;

            double dx = r.GoalPoint.Easting - r.Pivot.Easting;
            double dy = r.GoalPoint.Northing - r.Pivot.Northing;
            double d = Math.Sqrt(dx * dx + dy * dy);
            Assert.That(d, Is.GreaterThanOrEqualTo(Floor),
                $"Cycle {r.Cycle}: |goal − pivot| = {d:F3} m < lookahead floor "
                + $"{Floor:F3} m. This is the drive-over symptom — the "
                + "controller has no carrot in front to steer toward.");
        }
    }

    [Test]
    public void EndToEnd_GoalAlwaysForwardOfPivot()
    {
        // The lookahead point must lie in front of the tractor (positive
        // dot product with heading vector). A goal behind the pivot means
        // the controller is steering AWAY from the path, which manifests
        // as the tractor doubling back at the exit seam.
        for (int i = 0; i < _records.Count; i++)
        {
            var r = _records[i];
            if (!r.IsExecuting || !r.GoalComputed) continue;

            double hx = Math.Sin(r.HeadingRad);
            double hy = Math.Cos(r.HeadingRad);
            double dx = r.GoalPoint.Easting - r.Pivot.Easting;
            double dy = r.GoalPoint.Northing - r.Pivot.Northing;
            double dot = hx * dx + hy * dy;

            // Allow a small negative slop on the very last executing cycle:
            // the controller permits one cycle of "essentially-zero" at the
            // exact handoff point.
            bool isLastExecuting = i + 1 < _records.Count
                && !_records[i + 1].IsExecuting;
            double tolerance = isLastExecuting ? -0.1 : 0.0;

            Assert.That(dot, Is.GreaterThan(tolerance),
                $"Cycle {r.Cycle}: goal is behind the pivot (forward dot = "
                + $"{dot:F3}). Forward-of-pivot is the basic correctness "
                + "invariant for any pursuit-style carrot controller.");
        }
    }

    [Test]
    [Ignore("Deferred: completion detector doesn't fire in the synthetic Fixture-1 walker; real-path completion is covered by YouTurnEarlyCompletionTests.")]
    public void EndToEnd_PathsAwayChangesAtCompletion()
    {
        // HowManyPathsAway must change exactly once over the simulation,
        // and that change must coincide with the cycle where IsExecuting
        // flips false (turn completion writes the new pass number on the
        // same tick the lifecycle ends — see YouTurnStateMachine.CompleteTurn).
        // An extra/missing transition is the off-by-one pass-counter bug
        // that landed as the v5 exit-seam (commit 4563fcff).
        var transitions = new List<int>();
        for (int i = 1; i < _records.Count; i++)
        {
            if (_records[i].PathsAway != _records[i - 1].PathsAway)
                transitions.Add(_records[i].Cycle);
        }

        Assert.That(transitions.Count, Is.EqualTo(1),
            $"HowManyPathsAway should change exactly once at turn completion. "
            + $"Saw {transitions.Count} transitions at cycles "
            + $"[{string.Join(",", transitions)}]. "
            + "Multiple/zero transitions are the off-by-one pass-counter bug class.");

        // The single transition must coincide with the cycle IsExecuting
        // flips false.
        int transitionCycle = transitions[0];
        var prior = _records.First(r => r.Cycle == transitionCycle - 1);
        var atTrans = _records.First(r => r.Cycle == transitionCycle);
        Assert.That(prior.IsExecuting && !atTrans.IsExecuting, Is.True,
            $"PathsAway changed at cycle {transitionCycle} but IsExecuting did "
            + $"not flip false on the same cycle (prior.IsExecuting="
            + $"{prior.IsExecuting}, current.IsExecuting={atTrans.IsExecuting}). "
            + "This is the off-by-one cycle bug: pass number lags the lifecycle.");
    }
}
