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
/// Immutable per-cycle snapshot of <see cref="YouTurnWorkingState"/>.
/// Built by the cycle worker at end-of-tick, consumed on the UI thread by
/// ApplyGpsCycleResult to mirror onto <see cref="State.YouTurnState"/>.
/// List-valued fields use <see cref="IReadOnlyList{T}"/> so consumers can't
/// mutate working-state storage through the snapshot reference.
/// See <c>Plans/threading_model.svg</c> (cycle stage 3 — Emit snapshot).
/// </summary>
public record YouTurnSnapshot
{
    // Enable/trigger
    public bool IsEnabled { get; init; }
    public bool IsTriggered { get; init; }
    public bool IsExecuting { get; init; }

    // Turn path
    public IReadOnlyList<Vec3>? TurnPath { get; init; }
    public int PathIndex { get; init; }

    // Direction
    public bool IsTurnLeft { get; init; }
    public bool LastTurnWasLeft { get; init; }

    // Distance tracking
    public double DistanceToHeadland { get; init; }
    public double DistanceToTrigger { get; init; }

    // Next track after turn
    public Track.Track? NextTrack { get; init; }

    // Completion tracking
    public Vec2? LastCompletionPosition { get; init; }
    public bool HasCompletedFirstTurn { get; init; }

    // Counter for stability
    public int YouTurnCounter { get; init; }

    public bool WasHeadingSameWayAtTurnStart { get; init; }

    public double NextTrackTurnOffset { get; init; }

    public int? ReturnPassTargetPath { get; init; }

    public IReadOnlyList<int>? SnakeSequence { get; init; }
    public int SnakeIndex { get; init; }

    public TractorZone CurrentZone { get; init; }

    /// <summary>
    /// One-shot direction override mirrored from
    /// <see cref="YouTurnWorkingState.NextUTurnDirectionLeftOverride"/>.
    /// Surfaced on the snapshot so the UI can drop its own cache the
    /// cycle after the state machine consumes and clears the override —
    /// without this signal, the UI's value gets re-written into the
    /// working state every cycle and biases the next auto-armed turn
    /// with the operator's stale intent.
    /// </summary>
    public bool? NextUTurnDirectionLeftOverride { get; init; }

    /// <summary>
    /// True on the single cycle where a turn just completed — used by the UI
    /// to reset its TrackGuidanceState cache so the new offset track is
    /// searched globally rather than resumed from the pre-turn index. Replaces
    /// the legacy flat <c>GpsCycleResult.YouTurnCompleted</c> field.
    /// </summary>
    public bool JustCompleted { get; init; }
}
