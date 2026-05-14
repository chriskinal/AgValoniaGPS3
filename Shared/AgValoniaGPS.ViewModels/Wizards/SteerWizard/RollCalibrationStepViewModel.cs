// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Threading.Tasks;
using System.Windows.Input;

using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Roll/IMU calibration: inversion and zero offset.
/// </summary>
public class RollCalibrationStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;

    public override string Title => "Roll Calibration";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    public override string Description =>
        "1. Tilt your vehicle slightly to the RIGHT\n" +
        "2. The gauge should move to the RIGHT (positive)\n" +
        "3. If it moves LEFT (negative), enable 'Invert Roll'\n" +
        "4. Park on LEVEL GROUND\n" +
        "5. Press 'Zero Roll' to calibrate the zero position";

    public override bool CanSkip => true;

    private bool _isRollInvert;
    public bool IsRollInvert
    {
        get => _isRollInvert;
        set
        {
            // Push the new value into the store immediately so the next
            // NMEA parse uses it. NmeaParserServiceFast post-processes
            // roll with `if (IsRollInvert) roll = -roll; roll -= RollZero;`
            // — without the live store-write, the gauge keeps showing
            // raw uncalibrated roll until the operator advances past
            // the step and OnLeaving fires.
            if (SetProperty(ref _isRollInvert, value))
                _configService.Store.Ahrs.IsRollInvert = value;
        }
    }

    private double _rollZero;
    public double RollZero
    {
        get => _rollZero;
        set
        {
            // Same live-feedback push as IsRollInvert. The Zero Roll
            // button captures LiveRoll into this setter; the next NMEA
            // parse subtracts it from raw roll so the gauge centres.
            if (SetProperty(ref _rollZero, value))
                _configService.Store.Ahrs.RollZero = value;
        }
    }

    private double _liveRoll;
    /// <summary>Live roll angle from IMU.</summary>
    public double LiveRoll
    {
        get => _liveRoll;
        set => SetProperty(ref _liveRoll, value);
    }

    private bool _hasLiveData;
    public bool HasLiveData
    {
        get => _hasLiveData;
        set => SetProperty(ref _hasLiveData, value);
    }

    public ICommand ZeroRollCommand { get; }

    public RollCalibrationStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;

        ZeroRollCommand = new RelayCommand(() =>
        {
            // LiveRoll is the *post-calibration* angle: the NMEA parser
            // already applies IsRollInvert and subtracts the current
            // RollZero before publishing. Replacing the offset would
            // throw away the prior calibration on a second press —
            // matches the WAS Zero accumulator bug (#385 F/G). The
            // invert sign is already baked in upstream, so the Zero
            // formula here is plain addition.
            RollZero = _configService.Store.Ahrs.RollZero + LiveRoll;
        });
    }

    protected override void OnEntering()
    {
        var ahrs = _configService.Store.Ahrs;
        IsRollInvert = ahrs.IsRollInvert;
        RollZero = ahrs.RollZero;

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnStateUpdated;
    }

    protected override void OnLeaving()
    {
        if (_autoSteerService != null)
            _autoSteerService.StateUpdated -= OnStateUpdated;

        var ahrs = _configService.Store.Ahrs;
        ahrs.IsRollInvert = IsRollInvert;
        ahrs.RollZero = RollZero;
    }

    private void OnStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        LiveRoll = Math.Round(snapshot.Roll, 2);
        HasLiveData = true;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
