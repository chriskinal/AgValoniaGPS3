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
/// Ackermann Calibration step.
/// The user drives in a steady LEFT circle while the system tracks
/// GPS positions to measure the turning diameter, then auto-calculates Ackermann.
/// Ackermann compensates for the difference between inner and outer wheel angles during turning.
/// </summary>
public class AckermannTestStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private readonly IAutoSteerService? _autoSteerService;
    private HardwareInstalledStepViewModel? _hardwareStep;

    private double _startEasting;
    private double _startNorthing;
    private double _startAngle;
    private int _stableCounter;

    public override string Title => "Ackermann Calibration";

    public override string Description =>
        "Turn steering wheel to the LEFT about 20 degrees. " +
        "While driving in a steady circle, press Record and wait. " +
        "The system will measure the turning diameter and calculate Ackermann automatically.";

    public override bool CanSkip => true;

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    private int _ackermann;
    /// <summary>Ackermann value (0-200, 100 = neutral).</summary>
    public int Ackermann
    {
        get => _ackermann;
        set => SetProperty(ref _ackermann, value);
    }

    private bool _isRecording;
    /// <summary>True while recording GPS positions for circle measurement.</summary>
    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (SetProperty(ref _isRecording, value))
                OnPropertyChanged(nameof(CanRecord));
        }
    }

    private double _diameter;
    /// <summary>Maximum measured diameter during recording (meters).</summary>
    public double Diameter
    {
        get => _diameter;
        set => SetProperty(ref _diameter, value);
    }

    private double _calculatedSteerAngle;
    /// <summary>Calculated steer angle from the measured circle diameter.</summary>
    public double CalculatedSteerAngle
    {
        get => _calculatedSteerAngle;
        set => SetProperty(ref _calculatedSteerAngle, value);
    }

    private string _testResult = "";
    /// <summary>Result summary text after recording completes.</summary>
    public string TestResult
    {
        get => _testResult;
        set => SetProperty(ref _testResult, value);
    }

    private bool _isRtkFixed;
    /// <summary>True when GPS fix is RTK quality (FixQuality >= 4).</summary>
    public bool IsRtkFixed
    {
        get => _isRtkFixed;
        set
        {
            if (SetProperty(ref _isRtkFixed, value))
                OnPropertyChanged(nameof(CanRecord));
        }
    }

    private double _speed;
    /// <summary>Current vehicle speed in km/h.</summary>
    public double Speed
    {
        get => _speed;
        set
        {
            if (SetProperty(ref _speed, value))
                OnPropertyChanged(nameof(CanRecord));
        }
    }

    private double _liveSteerAngle;
    /// <summary>Live actual steer angle from PGN 253.</summary>
    public double LiveSteerAngle
    {
        get => _liveSteerAngle;
        set => SetProperty(ref _liveSteerAngle, value);
    }

    /// <summary>The WAS angle captured at the start of recording.</summary>
    public double CapturedStartAngle => _startAngle;

    /// <summary>True when recording can start: RTK fixed, moving, and not already recording.</summary>
    public bool CanRecord => IsRtkFixed && Speed > 0.5 && !IsRecording;

    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }

    public AckermannTestStepViewModel(IConfigurationService configService,
        IAutoSteerService? autoSteerService = null)
    {
        _configService = configService;
        _autoSteerService = autoSteerService;

        StartRecordingCommand = new RelayCommand(StartRecording, () => CanRecord);
        StopRecordingCommand = new RelayCommand(StopRecording, () => IsRecording);
    }

    /// <summary>
    /// Start recording from the current GPS position, capturing the current WAS angle.
    /// </summary>
    private void StartRecording()
    {
        var snapshot = _autoSteerService?.LatestSnapshot;
        if (snapshot == null) return;

        double currentAngle = _autoSteerService!.LastSteerData.ActualSteerAngle;
        StartRecordingAt(snapshot.Value.Easting, snapshot.Value.Northing, currentAngle);
    }

    /// <summary>
    /// Start recording from specified coordinates with a given start angle. Exposed for testing.
    /// </summary>
    public void StartRecordingAt(double easting, double northing, double startAngle)
    {
        _startEasting = easting;
        _startNorthing = northing;
        _startAngle = startAngle;
        Diameter = 0;
        _stableCounter = 0;
        CalculatedSteerAngle = 0;
        TestResult = "";
        IsRecording = true;
    }

    /// <summary>
    /// Stop recording manually without calculating Ackermann.
    /// </summary>
    private void StopRecording()
    {
        IsRecording = false;
        TestResult = "Recording stopped manually.";
    }

    /// <summary>
    /// Process a GPS position update during recording.
    /// Exposed for testing.
    /// </summary>
    public void ProcessGpsUpdate(double easting, double northing)
    {
        if (!IsRecording) return;

        double dist = Math.Sqrt(
            Math.Pow(easting - _startEasting, 2) +
            Math.Pow(northing - _startNorthing, 2));

        if (dist > Diameter)
        {
            Diameter = dist;
            _stableCounter = 0;
        }
        else
        {
            _stableCounter++;
        }

        if (_stableCounter > 9)
        {
            // Diameter stabilized - calculate Ackermann
            double wheelbase = _configService.Store.Vehicle.Wheelbase;
            double trackWidth = _configService.Store.Vehicle.TrackWidth;

            int newAckermann = CalculateAckermann(wheelbase, trackWidth, Diameter, _startAngle);

            double calcAngle = Math.Atan(wheelbase / ((Diameter - trackWidth * 0.5) / 2)) * 180.0 / Math.PI;
            CalculatedSteerAngle = Math.Round(calcAngle, 1);
            Ackermann = newAckermann;

            IsRecording = false;
            TestResult = $"Diameter: {Diameter:F1}m, Calc angle: {calcAngle:F1} deg, Ackermann: {Ackermann}";
        }
    }

    /// <summary>
    /// Pure math function for Ackermann calculation from circle test results.
    /// Uses the ratio of calculated angle to the starting (initial) WAS angle.
    /// </summary>
    /// <param name="wheelbase">Vehicle wheelbase in meters</param>
    /// <param name="trackWidth">Vehicle track width in meters</param>
    /// <param name="diameter">Measured turning circle diameter in meters</param>
    /// <param name="startAngle">WAS angle captured when recording started (degrees)</param>
    /// <returns>Ackermann value (0-200, 100 = neutral)</returns>
    public static int CalculateAckermann(double wheelbase, double trackWidth,
        double diameter, double startAngle)
    {
        double leftAngle = Math.Atan(wheelbase / ((diameter - trackWidth * 0.5) / 2));
        leftAngle = leftAngle * 180.0 / Math.PI;

        int ackermann = (int)((leftAngle / Math.Abs(startAngle)) * 100);
        return Math.Clamp(ackermann, 0, 200);
    }

    protected override void OnEntering()
    {
        Ackermann = _configService.Store.AutoSteer.Ackermann;

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated += OnStateUpdated;
    }

    protected override void OnLeaving()
    {
        // Stop recording if still in progress
        if (IsRecording)
        {
            IsRecording = false;
        }

        if (_autoSteerService != null)
            _autoSteerService.StateUpdated -= OnStateUpdated;

        _configService.Store.AutoSteer.Ackermann = Ackermann;
    }

    private void OnStateUpdated(object? sender, VehicleStateSnapshot snapshot)
    {
        IsRtkFixed = snapshot.FixQuality >= 4;
        Speed = Math.Round(snapshot.SpeedKmh, 1);
        LiveSteerAngle = Math.Round(_autoSteerService!.LastSteerData.ActualSteerAngle, 1);

        if (IsRecording)
        {
            ProcessGpsUpdate(snapshot.Easting, snapshot.Northing);
        }
    }

    public override Task<bool> ValidateAsync()
    {
        if (Ackermann < 0 || Ackermann > 200)
        {
            SetValidationError("Ackermann must be between 0 and 200");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
