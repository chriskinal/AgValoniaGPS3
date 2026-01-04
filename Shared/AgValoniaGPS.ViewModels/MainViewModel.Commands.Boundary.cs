using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
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

                    double centerLat, centerLon;
                    if (!string.IsNullOrEmpty(BoundaryMapResultBackgroundPath))
                    {
                        centerLat = (BoundaryMapResultNwLat + BoundaryMapResultSeLat) / 2;
                        centerLon = (BoundaryMapResultNwLon + BoundaryMapResultSeLon) / 2;
                    }
                    else
                    {
                        centerLat = BoundaryMapResultPoints.Average(p => p.Latitude);
                        centerLon = BoundaryMapResultPoints.Average(p => p.Longitude);
                    }

                    var origin = new Wgs84(centerLat, centerLon);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();
                    foreach (var (lat, lon) in BoundaryMapResultPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                    }

                    boundary.OuterBoundary = outerPolygon;
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    _fieldOriginLatitude = centerLat;
                    _fieldOriginLongitude = centerLon;
                    try
                    {
                        var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                        fieldInfo.Origin = new Position { Latitude = centerLat, Longitude = centerLon };
                        _fieldPlaneFileService.SaveField(fieldInfo, fieldPath);
                    }
                    catch { }

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
                            BoundaryMapResultSeLat, BoundaryMapResultSeLon);
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
            State.UI.ShowDialog(DialogType.HeadlandBuilder);
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
            StatusMessage = "Tool heading reset";
        });

        BuildHeadlandCommand = new RelayCommand(() =>
        {
            BuildHeadlandFromBoundary();
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

        // Headland Dialog (FormHeadLine) commands
        ShowHeadlandDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.Headland);
            UpdateHeadlandPreview();
        });

        CloseHeadlandDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            HeadlandPreviewLine = null;
        });

        ExtendHeadlandACommand = new RelayCommand(() =>
        {
            StatusMessage = "Extend A - not yet implemented";
        });

        ExtendHeadlandBCommand = new RelayCommand(() =>
        {
            StatusMessage = "Extend B - not yet implemented";
        });

        ShrinkHeadlandACommand = new RelayCommand(() =>
        {
            StatusMessage = "Shrink A - not yet implemented";
        });

        ShrinkHeadlandBCommand = new RelayCommand(() =>
        {
            StatusMessage = "Shrink B - not yet implemented";
        });

        ResetHeadlandCommand = new RelayCommand(() =>
        {
            ClearHeadlandCommand?.Execute(null);
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
            StatusMessage = "Undo - not yet implemented";
        });

        TurnOffHeadlandCommand = new RelayCommand(() =>
        {
            IsHeadlandOn = false;
            HasHeadland = false;
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
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
            State.UI.CloseDialog();
            _confirmationDialogCallback = null;
        });

        ConfirmConfirmationDialogCommand = new RelayCommand(() =>
        {
            var callback = _confirmationDialogCallback;
            State.UI.CloseDialog();
            _confirmationDialogCallback = null;
            callback?.Invoke();
        });

        DeleteBoundaryCommand = new RelayCommand(DeleteSelectedBoundary);

        ImportKmlBoundaryCommand = new AsyncRelayCommand(async () =>
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

        DrawMapBoundaryCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first to add boundary";
                return;
            }
            ShowBoundaryMapDialogCommand?.Execute(null);
        });

        DrawMapBoundaryDesktopCommand = new AsyncRelayCommand(async () =>
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
                    LocalPlane? localPlane = null;

                    if (result.BoundaryPoints.Count >= 3)
                    {
                        double sumLat = 0, sumLon = 0;
                        foreach (var point in result.BoundaryPoints)
                        {
                            sumLat += point.Latitude;
                            sumLon += point.Longitude;
                        }
                        double centerLat = sumLat / result.BoundaryPoints.Count;
                        double centerLon = sumLon / result.BoundaryPoints.Count;

                        var origin = new Wgs84(centerLat, centerLon);
                        var sharedProps = new SharedFieldProperties();
                        localPlane = new LocalPlane(origin, sharedProps);
                    }
                    else if (result.HasBackgroundImage)
                    {
                        double centerLat = (result.NorthWestLat + result.SouthEastLat) / 2;
                        double centerLon = (result.NorthWestLon + result.SouthEastLon) / 2;

                        var origin = new Wgs84(centerLat, centerLon);
                        var sharedProps = new SharedFieldProperties();
                        localPlane = new LocalPlane(origin, sharedProps);
                    }

                    if (result.BoundaryPoints.Count >= 3 && localPlane != null)
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

                        double originLat = localPlane.Origin.Latitude;
                        double originLon = localPlane.Origin.Longitude;
                        _fieldOriginLatitude = originLat;
                        _fieldOriginLongitude = originLon;
                        try
                        {
                            var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                            fieldInfo.Origin = new Position { Latitude = originLat, Longitude = originLon };
                            _fieldPlaneFileService.SaveField(fieldInfo, fieldPath);
                        }
                        catch { }

                        SetCurrentBoundary(boundary);
                        CenterMapOnBoundary(boundary);
                        RefreshBoundaryList();
                    }

                    if (result.HasBackgroundImage && !string.IsNullOrEmpty(result.BackgroundImagePath))
                    {
                        SaveBackgroundImage(result.BackgroundImagePath, fieldPath,
                            result.NorthWestLat, result.NorthWestLon,
                            result.SouthEastLat, result.SouthEastLon);
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
