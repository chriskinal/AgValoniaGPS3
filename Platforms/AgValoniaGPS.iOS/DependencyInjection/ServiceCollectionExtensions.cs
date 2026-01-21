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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Headland;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using AgValoniaGPS.Services.Tool;
using AgValoniaGPS.Services.Coverage;
using AgValoniaGPS.Services.Section;
using AgValoniaGPS.Services.Tram;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.iOS.Services;

namespace AgValoniaGPS.iOS.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgValoniaServices(this IServiceCollection services)
    {
        // Configure structured logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

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

        // Tool position service (for trailing implements)
        services.AddSingleton<IToolPositionService, ToolPositionService>();

        // Worked area calculation service
        services.AddSingleton<IWorkedAreaService, WorkedAreaService>();

        // Coverage map service (tracks worked area as triangle strips)
        services.AddSingleton<ICoverageMapService, CoverageMapService>();

        // Section control service (automatic section on/off based on coverage, boundaries, headlands)
        services.AddSingleton<ISectionControlService, SectionControlService>();

        // Tram line services (controlled traffic farming)
        services.AddSingleton<ITramLineOffsetService, TramLineOffsetService>();
        services.AddSingleton<ITramLineService, TramLineService>();

        // Vehicle profile service
        services.AddSingleton<IVehicleProfileService, VehicleProfileService>();

        // NTRIP profile service
        services.AddSingleton<INtripProfileService, NtripProfileService>();

        // Configuration service (single source of truth)
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // iOS-specific services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMapService, MapService>();

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
