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

using System;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Track management commands - AB lines, curves, guidance control, flags.
/// </summary>
public partial class MainViewModel
{
    private void InitializeTrackCommands()
    {
        // AB Line Guidance Commands - Bottom Bar
        SnapLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Left Track - not yet implemented";
        });

        SnapRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Right Track - not yet implemented";
        });

        StopGuidanceCommand = new RelayCommand(() =>
        {
            StatusMessage = "Guidance Stopped";
        });

        UTurnCommand = new RelayCommand(() =>
        {
            StatusMessage = "U-Turn - not yet implemented";
        });

        // AB Line Guidance Commands - Flyout Menu
        ShowTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.Tracks);
        });

        CloseTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // Track management commands
        DeleteSelectedTrackCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null)
            {
                SavedTracks.Remove(SelectedTrack);
                SelectedTrack = null;
                SaveTracksToFile();
                StatusMessage = "Track deleted";
            }
        });

        SwapABPointsCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null && SelectedTrack.Points.Count >= 2)
            {
                SelectedTrack.Points.Reverse();
                StatusMessage = $"Swapped A/B points for {SelectedTrack.Name}";
            }
        });

        SelectTrackAsActiveCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null)
            {
                if (SelectedTrack.IsActive)
                {
                    SelectedTrack = null;
                    StatusMessage = "Track deactivated";
                }
                else
                {
                    StatusMessage = $"Activated track: {SelectedTrack.Name}";
                }
                State.UI.CloseDialog();
            }
        });

        // Quick AB Selector
        ShowQuickABSelectorCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.QuickABSelector);
        });

        CloseQuickABSelectorCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowDrawABDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.DrawAB);
        });

        CloseDrawABDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        StartNewABLineCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Line - not yet implemented";
        });

        StartNewABCurveCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Curve - not yet implemented";
        });

        StartAPlusLineCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            StatusMessage = "A+ Line mode: Line created from current position and heading";
        });

        StartDriveABCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DriveAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = ABCreationInstructions;
        });

        StartCurveRecordingCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.Curve;
            _recordedCurvePoints.Clear();
            _lastCurvePoint = null;

            // Capture first point immediately at current position
            if (Easting != 0 || Northing != 0)
            {
                var headingRadians = Heading * Math.PI / 180.0;
                var firstPoint = new Vec3(Easting, Northing, headingRadians);
                _recordedCurvePoints.Add(firstPoint);
                _lastCurvePoint = firstPoint;

                // Show first point on map
                var displayPoints = _recordedCurvePoints.Select(p => (p.Easting, p.Northing)).ToList();
                _mapService.SetRecordingPoints(displayPoints);
            }

            StatusMessage = $"Curve recording started ({_recordedCurvePoints.Count} pts) - drive along path, tap when done";
            OnPropertyChanged(nameof(IsRecordingCurve));
            OnPropertyChanged(nameof(RecordedCurvePointCount));
            OnPropertyChanged(nameof(ABCreationInstructions));
        });

        FinishCurveRecordingCommand = new RelayCommand(() =>
        {
            if (CurrentABCreationMode != ABCreationMode.Curve)
            {
                return;
            }

            // Need at least 3 points for a valid curve
            if (_recordedCurvePoints.Count < 3)
            {
                StatusMessage = $"Need at least 3 points for a curve (have {_recordedCurvePoints.Count})";
                return;
            }

            // Deactivate all existing tracks before adding the new one
            foreach (var existingTrack in SavedTracks)
            {
                existingTrack.IsActive = false;
            }

            // Extend curve ends past boundary for U-turn detection
            var extendedPoints = ExtendCurvePastBoundary(_recordedCurvePoints);

            // Create the curve track
            var newTrack = Track.FromCurve(
                $"Curve {DateTime.Now:HH:mm:ss}",
                extendedPoints,
                isClosed: false);

            // Add track and select it as active (SelectedTrack setter handles IsActive and map update)
            SavedTracks.Add(newTrack);
            SelectedTrack = newTrack;
            SaveTracksToFile();

            StatusMessage = $"Created curve with {_recordedCurvePoints.Count} points: {newTrack.Name}";
            _logger.LogDebug($"[Curve] Created curve track: {newTrack.Name} with {_recordedCurvePoints.Count} points");

            // Clear recording display from map
            _mapService.ClearRecordingPoints();

            // Reset state
            CurrentABCreationMode = ABCreationMode.None;
            _recordedCurvePoints.Clear();
            _lastCurvePoint = null;
            OnPropertyChanged(nameof(IsRecordingCurve));
            OnPropertyChanged(nameof(RecordedCurvePointCount));
        });

        StartDrawABModeCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DrawAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = ABCreationInstructions;
        });

        StartDrawCurveModeCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DrawCurve;
            _drawnCurvePoints.Clear();
            StatusMessage = ABCreationInstructions;
            OnPropertyChanged(nameof(IsDrawingCurve));
            OnPropertyChanged(nameof(DrawnCurvePointCount));
            OnPropertyChanged(nameof(ABCreationInstructions));
        });

        FinishDrawCurveCommand = new RelayCommand(() =>
        {
            if (CurrentABCreationMode != ABCreationMode.DrawCurve)
            {
                return;
            }

            // Need at least 2 points for a valid track
            if (_drawnCurvePoints.Count < 2)
            {
                StatusMessage = $"Need at least 2 points (have {_drawnCurvePoints.Count})";
                return;
            }

            // Clear drawing display from map
            _mapService.ClearRecordingPoints();

            // Deactivate all existing tracks before adding the new one
            foreach (var existingTrack in SavedTracks)
            {
                existingTrack.IsActive = false;
            }

            Track newTrack;

            // If only 2 points, create a straight AB line
            if (_drawnCurvePoints.Count == 2)
            {
                var (extendedA, extendedB) = ExtendABLinePastBoundary(_drawnCurvePoints[0], _drawnCurvePoints[1]);
                newTrack = Track.FromABLine(
                    $"AB_{extendedA.Heading * 180.0 / Math.PI:F1} {DateTime.Now:HH:mm:ss}",
                    extendedA,
                    extendedB);
                StatusMessage = $"Created AB line: {newTrack.Name}";
                _logger.LogDebug($"[DrawCurve] Created AB line from 2 points: {newTrack.Name}");
            }
            else
            {
                // 3+ points - smooth the curve using Catmull-Rom spline, then extend past boundary
                var smoothedPoints = Models.Guidance.CurveProcessing.SmoothWithCatmullRom(_drawnCurvePoints, pointsPerSegment: 10);
                smoothedPoints = Models.Guidance.CurveProcessing.CalculateHeadings(smoothedPoints);
                var extendedPoints = ExtendCurvePastBoundary(smoothedPoints);
                newTrack = Track.FromCurve(
                    $"DrawnCurve {DateTime.Now:HH:mm:ss}",
                    extendedPoints,
                    isClosed: false);
                StatusMessage = $"Created smooth curve from {_drawnCurvePoints.Count} control points: {newTrack.Name}";
                _logger.LogDebug($"[DrawCurve] Created smooth curve track: {newTrack.Name} from {_drawnCurvePoints.Count} control points → {extendedPoints.Count} smoothed points");
            }

            // Add track and select it as active (SelectedTrack setter handles IsActive and map update)
            SavedTracks.Add(newTrack);
            SelectedTrack = newTrack;
            SaveTracksToFile();

            // Reset state
            CurrentABCreationMode = ABCreationMode.None;
            _drawnCurvePoints.Clear();
            OnPropertyChanged(nameof(IsDrawingCurve));
            OnPropertyChanged(nameof(DrawnCurvePointCount));
        });

        UndoLastDrawnPointCommand = new RelayCommand(() =>
        {
            if (CurrentABCreationMode != ABCreationMode.DrawCurve || _drawnCurvePoints.Count == 0)
            {
                return;
            }

            _drawnCurvePoints.RemoveAt(_drawnCurvePoints.Count - 1);

            // Update map display
            if (_drawnCurvePoints.Count > 0)
            {
                var displayPoints = _drawnCurvePoints.Select(p => (p.Easting, p.Northing)).ToList();
                _mapService.SetRecordingPoints(displayPoints);
            }
            else
            {
                _mapService.ClearRecordingPoints();
            }

            OnPropertyChanged(nameof(DrawnCurvePointCount));
            OnPropertyChanged(nameof(ABCreationInstructions));
            StatusMessage = $"Removed last point ({_drawnCurvePoints.Count} points remaining)";
        });

        SetABPointCommand = new RelayCommand<object?>(param =>
        {
            _logger.LogDebug($"[SetABPointCommand] Called with param={param?.GetType().Name ?? "null"}, Mode={CurrentABCreationMode}, Step={CurrentABPointStep}");

            if (CurrentABCreationMode == ABCreationMode.None)
            {
                _logger.LogDebug("[SetABPointCommand] Mode is None, returning");
                return;
            }

            // Handle curve mode - tap to finish recording
            if (CurrentABCreationMode == ABCreationMode.Curve)
            {
                _logger.LogDebug($"[SetABPointCommand] Curve mode - finishing with {_recordedCurvePoints.Count} points");
                FinishCurveRecordingCommand?.Execute(null);
                return;
            }

            // Handle draw curve mode - tap to add points
            if (CurrentABCreationMode == ABCreationMode.DrawCurve && param is Position curveMapPos)
            {
                // Calculate heading from previous point (or use 0 for first point)
                double heading = 0;
                if (_drawnCurvePoints.Count > 0)
                {
                    var lastPt = _drawnCurvePoints[^1];
                    heading = Math.Atan2(curveMapPos.Easting - lastPt.Easting, curveMapPos.Northing - lastPt.Northing);
                }

                var point = new Vec3(curveMapPos.Easting, curveMapPos.Northing, heading);
                _drawnCurvePoints.Add(point);

                // Update map display
                var displayPoints = _drawnCurvePoints.Select(p => (p.Easting, p.Northing)).ToList();
                _mapService.SetRecordingPoints(displayPoints);

                OnPropertyChanged(nameof(DrawnCurvePointCount));
                OnPropertyChanged(nameof(ABCreationInstructions));
                StatusMessage = $"Added point {_drawnCurvePoints.Count} - tap more points or Finish";
                _logger.LogDebug($"[SetABPointCommand] DrawCurve - Added point {_drawnCurvePoints.Count}: E={curveMapPos.Easting:F2}, N={curveMapPos.Northing:F2}");
                return;
            }

            Position pointToSet;

            if (CurrentABCreationMode == ABCreationMode.DriveAB)
            {
                pointToSet = new Position
                {
                    Latitude = Latitude,
                    Longitude = Longitude,
                    Easting = Easting,
                    Northing = Northing,
                    Heading = Heading
                };
                _logger.LogDebug($"[SetABPointCommand] DriveAB - GPS position: E={Easting:F2}, N={Northing:F2}");
            }
            else if (CurrentABCreationMode == ABCreationMode.DrawAB && param is Position mapPos)
            {
                pointToSet = mapPos;
                _logger.LogDebug($"[SetABPointCommand] DrawAB - Map position: E={mapPos.Easting:F2}, N={mapPos.Northing:F2}");
            }
            else
            {
                _logger.LogDebug($"[SetABPointCommand] Invalid state - returning");
                return;
            }

            if (CurrentABPointStep == ABPointStep.SettingPointA)
            {
                PendingPointA = pointToSet;
                CurrentABPointStep = ABPointStep.SettingPointB;
                StatusMessage = ABCreationInstructions;
                _logger.LogDebug($"[SetABPointCommand] Set Point A: E={pointToSet.Easting:F2}, N={pointToSet.Northing:F2}");
            }
            else if (CurrentABPointStep == ABPointStep.SettingPointB)
            {
                if (PendingPointA != null)
                {
                    // Deactivate all existing tracks before adding the new one
                    foreach (var existingTrack in SavedTracks)
                    {
                        existingTrack.IsActive = false;
                    }

                    var heading = CalculateHeading(PendingPointA, pointToSet);
                    var headingRadians = heading * Math.PI / 180.0;

                    // Extend AB Line points past boundary for proper U-turn detection
                    var (extendedA, extendedB) = ExtendABLinePastBoundary(
                        new Vec3(PendingPointA.Easting, PendingPointA.Northing, headingRadians),
                        new Vec3(pointToSet.Easting, pointToSet.Northing, headingRadians));

                    var newTrack = Track.FromABLine(
                        $"AB_{heading:F1} {DateTime.Now:HH:mm:ss}",
                        extendedA,
                        extendedB);

                    // Add track and select it as active (SelectedTrack setter handles IsActive and map update)
                    SavedTracks.Add(newTrack);
                    SelectedTrack = newTrack;
                    SaveTracksToFile();
                    StatusMessage = $"Created AB line: {newTrack.Name} ({heading:F1})";
                    _logger.LogDebug($"[SetABPointCommand] Created AB Line: {newTrack.Name}");

                    CurrentABCreationMode = ABCreationMode.None;
                    CurrentABPointStep = ABPointStep.None;
                    PendingPointA = null;
                }
            }
        });

        CancelABCreationCommand = new RelayCommand(() =>
        {
            // Clean up curve recording state if active
            if (CurrentABCreationMode == ABCreationMode.Curve)
            {
                _mapService.ClearRecordingPoints(); // Clear recording display from map
                _recordedCurvePoints.Clear();
                _lastCurvePoint = null;
                OnPropertyChanged(nameof(IsRecordingCurve));
                OnPropertyChanged(nameof(RecordedCurvePointCount));
            }

            // Clean up draw curve state if active
            if (CurrentABCreationMode == ABCreationMode.DrawCurve)
            {
                _mapService.ClearRecordingPoints(); // Clear drawing display from map
                _drawnCurvePoints.Clear();
                OnPropertyChanged(nameof(IsDrawingCurve));
                OnPropertyChanged(nameof(DrawnCurvePointCount));
            }

            CurrentABCreationMode = ABCreationMode.None;
            CurrentABPointStep = ABPointStep.None;
            PendingPointA = null;
            StatusMessage = "AB line/curve creation cancelled";
        });

        CycleABLinesCommand = new RelayCommand(() =>
        {
            StatusMessage = "Cycle AB Lines - not yet implemented";
        });

        SmoothABLineCommand = new RelayCommand(() =>
        {
            StatusMessage = "Smooth AB Line - not yet implemented";
        });

        // Nudge commands
        NudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Nudge Left - not yet implemented";
        });

        NudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Nudge Right - not yet implemented";
        });

        FineNudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Fine Nudge Left - not yet implemented";
        });

        FineNudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Fine Nudge Right - not yet implemented";
        });

        // Bottom Strip Commands
        ChangeMappingColorCommand = new RelayCommand(() =>
        {
            StatusMessage = "Section Mapping Color - not yet implemented";
        });

        SnapToPivotCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Pivot - not yet implemented";
        });

        ToggleYouSkipCommand = new RelayCommand(() =>
        {
            StatusMessage = "YouSkip Toggle - not yet implemented";
        });

        ToggleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            IsUTurnSkipRowsEnabled = !IsUTurnSkipRowsEnabled;
            StatusMessage = IsUTurnSkipRowsEnabled
                ? $"U-Turn skip rows: ON ({UTurnSkipRows} rows)"
                : "U-Turn skip rows: OFF";
        });

        CycleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            UTurnSkipRows = (UTurnSkipRows + 1) % 10;
            StatusMessage = $"Skip rows: {UTurnSkipRows}";
        });

        // Flags Commands
        PlaceRedFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Red Flag - not yet implemented";
        });

        PlaceGreenFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Green Flag - not yet implemented";
        });

        PlaceYellowFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Yellow Flag - not yet implemented";
        });

        DeleteAllFlagsCommand = new RelayCommand(() =>
        {
            StatusMessage = "Delete All Flags - not yet implemented";
        });

        // Section control commands
        ToggleManualModeCommand = new RelayCommand(() =>
        {
            IsManualSectionMode = !IsManualSectionMode;
            if (IsManualSectionMode)
                IsSectionMasterOn = false;

            var newState = IsManualSectionMode ? SectionButtonState.On : SectionButtonState.Off;
            for (int i = 0; i < _sectionControlService.NumSections; i++)
            {
                _sectionControlService.SetSectionState(i, newState);
            }

            StatusMessage = IsManualSectionMode ? "All sections ON" : "All sections OFF";
        });

        ToggleSectionMasterCommand = new RelayCommand(() =>
        {
            IsSectionMasterOn = !IsSectionMasterOn;
            if (IsSectionMasterOn)
                IsManualSectionMode = false;

            var newState = IsSectionMasterOn ? SectionButtonState.Auto : SectionButtonState.Off;
            for (int i = 0; i < _sectionControlService.NumSections; i++)
            {
                _sectionControlService.SetSectionState(i, newState);
            }

            StatusMessage = IsSectionMasterOn ? "All sections AUTO" : "All sections OFF";
        });

        ToggleSectionCommand = new RelayCommand<object>(param =>
        {
            if (param == null) return;

            int sectionIndex;
            if (param is int intVal)
                sectionIndex = intVal;
            else if (param is string strVal && int.TryParse(strVal, out var parsed))
                sectionIndex = parsed;
            else
                return;

            if (sectionIndex < 0 || sectionIndex >= _sectionControlService.NumSections)
                return;

            var currentState = _sectionControlService.SectionStates[sectionIndex].ButtonState;
            var newState = currentState switch
            {
                SectionButtonState.Off => SectionButtonState.Auto,
                SectionButtonState.Auto => SectionButtonState.On,
                SectionButtonState.On => SectionButtonState.Off,
                _ => SectionButtonState.Off
            };

            _sectionControlService.SetSectionState(sectionIndex, newState);
            StatusMessage = $"Section {sectionIndex + 1}: {newState}";
        });

        ToggleYouTurnCommand = new RelayCommand(() =>
        {
            IsYouTurnEnabled = !IsYouTurnEnabled;
            StatusMessage = IsYouTurnEnabled ? "YouTurn enabled" : "YouTurn disabled";
        });

        ManualYouTurnLeftCommand = new RelayCommand(TriggerManualYouTurnLeft);
        ManualYouTurnRightCommand = new RelayCommand(TriggerManualYouTurnRight);

        ToggleAutoSteerCommand = new RelayCommand(() =>
        {
            if (!IsAutoSteerAvailable)
            {
                StatusMessage = "AutoSteer not available - no active track";
                return;
            }

            // If trying to engage, validate boundaries
            if (!IsAutoSteerEngaged)
            {
                // Check for outer boundary
                if (!HasBoundary || _currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
                {
                    ShowErrorDialog("Missing Boundary",
                        "AutoSteer requires an outer boundary.\n\nPlease create or load a field boundary before engaging autosteer.");
                    return;
                }

                // Check for headland
                if (!HasHeadland || _currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
                {
                    ShowErrorDialog("Missing Headland",
                        "AutoSteer requires a headland boundary for U-turn detection.\n\nPlease create a headland using the Headland button in the boundary panel.");
                    return;
                }
            }

            IsAutoSteerEngaged = !IsAutoSteerEngaged;
            if (IsAutoSteerEngaged)
            {
                double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
                Console.WriteLine($"[NUDGE] AutoSteer ENGAGED: _howManyPathsAway={_howManyPathsAway}, offset={_howManyPathsAway * widthMinusOverlap:F2}m");
            }
            StatusMessage = IsAutoSteerEngaged ? "AutoSteer ENGAGED" : "AutoSteer disengaged";
        });

        // Contour commands
        ToggleContourModeCommand = new RelayCommand(() =>
        {
            IsContourModeOn = !IsContourModeOn;
            StatusMessage = IsContourModeOn ? "Contour mode ON" : "Contour mode OFF";
        });

        DeleteContoursCommand = new RelayCommand(() =>
        {
            _coverageMapService.ClearAll();
            // Reset track guidance state to force global search for nearest segment
            _trackGuidanceState = null;
            // Reset pass counter and track offset on ALL tracks
            _howManyPathsAway = 0;
            foreach (var track in SavedTracks)
                track.NudgeDistance = 0;
            SaveTracksToFile();
            StatusMessage = "Coverage/contours cleared";
        });

        DeleteAppliedAreaCommand = new RelayCommand(() =>
        {
            ShowConfirmationDialog(
                "Delete Applied Area",
                "Are you sure you want to delete all applied area coverage? This cannot be undone.",
                () =>
                {
                    _coverageMapService.ClearAll();

                    if (State.Field.ActiveField != null)
                    {
                        var sectionsFile = System.IO.Path.Combine(State.Field.ActiveField.DirectoryPath, "Sections.txt");
                        if (System.IO.File.Exists(sectionsFile))
                        {
                            try
                            {
                                System.IO.File.Delete(sectionsFile);
                                _logger.LogDebug($"[Coverage] Deleted {sectionsFile}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug($"[Coverage] Error deleting Sections.txt: {ex.Message}");
                            }
                        }
                    }

                    // Reset track guidance state to force global search for nearest segment
                    // Otherwise it will continue from where coverage ended
                    _trackGuidanceState = null;

                    // Reset pass counter and track offset to go back to the original track
                    Console.WriteLine($"[NUDGE] Resetting _howManyPathsAway from {_howManyPathsAway} to 0");
                    _howManyPathsAway = 0;

                    // Reset NudgeDistance on ALL tracks, not just selected
                    Console.WriteLine($"[NUDGE] Resetting NudgeDistance on {SavedTracks.Count} tracks");

                    // Verify SelectedTrack is in SavedTracks
                    if (SelectedTrack != null)
                    {
                        bool inCollection = SavedTracks.Contains(SelectedTrack);
                        Console.WriteLine($"[NUDGE] SelectedTrack '{SelectedTrack.Name}' in SavedTracks: {inCollection}");
                    }

                    foreach (var track in SavedTracks)
                    {
                        Console.WriteLine($"[NUDGE] Track '{track.Name}' NudgeDistance: {track.NudgeDistance:F1} -> 0");
                        track.NudgeDistance = 0;
                    }
                    // Save tracks to persist the reset NudgeDistance
                    SaveTracksToFile();
                    Console.WriteLine($"[NUDGE] Saved tracks to file, _howManyPathsAway is now {_howManyPathsAway}");

                    RefreshCoverageStatistics();
                    StatusMessage = "Applied area deleted";
                });
        });

        // Map zoom commands
        Toggle3DModeCommand = new RelayCommand(() =>
        {
            _mapService.Toggle3DMode();
            Is2DMode = !_mapService.Is3DMode;
        });

        ZoomInCommand = new RelayCommand(() =>
        {
            _mapService.Zoom(1.2);
            ZoomInRequested?.Invoke();
        });

        ZoomOutCommand = new RelayCommand(() =>
        {
            _mapService.Zoom(0.8);
            ZoomOutRequested?.Invoke();
        });
    }

    /// <summary>
    /// Extend AB Line points so they pass the outer boundary by a margin.
    /// This ensures headland raycast will find an intersection for U-turn detection.
    /// </summary>
    /// <param name="pointA">Original point A</param>
    /// <param name="pointB">Original point B</param>
    /// <param name="marginMeters">How far past the boundary to extend (default 10m)</param>
    /// <returns>Tuple of extended (pointA, pointB)</returns>
    private (Vec3 extendedA, Vec3 extendedB) ExtendABLinePastBoundary(Vec3 pointA, Vec3 pointB, double marginMeters = 20.0)
    {
        double heading = Math.Atan2(pointB.Easting - pointA.Easting, pointB.Northing - pointA.Northing);
        double sinH = Math.Sin(heading);
        double cosH = Math.Cos(heading);

        double extendA = marginMeters;
        double extendB = marginMeters;

        if (_currentBoundary?.OuterBoundary != null && _currentBoundary.OuterBoundary.IsValid)
        {
            var boundaryPts = _currentBoundary.OuterBoundary.Points;
            int count = boundaryPts.Count;

            // Raycast from pointA backwards to find boundary intersection
            for (int i = 0; i < count; i++)
            {
                var p1 = boundaryPts[i];
                var p2 = boundaryPts[(i + 1) % count];

                // Line segment intersection using parametric form
                double dx = -sinH; // backwards direction
                double dy = -cosH;
                double ex = p2.Easting - p1.Easting;
                double ey = p2.Northing - p1.Northing;

                double denom = dx * ey - dy * ex;
                if (Math.Abs(denom) < 0.0001) continue;

                double t = ((p1.Easting - pointA.Easting) * ey - (p1.Northing - pointA.Northing) * ex) / denom;
                double u = ((p1.Easting - pointA.Easting) * dy - (p1.Northing - pointA.Northing) * dx) / denom;

                if (t > 0 && u >= 0 && u <= 1)
                    extendA = Math.Max(extendA, t + marginMeters);
            }

            // Raycast from pointB forwards to find boundary intersection
            for (int i = 0; i < count; i++)
            {
                var p1 = boundaryPts[i];
                var p2 = boundaryPts[(i + 1) % count];

                double dx = sinH; // forwards direction
                double dy = cosH;
                double ex = p2.Easting - p1.Easting;
                double ey = p2.Northing - p1.Northing;

                double denom = dx * ey - dy * ex;
                if (Math.Abs(denom) < 0.0001) continue;

                double t = ((p1.Easting - pointB.Easting) * ey - (p1.Northing - pointB.Northing) * ex) / denom;
                double u = ((p1.Easting - pointB.Easting) * dy - (p1.Northing - pointB.Northing) * dx) / denom;

                if (t > 0 && u >= 0 && u <= 1)
                    extendB = Math.Max(extendB, t + marginMeters);
            }
        }

        var extendedA = new Vec3(
            pointA.Easting - sinH * extendA,
            pointA.Northing - cosH * extendA,
            heading);

        var extendedB = new Vec3(
            pointB.Easting + sinH * extendB,
            pointB.Northing + cosH * extendB,
            heading);

        _logger.LogDebug($"[ABLine] Extended A by {extendA:F1}m, B by {extendB:F1}m");

        return (extendedA, extendedB);
    }

    /// <summary>
    /// Extend curve endpoints so they pass the outer boundary by a margin.
    /// This ensures headland raycast will find an intersection for U-turn detection.
    /// </summary>
    /// <param name="points">Original curve points</param>
    /// <param name="marginMeters">How far past the boundary to extend (default 20m)</param>
    /// <returns>New list with extended endpoints</returns>
    private List<Vec3> ExtendCurvePastBoundary(List<Vec3> points, double marginMeters = 20.0)
    {
        if (points.Count < 2)
        {
            return new List<Vec3>(points);
        }

        var result = new List<Vec3>(points);

        // Get headings at curve ends
        var firstPoint = points[0];
        var secondPoint = points[1];
        var lastPoint = points[^1];
        var secondLastPoint = points[^2];

        // Heading at start (backwards from first segment)
        double startHeading = Math.Atan2(secondPoint.Easting - firstPoint.Easting,
                                          secondPoint.Northing - firstPoint.Northing);
        // Heading at end (forwards along last segment)
        double endHeading = Math.Atan2(lastPoint.Easting - secondLastPoint.Easting,
                                        lastPoint.Northing - secondLastPoint.Northing);

        double extendStart = marginMeters;
        double extendEnd = marginMeters;

        if (_currentBoundary?.OuterBoundary != null && _currentBoundary.OuterBoundary.IsValid)
        {
            var boundaryPts = _currentBoundary.OuterBoundary.Points;
            int count = boundaryPts.Count;

            // Raycast from first point backwards to find boundary intersection
            double sinStart = Math.Sin(startHeading);
            double cosStart = Math.Cos(startHeading);
            for (int i = 0; i < count; i++)
            {
                var p1 = boundaryPts[i];
                var p2 = boundaryPts[(i + 1) % count];

                double dx = -sinStart; // backwards direction
                double dy = -cosStart;
                double ex = p2.Easting - p1.Easting;
                double ey = p2.Northing - p1.Northing;

                double denom = dx * ey - dy * ex;
                if (Math.Abs(denom) < 0.0001) continue;

                double t = ((p1.Easting - firstPoint.Easting) * ey - (p1.Northing - firstPoint.Northing) * ex) / denom;
                double u = ((p1.Easting - firstPoint.Easting) * dy - (p1.Northing - firstPoint.Northing) * dx) / denom;

                if (t > 0 && u >= 0 && u <= 1)
                    extendStart = Math.Max(extendStart, t + marginMeters);
            }

            // Raycast from last point forwards to find boundary intersection
            double sinEnd = Math.Sin(endHeading);
            double cosEnd = Math.Cos(endHeading);
            for (int i = 0; i < count; i++)
            {
                var p1 = boundaryPts[i];
                var p2 = boundaryPts[(i + 1) % count];

                double dx = sinEnd; // forwards direction
                double dy = cosEnd;
                double ex = p2.Easting - p1.Easting;
                double ey = p2.Northing - p1.Northing;

                double denom = dx * ey - dy * ex;
                if (Math.Abs(denom) < 0.0001) continue;

                double t = ((p1.Easting - lastPoint.Easting) * ey - (p1.Northing - lastPoint.Northing) * ex) / denom;
                double u = ((p1.Easting - lastPoint.Easting) * dy - (p1.Northing - lastPoint.Northing) * dx) / denom;

                if (t > 0 && u >= 0 && u <= 1)
                    extendEnd = Math.Max(extendEnd, t + marginMeters);
            }
        }

        // Create extended start point
        double sinStart2 = Math.Sin(startHeading);
        double cosStart2 = Math.Cos(startHeading);
        var extendedStart = new Vec3(
            firstPoint.Easting - sinStart2 * extendStart,
            firstPoint.Northing - cosStart2 * extendStart,
            startHeading);

        // Create extended end point
        double sinEnd2 = Math.Sin(endHeading);
        double cosEnd2 = Math.Cos(endHeading);
        var extendedEnd = new Vec3(
            lastPoint.Easting + sinEnd2 * extendEnd,
            lastPoint.Northing + cosEnd2 * extendEnd,
            endHeading);

        // Insert extended start at beginning, replace first point
        result[0] = extendedStart;
        // Append extended end, replace last point
        result[^1] = extendedEnd;

        _logger.LogDebug($"[Curve] Extended start by {extendStart:F1}m, end by {extendEnd:F1}m");

        return result;
    }
}
