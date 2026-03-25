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
    /// Transform GPS antenna position to tractor center (pivot point).
    ///
    /// The GPS reports the antenna's true world coordinates. We calculate the tractor
    /// center position so that guidance steers the tractor/implement over the correct
    /// real-world coordinates.
    ///
    /// Example: Antenna is 1m LEFT of tractor center, heading North, antenna at x=-1.
    ///   - AntennaOffset = -1.0 (negative = LEFT of center)
    ///   - Tractor center = antenna_x - perpRight * (-1) = -1 + 1 = 0
    ///   - Result: Tractor center calculated at x=0 (prime meridian) ✓
    ///
    /// Sign conventions:
    ///   AntennaPivot: Positive = antenna is AHEAD of pivot (typical roof mount)
    ///   AntennaOffset: Negative = antenna is LEFT of center, Positive = RIGHT
    /// </summary>
    private void TransformAntennaToPivot(GpsData gpsData)
    {
        var vehicle = ConfigurationStore.Instance.Vehicle;

        // Skip transformation if no offsets configured
        if (Math.Abs(vehicle.AntennaPivot) < 0.001 && Math.Abs(vehicle.AntennaOffset) < 0.001)
            return;

        // Convert heading to radians
        double headingRadians = gpsData.CurrentPosition.Heading * Math.PI / 180.0;

        // Start with antenna position
        double pivotEasting = gpsData.CurrentPosition.Easting;
        double pivotNorthing = gpsData.CurrentPosition.Northing;

        // Apply fore/aft offset (AntennaPivot):
        // Positive value = antenna is ahead of pivot (typical roof mount)
        // To find pivot: move BACKWARD from antenna position
        if (Math.Abs(vehicle.AntennaPivot) > 0.001)
        {
            pivotEasting -= Math.Sin(headingRadians) * vehicle.AntennaPivot;
            pivotNorthing -= Math.Cos(headingRadians) * vehicle.AntennaPivot;
        }

        // Apply lateral offset (AntennaOffset):
        // Positive value = antenna is RIGHT of centerline
        // To find centerline: move LEFT from antenna position
        if (Math.Abs(vehicle.AntennaOffset) > 0.001)
        {
            double perpHeading = headingRadians + Math.PI / 2.0; // Points right
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