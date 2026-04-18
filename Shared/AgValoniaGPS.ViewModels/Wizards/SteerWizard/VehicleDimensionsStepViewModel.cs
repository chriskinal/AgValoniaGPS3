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

using System.Threading.Tasks;

using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Combined step for vehicle wheelbase and track width.
/// Shows a vehicle-type-dependent diagram with both input fields.
/// </summary>
public class VehicleDimensionsStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Vehicle Dimensions";

    public override string Description =>
        "Enter the wheelbase (front to rear axle) and track width " +
        "(left to right wheel center) of your vehicle.";

    private double _wheelbase;
    public double Wheelbase
    {
        get => _wheelbase;
        set => SetProperty(ref _wheelbase, value);
    }

    private double _trackWidth;
    public double TrackWidth
    {
        get => _trackWidth;
        set => SetProperty(ref _trackWidth, value);
    }

    /// <summary>
    /// Wheelbase diagram image path matching the current vehicle type.
    /// </summary>
    public string WheelbaseImageSource => _configService.Store.Vehicle.WheelbaseImageSource;

    /// <summary>
    /// Cropped wheelbase diagram focused on the wheel area.
    /// </summary>
    public string WheelbaseCropImageSource => _configService.Store.Vehicle.Type switch
    {
        Models.VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/WheelbaseHarvester.png",
        Models.VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/WheelbaseArticulated.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/WheelbaseTractor.png"
    };

    public string TrackWidthImageSource => _configService.Store.Vehicle.Type switch
    {
        Models.VehicleType.Harvester => "avares://AgValoniaGPS.Views/Assets/Icons/TrackWidthHarvester.png",
        Models.VehicleType.FourWD => "avares://AgValoniaGPS.Views/Assets/Icons/TrackWidthArticulated.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/TrackWidthTractor.png"
    };

    public VehicleDimensionsStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var vehicle = _configService.Store.Vehicle;
        Wheelbase = vehicle.Wheelbase;
        TrackWidth = vehicle.TrackWidth;
        OnPropertyChanged(nameof(WheelbaseImageSource));
    }

    protected override void OnLeaving()
    {
        var vehicle = _configService.Store.Vehicle;
        vehicle.Wheelbase = Wheelbase;
        vehicle.TrackWidth = TrackWidth;
    }

    public override Task<bool> ValidateAsync()
    {
        if (Wheelbase < 0.5)
        {
            SetValidationError("Wheelbase must be at least 0.5 meters");
            return Task.FromResult(false);
        }
        if (Wheelbase > 15)
        {
            SetValidationError("Wheelbase seems too large. Please check the value.");
            return Task.FromResult(false);
        }

        if (TrackWidth < 0.5)
        {
            SetValidationError("Track width must be at least 0.5 meters");
            return Task.FromResult(false);
        }
        if (TrackWidth > 10)
        {
            SetValidationError("Track width seems too large. Please check the value.");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
