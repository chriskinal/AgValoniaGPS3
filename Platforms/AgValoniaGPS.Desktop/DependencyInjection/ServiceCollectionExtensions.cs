using System;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Headland;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Desktop.Services;

namespace AgValoniaGPS.Desktop.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgValoniaServices(this IServiceCollection services)
    {
        // Centralized application state (single source of truth)
        services.AddSingleton<ApplicationState>();

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
        services.AddSingleton<ITrackGuidanceService, TrackGuidanceService>();

        // AutoSteer pipeline service (zero-copy GPS→PGN path)
        services.AddSingleton<IAutoSteerService, AutoSteerService>();

        // Module communication service (work switch, steer switch logic)
        services.AddSingleton<IModuleCommunicationService, ModuleCommunicationService>();

        // YouTurn services
        services.AddSingleton<YouTurnCreationService>();
        services.AddSingleton<YouTurnGuidanceService>();

        // Vehicle profile service
        services.AddSingleton<IVehicleProfileService, VehicleProfileService>();

        // Configuration service (single source of truth)
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Platform-specific services (Desktop implementations)
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<MapService>();
        services.AddSingleton<IMapService>(sp => sp.GetRequiredService<MapService>());

        return services;
    }

    /// <summary>
    /// Wire up services that need cross-references after the container is built.
    /// Call this after building the service provider.
    /// </summary>
    public static void WireUpServices(this IServiceProvider serviceProvider)
    {
        // Wire AutoSteerService into UdpCommunicationService for zero-copy GPS processing
        var udpService = serviceProvider.GetRequiredService<IUdpCommunicationService>() as UdpCommunicationService;
        var autoSteerService = serviceProvider.GetRequiredService<IAutoSteerService>();

        udpService?.SetAutoSteerService(autoSteerService);
    }
}