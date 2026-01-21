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
        set => SetProperty(ref _speed, value);
    }

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
