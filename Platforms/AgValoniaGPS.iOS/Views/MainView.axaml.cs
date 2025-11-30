using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private NumericKeypad? _numericKeypad;
    private Border? _keypadOverlay;
    private MainViewModel? _viewModel;
    private bool _editingLatitude;  // true = editing latitude, false = editing longitude

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
                _viewModel = App.Services.GetRequiredService<MainViewModel>();
                DataContext = _viewModel;
                Console.WriteLine("[MainView] DataContext set successfully.");

                // Get map control reference
                _mapControl = this.FindControl<SkiaMapControl>("MapControl");
                if (_mapControl != null)
                {
                    Console.WriteLine("[MainView] Map control found.");
                }

                // Get keypad references
                _numericKeypad = this.FindControl<NumericKeypad>("NumericKeypad");
                _keypadOverlay = this.FindControl<Border>("KeypadOverlay");

                // Wire up keypad events
                if (_numericKeypad != null)
                {
                    _numericKeypad.ValueEntered += OnKeypadValueEntered;
                    _numericKeypad.Cancelled += OnKeypadCancelled;
                }

                // Subscribe to ViewModel property changes to update map
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
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

    private void OnLatitudeClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _numericKeypad == null || _keypadOverlay == null)
            return;

        _editingLatitude = true;
        double.TryParse(_viewModel.SimCoordsLatitudeText, NumberStyles.Any, CultureInfo.InvariantCulture, out var currentValue);
        _numericKeypad.Initialize("Enter Latitude:", currentValue, maxDecimalPlaces: 7, allowNegative: true);
        _keypadOverlay.IsVisible = true;
    }

    private void OnLongitudeClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _numericKeypad == null || _keypadOverlay == null)
            return;

        _editingLatitude = false;
        double.TryParse(_viewModel.SimCoordsLongitudeText, NumberStyles.Any, CultureInfo.InvariantCulture, out var currentValue);
        _numericKeypad.Initialize("Enter Longitude:", currentValue, maxDecimalPlaces: 7, allowNegative: true);
        _keypadOverlay.IsVisible = true;
    }

    private void OnKeypadValueEntered(object? sender, double? value)
    {
        if (_viewModel == null || _keypadOverlay == null || !value.HasValue)
        {
            _keypadOverlay!.IsVisible = false;
            return;
        }

        if (_editingLatitude)
        {
            _viewModel.SimCoordsLatitudeText = value.Value.ToString("F7", CultureInfo.InvariantCulture);
        }
        else
        {
            _viewModel.SimCoordsLongitudeText = value.Value.ToString("F7", CultureInfo.InvariantCulture);
        }

        _keypadOverlay.IsVisible = false;
    }

    private void OnKeypadCancelled(object? sender, EventArgs e)
    {
        if (_keypadOverlay != null)
        {
            _keypadOverlay.IsVisible = false;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_mapControl == null || _viewModel == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Easting):
            case nameof(MainViewModel.Northing):
            case nameof(MainViewModel.Heading):
                // Update vehicle position on map
                double headingRadians = _viewModel.Heading * Math.PI / 180.0;
                _mapControl.SetVehiclePosition(_viewModel.Easting, _viewModel.Northing, headingRadians);

                // Also pan camera to follow vehicle
                _mapControl.PanTo(_viewModel.Easting, _viewModel.Northing);
                break;
        }
    }
}
