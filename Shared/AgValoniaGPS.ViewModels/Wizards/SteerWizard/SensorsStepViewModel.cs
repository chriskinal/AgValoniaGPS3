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
/// Step for configuring optional turn, pressure, and current sensors.
/// </summary>
public class SensorsStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Optional Sensors";

    public override string Description =>
        "Enable optional sensors if your hardware supports them. The Turn Sensor detects " +
        "steering wheel movement via an encoder. The Pressure Sensor monitors hydraulic " +
        "pressure for stall detection. The Current Sensor monitors motor current draw " +
        "to detect obstructions. Leave all disabled if your setup does not have these sensors.";

    public override bool CanSkip => true;

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

    public SensorsStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        TurnSensorEnabled = autoSteer.TurnSensorEnabled;
        PressureSensorEnabled = autoSteer.PressureSensorEnabled;
        CurrentSensorEnabled = autoSteer.CurrentSensorEnabled;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.TurnSensorEnabled = TurnSensorEnabled;
        autoSteer.PressureSensorEnabled = PressureSensorEnabled;
        autoSteer.CurrentSensorEnabled = CurrentSensorEnabled;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
