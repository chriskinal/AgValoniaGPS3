// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.YouTurn;

using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.Services.YouTurn;

/// <summary>
/// Drives the YouTurn state machine: zone tracking, distance-to-headland, turn creation
/// gating, trigger detection, reset on overshoot, and completion. Mutates
/// <see cref="YouTurnState"/> and <see cref="GuidanceState"/> directly; returns an
/// <see cref="YouTurnEffects"/> describing the side-effects the caller must apply
/// (map updates, guidance state reset, status message).
/// </summary>
public sealed class YouTurnStateMachine
{
    // Turn-creation window: only consider creating a turn when the tractor is this far
    // from the headland. AgOpenGPS parity — matches the legacy numbers exactly.
    private const double MinDistanceToCreate = 10.0;
    private const double MaxDistanceToCreate = 60.0;

    // Alignment tolerance around the AB line direction (forward or reverse) for turn gating.
    // ~20 degrees. Wider tolerances risk creating turns while mid-turn when heading swings.
    private static readonly double AlignmentTolerance = Math.PI / 9;

    // Trigger when the tractor is within this far of the pre-computed turn start point.
    private const double TriggerProximityMeters = 2.0;

    // Completion thresholds.
    // ClosestApproachThreshold: if the tractor was this close to the end
    // point and starts moving away, the turn is complete. This is more
    // robust than a fixed radius check because it works at any speed/GPS rate.
    private const double ClosestApproachThreshold = 5.0;
    private const double CompletionMinTraveledMeters = 5.0;

    private readonly YouTurnCreationService _creation;
    private readonly YouTurnPathingService _pathing;
    private readonly ILogger<YouTurnStateMachine> _logger;

    public YouTurnStateMachine(
        YouTurnCreationService creation,
        YouTurnPathingService pathing,
        ILogger<YouTurnStateMachine> logger)
    {
        _creation = creation;
        _pathing = pathing;
        _logger = logger;
    }

    /// <summary>
    /// Per-cycle inputs for the state machine. Immutable snapshot — the state machine
    /// reads these but does not retain references beyond a single call.
    /// </summary>
    public readonly record struct TickContext(
        Position CurrentPosition,
        Models.Track.Track? SelectedTrack,
        Boundary? Boundary,
        IReadOnlyList<Vec3>? HeadlandLine,
        int UTurnSkipRows,
        bool IsSkipWorkedMode,
        double HeadlandCalculatedWidth,
        double HeadlandDistance);

