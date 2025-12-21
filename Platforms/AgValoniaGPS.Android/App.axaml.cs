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

        // Wire up services that need cross-references
        _serviceProvider.WireUpServices();

        Console.WriteLine("[App] Services configured.");

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            Console.WriteLine("[App] Creating MainView...");

            var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var mapService = (MapService)_serviceProvider.GetRequiredService<IMapService>();

            singleViewLifetime.MainView = new MainView(viewModel, mapService);
            Console.WriteLine("[App] MainView created and assigned.");
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("[App] Framework initialization completed.");
    }
}
