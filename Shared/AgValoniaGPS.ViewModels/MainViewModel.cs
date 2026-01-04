using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using AgValoniaGPS.Models.Ntrip;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel : ObservableObject
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
    private readonly IModuleCommunicationService _moduleCommunicationService;
    private readonly IToolPositionService _toolPositionService;
    private readonly ICoverageMapService _coverageMapService;
    private readonly ISectionControlService _sectionControlService;
    private readonly INtripProfileService _ntripProfileService;
    private readonly ILogger<MainViewModel> _logger;
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

    // Track guidance state is now in MainViewModel.Guidance.cs
    // YouTurn state is now in MainViewModel.YouTurn.cs

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
    private bool _section8Active;
    private bool _section9Active;
    private bool _section10Active;
    private bool _section11Active;
    private bool _section12Active;
    private bool _section13Active;
    private bool _section14Active;
    private bool _section15Active;
    private bool _section16Active;

    // Section color codes (0=Red/Off, 1=Yellow/ManualOn, 2=Green/AutoOn, 3=Gray/AutoOff)
    private int _section1ColorCode;
    private int _section2ColorCode;
    private int _section3ColorCode;
    private int _section4ColorCode;
    private int _section5ColorCode;
    private int _section6ColorCode;
    private int _section7ColorCode;
    private int _section8ColorCode;
    private int _section9ColorCode;
    private int _section10ColorCode;
    private int _section11ColorCode;
    private int _section12ColorCode;
    private int _section13ColorCode;
    private int _section14ColorCode;
    private int _section15ColorCode;
    private int _section16ColorCode;

    // Cached section count for binding
    private int _numSections;

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

    // Tool position (for rendering)
    private double _toolEasting;
    private double _toolNorthing;
    private double _toolHeading;
    private double _toolWidth;
    private double _hitchEasting;
    private double _hitchNorthing;

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
        IToolPositionService toolPositionService,
        ICoverageMapService coverageMapService,
        ISectionControlService sectionControlService,
        INtripProfileService ntripProfileService,
        ILogger<MainViewModel> logger,
        ApplicationState appState)
    {
        _logger = logger;
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
        _moduleCommunicationService = moduleCommunicationService;
        _toolPositionService = toolPositionService;
        _coverageMapService = coverageMapService;
        _sectionControlService = sectionControlService;
        _ntripProfileService = ntripProfileService;
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
        _moduleCommunicationService.AutoSteerToggleRequested += OnAutoSteerToggleRequested;
        _moduleCommunicationService.SectionMasterToggleRequested += OnSectionMasterToggleRequested;
        _toolPositionService.PositionUpdated += OnToolPositionUpdated;
        _sectionControlService.SectionStateChanged += OnSectionStateChanged;

        // Subscribe to ConfigurationStore changes to update NumSections
        _numSections = Models.Configuration.ConfigurationStore.Instance.NumSections;
        Models.Configuration.ConfigurationStore.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Models.Configuration.ConfigurationStore.NumSections))
            {
                NumSections = Models.Configuration.ConfigurationStore.Instance.NumSections;
            }
        };

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

        // Initialize commands (split into partial class files for organization)
        InitializeNavigationCommands();
        InitializeSimulatorCommands();
        InitializeConfigurationCommands();
        InitializeFieldCommands();
        InitializeBoundaryCommands();
        InitializeTrackCommands();
        InitializeNtripCommands();
        InitializeWizardCommands();

        // Load display settings first, then restore our app settings on top
        // This ensures AppSettings takes precedence over DisplaySettings
        Dispatcher.UIThread.Post(() =>
        {
            _displaySettings.LoadSettings();
            RestoreSettings();
        }, DispatcherPriority.Background);

        // Start UDP communication (fire-and-forget but explicit)
        _ = InitializeAsync();
    }

    private void RestoreSettings()
    {
        var settings = _settingsService.Settings;

        // Restore vehicle profile settings
        LoadDefaultVehicleProfile();

        // Load NTRIP profiles
        _ = _ntripProfileService.LoadProfilesAsync();

        // Restore legacy NTRIP settings (used if no profiles exist)
        NtripCasterAddress = settings.NtripCasterIp;
        NtripCasterPort = settings.NtripCasterPort;
        NtripMountPoint = settings.NtripMountPoint;
        NtripUsername = settings.NtripUsername;
        NtripPassword = settings.NtripPassword;

        // Auto-connect to NTRIP if configured (legacy behavior, profiles will override when field loads)
        if (settings.NtripAutoConnect && !string.IsNullOrEmpty(settings.NtripCasterIp))
        {
            _logger.LogInformation("NTRIP auto-connecting at startup (legacy settings)");
            _ = ConnectToNtripAsync(); // Fire and forget - don't await in RestoreSettings
        }

        // Restore UI state (through _displaySettings service)
        _displaySettings.IsGridOn = settings.GridVisible;

        // IMPORTANT: Notify bindings that IsGridOn changed
        // (setting _displaySettings directly doesn't trigger property change notification)
        OnPropertyChanged(nameof(IsGridOn));

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

            _logger.LogDebug("Restored simulator: {Lat},{Lon}", settings.SimulatorLatitude, settings.SimulatorLongitude);
        }
    }

    private void LoadDefaultVehicleProfile()
    {
        try
        {
            var profiles = _configurationService.GetAvailableProfiles();
            if (profiles.Count == 0)
            {
                _logger.LogWarning("No vehicle profiles found in Vehicles directory");
                return;
            }

            // Try to load the last used profile first
            var lastUsedProfile = _settingsService.Settings.LastUsedVehicleProfile;
            string profileToLoad;

            if (!string.IsNullOrEmpty(lastUsedProfile) && profiles.Contains(lastUsedProfile))
            {
                profileToLoad = lastUsedProfile;
                _logger.LogDebug("Loading last used vehicle profile: {ProfileName}", profileToLoad);
            }
            else
            {
                // Fall back to first available profile
                profileToLoad = profiles[0];
                _logger.LogDebug("Loading first available vehicle profile: {ProfileName}", profileToLoad);
            }

            // Use ConfigurationService to load - this sets ConfigurationStore.ActiveProfileName
            if (_configurationService.LoadProfile(profileToLoad))
            {
                var store = _configurationService.Store;
                _logger.LogInformation("Loaded vehicle profile: {ProfileName}", store.ActiveProfileName);
                _logger.LogDebug("  Tool width: {ToolWidth}m (from {NumSections} sections)", store.ActualToolWidth, store.NumSections);
                _logger.LogDebug("  YouTurn radius: {Radius}m", store.Guidance.UTurnRadius);
                _logger.LogDebug("  Wheelbase: {Wheelbase}m", store.Vehicle.Wheelbase);
                _logger.LogDebug("  Sections: {NumSections}", store.NumSections);

                // Save as last used profile
                _settingsService.Settings.LastUsedVehicleProfile = profileToLoad;
                _settingsService.Save();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle profile");
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

    /// <summary>
    /// Handles NTRIP profile connection when a field is loaded.
    /// Checks for field-specific profile or falls back to default profile.
    /// </summary>
    private async System.Threading.Tasks.Task HandleNtripProfileForFieldAsync(string fieldName)
    {
        try
        {
            var profile = _ntripProfileService.GetProfileForField(fieldName);

            if (profile == null)
            {
                _logger.LogDebug("No NTRIP profile found for field '{FieldName}' (no default set)", fieldName);
                return;
            }

            if (!profile.AutoConnectOnFieldLoad)
            {
                _logger.LogDebug("NTRIP profile '{ProfileName}' has auto-connect disabled", profile.Name);
                return;
            }

            // Disconnect from current caster if connected
            if (_ntripService.IsConnected)
            {
                _logger.LogDebug("Disconnecting from current NTRIP caster");
                await _ntripService.DisconnectAsync();
            }

            // Update UI properties for display
            NtripCasterAddress = profile.CasterHost;
            NtripCasterPort = profile.CasterPort;
            NtripMountPoint = profile.MountPoint;
            NtripUsername = profile.Username;
            NtripPassword = profile.Password;

            // Connect to new caster
            _logger.LogInformation("Connecting to NTRIP profile '{ProfileName}' for field '{FieldName}'", profile.Name, fieldName);
            await ConnectToNtripAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NTRIP profile for field '{FieldName}'", fieldName);
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _udpService.StartAsync();
            NetworkStatus = $"UDP Connected: {_udpService.LocalIPAddress}";
            StatusMessage = "Ready - Waiting for modules...";

            // Start sending hello packets (fire-and-forget but explicit)
            _ = StartHelloTimerAsync();
        }
        catch (Exception ex)
        {
            NetworkStatus = $"UDP Error: {ex.Message}";
            StatusMessage = "Network error";
        }
    }

    private async Task StartHelloTimerAsync()
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "HelloTimer error");
            StatusMessage = "Module check error";
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public double Latitude
    {
        get => _latitude;
        set => SetProperty(ref _latitude, value);
    }

    public double Longitude
    {
        get => _longitude;
        set => SetProperty(ref _longitude, value);
    }

    public double Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    /// <summary>
    /// Current rendering frames per second
    /// </summary>
    public double CurrentFps
    {
        get => _currentFps;
        set => SetProperty(ref _currentFps, value);
    }

    /// <summary>
    /// GPS-to-PGN pipeline latency in milliseconds.
    /// This is the critical latency from GPS receipt to steering PGN send.
    /// </summary>
    public double GpsToPgnLatencyMs
    {
        get => _gpsToPgnLatencyMs;
        set => SetProperty(ref _gpsToPgnLatencyMs, value);
    }

    public int SatelliteCount
    {
        get => _satelliteCount;
        set => SetProperty(ref _satelliteCount, value);
    }

    public string FixQuality
    {
        get => _fixQuality;
        set => SetProperty(ref _fixQuality, value);
    }

    public string NetworkStatus
    {
        get => _networkStatus;
        set => SetProperty(ref _networkStatus, value);
    }

    // Guidance/Steering properties
    public double CrossTrackError
    {
        get => _crossTrackError;
        set => SetProperty(ref _crossTrackError, value);
    }

    public string CurrentGuidanceLine
    {
        get => _currentGuidanceLine;
        set => SetProperty(ref _currentGuidanceLine, value);
    }

    public bool IsAutoSteerActive
    {
        get => _isAutoSteerActive;
        set => SetProperty(ref _isAutoSteerActive, value);
    }

    public int ActiveSections
    {
        get => _activeSections;
        set => SetProperty(ref _activeSections, value);
    }

    // Section states
    public bool Section1Active
    {
        get => _section1Active;
        set => SetProperty(ref _section1Active, value);
    }

    public bool Section2Active
    {
        get => _section2Active;
        set => SetProperty(ref _section2Active, value);
    }

    public bool Section3Active
    {
        get => _section3Active;
        set => SetProperty(ref _section3Active, value);
    }

    public bool Section4Active
    {
        get => _section4Active;
        set => SetProperty(ref _section4Active, value);
    }

    public bool Section5Active
    {
        get => _section5Active;
        set => SetProperty(ref _section5Active, value);
    }

    public bool Section6Active
    {
        get => _section6Active;
        set => SetProperty(ref _section6Active, value);
    }

    public bool Section7Active
    {
        get => _section7Active;
        set => SetProperty(ref _section7Active, value);
    }

    public bool Section8Active
    {
        get => _section8Active;
        set => SetProperty(ref _section8Active, value);
    }

    public bool Section9Active
    {
        get => _section9Active;
        set => SetProperty(ref _section9Active, value);
    }

    public bool Section10Active
    {
        get => _section10Active;
        set => SetProperty(ref _section10Active, value);
    }

    public bool Section11Active
    {
        get => _section11Active;
        set => SetProperty(ref _section11Active, value);
    }

    public bool Section12Active
    {
        get => _section12Active;
        set => SetProperty(ref _section12Active, value);
    }

    public bool Section13Active
    {
        get => _section13Active;
        set => SetProperty(ref _section13Active, value);
    }

    public bool Section14Active
    {
        get => _section14Active;
        set => SetProperty(ref _section14Active, value);
    }

    public bool Section15Active
    {
        get => _section15Active;
        set => SetProperty(ref _section15Active, value);
    }

    public bool Section16Active
    {
        get => _section16Active;
        set => SetProperty(ref _section16Active, value);
    }

    // Section color codes for panel buttons (0=Red/Off, 1=Yellow/ManualOn, 2=Green/AutoOn, 3=Gray/AutoOff)
    public int Section1ColorCode
    {
        get => _section1ColorCode;
        set => SetProperty(ref _section1ColorCode, value);
    }

    public int Section2ColorCode
    {
        get => _section2ColorCode;
        set => SetProperty(ref _section2ColorCode, value);
    }

    public int Section3ColorCode
    {
        get => _section3ColorCode;
        set => SetProperty(ref _section3ColorCode, value);
    }

    public int Section4ColorCode
    {
        get => _section4ColorCode;
        set => SetProperty(ref _section4ColorCode, value);
    }

    public int Section5ColorCode
    {
        get => _section5ColorCode;
        set => SetProperty(ref _section5ColorCode, value);
    }

    public int Section6ColorCode
    {
        get => _section6ColorCode;
        set => SetProperty(ref _section6ColorCode, value);
    }

    public int Section7ColorCode
    {
        get => _section7ColorCode;
        set => SetProperty(ref _section7ColorCode, value);
    }

    public int Section8ColorCode
    {
        get => _section8ColorCode;
        set => SetProperty(ref _section8ColorCode, value);
    }

    public int Section9ColorCode
    {
        get => _section9ColorCode;
        set => SetProperty(ref _section9ColorCode, value);
    }

    public int Section10ColorCode
    {
        get => _section10ColorCode;
        set => SetProperty(ref _section10ColorCode, value);
    }

    public int Section11ColorCode
    {
        get => _section11ColorCode;
        set => SetProperty(ref _section11ColorCode, value);
    }

    public int Section12ColorCode
    {
        get => _section12ColorCode;
        set => SetProperty(ref _section12ColorCode, value);
    }

    public int Section13ColorCode
    {
        get => _section13ColorCode;
        set => SetProperty(ref _section13ColorCode, value);
    }

    public int Section14ColorCode
    {
        get => _section14ColorCode;
        set => SetProperty(ref _section14ColorCode, value);
    }

    public int Section15ColorCode
    {
        get => _section15ColorCode;
        set => SetProperty(ref _section15ColorCode, value);
    }

    public int Section16ColorCode
    {
        get => _section16ColorCode;
        set => SetProperty(ref _section16ColorCode, value);
    }

    /// <summary>
    /// Get section on/off states for map rendering.
    /// </summary>
    public bool[] GetSectionStates()
    {
        var states = _sectionControlService.SectionStates;
        var result = new bool[16];
        for (int i = 0; i < Math.Min(states.Count, 16); i++)
        {
            result[i] = states[i].IsOn;
        }
        return result;
    }

    /// <summary>
    /// Get section button states (Off=0, Auto=1, On=2) for 3-state rendering.
    /// </summary>
    public int[] GetSectionButtonStates()
    {
        var states = _sectionControlService.SectionStates;
        var result = new int[16];
        for (int i = 0; i < Math.Min(states.Count, 16); i++)
        {
            result[i] = (int)states[i].ButtonState; // Off=0, Auto=1, On=2
        }
        return result;
    }

    /// <summary>
    /// Get section widths in meters for map rendering.
    /// </summary>
    public double[] GetSectionWidths()
    {
        var config = Models.Configuration.ConfigurationStore.Instance;
        var result = new double[16];
        for (int i = 0; i < 16; i++)
        {
            result[i] = config.Tool.GetSectionWidth(i) / 100.0; // cm to meters
        }
        return result;
    }

    /// <summary>
    /// Number of configured sections for map rendering.
    /// </summary>
    public int NumSections
    {
        get => _numSections;
        private set => SetProperty(ref _numSections, value);
    }

    // AutoSteer Hello and Data properties
    public bool IsAutoSteerHelloOk
    {
        get => _isAutoSteerHelloOk;
        set => SetProperty(ref _isAutoSteerHelloOk, value);
    }

    public bool IsAutoSteerDataOk
    {
        get => _isAutoSteerDataOk;
        set => SetProperty(ref _isAutoSteerDataOk, value);
    }

    // Right Navigation Panel Properties
    private bool _isContourModeOn;
    // IsManualSectionMode and IsSectionMasterOn are now computed from _sectionControlService.MasterState
    private bool _isAutoSteerAvailable;
    private bool _isAutoSteerEngaged;

    public bool IsContourModeOn
    {
        get => _isContourModeOn;
        set => SetProperty(ref _isContourModeOn, value);
    }

    // Button state tracking - these just track what the convenience buttons last did
    private bool _isManualAllOn;
    private bool _isAutoAllOn;

    public bool IsManualSectionMode
    {
        get => _isManualAllOn;
        set => SetProperty(ref _isManualAllOn, value);
    }

    public bool IsSectionMasterOn
    {
        get => _isAutoAllOn;
        set => SetProperty(ref _isAutoAllOn, value);
    }

    public bool IsAutoSteerAvailable
    {
        get => _isAutoSteerAvailable;
        set => SetProperty(ref _isAutoSteerAvailable, value);
    }

    public bool IsAutoSteerEngaged
    {
        get => _isAutoSteerEngaged;
        set => SetProperty(ref _isAutoSteerEngaged, value);
    }

    // IsYouTurnEnabled is now in MainViewModel.YouTurn.cs

    // Machine Hello and Data properties
    public bool IsMachineHelloOk
    {
        get => _isMachineHelloOk;
        set => SetProperty(ref _isMachineHelloOk, value);
    }

    public bool IsMachineDataOk
    {
        get => _isMachineDataOk;
        set => SetProperty(ref _isMachineDataOk, value);
    }

    // IMU Hello and Data properties
    public bool IsImuHelloOk
    {
        get => _isImuHelloOk;
        set => SetProperty(ref _isImuHelloOk, value);
    }

    public bool IsImuDataOk
    {
        get => _isImuDataOk;
        set => SetProperty(ref _isImuDataOk, value);
    }

    // GPS Hello and Data properties (GPS doesn't have hello, just data from NMEA)
    public bool IsGpsDataOk
    {
        get => _isGpsDataOk;
        set => SetProperty(ref _isGpsDataOk, value);
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
        set => SetProperty(ref _isNtripConnected, value);
    }

    public string NtripStatus
    {
        get => _ntripStatus;
        set => SetProperty(ref _ntripStatus, value);
    }

    public string NtripBytesReceived
    {
        get => $"{(_ntripBytesReceived / 1024):N0} KB";
    }

    public string NtripCasterAddress
    {
        get => _ntripCasterAddress;
        set => SetProperty(ref _ntripCasterAddress, value);
    }

    public int NtripCasterPort
    {
        get => _ntripCasterPort;
        set => SetProperty(ref _ntripCasterPort, value);
    }

    public string NtripMountPoint
    {
        get => _ntripMountPoint;
        set => SetProperty(ref _ntripMountPoint, value);
    }

    public string NtripUsername
    {
        get => _ntripUsername;
        set => SetProperty(ref _ntripUsername, value);
    }

    public string NtripPassword
    {
        get => _ntripPassword;
        set => SetProperty(ref _ntripPassword, value);
    }

    public string DebugLog
    {
        get => _debugLog;
        set => SetProperty(ref _debugLog, value);
    }

    public double Easting
    {
        get => _easting;
        set => SetProperty(ref _easting, value);
    }

    public double Northing
    {
        get => _northing;
        set => SetProperty(ref _northing, value);
    }

    public double Heading
    {
        get => _heading;
        set => SetProperty(ref _heading, value);
    }

    // Tool position properties (for map rendering)
    public double ToolEasting
    {
        get => _toolEasting;
        set => SetProperty(ref _toolEasting, value);
    }

    public double ToolNorthing
    {
        get => _toolNorthing;
        set => SetProperty(ref _toolNorthing, value);
    }

    public double ToolHeadingRadians
    {
        get => _toolHeading;
        set => SetProperty(ref _toolHeading, value);
    }

    public double ToolWidth
    {
        get => _toolWidth;
        set => SetProperty(ref _toolWidth, value);
    }

    public double HitchEasting
    {
        get => _hitchEasting;
        set => SetProperty(ref _hitchEasting, value);
    }

    public double HitchNorthing
    {
        get => _hitchNorthing;
        set => SetProperty(ref _hitchNorthing, value);
    }

    // OnAutoSteerStateUpdated is now in MainViewModel.Guidance.cs

    private void OnToolPositionUpdated(object? sender, Services.Interfaces.ToolPositionUpdatedEventArgs e)
    {
        // Update tool position properties for map rendering
        // This fires after each GPS update when ToolPositionService.Update() is called
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateToolPositionProperties(e);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateToolPositionProperties(e));
        }
    }

    private void UpdateToolPositionProperties(Services.Interfaces.ToolPositionUpdatedEventArgs e)
    {
        var config = Models.Configuration.ConfigurationStore.Instance;

        // Calculate actual tool width from active sections (section widths are in cm)
        double totalWidthMeters = 0;
        int numSections = config.NumSections;
        for (int i = 0; i < numSections && i < 16; i++)
        {
            totalWidthMeters += config.Tool.GetSectionWidth(i) / 100.0; // cm to meters
        }

        // Get hitch position from the service
        var hitchPos = _toolPositionService.HitchPosition;

        // Set all properties BEFORE ToolEasting (which triggers the view update)
        ToolNorthing = e.ToolPosition.Northing;
        ToolHeadingRadians = e.ToolHeading;
        ToolWidth = totalWidthMeters; // Use calculated width from sections
        HitchEasting = hitchPos.Easting;
        HitchNorthing = hitchPos.Northing;

        // Update section control - determines which sections should be on/off in Auto mode
        _sectionControlService.Update(e.ToolPosition, e.ToolHeading, e.VehicleHeading, Speed);

        // Update coverage painting - paint when sections are active and moving
        UpdateCoveragePainting(e.ToolPosition, e.ToolHeading);

        // Set ToolEasting LAST - this triggers the PropertyChanged that updates the map
        ToolEasting = e.ToolPosition.Easting;
    }

    /// <summary>
    /// Update coverage painting based on section states.
    /// Paints triangle strips when sections are active and vehicle is moving.
    /// </summary>
    private void UpdateCoveragePainting(Vec3 toolPosition, double toolHeading)
    {
        // Minimum speed to paint coverage (0.3 m/s ≈ 1 km/h) - don't paint when stationary
        const double MinPaintingSpeed = 0.3;
        if (Speed < MinPaintingSpeed)
        {
            // Stop all mapping if vehicle is stationary
            for (int i = 0; i < _sectionControlService.NumSections; i++)
            {
                if (_coverageMapService.IsZoneMapping(i))
                {
                    _coverageMapService.StopMapping(i);
                }
            }
            return;
        }

        // Update each section's coverage based on its state
        var states = _sectionControlService.SectionStates;
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            var (left, right) = _sectionControlService.GetSectionWorldPosition(i, toolPosition, toolHeading);

            if (state.IsOn && !_coverageMapService.IsZoneMapping(i))
            {
                // Section just turned on - start mapping
                _coverageMapService.StartMapping(i, left, right);
            }
            else if (!state.IsOn && _coverageMapService.IsZoneMapping(i))
            {
                // Section just turned off - stop mapping
                _coverageMapService.StopMapping(i);
            }
            else if (state.IsOn && _coverageMapService.IsZoneMapping(i))
            {
                // Section still on - add coverage point
                _coverageMapService.AddCoveragePoint(i, left, right);
            }
        }
    }

    private void OnSectionStateChanged(object? sender, SectionStateChangedEventArgs e)
    {
        // Marshal to UI thread
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            UpdateSectionActiveProperties();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateSectionActiveProperties);
        }
    }

    private void UpdateSectionActiveProperties()
    {
        // Sync Section*Active properties with service state
        var states = _sectionControlService.SectionStates;
        Section1Active = states.Count > 0 && states[0].IsOn;
        Section2Active = states.Count > 1 && states[1].IsOn;
        Section3Active = states.Count > 2 && states[2].IsOn;
        Section4Active = states.Count > 3 && states[3].IsOn;
        Section5Active = states.Count > 4 && states[4].IsOn;
        Section6Active = states.Count > 5 && states[5].IsOn;
        Section7Active = states.Count > 6 && states[6].IsOn;
        Section8Active = states.Count > 7 && states[7].IsOn;
        Section9Active = states.Count > 8 && states[8].IsOn;
        Section10Active = states.Count > 9 && states[9].IsOn;
        Section11Active = states.Count > 10 && states[10].IsOn;
        Section12Active = states.Count > 11 && states[11].IsOn;
        Section13Active = states.Count > 12 && states[12].IsOn;
        Section14Active = states.Count > 13 && states[13].IsOn;
        Section15Active = states.Count > 14 && states[14].IsOn;
        Section16Active = states.Count > 15 && states[15].IsOn;

        // Update color codes for panel buttons (matching map rendering colors)
        // 0=Red (Off), 1=Yellow (Manual On), 2=Green (Auto On), 3=Gray (Auto Off)
        Section1ColorCode = states.Count > 0 ? GetSectionColorCode(states[0]) : 0;
        Section2ColorCode = states.Count > 1 ? GetSectionColorCode(states[1]) : 0;
        Section3ColorCode = states.Count > 2 ? GetSectionColorCode(states[2]) : 0;
        Section4ColorCode = states.Count > 3 ? GetSectionColorCode(states[3]) : 0;
        Section5ColorCode = states.Count > 4 ? GetSectionColorCode(states[4]) : 0;
        Section6ColorCode = states.Count > 5 ? GetSectionColorCode(states[5]) : 0;
        Section7ColorCode = states.Count > 6 ? GetSectionColorCode(states[6]) : 0;
        Section8ColorCode = states.Count > 7 ? GetSectionColorCode(states[7]) : 0;
        Section9ColorCode = states.Count > 8 ? GetSectionColorCode(states[8]) : 0;
        Section10ColorCode = states.Count > 9 ? GetSectionColorCode(states[9]) : 0;
        Section11ColorCode = states.Count > 10 ? GetSectionColorCode(states[10]) : 0;
        Section12ColorCode = states.Count > 11 ? GetSectionColorCode(states[11]) : 0;
        Section13ColorCode = states.Count > 12 ? GetSectionColorCode(states[12]) : 0;
        Section14ColorCode = states.Count > 13 ? GetSectionColorCode(states[13]) : 0;
        Section15ColorCode = states.Count > 14 ? GetSectionColorCode(states[14]) : 0;
        Section16ColorCode = states.Count > 15 ? GetSectionColorCode(states[15]) : 0;
    }

    /// <summary>
    /// Calculate color code for a section state (matches map rendering logic).
    /// </summary>
    private static int GetSectionColorCode(SectionControlState state)
    {
        // 3-state model: Off=Red, Auto=Green, On=Yellow
        return state.ButtonState switch
        {
            SectionButtonState.Off => 0,  // Red - manually off
            SectionButtonState.On => 1,   // Yellow - manually on
            SectionButtonState.Auto => 2, // Green - automatic mode
            _ => 0
        };
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
        // This applies antenna-to-pivot transformation to gpsData.CurrentPosition
        _gpsService.UpdateGpsData(gpsData);

        // Use the TRANSFORMED position (pivot/tractor center) for all guidance calculations
        var transformedPosition = gpsData.CurrentPosition;

        // Update tool position based on vehicle pivot position
        // Tool position service handles fixed, trailing, and TBT configurations
        var vehiclePivot = new Models.Base.Vec3(
            transformedPosition.Easting,
            transformedPosition.Northing,
            transformedPosition.Heading * Math.PI / 180.0  // Convert to radians
        );
        _toolPositionService.Update(vehiclePivot, transformedPosition.Heading * Math.PI / 180.0);

        // Process through AutoSteer pipeline for latency measurement
        _autoSteerService.ProcessSimulatedPosition(
            transformedPosition.Latitude, transformedPosition.Longitude, transformedPosition.Altitude,
            transformedPosition.Heading, transformedPosition.Speed, gpsData.FixQuality,
            gpsData.SatellitesInUse, gpsData.Hdop,
            transformedPosition.Easting, transformedPosition.Northing);

        // Auto-disengage autosteer if vehicle is outside the outer boundary
        if (IsAutoSteerEngaged && !IsPointInsideBoundary(transformedPosition.Easting, transformedPosition.Northing))
        {
            IsAutoSteerEngaged = false;
            StatusMessage = "AutoSteer disengaged - outside boundary";
        }

        // Calculate autosteer guidance if engaged and we have an active track
        if (IsAutoSteerEngaged && HasActiveTrack && SelectedTrack != null)
        {
            // Increment YouTurn counter (used for throttling)
            _youTurnCounter++;

            // Check for YouTurn execution or create path if approaching headland
            if (IsYouTurnEnabled && _currentHeadlandLine != null && _currentHeadlandLine.Count >= 3)
            {
                ProcessYouTurn(transformedPosition);
            }

            // If we're in a YouTurn, use YouTurn guidance; otherwise use AB line guidance
            if (_isYouTurnTriggered && _youTurnPath != null && _youTurnPath.Count > 0)
            {
                CalculateYouTurnGuidance(transformedPosition);
            }
            else
            {
                CalculateAutoSteerGuidance(transformedPosition);
            }
        }
    }

    // AutoSteer guidance methods (CalculateAutoSteerGuidance, UpdateActiveLineVisualization)
    // are now in MainViewModel.Guidance.cs

    // YouTurn methods (ProcessYouTurn, CreateYouTurnPath, CalculateYouTurnGuidance, etc.)
    // are now in MainViewModel.YouTurn.cs

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

    private void OnAutoSteerToggleRequested(object? sender, AutoSteerToggleEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Toggle autosteer when requested by module communication service
            // (e.g., from work switch or steer switch)
            ToggleAutoSteerCommand?.Execute(null);
        });
    }

    private void OnSectionMasterToggleRequested(object? sender, SectionMasterToggleEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Toggle section master when requested by module communication service
            // This replaces the direct PerformClick() calls from the WinForms implementation
            // TODO: When separate Auto/Manual section buttons are implemented, handle them individually
            ToggleSectionMasterCommand?.Execute(null);
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
        OnPropertyChanged(nameof(NtripBytesReceived));
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
        set => SetProperty(ref _activeField, value);
    }

    public string FieldsRootDirectory
    {
        get => _fieldsRootDirectory;
        set => SetProperty(ref _fieldsRootDirectory, value);
    }

    public string? ActiveFieldName => ActiveField?.Name;
    public double? ActiveFieldArea => ActiveField?.TotalArea;
    public bool HasActiveField => ActiveField != null;

    // Services exposed for UI/control access
    public AgValoniaGPS.Services.Interfaces.IFieldStatisticsService FieldStatistics => _fieldStatistics;

    // Field statistics properties for UI binding
    public string WorkedAreaDisplay => FormatArea(_coverageMapService.TotalWorkedArea);

    public string BoundaryAreaDisplay
    {
        get
        {
            // Use CurrentBoundary area directly (more reliable than pre-calculated field.TotalArea)
            var boundary = State.Field.CurrentBoundary;
            if (boundary != null && boundary.IsValid)
            {
                return $"{boundary.AreaHectares:F2} ha";
            }
            return "0.00 ha";
        }
    }

    public double RemainingPercent
    {
        get
        {
            var boundary = State.Field.CurrentBoundary;
            double boundaryArea = boundary?.AreaHectares ?? 0;
            double boundaryAreaSqM = boundaryArea * 10000; // Convert back to sq meters for comparison
            if (boundaryAreaSqM > 0)
            {
                double workedArea = _coverageMapService.TotalWorkedArea;
                return ((boundaryAreaSqM - workedArea) * 100 / boundaryAreaSqM);
            }
            return 100;
        }
    }

    // Instantaneous work rate based on current speed and tool width
    public string WorkRateDisplay
    {
        get
        {
            // Rate = Speed (m/h) × Tool Width (m) = m²/h
            // Convert: Speed is in m/s, need m/h; result in ha/hr
            double speedMetersPerHour = Speed * 3600; // m/s to m/h
            double toolWidthMeters = ToolWidth; // Already in meters
            double squareMetersPerHour = speedMetersPerHour * toolWidthMeters;
            double hectaresPerHour = squareMetersPerHour / 10000.0;
            return $"{hectaresPerHour:F1} ha/hr";
        }
    }

    // Helper method to format area
    private string FormatArea(double squareMeters)
    {
        // Convert to hectares
        double hectares = squareMeters * 0.0001;
        return $"{hectares:F2} ha";
    }

    /// <summary>
    /// Called when coverage is updated to refresh UI statistics
    /// </summary>
    public void RefreshCoverageStatistics()
    {
        OnPropertyChanged(nameof(WorkedAreaDisplay));
        OnPropertyChanged(nameof(RemainingPercent));
        OnPropertyChanged(nameof(WorkRateDisplay));
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
        // Save coverage to previous field before switching
        var previousField = ActiveField;
        if (previousField != null && !string.IsNullOrEmpty(previousField.DirectoryPath))
        {
            try
            {
                _coverageMapService.SaveToFile(previousField.DirectoryPath);
                _logger.LogDebug($"[Coverage] Saved coverage to {previousField.DirectoryPath}");
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[Coverage] Error saving coverage: {ex.Message}");
            }
        }

        // Clear coverage when switching fields
        _coverageMapService.ClearAll();

        // Update centralized state
        State.Field.ActiveField = field;

        // Legacy property
        ActiveField = field;
        OnPropertyChanged(nameof(ActiveFieldName));
        OnPropertyChanged(nameof(ActiveFieldArea));
        OnPropertyChanged(nameof(HasActiveField));

        // Update field statistics service with new boundary
        if (field?.Boundary != null)
        {
            // Sync boundary to State.Field.CurrentBoundary for section control and stats
            SetCurrentBoundary(field.Boundary);

            // Calculate boundary area and pass as list (outer boundary only for now)
            var boundaryAreas = new List<double> { field.Boundary.AreaHectares * 10000 }; // Convert ha to sq meters
            _fieldStatistics.UpdateBoundaryAreas(boundaryAreas);
            OnPropertyChanged(nameof(BoundaryAreaDisplay));
            OnPropertyChanged(nameof(WorkedAreaDisplay));
            OnPropertyChanged(nameof(RemainingPercent));
        }
        else
        {
            // No boundary - clear it
            SetCurrentBoundary(null);
        }

        // Load headland from field directory if available
        LoadHeadlandFromField(field);

        // Load AB lines from field directory if available
        LoadTracksFromField(field);

        // Load coverage from field directory if available
        if (field != null && !string.IsNullOrEmpty(field.DirectoryPath))
        {
            try
            {
                _coverageMapService.LoadFromFile(field.DirectoryPath);
                _logger.LogDebug($"[Coverage] Loaded coverage from {field.DirectoryPath}");

                // Refresh coverage statistics and notify UI
                RefreshCoverageStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[Coverage] Error loading coverage: {ex.Message}");
            }
        }
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
                OnPropertyChanged(nameof(CurrentHeadlandLine));

                HasHeadland = true;
                IsHeadlandOn = true;
                HeadlandDistance = headlandLine.Tracks[0].MoveDistance;

                _logger.LogDebug($"[Headland] Loaded headland from {field.DirectoryPath} ({_currentHeadlandLine.Count} points)");
            }
            else
            {
                State.Field.HeadlandLine = null;
                State.Field.HeadlandDistance = 0;

                _currentHeadlandLine = null;
                _mapService.SetHeadlandLine(null);
                HasHeadland = false;
                IsHeadlandOn = false;
                _logger.LogDebug($"[Headland] No headland found in {field.DirectoryPath}");
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug($"[Headland] Failed to load headland: {ex.Message}");
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
        set => SetProperty(ref _isViewSettingsPanelVisible, value);
    }

    private bool _isFileMenuPanelVisible;
    public bool IsFileMenuPanelVisible
    {
        get => _isFileMenuPanelVisible;
        set => SetProperty(ref _isFileMenuPanelVisible, value);
    }

    private bool _isToolsPanelVisible;
    public bool IsToolsPanelVisible
    {
        get => _isToolsPanelVisible;
        set => SetProperty(ref _isToolsPanelVisible, value);
    }

    private bool _isConfigurationPanelVisible;
    public bool IsConfigurationPanelVisible
    {
        get => _isConfigurationPanelVisible;
        set => SetProperty(ref _isConfigurationPanelVisible, value);
    }

    private bool _isJobMenuPanelVisible;
    public bool IsJobMenuPanelVisible
    {
        get => _isJobMenuPanelVisible;
        set => SetProperty(ref _isJobMenuPanelVisible, value);
    }

    private bool _isFieldToolsPanelVisible;
    public bool IsFieldToolsPanelVisible
    {
        get => _isFieldToolsPanelVisible;
        set => SetProperty(ref _isFieldToolsPanelVisible, value);
    }

    private bool _isSimulatorPanelVisible;
    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => SetProperty(ref _isSimulatorPanelVisible, value);
    }

    // Panel-based dialog data properties (visibility now managed by State.UI)
    private decimal? _simCoordsDialogLatitude;
    public decimal? SimCoordsDialogLatitude
    {
        get => _simCoordsDialogLatitude;
        set => SetProperty(ref _simCoordsDialogLatitude, value);
    }

    private decimal? _simCoordsDialogLongitude;
    public decimal? SimCoordsDialogLongitude
    {
        get => _simCoordsDialogLongitude;
        set => SetProperty(ref _simCoordsDialogLongitude, value);
    }

    // Field Selection Dialog properties (visibility managed by State.UI)
    public ObservableCollection<FieldSelectionItem> AvailableFields { get; } = new();

    private FieldSelectionItem? _selectedFieldInfo;
    public FieldSelectionItem? SelectedFieldInfo
    {
        get => _selectedFieldInfo;
        set => SetProperty(ref _selectedFieldInfo, value);
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
            SetProperty(ref _currentABCreationMode, value);
            OnPropertyChanged(nameof(IsCreatingABLine));
            OnPropertyChanged(nameof(EnableABClickSelection));
            OnPropertyChanged(nameof(ABCreationInstructions));
        }
    }

    private ABPointStep _currentABPointStep = ABPointStep.None;
    public ABPointStep CurrentABPointStep
    {
        get => _currentABPointStep;
        set
        {
            SetProperty(ref _currentABPointStep, value);
            OnPropertyChanged(nameof(ABCreationInstructions));
        }
    }

    // Temporary storage for Point A during AB creation
    private Position? _pendingPointA;
    public Position? PendingPointA
    {
        get => _pendingPointA;
        set => SetProperty(ref _pendingPointA, value);
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
            if (SetProperty(ref _selectedTrack, value))
            {
                // Sync IsActive state with selection
                if (oldValue != null)
                {
                    oldValue.IsActive = false;
                }
                if (value != null)
                {
                    value.IsActive = true;
                    State.Field.ActiveTrack = value;
                    // Show the track on the map when activated
                    _mapService.SetActiveTrack(value);
                }
                else
                {
                    State.Field.ActiveTrack = null;
                    // Clear the track from the map when deactivated
                    _mapService.SetActiveTrack(null);
                }

                // Update guidance availability
                HasActiveTrack = value != null;
                IsAutoSteerAvailable = value != null;

                _logger.LogDebug($"[SelectedTrack] Changed to: {value?.Name ?? "None"}");
            }
        }
    }

    // Track management commands
    public ICommand? DeleteSelectedTrackCommand { get; private set; }
    public ICommand? SwapABPointsCommand { get; private set; }
    public ICommand? SelectTrackAsActiveCommand { get; private set; }

    // NTRIP Profiles Dialog properties
    public ObservableCollection<NtripProfile> NtripProfiles { get; } = new();

    private NtripProfile? _selectedNtripProfile;
    public NtripProfile? SelectedNtripProfile
    {
        get => _selectedNtripProfile;
        set => SetProperty(ref _selectedNtripProfile, value);
    }

    private NtripProfile? _editingNtripProfile;
    public NtripProfile? EditingNtripProfile
    {
        get => _editingNtripProfile;
        set => SetProperty(ref _editingNtripProfile, value);
    }

    /// <summary>
    /// Available fields for NTRIP profile association (with selection state)
    /// </summary>
    public ObservableCollection<FieldAssociationItem> AvailableFieldsForProfile { get; } = new();

    // NTRIP Profiles commands
    public ICommand? ShowNtripProfilesDialogCommand { get; private set; }
    public ICommand? CloseNtripProfilesDialogCommand { get; private set; }
    public ICommand? AddNtripProfileCommand { get; private set; }
    public ICommand? EditNtripProfileCommand { get; private set; }
    public ICommand? DeleteNtripProfileCommand { get; private set; }
    public ICommand? SetDefaultNtripProfileCommand { get; private set; }
    public ICommand? SaveNtripProfileCommand { get; private set; }
    public ICommand? CancelNtripProfileEditCommand { get; private set; }
    public ICommand? TestNtripConnectionCommand { get; private set; }

    private string _ntripTestStatus = string.Empty;
    public string NtripTestStatus
    {
        get => _ntripTestStatus;
        set => SetProperty(ref _ntripTestStatus, value);
    }

    private bool _isTestingNtripConnection;
    public bool IsTestingNtripConnection
    {
        get => _isTestingNtripConnection;
        set => SetProperty(ref _isTestingNtripConnection, value);
    }

    // New Field Dialog properties (visibility managed by State.UI)
    private string _newFieldName = string.Empty;
    public string NewFieldName
    {
        get => _newFieldName;
        set => SetProperty(ref _newFieldName, value);
    }

    private double _newFieldLatitude;
    public double NewFieldLatitude
    {
        get => _newFieldLatitude;
        set => SetProperty(ref _newFieldLatitude, value);
    }

    private double _newFieldLongitude;
    public double NewFieldLongitude
    {
        get => _newFieldLongitude;
        set => SetProperty(ref _newFieldLongitude, value);
    }

    public ICommand? CancelNewFieldDialogCommand { get; private set; }
    public ICommand? ConfirmNewFieldDialogCommand { get; private set; }

    // From Existing Field Dialog properties (visibility managed by State.UI)
    private string _fromExistingFieldName = string.Empty;
    public string FromExistingFieldName
    {
        get => _fromExistingFieldName;
        set => SetProperty(ref _fromExistingFieldName, value);
    }

    private FieldSelectionItem? _fromExistingSelectedField;
    public FieldSelectionItem? FromExistingSelectedField
    {
        get => _fromExistingSelectedField;
        set
        {
            SetProperty(ref _fromExistingSelectedField, value);
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
        set => SetProperty(ref _copyFlags, value);
    }

    private bool _copyMapping = true;
    public bool CopyMapping
    {
        get => _copyMapping;
        set => SetProperty(ref _copyMapping, value);
    }

    private bool _copyHeadland = true;
    public bool CopyHeadland
    {
        get => _copyHeadland;
        set => SetProperty(ref _copyHeadland, value);
    }

    private bool _copyLines = true;
    public bool CopyLines
    {
        get => _copyLines;
        set => SetProperty(ref _copyLines, value);
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
            SetProperty(ref _selectedKmlFile, value);
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
        set => SetProperty(ref _kmlImportFieldName, value);
    }

    private int _kmlBoundaryPointCount;
    public int KmlBoundaryPointCount
    {
        get => _kmlBoundaryPointCount;
        set => SetProperty(ref _kmlBoundaryPointCount, value);
    }

    private double _kmlCenterLatitude;
    public double KmlCenterLatitude
    {
        get => _kmlCenterLatitude;
        set => SetProperty(ref _kmlCenterLatitude, value);
    }

    private double _kmlCenterLongitude;
    public double KmlCenterLongitude
    {
        get => _kmlCenterLongitude;
        set => SetProperty(ref _kmlCenterLongitude, value);
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
            SetProperty(ref _selectedIsoXmlFile, value);
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
        set => SetProperty(ref _isoXmlImportFieldName, value);
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
        set => SetProperty(ref _boundaryMapCenterLatitude, value);
    }

    private double _boundaryMapCenterLongitude;
    public double BoundaryMapCenterLongitude
    {
        get => _boundaryMapCenterLongitude;
        set => SetProperty(ref _boundaryMapCenterLongitude, value);
    }

    private int _boundaryMapPointCount;
    public int BoundaryMapPointCount
    {
        get => _boundaryMapPointCount;
        set => SetProperty(ref _boundaryMapPointCount, value);
    }

    private string _boundaryMapCoordinateText = string.Empty;
    public string BoundaryMapCoordinateText
    {
        get => _boundaryMapCoordinateText;
        set => SetProperty(ref _boundaryMapCoordinateText, value);
    }

    private bool _boundaryMapIncludeBackground = true;
    public bool BoundaryMapIncludeBackground
    {
        get => _boundaryMapIncludeBackground;
        set => SetProperty(ref _boundaryMapIncludeBackground, value);
    }

    private bool _boundaryMapCanSave;
    public bool BoundaryMapCanSave
    {
        get => _boundaryMapCanSave;
        set => SetProperty(ref _boundaryMapCanSave, value);
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
        set => SetProperty(ref _numericInputDialogTitle, value);
    }

    private decimal? _numericInputDialogValue;
    public decimal? NumericInputDialogValue
    {
        get => _numericInputDialogValue;
        set => SetProperty(ref _numericInputDialogValue, value);
    }

    private string _numericInputDialogDisplayText = string.Empty;
    public string NumericInputDialogDisplayText
    {
        get => _numericInputDialogDisplayText;
        set => SetProperty(ref _numericInputDialogDisplayText, value);
    }

    private bool _numericInputDialogIntegerOnly;
    public bool NumericInputDialogIntegerOnly
    {
        get => _numericInputDialogIntegerOnly;
        set => SetProperty(ref _numericInputDialogIntegerOnly, value);
    }

    private bool _numericInputDialogAllowNegative = true;
    public bool NumericInputDialogAllowNegative
    {
        get => _numericInputDialogAllowNegative;
        set => SetProperty(ref _numericInputDialogAllowNegative, value);
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
        set => SetProperty(ref _agShareSettingsServerUrl, value);
    }

    private string _agShareSettingsApiKey = string.Empty;
    public string AgShareSettingsApiKey
    {
        get => _agShareSettingsApiKey;
        set => SetProperty(ref _agShareSettingsApiKey, value);
    }

    private bool _agShareSettingsEnabled;
    public bool AgShareSettingsEnabled
    {
        get => _agShareSettingsEnabled;
        set => SetProperty(ref _agShareSettingsEnabled, value);
    }

    public ICommand? CancelAgShareSettingsDialogCommand { get; private set; }
    public ICommand? ConfirmAgShareSettingsDialogCommand { get; private set; }

    // AgShare Upload Dialog (visibility managed by State.UI)
    public ICommand? CancelAgShareUploadDialogCommand { get; private set; }

    // AgShare Download Dialog (visibility managed by State.UI)
    public ICommand? CancelAgShareDownloadDialogCommand { get; private set; }

    // Data I/O Commands
    public ICommand? ShowDataIODialogCommand { get; private set; }
    public ICommand? CloseDataIODialogCommand { get; private set; }

    private void CloseDataIODialog()
    {
        State.UI.CloseDialog();
    }

    // iOS Modal Sheet Visibility Properties
    private bool _isFileMenuVisible;
    public bool IsFileMenuVisible
    {
        get => _isFileMenuVisible;
        set
        {
            if (SetProperty(ref _isFileMenuVisible, value) && value)
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
            if (SetProperty(ref _isFieldToolsVisible, value) && value)
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
            if (SetProperty(ref _isSettingsVisible, value) && value)
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
            if (SetProperty(ref _isBoundaryPanelVisible, value) && value)
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
        set => SetProperty(ref _selectedBoundaryIndex, value);
    }

    private bool _isBoundaryPlayerPanelVisible;
    public bool IsBoundaryPlayerPanelVisible
    {
        get => _isBoundaryPlayerPanelVisible;
        set => SetProperty(ref _isBoundaryPlayerPanelVisible, value);
    }

    private bool _isBoundaryRecording;
    public bool IsBoundaryRecording
    {
        get => _isBoundaryRecording;
        set => SetProperty(ref _isBoundaryRecording, value);
    }

    private int _boundaryPointCount;
    public int BoundaryPointCount
    {
        get => _boundaryPointCount;
        set => SetProperty(ref _boundaryPointCount, value);
    }

    private double _boundaryAreaHectares;
    public double BoundaryAreaHectares
    {
        get => _boundaryAreaHectares;
        set => SetProperty(ref _boundaryAreaHectares, value);
    }

    // Boundary Player settings
    private bool _isBoundarySectionControlOn;
    public bool IsBoundarySectionControlOn
    {
        get => _isBoundarySectionControlOn;
        set
        {
            if (SetProperty(ref _isBoundarySectionControlOn, value) != value) return;
            StatusMessage = value ? "Boundary records when section is on" : "Boundary section control off";
        }
    }

    private bool _isDrawRightSide = true;
    public bool IsDrawRightSide
    {
        get => _isDrawRightSide;
        set
        {
            if (SetProperty(ref _isDrawRightSide, value) != value) return;
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
            if (SetProperty(ref _isDrawAtPivot, value) != value) return;
            StatusMessage = value ? "Recording at pivot point" : "Recording at tool";
        }
    }

    private double _boundaryOffset;
    public double BoundaryOffset
    {
        get => _boundaryOffset;
        set
        {
            if (SetProperty(ref _boundaryOffset, value))
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
        set => SetProperty(ref _configurationViewModel, value);
    }

    // AutoSteer Configuration Panel
    private AutoSteerConfigViewModel? _autoSteerConfigViewModel;
    public AutoSteerConfigViewModel? AutoSteerConfigViewModel
    {
        get => _autoSteerConfigViewModel;
        set => SetProperty(ref _autoSteerConfigViewModel, value);
    }

    public ICommand? ShowConfigurationDialogCommand { get; private set; }
    public ICommand? CancelConfigurationDialogCommand { get; private set; }
    public ICommand? ShowAutoSteerConfigCommand { get; private set; }
    public ICommand? ShowLoadProfileDialogCommand { get; private set; }
    public ICommand? ShowNewProfileDialogCommand { get; private set; }
    public ICommand? LoadSelectedProfileCommand { get; private set; }
    public ICommand? CancelProfileSelectionCommand { get; private set; }

    // Profile selection dialog
    private bool _isProfileSelectionVisible;
    public bool IsProfileSelectionVisible
    {
        get => _isProfileSelectionVisible;
        set => SetProperty(ref _isProfileSelectionVisible, value);
    }

    private System.Collections.ObjectModel.ObservableCollection<string> _availableProfiles = new();
    public System.Collections.ObjectModel.ObservableCollection<string> AvailableProfiles
    {
        get => _availableProfiles;
        set => SetProperty(ref _availableProfiles, value);
    }

    private string? _selectedProfile;
    public string? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public string CurrentProfileName => _configurationService.Store.ActiveProfileName;

    // Headland Builder properties (visibility managed by State.UI)
    private bool _isHeadlandOn;
    public bool IsHeadlandOn
    {
        get => _isHeadlandOn;
        set
        {
            if (SetProperty(ref _isHeadlandOn, value))
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
        set => SetProperty(ref _isSectionControlInHeadland, value);
    }

    // UTurnSkipRows and IsUTurnSkipRowsEnabled are now in MainViewModel.YouTurn.cs

    private double _headlandDistance = 12.0;
    public double HeadlandDistance
    {
        get => _headlandDistance;
        set => SetProperty(ref _headlandDistance, Math.Max(1.0, Math.Min(100.0, value)));
    }

    private int _headlandPasses = 1;
    public int HeadlandPasses
    {
        get => _headlandPasses;
        set => SetProperty(ref _headlandPasses, Math.Max(1, Math.Min(5, value)));
    }

    private List<Models.Base.Vec3>? _currentHeadlandLine;
    public List<Models.Base.Vec3>? CurrentHeadlandLine
    {
        get => _currentHeadlandLine;
        set
        {
            SetProperty(ref _currentHeadlandLine, value);
            _mapService.SetHeadlandLine(value);
            SaveHeadlandToFile(value);

            // Sync to FieldState for section control headland detection
            State.Field.HeadlandLine = value;
        }
    }

    private List<Models.Base.Vec2>? _headlandPreviewLine;
    public List<Models.Base.Vec2>? HeadlandPreviewLine
    {
        get => _headlandPreviewLine;
        set
        {
            SetProperty(ref _headlandPreviewLine, value);
            _mapService.SetHeadlandPreview(value);
        }
    }

    private bool _hasHeadland;
    public bool HasHeadland
    {
        get => _hasHeadland;
        set => SetProperty(ref _hasHeadland, value);
    }

    // Bottom strip state properties (matching AgOpenGPS conditional button visibility)
    private bool _hasActiveTrack;
    /// <summary>
    /// True when an AB line or track is active for guidance (equivalent to AgOpenGPS trk.idx > -1)
    /// </summary>
    public bool HasActiveTrack
    {
        get => _hasActiveTrack;
        set => SetProperty(ref _hasActiveTrack, value);
    }

    private bool _hasBoundary;
    /// <summary>
    /// True when a field boundary exists (equivalent to AgOpenGPS isBnd)
    /// </summary>
    public bool HasBoundary
    {
        get => _hasBoundary;
        set => SetProperty(ref _hasBoundary, value);
    }

    private bool _isNudgeEnabled;
    /// <summary>
    /// True when AB line nudging is enabled (controls visibility of snap/adjust buttons)
    /// </summary>
    public bool IsNudgeEnabled
    {
        get => _isNudgeEnabled;
        set => SetProperty(ref _isNudgeEnabled, value);
    }

    /// <summary>
    /// Gets the current field's boundary for use in the headland editor.
    /// </summary>
    private Boundary? _currentBoundary;
    public Boundary? CurrentBoundary
    {
        get => _currentBoundary;
        private set => SetProperty(ref _currentBoundary, value);
    }

    // Headland Dialog properties (visibility managed by State.UI)
    private bool _isHeadlandCurveMode = true;
    public bool IsHeadlandCurveMode
    {
        get => _isHeadlandCurveMode;
        set
        {
            var oldValue = _isHeadlandCurveMode;
            if (SetProperty(ref _isHeadlandCurveMode, value))
            {
                OnPropertyChanged(nameof(IsHeadlandLineMode));
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
        set => SetProperty(ref _isHeadlandZoomMode, value);
    }

    private bool _isHeadlandSectionControlled = true;
    public bool IsHeadlandSectionControlled
    {
        get => _isHeadlandSectionControlled;
        set => SetProperty(ref _isHeadlandSectionControlled, value);
    }

    private int _headlandToolWidthMultiplier = 1;
    public int HeadlandToolWidthMultiplier
    {
        get => _headlandToolWidthMultiplier;
        set
        {
            SetProperty(ref _headlandToolWidthMultiplier, value);
            OnPropertyChanged(nameof(HeadlandCalculatedWidth));
            // Update distance based on tool width multiplier
            if (value > 0)
            {
                HeadlandDistance = ConfigStore.ActualToolWidth * value;
            }
        }
    }

    public double HeadlandCalculatedWidth => ConfigStore.ActualToolWidth * _headlandToolWidthMultiplier;

    // Headland point selection (for clipping headland via boundary points)
    // Each point is stored as (segmentIndex, t parameter 0-1, world position)
    private int _headlandPoint1Index = -1;
    public int HeadlandPoint1Index
    {
        get => _headlandPoint1Index;
        set
        {
            SetProperty(ref _headlandPoint1Index, value);
            OnPropertyChanged(nameof(HeadlandPointsSelected));
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
            SetProperty(ref _headlandPoint2Index, value);
            OnPropertyChanged(nameof(HeadlandPointsSelected));
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
        set => SetProperty(ref _headlandSelectedMarkers, value);
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
        set => SetProperty(ref _isFieldOpen, value);
    }

    private string _currentFieldName = string.Empty;
    public string CurrentFieldName
    {
        get => _currentFieldName;
        set => SetProperty(ref _currentFieldName, value);
    }

    // Simulator properties
    private bool _isSimulatorEnabled;
    public bool IsSimulatorEnabled
    {
        get => _isSimulatorEnabled;
        set
        {
            if (SetProperty(ref _isSimulatorEnabled, value))
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
            SetProperty(ref _simulatorSteerAngle, value);
            State.Simulator.SteerAngle = value;
            OnPropertyChanged(nameof(SimulatorSteerAngleDisplay)); // Notify display property
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
            SetProperty(ref _simulatorSpeedKph, value);
            State.Simulator.Speed = value;
            State.Simulator.TargetSpeed = value;
            OnPropertyChanged(nameof(SimulatorSpeedDisplay));
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
            OnPropertyChanged();
        }
    }

    public bool IsDayMode
    {
        get => _displaySettings.IsDayMode;
        set
        {
            _displaySettings.IsDayMode = value;
            OnPropertyChanged();
        }
    }

    public double CameraPitch
    {
        get => _displaySettings.CameraPitch;
        set
        {
            _displaySettings.CameraPitch = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Is2DMode));
        }
    }

    public bool Is2DMode
    {
        get => _displaySettings.Is2DMode;
        set
        {
            _displaySettings.Is2DMode = value;
            OnPropertyChanged();
        }
    }

    public bool IsNorthUp
    {
        get => _displaySettings.IsNorthUp;
        set
        {
            _displaySettings.IsNorthUp = value;
            OnPropertyChanged();
        }
    }

    public int Brightness
    {
        get => _displaySettings.Brightness;
        set
        {
            _displaySettings.Brightness = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BrightnessDisplay));
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
    public ICommand? DeleteContoursCommand { get; private set; }
    public ICommand? DeleteAppliedAreaCommand { get; private set; }
    public ICommand? ToggleManualModeCommand { get; private set; }
    public ICommand? ToggleSectionMasterCommand { get; private set; }
    public ICommand? ToggleSectionCommand { get; private set; }
    public ICommand? ToggleYouTurnCommand { get; private set; }
    public ICommand? ToggleAutoSteerCommand { get; private set; }


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
            _logger.LogDebug($"[LoadBackgroundImage] Error loading background image: {ex.Message}");
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

        // Sync to FieldState for section control boundary/headland detection
        State.Field.CurrentBoundary = boundary;
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

            // Calculate area from boundary if available
            double area = 0;
            var boundary = _boundaryFileService.LoadBoundary(dirPath);
            if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid)
            {
                area = boundary.OuterBoundary.AreaHectares;
            }

            // Get NTRIP profile name for this field
            string ntripProfileName = string.Empty;
            var ntripProfile = _ntripProfileService.GetProfileForField(fieldName);
            if (ntripProfile != null)
            {
                // Show profile name, with "(Default)" suffix if it's the default profile
                // and not specifically associated with this field
                var isSpecificallyAssociated = ntripProfile.AssociatedFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
                ntripProfileName = isSpecificallyAssociated
                    ? ntripProfile.Name
                    : $"{ntripProfile.Name} (Default)";
            }

            var item = new FieldSelectionItem(fieldName, dirPath, 0, area, ntripProfileName);
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
            _logger.LogDebug($"[Headland] No valid boundary for point selection");
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

            _logger.LogDebug($"[Headland] Curve mode - Clicked ({easting:F1}, {northing:F1}), nearest headland segment: {headlandSegmentIndex}, t: {headlandT:F2}, pos: ({nearestX:F1}, {nearestY:F1}), dist: {Math.Sqrt(minDistSq):F2}m");
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

            _logger.LogDebug($"[Headland] Line mode - Clicked ({easting:F1}, {northing:F1}), nearest boundary segment: {nearestSegmentIndex}, t: {nearestT:F2}, pos: ({nearestX:F1}, {nearestY:F1}), dist: {Math.Sqrt(minDistSq):F2}m");
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
        OnPropertyChanged(nameof(HeadlandClipPath));
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

        _logger.LogDebug($"[Headland] Creating headland from {segmentPoints.Count} boundary points (forward: {forwardPath.Count}, backward: {backwardPath.Count}), distance: {HeadlandDistance:F1}m");

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

        _logger.LogDebug($"[Headland] Created {headlandPoints.Count} headland points");

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

        _logger.LogDebug($"[Headland] Clipping at line from ({clipStart.Easting:F1}, {clipStart.Northing:F1}) to ({clipEnd.Easting:F1}, {clipEnd.Northing:F1})");

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

        _logger.LogDebug($"[Headland] Found {intersections.Count} intersections with clip line");

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
            _logger.LogDebug($"[Headland] Curve mode: taking longer path ({clippedHeadland.Count} points)");
        }
        else
        {
            // Line mode: take the shorter path
            clippedHeadland = forwardPath.Count <= backwardPath.Count ? forwardPath : backwardPath;
            _logger.LogDebug($"[Headland] Line mode: taking shorter path ({clippedHeadland.Count} points)");
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

        _logger.LogDebug($"[Headland] Clipped headland has {clippedHeadland.Count} points");

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

        _logger.LogDebug($"[Headland] BuildClipPath(forward={forward}): {path.Count} points, cut1 seg={cut1.segmentIndex}, cut2 seg={cut2.segmentIndex}");

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
            _logger.LogDebug($"[Headland] Saved headland to {activeField.DirectoryPath} ({headlandPoints?.Count ?? 0} points)");
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug($"[Headland] Failed to save headland: {ex.Message}");
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
            _logger.LogDebug($"[TrackFiles] Saved {SavedTracks.Count} tracks to TrackLines.txt");
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug($"[TrackFiles] Failed to save tracks: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes the NtripProfiles collection from the service
    /// </summary>
    private void RefreshNtripProfiles()
    {
        NtripProfiles.Clear();
        foreach (var profile in _ntripProfileService.Profiles)
        {
            NtripProfiles.Add(profile);
        }
    }

    /// <summary>
    /// Populates the available fields list for NTRIP profile editing
    /// </summary>
    private void PopulateAvailableFieldsForProfile(NtripProfile profile)
    {
        AvailableFieldsForProfile.Clear();

        var availableFields = _ntripProfileService.GetAvailableFields();
        foreach (var fieldName in availableFields)
        {
            AvailableFieldsForProfile.Add(new FieldAssociationItem
            {
                FieldName = fieldName,
                IsSelected = profile.AssociatedFields.Contains(fieldName)
            });
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
            _logger.LogDebug("[TrackFiles] No field directory to load from");
            return;
        }

        try
        {
            // Try TrackLines.txt first (WinForms format)
            if (Services.TrackFilesService.Exists(field.DirectoryPath))
            {
                var tracks = Services.TrackFilesService.LoadTracks(field.DirectoryPath);
                int loadedCount = 0;
                Track? firstTrack = null;

                foreach (var track in tracks)
                {
                    // Ensure all tracks start inactive (SelectedTrack setter will activate)
                    track.IsActive = false;
                    State.Field.Tracks.Add(track);
                    SavedTracks.Add(track);

                    if (loadedCount == 0)
                    {
                        firstTrack = track;
                    }
                    loadedCount++;
                }

                _logger.LogDebug($"[TrackFiles] Loaded {loadedCount} tracks from TrackLines.txt");

                // Don't auto-activate any track - user must explicitly select one
                // HasActiveTrack and IsAutoSteerAvailable stay false until user selects
                return;
            }

            // Fallback to legacy ABLines.txt format
            var legacyFilePath = System.IO.Path.Combine(field.DirectoryPath, "ABLines.txt");
            if (System.IO.File.Exists(legacyFilePath))
            {
                _logger.LogDebug($"[TrackFiles] TrackLines.txt not found, trying legacy ABLines.txt");
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
                            // Don't auto-activate - user must explicitly select
                            track.IsActive = false;

                            State.Field.Tracks.Add(track);
                            SavedTracks.Add(track);
                            loadedCount++;
                        }
                    }
                }

                _logger.LogDebug($"[TrackFiles] Loaded {loadedCount} tracks from legacy ABLines.txt");

                // Don't auto-activate any track - user must explicitly select one
                // HasActiveTrack and IsAutoSteerAvailable stay false until user selects
            }
            else
            {
                _logger.LogDebug($"[TrackFiles] No track files found in {field.DirectoryPath}");
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug($"[TrackFiles] Failed to load tracks: {ex.Message}");
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
/// View model item for field selection list display.
/// </summary>
/// <param name="Name">Field name (directory name).</param>
/// <param name="DirectoryPath">Full path to the field directory.</param>
/// <param name="Distance">Distance to field (currently unused).</param>
/// <param name="Area">Field area in hectares.</param>
/// <param name="NtripProfileName">Associated NTRIP profile name.</param>
public record FieldSelectionItem(
    string Name,
    string DirectoryPath,
    double Distance,
    double Area,
    string NtripProfileName);

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