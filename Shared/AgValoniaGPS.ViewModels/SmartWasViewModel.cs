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
using System.Globalization;
using System.Windows.Input;

using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// View-model for the Smart WAS calibration dialog. Subscribes to
/// the calibration service's SnapshotChanged event (which fires on
/// the UDP receive thread) and marshals each update onto the UI
/// thread before mutating INPC properties.
/// </summary>
public partial class SmartWasViewModel : ObservableObject
{
    private const int MIN_SAMPLES_FOR_VALID = 200;

    private readonly ISmartWasCalibrationService _smartWas;
    private readonly IConfigurationService _configService;
    private readonly IUdpCommunicationService _udpService;
    private readonly IAutoSteerService _autoSteerService;
    private readonly IUiDispatcher _dispatcher;

    public SmartWasViewModel(
        ISmartWasCalibrationService smartWas,
        IConfigurationService configService,
        IUdpCommunicationService udpService,
        IAutoSteerService autoSteerService,
        IUiDispatcher dispatcher)
    {
        _smartWas = smartWas;
        _configService = configService;
        _udpService = udpService;
        _autoSteerService = autoSteerService;
        _dispatcher = dispatcher;

        StartCommand = new RelayCommand(() => _smartWas.Start());
        StopCommand = new RelayCommand(() => _smartWas.Stop());
        ResetCommand = new RelayCommand(() => _smartWas.Reset());
        ApplyCommand = new RelayCommand(Apply, () => _hasValidCalibration);

        _smartWas.SnapshotChanged += OnSnapshotChanged;

        // Pull initial state in case samples have already accumulated.
        OnSnapshotChanged(this, _smartWas.GetSnapshot());
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ApplyCommand { get; }

    // ────────────────────────────────────────────────────────────────────
    // Display properties (set on the UI thread from OnSnapshotChanged)
    // ────────────────────────────────────────────────────────────────────

    private bool _isCollecting;
    public bool IsCollecting
    {
        get => _isCollecting;
        private set => SetProperty(ref _isCollecting, value);
    }

    private int _sampleCount;
    public int SampleCount
    {
        get => _sampleCount;
        private set
        {
            if (SetProperty(ref _sampleCount, value))
            {
                OnPropertyChanged(nameof(SampleCountText));
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    public string SampleCountText =>
        string.Format(CultureInfo.InvariantCulture, "{0} / {1} min", _sampleCount, MIN_SAMPLES_FOR_VALID);

    private double _mean;
    public double Mean
    {
        get => _mean;
        private set => SetProperty(ref _mean, value);
    }

    private double _median;
    public double Median
    {
        get => _median;
        private set => SetProperty(ref _median, value);
    }

    private double _stdDev;
    public double StdDev
    {
        get => _stdDev;
        private set => SetProperty(ref _stdDev, value);
    }

    private double _recommendedOffsetDegrees;
    public double RecommendedOffsetDegrees
    {
        get => _recommendedOffsetDegrees;
        private set
        {
            if (SetProperty(ref _recommendedOffsetDegrees, value))
                OnPropertyChanged(nameof(RecommendedOffsetCounts));
        }
    }

    public int RecommendedOffsetCounts
    {
        get
        {
            var cpd = _configService.Store.AutoSteer.CountsPerDegree;
            return cpd > 0
                ? (int)Math.Round(_recommendedOffsetDegrees * cpd)
                : 0;
        }
    }

    private double _confidence;
    public double Confidence
    {
        get => _confidence;
        private set => SetProperty(ref _confidence, value);
    }

    private bool _hasValidCalibration;
    public bool HasValidCalibration
    {
        get => _hasValidCalibration;
        private set
        {
            if (SetProperty(ref _hasValidCalibration, value))
                ((RelayCommand)ApplyCommand).NotifyCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get
        {
            if (!_isCollecting)
                return _sampleCount > 0
                    ? string.Format(CultureInfo.InvariantCulture, "Stopped — {0} samples", _sampleCount)
                    : "Stopped";

            if (!_autoSteerService.IsEngaged)
                return "Waiting for autosteer engage...";

            if (_sampleCount < MIN_SAMPLES_FOR_VALID)
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Collecting — need {0} samples, have {1}",
                    MIN_SAMPLES_FOR_VALID, _sampleCount);

            return "Collecting";
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Snapshot subscription — marshalled to UI thread (PR #320 hazard)
    // ────────────────────────────────────────────────────────────────────

    private void OnSnapshotChanged(object? sender, SmartWasSnapshot snap)
    {
        if (_dispatcher.CheckAccess())
            ApplySnapshot(snap);
        else
            _dispatcher.Post(() => ApplySnapshot(snap));
    }

    private void ApplySnapshot(SmartWasSnapshot snap)
    {
        IsCollecting = snap.IsCollecting;
        SampleCount = snap.SampleCount;
        Mean = snap.Mean;
        Median = snap.Median;
        StdDev = snap.StdDev;
        RecommendedOffsetDegrees = snap.RecommendedOffset;
        Confidence = snap.Confidence;
        HasValidCalibration = snap.HasValidCalibration;
        OnPropertyChanged(nameof(StatusMessage));
    }

    // ────────────────────────────────────────────────────────────────────
    // Apply: push offset into config + module + persist + shift buffer
    // ────────────────────────────────────────────────────────────────────

    private void Apply()
    {
        var snap = _smartWas.GetSnapshot();
        if (!snap.HasValidCalibration) return;

        var autoSteer = _configService.Store.AutoSteer;
        var cpd = autoSteer.CountsPerDegree;
        if (cpd <= 0) return;

        int offsetCounts = (int)Math.Round(snap.RecommendedOffset * cpd);
        autoSteer.WasOffset += offsetCounts;
        _configService.Store.MarkChanged();

        // Shift the buffer so the next analysis recommends ~0°
        _smartWas.ApplyOffsetCorrection(snap.RecommendedOffset);

        // Push PGN 252 with the new WasOffset
        var pgn = AgValoniaGPS.Services.AutoSteer.PgnBuilder.BuildSteerSettingsPgn(autoSteer);
        _udpService.SendToModules(pgn);

        // Persist the active vehicle/tool pair so the WAS calibration
        // change lands in the right files when names diverge.
        var store = _configService.Store;
        if (!string.IsNullOrEmpty(store.ActiveVehicleProfileName))
            _configService.SaveProfiles(store.ActiveVehicleProfileName, store.ActiveToolProfileName);
    }
}
