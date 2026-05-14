// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
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
using AgValoniaGPS.Models.Track;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// YouTurn (automatic U-turn) state machine.
/// </summary>
public class YouTurnState : ObservableObject
{
    // Enable/trigger
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private bool _isTriggered;
    public bool IsTriggered
    {
        get => _isTriggered;
        set => SetProperty(ref _isTriggered, value);
    }

    private bool _isExecuting;
    public bool IsExecuting
    {
        get => _isExecuting;
        set => SetProperty(ref _isExecuting, value);
    }

    // Turn path
    private List<Vec3>? _turnPath;
    public List<Vec3>? TurnPath
    {
        get => _turnPath;
        set => SetProperty(ref _turnPath, value);
    }

    private int _pathIndex;
    public int PathIndex
    {
        get => _pathIndex;
        set => SetProperty(ref _pathIndex, value);
    }

    // Direction
    private bool _isTurnLeft;
    public bool IsTurnLeft
    {
        get => _isTurnLeft;
        set => SetProperty(ref _isTurnLeft, value);
    }

    private bool _lastTurnWasLeft;
    public bool LastTurnWasLeft
    {
        get => _lastTurnWasLeft;
        set => SetProperty(ref _lastTurnWasLeft, value);
    }

    // Distance tracking
    private double _distanceToHeadland;
    public double DistanceToHeadland
    {
        get => _distanceToHeadland;
        set => SetProperty(ref _distanceToHeadland, value);
    }

    private double _distanceToTrigger;
    public double DistanceToTrigger
    {
        get => _distanceToTrigger;
        set => SetProperty(ref _distanceToTrigger, value);
    }

    // Next track after turn (unified Track model)
    private Track.Track? _nextTrack;
    public Track.Track? NextTrack
    {
        get => _nextTrack;
        set => SetProperty(ref _nextTrack, value);
    }

    // Completion tracking
    private Vec2? _lastCompletionPosition;
    public Vec2? LastCompletionPosition
    {
        get => _lastCompletionPosition;
        set => SetProperty(ref _lastCompletionPosition, value);
    }

    private bool _hasCompletedFirstTurn;
    public bool HasCompletedFirstTurn
    {
        get => _hasCompletedFirstTurn;
        set => SetProperty(ref _hasCompletedFirstTurn, value);
    }

    // Counter for stability
    private int _youTurnCounter;
    public int YouTurnCounter
    {
        get => _youTurnCounter;
        set => SetProperty(ref _youTurnCounter, value);
    }

    // Heading direction captured at turn start, used to compute the post-turn pass offset.
    // Stored because _isHeadingSameWay on GuidanceState flips 180° once the turn completes.
    private bool _wasHeadingSameWayAtTurnStart;
    public bool WasHeadingSameWayAtTurnStart
    {
        get => _wasHeadingSameWayAtTurnStart;
        set => SetProperty(ref _wasHeadingSameWayAtTurnStart, value);
    }

    // Pre-computed perpendicular offset to the next track (meters, always positive).
    // Authoritative value for U-turn arc width — consumers should use this rather than recompute.
    private double _nextTrackTurnOffset;
    public double NextTrackTurnOffset
    {
        get => _nextTrackTurnOffset;
        set => SetProperty(ref _nextTrackTurnOffset, value);
    }

    // When set, CompleteTurn jumps directly to this path number (used by snake / skip-worked modes).
    private int? _returnPassTargetPath;
    public int? ReturnPassTargetPath
    {
        get => _returnPassTargetPath;
        set => SetProperty(ref _returnPassTargetPath, value);
    }

    // Pre-computed path sequence for skip-and-fill (snake) mode. Null when not in snake mode.
    private System.Collections.Generic.List<int>? _snakeSequence;
    public System.Collections.Generic.List<int>? SnakeSequence
    {
        get => _snakeSequence;
        set => SetProperty(ref _snakeSequence, value);
    }

    private int _snakeIndex = -1;
    public int SnakeIndex
    {
        get => _snakeIndex;
        set => SetProperty(ref _snakeIndex, value);
    }

    // Zone the tractor is in — source of truth for turn creation gating.
    private TractorZone _currentZone = TractorZone.OutsideBoundary;
    public TractorZone CurrentZone
    {
        get => _currentZone;
        set => SetProperty(ref _currentZone, value);
    }

    /// <summary>
    /// Previous distance to turn end point (state machine bookkeeping for closest-approach completion).
    /// </summary>
    public double PreviousDistToTurnEnd { get; set; } = double.MaxValue;

    /// <summary>
    /// One-shot override for the direction of the *next* armed automatic turn.
    /// Mirror of <see cref="Pipeline.YouTurnWorkingState.NextUTurnDirectionLeftOverride"/>.
    /// </summary>
    public bool? NextUTurnDirectionLeftOverride { get; set; }

    public void Reset()
    {
        IsTriggered = false;
        IsExecuting = false;
        TurnPath = null;
        PathIndex = 0;
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
        NextUTurnDirectionLeftOverride = null;
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
