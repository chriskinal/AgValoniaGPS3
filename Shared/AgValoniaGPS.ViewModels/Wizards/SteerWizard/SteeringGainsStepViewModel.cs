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

using System;
using System.Threading.Tasks;

using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring steering PID gains with live angle feedback.
/// </summary>
public class SteeringGainsStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;

    public override string Title => "Steering Gains";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    public override string Description =>
        "Choose your guidance algorithm and configure steering gains. Pure Pursuit is a good " +
        "default; Stanley is more responsive at low speeds. Proportional Gain (P) controls " +
        "correction aggressiveness. Integral Gain (I) corrects persistent drift.";

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

    private int _proportionalGain;
    public int ProportionalGain
    {
        get => _proportionalGain;
        set => SetProperty(ref _proportionalGain, value);
    }

    private double _integralGain;
    public double IntegralGain
    {
        get => _integralGain;
        set => SetProperty(ref _integralGain, value);
    }

    private double _sideHillCompensation;
    /// <summary>Degrees per degree of roll (0-1.0).</summary>
    public double SideHillCompensation
    {
        get => _sideHillCompensation;
        set => SetProperty(ref _sideHillCompensation, value);
    }

    private double _liveSteerAngle;
    /// <summary>Live actual steer angle from PGN 253.</summary>
    public double LiveSteerAngle
    {
        get => _liveSteerAngle;
        set => SetProperty(ref _liveSteerAngle, value);
    }

    private double _liveSteerError;
    /// <summary>Difference between commanded and actual angle.</summary>
    public double LiveSteerError
    {
        get => _liveSteerError;
        set => SetProperty(ref _liveSteerError, value);
    }

    public SteeringGainsStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        IsStanleyMode = autoSteer.IsStanleyMode;
        SteerResponseHold = autoSteer.SteerResponseHold;
        StanleyAggressiveness = autoSteer.StanleyAggressiveness;
        ProportionalGain = autoSteer.ProportionalGain;
        IntegralGain = autoSteer.IntegralGain;
        SideHillCompensation = autoSteer.SideHillCompensation;

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
    }

    protected override void OnLeaving()
    {
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated -= OnAutoSteerStateUpdated;

        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.IsStanleyMode = IsStanleyMode;
        autoSteer.SteerResponseHold = SteerResponseHold;
        autoSteer.StanleyAggressiveness = StanleyAggressiveness;
        autoSteer.ProportionalGain = ProportionalGain;
        autoSteer.IntegralGain = IntegralGain;
        autoSteer.SideHillCompensation = SideHillCompensation;
    }

    private void OnAutoSteerStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        double actual = _autoSteerService!.LastSteerData.ActualSteerAngle;
        LiveSteerAngle = Math.Round(actual, 1);
        LiveSteerError = Math.Round(Math.Abs(snapshot.SteerAngle - actual), 1);
    }

    public override Task<bool> ValidateAsync()
    {
        if (ProportionalGain < 1 || ProportionalGain > 100)
        {
            SetValidationError("Proportional Gain must be between 1 and 100");
            return Task.FromResult(false);
        }

        if (IntegralGain < 0 || IntegralGain > 1.0)
        {
            SetValidationError("Integral Gain must be between 0 and 1.0");
            return Task.FromResult(false);
        }

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

        if (SideHillCompensation < 0 || SideHillCompensation > 1.0)
        {
            SetValidationError("Side Hill Compensation must be between 0 and 1.0");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
