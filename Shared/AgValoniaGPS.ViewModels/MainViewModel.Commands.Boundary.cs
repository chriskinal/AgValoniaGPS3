using System;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.GPS;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Boundary commands - boundary recording, headland management, boundary map dialog
/// </summary>
public partial class MainViewModel
{
    // Boundary Panel Commands
    public ICommand? ToggleBoundaryPanelCommand { get; private set; }
    public ICommand? ShowBoundaryDialogCommand { get; private set; }

    // Boundary Recording Commands
    public ICommand? StartBoundaryRecordingCommand { get; private set; }
    public ICommand? PauseBoundaryRecordingCommand { get; private set; }
    public ICommand? StopBoundaryRecordingCommand { get; private set; }
    public ICommand? UndoBoundaryPointCommand { get; private set; }
    public ICommand? ClearBoundaryCommand { get; private set; }
    public ICommand? AddBoundaryPointCommand { get; private set; }
    public ICommand? ToggleRecordingCommand { get; private set; }
    public ICommand? ToggleBoundaryLeftRightCommand { get; private set; }
    public ICommand? ToggleBoundaryAntennaToolCommand { get; private set; }
    public ICommand? ShowBoundaryOffsetDialogCommand { get; private set; }

    // Boundary Management Commands
    public ICommand? DeleteBoundaryCommand { get; private set; }
    public ICommand? ImportKmlBoundaryCommand { get; private set; }
    public ICommand? DrawMapBoundaryCommand { get; private set; }
    public ICommand? DrawMapBoundaryDesktopCommand { get; private set; }
    public ICommand? BuildFromTracksCommand { get; private set; }
    public ICommand? DriveAroundFieldCommand { get; private set; }

    // Boundary Map Dialog Commands
    public ICommand? ShowBoundaryMapDialogCommand { get; private set; }
    public ICommand? CancelBoundaryMapDialogCommand { get; private set; }
    public ICommand? ConfirmBoundaryMapDialogCommand { get; private set; }

    // Headland Commands
    public ICommand? ShowHeadlandBuilderCommand { get; private set; }
    public ICommand? ToggleHeadlandCommand { get; private set; }
    public ICommand? ToggleSectionInHeadlandCommand { get; private set; }
    public ICommand? ResetToolHeadingCommand { get; private set; }
    public ICommand? BuildHeadlandCommand { get; private set; }
    public ICommand? ClearHeadlandCommand { get; private set; }
    public ICommand? CloseHeadlandBuilderCommand { get; private set; }
    public ICommand? SetHeadlandToToolWidthCommand { get; private set; }
    public ICommand? PreviewHeadlandCommand { get; private set; }
    public ICommand? IncrementHeadlandDistanceCommand { get; private set; }
    public ICommand? DecrementHeadlandDistanceCommand { get; private set; }
    public ICommand? IncrementHeadlandPassesCommand { get; private set; }
    public ICommand? DecrementHeadlandPassesCommand { get; private set; }

    // Headland Dialog (FormHeadLine) Commands
    public ICommand? ShowHeadlandDialogCommand { get; private set; }
    public ICommand? CloseHeadlandDialogCommand { get; private set; }
    public ICommand? ExtendHeadlandACommand { get; private set; }
    public ICommand? ExtendHeadlandBCommand { get; private set; }
    public ICommand? ShrinkHeadlandACommand { get; private set; }
    public ICommand? ShrinkHeadlandBCommand { get; private set; }
    public ICommand? ResetHeadlandCommand { get; private set; }
    public ICommand? ClipHeadlandLineCommand { get; private set; }
    public ICommand? UndoHeadlandCommand { get; private set; }
    public ICommand? TurnOffHeadlandCommand { get; private set; }

