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
/// Step for configuring motor/valve PWM output limits.
/// </summary>
public class PwmCalibrationStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Motor PWM Settings";

    public override string Description =>
        "Configure the PWM (Pulse Width Modulation) limits for your steering motor or hydraulic " +
        "valve. Max PWM sets the maximum drive signal at full correction. Min PWM sets the " +
        "minimum signal needed to overcome static friction and start moving the steering - " +
        "if set too low the motor will stall at small corrections.";

    public override bool CanSkip => true;

    private int _maxPwm;
    public int MaxPwm
    {
        get => _maxPwm;
        set => SetProperty(ref _maxPwm, value);
    }

    private int _minPwm;
    public int MinPwm
    {
        get => _minPwm;
        set => SetProperty(ref _minPwm, value);
    }

    public PwmCalibrationStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        MaxPwm = autoSteer.MaxPwm;
        MinPwm = autoSteer.MinPwm;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.MaxPwm = MaxPwm;
        autoSteer.MinPwm = MinPwm;
    }

    public override Task<bool> ValidateAsync()
    {
        if (MaxPwm < 50 || MaxPwm > 255)
        {
            SetValidationError("Max PWM must be between 50 and 255");
            return Task.FromResult(false);
        }

        if (MinPwm < 1 || MinPwm > 50)
        {
            SetValidationError("Min PWM must be between 1 and 50");
            return Task.FromResult(false);
        }

        if (MaxPwm <= MinPwm)
        {
            SetValidationError("Max PWM must be greater than Min PWM");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
