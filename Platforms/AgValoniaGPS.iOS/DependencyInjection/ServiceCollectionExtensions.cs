using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Headland;
using AgValoniaGPS.Services.Guidance;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Models;
using AgValoniaGPS.iOS.Services;

namespace AgValoniaGPS.iOS.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgValoniaServices(this IServiceCollection services)
    {
        // Register ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ConfigurationViewModel>();

        // Register Services
        services.AddSingleton<IUdpCommunicationService, UdpCommunicationService>();

        // Core services
        services.AddSingleton<IGpsService, GpsService>();
        services.AddSingleton<IDisplaySettingsService, DisplaySettingsService>();
        services.AddSingleton<IFieldStatisticsService, FieldStatisticsService>();
        services.AddSingleton<IGpsSimulationService, GpsSimulationService>();

        // Other services
        services.AddSingleton<IFieldService, FieldService>();
        services.AddSingleton<IGuidanceService, GuidanceService>();
        services.AddSingleton<INtripClientService, NtripClientService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // Field file I/O services
        services.AddSingleton<FieldPlaneFileService>();
        services.AddSingleton<BoundaryFileService>();

        // Boundary recording service
        services.AddSingleton<IBoundaryRecordingService, BoundaryRecordingService>();

        // Headland builder services
        services.AddSingleton<IPolygonOffsetService, PolygonOffsetService>();
        services.AddSingleton<IHeadlandBuilderService, HeadlandBuilderService>();
        services.AddSingleton<ITurnAreaService, TurnAreaService>();

        // Guidance algorithm services
        services.AddSingleton<IPurePursuitGuidanceService, PurePursuitGuidanceService>();
        services.AddSingleton<ITrackGuidanceService, TrackGuidanceService>();

        // YouTurn services
        services.AddSingleton<YouTurnCreationService>();
        services.AddSingleton<YouTurnGuidanceService>();

        // Vehicle profile service
        services.AddSingleton<IVehicleProfileService, VehicleProfileService>();

        // Configuration service (single source of truth)
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // iOS-specific services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMapService, MapService>();

        return services;
    }
}
