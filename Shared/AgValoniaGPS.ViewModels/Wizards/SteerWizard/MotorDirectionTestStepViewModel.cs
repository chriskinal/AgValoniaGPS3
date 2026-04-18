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

using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for testing motor direction with brief pulses.
/// The user presses Left or Right to send a 0.5s motor pulse,
/// then verifies the wheels moved in the correct direction.
/// </summary>
public class MotorDirectionTestStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;

    public override string Title => "Motor Direction Test";

    public override string Description =>
        "Press Left or Right to briefly pulse the motor. " +
        "Verify the wheels move in the correct direction. If backwards, enable Invert Motor.";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    private bool _invertMotor;
    public bool InvertMotor
    {
        get => _invertMotor;
        set => SetProperty(ref _invertMotor, value);
    }

    private double _liveSteerAngle;
    public double LiveSteerAngle
    {
        get => _liveSteerAngle;
        set => SetProperty(ref _liveSteerAngle, value);
    }

    private bool _isPulsing;
    public bool IsPulsing
    {
        get => _isPulsing;
        set => SetProperty(ref _isPulsing, value);
    }

    private string _pulseStatus = "";
    public string PulseStatus
    {
        get => _pulseStatus;
        set => SetProperty(ref _pulseStatus, value);
    }

    /// <summary>True when hardware is connected and sending data.</summary>
    public bool HasHardware => _autoSteerService != null;

    public ICommand PulseLeftCommand { get; }
    public ICommand PulseRightCommand { get; }

    public MotorDirectionTestStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;
        PulseLeftCommand = new AsyncRelayCommand(() => PulseMotor(-20));
        PulseRightCommand = new AsyncRelayCommand(() => PulseMotor(20));
    }

    private async Task PulseMotor(double angle)
    {
        if (_autoSteerService == null || IsPulsing) return;
        IsPulsing = true;
        PulseStatus = angle > 0 ? "Pulsing RIGHT..." : "Pulsing LEFT...";
        _autoSteerService.EnableFreeDrive();
        _autoSteerService.SetFreeDriveAngle(angle);
        await Task.Delay(500);
        _autoSteerService.SetFreeDriveAngle(0);
        _autoSteerService.DisableFreeDrive();
        PulseStatus = "";
        IsPulsing = false;
    }

    protected override void OnEntering()
    {
        InvertMotor = _configService.Store.AutoSteer.InvertMotor;
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnStateUpdated;
    }

    protected override void OnLeaving()
    {
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated -= OnStateUpdated;

        // Ensure free drive is off
        if (IsPulsing && _autoSteerService != null)
        {
            _autoSteerService.SetFreeDriveAngle(0);
            _autoSteerService.DisableFreeDrive();
            IsPulsing = false;
        }

        _configService.Store.AutoSteer.InvertMotor = InvertMotor;
    }

    private void OnStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        LiveSteerAngle = Math.Round(_autoSteerService!.LastSteerData.ActualSteerAngle, 1);
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
