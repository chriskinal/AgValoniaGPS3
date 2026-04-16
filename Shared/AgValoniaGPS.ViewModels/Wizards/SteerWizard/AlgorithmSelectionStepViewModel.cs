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
/// Step for selecting the guidance algorithm (Pure Pursuit vs Stanley).
/// </summary>
public class AlgorithmSelectionStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Guidance Algorithm";

    public override string Description =>
        "Choose your guidance algorithm. Pure Pursuit is simpler and a good default for most " +
        "setups - it uses a look-ahead point to calculate the steering arc. Stanley mode is " +
        "more responsive to cross-track error and works well at low speeds, but requires more " +
        "tuning. Steer Response Hold controls how far ahead the system looks (higher = smoother " +
        "but slower to correct). Stanley Aggressiveness controls correction strength in Stanley mode.";

    public override bool CanSkip => true;

    private bool _isStanleyMode;
    public bool IsStanleyMode
    {
        get => _isStanleyMode;
        set => SetProperty(ref _isStanleyMode, value);
    }

    private double _steerResponseHold;
    public double SteerResponseHold
    {
        get => _steerResponseHold;
        set => SetProperty(ref _steerResponseHold, value);
    }

    private double _stanleyAggressiveness;
    public double StanleyAggressiveness
    {
        get => _stanleyAggressiveness;
        set => SetProperty(ref _stanleyAggressiveness, value);
    }

    public AlgorithmSelectionStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        IsStanleyMode = autoSteer.IsStanleyMode;
        SteerResponseHold = autoSteer.SteerResponseHold;
        StanleyAggressiveness = autoSteer.StanleyAggressiveness;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.IsStanleyMode = IsStanleyMode;
        autoSteer.SteerResponseHold = SteerResponseHold;
        autoSteer.StanleyAggressiveness = StanleyAggressiveness;
    }

    public override Task<bool> ValidateAsync()
    {
        if (SteerResponseHold < 1 || SteerResponseHold > 10)
        {
            SetValidationError("Steer Response Hold must be between 1 and 10");
            return Task.FromResult(false);
        }

        if (StanleyAggressiveness < 0 || StanleyAggressiveness > 10)
        {
            SetValidationError("Stanley Aggressiveness must be between 0 and 10");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
