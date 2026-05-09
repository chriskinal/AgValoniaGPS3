// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using System.Linq;

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Services.YouTurn;

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests.YouTurn;

/// <summary>
/// Regression tests for YouTurnStateMachine.Tick populating
/// <see cref="YouTurnWorkingState.DistanceToTrigger"/>. Prior to this fix,
/// the field was only initialised to 0 in <c>Reset()</c> and never updated,
/// so any UI widget reading <c>State.YouTurn.DistanceToTrigger</c> always
/// saw 0.
/// </summary>
[TestFixture]
public class YouTurnDistanceToTriggerTests
{
    /// <summary>
    /// With a precomputed turn path whose start sits ~10 m north of the tractor
    /// (along the north-bound AB line, still inside the cultivated area), Tick
    /// must publish the actual pivot→turn-start distance. Trigger must NOT fire
    /// because we're outside the 2 m proximity threshold.
    /// </summary>
    [Test]
    public void Tick_with_armed_path_far_from_trigger_publishes_actual_distance()
    {
        var stateMachine = BuildStateMachine();
        var ctx = BuildTickContext(tractorEasting: 0, tractorNorthing: 0);
        var guidance = new GuidanceWorkingState();
        var turn = new YouTurnWorkingState
        {
            TurnPath = BuildTurnPath(turnStartEasting: 0, turnStartNorthing: 10),
        };

        stateMachine.Tick(in ctx, guidance, turn);

        Assert.Multiple(() =>
        {
            Assert.That(turn.DistanceToTrigger, Is.EqualTo(10.0).Within(0.01),
                "Tick must publish the Euclidean distance from current pivot to TurnPath[0]");
            Assert.That(turn.IsTriggered, Is.False, "10 m is far past the 2 m trigger threshold");
            Assert.That(turn.IsExecuting, Is.False);
        });
    }

    /// <summary>
    /// When the tractor is at the trigger point (within the &lt; 2 m proximity
    /// threshold), Tick must publish a distance &le; 2 m AND fire the trigger.
    /// </summary>
    [Test]
    public void Tick_with_tractor_at_trigger_point_publishes_near_zero_distance()
    {
        var stateMachine = BuildStateMachine();
        var ctx = BuildTickContext(tractorEasting: 0, tractorNorthing: 10);
        var guidance = new GuidanceWorkingState();
        var turn = new YouTurnWorkingState
        {
            TurnPath = BuildTurnPath(turnStartEasting: 0, turnStartNorthing: 10),
        };

        stateMachine.Tick(in ctx, guidance, turn);

        Assert.Multiple(() =>
        {
            Assert.That(turn.DistanceToTrigger, Is.EqualTo(0.0).Within(0.01),
                "At the trigger point, the published distance must be ~0");
            Assert.That(turn.IsTriggered, Is.True, "Within the proximity threshold, the trigger must fire");
            Assert.That(turn.IsExecuting, Is.True);
        });
    }

    // ── Direction override consumption ───────────────────────────────────
    // The U-turn direction toggle pre-flips NextUTurnDirectionLeftOverride
    // while the operator is idle in a row. The state machine must consult
    // and clear the override when it next computes IsTurnLeft for an
    // automatic turn. Mirrors legacy FormGPS.SwapDirection behavior.

    /// <summary>
    /// When NextUTurnDirectionLeftOverride is set before turn creation, Tick
    /// must use it as the direction for the upcoming turn AND clear the
    /// override so it doesn't leak into a later turn.
    /// </summary>
    [Test]
    public void Tick_with_direction_override_set_applies_it_then_clears()
    {
        var stateMachine = BuildStateMachine();
        // Tractor 35 m before the headland (within the 10–60 m create window),
        // aligned with the AB line, in the cultivated area — turn-creation
        // gating triggers on this cycle.
        var ctx = BuildTickContext(tractorEasting: 0, tractorNorthing: 10);
        var guidance = new GuidanceWorkingState();
        var turn = new YouTurnWorkingState
        {
            // Pre-flip: the user wants the next turn to swing LEFT regardless
            // of the geometry-derived default.
            NextUTurnDirectionLeftOverride = true,
        };

        stateMachine.Tick(in ctx, guidance, turn);

        Assert.Multiple(() =>
        {
            Assert.That(turn.IsTurnLeft, Is.True,
                "Override must drive IsTurnLeft when set");
            Assert.That(turn.NextUTurnDirectionLeftOverride, Is.Null,
                "Override must be cleared on consumption so it doesn't leak");
        });
    }

