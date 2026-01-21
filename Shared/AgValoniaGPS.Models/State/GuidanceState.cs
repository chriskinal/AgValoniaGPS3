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

using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Guidance calculation state - cross-track error, steering, goal points.
/// Updated by guidance service every frame when guidance is active.
/// </summary>
public class GuidanceState : ReactiveObject
{
    // Active track (unified Track model)
    private Track.Track? _activeTrack;
    public Track.Track? ActiveTrack
    {
        get => _activeTrack;
        set => this.RaiseAndSetIfChanged(ref _activeTrack, value);
    }

    private bool _isGuidanceActive;
    public bool IsGuidanceActive
    {
        get => _isGuidanceActive;
        set => this.RaiseAndSetIfChanged(ref _isGuidanceActive, value);
    }

    // Cross-track error (meters, positive = right of line)
    private double _crossTrackError;
    public double CrossTrackError
    {
        get => _crossTrackError;
        set => this.RaiseAndSetIfChanged(ref _crossTrackError, value);
    }

    private double _headingError;
    public double HeadingError
    {
        get => _headingError;
        set => this.RaiseAndSetIfChanged(ref _headingError, value);
    }

    // Steering output (degrees)
    private double _steerAngle;
    public double SteerAngle
    {
        get => _steerAngle;
        set => this.RaiseAndSetIfChanged(ref _steerAngle, value);
    }

    // Raw values for UDP transmission
    private short _steerAngleRaw;
    public short SteerAngleRaw
    {
        get => _steerAngleRaw;
        set => this.RaiseAndSetIfChanged(ref _steerAngleRaw, value);
    }

    private short _distanceOffRaw; // mm
    public short DistanceOffRaw
    {
        get => _distanceOffRaw;
        set => this.RaiseAndSetIfChanged(ref _distanceOffRaw, value);
    }

    // Pure Pursuit state (persisted between frames)
    private double _ppIntegral;
    public double PpIntegral
    {
        get => _ppIntegral;
        set => this.RaiseAndSetIfChanged(ref _ppIntegral, value);
    }

    private double _ppPivotDistanceError;
    public double PpPivotDistanceError
    {
        get => _ppPivotDistanceError;
        set => this.RaiseAndSetIfChanged(ref _ppPivotDistanceError, value);
    }

    private double _ppPivotDistanceErrorLast;
    public double PpPivotDistanceErrorLast
    {
        get => _ppPivotDistanceErrorLast;
        set => this.RaiseAndSetIfChanged(ref _ppPivotDistanceErrorLast, value);
    }

    private int _ppCounter;
    public int PpCounter
    {
        get => _ppCounter;
        set => this.RaiseAndSetIfChanged(ref _ppCounter, value);
    }

    // Visualization points
    private Vec2 _goalPoint;
    public Vec2 GoalPoint
    {
        get => _goalPoint;
        set => this.RaiseAndSetIfChanged(ref _goalPoint, value);
    }

    private Vec2 _radiusPoint;
    public Vec2 RadiusPoint
    {
        get => _radiusPoint;
        set => this.RaiseAndSetIfChanged(ref _radiusPoint, value);
    }

    private double _purePursuitRadius;
    public double PurePursuitRadius
    {
        get => _purePursuitRadius;
        set => this.RaiseAndSetIfChanged(ref _purePursuitRadius, value);
    }

    // Direction relative to track
    private bool _isHeadingSameWay;
    public bool IsHeadingSameWay
    {
        get => _isHeadingSameWay;
        set => this.RaiseAndSetIfChanged(ref _isHeadingSameWay, value);
    }

    private bool _isReverse;
    public bool IsReverse
    {
        get => _isReverse;
        set => this.RaiseAndSetIfChanged(ref _isReverse, value);
    }

    // Line offset (how many passes from original)
    private int _howManyPathsAway;
    public int HowManyPathsAway
    {
        get => _howManyPathsAway;
        set => this.RaiseAndSetIfChanged(ref _howManyPathsAway, value);
    }

    private string _currentLineLabel = "1L";
    public string CurrentLineLabel
    {
        get => _currentLineLabel;
        set => this.RaiseAndSetIfChanged(ref _currentLineLabel, value);
    }

    // Contour mode
    private bool _isContourMode;
    public bool IsContourMode
    {
        get => _isContourMode;
        set => this.RaiseAndSetIfChanged(ref _isContourMode, value);
    }

    public void Reset()
    {
        ActiveTrack = null;
        IsGuidanceActive = false;
        CrossTrackError = HeadingError = SteerAngle = 0;
        SteerAngleRaw = DistanceOffRaw = 0;
        PpIntegral = PpPivotDistanceError = PpPivotDistanceErrorLast = 0;
        PpCounter = 0;
        GoalPoint = new Vec2();
        RadiusPoint = new Vec2();
        PurePursuitRadius = 0;
        IsHeadingSameWay = true;
        IsReverse = false;
        HowManyPathsAway = 0;
        CurrentLineLabel = "1L";
        IsContourMode = false;
    }

    /// <summary>
    /// Update from TrackGuidanceOutput
    /// </summary>
    public void UpdateFromGuidance(TrackGuidanceOutput output)
    {
        CrossTrackError = output.CrossTrackError;
        SteerAngle = output.SteerAngle;
        SteerAngleRaw = output.GuidanceLineSteerAngle;
        DistanceOffRaw = output.GuidanceLineDistanceOff;
        GoalPoint = output.GoalPoint;
        RadiusPoint = output.RadiusPoint;
        PurePursuitRadius = output.PurePursuitRadius;
    }
}
