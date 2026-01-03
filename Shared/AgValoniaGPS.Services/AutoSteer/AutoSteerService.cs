using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.Interfaces;

// Alias to disambiguate from Track namespace
using TrackModel = AgValoniaGPS.Models.Track.Track;
using TrackInput = AgValoniaGPS.Models.Track.TrackGuidanceInput;

namespace AgValoniaGPS.Services.AutoSteer;

/// <summary>
/// Zero-copy AutoSteer pipeline service.
/// Coordinates GPS→Parse→Guidance→PGN flow with minimum latency.
/// Single VehicleState instance is updated in place - no allocations per cycle.
/// </summary>
public class AutoSteerService : IAutoSteerService
{
    // Core state - single instance, updated in place
    private VehicleState _state;

    // Dependencies
    private readonly ITrackGuidanceService _guidanceService;
    private readonly IUdpCommunicationService _udpService;

    // Local coordinate system reference
    private LocalPlane? _localPlane;
    private SharedFieldProperties _sharedFieldProperties;

    // Current track for guidance (set by MainViewModel)
    private TrackModel? _currentTrack;
    private TrackInput _guidanceInput;

    // Latency tracking (circular buffer for last 10 samples)
    private readonly double[] _latencySamples = new double[10];
    private int _latencySampleIndex;
    private long _cycleCount;
    private long _parseFailures;

    // Service state
    private bool _isEnabled;
    private bool _isEngaged;

    public event EventHandler<VehicleStateSnapshot>? StateUpdated;

    public bool IsEnabled => _isEnabled;
    public bool IsEngaged => _isEngaged;
    public bool IsInFreeDriveMode => _state.IsInFreeDriveMode;
    public double FreeDriveSteerAngle => _state.FreeDriveSteerAngle;

    public AutoSteerService(
        ITrackGuidanceService guidanceService,
        IUdpCommunicationService udpService)
    {
        _guidanceService = guidanceService;
        _udpService = udpService;

        // Initialize state
        _state = new VehicleState();
        _sharedFieldProperties = new SharedFieldProperties();
        _guidanceInput = new TrackInput();
    }

    public void Start()
    {
        _isEnabled = true;
    }

    public void Stop()
    {
        _isEnabled = false;
        _isEngaged = false;
    }

    public void Engage()
    {
        if (_isEnabled)
        {
            _isEngaged = true;
            _state.IsAutoSteerEngaged = true;
        }
    }

