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

using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing YouTurn (U-turn) UI glue.
/// The state machine, pathing, path creation, and geometry all live in services;
/// this file exposes UI properties/commands and applies state-machine side effects
/// (map updates, guidance reset, status message).
/// </summary>
public partial class MainViewModel
{
    // YouTurn state, turn direction, zone tracking, snake sequence, pass offsets:
    // All live on State.YouTurn and State.Guidance. See YouTurnState.cs / GuidanceState.cs.
    // TractorZone enum is in AgValoniaGPS.Models.State.TractorZone.

    #region YouTurn UI Properties

    private bool _isYouTurnEnabled;
    public bool IsYouTurnEnabled
    {
        get => _isYouTurnEnabled;
        set => SetProperty(ref _isYouTurnEnabled, value);
    }

    private int _uTurnSkipRows;
    /// <summary>Number of rows to skip during U-turn (0–9).</summary>
    public int UTurnSkipRows
    {
        get => _uTurnSkipRows;
        set => SetProperty(ref _uTurnSkipRows, Math.Max(0, Math.Min(9, value)));
    }

    private bool _isUTurnSkipRowsEnabled;
    public bool IsUTurnSkipRowsEnabled
    {
        get => _isUTurnSkipRowsEnabled;
        set => SetProperty(ref _isUTurnSkipRowsEnabled, value);
    }

    private bool _isSkipWorkedMode;
    /// <summary>
    /// When true, U-turns skip over already-worked paths to find the next unworked one,
    /// enabling the skip-and-fill pattern (work every Nth row, then return to fill gaps).
    /// </summary>
    public bool IsSkipWorkedMode
    {
        get => _isSkipWorkedMode;
        set => SetProperty(ref _isSkipWorkedMode, value);
    }

    private bool _nextUTurnDirectionLeftOverride;
    /// <summary>
    /// When the user taps the U-turn direction toggle while no turn is currently
    /// armed, this flag captures the desired direction for the *next* armed turn.
    /// Mirrors legacy <c>FormGPS.SwapDirection</c> behavior of pre-flipping
    /// <c>yt.isTurnLeft</c> before the next trigger. The setter forwards to the
    /// cycle worker via <see cref="Services.Interfaces.IGpsPipelineService.SetNextUTurnDirectionLeftOverride"/>;
    /// the state machine consumes and clears the override on the next
    /// turn-creation tick.
    /// </summary>
    public bool NextUTurnDirectionLeftOverride
    {
        get => _nextUTurnDirectionLeftOverride;
        set
        {
            if (SetProperty(ref _nextUTurnDirectionLeftOverride, value))
            {
                _gpsPipelineService.SetNextUTurnDirectionLeftOverride(value);
            }
        }
    }

    /// <summary>
    /// True when the U-turn distance-to-trigger widget should be shown.
    /// Visible while YouTurn is enabled AND either:
    ///   - the state machine has populated a meaningful approach distance
    ///     (<c>DistanceToTrigger &gt; 0.5</c> m), OR
    ///   - the turn is already armed (<c>IsTriggered</c>) or executing.
    /// The original implementation gated only on <c>IsTriggered</c>, which
    /// becomes true at the same instant the turn starts executing — the user
    /// only ever saw the widget showing 0 m mid-turn. Showing during approach
    /// matches legacy AgOpenGPS UTurn-button behavior.
    /// </summary>
    public bool IsUTurnDistanceVisible
    {
        get
        {
            var yt = State.YouTurn;
            return yt.IsEnabled && (yt.DistanceToTrigger > 0.5 || yt.IsTriggered || yt.IsExecuting);
        }
    }

