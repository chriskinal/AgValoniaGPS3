using System;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using Avalonia.Threading;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for boundary recording functionality.
/// Handles recording state, settings, and recording service events.
/// </summary>
public class BoundaryRecordingViewModel : ReactiveObject
{
    private readonly IBoundaryRecordingService _recordingService;
    private readonly IMapService _mapService;
    private readonly ApplicationState _appState;

    // Events for MainViewModel to subscribe to
    public event EventHandler<string>? StatusMessageChanged;
    public event EventHandler<BoundaryPolygon?>? RecordingFinished;

    public BoundaryRecordingViewModel(
        IBoundaryRecordingService recordingService,
        IMapService mapService,
        ApplicationState appState)
    {
        _recordingService = recordingService;
        _mapService = mapService;
        _appState = appState;

        // Subscribe to recording service events
        _recordingService.PointAdded += OnPointAdded;
        _recordingService.StateChanged += OnStateChanged;

        InitializeCommands();
    }

    #region Recording State Properties

    private bool _isRecording;
    /// <summary>
    /// Whether boundary recording is currently active.
    /// </summary>
    public bool IsRecording
    {
        get => _isRecording;
        private set => this.RaiseAndSetIfChanged(ref _isRecording, value);
    }

    private bool _isPaused;
    /// <summary>
    /// Whether boundary recording is paused.
    /// </summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    private int _pointCount;
    /// <summary>
    /// Number of points recorded.
    /// </summary>
    public int PointCount
    {
        get => _pointCount;
        private set => this.RaiseAndSetIfChanged(ref _pointCount, value);
    }

    private double _areaHectares;
    /// <summary>
    /// Current recorded area in hectares.
    /// </summary>
    public double AreaHectares
    {
        get => _areaHectares;
        private set => this.RaiseAndSetIfChanged(ref _areaHectares, value);
    }

    /// <summary>
    /// Current recorded area in acres.
    /// </summary>
    public double AreaAcres => _areaHectares * 2.47105;

    #endregion

    #region Recording Settings Properties

