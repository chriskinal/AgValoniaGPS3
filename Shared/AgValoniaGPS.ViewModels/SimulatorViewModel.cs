using System;
using System.Windows.Input;
using ReactiveUI;
using Avalonia.Threading;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for GPS simulator functionality.
/// Handles simulator state, controls, and coordinate management.
/// </summary>
public class SimulatorViewModel : ReactiveObject
{
    private readonly IGpsSimulationService _simulatorService;
    private readonly ISettingsService _settingsService;
    private readonly ApplicationState _appState;
    private readonly DispatcherTimer _simulatorTimer;
    private LocalPlane? _localPlane;

    // Events for MainViewModel to subscribe to
    public event EventHandler<GpsData>? SimulatedGpsDataReceived;

    public SimulatorViewModel(
        IGpsSimulationService simulatorService,
        ISettingsService settingsService,
        ApplicationState appState)
    {
        _simulatorService = simulatorService;
        _settingsService = settingsService;
        _appState = appState;

        // Subscribe to simulator GPS updates
        _simulatorService.GpsDataUpdated += OnSimulatorGpsDataUpdated;

        // Initialize simulator with default or saved position
        var settings = _settingsService.Settings;
        var lat = settings.SimulatorLatitude != 0 ? settings.SimulatorLatitude : 40.7128;
        var lon = settings.SimulatorLongitude != 0 ? settings.SimulatorLongitude : -74.0060;
        _simulatorService.Initialize(new Wgs84(lat, lon));
        _simulatorService.StepDistance = 0;

        // Create simulator timer (100ms tick rate)
        _simulatorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _simulatorTimer.Tick += OnSimulatorTick;

        InitializeCommands();
    }

    #region Properties

