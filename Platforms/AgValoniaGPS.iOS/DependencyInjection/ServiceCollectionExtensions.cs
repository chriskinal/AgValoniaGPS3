using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Headland;
using AgValoniaGPS.Services.Guidance;
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

        // Register Models (AOG_Dev integration)
        services.AddSingleton(sp => CreateDefaultVehicleConfiguration());

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

        // YouTurn services
        services.AddSingleton<YouTurnCreationService>();
        services.AddSingleton<YouTurnGuidanceService>();

        // iOS-specific services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMapService, MapService>();

        return services;
    }

    private static VehicleConfiguration CreateDefaultVehicleConfiguration()
    {
        return new VehicleConfiguration
        {
            // Physical dimensions - reasonable defaults for a medium tractor
            AntennaHeight = 3.0,
            AntennaPivot = 0.6,
            AntennaOffset = 0.0,
            Wheelbase = 2.5,
            TrackWidth = 1.8,

            // Vehicle type
            Type = VehicleType.Tractor,

            // Steering limits
            MaxSteerAngle = 35.0,
            MaxAngularVelocity = 35.0,

            // Goal point look-ahead parameters (from AOG_Dev)
            GoalPointLookAheadHold = 4.0,
            GoalPointLookAheadMult = 1.4,
            GoalPointAcquireFactor = 1.5,
            MinLookAheadDistance = 2.0,

            // Stanley steering algorithm parameters
            StanleyDistanceErrorGain = 0.8,
            StanleyHeadingErrorGain = 1.0,
            StanleyIntegralGainAB = 0.0,
            StanleyIntegralDistanceAwayTriggerAB = 0.3,

            // Pure Pursuit algorithm parameters
            PurePursuitIntegralGain = 0.0,

            // Heading dead zone
            DeadZoneHeading = 0.5,
            DeadZoneDelay = 10,

            // U-turn compensation
            UTurnCompensation = 1.0,

            // Hydraulic lift look-ahead distances
            HydLiftLookAheadDistanceLeft = 1.0,
            HydLiftLookAheadDistanceRight = 1.0
        };
    }
}
