// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Logs GPS elevation data to CSV when enabled and a field is open.
/// Legacy format: Elevation.txt with lat, lon, elevation, quality, easting, northing, heading, roll.
/// Only logs when vehicle moves >2.9m since last sample.
/// </summary>
public interface IElevationLogService
{
    bool IsEnabled { get; set; }

    /// <summary>Create CSV header for a new field.</summary>
    void CreateHeader(string fieldDirectory, double startLat, double startLon);

    /// <summary>Log a point if distance gate is satisfied (>2.9m since last).</summary>
    void LogPoint(double latitude, double longitude, double altitude,
        double antennaHeight, int fixQuality,
        double easting, double northing, double heading, double roll);

    /// <summary>Flush buffered rows to Elevation.txt.</summary>
    void Flush(string fieldDirectory);

    /// <summary>Clear buffer (on field close).</summary>
    void Clear();
}
