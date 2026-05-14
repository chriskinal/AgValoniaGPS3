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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Job;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.Timing;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Fields;
using AgValoniaGPS.Services.YouTurn;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.GPS;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Communication;
using AgValoniaGPS.Models.Ntrip;
using AgValoniaGPS.Models.Diagnostics;
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
    private readonly IMapService _mapService;
    private readonly IBoundaryRecordingService _boundaryRecordingService;
    private readonly IBoundaryBuilderService _boundaryBuilderService;
    private readonly BoundaryFileService _boundaryFileService;
    private readonly Services.Headland.IHeadlandBuilderService _headlandBuilderService;
    private readonly ITrackGuidanceService _trackGuidanceService;
    private readonly YouTurnCreationService _youTurnCreationService;
    private readonly YouTurnPathingService _youTurnPathingService;
    private readonly Services.Geometry.IPolygonOffsetService _polygonOffsetService;
    private readonly Services.Interfaces.ITurnAreaService _turnAreaService;
    private readonly YouTurnGuidanceService _youTurnGuidanceService;
    private readonly FieldPlaneFileService _fieldPlaneFileService;
    private readonly IVehicleProfileService _vehicleProfileService;
    private readonly IConfigurationService _configurationService;
    private readonly IAutoSteerService _autoSteerService;
    private readonly ISmartWasCalibrationService _smartWasService;
    private readonly ITrackCopierService _trackCopierService;
    private readonly IModuleCommunicationService _moduleCommunicationService;
    private readonly IToolPositionService _toolPositionService;
    private readonly ICoverageMapService _coverageMapService;
    private readonly ISectionControlService _sectionControlService;
    private readonly INtripProfileService _ntripProfileService;
    private readonly IChartDataService _chartDataService;
    private readonly IAudioService _audioService;
    private readonly IElevationLogService _elevationLogService;
    private readonly IJobService _jobService;
    private readonly ITramLineService _tramLineService;
    private bool _hasTramSystemsEverUsed;
    private readonly Dictionary<string, (int start, int count, bool isBoundary)> _tramSystemLineRanges = new();
    private readonly IGpsPipelineService _gpsPipelineService;
    private readonly ISteerMachineLoopService? _controlLoop;
    private readonly IPositionEstimator? _positionEstimator;
    private readonly IPipelineIntents _intents;
    private readonly ILogger<MainViewModel> _logger;
    private readonly ApplicationState _appState;
    private readonly Avalonia.Threading.DispatcherTimer _simulatorTimer;
    private Avalonia.Threading.DispatcherTimer? _renderPullTimer;

    /// <summary>
    /// Centralized application state - single source of truth for all runtime state.
    /// Use this for new code. Existing properties will gradually migrate to use State.
    /// </summary>
    public ApplicationState State => _appState;
    public DisplayConfig Display => ConfigurationStore.Instance.Display;

    // Convenience accessors for ConfigurationStore (replaces _vehicleConfig usage)
    private static ConfigurationStore ConfigStore => ConfigurationStore.Instance;
    private static VehicleConfig Vehicle => ConfigurationStore.Instance.Vehicle;
    private static ToolConfig Tool => ConfigurationStore.Instance.Tool;
    private static GuidanceConfig Guidance => ConfigurationStore.Instance.Guidance;

    // Current field origin (for map centering when GPS not active)
    private double _fieldOriginLatitude;
    private double _fieldOriginLongitude;

    /// <summary>
    /// Sets the field origin and propagates it into centralized FieldState so
    /// non-ViewModel consumers (map control, services) can read the LocalPlane.
    /// </summary>
    private void SetFieldOrigin(double latitude, double longitude)
    {
        _fieldOriginLatitude = latitude;
        _fieldOriginLongitude = longitude;
        _simulatorLocalPlane = null;

        State.Field.OriginLatitude = latitude;
        State.Field.OriginLongitude = longitude;
        State.Field.LocalPlane = new LocalPlane(
            new Wgs84(latitude, longitude),
            new SharedFieldProperties());
    }

    // Track-on-boundary detection: skip boundary disengage on first pass
    private bool _isSelectedTrackOnBoundary;

    // Track guidance state is now in MainViewModel.Guidance.cs
    // YouTurn state is now in MainViewModel.YouTurn.cs

    private string _statusMessage = "Starting...";
    private string _networkStatus = "Disconnected";
    private double _currentFps;
    private double _gpsToPgnLatencyMs;

    // Guidance/Steering status
    private double _crossTrackError;
    private string _currentGuidanceLine = "1L";
    private bool _isAutoSteerActive;
    private int _activeSections;

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
        IMapService mapService,
        IBoundaryRecordingService boundaryRecordingService,
        IBoundaryBuilderService boundaryBuilderService,
        BoundaryFileService boundaryFileService,
        Services.Headland.IHeadlandBuilderService headlandBuilderService,
        ITrackGuidanceService trackGuidanceService,
        YouTurnCreationService youTurnCreationService,
        YouTurnGuidanceService youTurnGuidanceService,
        YouTurnPathingService youTurnPathingService,
        Services.Geometry.IPolygonOffsetService polygonOffsetService,
        Services.Interfaces.ITurnAreaService turnAreaService,
        IVehicleProfileService vehicleProfileService,
        IConfigurationService configurationService,
        IAutoSteerService autoSteerService,
        ISmartWasCalibrationService smartWasService,
        ITrackCopierService trackCopierService,
        IModuleCommunicationService moduleCommunicationService,
        IToolPositionService toolPositionService,
        ICoverageMapService coverageMapService,
        ISectionControlService sectionControlService,
        INtripProfileService ntripProfileService,
        IChartDataService chartDataService,
        IAudioService audioService,
        IElevationLogService elevationLogService,
        IJobService jobService,
        ITramLineService tramLineService,
        IGpsPipelineService gpsPipelineService,
        IPipelineIntents intents,
        ILogger<MainViewModel> logger,
        ApplicationState appState,
        ISteerMachineLoopService? controlLoop = null,
        IPositionEstimator? positionEstimator = null)
    {
        _logger = logger;
        _tramLineService = tramLineService;

        // Sync GuidanceConfig.TramDisplay -> TramConfig.DisplayMode and regenerate
        ConfigStore.Guidance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Models.Configuration.GuidanceConfig.TramDisplay))
            {
                ConfigStore.Tram.DisplayMode = ConfigStore.Guidance.TramDisplay
                    ? Models.Configuration.TramDisplayMode.All
                    : Models.Configuration.TramDisplayMode.Off;
                UpdateTramLines(SelectedTrack);
            }
            else if (e.PropertyName == nameof(Models.Configuration.GuidanceConfig.TramPasses))
            {
                ConfigStore.Tram.Passes = ConfigStore.Guidance.TramPasses;
                UpdateTramLines(SelectedTrack);
            }
        };
        _udpService = udpService;
        _gpsService = gpsService;
        _fieldService = fieldService;
        _ntripService = ntripService;
        _displaySettings = displaySettings;
        _fieldStatistics = fieldStatistics;
        _simulatorService = simulatorService;
        _settingsService = settingsService;
        _mapService = mapService;
        _boundaryRecordingService = boundaryRecordingService;
        _boundaryBuilderService = boundaryBuilderService;
        _boundaryFileService = boundaryFileService;
        _headlandBuilderService = headlandBuilderService;
        _trackGuidanceService = trackGuidanceService;
        _youTurnCreationService = youTurnCreationService;
        _youTurnGuidanceService = youTurnGuidanceService;
        _youTurnPathingService = youTurnPathingService;
        _polygonOffsetService = polygonOffsetService;
        _turnAreaService = turnAreaService;
        _vehicleProfileService = vehicleProfileService;
        _configurationService = configurationService;
        // Refresh status-bar bindings whenever the active profile changes
        // (load / save / picker dialog) so labels like CurrentProfileName
        // re-render. The store updates correctly on its own, but bindings
        // through computed properties on this VM need an explicit notify.
        _configurationService.ProfileLoaded += (_, _) => RaiseProfileNameChanged();
        _configurationService.ProfileSaved += (_, _) => RaiseProfileNameChanged();
        _autoSteerService = autoSteerService;
        _smartWasService = smartWasService;
        _trackCopierService = trackCopierService;
        _moduleCommunicationService = moduleCommunicationService;
        _toolPositionService = toolPositionService;
        _coverageMapService = coverageMapService;
        _sectionControlService = sectionControlService;
        _ntripProfileService = ntripProfileService;
        _chartDataService = chartDataService;
        _audioService = audioService;
        _elevationLogService = elevationLogService;
        _jobService = jobService;
        // Refresh the field/job pill + status strip whenever the active job
        // changes (created, resumed, suspended).
        _jobService.ActiveJobChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentJobTaskName));
            OnPropertyChanged(nameof(CurrentFieldAndJobLabel));
        };
        _gpsPipelineService = gpsPipelineService;
        _controlLoop = controlLoop;
        _positionEstimator = positionEstimator;
        _intents = intents;
        _appState = appState;
        _fieldPlaneFileService = new FieldPlaneFileService();

        // Subscribe to events
        _gpsService.GpsDataUpdated += OnGpsDataUpdated;
        _autoSteerService.StateUpdated += OnAutoSteerStateUpdated;
        (_autoSteerService as Services.AutoSteer.AutoSteerService)?.SetTramLineService(_tramLineService);
        (_autoSteerService as Services.AutoSteer.AutoSteerService)?.SetSmartWasService(_smartWasService);
        _autoSteerService.Start(); // Enable zero-copy GPS pipeline

        // Start the background GPS processing pipeline
        _gpsPipelineService.CycleCompleted += OnGpsCycleCompleted;
        _gpsPipelineService.Start();

        // Host control loop (#313): runs at 100 Hz on its own thread.
        // Each tick: read interpolated pose from estimator, update tool
        // position + section state machine, send PGN 254 + PGN 239. This
        // gives sub-frame section edge accuracy (~0.05 m at 25 km/h vs
        // ~0.7 m on the prior 10 Hz path) and matches the firmware
        // autosteer cadence so PGNs land fresh every firmware tick.
        // Optional in test builds.
        if (_controlLoop is not null)
        {
            // TickHz is on the concrete SectionControlService, not the
            // interface (it's an internal-tuning concern, not a contract).
            if (_sectionControlService is Services.Section.SectionControlService scsConcrete)
                scsConcrete.TickHz = _controlLoop.FrequencyHz;
            _controlLoop.Ticked += OnControlLoopTicked;
            _controlLoop.Start();
        }

        // Renderer-pull timer (#313 commit 6): when a position estimator is
        // wired, push the latest dead-reckoned vehicle pose to the map at
        // ~30 Hz so the chevron moves smoothly between GPS samples instead
        // of stepping at the GPS rate (10 Hz). Pulled state, not pushed —
        // the estimator's pose at any tick reflects the latest GPS snapshot
        // dead-reckoned forward to that tick's timestamp.
        if (_positionEstimator is not null)
        {
            _renderPullTimer = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33), // ~30 Hz
            };
            _renderPullTimer.Tick += OnRenderPullTick;
            _renderPullTimer.Start();
        }
        _udpService.ModuleConnectionChanged += OnModuleConnectionChanged;
        _ntripService.ConnectionStatusChanged += OnNtripConnectionChanged;
        _ntripService.RtcmDataReceived += OnRtcmDataReceived;
        _fieldService.ActiveFieldChanged += OnActiveFieldChanged;
        FieldFullyLoaded += OnFieldFullyLoaded;
        _simulatorService.GpsDataUpdated += OnSimulatorGpsDataUpdated;
        _boundaryRecordingService.PointAdded += OnBoundaryPointAdded;
        _boundaryRecordingService.StateChanged += OnBoundaryStateChanged;
        _moduleCommunicationService.AutoSteerToggleRequested += OnAutoSteerToggleRequested;
        _moduleCommunicationService.SectionMasterToggleRequested += OnSectionMasterToggleRequested;
        _sectionControlService.SectionStateChanged += OnSectionStateChanged;
        _coverageMapService.BoundsExpanded += OnCoverageBoundsExpanded;

        // Sync drift compensation to AutoSteerService when edited via TextBox
        State.Field.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(State.Field.DriftEasting) or nameof(State.Field.DriftNorthing))
            {
                _autoSteerService.SetDriftCompensation(State.Field.DriftEasting, State.Field.DriftNorthing);
            }
        };

        // Wire YouTurn state -> IsUTurnDistanceVisible computed property
        // so the right-panel distance widget shows during approach, not just mid-turn.
        WireYouTurnDistanceVisibility();

        // Subscribe to ConfigurationStore changes to update NumSections
        _numSections = Models.Configuration.ConfigurationStore.Instance.NumSections;
        Models.Configuration.ConfigurationStore.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Models.Configuration.ConfigurationStore.NumSections))
            {
                NumSections = Models.Configuration.ConfigurationStore.Instance.NumSections;
            }
            else if (e.PropertyName == nameof(Models.Configuration.ConfigurationStore.IsMetric))
            {
                // Refresh all unit-dependent displays
                OnPropertyChanged(nameof(WorkedAreaDisplay));
                OnPropertyChanged(nameof(BoundaryAreaDisplay));
                OnPropertyChanged(nameof(WorkRateDisplay));
                OnPropertyChanged(nameof(SimulatorSpeedDisplay));
            }
        };

        // Note: FPS subscription is set up in platform code (MainWindow.axaml.cs / MainView.axaml.cs)
        // since ViewModels cannot reference Views directly

        // Note: NOT subscribing to DisplaySettings events - using direct property access instead
        // to avoid threading issues

        // Note: Simulator coordinates are restored in RestoreSettings() from saved app settings
        // Default values only used if no settings exist (first run)

        _simulatorTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30Hz — pipeline back-pressure skips if processing is slow
        };
        _simulatorTimer.Tick += OnSimulatorTick;

        // Initialize commands (split into partial class files for organization)
        InitializeNavigationCommands();
        InitializeSimulatorCommands();
        InitializeConfigurationCommands();
        InitializeFieldCommands();
        InitializeBoundaryCommands();
        InitializeTrackCommands();
        InitializeTrackManagementCommands();
        InitializeRecordedPathCommands();
        InitializeNtripCommands();
        InitializeWizardCommands();
        InitializeSettingsCommands();
        InitializeHotkeyCommands();
        InitializeChartCommands();

        // Load display settings first, then restore our app settings on top
        // This ensures AppSettings takes precedence over DisplaySettings
        // IMPORTANT: Run synchronously to ensure settings are loaded before any save can occur
        _displaySettings.LoadSettings();
        RestoreSettings();

        // Apply theme variant based on saved day/night mode
        ApplyThemeVariant(IsDayMode);

        // Initialize clock and auto day/night timer
        InitializeClock();
        InitializeAutoDayNight();

        // Start UDP communication (fire-and-forget but explicit)
        _ = InitializeAsync();

        // Diagnostic auto-resume field — lets the FPS test harness run field-open
        // scenarios across force-stop restarts without manual taps.
        if (DiagFlags.AutoResumeField)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (ResumeFieldCommand?.CanExecute(null) == true)
                {
                    _logger.LogInformation("[DiagFlags] auto_resume_field: invoking ResumeFieldCommand");
                    ResumeFieldCommand.Execute(null);
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Host control loop tick handler (#313). Runs at 100 Hz on the loop's
    /// own thread. Reads the latest GPS-anchored pose from the estimator,
    /// updates tool position + section state machine, then sends PGN 254 +
    /// PGN 239 so the firmware autosteer task — which also runs at 100 Hz
    /// — sees fresh data every cycle.
    /// </summary>
    private void OnControlLoopTicked(long timestampTicks)
    {
        // Only run the section/tool pipeline once a GPS sample exists; before
        // then the estimator returns default(InterpolatedPose) which would
        // pin tool position at (0,0). Autosteer PGN sends are still useful
        // before the first GPS sample (firmware needs to see SOMETHING to
        // know we're alive), so they don't gate.
        if (_positionEstimator?.GetLatestSnapshot() is not null)
        {
            var p = _positionEstimator.GetPose(timestampTicks);
            _toolPositionService.Update(
                new Vec3(p.Position.Easting, p.Position.Northing, p.Heading),
                p.Heading);
            _sectionControlService.Update(
                _toolPositionService.ToolPosition,
                _toolPositionService.ToolHeading,
                p.Heading,
                p.SpeedMps);
        }
        _autoSteerService.SendPgnsForControlTick();
    }

    /// <summary>
    /// 30 Hz UI-thread pull (#313 commit 6) of the dead-reckoned vehicle
    /// pose so the map chevron interpolates smoothly between GPS samples
    /// instead of stepping at the GPS rate. The estimator returns a pose
    /// dead-reckoned to "now" from the latest GPS snapshot using yaw rate
    /// and velocity, so position advances ~7 cm per 30 Hz frame at 25 km/h
    /// instead of a 28 cm jump every 100 ms.
    ///
    /// Tool/hitch are computed entirely from the current dead-reckoned
    /// vehicle pose (for hitch) and the Torriem-stable tool heading from
    /// the snapshot (for tool). Earlier attempts that translated the
    /// snapshot tool by a dead-reckoned hitch delta still mixed two
    /// estimator state bases when a GPS sample arrived between the last
    /// control-loop tick and the render tick — the snapshot hitch used
    /// the pre-arrival prediction while the new hitch used the post-
    /// arrival prediction, leaking a small snap into the implement at
    /// each sample. Computing both hitch and tool from the same single
    /// estimator pose eliminates that.
    /// </summary>
    private void OnRenderPullTick(object? sender, EventArgs e)
    {
        if (_positionEstimator?.GetLatestSnapshot() is null)
            return;
        var p = _positionEstimator.GetPose(Clock.Current.GetTimestamp());

        var tool = ConfigStore.Tool;
        double hitchDistance = Math.Abs(tool.HitchLength);
        if (tool.IsToolRearFixed || tool.IsToolTrailing || tool.IsToolTBT)
            hitchDistance = -hitchDistance;

        double hitchE = p.Position.Easting + Math.Sin(p.Heading) * hitchDistance;
        double hitchN = p.Position.Northing + Math.Cos(p.Heading) * hitchDistance;

        double toolE, toolN, toolHeading;
        if (tool.IsToolFrontFixed || tool.IsToolRearFixed)
        {
            // Fixed tool follows the vehicle exactly — no Torriem state.
            toolHeading = p.Heading;
            toolE = hitchE;
            toolN = hitchN;
        }
        else
        {
            // Trailing / TBT — use the Torriem-tracked heading from the
            // snapshot (stable across ticks) and project the tool back from
            // the freshly-computed hitch along that heading.
            toolHeading = _toolPositionService.ToolHeading;
            double pivotOffset = tool.TrailingHitchLength - tool.TrailingToolToPivotLength;
            toolE = hitchE - Math.Sin(toolHeading) * pivotOffset;
            toolN = hitchN - Math.Cos(toolHeading) * pivotOffset;
        }

        // Lateral offset perpendicular to tool heading (right is positive,
        // matching ToolPositionService.ApplyLateralOffset).
        if (Math.Abs(tool.Offset) > 0.001)
        {
            double perp = toolHeading + Math.PI / 2.0;
            toolE += Math.Sin(perp) * tool.Offset;
            toolN += Math.Cos(perp) * tool.Offset;
        }

        _mapService.SetAllPositions(
            p.Position.Easting, p.Position.Northing, p.Heading,
            toolE, toolN, toolHeading,
            ConfigStore.ActualToolWidth, hitchE, hitchN,
            _toolPositionService.IsToolPositionReady);
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

        // Restore last camera follow mode (Map / NorthUp / HeadingUp)
        if (settings.CameraMode != CameraMode.Free)
            CameraMode = settings.CameraMode;

        // Restore simulator settings (always restore coords, regardless of enabled state)
        _simulatorService.Initialize(new AgValoniaGPS.Models.Wgs84(
            settings.SimulatorLatitude,
            settings.SimulatorLongitude));
        _simulatorService.StepDistance = settings.SimulatorSpeed;

        // Also set Latitude/Longitude so map dialogs work correctly at startup
        Latitude = settings.SimulatorLatitude;
        Longitude = settings.SimulatorLongitude;

        _logger.LogDebug("Restored simulator: {Lat},{Lon}", settings.SimulatorLatitude, settings.SimulatorLongitude);

        // Restore simulator enabled state and panel visibility.
        // hide_all_panels diagnostic flag suppresses the auto-open so baseline
        // perf measurements aren't contaminated by the sim panel.
        IsSimulatorEnabled = settings.SimulatorEnabled;
        IsSimulatorPanelVisible = settings.SimulatorEnabled && !DiagFlags.HideAllPanels;

        // Initialize tool width from config so implement renders before GPS data flows
        var config = Models.Configuration.ConfigurationStore.Instance;
        double initialToolWidth = 0;
        for (int i = 0; i < config.NumSections && i < 16; i++)
            initialToolWidth += config.Tool.GetSectionWidth(i) / 100.0;
        if (initialToolWidth > 0.1)
            ToolWidth = initialToolWidth;
    }

    private void LoadDefaultVehicleProfile()
    {
        try
        {
            // One-time #346 migration: split any pre-v2 combined profiles
            // into paired v2 vehicle + tool files before the load below
            // tries to find them.
            if (_configurationService.MigrateV1ProfilesIfNeeded())
            {
                _logger.LogInformation("Migrated v1 vehicle profiles to v2 (split vehicle/tool)");
            }

            var profiles = _configurationService.GetAvailableProfiles();
            if (profiles.Count == 0)
            {
                _logger.LogWarning("No vehicle profiles found in Vehicles directory");
                return;
            }

            // Try to load the last used profile first
            var lastUsedVehicle = _settingsService.Settings.LastUsedVehicleProfile;
            var lastUsedTool = _settingsService.Settings.LastUsedToolProfile;
            string vehicleToLoad;

            if (!string.IsNullOrEmpty(lastUsedVehicle) && profiles.Contains(lastUsedVehicle))
            {
                vehicleToLoad = lastUsedVehicle;
                _logger.LogDebug("Loading last used vehicle profile: {ProfileName}", vehicleToLoad);
            }
            else
            {
                // Fall back to first available profile
                vehicleToLoad = profiles[0];
                _logger.LogDebug("Loading first available vehicle profile: {ProfileName}", vehicleToLoad);
            }

            // If no tool was previously paired (fresh install / pre-#346
            // settings file), fall back to a same-named tool — that's the
            // post-migration default and matches what the picker will show
            // until the operator picks a different combo.
            var availableTools = _configurationService.GetAvailableToolProfiles();
            string toolToLoad;
            if (!string.IsNullOrEmpty(lastUsedTool) && availableTools.Contains(lastUsedTool))
                toolToLoad = lastUsedTool;
            else if (availableTools.Contains(vehicleToLoad))
                toolToLoad = vehicleToLoad;
            else if (availableTools.Count > 0)
                toolToLoad = availableTools[0];
            else
                toolToLoad = vehicleToLoad; // best-effort; LoadProfiles tolerates a missing tool file

            // LoadProfiles persists the chosen pair back to AppSettings, so
            // a same-name fallback here will overwrite an empty
            // LastUsedToolProfile with a real name on the next save.
            if (_configurationService.LoadProfiles(vehicleToLoad, toolToLoad))
            {
                var store = _configurationService.Store;
                _logger.LogInformation(
                    "Loaded vehicle profile: {Vehicle} / tool: {Tool}",
                    store.ActiveVehicleProfileName,
                    store.ActiveToolProfileName);
                _logger.LogDebug("  Tool width: {ToolWidth}m (from {NumSections} sections)", store.ActualToolWidth, store.NumSections);
                _logger.LogDebug("  YouTurn radius: {Radius}m", store.Guidance.UTurnRadius);
                _logger.LogDebug("  Wheelbase: {Wheelbase}m", store.Vehicle.Wheelbase);
                _logger.LogDebug("  Sections: {NumSections}", store.NumSections);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle profile");
        }
    }

    // NTRIP methods (ConnectToNtripAsync, DisconnectFromNtripAsync, HandleNtripProfileForFieldAsync) are in MainViewModel.Ntrip.cs

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
                State.Connections.IsGpsConnected = gpsOk;
                State.Connections.AutoSteerIpAddress = _udpService.GetModuleIpAddress(ModuleType.AutoSteer);
                State.Connections.MachineIpAddress = _udpService.GetModuleIpAddress(ModuleType.Machine);
                State.Connections.ImuIpAddress = _udpService.GetModuleIpAddress(ModuleType.IMU);

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

    private bool _showRecordedPaths;
    public bool ShowRecordedPaths
    {
        get => _showRecordedPaths;
        set
        {
            SetProperty(ref _showRecordedPaths, value);
            UpdateRecordedPathsOnMap();
        }
    }

    private bool _isRecordingContour;
    public bool IsRecordingContour
    {
        get => _isRecordingContour;
        set => SetProperty(ref _isRecordingContour, value);
    }

    private bool _isRecordingPath;
    public bool IsRecordingPath
    {
        get => _isRecordingPath;
        set => SetProperty(ref _isRecordingPath, value);
    }

    public ObservableCollection<Track> ContourStrips { get; } = new();
    public ObservableCollection<Track> RecordedPathTracks { get; } = new();

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
        set
        {
            SetProperty(ref _isAutoSteerAvailable, value);
            RaiseUTurnButtonVisibleChanged();
        }
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

    // NTRIP properties are in MainViewModel.Ntrip.cs

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

    public bool IsToolPositionReady => _toolPositionService.IsToolPositionReady;

    // OnAutoSteerStateUpdated is now in MainViewModel.Guidance.cs



    // AutoSteer guidance state and event handlers
    // are now in MainViewModel.Guidance.cs

    // YouTurn methods (ProcessYouTurn, CreateYouTurnPath, CompleteYouTurn, etc.)
    // are now in MainViewModel.YouTurn.cs


    private void OnCoverageBoundsExpanded(object? sender, BoundsExpandedEventArgs e)
    {
        // Reinitialize display bitmap with new expanded bounds
        Dispatcher.UIThread.Post(() =>
        {
            _mapService.InitializeCoverageBitmapWithBounds(e.MinE, e.MaxE, e.MinN, e.MaxN);
            _logger.LogDebug($"[Coverage] Display bitmap reinitialized for expanded bounds: E[{e.MinE:F0},{e.MaxE:F0}] N[{e.MinN:F0},{e.MaxN:F0}]");
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

    private void OnModuleConnectionChanged(object? sender, ModuleConnectionEventArgs e)
    {
        // This event is no longer used - status is polled every 100ms
    }

    // NTRIP event handlers are in MainViewModel.Ntrip.cs

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

    /// <summary>
    /// Workable area in m²: boundary area minus headland area.
    /// </summary>
    private double WorkableAreaSqM
    {
        get
        {
            var boundary = State.Field.CurrentBoundary;
            double totalSqM = (boundary?.AreaHectares ?? 0) * 10000;
            if (totalSqM <= 0) return 0;

            // Subtract headland area if headland exists
            var headland = State.Field.HeadlandLine;
            if (headland != null && headland.Count >= 3)
            {
                double headlandArea = Math.Abs(PolygonArea(headland));
                return headlandArea; // Headland polygon IS the cultivated area
            }
            return totalSqM;
        }
    }

    private static double PolygonArea(System.Collections.Generic.List<Models.Base.Vec3> polygon)
    {
        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            int j = (i + 1) % polygon.Count;
            area += polygon[i].Easting * polygon[j].Northing;
            area -= polygon[j].Easting * polygon[i].Northing;
        }
        return area / 2.0;
    }

    public string BoundaryAreaDisplay
    {
        get
        {
            double areaSqM = WorkableAreaSqM;
            if (areaSqM > 0)
            {
                return FormatArea(areaSqM);
            }
            return ConfigStore.IsMetric ? "0.00 ha" : "0.00 ac";
        }
    }

    public double RemainingPercent
    {
        get
        {
            double workableArea = WorkableAreaSqM;
            if (workableArea > 0)
            {
                double workedArea = _coverageMapService.TotalWorkedArea;
                return ((workableArea - workedArea) * 100 / workableArea);
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
            double speedMetersPerHour = Speed * 3600; // m/s to m/h
            double squareMetersPerHour = speedMetersPerHour * ToolWidth;
            double hectaresPerHour = squareMetersPerHour / 10000.0;

            if (ConfigStore.IsMetric)
                return $"{hectaresPerHour:F1} ha/hr";
            else
                return $"{hectaresPerHour * 2.47105:F1} ac/hr";
        }
    }

    // Helper method to format area with metric/imperial support
    private string FormatArea(double squareMeters)
    {
        double hectares = squareMeters * 0.0001;
        if (ConfigStore.IsMetric)
            return $"{hectares:F2} ha";
        else
            return $"{hectares * 2.47105:F2} ac";
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

    /// <summary>
    /// Called after a field is fully loaded. Centers camera and refreshes dependent panels.
    /// </summary>
    private void OnFieldFullyLoaded(Field? field)
    {
        // Center camera on field
        if (CameraMode == Models.CameraMode.Free)
            CameraMode = _previousCameraMode;

        if (field?.Boundary != null)
            CenterMapOnBoundary(field.Boundary);
        else
            _mapService.PanTo(State.Vehicle.Easting, State.Vehicle.Northing);

        _mapService.SetVehiclePosition(
            State.Vehicle.Easting, State.Vehicle.Northing,
            State.Vehicle.Heading * Math.PI / 180.0);
    }

    private void OnActiveFieldChanged(object? sender, Field? field)
    {
        // This event is now only used for state synchronization, not for save/load
        // Save/load is handled by OpenFieldAsync and CloseFieldAsync
        State.Field.ActiveField = field;
        ActiveField = field;
        OnPropertyChanged(nameof(ActiveFieldName));
        OnPropertyChanged(nameof(ActiveFieldArea));
        OnPropertyChanged(nameof(HasActiveField));
    }

    // Pending intents consumed by the next OpenFieldAsync. Set by the
    // OpenField*Async overloads below; cleared on consumption. See the
    // JobService block inside OpenFieldAsync for why this exists
    // (race with CloseFieldAsync's coverage save).
    private (string FieldName, string WorkType, string Notes, string? TaskName)? _pendingNewJob;
    private (string FieldName, string TaskName)? _pendingResumeJob;
    private bool _pendingFieldOnly;

    /// <summary>
    /// Open a field and create a brand-new job inside it. Coverage from
    /// the previous active job is correctly saved to that previous job's
    /// folder before the new job is created.
    /// </summary>
    public Task OpenFieldStartingNewJobAsync(
        string fieldPath, string fieldName, string workType, string notes, string? taskName = null)
    {
        _pendingNewJob = (fieldName, workType, notes, taskName);
        _pendingResumeJob = null;
        _pendingFieldOnly = false;
        return OpenFieldAsync(fieldPath, fieldName);
    }

    /// <summary>
    /// Open a field and resume an existing job inside it.
    /// </summary>
    public Task OpenFieldResumingJobAsync(string fieldPath, string fieldName, string taskName)
    {
        _pendingResumeJob = (fieldName, taskName);
        _pendingNewJob = null;
        _pendingFieldOnly = false;
        return OpenFieldAsync(fieldPath, fieldName);
    }

    /// <summary>
    /// Open a field's geometry without activating any job (Decision #2).
    /// Coverage is not loaded; section paint is silently dropped at close
    /// because <see cref="IJobService.ActiveJob"/> stays null.
    /// </summary>
    public Task OpenFieldOnlyAsync(string fieldPath, string fieldName)
    {
        _pendingFieldOnly = true;
        _pendingNewJob = null;
        _pendingResumeJob = null;
        return OpenFieldAsync(fieldPath, fieldName);
    }

    /// <summary>
    /// Opens a field from the specified path. This is the single entry point for all field opening.
    /// Handles: closing previous field, loading boundary, background, coverage, tracks, headland.
    /// </summary>
    public async Task OpenFieldAsync(string fieldPath, string fieldName)
    {
        _logger.LogDebug($"[Field] OpenFieldAsync: {fieldName} at {fieldPath}");

        // Close current field first (saves coverage, clears state)
        await CloseFieldAsync();

        // Wrap any pre-#349 coverage into a synthetic imported job. Idempotent;
        // a no-op if the field already has a jobs/ folder.
        try
        {
            if (LegacyFieldMigrationService.MigrateIfNeeded(fieldPath))
                _logger.LogDebug("[Job] Migrated legacy coverage for '{FieldName}'", fieldName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Job] Legacy migration failed for '{FieldName}'", fieldName);
        }

        // Show busy overlay for loading
        State.UI.BusyMessage = "Loading field...";
        State.UI.IsBusy = true;
        _logger.LogDebug("[Busy] OpenFieldAsync: Loading field '{FieldName}'", fieldName);

        try
        {
            // Force UI to render busy overlay
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(50);

            // Update field state
            CurrentFieldName = fieldName;
            IsFieldOpen = true;
            FieldsRootDirectory = Path.GetDirectoryName(fieldPath) ?? string.Empty;
            _gpsPipelineService.SetHasActiveField(true);

            // Load field origin from Field.txt
            try
            {
                var fieldInfo = _fieldPlaneFileService.LoadField(fieldPath);

                // Recovery: if Field.txt has no origin or a zero origin, fall
                // back to field.origin — a separate file written at field-
                // create time and never touched by close-save. Fields
                // corrupted by the pre-#270 save-with-zero bug can be healed
                // this way; next close writes the real origin back to both
                // Field.txt and field.geojson.
                if (fieldInfo.Origin == null
                    || (fieldInfo.Origin.Latitude == 0 && fieldInfo.Origin.Longitude == 0))
                {
                    var originBackupPath = Path.Combine(fieldPath, "field.origin");
                    if (File.Exists(originBackupPath))
                    {
                        var originLine = File.ReadAllText(originBackupPath).Trim();
                        var parts = originLine.Split(',');
                        if (parts.Length == 2
                            && double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var backupLat)
                            && double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var backupLon)
                            && (backupLat != 0 || backupLon != 0)
                            && backupLat >= -90 && backupLat <= 90
                            && backupLon >= -180 && backupLon <= 180)
                        {
                            fieldInfo.Origin = new Position
                            {
                                Latitude = backupLat,
                                Longitude = backupLon,
                            };
                            _logger.LogDebug($"[Field] Recovered origin from field.origin backup: {backupLat}, {backupLon}");
                        }
                    }
                }

                if (fieldInfo.Origin != null)
                {
                    SetFieldOrigin(fieldInfo.Origin.Latitude, fieldInfo.Origin.Longitude);
                    _logger.LogDebug($"[Field] Set origin: {_fieldOriginLatitude}, {_fieldOriginLongitude}");
                    // Only reposition the simulator if the field has a real (non-zero)
                    // georeference. Fields that were never georeferenced persist an
                    // origin of (0, 0), which otherwise clobbers the user's
                    // simulator coords (saved to appsettings on window close).
                    if (_fieldOriginLatitude != 0 || _fieldOriginLongitude != 0)
                        SetSimulatorCoordinates(_fieldOriginLatitude, _fieldOriginLongitude);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[Field] Could not load Field.txt origin: {ex.Message}");
            }

            // Load boundary
            var boundary = _boundaryFileService.LoadBoundary(fieldPath);
            if (boundary != null)
            {
                SetCurrentBoundary(boundary);
                CenterMapOnBoundary(boundary);

                var boundaryAreas = new List<double> { boundary.AreaHectares * 10000 };
                _fieldStatistics.UpdateBoundaryAreas(boundaryAreas);
                OnPropertyChanged(nameof(BoundaryAreaDisplay));
            }

            // Load background image
            LoadBackgroundImage(fieldPath, boundary);

            // Create field object and set as active. Origin must be copied from
            // the loaded Field.txt — otherwise Field.Origin defaults to (0, 0)
            // and CloseFieldAsync silently overwrites the on-disk Field.txt with
            // a zero origin, corrupting the field for every future session.
            var field = new Field
            {
                Name = fieldName,
                DirectoryPath = fieldPath,
                Boundary = boundary,
                Origin = new Position
                {
                    Latitude = _fieldOriginLatitude,
                    Longitude = _fieldOriginLongitude,
                }
            };

            // Update field service (triggers OnActiveFieldChanged for state sync only)
            _fieldService.SetActiveField(field);

            // Load headland
            LoadHeadlandFromField(field);

            // Load tracks
            LoadTracksFromField(field);

            // Load recorded path from RecPath.txt
            LoadRecPathFromField(fieldPath);

            // Establish (or resume) the active job before any coverage paint
            // is allowed. Coverage now lives under <field>/jobs/<task>/.
            //
            // The pending-intent fields below are set by the OpenField*Async
            // overloads so the dialog can express "open field A and start
            // job J2" without mutating JobService.ActiveJob before
            // CloseFieldAsync runs — doing so used to misroute the previous
            // job's in-memory coverage into the new job's folder.
            Job? activeJob = null;
            var newIntent = _pendingNewJob;
            var resumeIntent = _pendingResumeJob;
            var fieldOnly = _pendingFieldOnly;
            _pendingNewJob = null;
            _pendingResumeJob = null;
            _pendingFieldOnly = false;

            if (fieldOnly)
            {
                _logger.LogDebug("[Job] Field-only open; no job will be activated");
            }
            else if (newIntent != null)
            {
                activeJob = _jobService.CreateJob(
                    newIntent.Value.FieldName,
                    newIntent.Value.WorkType,
                    newIntent.Value.Notes,
                    newIntent.Value.TaskName);
            }
            else if (resumeIntent != null)
            {
                _jobService.ResumeJob(resumeIntent.Value.FieldName, resumeIntent.Value.TaskName);
                activeJob = _jobService.ActiveJob!;
            }
            else
            {
                activeJob = _jobService.GetOrCreateDefaultJob(fieldName);
            }
            if (activeJob != null)
            {
                _logger.LogDebug("[Job] Active job: {TaskName} (status={Status})",
                    activeJob.TaskName, activeJob.Status);
            }

            // Load coverage (shows busy overlay — pixel buffer callback needs UI thread for bitmap access)
            State.UI.BusyMessage = "Loading coverage...";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            if (activeJob != null)
            {
                _coverageMapService.LoadFromFile(fieldPath, activeJob.TaskName);
                _logger.LogDebug($"[Coverage] Loaded coverage from {fieldPath} job={activeJob.TaskName}");
            }
            // Field-only opens skip coverage load — the bitmap was already
            // cleared by ClearFieldState during CloseFieldAsync, so there's
            // nothing to draw until the operator starts a job.
            RefreshCoverageStatistics();

            // Start periodic coverage autosave. The timer no-ops on each
            // tick when ActiveJob is null (field-only open), so it's safe
            // to start unconditionally here.
            StartCoverageAutosave();

            // Load tram lines
            try
            {
                _tramLineService.LoadFromFile(fieldPath);
                if (_tramLineService.HasTramLines)
                {
                    _mapService.SetTramLines(
                        _tramLineService.OuterBoundaryTrack,
                        _tramLineService.InnerBoundaryTrack,
                        _tramLineService.ParallelTramLines);
                    _logger.LogDebug($"[Tram] Loaded tram lines from {fieldPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load tram lines");
            }

            // Load tram systems
            try
            {
                var systems = Services.Tram.TramSystemFileService.Load(fieldPath);
                ConfigStore.Tram.Systems.Clear();
                foreach (var sys in systems)
                    ConfigStore.Tram.Systems.Add(sys);
                _hasTramSystemsEverUsed = systems.Count > 0;
                if (systems.Count > 0)
                    _logger.LogDebug($"[Tram] Loaded {systems.Count} tram systems");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load tram systems");
            }

            // Handle NTRIP profile
            _ = HandleNtripProfileForFieldAsync(fieldName);

            // Sync elevation log enabled state from config
            _elevationLogService.IsEnabled = Models.Configuration.ConfigurationStore.Instance.Display.ElevationLogEnabled;

            // Save as last opened field
            _settingsService.Settings.LastOpenedField = fieldName;
            _settingsService.Save();

            // Force simulator ticks so vehicle position updates to field origin
            if (IsSimulatorEnabled)
            {
                _simulatorService.Tick(0);
                _simulatorService.Tick(0);
            }

            // Let GPS events propagate through the UI
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(100);

            // Notify subscribers that the field is fully loaded
            FieldFullyLoaded?.Invoke(field);

            StatusMessage = $"Opened field: {fieldName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Field] Error opening field: {fieldName}");
            StatusMessage = $"Failed to open field: {ex.Message}";
        }
        finally
        {
            State.UI.IsBusy = false;
            _logger.LogDebug("[Busy] OpenFieldAsync: Complete");
        }
    }

    /// <summary>
    /// Closes the current field. This is the single entry point for all field closing.
    /// Handles: saving coverage, saving tracks, clearing all field state.
    /// </summary>
    public async Task CloseFieldAsync()
    {
        // Stop the autosave timer first so a tick can't fire mid-close
        // and race the explicit close-save below. ClearFieldState also
        // stops it, but stopping here covers both branches without
        // depending on call order.
        StopCoverageAutosave();

        if (ActiveField == null || string.IsNullOrEmpty(ActiveField.DirectoryPath))
        {
            // No field to close, just clear state
            ClearFieldState();
            return;
        }

        _logger.LogDebug($"[Field] CloseFieldAsync: {ActiveField.Name}");

        // Show busy overlay for saving
        State.UI.BusyMessage = "Saving field...";
        State.UI.IsBusy = true;
        _logger.LogDebug("[Busy] CloseFieldAsync: Saving field '{FieldName}'", ActiveField.Name);

        try
        {
            // Force UI to render busy overlay
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(50);

            // Save coverage on background thread (RLE compression can take seconds).
            // Coverage is keyed by the active job's task name; if no job is
            // active (field-only open) the save is skipped.
            var savePath = ActiveField.DirectoryPath;
            var activeJob = _jobService.ActiveJob;
            if (activeJob != null)
            {
                var taskName = activeJob.TaskName;
                await Task.Run(() => _coverageMapService.SaveToFile(savePath, taskName));
                _logger.LogDebug($"[Coverage] Saved coverage to {savePath} job={taskName}");
            }
            else
            {
                _logger.LogDebug("[Coverage] No active job; skipping coverage save (field-only open)");
            }

            // Save tram lines
            if (_tramLineService.HasTramLines)
            {
                _tramLineService.SaveToFile(ActiveField.DirectoryPath);
                _logger.LogDebug($"[Tram] Saved tram lines to {ActiveField.DirectoryPath}");
            }

            // Save tram systems
            if (ConfigStore.Tram.Systems.Count > 0)
            {
                Services.Tram.TramSystemFileService.Save(ActiveField.DirectoryPath, ConfigStore.Tram.Systems);
                _logger.LogDebug($"[Tram] Saved {ConfigStore.Tram.Systems.Count} tram systems");
            }

            // Flush elevation log
            _elevationLogService.Flush(ActiveField.DirectoryPath);
            _elevationLogService.Clear();

            // Save tracks
            SaveTracksToFile();

            // Save field (writes geojson + legacy formats)
            _fieldService.SaveField(ActiveField);

            // Suspend rather than close. M2 has no explicit "Close Job"
            // operator action — closing a field should leave the job
            // in-progress so the next open of the same field resumes it.
            // The dialog UI in M3+ will surface explicit close/done.
            _jobService.SuspendCurrentJob();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[Field] Error saving field: {ActiveField.Name}");
        }
        finally
        {
            State.UI.IsBusy = false;
            _logger.LogDebug("[Busy] CloseFieldAsync: Complete");
        }

        // Clear all field state
        ClearFieldState();
    }

    /// <summary>
    /// Clears all field-related state without saving.
    /// </summary>
    private void ClearFieldState()
    {
        // Stop autosave (idempotent if already stopped). Belt-and-braces
        // for callers that bypass CloseFieldAsync.
        StopCoverageAutosave();

        // Disengage autosteer first so the cycle stops emitting steer commands
        // before the track / boundary / U-turn state vanishes out from under it.
        // Without this the tractor keeps executing the last-sent steer angle
        // (visible as "keeps circling" when closing mid-U-turn).
        if (IsAutoSteerEngaged)
        {
            IsAutoSteerEngaged = false;
            _autoSteerService.Disengage();
            SyncGuidanceStateToPipeline();
        }

        CurrentFieldName = string.Empty;
        IsFieldOpen = false;
        _gpsPipelineService.SetHasActiveField(false);

        // Clear boundary
        SetCurrentBoundary(null);

        // Clear headland
        LoadHeadlandFromField(null);

        // Clear background
        _mapService.ClearBackground();

        // Clear tracks
        State.Field.Tracks.Clear();
        SavedTracks.Clear();
        SelectedTrack = null;

        // Clear U-turn state
        ClearYouTurnState();

        // Clear coverage
        _coverageMapService.ClearAll();

        // Update field service
        _fieldService.SetActiveField(null);
    }

    /// <summary>
    /// Load headland line from field directory
    /// </summary>
    private void LoadHeadlandFromField(Field? field)
    {
        if (field == null || string.IsNullOrEmpty(field.DirectoryPath))
        {
            // Clear headland if no field - update centralized state.
            // All in-memory headland state must be wiped here, otherwise it
            // leaks across field-switch boundaries (e.g. close field A,
            // create field B → field A's headland reappears on B because
            // segments / preview were never cleared).
            State.Field.HeadlandLine = null;
            State.Field.HeadlandDistance = 0;

            _currentHeadlandLine = null;
            _mapService.SetHeadlandLine(null);
            _mapService.SetHeadlandVisible(false);
            HeadlandSegments.Clear();
            HeadlandPreviewLine = null; // setter pushes null to map service

            HasHeadland = false;
            IsHeadlandOn = false;
            OnPropertyChanged(nameof(CurrentHeadlandLine));
            OnPropertyChanged(nameof(CurrentHeadlandLineForPreview));
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

        // Load headland segments
        try
        {
            var segments = Services.Headland.HeadlandSegmentFileService.Load(field.DirectoryPath);
            HeadlandSegments.Clear();
            foreach (var seg in segments)
            {
                // Recompute offsets with current algorithm (may differ from saved)
                ComputeSegmentOffset(seg);
                HeadlandSegments.Add(seg);
            }
            if (HeadlandSegments.Count > 0)
                BuildHeadlandFromSegments();
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug($"[Headland] Failed to load headland segments: {ex.Message}");
        }
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

    // Curve recording state (drive mode)
    private List<Vec3> _recordedCurvePoints = new();
    private Vec3? _lastCurvePoint;
    private const double CurveMinPointSpacing = 2.0; // Minimum 2m spacing between curve points

    /// <summary>
    /// Whether curve recording is currently active
    /// </summary>
    public bool IsRecordingCurve => CurrentABCreationMode == ABCreationMode.Curve;

    /// <summary>
    /// Number of points recorded in current curve
    /// </summary>
    public int RecordedCurvePointCount => _recordedCurvePoints.Count;

    // Curve drawing state (tap mode)
    private List<Vec3> _drawnCurvePoints = new();

    /// <summary>
    /// Whether curve drawing is currently active
    /// </summary>
    public bool IsDrawingCurve => CurrentABCreationMode == ABCreationMode.DrawCurve;

    /// <summary>
    /// Number of points drawn in current curve
    /// </summary>
    public int DrawnCurvePointCount => _drawnCurvePoints.Count;

    // Computed properties for UI binding
    public bool IsCreatingABLine => CurrentABCreationMode != ABCreationMode.None;

    public bool EnableABClickSelection => CurrentABCreationMode == ABCreationMode.DrawAB ||
                                          CurrentABCreationMode == ABCreationMode.DriveAB ||
                                          CurrentABCreationMode == ABCreationMode.Curve ||
                                          CurrentABCreationMode == ABCreationMode.DrawCurve ||
                                          _isPlaceFlagOnClickMode;

    private bool _isPlaceFlagOnClickMode;
    public bool IsPlaceFlagOnClickMode
    {
        get => _isPlaceFlagOnClickMode;
        set
        {
            SetProperty(ref _isPlaceFlagOnClickMode, value);
            OnPropertyChanged(nameof(EnableABClickSelection));
        }
    }

    /// <summary>
    /// Place a flag at the given world coordinates (from map click).
    /// </summary>
    public void PlaceFlagAtWorldPosition(double easting, double northing, FlagColor? color = null)
    {
        var actualColor = color ?? NextAutoColor();
        var id = _nextFlagId++;
        var flag = new Flag(easting, northing, actualColor, id, $"Flag {id}");
        Flags.Add(flag);
        UpdateFlagsOnMap();
        StatusMessage = $"{actualColor} flag '{flag.Name}' at E:{easting:F1} N:{northing:F1}";

        // Exit flag click mode after placing
        IsPlaceFlagOnClickMode = false;
    }

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
                (ABCreationMode.Curve, _) => $"RECORDING: Drive along curve ({RecordedCurvePointCount} pts) - Tap screen when done",
                (ABCreationMode.DrawCurve, _) => $"DRAWING: Tap to add points ({DrawnCurvePointCount} pts) - Tap Finish when done",
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
            SetProperty(ref _selectedTrack, value);
            if (!ReferenceEquals(oldValue, value))
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

                    // Generate tram lines from the selected track
                    UpdateTramLines(value);

                    // Phase D D6: compute the initial pass number + nudge offset
                    // locally from the track's persisted NudgeDistance (inverse of
                    // NudgeDistance = widthMinusOverlap * pathsAway + nudgeOffset).
                    // The cycle is the sole writer of State.Guidance post-D3, so
                    // we don't touch it here — the values flow into _guidanceWorking
                    // via SyncGuidanceStateToPipeline's SetActiveTrack below, and
                    // the next cycle's snapshot mirrors them back to State.Guidance.
                    double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
                    if (widthMinusOverlap > 0.1)
                    {
                        _pendingInitialPathsAway = (int)Math.Round(value.NudgeDistance / widthMinusOverlap);
                        _pendingInitialNudgeOffset = value.NudgeDistance - (_pendingInitialPathsAway.Value * widthMinusOverlap);
                        _logger.LogDebug($"[NUDGE] SelectedTrack setter: '{value.Name}' NudgeDistance={value.NudgeDistance:F2}m -> pathsAway={_pendingInitialPathsAway}, nudgeOffset={_pendingInitialNudgeOffset:F3}m");
                    }
                    else
                    {
                        _pendingInitialPathsAway = 0;
                        _pendingInitialNudgeOffset = 0;
                        _logger.LogDebug("[NUDGE] SelectedTrack setter: '{TrackName}' widthMinusOverlap too small, pathsAway=0", value.Name);
                    }

                    // Check if track runs along boundary (skip disengage on first pass)
                    _isSelectedTrackOnBoundary = IsTrackOnBoundary(value);
                    if (_isSelectedTrackOnBoundary)
                    {
                        _logger.LogDebug($"[SelectedTrack] Track '{value.Name}' is ON boundary - will skip boundary check on pass 0");
                    }

                    // For curved tracks, calculate and display max inward passes
                    if (value.Points.Count > 2)
                    {
                        // widthMinusOverlap already calculated above
                        double minRadius = CurveProcessing.CalculateMinRadiusOfCurvature(value.Points);
                        int maxPasses = CurveProcessing.CalculateMaxInwardPasses(value.Points, widthMinusOverlap);

                        if (maxPasses < 50) // Only show warning for reasonably tight curves
                        {
                            StatusMessage = $"Curve selected: min radius {minRadius:F1}m, max ~{maxPasses} inward passes";
                            _logger.LogInformation("Curve '{Name}' selected: min radius {Radius:F1}m, max inward passes ~{Max}",
                                value.Name, minRadius, maxPasses);
                        }
                    }
                }
                else
                {
                    State.Field.ActiveTrack = null;
                    // Clear the track and guidance from the map when deactivated
                    _mapService.SetActiveTrack(null);
                    _mapService.SetBaseTrack(null);
                    _mapService.SetGuidancePoints(0, 0, false);
                    _isSelectedTrackOnBoundary = false;
                    // Clear any U-turn state associated with the deactivated track
                    ClearYouTurnState();
                }

                // Update guidance availability
                HasActiveTrack = value != null;
                IsAutoSteerAvailable = value != null;

                // Sync to pipeline so guidance computes on background thread
                SyncGuidanceStateToPipeline();

                _logger.LogDebug($"[SelectedTrack] Changed to: {value?.Name ?? "None"}");
            }
        }
    }

    /// <summary>
    /// Generate tram lines from a track and update the map.
    /// </summary>
    private void UpdateTramLines(Track? track)
    {
        var config = ConfigurationStore.Instance.Tram;

        // Set boundary fence for clipping tram lines
        if (_currentBoundary?.OuterBoundary?.Points != null && _currentBoundary.OuterBoundary.Points.Count >= 3)
        {
            var fencePts = _currentBoundary.OuterBoundary.Points
                .Select(p => new Models.Base.Vec3(p.Easting, p.Northing, p.Heading)).ToList();
            _tramLineService.SetBoundaryFence(fencePts);
        }

        double fieldWidth = 500;
        if (_currentBoundary?.OuterBoundary?.Points != null && _currentBoundary.OuterBoundary.Points.Count > 0)
        {
            var pts = _currentBoundary.OuterBoundary.Points;
            double maxE = pts.Max(p => p.Easting), minE = pts.Min(p => p.Easting);
            double maxN = pts.Max(p => p.Northing), minN = pts.Min(p => p.Northing);
            fieldWidth = Math.Max(maxE - minE, maxN - minN) * 1.2;
        }

        _tramLineService.Clear();

        // Track if systems have ever been used (disables legacy fallback)
        if (config.Systems.Count > 0)
            _hasTramSystemsEverUsed = true;

        // If TramSystems exist, generate per-system; otherwise use legacy single-track mode
        _tramSystemLineRanges.Clear();
        if (config.Systems.Count > 0)
        {
            bool hasBoundarySystem = false;
            foreach (var sys in config.Systems)
            {
                if (!sys.IsEnabled) continue;

                // Boundary reference system: generate boundary tram tracks from field boundary
                if (sys.ReferenceBoundaryIndex >= 0)
                {
                    hasBoundarySystem = true;
                    int passes = sys.PassCount > 0 ? sys.PassCount : 1;
                    int bndStartIdx = _tramLineService.ParallelTramLines.Count;
                    if (_currentBoundary?.OuterBoundary?.Points != null &&
                        _currentBoundary.OuterBoundary.Points.Count >= 3)
                    {
                        var bndPts = _currentBoundary.OuterBoundary.Points
                            .Select(p => new Models.Base.Vec3(p.Easting, p.Northing, p.Heading)).ToList();
                        _tramLineService.GenerateBoundaryTramTracks(bndPts, passes, sys.Mode, sys.TramWidth);
                    }
                    int bndLineCount = _tramLineService.ParallelTramLines.Count - bndStartIdx;
                    _tramSystemLineRanges[sys.Name] = (bndStartIdx, bndLineCount, true);
                    continue;
                }

                // Track reference system: resolve by name only, skip if missing
                if (string.IsNullOrEmpty(sys.ReferenceTrackName)) continue;
                var refTrack = SavedTracks.FirstOrDefault(t => t.Name == sys.ReferenceTrackName);
                if (refTrack == null || refTrack.Points.Count < 2) continue;

                int startIdx = _tramLineService.ParallelTramLines.Count;
                var lines = _tramLineService.GenerateForSystem(sys, refTrack, fieldWidth);
                foreach (var line in lines)
                    _tramLineService.AddTramLine(line);
                _tramSystemLineRanges[sys.Name] = (startIdx, lines.Count, false);
            }
        }
        else if (!_hasTramSystemsEverUsed && track != null && track.Points.Count >= 2)
        {
            // Legacy: single track mode (only if systems have never been used in this field)
            _tramLineService.GenerateParallelTramLines(track, fieldWidth);

            // Legacy: also generate boundary tram tracks from headland
            if (_currentHeadlandLine != null && _currentHeadlandLine.Count >= 3)
                _tramLineService.GenerateBoundaryTramTracks(_currentHeadlandLine);
        }

        // Snapshot collections for thread-safe rendering
        var outerSnap = _tramLineService.OuterBoundaryTrack.ToList();
        var innerSnap = _tramLineService.InnerBoundaryTrack.ToList();
        var parallelSnap = _tramLineService.ParallelTramLines
            .Select(l => (IReadOnlyList<Models.Base.Vec2>)l.ToList()).ToList();
        var bndExtraSnap = _tramLineService.BoundaryExtraLines
            .Select(l => (IReadOnlyList<Models.Base.Vec2>)l.ToList()).ToList();

        _mapService.SetTramLines(outerSnap, innerSnap, parallelSnap, bndExtraSnap);

        OnPropertyChanged(nameof(TramLineCountDisplay));
    }

    // Flag markers
    private int _nextFlagId = 1;
    public ObservableCollection<Flag> Flags { get; } = new();

    private FlagColor NextAutoColor()
    {
        var allColors = Enum.GetValues<FlagColor>();
        var usedColors = Flags.Select(f => f.FlagColor).ToHashSet();
        foreach (var c in allColors)
            if (!usedColors.Contains(c)) return c;
        return allColors[Flags.Count % allColors.Length];
    }

    private void PlaceFlag(FlagColor color)
    {
        if (Easting == 0 && Northing == 0)
        {
            StatusMessage = "No GPS position - cannot place flag";
            return;
        }

        var id = _nextFlagId++;
        var flag = new Flag(Easting, Northing, color, id, $"Flag {id}");
        Flags.Add(flag);
        UpdateFlagsOnMap();
        StatusMessage = $"{color} flag '{flag.Name}' at E:{Easting:F1} N:{Northing:F1}";
    }

    public void UpdateFlagsOnMap()
    {
        var flags = Flags.Select(f =>
            (f.Easting, f.Northing, f.FlagColor.ToString(), f.Name)).ToList();
        _mapService.SetFlags(flags);
    }

    // Track management commands
    public ICommand? DeleteSelectedTrackCommand { get; private set; }
    public ICommand? DeleteAllTracksCommand { get; private set; }
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

    // Settings Commands
    public ICommand? ShowAppDirectoriesDialogCommand { get; private set; }
    public ICommand? CloseAppDirectoriesDialogCommand { get; private set; }
    public ICommand? ShowAboutDialogCommand { get; private set; }
    public ICommand? CloseAboutDialogCommand { get; private set; }
    public ICommand? ResetAllSettingsCommand { get; private set; }

    private ObservableCollection<AppDirectoryInfo> _appDirectories = new();
    public ObservableCollection<AppDirectoryInfo> AppDirectories
    {
        get => _appDirectories;
        set => SetProperty(ref _appDirectories, value);
    }

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
    private List<List<(double Latitude, double Longitude)>> _kmlParsedPolygons = new();

    /// <summary>
    /// When true, KML import adds boundaries to the current open field.
    /// When false (default), KML import creates a new field.
    /// </summary>
    private bool _kmlImportToExistingField;

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

    // Existing boundary polygons for reference display in the map dialog (WGS84 coordinates)
    public List<List<(double Latitude, double Longitude)>> BoundaryMapExistingPolygons { get; } = new();

    // Result properties for boundary map dialog
    public List<(double Latitude, double Longitude)> BoundaryMapResultPoints { get; } = new();
    public string? BoundaryMapResultBackgroundPath { get; set; }
    public double BoundaryMapResultNwLat { get; set; }
    public double BoundaryMapResultNwLon { get; set; }
    public double BoundaryMapResultSeLat { get; set; }
    public double BoundaryMapResultSeLon { get; set; }
    // Web Mercator bounds for proper satellite tile sampling
    public double BoundaryMapResultMercMinX { get; set; }
    public double BoundaryMapResultMercMaxX { get; set; }
    public double BoundaryMapResultMercMinY { get; set; }
    public double BoundaryMapResultMercMaxY { get; set; }

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

    // Confirmation Dialog properties (visibility managed by State.UI)
    private string _confirmationDialogTitle = string.Empty;
    public string ConfirmationDialogTitle
    {
        get => _confirmationDialogTitle;
        set => SetProperty(ref _confirmationDialogTitle, value);
    }

    private string _confirmationDialogMessage = string.Empty;
    public string ConfirmationDialogMessage
    {
        get => _confirmationDialogMessage;
        set => SetProperty(ref _confirmationDialogMessage, value);
    }

    // Callback to run when confirmation dialog is confirmed
    private Action? _confirmationDialogCallback;
    private Models.State.DialogType _previousDialogBeforeConfirmation;

    public ICommand? CancelConfirmationDialogCommand { get; private set; }
    public ICommand? ConfirmConfirmationDialogCommand { get; private set; }

    /// <summary>
    /// Shows a confirmation dialog with the specified title and message.
    /// When the user confirms, the callback is executed.
    /// Restores the previous dialog on cancel.
    /// </summary>
    public void ShowConfirmationDialog(string title, string message, Action onConfirm)
    {
        ConfirmationDialogTitle = title;
        ConfirmationDialogMessage = message;
        _confirmationDialogCallback = onConfirm;
        _previousDialogBeforeConfirmation = State.UI.ActiveDialog;
        State.UI.ShowDialog(Models.State.DialogType.Confirmation);
    }

    // Error Dialog properties (visibility managed by State.UI)
    private string _errorDialogTitle = string.Empty;
    public string ErrorDialogTitle
    {
        get => _errorDialogTitle;
        set => SetProperty(ref _errorDialogTitle, value);
    }

    private string _errorDialogMessage = string.Empty;
    public string ErrorDialogMessage
    {
        get => _errorDialogMessage;
        set => SetProperty(ref _errorDialogMessage, value);
    }

    public ICommand? DismissErrorDialogCommand { get; private set; }

    /// <summary>
    /// Shows an error dialog with the specified title and message.
    /// </summary>
    public void ShowErrorDialog(string title, string message)
    {
        ErrorDialogTitle = title;
        ErrorDialogMessage = message;
        State.UI.ShowDialog(Models.State.DialogType.Error);
    }

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

    // Boundary mode: tracks whether next boundary operation targets inner or outer
    private BoundaryType _pendingBoundaryType = BoundaryType.Outer;
    public BoundaryType PendingBoundaryType
    {
        get => _pendingBoundaryType;
        set
        {
            if (SetProperty(ref _pendingBoundaryType, value))
            {
                OnPropertyChanged(nameof(BoundaryRecordingHeaderText));
                OnPropertyChanged(nameof(IsInnerBoundaryMode));
            }
        }
    }

    public bool IsInnerBoundaryMode => PendingBoundaryType == BoundaryType.Inner;

    public string BoundaryRecordingHeaderText
    {
        get
        {
            if (_boundaryRecordingService.IsRecording || _boundaryRecordingService.State == BoundaryRecordingState.Paused)
            {
                return _boundaryRecordingService.CurrentBoundaryType == BoundaryType.Inner
                    ? "Recording Inner Boundary"
                    : "Recording Outer Boundary";
            }
            return "Start or Delete Boundary";
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

    // Boundary Player settings
    private bool _isBoundarySectionControlOn;
    public bool IsBoundarySectionControlOn
    {
        get => _isBoundarySectionControlOn;
        set
        {
            SetProperty(ref _isBoundarySectionControlOn, value);
            StatusMessage = value ? "Boundary records when section is on" : "Boundary section control off";
        }
    }

    private bool _isDrawRightSide = true;
    public bool IsDrawRightSide
    {
        get => _isDrawRightSide;
        set
        {
            SetProperty(ref _isDrawRightSide, value);
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
            SetProperty(ref _isDrawAtPivot, value);
            StatusMessage = value ? "Recording at pivot point" : "Recording at tool";
        }
    }

    private double _boundaryOffset;
    public double BoundaryOffset
    {
        get => _boundaryOffset;
        set
        {
            var oldValue = _boundaryOffset;
            SetProperty(ref _boundaryOffset, value);
            if (Math.Abs(oldValue - value) > 0.0001)
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

    // Smart WAS calibration dialog
    private SmartWasViewModel? _smartWasViewModel;
    public SmartWasViewModel? SmartWasViewModel
    {
        get => _smartWasViewModel;
        set => SetProperty(ref _smartWasViewModel, value);
    }

    // Load Vehicle/Tool picker dialog (#346)
    private LoadVehicleToolDialogViewModel? _loadVehicleToolDialogVm;
    public LoadVehicleToolDialogViewModel? LoadVehicleToolDialogVm
    {
        get => _loadVehicleToolDialogVm;
        set => SetProperty(ref _loadVehicleToolDialogVm, value);
    }

    // Start Work Session dialog (#349 M3)
    private StartWorkSessionDialogViewModel? _startWorkSessionDialogVm;
    public StartWorkSessionDialogViewModel? StartWorkSessionDialogVm
    {
        get => _startWorkSessionDialogVm;
        set => SetProperty(ref _startWorkSessionDialogVm, value);
    }

    // Resume Job cross-field history dialog (#349 M4)
    private ResumeJobDialogViewModel? _resumeJobDialogVm;
    public ResumeJobDialogViewModel? ResumeJobDialogVm
    {
        get => _resumeJobDialogVm;
        set => SetProperty(ref _resumeJobDialogVm, value);
    }

    public ICommand? ShowConfigurationDialogCommand { get; private set; }
    public ICommand? CancelConfigurationDialogCommand { get; private set; }
    public ICommand? ShowLoadVehicleToolDialogCommand { get; private set; }
    public ICommand? CancelLoadVehicleToolDialogCommand { get; private set; }
    public ICommand? ShowStartWorkSessionDialogCommand { get; private set; }
    public ICommand? CancelStartWorkSessionDialogCommand { get; private set; }
    public ICommand? ShowResumeJobDialogCommand { get; private set; }
    public ICommand? CancelResumeJobDialogCommand { get; private set; }
    public ICommand? ResumeLastJobCommand { get; private set; }
    public ICommand? ShowAutoSteerConfigCommand { get; private set; }
    public ICommand? ShowSmartWasCommand { get; private set; }
    public ICommand? CloseSmartWasDialogCommand { get; private set; }

    public string CurrentProfileName => _configurationService.Store.ActiveVehicleProfileName;
    public string CurrentToolProfileName => _configurationService.Store.ActiveToolProfileName;

    /// <summary>
    /// Combined "Vehicle / Tool" label for the configuration-panel pill so
    /// the operator sees both halves of the active pair at a glance.
    /// </summary>
    public string CurrentProfileSummary
    {
        get
        {
            var v = _configurationService.Store.ActiveVehicleProfileName;
            var t = _configurationService.Store.ActiveToolProfileName;
            if (string.IsNullOrEmpty(t)) return v;
            return $"{v} / {t}";
        }
    }

    /// <summary>
    /// Notifies bindings tied to the active vehicle/tool profile names.
    /// Wired to <see cref="IConfigurationService.ProfileLoaded"/> /
    /// <see cref="IConfigurationService.ProfileSaved"/> so labels like the
    /// status pill on ConfigurationPanel refresh after the picker dialog
    /// or any other profile change.
    /// </summary>
    private void RaiseProfileNameChanged()
    {
        OnPropertyChanged(nameof(CurrentProfileName));
        OnPropertyChanged(nameof(CurrentToolProfileName));
        OnPropertyChanged(nameof(CurrentProfileSummary));
    }

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

            // Set HasHeadland based on whether we have a valid headland line
            HasHeadland = value != null && value.Count >= 3;

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

    // Headland undo state
    private List<Vec3>? _previousHeadlandLine;
    private bool _previousHasHeadland;

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
                if (State.UI.IsFieldBuilderDialogVisible)
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
        set
        {
            if (SetProperty(ref _currentFieldName, value))
                OnPropertyChanged(nameof(CurrentFieldAndJobLabel));
        }
    }

    /// <summary>
    /// Active job's task name, or empty when no job is active.
    /// </summary>
    public string CurrentJobTaskName => _jobService?.ActiveJob?.TaskName ?? string.Empty;

    /// <summary>
    /// Combined "field / task" label for the JobMenu pill and the
    /// upper-right status strip. Returns just the field name when there's
    /// no active job (e.g. field-only opens once that path lands).
    /// </summary>
    public string CurrentFieldAndJobLabel
    {
        get
        {
            var fieldName = CurrentFieldName;
            var task = CurrentJobTaskName;
            if (string.IsNullOrEmpty(fieldName)) return string.Empty;
            return string.IsNullOrEmpty(task) ? fieldName : $"{fieldName} / {task}";
        }
    }

    // Commands
    public ICommand? ToggleViewSettingsPanelCommand { get; private set; }
    public ICommand? ToggleFileMenuPanelCommand { get; private set; }
    public ICommand? ToggleToolsPanelCommand { get; private set; }
    public ICommand? ToggleConfigurationPanelCommand { get; private set; }
    public ICommand? ToggleFieldOperationsPanelCommand { get; private set; }
    public ICommand? ToggleFieldToolsPanelCommand { get; private set; }
    public ICommand? ToggleAutoTrackCommand { get; private set; }
    public ICommand? ToggleGridCommand { get; private set; }
    public ICommand? ToggleDayNightCommand { get; private set; }
    public ICommand? Toggle2D3DCommand { get; private set; }
    public ICommand? ToggleNorthUpCommand { get; private set; }
    public ICommand? ToggleCameraModeCommand { get; private set; }
    public ICommand? IncreaseCameraPitchCommand { get; private set; }
    public ICommand? DecreaseCameraPitchCommand { get; private set; }
    public ICommand? CycleDisplayResolutionCommand { get; private set; }

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

    public event Action<string>? LanguageChanged;

    /// <summary>
    /// Fired after a field has been fully loaded (boundary, tracks, coverage, recpath).
    /// Subscribe to react to field changes without coupling to OpenFieldAsync internals.
    /// </summary>
    public event Action<Field?>? FieldFullyLoaded;

    /// <summary>
    /// Platform-provided callback that captures the current window as a PNG byte array.
    /// Set by platform code (MainWindow/MainView) after ViewModel is created.
    /// Used by debug dump to include a screenshot.
    /// </summary>
    public Func<byte[]?>? ScreenshotProvider { get; set; }

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
    public ICommand? RecordInnerBoundaryCommand { get; private set; }
    public ICommand? DriveAroundInnerBoundaryCommand { get; private set; }
    public ICommand? DrawMapInnerBoundaryCommand { get; private set; }
    public ICommand? ToggleDriveThroughCommand { get; private set; }
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
    public ICommand? FinishCurveRecordingCommand { get; private set; }
    public ICommand? CycleABLinesCommand { get; private set; }
    public ICommand? SmoothABLineCommand { get; private set; }
    public ICommand? NudgeLeftCommand { get; private set; }
    public ICommand? NudgeRightCommand { get; private set; }
    public ICommand? FineNudgeLeftCommand { get; private set; }
    public ICommand? FineNudgeRightCommand { get; private set; }
    public ICommand? HalfToolNudgeLeftCommand { get; private set; }
    public ICommand? HalfToolNudgeRightCommand { get; private set; }
    public ICommand? ResetNudgeCommand { get; private set; }
    public ICommand? StartDrawABModeCommand { get; private set; }
    public ICommand? StartDrawCurveModeCommand { get; private set; }
    public ICommand? FinishDrawCurveCommand { get; private set; }
    public ICommand? UndoLastDrawnPointCommand { get; private set; }
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
    public ICommand? DeleteFlagCommand { get; private set; }
    public ICommand? PlaceFlagOnClickCommand { get; private set; }
    public ICommand? PlaceFlagHereCommand { get; private set; }
    public ICommand? ShowFlagListCommand { get; private set; }
    public ICommand? CloseFlagListCommand { get; private set; }

    // Right Navigation Panel Commands
    public ICommand? ToggleContourModeCommand { get; private set; }
    public ICommand? DeleteContoursCommand { get; private set; }
    public ICommand? DeleteAppliedAreaCommand { get; private set; }
    public ICommand? ToggleTramDisplayCommand { get; private set; }
    public ICommand? BuildTramLinesCommand { get; private set; }
    public ICommand? CreateTrackFromBoundaryCommand { get; private set; }
    public ICommand? CreateCurveFromBoundaryCommand { get; private set; }
    public ICommand? CreateTracksFromAllEdgesCommand { get; private set; }
    public ICommand? CreateALineFromPositionCommand { get; private set; }
    public ICommand? ShowFieldBuilderCommand { get; private set; }
    public ICommand? CloseFieldBuilderCommand { get; private set; }
    public ICommand? IncreaseHeadlandDistanceCommand { get; private set; }
    public ICommand? DecreaseHeadlandDistanceCommand { get; private set; }

    public System.Collections.Generic.IReadOnlyList<Models.Base.Vec3>? CurrentHeadlandLineForPreview => _currentHeadlandLine;

    public string HeadlandStatusText
    {
        get
        {
            if (!HasHeadland || _currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
                return HeadlandSegments.Count > 0 ? $"{HeadlandSegments.Count} lines (no intersections)" : "No headland lines";

            double area = System.Math.Abs(CalculateSignedArea(_currentHeadlandLine)) / 10000.0; // m2 -> hectares
            return $"{area:F2} ha ({HeadlandSegments.Count} lines)";
        }
    }

    /// <summary>
    /// List of headland segments that form the headland polygon.
    /// </summary>
    public ObservableCollection<Models.Headland.HeadlandSegment> HeadlandSegments { get; } = new();

    private Models.Headland.HeadlandSegment? _selectedHeadlandSegment;
    public Models.Headland.HeadlandSegment? SelectedHeadlandSegment
    {
        get => _selectedHeadlandSegment;
        set => SetProperty(ref _selectedHeadlandSegment, value);
    }


    public ICommand? ShowTramSettingsCommand { get; private set; }
    public ICommand? CloseTramSettingsCommand { get; private set; }
    public ICommand? IncreaseTramPassesCommand { get; private set; }
    public ICommand? DecreaseTramPassesCommand { get; private set; }
    public ICommand? SetTramModeOffCommand { get; private set; }
    public ICommand? SetTramModeAllCommand { get; private set; }
    public ICommand? SetTramModeLinesCommand { get; private set; }
    public ICommand? SetTramModeOuterCommand { get; private set; }

    public int TramPasses => ConfigStore.Tram.Passes;
    public int TramStartPass => ConfigStore.Tram.StartPass;
    public double TramWidth => ConfigStore.Tram.TramWidth;
    public System.Collections.ObjectModel.ObservableCollection<Models.Tram.TramSystem> TramSystems => ConfigStore.Tram.Systems;
    public string TramToolWidthDisplay => $"{ConfigStore.ActualToolWidth:F2} m";
    public string TramWidthDisplay => $"{ConfigStore.Tram.TramWidth:F2} m";
    public string TramTrackWidthDisplay => $"{ConfigStore.Vehicle.TrackWidth:F2} m";
    public string TramLineCountDisplay => $"{_tramLineService.ParallelTramLines.Count}";
    public ICommand? IncreaseTramStartPassCommand { get; private set; }
    public ICommand? DecreaseTramStartPassCommand { get; private set; }
    public ICommand? SwapTramSideCommand { get; private set; }
    public ICommand? ClearTramLinesCommand { get; private set; }
    public ICommand? ToggleTramLeftManualCommand { get; private set; }
    public ICommand? ToggleTramRightManualCommand { get; private set; }
    public bool TramLeftManualOn => _tramLineService.IsLeftManualOn;
    public bool TramRightManualOn => _tramLineService.IsRightManualOn;
    /// <summary>Runtime tram detection byte: bit 0=right wheel, bit 1=left wheel.</summary>
    public byte TramControlByte { get; set; }
    public double TramTrackWidthValue => ConfigStore.Vehicle.TrackWidth;
    public int TramLineNumber => ConfigStore.Guidance.TramLine;
    public ICommand? IncreaseTramLineCommand { get; private set; }
    public ICommand? DecreaseTramLineCommand { get; private set; }

    public string TramDisplayIcon => ConfigStore.Tram.DisplayMode switch
    {
        Models.Configuration.TramDisplayMode.All => "avares://AgValoniaGPS.Views/Assets/Icons/TramAll.png",
        Models.Configuration.TramDisplayMode.LinesOnly => "avares://AgValoniaGPS.Views/Assets/Icons/TramLines.png",
        Models.Configuration.TramDisplayMode.OuterOnly => "avares://AgValoniaGPS.Views/Assets/Icons/TramOuter.png",
        _ => "avares://AgValoniaGPS.Views/Assets/Icons/TramOff.png"
    };

    public string TramDisplayLabel => ConfigStore.Tram.DisplayMode switch
    {
        Models.Configuration.TramDisplayMode.All => "All",
        Models.Configuration.TramDisplayMode.LinesOnly => "Lines",
        Models.Configuration.TramDisplayMode.OuterOnly => "Outer",
        _ => "Off"
    };

    /// <summary>
    /// Get tram line geometry for canvas preview rendering.
    /// </summary>
    public (IReadOnlyList<Models.Base.Vec2> outer, IReadOnlyList<Models.Base.Vec2> inner,
            IReadOnlyList<IReadOnlyList<Models.Base.Vec2>> parallel,
            IReadOnlyList<IReadOnlyList<Models.Base.Vec2>> boundaryExtra)? GetTramLineData()
    {
        if (!_tramLineService.HasTramLines) return null;
        return (_tramLineService.OuterBoundaryTrack, _tramLineService.InnerBoundaryTrack,
                _tramLineService.ParallelTramLines, _tramLineService.BoundaryExtraLines);
    }

    /// <summary>
    /// Get the line index range for a named tram system. Returns (-1,0) for boundary systems.
    /// </summary>
    public (int start, int count, bool isBoundary) GetTramSystemLineRange(string systemName)
    {
        return _tramSystemLineRanges.TryGetValue(systemName, out var range) ? range : (-1, 0, false);
    }
    public ICommand? ToggleRecordedPathsCommand { get; private set; }
    public ICommand? StartRecordedPathCommand { get; private set; }
    public ICommand? StopRecordedPathCommand { get; private set; }
    public ICommand? StartContourRecordingCommand { get; private set; }
    public ICommand? StopContourRecordingCommand { get; private set; }
    public ICommand? DeleteContourTrackCommand { get; private set; }
    public ICommand? ImportTracksCommand { get; private set; }
    public ICommand? ImportTracksFromFieldCommand { get; private set; }
    public ICommand? CloseImportTracksDialogCommand { get; private set; }
    public ObservableCollection<string> ImportFieldsList { get; } = new();
    public ICommand? ToggleManualModeCommand { get; private set; }
    public ICommand? ToggleSectionMasterCommand { get; private set; }
    public ICommand? ToggleSectionCommand { get; private set; }
    public ICommand? ToggleYouTurnCommand { get; private set; }
    public ICommand? ManualYouTurnLeftCommand { get; private set; }
    public ICommand? ManualYouTurnRightCommand { get; private set; }
    public ICommand? ToggleUTurnDirectionCommand { get; private set; }
    public ICommand? ToggleAutoSteerCommand { get; private set; }

    // Chart Commands
    public ICommand? ToggleSteerChartPanelCommand { get; private set; }
    public ICommand? ToggleHeadingChartPanelCommand { get; private set; }
    public ICommand? ToggleXTEChartPanelCommand { get; private set; }


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
    private void SaveBackgroundImage(string sourcePath, string fieldPath, double nwLat, double nwLon, double seLat, double seLon,
        double mercMinX, double mercMaxX, double mercMinY, double mercMaxY)
    {
        // Copy image to field directory
        var destPath = Path.Combine(fieldPath, "BackPic.png");
        File.Copy(sourcePath, destPath, overwrite: true);

        // Save geo-reference file (WGS84 format + Mercator bounds)
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var geoContent = $"$BackPic\ntrue\n{nwLat.ToString(inv)}\n{nwLon.ToString(inv)}\n{seLat.ToString(inv)}\n{seLon.ToString(inv)}\n{mercMinX.ToString(inv)}\n{mercMaxX.ToString(inv)}\n{mercMinY.ToString(inv)}\n{mercMaxY.ToString(inv)}";
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
            // Format: $BackPic, true, nwLat, nwLon, seLat, seLon[, mercMinX, mercMaxX, mercMinY, mercMaxY]
            var lines = File.ReadAllLines(backPicGeoPath);
            if (lines.Length < 6 || lines[0] != "$BackPic")
                return;

            // Check if enabled
            if (!bool.TryParse(lines[1], out bool enabled) || !enabled)
                return;

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.NumberStyles.Float;

            // Parse WGS84 bounds
            if (!double.TryParse(lines[2], style, inv, out double nwLat) ||
                !double.TryParse(lines[3], style, inv, out double nwLon) ||
                !double.TryParse(lines[4], style, inv, out double seLat) ||
                !double.TryParse(lines[5], style, inv, out double seLon))
                return;

            // Parse Mercator bounds (optional for backwards compatibility)
            double mercMinX = 0, mercMaxX = 0, mercMinY = 0, mercMaxY = 0;
            bool hasMercator = lines.Length >= 10 &&
                double.TryParse(lines[6], style, inv, out mercMinX) &&
                double.TryParse(lines[7], style, inv, out mercMaxX) &&
                double.TryParse(lines[8], style, inv, out mercMinY) &&
                double.TryParse(lines[9], style, inv, out mercMaxY);

            // Use field origin for LocalPlane (same origin used for boundary coordinates)
            // This ensures the background image aligns with the boundary
            var origin = new Wgs84(_fieldOriginLatitude, _fieldOriginLongitude);
            var sharedProps = new SharedFieldProperties();
            var localPlane = new LocalPlane(origin, sharedProps);

            _logger.LogDebug($"[LoadBG] Field origin from ViewModel: ({_fieldOriginLatitude:F8}, {_fieldOriginLongitude:F8})");
            _logger.LogDebug($"[LoadBG] LocalPlane origin: ({localPlane.Origin.Latitude:F8}, {localPlane.Origin.Longitude:F8})");
            _logger.LogDebug($"[LoadBG] WGS84 bounds: NW=({nwLat:F8}, {nwLon:F8}), SE=({seLat:F8}, {seLon:F8})");

            // Convert WGS84 to local coordinates
            var nwWgs = new Wgs84(nwLat, nwLon);
            var seWgs = new Wgs84(seLat, seLon);
            var nwLocal = localPlane.ConvertWgs84ToGeoCoord(nwWgs);
            var seLocal = localPlane.ConvertWgs84ToGeoCoord(seWgs);

            _logger.LogDebug($"[LoadBG] Local bounds: NW=({nwLocal.Easting:F2}, {nwLocal.Northing:F2}), SE=({seLocal.Easting:F2}, {seLocal.Northing:F2})");

            // Verify field origin converts to (0,0) in local coords
            var originWgs = new Wgs84(_fieldOriginLatitude, _fieldOriginLongitude);
            var originLocal = localPlane.ConvertWgs84ToGeoCoord(originWgs);
            _logger.LogDebug($"[LoadBG] Field origin in local coords (should be ~0,0): ({originLocal.Easting:F2}, {originLocal.Northing:F2})");

            // Use Mercator-aware method if bounds available, otherwise fall back to linear
            if (hasMercator)
            {
                _mapService.SetBackgroundImageWithMercator(backPicPath,
                    nwLocal.Easting, nwLocal.Northing, seLocal.Easting, seLocal.Northing,
                    mercMinX, mercMaxX, mercMinY, mercMaxY,
                    _fieldOriginLatitude, _fieldOriginLongitude);
            }
            else
            {
                _mapService.SetBackgroundImage(backPicPath, nwLocal.Easting, nwLocal.Northing, seLocal.Easting, seLocal.Northing);
            }
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
    /// Also populates HeadlandLine from HeadlandPolygon for section control.
    /// </summary>
    private void SetCurrentBoundary(Boundary? boundary)
    {
        _mapService.SetBoundary(boundary);
        CurrentBoundary = boundary;

        // Keep the in-memory Field model's boundary in sync. Without this,
        // ActiveField.Boundary remains whatever LoadField returned at open
        // time (usually the empty placeholder for a freshly-created field),
        // and CloseFieldAsync → FieldService.SaveField → SaveBoundary
        // overwrites the user's drawing on disk with "$Boundary\n".
        if (ActiveField != null)
            ActiveField.Boundary = boundary;

        // Set HasBoundary based on whether we have a valid outer boundary
        HasBoundary = boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid;

        // Set fixed field bounds for coverage bitmap coordinate system
        if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid && boundary.OuterBoundary.Points.Count > 0)
        {
            // Calculate bounding box with padding for stable bitmap coordinates
            double minE = double.MaxValue, maxE = double.MinValue;
            double minN = double.MaxValue, maxN = double.MinValue;
            foreach (var point in boundary.OuterBoundary.Points)
            {
                minE = Math.Min(minE, point.Easting);
                maxE = Math.Max(maxE, point.Easting);
                minN = Math.Min(minN, point.Northing);
                maxN = Math.Max(maxN, point.Northing);
            }
            // Add 50m padding to handle coverage near edges
            const double padding = 50.0;
            double boundsMinE = minE - padding;
            double boundsMaxE = maxE + padding;
            double boundsMinN = minN - padding;
            double boundsMaxN = maxN + padding;

            _coverageMapService.SetFieldBounds(boundsMinE, boundsMaxE, boundsMinN, boundsMaxN);

            // Initialize the coverage bitmap eagerly on field load
            // Background will be composited into it when SetBackgroundImage is called
            _mapService.InitializeCoverageBitmapWithBounds(boundsMinE, boundsMaxE, boundsMinN, boundsMaxN);
        }
        else
        {
            _coverageMapService.ClearFieldBounds();
            _autoCoverageBoundsInitialized = false; // Allow auto-init from GPS position
        }

        // Sync to FieldState for section control boundary/headland detection
        State.Field.CurrentBoundary = boundary;

        // Update area display
        OnPropertyChanged(nameof(BoundaryAreaDisplay));
        if (boundary != null && boundary.IsValid)
        {
            var boundaryAreas = new System.Collections.Generic.List<double> { boundary.AreaHectares * 10000 };
            _fieldStatistics.UpdateBoundaryAreas(boundaryAreas);
        }

        // Populate HeadlandLine from HeadlandPolygon for section control IsPointInHeadland check
        _logger.LogDebug($"[Headland] SetCurrentBoundary: HeadlandPolygon={boundary?.HeadlandPolygon != null}, IsValid={boundary?.HeadlandPolygon?.IsValid}, PointCount={boundary?.HeadlandPolygon?.Points?.Count ?? 0}");
        if (boundary?.HeadlandPolygon != null && boundary.HeadlandPolygon.IsValid)
        {
            var headlandPoints = new List<Vec3>();
            foreach (var point in boundary.HeadlandPolygon.Points)
            {
                headlandPoints.Add(new Vec3(point.Easting, point.Northing, point.Heading));
            }
            State.Field.HeadlandLine = headlandPoints;
            _currentHeadlandLine = headlandPoints;
            _mapService.SetHeadlandLine(headlandPoints);
            HasHeadland = true;
            IsHeadlandOn = true;
            _logger.LogDebug($"[Headland] Loaded {headlandPoints.Count} points from HeadlandPolygon for YouTurn");
        }
        else
        {
            // Boundary didn't carry a HeadlandPolygon (field.geojson has no headland role).
            // DO NOT clobber State.Field.HeadlandLine here — the legacy Headlines.txt
            // loader (LoadHeadland) runs separately on field open and is the authoritative
            // source for the legacy-format case. Nulling it here races with that loader
            // and silently disables U-turn headland detection (#289 F3). Field close and
            // explicit headland removal handle the reset case elsewhere.
            _logger.LogDebug($"[Headland] Boundary has no HeadlandPolygon — deferring to LoadHeadland / existing state");
        }

        // Sync boundary + headland to pipeline for guidance computations
        SyncGuidanceStateToPipeline();
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
    /// Parses ALL coordinate blocks: first = outer boundary, subsequent = inner boundaries.
    /// Results stored in both _kmlBoundaryPoints (first polygon, for field-creation flow)
    /// and _kmlParsedPolygons (all polygons, for import-to-existing flow).
    /// </summary>
    private void ParseKmlFile(string filePath)
    {
        _kmlBoundaryPoints.Clear();
        _kmlParsedPolygons.Clear();
        KmlBoundaryPointCount = 0;
        KmlCenterLatitude = 0;
        KmlCenterLongitude = 0;

        try
        {
            using var reader = new StreamReader(filePath);
            double sumLat = 0, sumLon = 0;
            int totalValidPoints = 0;

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (line == null) continue;

                int startIndex = line.IndexOf("<coordinates>");

                if (startIndex != -1)
                {
                    string? coordinates = null;

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
                        var polygonPoints = new List<(double Latitude, double Longitude)>();

                        foreach (string item in numberSets)
                        {
                            if (item.Length < 3) continue;

                            string[] parts = item.Split(',');
                            if (parts.Length >= 2 &&
                                double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lon) &&
                                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lat))
                            {
                                polygonPoints.Add((lat, lon));
                                sumLat += lat;
                                sumLon += lon;
                                totalValidPoints++;
                            }
                        }

                        if (polygonPoints.Count >= 3)
                        {
                            _kmlParsedPolygons.Add(polygonPoints);

                            // First polygon also populates _kmlBoundaryPoints for backwards compatibility
                            if (_kmlParsedPolygons.Count == 1)
                            {
                                _kmlBoundaryPoints.AddRange(polygonPoints);
                            }
                        }
                    }
                    // Continue to parse additional coordinate blocks (inner boundaries)
                }
            }

            if (totalValidPoints > 0)
            {
                KmlCenterLatitude = sumLat / totalValidPoints;
                KmlCenterLongitude = sumLon / totalValidPoints;
            }

            KmlBoundaryPointCount = totalValidPoints;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error parsing KML: {ex.Message}";
        }
    }

    /// <summary>
    /// Imports KML boundaries into the currently open field.
    /// First polygon becomes outer (or inner if PendingBoundaryType is Inner),
    /// subsequent polygons are added as inner boundaries.
    /// </summary>
    /// <summary>
    /// Converts existing boundary polygons from local coordinates to WGS84
    /// for display as reference layers in the boundary map dialog.
    /// </summary>
    private void PopulateBoundaryMapExistingPolygons()
    {
        BoundaryMapExistingPolygons.Clear();

        if (_currentBoundary == null || (_fieldOriginLatitude == 0 && _fieldOriginLongitude == 0))
            return;

        try
        {
            var origin = new Wgs84(_fieldOriginLatitude, _fieldOriginLongitude);
            var sharedProps = new SharedFieldProperties();
            var localPlane = new LocalPlane(origin, sharedProps);

            // Add outer boundary
            if (_currentBoundary.OuterBoundary?.Points != null && _currentBoundary.OuterBoundary.Points.Count >= 3)
            {
                var wgs84Points = new List<(double Latitude, double Longitude)>();
                foreach (var pt in _currentBoundary.OuterBoundary.Points)
                {
                    var geoCoord = new GeoCoord(pt.Northing, pt.Easting);
                    var wgs84 = localPlane.ConvertGeoCoordToWgs84(geoCoord);
                    wgs84Points.Add((wgs84.Latitude, wgs84.Longitude));
                }
                BoundaryMapExistingPolygons.Add(wgs84Points);
            }

            // Add inner boundaries
            foreach (var inner in _currentBoundary.InnerBoundaries)
            {
                if (inner.Points.Count >= 3)
                {
                    var wgs84Points = new List<(double Latitude, double Longitude)>();
                    foreach (var pt in inner.Points)
                    {
                        var geoCoord = new GeoCoord(pt.Northing, pt.Easting);
                        var wgs84 = localPlane.ConvertGeoCoordToWgs84(geoCoord);
                        wgs84Points.Add((wgs84.Latitude, wgs84.Longitude));
                    }
                    BoundaryMapExistingPolygons.Add(wgs84Points);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"[BoundaryMap] Failed to populate existing polygons: {ex.Message}");
        }
    }

    private void ImportKmlToExistingField()
    {
        if (string.IsNullOrEmpty(CurrentFieldName) || _kmlParsedPolygons.Count == 0)
        {
            StatusMessage = "No field open or no KML polygons parsed";
            return;
        }

        try
        {
            var fieldPath = Path.Combine(_settingsService.Settings.FieldsDirectory, CurrentFieldName);
            var boundary = _boundaryFileService.LoadBoundary(fieldPath) ?? new Boundary();

            var origin = new Wgs84(_fieldOriginLatitude, _fieldOriginLongitude);
            var sharedProps = new SharedFieldProperties();
            var localPlane = new LocalPlane(origin, sharedProps);

            for (int polyIdx = 0; polyIdx < _kmlParsedPolygons.Count; polyIdx++)
            {
                var polygon = new BoundaryPolygon();
                foreach (var (lat, lon) in _kmlParsedPolygons[polyIdx])
                {
                    var wgs84 = new Wgs84(lat, lon);
                    var geoCoord = localPlane.ConvertWgs84ToGeoCoord(wgs84);
                    polygon.Points.Add(new BoundaryPoint(geoCoord.Easting, geoCoord.Northing, 0));
                }

                // First polygon: respects PendingBoundaryType
                // Subsequent polygons: always inner
                if (polyIdx == 0 && PendingBoundaryType == BoundaryType.Outer)
                    boundary.OuterBoundary = polygon;
                else
                    boundary.InnerBoundaries.Add(polygon);
            }

            _boundaryFileService.SaveBoundary(boundary, fieldPath);
            SetCurrentBoundary(boundary);
            CenterMapOnBoundary(boundary);
            RefreshBoundaryList();

            // Update boundary area stats
            if (boundary.OuterBoundary != null)
            {
                var boundaryAreas = new List<double> { boundary.AreaHectares * 10000 };
                _fieldStatistics.UpdateBoundaryAreas(boundaryAreas);
                OnPropertyChanged(nameof(BoundaryAreaDisplay));
            }

            State.UI.CloseDialog();
            PendingBoundaryType = BoundaryType.Outer;

            var innerCount = _kmlParsedPolygons.Count > 1 ? _kmlParsedPolygons.Count - 1 : 0;
            var innerMsg = innerCount > 0 ? $" + {innerCount} inner" : "";
            StatusMessage = $"KML boundary imported{innerMsg}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error importing KML boundary: {ex.Message}";
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
    /// Adjust headland distance by a delta and rebuild the headland polygon.
    /// Positive delta extends (widens) the headland, negative shrinks it.
    /// </summary>
    private void AdjustHeadlandDistance(double deltaMeters)
    {
        if (!HasHeadland)
        {
            StatusMessage = "No headland to adjust - build one first";
            return;
        }

        HeadlandDistance += deltaMeters;
        BuildHeadlandFromBoundary();
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

        // Save undo state before applying
        _previousHeadlandLine = _currentHeadlandLine != null ? new List<Vec3>(_currentHeadlandLine) : null;
        _previousHasHeadland = HasHeadland;

        CurrentHeadlandLine = result.OuterHeadlandLine;
        HeadlandPreviewLine = null;
        HasHeadland = true;
        IsHeadlandOn = true;

        // Update _currentHeadlandLine for YouTurn zone detection (same as SetCurrentBoundary does on field load)
        if (result.OuterHeadlandLine != null && result.OuterHeadlandLine.Count >= 3)
        {
            _currentHeadlandLine = result.OuterHeadlandLine;
            State.Field.HeadlandLine = result.OuterHeadlandLine;
            _mapService.SetHeadlandLine(result.OuterHeadlandLine);
            _mapService.SetHeadlandVisible(true);
        }

        StatusMessage = $"Headland built at {HeadlandDistance:F1}m ({result.OuterHeadlandLine?.Count ?? 0} pts from {boundary.OuterBoundary.Points.Count} boundary pts)";
        OnPropertyChanged(nameof(HeadlandStatusText));
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
    public void SaveTracksToFile()
    {
        var activeField = _fieldService.ActiveField;
        if (activeField == null || string.IsNullOrEmpty(activeField.DirectoryPath))
        {
            return; // No active field to save to
        }

        // Update selected track's NudgeDistance from current pass number + nudge offset before saving
        if (SelectedTrack != null)
        {
            double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
            SelectedTrack.NudgeDistance = State.Guidance.HowManyPathsAway * widthMinusOverlap + State.Guidance.NudgeOffset;
            _logger.LogDebug($"[NUDGE] SaveTracksToFile: SelectedTrack '{SelectedTrack.Name}' NudgeDistance = {State.Guidance.HowManyPathsAway} * {widthMinusOverlap:F2} + {State.Guidance.NudgeOffset:F3} = {SelectedTrack.NudgeDistance:F2}m");
        }

        // Debug: Log all tracks' NudgeDistance before saving
        foreach (var track in SavedTracks)
        {
            _logger.LogDebug($"[NUDGE] SaveTracksToFile: Saving '{track.Name}': NudgeDistance={track.NudgeDistance:F2}m");
        }

        try
        {
            Services.TrackFilesService.Save(activeField.DirectoryPath, SavedTracks.ToList());
            _logger.LogDebug("[NUDGE] SaveTracksToFile: Saved {TrackCount} tracks", SavedTracks.Count);
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug("[NUDGE] SaveTracksToFile: FAILED - {ErrorMessage}", ex.Message);
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
                var tracks = Services.TrackFilesService.Load(field.DirectoryPath);
                int loadedCount = 0;
                Track? firstTrack = null;

                foreach (var track in tracks)
                {
                    // Ensure all tracks start inactive (SelectedTrack setter will activate)
                    track.IsActive = false;
                    State.Field.Tracks.Add(track);
                    SavedTracks.Add(track);

                    // Debug: log track details
                    _logger.LogDebug("[TrackFiles] Track: '{TrackName}', Points: {PointCount}, Type: {TrackType}, IsCurve: {IsCurve}", track.Name, track.Points.Count, track.Type, track.IsCurve);

                    if (loadedCount == 0)
                    {
                        firstTrack = track;
                    }
                    loadedCount++;
                }

                _logger.LogDebug($"[TrackFiles] Loaded {loadedCount} tracks from TrackLines.txt");

                // Rebuild recorded paths and contour strips from loaded tracks
                RebuildRecordedPathsAndContours();

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

    private void LoadRecPathFromField(string fieldPath)
    {
        try
        {
            var recPath = Services.RecPathFileService.LoadRecPath(fieldPath);
            if (recPath != null)
            {
                SavedTracks.Add(recPath);
                RecordedPathTracks.Add(recPath);
                UpdateRecordedPathsOnMap();
                _logger.LogDebug($"[RecPath] Loaded recorded path with {recPath.Points.Count} points");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"[RecPath] Failed to load RecPath.txt: {ex.Message}");
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