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
using System.Reactive;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Boundary and headland commands - boundary creation, headland building, AgShare.
/// </summary>
public partial class MainViewModel
{
    private void InitializeBoundaryCommands()
    {
        // Boundary Map Dialog Commands (satellite map boundary drawing)
        ShowBoundaryMapDialogCommand = ReactiveCommand.Create(() =>
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

        CancelBoundaryMapDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            BoundaryMapResultPoints.Clear();
        });

        ConfirmBoundaryMapDialogCommand = ReactiveCommand.Create(() =>
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

                    Console.WriteLine($"[BoundaryMap] Using existing field origin: ({originLat:F8}, {originLon:F8})");
                    Console.WriteLine($"[BoundaryMap] Current simulator position: ({Latitude:F8}, {Longitude:F8})");

                    var origin = new Wgs84(originLat, originLon);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();
                    foreach (var (lat, lon) in BoundaryMapResultPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                        Console.WriteLine($"[BoundaryMap] Point WGS84: ({lat:F8}, {lon:F8}) -> Local: ({geoCoord.Easting:F2}, {geoCoord.Northing:F2})");
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
        ShowAgShareDownloadDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareDownload);
        });

        CancelAgShareDownloadDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAgShareUploadDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareUpload);
        });

        CancelAgShareUploadDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAgShareSettingsDialogCommand = ReactiveCommand.Create(() =>
        {
            AgShareSettingsServerUrl = _settingsService.Settings.AgShareServer;
            AgShareSettingsApiKey = _settingsService.Settings.AgShareApiKey;
            AgShareSettingsEnabled = _settingsService.Settings.AgShareEnabled;
            State.UI.ShowDialog(DialogType.AgShareSettings);
        });

        CancelAgShareSettingsDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmAgShareSettingsDialogCommand = ReactiveCommand.Create(() =>
        {
            _settingsService.Settings.AgShareServer = AgShareSettingsServerUrl;
            _settingsService.Settings.AgShareApiKey = AgShareSettingsApiKey;
            _settingsService.Settings.AgShareEnabled = AgShareSettingsEnabled;
            _settingsService.Save();
            State.UI.CloseDialog();
            StatusMessage = "AgShare settings saved";
        });

        ShowBoundaryDialogCommand = ReactiveCommand.Create(() =>
        {
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        // Headland Commands
        ShowHeadlandBuilderCommand = ReactiveCommand.Create(() =>
        {
            if (!IsFieldOpen)
            {
                StatusMessage = "Open a field first";
                return;
            }
            State.UI.ShowDialog(DialogType.HeadlandBuilder);
            UpdateHeadlandPreview();
        });

        ToggleHeadlandCommand = ReactiveCommand.Create(() =>
        {
            if (!HasHeadland)
            {
                StatusMessage = "No headland defined";
                return;
            }
            IsHeadlandOn = !IsHeadlandOn;
        });

        ToggleSectionInHeadlandCommand = ReactiveCommand.Create(() =>
        {
            IsSectionControlInHeadland = !IsSectionControlInHeadland;
            StatusMessage = IsSectionControlInHeadland ? "Section control in headland: ON" : "Section control in headland: OFF";
        });

        ResetToolHeadingCommand = ReactiveCommand.Create(() =>
        {
            StatusMessage = "Tool heading reset";
        });

        BuildHeadlandCommand = ReactiveCommand.Create(() =>
        {
            BuildHeadlandFromBoundary();
        });

        ClearHeadlandCommand = ReactiveCommand.Create(() =>
        {
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
            HasHeadland = false;
            IsHeadlandOn = false;
            StatusMessage = "Headland cleared";
        });

        CloseHeadlandBuilderCommand = ReactiveCommand.Create(() =>
        {
            HeadlandPreviewLine = null;
            State.UI.CloseDialog();
        });

        SetHeadlandToToolWidthCommand = ReactiveCommand.Create(() =>
        {
            double actualWidth = ConfigStore.ActualToolWidth;
            HeadlandDistance = actualWidth > 0 ? actualWidth * 2 : 12.0;
            UpdateHeadlandPreview();
        });

        PreviewHeadlandCommand = ReactiveCommand.Create(() =>
        {
            UpdateHeadlandPreview();
        });

        IncrementHeadlandDistanceCommand = ReactiveCommand.Create(() =>
        {
            HeadlandDistance = Math.Min(HeadlandDistance + 0.5, 100.0);
            UpdateHeadlandPreview();
        });

        DecrementHeadlandDistanceCommand = ReactiveCommand.Create(() =>
        {
            HeadlandDistance = Math.Max(HeadlandDistance - 0.5, 0.5);
            UpdateHeadlandPreview();
        });

        IncrementHeadlandPassesCommand = ReactiveCommand.Create(() =>
        {
            HeadlandPasses = Math.Min(HeadlandPasses + 1, 10);
            UpdateHeadlandPreview();
        });

        DecrementHeadlandPassesCommand = ReactiveCommand.Create(() =>
        {
            HeadlandPasses = Math.Max(HeadlandPasses - 1, 1);
            UpdateHeadlandPreview();
        });

        // Headland Dialog (FormHeadLine) commands
        ShowHeadlandDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.ShowDialog(DialogType.Headland);
            UpdateHeadlandPreview();
        });

        CloseHeadlandDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            HeadlandPreviewLine = null;
        });

        ExtendHeadlandACommand = ReactiveCommand.Create(() =>
        {
            StatusMessage = "Extend A - not yet implemented";
        });

        ExtendHeadlandBCommand = ReactiveCommand.Create(() =>
        {
            StatusMessage = "Extend B - not yet implemented";
        });

        ShrinkHeadlandACommand = ReactiveCommand.Create(() =>
        {
            StatusMessage = "Shrink A - not yet implemented";
        });

        ShrinkHeadlandBCommand = ReactiveCommand.Create(() =>
        {
            StatusMessage = "Shrink B - not yet implemented";
        });

        ResetHeadlandCommand = ReactiveCommand.Create(() =>
        {
            ClearHeadlandCommand?.Execute(null);
            StatusMessage = "Headland reset";
        });

        ClipHeadlandLineCommand = ReactiveCommand.Create(() =>
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

        UndoHeadlandCommand = ReactiveCommand.Create(() =>
        {
            StatusMessage = "Undo - not yet implemented";
        });

        TurnOffHeadlandCommand = ReactiveCommand.Create(() =>
        {
            IsHeadlandOn = false;
            HasHeadland = false;
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
            StatusMessage = "Headland turned off";
        });

        // Boundary Recording Commands
        ToggleBoundaryPanelCommand = ReactiveCommand.Create(() =>
        {
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        StartBoundaryRecordingCommand = ReactiveCommand.Create(() =>
        {
            _boundaryRecordingService.StartRecording(BoundaryType.Outer);
            StatusMessage = "Boundary recording started";
        });

        PauseBoundaryRecordingCommand = ReactiveCommand.Create(() =>
        {
            _boundaryRecordingService.PauseRecording();
            IsBoundaryRecording = false;
            StatusMessage = "Boundary recording paused";
        });

        StopBoundaryRecordingCommand = ReactiveCommand.Create(() =>
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

        ToggleRecordingCommand = ReactiveCommand.Create(() =>
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

        UndoBoundaryPointCommand = ReactiveCommand.Create(() =>
        {
            _boundaryRecordingService.RemoveLastPoint();
        });

        ClearBoundaryCommand = ReactiveCommand.Create(() =>
        {
            _boundaryRecordingService.ClearPoints();
            StatusMessage = "Boundary cleared";
        });

        AddBoundaryPointCommand = ReactiveCommand.Create(() =>
        {
            double headingRadians = Heading * Math.PI / 180.0;
            var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(Easting, Northing, headingRadians);
            _boundaryRecordingService.AddPointManual(offsetEasting, offsetNorthing, headingRadians);
            StatusMessage = $"Point added ({_boundaryRecordingService.PointCount} total)";
        });

        ToggleBoundaryLeftRightCommand = ReactiveCommand.Create(() =>
        {
            IsDrawRightSide = !IsDrawRightSide;
        });

        ToggleBoundaryAntennaToolCommand = ReactiveCommand.Create(() =>
        {
            IsDrawAtPivot = !IsDrawAtPivot;
        });

        ShowBoundaryOffsetDialogCommand = ReactiveCommand.Create(() =>
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

        CancelNumericInputDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            _numericInputDialogCallback = null;
        });

        ConfirmNumericInputDialogCommand = ReactiveCommand.Create(() =>
        {
            if (NumericInputDialogValue.HasValue && _numericInputDialogCallback != null)
            {
                _numericInputDialogCallback((double)NumericInputDialogValue.Value);
            }
            State.UI.CloseDialog();
            _numericInputDialogCallback = null;
        });

        // Confirmation Dialog Commands
        CancelConfirmationDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
            _confirmationDialogCallback = null;
        });

        ConfirmConfirmationDialogCommand = ReactiveCommand.Create(() =>
        {
            var callback = _confirmationDialogCallback;
            State.UI.CloseDialog();
            _confirmationDialogCallback = null;
            callback?.Invoke();
        });

        // Error Dialog Command
        DismissErrorDialogCommand = ReactiveCommand.Create(() =>
        {
            State.UI.CloseDialog();
        });

        DeleteBoundaryCommand = ReactiveCommand.Create(DeleteSelectedBoundary);

        ImportKmlBoundaryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before importing a boundary";
                return;
            }

            var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
            var result = await _dialogService.ShowKmlImportDialogAsync(_settingsService.Settings.FieldsDirectory, fieldPath);

            if (result != null && result.BoundaryPoints.Count > 0)
            {
                try
                {
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();
                    var origin = new Wgs84(result.CenterLatitude, result.CenterLongitude);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();
                    foreach (var (lat, lon) in result.BoundaryPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                    }

                    boundary.OuterBoundary = outerPolygon;
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);
                    SetCurrentBoundary(boundary);
                    RefreshBoundaryList();
                    StatusMessage = $"Boundary imported from KML ({outerPolygon.Points.Count} points)";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error importing KML boundary: {ex.Message}";
                }
            }
        });

        DrawMapBoundaryCommand = ReactiveCommand.Create(() =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first to add boundary";
                return;
            }
            ShowBoundaryMapDialogCommand?.Execute(null);
        });

        DrawMapBoundaryDesktopCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first to add boundary";
                return;
            }

            var result = await _dialogService.ShowMapBoundaryDialogAsync(Latitude, Longitude);

            if (result != null && (result.BoundaryPoints.Count >= 3 || result.HasBackgroundImage))
            {
                try
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);

                    // Use the EXISTING field origin - do NOT change it!
                    // The field origin is set when the field is created and should remain constant.
                    Console.WriteLine($"[BoundaryMapDesktop] Using existing field origin: ({_fieldOriginLatitude:F8}, {_fieldOriginLongitude:F8})");

                    var origin = new Wgs84(_fieldOriginLatitude, _fieldOriginLongitude);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    if (result.BoundaryPoints.Count >= 3)
                    {
                        var boundary = new Boundary();
                        var outerPolygon = new BoundaryPolygon();

                        foreach (var point in result.BoundaryPoints)
                        {
                            var wgs84 = new Wgs84(point.Latitude, point.Longitude);
                            var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                            outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                        }

                        boundary.OuterBoundary = outerPolygon;
                        _boundaryFileService.SaveBoundary(boundary, fieldPath);

                        // NOTE: Do NOT overwrite the field origin - it should stay constant!

                        SetCurrentBoundary(boundary);
                        CenterMapOnBoundary(boundary);
                        RefreshBoundaryList();
                    }

                    if (result.HasBackgroundImage && !string.IsNullOrEmpty(result.BackgroundImagePath))
                    {
                        // AgShare downloads don't have Mercator bounds - use zeros (will fall back to linear sampling)
                        SaveBackgroundImage(result.BackgroundImagePath, fieldPath,
                            result.NorthWestLat, result.NorthWestLon,
                            result.SouthEastLat, result.SouthEastLon,
                            0, 0, 0, 0);
                    }

                    var msgParts = new System.Collections.Generic.List<string>();
                    if (result.BoundaryPoints.Count >= 3)
                        msgParts.Add($"boundary ({result.BoundaryPoints.Count} pts)");
                    if (result.HasBackgroundImage)
                        msgParts.Add("background image");

                    StatusMessage = $"Imported from satellite map: {string.Join(" + ", msgParts)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error importing: {ex.Message}";
                }
            }
        });

        BuildFromTracksCommand = ReactiveCommand.Create(() =>
        {
            StatusMessage = "Build boundary from tracks not yet implemented";
        });

        DriveAroundFieldCommand = ReactiveCommand.Create(() =>
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
