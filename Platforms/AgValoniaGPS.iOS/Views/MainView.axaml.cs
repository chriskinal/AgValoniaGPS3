using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.iOS.Controls;

namespace AgValoniaGPS.iOS.Views;

/// <summary>
/// iOS MainView - Mobile-optimized full-screen GPS display with SkiaSharp map
/// Uses bottom tab bar navigation with modal sheets for sub-panels
/// </summary>
public partial class MainView : UserControl
{
    private SkiaMapControl? _mapControl;

    public MainView()
    {
        try
        {
            Console.WriteLine("[MainView] Constructor starting...");
            InitializeComponent();
            Console.WriteLine("[MainView] InitializeComponent completed.");

            // Set DataContext from DI
            if (App.Services != null)
            {
                Console.WriteLine("[MainView] Getting MainViewModel from DI...");
                DataContext = App.Services.GetRequiredService<MainViewModel>();
                Console.WriteLine("[MainView] DataContext set successfully.");

                // Get map control reference
                _mapControl = this.FindControl<SkiaMapControl>("MapControl");
                if (_mapControl != null)
                {
                    Console.WriteLine("[MainView] Map control found.");
                }
            }
            else
            {
                Console.WriteLine("[MainView] WARNING: App.Services is null!");
            }

            Console.WriteLine("[MainView] Constructor completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainView] Constructor FAILED: {ex}");
        }
    }
}
