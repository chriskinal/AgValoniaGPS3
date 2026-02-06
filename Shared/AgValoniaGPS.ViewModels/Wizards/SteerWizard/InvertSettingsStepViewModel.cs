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
/// Step for configuring signal inversions.
/// Allows inverting WAS, motor direction, and relay outputs.
/// </summary>
public class InvertSettingsStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Signal Inversions";

    public override string Description =>
        "Configure signal inversions if your hardware requires it. " +
        "If the steering moves the wrong direction or sensors read backwards, " +
        "enable the appropriate inversion.";

    public override bool CanSkip => true;

    private bool _invertWas;
    public bool InvertWas
    {
        get => _invertWas;
        set => this.RaiseAndSetIfChanged(ref _invertWas, value);
    }

    private bool _invertMotor;
    public bool InvertMotor
    {
        get => _invertMotor;
        set => this.RaiseAndSetIfChanged(ref _invertMotor, value);
    }

    private bool _invertRelays;
    public bool InvertRelays
    {
        get => _invertRelays;
        set => this.RaiseAndSetIfChanged(ref _invertRelays, value);
    }

    public InvertSettingsStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        InvertWas = autoSteer.InvertWas;
        InvertMotor = autoSteer.InvertMotor;
        InvertRelays = autoSteer.InvertRelays;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.InvertWas = InvertWas;
        autoSteer.InvertMotor = InvertMotor;
        autoSteer.InvertRelays = InvertRelays;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
