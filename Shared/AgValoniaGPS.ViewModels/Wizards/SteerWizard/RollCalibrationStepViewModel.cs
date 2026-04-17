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

    public override string Title => "Roll Calibration";

    public override string Description =>
        "Calibrate your IMU roll sensor. Park the vehicle on level ground, then press " +
        "'Zero Roll' to capture the current reading as the zero offset. " +
        "Enable 'Invert Roll' if the roll reads backwards.";

    public override bool CanSkip => true;

    private bool _isRollInvert;
    public bool IsRollInvert
    {
        get => _isRollInvert;
        set => SetProperty(ref _isRollInvert, value);
    }

    private double _rollZero;
    public double RollZero
    {
        get => _rollZero;
        set => SetProperty(ref _rollZero, value);
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
            // Capture current roll as zero offset
            RollZero = LiveRoll;
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
