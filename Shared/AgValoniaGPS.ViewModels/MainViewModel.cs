using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.GPS;
using Avalonia.Threading;

namespace AgValoniaGPS.ViewModels;

public class MainViewModel : ReactiveObject
{
    private readonly IUdpCommunicationService _udpService;
    private readonly AgValoniaGPS.Services.Interfaces.IGpsService _gpsService;
    private readonly IFieldService _fieldService;
    private readonly IGuidanceService _guidanceService;
    private readonly INtripClientService _ntripService;
    private readonly AgValoniaGPS.Services.Interfaces.IDisplaySettingsService _displaySettings;
    private readonly AgValoniaGPS.Services.Interfaces.IFieldStatisticsService _fieldStatistics;
    private readonly AgValoniaGPS.Services.Interfaces.IGpsSimulationService _simulatorService;
    private readonly VehicleConfiguration _vehicleConfig;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IMapService _mapService;
    private readonly IBoundaryRecordingService _boundaryRecordingService;
    private readonly BoundaryFileService _boundaryFileService;
    private readonly NmeaParserService _nmeaParser;
    private readonly DispatcherTimer _simulatorTimer;
    private AgValoniaGPS.Models.LocalPlane? _simulatorLocalPlane;

    private string _statusMessage = "Starting...";
    private double _latitude;
    private double _longitude;
    private double _speed;
    private int _satelliteCount;
    private string _fixQuality = "No Fix";
    private string _networkStatus = "Disconnected";

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
        IGuidanceService guidanceService,
        INtripClientService ntripService,
        AgValoniaGPS.Services.Interfaces.IDisplaySettingsService displaySettings,
        AgValoniaGPS.Services.Interfaces.IFieldStatisticsService fieldStatistics,
        AgValoniaGPS.Services.Interfaces.IGpsSimulationService simulatorService,
        VehicleConfiguration vehicleConfig,
        ISettingsService settingsService,
        IDialogService dialogService,
        IMapService mapService,
        IBoundaryRecordingService boundaryRecordingService,
        BoundaryFileService boundaryFileService)
    {
        _udpService = udpService;
        _gpsService = gpsService;
        _fieldService = fieldService;
        _guidanceService = guidanceService;
        _ntripService = ntripService;
        _displaySettings = displaySettings;
        _fieldStatistics = fieldStatistics;
        _simulatorService = simulatorService;
        _vehicleConfig = vehicleConfig;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _mapService = mapService;
        _boundaryRecordingService = boundaryRecordingService;
        _boundaryFileService = boundaryFileService;
        _nmeaParser = new NmeaParserService(gpsService);

        // Subscribe to events
        _gpsService.GpsDataUpdated += OnGpsDataUpdated;
        _udpService.DataReceived += OnUdpDataReceived;
        _udpService.ModuleConnectionChanged += OnModuleConnectionChanged;
        _ntripService.ConnectionStatusChanged += OnNtripConnectionChanged;
        _ntripService.RtcmDataReceived += OnRtcmDataReceived;
        _fieldService.ActiveFieldChanged += OnActiveFieldChanged;
        _simulatorService.GpsDataUpdated += OnSimulatorGpsDataUpdated;
        _boundaryRecordingService.PointAdded += OnBoundaryPointAdded;
        _boundaryRecordingService.StateChanged += OnBoundaryStateChanged;

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

            // Update UI properties
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
    }

    // Boundary recording event handlers
    private void OnBoundaryPointAdded(object? sender, BoundaryPointAddedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            BoundaryPointCount = e.TotalPoints;
            BoundaryAreaHectares = e.AreaHectares;
        });
    }

    private void OnBoundaryStateChanged(object? sender, BoundaryRecordingStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsBoundaryRecording = e.State == BoundaryRecordingState.Recording;
            BoundaryPointCount = e.PointCount;
            BoundaryAreaHectares = e.AreaHectares;
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

    // AOG_Dev services - expose for UI/control access
    public VehicleConfiguration VehicleConfig => _vehicleConfig;
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
                // Save to settings
                _settingsService.Settings.SimulatorEnabled = value;
                _settingsService.Save();

                // Start or stop simulator timer based on enabled state
                if (value)
                {
                    _simulatorTimer.Start();
                    StatusMessage = "Simulator ON";
                }
                else
                {
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
        _settingsService.Save();

        // Also update the Latitude/Longitude properties directly so that
        // the map boundary dialog uses the correct coordinates even if
        // the simulator timer hasn't ticked yet
        Latitude = latitude;
        Longitude = longitude;

        StatusMessage = $"Simulator reset to {latitude:F7}, {longitude:F7}";
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
    public ICommand? ShowDataIODialogCommand { get; private set; }
    public ICommand? ShowSimCoordsDialogCommand { get; private set; }
    public ICommand? ShowFieldSelectionDialogCommand { get; private set; }
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
    public ICommand? BuildFromTracksCommand { get; private set; }
    public ICommand? DriveAroundFieldCommand { get; private set; }
    public ICommand? ToggleRecordingCommand { get; private set; }
    public ICommand? ToggleBoundaryLeftRightCommand { get; private set; }
    public ICommand? ToggleBoundaryAntennaToolCommand { get; private set; }
    public ICommand? ShowBoundaryOffsetDialogCommand { get; private set; }

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
        ShowDataIODialogCommand = new AsyncRelayCommand(async () =>
        {
            await _dialogService.ShowDataIODialogAsync();
        });

        ShowSimCoordsDialogCommand = new AsyncRelayCommand(async () =>
        {
            if (!IsSimulatorEnabled)
            {
                await _dialogService.ShowMessageAsync("Simulator Not Enabled", "Please enable the simulator first.");
                return;
            }
            var currentPos = GetSimulatorPosition();
            var result = await _dialogService.ShowSimCoordsDialogAsync(currentPos.Latitude, currentPos.Longitude);
            if (result.HasValue)
            {
                SetSimulatorCoordinates(result.Value.Latitude, result.Value.Longitude);
            }
        });

        ShowFieldSelectionDialogCommand = new AsyncRelayCommand(async () =>
        {
            // Use settings directory which defaults to ~/Documents/AgValoniaGPS/Fields
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrWhiteSpace(fieldsDir))
            {
                fieldsDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AgValoniaGPS", "Fields");
            }
            var result = await _dialogService.ShowFieldSelectionDialogAsync(fieldsDir);
            if (result != null)
            {
                FieldsRootDirectory = fieldsDir;
                CurrentFieldName = result.FieldName;
                IsFieldOpen = true;

                // Save as last opened field
                _settingsService.Settings.LastOpenedField = result.FieldName;
                _settingsService.Save();

                if (result.Boundary != null)
                {
                    _mapService.SetBoundary(result.Boundary);
                    CenterMapOnBoundary(result.Boundary);
                }

                IsJobMenuPanelVisible = false;
                StatusMessage = $"Opened field: {result.FieldName}";
            }
        });

        ShowNewFieldDialogCommand = new AsyncRelayCommand(async () =>
        {
            var currentPosition = new Position
            {
                Latitude = Latitude != 0 ? Latitude : 40.7128,
                Longitude = Longitude != 0 ? Longitude : -74.0060,
                Altitude = 0
            };
            var result = await _dialogService.ShowNewFieldDialogAsync(currentPosition);
            if (result != null)
            {
                CurrentFieldName = result.FieldName;
                IsFieldOpen = true;
                StatusMessage = $"Created field: {result.FieldName}";
            }
        });

        ShowFromExistingFieldDialogCommand = new AsyncRelayCommand(async () =>
        {
            var result = await _dialogService.ShowFromExistingFieldDialogAsync(_settingsService.Settings.FieldsDirectory);
            if (result != null)
            {
                CurrentFieldName = result.NewFieldName;
                IsFieldOpen = true;
                StatusMessage = $"Created field from existing: {result.NewFieldName}";
            }
        });

        ShowIsoXmlImportDialogCommand = new AsyncRelayCommand(async () =>
        {
            var result = await _dialogService.ShowIsoXmlImportDialogAsync(_settingsService.Settings.FieldsDirectory);
            if (result != null)
            {
                CurrentFieldName = result.FieldName;
                IsFieldOpen = true;
                if (result.ImportedBoundary != null)
                {
                    _mapService.SetBoundary(result.ImportedBoundary);
                }
                StatusMessage = $"Imported ISO-XML: {result.FieldName}";
            }
        });

        ShowKmlImportDialogCommand = new AsyncRelayCommand(async () =>
        {
            var result = await _dialogService.ShowKmlImportDialogAsync(_settingsService.Settings.FieldsDirectory);
            if (result != null)
            {
                CurrentFieldName = result.FieldName;
                IsFieldOpen = true;
                if (result.ImportedBoundary != null)
                {
                    _mapService.SetBoundary(result.ImportedBoundary);
                }
                StatusMessage = $"Imported KML: {result.FieldName}";
            }
        });

        ShowAgShareDownloadDialogCommand = new AsyncRelayCommand(async () =>
        {
            var result = await _dialogService.ShowAgShareDownloadDialogAsync(
                _settingsService.Settings.AgShareApiKey,
                _settingsService.Settings.FieldsDirectory);
            if (result != null)
            {
                CurrentFieldName = result.FieldName;
                IsFieldOpen = true;
                StatusMessage = $"Downloaded from AgShare: {result.FieldName}";
            }
        });

        ShowAgShareUploadDialogCommand = new AsyncRelayCommand(async () =>
        {
            if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName))
            {
                await _dialogService.ShowMessageAsync("No Field Open", "Please open a field first.");
                return;
            }
            var fieldDir = System.IO.Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
            var success = await _dialogService.ShowAgShareUploadDialogAsync(
                _settingsService.Settings.AgShareApiKey,
                CurrentFieldName,
                fieldDir);
            if (success)
            {
                StatusMessage = $"Uploaded to AgShare: {CurrentFieldName}";
            }
        });

        ShowAgShareSettingsDialogCommand = new AsyncRelayCommand(async () =>
        {
            await _dialogService.ShowAgShareSettingsDialogAsync();
        });

        ShowBoundaryDialogCommand = new RelayCommand(() =>
        {
            // Toggle boundary panel visibility - this shows the panel where user can
            // choose how to create the boundary (KML import, drive around, etc.)
            IsBoundaryPanelVisible = !IsBoundaryPanelVisible;
        });

        // Field Commands
        CloseFieldCommand = new RelayCommand(() =>
        {
            CurrentFieldName = string.Empty;
            IsFieldOpen = false;
            _mapService.SetBoundary(null);
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

            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            var fieldPath = Path.Combine(fieldsDir, lastField);

            if (!_fieldService.FieldExists(fieldPath))
            {
                StatusMessage = $"Field not found: {lastField}";
                return;
            }

            try
            {
                var field = _fieldService.LoadField(fieldPath);
                _fieldService.SetActiveField(field);

                CurrentFieldName = lastField;
                IsFieldOpen = true;

                if (field.Boundary != null)
                {
                    _mapService.SetBoundary(field.Boundary);
                    CenterMapOnBoundary(field.Boundary);
                }

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
                    _mapService.SetBoundary(boundary);
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

        ShowBoundaryOffsetDialogCommand = new AsyncRelayCommand(async () =>
        {
            var result = await _dialogService.ShowNumericInputDialogAsync(
                "Boundary Offset (cm)",
                BoundaryOffset,
                minValue: 0,
                maxValue: 500,
                decimalPlaces: 0);

            if (result.HasValue)
            {
                BoundaryOffset = result.Value;
                StatusMessage = $"Boundary offset set to {BoundaryOffset:F0} cm";
            }
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
                    _mapService.SetBoundary(boundary);

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

        DrawMapBoundaryCommand = new AsyncRelayCommand(async () =>
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

                        // Update map
                        _mapService.SetBoundary(boundary);
                        CenterMapOnBoundary(boundary);

                        // Refresh the boundary list
                        RefreshBoundaryList();
                    }

                    // Process background image if present
                    if (result.HasBackgroundImage && !string.IsNullOrEmpty(result.BackgroundImagePath) && localPlane != null)
                    {
                        // Convert WGS84 corners to local coordinates
                        var nwWgs84 = new Wgs84(result.NorthWestLat, result.NorthWestLon);
                        var seWgs84 = new Wgs84(result.SouthEastLat, result.SouthEastLon);

                        var nwLocal = localPlane.ConvertWgs84ToGeoCoord(nwWgs84);
                        var seLocal = localPlane.ConvertWgs84ToGeoCoord(seWgs84);

                        // Copy background image to field directory
                        var destPngPath = Path.Combine(fieldPath, "BackPic.png");
                        File.Copy(result.BackgroundImagePath, destPngPath, overwrite: true);

                        // Save geo-reference file
                        var geoFilePath = Path.Combine(fieldPath, "BackPic.txt");
                        using (var writer = new StreamWriter(geoFilePath))
                        {
                            writer.WriteLine("$BackPic");
                            writer.WriteLine("true");
                            writer.WriteLine(seLocal.Easting.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            writer.WriteLine(nwLocal.Easting.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            writer.WriteLine(nwLocal.Northing.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            writer.WriteLine(seLocal.Northing.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }

                        // Set the background image in the map
                        _mapService.SetBackgroundImage(destPngPath, nwLocal.Easting, nwLocal.Northing, seLocal.Easting, seLocal.Northing);
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
            _mapService.SetBoundary(boundary);
            StatusMessage = "Boundary deleted";
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