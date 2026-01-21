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
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.Android.DependencyInjection;
using AgValoniaGPS.Android.Views;
using AgValoniaGPS.Android.Services;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Android;

public partial class App : Avalonia.Application
{
    private IServiceProvider? _serviceProvider;

    public static IServiceProvider? Services { get; private set; }
    public static MainView? MainView { get; private set; }

    public override void Initialize()
    {
        Console.WriteLine("[App] Initializing...");
        AvaloniaXamlLoader.Load(this);
        Console.WriteLine("[App] XAML loaded.");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("[App] Framework initialization starting...");

        // Set up dependency injection
        var services = new ServiceCollection();
        services.AddAgValoniaServices();
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Wire up services that need cross-references
        _serviceProvider.WireUpServices();

        Console.WriteLine("[App] Services configured.");

        // Load settings and sync to ConfigurationStore
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.Load();
        try
        {
            var configService = Services.GetRequiredService<IConfigurationService>();
            configService.LoadAppSettings();
            Console.WriteLine("[App] Settings loaded and synced to ConfigurationStore.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] Error syncing settings: {ex.Message}");
        }

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            Console.WriteLine("[App] Creating MainView...");

            try
            {
                Console.WriteLine("[App] Getting MainViewModel...");
                var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                Console.WriteLine("[App] Getting MapService...");
                var mapService = (MapService)_serviceProvider.GetRequiredService<IMapService>();
                Console.WriteLine("[App] Getting CoverageMapService...");
                var coverageService = _serviceProvider.GetRequiredService<ICoverageMapService>();
                Console.WriteLine("[App] All services retrieved, creating MainView...");

                var mainView = new MainView(viewModel, mapService, coverageService);
                singleViewLifetime.MainView = mainView;
                MainView = mainView;
                Console.WriteLine("[App] MainView created and assigned.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Error creating MainView: {ex}");
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("[App] Framework initialization completed.");
    }
}
