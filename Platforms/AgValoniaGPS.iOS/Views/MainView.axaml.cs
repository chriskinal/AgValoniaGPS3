using System;
using System.ComponentModel;
using Avalonia.Controls;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;

namespace AgValoniaGPS.iOS.Views;

/// <summary>
/// iOS MainView with ViewModel - wires up map control to ViewModel commands
/// </summary>
public partial class MainView : UserControl
{
    private DrawingContextMapControl? _mapControl;
    private MainViewModel? _viewModel;

    public MainView()
    {
        Console.WriteLine("[MainView] Constructor starting...");
        InitializeComponent();
        Console.WriteLine("[MainView] InitializeComponent completed.");

        // Get reference to map control
        _mapControl = this.FindControl<DrawingContextMapControl>("MapControl");
    }

    public MainView(MainViewModel viewModel) : this()
    {
        Console.WriteLine("[MainView] Setting DataContext to MainViewModel...");
        DataContext = viewModel;
        _viewModel = viewModel;

        // Wire up zoom commands to map control
        if (_mapControl != null)
        {
            viewModel.ZoomInRequested += () => _mapControl.Zoom(1.2);
            viewModel.ZoomOutRequested += () => _mapControl.Zoom(0.8);
        }

        // Wire up position updates - when ViewModel properties change, update map control
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Console.WriteLine("[MainView] DataContext set.");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update vehicle position when Easting, Northing, or Heading changes
        if (_mapControl != null && _viewModel != null)
        {
            if (e.PropertyName == nameof(MainViewModel.Easting) ||
                e.PropertyName == nameof(MainViewModel.Northing) ||
                e.PropertyName == nameof(MainViewModel.Heading))
            {
                // Convert heading from degrees to radians (ViewModel stores degrees, map expects radians)
                double headingRadians = _viewModel.Heading * Math.PI / 180.0;
                _mapControl.SetVehiclePosition(_viewModel.Easting, _viewModel.Northing, headingRadians);
            }
        }
    }
}
