using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AgValoniaGPS.Desktop.Views;
using AgValoniaGPS.Desktop.DependencyInjection;
using AgValoniaGPS.Desktop.Services;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgValoniaGPS.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider? Services { get; private set; }

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
            })
            .Build();

        Services = _host.Services;

        // Wire up cross-referencing services (AutoSteer → UDP)
        Services.WireUpServices();

        // Load settings
        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.Load();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            // Set up DialogService with the main window
            var dialogService = Services.GetRequiredService<DialogService>();
            dialogService.SetParentWindow(mainWindow);

            desktop.Exit += (sender, args) =>
            {
                // Save settings on exit
                settingsService.Save();
                _host?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
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