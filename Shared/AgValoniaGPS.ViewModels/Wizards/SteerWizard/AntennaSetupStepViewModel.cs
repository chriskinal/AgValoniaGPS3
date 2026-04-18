// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Threading.Tasks;
using System.Windows.Input;

using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Combined step for all antenna dimensions: pivot distance, height, and lateral offset.
/// Shows the antenna icon matching the current vehicle type.
/// </summary>
public class AntennaSetupStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Antenna Position";

    public override string Description =>
        "Configure your GPS antenna position on the vehicle.";

    private double _antennaPivot;
    /// <summary>Distance from antenna to rear axle. Positive = ahead of axle.</summary>
    public double AntennaPivot
    {
        get => _antennaPivot;
        set => SetProperty(ref _antennaPivot, value);
    }

    private double _antennaHeight;
    /// <summary>Height of antenna above ground (meters).</summary>
    public double AntennaHeight
    {
        get => _antennaHeight;
        set => SetProperty(ref _antennaHeight, value);
    }

    private double _antennaOffset;
    /// <summary>Lateral offset from centerline. Positive = right.</summary>
    public double AntennaOffset
    {
        get => _antennaOffset;
        set
        {
            SetProperty(ref _antennaOffset, value);
            OnPropertyChanged(nameof(IsLeft));
            OnPropertyChanged(nameof(IsCenter));
            OnPropertyChanged(nameof(IsRight));
        }
    }

    public bool IsLeft => AntennaOffset < 0;
    public bool IsCenter => Math.Abs(AntennaOffset) < 0.01;
    public bool IsRight => AntennaOffset > 0;

    public ICommand SetLeftCommand { get; }
    public ICommand SetCenterCommand { get; }
    public ICommand SetRightCommand { get; }

    /// <summary>Antenna icon path matching the current vehicle type.</summary>
    public string AntennaIconSource => _configService.Store.Vehicle.AntennaImageSource;

    /// <summary>Top crop: pivot distance + height view.</summary>
    public string AntennaTopImageSource => _configService.Store.Vehicle.Type switch
    {
        Models.VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaHarvesterTop.png",
        Models.VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaArticulatedTop.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaTractorTop.png"
    };

    /// <summary>Bottom-left crop: lateral offset view.</summary>
    public string AntennaOffsetImageSource => _configService.Store.Vehicle.Type switch
    {
        Models.VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaHarvesterOffset.png",
        Models.VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaArticulatedOffset.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/AntennaTractorOffset.png"
    };

    public AntennaSetupStepViewModel(IConfigurationService configService)
    {
        _configService = configService;

        SetLeftCommand = new RelayCommand(() => { AntennaOffset = -0.5; });
        SetCenterCommand = new RelayCommand(() => { AntennaOffset = 0; });
        SetRightCommand = new RelayCommand(() => { AntennaOffset = 0.5; });
    }

    protected override void OnEntering()
    {
        var vehicle = _configService.Store.Vehicle;
        AntennaPivot = vehicle.AntennaPivot;
        AntennaHeight = vehicle.AntennaHeight;
        AntennaOffset = vehicle.AntennaOffset;
        OnPropertyChanged(nameof(AntennaIconSource));
    }

    protected override void OnLeaving()
    {
        var vehicle = _configService.Store.Vehicle;
        vehicle.AntennaPivot = AntennaPivot;
        vehicle.AntennaHeight = AntennaHeight;
        vehicle.AntennaOffset = AntennaOffset;
    }

    public override Task<bool> ValidateAsync()
    {
        if (AntennaPivot < -10 || AntennaPivot > 15)
        {
            SetValidationError("Antenna pivot distance must be between -10 and 15 meters");
            return Task.FromResult(false);
        }

        if (AntennaHeight < 0 || AntennaHeight > 10)
        {
            SetValidationError("Antenna height must be between 0 and 10 meters");
            return Task.FromResult(false);
        }

        if (AntennaOffset < -5 || AntennaOffset > 5)
        {
            SetValidationError("Antenna offset must be between -5 and 5 meters");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
