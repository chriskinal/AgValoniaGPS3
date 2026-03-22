using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
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
        return new MainViewModel(
            udpService: Substitute.For<IUdpCommunicationService>(),
            gpsService: Substitute.For<AgValoniaGPS.Services.Interfaces.IGpsService>(),
            fieldService: Substitute.For<IFieldService>(),
            ntripService: Substitute.For<INtripClientService>(),
            displaySettings: Substitute.For<AgValoniaGPS.Services.Interfaces.IDisplaySettingsService>(),
            fieldStatistics: Substitute.For<AgValoniaGPS.Services.Interfaces.IFieldStatisticsService>(),
            simulatorService: Substitute.For<AgValoniaGPS.Services.Interfaces.IGpsSimulationService>(),
            settingsService: SettingsService,
            mapService: Substitute.For<IMapService>(),
            boundaryRecordingService: Substitute.For<IBoundaryRecordingService>(),
            boundaryFileService: new BoundaryFileService(),
            headlandBuilderService: Substitute.For<AgValoniaGPS.Services.Headland.IHeadlandBuilderService>(),
            trackGuidanceService: Substitute.For<ITrackGuidanceService>(),
            youTurnCreationService: new YouTurnCreationService(NullLogger<YouTurnCreationService>.Instance),
            youTurnGuidanceService: new YouTurnGuidanceService(),
            polygonOffsetService: Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>(),
            turnAreaService: Substitute.For<AgValoniaGPS.Services.Interfaces.ITurnAreaService>(),
            vehicleProfileService: VehicleProfileService,
            configurationService: Substitute.For<IConfigurationService>(),
            autoSteerService: Substitute.For<IAutoSteerService>(),
            moduleCommunicationService: Substitute.For<IModuleCommunicationService>(),
            toolPositionService: Substitute.For<IToolPositionService>(),
            coverageMapService: Substitute.For<ICoverageMapService>(),
            sectionControlService: Substitute.For<ISectionControlService>(),
            ntripProfileService: NtripProfileService,
            logger: NullLogger<MainViewModel>.Instance,
            appState: new ApplicationState());
    }
}