    private void InitializeBoundaryCommands()
    {
        // Boundary Panel Commands
        ToggleBoundaryPanelCommand = new RelayCommand(() =>
        {
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        ShowBoundaryDialogCommand = new RelayCommand(() =>
        {
            // Toggle boundary panel visibility - this shows the panel where user can
            // choose how to create the boundary (KML import, drive around, etc.)
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        // Boundary Recording Commands
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
                // Save to current field
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

            // Hide the player panel
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
            // Show numeric input dialog for boundary offset
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

        // Boundary Management Commands
        DeleteBoundaryCommand = new RelayCommand(DeleteSelectedBoundary);

        ImportKmlBoundaryCommand = new AsyncRelayCommand(async () =>
        {
            // Must have a field open
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
                    // Load existing boundary or create new one
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

                    // Convert WGS84 boundary points to local coordinates
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

                    // Save boundary
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // Update map
                    SetCurrentBoundary(boundary);

                    // Refresh the boundary list
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
            // Must have a field open
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first to add boundary";
                return;
            }

            // Use the shared panel-based dialog (works on iOS and Desktop)
            ShowBoundaryMapDialogCommand?.Execute(null);
        });

        // Keep Desktop-only async version for IDialogService integration
        DrawMapBoundaryDesktopCommand = new AsyncRelayCommand(async () =>
        {
            // Must have a field open
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
                        // Calculate center of boundary points
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
                        // Use background image center as origin
                        double centerLat = (result.NorthWestLat + result.SouthEastLat) / 2;
                        double centerLon = (result.NorthWestLon + result.SouthEastLon) / 2;

                        var origin = new Wgs84(centerLat, centerLon);
                        var sharedProps = new SharedFieldProperties();
                        localPlane = new LocalPlane(origin, sharedProps);
                    }

                    // Process boundary points if present
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

                        // Save boundary
                        _boundaryFileService.SaveBoundary(boundary, fieldPath);

                        // Update Field.txt with the origin used for this boundary
                        double originLat = localPlane.Origin.Latitude;
                        double originLon = localPlane.Origin.Longitude;
                        _fieldOriginLatitude = originLat;
                        _fieldOriginLongitude = originLon;
                        try
                        {
                            var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                            fieldInfo.Origin = new Position { Latitude = originLat, Longitude = originLon };
                            _fieldPlaneFileService.SaveField(fieldInfo, fieldPath);
                            _logger.LogDebug($"[MapBoundary] Updated Field.txt origin to ({originLat:F8}, {originLon:F8})");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug($"[MapBoundary] Could not update Field.txt: {ex.Message}");
                        }

                        // Update map
                        SetCurrentBoundary(boundary);
                        CenterMapOnBoundary(boundary);

                        // Refresh the boundary list
                        RefreshBoundaryList();
                    }

                    // Process background image if present
                    if (result.HasBackgroundImage && !string.IsNullOrEmpty(result.BackgroundImagePath))
                    {
                        SaveBackgroundImage(result.BackgroundImagePath, fieldPath,
                            result.NorthWestLat, result.NorthWestLon,
                            result.SouthEastLat, result.SouthEastLon);
                    }

                    // Build status message
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
            // Must have a field open
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before recording a boundary";
                return;
            }

            // Hide boundary panel, show the player panel
            IsBoundaryPanelVisible = false;
            IsBoundaryPlayerPanelVisible = true;

            // Initialize recording service for a new boundary (paused state)
            _boundaryRecordingService.StartRecording(BoundaryType.Outer);
            _boundaryRecordingService.PauseRecording();

            StatusMessage = "Drive around the field boundary. Click Record to start.";
        });

        // Boundary Map Dialog Commands
        ShowBoundaryMapDialogCommand = new RelayCommand(() =>
        {
            // Set center: prefer field origin, then GPS position, then 0,0
            if (_fieldOriginLatitude != 0 || _fieldOriginLongitude != 0)
            {
                // Use field origin from Field.txt
                BoundaryMapCenterLatitude = _fieldOriginLatitude;
                BoundaryMapCenterLongitude = _fieldOriginLongitude;
            }
            else if (Latitude != 0 || Longitude != 0)
            {
                // Fall back to current GPS position
                BoundaryMapCenterLatitude = Latitude;
                BoundaryMapCenterLongitude = Longitude;
            }
            // else: leave as 0,0 (will show default location)

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
            _logger.LogDebug($"[BoundaryMap] ConfirmBoundaryMapDialogCommand called");
            _logger.LogDebug($"[BoundaryMap] Points: {BoundaryMapResultPoints.Count}, IsFieldOpen: {IsFieldOpen}, CurrentFieldName: {CurrentFieldName}");

            if (BoundaryMapResultPoints.Count >= 3 && IsFieldOpen && !string.IsNullOrEmpty(CurrentFieldName))
            {
                try
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                    _logger.LogDebug($"[BoundaryMap] Field path: {fieldPath}");
                    _logger.LogDebug($"[BoundaryMap] Directory exists: {Directory.Exists(fieldPath)}");

                    // Load existing boundary or create new one
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

                    // Calculate origin for LocalPlane
                    // IMPORTANT: If we have a background image, use its center as the origin
                    // This ensures the boundary aligns with landmarks in the image
                    // The user drew the boundary on specific landmarks in the viewport,
                    // so we need to use the same reference point for both
                    double centerLat, centerLon;
                    if (!string.IsNullOrEmpty(BoundaryMapResultBackgroundPath))
                    {
                        // Use image (viewport) center as origin - this is where the user was looking when drawing
                        centerLat = (BoundaryMapResultNwLat + BoundaryMapResultSeLat) / 2;
                        centerLon = (BoundaryMapResultNwLon + BoundaryMapResultSeLon) / 2;
                        _logger.LogDebug($"[BoundaryMap] Using image center as origin: ({centerLat:F8}, {centerLon:F8})");
                    }
                    else
                    {
                        // No background image - use boundary center as origin
                        centerLat = BoundaryMapResultPoints.Average(p => p.Latitude);
                        centerLon = BoundaryMapResultPoints.Average(p => p.Longitude);
                        _logger.LogDebug($"[BoundaryMap] Using boundary center as origin: ({centerLat:F8}, {centerLon:F8})");
                    }

                    // Convert WGS84 boundary points to local coordinates
                    var origin = new Wgs84(centerLat, centerLon);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();

                    _logger.LogDebug($"[BoundaryMap] Converting boundary points with origin ({centerLat:F8}, {centerLon:F8})");
                    foreach (var (lat, lon) in BoundaryMapResultPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                        _logger.LogDebug($"[BoundaryMap]   WGS84 ({lat:F8}, {lon:F8}) -> Local E={geoCoord.Easting:F1}, N={geoCoord.Northing:F1}");
                    }

                    boundary.OuterBoundary = outerPolygon;

                    // Save boundary
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // Update Field.txt with the origin used for this boundary
                    // This ensures background images load with the same coordinate system
                    _fieldOriginLatitude = centerLat;
                    _fieldOriginLongitude = centerLon;
                    try
                    {
                        var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                        fieldInfo.Origin = new Position { Latitude = centerLat, Longitude = centerLon };
                        _fieldPlaneFileService.SaveField(fieldInfo, fieldPath);
                        _logger.LogDebug($"[BoundaryMap] Updated Field.txt origin to ({centerLat:F8}, {centerLon:F8})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"[BoundaryMap] Could not update Field.txt: {ex.Message}");
                    }

                    // Update map
                    SetCurrentBoundary(boundary);

                    // Center camera on the boundary and set appropriate zoom
                    if (outerPolygon.Points.Count > 0)
                    {
                        // Calculate boundary center and extent
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
                        double extentE = maxE - minE;
                        double extentN = maxN - minN;
                        double maxExtent = Math.Max(extentE, extentN);

                        // Pan to center
                        _mapService.PanTo(centerE, centerN);

                        // Calculate zoom to fit boundary (viewHeight = 200/zoom, so zoom = 200/viewHeight)
                        // Add 20% padding
                        double desiredView = maxExtent * 1.2;
                        if (desiredView > 0)
                        {
                            double newZoom = 200.0 / desiredView;
                            newZoom = Math.Clamp(newZoom, 0.1, 10.0);
                            _mapService.SetCamera(centerE, centerN, newZoom, 0);
                        }

                        _logger.LogDebug($"[BoundaryMap] Saved boundary with {outerPolygon.Points.Count} points");
                        _logger.LogDebug($"[BoundaryMap] Center: ({centerE:F1}, {centerN:F1}), Extent: {maxExtent:F1}m");
                    }

                    // Handle background image if captured
                    if (!string.IsNullOrEmpty(BoundaryMapResultBackgroundPath))
                    {
                        SaveBackgroundImage(BoundaryMapResultBackgroundPath, fieldPath,
                            BoundaryMapResultNwLat, BoundaryMapResultNwLon,
                            BoundaryMapResultSeLat, BoundaryMapResultSeLon);
                    }

                    // Refresh the boundary list
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

        // Headland Commands
        ShowHeadlandBuilderCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen)
            {
                StatusMessage = "Open a field first";
                return;
            }
            State.UI.ShowDialog(DialogType.HeadlandBuilder);
            // Trigger initial preview
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
            // Reset tool heading to be directly behind tractor
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
            // Set headland distance to implement width (use actual width from sections)
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
            // TODO: Extend headland at point A
            StatusMessage = "Extend A - not yet implemented";
        });

        ExtendHeadlandBCommand = new RelayCommand(() =>
        {
            // TODO: Extend headland at point B
            StatusMessage = "Extend B - not yet implemented";
        });

        ShrinkHeadlandACommand = new RelayCommand(() =>
        {
            // TODO: Shrink headland at point A
            StatusMessage = "Shrink A - not yet implemented";
        });

        ShrinkHeadlandBCommand = new RelayCommand(() =>
        {
            // TODO: Shrink headland at point B
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

            // Check if we have a headland to clip (either built headland or preview)
            var headlandToClip = CurrentHeadlandLine ?? ConvertPreviewToVec3(HeadlandPreviewLine);
            if (headlandToClip == null || headlandToClip.Count < 3)
            {
                StatusMessage = "No headland to clip - use Build first";
                return;
            }

            // Clip the headland using the clip line (between the two selected points)
            ClipHeadlandAtLine(headlandToClip);
        });

        UndoHeadlandCommand = new RelayCommand(() =>
        {
            // TODO: Undo headland changes
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
    }
}
