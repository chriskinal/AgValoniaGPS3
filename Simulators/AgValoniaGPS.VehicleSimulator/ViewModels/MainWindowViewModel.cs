// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using AgValoniaGPS.VehicleSimulator.Models;
using AgValoniaGPS.VehicleSimulator.Modules;

namespace AgValoniaGPS.VehicleSimulator.ViewModels;

public enum DriveMode
{
    Bicycle,
    Raw
}

public class MainWindowViewModel : INotifyPropertyChanged
{
    private VirtualModuleHub? _hub;
    private DispatcherTimer? _simTimer;
    private long _packetCount;
    private readonly VehiclePhysicsModel _vehicle = new();

    // Vehicle
    private double _speed; // km/h
    private double _heading; // degrees
    private double _latitude = 42.0308;
    private double _longitude = -93.6319;
    // Bicycle-model axle length. Operator-adjustable so the host's wizard
    // belief about wheelbase can be tested against a configurable truth, the
    // same pattern as the virtual CPD / WAS offset knobs.
    private double _wheelbase = 2.5; // meters; matches VehiclePhysicsModel default

    // Sensors
    private double _wasAngle; // degrees
    private double _maxPhysicalWheelAngle = 35.0; // degrees, ± clamp
    private double _rollAngle; // degrees
    private int _fixQuality = 4; // RTK Fixed
    private int _satelliteCount = 12;

    // Status
    private bool _isRunning;
    private double _commandedAngle;
    private byte _pwmDisplay;
    private bool _steerSwitchOn;
    private bool _autoSteerEngaged;
    private DriveMode _driveMode = DriveMode.Bicycle;
    private string _statusText = "Stopped";

    // Applied module settings (mirrored from VirtualSteerModule for UI display)
    private byte _appliedKp;
    private byte _appliedCountsPerDegree;
    private short _appliedWasOffset;
    private byte _appliedHighPwm;
    private bool _appliedInvertWas;
    private bool _appliedIsDanfoss;
    private bool _appliedSteerSwitchEnabled;
    private bool _appliedWorkSwitchEnabled;
    private double _reportedSteerAngle;

    // Virtual WAS hardware truth — operator-adjustable knobs that simulate the
    // real CPD and off-center of the pretend WAS sensor. Persisted on the
    // ViewModel so values survive Stop/Start cycles of the module.
    private double _virtualCountsPerDegree = 100.0;
    private short _virtualWasOffset;
    private double _truthRawCounts;

    public double Speed
    {
        get => _speed;
        set { _speed = Math.Clamp(value, 0, 30); OnPropertyChanged(); }
    }

    public double Heading
    {
        get => _heading;
        set { _heading = ((value % 360) + 360) % 360; OnPropertyChanged(); }
    }

    public double Latitude
    {
        get => _latitude;
        set { _latitude = value; OnPropertyChanged(); }
    }

    public double Longitude
    {
        get => _longitude;
        set { _longitude = value; OnPropertyChanged(); }
    }

    public double Wheelbase
    {
        get => _wheelbase;
        set
        {
            _wheelbase = Math.Clamp(value, 1.0, 5.0);
            OnPropertyChanged();
            // Push live so changes affect the next kinematic step without
            // requiring a Stop/Start cycle.
            _vehicle.Wheelbase = _wheelbase;
        }
    }

    public double WasAngle
    {
        get => _wasAngle;
        set { _wasAngle = Math.Clamp(value, -45, 45); OnPropertyChanged(); }
    }

    /// <summary>
    /// Maximum physical wheel angle (±degrees) the simulated tractor
    /// can reach. The internal steer module clamps its integrated WAS
    /// at this value so the wizard's max-steering-angle test sees a
    /// real plateau rather than a slowly-converging PID output. Default
    /// 35° approximates a typical agricultural front axle; operator can
    /// dial it up or down to test the wizard against different limits.
    /// </summary>
    public double MaxPhysicalWheelAngle
    {
        get => _maxPhysicalWheelAngle;
        set
        {
            // Allow 5..60° to cover everything from articulated tractors
            // (very tight) to forklift-style steering (very wide).
            double clamped = Math.Clamp(value, 5.0, 60.0);
            if (_maxPhysicalWheelAngle != clamped)
            {
                _maxPhysicalWheelAngle = clamped;
                _hub.Steer.MaxPhysicalWheelAngleDeg = clamped;
                OnPropertyChanged();
            }
        }
    }

    public double RollAngle
    {
        get => _rollAngle;
        set { _rollAngle = Math.Clamp(value, -15, 15); OnPropertyChanged(); }
    }

    public int FixQuality
    {
        get => _fixQuality;
        set { _fixQuality = Math.Clamp(value, 0, 5); OnPropertyChanged(); }
    }

