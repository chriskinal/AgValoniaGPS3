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
/// Guides users through AutoSteer setup step by step.
/// </summary>
public class SteerWizardViewModel : WizardViewModel
{
    private readonly IConfigurationService _configService;

    public override string WizardTitle => "AutoSteer Configuration Wizard";

    public SteerWizardViewModel(IConfigurationService configService)
    {
        _configService = configService;

        // Group A: Introduction
        AddStep(new WelcomeStepViewModel());

        // Group B: Vehicle Dimensions
        AddStep(new WheelbaseStepViewModel(configService));
        AddStep(new TrackWidthStepViewModel(configService));
        AddStep(new AntennaPivotStepViewModel(configService));
        AddStep(new AntennaHeightStepViewModel(configService));
        AddStep(new AntennaOffsetStepViewModel(configService));

        // Group C: Hardware Configuration
        AddStep(new SteerEnableStepViewModel(configService));
        AddStep(new MotorDriverStepViewModel(configService));
        AddStep(new ADConverterStepViewModel(configService));
        AddStep(new InvertSettingsStepViewModel(configService));
        AddStep(new DanfossStepViewModel(configService));

        // Group G: Completion
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
