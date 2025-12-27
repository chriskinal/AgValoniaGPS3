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
using AgValoniaGPS.Models.Communication;
using Avalonia.Threading;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel : ReactiveObject
{
    private readonly IUdpCommunicationService _udpService;
    private readonly AgValoniaGPS.Services.Interfaces.IGpsService _gpsService;
    private readonly IFieldService _fieldService;
    private readonly INtripClientService _ntripService;
    private readonly AgValoniaGPS.Services.Interfaces.IDisplaySettingsService _displaySettings;
    private readonly AgValoniaGPS.Services.Interfaces.IFieldStatisticsService _fieldStatistics;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IMapService _mapService;
    private readonly IBoundaryRecordingService _boundaryRecordingService;
    private readonly BoundaryFileService _boundaryFileService;
    private readonly NmeaParserService _nmeaParser;
    private readonly Services.Headland.IHeadlandBuilderService _headlandBuilderService;
    private readonly ITrackGuidanceService _trackGuidanceService;
    private readonly Services.Geometry.IPolygonOffsetService _polygonOffsetService;
    private readonly Services.Interfaces.ITurnAreaService _turnAreaService;
    private readonly FieldPlaneFileService _fieldPlaneFileService;
    private readonly IVehicleProfileService _vehicleProfileService;
    private readonly IConfigurationService _configurationService;
    private readonly IAutoSteerService _autoSteerService;
    private readonly IModuleCommunicationService _moduleCommunicationService;
    private readonly ApplicationState _appState;

    /// <summary>
    /// Simulator ViewModel - exposes simulator UI state and commands.
    /// Use this for XAML bindings to simulator controls.
    /// </summary>
    public SimulatorViewModel Simulator { get; }

    /// <summary>
    /// YouTurn ViewModel - handles U-turn path creation, triggering, and guidance.
    /// Use this for YouTurn-related functionality.
    /// </summary>
    public YouTurnViewModel YouTurn { get; }

    /// <summary>
    /// Boundary Recording ViewModel - handles boundary recording state and settings.
    /// Use this for boundary recording functionality.
    /// </summary>
    public BoundaryRecordingViewModel BoundaryRecording { get; }

    /// <summary>
    /// Section Control ViewModel - handles section master state and individual section control.
    /// Use this for section control functionality.
    /// </summary>
    public SectionControlViewModel Sections { get; }

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

    // Note: Section fields (_activeSections, _section1Active-_section7Active) moved to SectionControlViewModel

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
        IModuleCommunicationService moduleCommunicationService,
        ApplicationState appState)
    {
        _udpService = udpService;
        _gpsService = gpsService;
        _fieldService = fieldService;
        _ntripService = ntripService;
        _displaySettings = displaySettings;
        _fieldStatistics = fieldStatistics;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _mapService = mapService;
        _boundaryRecordingService = boundaryRecordingService;
        _boundaryFileService = boundaryFileService;
        _headlandBuilderService = headlandBuilderService;
        _trackGuidanceService = trackGuidanceService;
        _polygonOffsetService = polygonOffsetService;
        _turnAreaService = turnAreaService;
        _vehicleProfileService = vehicleProfileService;
        _configurationService = configurationService;
        _autoSteerService = autoSteerService;
        _moduleCommunicationService = moduleCommunicationService;
        _appState = appState;
        _nmeaParser = new NmeaParserService(gpsService);
        _fieldPlaneFileService = new FieldPlaneFileService();

        // Create SimulatorViewModel (handles simulator UI and commands)
        Simulator = new SimulatorViewModel(simulatorService, settingsService, appState);
        Simulator.SimulatedGpsDataReceived += OnSimulatorGpsDataReceived;

        // Create YouTurnViewModel (handles U-turn path creation, triggering, and guidance)
        YouTurn = new YouTurnViewModel(
            youTurnCreationService,
            youTurnGuidanceService,
            mapService,
            polygonOffsetService,
            vehicleProfileService,
            appState);
        YouTurn.StatusMessageChanged += (s, msg) => StatusMessage = msg;
        YouTurn.SteerAngleChanged += (s, angle) => SimulatorSteerAngle = angle;
        YouTurn.TurnCompleted += (s, e) => { /* Additional turn completion logic if needed */ };

        // Create BoundaryRecordingViewModel (handles boundary recording state and events)
        BoundaryRecording = new BoundaryRecordingViewModel(boundaryRecordingService, mapService, appState);
        BoundaryRecording.StatusMessageChanged += (s, msg) => StatusMessage = msg;
        BoundaryRecording.RecordingFinished += OnBoundaryRecordingFinished;

        // Create SectionControlViewModel (handles section master and individual section states)
        Sections = new SectionControlViewModel(moduleCommunicationService, appState);
        Sections.StatusMessageChanged += (s, msg) => StatusMessage = msg;

        // Subscribe to events
        _gpsService.GpsDataUpdated += OnGpsDataUpdated;
        _udpService.DataReceived += OnUdpDataReceived;
        _autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
        _autoSteerService.Start(); // Enable zero-copy GPS pipeline
        _udpService.ModuleConnectionChanged += OnModuleConnectionChanged;
        _ntripService.ConnectionStatusChanged += OnNtripConnectionChanged;
        _ntripService.RtcmDataReceived += OnRtcmDataReceived;
        _fieldService.ActiveFieldChanged += OnActiveFieldChanged;
        // Note: Simulator GPS events handled via SimulatorViewModel.SimulatedGpsDataReceived
        // Note: Boundary recording events handled via BoundaryRecordingViewModel
        // Note: Section master toggle events handled via SectionControlViewModel
        _moduleCommunicationService.AutoSteerToggleRequested += OnAutoSteerToggleRequested;

        // Note: FPS subscription is set up in platform code (MainWindow.axaml.cs / MainView.axaml.cs)
        // since ViewModels cannot reference Views directly

        // Note: NOT subscribing to DisplaySettings events - using direct property access instead
        // to avoid threading issues with ReactiveUI

        // Note: Simulator initialization handled by SimulatorViewModel

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

        // Restore simulator settings via SimulatorViewModel
        Simulator.RestoreSettings();
        if (settings.SimulatorEnabled)
        {
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

    /// <summary>
    /// Delegates to Sections.ActiveCount for backward compatibility.
    /// </summary>
    public int ActiveSections
    {
        get => Sections.ActiveCount;
        set => Sections.ActiveCount = value;
    }

    // Section state properties - delegate to Sections ViewModel for backward compatibility
    public bool Section1Active
    {
        get => Sections.Section1Active;
        set => Sections.Section1Active = value;
    }

    public bool Section2Active
    {
        get => Sections.Section2Active;
        set => Sections.Section2Active = value;
    }

    public bool Section3Active
    {
        get => Sections.Section3Active;
        set => Sections.Section3Active = value;
    }

    public bool Section4Active
    {
        get => Sections.Section4Active;
        set => Sections.Section4Active = value;
    }

    public bool Section5Active
    {
        get => Sections.Section5Active;
        set => Sections.Section5Active = value;
    }

    public bool Section6Active
    {
        get => Sections.Section6Active;
        set => Sections.Section6Active = value;
    }

    public bool Section7Active
    {
        get => Sections.Section7Active;
        set => Sections.Section7Active = value;
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
    // Note: _isManualSectionMode and _isSectionMasterOn moved to SectionControlViewModel
    private bool _isAutoSteerAvailable;
    private bool _isAutoSteerEngaged;
    public bool IsContourModeOn
    {
        get => _isContourModeOn;
        set => this.RaiseAndSetIfChanged(ref _isContourModeOn, value);
    }

    /// <summary>
    /// Delegates to Sections.IsManualMode for backward compatibility.
    /// </summary>
    public bool IsManualSectionMode
    {
        get => Sections.IsManualMode;
        set => Sections.IsManualMode = value;
    }

    /// <summary>
    /// Delegates to Sections.IsMasterOn for backward compatibility.
    /// </summary>
    public bool IsSectionMasterOn
    {
        get => Sections.IsMasterOn;
        set => Sections.IsMasterOn = value;
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

    /// <summary>
    /// Delegates to YouTurn.IsYouTurnEnabled for backward compatibility.
    /// </summary>
    public bool IsYouTurnEnabled
    {
        get => YouTurn.IsYouTurnEnabled;
        set => YouTurn.IsYouTurnEnabled = value;
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

        // Add boundary point if recording is active (through BoundaryRecordingViewModel)
        if (BoundaryRecording.IsRecording)
        {
            double headingRadians = data.CurrentPosition.Heading * Math.PI / 180.0;
            BoundaryRecording.AddPoint(
                data.CurrentPosition.Easting,
                data.CurrentPosition.Northing,
                headingRadians);
        }
    }

    /// <summary>
    /// Handles simulated GPS data from SimulatorViewModel.
    /// </summary>
    private void OnSimulatorGpsDataReceived(object? sender, GpsData gpsData)
    {
        var position = gpsData.CurrentPosition;

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
            // Check for YouTurn execution or create path if approaching headland
            if (IsYouTurnEnabled && _currentHeadlandLine != null && _currentHeadlandLine.Count >= 3)
            {
                YouTurn.ProcessYouTurn(position, SelectedTrack, _currentHeadlandLine,
                    _currentBoundary, HeadlandDistance, HeadlandCalculatedWidth);
            }

            // If we're in a YouTurn, use YouTurn guidance; otherwise use AB line guidance
            if (YouTurn.ShouldUseYouTurnGuidance())
            {
                if (YouTurn.CalculateYouTurnGuidance(position, out double xte))
                {
                    CrossTrackError = xte;
                }
            }
            else
            {
                CalculateAutoSteerGuidance(position);
            }
        }
    }

    /// <summary>
    /// Calculate steering guidance using Pure Pursuit algorithm and apply to simulator.
    /// Uses YouTurn.HowManyPathsAway to dynamically calculate which parallel line to follow.
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
        int howManyPathsAway = YouTurn.HowManyPathsAway;
        double distAway = widthMinusOverlap * howManyPathsAway;

        // Calculate the perpendicular direction (90 degrees from AB heading)
        double perpAngle = abHeading + Math.PI / 2; // Always use same perpendicular reference
        double offsetEasting = Math.Sin(perpAngle) * distAway;
        double offsetNorthing = Math.Cos(perpAngle) * distAway;

        // Calculate the current guidance line points (offset from reference AB line)
        double currentPtAEasting = track.PointA.Easting + offsetEasting;
        double currentPtANorthing = track.PointA.Northing + offsetNorthing;
        double currentPtBEasting = track.PointB.Easting + offsetEasting;
        double currentPtBNorthing = track.PointB.Northing + offsetNorthing;

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
            IsYouTurnTriggered = YouTurn.IsYouTurnTriggered,

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
    /// Handle boundary recording finished - save the boundary to the field.
    /// </summary>
    private void OnBoundaryRecordingFinished(object? sender, Boundary? boundary)
    {
        // This is handled via StopBoundaryRecordingCommand which saves the boundary
        // Additional post-recording logic can be added here if needed
    }

    private void OnAutoSteerToggleRequested(object? sender, AutoSteerToggleEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Toggle autosteer when requested by module communication service
            // (e.g., from work switch or steer switch)
            ToggleAutoSteerCommand?.Execute(null);
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

    public bool IsSimulatorPanelVisible
    {
        get => Simulator.IsPanelVisible;
        set => Simulator.IsPanelVisible = value;
    }

    // Note: SimCoordsDialogLatitude/Longitude delegate to Simulator.DialogLatitude/Longitude
    // (see delegating properties in Simulator section below)

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

    /// <summary>
    /// Delegates to BoundaryRecording.IsPlayerPanelVisible for backward compatibility.
    /// </summary>
    public bool IsBoundaryPlayerPanelVisible
    {
        get => BoundaryRecording.IsPlayerPanelVisible;
        set => BoundaryRecording.IsPlayerPanelVisible = value;
    }

    /// <summary>
    /// Delegates to BoundaryRecording.IsRecording for backward compatibility.
    /// </summary>
    public bool IsBoundaryRecording
    {
        get => BoundaryRecording.IsRecording;
        set { } // Read-only, state managed by BoundaryRecordingViewModel
    }

    /// <summary>
    /// Delegates to BoundaryRecording.PointCount for backward compatibility.
    /// </summary>
    public int BoundaryPointCount
    {
        get => BoundaryRecording.PointCount;
        set { } // Read-only, state managed by BoundaryRecordingViewModel
    }

    /// <summary>
    /// Delegates to BoundaryRecording.AreaHectares for backward compatibility.
    /// </summary>
    public double BoundaryAreaHectares
    {
        get => BoundaryRecording.AreaHectares;
        set { } // Read-only, state managed by BoundaryRecordingViewModel
    }

    // Boundary Player settings - delegate to BoundaryRecording
    /// <summary>
    /// Delegates to BoundaryRecording.IsSectionControlOn for backward compatibility.
    /// </summary>
    public bool IsBoundarySectionControlOn
    {
        get => BoundaryRecording.IsSectionControlOn;
        set => BoundaryRecording.IsSectionControlOn = value;
    }

    /// <summary>
    /// Delegates to BoundaryRecording.IsDrawRightSide for backward compatibility.
    /// </summary>
    public bool IsDrawRightSide
    {
        get => BoundaryRecording.IsDrawRightSide;
        set => BoundaryRecording.IsDrawRightSide = value;
    }

    /// <summary>
    /// Delegates to BoundaryRecording.IsDrawAtPivot for backward compatibility.
    /// </summary>
    public bool IsDrawAtPivot
    {
        get => BoundaryRecording.IsDrawAtPivot;
        set => BoundaryRecording.IsDrawAtPivot = value;
    }

    /// <summary>
    /// Delegates to BoundaryRecording.Offset for backward compatibility.
    /// </summary>
    public double BoundaryOffset
    {
        get => BoundaryRecording.Offset;
        set => BoundaryRecording.Offset = value;
    }

    /// <summary>
    /// Delegates to BoundaryRecording.IsOffsetRight for backward compatibility.
    /// </summary>
    public bool IsBoundaryOffsetRight
    {
        get => BoundaryRecording.IsOffsetRight;
        set => BoundaryRecording.IsOffsetRight = value;
    }

    /// <summary>
    /// Delegates to BoundaryRecording.IsAntennaMode for backward compatibility.
    /// </summary>
    public bool IsBoundaryAntennaMode
    {
        get => BoundaryRecording.IsAntennaMode;
        set => BoundaryRecording.IsAntennaMode = value;
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

    /// <summary>
    /// When true, section control remains active in headland area.
    /// Delegates to Sections.IsControlInHeadland for backward compatibility.
    /// </summary>
    public bool IsSectionControlInHeadland
    {
        get => Sections.IsControlInHeadland;
        set => Sections.IsControlInHeadland = value;
    }

    /// <summary>
    /// Number of rows to skip during U-turn (0-9). Delegates to YouTurn.
    /// </summary>
    public int UTurnSkipRows
    {
        get => YouTurn.UTurnSkipRows;
        set => YouTurn.UTurnSkipRows = value;
    }

    /// <summary>
    /// When true, U-turn skip rows feature is enabled. Delegates to YouTurn.
    /// </summary>
    public bool IsUTurnSkipRowsEnabled
    {
        get => YouTurn.IsUTurnSkipRowsEnabled;
        set => YouTurn.IsUTurnSkipRowsEnabled = value;
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

    /// <summary>
    /// Whether headland controls sections.
    /// Delegates to Sections.IsHeadlandControlled for backward compatibility.
    /// </summary>
    public bool IsHeadlandSectionControlled
    {
        get => Sections.IsHeadlandControlled;
        set => Sections.IsHeadlandControlled = value;
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

    // Simulator properties - delegate to SimulatorViewModel
    public bool IsSimulatorEnabled
    {
        get => Simulator.IsEnabled;
        set
        {
            Simulator.IsEnabled = value;
            StatusMessage = value ? "Simulator ON" : "Simulator OFF";
        }
    }

    public double SimulatorSteerAngle
    {
        get => Simulator.SteerAngle;
        set => Simulator.SteerAngle = value;
    }

    public string SimulatorSteerAngleDisplay => Simulator.SteerAngleDisplay;

    public double SimulatorSpeedKph
    {
        get => Simulator.SpeedKph;
        set => Simulator.SpeedKph = value;
    }

    public string SimulatorSpeedDisplay => Simulator.SpeedDisplay;

    public decimal? SimCoordsDialogLatitude
    {
        get => Simulator.DialogLatitude;
        set => Simulator.DialogLatitude = value;
    }

    public decimal? SimCoordsDialogLongitude
    {
        get => Simulator.DialogLongitude;
        set => Simulator.DialogLongitude = value;
    }

    /// <summary>
    /// Set new starting coordinates for the simulator
    /// </summary>
    public void SetSimulatorCoordinates(double latitude, double longitude)
    {
        Simulator.SetCoordinates(latitude, longitude);
        // Also update the Latitude/Longitude properties directly for map dialog
        Latitude = latitude;
        Longitude = longitude;
        StatusMessage = $"Simulator reset to {latitude:F8}, {longitude:F8}";
    }

    /// <summary>
    /// Get current simulator position
    /// </summary>
    public AgValoniaGPS.Models.Wgs84 GetSimulatorPosition() => Simulator.GetPosition();

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

    /// <summary>
    /// Initializes all commands by calling partial initialization methods.
    /// Commands are organized by category in separate partial class files.
    /// </summary>
    private void InitializeCommands()
    {
        InitializeNavigationCommands();
        InitializeSimulatorCommands();
        InitializeConfigurationCommands();
        InitializeFieldCommands();
        InitializeBoundaryCommands();
        InitializeTrackCommands();
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