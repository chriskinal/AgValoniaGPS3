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
/// Step for configuring antenna pivot distance.
/// </summary>
public partial class AntennaPivotStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Antenna Pivot Distance";

    public override string Description =>
        "Enter the distance from the GPS antenna to the rear axle (pivot point). " +
        "Positive values mean the antenna is ahead of the rear axle, " +
        "negative values mean it's behind.";

    [ObservableProperty]
    private double _antennaPivot;

    [ObservableProperty]
    private string _unit = "m";

    public AntennaPivotStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        AntennaPivot = _configService.Store.Vehicle.AntennaPivot;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.AntennaPivot = AntennaPivot;
    }

    public override Task<bool> ValidateAsync()
    {
        if (AntennaPivot < -10 || AntennaPivot > 15)
        {
            SetValidationError("Antenna pivot distance should be between -10 and 15 meters");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
