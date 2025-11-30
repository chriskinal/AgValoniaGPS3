using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;

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

            // Ensure ClipToBounds is false for all ancestors when loaded
            this.Loaded += OnLoaded;

            Console.WriteLine("[MainView] Constructor completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainView] Constructor FAILED: {ex}");
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Disable clipping on this control and walk up the tree
        this.ClipToBounds = false;

        var parent = this.Parent;
        while (parent != null)
        {
            if (parent is Control control)
            {
                control.ClipToBounds = false;
                Console.WriteLine($"[MainView] Set ClipToBounds=false on {control.GetType().Name}");
            }
            parent = (parent as Control)?.Parent;
        }

        // Also set on LeftNavigationPanel if found
        var leftNav = this.FindControl<LeftNavigationPanel>("LeftNavPanel");
        if (leftNav != null)
        {
            leftNav.ClipToBounds = false;
            Console.WriteLine("[MainView] Set ClipToBounds=false on LeftNavigationPanel");
        }
    }
}
