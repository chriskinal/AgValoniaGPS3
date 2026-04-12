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
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Boundary and headland commands - boundary creation, headland building, AgShare.
/// </summary>
public partial class MainViewModel
{
    private void InitializeBoundaryCommands()
    {
        // Boundary Map Dialog Commands (satellite map boundary drawing)
        ShowBoundaryMapDialogCommand = new RelayCommand(() =>
        {
            if (_fieldOriginLatitude != 0 || _fieldOriginLongitude != 0)
            {
                BoundaryMapCenterLatitude = _fieldOriginLatitude;
                BoundaryMapCenterLongitude = _fieldOriginLongitude;
            }
            else if (Latitude != 0 || Longitude != 0)
            {
                BoundaryMapCenterLatitude = Latitude;
                BoundaryMapCenterLongitude = Longitude;
            }

            BoundaryMapPointCount = 0;
            BoundaryMapCanSave = false;
            BoundaryMapCoordinateText = string.Empty;
            BoundaryMapResultPoints.Clear();
            State.UI.ShowDialog(DialogType.BoundaryMap);
        });

        CancelBoundaryMapDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            BoundaryMapResultPoints.Clear();
        });

        ConfirmBoundaryMapDialogCommand = new RelayCommand(() =>
        {
            if (BoundaryMapResultPoints.Count >= 3 && IsFieldOpen && !string.IsNullOrEmpty(CurrentFieldName))
            {
                try
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

                    // Use the EXISTING field origin for LocalPlane conversion - do NOT change it!
                    // The field origin is set when the field is created and should remain constant.
                    // Changing it would break all local coordinate systems.
                    var originLat = _fieldOriginLatitude;
                    var originLon = _fieldOriginLongitude;

                    _logger.LogDebug($"[BoundaryMap] Using existing field origin: ({originLat:F8}, {originLon:F8})");
                    _logger.LogDebug($"[BoundaryMap] Current simulator position: ({Latitude:F8}, {Longitude:F8})");

                    var origin = new Wgs84(originLat, originLon);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();
                    foreach (var (lat, lon) in BoundaryMapResultPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                        _logger.LogDebug($"[BoundaryMap] Point WGS84: ({lat:F8}, {lon:F8}) -> Local: ({geoCoord.Easting:F2}, {geoCoord.Northing:F2})");
                    }

                    boundary.OuterBoundary = outerPolygon;
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // NOTE: Do NOT overwrite the field origin - it should stay constant!
                    // The simulator coordinates and field origin should not change when
                    // the user draws a boundary on the map.

                    SetCurrentBoundary(boundary);

                    if (outerPolygon.Points.Count > 0)
                    {
                        double minE = double.MaxValue, maxE = double.MinValue;
                        double minN = double.MaxValue, maxN = double.MinValue;
                        foreach (var pt in outerPolygon.Points)
                        {
                            minE = Math.Min(minE, pt.Easting);
                            maxE = Math.Max(maxE, pt.Easting);
                            minN = Math.Min(minN, pt.Northing);
                            maxN = Math.Max(maxN, pt.Northing);
                        }
                        double centerE = (minE + maxE) / 2.0;
                        double centerN = (minN + maxN) / 2.0;
                        double maxExtent = Math.Max(maxE - minE, maxN - minN);

                        _mapService.PanTo(centerE, centerN);
                        if (maxExtent > 0)
                        {
                            double newZoom = Math.Clamp(200.0 / (maxExtent * 1.2), 0.1, 10.0);
                            _mapService.SetCamera(centerE, centerN, newZoom, 0);
                        }
                    }

                    if (!string.IsNullOrEmpty(BoundaryMapResultBackgroundPath))
                    {
                        SaveBackgroundImage(BoundaryMapResultBackgroundPath, fieldPath,
                            BoundaryMapResultNwLat, BoundaryMapResultNwLon,
                            BoundaryMapResultSeLat, BoundaryMapResultSeLon,
                            BoundaryMapResultMercMinX, BoundaryMapResultMercMaxX,
                            BoundaryMapResultMercMinY, BoundaryMapResultMercMaxY);
                    }

                    RefreshBoundaryList();
                    StatusMessage = $"Boundary created with {BoundaryMapResultPoints.Count} points";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error creating boundary: {ex.Message}";
                }
            }

            State.UI.CloseDialog();
            IsBoundaryPanelVisible = false;
            BoundaryMapResultPoints.Clear();
        });

        // AgShare Dialogs
        ShowAgShareDownloadDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareDownload);
        });

        CancelAgShareDownloadDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAgShareUploadDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareUpload);
        });

        CancelAgShareUploadDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            AgShareSettingsServerUrl = _settingsService.Settings.AgShareServer;
            AgShareSettingsApiKey = _settingsService.Settings.AgShareApiKey;
            AgShareSettingsEnabled = _settingsService.Settings.AgShareEnabled;
            State.UI.ShowDialog(DialogType.AgShareSettings);
        });

        CancelAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            _settingsService.Settings.AgShareServer = AgShareSettingsServerUrl;
            _settingsService.Settings.AgShareApiKey = AgShareSettingsApiKey;
            _settingsService.Settings.AgShareEnabled = AgShareSettingsEnabled;
            _settingsService.Save();
            State.UI.CloseDialog();
            StatusMessage = "AgShare settings saved";
        });

        ShowBoundaryDialogCommand = new RelayCommand(() =>
        {
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        // Headland Commands
        ShowHeadlandBuilderCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen)
            {
                StatusMessage = "Open a field first";
                return;
            }
            State.UI.ShowDialog(DialogType.FieldBuilder);
            UpdateHeadlandPreview();
        });

        ToggleHeadlandCommand = new RelayCommand(() =>
        {
            if (!HasHeadland)
            {
                StatusMessage = "No headland defined";
                return;
            }
            IsHeadlandOn = !IsHeadlandOn;
        });

        ToggleSectionInHeadlandCommand = new RelayCommand(() =>
        {
            IsSectionControlInHeadland = !IsSectionControlInHeadland;
            StatusMessage = IsSectionControlInHeadland ? "Section control in headland: ON" : "Section control in headland: OFF";
        });

        ResetToolHeadingCommand = new RelayCommand(() =>
        {
            // Reset tool heading to match vehicle heading
            // This synchronizes the implement direction with the tractor
            double vehicleHeadingRadians = Heading * Math.PI / 180.0;
            ToolHeadingRadians = vehicleHeadingRadians;
            _toolPositionService.ResetTrailingState(
                new Models.Base.Vec3(Easting, Northing, vehicleHeadingRadians),
                vehicleHeadingRadians);
            StatusMessage = "Tool heading reset to vehicle heading";
        });

        BuildHeadlandCommand = new RelayCommand(() =>
        {
            // If segments exist, rebuild from segments; otherwise fall back to Clipper2
            if (HeadlandSegments.Count > 0)
            {
                foreach (var seg in HeadlandSegments)
                    ComputeSegmentOffset(seg);
                BuildHeadlandFromSegments();
            }
            else
            {
                BuildHeadlandFromBoundary();
            }
        });

        ClearHeadlandCommand = new RelayCommand(() =>
        {
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
            HasHeadland = false;
            IsHeadlandOn = false;
            StatusMessage = "Headland cleared";
        });

        CloseHeadlandBuilderCommand = new RelayCommand(() =>
        {
            HeadlandPreviewLine = null;
            State.UI.CloseDialog();
        });

        SetHeadlandToToolWidthCommand = new RelayCommand(() =>
        {
            double actualWidth = ConfigStore.ActualToolWidth;
            HeadlandDistance = actualWidth > 0 ? actualWidth * 2 : 12.0;
            UpdateHeadlandPreview();
        });

        PreviewHeadlandCommand = new RelayCommand(() =>
        {
            UpdateHeadlandPreview();
        });

        IncrementHeadlandDistanceCommand = new RelayCommand(() =>
        {
            HeadlandDistance = Math.Min(HeadlandDistance + 0.5, 100.0);
            UpdateHeadlandPreview();
        });

        DecrementHeadlandDistanceCommand = new RelayCommand(() =>
        {
            HeadlandDistance = Math.Max(HeadlandDistance - 0.5, 0.5);
            UpdateHeadlandPreview();
        });

        IncrementHeadlandPassesCommand = new RelayCommand(() =>
        {
            HeadlandPasses = Math.Min(HeadlandPasses + 1, 10);
            UpdateHeadlandPreview();
        });

        DecrementHeadlandPassesCommand = new RelayCommand(() =>
        {
            HeadlandPasses = Math.Max(HeadlandPasses - 1, 1);
            UpdateHeadlandPreview();
        });

        // Headland Dialog - now opens Field Builder
        ShowHeadlandDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.FieldBuilder);
            UpdateHeadlandPreview();
        });

        CloseHeadlandDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            HeadlandPreviewLine = null;
        });

        ExtendHeadlandACommand = new RelayCommand(() =>
        {
            AdjustHeadlandDistance(1.0);
        });

        ExtendHeadlandBCommand = new RelayCommand(() =>
        {
            AdjustHeadlandDistance(0.25);
        });

        ShrinkHeadlandACommand = new RelayCommand(() =>
        {
            AdjustHeadlandDistance(-1.0);
        });

        ShrinkHeadlandBCommand = new RelayCommand(() =>
        {
            AdjustHeadlandDistance(-0.25);
        });

        ResetHeadlandCommand = new RelayCommand(() =>
        {
            // Save for undo
            _previousHeadlandLine = _currentHeadlandLine != null ? new List<Vec3>(_currentHeadlandLine) : null;
            _previousHasHeadland = HasHeadland;

            ClearHeadlandCommand?.Execute(null);
            OnPropertyChanged(nameof(HeadlandStatusText));
            OnPropertyChanged(nameof(CurrentHeadlandLineForPreview));
            StatusMessage = "Headland reset";
        });

        ClipHeadlandLineCommand = new RelayCommand(() =>
        {
            if (!HeadlandPointsSelected)
            {
                StatusMessage = "Select 2 points on the boundary first";
                return;
            }

            var headlandToClip = CurrentHeadlandLine ?? ConvertPreviewToVec3(HeadlandPreviewLine);
            if (headlandToClip == null || headlandToClip.Count < 3)
            {
                StatusMessage = "No headland to clip - use Build first";
                return;
            }

            ClipHeadlandAtLine(headlandToClip);
        });

        UndoHeadlandCommand = new RelayCommand(() =>
        {
            if (_previousHeadlandLine == null && !_previousHasHeadland)
            {
                StatusMessage = "Nothing to undo";
                return;
            }

            // Restore previous state
            CurrentHeadlandLine = _previousHeadlandLine;
            HasHeadland = _previousHasHeadland;
            IsHeadlandOn = _previousHasHeadland;

            if (_previousHeadlandLine != null && _previousHeadlandLine.Count >= 3)
            {
                _currentHeadlandLine = _previousHeadlandLine;
                State.Field.HeadlandLine = _previousHeadlandLine;
                _mapService.SetHeadlandLine(_previousHeadlandLine);
                _mapService.SetHeadlandVisible(true);
            }
            else
            {
                _currentHeadlandLine = null;
                State.Field.HeadlandLine = null;
                _mapService.SetHeadlandVisible(false);
            }

            _previousHeadlandLine = null;
            _previousHasHeadland = false;

            OnPropertyChanged(nameof(HeadlandStatusText));
            OnPropertyChanged(nameof(CurrentHeadlandLineForPreview));
            StatusMessage = "Headland undone";
        });

        TurnOffHeadlandCommand = new RelayCommand(() =>
        {
            // Save for undo
            _previousHeadlandLine = _currentHeadlandLine != null ? new List<Vec3>(_currentHeadlandLine) : null;
            _previousHasHeadland = HasHeadland;

            IsHeadlandOn = false;
            HasHeadland = false;
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
            _currentHeadlandLine = null;
            State.Field.HeadlandLine = null;
            _mapService.SetHeadlandVisible(false);
            OnPropertyChanged(nameof(HeadlandStatusText));
            OnPropertyChanged(nameof(CurrentHeadlandLineForPreview));
            StatusMessage = "Headland turned off";
        });

        // Boundary Recording Commands
        ToggleBoundaryPanelCommand = new RelayCommand(() =>
        {
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        StartBoundaryRecordingCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.StartRecording(BoundaryType.Outer);
            StatusMessage = "Boundary recording started";
        });

        PauseBoundaryRecordingCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.PauseRecording();
            IsBoundaryRecording = false;
            StatusMessage = "Boundary recording paused";
        });

        StopBoundaryRecordingCommand = new RelayCommand(() =>
        {
            var polygon = _boundaryRecordingService.StopRecording();

            if (polygon != null && polygon.Points.Count >= 3)
            {
                if (!string.IsNullOrEmpty(CurrentFieldName))
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();
                    boundary.OuterBoundary = polygon;
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);
                    SetCurrentBoundary(boundary);
                    RefreshBoundaryList();
                    StatusMessage = $"Boundary saved with {polygon.Points.Count} points, Area: {polygon.AreaHectares:F2} Ha";
                }
                else
                {
                    StatusMessage = "Cannot save boundary - no field is open";
                }
            }
            else
            {
                StatusMessage = "Boundary not saved - need at least 3 points";
            }

            IsBoundaryPlayerPanelVisible = false;
            IsBoundaryRecording = false;
        });

        ToggleRecordingCommand = new RelayCommand(() =>
        {
            if (IsBoundaryRecording)
            {
                _boundaryRecordingService.PauseRecording();
                IsBoundaryRecording = false;
                StatusMessage = "Recording paused";
            }
            else
            {
                _boundaryRecordingService.ResumeRecording();
                IsBoundaryRecording = true;
                StatusMessage = "Recording boundary - drive around the perimeter";
            }
        });

        UndoBoundaryPointCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.RemoveLastPoint();
        });

        ClearBoundaryCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.ClearPoints();
            StatusMessage = "Boundary cleared";
        });

        AddBoundaryPointCommand = new RelayCommand(() =>
        {
            double headingRadians = Heading * Math.PI / 180.0;
            var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(Easting, Northing, headingRadians);
            _boundaryRecordingService.AddPointManual(offsetEasting, offsetNorthing, headingRadians);
            StatusMessage = $"Point added ({_boundaryRecordingService.PointCount} total)";
        });

        ToggleBoundaryLeftRightCommand = new RelayCommand(() =>
        {
            IsDrawRightSide = !IsDrawRightSide;
        });

        ToggleBoundaryAntennaToolCommand = new RelayCommand(() =>
        {
            IsDrawAtPivot = !IsDrawAtPivot;
        });

        ShowBoundaryOffsetDialogCommand = new RelayCommand(() =>
        {
            NumericInputDialogTitle = "Boundary Offset (cm)";
            NumericInputDialogValue = (decimal)BoundaryOffset;
            NumericInputDialogDisplayText = BoundaryOffset.ToString("F0");
            NumericInputDialogIntegerOnly = true;
            NumericInputDialogAllowNegative = false;
            _numericInputDialogCallback = (value) =>
            {
                BoundaryOffset = value;
                StatusMessage = $"Boundary offset set to {BoundaryOffset:F0} cm";
            };
            State.UI.ShowDialog(DialogType.NumericInput);
        });

        CancelNumericInputDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            _numericInputDialogCallback = null;
        });

        ConfirmNumericInputDialogCommand = new RelayCommand(() =>
        {
            if (NumericInputDialogValue.HasValue && _numericInputDialogCallback != null)
            {
                _numericInputDialogCallback((double)NumericInputDialogValue.Value);
            }
            State.UI.CloseDialog();
            _numericInputDialogCallback = null;
        });

        // Confirmation Dialog Commands
        CancelConfirmationDialogCommand = new RelayCommand(() =>
        {
            var prev = _previousDialogBeforeConfirmation;
            _confirmationDialogCallback = null;
            _previousDialogBeforeConfirmation = Models.State.DialogType.None;
            if (prev != Models.State.DialogType.None && prev != Models.State.DialogType.Confirmation)
                State.UI.ShowDialog(prev);
            else
                State.UI.CloseDialog();
        });

        ConfirmConfirmationDialogCommand = new RelayCommand(() =>
        {
            var callback = _confirmationDialogCallback;
            var prev = _previousDialogBeforeConfirmation;
            _confirmationDialogCallback = null;
            _previousDialogBeforeConfirmation = Models.State.DialogType.None;
            if (prev != Models.State.DialogType.None && prev != Models.State.DialogType.Confirmation)
                State.UI.ShowDialog(prev);
            else
                State.UI.CloseDialog();
            callback?.Invoke();
        });

        // Error Dialog Command
        DismissErrorDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        DeleteBoundaryCommand = new RelayCommand(DeleteSelectedBoundary);

        ImportKmlBoundaryCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before importing a boundary";
                return;
            }

            State.UI.ShowDialog(DialogType.KmlImport);
        });

        DrawMapBoundaryCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first to add boundary";
                return;
            }
            ShowBoundaryMapDialogCommand?.Execute(null);
        });

        BuildFromTracksCommand = new RelayCommand(() =>
        {
            StatusMessage = "Build boundary from tracks not yet implemented";
        });

        DriveAroundFieldCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before recording a boundary";
                return;
            }

            IsBoundaryPanelVisible = false;
            IsBoundaryPlayerPanelVisible = true;

            _boundaryRecordingService.StartRecording(BoundaryType.Outer);
            _boundaryRecordingService.PauseRecording();

            StatusMessage = "Drive around the field boundary. Click Record to start.";
        });
    }
}
