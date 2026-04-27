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

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;

using CommunityToolkit.Mvvm.ComponentModel;

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
            if (SelectedTrack == null)
            {
                StatusMessage = "No track selected";
                return;
            }
            _intents.RequestGuidanceSnap(left: true);
            StatusMessage = "Snapped left";
        });

        SnapRightCommand = new RelayCommand(() =>
        {
            if (SelectedTrack == null)
            {
                StatusMessage = "No track selected";
                return;
            }
            _intents.RequestGuidanceSnap(left: false);
            StatusMessage = "Snapped right";
        });

        StopGuidanceCommand = new RelayCommand(() =>
        {
            StatusMessage = "Guidance Stopped";
        });

        UTurnCommand = new RelayCommand(() =>
        {
            if (SelectedTrack == null)
            {
                StatusMessage = "No track selected for U-turn";
                return;
            }

            if (!IsAutoSteerEngaged)
            {
                StatusMessage = "Enable autosteer before triggering U-turn";
                return;
            }

            if (!HasBoundary && !HasHeadland)
            {
                _logger.LogDebug("[UTurn] No boundary/headland, triggering manual U-turn left");
            }

            TriggerManualYouTurnLeft();
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

        DeleteAllTracksCommand = new RelayCommand(() =>
        {
            if (SavedTracks.Count == 0)
            {
                StatusMessage = "No tracks to delete";
                return;
            }
            ShowConfirmationDialog(
                "Delete All Tracks",
                $"Delete all {SavedTracks.Count} tracks? This cannot be undone.",
                () =>
                {
                    SavedTracks.Clear();
                    SelectedTrack = null;
                    SaveTracksToFile();
                    StatusMessage = "All tracks deleted";
                });
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
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DriveAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = "Drive-in AB Line: tap to set Point A at current position";
        });

        StartNewABCurveCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.Curve;
            _recordedCurvePoints.Clear();
            _lastCurvePoint = null;

            if (Easting != 0 || Northing != 0)
            {
                var headingRadians = Heading * Math.PI / 180.0;
                var firstPoint = new Vec3(Easting, Northing, headingRadians);
                _recordedCurvePoints.Add(firstPoint);
                _lastCurvePoint = firstPoint;

                var displayPoints = _recordedCurvePoints.Select(p => (p.Easting, p.Northing)).ToList();
                _mapService.SetRecordingPoints(displayPoints);
            }

            StatusMessage = $"Curve recording started ({_recordedCurvePoints.Count} pts) - drive along path, tap when done";
            OnPropertyChanged(nameof(IsRecordingCurve));
            OnPropertyChanged(nameof(RecordedCurvePointCount));
            OnPropertyChanged(nameof(ABCreationInstructions));
        });

        StartAPlusLineCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();

            if (Easting == 0 && Northing == 0)
            {
                StatusMessage = "No GPS position - cannot create A+ line";
                return;
            }

            double headingRad = Heading * Math.PI / 180.0;
            var pointA = new Vec3(Easting, Northing, headingRad);
            // Project Point B 100m ahead along current heading
            var pointB = new Vec3(
                Easting + Math.Sin(headingRad) * 100.0,
                Northing + Math.Cos(headingRad) * 100.0,
                headingRad);

            var track = Track.FromABLine($"A+ {DateTime.Now:HH:mm}", pointA, pointB);
            SavedTracks.Add(track);
            SelectedTrack = track;
            _mapService.SetActiveTrack(track);

            CurrentABCreationMode = ABCreationMode.None;
            StatusMessage = $"A+ line '{track.Name}' created at heading {Heading:F1}";
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
            if (SavedTracks.Count == 0)
            {
                StatusMessage = "No tracks to cycle";
                return;
            }

            int currentIndex = SelectedTrack != null ? SavedTracks.IndexOf(SelectedTrack) : -1;
            int nextIndex = (currentIndex + 1) % SavedTracks.Count;
            SelectedTrack = SavedTracks[nextIndex];
            StatusMessage = $"Active track: {SelectedTrack.Name}";
        });

        SmoothABLineCommand = new RelayCommand(() =>
        {
            if (SelectedTrack == null)
            {
                StatusMessage = "No track selected";
                return;
            }
            if (SelectedTrack.IsABLine)
            {
                StatusMessage = "Cannot smooth AB lines (only 2 points)";
                return;
            }
            if (SelectedTrack.Points.Count < 5)
            {
                StatusMessage = "Too few points to smooth (need at least 5)";
                return;
            }

            int beforeCount = SelectedTrack.Points.Count;
            var smoothed = Models.Guidance.CurveProcessing.SmoothWithCatmullRom(SelectedTrack.Points, 4);
            smoothed = Models.Guidance.CurveProcessing.CalculateHeadings(smoothed);
            SelectedTrack.Points = smoothed;

            // Invalidate guidance state so it recalculates from the new curve
            _trackGuidanceState = null;
            _mapService.SetActiveTrack(SelectedTrack);
            SaveTracksToFile();

            StatusMessage = $"Smoothed '{SelectedTrack.Name}': {beforeCount} -> {smoothed.Count} points";
        });

        // Nudge commands
        NudgeLeftCommand = new RelayCommand(() =>
        {
            NudgeTrack(-ConfigStore.AutoSteer.NudgeDistance * 0.01); // cm to m, negative = left
        });

        NudgeRightCommand = new RelayCommand(() =>
        {
            NudgeTrack(ConfigStore.AutoSteer.NudgeDistance * 0.01); // cm to m, positive = right
        });

        FineNudgeLeftCommand = new RelayCommand(() =>
        {
            NudgeTrack(-ConfigStore.AutoSteer.NudgeDistance * 0.0025); // 1/4 of standard nudge, left
        });

        FineNudgeRightCommand = new RelayCommand(() =>
        {
            NudgeTrack(ConfigStore.AutoSteer.NudgeDistance * 0.0025); // 1/4 of standard nudge, right
        });

        // Half-tool-width nudge (legacy FormNudge half-tool buttons)
        HalfToolNudgeLeftCommand = new RelayCommand(() =>
        {
            double halfWidth = (ConfigStore.ActualToolWidth - ConfigStore.Tool.Overlap) * 0.5;
            NudgeTrack(-halfWidth);
        });

        HalfToolNudgeRightCommand = new RelayCommand(() =>
        {
            double halfWidth = (ConfigStore.ActualToolWidth - ConfigStore.Tool.Overlap) * 0.5;
            NudgeTrack(halfWidth);
        });

        // Reset nudge to zero (legacy FormNudge zero button)
        ResetNudgeCommand = new RelayCommand(() =>
        {
            if (SelectedTrack == null) return;
            SelectedTrack.NudgeDistance = 0;
            _intents.RequestGuidanceResetNudge();
            StatusMessage = "Nudge reset to zero";
        });

        // Bottom Strip Commands - cycle through preset coverage colors
        ChangeMappingColorCommand = new RelayCommand(() =>
        {
            uint[] presets = new uint[]
            {
                0x98FB98, // Pale green (default)
                0x00CED1, // Dark turquoise
                0xFFD700, // Gold
                0xFF8C00, // Dark orange
                0xFF69B4, // Hot pink
                0x87CEEB, // Sky blue
                0xDDA0DD, // Plum
                0xF0E68C, // Khaki
            };

            var tool = ConfigStore.Tool;
            uint current = tool.SingleCoverageColor;

            // Find current index and cycle to next
            int idx = Array.IndexOf(presets, current);
            int next = (idx + 1) % presets.Length;
            tool.SingleCoverageColor = presets[next];

            // Extract RGB for status message
            byte r = (byte)((presets[next] >> 16) & 0xFF);
            byte g = (byte)((presets[next] >> 8) & 0xFF);
            byte b = (byte)(presets[next] & 0xFF);
            string[] names = { "Green", "Turquoise", "Gold", "Orange", "Pink", "Blue", "Plum", "Khaki" };
            StatusMessage = $"Coverage color: {names[next]}";
        });

        SnapToPivotCommand = new RelayCommand(() =>
        {
            if (SelectedTrack == null)
            {
                StatusMessage = "No track selected";
                return;
            }
            // Snap by nudging the track by the current cross-track error (XTE)
            // This aligns the guidance line to the vehicle's current position
            double xte = State.Guidance.CrossTrackError;
            if (Math.Abs(xte) < 0.001)
            {
                StatusMessage = "Already on track";
                return;
            }
            NudgeTrack(xte);
        });

        ToggleYouSkipCommand = new RelayCommand(() =>
        {
            IsSkipWorkedMode = !IsSkipWorkedMode;
            StatusMessage = IsSkipWorkedMode
                ? "Skip worked tracks: ON — will skip already-worked rows"
                : "Skip worked tracks: OFF — fixed skip pattern";
        });

        ToggleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            IsUTurnSkipRowsEnabled = !IsUTurnSkipRowsEnabled;
            IsSkipWorkedMode = IsUTurnSkipRowsEnabled;
            // Reset snake sequence so it rebuilds on next turn
            State.YouTurn.SnakeSequence = null;
            State.YouTurn.SnakeIndex = -1;
            StatusMessage = IsUTurnSkipRowsEnabled
                ? $"U-Turn skip rows: ON ({UTurnSkipRows} rows, snake pattern)"
                : "U-Turn skip rows: OFF";
        });

        CycleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            UTurnSkipRows = (UTurnSkipRows + 1) % 10;
            StatusMessage = $"Skip rows: {UTurnSkipRows}";
        });

        // Flags Commands
        PlaceRedFlagCommand = new RelayCommand(() => PlaceFlag(FlagColor.Red));
        PlaceGreenFlagCommand = new RelayCommand(() => PlaceFlag(FlagColor.Green));
        PlaceYellowFlagCommand = new RelayCommand(() => PlaceFlag(FlagColor.Yellow));

        PlaceFlagHereCommand = new RelayCommand(() => PlaceFlag(NextAutoColor()));

        DeleteAllFlagsCommand = new RelayCommand(() =>
        {
            if (Flags.Count == 0)
            {
                StatusMessage = "No flags to delete";
                return;
            }
            ShowConfirmationDialog(
                "Delete All Flags",
                $"Delete all {Flags.Count} flags? This cannot be undone.",
                () =>
                {
                    int count = Flags.Count;
                    Flags.Clear();
                    _nextFlagId = 1;
                    UpdateFlagsOnMap();
                    StatusMessage = $"Deleted {count} flags";
                });
        });

        DeleteFlagCommand = new RelayCommand<object>(param =>
        {
            if (param is Flag flag)
            {
                Flags.Remove(flag);
                UpdateFlagsOnMap();
                StatusMessage = $"Deleted flag '{flag.Name}'";
            }
        });

        ShowFlagListCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(Models.State.DialogType.FlagList);
        });

        CloseFlagListCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        PlaceFlagOnClickCommand = new RelayCommand(() =>
        {
            IsPlaceFlagOnClickMode = !IsPlaceFlagOnClickMode;
            StatusMessage = IsPlaceFlagOnClickMode
                ? "Tap on map to place a flag (tap again to cancel)"
                : "Flag placement cancelled";
            // Close any open dialog so the map is visible for tapping
            if (IsPlaceFlagOnClickMode)
                State.UI.CloseDialog();
        });

        // Section control commands
        ToggleManualModeCommand = new RelayCommand(() =>
        {
            IsManualSectionMode = !IsManualSectionMode;
            if (IsManualSectionMode)
                IsSectionMasterOn = false;

            var newState = IsManualSectionMode ? SectionButtonState.On : SectionButtonState.Off;
            _sectionControlService.SetAllSections(newState);

            StatusMessage = IsManualSectionMode ? "All sections ON" : "All sections OFF";
        });

        ToggleSectionMasterCommand = new RelayCommand(() =>
        {
            IsSectionMasterOn = !IsSectionMasterOn;
            if (IsSectionMasterOn)
                IsManualSectionMode = false;

            var newState = IsSectionMasterOn ? SectionButtonState.Auto : SectionButtonState.Off;
            _sectionControlService.SetAllSections(newState);

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
            SyncGuidanceStateToPipeline();
            StatusMessage = IsYouTurnEnabled ? "YouTurn enabled" : "YouTurn disabled";
        });

        ManualYouTurnLeftCommand = new RelayCommand(TriggerManualYouTurnLeft);
        ManualYouTurnRightCommand = new RelayCommand(TriggerManualYouTurnRight);

        ToggleAutoSteerCommand = new RelayCommand(() =>
        {
            // Disengage is always allowed — the user must be able to stop the
            // tractor even after the track/field has been cleared. Engagement
            // is the only path with preconditions.
            if (!IsAutoSteerEngaged && !IsAutoSteerAvailable)
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

                // Headland is only required when U-turns are enabled
                if (IsYouTurnEnabled && (!HasHeadland || _currentHeadlandLine == null || _currentHeadlandLine.Count < 3))
                {
                    ShowErrorDialog("Missing Headland",
                        "U-Turn guidance requires a headland boundary.\n\nPlease create a headland using the Headland button in the boundary panel, or disable U-turns.");
                    return;
                }
            }

            IsAutoSteerEngaged = !IsAutoSteerEngaged;
            _audioService.Play(IsAutoSteerEngaged
                ? Services.Interfaces.SoundEffect.AutoSteerOn
                : Services.Interfaces.SoundEffect.AutoSteerOff);
            if (IsAutoSteerEngaged)
            {
                double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
                _logger.LogDebug($"[NUDGE] AutoSteer ENGAGED: State.Guidance.HowManyPathsAway={State.Guidance.HowManyPathsAway}, offset={State.Guidance.HowManyPathsAway * widthMinusOverlap:F2}m");
            }
            SyncGuidanceStateToPipeline();
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
            // Phase D D6: seed pending zeros and sync — the cycle becomes the
            // writer of HowManyPathsAway / NudgeOffset (via SetActiveTrack in
            // SyncGuidanceStateToPipeline). State.Guidance gets zeroed on the
            // next snapshot mirror.
            _pendingInitialPathsAway = 0;
            _pendingInitialNudgeOffset = 0;
            SyncGuidanceStateToPipeline();
            foreach (var track in SavedTracks)
            {
                track.NudgeDistance = 0;
                track.ClearWorkedPaths();
            }
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

                    // Phase D D6: seed pending zeros; sync below carries them into
                    // _guidanceWorking and the next snapshot zeroes State.Guidance.
                    _logger.LogDebug("[NUDGE] Resetting pathsAway from {HowManyPathsAway} to 0, nudgeOffset from {NudgeOffset:F3} to 0", State.Guidance.HowManyPathsAway, State.Guidance.NudgeOffset);
                    _pendingInitialPathsAway = 0;
                    _pendingInitialNudgeOffset = 0;
                    SyncGuidanceStateToPipeline();

                    // Reset NudgeDistance on ALL tracks, not just selected
                    _logger.LogDebug("[NUDGE] Resetting NudgeDistance on {TrackCount} tracks", SavedTracks.Count);

                    // Verify SelectedTrack is in SavedTracks
                    if (SelectedTrack != null)
                    {
                        bool inCollection = SavedTracks.Contains(SelectedTrack);
                        _logger.LogDebug("[NUDGE] SelectedTrack '{TrackName}' in SavedTracks: {InCollection}", SelectedTrack.Name, inCollection);
                    }

                    foreach (var track in SavedTracks)
                    {
                        _logger.LogDebug($"[NUDGE] Track '{track.Name}' NudgeDistance: {track.NudgeDistance:F1} -> 0");
                        track.NudgeDistance = 0;
                    }
                    // Save tracks to persist the reset NudgeDistance
                    SaveTracksToFile();
                    _logger.LogDebug("[NUDGE] Saved tracks to file, State.Guidance.HowManyPathsAway is now {HowManyPathsAway}", State.Guidance.HowManyPathsAway);

                    RefreshCoverageStatistics();
                    StatusMessage = "Applied area deleted";
                });
        });

        // Tram line commands
        ToggleTramDisplayCommand = new RelayCommand(() =>
        {
            var tram = ConfigStore.Tram;

            // Cycle through modes like legacy: if only parallel lines, toggle on/off
            // Otherwise cycle Off -> All -> Lines -> Outer -> Off
            if (_tramLineService.ParallelTramLines.Count > 0 &&
                _tramLineService.OuterBoundaryTrack.Count == 0)
            {
                tram.DisplayMode = tram.DisplayMode != Models.Configuration.TramDisplayMode.Off
                    ? Models.Configuration.TramDisplayMode.Off
                    : Models.Configuration.TramDisplayMode.LinesOnly;
            }
            else
            {
                tram.DisplayMode = tram.DisplayMode switch
                {
                    Models.Configuration.TramDisplayMode.Off => Models.Configuration.TramDisplayMode.All,
                    Models.Configuration.TramDisplayMode.All => Models.Configuration.TramDisplayMode.LinesOnly,
                    Models.Configuration.TramDisplayMode.LinesOnly => Models.Configuration.TramDisplayMode.OuterOnly,
                    _ => Models.Configuration.TramDisplayMode.Off,
                };
            }

            ConfigStore.Guidance.TramDisplay = tram.DisplayMode != Models.Configuration.TramDisplayMode.Off;
            UpdateTramLines(SelectedTrack);
            OnPropertyChanged(nameof(TramDisplayIcon));
            OnPropertyChanged(nameof(TramDisplayLabel));
            StatusMessage = tram.DisplayMode switch
            {
                Models.Configuration.TramDisplayMode.Off => "Tram lines OFF",
                Models.Configuration.TramDisplayMode.All => "Tram lines: All",
                Models.Configuration.TramDisplayMode.LinesOnly => "Tram lines: Lines only",
                Models.Configuration.TramDisplayMode.OuterOnly => "Tram lines: Outer only",
                _ => "Tram lines"
            };
        });

        BuildTramLinesCommand = new RelayCommand(() =>
        {
            // Systems resolve their own references; only require selected track for legacy mode
            if (ConfigStore.Tram.Systems.Count == 0 &&
                (SelectedTrack == null || SelectedTrack.Points.Count < 2))
            {
                ShowErrorDialog("No Track Selected",
                    "Select an AB line or curve track before building tram lines.");
                return;
            }

            ConfigStore.Tram.DisplayMode = Models.Configuration.TramDisplayMode.All;
            ConfigStore.Guidance.TramDisplay = true;
            UpdateTramLines(SelectedTrack);
            OnPropertyChanged(nameof(TramDisplayIcon));
            OnPropertyChanged(nameof(TramDisplayLabel));
            StatusMessage = ConfigStore.Tram.Systems.Count > 0
                ? $"Tram lines built from {ConfigStore.Tram.Systems.Count} system(s)"
                : $"Tram lines built from '{SelectedTrack!.Name}'";
        });

        ShowTramSettingsCommand = new RelayCommand(() =>
        {
            if (SelectedTrack == null)
            {
                ShowErrorDialog("No Track Selected", "Select an AB line or curve track first.");
                return;
            }
            State.UI.ShowDialog(Models.State.DialogType.TramSettings);
        });

        CloseTramSettingsCommand = new RelayCommand(() => State.UI.CloseDialog());

        IncreaseTramPassesCommand = new RelayCommand(() =>
        {
            ConfigStore.Tram.Passes = Math.Min(20, ConfigStore.Tram.Passes + 1);
            ConfigStore.Guidance.TramPasses = ConfigStore.Tram.Passes;
            UpdateTramLines(SelectedTrack);
            OnPropertyChanged(nameof(TramPasses));
            OnPropertyChanged(nameof(TramWidthDisplay));
            OnPropertyChanged(nameof(TramLineCountDisplay));
        });

        DecreaseTramPassesCommand = new RelayCommand(() =>
        {
            ConfigStore.Tram.Passes = Math.Max(1, ConfigStore.Tram.Passes - 1);
            ConfigStore.Guidance.TramPasses = ConfigStore.Tram.Passes;
            UpdateTramLines(SelectedTrack);
            OnPropertyChanged(nameof(TramPasses));
            OnPropertyChanged(nameof(TramWidthDisplay));
            OnPropertyChanged(nameof(TramLineCountDisplay));
        });

        void SetTramMode(Models.Configuration.TramDisplayMode mode)
        {
            ConfigStore.Tram.DisplayMode = mode;
            ConfigStore.Guidance.TramDisplay = mode != Models.Configuration.TramDisplayMode.Off;
            UpdateTramLines(SelectedTrack);
            OnPropertyChanged(nameof(TramDisplayIcon));
            OnPropertyChanged(nameof(TramDisplayLabel));
        }

        SetTramModeOffCommand = new RelayCommand(() => SetTramMode(Models.Configuration.TramDisplayMode.Off));
        SetTramModeAllCommand = new RelayCommand(() => SetTramMode(Models.Configuration.TramDisplayMode.All));
        SetTramModeLinesCommand = new RelayCommand(() => SetTramMode(Models.Configuration.TramDisplayMode.LinesOnly));
        SetTramModeOuterCommand = new RelayCommand(() => SetTramMode(Models.Configuration.TramDisplayMode.OuterOnly));

        IncreaseTramStartPassCommand = new RelayCommand(() =>
        {
            ConfigStore.Tram.StartPass++;
            UpdateTramLines(SelectedTrack);
            OnPropertyChanged(nameof(TramStartPass));
            OnPropertyChanged(nameof(TramLineCountDisplay));
        });

        DecreaseTramStartPassCommand = new RelayCommand(() =>
        {
            ConfigStore.Tram.StartPass = Math.Max(0, ConfigStore.Tram.StartPass - 1);
            UpdateTramLines(SelectedTrack);
            OnPropertyChanged(nameof(TramStartPass));
            OnPropertyChanged(nameof(TramLineCountDisplay));
        });

        SwapTramSideCommand = new RelayCommand(() =>
        {
            ConfigStore.Tram.IsOuterInverted = !ConfigStore.Tram.IsOuterInverted;
            UpdateTramLines(SelectedTrack);
            StatusMessage = $"Tram side: {(ConfigStore.Tram.IsOuterInverted ? "Inverted" : "Normal")}";
        });

        ClearTramLinesCommand = new RelayCommand(() =>
        {
            ShowConfirmationDialog("Clear Tram Lines",
                "Delete all tram lines? This cannot be undone.",
                () =>
                {
                    _tramLineService.Clear();
                    ConfigStore.Tram.DisplayMode = Models.Configuration.TramDisplayMode.Off;
                    _mapService.SetTramLines(
                        _tramLineService.OuterBoundaryTrack,
                        _tramLineService.InnerBoundaryTrack,
                        _tramLineService.ParallelTramLines);
                    OnPropertyChanged(nameof(TramLineCountDisplay));
                    StatusMessage = "Tram lines cleared";
                });
        });

        IncreaseTramLineCommand = new RelayCommand(() =>
        {
            ConfigStore.Guidance.TramLine++;
            OnPropertyChanged(nameof(TramLineNumber));
        });

        DecreaseTramLineCommand = new RelayCommand(() =>
        {
            ConfigStore.Guidance.TramLine = Math.Max(1, ConfigStore.Guidance.TramLine - 1);
            OnPropertyChanged(nameof(TramLineNumber));
        });

        ToggleTramLeftManualCommand = new RelayCommand(() =>
        {
            _tramLineService.IsLeftManualOn = !_tramLineService.IsLeftManualOn;
            OnPropertyChanged(nameof(TramLeftManualOn));
        });

        ToggleTramRightManualCommand = new RelayCommand(() =>
        {
            _tramLineService.IsRightManualOn = !_tramLineService.IsRightManualOn;
            OnPropertyChanged(nameof(TramRightManualOn));
        });

        CreateTrackFromBoundaryCommand = new RelayCommand(() =>
        {
            var boundary = _currentBoundary?.OuterBoundary;
            if (boundary?.Points == null || boundary.Points.Count < 3)
            {
                ShowErrorDialog("No Boundary", "Load a field with a boundary first.");
                return;
            }

            // Find the longest edge of the boundary polygon
            var pts = boundary.Points;
            double maxDist = 0;
            int bestIdx = 0;

            for (int i = 0; i < pts.Count; i++)
            {
                int next = (i + 1) % pts.Count;
                double dx = pts[next].Easting - pts[i].Easting;
                double dy = pts[next].Northing - pts[i].Northing;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    bestIdx = i;
                }
            }

            var p1 = pts[bestIdx];
            var p2 = pts[(bestIdx + 1) % pts.Count];
            double heading = Math.Atan2(p2.Easting - p1.Easting, p2.Northing - p1.Northing);

            // Extend 50m past both ends for full field coverage
            var a = new Models.Base.Vec3(
                p1.Easting - Math.Sin(heading) * 50,
                p1.Northing - Math.Cos(heading) * 50,
                heading);
            var b = new Models.Base.Vec3(
                p2.Easting + Math.Sin(heading) * 50,
                p2.Northing + Math.Cos(heading) * 50,
                heading);

            var track = new Models.Track.Track
            {
                Name = $"Boundary Edge {bestIdx + 1}",
                Points = new System.Collections.Generic.List<Models.Base.Vec3> { a, b },
                Type = Models.Track.TrackType.ABLine,
                IsVisible = true
            };

            SavedTracks.Add(track);
            SelectedTrack = track;
            StatusMessage = $"Created AB line from longest boundary edge ({maxDist:F0}m)";
        });

        // A Line: create AB line from current position + heading
        CreateALineFromPositionCommand = new RelayCommand(() =>
        {
            double heading = State.Vehicle.Heading * Math.PI / 180.0;
            double e = Easting;
            double n = Northing;

            // Extend 200m in both directions from current position
            var a = new Models.Base.Vec3(
                e - Math.Sin(heading) * 200,
                n - Math.Cos(heading) * 200,
                heading);
            var b = new Models.Base.Vec3(
                e + Math.Sin(heading) * 200,
                n + Math.Cos(heading) * 200,
                heading);

            var track = new Models.Track.Track
            {
                Name = $"A+ {Math.Round(State.Vehicle.Heading, 1)}\u00B0",
                Points = new System.Collections.Generic.List<Models.Base.Vec3> { a, b },
                Type = Models.Track.TrackType.ABLine,
                IsVisible = true
            };

            SavedTracks.Add(track);
            SelectedTrack = track;
            StatusMessage = $"Created A+ line at {State.Vehicle.Heading:F0}\u00B0";
        });

        // Field Builder dialog
        ShowFieldBuilderCommand = new RelayCommand(() =>
            State.UI.ShowDialog(Models.State.DialogType.FieldBuilder));

        CloseFieldBuilderCommand = new RelayCommand(() =>
            State.UI.CloseDialog());

        IncreaseHeadlandDistanceCommand = new RelayCommand(() =>
        {
            HeadlandDistance = Math.Min(100, HeadlandDistance + 1.0);
            OnPropertyChanged(nameof(HeadlandDistance));
        });

        DecreaseHeadlandDistanceCommand = new RelayCommand(() =>
        {
            HeadlandDistance = Math.Max(1, HeadlandDistance - 1.0);
            OnPropertyChanged(nameof(HeadlandDistance));
        });

        CreateCurveFromBoundaryCommand = new RelayCommand(() =>
        {
            var boundary = _currentBoundary?.OuterBoundary;
            if (boundary?.Points == null || boundary.Points.Count < 3)
            {
                ShowErrorDialog("No Boundary", "Load a field with a boundary first.");
                return;
            }

            var pts = boundary.Points;
            var curvePoints = new System.Collections.Generic.List<Models.Base.Vec3>();
            for (int i = 0; i < pts.Count; i++)
            {
                curvePoints.Add(new Models.Base.Vec3(pts[i].Easting, pts[i].Northing, pts[i].Heading));
            }
            // Close the loop
            curvePoints.Add(new Models.Base.Vec3(pts[0].Easting, pts[0].Northing, pts[0].Heading));

            var track = new Models.Track.Track
            {
                Name = "Boundary Curve",
                Points = curvePoints,
                Type = Models.Track.TrackType.Curve,
                IsVisible = true
            };

            SavedTracks.Add(track);
            SelectedTrack = track;
            StatusMessage = $"Created boundary curve ({curvePoints.Count} points)";
        });

        CreateTracksFromAllEdgesCommand = new RelayCommand(() =>
        {
            var boundary = _currentBoundary?.OuterBoundary;
            if (boundary?.Points == null || boundary.Points.Count < 3)
            {
                ShowErrorDialog("No Boundary", "Load a field with a boundary first.");
                return;
            }

            var pts = boundary.Points;
            int created = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                int next = (i + 1) % pts.Count;
                double dx = pts[next].Easting - pts[i].Easting;
                double dy = pts[next].Northing - pts[i].Northing;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < 5.0) continue; // Skip tiny edges

                double heading = Math.Atan2(dx, dy);
                var a = new Models.Base.Vec3(
                    pts[i].Easting - Math.Sin(heading) * 50,
                    pts[i].Northing - Math.Cos(heading) * 50, heading);
                var b = new Models.Base.Vec3(
                    pts[next].Easting + Math.Sin(heading) * 50,
                    pts[next].Northing + Math.Cos(heading) * 50, heading);

                var track = new Models.Track.Track
                {
                    Name = $"Edge {i + 1} ({dist:F0}m)",
                    Points = new System.Collections.Generic.List<Models.Base.Vec3> { a, b },
                    Type = Models.Track.TrackType.ABLine,
                    IsVisible = true
                };
                SavedTracks.Add(track);
                created++;
            }

            if (created > 0)
                SelectedTrack = SavedTracks[SavedTracks.Count - 1];
            StatusMessage = $"Created {created} AB lines from boundary edges";
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
        });

        ZoomOutCommand = new RelayCommand(() =>
        {
            _mapService.Zoom(0.8);
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

    /// <summary>
    /// Nudge the current guidance line by a distance in meters.
    /// Positive = right, Negative = left (unadjusted — the cycle applies
    /// the heading-same-way sign flip).
    /// Phase D D5: posts an intent; the cycle drains and mutates
    /// _guidanceWorking.NudgeOffset on its own thread.
    /// </summary>
    private void NudgeTrack(double distanceMeters)
    {
        if (SelectedTrack == null)
        {
            StatusMessage = "No track selected";
            return;
        }

        _intents.RequestGuidanceNudge(distanceMeters);
        StatusMessage = $"Nudged {(distanceMeters > 0 ? "right" : "left")} {Math.Abs(distanceMeters * 100):F1}cm";
    }
}