    private bool _isPlayerPanelVisible;
    /// <summary>
    /// Whether the boundary player panel is visible.
    /// </summary>
    public bool IsPlayerPanelVisible
    {
        get => _isPlayerPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isPlayerPanelVisible, value);
    }

    private bool _isSectionControlOn;
    /// <summary>
    /// When true, boundary records when section is on.
    /// </summary>
    public bool IsSectionControlOn
    {
        get => _isSectionControlOn;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isSectionControlOn, value))
            {
                StatusMessageChanged?.Invoke(this, value
                    ? "Boundary records when section is on"
                    : "Boundary section control off");
            }
        }
    }

    private bool _isDrawRightSide = true;
    /// <summary>
    /// Whether to draw boundary on right side (true) or left side (false).
    /// </summary>
    public bool IsDrawRightSide
    {
        get => _isDrawRightSide;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isDrawRightSide, value))
            {
                StatusMessageChanged?.Invoke(this, value
                    ? "Boundary on right side"
                    : "Boundary on left side");
                UpdateOffsetIndicator();
            }
        }
    }

    private bool _isDrawAtPivot;
    /// <summary>
    /// When true, records at pivot point instead of tool.
    /// </summary>
    public bool IsDrawAtPivot
    {
        get => _isDrawAtPivot;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isDrawAtPivot, value))
            {
                StatusMessageChanged?.Invoke(this, value
                    ? "Recording at pivot point"
                    : "Recording at tool");
            }
        }
    }

    private double _offset;
    /// <summary>
    /// Boundary offset in centimeters.
    /// </summary>
    public double Offset
    {
        get => _offset;
        set
        {
            this.RaiseAndSetIfChanged(ref _offset, value);
            if (true)
            {
                UpdateOffsetIndicator();
            }
        }
    }

    private bool _isOffsetRight = true;
    /// <summary>
    /// Whether offset is to the right (true) or left (false).
    /// </summary>
    public bool IsOffsetRight
    {
        get => _isOffsetRight;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isOffsetRight, value))
            {
                StatusMessageChanged?.Invoke(this, value
                    ? "Boundary offset: RIGHT"
                    : "Boundary offset: LEFT");
            }
        }
    }

    private bool _isAntennaMode;
    /// <summary>
    /// When true, uses antenna position instead of tool position.
    /// </summary>
    public bool IsAntennaMode
    {
        get => _isAntennaMode;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isAntennaMode, value))
            {
                StatusMessageChanged?.Invoke(this, value
                    ? "Using ANTENNA position"
                    : "Using TOOL position");
            }
        }
    }

    #endregion

    #region Commands

    public ICommand? StartRecordingCommand { get; private set; }
    public ICommand? PauseRecordingCommand { get; private set; }
    public ICommand? ResumeRecordingCommand { get; private set; }
    public ICommand? ToggleRecordingCommand { get; private set; }
    public ICommand? ClearPointsCommand { get; private set; }
    public ICommand? ToggleLeftRightCommand { get; private set; }
    public ICommand? ToggleAntennaToolCommand { get; private set; }

    private void InitializeCommands()
    {
        StartRecordingCommand = new RelayCommand(() =>
        {
            _recordingService.StartRecording(BoundaryType.Outer);
            StatusMessageChanged?.Invoke(this, "Boundary recording started");
        });

        PauseRecordingCommand = new RelayCommand(() =>
        {
            if (_recordingService.IsRecording)
            {
                _recordingService.PauseRecording();
                StatusMessageChanged?.Invoke(this, "Boundary recording paused");
            }
        });

        ResumeRecordingCommand = new RelayCommand(() =>
        {
            if (_recordingService.State == BoundaryRecordingState.Paused)
            {
                _recordingService.ResumeRecording();
                StatusMessageChanged?.Invoke(this, "Boundary recording resumed");
            }
        });

        ToggleRecordingCommand = new RelayCommand(() =>
        {
            if (_recordingService.State == BoundaryRecordingState.Paused)
            {
                _recordingService.ResumeRecording();
                StatusMessageChanged?.Invoke(this, "Recording resumed");
            }
            else if (_recordingService.IsRecording)
            {
                _recordingService.PauseRecording();
                StatusMessageChanged?.Invoke(this, "Recording paused");
            }
        });

        ClearPointsCommand = new RelayCommand(() =>
        {
            _recordingService.ClearPoints();
            StatusMessageChanged?.Invoke(this, "Boundary points cleared");
        });

        ToggleLeftRightCommand = new RelayCommand(() =>
        {
            IsOffsetRight = !IsOffsetRight;
        });

        ToggleAntennaToolCommand = new RelayCommand(() =>
        {
            IsAntennaMode = !IsAntennaMode;
        });
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Start a new boundary recording session (for drive-around mode).
    /// </summary>
    public void StartDriveAroundSession()
    {
        _recordingService.StartRecording(BoundaryType.Outer);
        _recordingService.PauseRecording();
        IsPlayerPanelVisible = true;
        StatusMessageChanged?.Invoke(this, "Drive around the field boundary. Click Record to start.");
    }

    /// <summary>
    /// Add a point at the specified position.
    /// </summary>
    public void AddPoint(double easting, double northing, double headingRadians)
    {
        if (_recordingService.IsRecording)
        {
            var (offsetE, offsetN) = CalculateOffsetPosition(easting, northing, headingRadians);
            _recordingService.AddPoint(offsetE, offsetN, headingRadians);
            StatusMessageChanged?.Invoke(this, $"Point added ({_recordingService.PointCount} total)");
        }
    }

    /// <summary>
    /// Finish recording and return the boundary.
    /// </summary>
    /// <returns>The completed boundary, or null if recording failed.</returns>
    public BoundaryPolygon? FinishRecording()
    {
        if (!_recordingService.IsRecording)
        {
            StatusMessageChanged?.Invoke(this, "No recording in progress");
            return null;
        }

        var boundary = _recordingService.StopRecording();
        IsPlayerPanelVisible = false;

        if (boundary != null)
        {
            RecordingFinished?.Invoke(this, boundary);
        }
        else
        {
            StatusMessageChanged?.Invoke(this, "Recording finished but no boundary created");
        }

        return boundary;
    }

    /// <summary>
    /// Check if recording is active (either recording or paused).
    /// </summary>
    public bool IsActive => _recordingService.IsRecording || _recordingService.State == BoundaryRecordingState.Paused;

    #endregion

    #region Private Methods

    private void UpdateOffsetIndicator()
    {
        double signedOffsetMeters = _offset / 100.0;
        if (!_isDrawRightSide)
        {
            signedOffsetMeters = -signedOffsetMeters;
        }
        _mapService.SetBoundaryOffsetIndicator(true, signedOffsetMeters);
    }

    /// <summary>
    /// Calculate offset position perpendicular to heading.
    /// </summary>
    public (double easting, double northing) CalculateOffsetPosition(double easting, double northing, double headingRadians)
    {
        if (_offset == 0)
            return (easting, northing);

        // Convert centimeters to meters
        double offsetMeters = _offset / 100.0;

        // Apply direction: right side = positive offset, left side = negative offset
        if (!_isDrawRightSide)
        {
            offsetMeters = -offsetMeters;
        }

        // Calculate perpendicular offset (90 degrees from heading)
        // Heading is in radians, 0 = North, positive = clockwise
        // Right of heading = heading + 90 degrees = heading + PI/2
        double perpAngle = headingRadians + Math.PI / 2;
        double offsetEasting = easting + Math.Sin(perpAngle) * offsetMeters;
        double offsetNorthing = northing + Math.Cos(perpAngle) * offsetMeters;

        return (offsetEasting, offsetNorthing);
    }

    #endregion

    #region Event Handlers

    private void OnPointAdded(object? sender, BoundaryPointAddedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update state
            PointCount = e.TotalPoints;
            AreaHectares = e.AreaHectares;

            // Update centralized state
            _appState.BoundaryRec.PointCount = e.TotalPoints;
            _appState.BoundaryRec.AreaHectares = e.AreaHectares;
            _appState.BoundaryRec.AreaAcres = e.AreaHectares * 2.47105;

            // Update map
            var points = _recordingService.RecordedPoints
                .Select(p => (p.Easting, p.Northing))
                .ToList();
            _mapService.SetRecordingPoints(points);
        });
    }

    private void OnStateChanged(object? sender, BoundaryRecordingStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update state
            IsRecording = e.State == BoundaryRecordingState.Recording;
            IsPaused = e.State == BoundaryRecordingState.Paused;
            PointCount = e.PointCount;
            AreaHectares = e.AreaHectares;

            // Update centralized state
            _appState.BoundaryRec.IsRecording = e.State == BoundaryRecordingState.Recording;
            _appState.BoundaryRec.IsPaused = e.State == BoundaryRecordingState.Paused;
            _appState.BoundaryRec.PointCount = e.PointCount;
            _appState.BoundaryRec.AreaHectares = e.AreaHectares;
            _appState.BoundaryRec.AreaAcres = e.AreaHectares * 2.47105;

            // Clear recording points from map when recording becomes idle
            if (e.State == BoundaryRecordingState.Idle)
            {
                _appState.BoundaryRec.RecordingPoints.Clear();
                _mapService.ClearRecordingPoints();
            }
            else if (e.PointCount >= 0)
            {
                var points = _recordingService.RecordedPoints
                    .Select(p => (p.Easting, p.Northing))
                    .ToList();
                _mapService.SetRecordingPoints(points);
            }
        });
    }

    #endregion
}