    [Test]
    public void Tick_with_direction_override_false_applies_right_turn()
    {
        var stateMachine = BuildStateMachine();
        var ctx = BuildTickContext(tractorEasting: 0, tractorNorthing: 10);
        var guidance = new GuidanceWorkingState();
        var turn = new YouTurnWorkingState
        {
            NextUTurnDirectionLeftOverride = false,
        };

        stateMachine.Tick(in ctx, guidance, turn);

        Assert.Multiple(() =>
        {
            Assert.That(turn.IsTurnLeft, Is.False,
                "Override=false must drive IsTurnLeft to false (right turn)");
            Assert.That(turn.NextUTurnDirectionLeftOverride, Is.Null,
                "Override must be cleared on consumption");
        });
    }

    /// <summary>
    /// Operator presses the swap-direction button while a turn path is already
    /// rendered but execution hasn't started. Tick must drop the rendered path
    /// (so the CREATE branch can recompute it with the new direction) and leave
    /// the override set so the next CREATE consumes it. Without this, the
    /// override is captured but never applied — the operator's tap has no
    /// visible effect.
    /// </summary>
    [Test]
    public void Tick_with_override_set_and_path_rendered_drops_path_for_recompute()
    {
        var stateMachine = BuildStateMachine();
        // Pre-trigger window: tractor 10 m before the turn-start point, AB
        // aligned, in cultivated area, headland in range.
        var ctx = BuildTickContext(tractorEasting: 0, tractorNorthing: 30);
        var guidance = new GuidanceWorkingState();
        var renderedPath = BuildTurnPath(turnStartEasting: 0, turnStartNorthing: 40);
        var turn = new YouTurnWorkingState
        {
            TurnPath = renderedPath,
            IsTriggered = false,
            IsExecuting = false,
            NextUTurnDirectionLeftOverride = true, // operator just tapped the button
        };

        stateMachine.Tick(in ctx, guidance, turn);

        Assert.Multiple(() =>
        {
            Assert.That(turn.TurnPath, Is.Not.SameAs(renderedPath),
                "Rendered path must be cleared so CREATE can recompute with the new direction");
            // Either the path is null (nothing to recompute this cycle because
            // alignment/range conditions weren't all met) or it was rebuilt by
            // the CREATE branch later in this same Tick. Both are acceptable —
            // the key invariant is the original (wrong-direction) path no
            // longer survives.
        });
    }

    /// <summary>
    /// Mid-arc safety: when the turn is already executing, the swap-direction
    /// override must NOT yank the path out from under the guidance service.
    /// Flipping direction mid-arc would whip the steering. The override stays
    /// set; the state machine simply ignores it until the next idle cycle.
    /// </summary>
    [Test]
    public void Tick_with_override_set_during_execution_does_not_clear_path()
    {
        var stateMachine = BuildStateMachine();
        var ctx = BuildTickContext(tractorEasting: 0, tractorNorthing: 40);
        var guidance = new GuidanceWorkingState();
        var renderedPath = BuildTurnPath(turnStartEasting: 0, turnStartNorthing: 40);
        var turn = new YouTurnWorkingState
        {
            TurnPath = renderedPath,
            IsTriggered = true,
            IsExecuting = true,
            NextUTurnDirectionLeftOverride = true,
        };

        stateMachine.Tick(in ctx, guidance, turn);

        Assert.That(turn.TurnPath, Is.SameAs(renderedPath),
            "Mid-arc the rendered path must be preserved — flipping direction now would whip the steering");
    }

