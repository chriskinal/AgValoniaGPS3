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
using ReactiveUI;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring how AutoSteer is enabled.
/// Options: None (software only), Switch (physical toggle), Button (momentary)
/// </summary>
public class SteerEnableStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Steer Enable Method";

    public override string Description =>
        "Select how AutoSteer will be enabled. A physical switch or button provides " +
        "a hardware safety override. 'None' uses software-only control.";

    public override bool CanSkip => false;

    private int _externalEnable;
    public int ExternalEnable
    {
        get => _externalEnable;
        set
        {
            this.RaiseAndSetIfChanged(ref _externalEnable, value);
            this.RaisePropertyChanged(nameof(IsNoneSelected));
            this.RaisePropertyChanged(nameof(IsSwitchSelected));
            this.RaisePropertyChanged(nameof(IsButtonSelected));
        }
    }

    /// <summary>
    /// True when no external enable is selected (software only).
    /// </summary>
    public bool IsNoneSelected => ExternalEnable == 0;

    /// <summary>
    /// True when switch enable is selected.
    /// </summary>
    public bool IsSwitchSelected => ExternalEnable == 1;

    /// <summary>
    /// True when button enable is selected.
    /// </summary>
    public bool IsButtonSelected => ExternalEnable == 2;

    public SteerEnableStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        ExternalEnable = _configService.Store.AutoSteer.ExternalEnable;
    }

    protected override void OnLeaving()
    {
        _configService.Store.AutoSteer.ExternalEnable = ExternalEnable;
    }

    public void SelectNone() => ExternalEnable = 0;
    public void SelectSwitch() => ExternalEnable = 1;
    public void SelectButton() => ExternalEnable = 2;

    public override Task<bool> ValidateAsync()
    {
        if (ExternalEnable < 0 || ExternalEnable > 2)
        {
            SetValidationError("Please select a valid enable method");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
