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

using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for selecting the vehicle type.
/// Options: Tractor, Harvester, or Articulated 4WD.
/// This determines steering geometry, antenna positioning, and wheelbase configuration.
/// </summary>
public class VehicleTypeStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Vehicle Type";

    public override string Description =>
        "Select your vehicle type. This determines steering geometry, " +
        "antenna positioning, and wheelbase configuration.";

    public override bool CanSkip => false;

    private VehicleType _vehicleType;
    public VehicleType VehicleType
    {
        get => _vehicleType;
        set
        {
            SetProperty(ref _vehicleType, value);
            OnPropertyChanged(nameof(IsTractorSelected));
            OnPropertyChanged(nameof(IsHarvesterSelected));
            OnPropertyChanged(nameof(IsFourWDSelected));
        }
    }

    /// <summary>
    /// True when Tractor is selected.
    /// </summary>
    public bool IsTractorSelected => VehicleType == VehicleType.Tractor;

    /// <summary>
    /// True when Harvester is selected.
    /// </summary>
    public bool IsHarvesterSelected => VehicleType == VehicleType.Harvester;

    /// <summary>
    /// True when Articulated 4WD is selected.
    /// </summary>
    public bool IsFourWDSelected => VehicleType == VehicleType.FourWD;

    public VehicleTypeStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        VehicleType = _configService.Store.Vehicle.Type;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.Type = VehicleType;
    }

    public void SelectTractor() => VehicleType = VehicleType.Tractor;
    public void SelectHarvester() => VehicleType = VehicleType.Harvester;
    public void SelectFourWD() => VehicleType = VehicleType.FourWD;

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
