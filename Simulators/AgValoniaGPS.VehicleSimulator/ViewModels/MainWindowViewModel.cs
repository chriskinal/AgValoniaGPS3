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

    // Sensors
    private double _wasAngle; // degrees
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

    public double WasAngle
    {
        get => _wasAngle;
        set { _wasAngle = Math.Clamp(value, -45, 45); OnPropertyChanged(); }
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

            _hub = new VirtualModuleHub(hostReceivePort: 9999, moduleListenPort: 8888);
            _hub.Gps.Latitude = Latitude;
            _hub.Gps.Longitude = Longitude;
            _hub.Gps.HeadingDegrees = Heading;
            _hub.Gps.SpeedKnots = Speed / 1.852;
            _hub.Gps.FixQuality = FixQuality;
            _hub.Gps.Satellites = SatelliteCount;
            _hub.Gps.RollDegrees = RollAngle;
            _hub.Steer.ActualSteerAngleDeg = WasAngle;
            _hub.Steer.SimulateSteerResponse = false; // We control WAS directly

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
        // engaged, the simulated WAS follows the commanded angle with lag.
        bool engaged = _hub.Steer.LastCommand?.IsEngaged ?? false;
        AutoSteerEngaged = engaged;
        if (engaged)
        {
            double commanded = _hub.Steer.CommandedSteerAngleDeg;
            double responseRate = 0.3;
            WasAngle += (commanded - WasAngle) * responseRate;
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
        _hub.Steer.ActualSteerAngleDeg = WasAngle;
        _hub.Steer.SteerSwitchActive = SteerSwitchOn;

        // Send GPS data
        _hub.Gps.SendOnce();
        _packetCount++;

        // Read back commanded angle from steer module
        CommandedAngle = _hub.Steer.CommandedSteerAngleDeg;
        PwmDisplay = _hub.Steer.PwmDisplay;

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
