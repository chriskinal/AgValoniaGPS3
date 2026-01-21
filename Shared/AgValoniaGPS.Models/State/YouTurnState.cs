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
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// YouTurn (automatic U-turn) state machine.
/// </summary>
public class YouTurnState : ReactiveObject
{
    // Enable/trigger
    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    private bool _isTriggered;
    public bool IsTriggered
    {
        get => _isTriggered;
        set => this.RaiseAndSetIfChanged(ref _isTriggered, value);
    }

    private bool _isExecuting;
    public bool IsExecuting
    {
        get => _isExecuting;
        set => this.RaiseAndSetIfChanged(ref _isExecuting, value);
    }

    // Turn path
    private List<Vec3>? _turnPath;
    public List<Vec3>? TurnPath
    {
        get => _turnPath;
        set => this.RaiseAndSetIfChanged(ref _turnPath, value);
    }

    private int _pathIndex;
    public int PathIndex
    {
        get => _pathIndex;
        set => this.RaiseAndSetIfChanged(ref _pathIndex, value);
    }

    // Direction
    private bool _isTurnLeft;
    public bool IsTurnLeft
    {
        get => _isTurnLeft;
        set => this.RaiseAndSetIfChanged(ref _isTurnLeft, value);
    }

    private bool _lastTurnWasLeft;
    public bool LastTurnWasLeft
    {
        get => _lastTurnWasLeft;
        set => this.RaiseAndSetIfChanged(ref _lastTurnWasLeft, value);
    }

    // Distance tracking
    private double _distanceToHeadland;
    public double DistanceToHeadland
    {
        get => _distanceToHeadland;
        set => this.RaiseAndSetIfChanged(ref _distanceToHeadland, value);
    }

    private double _distanceToTrigger;
    public double DistanceToTrigger
    {
        get => _distanceToTrigger;
        set => this.RaiseAndSetIfChanged(ref _distanceToTrigger, value);
    }

    // Next track after turn (unified Track model)
    private Track.Track? _nextTrack;
    public Track.Track? NextTrack
    {
        get => _nextTrack;
        set => this.RaiseAndSetIfChanged(ref _nextTrack, value);
    }

    // Completion tracking
    private Vec2? _lastCompletionPosition;
    public Vec2? LastCompletionPosition
    {
        get => _lastCompletionPosition;
        set => this.RaiseAndSetIfChanged(ref _lastCompletionPosition, value);
    }

    private bool _hasCompletedFirstTurn;
    public bool HasCompletedFirstTurn
    {
        get => _hasCompletedFirstTurn;
        set => this.RaiseAndSetIfChanged(ref _hasCompletedFirstTurn, value);
    }

    // Counter for stability
    private int _youTurnCounter;
    public int YouTurnCounter
    {
        get => _youTurnCounter;
        set => this.RaiseAndSetIfChanged(ref _youTurnCounter, value);
    }

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