    /// <summary>
    /// Run one cycle of the state machine. Precondition: autosteer engaged, track active,
    /// YouTurn enabled, headland line has ≥3 points. The caller should gate these before
    /// invoking Tick.
    /// </summary>
    public YouTurnEffects Tick(in TickContext ctx, GuidanceWorkingState guidance, YouTurnWorkingState turn)
    {
        var effects = new YouTurnEffects();
        var track = ctx.SelectedTrack;
        if (track == null || track.Points.Count < 2 || ctx.HeadlandLine == null)
            return effects;

        var currentPosition = ctx.CurrentPosition;
        double headingRadians = currentPosition.Heading * Math.PI / 180.0;
        bool isCurve = track.Points.Count > 2;

        // Local AB heading. For curves, use the heading at the nearest point;
        // for AB lines, derive from the two endpoints.
        double abHeading;
        if (isCurve)
        {
            double minDistSq = double.MaxValue;
            int nearestIdx = 0;
            for (int i = 0; i < track.Points.Count; i++)
            {
                double dx = track.Points[i].Easting - currentPosition.Easting;
                double dy = track.Points[i].Northing - currentPosition.Northing;
                double distSq = dx * dx + dy * dy;
                if (distSq < minDistSq) { minDistSq = distSq; nearestIdx = i; }
            }
            abHeading = track.Points[nearestIdx].Heading;
            _logger.LogDebug("[YouTurn] Curve mode: nearest index={Idx}, localHeading={Deg:F1}°",
                nearestIdx, abHeading * 180 / Math.PI);
        }
        else
        {
            var trackPointA = track.Points[0];
            var trackPointB = track.Points[1];
            double abDx = trackPointB.Easting - trackPointA.Easting;
            double abDy = trackPointB.Northing - trackPointA.Northing;
            abHeading = Math.Atan2(abDx, abDy);
            if (turn.YouTurnCounter % 30 == 0)
                _logger.LogDebug("[YouTurn] AB Line: abHeading={Deg:F1}°", abHeading * 180 / Math.PI);
        }

        // Is the vehicle heading the same way as the AB line (forward) or reverse?
        double headingDiff = headingRadians - abHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        guidance.IsHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        bool alignedForward = Math.Abs(headingDiff) < AlignmentTolerance;
        bool alignedReverse = Math.Abs(headingDiff) > (Math.PI - AlignmentTolerance);
        bool isAlignedWithABLine = alignedForward || alignedReverse;

        // Distance-to-headland raycast is valid only when the tractor is aligned with the track;
        // otherwise heading swings mid-turn create spurious proximity readings.
        double travelHeading = abHeading;
        if (!guidance.IsHeadingSameWay)
        {
            travelHeading += Math.PI;
            if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
        }

        turn.DistanceToHeadland = isAlignedWithABLine
            ? RaycastDistanceToHeadland(currentPosition, travelHeading, ctx.HeadlandLine, guidance.IsHeadingSameWay, turn.YouTurnCounter)
            : double.MaxValue;

        turn.CurrentZone = DetermineZone(currentPosition.Easting, currentPosition.Northing, ctx.Boundary, ctx.HeadlandLine);

        bool isInCultivatedArea = turn.CurrentZone == TractorZone.InCultivatedArea;
        bool isInHeadlandZone = turn.CurrentZone == TractorZone.InHeadland;

        bool headlandInRange = turn.DistanceToHeadland > MinDistanceToCreate &&
                               turn.DistanceToHeadland < MaxDistanceToCreate;

        if (turn.TurnPath == null && !turn.IsExecuting && turn.DistanceToHeadland < 100)
        {
            _logger.LogDebug("[YouTurn] Zone={Zone}, dist={Dist:F1}m, aligned={Aligned}, inRange={InRange}",
                turn.CurrentZone, turn.DistanceToHeadland, isAlignedWithABLine, headlandInRange);
        }

        // ── TURN CREATION ───────────────────────────────────────────────
        bool canCreateTurn = isInCultivatedArea && headlandInRange;
        if (!canCreateTurn && ctx.IsSkipWorkedMode && isInHeadlandZone && isAlignedWithABLine
            && turn.SnakeSequence != null && turn.TurnPath == null && !turn.IsExecuting)
        {
            // In the headland at the far end of a field: the tractor reached the end of the
            // current pass; the snake sequence may still have more passes to work.
            canCreateTurn = _pathing.GetNextSnakePath(turn).HasValue;
        }

        // Default: no armable trigger point — widget shows nothing meaningful.
        // Recomputed below when a precomputed turn path exists and execution hasn't started.
        turn.DistanceToTrigger = 0;

        // ── DIRECTION-OVERRIDE RE-ARM ───────────────────────────────────
        // If the operator pressed the swap-direction button while a turn
        // was already rendered (TurnPath != null) but execution hasn't
        // begun, drop the rendered path so the CREATE branch below
        // recomputes it with the new direction this same cycle. The
        // override flag itself is cleared inside HandleNormalCreation /
        // HandleSnakeCreation after consumption — leave it set here.
        // Mid-arc flips are unsafe and stay no-op.
        if (turn.NextUTurnDirectionLeftOverride.HasValue
            && turn.TurnPath != null
            && !turn.IsExecuting)
        {
            _logger.LogDebug("[YouTurn] Direction override set with rendered path — re-arming with {Dir}",
                turn.NextUTurnDirectionLeftOverride.Value ? "LEFT" : "RIGHT");
            turn.TurnPath = null;
            turn.NextTrack = null;
            turn.IsTriggered = false;
            effects.SyncTurnPathToMap = true;
            effects.SyncNextTrackToMap = true;
        }

        if (turn.TurnPath == null && !turn.IsExecuting && canCreateTurn && isAlignedWithABLine)
        {
            if (ctx.IsSkipWorkedMode)
                HandleSnakeCreation(in ctx, track, abHeading, currentPosition, headingRadians, guidance, turn, effects);
            else
                HandleNormalCreation(in ctx, track, abHeading, currentPosition, headingRadians, guidance, turn, effects);
        }
        // ── TURN TRIGGER ────────────────────────────────────────────────
        else if (turn.TurnPath != null && turn.TurnPath.Count > 2 && !turn.IsTriggered && !turn.IsExecuting)
        {
            var turnStart = turn.TurnPath[0];
            double distToTurnStart = Math.Sqrt(
                (currentPosition.Easting - turnStart.Easting) * (currentPosition.Easting - turnStart.Easting) +
                (currentPosition.Northing - turnStart.Northing) * (currentPosition.Northing - turnStart.Northing));

            // Publish current pivot→trigger distance for the UI countdown widget.
            // Only meaningful while a precomputed turn-start exists and we haven't
            // begun executing yet; once IsExecuting flips, the widget is expected
            // to switch to a "turning" state and we revert to 0 (set above).
            turn.DistanceToTrigger = distToTurnStart;

            if (distToTurnStart <= TriggerProximityMeters)
            {
                turn.IsTriggered = true;
                turn.IsExecuting = true;
                effects.StatusMessage = "YouTurn triggered!";
                _logger.LogDebug("[YouTurn] Triggered at {Dist:F2}m from turn start", distToTurnStart);
            }
        }
        // ── TURN RESET (drove past turn start into headland without triggering) ─
        else if (turn.TurnPath != null && !turn.IsTriggered && isInHeadlandZone)
        {
            _logger.LogDebug("[YouTurn] Entered headland without triggering - resetting turn");
            turn.TurnPath = null;
            turn.NextTrack = null;
            effects.SyncTurnPathToMap = true;
            effects.SyncNextTrackToMap = true;
        }

        // ── TURN COMPLETION ─────────────────────────────────────────────
        // Two-stage completion:
        //   (1) Early-completion gated on remaining ARC LENGTH along the
        //       path (not Euclidean distance to endpoint). Switches
        //       guidance back to the regular track before the pure-
        //       pursuit goal can collapse to zero on the final segment.
        //   (2) Legacy closest-approach as a backstop for the case
        //       where (1) misses (e.g., a very short path where the
        //       arc-length never exceeds the lookahead).
        if (turn.IsExecuting && turn.TurnPath != null && turn.TurnPath.Count > 2)
        {
            var startPoint = turn.TurnPath[0];
            var endPoint = turn.TurnPath[turn.TurnPath.Count - 1];

            double distToTurnStart = Math.Sqrt(
                (currentPosition.Easting - startPoint.Easting) * (currentPosition.Easting - startPoint.Easting) +
                (currentPosition.Northing - startPoint.Northing) * (currentPosition.Northing - startPoint.Northing));
            double distToTurnEnd = Math.Sqrt(
                (currentPosition.Easting - endPoint.Easting) * (currentPosition.Easting - endPoint.Easting) +
                (currentPosition.Northing - endPoint.Northing) * (currentPosition.Northing - endPoint.Northing));

            // Early-completion: ARC-LENGTH-based, NOT Euclidean. On omega
            // U-turns the path's start and end are physically close
            // (~3 m apart on the v12 production geometry), so a
            // Euclidean distToTurnEnd check fires the moment the path
            // is plotted — the v14 break. Arc-length to the end via the
            // pivot's closest path index correctly tracks actual
            // progress along the loop and only crosses the lookahead
            // threshold when the pivot really is in the last few
            // segments.
            //
            // Magic number 4.0 m matches
            // ConfigurationStore.Guidance.GoalPointLookAheadHold (the
            // pure-pursuit lookahead in YouTurnGuidanceService). Once
            // remaining arc-length falls below lookahead, the walk
            // can't satisfy the configured lookahead anyway — the goal
            // would collapse toward the path endpoint on subsequent
            // ticks. Defense-in-depth backstop: the
            // YouTurnGuidanceService collapsed-goal post-walk guard
            // (commit 61674b59) still catches the corner case if this
            // check misses.
            const double EarlyCompletionLookahead = 4.0;
            double remainingArc = ComputeRemainingArcLength(turn.TurnPath, currentPosition);
            double totalArc = ComputeTotalArcLength(turn.TurnPath);
            double traveledArc = totalArc - remainingArc;

            // Fire when:
            //   (a) less than lookahead of arc-length remains (the goal
            //       would collapse on the final segment), AND
            //   (b) at least lookahead of arc-length has been traversed
            //       (the tractor has genuinely progressed; protects
            //       against tick-0 fire on paths shorter than 2 ×
            //       lookahead AND against fresh-path-pivot-near-end
            //       cases where creation was wrong).
            // Together these require the path itself to be longer than
            // 2 × lookahead, which is true for any production U-turn
            // path (the v12 omega is ~42 m) but false for synthetic
            // test paths (~3-5 m). Short paths fall through to the
            // legacy closest-approach detector.
            if (remainingArc < EarlyCompletionLookahead
                && traveledArc > EarlyCompletionLookahead)
            {
                _logger.LogDebug("[YouTurn] Early-completion (remaining arc {Arc:F1}m < {L:F1}m, "
                    + "traveled {Tr:F1}m): distStart={DistStart:F2}m distEnd={DistEnd:F2}m",
                    remainingArc, EarlyCompletionLookahead, traveledArc,
                    distToTurnStart, distToTurnEnd);
                CompleteTurn(in ctx, guidance, turn, effects);
                return effects;
            }

            // Closest-approach backstop: complete when the tractor was
            // close to end, is now moving away, and has traveled far
            // enough from the start. Speed/GPS-rate independent — a
            // fixed radius check misses the end point at high speeds
            // because discrete GPS steps jump over it.
            bool wasClose = turn.PreviousDistToTurnEnd < ClosestApproachThreshold;
            bool movingAway = distToTurnEnd > turn.PreviousDistToTurnEnd;
            bool traveledEnough = distToTurnStart > CompletionMinTraveledMeters;

            if (wasClose && movingAway && traveledEnough
                && distToTurnEnd < distToTurnStart)
            {
                _logger.LogDebug("[YouTurn] Closest-approach completion: distEnd={DistEnd:F1}m prevDist={Prev:F1}m distStart={DistStart:F1}m",
                    distToTurnEnd, turn.PreviousDistToTurnEnd, distToTurnStart);
                CompleteTurn(in ctx, guidance, turn, effects);
            }

            turn.PreviousDistToTurnEnd = distToTurnEnd;
        }

        return effects;
    }

