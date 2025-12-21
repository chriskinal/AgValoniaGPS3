using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Behaviors;
using AgValoniaGPS.Android.Services;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Android.Views;

/// <summary>
/// Android MainView with ViewModel - wires up map control to ViewModel commands
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

    public MainView(MainViewModel viewModel, MapService mapService) : this()
    {
        Console.WriteLine("[MainView] Setting DataContext to MainViewModel...");
        DataContext = viewModel;
        _viewModel = viewModel;

        // Register the map control with the MapService so it can receive commands
        if (_mapControl != null)
        {
            mapService.RegisterMapControl(_mapControl);
            Console.WriteLine("[MainView] MapControl registered with MapService.");

            viewModel.ZoomInRequested += () => _mapControl.Zoom(1.2);
            viewModel.ZoomOutRequested += () => _mapControl.Zoom(0.8);

            // Wire up MapClicked event for AB line creation
            _mapControl.MapClicked += OnMapClicked;
        }

        // Wire up position updates - when ViewModel properties change, update map control
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Subscribe to track collection changes to update active track display
        viewModel.SavedTracks.CollectionChanged += SavedTracks_CollectionChanged;

        // Update active track immediately in case field was already loaded
        UpdateActiveTrack();

        // Subscribe to FPS updates from map control
        DrawingContextMapControl.FpsUpdated += fps =>
        {
            if (viewModel != null)
                viewModel.CurrentFps = fps;
        };

        Console.WriteLine("[MainView] DataContext set.");
    }

    private void OnMapClicked(object? sender, MapClickEventArgs e)
    {
        if (_viewModel == null) return;

        // For DriveAB mode, we use current GPS position (not the clicked position)
        // For DrawAB mode, we use the clicked map position
        if (_viewModel.CurrentABCreationMode == ABCreationMode.DriveAB)
        {
            // In DriveAB mode, any tap triggers setting the point at current GPS position
            _viewModel.SetABPointCommand?.Execute(null);
        }
        else if (_viewModel.CurrentABCreationMode == ABCreationMode.DrawAB)
        {
            // In DrawAB mode, pass the clicked map coordinates
            var mapPosition = new Position
            {
                Easting = e.Easting,
                Northing = e.Northing
            };
            _viewModel.SetABPointCommand?.Execute(mapPosition);
        }
    }

    private void SavedTracks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // When a new track is added, show the most recently active one
        UpdateActiveTrack();
    }

    private void UpdateActiveTrack()
    {
        if (_mapControl != null && _viewModel != null)
        {
            // Find the active track (or the most recently added one)
            var activeTrack = _viewModel.SavedTracks.FirstOrDefault(t => t.IsActive)
                          ?? _viewModel.SavedTracks.LastOrDefault();
            _mapControl.SetActiveTrack(activeTrack);
        }
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
            else if (e.PropertyName == nameof(MainViewModel.EnableABClickSelection))
            {
                // Update map control click selection mode
                _mapControl.EnableClickSelection = _viewModel.EnableABClickSelection;
            }
        }
    }

    // Section Control drag handlers - use shared DragBehavior
    private void SectionControl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
            DragBehavior.OnPointerPressed(control, e);
    }

    private void SectionControl_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Control control)
            DragBehavior.OnPointerMoved(control, this, e);
    }

    private void SectionControl_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        DragBehavior.OnPointerReleased(e);
    }

    // Bottom Navigation Panel drag handlers - use shared DragBehavior
    private void BottomNavPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
            DragBehavior.OnPointerPressed(control, e);
    }

    private void BottomNavPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Control control)
            DragBehavior.OnPointerMoved(control, this, e);
    }

    private void BottomNavPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        DragBehavior.OnPointerReleased(e);
    }
}
