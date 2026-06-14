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
using AgValoniaGPS.Services.Gps;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Headland;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using AgValoniaGPS.Services.Tool;
using AgValoniaGPS.Services.Coverage;
using AgValoniaGPS.Services.Logging;
using AgValoniaGPS.Services.Pipeline;
using AgValoniaGPS.Services.Section;
using AgValoniaGPS.Services.Tram;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Desktop.Services;
using AgValoniaGPS.Views.Infrastructure;

namespace AgValoniaGPS.Desktop.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgValoniaServices(this IServiceCollection services)
    {
        // Configure structured logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.AddProvider(new InMemoryLoggerProvider());
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Centralized application state (single source of truth)
        services.AddSingleton<ApplicationState>();           // ephemeral, in-memory only
        services.AddSingleton(_ => PersistentAppState.Instance); // persisted to appstate.json (same object as .Instance)

        // UI-thread dispatcher abstraction (replaces direct Dispatcher.UIThread
        // in the VM layer — see Plans/CONFIG_STATE_AUDIT.md §11).
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();

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
        services.AddSingleton<IPersistentStateService, PersistentStateService>();

        // Per-session jobs (#349). Reads FieldsRoot through ISettingsService
        // so changes to the user's Documents path are picked up live.
        services.AddSingleton<IJobService>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            return new JobService(() => settings.Settings.FieldsDirectory);
        });

        // Field file I/O services
        services.AddSingleton<FieldPlaneFileService>();
        services.AddSingleton<BoundaryFileService>();

        // Boundary recording service
        services.AddSingleton<IBoundaryRecordingService, BoundaryRecordingService>();
        services.AddSingleton<IBoundaryBuilderService, BoundaryBuilderService>();

        // Headland builder services
        services.AddSingleton<IPolygonOffsetService, PolygonOffsetService>();
        services.AddSingleton<IHeadlandBuilderService, HeadlandBuilderService>();
        services.AddSingleton<ITurnAreaService, TurnAreaService>();

        // Guidance algorithm services
        services.AddSingleton<ITrackGuidanceService, TrackGuidanceService>();

        // AutoSteer pipeline service (zero-copy GPS→PGN path)
        services.AddSingleton<IAutoSteerService, AutoSteerService>();

        // Smart WAS calibration (statistical WAS zero analyzer)
        services.AddSingleton<ISmartWasCalibrationService, SmartWasCalibrationService>();

        // Track copying between fields (transforms via source plane → WGS84 → target plane)
        services.AddSingleton<ITrackCopierService, TrackCopierService>();

        // Chart data service (collects rolling time-series for diagnostic charts)
        services.AddSingleton<IChartDataService, ChartDataService>();

        // Audio service (cross-platform sound effects)
        services.AddSingleton<IAudioService, AgValoniaGPS.Desktop.Services.AudioService>();

        // Battery service — per-platform reader (Windows P/Invoke, macOS pmset,
        // Linux /sys/class/power_supply). Reports Unavailable on machines with
        // no battery so the strip icon hides itself.
        services.AddSingleton<IBatteryService, AgValoniaGPS.Desktop.Services.DesktopBatteryService>();

        // Module communication service (work switch, steer switch logic)
        services.AddSingleton<IModuleCommunicationService, ModuleCommunicationService>();

        // YouTurn services
        services.AddSingleton<YouTurnCreationService>();
        services.AddSingleton<YouTurnGuidanceService>();
        services.AddSingleton<YouTurnPathingService>();
        services.AddSingleton<YouTurnStateMachine>();

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
        services.AddSingleton<IToolProfileService, ToolProfileService>();

        // NTRIP profile service
        services.AddSingleton<INtripProfileService, NtripProfileService>();

        // Configuration service (single source of truth)
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Elevation log service (#120)
        services.AddSingleton<IElevationLogService, ElevationLogService>();

        // GPS processing pipeline (background-thread orchestration)
        // Singleton: UI commands and the cycle worker must share one intent instance.
        services.AddSingleton<IPipelineIntents, PipelineIntents>();
        // Fusion service holds fix-to-fix state between cycles; singleton required.
        services.AddSingleton<IGpsHeadingFusionService, GpsHeadingFusionService>();
        // Position estimator: GPS-anchored snapshot bridge between the GPS
        // arrival path (10 Hz) and the host control loop (100 Hz). Singleton —
        // single shared snapshot.
        services.AddSingleton<IPositionEstimator, PositionEstimator>();
        // Host control loop (#313): runs at 100 Hz on its own thread, sends
        // PGN 254 + PGN 239 every tick to match the firmware autosteer cadence.
        services.AddSingleton<ISteerMachineLoopService>(_ => new SteerMachineLoopService(frequencyHz: 100.0));
        services.AddSingleton<IGpsPipelineService, GpsPipelineService>();

        // Platform-specific services (Desktop implementations)
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