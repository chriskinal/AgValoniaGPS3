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

    /// <summary>
    /// Persistent status bar showing live hardware data across all wizard steps.
    /// </summary>
    public override WizardStatusBarViewModel? StatusBar { get; }

    public SteerWizardViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        StatusBar = new WizardStatusBarViewModel(autoSteerService);

        // Step 1: Welcome
        AddStep(new WelcomeStepViewModel());

        // Step 2: Vehicle Type
        AddStep(new VehicleTypeStepViewModel(configService));

        // Step 3: Hardware Installed (GPS only / AutoSteer / Full)
        var hardwareStep = new HardwareInstalledStepViewModel();
        AddStep(hardwareStep);

        // Step 4: Vehicle Dimensions (wheelbase + track width)
        AddStep(new VehicleDimensionsStepViewModel(configService));

        // Step 5: Antenna Position (pivot + height + offset)
        AddStep(new AntennaSetupStepViewModel(configService));

        // Steps 6-10: AutoSteer-only steps (skipped when GPS Only)
        var hwConfig = new HardwareConfigStepViewModel(configService);
        hwConfig.SetHardwareStep(hardwareStep);
        AddStep(hwConfig);

        var rollCal = new RollCalibrationStepViewModel(configService, autoSteerService);
        rollCal.SetHardwareStep(hardwareStep);
        AddStep(rollCal);

        var wasCal = new WasCalibrationStepViewModel(configService, autoSteerService);
        wasCal.SetHardwareStep(hardwareStep);
        AddStep(wasCal);

        var autoCal = new AutoMotorCalibrationStepViewModel(configService, autoSteerService);
        autoCal.SetHardwareStep(hardwareStep);
        AddStep(autoCal);

        // Step 10: Maximum Steering Angle (split out of the old combined
        // motor cal step so the stress-style full-lock test gets its own
        // operator-facing page with distinct warnings).
        var maxSteer = new MaxSteeringAngleStepViewModel(configService, autoSteerService);
        maxSteer.SetHardwareStep(hardwareStep);
        AddStep(maxSteer);

        var cpdCircle = new CpdCircleTestStepViewModel(configService, autoSteerService);
        cpdCircle.SetHardwareStep(hardwareStep);
        AddStep(cpdCircle);

        var ackermannTest = new AckermannTestStepViewModel(configService, autoSteerService);
        ackermannTest.SetHardwareStep(hardwareStep);
        AddStep(ackermannTest);

        var steerGains = new SteeringGainsStepViewModel(configService, autoSteerService);
        steerGains.SetHardwareStep(hardwareStep);
        AddStep(steerGains);

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
        var store = _configService.Store;
        _configService.SaveProfiles(store.ActiveVehicleProfileName, store.ActiveToolProfileName);
        return Task.CompletedTask;
    }
}
