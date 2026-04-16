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
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Simulator configuration - ONE place only.
/// Replaces: simulator fields previously scattered across AppSettings and profile DTOs
/// </summary>
public class SimulatorConfig : ObservableObject
{
    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    private double _latitude = 40.7128;
    public double Latitude
    {
        get => _latitude;
        set => SetProperty(ref _latitude, Math.Clamp(value, -90, 90));
    }

    private double _longitude = -74.0060;
    public double Longitude
    {
        get => _longitude;
        set => SetProperty(ref _longitude, Math.Clamp(value, -180, 180));
    }

    private double _heading;
    public double Heading
    {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    private double _speed;
    public double Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    private double _steerAngle;
    public double SteerAngle
    {
        get => _steerAngle;
        set => SetProperty(ref _steerAngle, value);
    }
}
