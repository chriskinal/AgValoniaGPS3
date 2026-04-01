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
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AgValoniaGPS.Desktop.Views;
using AgValoniaGPS.Desktop.DependencyInjection;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgValoniaGPS.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider? Services { get; set; }

    /// <summary>
    /// Hook for integration tests to replace services before DI build.
    /// </summary>
    public static Action<IServiceCollection>? ConfigureTestServices { get; set; }

    /// <summary>
    /// Hook for integration tests to run scenarios after MainWindow is shown.
    /// </summary>
    public static Func<IClassicDesktopStyleApplicationLifetime, Task>? OnAppReady { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build DI container
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddAgValoniaServices();
                ConfigureTestServices?.Invoke(services);
            })
            .Build();

        Services = _host.Services;

        // Provide DI to chart panels for auto-configuration
        AgValoniaGPS.Views.Controls.Panels.SteerChartPanel.ServiceProvider = Services;
        AgValoniaGPS.Views.Controls.Panels.HeadingChartPanel.ServiceProvider = Services;
        AgValoniaGPS.Views.Controls.Panels.XTEChartPanel.ServiceProvider = Services;

        // Wire up cross-referencing services (AutoSteer → UDP)
        Services.WireUpServices();

        // Extract sound files from Avalonia resources for cross-platform audio
        ExtractSoundFiles(Services);

        // Load settings and sync to ConfigurationStore
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.Load();
        var configService = Services.GetRequiredService<IConfigurationService>();
        configService.LoadAppSettings();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            desktop.Exit += (sender, args) =>
            {
                // Save settings on exit
                settingsService.Save();
                _host?.Dispose();
            };

            // Integration test hook: run scenarios after window is ready
            if (OnAppReady != null)
            {
                var callback = OnAppReady;
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await Task.Delay(1000); // Let window fully render
                        await callback(desktop);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[IntTest] SCENARIO FAILED: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        desktop.Shutdown();
                    }
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ExtractSoundFiles(IServiceProvider services)
    {
        try
        {
            var audioService = services.GetService<IAudioService>() as AgValoniaGPS.Services.Audio.AudioService;
            if (audioService == null) return;

            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AgValoniaGPS", "Sounds");
            System.IO.Directory.CreateDirectory(tempDir);

            string[] soundFiles = { "Alarm10.wav", "SteerOn.wav", "SteerOff.wav", "HydUp.wav",
                "HydDown.wav", "rtk_lost.wav", "rtk_back.wav", "SectionOn.wav",
                "SectionOff.wav", "Headland.wav" };

            foreach (var fileName in soundFiles)
            {
                var destPath = System.IO.Path.Combine(tempDir, fileName);
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

            audioService.SetSoundDirectory(tempDir);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Audio] Sound extraction failed: {ex.Message}");
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}