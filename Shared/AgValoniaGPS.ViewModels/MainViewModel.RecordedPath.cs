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
    public ICommand? ShowRecordedPathDialogCommand { get; private set; }
    public ICommand? CloseRecordedPathDialogCommand { get; private set; }
    public ICommand? SetRecordedPathTabCommand { get; private set; }
    public ICommand? SaveNamedRecordedPathCommand { get; private set; }

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

    // Dialog tab state: 0 = Record, 1 = Playback
    private int _recordedPathTabIndex;
    public int RecordedPathTabIndex
    {
        get => _recordedPathTabIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _recordedPathTabIndex, value);
            this.RaisePropertyChanged(nameof(IsRecordTabActive));
            this.RaisePropertyChanged(nameof(IsPlaybackTabActive));
        }
    }
    public bool IsRecordTabActive => RecordedPathTabIndex == 0;
    public bool IsPlaybackTabActive => RecordedPathTabIndex == 1;

    // Name for saving recorded path
    private string _recordedPathName = "";
    public string RecordedPathName
    {
        get => _recordedPathName;
        set => this.RaiseAndSetIfChanged(ref _recordedPathName, value);
    }

    // True when a recording just finished and needs naming
    private bool _hasUnsavedRecordedPath;
    public bool HasUnsavedRecordedPath
    {
        get => _hasUnsavedRecordedPath;
        set => this.RaiseAndSetIfChanged(ref _hasUnsavedRecordedPath, value);
    }

    // Info text about loaded path
    private string _recordedPathInfo = "No path loaded";
    public string RecordedPathInfo
    {
        get => _recordedPathInfo;
        set => this.RaiseAndSetIfChanged(ref _recordedPathInfo, value);
    }

    // List of available .rec files for the picker
    public ObservableCollection<string> AvailableRecFiles { get; } = new();

    private string? _selectedRecFile;
    public string? SelectedRecFile
    {
        get => _selectedRecFile;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _selectedRecFile, value) != null && value != null)
                OnRecFileSelected(value);
        }
    }

    private void OnRecFileSelected(string fileName)
    {
        var activeField = _fieldService.ActiveField;
        if (activeField == null) return;

        var srcPath = Path.Combine(activeField.DirectoryPath, fileName);
        var dstPath = Path.Combine(activeField.DirectoryPath, "RecPath.txt");
        try
        {
            File.Copy(srcPath, dstPath, true);
            var points = Services.RecPathFileService.LoadRecPathPoints(activeField.DirectoryPath);
            if (points != null && points.Count >= 2)
            {
                State.RecordedPath.RecordedPoints = points;
                State.RecordedPath.CurrentPositionIndex = 0;
                UpdateRecordedPathDisplayOnMap();
                RecordedPathInfo = $"Selected: {fileName} ({points.Count} points)";
                RecalculateDubinsPreview();
            }
        }
        catch (Exception ex)
        {
            RecordedPathInfo = $"Failed to load: {ex.Message}";
        }
    }

    // Font weight converter for tab buttons (Bold for active tab)
    public static Avalonia.Data.Converters.FuncValueConverter<int, Avalonia.Media.FontWeight> TabFontWeightConverter { get; }
        = new(tabIndex =>
        {
            // ConverterParameter is passed as string, but this is the bound value
            // We'll handle this differently in XAML
            return Avalonia.Media.FontWeight.Normal;
        });

    private void InitializeRecordedPathCommands()
    {
        // Refresh rec panel when a field is loaded
        FieldFullyLoaded += _ =>
        {
            if (IsRecordedPathPanelVisible)
                LoadRecPathForPlayback();
        };

        ToggleRecordedPathPanelCommand = ReactiveCommand.Create(() =>
        {
            IsRecordedPathPanelVisible = !IsRecordedPathPanelVisible;
            if (IsRecordedPathPanelVisible)
                LoadRecPathForPlayback();
        });

        ShowRecordedPathDialogCommand = ReactiveCommand.Create(() =>
        {
            IsRecordedPathPanelVisible = true;
            LoadRecPathForPlayback();
        });

        CloseRecordedPathDialogCommand = ReactiveCommand.Create(() =>
        {
            IsRecordedPathPanelVisible = false;
        });

        SetRecordedPathTabCommand = ReactiveCommand.Create<string>(tab =>
        {
            if (int.TryParse(tab, out int idx))
                RecordedPathTabIndex = idx;
        });

        SaveNamedRecordedPathCommand = ReactiveCommand.Create(() =>
        {
            if (!HasUnsavedRecordedPath) return;
            var activeField = _fieldService.ActiveField;
            if (activeField == null) return;

            var name = string.IsNullOrWhiteSpace(RecordedPathName)
                ? $"RecPath_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
                : RecordedPathName.Trim();

            if (!name.EndsWith(".rec")) name += ".rec";

            try
            {
                Services.RecPathFileService.SaveRecPathToFile(
                    System.IO.Path.Combine(activeField.DirectoryPath, name),
                    State.RecordedPath.RecordedPoints);
                HasUnsavedRecordedPath = false;
                RecordedPathName = "";
                LoadRecPathForPlayback(); // Refresh file list
                StatusMessage = $"Saved: {name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
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
            RecalculateDubinsPreview();
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
            RecalculateDubinsPreview();
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
                SelectedRecFile = fileName;
                RecordedPathInfo = $"Selected: {fileName} ({State.RecordedPath.RecordedPoints.Count} points)";
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
        int startIdx = GetResumeStartIndex();

        recState.StartPathIndex = startIdx;

        // Generate Dubins approach path
        var goalPt = recState.RecordedPoints[startIdx];
        var goal = new Vec3(goalPt.Easting, goalPt.Northing, goalPt.Heading);

        // Bump current position forward 3m (matching legacy)
        double headingRad = State.Vehicle.Heading * Math.PI / 180.0;
        var start = new Vec3(
            State.Vehicle.Easting + 3.0 * Math.Sin(headingRad),
            State.Vehicle.Northing + 3.0 * Math.Cos(headingRad),
            headingRad);

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
            State.Vehicle.Northing, headingRad));

        recState.DubinsApproachPath = dubinsPath;
        _dubinsClosestIdx = 0;
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
        double vehicleH = State.Vehicle.Heading * Math.PI / 180.0; // Convert degrees to radians

        if (recState.IsFollowingDubinsToPath)
        {
            // Phase 1: Following Dubins approach path (user controls speed)
            UpdateDubinsApproach(vehicleE, vehicleN, vehicleH);
        }
        else if (recState.IsFollowingRecPath)
        {
            // Phase 2: Following recorded path (user controls speed)
            UpdateRecPathFollowing(vehicleE, vehicleN);
        }
    }

    private int _dubinsClosestIdx;

    private void UpdateDubinsApproach(double vehicleE, double vehicleN, double vehicleH)
    {
        var recState = State.RecordedPath;
        var dubinsPath = recState.DubinsApproachPath;
        if (dubinsPath.Count < 2) { StopDrivingRecordedPath(); return; }

        // Find closest point on Dubins path (search forward from last known position)
        int searchStart = Math.Max(0, _dubinsClosestIdx - 3);
        double closestDist = double.MaxValue;
        for (int i = searchStart; i < dubinsPath.Count; i++)
        {
            double dx = dubinsPath[i].Easting - vehicleE;
            double dy = dubinsPath[i].Northing - vehicleN;
            double d = dx * dx + dy * dy;
            if (d < closestDist) { closestDist = d; _dubinsClosestIdx = i; }
        }

        int remaining = dubinsPath.Count - _dubinsClosestIdx;

        // Transition when Dubins path is completed
        if (remaining < 3)
        {
            int nearestRecIdx = FindClosestPoint(recState.RecordedPoints, vehicleE, vehicleN);
            recState.IsFollowingDubinsToPath = false;
            recState.IsFollowingRecPath = true;
            recState.CurrentPositionIndex = nearestRecIdx;
            recState.DubinsApproachPath.Clear();
            _dubinsClosestIdx = 0;
            _mapService.SetYouTurnPath(null);
            StatusMessage = "Following recorded path...";
            return;
        }

        // Pure Pursuit along the Dubins path: lookahead 3-5 points ahead
        int lookAhead = Math.Min(5, dubinsPath.Count - _dubinsClosestIdx - 1);
        if (lookAhead < 1) lookAhead = 1;
        int lookIdx = _dubinsClosestIdx + lookAhead;
        var lookPt = dubinsPath[lookIdx];

        double dxLook = lookPt.Easting - vehicleE;
        double dyLook = lookPt.Northing - vehicleN;
        double lookDistSq = dxLook * dxLook + dyLook * dyLook;

        if (lookDistSq > 0.01)
        {
            // Transform to vehicle-local coordinates (vehicleH already in radians)
            double cosH = Math.Cos(vehicleH);
            double sinH = Math.Sin(vehicleH);
            double localX = dxLook * cosH - dyLook * sinH;  // lateral (right+)
            double localY = dxLook * sinH + dyLook * cosH;  // forward

            double wheelbase = Math.Max(ConfigurationStore.Instance.Vehicle.Wheelbase, 2.0);
            double steerAngle;
            if (localY > 0.1)
                steerAngle = Math.Atan2(2.0 * wheelbase * localX, lookDistSq);
            else
                steerAngle = localX > 0 ? 0.5 : -0.5;

            double maxSteer = ConfigurationStore.Instance.Vehicle.MaxSteerAngle * Math.PI / 180.0;
            steerAngle = Math.Clamp(steerAngle, -maxSteer, maxSteer);

            SimulatorSteerAngle = steerAngle * 180.0 / Math.PI;
        }
    }

    private void UpdateRecPathFollowing(double vehicleE, double vehicleN)
    {
        var recState = State.RecordedPath;
        var points = recState.RecordedPoints;
        if (points.Count < 2) { StopDrivingRecordedPath(); return; }

        // Find closest point - search entire path from current position onward
        // Wide search prevents losing track on tight curves
        int searchStart = Math.Max(0, recState.CurrentPositionIndex - 5);
        int closestIdx = searchStart;
        double closestDist = double.MaxValue;

        for (int i = searchStart; i < points.Count; i++)
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

        // Pure Pursuit guidance: shorter lookahead for tight curves
        int lookAhead = Math.Max(2, Math.Min(3, points.Count - closestIdx - 1));
        int lookIdx = Math.Min(closestIdx + lookAhead, points.Count - 1);
        var lookPt = points[lookIdx];
        double dxLook = lookPt.Easting - vehicleE;
        double dyLook = lookPt.Northing - vehicleN;
        double lookDistSq = dxLook * dxLook + dyLook * dyLook;
        double lookDist = Math.Sqrt(lookDistSq);

        if (lookDist > 0.1)
        {
            // Transform to vehicle-local coordinates
            double vehicleHRad = State.Vehicle.Heading * Math.PI / 180.0;
            double cosH = Math.Cos(vehicleHRad);
            double sinH = Math.Sin(vehicleHRad);
            double localX = dxLook * cosH - dyLook * sinH;  // lateral (right+)
            double localY = dxLook * sinH + dyLook * cosH;  // forward

            // Pure Pursuit formula: steer = atan(2 * wheelbase * localX / L^2)
            double wheelbase = Math.Max(ConfigurationStore.Instance.Vehicle.Wheelbase, 2.0);
            double steerAngle;
            if (localY > 0.1)
                steerAngle = Math.Atan2(2.0 * wheelbase * localX, lookDistSq);
            else
                steerAngle = localX > 0 ? 0.5 : -0.5; // goal behind, turn toward it

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
            RecordedPathInfo = $"Loaded: {points.Count} points";
            RecalculateDubinsPreview();
        }
        else
        {
            RecordedPathInfo = "No path loaded";
        }

        // Also refresh the .rec file list
        AvailableRecFiles.Clear();
        foreach (var f in Services.RecPathFileService.ListRecFiles(activeField.DirectoryPath))
            AvailableRecFiles.Add(f);
    }

    /// <summary>
    /// Calculate and display the Dubins approach path preview on the map.
    /// Does not start playback - just shows where the tractor will go.
    /// </summary>
    private void RecalculateDubinsPreview()
    {
        var recState = State.RecordedPath;
        if (recState.RecordedPoints.Count < 5)
        {
            _mapService.SetYouTurnPath(null);
            return;
        }

        // Determine start index based on resume mode
        int startIdx = GetResumeStartIndex();

        var goalPt = recState.RecordedPoints[startIdx];
        var goal = new Vec3(goalPt.Easting, goalPt.Northing, goalPt.Heading);

        double headingRad = State.Vehicle.Heading * Math.PI / 180.0;
        var start = new Vec3(
            State.Vehicle.Easting + 3.0 * Math.Sin(headingRad),
            State.Vehicle.Northing + 3.0 * Math.Cos(headingRad),
            headingRad);

        var dubins = new DubinsPathService(0.5);
        var youTurnRadius = ConfigurationStore.Instance.Guidance.UTurnRadius;
        dubins.TurningRadius = Math.Max(youTurnRadius * 1.2, 5.0);

        var dubinsPath = dubins.GeneratePath(start, goal);
        if (dubinsPath != null && dubinsPath.Count >= 2)
        {
            _mapService.SetYouTurnPath(dubinsPath.Select(p => (p.Easting, p.Northing)).ToList());
            var pts = State.RecordedPath.RecordedPoints.Count;
            RecordedPathInfo = $"{pts} points | Approach to idx {startIdx}";
        }
        else
        {
            _mapService.SetYouTurnPath(null);
        }
    }

    /// <summary>
    /// Get the path start index based on current resume mode.
    /// </summary>
    private int GetResumeStartIndex()
    {
        var recState = State.RecordedPath;
        switch (recState.ResumeState)
        {
            case 1: // Last position
                int lastIdx = recState.CurrentPositionIndex;
                return (lastIdx + 5 > recState.RecordedPoints.Count) ? 0 : lastIdx;
            case 2: // Closest point
                int closestIdx = FindClosestPoint(recState.RecordedPoints,
                    State.Vehicle.Easting, State.Vehicle.Northing);
                return Math.Min(closestIdx + 5, recState.RecordedPoints.Count - 1);
            default: // Start
                return 0;
        }
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
