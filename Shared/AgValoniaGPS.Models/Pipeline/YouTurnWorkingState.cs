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

using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.Models.Pipeline;

/// <summary>
/// Cycle-worker-owned mirror of <see cref="State.YouTurnState"/>.
/// Plain POCO: no ObservableObject, no PropertyChanged, no UI thread awareness.
/// Single-writer — the cycle worker mutates freely. Never touched on the UI thread.
/// See <c>Plans/threading_model.svg</c> (yellow WorkingState box).
/// </summary>
public class YouTurnWorkingState
{
    // Enable/trigger
    public bool IsEnabled { get; set; }
    public bool IsTriggered { get; set; }
    public bool IsExecuting { get; set; }

    // Turn path
    public List<Vec3>? TurnPath { get; set; }
    public int PathIndex { get; set; }

    // Direction
    public bool IsTurnLeft { get; set; }
    public bool LastTurnWasLeft { get; set; }

    // Distance tracking
    public double DistanceToHeadland { get; set; }
    public double DistanceToTrigger { get; set; }

    // Next track after turn (unified Track model)
    public Track.Track? NextTrack { get; set; }

    // Completion tracking
    public Vec2? LastCompletionPosition { get; set; }
    public bool HasCompletedFirstTurn { get; set; }

    // Counter for stability
    public int YouTurnCounter { get; set; }

    // Heading direction captured at turn start, used to compute the post-turn pass offset.
    // Stored because IsHeadingSameWay on GuidanceState flips 180° once the turn completes.
    public bool WasHeadingSameWayAtTurnStart { get; set; }

    // Pre-computed perpendicular offset to the next track (meters, always positive).
    // Authoritative value for U-turn arc width — consumers should use this rather than recompute.
    public double NextTrackTurnOffset { get; set; }

    // When set, CompleteTurn jumps directly to this path number (used by snake / skip-worked modes).
    public int? ReturnPassTargetPath { get; set; }

    // Pre-computed path sequence for skip-and-fill (snake) mode. Null when not in snake mode.
    public List<int>? SnakeSequence { get; set; }

    public int SnakeIndex { get; set; } = -1;

    // Zone the tractor is in — source of truth for turn creation gating.
    public TractorZone CurrentZone { get; set; } = TractorZone.OutsideBoundary;

    /// <summary>
    /// Previous distance to turn end point. Used for closest-approach completion:
    /// when distance starts increasing after being close, the turn is complete.
    /// </summary>
    public double PreviousDistToTurnEnd { get; set; } = double.MaxValue;

    public void Reset()
    {
        IsTriggered = false;
        IsExecuting = false;
        TurnPath = null;
        PathIndex = 0;
        PreviousDistToTurnEnd = double.MaxValue;
        DistanceToHeadland = double.MaxValue;
        DistanceToTrigger = 0;
        NextTrack = null;
        HasCompletedFirstTurn = false;
        YouTurnCounter = 0;
        WasHeadingSameWayAtTurnStart = false;
        NextTrackTurnOffset = 0;
        ReturnPassTargetPath = null;
        SnakeSequence = null;
        SnakeIndex = -1;
        CurrentZone = TractorZone.OutsideBoundary;
    }

    public void CompleteTurn()
    {
        IsExecuting = false;
        IsTriggered = false;
        TurnPath = null;
        LastTurnWasLeft = IsTurnLeft;
        HasCompletedFirstTurn = true;
        YouTurnCounter = 0;
    }
}
