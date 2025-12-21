using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AgValoniaGPS.iOS.Views;
using AgValoniaGPS.iOS.DependencyInjection;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AgValoniaGPS.iOS;

public partial class App : Avalonia.Application
{
    private IServiceProvider? _services;

    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[App] Initialize starting...");
            AvaloniaXamlLoader.Load(this);
            System.Diagnostics.Debug.WriteLine("[App] Initialize completed.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Initialize FAILED: {ex}");
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[App] OnFrameworkInitializationCompleted starting...");

            // Build DI container
            System.Diagnostics.Debug.WriteLine("[App] Building DI container...");
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddAgValoniaServices();
            _services = serviceCollection.BuildServiceProvider();
            Services = _services;
            System.Diagnostics.Debug.WriteLine("[App] DI container built.");

            // Wire up cross-referencing services (AutoSteer → UDP)
            Services.WireUpServices();
            System.Diagnostics.Debug.WriteLine("[App] Services wired up.");

            // Load settings
            System.Diagnostics.Debug.WriteLine("[App] Loading settings...");
            var settingsService = Services.GetRequiredService<ISettingsService>();
            settingsService.Load();
            System.Diagnostics.Debug.WriteLine("[App] Settings loaded.");

            if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                System.Diagnostics.Debug.WriteLine("[App] Creating MainView with ViewModel...");
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                DisableAvaloniaDataAnnotationValidation();

                // Get MainViewModel and MapService from DI and create view with them
                var viewModel = Services.GetRequiredService<MainViewModel>();
                System.Diagnostics.Debug.WriteLine("[App] MainViewModel created from DI.");

                var mapService = Services.GetRequiredService<AgValoniaGPS.Services.Interfaces.IMapService>();
                var concreteMapService = mapService as AgValoniaGPS.iOS.Services.MapService;
                System.Diagnostics.Debug.WriteLine($"[App] MapService retrieved from DI: {concreteMapService != null}");

                singleViewPlatform.MainView = new MainView(viewModel, concreteMapService!);
                System.Diagnostics.Debug.WriteLine("[App] MainView created and assigned.");
            }

            base.OnFrameworkInitializationCompleted();
            System.Diagnostics.Debug.WriteLine("[App] OnFrameworkInitializationCompleted finished.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] OnFrameworkInitializationCompleted FAILED: {ex}");
            throw;
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
