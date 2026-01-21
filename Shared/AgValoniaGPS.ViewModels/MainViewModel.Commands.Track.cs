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
            StatusMessage = "Curve mode: Start driving to record curve path";
        });

        StartDrawABModeCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DrawAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = ABCreationInstructions;
        });

        SetABPointCommand = new RelayCommand<object?>(param =>
        {
            _logger.LogDebug($"[SetABPointCommand] Called with param={param?.GetType().Name ?? "null"}, Mode={CurrentABCreationMode}, Step={CurrentABPointStep}");

            if (CurrentABCreationMode == ABCreationMode.None)
            {
                _logger.LogDebug("[SetABPointCommand] Mode is None, returning");
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
                    newTrack.IsActive = true;

                    SavedTracks.Add(newTrack);
                    SaveTracksToFile();
                    HasActiveTrack = true;
                    IsAutoSteerAvailable = true;
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
            CurrentABCreationMode = ABCreationMode.None;
            CurrentABPointStep = ABPointStep.None;
            PendingPointA = null;
            StatusMessage = "AB line creation cancelled";
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
}
