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
using System.Windows.Input;

using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for calibrating motor/valve PWM settings with free-drive testing.
/// </summary>
public class PwmCalibrationStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;

    public override string Title => "Motor PWM Settings";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    public override string Description =>
        "Configure PWM limits for your steering motor or valve. " +
        "Use Free Drive mode to test motor response - the buttons send steering commands " +
        "directly to the motor so you can verify it moves correctly.";

    public override bool CanSkip => true;

    private int _maxPwm;
    public int MaxPwm
    {
        get => _maxPwm;
        set => SetProperty(ref _maxPwm, value);
    }

    private int _minPwm;
    public int MinPwm
    {
        get => _minPwm;
        set => SetProperty(ref _minPwm, value);
    }

    private bool _invertMotor;
    public bool InvertMotor
    {
        get => _invertMotor;
        set => SetProperty(ref _invertMotor, value);
    }

    private bool _isFreeDriveActive;
    /// <summary>Whether free-drive motor test mode is active.</summary>
    public bool IsFreeDriveActive
    {
        get => _isFreeDriveActive;
        set => SetProperty(ref _isFreeDriveActive, value);
    }

    private double _freeDriveAngle;
    /// <summary>Free-drive steer angle (-40 to +40 degrees).</summary>
    public double FreeDriveAngle
    {
        get => _freeDriveAngle;
        set
        {
            if (SetProperty(ref _freeDriveAngle, value))
                _autoSteerService?.SetFreeDriveAngle(value);
        }
    }

    private double _liveSteerAngle;
    /// <summary>Live steer angle feedback from PGN 253.</summary>
    public double LiveSteerAngle
    {
        get => _liveSteerAngle;
        set => SetProperty(ref _liveSteerAngle, value);
    }

    /// <summary>True when hardware is connected and sending data.</summary>
    public bool HasHardware => _autoSteerService != null;

    public ICommand ToggleFreeDriveCommand { get; }
    public ICommand FreeDriveLeftCommand { get; }
    public ICommand FreeDriveRightCommand { get; }
    public ICommand FreeDriveCenterCommand { get; }

    public PwmCalibrationStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;

        ToggleFreeDriveCommand = new RelayCommand(ToggleFreeDrive);
        FreeDriveLeftCommand = new RelayCommand(() => FreeDriveAngle = Math.Max(-40, FreeDriveAngle - 5));
        FreeDriveRightCommand = new RelayCommand(() => FreeDriveAngle = Math.Min(40, FreeDriveAngle + 5));
        FreeDriveCenterCommand = new RelayCommand(() => FreeDriveAngle = 0);
    }

    private void ToggleFreeDrive()
    {
        if (_autoSteerService == null) return;

        if (IsFreeDriveActive)
        {
            _autoSteerService.DisableFreeDrive();
            IsFreeDriveActive = false;
            FreeDriveAngle = 0;
        }
        else
        {
            _autoSteerService.EnableFreeDrive();
            IsFreeDriveActive = true;
            FreeDriveAngle = 0;
        }
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        MaxPwm = autoSteer.MaxPwm;
        MinPwm = autoSteer.MinPwm;
        InvertMotor = autoSteer.InvertMotor;
        FreeDriveAngle = 0;
        IsFreeDriveActive = false;

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
    }

    protected override void OnLeaving()
    {
        // Ensure free drive is disabled when leaving
        if (IsFreeDriveActive && _autoSteerService != null)
        {
            _autoSteerService.DisableFreeDrive();
            IsFreeDriveActive = false;
        }

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated -= OnAutoSteerStateUpdated;

        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.MaxPwm = MaxPwm;
        autoSteer.MinPwm = MinPwm;
        autoSteer.InvertMotor = InvertMotor;
    }

    private void OnAutoSteerStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        // Show actual WAS angle from hardware for motor response feedback
        LiveSteerAngle = Math.Round(_autoSteerService!.LastSteerData.ActualSteerAngle, 1);
    }

    public override Task<bool> ValidateAsync()
    {
        if (MaxPwm < 50 || MaxPwm > 255)
        {
            SetValidationError("Max PWM must be between 50 and 255");
            return Task.FromResult(false);
        }

        if (MinPwm < 1 || MinPwm > 50)
        {
            SetValidationError("Min PWM must be between 1 and 50");
            return Task.FromResult(false);
        }

        if (MaxPwm <= MinPwm)
        {
            SetValidationError("Max PWM must be greater than Min PWM");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
