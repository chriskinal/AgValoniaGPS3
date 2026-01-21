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
/// Step for configuring Danfoss hydraulic valve support.
/// </summary>
public partial class DanfossStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Danfoss Valve";

    public override string Description =>
        "Enable this if you're using a Danfoss hydraulic steering valve. " +
        "Danfoss valves require special PWM signal timing. " +
        "Leave disabled for standard motor or hydraulic setups.";

    public override bool CanSkip => true;

    [ObservableProperty]
    private bool _danfossEnabled;

    public DanfossStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        DanfossEnabled = _configService.Store.AutoSteer.DanfossEnabled;
    }

    protected override void OnLeaving()
    {
        _configService.Store.AutoSteer.DanfossEnabled = DanfossEnabled;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
