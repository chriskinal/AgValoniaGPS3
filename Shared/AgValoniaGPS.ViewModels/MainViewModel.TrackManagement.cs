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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ReactiveUI;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Track management features: recorded path display, track import,
/// contour recording/deletion, contour guidance.
/// </summary>
public partial class MainViewModel
{
    #region Contour Recording State

    private readonly List<Vec3> _contourRecordingPoints = new();
    private Vec3? _lastContourPoint;
    private const double ContourMinPointSpacing = 1.0; // Minimum 1m between contour points

    #endregion

    #region Track Management Command Initialization

    private void InitializeTrackManagementCommands()
    {
        ToggleRecordedPathsCommand = ReactiveCommand.Create(() =>
        {
            ShowRecordedPaths = !ShowRecordedPaths;
            StatusMessage = ShowRecordedPaths ? "Recorded paths visible" : "Recorded paths hidden";
        });

        ImportTracksCommand = ReactiveCommand.Create(() =>
        {
            var activeField = _fieldService.ActiveField;
            if (activeField == null || string.IsNullOrEmpty(activeField.DirectoryPath))
            {
                StatusMessage = "Open a field first before importing tracks";
                return;
            }

            // Populate available fields for import
            ImportFieldsList.Clear();
            var fieldsDir = FieldsRootDirectory;
            if (string.IsNullOrEmpty(fieldsDir) || !Directory.Exists(fieldsDir))
            {
                StatusMessage = "No fields directory found";
                return;
            }

            foreach (var dir in Directory.GetDirectories(fieldsDir))
            {
                var fieldName = Path.GetFileName(dir);
                // Skip the current active field
                if (dir == activeField.DirectoryPath)
                    continue;
                // Only include fields that have tracks
                if (Services.TrackFilesService.Exists(dir))
                    ImportFieldsList.Add(fieldName);
            }

            if (ImportFieldsList.Count == 0)
            {
                StatusMessage = "No other fields with tracks found";
                return;
            }

            State.UI.ShowDialog(DialogType.ImportTracks);
        });

        ImportTracksFromFieldCommand = ReactiveCommand.Create<string>(fieldName =>
        {
            if (string.IsNullOrEmpty(fieldName))
                return;

            var fieldsDir = FieldsRootDirectory;
            var sourceDir = Path.Combine(fieldsDir, fieldName);

            try
            {
                var importedTracks = Services.TrackFilesService.LoadTracks(sourceDir);
                if (importedTracks.Count == 0)
                {
                    StatusMessage = "No tracks found in selected field";
                    return;
                }

                int importCount = 0;
                foreach (var track in importedTracks)
                {
                    track.IsActive = false;
                    SavedTracks.Add(track);

                    if (track.Type == TrackType.RecordedPath)
                        RecordedPathTracks.Add(track);
                    else if (track.Type == TrackType.Contour)
                        ContourStrips.Add(track);

                    importCount++;
                }

                SaveTracksToFile();
                UpdateRecordedPathsOnMap();
                UpdateContourStripsOnMap();
                State.UI.CloseDialog();
                StatusMessage = $"Imported {importCount} track(s) from '{fieldName}'";
                _logger.LogDebug("[TrackImport] Imported {Count} tracks from {Field}", importCount, fieldName);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
                _logger.LogWarning(ex, "[TrackImport] Failed to import tracks from {Field}", fieldName);
            }
        });

        CloseImportTracksDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        DeleteContourTrackCommand = ReactiveCommand.Create(() =>
        {
            if (SelectedTrack == null)
            {
                StatusMessage = "No track selected";
                return;
            }

            var trackName = SelectedTrack.Name;
            ShowConfirmationDialog(
                "Delete Track",
                $"Delete track '{trackName}'? This cannot be undone.",
                () =>
                {
                    SavedTracks.Remove(SelectedTrack);
                    SelectedTrack = null;
                    SaveTracksToFile();
                    StatusMessage = $"Deleted track '{trackName}'";
                });
        });

        StartContourRecordingCommand = ReactiveCommand.Create(() =>
        {
            if (!IsContourModeOn)
            {
                StatusMessage = "Enable contour mode first";
                return;
            }

            _contourRecordingPoints.Clear();
            _lastContourPoint = null;
            IsRecordingContour = true;

            // Capture first point at current GPS position
            if (Easting != 0 || Northing != 0)
            {
                var headingRadians = Heading * Math.PI / 180.0;
                var firstPoint = new Vec3(Easting, Northing, headingRadians);
                _contourRecordingPoints.Add(firstPoint);
                _lastContourPoint = firstPoint;
            }

            StatusMessage = $"Contour recording started ({_contourRecordingPoints.Count} pts)";
        });

        StopContourRecordingCommand = ReactiveCommand.Create(() =>
        {
            if (!IsRecordingContour)
                return;

            IsRecordingContour = false;

            if (_contourRecordingPoints.Count < 3)
            {
                StatusMessage = $"Need at least 3 points for contour (have {_contourRecordingPoints.Count})";
                _contourRecordingPoints.Clear();
                _lastContourPoint = null;
                return;
            }

            // Create contour track from recorded points
            var contourTrack = Track.FromContour(
                $"Contour {DateTime.Now:HH:mm:ss}",
                new List<Vec3>(_contourRecordingPoints));

            ContourStrips.Add(contourTrack);
            SavedTracks.Add(contourTrack);
            SaveTracksToFile();
            UpdateContourStripsOnMap();

            StatusMessage = $"Contour saved with {_contourRecordingPoints.Count} points";
            _logger.LogDebug("[Contour] Created contour strip with {Count} points", _contourRecordingPoints.Count);

            _contourRecordingPoints.Clear();
            _lastContourPoint = null;
        });
    }

