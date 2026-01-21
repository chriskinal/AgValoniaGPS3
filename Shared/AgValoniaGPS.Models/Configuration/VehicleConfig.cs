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
using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Vehicle physical configuration.
/// Replaces: Vehicle.cs, VehicleConfiguration.cs (physical parts)
/// </summary>
public class VehicleConfig : ReactiveObject
{
    // Identity
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    // Vehicle type
    private VehicleType _type = VehicleType.Tractor;
    public VehicleType Type
    {
        get => _type;
        set
        {
            var oldValue = _type;
            this.RaiseAndSetIfChanged(ref _type, value);
            if (oldValue != value)
            {
                // Notify computed properties that depend on Type
                this.RaisePropertyChanged(nameof(WheelbaseImageSource));
                this.RaisePropertyChanged(nameof(AntennaImageSource));
                this.RaisePropertyChanged(nameof(VehicleTypeDisplayName));
            }
        }
    }

    // Physical dimensions
    private double _wheelbase = 2.5;
    public double Wheelbase
    {
        get => _wheelbase;
        set
        {
            var oldValue = _wheelbase;
            this.RaiseAndSetIfChanged(ref _wheelbase, value);
            if (oldValue != value)
            {
                this.RaisePropertyChanged(nameof(MinTurningRadius));
            }
        }
    }

    private double _trackWidth = 1.8;
    public double TrackWidth
    {
        get => _trackWidth;
        set => this.RaiseAndSetIfChanged(ref _trackWidth, value);
    }

    // Antenna position
    private double _antennaHeight = 3.0;
    public double AntennaHeight
    {
        get => _antennaHeight;
        set => this.RaiseAndSetIfChanged(ref _antennaHeight, value);
    }

    private double _antennaPivot = 0.0;
    public double AntennaPivot
    {
        get => _antennaPivot;
        set => this.RaiseAndSetIfChanged(ref _antennaPivot, value);
    }

    private double _antennaOffset = 0.0;
    public double AntennaOffset
    {
        get => _antennaOffset;
        set => this.RaiseAndSetIfChanged(ref _antennaOffset, value);
    }

    // Steering limits
    private double _maxSteerAngle = 35.0;
    public double MaxSteerAngle
    {
        get => _maxSteerAngle;
        set
        {
            var oldValue = _maxSteerAngle;
            this.RaiseAndSetIfChanged(ref _maxSteerAngle, value);
            if (oldValue != value)
            {
                this.RaisePropertyChanged(nameof(MinTurningRadius));
            }
        }
    }

    private double _maxAngularVelocity = 35.0;
    public double MaxAngularVelocity
    {
        get => _maxAngularVelocity;
        set => this.RaiseAndSetIfChanged(ref _maxAngularVelocity, value);
    }

    // Computed properties
    public double MinTurningRadius => Wheelbase / Math.Tan(MaxSteerAngle * Math.PI / 180.0);

    /// <summary>
    /// Gets the image source for the wheelbase/track diagram based on vehicle type
    /// </summary>
    public string WheelbaseImageSource => Type switch
    {
        VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBaseHarvester.png",
        VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBaseArticulated.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/RadiusWheelBase.png"
    };

    /// <summary>
    /// Gets the image source for the antenna position diagram based on vehicle type
    /// </summary>
    public string AntennaImageSource => Type switch
    {
        VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaHarvester.png",
        VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaArticulated.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaTractor.png"
    };

    /// <summary>
    /// Gets a user-friendly display name for the current vehicle type
    /// </summary>
    public string VehicleTypeDisplayName => Type switch
    {
        VehicleType.Harvester => "Harvester",
        VehicleType.FourWD => "Articulated",
        _ => "Tractor"
    };
}
