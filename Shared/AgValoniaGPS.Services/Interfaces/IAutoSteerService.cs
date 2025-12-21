using System;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Zero-copy AutoSteer pipeline coordinator.
/// Owns the VehicleState and coordinates GPS→Parse→Guidance→PGN flow.
/// Runs synchronously with GPS at 10Hz for minimum latency.
/// </summary>
public interface IAutoSteerService
{
    /// <summary>
    /// Event fired when the control cycle completes (for UI updates).
    /// Note: UI should not rely on this for control - it's purely observational.
    /// </summary>
    event EventHandler<VehicleStateSnapshot>? StateUpdated;

    /// <summary>
    /// Whether auto-steer is enabled and processing GPS data.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Whether the vehicle is currently being auto-steered.
    /// </summary>
    bool IsEngaged { get; }

    /// <summary>
    /// Start the AutoSteer service.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop the AutoSteer service.
    /// </summary>
    void Stop();

    /// <summary>
    /// Process incoming GPS data buffer.
    /// This is the entry point for the zero-copy pipeline.
    /// Called directly from UDP receive handler.
    /// </summary>
    /// <param name="buffer">Raw UDP receive buffer (no copy)</param>
    /// <param name="length">Valid bytes in buffer</param>
    void ProcessGpsBuffer(byte[] buffer, int length);

    /// <summary>
    /// Process simulated position data (bypass NMEA parsing).
    /// Used by simulator which already has parsed GPS data.
    /// </summary>
    void ProcessSimulatedPosition(double latitude, double longitude, double altitude,
        double headingDegrees, double speedMps, int fixQuality, int satellites, double hdop,
        double easting, double northing);

    /// <summary>
    /// Engage auto-steer (start sending steering commands to hardware).
    /// </summary>
    void Engage();

    /// <summary>
    /// Disengage auto-steer (stop sending steering commands).
    /// </summary>
    void Disengage();

    /// <summary>
    /// Get current latency metrics (for diagnostics display).
    /// </summary>
    AutoSteerLatencyMetrics GetLatencyMetrics();
}

/// <summary>
/// Snapshot of VehicleState for UI consumption.
/// Copied from VehicleState when control cycle completes.
/// UI can hold this reference without affecting control path.
/// </summary>
public readonly struct VehicleStateSnapshot
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Altitude { get; init; }
    public double Speed { get; init; }
    public double SpeedKmh => Speed * 3.6;
    public double Heading { get; init; }
    public int FixQuality { get; init; }
    public int Satellites { get; init; }
    public double Hdop { get; init; }
    public double DifferentialAge { get; init; }

    public double Roll { get; init; }
    public double Pitch { get; init; }
    public double YawRate { get; init; }

    public double Easting { get; init; }
    public double Northing { get; init; }

    public double CrossTrackError { get; init; }
    public double SteerAngle { get; init; }
    public double DistanceToTurn { get; init; }
    public double DistanceToEnd { get; init; }
    public bool IsOnTrack { get; init; }
    public bool IsAutoSteerEngaged { get; init; }

    public ushort SectionStates { get; init; }
    public bool MasterSectionOn { get; init; }

    public double TotalLatencyMs { get; init; }
    public double ParseLatencyMs { get; init; }
    public double GuidanceLatencyMs { get; init; }

    public bool GpsValid { get; init; }
    public bool GuidanceValid { get; init; }
    public bool ImuValid { get; init; }
    public bool IsRtkFix => FixQuality >= 4;
}

/// <summary>
/// Latency metrics for diagnostics.
/// </summary>
public readonly struct AutoSteerLatencyMetrics
{
    /// <summary>Last cycle total latency (GPS receive to PGN send) in milliseconds.</summary>
    public double LastTotalLatencyMs { get; init; }

    /// <summary>Average total latency over last 10 cycles.</summary>
    public double AvgTotalLatencyMs { get; init; }

    /// <summary>Maximum total latency over last 10 cycles.</summary>
    public double MaxTotalLatencyMs { get; init; }

    /// <summary>Last parse latency in milliseconds.</summary>
    public double LastParseLatencyMs { get; init; }

    /// <summary>Last guidance calculation latency in milliseconds.</summary>
    public double LastGuidanceLatencyMs { get; init; }

    /// <summary>Number of cycles processed.</summary>
    public long CycleCount { get; init; }

    /// <summary>Number of parse failures.</summary>
    public long ParseFailures { get; init; }
}
