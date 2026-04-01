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
using ReactiveUI;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing GPS data handling.
/// Processes incoming GPS data and updates position/status properties.
/// </summary>
public partial class MainViewModel
{
    #region GPS Fields

    private double _latitude;
    private double _longitude;
    private double _speed;
    private int _satelliteCount;
    private string _fixQuality = "No Fix";

    private double _easting;
    private double _northing;
    private double _heading;

    #endregion

    #region GPS Properties

    public double Latitude
    {
        get => _latitude;
        set => this.RaiseAndSetIfChanged(ref _latitude, value);
    }

    public double Longitude
    {
        get => _longitude;
        set => this.RaiseAndSetIfChanged(ref _longitude, value);
    }

    public double Speed
    {
        get => _speed;
        set
        {
            this.RaiseAndSetIfChanged(ref _speed, value);
            this.RaisePropertyChanged(nameof(SpeedKmh));
            this.RaisePropertyChanged(nameof(IsReversing));
        }
    }

    /// <summary>True when the vehicle is moving backward (negative speed).</summary>
    public bool IsReversing => _speed < -0.1;

    /// <summary>Speed in km/h for display (absolute value).</summary>
    public double SpeedKmh => Math.Abs(_speed) * 3.6;

    public int SatelliteCount
    {
        get => _satelliteCount;
        set => this.RaiseAndSetIfChanged(ref _satelliteCount, value);
    }

    public string FixQuality
    {
        get => _fixQuality;
        set => this.RaiseAndSetIfChanged(ref _fixQuality, value);
    }

    public double Easting
    {
        get => _easting;
        set => this.RaiseAndSetIfChanged(ref _easting, value);
    }

    public double Northing
    {
        get => _northing;
        set => this.RaiseAndSetIfChanged(ref _northing, value);
    }

    public double Heading
    {
        get => _heading;
        set => this.RaiseAndSetIfChanged(ref _heading, value);
    }

    #endregion

    #region GPS Event Handlers

    private void OnGpsDataUpdated(object? sender, AgValoniaGPS.Models.GpsData data)
    {
        // Marshal to UI thread (use Invoke for synchronous execution to avoid modal dialog issues)
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread, execute directly
            UpdateGpsProperties(data);
        }
        else
        {
            // Not on UI thread, invoke synchronously
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateGpsProperties(data));
        }
    }

    private void UpdateGpsProperties(AgValoniaGPS.Models.GpsData data)
    {
        // Update centralized state (single source of truth)
        State.Vehicle.UpdateFromGps(
            data.CurrentPosition,
            data.FixQuality,
            data.SatellitesInUse,
            data.Hdop,
            data.DifferentialAge);

        // Legacy property updates (for existing bindings - will be removed in Phase 5)
        Latitude = data.CurrentPosition.Latitude;
        Longitude = data.CurrentPosition.Longitude;
        Speed = data.CurrentPosition.Speed;
        SatelliteCount = data.SatellitesInUse;
        FixQuality = GetFixQualityString(data.FixQuality);
        StatusMessage = data.IsValid ? "GPS Active" : "Waiting for GPS";

        // Update UTM coordinates and heading for map rendering
        Easting = data.CurrentPosition.Easting;
        Northing = data.CurrentPosition.Northing;
        Heading = data.CurrentPosition.Heading;

        // Update reverse indicator on map
        _mapService.SetReversing(IsReversing);

        // Add boundary point if recording is active
        if (_boundaryRecordingService.IsRecording)
        {
            double headingRadians = data.CurrentPosition.Heading * Math.PI / 180.0;
            var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(
                data.CurrentPosition.Easting,
                data.CurrentPosition.Northing,
                headingRadians);
            _boundaryRecordingService.AddPoint(offsetEasting, offsetNorthing, headingRadians);
        }

        // Add curve point if curve recording is active
        if (CurrentABCreationMode == ABCreationMode.Curve)
        {
            AddCurvePoint(data.CurrentPosition.Easting, data.CurrentPosition.Northing, data.CurrentPosition.Heading);
        }

        // Add contour point if contour recording is active
        if (IsRecordingContour)
        {
            AddContourPoint(data.CurrentPosition.Easting, data.CurrentPosition.Northing, data.CurrentPosition.Heading);
        }

        // Update headland proximity distance for HUD readout
        UpdateHeadlandProximity(data.CurrentPosition);
    }

    private static readonly AgValoniaGPS.Services.Headland.HeadlandDetectionService _headlandDetector = new();

    /// <summary>
    /// Calculate distance from tool pivot to nearest headland boundary line.
    /// Uses HeadlandDetectionService with direction-aware warnings, matching legacy AgOpenGPS.
    /// </summary>
    private void UpdateHeadlandProximity(AgValoniaGPS.Models.Position position)
    {
        var headlandLine = State.Field.HeadlandLine;
        if (headlandLine == null || headlandLine.Count < 3)
        {
            State.Field.HeadlandProximityDistance = null;
            State.Field.HeadlandProximityWarning = false;
            return;
        }

        // Use tool pivot position (implement hitch point), matching legacy mf.toolPivotPos
        var toolPivot = _toolPositionService.ToolPivotPosition;

        // Build minimal input for proximity calculation
        var input = new AgValoniaGPS.Models.Headland.HeadlandDetectionInput
        {
            IsHeadlandOn = true,
            VehiclePosition = toolPivot,
            Boundaries = new System.Collections.Generic.List<AgValoniaGPS.Models.Headland.BoundaryData>
            {
                new AgValoniaGPS.Models.Headland.BoundaryData
                {
                    HeadlandLine = new System.Collections.Generic.List<Models.Base.Vec3>(headlandLine)
                }
            }
        };

        var output = _headlandDetector.DetectHeadland(input);

        State.Field.HeadlandProximityDistance = output.HeadlandDistance;
        State.Field.HeadlandProximityWarning = output.ShouldTriggerWarning;
    }

    /// <summary>
    /// Add a point to the curve being recorded, with minimum spacing filtering.
    /// </summary>
    private void AddCurvePoint(double easting, double northing, double headingDegrees)
    {
        double headingRadians = headingDegrees * Math.PI / 180.0;

        // Check minimum spacing from last point
        if (_lastCurvePoint.HasValue)
        {
            double dx = easting - _lastCurvePoint.Value.Easting;
            double dy = northing - _lastCurvePoint.Value.Northing;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < CurveMinPointSpacing)
            {
                return; // Too close to last point, skip
            }
        }

        var point = new Models.Base.Vec3(easting, northing, headingRadians);
        _recordedCurvePoints.Add(point);
        _lastCurvePoint = point;

        // Update map display with recorded curve points
        var displayPoints = _recordedCurvePoints.Select(p => (p.Easting, p.Northing)).ToList();
        _mapService.SetRecordingPoints(displayPoints);

        // Update UI periodically (every 5 points to avoid excessive updates)
        if (_recordedCurvePoints.Count % 5 == 0)
        {
            this.RaisePropertyChanged(nameof(RecordedCurvePointCount));
            this.RaisePropertyChanged(nameof(ABCreationInstructions)); // Update instruction text with point count
            StatusMessage = $"Recording curve: {_recordedCurvePoints.Count} points";
        }
    }

    private static string GetFixQualityString(int fixQuality) => fixQuality switch
    {
        0 => "No Fix",
        1 => "GPS Fix",
        2 => "DGPS Fix",
        4 => "RTK Fixed",
        5 => "RTK Float",
        _ => "Unknown"
    };

    #endregion
}
