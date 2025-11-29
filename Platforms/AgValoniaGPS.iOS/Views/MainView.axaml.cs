using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.iOS.Views;

/// <summary>
/// iOS MainView - Mobile-optimized full-screen GPS display with SkiaSharp map
/// </summary>
public partial class MainView : UserControl
{
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
