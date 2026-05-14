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
        set
        {
            // Push to the store immediately so PGN 251/252 emit the new
            // setting on the next AutoSteer config cycle. Without the
            // live write, the operator's toggle takes effect only when
            // they advance past the wizard step (OnLeaving), so live
            // WAS bar feedback in the gauge above doesn't match the
            // toggle state.
            if (SetProperty(ref _invertWas, value))
                _configService.Store.AutoSteer.InvertWas = value;
        }
    }

    private int _wasOffset;
    public int WasOffset
    {
        get => _wasOffset;
        set
        {
            // Same live push as InvertWas. The Zero WAS button captures
            // the current angle into this setter; the host then emits
            // PGN 251 with the new WasOffset on the next config cycle
            // so the module starts subtracting it from raw counts.
            if (SetProperty(ref _wasOffset, value))
                _configService.Store.AutoSteer.WasOffset = value;
        }
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
        if (_autoSteerService == null)
            return;

        // ActualSteerAngle from PGN 253 is the wheel angle the module
        // has already post-processed: rawCounts -> subtract WasOffset ->
        // divide by CountsPerDegree -> apply InvertWas sign. To drive
        // that reported angle back to zero we need a new offset that
        // accounts for the prior calibration, not one that replaces it.
        //
        // Forward direction (InvertWas = false):
        //     reported = (raw - offset) / cpd
        //     desired:  0 = (raw - offset') / cpd
        //          =>   offset' = raw = offset + reported * cpd
        //
        // Inverted direction (InvertWas = true):
        //     reported = -(raw - offset) / cpd
        //          =>   raw = offset - reported * cpd
        //          =>   offset' = offset - reported * cpd
        //
        // Unifying with a sign factor: offset' = offset + sign * reported * cpd
        double actualAngle = _autoSteerService.LastSteerData.ActualSteerAngle;
        var autoSteer = _configService.Store.AutoSteer;
        int sign = autoSteer.InvertWas ? -1 : +1;
        WasOffset = autoSteer.WasOffset + sign * (int)(actualAngle * autoSteer.CountsPerDegree);
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
