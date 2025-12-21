using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.YouTurn;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.GPS;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Models.State;
using Avalonia.Threading;

namespace AgValoniaGPS.ViewModels;

public class MainViewModel : ReactiveObject
{
    private readonly IUdpCommunicationService _udpService;
    private readonly AgValoniaGPS.Services.Interfaces.IGpsService _gpsService;
    private readonly IFieldService _fieldService;
    private readonly INtripClientService _ntripService;
    private readonly AgValoniaGPS.Services.Interfaces.IDisplaySettingsService _displaySettings;
    private readonly AgValoniaGPS.Services.Interfaces.IFieldStatisticsService _fieldStatistics;
    private readonly AgValoniaGPS.Services.Interfaces.IGpsSimulationService _simulatorService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IMapService _mapService;
    private readonly IBoundaryRecordingService _boundaryRecordingService;
    private readonly BoundaryFileService _boundaryFileService;
    private readonly NmeaParserService _nmeaParser;
    private readonly Services.Headland.IHeadlandBuilderService _headlandBuilderService;
    private readonly ITrackGuidanceService _trackGuidanceService;
    private readonly YouTurnCreationService _youTurnCreationService;
    private readonly Services.Geometry.IPolygonOffsetService _polygonOffsetService;
    private readonly Services.Interfaces.ITurnAreaService _turnAreaService;
    private readonly YouTurnGuidanceService _youTurnGuidanceService;
    private readonly FieldPlaneFileService _fieldPlaneFileService;
    private readonly IVehicleProfileService _vehicleProfileService;
    private readonly IConfigurationService _configurationService;
    private readonly IAutoSteerService _autoSteerService;
    private readonly ApplicationState _appState;
    private readonly DispatcherTimer _simulatorTimer;
    private AgValoniaGPS.Models.LocalPlane? _simulatorLocalPlane;

    /// <summary>
    /// Centralized application state - single source of truth for all runtime state.
    /// Use this for new code. Existing properties will gradually migrate to use State.
    /// </summary>
    public ApplicationState State => _appState;

    // Convenience accessors for ConfigurationStore (replaces _vehicleConfig usage)
    private static ConfigurationStore ConfigStore => ConfigurationStore.Instance;
    private static VehicleConfig Vehicle => ConfigurationStore.Instance.Vehicle;
    private static ToolConfig Tool => ConfigurationStore.Instance.Tool;
    private static GuidanceConfig Guidance => ConfigurationStore.Instance.Guidance;

    // Current field origin (for map centering when GPS not active)
    private double _fieldOriginLatitude;
    private double _fieldOriginLongitude;

    // Track guidance state (carried between iterations)
    private TrackGuidanceState? _trackGuidanceState;

    // YouTurn state
    private bool _isYouTurnTriggered;
    private bool _isInYouTurn; // True when executing the U-turn
    private List<Vec3>? _youTurnPath;
    private int _youTurnCounter;
    private double _distanceToHeadland;
    private bool _isHeadingSameWay;
    private bool _isTurnLeft; // Direction of the current/pending U-turn
    private bool _wasHeadingSameWayAtTurnStart; // Heading direction when turn was created (for offset calc)
    private bool _lastTurnWasLeft; // Track last turn direction to alternate
    private bool _hasCompletedFirstTurn; // Track if we've done at least one turn
    private Track? _nextTrack; // The next track to switch to after U-turn completes
    private int _howManyPathsAway; // Which parallel offset line we're on (like AgOpenGPS)
    private Vec2? _lastTurnCompletionPosition; // Position where last U-turn completed - used to prevent immediate re-triggering

    private string _statusMessage = "Starting...";
    private double _latitude;
    private double _longitude;
    private double _speed;
    private int _satelliteCount;
    private string _fixQuality = "No Fix";
    private string _networkStatus = "Disconnected";
    private double _currentFps;
    private double _gpsToPgnLatencyMs;

    // Guidance/Steering status
    private double _crossTrackError;
    private string _currentGuidanceLine = "1L";
    private bool _isAutoSteerActive;
    private int _activeSections;

    // Section states
    private bool _section1Active;
    private bool _section2Active;
    private bool _section3Active;
    private bool _section4Active;
    private bool _section5Active;
    private bool _section6Active;
    private bool _section7Active;
    // Hello status (connection health)
    private bool _isAutoSteerHelloOk;
    private bool _isMachineHelloOk;
    private bool _isImuHelloOk;
    private bool _isGpsHelloOk;

    // Data status (data flow)
    private bool _isAutoSteerDataOk;
    private bool _isMachineDataOk;
    private bool _isImuDataOk;
    private bool _isGpsDataOk;
    private string _debugLog = "";
    private double _easting;
    private double _northing;
    private double _heading;

    // Field properties
    private Field? _activeField;
    private string _fieldsRootDirectory = string.Empty;

    public MainViewModel(
        IUdpCommunicationService udpService,
        AgValoniaGPS.Services.Interfaces.IGpsService gpsService,
        IFieldService fieldService,
        INtripClientService ntripService,
        AgValoniaGPS.Services.Interfaces.IDisplaySettingsService displaySettings,
        AgValoniaGPS.Services.Interfaces.IFieldStatisticsService fieldStatistics,
        AgValoniaGPS.Services.Interfaces.IGpsSimulationService simulatorService,
        ISettingsService settingsService,
        IDialogService dialogService,
        IMapService mapService,
        IBoundaryRecordingService boundaryRecordingService,
        BoundaryFileService boundaryFileService,
        Services.Headland.IHeadlandBuilderService headlandBuilderService,
        ITrackGuidanceService trackGuidanceService,
        YouTurnCreationService youTurnCreationService,
        YouTurnGuidanceService youTurnGuidanceService,
        Services.Geometry.IPolygonOffsetService polygonOffsetService,
        Services.Interfaces.ITurnAreaService turnAreaService,
        IVehicleProfileService vehicleProfileService,
        IConfigurationService configurationService,
        IAutoSteerService autoSteerService,
        ApplicationState appState)
    {
        _udpService = udpService;
        _gpsService = gpsService;
        _fieldService = fieldService;
        _ntripService = ntripService;
        _displaySettings = displaySettings;
        _fieldStatistics = fieldStatistics;
        _simulatorService = simulatorService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _mapService = mapService;
        _boundaryRecordingService = boundaryRecordingService;
        _boundaryFileService = boundaryFileService;
        _headlandBuilderService = headlandBuilderService;
        _trackGuidanceService = trackGuidanceService;
        _youTurnCreationService = youTurnCreationService;
        _youTurnGuidanceService = youTurnGuidanceService;
        _polygonOffsetService = polygonOffsetService;
        _turnAreaService = turnAreaService;
        _vehicleProfileService = vehicleProfileService;
        _configurationService = configurationService;
        _autoSteerService = autoSteerService;
        _appState = appState;
        _nmeaParser = new NmeaParserService(gpsService);
        _fieldPlaneFileService = new FieldPlaneFileService();

        // Subscribe to events
        _gpsService.GpsDataUpdated += OnGpsDataUpdated;
        _udpService.DataReceived += OnUdpDataReceived;
        _autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
        _autoSteerService.Start(); // Enable zero-copy GPS pipeline
        _udpService.ModuleConnectionChanged += OnModuleConnectionChanged;
        _ntripService.ConnectionStatusChanged += OnNtripConnectionChanged;
        _ntripService.RtcmDataReceived += OnRtcmDataReceived;
        _fieldService.ActiveFieldChanged += OnActiveFieldChanged;
        _simulatorService.GpsDataUpdated += OnSimulatorGpsDataUpdated;
        _boundaryRecordingService.PointAdded += OnBoundaryPointAdded;
        _boundaryRecordingService.StateChanged += OnBoundaryStateChanged;

        // Note: FPS subscription is set up in platform code (MainWindow.axaml.cs / MainView.axaml.cs)
        // since ViewModels cannot reference Views directly

        // Note: NOT subscribing to DisplaySettings events - using direct property access instead
        // to avoid threading issues with ReactiveUI

        // Initialize simulator service with default position (will be updated when GPS gets fix)
        _simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(40.7128, -74.0060)); // Default to NYC coordinates
        _simulatorService.StepDistance = 0; // Stationary initially

        // Create simulator timer (100ms tick rate, matching WinForms implementation)
        _simulatorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _simulatorTimer.Tick += OnSimulatorTick;

        // Initialize commands immediately (with MainThreadScheduler they're thread-safe)
        InitializeCommands();

        // Load display settings first, then restore our app settings on top
        // This ensures AppSettings takes precedence over DisplaySettings
        Dispatcher.UIThread.Post(() =>
        {
            _displaySettings.LoadSettings();
            RestoreSettings();
        }, DispatcherPriority.Background);