    /// <summary>
    /// Sum of segment lengths from the closest path point to
    /// <paramref name="pivot"/> through the path's last point. Returns
    /// arc-length remaining "along the path", which is topology-aware
    /// (handles omega U-turns where the path's start and end are
    /// physically close but separated by the full loop's arc).
    /// </summary>
    private static double ComputeRemainingArcLength(
        IReadOnlyList<Vec3> path, Position pivot)
    {
        if (path.Count < 2) return 0;

        // Closest path index.
        int closestIdx = 0;
        double minDistSq = double.MaxValue;
        for (int i = 0; i < path.Count; i++)
        {
            double dx = path[i].Easting - pivot.Easting;
            double dy = path[i].Northing - pivot.Northing;
            double d2 = dx * dx + dy * dy;
            if (d2 < minDistSq) { minDistSq = d2; closestIdx = i; }
        }

        // Sum segment lengths from closestIdx to end.
        double remaining = 0;
        for (int i = closestIdx; i < path.Count - 1; i++)
        {
            double dx = path[i + 1].Easting - path[i].Easting;
            double dy = path[i + 1].Northing - path[i].Northing;
            remaining += Math.Sqrt(dx * dx + dy * dy);
        }
        return remaining;
    }

    /// <summary>
    /// Total arc length of <paramref name="path"/>, computed as the sum of
    /// pairwise distances between consecutive points. Used to gate the
    /// early-completion check against short synthetic paths.
    /// </summary>
    private static double ComputeTotalArcLength(IReadOnlyList<Vec3> path)
    {
        double total = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            double dx = path[i + 1].Easting - path[i].Easting;
            double dy = path[i + 1].Northing - path[i].Northing;
            total += Math.Sqrt(dx * dx + dy * dy);
        }
        return total;
    }

    /// <summary>
    /// Manually trigger a U-turn in the specified direction. Used for tracks along boundaries
    /// where automatic headland detection doesn't fire.
    /// </summary>
    public YouTurnEffects TriggerManual(
        bool turnLeft,
        bool isAutoSteerEngaged,
        in TickContext ctx,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn)
    {
        var effects = new YouTurnEffects();

        if (!isAutoSteerEngaged || ctx.SelectedTrack == null)
        {
            effects.StatusMessage = "Enable autosteer first";
            return effects;
        }

        if (turn.IsExecuting)
        {
            // Refuse to swap paths while the guidance service is actively following
            // one — doing so could whip the steering mid-arc.
            effects.StatusMessage = "U-turn already in progress";
            return effects;
        }

        // If an auto-trigger plotted a path but it hasn't engaged yet, discard it so
        // the manual trigger's immediate arc takes over.
        if (turn.TurnPath != null)
        {
            turn.TurnPath = null;
            turn.IsTriggered = false;
            effects.SyncTurnPathToMap = true;
        }

        var track = ctx.SelectedTrack;
        if (track.Points.Count < 2)
        {
            effects.StatusMessage = "Invalid track";
            return effects;
        }

        var currentPosition = ctx.CurrentPosition;
        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

        // For manual turns, always use the straight-line AB heading even for curves (matches legacy behavior).
        var trackPointA = track.Points[0];
        var trackPointB = track.Points[track.Points.Count - 1];
        double abDx = trackPointB.Easting - trackPointA.Easting;
        double abDy = trackPointB.Northing - trackPointA.Northing;
        double abHeading = Math.Atan2(abDx, abDy);

        double headingDiff = headingRadians - abHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        guidance.IsHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        turn.IsTurnLeft = turnLeft;
        turn.WasHeadingSameWayAtTurnStart = guidance.IsHeadingSameWay;

        _logger.LogDebug("[ManualYouTurn] Triggering {Dir} turn, isHeadingSameWay={Way}",
            turnLeft ? "LEFT" : "RIGHT", guidance.IsHeadingSameWay);

        _pathing.ComputeNextTrack(track, abHeading, guidance, turn,
            ctx.UTurnSkipRows, ctx.IsSkipWorkedMode, ctx.SelectedTrack);
        effects.SyncNextTrackToMap = true;
        effects.IsInYouTurnMapFlag = true;

        // Manual triggers skip the boundary-anchored Dubins pipeline used by auto turns.
        // The arc starts at the tractor's current position so the turn begins the instant
        // the path renders — no headland traversal, no entry/exit legs (#260).
        var path = _creation.CreateManualArcPath(
            ctx.CurrentPosition, abHeading, turnLeft,
            ctx.Boundary, guidance, ctx.UTurnSkipRows);

        if (path.Count > 2)
        {
            turn.TurnPath = path;
            turn.YouTurnCounter = 0;
            effects.SyncTurnPathToMap = true;
            // Manual turns trigger immediately — no proximity check against the turn start.
            turn.IsTriggered = true;
            turn.IsExecuting = true;
            effects.StatusMessage = $"Manual {(turnLeft ? "left" : "right")} U-turn started";
        }
        else
        {
            effects.StatusMessage = "Not enough room — turn would leave the field";
        }

        return effects;
    }

    /// <summary>
    /// Clear all U-turn state. Called when closing a field. Safe to call at any time.
    /// </summary>
    public static void ClearState(YouTurnWorkingState turn)
    {
        turn.TurnPath = null;
        turn.NextTrack = null;
        turn.IsTriggered = false;
        turn.IsExecuting = false;
        turn.YouTurnCounter = 0;
        turn.CurrentZone = TractorZone.OutsideBoundary;
    }

    // ── Private helpers ─────────────────────────────────────────────────

    private void HandleSnakeCreation(
        in TickContext ctx,
        Models.Track.Track track,
        double abHeading,
        Position currentPosition,
        double headingRadians,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        YouTurnEffects effects)
    {
        // Build the rotated snake sequence lazily on first turn.
        if (turn.SnakeSequence == null)
        {
            _pathing.BuildSnakeSequence(track, abHeading, guidance, turn, ctx.Boundary, ctx.HeadlandLine);
        }

        int? nextPath = _pathing.GetNextSnakePath(turn);
        if (nextPath == null)
        {
            _logger.LogDebug("[YouTurn] Snake sequence complete — field done");
            effects.StatusMessage = "Field complete — all tracks worked";
            return;
        }

        var config = ConfigurationStore.Instance;
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        double nextDistAway = widthMinusOverlap * nextPath.Value;
        int pathDiff = nextPath.Value - guidance.HowManyPathsAway;

        // Snake mode directly sets the turn geometry without going through the regular
        // skip logic — the sequence dictates pathDiff.
        bool positiveOffset = pathDiff > 0;
        turn.IsTurnLeft = positiveOffset ^ guidance.IsHeadingSameWay;
        // Apply the UI's one-shot direction override before the snake geometry is
        // committed; clear so the next snake step computes its own direction.
        if (turn.NextUTurnDirectionLeftOverride is { } overrideLeft)
        {
            turn.IsTurnLeft = overrideLeft;
            turn.NextUTurnDirectionLeftOverride = null;
        }
        turn.WasHeadingSameWayAtTurnStart = guidance.IsHeadingSameWay;
        turn.NextTrackTurnOffset = Math.Abs(pathDiff) * widthMinusOverlap;

        var refA = track.Points[0];
        var refB = track.Points[track.Points.Count - 1];
        double perpAngle = abHeading + Math.PI / 2;

        if (track.Points.Count == 2)
        {
            double offsetE = Math.Sin(perpAngle) * nextDistAway;
            double offsetN = Math.Cos(perpAngle) * nextDistAway;
            turn.NextTrack = Models.Track.Track.FromABLine(
                $"Path {nextPath.Value}",
                new Vec3(refA.Easting + offsetE, refA.Northing + offsetN, abHeading),
                new Vec3(refB.Easting + offsetE, refB.Northing + offsetN, abHeading));
        }
        else
        {
            var offsetPoints = CurveProcessing.CreateOffsetCurve(track.Points, nextDistAway);
            turn.NextTrack = Models.Track.Track.FromCurve($"Path {nextPath.Value}", offsetPoints, track.IsClosed);
        }
        turn.NextTrack.IsActive = false;

        // CompleteTurn will jump directly to this path number instead of computing a skip.
        turn.ReturnPassTargetPath = nextPath.Value;

        _logger.LogDebug("[YouTurn] Snake: path {Cur} -> {Next} (diff={Diff}, offset={Off:F1}m, turnLeft={Left})",
            guidance.HowManyPathsAway, nextPath.Value, pathDiff, nextDistAway, turn.IsTurnLeft);

        effects.SyncNextTrackToMap = true;
        effects.IsInYouTurnMapFlag = true;

        CreatePathAndSync(in ctx, track, headingRadians, abHeading, guidance, turn, effects);
    }

    private void HandleNormalCreation(
        in TickContext ctx,
        Models.Track.Track track,
        double abHeading,
        Position currentPosition,
        double headingRadians,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        YouTurnEffects effects)
    {
        var (nextLineInside, positiveDirection) = _pathing.WouldNextLineBeInsideBoundary(
            track, abHeading, guidance, ctx.Boundary, ctx.HeadlandLine, ctx.UTurnSkipRows);

        _logger.LogDebug("[YouTurn] Creating turn? nextLineInside={Inside} positiveDir={Dir}", nextLineInside, positiveDirection);
        if (!nextLineInside)
        {
            _logger.LogDebug("[YouTurn] Next line would be outside boundary - stopping U-turns");
            effects.StatusMessage = "End of field reached";
            return;
        }

        _logger.LogDebug("[YouTurn] Creating turn path at {Dist:F1}m from headland", turn.DistanceToHeadland);
        // IsTurnLeft depends on which direction has cultivated area and the current heading.
        // positiveDirection ^ IsHeadingSameWay gives the correct turn direction.
        turn.IsTurnLeft = positiveDirection ^ guidance.IsHeadingSameWay;
        // Apply the UI's one-shot direction override (set by the U-turn direction
        // toggle while idle), then clear it so it doesn't leak into a later turn.
        // Mirrors legacy FormGPS.SwapDirection — the user pre-selects which way
        // the next U-turn should swing while still in the cultivated row.
        if (turn.NextUTurnDirectionLeftOverride is { } overrideLeft)
        {
            turn.IsTurnLeft = overrideLeft;
            turn.NextUTurnDirectionLeftOverride = null;
            _logger.LogDebug("[YouTurn] Applied direction override: turnLeft={Left}", overrideLeft);
        }
        turn.WasHeadingSameWayAtTurnStart = guidance.IsHeadingSameWay;

        _pathing.ComputeNextTrack(
            track, abHeading, guidance, turn,
            ctx.UTurnSkipRows, ctx.IsSkipWorkedMode, ctx.SelectedTrack);
        effects.SyncNextTrackToMap = true;
        effects.IsInYouTurnMapFlag = true;

        CreatePathAndSync(in ctx, track, headingRadians, abHeading, guidance, turn, effects);
    }

    private void CreatePathAndSync(
        in TickContext ctx,
        Models.Track.Track track,
        double headingRadians,
        double abHeading,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        YouTurnEffects effects)
    {
        var result = _creation.CreateTurnPath(
            ctx.CurrentPosition, track, headingRadians, abHeading,
            ctx.Boundary, ctx.HeadlandLine,
            guidance, turn,
            ctx.UTurnSkipRows, ctx.HeadlandCalculatedWidth, ctx.HeadlandDistance);

        if (result.Path == null) return;

        turn.TurnPath = result.Path;
        turn.YouTurnCounter = 0;
        if (!result.UsedFallback && effects.StatusMessage == null)
            effects.StatusMessage = $"YouTurn path created ({result.Path.Count} points)";
        effects.SyncTurnPathToMap = true;
    }

    private void CompleteTurn(
        in TickContext ctx,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        YouTurnEffects effects)
    {
        // Guard against double-call (turn completion can fire from both Tick and
        // eventual pipeline guidance completion).
        if (!turn.IsExecuting)
        {
            _logger.LogDebug("[YouTurn] CompleteTurn called but not in turn - ignoring");
            return;
        }

        var config = ConfigurationStore.Instance;

        if (turn.ReturnPassTargetPath.HasValue)
        {
            _logger.LogDebug("[YouTurn] Turn complete! Jumping to path {Target} (was {Cur})",
                turn.ReturnPassTargetPath.Value, guidance.HowManyPathsAway);
            guidance.HowManyPathsAway = turn.ReturnPassTargetPath.Value;
            turn.ReturnPassTargetPath = null;
            _pathing.AdvanceSnakeSequence(turn);
        }
        else
        {
            int pathsToMove = ctx.UTurnSkipRows + 1;

            // WasHeadingSameWayAtTurnStart was saved at turn creation — IsHeadingSameWay has
            // since flipped (we just finished a 180° turn), so we need the pre-turn value.
            // CreateOffsetCurve offsets RIGHT of heading (positive perpAngle in the
            // Sin/Cos coordinate system). So positive pass = right of AB heading.
            // Turn left while heading same way = next pass is LEFT of heading = negative offset.
            bool positiveOffset = turn.IsTurnLeft ^ turn.WasHeadingSameWayAtTurnStart;

            if (ctx.IsSkipWorkedMode && ctx.SelectedTrack != null)
            {
                pathsToMove = _pathing.GetNextUnworkedPathSkip(
                    ctx.SelectedTrack, guidance.HowManyPathsAway, positiveOffset, pathsToMove);
            }

            int offsetChange = positiveOffset ? pathsToMove : -pathsToMove;
            guidance.HowManyPathsAway += offsetChange;

            _logger.LogDebug("[YouTurn] Turn complete! Normal: offset {Sign} by {Change}",
                positiveOffset ? "positive" : "negative", offsetChange);
        }

        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        _logger.LogDebug("[YouTurn] Now on path {Path} ({Off:F1}m from reference)",
            guidance.HowManyPathsAway, widthMinusOverlap * guidance.HowManyPathsAway);

        turn.LastTurnWasLeft = turn.IsTurnLeft;
        turn.HasCompletedFirstTurn = true;
        turn.IsTriggered = false;
        turn.IsExecuting = false;
        turn.TurnPath = null;
        turn.NextTrack = null;
        // Keep counter high so the next turn creation window opens immediately.
        turn.YouTurnCounter = 10;

        effects.SyncTurnPathToMap = true;
        effects.SyncNextTrackToMap = true;
        effects.IsInYouTurnMapFlag = false;
        effects.TurnCompleted = true;
        effects.StatusMessage = $"Following path {guidance.HowManyPathsAway} ({widthMinusOverlap * Math.Abs(guidance.HowManyPathsAway):F1}m offset)";
    }

    private double RaycastDistanceToHeadland(
        Position currentPosition,
        double headingRadians,
        IReadOnlyList<Vec3> headlandLine,
        bool isHeadingSameWay,
        int counter)
    {
        if (headlandLine.Count < 3) return double.MaxValue;

        double minDistance = double.MaxValue;
        var pos = new Vec2(currentPosition.Easting, currentPosition.Northing);
        var dir = new Vec2(Math.Sin(headingRadians), Math.Cos(headingRadians));

        int intersectionCount = 0;
        int n = headlandLine.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = headlandLine[i];
            var p2 = headlandLine[(i + 1) % n];

            var edge = new Vec2(p2.Easting - p1.Easting, p2.Northing - p1.Northing);
            var toP1 = new Vec2(p1.Easting - pos.Easting, p1.Northing - pos.Northing);

            double cross = dir.Easting * edge.Northing - dir.Northing * edge.Easting;
            if (Math.Abs(cross) < 1e-10) continue;

            double t = (toP1.Easting * edge.Northing - toP1.Northing * edge.Easting) / cross;
            double u = (toP1.Easting * dir.Northing - toP1.Northing * dir.Easting) / cross;

            if (t > 0 && u >= 0 && u <= 1)
            {
                intersectionCount++;
                if (t < minDistance) minDistance = t;
            }
        }

        if (counter % 120 == 0)
        {
            _logger.LogDebug("[Headland] Raycast: pos=({E:F1},{N:F1}), heading={Deg:F0}°, intersections={Hits}, minDist={Dist:F1}m, isHeadingSameWay={Way}",
                pos.Easting, pos.Northing, headingRadians * 180 / Math.PI, intersectionCount, minDistance, isHeadingSameWay);
        }

        return minDistance;
    }

    private static TractorZone DetermineZone(
        double easting,
        double northing,
        Boundary? boundary,
        IReadOnlyList<Vec3>? headlandLine)
    {
        // Cultivated area (inside the headland) — most common case, check first.
        if (headlandLine != null && headlandLine.Count >= 3)
        {
            if (GeometryMath.IsPointInPolygon(headlandLine, new Vec2(easting, northing)))
                return TractorZone.InCultivatedArea;
        }

        if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid)
        {
            if (boundary.OuterBoundary.IsPointInside(easting, northing))
                return TractorZone.InHeadland;
        }

        return TractorZone.OutsideBoundary;
    }
}

