// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// State for recorded path recording and playback.
/// Mirrors the legacy AgOpenGPS CRecordedPath state machine.
/// </summary>
public class RecordedPathState : ObservableObject
{
    /// <summary>Full recorded points with speed and section state.</summary>
    public List<RecPathPoint> RecordedPoints { get; set; } = new();

    /// <summary>Dubins approach path generated for playback start.</summary>
    public List<Vec3> DubinsApproachPath { get; set; } = new();

    // -- Recording state --

    private bool _isRecordingOn;
    public bool IsRecordingOn
    {
        get => _isRecordingOn;
        set => SetProperty(ref _isRecordingOn, value);
    }

    // -- Playback state flags (mirrors legacy CRecordedPath) --

    private bool _isDrivingRecordedPath;
    public bool IsDrivingRecordedPath
    {
        get => _isDrivingRecordedPath;
        set => SetProperty(ref _isDrivingRecordedPath, value);
    }

    private bool _isFollowingDubinsToPath;
    public bool IsFollowingDubinsToPath
    {
        get => _isFollowingDubinsToPath;
        set => SetProperty(ref _isFollowingDubinsToPath, value);
    }

    private bool _isFollowingRecPath;
    public bool IsFollowingRecPath
    {
        get => _isFollowingRecPath;
        set => SetProperty(ref _isFollowingRecPath, value);
    }

    private bool _isEndOfLine;
    public bool IsEndOfLine
    {
        get => _isEndOfLine;
        set => SetProperty(ref _isEndOfLine, value);
    }

    // -- Resume mode: 0=Start, 1=Last, 2=Closest --

    private int _resumeState;
    public int ResumeState
    {
        get => _resumeState;
        set => SetProperty(ref _resumeState, value);
    }

    // -- Position tracking --

    public int CurrentPositionIndex { get; set; }
    public int StartPathIndex { get; set; }
    public Vec3 HomePosition { get; set; }

    public void Reset()
    {
        RecordedPoints.Clear();
        DubinsApproachPath.Clear();
        IsRecordingOn = false;
        IsDrivingRecordedPath = false;
        IsFollowingDubinsToPath = false;
        IsFollowingRecPath = false;
        IsEndOfLine = false;
        ResumeState = 0;
        CurrentPositionIndex = 0;
        StartPathIndex = 0;
        HomePosition = default;
    }
}