    /// <summary>
    /// With no precomputed turn path (no upcoming turn the state machine knows
    /// about), Tick must leave <c>DistanceToTrigger</c> at 0 — there's no
    /// trigger point to count down to.
    /// </summary>
    [Test]
    public void Tick_without_armable_turn_keeps_distance_to_trigger_zero()
    {
        var stateMachine = BuildStateMachine();
        // Tractor parked in cultivated area but with no boundary aligned for headland creation:
        // we deliberately seed a non-zero DistanceToTrigger to ensure Tick clears it.
        var ctx = BuildTickContext(tractorEasting: 0, tractorNorthing: 0);
        var guidance = new GuidanceWorkingState();
        var turn = new YouTurnWorkingState
        {
            DistanceToTrigger = 999.0, // sentinel — Tick must overwrite
            // No TurnPath, not executing — there is nothing to count down to.
        };

        stateMachine.Tick(in ctx, guidance, turn);

        Assert.That(turn.DistanceToTrigger, Is.EqualTo(0.0),
            "Without an armed TurnPath, DistanceToTrigger must remain 0");
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static YouTurnStateMachine BuildStateMachine()
    {
        var polygonOffset = Substitute.For<Services.Geometry.IPolygonOffsetService>();
        var creation = new YouTurnCreationService(
            NullLogger<YouTurnCreationService>.Instance, polygonOffset);
        var pathing = new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance);
        return new YouTurnStateMachine(creation, pathing, NullLogger<YouTurnStateMachine>.Instance);
    }

    /// <summary>
    /// Square 100×100 field centered on origin, 5 m headland inset, north-south
    /// AB line at x=0. Tractor sits at the requested location facing north so
    /// it's aligned with the AB line (Tick's gating prerequisites).
    /// </summary>
    private static YouTurnStateMachine.TickContext BuildTickContext(
        double tractorEasting,
        double tractorNorthing)
    {
        var outerPoints = new List<Vec2>
        {
            new(-50, -50), new(50, -50), new(50, 50), new(-50, 50),
        };
        var outerPolygon = new BoundaryPolygon
        {
            Points = outerPoints.Select(p => new BoundaryPoint(p.Easting, p.Northing, 0)).ToList(),
        };
        var boundary = new Boundary { OuterBoundary = outerPolygon };

        var headlandLine = new List<Vec3>
        {
            new(-45, -45, 0), new(45, -45, 0), new(45, 45, 0), new(-45, 45, 0),
        };

        var track = Models.Track.Track.FromABLine(
            "AB-test", new Vec3(0, -100, 0), new Vec3(0, 100, 0));

        var currentPosition = new Position
        {
            Easting = tractorEasting,
            Northing = tractorNorthing,
            Heading = 0, // north (degrees)
        };

        return new YouTurnStateMachine.TickContext(
            currentPosition,
            track,
            boundary,
            headlandLine,
            UTurnSkipRows: 0,
            IsSkipWorkedMode: false,
            HeadlandCalculatedWidth: 10.0,
            HeadlandDistance: 5.0);
    }

    /// <summary>
    /// Build a synthetic 3-point turn path. TurnPath[0] is the trigger point
    /// (start of the arc); the rest of the geometry is irrelevant for the
    /// distance-to-trigger publication path.
    /// </summary>
    private static List<Vec3> BuildTurnPath(double turnStartEasting, double turnStartNorthing)
    {
        return new List<Vec3>
        {
            new(turnStartEasting, turnStartNorthing, 0),
            new(turnStartEasting + 1, turnStartNorthing + 1, 0),
            new(turnStartEasting + 2, turnStartNorthing + 2, 0),
        };
    }
}
