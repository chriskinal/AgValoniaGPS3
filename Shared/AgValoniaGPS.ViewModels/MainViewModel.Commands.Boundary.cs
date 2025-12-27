using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing boundary and headland command initialization.
/// </summary>
public partial class MainViewModel
{
    private void InitializeBoundaryCommands()
    {
        // Boundary Panel toggle
        ShowBoundaryDialogCommand = new RelayCommand(() =>
        {
            // Toggle boundary panel visibility - this shows the panel where user can
            // choose how to create the boundary (KML import, drive around, etc.)
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        ToggleBoundaryPanelCommand = new RelayCommand(() =>
        {
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
            if (IsBoundaryPanelVisible)
            {
                // Refresh boundary list when panel opens
                RefreshBoundaryList();
            }
        });

        // Boundary Recording Commands
        StartBoundaryRecordingCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.StartRecording(BoundaryType.Outer);
            StatusMessage = "Boundary recording started";
        });

        PauseBoundaryRecordingCommand = new RelayCommand(() =>
        {
            if (_boundaryRecordingService.IsRecording)
            {
                _boundaryRecordingService.PauseRecording();
                StatusMessage = "Boundary recording paused";
            }
        });

        StopBoundaryRecordingCommand = new RelayCommand(() =>
        {
            if (!_boundaryRecordingService.IsRecording)
            {
                StatusMessage = "No recording in progress";
                return;
            }

            var boundary = _boundaryRecordingService.FinishRecording();
            if (boundary != null && IsFieldOpen && !string.IsNullOrEmpty(CurrentFieldName))
            {
                var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                _boundaryFileService.SaveBoundary(boundary, fieldPath);
                SetCurrentBoundary(boundary);
                RefreshBoundaryList();
                StatusMessage = $"Boundary saved with {boundary.OuterBoundary?.Points.Count ?? 0} points";
            }
            else
            {
                StatusMessage = "Recording finished but no boundary created";
            }

            IsBoundaryPlayerPanelVisible = false;
        });

        UndoBoundaryPointCommand = new RelayCommand(() =>
        {
            // TODO: Implement undo last point
            StatusMessage = "Undo point - not yet implemented";
        });

        ClearBoundaryCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.ClearPoints();
            StatusMessage = "Boundary points cleared";
        });

        AddBoundaryPointCommand = new RelayCommand(() =>
        {
            if (_boundaryRecordingService.IsRecording)
            {
                // Add point at current GPS position
                double headingRadians = Heading * Math.PI / 180.0;
                var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(Easting, Northing, headingRadians);
                _boundaryRecordingService.AddPoint(offsetEasting, offsetNorthing, headingRadians);
                StatusMessage = $"Point added ({_boundaryRecordingService.PointCount} total)";
            }
        });

        DeleteBoundaryCommand = new RelayCommand(() =>
        {
            DeleteSelectedBoundary();
        });

        ImportKmlBoundaryCommand = new RelayCommand(() =>
        {
            StatusMessage = "Import KML boundary - use the KML Import dialog";
        });

        // Boundary Map Dialog Commands (for satellite map boundary drawing)
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
            Console.WriteLine($"[BoundaryMap] ConfirmBoundaryMapDialogCommand called");
            Console.WriteLine($"[BoundaryMap] Points: {BoundaryMapResultPoints.Count}, IsFieldOpen: {IsFieldOpen}, CurrentFieldName: {CurrentFieldName}");

            if (BoundaryMapResultPoints.Count >= 3 && IsFieldOpen && !string.IsNullOrEmpty(CurrentFieldName))
            {
                try
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                    Console.WriteLine($"[BoundaryMap] Field path: {fieldPath}");
                    Console.WriteLine($"[BoundaryMap] Directory exists: {Directory.Exists(fieldPath)}");

                    // Load existing boundary or create new one
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

                    // Calculate origin for LocalPlane
                    double centerLat, centerLon;
                    if (!string.IsNullOrEmpty(BoundaryMapResultBackgroundPath))
                    {
                        // Use image (viewport) center as origin
                        centerLat = (BoundaryMapResultNwLat + BoundaryMapResultSeLat) / 2;
                        centerLon = (BoundaryMapResultNwLon + BoundaryMapResultSeLon) / 2;
                        Console.WriteLine($"[BoundaryMap] Using image center as origin: ({centerLat:F8}, {centerLon:F8})");
                    }
                    else
                    {
                        // No background image - use boundary center as origin
                        centerLat = BoundaryMapResultPoints.Average(p => p.Latitude);
                        centerLon = BoundaryMapResultPoints.Average(p => p.Longitude);
                        Console.WriteLine($"[BoundaryMap] Using boundary center as origin: ({centerLat:F8}, {centerLon:F8})");
                    }

                    // Convert WGS84 boundary points to local coordinates
                    var origin = new Wgs84(centerLat, centerLon);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();

                    Console.WriteLine($"[BoundaryMap] Converting boundary points with origin ({centerLat:F8}, {centerLon:F8})");
                    foreach (var (lat, lon) in BoundaryMapResultPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                        Console.WriteLine($"[BoundaryMap]   WGS84 ({lat:F8}, {lon:F8}) -> Local E={geoCoord.Easting:F1}, N={geoCoord.Northing:F1}");
                    }

                    boundary.OuterBoundary = outerPolygon;

                    // Save boundary
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // Update Field.txt with the origin used for this boundary
                    _fieldOriginLatitude = centerLat;
                    _fieldOriginLongitude = centerLon;
                    try
                    {
                        var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                        fieldInfo.Origin = new Position { Latitude = centerLat, Longitude = centerLon };
                        _fieldPlaneFileService.SaveField(fieldInfo, fieldPath);
                        Console.WriteLine($"[BoundaryMap] Updated Field.txt origin to ({centerLat:F8}, {centerLon:F8})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BoundaryMap] Could not update Field.txt: {ex.Message}");
                    }

                    // Update map
                    SetCurrentBoundary(boundary);

                    // Center camera on the boundary and set appropriate zoom
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
                        double extentE = maxE - minE;
                        double extentN = maxN - minN;
                        double maxExtent = Math.Max(extentE, extentN);

                        _mapService.PanTo(centerE, centerN);

                        double desiredView = maxExtent * 1.2;
                        if (desiredView > 0)
                        {
                            double newZoom = 200.0 / desiredView;
                            newZoom = Math.Clamp(newZoom, 0.1, 10.0);
                            _mapService.SetCamera(centerE, centerN, newZoom, 0);
                        }

                        Console.WriteLine($"[BoundaryMap] Saved boundary with {outerPolygon.Points.Count} points");
                        Console.WriteLine($"[BoundaryMap] Center: ({centerE:F1}, {centerN:F1}), Extent: {maxExtent:F1}m");
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

        DrawMapBoundaryCommand = new RelayCommand(() =>
        {
            // Must have a field open
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before drawing a boundary";
                return;
            }

            // Show the boundary map dialog for iOS (web-based map)
            ShowBoundaryMapDialogCommand?.Execute(null);
        });

        DrawMapBoundaryDesktopCommand = new RelayCommand(async () =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before drawing a boundary";
                return;
            }

            IsBoundaryPanelVisible = false;

            var centerLat = _fieldOriginLatitude != 0 ? _fieldOriginLatitude : (Latitude != 0 ? Latitude : 40.7128);
            var centerLon = _fieldOriginLongitude != 0 ? _fieldOriginLongitude : (Longitude != 0 ? Longitude : -74.0060);

            try
            {
                var result = await _dialogService.ShowBoundaryMapDialogAsync(centerLat, centerLon);

                if (result?.BoundaryPoints != null && result.BoundaryPoints.Count >= 3)
                {
                    var fieldsDir = _settingsService.Settings.FieldsDirectory;
                    if (string.IsNullOrWhiteSpace(fieldsDir))
                    {
                        fieldsDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "AgValoniaGPS", "Fields");
                    }

                    var fieldPath = Path.Combine(fieldsDir, CurrentFieldName);
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

                    // Use same origin as the map
                    _fieldOriginLatitude = centerLat;
                    _fieldOriginLongitude = centerLon;
                    var origin = new Wgs84(centerLat, centerLon);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();
                    foreach (var pt in result.BoundaryPoints)
                    {
                        var wgs84 = new Wgs84(pt.Latitude, pt.Longitude);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                    }

                    boundary.OuterBoundary = outerPolygon;
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // Update map
                    SetCurrentBoundary(boundary);
                    CenterMapOnBoundary(boundary);

                    // Refresh the boundary list
                    RefreshBoundaryList();
                }

                // Process background image if present
                if (result?.HasBackgroundImage == true && !string.IsNullOrEmpty(result.BackgroundImagePath))
                {
                    var fieldsDir = _settingsService.Settings.FieldsDirectory;
                    if (string.IsNullOrWhiteSpace(fieldsDir))
                    {
                        fieldsDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "AgValoniaGPS", "Fields");
                    }
                    var fieldPath = Path.Combine(fieldsDir, CurrentFieldName);

                    SaveBackgroundImage(result.BackgroundImagePath, fieldPath,
                        result.NorthWestLat, result.NorthWestLon,
                        result.SouthEastLat, result.SouthEastLon);
                }

                // Build status message
                if (result != null)
                {
                    var msgParts = new System.Collections.Generic.List<string>();
                    if (result.BoundaryPoints?.Count >= 3)
                        msgParts.Add($"boundary ({result.BoundaryPoints.Count} pts)");
                    if (result.HasBackgroundImage)
                        msgParts.Add("background image");

                    StatusMessage = $"Imported from satellite map: {string.Join(" + ", msgParts)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing: {ex.Message}";
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

        // Recording player panel commands
        ToggleRecordingCommand = new RelayCommand(() =>
        {
            if (_boundaryRecordingService.IsPaused)
            {
                _boundaryRecordingService.ResumeRecording();
                StatusMessage = "Recording resumed";
            }
            else if (_boundaryRecordingService.IsRecording)
            {
                _boundaryRecordingService.PauseRecording();
                StatusMessage = "Recording paused";
            }
        });

        ToggleBoundaryLeftRightCommand = new RelayCommand(() =>
        {
            IsBoundaryOffsetRight = !IsBoundaryOffsetRight;
            StatusMessage = IsBoundaryOffsetRight ? "Boundary offset: RIGHT" : "Boundary offset: LEFT";
        });

        ToggleBoundaryAntennaToolCommand = new RelayCommand(() =>
        {
            IsBoundaryAntennaMode = !IsBoundaryAntennaMode;
            StatusMessage = IsBoundaryAntennaMode ? "Using ANTENNA position" : "Using TOOL position";
        });

        ShowBoundaryOffsetDialogCommand = new RelayCommand(() =>
        {
            // TODO: Show numeric input dialog for boundary offset
            StatusMessage = "Boundary offset dialog - not yet implemented";
        });

        InitializeHeadlandCommands();
    }

    private void InitializeHeadlandCommands()
    {
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
            // Set headland distance to implement width (use track width * 2 as approximation)
            HeadlandDistance = Vehicle.TrackWidth > 0 ? Vehicle.TrackWidth * 2 : 12.0;
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
