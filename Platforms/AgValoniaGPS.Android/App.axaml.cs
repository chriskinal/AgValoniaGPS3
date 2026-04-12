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
using Avalonia.Android;
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

        // Extract sound files from Avalonia resources
        ExtractSoundFiles(Services);

        // Apply saved language (#40)
        var savedLang = settingsService.Settings.Language;
        if (!string.IsNullOrEmpty(savedLang) && savedLang != "en")
        {
            try
            {
                AgValoniaGPS.Views.Localization.TranslationSource.Instance.CurrentCulture =
                    new System.Globalization.CultureInfo(savedLang);
            }
            catch { /* fall back to English */ }
        }

        // Create ViewModel on the UI thread before the factory lambda
        Console.WriteLine("[App] Getting MainViewModel...");
        var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        Console.WriteLine("[App] Getting MapService...");
        var mapService = (MapService)_serviceProvider.GetRequiredService<IMapService>();
        Console.WriteLine("[App] Getting CoverageMapService...");
        var coverageService = _serviceProvider.GetRequiredService<ICoverageMapService>();

        // Wire language change to TranslationSource (#40)
        viewModel.LanguageChanged += code =>
        {
            try
            {
                AgValoniaGPS.Views.Localization.TranslationSource.Instance.CurrentCulture =
                    new System.Globalization.CultureInfo(code);
            }
            catch { }
        };

        // Provide DI to chart panels for auto-configuration
        AgValoniaGPS.Views.Controls.Panels.SteerChartPanel.ServiceProvider = Services;
        AgValoniaGPS.Views.Controls.Panels.HeadingChartPanel.ServiceProvider = Services;
        AgValoniaGPS.Views.Controls.Panels.XTEChartPanel.ServiceProvider = Services;

        if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
        {
            Console.WriteLine("[App] Using IActivityApplicationLifetime with MainViewFactory...");
            activityLifetime.MainViewFactory = () =>
            {
                Console.WriteLine("[App] MainViewFactory creating MainView...");
                var mainView = new MainView(viewModel, mapService, coverageService);
                MainView = mainView;
                Console.WriteLine("[App] MainView created and assigned.");
                return mainView;
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            Console.WriteLine("[App] Fallback: Using ISingleViewApplicationLifetime...");
            var mainView = new MainView(viewModel, mapService, coverageService);
            singleViewLifetime.MainView = mainView;
            MainView = mainView;
            Console.WriteLine("[App] MainView created and assigned.");
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("[App] Framework initialization completed.");
    }

    private static void ExtractSoundFiles(IServiceProvider services)
    {
        try
        {
            var audioService = services.GetService<IAudioService>() as AgValoniaGPS.Services.Audio.AudioServiceBase;
            if (audioService == null) return;

            var cacheDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sounds");
            System.IO.Directory.CreateDirectory(cacheDir);

            var soundFiles = AgValoniaGPS.Services.Audio.AudioServiceBase.GetSoundFileNames();

            foreach (var fileName in soundFiles)
            {
                var destPath = System.IO.Path.Combine(cacheDir, fileName);
                if (System.IO.File.Exists(destPath)) continue;

                try
                {
                    var uri = new Uri($"avares://AgValoniaGPS.Views/Assets/Sounds/{fileName}");
                    using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                    using var fileStream = System.IO.File.Create(destPath);
                    stream.CopyTo(fileStream);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Audio] Failed to extract {fileName}: {ex.Message}");
                }
            }

            audioService.SetSoundDirectory(cacheDir);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Audio] Sound extraction failed: {ex.Message}");
        }
    }
}