    private bool _isPanelVisible;
    public bool IsPanelVisible
    {
        get => _isPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isPanelVisible, value);
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isEnabled, value))
            {
                _appState.Simulator.IsEnabled = value;
                _settingsService.Settings.SimulatorEnabled = value;
                _settingsService.Save();

                if (value)
                {
                    var settings = _settingsService.Settings;
                    _simulatorService.Initialize(new Wgs84(
                        settings.SimulatorLatitude,
                        settings.SimulatorLongitude));
                    _appState.Simulator.IsRunning = true;
                    _simulatorTimer.Start();
                }
                else
                {
                    _appState.Simulator.IsRunning = false;
                    _simulatorTimer.Stop();
                }
            }
        }
    }

    private double _steerAngle;
    /// <summary>
    /// Current steering angle in degrees. Set by guidance algorithm, used by simulator.
    /// </summary>
    public double SteerAngle
    {
        get => _steerAngle;
        set
        {
            this.RaiseAndSetIfChanged(ref _steerAngle, value);
            _appState.Simulator.SteerAngle = value;
            this.RaisePropertyChanged(nameof(SteerAngleDisplay));
            if (_isEnabled)
            {
                _simulatorService.SteerAngle = value;
            }
        }
    }

    public string SteerAngleDisplay => $"Steer Angle: {_steerAngle:F1}°";

    private double _speedKph;
    /// <summary>
    /// Simulator speed in kph. Range: -10 to +25 kph.
    /// </summary>
    public double SpeedKph
    {
        get => _speedKph;
        set
        {
            value = Math.Max(-10, Math.Min(25, value));
            this.RaiseAndSetIfChanged(ref _speedKph, value);
            _appState.Simulator.Speed = value;
            _appState.Simulator.TargetSpeed = value;
            this.RaisePropertyChanged(nameof(SpeedDisplay));
            if (_isEnabled)
            {
                _simulatorService.StepDistance = value / 40.0;
                _simulatorService.IsAcceleratingForward = false;
                _simulatorService.IsAcceleratingBackward = false;
            }
        }
    }

    public string SpeedDisplay => $"Speed: {_speedKph:F1} kph";

    // Coordinate dialog properties
    private decimal? _dialogLatitude;
    public decimal? DialogLatitude
    {
        get => _dialogLatitude;
        set => this.RaiseAndSetIfChanged(ref _dialogLatitude, value);
    }

    private decimal? _dialogLongitude;
    public decimal? DialogLongitude
    {
        get => _dialogLongitude;
        set => this.RaiseAndSetIfChanged(ref _dialogLongitude, value);
    }

    #endregion

    #region Commands

    public ICommand? TogglePanelCommand { get; private set; }
    public ICommand? ResetCommand { get; private set; }
    public ICommand? ResetSteerAngleCommand { get; private set; }
    public ICommand? ForwardCommand { get; private set; }
    public ICommand? StopCommand { get; private set; }
    public ICommand? ReverseCommand { get; private set; }
    public ICommand? ReverseDirectionCommand { get; private set; }
    public ICommand? SteerLeftCommand { get; private set; }
    public ICommand? SteerRightCommand { get; private set; }
    public ICommand? ShowCoordsDialogCommand { get; private set; }
    public ICommand? CancelCoordsDialogCommand { get; private set; }
    public ICommand? ConfirmCoordsDialogCommand { get; private set; }

    private void InitializeCommands()
    {
        TogglePanelCommand = new RelayCommand(() =>
        {
            IsPanelVisible = !IsPanelVisible;
        });

        ResetCommand = new RelayCommand(() =>
        {
            _simulatorService.Reset();
            SteerAngle = 0;
        });

        ResetSteerAngleCommand = new RelayCommand(() =>
        {
            SteerAngle = 0;
        });

        ForwardCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;
            _simulatorService.IsAcceleratingForward = true;
            _simulatorService.IsAcceleratingBackward = false;
        });

        StopCommand = new RelayCommand(() =>
        {
            _simulatorService.IsAcceleratingForward = false;
            _simulatorService.IsAcceleratingBackward = false;
            _simulatorService.StepDistance = 0;
            _speedKph = 0;
            this.RaisePropertyChanged(nameof(SpeedKph));
            this.RaisePropertyChanged(nameof(SpeedDisplay));
        });

        ReverseCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;
            _simulatorService.IsAcceleratingBackward = true;
            _simulatorService.IsAcceleratingForward = false;
        });

        ReverseDirectionCommand = new RelayCommand(() =>
        {
            var newHeading = _simulatorService.HeadingRadians + Math.PI;
            if (newHeading > Math.PI * 2)
                newHeading -= Math.PI * 2;
            _simulatorService.SetHeading(newHeading);
        });

        SteerLeftCommand = new RelayCommand(() =>
        {
            SteerAngle -= 5.0;
        });

        SteerRightCommand = new RelayCommand(() =>
        {
            SteerAngle += 5.0;
        });

        ShowCoordsDialogCommand = new RelayCommand(() =>
        {
            if (IsEnabled) return; // Don't allow changing coords while running
            var currentPos = GetPosition();
            DialogLatitude = Math.Round((decimal)currentPos.Latitude, 8);
            DialogLongitude = Math.Round((decimal)currentPos.Longitude, 8);
            _appState.UI.ShowDialog(DialogType.SimCoords);
        });

        CancelCoordsDialogCommand = new RelayCommand(() =>
        {
            _appState.UI.CloseDialog();
        });

        ConfirmCoordsDialogCommand = new RelayCommand(() =>
        {
            double lat = (double)(DialogLatitude ?? 0m);
            double lon = (double)(DialogLongitude ?? 0m);
            SetCoordinates(lat, lon);
            _appState.UI.CloseDialog();
        });
    }

    #endregion

    #region Methods

    /// <summary>
    /// Set new starting coordinates for the simulator
    /// </summary>
    public void SetCoordinates(double latitude, double longitude)
    {
        _simulatorService.Initialize(new Wgs84(latitude, longitude));
        _simulatorService.StepDistance = 0;
        _localPlane = null;
        SteerAngle = 0;

        _settingsService.Settings.SimulatorLatitude = latitude;
        _settingsService.Settings.SimulatorLongitude = longitude;
        _settingsService.Save();
    }

    /// <summary>
    /// Get current simulator position
    /// </summary>
    public Wgs84 GetPosition() => _simulatorService.CurrentPosition;

    /// <summary>
    /// Restore settings from saved state
    /// </summary>
    public void RestoreSettings()
    {
        var settings = _settingsService.Settings;
        if (settings.SimulatorEnabled)
        {
            _simulatorService.Initialize(new Wgs84(
                settings.SimulatorLatitude,
                settings.SimulatorLongitude));
            _simulatorService.StepDistance = settings.SimulatorSpeed;
        }
    }

    #endregion

    #region Event Handlers

    private void OnSimulatorTick(object? sender, EventArgs e)
    {
        _simulatorService.Tick(SteerAngle);
    }

    private void OnSimulatorGpsDataUpdated(object? sender, GpsSimulationEventArgs e)
    {
        var simulatedData = e.Data;

        // Create LocalPlane if not yet created
        if (_localPlane == null)
        {
            var sharedProps = new SharedFieldProperties();
            _localPlane = new LocalPlane(simulatedData.Position, sharedProps);
        }

        var localCoord = _localPlane.ConvertWgs84ToGeoCoord(simulatedData.Position);

        var position = new Position
        {
            Latitude = simulatedData.Position.Latitude,
            Longitude = simulatedData.Position.Longitude,
            Altitude = simulatedData.Altitude,
            Easting = localCoord.Easting,
            Northing = localCoord.Northing,
            Heading = simulatedData.HeadingDegrees,
            Speed = simulatedData.SpeedKmh / 3.6
        };

        var gpsData = new GpsData
        {
            CurrentPosition = position,
            FixQuality = 4, // RTK Fix (simulator always reports RTK)
            SatellitesInUse = simulatedData.SatellitesTracked,
            Hdop = simulatedData.Hdop,
            IsValid = true,
            Timestamp = DateTime.UtcNow
        };

        // Raise event for MainViewModel to process
        SimulatedGpsDataReceived?.Invoke(this, gpsData);
    }

    /// <summary>
    /// Reset LocalPlane (call when field origin changes)
    /// </summary>
    public void ResetLocalPlane()
    {
        _localPlane = null;
    }

    #endregion
}