/// <summary>
/// Side effects emitted by the <see cref="YouTurnStateMachine"/>. The caller applies
/// these after state mutation: syncs the map service, resets guidance state when a
/// turn completes, and updates the UI status message.
/// </summary>
public sealed class YouTurnEffects
{
    /// <summary>Set when a user-visible status message should be raised.</summary>
    public string? StatusMessage { get; set; }

    /// <summary>True when the caller should push <c>YouTurnState.TurnPath</c> to the map.</summary>
    public bool SyncTurnPathToMap { get; set; }

    /// <summary>True when the caller should push <c>YouTurnState.NextTrack</c> to the map.</summary>
    public bool SyncNextTrackToMap { get; set; }

    /// <summary>
    /// When non-null, the caller should toggle the map's "in YouTurn" flag. This is the
    /// cyan-line / dotted-current-line render flag; it's independent of
    /// <c>YouTurnState.IsExecuting</c> — the map shows it from the moment a next track
    /// is drafted, and clears on completion.
    /// </summary>
    public bool? IsInYouTurnMapFlag { get; set; }

    /// <summary>
    /// True when a turn just completed. The caller should clear its
    /// TrackGuidanceState cache and re-sync the pipeline so guidance targets
    /// the newly-offset track from the start.
    /// </summary>
    public bool TurnCompleted { get; set; }
}