    public int SatelliteCount
    {
        get => _satelliteCount;
        set { _satelliteCount = Math.Clamp(value, 0, 30); OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartStopLabel)); }
    }

    public double CommandedAngle
    {
        get => _commandedAngle;
        set { _commandedAngle = value; OnPropertyChanged(); }
    }

    public byte PwmDisplay
    {
        get => _pwmDisplay;
        set { _pwmDisplay = value; OnPropertyChanged(); }
    }

    public bool SteerSwitchOn
    {
        get => _steerSwitchOn;
        set { _steerSwitchOn = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Read-only indicator: autosteer engaged signal received from the host
    /// (PGN 254 IsEngaged bit).
    /// </summary>
    public bool AutoSteerEngaged
    {
        get => _autoSteerEngaged;
        private set { _autoSteerEngaged = value; OnPropertyChanged(); }
    }

    public DriveMode DriveMode
    {
        get => _driveMode;
        set { _driveMode = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    // === Applied-settings panel (read-only mirrors of VirtualSteerModule state) ===
    // These reflect the most recent values applied from PGN 251 / 252 plus the
    // live PWM and post-calibration angle, so the operator can sanity-check
    // that the host's wizard config landed on the module.

    public byte AppliedKp
    {
        get => _appliedKp;
        private set { _appliedKp = value; OnPropertyChanged(); }
    }

    public byte AppliedCountsPerDegree
    {
        get => _appliedCountsPerDegree;
        private set { _appliedCountsPerDegree = value; OnPropertyChanged(); }
    }

    public short AppliedWasOffset
    {
        get => _appliedWasOffset;
        private set { _appliedWasOffset = value; OnPropertyChanged(); }
    }

    public byte AppliedHighPwm
    {
        get => _appliedHighPwm;
        private set { _appliedHighPwm = value; OnPropertyChanged(); }
    }

    public bool AppliedInvertWas
    {
        get => _appliedInvertWas;
        private set { _appliedInvertWas = value; OnPropertyChanged(); }
    }

    public bool AppliedIsDanfoss
    {
        get => _appliedIsDanfoss;
        private set { _appliedIsDanfoss = value; OnPropertyChanged(); }
    }

    public bool AppliedSteerSwitchEnabled
    {
        get => _appliedSteerSwitchEnabled;
        private set
        {
            _appliedSteerSwitchEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SteerSwitchToggleEnabled));
        }
    }

    public bool AppliedWorkSwitchEnabled
    {
        get => _appliedWorkSwitchEnabled;
        private set { _appliedWorkSwitchEnabled = value; OnPropertyChanged(); }
    }

    public double ReportedSteerAngle
    {
        get => _reportedSteerAngle;
        private set { _reportedSteerAngle = value; OnPropertyChanged(); }
    }

    // === Virtual WAS hardware truth (operator-adjustable knobs) ===

    public double VirtualCountsPerDegree
    {
        get => _virtualCountsPerDegree;
        set
        {
            _virtualCountsPerDegree = Math.Clamp(value, 1.0, 255.0);
            OnPropertyChanged();
            if (_hub != null) _hub.Steer.VirtualCountsPerDegree = _virtualCountsPerDegree;
        }
    }

    public short VirtualWasOffset
    {
        get => _virtualWasOffset;
        set
        {
            _virtualWasOffset = (short)Math.Clamp((int)value, -2000, 2000);
            OnPropertyChanged();
            if (_hub != null) _hub.Steer.VirtualWasOffset = _virtualWasOffset;
        }
    }

    public double TruthRawCounts
    {
        get => _truthRawCounts;
        private set { _truthRawCounts = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether the UI's Steer-Switch toggle should accept input. When the host
    /// has not configured a physical steer switch, the module reports the bit
    /// as continuously active regardless of the toggle, so the toggle is
    /// disabled in the UI to make that explicit.
    /// </summary>
    public bool SteerSwitchToggleEnabled => _appliedSteerSwitchEnabled;

    public string StartStopLabel => IsRunning ? "Stop" : "Start";

    public ICommand StartStopCommand { get; }

    public MainWindowViewModel()
    {
        StartStopCommand = new RelayCommand(ToggleRunning);
    }

    private void ToggleRunning()
    {
        if (IsRunning)
            StopSimulation();
        else
            StartSimulation();
    }

    private void StartSimulation()
    {
        try
        {
            // Initialize physics model with current UI values
            _vehicle.Latitude = Latitude;
            _vehicle.Longitude = Longitude;
            _vehicle.HeadingDeg = Heading;
            _vehicle.SpeedKmh = Speed;
            _vehicle.SteerAngleDeg = WasAngle;
            _vehicle.Wheelbase = Wheelbase;

            _hub = new VirtualModuleHub(hostReceivePort: 9999, moduleListenPort: 8888);
            _hub.Gps.Latitude = Latitude;
            _hub.Gps.Longitude = Longitude;
            _hub.Gps.HeadingDegrees = Heading;
            _hub.Gps.SpeedKnots = Speed / 1.852;
            _hub.Gps.FixQuality = FixQuality;
            _hub.Gps.Satellites = SatelliteCount;
            _hub.Gps.RollDegrees = RollAngle;
            _hub.Steer.ActualSteerAngleDeg = WasAngle;
            // Push the operator's mechanical-limit setting before Start
            // so the synthetic PWM loop clamps the simulated wheel angle
            // at the configured maximum from the first tick.
            _hub.Steer.MaxPhysicalWheelAngleDeg = MaxPhysicalWheelAngle;
            // The module's synthetic PWM tick loop drives WAS when autosteer
            // is engaged; the legacy on-receive WAS-follow stays off.
            _hub.Steer.SimulateSteerResponse = false;
            // Seed the module's virtual hardware truth from the operator-set VM
            // values, so PGN 253 picks up the correct calibration on the first
            // emit after Start (rather than the module defaults).
            _hub.Steer.VirtualCountsPerDegree = VirtualCountsPerDegree;
            _hub.Steer.VirtualWasOffset = VirtualWasOffset;

            _hub.Start();

            _packetCount = 0;

            _simTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 10 Hz
            };
            _simTimer.Tick += SimulationTick;
            _simTimer.Start();

            IsRunning = true;
            StatusText = "Running";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void StopSimulation()
    {
        _simTimer?.Stop();
        _simTimer = null;

        _hub?.Dispose();
        _hub = null;

        IsRunning = false;
        StatusText = "Stopped";
    }

    private void SimulationTick(object? sender, EventArgs e)
    {
        if (_hub == null) return;

        // Mirror host's autosteer engagement to the read-only indicator. When
        // engaged, the module's synthetic PWM loop owns the WAS angle; reflect
        // it back into the UI slider so the operator sees the simulated motor
        // turning the wheel. When not engaged, the slider is the source of
        // truth and we push it down to the module further below.
        bool engaged = _hub.Steer.LastCommand?.IsEngaged ?? false;
        AutoSteerEngaged = engaged;
        if (engaged)
        {
            WasAngle = _hub.Steer.ActualSteerAngleDeg;
        }

        const double dt = 0.1; // 10 Hz tick
        if (DriveMode == DriveMode.Bicycle)
        {
            _vehicle.SpeedKmh = Speed;
            _vehicle.SteerAngleDeg = WasAngle;
            _vehicle.Step(dt);

            Heading = _vehicle.HeadingDeg;
            Latitude = _vehicle.Latitude;
            Longitude = _vehicle.Longitude;
        }
        else
        {
            // Raw mode: slider Heading is source of truth; just integrate position.
            double speedMs = Speed / 3.6;
            double headingRad = Heading * Math.PI / 180.0;
            double dNorth = speedMs * Math.Cos(headingRad) * dt;
            double dEast = speedMs * Math.Sin(headingRad) * dt;
            Latitude += dNorth / 111320.0;
            Longitude += dEast / (111320.0 * Math.Cos(Latitude * Math.PI / 180.0));

            // Keep the bicycle model's pose in sync so a switch back to Bicycle
            // mode resumes from the user's current pose rather than snapping back.
            _vehicle.Latitude = Latitude;
            _vehicle.Longitude = Longitude;
            _vehicle.HeadingDeg = Heading;
        }

        // Push updated values to GPS module
        _hub.Gps.Latitude = Latitude;
        _hub.Gps.Longitude = Longitude;
        _hub.Gps.HeadingDegrees = Heading;
        _hub.Gps.SpeedKnots = Speed / 1.852; // km/h to knots
        _hub.Gps.FixQuality = FixQuality;
        _hub.Gps.Satellites = SatelliteCount;
        _hub.Gps.RollDegrees = RollAngle;
        // Only push slider → WAS when not engaged. When engaged the module's
        // PWM loop is the source of truth (we already pulled it into WasAngle
        // above), and pushing the slider value back would fight that loop.
        if (!engaged)
        {
            _hub.Steer.ActualSteerAngleDeg = WasAngle;
        }
        _hub.Steer.SteerSwitchActive = SteerSwitchOn;

        // Send GPS data
        _hub.Gps.SendOnce();
        _packetCount++;

        // Read back commanded angle from steer module
        CommandedAngle = _hub.Steer.CommandedSteerAngleDeg;
        PwmDisplay = _hub.Steer.PwmDisplay;

        // Refresh the applied-settings panel so operators can see PGN 251 / 252
        // values landing on the module in real time.
        AppliedKp = _hub.Steer.Kp;
        AppliedCountsPerDegree = _hub.Steer.CountsPerDegree;
        AppliedWasOffset = _hub.Steer.WasOffset;
        AppliedHighPwm = _hub.Steer.HighPWM;
        AppliedInvertWas = _hub.Steer.InvertWas;
        AppliedIsDanfoss = _hub.Steer.IsDanfoss;
        AppliedSteerSwitchEnabled = _hub.Steer.SteerSwitchEnabled;
        AppliedWorkSwitchEnabled = _hub.Steer.WorkSwitchEnabled;
        ReportedSteerAngle = _hub.Steer.ReportedSteerAngleDeg;
        TruthRawCounts = _hub.Steer.TruthRawCounts;

        StatusText = $"Running, {_packetCount} packets sent";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}
