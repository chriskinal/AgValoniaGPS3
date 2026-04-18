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
/// Step for WAS (Wheel Angle Sensor) calibration with live sensor reading.
/// </summary>
public class WasCalibrationStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;

    public override string Title => "Wheel Angle Sensor";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    public override string Description =>
        "1. Point wheels straight ahead\n" +
        "2. Press 'Zero WAS' to set the zero position\n" +
        "3. Turn wheels RIGHT - the angle should read POSITIVE\n" +
        "4. If it reads negative, enable 'Invert WAS'";

    private bool _invertWas;
    /// <summary>Invert WAS direction if steering reads backwards.</summary>
    public bool InvertWas
    {
        get => _invertWas;
        set => SetProperty(ref _invertWas, value);
    }

    private int _wasOffset;
    public int WasOffset
    {
        get => _wasOffset;
        set => SetProperty(ref _wasOffset, value);
    }

    private double _liveSteerAngle;
    /// <summary>Live steer angle from PGN 253 (degrees).</summary>
    public double LiveSteerAngle
    {
        get => _liveSteerAngle;
        set => SetProperty(ref _liveSteerAngle, value);
    }

    private bool _hasLiveData;
    /// <summary>True if live sensor data is being received.</summary>
    public bool HasLiveData
    {
        get => _hasLiveData;
        set => SetProperty(ref _hasLiveData, value);
    }

    public ICommand ZeroWasCommand { get; }

    public WasCalibrationStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;

        ZeroWasCommand = new RelayCommand(ZeroWas);
    }

    private void ZeroWas()
    {
        // Capture the current live angle as the new zero offset
        // The WAS offset is applied so that the current reading becomes 0
        if (_autoSteerService != null)
        {
            // Use actual WAS angle from PGN 253 (hardware sensor reading)
            double actualAngle = _autoSteerService.LastSteerData.ActualSteerAngle;
            // The actual angle * CPD gives approximate raw counts to zero
            double cpd = _configService.Store.AutoSteer.CountsPerDegree;
            WasOffset = (int)(actualAngle * cpd);
        }
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        InvertWas = autoSteer.InvertWas;
        WasOffset = autoSteer.WasOffset;

        // Start listening for live data
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
    }

    protected override void OnLeaving()
    {
        // Stop listening
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated -= OnAutoSteerStateUpdated;

        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.InvertWas = InvertWas;
        autoSteer.WasOffset = WasOffset;
    }

    private void OnAutoSteerStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        // Show actual WAS angle from hardware (PGN 253), not commanded angle
        LiveSteerAngle = Math.Round(_autoSteerService!.LastSteerData.ActualSteerAngle, 1);
        HasLiveData = true;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