    #endregion

    #region Recorded Path Display

    private void UpdateRecordedPathsOnMap()
    {
        if (ShowRecordedPaths)
        {
            var visiblePaths = RecordedPathTracks.Where(t => t.IsVisible).ToList();
            _mapService.SetRecordedPaths(visiblePaths);
        }
        else
        {
            _mapService.SetRecordedPaths(Array.Empty<Track>());
        }
    }

    private void UpdateContourStripsOnMap()
    {
        var visibleStrips = ContourStrips.Where(t => t.IsVisible).ToList();
        _mapService.SetContourStrips(visibleStrips);
    }

    /// <summary>
    /// Rebuild recorded paths and contour strips from SavedTracks after loading a field.
    /// </summary>
    private void RebuildRecordedPathsAndContours()
    {
        RecordedPathTracks.Clear();
        ContourStrips.Clear();

        foreach (var track in SavedTracks)
        {
            if (track.Type == TrackType.RecordedPath)
                RecordedPathTracks.Add(track);
            else if (track.Type == TrackType.Contour)
                ContourStrips.Add(track);
        }

        UpdateRecordedPathsOnMap();
        UpdateContourStripsOnMap();
    }

    #endregion

    #region Contour Recording (GPS point capture)

    /// <summary>
    /// Add a point to the contour being recorded, with minimum spacing filtering.
    /// Called from GPS update handler when IsRecordingContour is true.
    /// </summary>
    private void AddContourPoint(double easting, double northing, double headingDegrees)
    {
        double headingRadians = headingDegrees * Math.PI / 180.0;

        // Check minimum spacing from last point
        if (_lastContourPoint.HasValue)
        {
            double dx = easting - _lastContourPoint.Value.Easting;
            double dy = northing - _lastContourPoint.Value.Northing;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < ContourMinPointSpacing)
                return;
        }

        var point = new Vec3(easting, northing, headingRadians);
        _contourRecordingPoints.Add(point);
        _lastContourPoint = point;

        // Show recording on map
        var displayPoints = _contourRecordingPoints.Select(p => (p.Easting, p.Northing)).ToList();
        _mapService.SetRecordingPoints(displayPoints);

