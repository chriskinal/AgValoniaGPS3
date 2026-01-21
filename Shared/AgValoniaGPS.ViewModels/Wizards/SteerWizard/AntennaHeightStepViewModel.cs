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
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring antenna height.
/// </summary>
public partial class AntennaHeightStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Antenna Height";

    public override string Description =>
        "Enter the height of the GPS antenna above ground level. " +
        "This is used for terrain compensation calculations.";

    public override bool CanSkip => true; // Optional setting

    [ObservableProperty]
    private double _antennaHeight;

    [ObservableProperty]
    private string _unit = "m";

    public AntennaHeightStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        AntennaHeight = _configService.Store.Vehicle.AntennaHeight;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.AntennaHeight = AntennaHeight;
    }

    public override Task<bool> ValidateAsync()
    {
        if (AntennaHeight < 0)
        {
            SetValidationError("Antenna height cannot be negative");
            return Task.FromResult(false);
        }
        if (AntennaHeight > 10)
        {
            SetValidationError("Antenna height seems too large. Please check the value.");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
