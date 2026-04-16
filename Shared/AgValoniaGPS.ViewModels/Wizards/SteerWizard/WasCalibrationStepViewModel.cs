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
/// Step for WAS (Wheel Angle Sensor) calibration settings.
/// </summary>
public class WasCalibrationStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Wheel Angle Sensor";

    public override string Description =>
        "Configure your Wheel Angle Sensor (WAS). The WAS Offset zeros the sensor when the " +
        "wheels are pointed straight ahead. Counts Per Degree (CPD) defines how many sensor " +
        "counts equal one degree of steering angle - measure this by turning the wheels a known " +
        "angle and dividing the count change by the degrees turned. Max Steer Angle limits " +
        "the commanded steering range for safety.";

    private int _wasOffset;
    public int WasOffset
    {
        get => _wasOffset;
        set => SetProperty(ref _wasOffset, value);
    }

    private double _countsPerDegree;
    public double CountsPerDegree
    {
        get => _countsPerDegree;
        set => SetProperty(ref _countsPerDegree, value);
    }

    private int _maxSteerAngle;
    public int MaxSteerAngle
    {
        get => _maxSteerAngle;
        set => SetProperty(ref _maxSteerAngle, value);
    }

    public WasCalibrationStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        WasOffset = autoSteer.WasOffset;
        CountsPerDegree = autoSteer.CountsPerDegree;
        MaxSteerAngle = autoSteer.MaxSteerAngle;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.WasOffset = WasOffset;
        autoSteer.CountsPerDegree = CountsPerDegree;
        autoSteer.MaxSteerAngle = MaxSteerAngle;
    }

    public override Task<bool> ValidateAsync()
    {
        if (CountsPerDegree < 1 || CountsPerDegree > 255)
        {
            SetValidationError("Counts Per Degree must be between 1 and 255");
            return Task.FromResult(false);
        }

        if (MaxSteerAngle < 10 || MaxSteerAngle > 90)
        {
            SetValidationError("Max Steer Angle must be between 10 and 90 degrees");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
