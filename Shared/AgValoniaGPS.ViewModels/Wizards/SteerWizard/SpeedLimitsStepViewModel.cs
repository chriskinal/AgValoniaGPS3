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

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring steering speed constraints.
/// </summary>
public class SpeedLimitsStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Speed Limits";

    public override string Description =>
        "Set the speed range for automatic steering. Min Steer Speed is the minimum ground " +
        "speed required before the system will engage steering - this prevents steering " +
        "activation while stationary. Max Steer Speed is the safety cutoff that disables " +
        "steering above this speed to prevent dangerous corrections at high speed.";

    public override bool CanSkip => true;

    private double _minSteerSpeed;
    public double MinSteerSpeed
    {
        get => _minSteerSpeed;
        set => SetProperty(ref _minSteerSpeed, value);
    }

    private double _maxSteerSpeed;
    public double MaxSteerSpeed
    {
        get => _maxSteerSpeed;
        set => SetProperty(ref _maxSteerSpeed, value);
    }

    public SpeedLimitsStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        MinSteerSpeed = autoSteer.MinSteerSpeed;
        MaxSteerSpeed = autoSteer.MaxSteerSpeed;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.MinSteerSpeed = MinSteerSpeed;
        autoSteer.MaxSteerSpeed = MaxSteerSpeed;
    }

    public override Task<bool> ValidateAsync()
    {
        if (MinSteerSpeed < 0)
        {
            SetValidationError("Min Steer Speed must be 0 or greater");
            return Task.FromResult(false);
        }

        if (MaxSteerSpeed <= 0)
        {
            SetValidationError("Max Steer Speed must be greater than 0");
            return Task.FromResult(false);
        }

        if (MaxSteerSpeed <= MinSteerSpeed)
        {
            SetValidationError("Max Steer Speed must be greater than Min Steer Speed");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
