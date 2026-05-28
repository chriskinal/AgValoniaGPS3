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

using CommunityToolkit.Mvvm.ComponentModel;

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

    // PERF-05 Phase 2c #1/#2: latest snapshot values for status-display
    // properties that aren't on State.Vehicle. Each is written by its
    // upstream source at full source rate (GPS 10 Hz, control loop 100 Hz,
    // AutoSteer 100 Hz) and read by OnStatusTick at the unified 5 Hz status
    // cadence. Single-double writes/reads are atomic on x86/ARM; no lock
    // needed for diagnostic display values.
    private double _latestRollDegrees;
    private double _latestGpsToPgnLatencyMs;

    #endregion

    #region GPS Properties

    public double Latitude
    {
        get => _latitude;
        set => SetProperty(ref _latitude, value);
    }

    public double Longitude
    {
        get => _longitude;
        set => SetProperty(ref _longitude, value);
    }

    public double Speed
    {
        get => _speed;
        set
        {
            SetProperty(ref _speed, value);
            OnPropertyChanged(nameof(SpeedKmh));
            OnPropertyChanged(nameof(SpeedLargeValue));
            OnPropertyChanged(nameof(IsReversing));
        }
    }

    /// <summary>True when the vehicle is moving backward (negative speed).</summary>
    public bool IsReversing => _speed < -0.1;

    /// <summary>Speed in km/h for display (absolute value).</summary>
    public double SpeedKmh => Math.Abs(_speed) * 3.6;

    /// <summary>
    /// Speed in the user's preferred units (km/h when metric, mph when
    /// imperial). Used by the large speed slot in the top status strip.
    /// </summary>
    public double SpeedLargeValue =>
        Models.Configuration.ConfigurationStore.Instance.IsMetric
            ? SpeedKmh
            : SpeedKmh * 0.621371;

    /// <summary>Unit label that pairs with <see cref="SpeedLargeValue"/>.</summary>
    public string SpeedLargeUnit =>
        Models.Configuration.ConfigurationStore.Instance.IsMetric ? "km/h" : "mph";

    public int SatelliteCount
    {
        get => _satelliteCount;
        set => SetProperty(ref _satelliteCount, value);
    }

    public string FixQuality
    {
        get => _fixQuality;
        set => SetProperty(ref _fixQuality, value);
    }

    public double Easting
    {
        get => _easting;
        set => SetProperty(ref _easting, value);
    }

    public double Northing
    {
        get => _northing;
        set => SetProperty(ref _northing, value);
    }

    public double Heading
    {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    public double RollDegrees
    {
        get => _rollDegrees;
        set => SetProperty(ref _rollDegrees, value);
    }

    #endregion

    #region GPS Event Handlers

    private void OnGpsDataUpdated(object? sender, AgValoniaGPS.Models.GpsData data)
    {
        // The GpsPipelineService handles all heavy processing (tool position, guidance,
        // section control, coverage, boundary checks) on a background thread.
        // This handler only does lightweight UI-only work and user-action-driven recording.

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            HandleGpsUiUpdates(data);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleGpsUiUpdates(data));
        }
    }

    /// <summary>
    /// Lightweight GPS handler for UI-only work.
    /// Heavy processing (guidance, sections, coverage) is done by GpsPipelineService.
    /// </summary>
    private void HandleGpsUiUpdates(AgValoniaGPS.Models.GpsData data)
    {
        // State.Vehicle is now written exclusively by ApplyGpsCycleResult
        // (Phase B completion — Rule 2: only one State writer per domain).

        // Compute local coordinates for recording operations
        double posEasting = data.CurrentPosition.Easting;
        double posNorthing = data.CurrentPosition.Northing;

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

        double headingRad = data.CurrentPosition.Heading * Math.PI / 180.0;

        // Auto-initialize coverage bounds from GPS if no boundary exists (#138)
        EnsureCoverageBoundsInitialized(posEasting, posNorthing);

        // ── User-action-driven recording (stays in ViewModel) ───────────

        // Add boundary point if recording is active
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

        // Log elevation data if enabled (#120)
        if (Models.Configuration.ConfigurationStore.Instance.Display.ElevationLogEnabled && IsFieldOpen)
        {
            _elevationLogService.IsEnabled = true;
            var config = Models.Configuration.ConfigurationStore.Instance;
            _elevationLogService.LogPoint(
                data.CurrentPosition.Latitude, data.CurrentPosition.Longitude,
                data.CurrentPosition.Altitude, config.Vehicle.AntennaHeight,
                data.FixQuality,
                posEasting, posNorthing, data.CurrentPosition.Heading,
                RollDegrees);
        }

        // Add recorded path point if path recording is active
        if (IsRecordingPath)
        {
            AddRecordedPathPoint(posEasting, posNorthing, data.CurrentPosition.Heading);
        }

        // Update recorded path playback if active
        if (State.RecordedPath.IsDrivingRecordedPath)
        {
            UpdateRecordedPathPlayback();
        }

        // Auto-select closest track when autosteer is not engaged (#143)
        UpdateAutoTrackSelection(data.CurrentPosition);

        // Phase C C4: the YouTurn tick now runs on the cycle worker
        // (GpsPipelineService.ProcessCycle) and mirrors back to State.YouTurn
        // via ApplyGpsCycleResult. The previous UI-thread tick + SetYouTurnState
        // push lived here; both are gone.
    }

    private bool _isAutoTrackEnabled = true;
    public bool IsAutoTrackEnabled
    {
        get => _isAutoTrackEnabled;
        set => SetProperty(ref _isAutoTrackEnabled, value);
    }

    private DateTime _lastAutoTrackTime = DateTime.MinValue;
    private const double AUTO_TRACK_INTERVAL_SECONDS = 3.0;

    /// <summary>
    /// Auto-select closest track when autosteer is not engaged.
    /// Only runs when no track is manually selected (SelectedTrack == null).
    /// Matches legacy: 3-second debounce, heading alignment, visibility filter.
    /// </summary>
    private void UpdateAutoTrackSelection(AgValoniaGPS.Models.Position position)
    {
        if (!_isAutoTrackEnabled || IsAutoSteerEngaged)
            return;

        // Don't override a manually selected track
        if (SelectedTrack != null)
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

        if (closest != null)
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
            OnPropertyChanged(nameof(RecordedCurvePointCount));
            OnPropertyChanged(nameof(ABCreationInstructions)); // Update instruction text with point count
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

    #region Pipeline State Sync

    // Phase D D6: when the SelectedTrack setter needs to seed a fresh pass
    // number + nudge offset from the track's persisted NudgeDistance, it
    // stores them here for the next SyncGuidanceStateToPipeline to carry
    // into _guidanceWorking (via SetActiveTrack). Cleared after the sync
    // consumes them — subsequent syncs read the current _guidanceWorking
    // values back (mirrored onto State.Guidance by ApplyGpsCycleResult).
    private int? _pendingInitialPathsAway;
    private double? _pendingInitialNudgeOffset;

    // Phase D D7: last DisplayTrack / BaseTrack references pushed to the map
    // service, used by ApplyGpsCycleResult to skip SetActiveTrack /
    // SetBaseTrack when the snapshot carries the same references (the cycle
    // reuses them across ticks whenever pass / nudge haven't changed).
    // Prevents per-cycle SendStateToHandler churn.
    private AgValoniaGPS.Models.Track.Track? _lastMirroredDisplayTrack;
    private AgValoniaGPS.Models.Track.Track? _lastMirroredBaseTrack;
    private AgValoniaGPS.Models.Track.Track? _lastMirroredNextTrack;

    /// <summary>
    /// Sync all guidance-relevant state to the pipeline service.
    /// Call this from commands that change autosteer, track, boundary, headland, or drift state.
    /// The pipeline runs on a background thread and needs its own copy of this state.
    /// </summary>
    private void SyncGuidanceStateToPipeline()
    {
        var track = SelectedTrack;
        bool isOnBoundary = track != null && State.Field.Tracks.IndexOf(track) == 0
            && CurrentBoundary?.OuterBoundary != null;

        // Use pending initial values if the SelectedTrack setter just seeded
        // them; otherwise re-push the current State.Guidance (which the cycle
        // wrote on the last snapshot). The pipeline's SetActiveTrack is
        // idempotent when the values haven't changed.
        int pathsAway = _pendingInitialPathsAway ?? State.Guidance.HowManyPathsAway;
        double nudgeOffset = _pendingInitialNudgeOffset ?? State.Guidance.NudgeOffset;
        _pendingInitialPathsAway = null;
        _pendingInitialNudgeOffset = null;

        _gpsPipelineService.SetAutoSteerEngaged(_isAutoSteerEngaged);
        _gpsPipelineService.SetActiveTrack(track, pathsAway, nudgeOffset, isOnBoundary);
        _gpsPipelineService.SetBoundary(CurrentBoundary);
        _gpsPipelineService.SetHeadlandLine(_currentHeadlandLine);
        _gpsPipelineService.SetDriftCompensation(State.Field.DriftEasting, State.Field.DriftNorthing);
        // Never arm U-turns on a closed/polygon track, regardless of the toggle (#421).
        _gpsPipelineService.SetYouTurnEnabled(IsYouTurnEnabled && !IsActiveTrackClosed);
        _gpsPipelineService.SetYouTurnConfig(
            UTurnSkipRows, IsSkipWorkedMode, HeadlandCalculatedWidth, HeadlandDistance);
    }

    #endregion

    #region Status Display Tick (PERF-05 Phase 2c #1/#2)

    /// <summary>
    /// Unified 5 Hz status-display sampler. The single tick that publishes
    /// every MainViewModel property bound to the top status bar, regardless
    /// of upstream source rate. Matches the cadence architecture:
    /// each subsystem runs at its own appropriate rate (GPS 10 Hz, control
    /// loop 100 Hz, AutoSteer 100 Hz) and updates State / caches; display
    /// refresh is independent of all of them and tuned for human readability.
    ///
    /// Sources:
    /// - <c>State.Vehicle</c> — system of record, updated by
    ///   ApplyGpsCycleResult via State.Vehicle.UpdateFromGps (GPS rate).
    /// - <c>_latestRollDegrees</c> — cached by ApplyGpsCycleResult.
    /// - <c>_latestGpsToPgnLatencyMs</c> — cached by OnAutoSteerStateUpdated
    ///   at the AutoSteer 100 Hz tick rate.
    ///
    /// 5 Hz (200 ms) is below the human perception threshold for numeric
    /// text on a status bar and consolidates every status PropertyChanged
    /// cascade to the same predictable rate — no future diagnostic readout
    /// can accidentally re-introduce a 100-Hz PropertyChanged storm by
    /// setting its MainViewModel property from its source event handler.
    /// New status values just add a <c>_latest…</c> field and one line to
    /// this method.
    ///
    /// SetProperty short-circuits when the new value equals the old (ref
    /// comparison for strings, value comparison for primitives via the
    /// CommunityToolkit equality helper), so unchanged values don't fire
    /// PropertyChanged — no per-property epsilon check needed.
    /// </summary>
    private void OnStatusTick(object? sender, EventArgs e)
    {
        var v = State.Vehicle;
        Latitude = v.Latitude;
        Longitude = v.Longitude;
        Easting = v.Easting;
        Northing = v.Northing;
        Heading = v.Heading;
        Speed = v.Speed;
        RollDegrees = _latestRollDegrees;
        FixQuality = GetFixQualityString(v.FixQuality);
        GpsToPgnLatencyMs = _latestGpsToPgnLatencyMs;
    }

    #endregion
}
