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

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for GPS data processing and management
/// </summary>
public interface IGpsService
{
    /// <summary>
    /// Event fired when new GPS data is received
    /// </summary>
    event EventHandler<GpsData>? GpsDataUpdated;

    /// <summary>
    /// Current GPS data
    /// </summary>
    GpsData CurrentData { get; }

    /// <summary>
    /// Whether GPS is currently connected and receiving data
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Start GPS service
    /// </summary>
    void Start();

    /// <summary>
    /// Stop GPS service
    /// </summary>
    void Stop();

    /// <summary>
    /// Process NMEA sentence
    /// </summary>
    void ProcessNmeaSentence(string sentence);

    /// <summary>
    /// Update GPS data from parsed NMEA sentence
    /// </summary>
    void UpdateGpsData(GpsData newData);

    /// <summary>
    /// Update IMU data timestamp (called when IMU data received)
    /// </summary>
    void UpdateImuData();

    /// <summary>
    /// Check if GPS data is flowing (10Hz expected)
    /// </summary>
    bool IsGpsDataOk();

    /// <summary>
    /// Check if IMU data is flowing (10Hz expected)
    /// </summary>
    bool IsImuDataOk();
}