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
/// Combined step for speed limits and optional sensor configuration.
/// </summary>
public class SpeedAndSensorsStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Speed and Sensors";

    public override string Description =>
        "Set the steering speed range and enable optional sensors. " +
        "Min speed prevents engagement while stationary, max speed is a safety cutoff.";

    public override bool CanSkip => true;

    // --- Speed Limits ---

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

    // --- Sensors ---

    private bool _turnSensorEnabled;
    public bool TurnSensorEnabled
    {
        get => _turnSensorEnabled;
        set => SetProperty(ref _turnSensorEnabled, value);
    }

    private bool _pressureSensorEnabled;
    public bool PressureSensorEnabled
    {
        get => _pressureSensorEnabled;
        set => SetProperty(ref _pressureSensorEnabled, value);
    }

    private bool _currentSensorEnabled;
    public bool CurrentSensorEnabled
    {
        get => _currentSensorEnabled;
        set => SetProperty(ref _currentSensorEnabled, value);
    }

    // --- Safety ---

    private bool _steerInReverse;
    public bool SteerInReverse
    {
        get => _steerInReverse;
        set => SetProperty(ref _steerInReverse, value);
    }

    private double _deadzoneHeading;
    public double DeadzoneHeading
    {
        get => _deadzoneHeading;
        set => SetProperty(ref _deadzoneHeading, value);
    }

    private bool _manualTurnsEnabled;
    public bool ManualTurnsEnabled
    {
        get => _manualTurnsEnabled;
        set => SetProperty(ref _manualTurnsEnabled, value);
    }

    public SpeedAndSensorsStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        MinSteerSpeed = autoSteer.MinSteerSpeed;
        MaxSteerSpeed = autoSteer.MaxSteerSpeed;
        TurnSensorEnabled = autoSteer.TurnSensorEnabled;
        PressureSensorEnabled = autoSteer.PressureSensorEnabled;
        CurrentSensorEnabled = autoSteer.CurrentSensorEnabled;
        SteerInReverse = autoSteer.SteerInReverse;
        DeadzoneHeading = autoSteer.DeadzoneHeading;
        ManualTurnsEnabled = autoSteer.ManualTurnsEnabled;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.MinSteerSpeed = MinSteerSpeed;
        autoSteer.MaxSteerSpeed = MaxSteerSpeed;
        autoSteer.TurnSensorEnabled = TurnSensorEnabled;
        autoSteer.PressureSensorEnabled = PressureSensorEnabled;
        autoSteer.CurrentSensorEnabled = CurrentSensorEnabled;
        autoSteer.SteerInReverse = SteerInReverse;
        autoSteer.DeadzoneHeading = DeadzoneHeading;
        autoSteer.ManualTurnsEnabled = ManualTurnsEnabled;
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