    public void Disengage()
    {
        _isEngaged = false;
        _state.IsAutoSteerEngaged = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Free Drive Mode
    // ═══════════════════════════════════════════════════════════════════════

    public void EnableFreeDrive()
    {
        _state.IsInFreeDriveMode = true;
        _state.FreeDriveSteerAngle = 0;
    }

    public void DisableFreeDrive()
    {
        _state.IsInFreeDriveMode = false;
        _state.FreeDriveSteerAngle = 0;
    }

    public void SetFreeDriveAngle(double angleDegrees)
    {
        // Clamp to safe range (-40 to +40 degrees)
        _state.FreeDriveSteerAngle = Math.Clamp(angleDegrees, -40, 40);
    }

    /// <summary>
    /// Set the local coordinate system for GPS→local conversion.
    /// Called by MainViewModel when field is loaded or first GPS position received.
    /// </summary>
    public void SetLocalPlane(LocalPlane localPlane, SharedFieldProperties sharedFieldProperties)
    {
        _localPlane = localPlane;
        _sharedFieldProperties = sharedFieldProperties;
    }

    /// <summary>
    /// Set the current track for guidance.
    /// Called by MainViewModel when active track changes.
    /// </summary>
    public void SetCurrentTrack(TrackModel? track)
    {
        _currentTrack = track;
    }

    /// <summary>
    /// Process incoming GPS buffer - entry point for zero-copy pipeline.
    /// Called directly from UDP receive handler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ProcessGpsBuffer(byte[] buffer, int length)
    {
        if (!_isEnabled) return;

        // Mark cycle start
        _state.BeginNewCycle();

        // Parse directly into VehicleState (zero-copy)
        ReadOnlySpan<byte> data = buffer.AsSpan(0, length);
        if (!NmeaParserServiceFast.ParseIntoState(data, ref _state))
        {
            _parseFailures++;
            return;
        }

        // Convert to local coordinates if we have a plane
        if (_localPlane != null)
        {
            var geoCoord = _localPlane.ConvertWgs84ToGeoCoord(
                new Wgs84(_state.Latitude, _state.Longitude));
            _state.Easting = geoCoord.Easting;
            _state.Northing = geoCoord.Northing;
        }

        // Calculate guidance if we have an active track
        if (_currentTrack != null && _currentTrack.Points.Count >= 2)
        {
            CalculateGuidance();
        }

        // Build and send PGNs
        SendPgns();

        // Mark PGN sent and record latency
        _state.MarkPgnSent();
        RecordLatency(_state.TotalLatencyMs);

        // Notify UI (creates snapshot copy)
        NotifyStateUpdated();

        _cycleCount++;
    }

    /// <summary>
    /// Process simulated position data (bypass NMEA parsing).
    /// Used by simulator which already has parsed GPS data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ProcessSimulatedPosition(double latitude, double longitude, double altitude,
        double headingDegrees, double speedMps, int fixQuality, int satellites, double hdop,
        double easting, double northing)
    {
        if (!_isEnabled) return;

        // Mark cycle start
        _state.BeginNewCycle();

        // Set position directly (no parsing needed)
        _state.Latitude = latitude;
        _state.Longitude = longitude;
        _state.Altitude = altitude;
        _state.Heading = headingDegrees;
        _state.HeadingRadians = headingDegrees * Math.PI / 180.0;
        _state.Speed = speedMps;
        _state.FixQuality = fixQuality;
        _state.Satellites = satellites;
        _state.Hdop = hdop;
        _state.Easting = easting;
        _state.Northing = northing;
        _state.GpsValid = true;
        _state.MarkParseEnd(); // Mark parse complete (even though we skipped parsing)

        // Calculate guidance if we have an active track
        if (_currentTrack != null && _currentTrack.Points.Count >= 2)
        {
            CalculateGuidance();
        }

        // Build and send PGNs
        SendPgns();

        // Mark PGN sent and record latency
        _state.MarkPgnSent();
        RecordLatency(_state.TotalLatencyMs);

        // Notify UI (creates snapshot copy)
        NotifyStateUpdated();

        _cycleCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateGuidance()
    {
        // Build guidance input from current state
        _guidanceInput.Track = _currentTrack!;
        _guidanceInput.PivotPosition = new Vec3(_state.Easting, _state.Northing, _state.Heading);
        _guidanceInput.SteerPosition = new Vec3(_state.Easting, _state.Northing, _state.Heading);
        _guidanceInput.FixHeading = _state.HeadingRadians;

        // Calculate guidance
        var output = _guidanceService.CalculateGuidance(_guidanceInput);

        // Update state with guidance results
        _state.CrossTrackError = output.DistanceFromLinePivot;
        _state.SteerAngle = output.SteerAngle;
        _state.IsOnTrack = Math.Abs(output.DistanceFromLinePivot) < 1.0; // Within 1 meter
        _state.MarkGuidanceEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SendPgns()
    {
        // Always send PGN 254 - the engage status is a bit in the status byte
        // This keeps the module informed of current position/speed even when not engaged
        var pgn = PgnBuilder.BuildAutoSteerPgn(ref _state);
        _udpService.SendToModules(pgn);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordLatency(double latencyMs)
    {
        _latencySamples[_latencySampleIndex] = latencyMs;
        _latencySampleIndex = (_latencySampleIndex + 1) % _latencySamples.Length;
    }

    private void NotifyStateUpdated()
    {
        StateUpdated?.Invoke(this, CreateSnapshot());
    }

    private VehicleStateSnapshot CreateSnapshot()
    {
        return new VehicleStateSnapshot
        {
            Latitude = _state.Latitude,
            Longitude = _state.Longitude,
            Altitude = _state.Altitude,
            Speed = _state.Speed,
            Heading = _state.Heading,
            FixQuality = _state.FixQuality,
            Satellites = _state.Satellites,
            Hdop = _state.Hdop,
            DifferentialAge = _state.DifferentialAge,
            Roll = _state.Roll,
            Pitch = _state.Pitch,
            YawRate = _state.YawRate,
            Easting = _state.Easting,
            Northing = _state.Northing,
            CrossTrackError = _state.CrossTrackError,
            SteerAngle = _state.SteerAngle,
            DistanceToTurn = _state.DistanceToTurn,
            DistanceToEnd = _state.DistanceToEnd,
            IsOnTrack = _state.IsOnTrack,
            IsAutoSteerEngaged = _state.IsAutoSteerEngaged,
            SectionStates = _state.SectionStates,
            MasterSectionOn = _state.MasterSectionOn,
            TotalLatencyMs = _state.TotalLatencyMs,
            ParseLatencyMs = _state.ParseLatencyMs,
            GuidanceLatencyMs = _state.GuidanceLatencyMs,
            GpsValid = _state.GpsValid,
            GuidanceValid = _state.GuidanceValid,
            ImuValid = _state.ImuValid
        };
    }

    public AutoSteerLatencyMetrics GetLatencyMetrics()
    {
        double sum = 0;
        double max = 0;
        int count = 0;

        for (int i = 0; i < _latencySamples.Length; i++)
        {
            if (_latencySamples[i] > 0)
            {
                sum += _latencySamples[i];
                if (_latencySamples[i] > max) max = _latencySamples[i];
                count++;
            }
        }

        double avg = count > 0 ? sum / count : 0;
        int lastIndex = (_latencySampleIndex - 1 + _latencySamples.Length) % _latencySamples.Length;

        return new AutoSteerLatencyMetrics
        {
            LastTotalLatencyMs = _latencySamples[lastIndex],
            AvgTotalLatencyMs = avg,
            MaxTotalLatencyMs = max,
            LastParseLatencyMs = _state.ParseLatencyMs,
            LastGuidanceLatencyMs = _state.GuidanceLatencyMs,
            CycleCount = _cycleCount,
            ParseFailures = _parseFailures
        };
    }
}
