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
/// Step for selecting the motor driver type.
/// Options: IBT2 (dual H-bridge) or Cytron (single direction PWM)
/// </summary>
public partial class MotorDriverStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Motor Driver";

    public override string Description =>
        "Select the type of motor driver used for steering. " +
        "IBT2 is a dual H-bridge driver (most common). " +
        "Cytron uses a single direction PWM signal.";

    public override bool CanSkip => false;

    [ObservableProperty]
    private int _motorDriver;

    /// <summary>
    /// True when IBT2 driver is selected.
    /// </summary>
    public bool IsIBT2Selected => MotorDriver == 0;

    /// <summary>
    /// True when Cytron driver is selected.
    /// </summary>
    public bool IsCytronSelected => MotorDriver == 1;

    public MotorDriverStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        MotorDriver = _configService.Store.AutoSteer.MotorDriver;
    }

    protected override void OnLeaving()
    {
        _configService.Store.AutoSteer.MotorDriver = MotorDriver;
    }

    partial void OnMotorDriverChanged(int value)
    {
        OnPropertyChanged(nameof(IsIBT2Selected));
        OnPropertyChanged(nameof(IsCytronSelected));
    }

    public void SelectIBT2() => MotorDriver = 0;
    public void SelectCytron() => MotorDriver = 1;

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