    /// <summary>
    /// Subscribe to <see cref="YouTurnState"/> property changes that affect
    /// <see cref="IsUTurnDistanceVisible"/> so the bound AXAML re-evaluates
    /// when the state machine snapshot lands.
    /// Called once from the MainViewModel constructor.
    /// </summary>
    private void WireYouTurnDistanceVisibility()
    {
        State.YouTurn.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is
                nameof(Models.State.YouTurnState.IsEnabled) or
                nameof(Models.State.YouTurnState.IsTriggered) or
                nameof(Models.State.YouTurnState.IsExecuting) or
                nameof(Models.State.YouTurnState.DistanceToTrigger))
            {
                OnPropertyChanged(nameof(IsUTurnDistanceVisible));
            }
        };
    }

    #endregion

    #region YouTurn Entry Points

    // Phase C C6/C7: YouTurn commands post intents and return. The cycle worker
    // drains them at the top of ProcessCycle and runs YouTurnStateMachine on
    // the cycle thread against its own POCO working state. The UI thread no
    // longer touches the state machine or the cycle's working state. Map
    // updates (SetYouTurnPath / SetNextTrack / SetIsInYouTurn) land via the
    // GpsCycleResult.YouTurn snapshot mirror in ApplyGpsCycleResult.

    /// <summary>Clear all U-turn state — called on field close or track deselect.</summary>
    public void ClearYouTurnState() => _intents.RequestClearYouTurn();

    public void TriggerManualYouTurnLeft() => _intents.RequestManualYouTurn(turnLeft: true);

    public void TriggerManualYouTurnRight() => _intents.RequestManualYouTurn(turnLeft: false);

    /// <summary>
    /// Toggle the U-turn direction. Mirrors legacy <c>FormGPS.SwapDirection</c>
    /// (AgOpen_Snapshot/GPS/Forms/GUI.Designer.cs:1426).
    ///
    /// Behavior:
    /// - Turn armed but not yet executing: flip <see cref="YouTurnState.IsTurnLeft"/>.
    /// - Turn currently executing: no-op (unsafe to flip mid-turn).
    /// - Idle (no armed turn): flip <see cref="NextUTurnDirectionLeftOverride"/>
    ///   so the next armed turn picks up the user's intent.
    /// </summary>
    public void ToggleUTurnDirection()
    {
        if (State.YouTurn.IsExecuting)
        {
            StatusMessage = "Cannot flip U-turn direction while executing";
            return;
        }

        if (State.YouTurn.IsTriggered)
        {
            State.YouTurn.IsTurnLeft = !State.YouTurn.IsTurnLeft;
            StatusMessage = State.YouTurn.IsTurnLeft ? "U-turn direction: Left" : "U-turn direction: Right";
            return;
        }

        NextUTurnDirectionLeftOverride = !NextUTurnDirectionLeftOverride;
        StatusMessage = NextUTurnDirectionLeftOverride
            ? "Next U-turn direction: Left"
            : "Next U-turn direction: Right";
    }

    #endregion

    #region VM utilities (used outside YouTurn too)

    /// <summary>
    /// Check if a track runs along the outer boundary (high percentage of points within
    /// <paramref name="threshold"/> meters). Tracks that do skip the boundary-disengage check
    /// on the first pass.
    /// </summary>
    private bool IsTrackOnBoundary(Track? track, double threshold = 5.0, double minOverlapPercent = 0.5)
    {
        if (track == null || track.Points.Count == 0) return false;
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid) return false;

        int pointsNearBoundary = 0;
        foreach (var point in track.Points)
        {
            if (DistanceToBoundary(point.Easting, point.Northing) < threshold)
                pointsNearBoundary++;
        }

        return (double)pointsNearBoundary / track.Points.Count >= minOverlapPercent;
    }

    private double DistanceToBoundary(double easting, double northing)
    {
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
            return double.MaxValue;

        var points = _currentBoundary.OuterBoundary.Points;
        double minDist = double.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % points.Count];
            double dist = GeometryMath.PointToSegmentDistance(
                easting, northing, p1.Easting, p1.Northing, p2.Easting, p2.Northing);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    #endregion
}