        // Start UDP communication
        InitializeAsync();
    }

    private void RestoreSettings()
    {
        var settings = _settingsService.Settings;

        // Restore vehicle profile settings
        LoadDefaultVehicleProfile();

        // Restore NTRIP settings
        NtripCasterAddress = settings.NtripCasterIp;
        NtripCasterPort = settings.NtripCasterPort;
        NtripMountPoint = settings.NtripMountPoint;
        NtripUsername = settings.NtripUsername;
        NtripPassword = settings.NtripPassword;

        // Restore UI state (through _displaySettings service)
        _displaySettings.IsGridOn = settings.GridVisible;

        // IMPORTANT: Notify bindings that IsGridOn changed
        // (setting _displaySettings directly doesn't trigger property change notification)
        this.RaisePropertyChanged(nameof(IsGridOn));

        // Restore simulator settings
        if (settings.SimulatorEnabled)
        {
            // Initialize simulator with saved coordinates
            _simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(
                settings.SimulatorLatitude,
                settings.SimulatorLongitude));
            _simulatorService.StepDistance = settings.SimulatorSpeed;

            // Also set Latitude/Longitude so map dialogs work correctly at startup
            Latitude = settings.SimulatorLatitude;
            Longitude = settings.SimulatorLongitude;

            Console.WriteLine($"  Restored simulator: {settings.SimulatorLatitude},{settings.SimulatorLongitude}");
        }
    }

    private void LoadDefaultVehicleProfile()
    {
        try
        {
            var profiles = _configurationService.GetAvailableProfiles();
            if (profiles.Count == 0)
            {
                Console.WriteLine("No vehicle profiles found in Vehicles directory");
                return;
            }

            // Try to load the last used profile first
            var lastUsedProfile = _settingsService.Settings.LastUsedVehicleProfile;
            string profileToLoad;

            if (!string.IsNullOrEmpty(lastUsedProfile) && profiles.Contains(lastUsedProfile))
            {
                profileToLoad = lastUsedProfile;
                Console.WriteLine($"Loading last used vehicle profile: {profileToLoad}");
            }
            else
            {
                // Fall back to first available profile
                profileToLoad = profiles[0];
                Console.WriteLine($"Loading first available vehicle profile: {profileToLoad}");
            }

            // Use ConfigurationService to load - this sets ConfigurationStore.ActiveProfileName
            if (_configurationService.LoadProfile(profileToLoad))
            {
                var store = _configurationService.Store;
                Console.WriteLine($"Loaded vehicle profile: {store.ActiveProfileName}");
                Console.WriteLine($"  Tool width: {store.Tool.Width}m");
                Console.WriteLine($"  YouTurn radius: {store.Guidance.UTurnRadius}m");
                Console.WriteLine($"  Wheelbase: {store.Vehicle.Wheelbase}m");
                Console.WriteLine($"  Sections: {store.NumSections}");

                // Save as last used profile
                _settingsService.Settings.LastUsedVehicleProfile = profileToLoad;
                _settingsService.Save();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading vehicle profile: {ex.Message}");
        }
    }

    public async System.Threading.Tasks.Task ConnectToNtripAsync()
    {
        try
        {
            var config = new NtripConfiguration
            {
                CasterAddress = NtripCasterAddress,
                CasterPort = NtripCasterPort,
                MountPoint = NtripMountPoint,
                Username = NtripUsername,
                Password = NtripPassword,
                SubnetAddress = "192.168.5",
                UdpForwardPort = 2233,
                GgaIntervalSeconds = 10,
                UseManualPosition = false
            };

            await _ntripService.ConnectAsync(config);
        }
        catch (Exception ex)
        {
            NtripStatus = $"Error: {ex.Message}";
        }
    }

    public async System.Threading.Tasks.Task DisconnectFromNtripAsync()
    {
        await _ntripService.DisconnectAsync();
    }

    private async void InitializeAsync()
    {
        try
        {
            await _udpService.StartAsync();
            NetworkStatus = $"UDP Connected: {_udpService.LocalIPAddress}";
            StatusMessage = "Ready - Waiting for modules...";

            // Start sending hello packets every second
            StartHelloTimer();
        }
        catch (Exception ex)
        {
            NetworkStatus = $"UDP Error: {ex.Message}";
            StatusMessage = "Network error";
        }
    }

    private async void StartHelloTimer()
    {
        while (_udpService.IsConnected)
        {
            // Send hello packet every second
            _udpService.SendHelloPacket();

            // Check module status using appropriate method for each:
            // - AutoSteer: Data flow (sends PGN 250/253 regularly)
            // - Machine: Hello only (receive-only, no data sent)
            // - IMU: Hello only (only sends when active)
            // - GPS: Data flow (sends NMEA regularly)

            var steerOk = _udpService.IsModuleDataOk(ModuleType.AutoSteer);
            var machineOk = _udpService.IsModuleHelloOk(ModuleType.Machine);
            var imuOk = _udpService.IsModuleHelloOk(ModuleType.IMU);
            var gpsOk = _gpsService.IsGpsDataOk();

            // Update centralized state (single source of truth)
            State.Connections.IsAutoSteerDataOk = steerOk;
            State.Connections.IsMachineDataOk = machineOk;
            State.Connections.IsImuDataOk = imuOk;
            State.Connections.IsGpsDataOk = gpsOk;

            // Legacy property updates (for existing bindings - will be removed in Phase 5)
            IsAutoSteerDataOk = steerOk;
            IsMachineDataOk = machineOk;
            IsImuDataOk = imuOk;
            IsGpsDataOk = gpsOk;

            if (!gpsOk)
            {
                StatusMessage = "GPS Timeout";
                FixQuality = "No Fix";
            }
            else
            {
                UpdateStatusMessage();
            }

            await System.Threading.Tasks.Task.Delay(100); // Check every 100ms for fast response
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public double Latitude
    {
        get => _latitude;
        set => this.RaiseAndSetIfChanged(ref _latitude, value);
    }

    public double Longitude
    {
        get => _longitude;
        set => this.RaiseAndSetIfChanged(ref _longitude, value);
    }

    public double Speed
    {
        get => _speed;
        set => this.RaiseAndSetIfChanged(ref _speed, value);
    }

    /// <summary>
    /// Current rendering frames per second
    /// </summary>
    public double CurrentFps
    {
        get => _currentFps;
        set => this.RaiseAndSetIfChanged(ref _currentFps, value);
    }

    /// <summary>
    /// GPS-to-PGN pipeline latency in milliseconds.
    /// This is the critical latency from GPS receipt to steering PGN send.
    /// </summary>
    public double GpsToPgnLatencyMs
    {
        get => _gpsToPgnLatencyMs;
        set => this.RaiseAndSetIfChanged(ref _gpsToPgnLatencyMs, value);
    }

    public int SatelliteCount
    {
        get => _satelliteCount;
        set => this.RaiseAndSetIfChanged(ref _satelliteCount, value);
    }

    public string FixQuality
    {
        get => _fixQuality;
        set => this.RaiseAndSetIfChanged(ref _fixQuality, value);
    }

    public string NetworkStatus
    {
        get => _networkStatus;
        set => this.RaiseAndSetIfChanged(ref _networkStatus, value);
    }

    // Guidance/Steering properties
    public double CrossTrackError
    {
        get => _crossTrackError;
        set => this.RaiseAndSetIfChanged(ref _crossTrackError, value);
    }

    public string CurrentGuidanceLine
    {
        get => _currentGuidanceLine;
        set => this.RaiseAndSetIfChanged(ref _currentGuidanceLine, value);
    }

    public bool IsAutoSteerActive
    {
        get => _isAutoSteerActive;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerActive, value);
    }

    public int ActiveSections
    {
        get => _activeSections;
        set => this.RaiseAndSetIfChanged(ref _activeSections, value);
    }

    // Section states
    public bool Section1Active
    {
        get => _section1Active;
        set => this.RaiseAndSetIfChanged(ref _section1Active, value);
    }

    public bool Section2Active
    {
        get => _section2Active;
        set => this.RaiseAndSetIfChanged(ref _section2Active, value);
    }

    public bool Section3Active
    {
        get => _section3Active;
        set => this.RaiseAndSetIfChanged(ref _section3Active, value);
    }

    public bool Section4Active
    {
        get => _section4Active;
        set => this.RaiseAndSetIfChanged(ref _section4Active, value);
    }

    public bool Section5Active
    {
        get => _section5Active;
        set => this.RaiseAndSetIfChanged(ref _section5Active, value);
    }

    public bool Section6Active
    {
        get => _section6Active;
        set => this.RaiseAndSetIfChanged(ref _section6Active, value);
    }

    public bool Section7Active
    {
        get => _section7Active;
        set => this.RaiseAndSetIfChanged(ref _section7Active, value);
    }

    // AutoSteer Hello and Data properties
    public bool IsAutoSteerHelloOk
    {
        get => _isAutoSteerHelloOk;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerHelloOk, value);
    }

    public bool IsAutoSteerDataOk
    {
        get => _isAutoSteerDataOk;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerDataOk, value);
    }

    // Right Navigation Panel Properties
    private bool _isContourModeOn;
    private bool _isManualSectionMode;
    private bool _isSectionMasterOn = false; // Default off
    private bool _isAutoSteerAvailable;
    private bool _isAutoSteerEngaged;
    private bool _isYouTurnEnabled; // YouTurn auto U-turn feature

    public bool IsContourModeOn
    {
        get => _isContourModeOn;
        set => this.RaiseAndSetIfChanged(ref _isContourModeOn, value);
    }

    public bool IsManualSectionMode
    {
        get => _isManualSectionMode;
        set => this.RaiseAndSetIfChanged(ref _isManualSectionMode, value);
    }

    public bool IsSectionMasterOn
    {
        get => _isSectionMasterOn;
        set => this.RaiseAndSetIfChanged(ref _isSectionMasterOn, value);
    }

    public bool IsAutoSteerAvailable
    {
        get => _isAutoSteerAvailable;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerAvailable, value);
    }

    public bool IsAutoSteerEngaged
    {
        get => _isAutoSteerEngaged;
        set => this.RaiseAndSetIfChanged(ref _isAutoSteerEngaged, value);
    }

    public bool IsYouTurnEnabled
    {
        get => _isYouTurnEnabled;
        set => this.RaiseAndSetIfChanged(ref _isYouTurnEnabled, value);
    }

    // Machine Hello and Data properties
    public bool IsMachineHelloOk
    {
        get => _isMachineHelloOk;
        set => this.RaiseAndSetIfChanged(ref _isMachineHelloOk, value);
    }

    public bool IsMachineDataOk
    {
        get => _isMachineDataOk;
        set => this.RaiseAndSetIfChanged(ref _isMachineDataOk, value);
    }

    // IMU Hello and Data properties
    public bool IsImuHelloOk
    {
        get => _isImuHelloOk;
        set => this.RaiseAndSetIfChanged(ref _isImuHelloOk, value);
    }

    public bool IsImuDataOk
    {
        get => _isImuDataOk;
        set => this.RaiseAndSetIfChanged(ref _isImuDataOk, value);
    }

    // GPS Hello and Data properties (GPS doesn't have hello, just data from NMEA)
    public bool IsGpsDataOk
    {
        get => _isGpsDataOk;
        set => this.RaiseAndSetIfChanged(ref _isGpsDataOk, value);
    }

    // NTRIP properties
    private bool _isNtripConnected;
    private string _ntripStatus = "Not Connected";
    private ulong _ntripBytesReceived;
    private string _ntripCasterAddress = "rtk2go.com";
    private int _ntripCasterPort = 2101;
    private string _ntripMountPoint = "";
    private string _ntripUsername = "";
    private string _ntripPassword = "";

    public bool IsNtripConnected
    {
        get => _isNtripConnected;
        set => this.RaiseAndSetIfChanged(ref _isNtripConnected, value);
    }

    public string NtripStatus
    {
        get => _ntripStatus;
        set => this.RaiseAndSetIfChanged(ref _ntripStatus, value);
    }

    public string NtripBytesReceived
    {
        get => $"{(_ntripBytesReceived / 1024):N0} KB";
    }

    public string NtripCasterAddress
    {
        get => _ntripCasterAddress;
        set => this.RaiseAndSetIfChanged(ref _ntripCasterAddress, value);
    }

    public int NtripCasterPort
    {
        get => _ntripCasterPort;
        set => this.RaiseAndSetIfChanged(ref _ntripCasterPort, value);
    }

    public string NtripMountPoint
    {
        get => _ntripMountPoint;
        set => this.RaiseAndSetIfChanged(ref _ntripMountPoint, value);
    }

    public string NtripUsername
    {
        get => _ntripUsername;
        set => this.RaiseAndSetIfChanged(ref _ntripUsername, value);
    }

    public string NtripPassword
    {
        get => _ntripPassword;
        set => this.RaiseAndSetIfChanged(ref _ntripPassword, value);
    }

    public string DebugLog
    {
        get => _debugLog;
        set => this.RaiseAndSetIfChanged(ref _debugLog, value);
    }

    public double Easting
    {
        get => _easting;
        set => this.RaiseAndSetIfChanged(ref _easting, value);
    }

    public double Northing
    {
        get => _northing;
        set => this.RaiseAndSetIfChanged(ref _northing, value);
    }

    public double Heading
    {
        get => _heading;
        set => this.RaiseAndSetIfChanged(ref _heading, value);
    }

    private void OnAutoSteerStateUpdated(object? sender, VehicleStateSnapshot state)
    {
        // Update latency display from AutoSteer pipeline
        // This fires at 10Hz from the GPS receive path
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            GpsToPgnLatencyMs = state.TotalLatencyMs;
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => GpsToPgnLatencyMs = state.TotalLatencyMs);
        }
    }

    private void OnGpsDataUpdated(object? sender, AgValoniaGPS.Models.GpsData data)
    {
        // Marshal to UI thread (use Invoke for synchronous execution to avoid modal dialog issues)
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread, execute directly
            UpdateGpsProperties(data);
        }
        else
        {
            // Not on UI thread, invoke synchronously
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateGpsProperties(data));
        }
    }

    private void UpdateGpsProperties(AgValoniaGPS.Models.GpsData data)
    {
        // Update centralized state (single source of truth)
        State.Vehicle.UpdateFromGps(
            data.CurrentPosition,
            data.FixQuality,
            data.SatellitesInUse,
            data.Hdop,
            data.DifferentialAge);

        // Legacy property updates (for existing bindings - will be removed in Phase 5)
        Latitude = data.CurrentPosition.Latitude;
        Longitude = data.CurrentPosition.Longitude;
        Speed = data.CurrentPosition.Speed;
        SatelliteCount = data.SatellitesInUse;
        FixQuality = GetFixQualityString(data.FixQuality);
        StatusMessage = data.IsValid ? "GPS Active" : "Waiting for GPS";

        // Update UTM coordinates and heading for map rendering
        Easting = data.CurrentPosition.Easting;
        Northing = data.CurrentPosition.Northing;
        Heading = data.CurrentPosition.Heading;

        // Add boundary point if recording is active
        if (_boundaryRecordingService.IsRecording)
        {
            double headingRadians = data.CurrentPosition.Heading * Math.PI / 180.0;
            var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(
                data.CurrentPosition.Easting,
                data.CurrentPosition.Northing,
                headingRadians);
            _boundaryRecordingService.AddPoint(offsetEasting, offsetNorthing, headingRadians);
        }
    }

    // Simulator event handlers
    private void OnSimulatorTick(object? sender, EventArgs e)
    {
        // Call simulator Tick with current steer angle
        _simulatorService.Tick(SimulatorSteerAngle);
    }

    private void OnSimulatorGpsDataUpdated(object? sender, GpsSimulationEventArgs e)
    {
        var simulatedData = e.Data;

        // Create LocalPlane if not yet created (using simulator's initial position as origin)
        if (_simulatorLocalPlane == null)
        {
            var sharedProps = new AgValoniaGPS.Models.SharedFieldProperties();
            _simulatorLocalPlane = new AgValoniaGPS.Models.LocalPlane(simulatedData.Position, sharedProps);
        }

        // Convert WGS84 to local coordinates (Northing/Easting)
        var localCoord = _simulatorLocalPlane.ConvertWgs84ToGeoCoord(simulatedData.Position);

        // Build Position object with both WGS84 and UTM coordinates
        var position = new AgValoniaGPS.Models.Position
        {
            Latitude = simulatedData.Position.Latitude,
            Longitude = simulatedData.Position.Longitude,
            Altitude = simulatedData.Altitude,
            Easting = localCoord.Easting,
            Northing = localCoord.Northing,
            Heading = simulatedData.HeadingDegrees,
            Speed = simulatedData.SpeedKmh / 3.6  // Convert km/h to m/s
        };

        // Build GpsData object
        var gpsData = new AgValoniaGPS.Models.GpsData
        {
            CurrentPosition = position,
            FixQuality = 4,  // RTK Fixed
            SatellitesInUse = simulatedData.SatellitesTracked,
            Hdop = simulatedData.Hdop,
            DifferentialAge = 0.0,
            Timestamp = DateTime.Now
        };

        // Directly update GPS service (bypasses NMEA parsing like WinForms version does)
        _gpsService.UpdateGpsData(gpsData);

        // Process through AutoSteer pipeline for latency measurement
        _autoSteerService.ProcessSimulatedPosition(
            position.Latitude, position.Longitude, position.Altitude,
            position.Heading, position.Speed, gpsData.FixQuality,
            gpsData.SatellitesInUse, gpsData.Hdop,
            position.Easting, position.Northing);

        // Calculate autosteer guidance if engaged and we have an active track
        if (IsAutoSteerEngaged && HasActiveTrack && SelectedTrack != null)
        {
            // Increment YouTurn counter (used for throttling)
            _youTurnCounter++;

            // Check for YouTurn execution or create path if approaching headland
            if (IsYouTurnEnabled && _currentHeadlandLine != null && _currentHeadlandLine.Count >= 3)
            {
                ProcessYouTurn(position);
            }

            // If we're in a YouTurn, use YouTurn guidance; otherwise use AB line guidance
            if (_isYouTurnTriggered && _youTurnPath != null && _youTurnPath.Count > 0)
            {
                CalculateYouTurnGuidance(position);
            }
            else
            {
                CalculateAutoSteerGuidance(position);
            }
        }
    }

    /// <summary>
    /// Calculate steering guidance using Pure Pursuit algorithm and apply to simulator.
    /// Uses _howManyPathsAway to dynamically calculate which parallel line to follow.
    /// </summary>
    private void CalculateAutoSteerGuidance(AgValoniaGPS.Models.Position currentPosition)
    {
        var track = SelectedTrack;
        if (track == null) return;

        // Convert heading from degrees to radians for the algorithm
        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

        // Calculate AB line heading (from the original/reference AB line)
        double abDx = track.PointB.Easting - track.PointA.Easting;
        double abDy = track.PointB.Northing - track.PointA.Northing;
        double abHeading = Math.Atan2(abDx, abDy); // Note: atan2(dx, dy) for north-based heading

        // Determine if vehicle is heading the same way as the AB line
        double headingDiff = headingRadians - abHeading;
        // Normalize to -PI to PI
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        bool isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Calculate the perpendicular offset distance based on howManyPathsAway
        // This is the key insight from AgOpenGPS - the guidance line is dynamically calculated
        double widthMinusOverlap = Vehicle.TrackWidth; // Could subtract overlap if needed
        double distAway = widthMinusOverlap * _howManyPathsAway;

        // Calculate the perpendicular direction (90 degrees from AB heading)
        double perpAngle = abHeading + Math.PI / 2; // Always use same perpendicular reference
        double offsetEasting = Math.Sin(perpAngle) * distAway;
        double offsetNorthing = Math.Cos(perpAngle) * distAway;

        // Calculate the current guidance line points (offset from reference AB line)
        double currentPtAEasting = track.PointA.Easting + offsetEasting;
        double currentPtANorthing = track.PointA.Northing + offsetNorthing;
        double currentPtBEasting = track.PointB.Easting + offsetEasting;
        double currentPtBNorthing = track.PointB.Northing + offsetNorthing;

        // Debug: log which offset we're following every second (30 frames at 30fps)
        if (_youTurnCounter % 30 == 0)
        {
            Console.WriteLine($"[AutoSteer] Following path {_howManyPathsAway}, offset {distAway:F1}m, heading {(isHeadingSameWay ? "same" : "opposite")}");
            Console.WriteLine($"[AutoSteer] Current line: A({currentPtAEasting:F1},{currentPtANorthing:F1}) B({currentPtBEasting:F1},{currentPtBNorthing:F1})");
        }

        // Calculate dynamic look-ahead distance based on speed
        double speed = currentPosition.Speed * 3.6; // Convert m/s to km/h for look-ahead calc
        double lookAhead = Guidance.GoalPointLookAheadHold;
        if (speed > 1)
        {
            lookAhead = Math.Max(
                Guidance.MinLookAheadDistance,
                Guidance.GoalPointLookAheadHold + (speed * Guidance.GoalPointLookAheadMult * 0.1)
            );
        }

        // Create a Track from the dynamically calculated line
        var currentTrack = Models.Track.Track.FromABLine(
            "CurrentGuidance",
            new Vec3(currentPtAEasting, currentPtANorthing, abHeading),
            new Vec3(currentPtBEasting, currentPtBNorthing, abHeading));

        // Calculate steer axle position (ahead of pivot by wheelbase)
        double steerEasting = currentPosition.Easting + Math.Sin(headingRadians) * Vehicle.Wheelbase;
        double steerNorthing = currentPosition.Northing + Math.Cos(headingRadians) * Vehicle.Wheelbase;

        // Build unified guidance input
        var input = new TrackGuidanceInput
        {
            Track = currentTrack,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            SteerPosition = new Vec3(steerEasting, steerNorthing, headingRadians),
            UseStanley = false, // Use Pure Pursuit
            IsHeadingSameWay = isHeadingSameWay,

            // Vehicle configuration
            Wheelbase = Vehicle.Wheelbase,
            MaxSteerAngle = Vehicle.MaxSteerAngle,
            GoalPointDistance = lookAhead,
            SideHillCompFactor = 0, // No IMU roll compensation in simulator

            // Pure Pursuit gains
            PurePursuitIntegralGain = Guidance.PurePursuitIntegralGain,

            // Vehicle state
            FixHeading = headingRadians,
            AvgSpeed = speed,
            IsReverse = false,
            IsAutoSteerOn = true,
            IsYouTurnTriggered = _isYouTurnTriggered,

            // AHRS data (88888 = invalid/no IMU)
            ImuRoll = 88888,

            // Previous state for filtering/integration
            PreviousState = _trackGuidanceState,
            FindGlobalNearest = _trackGuidanceState == null // Global search on first iteration
        };

        // Calculate guidance using unified service
        var output = _trackGuidanceService.CalculateGuidance(input);

        // Store state for next iteration
        _trackGuidanceState = output.State;

        // Update centralized guidance state
        State.Guidance.UpdateFromGuidance(output);

        // Apply calculated steering to simulator
        SimulatorSteerAngle = output.SteerAngle;

        // Update cross-track error for display (convert from meters to cm) - legacy property
        CrossTrackError = output.CrossTrackError * 100;

        // Update the map to show the current guidance line
        UpdateActiveLineVisualization(currentPtAEasting, currentPtANorthing, currentPtBEasting, currentPtBNorthing);
    }

    /// <summary>
    /// Update the map visualization to show the current dynamically-calculated guidance line.
    /// </summary>
    private void UpdateActiveLineVisualization(double ptAEasting, double ptANorthing, double ptBEasting, double ptBNorthing)
    {
        // Create a temporary Track for visualization that represents the current offset line
        var currentGuidanceTrack = Track.FromABLine(
            "CurrentGuidance",
            new Vec3(ptAEasting, ptANorthing, 0),
            new Vec3(ptBEasting, ptBNorthing, 0));
        currentGuidanceTrack.IsActive = true;
        _mapService.SetActiveTrack(currentGuidanceTrack);
    }

    /// <summary>
    /// Process YouTurn - check distance to headland, create turn path if needed, trigger turn.
    /// </summary>
    private void ProcessYouTurn(AgValoniaGPS.Models.Position currentPosition)
    {
        var track = SelectedTrack;
        if (track == null || track.Points.Count < 2 || _currentHeadlandLine == null) return;

        var trackPointA = track.Points[0];
        var trackPointB = track.Points[track.Points.Count - 1];

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

        // Calculate track heading to determine direction
        double abDx = trackPointB.Easting - trackPointA.Easting;
        double abDy = trackPointB.Northing - trackPointA.Northing;
        double abHeading = Math.Atan2(abDx, abDy);

        // Determine if vehicle is heading the same way as the AB line
        double headingDiff = headingRadians - abHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        _isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Check if vehicle is aligned with AB line (not mid-turn)
        // We need to be within ~20 degrees of the AB line direction (either forward or reverse)
        // Math.Abs(headingDiff) < PI/2 means heading same way, > PI/2 means opposite
        // We want to check alignment to either direction of the AB line
        double alignmentTolerance = Math.PI / 9;  // ~20 degrees
        bool alignedForward = Math.Abs(headingDiff) < alignmentTolerance;
        bool alignedReverse = Math.Abs(headingDiff) > (Math.PI - alignmentTolerance);
        bool isAlignedWithABLine = alignedForward || alignedReverse;

        // Only calculate distance to headland when aligned with the AB line
        // This prevents creating turns while mid-turn when heading changes rapidly
        if (isAlignedWithABLine)
        {
            // IMPORTANT: Calculate distance using the travel heading (AB heading adjusted for direction),
            // not the vehicle heading. This ensures the raycast direction matches the path construction
            // direction, preventing arc positioning errors when vehicle heading differs from AB heading.
            double travelHeading = abHeading;
            if (!_isHeadingSameWay)
            {
                travelHeading += Math.PI;
                if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
            }
            _distanceToHeadland = CalculateDistanceToHeadland(currentPosition, travelHeading);
        }
        else
        {
            _distanceToHeadland = double.MaxValue;  // Don't detect headland if not aligned
        }

        // Create U-turn path when approaching the headland ahead
        // The raycast already looks in the direction we're heading, so it finds the headland in front
        // We only need to check if we're within a reasonable trigger distance (not too close, not too far)
        double minDistanceToCreate = 30.0;  // meters - don't create if we're already too close (in the turn zone)

        // The headland must be ahead of us (raycast found something) and not too close
        // AND we must be aligned with the AB line (not mid-turn)
        bool headlandAhead = _distanceToHeadland > minDistanceToCreate &&
                             _distanceToHeadland < double.MaxValue &&
                             isAlignedWithABLine;

        // Debug: Log status periodically
        if (_youTurnPath == null && !_isInYouTurn && _youTurnCounter % 60 == 0)
        {
            Console.WriteLine($"[YouTurn] Status: distToHeadland={_distanceToHeadland:F1}m, headlandAhead={headlandAhead}, aligned={isAlignedWithABLine}, counter={_youTurnCounter}");
        }

        if (_youTurnPath == null && _youTurnCounter >= 4 && !_isInYouTurn && headlandAhead)
        {
            // First check if a U-turn would put us outside the boundary
            if (WouldNextLineBeInsideBoundary(track, abHeading))
            {
                Console.WriteLine($"[YouTurn] Creating turn path - dist ahead: {_distanceToHeadland:F1}m");
                CreateYouTurnPath(currentPosition, headingRadians, abHeading);
            }
            else
            {
                Console.WriteLine("[YouTurn] Next line would be outside boundary - stopping U-turns");
                StatusMessage = "End of field reached";
            }
        }
        // If we have a valid path and distance is close, trigger the turn
        else if (_youTurnPath != null && _youTurnPath.Count > 2 && !_isYouTurnTriggered && !_isInYouTurn)
        {
            // Calculate distance to turn start point
            double distToTurnStart = Math.Sqrt(
                Math.Pow(currentPosition.Easting - _youTurnPath[0].Easting, 2) +
                Math.Pow(currentPosition.Northing - _youTurnPath[0].Northing, 2));

            // Trigger when within 2 meters of turn start
            if (distToTurnStart <= 2.0)
            {
                // Update centralized state
                State.YouTurn.IsTriggered = true;
                State.YouTurn.IsExecuting = true;

                _isYouTurnTriggered = true;
                _isInYouTurn = true;
                StatusMessage = "YouTurn triggered!";
                Console.WriteLine($"[YouTurn] Triggered at distance {distToTurnStart:F2}m from turn start");

                // Compute the next track (offset by row skip width)
                ComputeNextTrack(track, abHeading);
            }
        }

        // Check if U-turn is complete (vehicle reached end of turn path)
        if (_isInYouTurn && _youTurnPath != null && _youTurnPath.Count > 2)
        {
            var startPoint = _youTurnPath[0];
            var endPoint = _youTurnPath[_youTurnPath.Count - 1];

            double distToTurnStart = Math.Sqrt(
                Math.Pow(currentPosition.Easting - startPoint.Easting, 2) +
                Math.Pow(currentPosition.Northing - startPoint.Northing, 2));
            double distToTurnEnd = Math.Sqrt(
                Math.Pow(currentPosition.Easting - endPoint.Easting, 2) +
                Math.Pow(currentPosition.Northing - endPoint.Northing, 2));

            // Complete turn when:
            // 1. Within 2 meters of turn end, AND
            // 2. Closer to end than to start (prevents immediate completion when start/end are close)
            // 3. At least 5 meters from start (ensures we've actually traveled into the turn)
            if (distToTurnEnd <= 2.0 && distToTurnEnd < distToTurnStart && distToTurnStart > 5.0)
            {
                CompleteYouTurn();
            }
        }
    }

    /// <summary>
    /// Check if the next track (after a U-turn) would be inside the field boundary.
    /// </summary>
    private bool WouldNextLineBeInsideBoundary(Track currentTrack, double abHeading)
    {
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
            return true; // No boundary, assume OK

        if (currentTrack.Points.Count < 2)
            return true; // Invalid track, assume OK

        var pointA = currentTrack.Points[0];
        var pointB = currentTrack.Points[currentTrack.Points.Count - 1];

        // Calculate where the next line would be
        int rowSkipWidth = UTurnSkipRows + 1;
        double offsetDistance = rowSkipWidth * Vehicle.TrackWidth;

        // Perpendicular offset direction
        double perpAngle = abHeading + (_isHeadingSameWay ? -Math.PI / 2 : Math.PI / 2);
        double offsetEasting = Math.Sin(perpAngle) * offsetDistance;
        double offsetNorthing = Math.Cos(perpAngle) * offsetDistance;

        // Check if midpoint of next line would be inside boundary
        double midEasting = (pointA.Easting + pointB.Easting) / 2 + offsetEasting;
        double midNorthing = (pointA.Northing + pointB.Northing) / 2 + offsetNorthing;

        return IsPointInsideBoundary(midEasting, midNorthing);
    }

    /// <summary>
    /// Check if a point is inside the outer boundary.
    /// </summary>
    private bool IsPointInsideBoundary(double easting, double northing)
    {
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
            return true;

        var points = _currentBoundary.OuterBoundary.Points;
        int n = points.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = points[i];
            var pj = points[j];

            if (((pi.Northing > northing) != (pj.Northing > northing)) &&
                (easting < (pj.Easting - pi.Easting) * (northing - pi.Northing) / (pj.Northing - pi.Northing) + pi.Easting))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Compute the next track offset perpendicular to the current line.
    /// </summary>
    private void ComputeNextTrack(Track referenceTrack, double abHeading)
    {
        if (referenceTrack.Points.Count < 2)
            return;

        var refPointA = referenceTrack.Points[0];
        var refPointB = referenceTrack.Points[referenceTrack.Points.Count - 1];

        // Following AgOpenGPS approach exactly:
        // howManyPathsAway += (isTurnLeft ^ isHeadingSameWay) ? rowSkipsWidth : -rowSkipsWidth
        // XOR truth table:
        //   turnLeft=true,  sameWay=true  -> true  -> positive offset (left of AB when facing A->B)
        //   turnLeft=true,  sameWay=false -> false -> negative offset
        //   turnLeft=false, sameWay=true  -> false -> negative offset
        //   turnLeft=false, sameWay=false -> true  -> positive offset
        int rowSkipWidth = UTurnSkipRows + 1;

        // Calculate offset direction using XOR like AgOpenGPS
        bool positiveOffset = _isTurnLeft ^ _isHeadingSameWay;
        int offsetChange = positiveOffset ? rowSkipWidth : -rowSkipWidth;
        int nextPathsAway = _howManyPathsAway + offsetChange;

        // Calculate the total offset for the next line
        double widthMinusOverlap = Vehicle.TrackWidth;
        double nextDistAway = widthMinusOverlap * nextPathsAway;

        // Calculate the perpendicular direction (90 degrees from AB heading)
        // Positive offset is to the LEFT of the AB line (when looking from A to B)
        double perpAngle = abHeading + Math.PI / 2;
        double offsetEasting = Math.Sin(perpAngle) * nextDistAway;
        double offsetNorthing = Math.Cos(perpAngle) * nextDistAway;

        // Create the next track for visualization (relative to reference track)
        _nextTrack = Track.FromABLine(
            $"Path {nextPathsAway}",
            new Vec3(refPointA.Easting + offsetEasting, refPointA.Northing + offsetNorthing, abHeading),
            new Vec3(refPointB.Easting + offsetEasting, refPointB.Northing + offsetNorthing, abHeading));
        _nextTrack.IsActive = false;

        Console.WriteLine($"[YouTurn] Turn {(_isTurnLeft ? "LEFT" : "RIGHT")}, heading {(_isHeadingSameWay ? "SAME" : "OPPOSITE")} way");
        Console.WriteLine($"[YouTurn] Offset {(positiveOffset ? "positive" : "negative")}: path {_howManyPathsAway} -> {nextPathsAway} ({nextDistAway:F1}m)");

        // Update map visualization
        _mapService.SetNextTrack(_nextTrack);
        _mapService.SetIsInYouTurn(true);
    }

    /// <summary>
    /// Complete the U-turn: switch to the next line and reset state.
    /// </summary>
    private void CompleteYouTurn()
    {
        // Save the turn completion position (end of turn path) to prevent immediate re-triggering
        if (_youTurnPath != null && _youTurnPath.Count > 0)
        {
            var endPoint = _youTurnPath[_youTurnPath.Count - 1];
            _lastTurnCompletionPosition = new Vec2(endPoint.Easting, endPoint.Northing);
        }

        // Following AgOpenGPS approach exactly:
        // howManyPathsAway += (isTurnLeft ^ isHeadingSameWay) ? rowSkipsWidth : -rowSkipsWidth
        int rowSkipWidth = UTurnSkipRows + 1;

        // Calculate offset direction using XOR like AgOpenGPS
        // IMPORTANT: Use _wasHeadingSameWayAtTurnStart (saved at turn creation), NOT _isHeadingSameWay
        // (which has now flipped because we completed a 180° turn)
        bool positiveOffset = _isTurnLeft ^ _wasHeadingSameWayAtTurnStart;
        int offsetChange = positiveOffset ? rowSkipWidth : -rowSkipWidth;
        _howManyPathsAway += offsetChange;

        Console.WriteLine($"[YouTurn] Turn complete! Turn was {(_isTurnLeft ? "LEFT" : "RIGHT")}, heading WAS {(_wasHeadingSameWayAtTurnStart ? "SAME" : "OPPOSITE")} at start");
        Console.WriteLine($"[YouTurn] Offset {(positiveOffset ? "positive" : "negative")} by {offsetChange}, now on path {_howManyPathsAway}");
        Console.WriteLine($"[YouTurn] Total offset: {Vehicle.TrackWidth * _howManyPathsAway:F1}m from reference line");

        // Remember this turn direction for alternating pattern
        _lastTurnWasLeft = _isTurnLeft;
        _hasCompletedFirstTurn = true;

        // Update centralized state
        State.YouTurn.LastTurnWasLeft = _isTurnLeft;
        State.YouTurn.HasCompletedFirstTurn = true;
        State.YouTurn.IsTriggered = false;
        State.YouTurn.IsExecuting = false;
        State.YouTurn.TurnPath = null;

        // Clear the U-turn state
        _isYouTurnTriggered = false;
        _isInYouTurn = false;
        _youTurnPath = null;
        _nextTrack = null;
        _youTurnCounter = 10; // Keep high so next U-turn path is created when conditions are met

        // Update map visualization - clear the old turn path and next line
        // The active line will be updated by UpdateActiveLineVisualization in CalculateAutoSteerGuidance
        _mapService.SetYouTurnPath(null);
        _mapService.SetNextTrack(null);
        _mapService.SetIsInYouTurn(false);

        StatusMessage = $"Following path {_howManyPathsAway} ({Vehicle.TrackWidth * Math.Abs(_howManyPathsAway):F1}m offset)";
    }

    /// <summary>
    /// Calculate distance from current position to the headland boundary in the direction of travel.
    /// </summary>
    private double CalculateDistanceToHeadland(AgValoniaGPS.Models.Position currentPosition, double headingRadians)
    {
        if (_currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
            return double.MaxValue;

        // Use a simple raycast approach
        double minDistance = double.MaxValue;
        Vec2 pos = new Vec2(currentPosition.Easting, currentPosition.Northing);
        Vec2 dir = new Vec2(Math.Sin(headingRadians), Math.Cos(headingRadians));

        int n = _currentHeadlandLine.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = _currentHeadlandLine[i];
            var p2 = _currentHeadlandLine[(i + 1) % n];

            // Ray-segment intersection
            Vec2 edge = new Vec2(p2.Easting - p1.Easting, p2.Northing - p1.Northing);
            Vec2 toP1 = new Vec2(p1.Easting - pos.Easting, p1.Northing - pos.Northing);

            double cross = dir.Easting * edge.Northing - dir.Northing * edge.Easting;
            if (Math.Abs(cross) < 1e-10) continue; // Parallel

            double t = (toP1.Easting * edge.Northing - toP1.Northing * edge.Easting) / cross;
            double u = (toP1.Easting * dir.Northing - toP1.Northing * dir.Easting) / cross;

            if (t > 0 && u >= 0 && u <= 1)
            {
                if (t < minDistance)
                    minDistance = t;
            }
        }

        return minDistance;
    }

    /// <summary>
    /// Create a YouTurn path when approaching headland.
    /// Uses a simplified direct approach that creates entry leg, semicircle, and exit leg.
    /// </summary>
    private void CreateYouTurnPath(AgValoniaGPS.Models.Position currentPosition, double headingRadians, double abHeading)
    {
        var track = SelectedTrack;
        if (track == null || _currentHeadlandLine == null) return;

        // Determine turn direction for zig-zag pattern across field
        // The turn direction depends on whether we're incrementing tracks (_howManyPathsAway increasing)
        // For zig-zag pattern:
        // - When going A→B (same way): turn LEFT to increment track
        // - When going B→A (opposite way): turn RIGHT to increment track
        // This creates alternating left/right turns as we traverse the field
        bool turnLeft = _isHeadingSameWay;  // Same direction = turn left, opposite = turn right
        _isTurnLeft = turnLeft;
        _wasHeadingSameWayAtTurnStart = _isHeadingSameWay;

        Console.WriteLine($"[YouTurn] Creating turn with YouTurnCreationService: direction={(_isTurnLeft ? "LEFT" : "RIGHT")}, isHeadingSameWay={_isHeadingSameWay}, pathsAway={_howManyPathsAway}");

        try
        {
            // Build the YouTurnCreationInput with proper boundary wiring
            var input = BuildYouTurnCreationInput(currentPosition, headingRadians, abHeading, turnLeft);
            if (input == null)
            {
                Console.WriteLine($"[YouTurn] Failed to build creation input - using simple fallback");
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    State.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
                return;
            }

            // Use the YouTurnCreationService to create the path
            var output = _youTurnCreationService.CreateTurn(input);

            if (output.Success && output.TurnPath != null && output.TurnPath.Count > 10)
            {
                State.YouTurn.TurnPath = output.TurnPath;
                _youTurnPath = output.TurnPath;
                _youTurnCounter = 0;
                StatusMessage = $"YouTurn path created ({output.TurnPath.Count} points)";
                Console.WriteLine($"[YouTurn] Service path created with {output.TurnPath.Count} points, distToTurnLine={output.DistancePivotToTurnLine:F1}m");

                // Update map to show the turn path
                _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
            }
            else
            {
                Console.WriteLine($"[YouTurn] Service creation failed: {output.FailureReason ?? "unknown"}, using simple fallback");
                // Fall back to simple geometric approach
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    State.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YouTurn] Exception creating path: {ex.Message}");
            // Fall back to simple geometric approach
            try
            {
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    State.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Build the YouTurnCreationInput with proper boundary wiring.
    ///
    /// The IsPointInsideTurnArea delegate must return:
    /// - 0 = point is in the FIELD (safe to drive, inside headland boundary)
    /// - != 0 = point is in the TURN AREA (headland zone, where turn arc should be)
    ///
    /// We set this up with:
    /// - turnAreaPolygons[0] = outer field boundary (outer limit)
    /// - turnAreaPolygons[1] = headland boundary (inner limit, marks the field)
    ///
    /// So points between outer and headland return 0 (in outer but not in inner = headland zone... wait, that's wrong)
    /// Actually TurnAreaService returns 0 if in outer and NOT in any inner.
    /// So we need to INVERT the logic or structure it differently.
    ///
    /// Simpler approach: Create a custom delegate that directly tests:
    /// - If point is OUTSIDE outer boundary -> return 1 (out of bounds)
    /// - If point is INSIDE headland boundary (in the field) -> return 0 (safe)
    /// - Otherwise (in headland zone) -> return 1 (turn area)
    /// </summary>
    private YouTurnCreationInput? BuildYouTurnCreationInput(
        AgValoniaGPS.Models.Position currentPosition,
        double headingRadians,
        double abHeading,
        bool turnLeft)
    {
        // Need boundary to create turn boundaries
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
        {
            Console.WriteLine($"[YouTurn] No valid outer boundary available");
            return null;
        }

        var track = SelectedTrack;
        if (track == null)
        {
            Console.WriteLine($"[YouTurn] No track selected");
            return null;
        }

        // Tool width (track width in our case)
        double toolWidth = Vehicle.TrackWidth;

        // Total headland width from the headland multiplier setting
        double totalHeadlandWidth = HeadlandCalculatedWidth;

        // Create outer boundary Vec3 list
        var outerPoints = _currentBoundary.OuterBoundary.Points
            .Select(p => new Vec2(p.Easting, p.Northing))
            .ToList();
        var outerBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(outerPoints);

        // Create turn boundary: outer boundary offset inward by 1 tool width
        // This is the line that the turn arc should tangent
        var turnBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, toolWidth);
        if (turnBoundaryVec2 == null || turnBoundaryVec2.Count < 3)
        {
            Console.WriteLine($"[YouTurn] Failed to create turn boundary (1 tool width offset)");
            return null;
        }
        var turnBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(turnBoundaryVec2);

        // Create headland boundary: outer boundary offset inward by total headland width
        // This marks the inner edge of the turn zone (where the field starts)
        var headlandBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, totalHeadlandWidth);
        if (headlandBoundaryVec2 == null || headlandBoundaryVec2.Count < 3)
        {
            Console.WriteLine($"[YouTurn] Failed to create headland boundary");
            return null;
        }
        var headlandBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(headlandBoundaryVec2);

        // Create the BoundaryTurnLine for the target turn boundary (where turn tangents)
        var boundaryTurnLines = new List<BoundaryTurnLine>
        {
            new BoundaryTurnLine
            {
                Points = turnBoundaryVec3,
                BoundaryIndex = 0
            }
        };

        // HeadlandWidth = distance from headland boundary to turn boundary
        double headlandWidthForTurn = Math.Max(totalHeadlandWidth - toolWidth, toolWidth);

        // Create IsPointInsideTurnArea delegate
        // Returns: 0 = inside field (OK), != 0 = in turn area or out of bounds
        Func<Vec3, int> isPointInsideTurnArea = (point) =>
        {
            // Check if outside outer boundary (completely out of bounds)
            if (!GeometryMath.IsPointInPolygon(outerBoundaryVec3, point))
            {
                return -1; // Outside field entirely
            }

            // Check if inside headland boundary (in the working field)
            if (GeometryMath.IsPointInPolygon(headlandBoundaryVec3, point))
            {
                return 0; // In the field - safe to drive
            }

            // Point is between outer and headland = in the turn zone
            return 1; // In turn area
        };

        // Build the input
        var input = new YouTurnCreationInput
        {
            TurnType = YouTurnType.AlbinStyle,
            IsTurnLeft = turnLeft,
            GuidanceType = GuidanceLineType.ABLine,

            // Boundary data - the turn line the path should tangent
            BoundaryTurnLines = boundaryTurnLines,

            // Custom delegate for turn area testing
            IsPointInsideTurnArea = isPointInsideTurnArea,

            // AB line guidance data
            ABHeading = abHeading,
            // Calculate reference point on the CURRENT track (not the original AB line)
            // Offset PointA perpendicular to the AB heading by howManyPathsAway * trackWidth
            ABReferencePoint = CalculateCurrentTrackReferencePoint(track, toolWidth, abHeading),
            IsHeadingSameWay = _isHeadingSameWay,

            // Vehicle position and configuration
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            ToolWidth = toolWidth,
            ToolOverlap = _vehicleProfileService.ActiveProfile?.Tool.Overlap ?? 0.0,
            ToolOffset = _vehicleProfileService.ActiveProfile?.Tool.Offset ?? 0.0,
            TurnRadius = _vehicleProfileService.ActiveProfile?.YouTurn.TurnRadius ?? 8.0,

            // Turn parameters
            RowSkipsWidth = UTurnSkipRows + 1,
            TurnStartOffset = 0,
            HowManyPathsAway = _howManyPathsAway,
            NudgeDistance = 0.0,
            TrackMode = 0, // Standard mode

            // State machine
            MakeUTurnCounter = _youTurnCounter + 10, // Ensure we pass the throttle check

            // Leg extension - this is key for proper leg length through headland
            YouTurnLegExtensionMultiplier = 2.5,
            HeadlandWidth = headlandWidthForTurn
        };

        Console.WriteLine($"[YouTurn] Input built: toolWidth={toolWidth:F1}m, totalHeadland={totalHeadlandWidth:F1}m, headlandWidthForTurn={headlandWidthForTurn:F1}m, turnBoundaryPoints={turnBoundaryVec3.Count}, headlandPoints={headlandBoundaryVec3.Count}");

        return input;
    }

    /// <summary>
    /// Calculate a reference point on the current track (offset from the original AB line).
    /// The track number is determined by _howManyPathsAway.
    /// </summary>
    private Vec2 CalculateCurrentTrackReferencePoint(Track track, double toolWidth, double abHeading)
    {
        if (track.Points.Count == 0)
            return new Vec2(0, 0);

        // Start with the first point on the original track
        double baseEasting = track.Points[0].Easting;
        double baseNorthing = track.Points[0].Northing;

        // Calculate perpendicular offset to get to the current track
        // The perpendicular direction is 90° from the AB heading
        double perpAngle = abHeading + Math.PI / 2.0;

        // The offset distance is howManyPathsAway * toolWidth
        double offsetDistance = _howManyPathsAway * toolWidth;

        // Apply the offset perpendicular to the AB line
        double offsetEasting = baseEasting + Math.Sin(perpAngle) * offsetDistance;
        double offsetNorthing = baseNorthing + Math.Cos(perpAngle) * offsetDistance;

        Console.WriteLine($"[YouTurn] Reference point: howManyPathsAway={_howManyPathsAway}, offset={offsetDistance:F2}m, perpAngle={perpAngle * 180 / Math.PI:F1}°");

        return new Vec2(offsetEasting, offsetNorthing);
    }

    /// <summary>
    /// Create a simple U-turn path directly using geometry.
    /// This creates a SYMMETRICAL U-turn by calculating exact endpoint positions first,
    /// then building the path to connect them.
    /// </summary>
    private List<Vec3> CreateSimpleUTurnPath(AgValoniaGPS.Models.Position currentPosition, double headingRadians, double abHeading, bool turnLeft)
    {
        var path = new List<Vec3>();

        // Parameters
        double pointSpacing = 0.5; // meters between path points
        int rowSkipWidth = UTurnSkipRows + 1;
        double trackWidth = Vehicle.TrackWidth;
        double turnOffset = trackWidth * rowSkipWidth; // Perpendicular distance to next track

        // Turn radius = half of turn offset so semicircle diameter = track spacing
        double turnRadius = turnOffset / 2.0;

        // Minimum turn radius constraint
        double minTurnRadius = 4.0;
        if (turnRadius < minTurnRadius)
        {
            turnRadius = minTurnRadius;
        }

        // Get the heading we're traveling (adjusted for same/opposite to AB)
        double travelHeading = abHeading;
        if (!_isHeadingSameWay)
        {
            travelHeading += Math.PI;
            if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
        }

        // Exit heading is 180° opposite (going back toward field)
        double exitHeading = travelHeading + Math.PI;
        if (exitHeading >= Math.PI * 2) exitHeading -= Math.PI * 2;

        // Perpendicular direction (toward next track)
        double perpAngle = turnLeft ? (travelHeading - Math.PI / 2) : (travelHeading + Math.PI / 2);

        // Calculate the headland boundary point on CURRENT track
        double distToHeadland = _distanceToHeadland;
        double headlandBoundaryEasting = currentPosition.Easting + Math.Sin(travelHeading) * distToHeadland;
        double headlandBoundaryNorthing = currentPosition.Northing + Math.Cos(travelHeading) * distToHeadland;

        // Leg lengths
        // The arc extends turnRadius beyond the arc start (toward the outer boundary)
        // So: arc_top_position = headlandLegLength + turnRadius
        // We want arc_top to be at HeadlandDistance (at the outer boundary)
        // Therefore: headlandLegLength = HeadlandDistance - turnRadius
        // But ensure arc start is at least a small margin past the headland boundary
        double minArcStartMargin = 2.0; // meters past headland boundary
        double headlandLegLength = Math.Max(HeadlandDistance - turnRadius, minArcStartMargin);

        // How far path extends into cultivated area (entry/exit legs)
        double fieldLegLength = Math.Max(HeadlandDistance * 0.5, turnRadius);

        Console.WriteLine($"[YouTurn] HeadlandBoundary: E={headlandBoundaryEasting:F1}, N={headlandBoundaryNorthing:F1}");
        Console.WriteLine($"[YouTurn] HeadlandDistance={HeadlandDistance:F1}m, headlandLegLength={headlandLegLength:F1}m, turnRadius={turnRadius:F1}m, turnOffset={turnOffset:F1}m");
        Console.WriteLine($"[YouTurn] Arc will extend to {headlandLegLength + turnRadius:F1}m past headland boundary (headland zone is {HeadlandDistance:F1}m)");

        // ============================================
        // CALCULATE KEY WAYPOINTS IN ABSOLUTE COORDINATES
        // ============================================
        // The U-turn connects two parallel AB lines separated by turnOffset.
        // Entry start and exit end must BOTH be in the cultivated area (outside headland).
        // The arc happens deep in the headland.

        // STEP 1: Calculate the ENTRY START position (green marker)
        // This is on the CURRENT track, fieldLegLength BEHIND the headland boundary
        double entryStartE = headlandBoundaryEasting - Math.Sin(travelHeading) * fieldLegLength;
        double entryStartN = headlandBoundaryNorthing - Math.Cos(travelHeading) * fieldLegLength;

        // STEP 2: Calculate the ARC START position
        // This is on the CURRENT track, deep in the headland
        double arcStartE = headlandBoundaryEasting + Math.Sin(travelHeading) * headlandLegLength;
        double arcStartN = headlandBoundaryNorthing + Math.Cos(travelHeading) * headlandLegLength;

        // STEP 3: Calculate the ARC CENTER (center of semicircle)
        // Perpendicular from arc start by turnRadius
        double arcCenterE = arcStartE + Math.Sin(perpAngle) * turnRadius;
        double arcCenterN = arcStartN + Math.Cos(perpAngle) * turnRadius;

        // STEP 4: Calculate the ARC END position
        // Arc end is where the semicircle ends: diameter = 2 * turnRadius from arcStart
        // (This may differ from turnOffset when turnRadius is clamped to minTurnRadius)
        double arcDiameter = 2.0 * turnRadius;
        double arcEndE = arcStartE + Math.Sin(perpAngle) * arcDiameter;
        double arcEndN = arcStartN + Math.Cos(perpAngle) * arcDiameter;

        // STEP 5: Calculate the EXIT END position (red marker)
        // The exit end must be on the NEXT track, at the same distance from headland as entry start
        // Since perpAngle already points toward the next track (based on turnLeft and travelHeading),
        // we just need to offset by turnOffset in that direction
        double exitEndE = entryStartE + Math.Sin(perpAngle) * turnOffset;
        double exitEndN = entryStartN + Math.Cos(perpAngle) * turnOffset;
        Console.WriteLine($"[YouTurn] ExitEnd calc: entryStart({entryStartE:F1},{entryStartN:F1}) + perpAngle({perpAngle * 180 / Math.PI:F1}°) * {turnOffset:F1}m = ({exitEndE:F1},{exitEndN:F1})");
        Console.WriteLine($"[YouTurn] perpAngle direction: turnLeft={turnLeft}, travelHeading={travelHeading * 180 / Math.PI:F1}°");

        Console.WriteLine($"[YouTurn] turnOffset={turnOffset:F1}m, arcDiameter={arcDiameter:F1}m (2*turnRadius)");
        Console.WriteLine($"[YouTurn] EntryStart (green): E={entryStartE:F1}, N={entryStartN:F1}");
        Console.WriteLine($"[YouTurn] ExitEnd (red): E={exitEndE:F1}, N={exitEndN:F1} = entryStart + perpOffset({turnOffset:F1}m)");
        Console.WriteLine($"[YouTurn] ArcStart: E={arcStartE:F1}, N={arcStartN:F1}");
        Console.WriteLine($"[YouTurn] ArcEnd: E={arcEndE:F1}, N={arcEndN:F1} = arcStart + perpOffset({arcDiameter:F1}m)");

        // ============================================
        // BUILD PATH: Entry Leg
        // ============================================
        double totalEntryLength = fieldLegLength + headlandLegLength;
        int totalEntryPoints = (int)(totalEntryLength / pointSpacing);

        for (int i = 0; i <= totalEntryPoints; i++)
        {
            double dist = i * pointSpacing;
            Vec3 pt = new Vec3
            {
                Easting = entryStartE + Math.Sin(travelHeading) * dist,
                Northing = entryStartN + Math.Cos(travelHeading) * dist,
                Heading = travelHeading
            };
            path.Add(pt);
        }

        // ============================================
        // BUILD PATH: Semicircle Arc
        // ============================================
        // Generate arc points from arcStart to arcEnd around arcCenter
        int arcPoints = Math.Max((int)(Math.PI * turnRadius / pointSpacing), 20);

        for (int i = 1; i <= arcPoints; i++)
        {
            // Fraction around the arc (0 to 1)
            double t = (double)i / arcPoints;

            // Angle: start pointing back toward entry leg, sweep 180° toward exit leg
            // Start angle: direction from center to arcStart
            double startAngle = Math.Atan2(arcStartE - arcCenterE, arcStartN - arcCenterN);

            // Sweep direction in Easting/Northing coordinate system where:
            //   Easting = sin(angle), Northing = cos(angle)
            //   angle=0 is north, angle=π/2 is east, angle=π is south, angle=3π/2 is west
            // For left turn: arc center is to the left of travel direction
            //   We want to sweep AWAY from field (into headland), which means DECREASING angle
            // For right turn: arc center is to the right of travel direction
            //   We want to sweep AWAY from field (into headland), which means INCREASING angle
            double sweepAngle = turnLeft ? (-Math.PI * t) : (Math.PI * t);
            double currentAngle = startAngle + sweepAngle;

            // Point on arc
            double ptE = arcCenterE + Math.Sin(currentAngle) * turnRadius;
            double ptN = arcCenterN + Math.Cos(currentAngle) * turnRadius;

            // Heading is tangent to circle (perpendicular to radius)
            // For left turn (decreasing angle/clockwise), tangent is +90° from radius
            // For right turn (increasing angle/counter-clockwise), tangent is -90° from radius
            double tangentHeading = currentAngle + (turnLeft ? -Math.PI / 2 : Math.PI / 2);
            if (tangentHeading < 0) tangentHeading += Math.PI * 2;
            if (tangentHeading >= Math.PI * 2) tangentHeading -= Math.PI * 2;

            Vec3 pt = new Vec3
            {
                Easting = ptE,
                Northing = ptN,
                Heading = tangentHeading
            };
            path.Add(pt);
        }

        // ============================================
        // BUILD PATH: Exit Leg
        // ============================================
        // Exit leg goes from arcEnd to exitEnd (the pre-calculated symmetric endpoint)
        // Calculate the actual distance from arcEnd to exitEnd
        double exitLegDeltaE = exitEndE - arcEndE;
        double exitLegDeltaN = exitEndN - arcEndN;
        double actualExitLength = Math.Sqrt(exitLegDeltaE * exitLegDeltaE + exitLegDeltaN * exitLegDeltaN);
        int totalExitPoints = Math.Max((int)(actualExitLength / pointSpacing), 10);

        for (int i = 1; i <= totalExitPoints; i++)
        {
            // Interpolate from arcEnd to exitEnd
            double t = (double)i / totalExitPoints;
            Vec3 pt = new Vec3
            {
                Easting = arcEndE + exitLegDeltaE * t,
                Northing = arcEndN + exitLegDeltaN * t,
                Heading = exitHeading
            };
            path.Add(pt);
        }

        Console.WriteLine($"[YouTurn] Path has {path.Count} points: {totalEntryPoints + 1} entry, {arcPoints} arc, {totalExitPoints} exit");
        Console.WriteLine($"[YouTurn] Actual entry start: E={path[0].Easting:F1}, N={path[0].Northing:F1}");
        Console.WriteLine($"[YouTurn] Actual exit end: E={path[path.Count - 1].Easting:F1}, N={path[path.Count - 1].Northing:F1}");

        return path;
    }

    /// <summary>
    /// Check if a point is inside the headland boundary.
    /// </summary>
    private bool IsPointInsideHeadland(Vec3 point)
    {
        if (_currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
            return false;

        // Use ray casting algorithm
        int n = _currentHeadlandLine.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = _currentHeadlandLine[i];
            var pj = _currentHeadlandLine[j];

            if (((pi.Northing > point.Northing) != (pj.Northing > point.Northing)) &&
                (point.Easting < (pj.Easting - pi.Easting) * (point.Northing - pi.Northing) / (pj.Northing - pi.Northing) + pi.Easting))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Calculate steering guidance while following the YouTurn path.
    /// </summary>
    private void CalculateYouTurnGuidance(AgValoniaGPS.Models.Position currentPosition)
    {
        if (_youTurnPath == null || _youTurnPath.Count == 0) return;

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;
        double speed = currentPosition.Speed * 3.6; // km/h

        var input = new YouTurnGuidanceInput
        {
            TurnPath = _youTurnPath,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            SteerPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            Wheelbase = Vehicle.Wheelbase,
            MaxSteerAngle = Vehicle.MaxSteerAngle,
            UseStanley = false, // Use Pure Pursuit for smoother turns
            GoalPointDistance = Guidance.GoalPointLookAheadHold,
            UTurnCompensation = Guidance.UTurnCompensation,
            FixHeading = headingRadians,
            AvgSpeed = speed,
            IsReverse = false,
            UTurnStyle = 0 // Albin style
        };

        var output = _youTurnGuidanceService.CalculateGuidance(input);

        if (output.IsTurnComplete)
        {
            // Turn complete - switch to next line and reset state
            Console.WriteLine("[YouTurn] Guidance detected turn complete, calling CompleteYouTurn");
            CompleteYouTurn();
        }
        else
        {
            // Apply steering from YouTurn guidance with compensation
            SimulatorSteerAngle = output.SteerAngle * Guidance.UTurnCompensation;

            // Update centralized guidance state
            State.Guidance.CrossTrackError = output.DistanceFromCurrentLine;
            State.Guidance.SteerAngle = output.SteerAngle;

            // Legacy property (for existing bindings - display in cm)
            CrossTrackError = output.DistanceFromCurrentLine * 100;
        }
    }

    // Boundary recording event handlers
    private void OnBoundaryPointAdded(object? sender, BoundaryPointAddedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update centralized state
            State.BoundaryRec.PointCount = e.TotalPoints;
            State.BoundaryRec.AreaHectares = e.AreaHectares;
            State.BoundaryRec.AreaAcres = e.AreaHectares * 2.47105;

            // Legacy properties
            BoundaryPointCount = e.TotalPoints;
            BoundaryAreaHectares = e.AreaHectares;

            // Update map with recorded points
            var points = _boundaryRecordingService.RecordedPoints
                .Select(p => (p.Easting, p.Northing))
                .ToList();
            _mapService.SetRecordingPoints(points);
        });
    }

    private void OnBoundaryStateChanged(object? sender, BoundaryRecordingStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Update centralized state
            State.BoundaryRec.IsRecording = e.State == BoundaryRecordingState.Recording;
            State.BoundaryRec.IsPaused = e.State == BoundaryRecordingState.Paused;
            State.BoundaryRec.PointCount = e.PointCount;
            State.BoundaryRec.AreaHectares = e.AreaHectares;
            State.BoundaryRec.AreaAcres = e.AreaHectares * 2.47105;

            // Legacy properties
            IsBoundaryRecording = e.State == BoundaryRecordingState.Recording;
            BoundaryPointCount = e.PointCount;
            BoundaryAreaHectares = e.AreaHectares;

            // Clear recording points from map when recording becomes idle
            if (e.State == BoundaryRecordingState.Idle)
            {
                State.BoundaryRec.RecordingPoints.Clear();
                _mapService.ClearRecordingPoints();
            }
            // Update map with current recorded points (for undo/clear operations)
            else if (e.PointCount >= 0)
            {
                var points = _boundaryRecordingService.RecordedPoints
                    .Select(p => (p.Easting, p.Northing))
                    .ToList();
                _mapService.SetRecordingPoints(points);
            }
        });
    }

    private void OnUdpDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        var now = DateTime.Now;
        var packetAge = (now - e.Timestamp).TotalMilliseconds;

        // Handle different message types
        if (e.PGN == 0)
        {
            // NMEA text sentence
            try
            {
                string sentence = System.Text.Encoding.ASCII.GetString(e.Data);
                _nmeaParser.ParseSentence(sentence);
            }
            catch { }
        }
        else
        {
            // Binary PGN message - log it with age to detect buffering
            DebugLog = $"PGN: {e.PGN} (0x{e.PGN:X2}) @ {e.Timestamp:HH:mm:ss.fff} (age: {packetAge:F0}ms)";

            switch (e.PGN)
            {
                case PgnNumbers.HELLO_FROM_AUTOSTEER:
                    // AutoSteer module is alive
                    break;

                case PgnNumbers.HELLO_FROM_MACHINE:
                    // Machine module is alive
                    break;

                case PgnNumbers.HELLO_FROM_IMU:
                    // IMU module is alive
                    break;

                // TODO: Add more PGN handlers as needed
            }
        }
    }

    private void OnModuleConnectionChanged(object? sender, ModuleConnectionEventArgs e)
    {
        // This event is no longer used - status is polled every 100ms
    }

    private void OnNtripConnectionChanged(object? sender, NtripConnectionEventArgs e)
    {
        // Marshal to UI thread (use Invoke for synchronous execution to avoid modal dialog issues)
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateNtripConnectionProperties(e);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateNtripConnectionProperties(e));
        }
    }

    private void UpdateNtripConnectionProperties(NtripConnectionEventArgs e)
    {
        // Update centralized state
        State.Connections.IsNtripConnected = e.IsConnected;
        State.Connections.NtripStatus = e.Message ?? (e.IsConnected ? "Connected" : "Not Connected");

        // Legacy property updates
        IsNtripConnected = e.IsConnected;
        NtripStatus = e.Message ?? (e.IsConnected ? "Connected" : "Not Connected");
    }

    private void OnRtcmDataReceived(object? sender, RtcmDataReceivedEventArgs e)
    {
        // Marshal to UI thread (use Invoke for synchronous execution to avoid modal dialog issues)
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateNtripDataProperties();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateNtripDataProperties());
        }
    }

    private void UpdateNtripDataProperties()
    {
        // Update centralized state
        State.Connections.NtripBytesReceived = _ntripService.TotalBytesReceived;

        // Legacy property updates
        _ntripBytesReceived = _ntripService.TotalBytesReceived;
        this.RaisePropertyChanged(nameof(NtripBytesReceived));
    }

    private void UpdateStatusMessage()
    {
        int connectedCount = 0;
        if (IsAutoSteerDataOk) connectedCount++;
        if (IsMachineDataOk) connectedCount++;
        if (IsImuDataOk) connectedCount++;

        StatusMessage = connectedCount > 0
            ? $"{connectedCount} module(s) active"
            : "Waiting for modules...";
    }

    private string GetFixQualityString(int fixQuality) => fixQuality switch
    {
        0 => "No Fix",
        1 => "GPS Fix",
        2 => "DGPS Fix",
        4 => "RTK Fixed",
        5 => "RTK Float",
        _ => "Unknown"
    };

    // Field management properties
    public Field? ActiveField
    {
        get => _activeField;
        set => this.RaiseAndSetIfChanged(ref _activeField, value);
    }

    public string FieldsRootDirectory
    {
        get => _fieldsRootDirectory;
        set => this.RaiseAndSetIfChanged(ref _fieldsRootDirectory, value);
    }

    public string? ActiveFieldName => ActiveField?.Name;
    public double? ActiveFieldArea => ActiveField?.TotalArea;

    // Services exposed for UI/control access
    public AgValoniaGPS.Services.Interfaces.IFieldStatisticsService FieldStatistics => _fieldStatistics;

    // Field statistics properties for UI binding
    public string WorkedAreaDisplay => FormatArea(_fieldStatistics.Statistics.WorkedAreaTotal);
    public string BoundaryAreaDisplay => FormatArea(_fieldStatistics.Statistics.AreaOuterBoundary);
    public double RemainingPercent => _fieldStatistics.Statistics.AreaBoundaryOuterLessInner > 0
        ? ((_fieldStatistics.Statistics.AreaBoundaryOuterLessInner - _fieldStatistics.Statistics.WorkedAreaTotal) * 100 / _fieldStatistics.Statistics.AreaBoundaryOuterLessInner)
        : 0;

    // Helper method to format area
    private string FormatArea(double squareMeters)
    {
        // Convert to hectares
        double hectares = squareMeters * 0.0001;
        return $"{hectares:F2} ha";
    }

    private void OnActiveFieldChanged(object? sender, Field? field)
    {
        // Marshal to UI thread
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateActiveField(field);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateActiveField(field));
        }
    }

    private void UpdateActiveField(Field? field)
    {
        // Update centralized state
        State.Field.ActiveField = field;

        // Legacy property
        ActiveField = field;
        this.RaisePropertyChanged(nameof(ActiveFieldName));
        this.RaisePropertyChanged(nameof(ActiveFieldArea));

        // Update field statistics service with new boundary
        if (field?.Boundary != null)
        {
            // Calculate boundary area and pass as list (outer boundary only for now)
            var boundaryAreas = new List<double> { field.TotalArea };
            _fieldStatistics.UpdateBoundaryAreas(boundaryAreas);
            this.RaisePropertyChanged(nameof(BoundaryAreaDisplay));
            this.RaisePropertyChanged(nameof(WorkedAreaDisplay));
            this.RaisePropertyChanged(nameof(RemainingPercent));
        }

        // Load headland from field directory if available
        LoadHeadlandFromField(field);

        // Load AB lines from field directory if available
        LoadTracksFromField(field);
    }

    /// <summary>
    /// Load headland line from field directory
    /// </summary>
    private void LoadHeadlandFromField(Field? field)
    {
        if (field == null || string.IsNullOrEmpty(field.DirectoryPath))
        {
            // Clear headland if no field - update centralized state
            State.Field.HeadlandLine = null;
            State.Field.HeadlandDistance = 0;

            _currentHeadlandLine = null;
            _mapService.SetHeadlandLine(null);
            HasHeadland = false;
            IsHeadlandOn = false;
            return;
        }

        try
        {
            var headlandLine = HeadlandLineSerializer.Load(field.DirectoryPath);

            if (headlandLine.Tracks.Count > 0 && headlandLine.Tracks[0].TrackPoints.Count > 0)
            {
                // Update centralized state
                State.Field.HeadlandLine = headlandLine.Tracks[0].TrackPoints;
                State.Field.HeadlandDistance = headlandLine.Tracks[0].MoveDistance;

                // Use direct field assignment to avoid triggering save
                _currentHeadlandLine = headlandLine.Tracks[0].TrackPoints;
                _mapService.SetHeadlandLine(_currentHeadlandLine);
                this.RaisePropertyChanged(nameof(CurrentHeadlandLine));

                HasHeadland = true;
                IsHeadlandOn = true;
                HeadlandDistance = headlandLine.Tracks[0].MoveDistance;

                Console.WriteLine($"[Headland] Loaded headland from {field.DirectoryPath} ({_currentHeadlandLine.Count} points)");
            }
            else
            {
                State.Field.HeadlandLine = null;
                State.Field.HeadlandDistance = 0;

                _currentHeadlandLine = null;
                _mapService.SetHeadlandLine(null);
                HasHeadland = false;
                IsHeadlandOn = false;
                Console.WriteLine($"[Headland] No headland found in {field.DirectoryPath}");
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[Headland] Failed to load headland: {ex.Message}");
            State.Field.HeadlandLine = null;
            State.Field.HeadlandDistance = 0;

            _currentHeadlandLine = null;
            _mapService.SetHeadlandLine(null);
            HasHeadland = false;
            IsHeadlandOn = false;
        }
    }

    // ========== View Settings ==========

    private bool _isViewSettingsPanelVisible;
    public bool IsViewSettingsPanelVisible
    {
        get => _isViewSettingsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isViewSettingsPanelVisible, value);
    }

    private bool _isFileMenuPanelVisible;
    public bool IsFileMenuPanelVisible
    {
        get => _isFileMenuPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isFileMenuPanelVisible, value);
    }

    private bool _isToolsPanelVisible;
    public bool IsToolsPanelVisible
    {
        get => _isToolsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isToolsPanelVisible, value);
    }

    private bool _isConfigurationPanelVisible;
    public bool IsConfigurationPanelVisible
    {
        get => _isConfigurationPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isConfigurationPanelVisible, value);
    }

    private bool _isJobMenuPanelVisible;
    public bool IsJobMenuPanelVisible
    {
        get => _isJobMenuPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isJobMenuPanelVisible, value);
    }

    private bool _isFieldToolsPanelVisible;
    public bool IsFieldToolsPanelVisible
    {
        get => _isFieldToolsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isFieldToolsPanelVisible, value);
    }

    private bool _isSimulatorPanelVisible;
    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isSimulatorPanelVisible, value);
    }

    // Panel-based dialog data properties (visibility now managed by State.UI)
    private decimal? _simCoordsDialogLatitude;
    public decimal? SimCoordsDialogLatitude
    {
        get => _simCoordsDialogLatitude;
        set => this.RaiseAndSetIfChanged(ref _simCoordsDialogLatitude, value);
    }

    private decimal? _simCoordsDialogLongitude;
    public decimal? SimCoordsDialogLongitude
    {
        get => _simCoordsDialogLongitude;
        set => this.RaiseAndSetIfChanged(ref _simCoordsDialogLongitude, value);
    }

    // Field Selection Dialog properties (visibility managed by State.UI)
    public ObservableCollection<FieldSelectionItem> AvailableFields { get; } = new();

    private FieldSelectionItem? _selectedFieldInfo;
    public FieldSelectionItem? SelectedFieldInfo
    {
        get => _selectedFieldInfo;
        set => this.RaiseAndSetIfChanged(ref _selectedFieldInfo, value);
    }

    private string _fieldSelectionDirectory = string.Empty;
    private bool _fieldsSortedAZ = false;

    // AB Line Creation Mode state (dialog visibility managed by State.UI)
    private ABCreationMode _currentABCreationMode = ABCreationMode.None;
    public ABCreationMode CurrentABCreationMode
    {
        get => _currentABCreationMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentABCreationMode, value);
            this.RaisePropertyChanged(nameof(IsCreatingABLine));
            this.RaisePropertyChanged(nameof(EnableABClickSelection));
            this.RaisePropertyChanged(nameof(ABCreationInstructions));
        }
    }

    private ABPointStep _currentABPointStep = ABPointStep.None;
    public ABPointStep CurrentABPointStep
    {
        get => _currentABPointStep;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentABPointStep, value);
            this.RaisePropertyChanged(nameof(ABCreationInstructions));
        }
    }

    // Temporary storage for Point A during AB creation
    private Position? _pendingPointA;
    public Position? PendingPointA
    {
        get => _pendingPointA;
        set => this.RaiseAndSetIfChanged(ref _pendingPointA, value);
    }

    // Computed properties for UI binding
    public bool IsCreatingABLine => CurrentABCreationMode != ABCreationMode.None;

    public bool EnableABClickSelection => CurrentABCreationMode == ABCreationMode.DrawAB ||
                                          CurrentABCreationMode == ABCreationMode.DriveAB;

    public string ABCreationInstructions
    {
        get
        {
            return (CurrentABCreationMode, CurrentABPointStep) switch
            {
                (ABCreationMode.DriveAB, ABPointStep.SettingPointA) => "Tap screen to set Point A at current position",
                (ABCreationMode.DriveAB, ABPointStep.SettingPointB) => "Drive to B, then tap screen to set Point B",
                (ABCreationMode.DrawAB, ABPointStep.SettingPointA) => "Tap on map to place Point A",
                (ABCreationMode.DrawAB, ABPointStep.SettingPointB) => "Tap on map to place Point B",
                (ABCreationMode.Curve, _) => "Drive along curve path, then tap to finish",
                _ => string.Empty
            };
        }
    }

    // Tracks Dialog data properties
    public ObservableCollection<Track> SavedTracks { get; } = new();

    private Track? _selectedTrack;
    public Track? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            var oldValue = _selectedTrack;
            if (this.RaiseAndSetIfChanged(ref _selectedTrack, value) != oldValue)
            {
                var oldA = oldValue?.Points.FirstOrDefault();
                var oldB = oldValue?.Points.LastOrDefault();
                var newA = value?.Points.FirstOrDefault();
                var newB = value?.Points.LastOrDefault();
                Console.WriteLine($"[SelectedTrack] Changed from A({oldA?.Easting:F1},{oldA?.Northing:F1}) B({oldB?.Easting:F1},{oldB?.Northing:F1})");
                Console.WriteLine($"[SelectedTrack]       to A({newA?.Easting:F1},{newA?.Northing:F1}) B({newB?.Easting:F1},{newB?.Northing:F1})");
                Console.WriteLine($"[SelectedTrack] Stack trace: {Environment.StackTrace}");
            }
        }
    }

    // Track management commands
    public ICommand? DeleteSelectedTrackCommand { get; private set; }
    public ICommand? SwapABPointsCommand { get; private set; }
    public ICommand? SelectTrackAsActiveCommand { get; private set; }

    // New Field Dialog properties (visibility managed by State.UI)
    private string _newFieldName = string.Empty;
    public string NewFieldName
    {
        get => _newFieldName;
        set => this.RaiseAndSetIfChanged(ref _newFieldName, value);
    }

    private double _newFieldLatitude;
    public double NewFieldLatitude
    {
        get => _newFieldLatitude;
        set => this.RaiseAndSetIfChanged(ref _newFieldLatitude, value);
    }

    private double _newFieldLongitude;
    public double NewFieldLongitude
    {
        get => _newFieldLongitude;
        set => this.RaiseAndSetIfChanged(ref _newFieldLongitude, value);
    }

    public ICommand? CancelNewFieldDialogCommand { get; private set; }
    public ICommand? ConfirmNewFieldDialogCommand { get; private set; }

    // From Existing Field Dialog properties (visibility managed by State.UI)
    private string _fromExistingFieldName = string.Empty;
    public string FromExistingFieldName
    {
        get => _fromExistingFieldName;
        set => this.RaiseAndSetIfChanged(ref _fromExistingFieldName, value);
    }

    private FieldSelectionItem? _fromExistingSelectedField;
    public FieldSelectionItem? FromExistingSelectedField
    {
        get => _fromExistingSelectedField;
        set
        {
            this.RaiseAndSetIfChanged(ref _fromExistingSelectedField, value);
            if (value != null)
            {
                // Auto-populate field name when selection changes
                FromExistingFieldName = value.Name;
            }
        }
    }

    // Copy options for FromExistingField
    private bool _copyFlags = true;
    public bool CopyFlags
    {
        get => _copyFlags;
        set => this.RaiseAndSetIfChanged(ref _copyFlags, value);
    }

    private bool _copyMapping = true;
    public bool CopyMapping
    {
        get => _copyMapping;
        set => this.RaiseAndSetIfChanged(ref _copyMapping, value);
    }

    private bool _copyHeadland = true;
    public bool CopyHeadland
    {
        get => _copyHeadland;
        set => this.RaiseAndSetIfChanged(ref _copyHeadland, value);
    }

    private bool _copyLines = true;
    public bool CopyLines
    {
        get => _copyLines;
        set => this.RaiseAndSetIfChanged(ref _copyLines, value);
    }

    public ICommand? CancelFromExistingFieldDialogCommand { get; private set; }
    public ICommand? ConfirmFromExistingFieldDialogCommand { get; private set; }
    public ICommand? AppendVehicleNameCommand { get; private set; }
    public ICommand? AppendDateCommand { get; private set; }
    public ICommand? AppendTimeCommand { get; private set; }
    public ICommand? BackspaceFieldNameCommand { get; private set; }
    public ICommand? ToggleCopyFlagsCommand { get; private set; }
    public ICommand? ToggleCopyMappingCommand { get; private set; }
    public ICommand? ToggleCopyHeadlandCommand { get; private set; }
    public ICommand? ToggleCopyLinesCommand { get; private set; }

    // KML Import Dialog properties (visibility managed by State.UI)
    public ObservableCollection<KmlFileItem> AvailableKmlFiles { get; } = new();

    private KmlFileItem? _selectedKmlFile;
    public KmlFileItem? SelectedKmlFile
    {
        get => _selectedKmlFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedKmlFile, value);
            if (value != null)
            {
                KmlImportFieldName = Path.GetFileNameWithoutExtension(value.Name);
                ParseKmlFile(value.FullPath);
            }
        }
    }

    private string _kmlImportFieldName = string.Empty;
    public string KmlImportFieldName
    {
        get => _kmlImportFieldName;
        set => this.RaiseAndSetIfChanged(ref _kmlImportFieldName, value);
    }

    private int _kmlBoundaryPointCount;
    public int KmlBoundaryPointCount
    {
        get => _kmlBoundaryPointCount;
        set => this.RaiseAndSetIfChanged(ref _kmlBoundaryPointCount, value);
    }

    private double _kmlCenterLatitude;
    public double KmlCenterLatitude
    {
        get => _kmlCenterLatitude;
        set => this.RaiseAndSetIfChanged(ref _kmlCenterLatitude, value);
    }

    private double _kmlCenterLongitude;
    public double KmlCenterLongitude
    {
        get => _kmlCenterLongitude;
        set => this.RaiseAndSetIfChanged(ref _kmlCenterLongitude, value);
    }

    private List<(double Latitude, double Longitude)> _kmlBoundaryPoints = new();

    public ICommand? CancelKmlImportDialogCommand { get; private set; }
    public ICommand? ConfirmKmlImportDialogCommand { get; private set; }
    public ICommand? KmlAppendDateCommand { get; private set; }
    public ICommand? KmlAppendTimeCommand { get; private set; }
    public ICommand? KmlBackspaceFieldNameCommand { get; private set; }

    // ISO-XML Import Dialog properties (visibility managed by State.UI)
    public ObservableCollection<IsoXmlFileItem> AvailableIsoXmlFiles { get; } = new();

    private IsoXmlFileItem? _selectedIsoXmlFile;
    public IsoXmlFileItem? SelectedIsoXmlFile
    {
        get => _selectedIsoXmlFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedIsoXmlFile, value);
            if (value != null)
            {
                IsoXmlImportFieldName = value.Name;
            }
        }
    }

    private string _isoXmlImportFieldName = string.Empty;
    public string IsoXmlImportFieldName
    {
        get => _isoXmlImportFieldName;
        set => this.RaiseAndSetIfChanged(ref _isoXmlImportFieldName, value);
    }

    public ICommand? CancelIsoXmlImportDialogCommand { get; private set; }
    public ICommand? ConfirmIsoXmlImportDialogCommand { get; private set; }
    public ICommand? IsoXmlAppendDateCommand { get; private set; }
    public ICommand? IsoXmlAppendTimeCommand { get; private set; }
    public ICommand? IsoXmlBackspaceFieldNameCommand { get; private set; }

    // Boundary Map Dialog properties (visibility managed by State.UI)
    private double _boundaryMapCenterLatitude;
    public double BoundaryMapCenterLatitude
    {
        get => _boundaryMapCenterLatitude;
        set => this.RaiseAndSetIfChanged(ref _boundaryMapCenterLatitude, value);
    }

    private double _boundaryMapCenterLongitude;
    public double BoundaryMapCenterLongitude
    {
        get => _boundaryMapCenterLongitude;
        set => this.RaiseAndSetIfChanged(ref _boundaryMapCenterLongitude, value);
    }

    private int _boundaryMapPointCount;
    public int BoundaryMapPointCount
    {
        get => _boundaryMapPointCount;
        set => this.RaiseAndSetIfChanged(ref _boundaryMapPointCount, value);
    }

    private string _boundaryMapCoordinateText = string.Empty;
    public string BoundaryMapCoordinateText
    {
        get => _boundaryMapCoordinateText;
        set => this.RaiseAndSetIfChanged(ref _boundaryMapCoordinateText, value);
    }

    private bool _boundaryMapIncludeBackground = true;
    public bool BoundaryMapIncludeBackground
    {
        get => _boundaryMapIncludeBackground;
        set => this.RaiseAndSetIfChanged(ref _boundaryMapIncludeBackground, value);
    }

    private bool _boundaryMapCanSave;
    public bool BoundaryMapCanSave
    {
        get => _boundaryMapCanSave;
        set => this.RaiseAndSetIfChanged(ref _boundaryMapCanSave, value);
    }

    // Result properties for boundary map dialog
    public List<(double Latitude, double Longitude)> BoundaryMapResultPoints { get; } = new();
    public string? BoundaryMapResultBackgroundPath { get; set; }
    public double BoundaryMapResultNwLat { get; set; }
    public double BoundaryMapResultNwLon { get; set; }
    public double BoundaryMapResultSeLat { get; set; }
    public double BoundaryMapResultSeLon { get; set; }

    public ICommand? ShowBoundaryMapDialogCommand { get; private set; }
    public ICommand? CancelBoundaryMapDialogCommand { get; private set; }
    public ICommand? ConfirmBoundaryMapDialogCommand { get; private set; }

    // Numeric Input Dialog properties (visibility managed by State.UI)
    private string _numericInputDialogTitle = string.Empty;
    public string NumericInputDialogTitle
    {
        get => _numericInputDialogTitle;
        set => this.RaiseAndSetIfChanged(ref _numericInputDialogTitle, value);
    }

    private decimal? _numericInputDialogValue;
    public decimal? NumericInputDialogValue
    {
        get => _numericInputDialogValue;
        set => this.RaiseAndSetIfChanged(ref _numericInputDialogValue, value);
    }

    private string _numericInputDialogDisplayText = string.Empty;
    public string NumericInputDialogDisplayText
    {
        get => _numericInputDialogDisplayText;
        set => this.RaiseAndSetIfChanged(ref _numericInputDialogDisplayText, value);
    }

    private bool _numericInputDialogIntegerOnly;
    public bool NumericInputDialogIntegerOnly
    {
        get => _numericInputDialogIntegerOnly;
        set => this.RaiseAndSetIfChanged(ref _numericInputDialogIntegerOnly, value);
    }

    private bool _numericInputDialogAllowNegative = true;
    public bool NumericInputDialogAllowNegative
    {
        get => _numericInputDialogAllowNegative;
        set => this.RaiseAndSetIfChanged(ref _numericInputDialogAllowNegative, value);
    }

    // Callback to run when numeric input is confirmed
    private Action<double>? _numericInputDialogCallback;

    public ICommand? CancelNumericInputDialogCommand { get; private set; }
    public ICommand? ConfirmNumericInputDialogCommand { get; private set; }

    // AgShare Settings Dialog properties (visibility managed by State.UI)
    private string _agShareSettingsServerUrl = "https://agshare.agopengps.com";
    public string AgShareSettingsServerUrl
    {
        get => _agShareSettingsServerUrl;
        set => this.RaiseAndSetIfChanged(ref _agShareSettingsServerUrl, value);
    }

    private string _agShareSettingsApiKey = string.Empty;
    public string AgShareSettingsApiKey
    {
        get => _agShareSettingsApiKey;
        set => this.RaiseAndSetIfChanged(ref _agShareSettingsApiKey, value);
    }

    private bool _agShareSettingsEnabled;
    public bool AgShareSettingsEnabled
    {
        get => _agShareSettingsEnabled;
        set => this.RaiseAndSetIfChanged(ref _agShareSettingsEnabled, value);
    }

    public ICommand? CancelAgShareSettingsDialogCommand { get; private set; }
    public ICommand? ConfirmAgShareSettingsDialogCommand { get; private set; }

    // AgShare Upload Dialog (visibility managed by State.UI)
    public ICommand? CancelAgShareUploadDialogCommand { get; private set; }

    // AgShare Download Dialog (visibility managed by State.UI)
    public ICommand? CancelAgShareDownloadDialogCommand { get; private set; }

    // Data I/O Dialog properties (visibility managed by State.UI)
    private bool _isDataIOKeyboardVisible;
    public bool IsDataIOKeyboardVisible
    {
        get => _isDataIOKeyboardVisible;
        set => this.RaiseAndSetIfChanged(ref _isDataIOKeyboardVisible, value);
    }

    private string _dataIOKeyboardText = "";
    public string DataIOKeyboardText
    {
        get => _dataIOKeyboardText;
        set
        {
            var oldValue = _dataIOKeyboardText;
            this.RaiseAndSetIfChanged(ref _dataIOKeyboardText, value);
            if (oldValue != value && !string.IsNullOrEmpty(_activeDataIOField))
            {
                // Update the appropriate field based on active field
                UpdateActiveDataIOField(value);
            }
        }
    }

    private string _activeDataIOField = "";
    public string ActiveDataIOField
    {
        get => _activeDataIOField;
        set => this.RaiseAndSetIfChanged(ref _activeDataIOField, value);
    }

    // Data I/O Commands
    public ICommand? ShowDataIODialogCommand { get; private set; }
    public ICommand? CloseDataIODialogCommand { get; private set; }
    public ICommand? ConnectToNtripCommand { get; private set; }
    public ICommand? DisconnectFromNtripCommand { get; private set; }
    public ICommand? SaveNtripSettingsCommand { get; private set; }
    public ICommand? SetActiveDataIOFieldCommand { get; private set; }

    private void UpdateActiveDataIOField(string value)
    {
        switch (_activeDataIOField)
        {
            case "CasterAddress":
                NtripCasterAddress = value;
                break;
            case "CasterPort":
                if (int.TryParse(value, out int port))
                    NtripCasterPort = port;
                break;
            case "MountPoint":
                NtripMountPoint = value;
                break;
            case "Username":
                NtripUsername = value;
                break;
            case "Password":
                NtripPassword = value;
                break;
        }
    }

    private void SetActiveDataIOField(string? fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            IsDataIOKeyboardVisible = false;
            ActiveDataIOField = "";
            return;
        }

        ActiveDataIOField = fieldName;

        // Set current value for the keyboard
        DataIOKeyboardText = fieldName switch
        {
            "CasterAddress" => NtripCasterAddress,
            "CasterPort" => NtripCasterPort.ToString(),
            "MountPoint" => NtripMountPoint,
            "Username" => NtripUsername,
            "Password" => NtripPassword,
            _ => ""
        };

        IsDataIOKeyboardVisible = true;
    }

    private void CloseDataIODialog()
    {
        IsDataIOKeyboardVisible = false;
        State.UI.CloseDialog();
    }

    private void SaveNtripSettings()
    {
        var settings = _settingsService.Settings;
        settings.NtripCasterIp = NtripCasterAddress;
        settings.NtripCasterPort = NtripCasterPort;
        settings.NtripMountPoint = NtripMountPoint;
        settings.NtripUsername = NtripUsername;
        settings.NtripPassword = NtripPassword;
        _settingsService.Save();

        StatusMessage = "NTRIP settings saved";
        Console.WriteLine("[DataIO] NTRIP settings saved");
    }

    // iOS Modal Sheet Visibility Properties
    private bool _isFileMenuVisible;
    public bool IsFileMenuVisible
    {
        get => _isFileMenuVisible;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isFileMenuVisible, value) && value)
            {
                // Close other sheets when opening this one
                IsFieldToolsVisible = false;
                IsSettingsVisible = false;
            }
        }
    }

    private bool _isFieldToolsVisible;
    public bool IsFieldToolsVisible
    {
        get => _isFieldToolsVisible;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isFieldToolsVisible, value) && value)
            {
                // Close other sheets when opening this one
                IsFileMenuVisible = false;
                IsSettingsVisible = false;
            }
        }
    }

    private bool _isSettingsVisible;
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isSettingsVisible, value) && value)
            {
                // Close other sheets when opening this one
                IsFileMenuVisible = false;
                IsFieldToolsVisible = false;
            }
        }
    }

    private bool _isBoundaryPanelVisible;
    public bool IsBoundaryPanelVisible
    {
        get => _isBoundaryPanelVisible;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isBoundaryPanelVisible, value) && value)
            {
                RefreshBoundaryList();
            }
        }
    }

    // Boundary list for the boundary panel
    public ObservableCollection<BoundaryListItem> BoundaryItems { get; } = new();

    private int _selectedBoundaryIndex = -1;
    public int SelectedBoundaryIndex
    {
        get => _selectedBoundaryIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedBoundaryIndex, value);
    }

    private bool _isBoundaryPlayerPanelVisible;
    public bool IsBoundaryPlayerPanelVisible
    {
        get => _isBoundaryPlayerPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isBoundaryPlayerPanelVisible, value);
    }

    private bool _isBoundaryRecording;
    public bool IsBoundaryRecording
    {
        get => _isBoundaryRecording;
        set => this.RaiseAndSetIfChanged(ref _isBoundaryRecording, value);
    }

    private int _boundaryPointCount;
    public int BoundaryPointCount
    {
        get => _boundaryPointCount;
        set => this.RaiseAndSetIfChanged(ref _boundaryPointCount, value);
    }

    private double _boundaryAreaHectares;
    public double BoundaryAreaHectares
    {
        get => _boundaryAreaHectares;
        set => this.RaiseAndSetIfChanged(ref _boundaryAreaHectares, value);
    }

    // Boundary Player settings
    private bool _isBoundarySectionControlOn;
    public bool IsBoundarySectionControlOn
    {
        get => _isBoundarySectionControlOn;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isBoundarySectionControlOn, value) != value) return;
            StatusMessage = value ? "Boundary records when section is on" : "Boundary section control off";
        }
    }

    private bool _isDrawRightSide = true;
    public bool IsDrawRightSide
    {
        get => _isDrawRightSide;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isDrawRightSide, value) != value) return;
            StatusMessage = value ? "Boundary on right side" : "Boundary on left side";
            UpdateBoundaryOffsetIndicator();
        }
    }

    private bool _isDrawAtPivot;
    public bool IsDrawAtPivot
    {
        get => _isDrawAtPivot;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isDrawAtPivot, value) != value) return;
            StatusMessage = value ? "Recording at pivot point" : "Recording at tool";
        }
    }

    private double _boundaryOffset;
    public double BoundaryOffset
    {
        get => _boundaryOffset;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _boundaryOffset, value) != value) return;
            UpdateBoundaryOffsetIndicator();
        }
    }

    private void UpdateBoundaryOffsetIndicator()
    {
        // Apply direction: right side = positive offset, left side = negative offset
        double signedOffsetMeters = _boundaryOffset / 100.0;
        if (!_isDrawRightSide)
        {
            signedOffsetMeters = -signedOffsetMeters;
        }
        _mapService.SetBoundaryOffsetIndicator(true, signedOffsetMeters);
    }

    /// <summary>
    /// Calculate offset position perpendicular to heading.
    /// Returns (easting, northing) with offset applied.
    /// </summary>
    private (double easting, double northing) CalculateOffsetPosition(double easting, double northing, double headingRadians)
    {
        if (_boundaryOffset == 0)
            return (easting, northing);

        // Offset in meters (input is cm)
        double offsetMeters = _boundaryOffset / 100.0;

        // If drawing on left side, negate the offset
        if (!_isDrawRightSide)
            offsetMeters = -offsetMeters;

        // Calculate perpendicular offset (90 degrees to the right of heading)
        double perpAngle = headingRadians + Math.PI / 2.0;

        double offsetEasting = easting + offsetMeters * Math.Sin(perpAngle);
        double offsetNorthing = northing + offsetMeters * Math.Cos(perpAngle);

        return (offsetEasting, offsetNorthing);
    }

    // Configuration Dialog properties
    // Configuration Dialog (visibility managed by State.UI)
    private ConfigurationViewModel? _configurationViewModel;
    public ConfigurationViewModel? ConfigurationViewModel
    {
        get => _configurationViewModel;
        set => this.RaiseAndSetIfChanged(ref _configurationViewModel, value);
    }

    public ICommand? ShowConfigurationDialogCommand { get; private set; }
    public ICommand? CancelConfigurationDialogCommand { get; private set; }
    public ICommand? ShowLoadProfileDialogCommand { get; private set; }
    public ICommand? ShowNewProfileDialogCommand { get; private set; }
    public ICommand? LoadSelectedProfileCommand { get; private set; }
    public ICommand? CancelProfileSelectionCommand { get; private set; }

    // Profile selection dialog
    private bool _isProfileSelectionVisible;
    public bool IsProfileSelectionVisible
    {
        get => _isProfileSelectionVisible;
        set => this.RaiseAndSetIfChanged(ref _isProfileSelectionVisible, value);
    }

    private System.Collections.ObjectModel.ObservableCollection<string> _availableProfiles = new();
    public System.Collections.ObjectModel.ObservableCollection<string> AvailableProfiles
    {
        get => _availableProfiles;
        set => this.RaiseAndSetIfChanged(ref _availableProfiles, value);
    }

    private string? _selectedProfile;
    public string? SelectedProfile
    {
        get => _selectedProfile;
        set => this.RaiseAndSetIfChanged(ref _selectedProfile, value);
    }

    public string CurrentProfileName => _configurationService.Store.ActiveProfileName;

    // Headland Builder properties (visibility managed by State.UI)
    private bool _isHeadlandOn;
    public bool IsHeadlandOn
    {
        get => _isHeadlandOn;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isHeadlandOn, value))
            {
                StatusMessage = value ? "Headland ON" : "Headland OFF";
                _mapService.SetHeadlandVisible(value);
            }
        }
    }

    private bool _isSectionControlInHeadland;
    /// <summary>
    /// When true, section control remains active in headland area
    /// </summary>
    public bool IsSectionControlInHeadland
    {
        get => _isSectionControlInHeadland;
        set => this.RaiseAndSetIfChanged(ref _isSectionControlInHeadland, value);
    }

    private int _uTurnSkipRows;
    /// <summary>
    /// Number of rows to skip during U-turn (0-9)
    /// </summary>
    public int UTurnSkipRows
    {
        get => _uTurnSkipRows;
        set => this.RaiseAndSetIfChanged(ref _uTurnSkipRows, Math.Max(0, Math.Min(9, value)));
    }

    private bool _isUTurnSkipRowsEnabled;
    /// <summary>
    /// When true, U-turn skip rows feature is enabled
    /// </summary>
    public bool IsUTurnSkipRowsEnabled
    {
        get => _isUTurnSkipRowsEnabled;
        set => this.RaiseAndSetIfChanged(ref _isUTurnSkipRowsEnabled, value);
    }

    private double _headlandDistance = 12.0;
    public double HeadlandDistance
    {
        get => _headlandDistance;
        set => this.RaiseAndSetIfChanged(ref _headlandDistance, Math.Max(1.0, Math.Min(100.0, value)));
    }

    private int _headlandPasses = 1;
    public int HeadlandPasses
    {
        get => _headlandPasses;
        set => this.RaiseAndSetIfChanged(ref _headlandPasses, Math.Max(1, Math.Min(5, value)));
    }

    private List<Models.Base.Vec3>? _currentHeadlandLine;
    public List<Models.Base.Vec3>? CurrentHeadlandLine
    {
        get => _currentHeadlandLine;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentHeadlandLine, value);
            _mapService.SetHeadlandLine(value);
            SaveHeadlandToFile(value);
        }
    }

    private List<Models.Base.Vec2>? _headlandPreviewLine;
    public List<Models.Base.Vec2>? HeadlandPreviewLine
    {
        get => _headlandPreviewLine;
        set
        {
            this.RaiseAndSetIfChanged(ref _headlandPreviewLine, value);
            _mapService.SetHeadlandPreview(value);
        }
    }

    private bool _hasHeadland;
    public bool HasHeadland
    {
        get => _hasHeadland;
        set => this.RaiseAndSetIfChanged(ref _hasHeadland, value);
    }

    // Bottom strip state properties (matching AgOpenGPS conditional button visibility)
    private bool _hasActiveTrack;
    /// <summary>
    /// True when an AB line or track is active for guidance (equivalent to AgOpenGPS trk.idx > -1)
    /// </summary>
    public bool HasActiveTrack
    {
        get => _hasActiveTrack;
        set => this.RaiseAndSetIfChanged(ref _hasActiveTrack, value);
    }

    private bool _hasBoundary;
    /// <summary>
    /// True when a field boundary exists (equivalent to AgOpenGPS isBnd)
    /// </summary>
    public bool HasBoundary
    {
        get => _hasBoundary;
        set => this.RaiseAndSetIfChanged(ref _hasBoundary, value);
    }

    private bool _isNudgeEnabled;
    /// <summary>
    /// True when AB line nudging is enabled (controls visibility of snap/adjust buttons)
    /// </summary>
    public bool IsNudgeEnabled
    {
        get => _isNudgeEnabled;
        set => this.RaiseAndSetIfChanged(ref _isNudgeEnabled, value);
    }

    /// <summary>
    /// Gets the current field's boundary for use in the headland editor.
    /// </summary>
    private Boundary? _currentBoundary;
    public Boundary? CurrentBoundary
    {
        get => _currentBoundary;
        private set => this.RaiseAndSetIfChanged(ref _currentBoundary, value);
    }

    // Headland Dialog properties (visibility managed by State.UI)
    private bool _isHeadlandCurveMode = true;
    public bool IsHeadlandCurveMode
    {
        get => _isHeadlandCurveMode;
        set
        {
            var oldValue = _isHeadlandCurveMode;
            if (this.RaiseAndSetIfChanged(ref _isHeadlandCurveMode, value))
            {
                this.RaisePropertyChanged(nameof(IsHeadlandLineMode));
                // Update preview when track type changes
                if (State.UI.IsHeadlandDialogVisible || State.UI.IsHeadlandBuilderDialogVisible)
                {
                    UpdateHeadlandPreview();
                }
            }
        }
    }

    public bool IsHeadlandLineMode
    {
        get => !_isHeadlandCurveMode;
        set
        {
            if (value != !_isHeadlandCurveMode)
            {
                IsHeadlandCurveMode = !value;
                // No need to call UpdateHeadlandPreview here - IsHeadlandCurveMode setter handles it
            }
        }
    }

    private bool _isHeadlandZoomMode;
    public bool IsHeadlandZoomMode
    {
        get => _isHeadlandZoomMode;
        set => this.RaiseAndSetIfChanged(ref _isHeadlandZoomMode, value);
    }

    private bool _isHeadlandSectionControlled = true;
    public bool IsHeadlandSectionControlled
    {
        get => _isHeadlandSectionControlled;
        set => this.RaiseAndSetIfChanged(ref _isHeadlandSectionControlled, value);
    }

    private int _headlandToolWidthMultiplier = 1;
    public int HeadlandToolWidthMultiplier
    {
        get => _headlandToolWidthMultiplier;
        set
        {
            this.RaiseAndSetIfChanged(ref _headlandToolWidthMultiplier, value);
            this.RaisePropertyChanged(nameof(HeadlandCalculatedWidth));
            // Update distance based on tool width multiplier
            if (value > 0)
            {
                HeadlandDistance = Vehicle.TrackWidth * value;
            }
        }
    }

    public double HeadlandCalculatedWidth => Vehicle.TrackWidth * _headlandToolWidthMultiplier;

    // Headland point selection (for clipping headland via boundary points)
    // Each point is stored as (segmentIndex, t parameter 0-1, world position)
    private int _headlandPoint1Index = -1;
    public int HeadlandPoint1Index
    {
        get => _headlandPoint1Index;
        set
        {
            this.RaiseAndSetIfChanged(ref _headlandPoint1Index, value);
            this.RaisePropertyChanged(nameof(HeadlandPointsSelected));
        }
    }
    private double _headlandPoint1T = 0;  // Parameter along segment (0 = start vertex, 1 = end vertex)
    private Models.Base.Vec2? _headlandPoint1Position;  // Actual world position

    private int _headlandPoint2Index = -1;
    public int HeadlandPoint2Index
    {
        get => _headlandPoint2Index;
        set
        {
            this.RaiseAndSetIfChanged(ref _headlandPoint2Index, value);
            this.RaisePropertyChanged(nameof(HeadlandPointsSelected));
        }
    }
    private double _headlandPoint2T = 0;  // Parameter along segment (0 = start vertex, 1 = end vertex)
    private Models.Base.Vec2? _headlandPoint2Position;  // Actual world position

    // For curve mode: store headland segment index/t (separate from boundary segment)
    private int _headlandCurvePoint1Index = -1;
    private double _headlandCurvePoint1T = 0;
    private int _headlandCurvePoint2Index = -1;
    private double _headlandCurvePoint2T = 0;

    // Cached clip path (to avoid recalculating on every access)
    private List<Models.Base.Vec2>? _cachedClipPath;
    private bool _clipPathDirty = true;

    // Visual markers for selected points (world coordinates)
    private List<Models.Base.Vec2>? _headlandSelectedMarkers;
    public List<Models.Base.Vec2>? HeadlandSelectedMarkers
    {
        get => _headlandSelectedMarkers;
        set => this.RaiseAndSetIfChanged(ref _headlandSelectedMarkers, value);
    }

    public bool HeadlandPointsSelected => _headlandPoint1Index >= 0 && _headlandPoint2Index >= 0;

    // Clip line for headland clipping (line between two selected points) - used in LINE mode
    public (Models.Base.Vec2 Start, Models.Base.Vec2 End)? HeadlandClipLine
    {
        get
        {
            // Only return straight clip line when NOT in curve mode
            if (!IsHeadlandCurveMode && _headlandPoint1Position.HasValue && _headlandPoint2Position.HasValue)
            {
                return (_headlandPoint1Position.Value, _headlandPoint2Position.Value);
            }
            return null;
        }
    }

    // Clip path for headland clipping (follows the headland curve) - used in CURVE MODE
    // This shows the section that will be REMOVED (the shorter path)
    public List<Models.Base.Vec2>? HeadlandClipPath
    {
        get
        {
            // Only return clip path when in curve mode and both points selected on headland
            if (!IsHeadlandCurveMode || _headlandCurvePoint1Index < 0 || _headlandCurvePoint2Index < 0)
            {
                _cachedClipPath = null;
                return null;
            }

            // Return cached path if available and not dirty
            if (!_clipPathDirty && _cachedClipPath != null)
                return _cachedClipPath;

            var headland = CurrentHeadlandLine ?? ConvertPreviewToVec3(HeadlandPreviewLine);
            if (headland == null || headland.Count < 3)
            {
                _cachedClipPath = null;
                return null;
            }

            // Build both paths along the headland between the two selected points
            var forwardPath = BuildCurveModePath(headland, _headlandCurvePoint1Index, _headlandCurvePoint1T,
                                                           _headlandCurvePoint2Index, _headlandCurvePoint2T, true);
            var backwardPath = BuildCurveModePath(headland, _headlandCurvePoint1Index, _headlandCurvePoint1T,
                                                            _headlandCurvePoint2Index, _headlandCurvePoint2T, false);

            // Return the LONGER path - this is what will be REMOVED (shown in red)
            // Curve mode keeps the shorter section (between the two points), so the red line shows what's being cut away
            _cachedClipPath = forwardPath.Count > backwardPath.Count ? forwardPath : backwardPath;
            _clipPathDirty = false;
            return _cachedClipPath;
        }
    }

    private void InvalidateClipPathCache()
    {
        _clipPathDirty = true;
        _cachedClipPath = null;
    }

    // Helper to build clip path for curve mode visualization
    private List<Models.Base.Vec2> BuildCurveModePath(List<Models.Base.Vec3> headland, int idx1, double t1, int idx2, double t2, bool forward)
    {
        var path = new List<Models.Base.Vec2>();
        int n = headland.Count;

        // Start position (interpolated on headland segment)
        var start = InterpolateHeadlandPoint(headland, idx1, t1);
        path.Add(start);

        if (forward)
        {
            // Go from idx1 to idx2 in forward (increasing index) direction
            // Start from the next vertex after idx1's segment end
            int current = (idx1 + 1) % n;
            int target = (idx2 + 1) % n;
            int iterations = 0;

            while (current != target && iterations < n)
            {
                path.Add(new Models.Base.Vec2(headland[current].Easting, headland[current].Northing));
                current = (current + 1) % n;
                iterations++;
            }
        }
        else
        {
            // Go from idx1 to idx2 in backward (decreasing index) direction
            // Start from idx1's vertex (segment start)
            int current = idx1;
            int target = idx2;
            int iterations = 0;

            while (current != target && iterations < n)
            {
                path.Add(new Models.Base.Vec2(headland[current].Easting, headland[current].Northing));
                current = (current - 1 + n) % n;
                iterations++;
            }
        }

        // End position (interpolated on headland segment)
        var end = InterpolateHeadlandPoint(headland, idx2, t2);
        path.Add(end);

        return path;
    }

    // Helper to interpolate a point on a headland segment
    private Models.Base.Vec2 InterpolateHeadlandPoint(List<Models.Base.Vec3> headland, int segmentIndex, double t)
    {
        int n = headland.Count;
        var p1 = headland[segmentIndex];
        var p2 = headland[(segmentIndex + 1) % n];
        return new Models.Base.Vec2(
            p1.Easting + t * (p2.Easting - p1.Easting),
            p1.Northing + t * (p2.Northing - p1.Northing));
    }

    // Field management properties
    private bool _isFieldOpen;
    public bool IsFieldOpen
    {
        get => _isFieldOpen;
        set => this.RaiseAndSetIfChanged(ref _isFieldOpen, value);
    }

    private string _currentFieldName = string.Empty;
    public string CurrentFieldName
    {
        get => _currentFieldName;
        set => this.RaiseAndSetIfChanged(ref _currentFieldName, value);
    }

    // Simulator properties
    private bool _isSimulatorEnabled;
    public bool IsSimulatorEnabled
    {
        get => _isSimulatorEnabled;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isSimulatorEnabled, value))
            {
                // Update centralized state
                State.Simulator.IsEnabled = value;

                // Save to settings
                _settingsService.Settings.SimulatorEnabled = value;
                _settingsService.Save();

                // Start or stop simulator timer based on enabled state
                if (value)
                {
                    // Initialize simulator with saved coordinates
                    var settings = _settingsService.Settings;
                    _simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(
                        settings.SimulatorLatitude,
                        settings.SimulatorLongitude));

                    State.Simulator.IsRunning = true;
                    _simulatorTimer.Start();
                    StatusMessage = $"Simulator ON at {settings.SimulatorLatitude:F8}, {settings.SimulatorLongitude:F8}";
                }
                else
                {
                    State.Simulator.IsRunning = false;
                    _simulatorTimer.Stop();
                    StatusMessage = "Simulator OFF";
                }
            }
        }
    }

    private double _simulatorSteerAngle;
    public double SimulatorSteerAngle
    {
        get => _simulatorSteerAngle;
        set
        {
            this.RaiseAndSetIfChanged(ref _simulatorSteerAngle, value);
            State.Simulator.SteerAngle = value;
            this.RaisePropertyChanged(nameof(SimulatorSteerAngleDisplay)); // Notify display property
            if (_isSimulatorEnabled)
            {
                _simulatorService.SteerAngle = value;
            }
        }
    }

    public string SimulatorSteerAngleDisplay => $"Steer Angle: {_simulatorSteerAngle:F1}°";

    private double _simulatorSpeedKph;
    /// <summary>
    /// Simulator speed in kph. Range: -10 to +25 kph.
    /// Converts to/from stepDistance using formula: speedKph = stepDistance * 40
    /// </summary>
    public double SimulatorSpeedKph
    {
        get => _simulatorSpeedKph;
        set
        {
            // Clamp to valid range
            value = Math.Max(-10, Math.Min(25, value));
            this.RaiseAndSetIfChanged(ref _simulatorSpeedKph, value);
            State.Simulator.Speed = value;
            State.Simulator.TargetSpeed = value;
            this.RaisePropertyChanged(nameof(SimulatorSpeedDisplay));
            if (_isSimulatorEnabled)
            {
                // Convert kph to stepDistance: stepDistance = speedKph / 40
                _simulatorService.StepDistance = value / 40.0;
                // Disable acceleration when manually setting speed
                _simulatorService.IsAcceleratingForward = false;
                _simulatorService.IsAcceleratingBackward = false;
            }
        }
    }

    public string SimulatorSpeedDisplay => $"Speed: {_simulatorSpeedKph:F1} kph";

    /// <summary>
    /// Set new starting coordinates for the simulator
    /// </summary>
    public void SetSimulatorCoordinates(double latitude, double longitude)
    {
        // Reinitialize simulator with new coordinates
        _simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(latitude, longitude));
        _simulatorService.StepDistance = 0;

        // Clear LocalPlane so it will be recreated with new origin on next GPS data update
        _simulatorLocalPlane = null;

        // Reset steering
        SimulatorSteerAngle = 0;

        // Save coordinates to settings so they persist
        _settingsService.Settings.SimulatorLatitude = latitude;
        _settingsService.Settings.SimulatorLongitude = longitude;
        var saved = _settingsService.Save();

        // Also update the Latitude/Longitude properties directly so that
        // the map boundary dialog uses the correct coordinates even if
        // the simulator timer hasn't ticked yet
        Latitude = latitude;
        Longitude = longitude;

        StatusMessage = saved
            ? $"Simulator reset to {latitude:F8}, {longitude:F8}"
            : $"Reset to {latitude:F8}, {longitude:F8} (save failed: {_settingsService.GetSettingsFilePath()})";
    }

    /// <summary>
    /// Get current simulator position
    /// </summary>
    public AgValoniaGPS.Models.Wgs84 GetSimulatorPosition()
    {
        return _simulatorService.CurrentPosition;
    }

    // Navigation settings properties (forwarded from service)
    public bool IsGridOn
    {
        get => _displaySettings.IsGridOn;
        set
        {
            _displaySettings.IsGridOn = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsDayMode
    {
        get => _displaySettings.IsDayMode;
        set
        {
            _displaySettings.IsDayMode = value;
            this.RaisePropertyChanged();
        }
    }

    public double CameraPitch
    {
        get => _displaySettings.CameraPitch;
        set
        {
            _displaySettings.CameraPitch = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Is2DMode));
        }
    }

    public bool Is2DMode
    {
        get => _displaySettings.Is2DMode;
        set
        {
            _displaySettings.Is2DMode = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsNorthUp
    {
        get => _displaySettings.IsNorthUp;
        set
        {
            _displaySettings.IsNorthUp = value;
            this.RaisePropertyChanged();
        }
    }

    public int Brightness
    {
        get => _displaySettings.Brightness;
        set
        {
            _displaySettings.Brightness = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(BrightnessDisplay));
        }
    }

    public string BrightnessDisplay => _displaySettings.IsBrightnessSupported
        ? $"{_displaySettings.Brightness}%"
        : "??";

    // Commands
    public ICommand? ToggleViewSettingsPanelCommand { get; private set; }
    public ICommand? ToggleFileMenuPanelCommand { get; private set; }
    public ICommand? ToggleToolsPanelCommand { get; private set; }
    public ICommand? ToggleConfigurationPanelCommand { get; private set; }
    public ICommand? ToggleJobMenuPanelCommand { get; private set; }
    public ICommand? ToggleFieldToolsPanelCommand { get; private set; }
    public ICommand? ToggleGridCommand { get; private set; }
    public ICommand? ToggleDayNightCommand { get; private set; }
    public ICommand? Toggle2D3DCommand { get; private set; }
    public ICommand? ToggleNorthUpCommand { get; private set; }
    public ICommand? IncreaseCameraPitchCommand { get; private set; }
    public ICommand? DecreaseCameraPitchCommand { get; private set; }
    public ICommand? IncreaseBrightnessCommand { get; private set; }
    public ICommand? DecreaseBrightnessCommand { get; private set; }

    // iOS Sheet Toggle Commands
    public ICommand? ToggleFileMenuCommand { get; private set; }
    public ICommand? ToggleFieldToolsCommand { get; private set; }
    public ICommand? ToggleSettingsCommand { get; private set; }

    // Simulator Commands
    public ICommand? ToggleSimulatorPanelCommand { get; private set; }
    public ICommand? ResetSimulatorCommand { get; private set; }
    public ICommand? ResetSteerAngleCommand { get; private set; }
    public ICommand? SimulatorForwardCommand { get; private set; }
    public ICommand? SimulatorStopCommand { get; private set; }
    public ICommand? SimulatorReverseCommand { get; private set; }
    public ICommand? SimulatorReverseDirectionCommand { get; private set; }
    public ICommand? SimulatorSteerLeftCommand { get; private set; }
    public ICommand? SimulatorSteerRightCommand { get; private set; }

    // Dialog Commands
    public ICommand? ShowSimCoordsDialogCommand { get; private set; }
    public ICommand? CancelSimCoordsDialogCommand { get; private set; }
    public ICommand? ConfirmSimCoordsDialogCommand { get; private set; }
    public ICommand? ShowFieldSelectionDialogCommand { get; private set; }
    public ICommand? CancelFieldSelectionDialogCommand { get; private set; }
    public ICommand? ConfirmFieldSelectionDialogCommand { get; private set; }
    public ICommand? DeleteSelectedFieldCommand { get; private set; }
    public ICommand? SortFieldsCommand { get; private set; }
    public ICommand? ShowNewFieldDialogCommand { get; private set; }
    public ICommand? ShowFromExistingFieldDialogCommand { get; private set; }
    public ICommand? ShowIsoXmlImportDialogCommand { get; private set; }
    public ICommand? ShowKmlImportDialogCommand { get; private set; }
    public ICommand? ShowAgShareDownloadDialogCommand { get; private set; }
    public ICommand? ShowAgShareUploadDialogCommand { get; private set; }
    public ICommand? ShowAgShareSettingsDialogCommand { get; private set; }
    public ICommand? ShowBoundaryDialogCommand { get; private set; }

    // Field Commands
    public ICommand? CloseFieldCommand { get; private set; }
    public ICommand? DriveInCommand { get; private set; }
    public ICommand? ResumeFieldCommand { get; private set; }

    // Map Commands
    public ICommand? Toggle3DModeCommand { get; private set; }
    public ICommand? ZoomInCommand { get; private set; }
    public ICommand? ZoomOutCommand { get; private set; }

    // Events for views to wire up to map controls
    public event Action? ZoomInRequested;
    public event Action? ZoomOutRequested;

    // Boundary Recording Commands
    public ICommand? ToggleBoundaryPanelCommand { get; private set; }
    public ICommand? StartBoundaryRecordingCommand { get; private set; }
    public ICommand? PauseBoundaryRecordingCommand { get; private set; }
    public ICommand? StopBoundaryRecordingCommand { get; private set; }
    public ICommand? UndoBoundaryPointCommand { get; private set; }
    public ICommand? ClearBoundaryCommand { get; private set; }
    public ICommand? AddBoundaryPointCommand { get; private set; }
    public ICommand? DeleteBoundaryCommand { get; private set; }
    public ICommand? ImportKmlBoundaryCommand { get; private set; }
    public ICommand? DrawMapBoundaryCommand { get; private set; }
    public ICommand? DrawMapBoundaryDesktopCommand { get; private set; }
    public ICommand? BuildFromTracksCommand { get; private set; }
    public ICommand? DriveAroundFieldCommand { get; private set; }
    public ICommand? ToggleRecordingCommand { get; private set; }
    public ICommand? ToggleBoundaryLeftRightCommand { get; private set; }
    public ICommand? ToggleBoundaryAntennaToolCommand { get; private set; }
    public ICommand? ShowBoundaryOffsetDialogCommand { get; private set; }

    // Headland commands
    public ICommand? ShowHeadlandBuilderCommand { get; private set; }
    public ICommand? ToggleHeadlandCommand { get; private set; }
    public ICommand? ToggleSectionInHeadlandCommand { get; private set; }
    public ICommand? ResetToolHeadingCommand { get; private set; }
    public ICommand? BuildHeadlandCommand { get; private set; }
    public ICommand? ClearHeadlandCommand { get; private set; }
    public ICommand? CloseHeadlandBuilderCommand { get; private set; }
    public ICommand? SetHeadlandToToolWidthCommand { get; private set; }
    public ICommand? PreviewHeadlandCommand { get; private set; }
    public ICommand? IncrementHeadlandDistanceCommand { get; private set; }
    public ICommand? DecrementHeadlandDistanceCommand { get; private set; }
    public ICommand? IncrementHeadlandPassesCommand { get; private set; }
    public ICommand? DecrementHeadlandPassesCommand { get; private set; }

    // Headland Dialog (FormHeadLine) commands
    public ICommand? ShowHeadlandDialogCommand { get; private set; }
    public ICommand? CloseHeadlandDialogCommand { get; private set; }
    public ICommand? ExtendHeadlandACommand { get; private set; }
    public ICommand? ExtendHeadlandBCommand { get; private set; }
    public ICommand? ShrinkHeadlandACommand { get; private set; }
    public ICommand? ShrinkHeadlandBCommand { get; private set; }
    public ICommand? ResetHeadlandCommand { get; private set; }
    public ICommand? ClipHeadlandLineCommand { get; private set; }
    public ICommand? UndoHeadlandCommand { get; private set; }
    public ICommand? TurnOffHeadlandCommand { get; private set; }

    // AB Line Guidance Commands - Bottom Bar (always visible)
    public ICommand? SnapLeftCommand { get; private set; }
    public ICommand? SnapRightCommand { get; private set; }
    public ICommand? StopGuidanceCommand { get; private set; }
    public ICommand? UTurnCommand { get; private set; }

    // AB Line Guidance Commands - Flyout Menu
    public ICommand? ShowTracksDialogCommand { get; private set; }
    public ICommand? ShowQuickABSelectorCommand { get; private set; }
    public ICommand? ShowDrawABDialogCommand { get; private set; }
    public ICommand? CloseTracksDialogCommand { get; private set; }
    public ICommand? CloseQuickABSelectorCommand { get; private set; }
    public ICommand? CloseDrawABDialogCommand { get; private set; }
    public ICommand? StartNewABLineCommand { get; private set; }
    public ICommand? StartNewABCurveCommand { get; private set; }
    public ICommand? StartAPlusLineCommand { get; private set; }
    public ICommand? StartDriveABCommand { get; private set; }
    public ICommand? StartCurveRecordingCommand { get; private set; }
    public ICommand? CycleABLinesCommand { get; private set; }
    public ICommand? SmoothABLineCommand { get; private set; }
    public ICommand? NudgeLeftCommand { get; private set; }
    public ICommand? NudgeRightCommand { get; private set; }
    public ICommand? FineNudgeLeftCommand { get; private set; }
    public ICommand? FineNudgeRightCommand { get; private set; }
    public ICommand? StartDrawABModeCommand { get; private set; }
    public ICommand? SetABPointCommand { get; private set; }
    public ICommand? CancelABCreationCommand { get; private set; }

    // Bottom Strip Commands (matching AgOpenGPS panelBottom)
    public ICommand? ChangeMappingColorCommand { get; private set; }
    public ICommand? SnapToPivotCommand { get; private set; }
    public ICommand? ToggleYouSkipCommand { get; private set; }
    public ICommand? ToggleUTurnSkipRowsCommand { get; private set; }
    public ICommand? CycleUTurnSkipRowsCommand { get; private set; }

    // Flags Commands
    public ICommand? PlaceRedFlagCommand { get; private set; }
    public ICommand? PlaceGreenFlagCommand { get; private set; }
    public ICommand? PlaceYellowFlagCommand { get; private set; }
    public ICommand? DeleteAllFlagsCommand { get; private set; }

    // Right Navigation Panel Commands
    public ICommand? ToggleContourModeCommand { get; private set; }
    public ICommand? ToggleManualModeCommand { get; private set; }
    public ICommand? ToggleSectionMasterCommand { get; private set; }
    public ICommand? ToggleYouTurnCommand { get; private set; }
    public ICommand? ToggleAutoSteerCommand { get; private set; }

    private void InitializeCommands()
    {
        // Use simple RelayCommand to avoid ReactiveCommand threading issues
        // Use property setters instead of service methods to ensure PropertyChanged fires
        ToggleViewSettingsPanelCommand = new RelayCommand(() =>
        {
            IsViewSettingsPanelVisible = !IsViewSettingsPanelVisible;
        });

        ToggleFileMenuPanelCommand = new RelayCommand(() =>
        {
            IsFileMenuPanelVisible = !IsFileMenuPanelVisible;
        });

        ToggleToolsPanelCommand = new RelayCommand(() =>
        {
            IsToolsPanelVisible = !IsToolsPanelVisible;
        });

        ToggleConfigurationPanelCommand = new RelayCommand(() =>
        {
            IsConfigurationPanelVisible = !IsConfigurationPanelVisible;
        });

        ToggleJobMenuPanelCommand = new RelayCommand(() =>
        {
            IsJobMenuPanelVisible = !IsJobMenuPanelVisible;
        });

        ToggleFieldToolsPanelCommand = new RelayCommand(() =>
        {
            IsFieldToolsPanelVisible = !IsFieldToolsPanelVisible;
        });

        ToggleGridCommand = new RelayCommand(() =>
        {
            IsGridOn = !IsGridOn;
        });

        ToggleDayNightCommand = new RelayCommand(() =>
        {
            IsDayMode = !IsDayMode;
        });

        Toggle2D3DCommand = new RelayCommand(() =>
        {
            Is2DMode = !Is2DMode;
        });

        ToggleNorthUpCommand = new RelayCommand(() =>
        {
            IsNorthUp = !IsNorthUp;
        });

        IncreaseCameraPitchCommand = new RelayCommand(() =>
        {
            CameraPitch += 5.0;
        });

        DecreaseCameraPitchCommand = new RelayCommand(() =>
        {
            CameraPitch -= 5.0;
        });

        IncreaseBrightnessCommand = new RelayCommand(() =>
        {
            Brightness += 5; // Match the step from DisplaySettingsService
        });

        DecreaseBrightnessCommand = new RelayCommand(() =>
        {
            Brightness -= 5;
        });

        // iOS Sheet toggle commands
        ToggleFileMenuCommand = new RelayCommand(() =>
        {
            IsFileMenuVisible = !IsFileMenuVisible;
        });

        ToggleFieldToolsCommand = new RelayCommand(() =>
        {
            IsFieldToolsVisible = !IsFieldToolsVisible;
        });

        ToggleSettingsCommand = new RelayCommand(() =>
        {
            IsSettingsVisible = !IsSettingsVisible;
        });

        // Simulator commands
        ToggleSimulatorPanelCommand = new RelayCommand(() =>
        {
            IsSimulatorPanelVisible = !IsSimulatorPanelVisible;
        });

        ResetSimulatorCommand = new RelayCommand(() =>
        {
            _simulatorService.Reset();
            SimulatorSteerAngle = 0;
            StatusMessage = "Simulator Reset";
        });

        ResetSteerAngleCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle = 0;
            StatusMessage = "Steer Angle Reset to 0°";
        });

        SimulatorForwardCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;  // Reset speed before accelerating
            _simulatorService.IsAcceleratingForward = true;
            _simulatorService.IsAcceleratingBackward = false;
            StatusMessage = "Sim: Accelerating Forward";
        });

        SimulatorStopCommand = new RelayCommand(() =>
        {
            _simulatorService.IsAcceleratingForward = false;
            _simulatorService.IsAcceleratingBackward = false;
            _simulatorService.StepDistance = 0;  // Immediately stop movement
            _simulatorSpeedKph = 0;  // Reset speed slider (use backing field to avoid triggering setter again)
            this.RaisePropertyChanged(nameof(SimulatorSpeedKph));
            this.RaisePropertyChanged(nameof(SimulatorSpeedDisplay));
            StatusMessage = "Sim: Stopped";
        });

        SimulatorReverseCommand = new RelayCommand(() =>
        {
            _simulatorService.StepDistance = 0;  // Reset speed before accelerating
            _simulatorService.IsAcceleratingBackward = true;
            _simulatorService.IsAcceleratingForward = false;
            StatusMessage = "Sim: Accelerating Reverse";
        });

        SimulatorReverseDirectionCommand = new RelayCommand(() =>
        {
            // Reverse direction by adding 180 degrees to current heading
            var newHeading = _simulatorService.HeadingRadians + Math.PI;
            // Normalize to 0-2π range
            if (newHeading > Math.PI * 2)
                newHeading -= Math.PI * 2;
            _simulatorService.SetHeading(newHeading);
            StatusMessage = "Sim: Direction Reversed";
        });

        SimulatorSteerLeftCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle -= 5.0; // 5 degree increments
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}°";
        });

        SimulatorSteerRightCommand = new RelayCommand(() =>
        {
            SimulatorSteerAngle += 5.0; // 5 degree increments
            StatusMessage = $"Steer: {SimulatorSteerAngle:F1}°";
        });

        // Dialog Commands
        ShowDataIODialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.DataIO);
        });

        CloseDataIODialogCommand = new RelayCommand(CloseDataIODialog);
        ConnectToNtripCommand = new AsyncRelayCommand(ConnectToNtripAsync);
        DisconnectFromNtripCommand = new AsyncRelayCommand(DisconnectFromNtripAsync);
        SaveNtripSettingsCommand = new RelayCommand(SaveNtripSettings);
        SetActiveDataIOFieldCommand = new RelayCommand<string>(SetActiveDataIOField);

        // Configuration Dialog Commands
        ShowConfigurationDialogCommand = new RelayCommand(() =>
        {
            ConfigurationViewModel = new ConfigurationViewModel(_configurationService);
            ConfigurationViewModel.CloseRequested += (s, e) =>
            {
                ConfigurationViewModel.IsDialogVisible = false;
            };
            ConfigurationViewModel.IsDialogVisible = true;
        });

        CancelConfigurationDialogCommand = new RelayCommand(() =>
        {
            if (ConfigurationViewModel != null)
                ConfigurationViewModel.IsDialogVisible = false;
        });

        ShowLoadProfileDialogCommand = new RelayCommand(() =>
        {
            // Refresh available profiles
            AvailableProfiles.Clear();
            foreach (var profile in _configurationService.GetAvailableProfiles())
            {
                AvailableProfiles.Add(profile);
            }
            SelectedProfile = _configurationService.Store.ActiveProfileName;
            IsProfileSelectionVisible = true;
        });

        LoadSelectedProfileCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(SelectedProfile))
            {
                _configurationService.LoadProfile(SelectedProfile);
                _settingsService.Settings.LastUsedVehicleProfile = SelectedProfile;
                _settingsService.Save();
                this.RaisePropertyChanged(nameof(CurrentProfileName));
            }
            IsProfileSelectionVisible = false;
        });

        CancelProfileSelectionCommand = new RelayCommand(() =>
        {
            IsProfileSelectionVisible = false;
        });

        ShowNewProfileDialogCommand = new RelayCommand(() =>
        {
            // Generate a unique profile name
            var baseName = "New Profile";
            var profileName = baseName;
            var counter = 1;
            var existingProfiles = _configurationService.GetAvailableProfiles();
            while (existingProfiles.Contains(profileName))
            {
                profileName = $"{baseName} {counter++}";
            }

            _configurationService.CreateProfile(profileName);
            _settingsService.Settings.LastUsedVehicleProfile = profileName;
            _settingsService.Save();
            this.RaisePropertyChanged(nameof(CurrentProfileName));
        });

        ShowSimCoordsDialogCommand = new RelayCommand(() =>
        {
            if (IsSimulatorEnabled)
            {
                // Don't allow changing coords while simulator is running
                System.Diagnostics.Debug.WriteLine("[SimCoords] Disable simulator first to change coordinates");
                return;
            }
            // Load current position into the dialog fields
            // Round to 8 decimal places
            var currentPos = GetSimulatorPosition();
            SimCoordsDialogLatitude = Math.Round((decimal)currentPos.Latitude, 8);
            SimCoordsDialogLongitude = Math.Round((decimal)currentPos.Longitude, 8);
            // Show the panel-based dialog
            State.UI.ShowDialog(DialogType.SimCoords);
        });

        CancelSimCoordsDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmSimCoordsDialogCommand = new RelayCommand(() =>
        {
            // Apply the coordinates from the dialog (convert from decimal? to double)
            double lat = (double)(SimCoordsDialogLatitude ?? 0m);
            double lon = (double)(SimCoordsDialogLongitude ?? 0m);
            SetSimulatorCoordinates(lat, lon);
            State.UI.CloseDialog();
        });

        ShowFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            // Use settings directory which defaults to ~/Documents/AgValoniaGPS/Fields
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            System.Diagnostics.Debug.WriteLine($"[FieldSelection] Settings.FieldsDirectory = '{fieldsDir}'");
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
                System.Diagnostics.Debug.WriteLine($"[FieldSelection] Using fallback path: '{fieldsDir}'");
            }
            _fieldSelectionDirectory = fieldsDir;
            System.Diagnostics.Debug.WriteLine($"[FieldSelection] Directory exists: {Directory.Exists(fieldsDir)}");

            // Populate the available fields list
            PopulateAvailableFields(fieldsDir);
            System.Diagnostics.Debug.WriteLine($"[FieldSelection] Found {AvailableFields.Count} fields");

            // Show the panel-based dialog
            State.UI.ShowDialog(DialogType.FieldSelection);
        });

        CancelFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedFieldInfo = null;
        });

        ConfirmFieldSelectionDialogCommand = new RelayCommand(() =>
        {
            if (SelectedFieldInfo == null) return;

            var fieldPath = Path.Combine(_fieldSelectionDirectory, SelectedFieldInfo.Name);
            FieldsRootDirectory = _fieldSelectionDirectory;
            CurrentFieldName = SelectedFieldInfo.Name;
            IsFieldOpen = true;

            // Save as last opened field
            _settingsService.Settings.LastOpenedField = SelectedFieldInfo.Name;
            _settingsService.Save();

            // Load field origin from Field.txt (for map centering)
            try
            {
                var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                if (fieldInfo.Origin != null)
                {
                    _fieldOriginLatitude = fieldInfo.Origin.Latitude;
                    _fieldOriginLongitude = fieldInfo.Origin.Longitude;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Field] Could not load Field.txt origin: {ex.Message}");
            }

            // Try to load boundary from field
            var boundary = _boundaryFileService.LoadBoundary(fieldPath);
            if (boundary != null)
            {
                SetCurrentBoundary(boundary);
                CenterMapOnBoundary(boundary);
            }

            // Try to load background image from field
            LoadBackgroundImage(fieldPath, boundary);

            // Set the active field so headland and other field-specific data loads
            var field = new Field
            {
                Name = SelectedFieldInfo.Name,
                DirectoryPath = fieldPath,
                Boundary = boundary
            };
            _fieldService.SetActiveField(field);

            State.UI.CloseDialog();
            IsJobMenuPanelVisible = false;
            StatusMessage = $"Opened field: {SelectedFieldInfo.Name}";
            SelectedFieldInfo = null;
        });

        DeleteSelectedFieldCommand = new RelayCommand(() =>
        {
            if (SelectedFieldInfo == null) return;

            var fieldPath = Path.Combine(_fieldSelectionDirectory, SelectedFieldInfo.Name);
            try
            {
                if (Directory.Exists(fieldPath))
                {
                    Directory.Delete(fieldPath, true);
                    StatusMessage = $"Deleted field: {SelectedFieldInfo.Name}";
                    PopulateAvailableFields(_fieldSelectionDirectory);
                    SelectedFieldInfo = null;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting field: {ex.Message}";
            }
        });

        SortFieldsCommand = new RelayCommand(() =>
        {
            _fieldsSortedAZ = !_fieldsSortedAZ;
            var sorted = _fieldsSortedAZ
                ? AvailableFields.OrderBy(f => f.Name).ToList()
                : AvailableFields.OrderByDescending(f => f.Name).ToList();
            AvailableFields.Clear();
            foreach (var field in sorted)
            {
                AvailableFields.Add(field);
            }
        });

        ShowNewFieldDialogCommand = new RelayCommand(() =>
        {
            // Initialize with current GPS position or defaults
            NewFieldLatitude = Latitude != 0 ? Latitude : 40.7128;
            NewFieldLongitude = Longitude != 0 ? Longitude : -74.0060;
            NewFieldName = string.Empty;
            State.UI.ShowDialog(DialogType.NewField);
        });

        CancelNewFieldDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            NewFieldName = string.Empty;
        });

        ConfirmNewFieldDialogCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(NewFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            // Get the fields directory
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            // Create the field directory
            var fieldPath = Path.Combine(fieldsDir, NewFieldName);
            if (Directory.Exists(fieldPath))
            {
                StatusMessage = $"Field '{NewFieldName}' already exists";
                return;
            }

            try
            {
                Directory.CreateDirectory(fieldPath);

                // Save the field origin coordinates
                var originFile = Path.Combine(fieldPath, "field.origin");
                File.WriteAllText(originFile, $"{NewFieldLatitude},{NewFieldLongitude}");

                // Create Field.txt in AgOpenGPS format
                var fieldTxtPath = Path.Combine(fieldPath, "Field.txt");
                var fieldTxtContent = $"{DateTime.Now:yyyy-MMM-dd hh:mm:ss tt}\n" +
                                      "$FieldDir\n" +
                                      $"{NewFieldName}\n" +
                                      "$Offsets\n" +
                                      "0,0\n" +
                                      "Convergence\n" +
                                      "0\n" +
                                      "StartFix\n" +
                                      $"{NewFieldLatitude},{NewFieldLongitude}\n";
                File.WriteAllText(fieldTxtPath, fieldTxtContent);

                // Set as current field
                CurrentFieldName = NewFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                // Reset LocalPlane so it will be recreated with new origin
                _simulatorLocalPlane = null;

                // Save as last opened field
                _settingsService.Settings.LastOpenedField = NewFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Created field: {NewFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating field: {ex.Message}";
            }
        });

        ShowFromExistingFieldDialogCommand = new RelayCommand(() =>
        {
            // Populate fields list (reuse same list as field selection)
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }
            _fieldSelectionDirectory = fieldsDir;
            PopulateAvailableFields(fieldsDir);

            // Reset copy options
            CopyFlags = true;
            CopyMapping = true;
            CopyHeadland = true;
            CopyLines = true;
            FromExistingFieldName = string.Empty;
            FromExistingSelectedField = null;

            // Pre-select first field if available
            if (AvailableFields.Count > 0)
            {
                FromExistingSelectedField = AvailableFields[0];
            }

            State.UI.ShowDialog(DialogType.FromExistingField);
        });

        CancelFromExistingFieldDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            FromExistingSelectedField = null;
            FromExistingFieldName = string.Empty;
        });

        ConfirmFromExistingFieldDialogCommand = new RelayCommand(() =>
        {
            if (FromExistingSelectedField == null)
            {
                StatusMessage = "Please select a field to copy from";
                return;
            }

            var newFieldName = FromExistingFieldName.Trim();
            if (string.IsNullOrWhiteSpace(newFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            // Get the fields directory
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var sourcePath = Path.Combine(fieldsDir, FromExistingSelectedField.Name);
            var newFieldPath = Path.Combine(fieldsDir, newFieldName);

            // Check if field already exists (unless same name as source)
            if (Directory.Exists(newFieldPath) && newFieldName != FromExistingSelectedField.Name)
            {
                StatusMessage = $"Field '{newFieldName}' already exists";
                return;
            }

            try
            {
                // Create the new field directory
                Directory.CreateDirectory(newFieldPath);

                // Copy field.origin if exists
                var originFile = Path.Combine(sourcePath, "field.origin");
                if (File.Exists(originFile))
                {
                    File.Copy(originFile, Path.Combine(newFieldPath, "field.origin"), true);
                }

                // Copy boundary
                var boundaryFile = Path.Combine(sourcePath, "boundary.json");
                if (File.Exists(boundaryFile))
                {
                    File.Copy(boundaryFile, Path.Combine(newFieldPath, "boundary.json"), true);
                }

                // Copy flags if enabled
                if (CopyFlags)
                {
                    var flagsFile = Path.Combine(sourcePath, "flags.json");
                    if (File.Exists(flagsFile))
                    {
                        File.Copy(flagsFile, Path.Combine(newFieldPath, "flags.json"), true);
                    }
                }

                // Copy mapping if enabled
                if (CopyMapping)
                {
                    var mappingFile = Path.Combine(sourcePath, "mapping.json");
                    if (File.Exists(mappingFile))
                    {
                        File.Copy(mappingFile, Path.Combine(newFieldPath, "mapping.json"), true);
                    }
                }

                // Copy headland if enabled
                if (CopyHeadland)
                {
                    var headlandFile = Path.Combine(sourcePath, "headland.json");
                    if (File.Exists(headlandFile))
                    {
                        File.Copy(headlandFile, Path.Combine(newFieldPath, "headland.json"), true);
                    }
                }

                // Copy lines if enabled
                if (CopyLines)
                {
                    var linesFile = Path.Combine(sourcePath, "lines.json");
                    if (File.Exists(linesFile))
                    {
                        File.Copy(linesFile, Path.Combine(newFieldPath, "lines.json"), true);
                    }
                    var abLinesFile = Path.Combine(sourcePath, "ablines.json");
                    if (File.Exists(abLinesFile))
                    {
                        File.Copy(abLinesFile, Path.Combine(newFieldPath, "ablines.json"), true);
                    }
                }

                // Set as current field
                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                // Save as last opened field
                _settingsService.Settings.LastOpenedField = newFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Created field from existing: {newFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating field: {ex.Message}";
            }
        });

        AppendVehicleNameCommand = new RelayCommand(() =>
        {
            var vehicleName = Vehicle.VehicleTypeDisplayName;
            if (!string.IsNullOrWhiteSpace(vehicleName))
            {
                FromExistingFieldName = (FromExistingFieldName + " " + vehicleName).Trim();
            }
        });

        AppendDateCommand = new RelayCommand(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            FromExistingFieldName = (FromExistingFieldName + " " + dateStr).Trim();
        });

        AppendTimeCommand = new RelayCommand(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            FromExistingFieldName = (FromExistingFieldName + " " + timeStr).Trim();
        });

        BackspaceFieldNameCommand = new RelayCommand(() =>
        {
            if (FromExistingFieldName.Length > 0)
            {
                FromExistingFieldName = FromExistingFieldName.Substring(0, FromExistingFieldName.Length - 1);
            }
        });

        ToggleCopyFlagsCommand = new RelayCommand(() => CopyFlags = !CopyFlags);
        ToggleCopyMappingCommand = new RelayCommand(() => CopyMapping = !CopyMapping);
        ToggleCopyHeadlandCommand = new RelayCommand(() => CopyHeadland = !CopyHeadland);
        ToggleCopyLinesCommand = new RelayCommand(() => CopyLines = !CopyLines);

        // KML Import Dialog
        ShowKmlImportDialogCommand = new RelayCommand(() =>
        {
            PopulateAvailableKmlFiles();
            KmlImportFieldName = string.Empty;
            KmlBoundaryPointCount = 0;
            KmlCenterLatitude = 0;
            KmlCenterLongitude = 0;
            _kmlBoundaryPoints.Clear();
            SelectedKmlFile = null;

            // Pre-select first file if available
            if (AvailableKmlFiles.Count > 0)
            {
                SelectedKmlFile = AvailableKmlFiles[0];
            }

            State.UI.ShowDialog(DialogType.KmlImport);
        });

        CancelKmlImportDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedKmlFile = null;
            KmlImportFieldName = string.Empty;
        });

        ConfirmKmlImportDialogCommand = new RelayCommand(() =>
        {
            if (SelectedKmlFile == null)
            {
                StatusMessage = "Please select a KML file";
                return;
            }

            var newFieldName = KmlImportFieldName.Trim();
            if (string.IsNullOrWhiteSpace(newFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            if (_kmlBoundaryPoints.Count < 3)
            {
                StatusMessage = "KML file must contain at least 3 boundary points";
                return;
            }

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var newFieldPath = Path.Combine(fieldsDir, newFieldName);
            if (Directory.Exists(newFieldPath))
            {
                StatusMessage = $"Field '{newFieldName}' already exists";
                return;
            }

            try
            {
                // Create field directory
                Directory.CreateDirectory(newFieldPath);

                // Save origin coordinates
                var originFile = Path.Combine(newFieldPath, "field.origin");
                File.WriteAllText(originFile, $"{KmlCenterLatitude},{KmlCenterLongitude}");

                // Create and save boundary
                var origin = new Wgs84(KmlCenterLatitude, KmlCenterLongitude);
                var sharedProps = new SharedFieldProperties();
                var localPlane = new LocalPlane(origin, sharedProps);

                var outerPolygon = new BoundaryPolygon();
                foreach (var (lat, lon) in _kmlBoundaryPoints)
                {
                    var wgs84 = new Wgs84(lat, lon);
                    var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                    outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                }

                var boundary = new Boundary { OuterBoundary = outerPolygon };
                _boundaryFileService.SaveBoundary(boundary, newFieldPath);

                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                _settingsService.Settings.LastOpenedField = newFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Imported KML: {newFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing KML: {ex.Message}";
            }
        });

        KmlAppendDateCommand = new RelayCommand(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            KmlImportFieldName = (KmlImportFieldName + " " + dateStr).Trim();
        });

        KmlAppendTimeCommand = new RelayCommand(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            KmlImportFieldName = (KmlImportFieldName + " " + timeStr).Trim();
        });

        KmlBackspaceFieldNameCommand = new RelayCommand(() =>
        {
            if (KmlImportFieldName.Length > 0)
            {
                KmlImportFieldName = KmlImportFieldName.Substring(0, KmlImportFieldName.Length - 1);
            }
        });

        // ISO-XML Import Dialog
        ShowIsoXmlImportDialogCommand = new RelayCommand(() =>
        {
            PopulateAvailableIsoXmlFiles();
            IsoXmlImportFieldName = string.Empty;
            SelectedIsoXmlFile = null;

            if (AvailableIsoXmlFiles.Count > 0)
            {
                SelectedIsoXmlFile = AvailableIsoXmlFiles[0];
            }

            State.UI.ShowDialog(DialogType.IsoXmlImport);
        });

        CancelIsoXmlImportDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedIsoXmlFile = null;
            IsoXmlImportFieldName = string.Empty;
        });

        ConfirmIsoXmlImportDialogCommand = new RelayCommand(() =>
        {
            if (SelectedIsoXmlFile == null)
            {
                StatusMessage = "Please select an ISO-XML folder";
                return;
            }

            var newFieldName = IsoXmlImportFieldName.Trim();
            if (string.IsNullOrWhiteSpace(newFieldName))
            {
                StatusMessage = "Please enter a field name";
                return;
            }

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var newFieldPath = Path.Combine(fieldsDir, newFieldName);
            if (Directory.Exists(newFieldPath))
            {
                StatusMessage = $"Field '{newFieldName}' already exists";
                return;
            }

            try
            {
                // TODO: Implement ISO-XML parsing when needed
                // For now, just create the field directory
                Directory.CreateDirectory(newFieldPath);

                CurrentFieldName = newFieldName;
                FieldsRootDirectory = fieldsDir;
                IsFieldOpen = true;

                _settingsService.Settings.LastOpenedField = newFieldName;
                _settingsService.Save();

                State.UI.CloseDialog();
                IsJobMenuPanelVisible = false;
                StatusMessage = $"Imported ISO-XML: {newFieldName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing ISO-XML: {ex.Message}";
            }
        });

        IsoXmlAppendDateCommand = new RelayCommand(() =>
        {
            var dateStr = DateTime.Now.ToString("yyyy-MMM-dd");
            IsoXmlImportFieldName = (IsoXmlImportFieldName + " " + dateStr).Trim();
        });

        IsoXmlAppendTimeCommand = new RelayCommand(() =>
        {
            var timeStr = DateTime.Now.ToString("HH-mm");
            IsoXmlImportFieldName = (IsoXmlImportFieldName + " " + timeStr).Trim();
        });

        IsoXmlBackspaceFieldNameCommand = new RelayCommand(() =>
        {
            if (IsoXmlImportFieldName.Length > 0)
            {
                IsoXmlImportFieldName = IsoXmlImportFieldName.Substring(0, IsoXmlImportFieldName.Length - 1);
            }
        });

        // Boundary Map Dialog Commands (for satellite map boundary drawing)
        ShowBoundaryMapDialogCommand = new RelayCommand(() =>
        {
            // Set center: prefer field origin, then GPS position, then 0,0
            if (_fieldOriginLatitude != 0 || _fieldOriginLongitude != 0)
            {
                // Use field origin from Field.txt
                BoundaryMapCenterLatitude = _fieldOriginLatitude;
                BoundaryMapCenterLongitude = _fieldOriginLongitude;
            }
            else if (Latitude != 0 || Longitude != 0)
            {
                // Fall back to current GPS position
                BoundaryMapCenterLatitude = Latitude;
                BoundaryMapCenterLongitude = Longitude;
            }
            // else: leave as 0,0 (will show default location)

            BoundaryMapPointCount = 0;
            BoundaryMapCanSave = false;
            BoundaryMapCoordinateText = string.Empty;
            BoundaryMapResultPoints.Clear();
            State.UI.ShowDialog(DialogType.BoundaryMap);
        });

        CancelBoundaryMapDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            BoundaryMapResultPoints.Clear();
        });

        ConfirmBoundaryMapDialogCommand = new RelayCommand(() =>
        {
            Console.WriteLine($"[BoundaryMap] ConfirmBoundaryMapDialogCommand called");
            Console.WriteLine($"[BoundaryMap] Points: {BoundaryMapResultPoints.Count}, IsFieldOpen: {IsFieldOpen}, CurrentFieldName: {CurrentFieldName}");

            if (BoundaryMapResultPoints.Count >= 3 && IsFieldOpen && !string.IsNullOrEmpty(CurrentFieldName))
            {
                try
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                    Console.WriteLine($"[BoundaryMap] Field path: {fieldPath}");
                    Console.WriteLine($"[BoundaryMap] Directory exists: {Directory.Exists(fieldPath)}");

                    // Load existing boundary or create new one
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

                    // Calculate origin for LocalPlane
                    // IMPORTANT: If we have a background image, use its center as the origin
                    // This ensures the boundary aligns with landmarks in the image
                    // The user drew the boundary on specific landmarks in the viewport,
                    // so we need to use the same reference point for both
                    double centerLat, centerLon;
                    if (!string.IsNullOrEmpty(BoundaryMapResultBackgroundPath))
                    {
                        // Use image (viewport) center as origin - this is where the user was looking when drawing
                        centerLat = (BoundaryMapResultNwLat + BoundaryMapResultSeLat) / 2;
                        centerLon = (BoundaryMapResultNwLon + BoundaryMapResultSeLon) / 2;
                        Console.WriteLine($"[BoundaryMap] Using image center as origin: ({centerLat:F8}, {centerLon:F8})");
                    }
                    else
                    {
                        // No background image - use boundary center as origin
                        centerLat = BoundaryMapResultPoints.Average(p => p.Latitude);
                        centerLon = BoundaryMapResultPoints.Average(p => p.Longitude);
                        Console.WriteLine($"[BoundaryMap] Using boundary center as origin: ({centerLat:F8}, {centerLon:F8})");
                    }

                    // Convert WGS84 boundary points to local coordinates
                    var origin = new Wgs84(centerLat, centerLon);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();

                    Console.WriteLine($"[BoundaryMap] Converting boundary points with origin ({centerLat:F8}, {centerLon:F8})");
                    foreach (var (lat, lon) in BoundaryMapResultPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                        Console.WriteLine($"[BoundaryMap]   WGS84 ({lat:F8}, {lon:F8}) -> Local E={geoCoord.Easting:F1}, N={geoCoord.Northing:F1}");
                    }

                    boundary.OuterBoundary = outerPolygon;

                    // Save boundary
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // Update Field.txt with the origin used for this boundary
                    // This ensures background images load with the same coordinate system
                    _fieldOriginLatitude = centerLat;
                    _fieldOriginLongitude = centerLon;
                    try
                    {
                        var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                        fieldInfo.Origin = new Position { Latitude = centerLat, Longitude = centerLon };
                        _fieldPlaneFileService.SaveField(fieldInfo, fieldPath);
                        Console.WriteLine($"[BoundaryMap] Updated Field.txt origin to ({centerLat:F8}, {centerLon:F8})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BoundaryMap] Could not update Field.txt: {ex.Message}");
                    }

                    // Update map
                    SetCurrentBoundary(boundary);

                    // Center camera on the boundary and set appropriate zoom
                    if (outerPolygon.Points.Count > 0)
                    {
                        // Calculate boundary center and extent
                        double minE = double.MaxValue, maxE = double.MinValue;
                        double minN = double.MaxValue, maxN = double.MinValue;
                        foreach (var pt in outerPolygon.Points)
                        {
                            minE = Math.Min(minE, pt.Easting);
                            maxE = Math.Max(maxE, pt.Easting);
                            minN = Math.Min(minN, pt.Northing);
                            maxN = Math.Max(maxN, pt.Northing);
                        }
                        double centerE = (minE + maxE) / 2.0;
                        double centerN = (minN + maxN) / 2.0;
                        double extentE = maxE - minE;
                        double extentN = maxN - minN;
                        double maxExtent = Math.Max(extentE, extentN);

                        // Pan to center
                        _mapService.PanTo(centerE, centerN);

                        // Calculate zoom to fit boundary (viewHeight = 200/zoom, so zoom = 200/viewHeight)
                        // Add 20% padding
                        double desiredView = maxExtent * 1.2;
                        if (desiredView > 0)
                        {
                            double newZoom = 200.0 / desiredView;
                            newZoom = Math.Clamp(newZoom, 0.1, 10.0);
                            _mapService.SetCamera(centerE, centerN, newZoom, 0);
                        }

                        Console.WriteLine($"[BoundaryMap] Saved boundary with {outerPolygon.Points.Count} points");
                        Console.WriteLine($"[BoundaryMap] Center: ({centerE:F1}, {centerN:F1}), Extent: {maxExtent:F1}m");
                    }

                    // Handle background image if captured
                    if (!string.IsNullOrEmpty(BoundaryMapResultBackgroundPath))
                    {
                        SaveBackgroundImage(BoundaryMapResultBackgroundPath, fieldPath,
                            BoundaryMapResultNwLat, BoundaryMapResultNwLon,
                            BoundaryMapResultSeLat, BoundaryMapResultSeLon);
                    }

                    // Refresh the boundary list
                    RefreshBoundaryList();

                    StatusMessage = $"Boundary created with {BoundaryMapResultPoints.Count} points";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error creating boundary: {ex.Message}";
                }
            }

            State.UI.CloseDialog();
            IsBoundaryPanelVisible = false;
            BoundaryMapResultPoints.Clear();
        });

        ShowAgShareDownloadDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareDownload);
        });

        CancelAgShareDownloadDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAgShareUploadDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.AgShareUpload);
        });

        CancelAgShareUploadDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            // Load current settings from storage
            AgShareSettingsServerUrl = _settingsService.Settings.AgShareServer;
            AgShareSettingsApiKey = _settingsService.Settings.AgShareApiKey;
            AgShareSettingsEnabled = _settingsService.Settings.AgShareEnabled;
            State.UI.ShowDialog(DialogType.AgShareSettings);
        });

        CancelAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ConfirmAgShareSettingsDialogCommand = new RelayCommand(() =>
        {
            // Save settings to storage
            _settingsService.Settings.AgShareServer = AgShareSettingsServerUrl;
            _settingsService.Settings.AgShareApiKey = AgShareSettingsApiKey;
            _settingsService.Settings.AgShareEnabled = AgShareSettingsEnabled;
            _settingsService.Save();

            State.UI.CloseDialog();
            StatusMessage = "AgShare settings saved";
        });

        ShowBoundaryDialogCommand = new RelayCommand(() =>
        {
            // Toggle boundary panel visibility - this shows the panel where user can
            // choose how to create the boundary (KML import, drive around, etc.)
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        // Headland Commands
        ShowHeadlandBuilderCommand = new RelayCommand(() =>
        {
            if (!IsFieldOpen)
            {
                StatusMessage = "Open a field first";
                return;
            }
            State.UI.ShowDialog(DialogType.HeadlandBuilder);
            // Trigger initial preview
            UpdateHeadlandPreview();
        });

        ToggleHeadlandCommand = new RelayCommand(() =>
        {
            if (!HasHeadland)
            {
                StatusMessage = "No headland defined";
                return;
            }
            IsHeadlandOn = !IsHeadlandOn;
        });

        ToggleSectionInHeadlandCommand = new RelayCommand(() =>
        {
            IsSectionControlInHeadland = !IsSectionControlInHeadland;
            StatusMessage = IsSectionControlInHeadland ? "Section control in headland: ON" : "Section control in headland: OFF";
        });

        ResetToolHeadingCommand = new RelayCommand(() =>
        {
            // Reset tool heading to be directly behind tractor
            StatusMessage = "Tool heading reset";
        });

        BuildHeadlandCommand = new RelayCommand(() =>
        {
            BuildHeadlandFromBoundary();
        });

        ClearHeadlandCommand = new RelayCommand(() =>
        {
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
            HasHeadland = false;
            IsHeadlandOn = false;
            StatusMessage = "Headland cleared";
        });

        CloseHeadlandBuilderCommand = new RelayCommand(() =>
        {
            HeadlandPreviewLine = null;
            State.UI.CloseDialog();
        });

        SetHeadlandToToolWidthCommand = new RelayCommand(() =>
        {
            // Set headland distance to implement width (use track width * 2 as approximation)
            // TODO: Add actual tool/implement width to VehicleConfiguration
            HeadlandDistance = Vehicle.TrackWidth > 0 ? Vehicle.TrackWidth * 2 : 12.0;
            UpdateHeadlandPreview();
        });

        PreviewHeadlandCommand = new RelayCommand(() =>
        {
            UpdateHeadlandPreview();
        });

        IncrementHeadlandDistanceCommand = new RelayCommand(() =>
        {
            HeadlandDistance = Math.Min(HeadlandDistance + 0.5, 100.0);
            UpdateHeadlandPreview();
        });

        DecrementHeadlandDistanceCommand = new RelayCommand(() =>
        {
            HeadlandDistance = Math.Max(HeadlandDistance - 0.5, 0.5);
            UpdateHeadlandPreview();
        });

        IncrementHeadlandPassesCommand = new RelayCommand(() =>
        {
            HeadlandPasses = Math.Min(HeadlandPasses + 1, 10);
            UpdateHeadlandPreview();
        });

        DecrementHeadlandPassesCommand = new RelayCommand(() =>
        {
            HeadlandPasses = Math.Max(HeadlandPasses - 1, 1);
            UpdateHeadlandPreview();
        });

        // Headland Dialog (FormHeadLine) commands
        ShowHeadlandDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.Headland);
            UpdateHeadlandPreview();
        });

        CloseHeadlandDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            HeadlandPreviewLine = null;
        });

        ExtendHeadlandACommand = new RelayCommand(() =>
        {
            // TODO: Extend headland at point A
            StatusMessage = "Extend A - not yet implemented";
        });

        ExtendHeadlandBCommand = new RelayCommand(() =>
        {
            // TODO: Extend headland at point B
            StatusMessage = "Extend B - not yet implemented";
        });

        ShrinkHeadlandACommand = new RelayCommand(() =>
        {
            // TODO: Shrink headland at point A
            StatusMessage = "Shrink A - not yet implemented";
        });

        ShrinkHeadlandBCommand = new RelayCommand(() =>
        {
            // TODO: Shrink headland at point B
            StatusMessage = "Shrink B - not yet implemented";
        });

        ResetHeadlandCommand = new RelayCommand(() =>
        {
            ClearHeadlandCommand?.Execute(null);
            StatusMessage = "Headland reset";
        });

        ClipHeadlandLineCommand = new RelayCommand(() =>
        {
            if (!HeadlandPointsSelected)
            {
                StatusMessage = "Select 2 points on the boundary first";
                return;
            }

            // Check if we have a headland to clip (either built headland or preview)
            var headlandToClip = CurrentHeadlandLine ?? ConvertPreviewToVec3(HeadlandPreviewLine);
            if (headlandToClip == null || headlandToClip.Count < 3)
            {
                StatusMessage = "No headland to clip - use Build first";
                return;
            }

            // Clip the headland using the clip line (between the two selected points)
            ClipHeadlandAtLine(headlandToClip);
        });

        UndoHeadlandCommand = new RelayCommand(() =>
        {
            // TODO: Undo headland changes
            StatusMessage = "Undo - not yet implemented";
        });

        TurnOffHeadlandCommand = new RelayCommand(() =>
        {
            IsHeadlandOn = false;
            HasHeadland = false;
            CurrentHeadlandLine = null;
            HeadlandPreviewLine = null;
            StatusMessage = "Headland turned off";
        });

        // AB Line Guidance Commands - Bottom Bar
        SnapLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Left Track - not yet implemented";
        });

        SnapRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Right Track - not yet implemented";
        });

        StopGuidanceCommand = new RelayCommand(() =>
        {
            StatusMessage = "Guidance Stopped";
        });

        UTurnCommand = new RelayCommand(() =>
        {
            StatusMessage = "U-Turn - not yet implemented";
        });

        // AB Line Guidance Commands - Flyout Menu
        ShowTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.Tracks);
        });

        CloseTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // Track management commands
        DeleteSelectedTrackCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null)
            {
                SavedTracks.Remove(SelectedTrack);
                SelectedTrack = null;
                SaveTracksToFile(); // Persist deletion to disk
                StatusMessage = "Track deleted";
            }
        });

        SwapABPointsCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null && SelectedTrack.Points.Count >= 2)
            {
                // Reverse the points list to swap A and B
                SelectedTrack.Points.Reverse();
                StatusMessage = $"Swapped A/B points for {SelectedTrack.Name}";
            }
        });

        SelectTrackAsActiveCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null)
            {
                // Deactivate all tracks first
                foreach (var track in SavedTracks)
                {
                    track.IsActive = false;
                }
                // Activate the selected track
                SelectedTrack.IsActive = true;
                HasActiveTrack = true;
                IsAutoSteerAvailable = true;
                StatusMessage = $"Activated track: {SelectedTrack.Name}";
                State.UI.CloseDialog();
            }
        });

        ShowQuickABSelectorCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.QuickABSelector);
        });

        CloseQuickABSelectorCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowDrawABDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.DrawAB);
        });

        CloseDrawABDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        StartNewABLineCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Line - not yet implemented";
        });

        StartNewABCurveCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Curve - not yet implemented";
        });

        // Quick AB Mode Commands
        StartAPlusLineCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            StatusMessage = "A+ Line mode: Line created from current position and heading";
            // TODO: Create AB line from current position using current heading
        });

        StartDriveABCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DriveAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = ABCreationInstructions;
        });

        StartCurveRecordingCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            StatusMessage = "Curve mode: Start driving to record curve path";
            // TODO: Start curve recording mode
        });

        StartDrawABModeCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DrawAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = ABCreationInstructions;
        });

        // SetABPointCommand is called when user taps during AB creation mode
        // For DriveAB mode: uses current GPS position
        // For DrawAB mode: uses the tapped map coordinates (passed as parameter)
        SetABPointCommand = new RelayCommand<object?>(param =>
        {
            System.Console.WriteLine($"[SetABPointCommand] Called with param={param?.GetType().Name ?? "null"}, Mode={CurrentABCreationMode}, Step={CurrentABPointStep}");

            if (CurrentABCreationMode == ABCreationMode.None)
            {
                System.Console.WriteLine("[SetABPointCommand] Mode is None, returning");
                return;
            }

            Position pointToSet;

            if (CurrentABCreationMode == ABCreationMode.DriveAB)
            {
                // Use current GPS position
                pointToSet = new Position
                {
                    Latitude = Latitude,
                    Longitude = Longitude,
                    Easting = Easting,
                    Northing = Northing,
                    Heading = Heading
                };
                System.Console.WriteLine($"[SetABPointCommand] DriveAB - GPS position: E={Easting:F2}, N={Northing:F2}");
            }
            else if (CurrentABCreationMode == ABCreationMode.DrawAB && param is Position mapPos)
            {
                // Use the tapped map position
                pointToSet = mapPos;
                System.Console.WriteLine($"[SetABPointCommand] DrawAB - Map position: E={mapPos.Easting:F2}, N={mapPos.Northing:F2}");
            }
            else
            {
                System.Console.WriteLine($"[SetABPointCommand] Invalid state - returning");
                return; // Invalid state
            }

            if (CurrentABPointStep == ABPointStep.SettingPointA)
            {
                // Store Point A and move to Point B
                PendingPointA = pointToSet;
                CurrentABPointStep = ABPointStep.SettingPointB;
                StatusMessage = ABCreationInstructions;
                System.Console.WriteLine($"[SetABPointCommand] Set Point A: E={pointToSet.Easting:F2}, N={pointToSet.Northing:F2}");
            }
            else if (CurrentABPointStep == ABPointStep.SettingPointB)
            {
                // Create the AB line with Point A and Point B
                if (PendingPointA != null)
                {
                    var heading = CalculateHeading(PendingPointA, pointToSet);
                    var headingRadians = heading * Math.PI / 180.0;
                    var newTrack = Track.FromABLine(
                        $"AB_{heading:F1}° {DateTime.Now:HH:mm:ss}",
                        new Vec3(PendingPointA.Easting, PendingPointA.Northing, headingRadians),
                        new Vec3(pointToSet.Easting, pointToSet.Northing, headingRadians));
                    newTrack.IsActive = true;

                    SavedTracks.Add(newTrack);
                    SaveTracksToFile(); // Persist to disk
                    HasActiveTrack = true;
                    IsAutoSteerAvailable = true;
                    StatusMessage = $"Created AB line: {newTrack.Name} ({heading:F1}°)";
                    System.Console.WriteLine($"[SetABPointCommand] Created AB Line: {newTrack.Name}, A=({PendingPointA.Easting:F2},{PendingPointA.Northing:F2}), B=({pointToSet.Easting:F2},{pointToSet.Northing:F2}), Heading={heading:F1}°");

                    // Reset state
                    CurrentABCreationMode = ABCreationMode.None;
                    CurrentABPointStep = ABPointStep.None;
                    PendingPointA = null;
                }
            }
        });

        CancelABCreationCommand = new RelayCommand(() =>
        {
            CurrentABCreationMode = ABCreationMode.None;
            CurrentABPointStep = ABPointStep.None;
            PendingPointA = null;
            StatusMessage = "AB line creation cancelled";
        });

        CycleABLinesCommand = new RelayCommand(() =>
        {
            StatusMessage = "Cycle AB Lines - not yet implemented";
        });

        SmoothABLineCommand = new RelayCommand(() =>
        {
            StatusMessage = "Smooth AB Line - not yet implemented";
        });

        NudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Nudge Left - not yet implemented";
        });

        NudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Nudge Right - not yet implemented";
        });

        FineNudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Fine Nudge Left - not yet implemented";
        });

        FineNudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Fine Nudge Right - not yet implemented";
        });

        // Bottom Strip Commands (matching AgOpenGPS panelBottom)
        ChangeMappingColorCommand = new RelayCommand(() =>
        {
            StatusMessage = "Section Mapping Color - not yet implemented";
        });

        SnapToPivotCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Pivot - not yet implemented";
        });

        ToggleYouSkipCommand = new RelayCommand(() =>
        {
            StatusMessage = "YouSkip Toggle - not yet implemented";
        });

        ToggleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            IsUTurnSkipRowsEnabled = !IsUTurnSkipRowsEnabled;
            StatusMessage = IsUTurnSkipRowsEnabled
                ? $"U-Turn skip rows: ON ({UTurnSkipRows} rows)"
                : "U-Turn skip rows: OFF";
        });

        CycleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            // Cycle through 0-9, wrap back to 0 after 9
            UTurnSkipRows = (UTurnSkipRows + 1) % 10;
            StatusMessage = $"Skip rows: {UTurnSkipRows}";
        });

        // Flags Commands
        PlaceRedFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Red Flag - not yet implemented";
        });

        PlaceGreenFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Green Flag - not yet implemented";
        });

        PlaceYellowFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Yellow Flag - not yet implemented";
        });

        DeleteAllFlagsCommand = new RelayCommand(() =>
        {
            StatusMessage = "Delete All Flags - not yet implemented";
        });

        // Right Navigation Panel Commands
        ToggleContourModeCommand = new RelayCommand(() =>
        {
            IsContourModeOn = !IsContourModeOn;
            StatusMessage = IsContourModeOn ? "Contour mode ON" : "Contour mode OFF";
        });

        ToggleManualModeCommand = new RelayCommand(() =>
        {
            IsManualSectionMode = !IsManualSectionMode;
            StatusMessage = IsManualSectionMode ? "Manual section mode ON" : "Manual section mode OFF";
        });

        ToggleSectionMasterCommand = new RelayCommand(() =>
        {
            IsSectionMasterOn = !IsSectionMasterOn;
            StatusMessage = IsSectionMasterOn ? "Section master ON" : "Section master OFF";
        });

        ToggleYouTurnCommand = new RelayCommand(() =>
        {
            IsYouTurnEnabled = !IsYouTurnEnabled;
            StatusMessage = IsYouTurnEnabled ? "YouTurn enabled" : "YouTurn disabled";
        });

        ToggleAutoSteerCommand = new RelayCommand(() =>
        {
            if (!IsAutoSteerAvailable)
            {
                StatusMessage = "AutoSteer not available - no active track";
                return;
            }
            IsAutoSteerEngaged = !IsAutoSteerEngaged;
            StatusMessage = IsAutoSteerEngaged ? "AutoSteer ENGAGED" : "AutoSteer disengaged";
        });

        // Field Commands
        CloseFieldCommand = new RelayCommand(() =>
        {
            CurrentFieldName = string.Empty;
            IsFieldOpen = false;
            SetCurrentBoundary(null);
            StatusMessage = "Field closed";
        });

        DriveInCommand = new RelayCommand(() =>
        {
            // Start a new field at current GPS position
            if (Latitude != 0 && Longitude != 0)
            {
                StatusMessage = "Drive-in field started";
            }
        });

        ResumeFieldCommand = new RelayCommand(() =>
        {
            var lastField = _settingsService.Settings.LastOpenedField;
            if (string.IsNullOrEmpty(lastField))
            {
                StatusMessage = "No previous field to resume";
                return;
            }

            // Get fields directory from settings (same pattern as ConfirmFieldSelectionDialogCommand)
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrEmpty(fieldsDir))
            {
                fieldsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }

            var fieldPath = Path.Combine(fieldsDir, lastField);

            if (!Directory.Exists(fieldPath))
            {
                StatusMessage = $"Field not found: {lastField}";
                return;
            }

            try
            {
                FieldsRootDirectory = fieldsDir;
                CurrentFieldName = lastField;
                IsFieldOpen = true;

                // Load field origin from Field.txt (for coordinate conversions)
                try
                {
                    var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                    if (fieldInfo.Origin != null)
                    {
                        _fieldOriginLatitude = fieldInfo.Origin.Latitude;
                        _fieldOriginLongitude = fieldInfo.Origin.Longitude;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Field] Could not load Field.txt origin: {ex.Message}");
                }

                // Load boundary from field (same pattern as ConfirmFieldSelectionDialogCommand)
                var boundary = _boundaryFileService.LoadBoundary(fieldPath);
                if (boundary != null)
                {
                    SetCurrentBoundary(boundary);
                    CenterMapOnBoundary(boundary);

                    // Debug: show boundary extents
                    if (boundary.OuterBoundary?.Points.Count > 0)
                    {
                        var pts = boundary.OuterBoundary.Points;
                        double minE = pts.Min(p => p.Easting);
                        double maxE = pts.Max(p => p.Easting);
                        double minN = pts.Min(p => p.Northing);
                        double maxN = pts.Max(p => p.Northing);
                        Console.WriteLine($"[Boundary] Extents (local): E({minE:F1} to {maxE:F1}), N({minN:F1} to {maxN:F1})");
                    }
                }

                // Load background image from field
                LoadBackgroundImage(fieldPath, boundary);

                // Set the active field so headland and other field-specific data loads
                var field = new Field
                {
                    Name = lastField,
                    DirectoryPath = fieldPath,
                    Boundary = boundary
                };
                _fieldService.SetActiveField(field);

                IsJobMenuPanelVisible = false;
                StatusMessage = $"Resumed field: {lastField}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load field: {ex.Message}";
            }
        });

        // Map Commands
        Toggle3DModeCommand = new RelayCommand(() =>
        {
            _mapService.Toggle3DMode();
            Is2DMode = !_mapService.Is3DMode;
        });

        ZoomInCommand = new RelayCommand(() =>
        {
            _mapService.Zoom(1.2);
            ZoomInRequested?.Invoke();
        });

        ZoomOutCommand = new RelayCommand(() =>
        {
            _mapService.Zoom(0.8);
            ZoomOutRequested?.Invoke();
        });

        // Boundary Recording Commands
        ToggleBoundaryPanelCommand = new RelayCommand(() =>
        {
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        StartBoundaryRecordingCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.StartRecording(BoundaryType.Outer);
            StatusMessage = "Boundary recording started";
        });

        PauseBoundaryRecordingCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.PauseRecording();
            IsBoundaryRecording = false;
            StatusMessage = "Boundary recording paused";
        });

        StopBoundaryRecordingCommand = new RelayCommand(() =>
        {
            var polygon = _boundaryRecordingService.StopRecording();

            if (polygon != null && polygon.Points.Count >= 3)
            {
                // Save to current field
                if (!string.IsNullOrEmpty(CurrentFieldName))
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();
                    boundary.OuterBoundary = polygon;
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);
                    SetCurrentBoundary(boundary);
                    RefreshBoundaryList();
                    StatusMessage = $"Boundary saved with {polygon.Points.Count} points, Area: {polygon.AreaHectares:F2} Ha";
                }
                else
                {
                    StatusMessage = "Cannot save boundary - no field is open";
                }
            }
            else
            {
                StatusMessage = "Boundary not saved - need at least 3 points";
            }

            // Hide the player panel
            IsBoundaryPlayerPanelVisible = false;
            IsBoundaryRecording = false;
        });

        ToggleRecordingCommand = new RelayCommand(() =>
        {
            if (IsBoundaryRecording)
            {
                _boundaryRecordingService.PauseRecording();
                IsBoundaryRecording = false;
                StatusMessage = "Recording paused";
            }
            else
            {
                _boundaryRecordingService.ResumeRecording();
                IsBoundaryRecording = true;
                StatusMessage = "Recording boundary - drive around the perimeter";
            }
        });

        UndoBoundaryPointCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.RemoveLastPoint();
        });

        ClearBoundaryCommand = new RelayCommand(() =>
        {
            _boundaryRecordingService.ClearPoints();
            StatusMessage = "Boundary cleared";
        });

        AddBoundaryPointCommand = new RelayCommand(() =>
        {
            double headingRadians = Heading * Math.PI / 180.0;
            var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(Easting, Northing, headingRadians);
            _boundaryRecordingService.AddPointManual(offsetEasting, offsetNorthing, headingRadians);
            StatusMessage = $"Point added ({_boundaryRecordingService.PointCount} total)";
        });

        ToggleBoundaryLeftRightCommand = new RelayCommand(() =>
        {
            IsDrawRightSide = !IsDrawRightSide;
        });

        ToggleBoundaryAntennaToolCommand = new RelayCommand(() =>
        {
            IsDrawAtPivot = !IsDrawAtPivot;
        });

        ShowBoundaryOffsetDialogCommand = new RelayCommand(() =>
        {
            // Show numeric input dialog for boundary offset
            NumericInputDialogTitle = "Boundary Offset (cm)";
            NumericInputDialogValue = (decimal)BoundaryOffset;
            NumericInputDialogDisplayText = BoundaryOffset.ToString("F0");
            NumericInputDialogIntegerOnly = true;
            NumericInputDialogAllowNegative = false;
            _numericInputDialogCallback = (value) =>
            {
                BoundaryOffset = value;
                StatusMessage = $"Boundary offset set to {BoundaryOffset:F0} cm";
            };
            State.UI.ShowDialog(DialogType.NumericInput);
        });

        CancelNumericInputDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            _numericInputDialogCallback = null;
        });

        ConfirmNumericInputDialogCommand = new RelayCommand(() =>
        {
            if (NumericInputDialogValue.HasValue && _numericInputDialogCallback != null)
            {
                _numericInputDialogCallback((double)NumericInputDialogValue.Value);
            }
            State.UI.CloseDialog();
            _numericInputDialogCallback = null;
        });

        DeleteBoundaryCommand = new RelayCommand(DeleteSelectedBoundary);

        ImportKmlBoundaryCommand = new AsyncRelayCommand(async () =>
        {
            // Must have a field open
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before importing a boundary";
                return;
            }

            var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
            var result = await _dialogService.ShowKmlImportDialogAsync(_settingsService.Settings.FieldsDirectory, fieldPath);

            if (result != null && result.BoundaryPoints.Count > 0)
            {
                try
                {
                    // Load existing boundary or create new one
                    var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

                    // Convert WGS84 boundary points to local coordinates
                    var origin = new Wgs84(result.CenterLatitude, result.CenterLongitude);
                    var sharedProps = new SharedFieldProperties();
                    var localPlane = new LocalPlane(origin, sharedProps);

                    var outerPolygon = new BoundaryPolygon();

                    foreach (var (lat, lon) in result.BoundaryPoints)
                    {
                        var wgs84 = new Wgs84(lat, lon);
                        var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                        outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                    }

                    boundary.OuterBoundary = outerPolygon;

                    // Save boundary
                    _boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // Update map
                    SetCurrentBoundary(boundary);

                    // Refresh the boundary list
                    RefreshBoundaryList();

                    StatusMessage = $"Boundary imported from KML ({outerPolygon.Points.Count} points)";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error importing KML boundary: {ex.Message}";
                }
            }
        });

        DrawMapBoundaryCommand = new RelayCommand(() =>
        {
            // Must have a field open
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first to add boundary";
                return;
            }

            // Use the shared panel-based dialog (works on iOS and Desktop)
            ShowBoundaryMapDialogCommand?.Execute(null);
        });

        // Keep Desktop-only async version for IDialogService integration
        DrawMapBoundaryDesktopCommand = new AsyncRelayCommand(async () =>
        {
            // Must have a field open
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first to add boundary";
                return;
            }

            var result = await _dialogService.ShowMapBoundaryDialogAsync(Latitude, Longitude);

            if (result != null && (result.BoundaryPoints.Count >= 3 || result.HasBackgroundImage))
            {
                try
                {
                    var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
                    LocalPlane? localPlane = null;

                    if (result.BoundaryPoints.Count >= 3)
                    {
                        // Calculate center of boundary points
                        double sumLat = 0, sumLon = 0;
                        foreach (var point in result.BoundaryPoints)
                        {
                            sumLat += point.Latitude;
                            sumLon += point.Longitude;
                        }
                        double centerLat = sumLat / result.BoundaryPoints.Count;
                        double centerLon = sumLon / result.BoundaryPoints.Count;

                        var origin = new Wgs84(centerLat, centerLon);
                        var sharedProps = new SharedFieldProperties();
                        localPlane = new LocalPlane(origin, sharedProps);
                    }
                    else if (result.HasBackgroundImage)
                    {
                        // Use background image center as origin
                        double centerLat = (result.NorthWestLat + result.SouthEastLat) / 2;
                        double centerLon = (result.NorthWestLon + result.SouthEastLon) / 2;

                        var origin = new Wgs84(centerLat, centerLon);
                        var sharedProps = new SharedFieldProperties();
                        localPlane = new LocalPlane(origin, sharedProps);
                    }

                    // Process boundary points if present
                    if (result.BoundaryPoints.Count >= 3 && localPlane != null)
                    {
                        var boundary = new Boundary();
                        var outerPolygon = new BoundaryPolygon();

                        foreach (var point in result.BoundaryPoints)
                        {
                            var wgs84 = new Wgs84(point.Latitude, point.Longitude);
                            var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                            outerPolygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                        }

                        boundary.OuterBoundary = outerPolygon;

                        // Save boundary
                        _boundaryFileService.SaveBoundary(boundary, fieldPath);

                        // Update Field.txt with the origin used for this boundary
                        double originLat = localPlane.Origin.Latitude;
                        double originLon = localPlane.Origin.Longitude;
                        _fieldOriginLatitude = originLat;
                        _fieldOriginLongitude = originLon;
                        try
                        {
                            var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);
                            fieldInfo.Origin = new Position { Latitude = originLat, Longitude = originLon };
                            _fieldPlaneFileService.SaveField(fieldInfo, fieldPath);
                            Console.WriteLine($"[MapBoundary] Updated Field.txt origin to ({originLat:F8}, {originLon:F8})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MapBoundary] Could not update Field.txt: {ex.Message}");
                        }

                        // Update map
                        SetCurrentBoundary(boundary);
                        CenterMapOnBoundary(boundary);

                        // Refresh the boundary list
                        RefreshBoundaryList();
                    }

                    // Process background image if present
                    if (result.HasBackgroundImage && !string.IsNullOrEmpty(result.BackgroundImagePath))
                    {
                        SaveBackgroundImage(result.BackgroundImagePath, fieldPath,
                            result.NorthWestLat, result.NorthWestLon,
                            result.SouthEastLat, result.SouthEastLon);
                    }

                    // Build status message
                    var msgParts = new System.Collections.Generic.List<string>();
                    if (result.BoundaryPoints.Count >= 3)
                        msgParts.Add($"boundary ({result.BoundaryPoints.Count} pts)");
                    if (result.HasBackgroundImage)
                        msgParts.Add("background image");

                    StatusMessage = $"Imported from satellite map: {string.Join(" + ", msgParts)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error importing: {ex.Message}";
                }
            }
        });

        BuildFromTracksCommand = new RelayCommand(() =>
        {
            StatusMessage = "Build boundary from tracks not yet implemented";
        });

        DriveAroundFieldCommand = new RelayCommand(() =>
        {
            // Must have a field open
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                StatusMessage = "Open a field first before recording a boundary";
                return;
            }

            // Hide boundary panel, show the player panel
            IsBoundaryPanelVisible = false;
            IsBoundaryPlayerPanelVisible = true;

            // Initialize recording service for a new boundary (paused state)
            _boundaryRecordingService.StartRecording(BoundaryType.Outer);
            _boundaryRecordingService.PauseRecording();

            StatusMessage = "Drive around the field boundary. Click Record to start.";
        });
    }

    private void CenterMapOnBoundary(Boundary boundary)
    {
        if (boundary.OuterBoundary?.Points == null || boundary.OuterBoundary.Points.Count == 0)
            return;

        double sumE = 0, sumN = 0;
        foreach (var point in boundary.OuterBoundary.Points)
        {
            sumE += point.Easting;
            sumN += point.Northing;
        }
        double centerE = sumE / boundary.OuterBoundary.Points.Count;
        double centerN = sumN / boundary.OuterBoundary.Points.Count;
        _mapService.PanTo(centerE, centerN);
    }

    /// <summary>
    /// Save background image and geo-reference file to field directory, then load it.
    /// </summary>
    private void SaveBackgroundImage(string sourcePath, string fieldPath, double nwLat, double nwLon, double seLat, double seLon)
    {
        // Copy image to field directory
        var destPath = Path.Combine(fieldPath, "BackPic.png");
        File.Copy(sourcePath, destPath, overwrite: true);

        // Save geo-reference file (WGS84 format)
        var geoContent = $"$BackPic\ntrue\n{nwLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n{nwLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n{seLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n{seLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var geoPath = Path.Combine(fieldPath, "BackPic.txt");
        File.WriteAllText(geoPath, geoContent);

        // Load through single method (applies Mapsui offset correction)
        LoadBackgroundImage(fieldPath, null);
    }

    private void LoadBackgroundImage(string fieldPath, Boundary? boundary)
    {
        try
        {
            var backPicPath = Path.Combine(fieldPath, "BackPic.png");
            var backPicGeoPath = Path.Combine(fieldPath, "BackPic.txt");

            if (!File.Exists(backPicPath) || !File.Exists(backPicGeoPath))
                return;

            // Read the geo-reference file
            // Format: $BackPic, true, nwLat, nwLon, seLat, seLon
            var lines = File.ReadAllLines(backPicGeoPath);
            if (lines.Length < 6 || lines[0] != "$BackPic")
                return;

            // Check if enabled
            if (!bool.TryParse(lines[1], out bool enabled) || !enabled)
                return;

            // Parse WGS84 bounds
            if (!double.TryParse(lines[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double nwLat) ||
                !double.TryParse(lines[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double nwLon) ||
                !double.TryParse(lines[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seLat) ||
                !double.TryParse(lines[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seLon))
                return;

            // Use field origin for LocalPlane (same origin used for boundary coordinates)
            // This ensures the background image aligns with the boundary
            var origin = new Wgs84(_fieldOriginLatitude, _fieldOriginLongitude);
            var sharedProps = new SharedFieldProperties();
            var localPlane = new LocalPlane(origin, sharedProps);

            // Convert WGS84 to local coordinates
            var nwWgs = new Wgs84(nwLat, nwLon);
            var seWgs = new Wgs84(seLat, seLon);
            var nwLocal = localPlane.ConvertWgs84ToGeoCoord(nwWgs);
            var seLocal = localPlane.ConvertWgs84ToGeoCoord(seWgs);

            // CORRECTION: Mapsui's viewport-to-world coordinate conversion has a ~150m northward offset
            // from actual tile rendering position. Shift image bounds 150m north to compensate.
            const double NorthingCorrectionMeters = 150.0;
            double correctedNwNorthing = nwLocal.Northing + NorthingCorrectionMeters;
            double correctedSeNorthing = seLocal.Northing + NorthingCorrectionMeters;

            // SetBackgroundImage expects: minX (west), maxY (north), maxX (east), minY (south)
            _mapService.SetBackgroundImage(backPicPath, nwLocal.Easting, correctedNwNorthing, seLocal.Easting, correctedSeNorthing);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadBackgroundImage] Error loading background image: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresh the boundary list from the current field's boundary file
    /// </summary>
    public void RefreshBoundaryList()
    {
        BoundaryItems.Clear();
        SelectedBoundaryIndex = -1;

        if (string.IsNullOrEmpty(CurrentFieldName)) return;

        var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
        var boundary = _boundaryFileService.LoadBoundary(fieldPath);

        if (boundary == null) return;

        int index = 0;

        // Add outer boundary if exists
        if (boundary.OuterBoundary != null && boundary.OuterBoundary.IsValid)
        {
            BoundaryItems.Add(new BoundaryListItem
            {
                Index = index++,
                BoundaryType = "Outer",
                AreaAcres = boundary.OuterBoundary.AreaAcres,
                IsDriveThrough = boundary.OuterBoundary.IsDriveThrough
            });
        }

        // Add inner boundaries
        for (int i = 0; i < boundary.InnerBoundaries.Count; i++)
        {
            var inner = boundary.InnerBoundaries[i];
            if (inner.IsValid)
            {
                BoundaryItems.Add(new BoundaryListItem
                {
                    Index = index++,
                    BoundaryType = $"Inner {i + 1}",
                    AreaAcres = inner.AreaAcres,
                    IsDriveThrough = inner.IsDriveThrough
                });
            }
        }
    }

    /// <summary>
    /// Delete the selected boundary from the field
    /// </summary>
    private void DeleteSelectedBoundary()
    {
        if (SelectedBoundaryIndex < 0)
        {
            StatusMessage = "Select a boundary to delete";
            return;
        }

        if (string.IsNullOrEmpty(CurrentFieldName))
        {
            StatusMessage = "No field open";
            return;
        }

        var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
        var boundary = _boundaryFileService.LoadBoundary(fieldPath);

        if (boundary == null) return;

        int currentIndex = 0;
        bool deleted = false;

        // Check if outer boundary is selected
        if (boundary.OuterBoundary != null && boundary.OuterBoundary.IsValid)
        {
            if (currentIndex == SelectedBoundaryIndex)
            {
                boundary.OuterBoundary = null;
                deleted = true;
            }
            currentIndex++;
        }

        // Check inner boundaries
        if (!deleted)
        {
            for (int i = 0; i < boundary.InnerBoundaries.Count; i++)
            {
                if (boundary.InnerBoundaries[i].IsValid)
                {
                    if (currentIndex == SelectedBoundaryIndex)
                    {
                        boundary.InnerBoundaries.RemoveAt(i);
                        deleted = true;
                        break;
                    }
                    currentIndex++;
                }
            }
        }

        if (deleted)
        {
            _boundaryFileService.SaveBoundary(boundary, fieldPath);
            RefreshBoundaryList();
            SetCurrentBoundary(boundary);
            StatusMessage = "Boundary deleted";
        }
    }

    /// <summary>
    /// Sets the boundary on both the map service and the ViewModel's CurrentBoundary property.
    /// </summary>
    private void SetCurrentBoundary(Boundary? boundary)
    {
        _mapService.SetBoundary(boundary);
        CurrentBoundary = boundary;
    }

    /// <summary>
    /// Populates the AvailableFields collection from the specified directory.
    /// </summary>
    private void PopulateAvailableFields(string fieldsDirectory)
    {
        AvailableFields.Clear();
        _fieldsSortedAZ = false;

        if (!Directory.Exists(fieldsDirectory))
        {
            Directory.CreateDirectory(fieldsDirectory);
            return;
        }

        foreach (var dirPath in Directory.GetDirectories(fieldsDirectory))
        {
            var fieldName = Path.GetFileName(dirPath);
            var item = new FieldSelectionItem
            {
                Name = fieldName,
                DirectoryPath = dirPath
            };

            // Try to load boundary to calculate area
            var boundary = _boundaryFileService.LoadBoundary(dirPath);
            if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid)
            {
                // Calculate area in hectares
                item.Area = boundary.OuterBoundary.AreaHectares;
                // Distance is not calculated - boundary points are in local coordinates
            }

            AvailableFields.Add(item);
        }
    }

    /// <summary>
    /// Populates the AvailableKmlFiles collection from the KML import directory.
    /// Looks in Documents/AgValoniaGPS/Import for KML/KMZ files.
    /// </summary>
    private void PopulateAvailableKmlFiles()
    {
        AvailableKmlFiles.Clear();

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(documentsPath))
        {
            documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        var importDir = Path.Combine(documentsPath, "AgValoniaGPS", "Import");

        if (!Directory.Exists(importDir))
        {
            Directory.CreateDirectory(importDir);
            return;
        }

        // Search for .kml and .kmz files
        var kmlFiles = Directory.GetFiles(importDir, "*.kml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(importDir, "*.kmz", SearchOption.AllDirectories));

        foreach (var filePath in kmlFiles)
        {
            var fileInfo = new FileInfo(filePath);
            AvailableKmlFiles.Add(new KmlFileItem
            {
                Name = fileInfo.Name,
                FullPath = filePath,
                ModifiedDate = fileInfo.LastWriteTime,
                FileSizeBytes = fileInfo.Length
            });
        }
    }

    /// <summary>
    /// Parses a KML file to extract boundary coordinates.
    /// </summary>
    private void ParseKmlFile(string filePath)
    {
        _kmlBoundaryPoints.Clear();
        KmlBoundaryPointCount = 0;
        KmlCenterLatitude = 0;
        KmlCenterLongitude = 0;

        try
        {
            string? coordinates = null;
            int startIndex;

            using var reader = new StreamReader(filePath);
            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line == null) continue;

                startIndex = line.IndexOf("<coordinates>");

                if (startIndex != -1)
                {
                    // Found start of coordinates block
                    while (true)
                    {
                        int endIndex = line.IndexOf("</coordinates>");

                        if (endIndex == -1)
                        {
                            if (startIndex == -1)
                                coordinates += " " + line.Substring(0);
                            else
                                coordinates += line.Substring(startIndex + 13);
                        }
                        else
                        {
                            if (startIndex == -1)
                                coordinates += " " + line.Substring(0, endIndex);
                            else
                                coordinates += line.Substring(startIndex + 13, endIndex - (startIndex + 13));
                            break;
                        }

                        line = reader.ReadLine();
                        if (line == null) break;
                        line = line.Trim();
                        startIndex = -1;
                    }

                    if (coordinates == null) continue;

                    // Parse coordinate pairs: format is "lon,lat,alt lon,lat,alt ..."
                    char[] delimiterChars = { ' ', '\t', '\r', '\n' };
                    string[] numberSets = coordinates.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);

                    if (numberSets.Length >= 3)
                    {
                        double sumLat = 0, sumLon = 0;
                        int validPoints = 0;

                        foreach (string item in numberSets)
                        {
                            if (item.Length < 3) continue;

                            string[] parts = item.Split(',');
                            if (parts.Length >= 2 &&
                                double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon) &&
                                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat))
                            {
                                _kmlBoundaryPoints.Add((lat, lon));
                                sumLat += lat;
                                sumLon += lon;
                                validPoints++;
                            }
                        }

                        if (validPoints > 0)
                        {
                            KmlCenterLatitude = sumLat / validPoints;
                            KmlCenterLongitude = sumLon / validPoints;
                        }

                        KmlBoundaryPointCount = validPoints;
                    }

                    // Only parse first coordinate block (outer boundary)
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error parsing KML: {ex.Message}";
        }
    }

    /// <summary>
    /// Populates the AvailableIsoXmlFiles collection from the ISO-XML import directory.
    /// Looks for TASKDATA.xml files in Documents/AgValoniaGPS/Import.
    /// </summary>
    private void PopulateAvailableIsoXmlFiles()
    {
        AvailableIsoXmlFiles.Clear();

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(documentsPath))
        {
            documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        var importDir = Path.Combine(documentsPath, "AgValoniaGPS", "Import");

        if (!Directory.Exists(importDir))
        {
            Directory.CreateDirectory(importDir);
            return;
        }

        // Search for directories containing TASKDATA.xml
        foreach (var dir in Directory.GetDirectories(importDir))
        {
            var taskDataFile = Path.Combine(dir, "TASKDATA.xml");
            if (File.Exists(taskDataFile))
            {
                var dirInfo = new DirectoryInfo(dir);
                AvailableIsoXmlFiles.Add(new IsoXmlFileItem
                {
                    Name = dirInfo.Name,
                    FullPath = dir,
                    ModifiedDate = dirInfo.LastWriteTime,
                    IsTaskData = true
                });
            }
        }
    }

    /// <summary>
    /// Build headland from the current field boundary using configured options
    /// </summary>
    private void BuildHeadlandFromBoundary()
    {
        if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
        {
            StatusMessage = "No field open";
            return;
        }

        var fieldsDir = _settingsService.Settings.FieldsDirectory;
        if (string.IsNullOrEmpty(fieldsDir))
        {
            fieldsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AgValoniaGPS", "Fields");
        }

        var fieldPath = Path.Combine(fieldsDir, CurrentFieldName);
        var boundary = _boundaryFileService.LoadBoundary(fieldPath);

        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            StatusMessage = "No valid boundary to create headland from";
            return;
        }

        var options = new Services.Headland.HeadlandBuildOptions
        {
            Distance = HeadlandDistance,
            Passes = HeadlandPasses,
            JoinType = IsHeadlandCurveMode
                ? Services.Geometry.OffsetJoinType.Round
                : Services.Geometry.OffsetJoinType.Miter,
            IncludeInnerBoundaries = true
        };

        System.Diagnostics.Debug.WriteLine($"[Headland] Boundary points: {boundary.OuterBoundary.Points.Count}, JoinType: {options.JoinType}");

        var result = _headlandBuilderService.BuildHeadland(boundary, options);

        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "Failed to build headland";
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[Headland] Result points: {result.OuterHeadlandLine?.Count ?? 0}");

        CurrentHeadlandLine = result.OuterHeadlandLine;
        HeadlandPreviewLine = null;
        HasHeadland = true;
        IsHeadlandOn = true;
        State.UI.CloseDialog();

        StatusMessage = $"Headland built at {HeadlandDistance:F1}m ({result.OuterHeadlandLine?.Count ?? 0} pts from {boundary.OuterBoundary.Points.Count} boundary pts)";
    }

    /// <summary>
    /// Update the headland preview line on the map
    /// </summary>
    private void UpdateHeadlandPreview()
    {
        if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
        {
            HeadlandPreviewLine = null;
            return;
        }

        var fieldsDir = _settingsService.Settings.FieldsDirectory;
        if (string.IsNullOrEmpty(fieldsDir))
        {
            fieldsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AgValoniaGPS", "Fields");
        }

        var fieldPath = Path.Combine(fieldsDir, CurrentFieldName);
        var boundary = _boundaryFileService.LoadBoundary(fieldPath);

        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            HeadlandPreviewLine = null;
            return;
        }

        // Get boundary points as Vec2
        var boundaryPoints = boundary.OuterBoundary.Points
            .Select(p => new Models.Base.Vec2(p.Easting, p.Northing))
            .ToList();

        // Determine join type based on curve/line mode
        var joinType = IsHeadlandCurveMode
            ? Services.Geometry.OffsetJoinType.Round
            : Services.Geometry.OffsetJoinType.Miter;

        // Create preview
        var preview = _headlandBuilderService.PreviewHeadland(boundaryPoints, HeadlandDistance, joinType);
        HeadlandPreviewLine = preview;
    }

    /// <summary>
    /// Handle a click on the headland map to select a point.
    /// In curve mode: snaps to headland line
    /// In line mode: snaps to outer boundary
    /// </summary>
    public void HandleHeadlandMapClick(double easting, double northing)
    {
        var boundary = CurrentBoundary;
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            Console.WriteLine($"[Headland] No valid boundary for point selection");
            return;
        }

        double nearestX = 0, nearestY = 0;
        int nearestSegmentIndex = -1;
        double nearestT = 0;
        int headlandSegmentIndex = -1;
        double headlandT = 0;

        if (IsHeadlandCurveMode)
        {
            // CURVE MODE: Snap to the headland line
            var headland = CurrentHeadlandLine ?? ConvertPreviewToVec3(HeadlandPreviewLine);
            if (headland == null || headland.Count < 3)
            {
                StatusMessage = "Build a headland first before selecting points in curve mode";
                return;
            }

            // Find nearest point on headland
            double minDistSq = double.MaxValue;
            for (int i = 0; i < headland.Count; i++)
            {
                var p1 = headland[i];
                var p2 = headland[(i + 1) % headland.Count];

                double segDx = p2.Easting - p1.Easting;
                double segDy = p2.Northing - p1.Northing;
                double segLenSq = segDx * segDx + segDy * segDy;

                double t = 0;
                if (segLenSq >= 1e-10)
                {
                    t = ((easting - p1.Easting) * segDx + (northing - p1.Northing) * segDy) / segLenSq;
                    t = Math.Clamp(t, 0, 1);
                }

                double closestX = p1.Easting + t * segDx;
                double closestY = p1.Northing + t * segDy;
                double dx = easting - closestX;
                double dy = northing - closestY;
                double distSq = dx * dx + dy * dy;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    headlandSegmentIndex = i;
                    headlandT = t;
                    nearestX = closestX;
                    nearestY = closestY;
                }
            }

            // Also set boundary index (same as headland since they correspond)
            nearestSegmentIndex = headlandSegmentIndex;
            nearestT = headlandT;

            Console.WriteLine($"[Headland] Curve mode - Clicked ({easting:F1}, {northing:F1}), nearest headland segment: {headlandSegmentIndex}, t: {headlandT:F2}, pos: ({nearestX:F1}, {nearestY:F1}), dist: {Math.Sqrt(minDistSq):F2}m");
        }
        else
        {
            // LINE MODE: Snap to outer boundary
            var points = boundary.OuterBoundary.Points;
            int count = points.Count;

            double minDistSq = double.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % count];

                double segDx = p2.Easting - p1.Easting;
                double segDy = p2.Northing - p1.Northing;
                double segLenSq = segDx * segDx + segDy * segDy;

                double t = 0;
                if (segLenSq >= 1e-10)
                {
                    t = ((easting - p1.Easting) * segDx + (northing - p1.Northing) * segDy) / segLenSq;
                    t = Math.Clamp(t, 0, 1);
                }

                double closestX = p1.Easting + t * segDx;
                double closestY = p1.Northing + t * segDy;
                double dx = easting - closestX;
                double dy = northing - closestY;
                double distSq = dx * dx + dy * dy;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearestSegmentIndex = i;
                    nearestT = t;
                    nearestX = closestX;
                    nearestY = closestY;
                }
            }

            Console.WriteLine($"[Headland] Line mode - Clicked ({easting:F1}, {northing:F1}), nearest boundary segment: {nearestSegmentIndex}, t: {nearestT:F2}, pos: ({nearestX:F1}, {nearestY:F1}), dist: {Math.Sqrt(minDistSq):F2}m");
        }

        var nearestPosition = new Models.Base.Vec2(nearestX, nearestY);

        // Store the point (first click = point 1, second click = point 2)
        if (HeadlandPoint1Index < 0)
        {
            HeadlandPoint1Index = nearestSegmentIndex;
            _headlandPoint1T = nearestT;
            _headlandPoint1Position = nearestPosition;
            if (IsHeadlandCurveMode)
            {
                _headlandCurvePoint1Index = headlandSegmentIndex;
                _headlandCurvePoint1T = headlandT;
                InvalidateClipPathCache();
            }
            StatusMessage = $"Point 1 selected. Click again to select Point 2.";
        }
        else if (HeadlandPoint2Index < 0)
        {
            // Check if points are too close (same position)
            if (_headlandPoint1Position.HasValue)
            {
                double dx = nearestX - _headlandPoint1Position.Value.Easting;
                double dy = nearestY - _headlandPoint1Position.Value.Northing;
                if (dx * dx + dy * dy < 1.0)  // Less than 1 meter apart
                {
                    StatusMessage = "Point 2 must be different from Point 1. Select a different location.";
                    return;
                }
            }
            HeadlandPoint2Index = nearestSegmentIndex;
            _headlandPoint2T = nearestT;
            _headlandPoint2Position = nearestPosition;
            if (IsHeadlandCurveMode)
            {
                _headlandCurvePoint2Index = headlandSegmentIndex;
                _headlandCurvePoint2T = headlandT;
                InvalidateClipPathCache();
            }
            StatusMessage = $"Point 2 selected. Click Clip to create headland line.";
        }
        else
        {
            // Reset and start over
            HeadlandPoint1Index = nearestSegmentIndex;
            _headlandPoint1T = nearestT;
            _headlandPoint1Position = nearestPosition;
            HeadlandPoint2Index = -1;
            _headlandPoint2T = 0;
            _headlandPoint2Position = null;
            if (IsHeadlandCurveMode)
            {
                _headlandCurvePoint1Index = headlandSegmentIndex;
                _headlandCurvePoint1T = headlandT;
                _headlandCurvePoint2Index = -1;
                _headlandCurvePoint2T = 0;
                InvalidateClipPathCache();
            }
            StatusMessage = $"Point 1 re-selected. Click again to select Point 2.";
        }

        // Update markers for visualization
        UpdateHeadlandSelectedMarkers();
    }

    /// <summary>
    /// Update the visual markers for selected headland points
    /// </summary>
    private void UpdateHeadlandSelectedMarkers()
    {
        var markers = new List<Models.Base.Vec2>();

        if (HeadlandPoint1Index >= 0 && _headlandPoint1Position.HasValue)
        {
            markers.Add(_headlandPoint1Position.Value);
        }

        if (HeadlandPoint2Index >= 0 && _headlandPoint2Position.HasValue)
        {
            markers.Add(_headlandPoint2Position.Value);
        }

        HeadlandSelectedMarkers = markers.Count > 0 ? markers : null;

        // Also notify that HeadlandClipPath may have changed (it's computed from curve mode indices)
        this.RaisePropertyChanged(nameof(HeadlandClipPath));
    }

    /// <summary>
    /// Clear the selected headland points
    /// </summary>
    public void ClearHeadlandPointSelection()
    {
        HeadlandPoint1Index = -1;
        _headlandPoint1T = 0;
        _headlandPoint1Position = null;
        HeadlandPoint2Index = -1;
        _headlandPoint2T = 0;
        _headlandPoint2Position = null;
        HeadlandSelectedMarkers = null;
        // Also clear curve mode fields
        _headlandCurvePoint1Index = -1;
        _headlandCurvePoint1T = 0;
        _headlandCurvePoint2Index = -1;
        _headlandCurvePoint2T = 0;
        InvalidateClipPathCache();
    }

    /// <summary>
    /// Create a headland line from the selected boundary segment
    /// </summary>
    private void CreateHeadlandFromSelectedPoints(Boundary boundary)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            StatusMessage = "No valid boundary";
            return;
        }

        if (_headlandPoint1Position == null || _headlandPoint2Position == null)
        {
            StatusMessage = "Invalid point selection";
            return;
        }

        var points = boundary.OuterBoundary.Points;
        int count = points.Count;

        // We have segment indices and t values for both points
        // Now extract the boundary path between them, including the interpolated endpoints
        int seg1 = HeadlandPoint1Index;
        double t1 = _headlandPoint1T;
        int seg2 = HeadlandPoint2Index;
        double t2 = _headlandPoint2T;

        // Build segment points list - we need to traverse from point1 to point2
        // Try both directions and take the shorter path
        var forwardPath = ExtractBoundaryPath(points, seg1, t1, seg2, t2, true);
        var backwardPath = ExtractBoundaryPath(points, seg1, t1, seg2, t2, false);

        // Calculate path lengths
        double forwardLen = CalculatePathLength(forwardPath);
        double backwardLen = CalculatePathLength(backwardPath);

        var segmentPoints = forwardLen <= backwardLen ? forwardPath : backwardPath;

        Console.WriteLine($"[Headland] Creating headland from {segmentPoints.Count} boundary points (forward: {forwardPath.Count}, backward: {backwardPath.Count}), distance: {HeadlandDistance:F1}m");

        if (segmentPoints.Count < 2)
        {
            StatusMessage = "Not enough points in segment";
            return;
        }

        // Determine the offset direction (inward = toward center of boundary)
        // Calculate boundary centroid to determine offset direction
        double centerX = 0, centerY = 0;
        foreach (var pt in points)
        {
            centerX += pt.Easting;
            centerY += pt.Northing;
        }
        centerX /= count;
        centerY /= count;

        // Check if the midpoint of the segment offset needs to go toward or away from center
        var midPt = segmentPoints[segmentPoints.Count / 2];
        double dx = centerX - midPt.Easting;
        double dy = centerY - midPt.Northing;

        // Calculate perpendicular direction of segment at midpoint
        int midIdx = segmentPoints.Count / 2;
        var prevPt = segmentPoints[System.Math.Max(0, midIdx - 1)];
        var nextPt = segmentPoints[System.Math.Min(segmentPoints.Count - 1, midIdx + 1)];
        double segDx = nextPt.Easting - prevPt.Easting;
        double segDy = nextPt.Northing - prevPt.Northing;

        // Perpendicular to segment (90 deg clockwise): (segDy, -segDx)
        // Dot product with center direction determines offset sign
        double dotProduct = dx * segDy + dy * (-segDx);
        double offsetDistance = dotProduct > 0 ? HeadlandDistance : -HeadlandDistance;

        // Get join type
        var joinType = IsHeadlandCurveMode
            ? Services.Geometry.OffsetJoinType.Round
            : Services.Geometry.OffsetJoinType.Miter;

        // Create the offset line - use simple perpendicular offset for each point
        var headlandPoints = new List<Models.Base.Vec2>();
        for (int i = 0; i < segmentPoints.Count; i++)
        {
            // Get direction at this point
            Models.Base.Vec2 dir;
            if (i == 0)
            {
                dir = new Models.Base.Vec2(
                    segmentPoints[1].Easting - segmentPoints[0].Easting,
                    segmentPoints[1].Northing - segmentPoints[0].Northing);
            }
            else if (i == segmentPoints.Count - 1)
            {
                dir = new Models.Base.Vec2(
                    segmentPoints[i].Easting - segmentPoints[i - 1].Easting,
                    segmentPoints[i].Northing - segmentPoints[i - 1].Northing);
            }
            else
            {
                dir = new Models.Base.Vec2(
                    segmentPoints[i + 1].Easting - segmentPoints[i - 1].Easting,
                    segmentPoints[i + 1].Northing - segmentPoints[i - 1].Northing);
            }

            // Normalize
            double len = System.Math.Sqrt(dir.Easting * dir.Easting + dir.Northing * dir.Northing);
            if (len > 1e-10)
            {
                dir = new Models.Base.Vec2(dir.Easting / len, dir.Northing / len);
            }

            // Perpendicular offset (rotate 90 degrees clockwise for positive offset)
            double perpX = dir.Northing * offsetDistance;
            double perpY = -dir.Easting * offsetDistance;

            headlandPoints.Add(new Models.Base.Vec2(
                segmentPoints[i].Easting + perpX,
                segmentPoints[i].Northing + perpY));
        }

        Console.WriteLine($"[Headland] Created {headlandPoints.Count} headland points");

        // Convert to Vec3 with headings
        var headlandWithHeadings = new List<Models.Base.Vec3>();
        for (int i = 0; i < headlandPoints.Count; i++)
        {
            // Calculate heading from direction between adjacent points
            double heading;
            if (headlandPoints.Count < 2)
            {
                heading = 0;
            }
            else if (i == 0)
            {
                double hdx = headlandPoints[1].Easting - headlandPoints[0].Easting;
                double hdy = headlandPoints[1].Northing - headlandPoints[0].Northing;
                heading = System.Math.Atan2(hdx, hdy);
            }
            else if (i == headlandPoints.Count - 1)
            {
                double hdx = headlandPoints[i].Easting - headlandPoints[i - 1].Easting;
                double hdy = headlandPoints[i].Northing - headlandPoints[i - 1].Northing;
                heading = System.Math.Atan2(hdx, hdy);
            }
            else
            {
                double hdx = headlandPoints[i + 1].Easting - headlandPoints[i - 1].Easting;
                double hdy = headlandPoints[i + 1].Northing - headlandPoints[i - 1].Northing;
                heading = System.Math.Atan2(hdx, hdy);
            }
            headlandWithHeadings.Add(new Models.Base.Vec3(
                headlandPoints[i].Easting,
                headlandPoints[i].Northing,
                heading));
        }

        // Set the headland line
        CurrentHeadlandLine = headlandWithHeadings;
        HasHeadland = true;
        IsHeadlandOn = true;

        // Clear selection
        ClearHeadlandPointSelection();

        StatusMessage = $"Headland created with {headlandWithHeadings.Count} points";
    }

    /// <summary>
    /// Extract a path along the boundary between two points (each specified by segment index + t parameter)
    /// </summary>
    private List<Models.Base.Vec2> ExtractBoundaryPath(
        IReadOnlyList<BoundaryPoint> points,
        int seg1, double t1,
        int seg2, double t2,
        bool forward)
    {
        var result = new List<Models.Base.Vec2>();
        int count = points.Count;

        // Helper to interpolate a point on a segment
        Models.Base.Vec2 Interpolate(int segIdx, double t)
        {
            var p1 = points[segIdx];
            var p2 = points[(segIdx + 1) % count];
            return new Models.Base.Vec2(
                p1.Easting + t * (p2.Easting - p1.Easting),
                p1.Northing + t * (p2.Northing - p1.Northing));
        }

        // Add the start point (point1)
        result.Add(Interpolate(seg1, t1));

        if (forward)
        {
            // Forward: go from seg1 toward seg2 in increasing index order
            if (seg1 == seg2)
            {
                // Both points on same segment
                if (t2 > t1)
                {
                    // Already have start point, just add end point
                    result.Add(Interpolate(seg2, t2));
                }
                else
                {
                    // Need to go all the way around (very rare case)
                    for (int i = seg1 + 1; i < count; i++)
                        result.Add(new Models.Base.Vec2(points[i].Easting, points[i].Northing));
                    for (int i = 0; i <= seg2; i++)
                        result.Add(new Models.Base.Vec2(points[i].Easting, points[i].Northing));
                    result.Add(Interpolate(seg2, t2));
                }
            }
            else
            {
                // Different segments - traverse forward from seg1 to seg2
                int current = seg1;
                while (current != seg2)
                {
                    current = (current + 1) % count;
                    result.Add(new Models.Base.Vec2(points[current].Easting, points[current].Northing));
                }
                // Replace last vertex with interpolated endpoint if t2 > 0
                if (t2 > 0)
                {
                    result[result.Count - 1] = Interpolate(seg2, t2);
                }
            }
        }
        else
        {
            // Backward: go from seg1 toward seg2 in decreasing index order
            if (seg1 == seg2)
            {
                // Both points on same segment
                if (t2 < t1)
                {
                    // Already have start point, just add end point
                    result.Add(Interpolate(seg2, t2));
                }
                else
                {
                    // Need to go all the way around backward
                    for (int i = seg1; i >= 0; i--)
                        result.Add(new Models.Base.Vec2(points[i].Easting, points[i].Northing));
                    for (int i = count - 1; i > seg2; i--)
                        result.Add(new Models.Base.Vec2(points[i].Easting, points[i].Northing));
                    result.Add(Interpolate(seg2, t2));
                }
            }
            else
            {
                // Different segments - traverse backward from seg1 to seg2
                int current = seg1;
                // First add the start vertex of seg1
                result.Add(new Models.Base.Vec2(points[current].Easting, points[current].Northing));

                while (current != seg2)
                {
                    current = (current - 1 + count) % count;
                    result.Add(new Models.Base.Vec2(points[current].Easting, points[current].Northing));
                }
                // Add the interpolated endpoint
                result.Add(Interpolate(seg2, t2));
            }
        }

        return result;
    }

    /// <summary>
    /// Calculate heading (in degrees) from point A to point B using Easting/Northing
    /// </summary>
    private double CalculateHeading(Position pointA, Position pointB)
    {
        double dx = pointB.Easting - pointA.Easting;
        double dy = pointB.Northing - pointA.Northing;
        double headingRadians = System.Math.Atan2(dx, dy); // atan2(east, north) for navigation heading
        double headingDegrees = headingRadians * 180.0 / System.Math.PI;
        if (headingDegrees < 0) headingDegrees += 360.0;
        return headingDegrees;
    }

    /// <summary>
    /// Calculate the total length of a path
    /// </summary>
    private double CalculatePathLength(List<Models.Base.Vec2> path)
    {
        double length = 0;
        for (int i = 1; i < path.Count; i++)
        {
            double dx = path[i].Easting - path[i - 1].Easting;
            double dy = path[i].Northing - path[i - 1].Northing;
            length += System.Math.Sqrt(dx * dx + dy * dy);
        }
        return length;
    }

    /// <summary>
    /// Convert Vec2 preview line to Vec3 with calculated headings
    /// </summary>
    private List<Models.Base.Vec3>? ConvertPreviewToVec3(List<Models.Base.Vec2>? preview)
    {
        if (preview == null || preview.Count < 3) return null;

        var result = new List<Models.Base.Vec3>(preview.Count);
        for (int i = 0; i < preview.Count; i++)
        {
            double heading;
            if (i == 0)
            {
                double dx = preview[1].Easting - preview[0].Easting;
                double dy = preview[1].Northing - preview[0].Northing;
                heading = System.Math.Atan2(dx, dy);
            }
            else if (i == preview.Count - 1)
            {
                double dx = preview[i].Easting - preview[i - 1].Easting;
                double dy = preview[i].Northing - preview[i - 1].Northing;
                heading = System.Math.Atan2(dx, dy);
            }
            else
            {
                double dx = preview[i + 1].Easting - preview[i - 1].Easting;
                double dy = preview[i + 1].Northing - preview[i - 1].Northing;
                heading = System.Math.Atan2(dx, dy);
            }
            result.Add(new Models.Base.Vec3(preview[i].Easting, preview[i].Northing, heading));
        }
        return result;
    }

    /// <summary>
    /// Clip the headland polygon at the line defined by the two selected points.
    /// The result is an open polyline (not closed polygon).
    /// </summary>
    private void ClipHeadlandAtLine(List<Models.Base.Vec3> headland)
    {
        if (_headlandPoint1Position == null || _headlandPoint2Position == null)
        {
            StatusMessage = "No clip line defined";
            return;
        }

        var clipStart = _headlandPoint1Position.Value;
        var clipEnd = _headlandPoint2Position.Value;

        Console.WriteLine($"[Headland] Clipping at line from ({clipStart.Easting:F1}, {clipStart.Northing:F1}) to ({clipEnd.Easting:F1}, {clipEnd.Northing:F1})");

        // Find where the clip line intersects the headland polygon
        var intersections = new List<(int segmentIndex, double t, Models.Base.Vec2 point)>();

        for (int i = 0; i < headland.Count; i++)
        {
            int nextI = (i + 1) % headland.Count;
            var p1 = new Models.Base.Vec2(headland[i].Easting, headland[i].Northing);
            var p2 = new Models.Base.Vec2(headland[nextI].Easting, headland[nextI].Northing);

            // Find intersection of segment (p1, p2) with infinite line through (clipStart, clipEnd)
            if (LineSegmentIntersectsLine(p1, p2, clipStart, clipEnd, out double t, out var intersectPoint))
            {
                intersections.Add((i, t, intersectPoint));
            }
        }

        Console.WriteLine($"[Headland] Found {intersections.Count} intersections with clip line");

        if (intersections.Count < 2)
        {
            StatusMessage = "Clip line doesn't cross headland properly";
            return;
        }

        // Sort intersections by segment index, then t
        intersections.Sort((a, b) =>
        {
            int cmp = a.segmentIndex.CompareTo(b.segmentIndex);
            return cmp != 0 ? cmp : a.t.CompareTo(b.t);
        });

        // Take first two intersections - these define where to cut
        var cut1 = intersections[0];
        var cut2 = intersections[1];

        // Build both possible paths (forward and backward around the polygon)
        var forwardPath = BuildClipPath(headland, cut1, cut2, true);
        var backwardPath = BuildClipPath(headland, cut1, cut2, false);

        // Choose path based on mode:
        // - Curve mode (left button): take the LONGER path (follows the curve around)
        // - Line mode (right button): take the SHORTER path (direct cut)
        List<Models.Base.Vec3> clippedHeadland;
        if (IsHeadlandCurveMode)
        {
            // Curve mode: take the longer path
            clippedHeadland = forwardPath.Count >= backwardPath.Count ? forwardPath : backwardPath;
            Console.WriteLine($"[Headland] Curve mode: taking longer path ({clippedHeadland.Count} points)");
        }
        else
        {
            // Line mode: take the shorter path
            clippedHeadland = forwardPath.Count <= backwardPath.Count ? forwardPath : backwardPath;
            Console.WriteLine($"[Headland] Line mode: taking shorter path ({clippedHeadland.Count} points)");
        }

        // Recalculate headings for the clipped line
        for (int i = 0; i < clippedHeadland.Count; i++)
        {
            double heading;
            if (clippedHeadland.Count < 2)
            {
                heading = 0;
            }
            else if (i == 0)
            {
                double dx = clippedHeadland[1].Easting - clippedHeadland[0].Easting;
                double dy = clippedHeadland[1].Northing - clippedHeadland[0].Northing;
                heading = System.Math.Atan2(dx, dy);
            }
            else if (i == clippedHeadland.Count - 1)
            {
                double dx = clippedHeadland[i].Easting - clippedHeadland[i - 1].Easting;
                double dy = clippedHeadland[i].Northing - clippedHeadland[i - 1].Northing;
                heading = System.Math.Atan2(dx, dy);
            }
            else
            {
                double dx = clippedHeadland[i + 1].Easting - clippedHeadland[i - 1].Easting;
                double dy = clippedHeadland[i + 1].Northing - clippedHeadland[i - 1].Northing;
                heading = System.Math.Atan2(dx, dy);
            }
            clippedHeadland[i] = new Models.Base.Vec3(clippedHeadland[i].Easting, clippedHeadland[i].Northing, heading);
        }

        Console.WriteLine($"[Headland] Clipped headland has {clippedHeadland.Count} points");

        // Set the clipped headland
        CurrentHeadlandLine = clippedHeadland;
        HeadlandPreviewLine = null;
        HasHeadland = true;
        IsHeadlandOn = true;

        // Clear selection
        ClearHeadlandPointSelection();

        StatusMessage = $"Headland clipped with {clippedHeadland.Count} points";
    }

    /// <summary>
    /// Check if line segment (p1, p2) intersects the infinite line through (lineA, lineB)
    /// Returns the t parameter (0-1) along the segment and the intersection point
    /// </summary>
    private bool LineSegmentIntersectsLine(
        Models.Base.Vec2 p1, Models.Base.Vec2 p2,
        Models.Base.Vec2 lineA, Models.Base.Vec2 lineB,
        out double t, out Models.Base.Vec2 intersection)
    {
        t = 0;
        intersection = new Models.Base.Vec2(0, 0);

        // Segment direction
        double dx = p2.Easting - p1.Easting;
        double dy = p2.Northing - p1.Northing;

        // Line direction
        double ldx = lineB.Easting - lineA.Easting;
        double ldy = lineB.Northing - lineA.Northing;

        // Cross product of directions
        double cross = dx * ldy - dy * ldx;

        if (System.Math.Abs(cross) < 1e-10)
        {
            // Parallel lines
            return false;
        }

        // Vector from line start to segment start
        double qpx = p1.Easting - lineA.Easting;
        double qpy = p1.Northing - lineA.Northing;

        // Calculate t (parameter along segment)
        t = (qpx * ldy - qpy * ldx) / (-cross);

        // Only accept if intersection is on the segment (0 <= t <= 1)
        if (t < 0 || t > 1)
        {
            return false;
        }

        // Calculate intersection point
        intersection = new Models.Base.Vec2(
            p1.Easting + t * dx,
            p1.Northing + t * dy);

        return true;
    }

    /// <summary>
    /// Build a path along the headland polygon between two cut points.
    /// </summary>
    /// <param name="headland">The headland polygon points</param>
    /// <param name="cut1">First cut point (segment index, t parameter, intersection point)</param>
    /// <param name="cut2">Second cut point (segment index, t parameter, intersection point)</param>
    /// <param name="forward">If true, traverse forward from cut1 to cut2; if false, traverse backward</param>
    /// <returns>List of points forming the path between cut points</returns>
    private List<Models.Base.Vec3> BuildClipPath(
        List<Models.Base.Vec3> headland,
        (int segmentIndex, double t, Models.Base.Vec2 point) cut1,
        (int segmentIndex, double t, Models.Base.Vec2 point) cut2,
        bool forward)
    {
        var path = new List<Models.Base.Vec3>();
        int n = headland.Count;

        // Start with cut1 intersection point
        path.Add(new Models.Base.Vec3(cut1.point.Easting, cut1.point.Northing, 0));

        if (forward)
        {
            // Forward: go from cut1 to cut2 in increasing index order
            // Start at the vertex after cut1's segment
            int startVertex = (cut1.segmentIndex + 1) % n;

            // End at the vertex at or before cut2's segment
            // If cut2 is on the segment from vertex i to i+1, we include vertices up to i
            int endVertex = cut2.segmentIndex;

            // Handle wrap-around
            int current = startVertex;
            int iterations = 0;
            int maxIterations = n + 1; // Safety limit

            while (current != (endVertex + 1) % n && iterations < maxIterations)
            {
                path.Add(headland[current]);
                current = (current + 1) % n;
                iterations++;

                // If we've gone all the way around, break
                if (current == startVertex && iterations > 0)
                    break;
            }
        }
        else
        {
            // Backward: go from cut1 to cut2 in decreasing index order
            // Start at the vertex at cut1's segment (the start of that segment)
            int startVertex = cut1.segmentIndex;

            // End at the vertex after cut2's segment
            int endVertex = (cut2.segmentIndex + 1) % n;

            // Handle wrap-around going backward
            int current = startVertex;
            int iterations = 0;
            int maxIterations = n + 1; // Safety limit

            while (current != (endVertex - 1 + n) % n && iterations < maxIterations)
            {
                path.Add(headland[current]);
                current = (current - 1 + n) % n;
                iterations++;

                // If we've gone all the way around, break
                if (current == startVertex && iterations > 0)
                    break;
            }
        }

        // End with cut2 intersection point
        path.Add(new Models.Base.Vec3(cut2.point.Easting, cut2.point.Northing, 0));

        Console.WriteLine($"[Headland] BuildClipPath(forward={forward}): {path.Count} points, cut1 seg={cut1.segmentIndex}, cut2 seg={cut2.segmentIndex}");

        return path;
    }

    /// <summary>
    /// Save headland line to file in the active field directory
    /// </summary>
    private void SaveHeadlandToFile(List<Models.Base.Vec3>? headlandPoints)
    {
        var activeField = _fieldService.ActiveField;
        if (activeField == null || string.IsNullOrEmpty(activeField.DirectoryPath))
        {
            return; // No active field to save to
        }

        try
        {
            var headlandLine = new Models.Guidance.HeadlandLine();

            if (headlandPoints != null && headlandPoints.Count > 0)
            {
                var headlandPath = new Models.Guidance.HeadlandPath
                {
                    Name = "Headland",
                    TrackPoints = headlandPoints,
                    MoveDistance = HeadlandDistance,
                    Mode = 0,
                    APointIndex = 0
                };
                headlandLine.Tracks.Add(headlandPath);
            }

            HeadlandLineSerializer.Save(activeField.DirectoryPath, headlandLine);
            Console.WriteLine($"[Headland] Saved headland to {activeField.DirectoryPath} ({headlandPoints?.Count ?? 0} points)");
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[Headland] Failed to save headland: {ex.Message}");
        }
    }

    /// <summary>
    /// Save tracks to TrackLines.txt in the active field directory.
    /// Uses WinForms-compatible format via TrackFilesService.
    /// </summary>
    private void SaveTracksToFile()
    {
        var activeField = _fieldService.ActiveField;
        if (activeField == null || string.IsNullOrEmpty(activeField.DirectoryPath))
        {
            return; // No active field to save to
        }

        try
        {
            Services.TrackFilesService.SaveTracks(activeField.DirectoryPath, SavedTracks.ToList());
            Console.WriteLine($"[TrackFiles] Saved {SavedTracks.Count} tracks to TrackLines.txt");
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[TrackFiles] Failed to save tracks: {ex.Message}");
        }
    }

    /// <summary>
    /// Load tracks from field directory.
    /// Supports WinForms TrackLines.txt format (primary) and legacy ABLines.txt format (fallback).
    /// </summary>
    private void LoadTracksFromField(Field? field)
    {
        // Clear existing tracks from both state and legacy collection
        State.Field.Tracks.Clear();
        SavedTracks.Clear();

        if (field == null || string.IsNullOrEmpty(field.DirectoryPath))
        {
            Console.WriteLine("[TrackFiles] No field directory to load from");
            return;
        }

        try
        {
            // Try TrackLines.txt first (WinForms format)
            if (Services.TrackFilesService.Exists(field.DirectoryPath))
            {
                var tracks = Services.TrackFilesService.LoadTracks(field.DirectoryPath);
                int loadedCount = 0;

                foreach (var track in tracks)
                {
                    // First track is active by default
                    if (loadedCount == 0)
                    {
                        track.IsActive = true;
                        State.Field.ActiveTrack = track;
                    }
                    State.Field.Tracks.Add(track);
                    SavedTracks.Add(track);
                    loadedCount++;
                }

                Console.WriteLine($"[TrackFiles] Loaded {loadedCount} tracks from TrackLines.txt");

                if (loadedCount > 0)
                {
                    HasActiveTrack = true;
                    IsAutoSteerAvailable = true;
                }
                return;
            }

            // Fallback to legacy ABLines.txt format
            var legacyFilePath = System.IO.Path.Combine(field.DirectoryPath, "ABLines.txt");
            if (System.IO.File.Exists(legacyFilePath))
            {
                Console.WriteLine($"[TrackFiles] TrackLines.txt not found, trying legacy ABLines.txt");
                var lines = System.IO.File.ReadAllLines(legacyFilePath);
                int loadedCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 4)
                    {
                        // Parse legacy: Name,Heading,PointA_Easting,PointA_Northing[,PointB_Easting,PointB_Northing]
                        var name = parts[0];
                        if (double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var heading) &&
                            double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var eastingA) &&
                            double.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var northingA))
                        {
                            double eastingB, northingB;

                            if (parts.Length >= 6 &&
                                double.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out eastingB) &&
                                double.TryParse(parts[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out northingB))
                            {
                                // Use stored Point B
                            }
                            else
                            {
                                // Calculate Point B from Point A and heading
                                var headingRad = heading * Math.PI / 180.0;
                                var lineLength = 100.0;
                                eastingB = eastingA + Math.Sin(headingRad) * lineLength;
                                northingB = northingA + Math.Cos(headingRad) * lineLength;
                            }

                            var headingRadians = heading * Math.PI / 180.0;
                            var track = Track.FromABLine(
                                name,
                                new Vec3(eastingA, northingA, headingRadians),
                                new Vec3(eastingB, northingB, headingRadians));
                            track.IsActive = loadedCount == 0;

                            if (loadedCount == 0)
                            {
                                State.Field.ActiveTrack = track;
                            }
                            State.Field.Tracks.Add(track);
                            SavedTracks.Add(track);
                            loadedCount++;
                        }
                    }
                }

                Console.WriteLine($"[TrackFiles] Loaded {loadedCount} tracks from legacy ABLines.txt");

                if (loadedCount > 0)
                {
                    HasActiveTrack = true;
                    IsAutoSteerAvailable = true;
                }
            }
            else
            {
                Console.WriteLine($"[TrackFiles] No track files found in {field.DirectoryPath}");
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[TrackFiles] Failed to load tracks: {ex.Message}");
        }
    }
}

/// <summary>
/// View model item for boundary list display
/// </summary>
public class BoundaryListItem
{
    public int Index { get; set; }
    public string BoundaryType { get; set; } = string.Empty;
    public double AreaAcres { get; set; }
    public bool IsDriveThrough { get; set; }
    public string AreaDisplay => $"{AreaAcres:F2} Ac";
    public string DriveThruDisplay => IsDriveThrough ? "Yes" : "--";
}

/// <summary>
/// View model item for field selection list display
/// </summary>
public class FieldSelectionItem
{
    public string Name { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public double Distance { get; set; }
    public double Area { get; set; }
}

/// <summary>
/// View model item for KML file list display
/// </summary>
public class KmlFileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime ModifiedDate { get; set; }
    public long FileSizeBytes { get; set; }
    public string FileSizeDisplay => FileSizeBytes < 1024 ? $"{FileSizeBytes} B" :
                                     FileSizeBytes < 1024 * 1024 ? $"{FileSizeBytes / 1024.0:F1} KB" :
                                     $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
}

/// <summary>
/// View model item for ISO-XML file/folder list display
/// </summary>
public class IsoXmlFileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime ModifiedDate { get; set; }
    public bool IsTaskData { get; set; }
}