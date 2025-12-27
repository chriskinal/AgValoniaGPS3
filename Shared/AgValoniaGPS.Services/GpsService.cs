using System;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Implementation of GPS service for data processing
/// </summary>
public class GpsService : IGpsService
{
    public event EventHandler<GpsData>? GpsDataUpdated;

    public GpsData CurrentData { get; private set; } = new();

    public bool IsConnected { get; private set; }

    private DateTime _lastGpsDataReceived = DateTime.MinValue;
    private DateTime _lastImuDataReceived = DateTime.MinValue;
    private const int GPS_TIMEOUT_MS = 300; // 10Hz data = 100ms cycle, allow 300ms
    private const int IMU_TIMEOUT_MS = 300; // 10Hz data = 100ms cycle, allow 300ms

    public void Start()
    {
        IsConnected = true;
    }

    public void Stop()
    {
        IsConnected = false;
    }

    public void ProcessNmeaSentence(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence))
            return;

        // This is called by NmeaParserService after parsing
        // Just trigger event notification
        GpsDataUpdated?.Invoke(this, CurrentData);
    }

    /// <summary>
    /// Update GPS data directly (called by NMEA parser)
    /// Applies antenna-to-pivot transformation before storing
    /// </summary>
    public void UpdateGpsData(GpsData newData)
    {
        // Apply antenna-to-pivot transformation
        TransformAntennaToPivot(newData);

        CurrentData = newData;
        _lastGpsDataReceived = DateTime.Now;
        IsConnected = newData.IsValid;
        GpsDataUpdated?.Invoke(this, CurrentData);
    }

    /// <summary>
    /// Transform GPS antenna position to vehicle pivot point using configured offsets.
    /// The GPS antenna is typically mounted above and behind the pivot point.
    /// </summary>
    private void TransformAntennaToPivot(GpsData gpsData)
    {
        var vehicle = ConfigurationStore.Instance.Vehicle;

        // Skip transformation if no offsets configured
        if (Math.Abs(vehicle.AntennaPivot) < 0.001 && Math.Abs(vehicle.AntennaOffset) < 0.001)
            return;

        // Convert heading to radians
        double headingRadians = gpsData.CurrentPosition.Heading * Math.PI / 180.0;

        // Transform antenna position to pivot position
        // Pivot is behind antenna by AntennaPivot distance (positive = antenna ahead of pivot)
        double pivotEasting = gpsData.CurrentPosition.Easting - Math.Sin(headingRadians) * vehicle.AntennaPivot;
        double pivotNorthing = gpsData.CurrentPosition.Northing - Math.Cos(headingRadians) * vehicle.AntennaPivot;

        // Apply lateral offset if antenna is not on centerline
        // Positive AntennaOffset = antenna is to the right of centerline
        if (Math.Abs(vehicle.AntennaOffset) > 0.001)
        {
            double perpHeading = headingRadians + Math.PI / 2.0;
            pivotEasting -= Math.Sin(perpHeading) * vehicle.AntennaOffset;
            pivotNorthing -= Math.Cos(perpHeading) * vehicle.AntennaOffset;
        }

        // Create new Position with transformed coordinates (Position is a record with init-only props)
        gpsData.CurrentPosition = gpsData.CurrentPosition with
        {
            Easting = pivotEasting,
            Northing = pivotNorthing
        };
    }

    /// <summary>
    /// Update IMU data timestamp (called when IMU data received)
    /// </summary>
    public void UpdateImuData()
    {
        _lastImuDataReceived = DateTime.Now;
    }

    /// <summary>
    /// Check if GPS data is flowing (10Hz expected)
    /// </summary>
    public bool IsGpsDataOk()
    {
        return (DateTime.Now - _lastGpsDataReceived).TotalMilliseconds < GPS_TIMEOUT_MS;
    }

    /// <summary>
    /// Check if IMU data is flowing (10Hz expected)
    /// </summary>
    public bool IsImuDataOk()
    {
        return (DateTime.Now - _lastImuDataReceived).TotalMilliseconds < IMU_TIMEOUT_MS;
    }
}