        // Update UI periodically
        if (_contourRecordingPoints.Count % 10 == 0)
        {
            StatusMessage = $"Recording contour: {_contourRecordingPoints.Count} points";
        }
    }

    #endregion

    #region Contour Guidance

    /// <summary>
    /// Find the nearest contour strip to the current position and calculate cross-track error.
    /// Returns the nearest strip and the signed perpendicular distance.
    /// </summary>
    private (Track? nearestStrip, double crossTrackError, double stripHeading) FindNearestContour(
        double easting, double northing, double headingRadians)
    {
        Track? nearestStrip = null;
        double minDistSq = double.MaxValue;
        double bestXTE = 0;
        double bestHeading = 0;

        foreach (var strip in ContourStrips)
        {
            if (!strip.IsVisible || strip.Points.Count < 2)
                continue;

            // Sample every 3rd point for coarse search (matching original AgOpenGPS algorithm)
            for (int i = 0; i < strip.Points.Count - 1; i += 3)
            {
                int idx = Math.Min(i, strip.Points.Count - 2);
                var p1 = strip.Points[idx];
                var p2 = strip.Points[idx + 1];

                double dx = easting - p1.Easting;
                double dy = northing - p1.Northing;
                double distSq = dx * dx + dy * dy;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearestStrip = strip;

                    // Calculate signed perpendicular distance (cross-track error)
                    double segDx = p2.Easting - p1.Easting;
                    double segDy = p2.Northing - p1.Northing;
                    double segLen = Math.Sqrt(segDx * segDx + segDy * segDy);

                    if (segLen > 0.01)
                    {
                        bestXTE = (segDy * easting - segDx * northing
                            + p2.Easting * p1.Northing - p2.Northing * p1.Easting) / segLen;
                        bestHeading = Math.Atan2(segDx, segDy);
                    }
                }
            }
        }

        return (nearestStrip, bestXTE, bestHeading);
    }

    /// <summary>
    /// Calculate contour guidance: find nearest contour and use it for steering.
    /// Creates an offset guidance line from the nearest contour strip.
    /// </summary>
    private void CalculateContourGuidance(Models.Position currentPosition)
    {
        if (ContourStrips.Count == 0) return;

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;
        var (nearestStrip, xte, stripHeading) = FindNearestContour(
            currentPosition.Easting, currentPosition.Northing, headingRadians);

        if (nearestStrip == null) return;

        // Calculate parallel offset (which adjacent pass are we on)
        double widthMinusOverlap = ConfigStore.ActualToolWidth
            - ConfigStore.Tool.Overlap;

        if (widthMinusOverlap < 0.1) return;

        // Determine how many passes away we are
        int pathsAway = (int)Math.Round(xte / widthMinusOverlap);
        double distAway = pathsAway * widthMinusOverlap;

        // Use the nearest strip as a guidance track with the appropriate offset
        var input = new Models.Track.TrackGuidanceInput
        {
            Track = nearestStrip,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            SteerPosition = new Vec3(
                currentPosition.Easting + Math.Sin(headingRadians) * ConfigStore.Vehicle.Wheelbase,
                currentPosition.Northing + Math.Cos(headingRadians) * ConfigStore.Vehicle.Wheelbase,
                headingRadians),
            UseStanley = false,
            IsHeadingSameWay = Math.Abs(headingRadians - stripHeading) < Math.PI / 2
                || Math.Abs(headingRadians - stripHeading) > 3 * Math.PI / 2,

            Wheelbase = ConfigStore.Vehicle.Wheelbase,
            MaxSteerAngle = ConfigStore.Vehicle.MaxSteerAngle,
            GoalPointDistance = ConfigStore.Guidance.GoalPointLookAheadHold,
            SideHillCompFactor = 0,
            PurePursuitIntegralGain = ConfigStore.Guidance.PurePursuitIntegralGain,

            FixHeading = headingRadians,
            AvgSpeed = currentPosition.Speed * 3.6,
            IsReverse = false,
            IsAutoSteerOn = true,
            IsYouTurnTriggered = false,
            ImuRoll = 88888,

            PreviousState = _trackGuidanceState,
            FindGlobalNearest = _trackGuidanceState == null,
            CurrentLocationIndex = _trackGuidanceState?.CurrentLocationIndex ?? 0
        };

        var output = _trackGuidanceService.CalculateGuidance(input);
        _trackGuidanceState = output.State;
        if (_trackGuidanceState != null)
            _trackGuidanceState.CurrentLocationIndex = output.CurrentLocationIndex;

        State.Guidance.UpdateFromGuidance(output);
        SimulatorSteerAngle = output.SteerAngle;
        CrossTrackError = output.CrossTrackError * 100;
    }

    #endregion
}
