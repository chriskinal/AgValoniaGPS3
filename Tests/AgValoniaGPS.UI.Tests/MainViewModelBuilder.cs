using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Battery;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.YouTurn;
using AgValoniaGPS.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Builds a MainViewModel with all services mocked via NSubstitute.
/// Call Build() to get a ready instance for headless UI tests.
/// </summary>
public class MainViewModelBuilder
{
    public ISettingsService SettingsService { get; } = Substitute.For<ISettingsService>();
    public IVehicleProfileService VehicleProfileService { get; } = Substitute.For<IVehicleProfileService>();
    public INtripProfileService NtripProfileService { get; } = Substitute.For<INtripProfileService>();
    public IAutoSteerService AutoSteerService { get; } = Substitute.For<IAutoSteerService>();
    public ICoverageMapService CoverageMapService { get; } = Substitute.For<ICoverageMapService>();
    public IJobService JobService { get; } = Substitute.For<IJobService>();
    public IMapService MapService { get; } = Substitute.For<IMapService>();
    public IFieldService FieldService { get; } = Substitute.For<IFieldService>();
    public AgValoniaGPS.Services.Pipeline.PipelineIntents Intents { get; } = new();

    public MainViewModelBuilder()
    {
        // Sensible defaults so the VM constructor doesn't NullRef
        var settings = new AppSettings { FieldsDirectory = Path.GetTempPath() };
        SettingsService.Settings.Returns(settings);
        SettingsService.GetSettingsFilePath().Returns(Path.Combine(Path.GetTempPath(), "appsettings.json"));

        VehicleProfileService.VehiclesDirectory.Returns(Path.GetTempPath());
        NtripProfileService.ProfilesDirectory.Returns(Path.GetTempPath());
        NtripProfileService.Profiles.Returns(new List<AgValoniaGPS.Models.Ntrip.NtripProfile>());
        VehicleProfileService.GetAvailableProfiles().Returns(new List<string>());
    }

    public MainViewModel Build()
    {
        // ConfigurationViewModel (lazily built by config-backed dialogs such as
        // App Settings) reads IConfigurationService.Store in its ctor, so the
        // mock must return the real singleton store.
        var configurationService = Substitute.For<IConfigurationService>();
        configurationService.Store.Returns(AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance);

        return new MainViewModel(
            udpService: Substitute.For<IUdpCommunicationService>(),
            gpsService: Substitute.For<AgValoniaGPS.Services.Interfaces.IGpsService>(),
            fieldService: FieldService,
            ntripService: Substitute.For<INtripClientService>(),
            displaySettings: Substitute.For<AgValoniaGPS.Services.Interfaces.IDisplaySettingsService>(),
            fieldStatistics: Substitute.For<AgValoniaGPS.Services.Interfaces.IFieldStatisticsService>(),
            simulatorService: Substitute.For<AgValoniaGPS.Services.Interfaces.IGpsSimulationService>(),
            settingsService: SettingsService,
            mapService: MapService,
            boundaryRecordingService: Substitute.For<IBoundaryRecordingService>(),
            boundaryBuilderService: Substitute.For<IBoundaryBuilderService>(),
            boundaryFileService: new BoundaryFileService(),
            headlandBuilderService: Substitute.For<AgValoniaGPS.Services.Headland.IHeadlandBuilderService>(),
            trackGuidanceService: Substitute.For<ITrackGuidanceService>(),
            youTurnCreationService: new YouTurnCreationService(
                NullLogger<YouTurnCreationService>.Instance,
                Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>()),
            youTurnGuidanceService: new YouTurnGuidanceService(),
            youTurnPathingService: new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance),
            polygonOffsetService: Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>(),
            turnAreaService: Substitute.For<AgValoniaGPS.Services.Interfaces.ITurnAreaService>(),
            vehicleProfileService: VehicleProfileService,
            configurationService: configurationService,
            autoSteerService: AutoSteerService,
            smartWasService: Substitute.For<ISmartWasCalibrationService>(),
            trackCopierService: Substitute.For<ITrackCopierService>(),
            moduleCommunicationService: Substitute.For<IModuleCommunicationService>(),
            toolPositionService: Substitute.For<IToolPositionService>(),
            coverageMapService: CoverageMapService,
            sectionControlService: Substitute.For<ISectionControlService>(),
            ntripProfileService: NtripProfileService,
            chartDataService: Substitute.For<IChartDataService>(),
            audioService: Substitute.For<IAudioService>(),
            elevationLogService: Substitute.For<IElevationLogService>(),
            jobService: JobService,
            tramLineService: Substitute.For<ITramLineService>(),
            gpsPipelineService: Substitute.For<IGpsPipelineService>(),
            intents: Intents,
            logger: NullLogger<MainViewModel>.Instance,
            appState: new ApplicationState(),
            persistentStateService: Substitute.For<IPersistentStateService>(),
            batteryService: new NullBatteryService(),
            uiDispatcher: new AgValoniaGPS.Services.Threading.InlineUiDispatcher());
    }
}
