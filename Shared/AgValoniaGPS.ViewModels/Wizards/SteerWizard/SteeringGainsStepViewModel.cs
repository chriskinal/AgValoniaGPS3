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

    public override string Title => "Steering Gains";

    public override string Description =>
        "Configure the steering controller gains. Proportional Gain (P) controls how aggressively " +
        "the system corrects steering error - a good starting value is 10. Integral Gain (I) " +
        "accumulates small persistent errors over time - start at 0 and only increase if the " +
        "system consistently drifts to one side. Too much I gain causes oscillation.";

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
        ProportionalGain = autoSteer.ProportionalGain;
        IntegralGain = autoSteer.IntegralGain;

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
    }

    protected override void OnLeaving()
    {
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated -= OnAutoSteerStateUpdated;

        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.ProportionalGain = ProportionalGain;
        autoSteer.IntegralGain = IntegralGain;
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

        ClearValidation();
        return Task.FromResult(true);
    }
}
