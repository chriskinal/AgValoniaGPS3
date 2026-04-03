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
    private int _previousFixQuality;

    private double _easting;
    private double _northing;
    private double _heading;
    private double _rollDegrees;

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

    public double RollDegrees
    {
        get => _rollDegrees;
        set => this.RaiseAndSetIfChanged(ref _rollDegrees, value);
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

        // RTK quality change sounds
        if (data.FixQuality != _previousFixQuality)
        {
            bool wasRtk = _previousFixQuality >= 4;
            bool isRtk = data.FixQuality >= 4;
            if (wasRtk && !isRtk)
                _audioService.Play(Services.Interfaces.SoundEffect.RtkLost);
            else if (!wasRtk && isRtk)
                _audioService.Play(Services.Interfaces.SoundEffect.RtkRecovered);
            _previousFixQuality = data.FixQuality;
        }

        // Convert WGS84 to local coordinates for display
        // In simulator mode, this is already done. In real GPS mode, GpsData only has lat/lon.
        double posEasting = data.CurrentPosition.Easting;
        double posNorthing = data.CurrentPosition.Northing;

        // If easting/northing are zero (real GPS path), convert from lat/lon
        if (Math.Abs(posEasting) < 0.001 && Math.Abs(posNorthing) < 0.001
            && Math.Abs(data.CurrentPosition.Latitude) > 0.001)
        {
            var localPlane = State.Field.LocalPlane;
            if (localPlane != null)
            {
                var geoCoord = localPlane.ConvertWgs84ToGeoCoord(
                    new Models.Wgs84(data.CurrentPosition.Latitude, data.CurrentPosition.Longitude));
                posEasting = geoCoord.Easting;
                posNorthing = geoCoord.Northing;
            }
        }

        // Apply GPS drift compensation
        double driftedEasting = posEasting + State.Field.DriftEasting;
        double driftedNorthing = posNorthing + State.Field.DriftNorthing;
        double headingRad = data.CurrentPosition.Heading * Math.PI / 180.0;

        // Update tool/implement position from drifted vehicle position
        _toolPositionService.Update(
            new Models.Base.Vec3(driftedEasting, driftedNorthing, headingRad),
            headingRad);

        // Atomic map update: vehicle + tool + hitch in one call
        // Prevents rendering mismatches between vehicle and tool positions
        var hitchPos = _toolPositionService.HitchPosition;
        var toolPos = _toolPositionService.ToolPosition;
        _mapService.SetAllPositions(
            driftedEasting, driftedNorthing, headingRad,
            toolPos.Easting, toolPos.Northing, _toolPositionService.ToolHeading,
            ToolWidth, hitchPos.Easting, hitchPos.Northing,
            _toolPositionService.IsToolPositionReady);

        // Update properties for bindings (but map already updated atomically above)
        Easting = driftedEasting;
        Northing = driftedNorthing;
        Heading = data.CurrentPosition.Heading;
        RollDegrees = Models.Configuration.SensorState.Instance.ImuRoll;

        // Update reverse indicator on map
        _mapService.SetReversing(IsReversing);

        // Auto-initialize coverage bounds from GPS if no boundary exists (#138)
        // Use posEasting/posNorthing (un-drifted local coords, works for both simulator and real GPS)
        EnsureCoverageBoundsInitialized(posEasting, posNorthing);

        // Add boundary point if recording is active
        // Use un-drifted local coords so boundary is in field-fixed coordinates
        if (_boundaryRecordingService.IsRecording)
        {
            var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(
                posEasting, posNorthing, headingRad);
            _boundaryRecordingService.AddPoint(offsetEasting, offsetNorthing, headingRad);
        }

        // Add curve point if curve recording is active
        if (CurrentABCreationMode == ABCreationMode.Curve)
        {
            AddCurvePoint(posEasting, posNorthing, data.CurrentPosition.Heading);
        }

        // Add contour point if contour recording is active
        if (IsRecordingContour)
        {
            AddContourPoint(posEasting, posNorthing, data.CurrentPosition.Heading);
        }

        // Update headland proximity distance for HUD readout
        UpdateHeadlandProximity(data.CurrentPosition);

        // Auto-select closest track when autosteer is not engaged (#143)
        UpdateAutoTrackSelection(data.CurrentPosition);

        // Run guidance when using real GPS (simulator path has its own guidance call).
        // Without this, autosteer has no steering output when the simulator is disabled.
        // Must use drifted local coordinates, not raw NMEA (which has easting=0).
        if (!IsSimulatorEnabled && IsAutoSteerEngaged && HasActiveTrack)
        {
            var guidancePos = data.CurrentPosition with
            {
                Easting = driftedEasting,
                Northing = driftedNorthing
            };
            CalculateAutoSteerGuidance(guidancePos);
        }
    }

    private bool _isAutoTrackEnabled = true;
    public bool IsAutoTrackEnabled
    {
        get => _isAutoTrackEnabled;
        set => this.RaiseAndSetIfChanged(ref _isAutoTrackEnabled, value);
    }

    private DateTime _lastAutoTrackTime = DateTime.MinValue;
    private const double AUTO_TRACK_INTERVAL_SECONDS = 3.0;

    /// <summary>
    /// Auto-select closest track when autosteer is not engaged.
    /// Matches legacy: 3-second debounce, heading alignment, visibility filter.
    /// </summary>
    private void UpdateAutoTrackSelection(AgValoniaGPS.Models.Position position)
    {
        if (!_isAutoTrackEnabled || IsAutoSteerEngaged)
            return;

        var tracks = State.Field.Tracks;
        if (tracks.Count == 0)
            return;

        // 3-second debounce
        var now = DateTime.UtcNow;
        if ((now - _lastAutoTrackTime).TotalSeconds < AUTO_TRACK_INTERVAL_SECONDS)
            return;
        _lastAutoTrackTime = now;

        double headingRadians = position.Heading * Math.PI / 180.0;
        var vehiclePos = new Models.Base.Vec2(position.Easting, position.Northing);

        var closest = Services.Track.AutoTrackSelectionService.FindClosestTrack(
            tracks, vehiclePos, headingRadians);

        if (closest != null && closest != SelectedTrack)
        {
            SelectedTrack = closest;
        }
    }

    private bool _autoCoverageBoundsInitialized;

    /// <summary>
    /// Auto-initialize coverage bounds from GPS position when no boundary exists.
    /// Creates a 500m x 500m area centered on current position. Only runs once per field session.
    /// </summary>
    private void EnsureCoverageBoundsInitialized(double easting, double northing)
    {
        if (_coverageMapService.IsFieldBoundsSet || _autoCoverageBoundsInitialized)
            return;

        // Only auto-init if we have a valid position (not at origin)
        if (Math.Abs(easting) < 0.1 && Math.Abs(northing) < 0.1)
            return;

        _autoCoverageBoundsInitialized = true;

        const double halfSize = 250.0; // 500m x 500m default area
        _coverageMapService.SetFieldBoundsFromPosition(easting, northing, halfSize);

        // Also initialize the display bitmap
        _mapService.InitializeCoverageBitmapWithBounds(
            easting - halfSize, easting + halfSize,
            northing - halfSize, northing + halfSize);
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

        bool wasWarning = State.Field.HeadlandProximityWarning;
        State.Field.HeadlandProximityDistance = output.HeadlandDistance;
        State.Field.HeadlandProximityWarning = output.ShouldTriggerWarning;

        // Play headland alarm on warning transition (not every frame)
        if (output.ShouldTriggerWarning && !wasWarning)
        {
            _audioService.Play(AgValoniaGPS.Services.Interfaces.SoundEffect.Headland);
        }
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
