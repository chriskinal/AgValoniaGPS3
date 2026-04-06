// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.PathPlanning;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Recorded path playback: Dubins approach + Pure Pursuit following.
/// Mirrors legacy AgOpenGPS CRecordedPath state machine.
/// </summary>
public partial class MainViewModel
{
    // -- Recorded Path Playback Commands --

    public ICommand? PlayRecordedPathCommand { get; private set; }
    public ICommand? StopPlaybackCommand { get; private set; }
    public ICommand? CycleResumeModeCommand { get; private set; }
    public ICommand? ToggleRecordedPathPanelCommand { get; private set; }
    public ICommand? ReverseRecordedPathCommand { get; private set; }
    public ICommand? PickRecordedPathCommand { get; private set; }
    public ICommand? DeleteRecordedPathCommand { get; private set; }
    public ICommand? TurnOffRecordedPathCommand { get; private set; }

    private bool _isRecordedPathPanelVisible;
    public bool IsRecordedPathPanelVisible
    {
        get => _isRecordedPathPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isRecordedPathPanelVisible, value);
    }

    private string _resumeModeLabel = "Start";
    public string ResumeModeLabel
    {
        get => _resumeModeLabel;
        set => this.RaiseAndSetIfChanged(ref _resumeModeLabel, value);
    }

    // List of available .rec files for the picker
    public ObservableCollection<string> AvailableRecFiles { get; } = new();

    private string? _selectedRecFile;
    public string? SelectedRecFile
    {
        get => _selectedRecFile;
        set => this.RaiseAndSetIfChanged(ref _selectedRecFile, value);
    }

    private void InitializeRecordedPathCommands()
    {
        ToggleRecordedPathPanelCommand = ReactiveCommand.Create(() =>
        {
            IsRecordedPathPanelVisible = !IsRecordedPathPanelVisible;
            if (IsRecordedPathPanelVisible)
            {
                // Auto-load RecPath.txt when opening panel
                LoadRecPathForPlayback();
            }
        });

        PlayRecordedPathCommand = ReactiveCommand.Create(() =>
        {
            var recState = State.RecordedPath;
            if (recState.IsDrivingRecordedPath)
            {
                StopDrivingRecordedPath();
                return;
            }

            if (!StartDrivingRecordedPath())
            {
                StatusMessage = "Cannot start playback (need at least 5 points)";
            }
        });

        StopPlaybackCommand = ReactiveCommand.Create(() =>
        {
            StopDrivingRecordedPath();
        });

        CycleResumeModeCommand = ReactiveCommand.Create(() =>
        {
            var recState = State.RecordedPath;
            recState.ResumeState = (recState.ResumeState + 1) % 3;
            ResumeModeLabel = recState.ResumeState switch
            {
                0 => "Start",
                1 => "Last",
                2 => "Closest",
                _ => "Start"
            };
            StatusMessage = $"Resume mode: {ResumeModeLabel}";
        });

        ReverseRecordedPathCommand = ReactiveCommand.Create(() =>
        {
            var recState = State.RecordedPath;
            if (recState.RecordedPoints.Count < 2) return;
            if (recState.IsDrivingRecordedPath) return;

            recState.RecordedPoints = ReverseRecordedPath(recState.RecordedPoints);
            recState.CurrentPositionIndex = 0;
            UpdateRecordedPathDisplayOnMap();
            StatusMessage = "Path reversed";
        });

        PickRecordedPathCommand = ReactiveCommand.Create<string>(fileName =>
        {
            if (string.IsNullOrEmpty(fileName)) return;
            var activeField = _fieldService.ActiveField;
            if (activeField == null) return;

            // Copy selected .rec to RecPath.txt and load
            var srcPath = Path.Combine(activeField.DirectoryPath, fileName);
            var dstPath = Path.Combine(activeField.DirectoryPath, "RecPath.txt");
            try
            {
                File.Copy(srcPath, dstPath, true);
                LoadRecPathForPlayback();
                State.UI.CloseDialog();
                StatusMessage = $"Loaded: {fileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load: {ex.Message}";
            }
        });

        DeleteRecordedPathCommand = ReactiveCommand.Create<string>(fileName =>
        {
            if (string.IsNullOrEmpty(fileName)) return;
            var activeField = _fieldService.ActiveField;
            if (activeField == null) return;

            if (Services.RecPathFileService.DeleteRecFile(activeField.DirectoryPath, fileName))
            {
                AvailableRecFiles.Remove(fileName);
                StatusMessage = $"Deleted: {fileName}";
            }
        });

        TurnOffRecordedPathCommand = ReactiveCommand.Create(() =>
        {
            StopDrivingRecordedPath();
            State.RecordedPath.RecordedPoints.Clear();
            IsRecordedPathPanelVisible = false;
            UpdateRecordedPathDisplayOnMap();
            StatusMessage = "Recorded path cleared";
        });
    }

    // -- Playback Engine --

    /// <summary>
    /// Start driving recorded path. Returns false if not enough points.
    /// </summary>
    private bool StartDrivingRecordedPath()
    {
        var recState = State.RecordedPath;
        if (recState.RecordedPoints.Count < 5) return false;

        // Save home position
        recState.HomePosition = new Vec3(
            State.Vehicle.Easting, State.Vehicle.Northing,
            State.Vehicle.Heading);

        // Determine start index based on resume mode
        int startIdx;
        switch (recState.ResumeState)
        {
            case 1: // Last position
                startIdx = recState.CurrentPositionIndex;
                if (startIdx + 5 > recState.RecordedPoints.Count)
                    startIdx = 0;
                break;
            case 2: // Closest point
                startIdx = FindClosestPoint(recState.RecordedPoints,
                    State.Vehicle.Easting, State.Vehicle.Northing);
                startIdx = Math.Min(startIdx + 5, recState.RecordedPoints.Count - 1);
                break;
            default: // Start
                startIdx = 0;
                break;
        }

        recState.StartPathIndex = startIdx;

        // Generate Dubins approach path
        var goalPt = recState.RecordedPoints[startIdx];
        var goal = new Vec3(goalPt.Easting, goalPt.Northing, goalPt.Heading);

        // Bump current position forward 3m (matching legacy)
        double heading = State.Vehicle.Heading;
        var start = new Vec3(
            State.Vehicle.Easting + 3.0 * Math.Sin(heading),
            State.Vehicle.Northing + 3.0 * Math.Cos(heading),
            heading);

        var dubins = new DubinsPathService(0.5);
        var youTurnRadius = ConfigurationStore.Instance.Guidance.UTurnRadius;
        dubins.TurningRadius = Math.Max(youTurnRadius * 1.2, 5.0);

        var dubinsPath = dubins.GeneratePath(start, goal);
        if (dubinsPath == null || dubinsPath.Count < 2)
        {
            _logger.LogDebug("[RecPath] Dubins approach path generation failed");
            return false;
        }

        // Insert current position at front
        dubinsPath.Insert(0, new Vec3(State.Vehicle.Easting,
            State.Vehicle.Northing, heading));

        recState.DubinsApproachPath = dubinsPath;
        recState.IsFollowingDubinsToPath = true;
        recState.IsFollowingRecPath = false;
        recState.IsEndOfLine = false;
        recState.IsDrivingRecordedPath = true;
        recState.CurrentPositionIndex = startIdx;

        // Set Dubins approach path on map
        _mapService.SetYouTurnPath(dubinsPath.Select(p =>
            (p.Easting, p.Northing)).ToList());

        StatusMessage = "Driving to recorded path start...";
        return true;
    }

    /// <summary>
    /// Stop playback and clear all playback state.
    /// </summary>
    private void StopDrivingRecordedPath()
    {
        var recState = State.RecordedPath;
        recState.IsDrivingRecordedPath = false;
        recState.IsFollowingDubinsToPath = false;
        recState.IsFollowingRecPath = false;
        recState.IsEndOfLine = false;
        recState.DubinsApproachPath.Clear();

        // Clear approach path from map
        _mapService.SetYouTurnPath(null);

        StatusMessage = "Playback stopped";
    }

    /// <summary>
    /// Called every GPS fix during playback. Handles Dubins approach and path following.
    /// </summary>
    internal void UpdateRecordedPathPlayback()
    {
        var recState = State.RecordedPath;
        if (!recState.IsDrivingRecordedPath) return;

        double vehicleE = State.Vehicle.Easting;
        double vehicleN = State.Vehicle.Northing;
        double vehicleH = State.Vehicle.Heading;

        if (recState.IsFollowingDubinsToPath)
        {
            // Phase 1: Following Dubins approach path
            UpdateDubinsApproach(vehicleE, vehicleN, vehicleH);
        }
        else if (recState.IsFollowingRecPath)
        {
            // Phase 2: Following recorded path
            UpdateRecPathFollowing(vehicleE, vehicleN);
        }
    }

    private void UpdateDubinsApproach(double vehicleE, double vehicleN, double vehicleH)
    {
        var recState = State.RecordedPath;
        var dubinsPath = recState.DubinsApproachPath;
        if (dubinsPath.Count < 2) { StopDrivingRecordedPath(); return; }

        // Find closest point on Dubins path
        int closestIdx = 0;
        double closestDist = double.MaxValue;
        for (int i = 0; i < dubinsPath.Count; i++)
        {
            double dx = dubinsPath[i].Easting - vehicleE;
            double dy = dubinsPath[i].Northing - vehicleN;
            double d = dx * dx + dy * dy;
            if (d < closestDist) { closestDist = d; closestIdx = i; }
        }

        int remaining = dubinsPath.Count - closestIdx;

        // Check if close enough to path start to transition
        var goalPt = recState.RecordedPoints[recState.StartPathIndex];
        double dxGoal = goalPt.Easting - vehicleE;
        double dyGoal = goalPt.Northing - vehicleN;
        double distToGoal = dxGoal * dxGoal + dyGoal * dyGoal;

        if (remaining < 8 && distToGoal < 4.0) // 2m squared
        {
            // Transition to recorded path following
            recState.IsFollowingDubinsToPath = false;
            recState.IsFollowingRecPath = true;
            recState.DubinsApproachPath.Clear();
            _mapService.SetYouTurnPath(null);

            StatusMessage = "Following recorded path...";
            return;
        }

        // Use Pure Pursuit on the Dubins path via YouTurnGuidanceService
        // Calculate steering from approach path (lookahead-based)
        if (closestIdx + 1 < dubinsPath.Count)
        {
            // Lookahead: pick a point ~5m ahead
            int lookIdx = Math.Min(closestIdx + 10, dubinsPath.Count - 1);
            var lookPt = dubinsPath[lookIdx];

            double dx = lookPt.Easting - vehicleE;
            double dy = lookPt.Northing - vehicleN;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist > 0.1)
            {
                double targetHeading = Math.Atan2(dx, dy);
                double headingError = targetHeading - vehicleH;
                // Normalize to -PI..PI
                while (headingError > Math.PI) headingError -= 2 * Math.PI;
                while (headingError < -Math.PI) headingError += 2 * Math.PI;

                // Simple proportional steering for approach
                double steerAngle = headingError * 2.0;
                double maxSteer = ConfigurationStore.Instance.Vehicle.MaxSteerAngle * Math.PI / 180.0;
                steerAngle = Math.Clamp(steerAngle, -maxSteer, maxSteer);

                // Apply to autosteer if engaged
                if (IsAutoSteerEngaged)
                {
                    SimulatorSteerAngle = steerAngle * 180.0 / Math.PI;
                }
            }
        }
    }

    private void UpdateRecPathFollowing(double vehicleE, double vehicleN)
    {
        var recState = State.RecordedPath;
        var points = recState.RecordedPoints;
        if (points.Count < 2) { StopDrivingRecordedPath(); return; }

        // Find closest point in local range [currentIndex, currentIndex+10]
        int searchStart = recState.CurrentPositionIndex;
        int searchEnd = Math.Min(searchStart + 10, points.Count);
        int closestIdx = searchStart;
        double closestDist = double.MaxValue;

        for (int i = searchStart; i < searchEnd; i++)
        {
            double dx = points[i].Easting - vehicleE;
            double dy = points[i].Northing - vehicleN;
            double d = dx * dx + dy * dy;
            if (d < closestDist) { closestDist = d; closestIdx = i; }
        }

        recState.CurrentPositionIndex = closestIdx;

        // Check end of path
        if (closestIdx >= points.Count - 2)
        {
            recState.IsEndOfLine = true;
            StopDrivingRecordedPath();
            StatusMessage = "Recorded path complete";
            return;
        }

        // Section control replay: match recorded section state
        if (closestIdx < points.Count)
        {
            bool recordedAutoState = points[closestIdx].AutoBtnState;
            if (IsSectionMasterOn != recordedAutoState)
            {
                ToggleSectionMasterCommand?.Execute(null);
            }
        }

        // Pure Pursuit guidance using lookahead
        int lookIdx = Math.Min(closestIdx + 5, points.Count - 1);
        var lookPt = points[lookIdx];
        double dxLook = lookPt.Easting - vehicleE;
        double dyLook = lookPt.Northing - vehicleN;
        double dist = Math.Sqrt(dxLook * dxLook + dyLook * dyLook);

        if (dist > 0.1 && IsAutoSteerEngaged)
        {
            double targetHeading = Math.Atan2(dxLook, dyLook);
            double vehicleH = State.Vehicle.Heading;
            double headingError = targetHeading - vehicleH;
            while (headingError > Math.PI) headingError -= 2 * Math.PI;
            while (headingError < -Math.PI) headingError += 2 * Math.PI;

            double steerAngle = headingError * 2.0;
            double maxSteer = ConfigurationStore.Instance.Vehicle.MaxSteerAngle * Math.PI / 180.0;
            steerAngle = Math.Clamp(steerAngle, -maxSteer, maxSteer);

            SimulatorSteerAngle = steerAngle * 180.0 / Math.PI;
        }

        // Update display guidance values (cross-track error)
        if (closestIdx + 1 < points.Count)
        {
            var ptA = points[closestIdx];
            var ptB = points[Math.Min(closestIdx + 1, points.Count - 1)];
            double segDx = ptB.Easting - ptA.Easting;
            double segDy = ptB.Northing - ptA.Northing;
            double segLen = Math.Sqrt(segDx * segDx + segDy * segDy);
            if (segLen > 0.01)
            {
                // Perpendicular distance from vehicle to segment
                double xte = ((vehicleE - ptA.Easting) * segDy - (vehicleN - ptA.Northing) * segDx) / segLen;
                CrossTrackError = xte;
            }
        }
    }

    // -- Helpers --

    private void LoadRecPathForPlayback()
    {
        var activeField = _fieldService.ActiveField;
        if (activeField == null) return;

        var points = Services.RecPathFileService.LoadRecPathPoints(activeField.DirectoryPath);
        if (points != null && points.Count >= 2)
        {
            State.RecordedPath.RecordedPoints = points;
            State.RecordedPath.CurrentPositionIndex = 0;
            UpdateRecordedPathDisplayOnMap();
            StatusMessage = $"Recorded path loaded ({points.Count} points)";
        }
        else
        {
            StatusMessage = "No recorded path found";
        }

        // Also refresh the .rec file list
        AvailableRecFiles.Clear();
        foreach (var f in Services.RecPathFileService.ListRecFiles(activeField.DirectoryPath))
            AvailableRecFiles.Add(f);
    }

    private void UpdateRecordedPathDisplayOnMap()
    {
        var recState = State.RecordedPath;
        if (recState.RecordedPoints.Count < 2) return;

        var vec3List = recState.RecordedPoints.Select(p =>
            new Vec3(p.Easting, p.Northing, p.Heading)).ToList();
        var track = Track.FromRecordedPath("Playback Path", vec3List);
        _mapService.SetRecordedPaths(new[] { track });
    }

    private static int FindClosestPoint(List<RecPathPoint> points, double easting, double northing)
    {
        int closestIdx = 0;
        double closestDist = double.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            double dx = points[i].Easting - easting;
            double dy = points[i].Northing - northing;
            double d = dx * dx + dy * dy;
            if (d < closestDist) { closestDist = d; closestIdx = i; }
        }

        return closestIdx;
    }

    private static List<RecPathPoint> ReverseRecordedPath(List<RecPathPoint> points)
    {
        var reversed = new List<RecPathPoint>(points.Count);
        for (int i = points.Count - 1; i >= 0; i--)
        {
            var pt = points[i];
            double newHeading = pt.Heading + Math.PI;
            if (newHeading > Math.PI * 2) newHeading -= Math.PI * 2;
            if (newHeading < 0) newHeading += Math.PI * 2;
            reversed.Add(new RecPathPoint(pt.Easting, pt.Northing, newHeading, pt.Speed, pt.AutoBtnState));
        }
        return reversed;
    }
}
