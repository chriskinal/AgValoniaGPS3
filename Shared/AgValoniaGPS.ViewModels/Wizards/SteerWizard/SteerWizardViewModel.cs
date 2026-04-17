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
/// ViewModel for the Steer Configuration Wizard.
/// Guides users through AutoSteer setup in 10 combined steps.
/// </summary>
public class SteerWizardViewModel : WizardViewModel
{
    private readonly IConfigurationService _configService;

    public override string WizardTitle => "AutoSteer Configuration Wizard";

    public SteerWizardViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;

        // Step 1: Welcome
        AddStep(new WelcomeStepViewModel());

        // Step 2: Vehicle Type
        AddStep(new VehicleTypeStepViewModel(configService));

        // Step 3: Hardware Installed (GPS only / AutoSteer / Full)
        AddStep(new HardwareInstalledStepViewModel());

        // Step 4: Vehicle Dimensions (wheelbase + track width)
        AddStep(new VehicleDimensionsStepViewModel(configService));

        // Step 5: Antenna Position (pivot + height + offset)
        AddStep(new AntennaSetupStepViewModel(configService));

        // Step 6: Hardware Configuration (enable + motor + ADC + inversions + Danfoss)
        AddStep(new HardwareConfigStepViewModel(configService));

        // Step 7: Roll Calibration (IMU roll invert + zero)
        AddStep(new RollCalibrationStepViewModel(configService, autoSteerService));

        // Step 8: WAS Calibration (with live hardware access)
        AddStep(new WasCalibrationStepViewModel(configService, autoSteerService));

        // Step 9: Motor PWM Test (with live hardware access)
        AddStep(new PwmCalibrationStepViewModel(configService, autoSteerService));

        // Step 10: Steering Gains + Algorithm (with live hardware access)
        AddStep(new SteeringGainsStepViewModel(configService, autoSteerService));

        // Step 11: Speed Limits + Sensors
        AddStep(new SpeedAndSensorsStepViewModel(configService));

        // Step 12: Finish
        AddStep(new FinishStepViewModel());

        // Initialize navigation
        Initialize();
    }

    protected override Task OnCompletingAsync()
    {
        // Save all configuration changes
        _configService.SaveProfile(_configService.Store.ActiveProfileName);
        return Task.CompletedTask;
    }
}
