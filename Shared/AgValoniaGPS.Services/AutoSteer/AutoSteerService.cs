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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

// Aliases to disambiguate from colliding namespaces.
using TrackModel = AgValoniaGPS.Models.Track.Track;
using TrackInput = AgValoniaGPS.Models.Track.TrackGuidanceInput;
using ApplicationState = AgValoniaGPS.Models.State.ApplicationState;

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
    private readonly IGpsService _gpsService;
    private readonly ApplicationState _appState;
    private ITramLineService? _tramLineService;
    private ISmartWasCalibrationService? _smartWas;

    // Drift compensation applied after LocalPlane → local coordinate conversion.
    // LocalPlane itself is owned by ApplicationState.Field.LocalPlane — single shared instance
    // across AutoSteer and the cycle worker. Created by field-open (UI thread) or by the
    // cycle worker on first valid fix; never written here (receive thread).
    private double _driftEasting;
    private double _driftNorthing;

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
        IUdpCommunicationService udpService,
        IGpsService gpsService,
        ApplicationState appState)
    {
        _guidanceService = guidanceService;
        _udpService = udpService;
        _gpsService = gpsService;
        _appState = appState;

        // Initialize state
        _state = new VehicleState();
        _guidanceInput = new TrackInput();
    }

    /// <summary>
    /// Set the tram line service for real-time wheel detection in PGN 239.
    /// </summary>
    public void SetTramLineService(ITramLineService tramLineService)
    {
        _tramLineService = tramLineService;
    }

    /// <summary>
    /// Set the Smart WAS calibration service so each PGN 253 sample is
    /// forwarded to its analyzer. Setter-injected to avoid the constructor
    /// cycle (Smart WAS depends on IAutoSteerService).
    /// </summary>
    public void SetSmartWasService(ISmartWasCalibrationService smartWas)
    {
        _smartWas = smartWas;
    }

    public void Start()
    {
        _isEnabled = true;
        _udpService.DataReceived += OnUdpDataReceived;
    }

    public void Stop()
    {
        _udpService.DataReceived -= OnUdpDataReceived;
        _isEnabled = false;
        _isEngaged = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Module Data Reception (PGN 253, 250)
    // ═══════════════════════════════════════════════════════════════════════

    // Latest module data - updated from PGN 253/250
    private SteerModuleData _lastSteerData;
    private SensorModuleData _lastSensorData;
    private double _sensorPercent;

    /// <summary>Latest steer data from module (PGN 253).</summary>
    public SteerModuleData LastSteerData => _lastSteerData;

    /// <summary>Latest sensor data from module (PGN 250).</summary>
    public SensorModuleData LastSensorData => _lastSensorData;

    /// <summary>Sensor reading as percentage (0-100).</summary>
    public double SensorPercent => _sensorPercent;

    /// <inheritdoc/>
    public VehicleStateSnapshot? LatestSnapshot => _latestSnapshot;
    private VehicleStateSnapshot? _latestSnapshot;

    /// <summary>
    /// Handle incoming UDP data from steering module.
    /// </summary>
    private void OnUdpDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        switch (e.PGN)
        {
            case PgnNumbers.AUTOSTEER_DATA: // 253 - Steer Data from module
                ProcessSteerData(e.Data);
                break;

            case PgnNumbers.SENSOR_DATA: // 250 - Sensor Data from module
                ProcessSensorData(e.Data);
                break;
        }
    }

    /// <summary>
    /// Process PGN 253 - Steer Data from module.
    /// Contains actual steer angle, switch states, PWM feedback.
    /// </summary>
    private void ProcessSteerData(byte[] data)
    {
        if (!PgnBuilder.TryParseSteerData(data, out var steerData))
            return;

        _lastSteerData = steerData;

        // Update vehicle state with actual angle from WAS
        _state.ActualSteerAngle = steerData.ActualSteerAngle;
        _state.SteerSwitchActive = steerData.SteerSwitchActive;
        _state.WorkSwitchActive = steerData.WorkSwitchActive;

        _smartWas?.AddSample(steerData.ActualSteerAngle);
    }

    /// <summary>
    /// Process PGN 250 - Sensor Data from module.
    /// Contains pressure/current sensor readings for kickout.
    /// </summary>
    private void ProcessSensorData(byte[] data)
    {
        if (!PgnBuilder.TryParseSensorData(data, out var sensorData))
            return;

        _lastSensorData = sensorData;

        // Map sensor value (0-255) to percentage (0-100)
        _sensorPercent = sensorData.SensorValue / 255.0 * 100.0;

        // Get config for sensor settings
        var config = ConfigurationStore.Instance.AutoSteer;

        // Check pressure sensor trip point (hydraulic kickout)
        if (config.PressureSensorEnabled && config.PressureTripPoint > 0)
        {
            if (_sensorPercent >= config.PressureTripPoint)
            {
                Disengage();
                Debug.WriteLine($"[AutoSteer] Pressure kickout: {_sensorPercent:F1}% >= {config.PressureTripPoint}%");
            }
        }
        // Check current sensor trip point (motor overload protection)
        else if (config.CurrentSensorEnabled && config.CurrentTripPoint > 0)
        {
            if (_sensorPercent >= config.CurrentTripPoint)
            {
                Disengage();
                Debug.WriteLine($"[AutoSteer] Current kickout: {_sensorPercent:F1}% >= {config.CurrentTripPoint}%");
            }
        }
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
    /// Set GPS drift compensation (offset fix). Applied to local coordinates
    /// before guidance and tool position calculations, so tractor + implement
    /// move together. Values in meters.
    /// </summary>
    public void SetDriftCompensation(double driftEasting, double driftNorthing)
    {
        _driftEasting = driftEasting;
        _driftNorthing = driftNorthing;
    }

    public void SendMachineConfig()
    {
        var config = ConfigurationStore.Instance.Machine;
        var pgn = PgnBuilder.BuildMachineConfigPgn(config);
        _udpService.SendToModules(pgn);
    }

    public void SendMachinePinConfig()
    {
        var config = ConfigurationStore.Instance.Machine;
        var pgn = PgnBuilder.BuildMachinePinsPgn(config);
        _udpService.SendToModules(pgn);
    }

    public void SetMachineState(ushort sectionBits, bool isInUTurn, byte hydLiftState = 0)
    {
        _state.SectionStates = sectionBits;
        _state.IsInUTurn = isInUTurn;
        _state.HydLiftState = hydLiftState;
    }

    /// <summary>
    /// Set the current track for guidance.
    /// Called by MainViewModel when active track changes.
    /// </summary>
    public void SetCurrentTrack(TrackModel? track)
    {
        _currentTrack = track;
    }

    public void UpdateGuidanceResults(double steerAngle, double crossTrackError)
    {
        _state.SteerAngle = steerAngle;
        _state.CrossTrackError = crossTrackError;
    }

    /// <summary>
    /// Process incoming GPS buffer — entry point for the zero-copy pipeline.
    /// Called directly from the UDP receive callback.
    ///
    /// Phase B C4: this method does parse-and-return only. Coordinate
    /// conversion, guidance, PGN build, tram detection, notify, and latency
    /// recording all moved to the cycle worker, which calls
    /// <see cref="ProcessSimulatedPosition"/> once per tick with the
    /// cycle-computed coordinates and heading. Pre-C4, those steps ran here
    /// on the receive thread, violating the §0 invariant and emitting PGNs
    /// twice per packet (once here, once from the cycle).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ProcessGpsBuffer(byte[] buffer, int length)
    {
        if (!_isEnabled) return;

        // Parse directly into VehicleState (zero-copy). ParseIntoState marks
        // its own parse-timing fields; no BeginNewCycle here because the
        // cycle owns timing now.
        ReadOnlySpan<byte> data = buffer.AsSpan(0, length);
        if (!NmeaParserServiceFast.ParseIntoState(data, ref _state))
        {
            _parseFailures++;
            return;
        }

        // Publish the parsed fix to GpsService. This is the sole event the
        // cycle worker listens to; everything else runs there.
        PublishGpsData();
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

        // Detect tram line wheel positions for PGN 239
        UpdateTramState();

        // PGN sends now happen on the host control loop (#313 commit 4 of 11)
        // at 100 Hz, matching the firmware autosteer task cadence. State mutation
        // here still happens at GPS rate; the loop reads the latest state on
        // each tick.
        _state.MarkPgnSent();
        RecordLatency(_state.TotalLatencyMs);

        // Notify UI (creates snapshot copy)
        NotifyStateUpdated();

        _cycleCount++;
    }

    /// <summary>
    /// Mirror the parsed VehicleState fields onto a fresh GpsData and fire
    /// GpsService.UpdateGpsData — this is what kicks off the cycle worker.
    /// A fresh instance per packet is required: the event handler hands
    /// the reference to a background Task.Run (the cycle worker), and a
    /// reused instance would race with the next receive-thread mutation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PublishGpsData()
    {
        var gpsData = new GpsData
        {
            CurrentPosition = new Position
            {
                Latitude = _state.Latitude,
                Longitude = _state.Longitude,
                Altitude = _state.Altitude,
                Heading = _state.Heading,
                Speed = _state.Speed,
            },
            FixQuality = _state.FixQuality,
            SatellitesInUse = _state.Satellites,
            Hdop = _state.Hdop,
            DifferentialAge = _state.DifferentialAge,
            ImuRoll = _state.Roll,
            ImuPitch = _state.Pitch,
            ImuYawRate = _state.YawRate,
            ImuHeading = _state.ImuHeading,
            ImuValid = _state.ImuValid,
            Timestamp = DateTime.UtcNow,
        };
        _gpsService.UpdateGpsData(gpsData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateTramState()
    {
        if (_tramLineService != null && _tramLineService.HasTramLines &&
            ConfigurationStore.Instance.Tram.DisplayMode != Models.Configuration.TramDisplayMode.Off)
        {
            // Use approximate tool position so detection matches implement indicators
            var config = ConfigurationStore.Instance;
            double hitchLen = config.Tool.HitchLength + config.Tool.TrailingHitchLength;
            double toolE = _state.Easting + Math.Sin(_state.HeadingRadians) * hitchLen;
            double toolN = _state.Northing + Math.Cos(_state.HeadingRadians) * hitchLen;

            _state.TramState = _tramLineService.DetectTramWheels(
                new Models.Base.Vec3(toolE, toolN, _state.Heading),
                _state.HeadingRadians, 0.5);
        }
        else
        {
            _state.TramState = 0;
        }
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

        // Send PGN 239 (Machine Data) - section control, speed, tramline state
        // Sent every GPS cycle so machine module has current section/speed info
        var machinePgn = PgnBuilder.BuildMachinePgn(ref _state,
            uturn: _state.IsInUTurn ? (byte)1 : (byte)0,
            hydLift: _state.HydLiftState,
            tram: _state.TramState,
            geoStop: _state.GeoStopState);
        _udpService.SendToModules(machinePgn);
    }

    /// <summary>
    /// Build and send PGN 254 + PGN 239 from the current vehicle state.
    /// Called by the host control loop (#313) on every tick (100 Hz) so the
    /// firmware autosteer task — which also runs at 100 Hz — sees a fresh
    /// PGN every cycle. Reads <c>_state</c> without locking; concurrent
    /// updates from the GPS pipeline thread may produce a torn read on
    /// individual fields, which the firmware tolerates by acting on
    /// whatever bits it has at its next loop iteration.
    /// </summary>
    public void SendPgnsForControlTick()
    {
        if (!_isEnabled) return;
        SendPgns();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordLatency(double latencyMs)
    {
        _latencySamples[_latencySampleIndex] = latencyMs;
        _latencySampleIndex = (_latencySampleIndex + 1) % _latencySamples.Length;
    }

    private void NotifyStateUpdated()
    {
        var snapshot = CreateSnapshot();
        _latestSnapshot = snapshot;
        StateUpdated?.Invoke(this, snapshot);
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
            TramState = _state.TramState,
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
