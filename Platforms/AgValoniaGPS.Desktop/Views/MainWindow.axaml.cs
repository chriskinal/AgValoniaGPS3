using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using Microsoft.Extensions.DependencyInjection;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Desktop.Controls;

namespace AgValoniaGPS.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private IMapControl? MapControl;
    private bool _isDraggingSection = false;
    private bool _isDraggingLeftPanel = false;
    private bool _isDraggingFileMenu = false;
    private bool _isDraggingViewSettings = false;
    private bool _isDraggingTools = false;
    private bool _isDraggingConfiguration = false;
    private bool _isDraggingJobMenu = false;
    private bool _isDraggingFieldTools = false;
    private bool _isDraggingSimulator = false;
    private bool _isDraggingBoundary = false;
    private bool _isDraggingBoundaryPlayer = false;
    private Avalonia.Point _dragStartPoint;
    private DateTime _leftPanelPressTime;
    private const int TapTimeThresholdMs = 300;
    private const double TapDistanceThreshold = 5.0;

    public MainWindow()
    {
        InitializeComponent();

        // Create platform-specific map control
        CreateMapControl();

        // Set DataContext from DI
        if (App.Services != null)
        {
            DataContext = App.Services.GetRequiredService<MainViewModel>();
        }

        // Handle window resize to keep section control in bounds
        this.PropertyChanged += MainWindow_PropertyChanged;

        // Load window settings AFTER window is opened to avoid Avalonia overriding them
        this.Opened += MainWindow_Opened;

        // Subscribe to GPS position changes
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // Add keyboard shortcut for 3D mode toggle (F3)
        this.KeyDown += MainWindow_KeyDown;

        // Save window settings on close
        this.Closing += MainWindow_Closing;
    }

    private void CreateMapControl()
    {
        // Check if running on iOS/mobile platform - use SkiaMapControl
        // On desktop platforms, use OpenGLMapControl for better 3D support
        bool useSoftwareRenderer = OperatingSystem.IsIOS() || OperatingSystem.IsAndroid();

        Control mapControl;
        if (useSoftwareRenderer)
        {
            var skiaControl = new SkiaMapControl();
            MapControl = skiaControl;
            mapControl = skiaControl;
        }
        else
        {
            var glControl = new OpenGLMapControl();
            MapControl = glControl;
            mapControl = glControl;
        }

        // Set the map control as the content of the container
        MapControlContainer.Content = mapControl;

        // Apply initial grid visibility from ViewModel binding
        if (ViewModel != null)
        {
            MapControl.IsGridVisible = ViewModel.IsGridOn;
        }

        // Wire up the MapService with the MapControl
        if (App.Services != null && MapControl != null)
        {
            var mapService = App.Services.GetRequiredService<AgValoniaGPS.Desktop.Services.MapService>();
            mapService.SetMapControl(MapControl);
        }
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        // Load settings after window is opened
        LoadWindowSettings();
    }

    private void LoadWindowSettings()
    {
        if (App.Services == null) return;

        var settingsService = App.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Settings;

        // Apply window size and position
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        if (settings.WindowX >= 0 && settings.WindowY >= 0)
        {
            Position = new PixelPoint((int)settings.WindowX, (int)settings.WindowY);
        }

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        // Apply simulator panel position if saved
        if (SimulatorPanel != null && !double.IsNaN(settings.SimulatorPanelX) && !double.IsNaN(settings.SimulatorPanelY))
        {
            Canvas.SetLeft(SimulatorPanel, settings.SimulatorPanelX);
            Canvas.SetTop(SimulatorPanel, settings.SimulatorPanelY);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.Services == null) return;

        var settingsService = App.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Settings;

        // Save window state
        settings.WindowMaximized = WindowState == WindowState.Maximized;

        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowX = Position.X;
            settings.WindowY = Position.Y;
        }

        // Save simulator panel position
        if (SimulatorPanel != null)
        {
            settings.SimulatorPanelX = Canvas.GetLeft(SimulatorPanel);
            settings.SimulatorPanelY = Canvas.GetTop(SimulatorPanel);
            settings.SimulatorPanelVisible = SimulatorPanel.IsVisible;
        }

        // Save UI state
        if (ViewModel != null)
        {
            settings.SimulatorEnabled = ViewModel.IsSimulatorEnabled;
            settings.GridVisible = ViewModel.IsGridOn;
        }

        // Settings will be saved automatically by App.Exit handler
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3 && MapControl != null)
        {
            MapControl.Toggle3DMode();
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp && MapControl != null)
        {
            // Increase pitch (tilt camera up)
            MapControl.SetPitch(0.05);
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown && MapControl != null)
        {
            // Decrease pitch (tilt camera down)
            MapControl.SetPitch(-0.05);
            e.Handled = true;
        }
    }

    private void MainWindow_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(Bounds))
        {
            // Constrain section control to new window bounds
            if (SectionControlPanel != null)
            {
                double currentLeft = Canvas.GetLeft(SectionControlPanel);
                double currentTop = Canvas.GetTop(SectionControlPanel);

                if (double.IsNaN(currentLeft)) currentLeft = 900; // Default initial position
                if (double.IsNaN(currentTop)) currentTop = 600;

                double maxLeft = Bounds.Width - SectionControlPanel.Bounds.Width;
                double maxTop = Bounds.Height - SectionControlPanel.Bounds.Height;

                double newLeft = Math.Clamp(currentLeft, 0, Math.Max(0, maxLeft));
                double newTop = Math.Clamp(currentTop, 0, Math.Max(0, maxTop));

                Canvas.SetLeft(SectionControlPanel, newLeft);
                Canvas.SetTop(SectionControlPanel, newTop);
            }

            // Constrain left panel to new window bounds
            if (LeftNavigationPanel != null)
            {
                double currentLeft = Canvas.GetLeft(LeftNavigationPanel);
                double currentTop = Canvas.GetTop(LeftNavigationPanel);

                if (double.IsNaN(currentLeft)) currentLeft = 20; // Default initial position
                if (double.IsNaN(currentTop)) currentTop = 100;

                double maxLeft = Bounds.Width - LeftNavigationPanel.Bounds.Width;
                double maxTop = Bounds.Height - LeftNavigationPanel.Bounds.Height;

                double newLeft = Math.Clamp(currentLeft, 0, Math.Max(0, maxLeft));
                double newTop = Math.Clamp(currentTop, 0, Math.Max(0, maxTop));

                Canvas.SetLeft(LeftNavigationPanel, newLeft);
                Canvas.SetTop(LeftNavigationPanel, newTop);
            }
        }
    }

    // Removed: BtnNtripConnect_Click, BtnNtripDisconnect_Click, BtnDataIO_Click
    // These are now handled by ViewModel commands

    // Removed: BtnEnterSimCoords_Click, Btn3DToggle_Click
    // These are now handled by ViewModel commands (ShowSimCoordsDialogCommand, Toggle3DModeCommand)

    // Removed: BtnFields_Click, BtnNewField_Click, BtnOpenField_Click, BtnCloseField_Click, BtnFromExisting_Click, CopyFileIfExists
    // These are now handled by ViewModel commands via IDialogService

    // Removed: BtnIsoXml_Click, BtnKml_Click, BtnDriveIn_Click, BtnResumeField_Click
    // These are now handled by ViewModel commands via IDialogService

    // Drag functionality for Section Control
    private void SectionControl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border)
        {
            _isDraggingSection = true;
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(border);
        }
    }

    private void SectionControl_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingSection && sender is Border border)
        {
            var currentPoint = e.GetPosition(this);
            var delta = currentPoint - _dragStartPoint;

            // Calculate new position
            double newLeft = Canvas.GetLeft(border) + delta.X;
            double newTop = Canvas.GetTop(border) + delta.Y;

            // Constrain to window bounds
            double maxLeft = Bounds.Width - border.Bounds.Width;
            double maxTop = Bounds.Height - border.Bounds.Height;

            newLeft = Math.Clamp(newLeft, 0, maxLeft);
            newTop = Math.Clamp(newTop, 0, maxTop);

            // Update position
            Canvas.SetLeft(border, newLeft);
            Canvas.SetTop(border, newTop);

            _dragStartPoint = currentPoint;
        }
    }

    private void SectionControl_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingSection)
        {
            _isDraggingSection = false;
            if (sender is Border border)
            {
                e.Pointer.Capture(null);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Easting) ||
            e.PropertyName == nameof(MainViewModel.Northing) ||
            e.PropertyName == nameof(MainViewModel.Heading))
        {
            if (ViewModel != null && MapControl != null)
            {
                // Convert heading from degrees to radians
                double headingRadians = ViewModel.Heading * Math.PI / 180.0;
                MapControl.SetVehiclePosition(ViewModel.Easting, ViewModel.Northing, headingRadians);

                // Add boundary point if recording
                if (BoundaryRecordingService?.IsRecording == true)
                {
                    // Apply boundary offset
                    var (offsetEasting, offsetNorthing) = CalculateOffsetPosition(
                        ViewModel.Easting, ViewModel.Northing, headingRadians);
                    BoundaryRecordingService.AddPoint(offsetEasting, offsetNorthing, headingRadians);
                    UpdateBoundaryStatusDisplay();
                }
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsGridOn))
        {
            if (ViewModel != null && MapControl != null)
            {
                MapControl.SetGridVisible(ViewModel.IsGridOn);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.CameraPitch))
        {
            if (ViewModel != null && MapControl != null)
            {
                // Camera pitch from service is negative degrees (-90 to -10)
                // OpenGL expects positive radians (0 = overhead, PI/2 = horizontal)
                // So we negate the degrees and convert: -90° -> 0 rad, -10° -> ~1.4 rad
                double pitchRadians = -ViewModel.CameraPitch * Math.PI / 180.0;
                MapControl.SetPitchAbsolute(pitchRadians);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.Is2DMode))
        {
            if (ViewModel != null && MapControl != null)
            {
                // Is2DMode = true means 3D is off, so invert the value
                MapControl.Set3DMode(!ViewModel.Is2DMode);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsDayMode))
        {
            // Day/Night mode visual implementation not yet added to OpenGLMapControl
            // TODO: Implement theme switching (background color, grid color, etc.)
        }
        else if (e.PropertyName == nameof(MainViewModel.IsNorthUp))
        {
            // North-up rotation mode not yet implemented in OpenGLMapControl
            // TODO: Implement camera rotation locking to north
        }
        else if (e.PropertyName == nameof(MainViewModel.Brightness))
        {
            // Brightness control depends on platform-specific implementation
            // Currently marked as not supported in DisplaySettingsService
        }
    }

    // Map overlay event handlers that forward to MapControl
    private void MapOverlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Check if pointer is over any UI panel - if so, don't handle the event
        if (IsPointerOverUIPanel(e))
        {
            return; // Let the UI panel handle it
        }

        if (MapControl != null)
        {
            // Forward event to MapControl's internal handler
            var point = e.GetCurrentPoint(this);

            if (point.Properties.IsLeftButtonPressed)
            {
                MapControl.StartPan(point.Position);
                e.Handled = true;
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                MapControl.StartRotate(point.Position);
                e.Handled = true;
            }
        }
    }

    private void MapOverlay_PointerMoved(object? sender, PointerEventArgs e)
    {
        // Check if pointer is over any UI panel - if so, don't handle the event
        if (IsPointerOverUIPanel(e))
        {
            return; // Let the UI panel handle it
        }

        if (MapControl != null)
        {
            var point = e.GetCurrentPoint(this);
            MapControl.UpdateMouse(point.Position);
            e.Handled = true;
        }
    }

    private void MapOverlay_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Check if pointer is over any UI panel - if so, don't handle the event
        if (IsPointerOverUIPanel(e))
        {
            return; // Let the UI panel handle it
        }

        if (MapControl != null)
        {
            MapControl.EndPanRotate();
            e.Handled = true;
        }
    }

    private void MapOverlay_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Check if pointer is over any UI panel - if so, don't handle the event
        if (IsPointerOverUIPanel(e))
        {
            return; // Let the UI panel handle it
        }

        if (MapControl != null)
        {
            double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            MapControl.Zoom(zoomFactor);
            e.Handled = true;
        }
    }

    // Combined tap-to-rotate and hold-to-drag for Left Navigation Panel
    private void LeftPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Sender is the touch handle Grid
        if (LeftNavigationPanel != null && sender is Grid touchHandle)
        {
            _leftPanelPressTime = DateTime.Now;
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(touchHandle);

            // Close any open tooltips to prevent position issues during drag
            ToolTip.SetIsOpen(touchHandle, false);

            e.Handled = true; // Prevent map from handling this event
        }
    }

    private void LeftPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (LeftNavigationPanel != null && e.Pointer.Captured == sender && sender is Grid touchHandle)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Start dragging if moved beyond threshold
            if (!_isDraggingLeftPanel && distance > TapDistanceThreshold)
            {
                _isDraggingLeftPanel = true;
                // Ensure tooltip stays closed while dragging
                ToolTip.SetIsOpen(touchHandle, false);
            }

            if (_isDraggingLeftPanel)
            {
                var delta = currentPoint - _dragStartPoint;

                // Calculate new position
                double newLeft = Canvas.GetLeft(LeftNavigationPanel) + delta.X;
                double newTop = Canvas.GetTop(LeftNavigationPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - LeftNavigationPanel.Bounds.Width;
                double maxTop = Bounds.Height - LeftNavigationPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                // Update position
                Canvas.SetLeft(LeftNavigationPanel, newLeft);
                Canvas.SetTop(LeftNavigationPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void LeftPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (LeftNavigationPanel != null && e.Pointer.Captured == sender)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));
            var elapsed = (DateTime.Now - _leftPanelPressTime).TotalMilliseconds;

            // Detect tap: quick release with minimal movement
            bool isTap = !_isDraggingLeftPanel &&
                        elapsed < TapTimeThresholdMs &&
                        distance < TapDistanceThreshold;

            if (isTap)
            {
                // Tap detected - rotate the panel
                if (LeftPanelStack != null)
                {
                    if (LeftPanelStack.Orientation == Avalonia.Layout.Orientation.Vertical)
                    {
                        LeftPanelStack.Orientation = Avalonia.Layout.Orientation.Horizontal;
                    }
                    else
                    {
                        LeftPanelStack.Orientation = Avalonia.Layout.Orientation.Vertical;
                    }
                }
            }

            // Reset state
            _isDraggingLeftPanel = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }


    // Event blockers for Left Panel Border - prevent events from reaching map overlay
    // But DON'T block if the event comes from interactive children (buttons, drag handle, etc.)
    private void LeftPanelBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only block if event originated from the Border itself (not a child control)
        if (e.Source == sender)
        {
            e.Handled = true;
        }
    }

    private void LeftPanelBorder_PointerMoved(object? sender, PointerEventArgs e)
    {
        // Don't block - let children handle their own moves
    }

    private void LeftPanelBorder_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Only block if event originated from the Border itself (not a child control)
        if (e.Source == sender)
        {
            e.Handled = true;
        }
    }

    // File Menu Panel drag handlers
    private void FileMenuPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Sender is the header Grid
        if (FileMenuPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);

            // Close any open tooltips to prevent position issues during drag
            ToolTip.SetIsOpen(header, false);

            e.Handled = true;
        }
    }

    private void FileMenuPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (FileMenuPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Start dragging if moved beyond threshold
            if (!_isDraggingFileMenu && distance > TapDistanceThreshold)
            {
                _isDraggingFileMenu = true;
                // Ensure tooltip stays closed while dragging
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingFileMenu)
            {
                var delta = currentPoint - _dragStartPoint;

                // Calculate new position
                double newLeft = Canvas.GetLeft(FileMenuPanel) + delta.X;
                double newTop = Canvas.GetTop(FileMenuPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - FileMenuPanel.Bounds.Width;
                double maxTop = Bounds.Height - FileMenuPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                // Update position
                Canvas.SetLeft(FileMenuPanel, newLeft);
                Canvas.SetTop(FileMenuPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void FileMenuPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (FileMenuPanel != null && e.Pointer.Captured == sender)
        {
            // Reset state
            _isDraggingFileMenu = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    // View Settings Panel drag handlers
    private void ViewSettingsPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Sender is the header Grid
        if (ViewSettingsPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);

            // Close any open tooltips to prevent position issues during drag
            ToolTip.SetIsOpen(header, false);

            e.Handled = true;
        }
    }

    private void ViewSettingsPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (ViewSettingsPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Start dragging if moved beyond threshold
            if (!_isDraggingViewSettings && distance > TapDistanceThreshold)
            {
                _isDraggingViewSettings = true;
                // Ensure tooltip stays closed while dragging
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingViewSettings)
            {
                var delta = currentPoint - _dragStartPoint;

                // Calculate new position
                double newLeft = Canvas.GetLeft(ViewSettingsPanel) + delta.X;
                double newTop = Canvas.GetTop(ViewSettingsPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - ViewSettingsPanel.Bounds.Width;
                double maxTop = Bounds.Height - ViewSettingsPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                // Update position
                Canvas.SetLeft(ViewSettingsPanel, newLeft);
                Canvas.SetTop(ViewSettingsPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void ViewSettingsPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ViewSettingsPanel != null && e.Pointer.Captured == sender)
        {
            // Reset state
            _isDraggingViewSettings = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    // Tools Panel drag handlers
    private void ToolsPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Sender is the header Grid
        if (ToolsPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);

            // Close any open tooltips to prevent position issues during drag
            ToolTip.SetIsOpen(header, false);

            e.Handled = true;
        }
    }

    private void ToolsPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (ToolsPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Start dragging if moved beyond threshold
            if (!_isDraggingTools && distance > TapDistanceThreshold)
            {
                _isDraggingTools = true;
                // Ensure tooltip stays closed while dragging
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingTools)
            {
                var delta = currentPoint - _dragStartPoint;

                // Calculate new position
                double newLeft = Canvas.GetLeft(ToolsPanel) + delta.X;
                double newTop = Canvas.GetTop(ToolsPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - ToolsPanel.Bounds.Width;
                double maxTop = Bounds.Height - ToolsPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                // Update position
                Canvas.SetLeft(ToolsPanel, newLeft);
                Canvas.SetTop(ToolsPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void ToolsPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ToolsPanel != null && e.Pointer.Captured == sender)
        {
            // Reset state
            _isDraggingTools = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    // Configuration Panel drag handlers
    private void ConfigurationPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Sender is the header Grid
        if (ConfigurationPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);

            // Close any open tooltips to prevent position issues during drag
            ToolTip.SetIsOpen(header, false);

            e.Handled = true;
        }
    }

    private void ConfigurationPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (ConfigurationPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Start dragging if moved beyond threshold
            if (!_isDraggingConfiguration && distance > TapDistanceThreshold)
            {
                _isDraggingConfiguration = true;
                // Ensure tooltip stays closed while dragging
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingConfiguration)
            {
                var delta = currentPoint - _dragStartPoint;

                // Calculate new position
                double newLeft = Canvas.GetLeft(ConfigurationPanel) + delta.X;
                double newTop = Canvas.GetTop(ConfigurationPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - ConfigurationPanel.Bounds.Width;
                double maxTop = Bounds.Height - ConfigurationPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                // Update position
                Canvas.SetLeft(ConfigurationPanel, newLeft);
                Canvas.SetTop(ConfigurationPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void ConfigurationPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ConfigurationPanel != null && e.Pointer.Captured == sender)
        {
            // Reset state
            _isDraggingConfiguration = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    // Job Menu Panel drag handlers
    private void JobMenuPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (JobMenuPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);
            // Suppress tooltip to prevent it from following during drag
            ToolTip.SetIsOpen(header, false);
            e.Handled = true;
        }
    }

    private void JobMenuPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (JobMenuPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Only start dragging if moved beyond threshold
            if (!_isDraggingJobMenu && distance > TapDistanceThreshold)
            {
                _isDraggingJobMenu = true;
                // Suppress tooltip when dragging starts
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingJobMenu)
            {
                var delta = currentPoint - _dragStartPoint;

                double newLeft = Canvas.GetLeft(JobMenuPanel) + delta.X;
                double newTop = Canvas.GetTop(JobMenuPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - JobMenuPanel.Bounds.Width;
                double maxTop = Bounds.Height - JobMenuPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                Canvas.SetLeft(JobMenuPanel, newLeft);
                Canvas.SetTop(JobMenuPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void JobMenuPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (JobMenuPanel != null && e.Pointer.Captured == sender)
        {
            // Reset state
            _isDraggingJobMenu = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    // Field Tools Panel drag handlers
    private void FieldToolsPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (FieldToolsPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);
            // Suppress tooltip to prevent it from following during drag
            ToolTip.SetIsOpen(header, false);
            e.Handled = true;
        }
    }

    private void FieldToolsPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (FieldToolsPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Only start dragging if moved beyond threshold
            if (!_isDraggingFieldTools && distance > TapDistanceThreshold)
            {
                _isDraggingFieldTools = true;
                // Suppress tooltip when dragging starts
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingFieldTools)
            {
                var delta = currentPoint - _dragStartPoint;

                double newLeft = Canvas.GetLeft(FieldToolsPanel) + delta.X;
                double newTop = Canvas.GetTop(FieldToolsPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - FieldToolsPanel.Bounds.Width;
                double maxTop = Bounds.Height - FieldToolsPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                Canvas.SetLeft(FieldToolsPanel, newLeft);
                Canvas.SetTop(FieldToolsPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void FieldToolsPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (FieldToolsPanel != null && e.Pointer.Captured == sender)
        {
            // Reset state
            _isDraggingFieldTools = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    // Simulator Panel drag handlers
    private void SimulatorPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (SimulatorPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);
            // Suppress tooltip to prevent it from following during drag
            ToolTip.SetIsOpen(header, false);
            e.Handled = true;
        }
    }

    private void SimulatorPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (SimulatorPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Only start dragging if moved beyond threshold
            if (!_isDraggingSimulator && distance > TapDistanceThreshold)
            {
                _isDraggingSimulator = true;
                // Suppress tooltip when dragging starts
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingSimulator)
            {
                var delta = currentPoint - _dragStartPoint;

                double newLeft = Canvas.GetLeft(SimulatorPanel) + delta.X;
                double newTop = Canvas.GetTop(SimulatorPanel) + delta.Y;

                // Constrain to window bounds
                double maxLeft = Bounds.Width - SimulatorPanel.Bounds.Width;
                double maxTop = Bounds.Height - SimulatorPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                Canvas.SetLeft(SimulatorPanel, newLeft);
                Canvas.SetTop(SimulatorPanel, newTop);

                _dragStartPoint = currentPoint;
                e.Handled = true;
            }
        }
    }

    private void SimulatorPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (SimulatorPanel != null)
        {
            // Always release capture and reset state, regardless of whether we were dragging
            _isDraggingSimulator = false;
            if (e.Pointer.Captured == sender)
            {
                e.Pointer.Capture(null);
            }
            e.Handled = true;
        }
    }

    // Helper method to check if pointer is over any UI panel
    private bool IsPointerOverUIPanel(PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        // Check left navigation panel
        if (LeftNavigationPanel != null && LeftNavigationPanel.IsVisible && LeftNavigationPanel.Bounds.Width > 0 && LeftNavigationPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(LeftNavigationPanel);
            double top = Canvas.GetTop(LeftNavigationPanel);

            if (double.IsNaN(left)) left = 20;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, LeftNavigationPanel.Bounds.Width, LeftNavigationPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check section control panel
        if (SectionControlPanel != null && SectionControlPanel.IsVisible && SectionControlPanel.Bounds.Width > 0 && SectionControlPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(SectionControlPanel);
            double top = Canvas.GetTop(SectionControlPanel);

            if (double.IsNaN(left)) left = 900;
            if (double.IsNaN(top)) top = 600;

            var panelBounds = new Rect(left, top, SectionControlPanel.Bounds.Width, SectionControlPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check file menu panel
        if (FileMenuPanel != null && FileMenuPanel.IsVisible && FileMenuPanel.Bounds.Width > 0 && FileMenuPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(FileMenuPanel);
            double top = Canvas.GetTop(FileMenuPanel);

            if (double.IsNaN(left)) left = 90;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, FileMenuPanel.Bounds.Width, FileMenuPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check view settings panel
        if (ViewSettingsPanel != null && ViewSettingsPanel.IsVisible && ViewSettingsPanel.Bounds.Width > 0 && ViewSettingsPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(ViewSettingsPanel);
            double top = Canvas.GetTop(ViewSettingsPanel);

            if (double.IsNaN(left)) left = 90;
            if (double.IsNaN(top)) top = 200;

            var panelBounds = new Rect(left, top, ViewSettingsPanel.Bounds.Width, ViewSettingsPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check tools panel
        if (ToolsPanel != null && ToolsPanel.IsVisible && ToolsPanel.Bounds.Width > 0 && ToolsPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(ToolsPanel);
            double top = Canvas.GetTop(ToolsPanel);

            if (double.IsNaN(left)) left = 90;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, ToolsPanel.Bounds.Width, ToolsPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check configuration panel
        if (ConfigurationPanel != null && ConfigurationPanel.IsVisible && ConfigurationPanel.Bounds.Width > 0 && ConfigurationPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(ConfigurationPanel);
            double top = Canvas.GetTop(ConfigurationPanel);

            if (double.IsNaN(left)) left = 90;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, ConfigurationPanel.Bounds.Width, ConfigurationPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check job menu panel
        if (JobMenuPanel != null && JobMenuPanel.IsVisible && JobMenuPanel.Bounds.Width > 0 && JobMenuPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(JobMenuPanel);
            double top = Canvas.GetTop(JobMenuPanel);

            if (double.IsNaN(left)) left = 90;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, JobMenuPanel.Bounds.Width, JobMenuPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check field tools panel
        if (FieldToolsPanel != null && FieldToolsPanel.IsVisible && FieldToolsPanel.Bounds.Width > 0 && FieldToolsPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(FieldToolsPanel);
            double top = Canvas.GetTop(FieldToolsPanel);

            if (double.IsNaN(left)) left = 90;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, FieldToolsPanel.Bounds.Width, FieldToolsPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check simulator panel
        if (SimulatorPanel != null && SimulatorPanel.IsVisible && SimulatorPanel.Bounds.Width > 0 && SimulatorPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(SimulatorPanel);
            double top = Canvas.GetTop(SimulatorPanel);

            if (double.IsNaN(left)) left = 400;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, SimulatorPanel.Bounds.Width, SimulatorPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        // Check boundary recording panel
        if (BoundaryRecordingPanel != null && BoundaryRecordingPanel.IsVisible && BoundaryRecordingPanel.Bounds.Width > 0 && BoundaryRecordingPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(BoundaryRecordingPanel);
            double top = Canvas.GetTop(BoundaryRecordingPanel);

            if (double.IsNaN(left)) left = 200;
            if (double.IsNaN(top)) top = 150;

            var panelBounds = new Rect(left, top, BoundaryRecordingPanel.Bounds.Width, BoundaryRecordingPanel.Bounds.Height);

            if (panelBounds.Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    // AgShare Settings button click
    // Removed: BtnAgShareSettings_Click, BtnAgShareDownload_Click, BtnAgShareUpload_Click
    // These are now handled by ViewModel commands via IDialogService

    // ========== Boundary Recording Panel Handlers ==========

    private IBoundaryRecordingService? _boundaryRecordingService;
    private BoundaryType _currentBoundaryType = BoundaryType.Outer;

    private IBoundaryRecordingService BoundaryRecordingService
    {
        get
        {
            if (_boundaryRecordingService == null && App.Services != null)
            {
                _boundaryRecordingService = App.Services.GetRequiredService<IBoundaryRecordingService>();
                // Subscribe to events for live boundary display
                _boundaryRecordingService.PointAdded += BoundaryRecordingService_PointAdded;
                _boundaryRecordingService.StateChanged += BoundaryRecordingService_StateChanged;
            }
            return _boundaryRecordingService!;
        }
    }

    private void BoundaryRecordingService_PointAdded(object? sender, BoundaryPointAddedEventArgs e)
    {
        // Update the map display with the current recording points
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateRecordingDisplay();
            // Display updated via ViewModel bindings
        });
    }

    private void BoundaryRecordingService_StateChanged(object? sender, BoundaryRecordingStateChangedEventArgs e)
    {
        // Update the map display when state changes (includes point removal, clear, etc.)
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (e.State == BoundaryRecordingState.Idle)
            {
                MapControl?.ClearRecordingPoints();
            }
            else
            {
                // Update display for point count changes (e.g., when points are deleted)
                UpdateRecordingDisplay();
            }
        });
    }

    private void UpdateRecordingDisplay()
    {
        if (MapControl == null || BoundaryRecordingService == null) return;

        var points = BoundaryRecordingService.RecordedPoints;
        if (points.Count > 0)
        {
            var pointsList = points.Select(p => (p.Easting, p.Northing)).ToList();
            MapControl.SetRecordingPoints(pointsList);
        }
        else
        {
            MapControl.ClearRecordingPoints();
        }
    }

    // Add new boundary button - shows choice panel (kept for AddBoundaryChoicePanel)
    private void BtnAddBoundary_Click(object? sender, RoutedEventArgs e)
    {
        // Show the Add Boundary Choice panel
        var choicePanel = this.FindControl<Border>("AddBoundaryChoicePanel");
        if (choicePanel != null)
        {
            choicePanel.IsVisible = true;
        }
    }

    // Import KML button - open file dialog to import KML boundary
    private void BtnImportKml_Click(object? sender, RoutedEventArgs e)
    {
        // Hide the choice panel
        var choicePanel = this.FindControl<Border>("AddBoundaryChoicePanel");
        if (choicePanel != null)
        {
            choicePanel.IsVisible = false;
        }

        if (ViewModel != null)
        {
            ViewModel.StatusMessage = "KML import not yet implemented";
        }
        // TODO: Implement KML file import
    }

    // Drive/Record button - show BoundaryPlayerPanel
    private void BtnDriveRecord_Click(object? sender, RoutedEventArgs e)
    {
        // Hide the choice panel
        var choicePanel = this.FindControl<Border>("AddBoundaryChoicePanel");
        if (choicePanel != null)
        {
            choicePanel.IsVisible = false;
        }

        // Hide the main boundary panel
        if (ViewModel != null)
        {
            ViewModel.IsBoundaryPanelVisible = false;
        }

        // Show the BoundaryPlayerPanel via ViewModel
        if (ViewModel != null)
        {
            ViewModel.IsBoundaryPlayerPanelVisible = true;
            ViewModel.StatusMessage = "Boundary recording ready - Click Record (R) to start";
        }
    }

    // Cancel add boundary choice
    private void BtnCancelAddBoundary_Click(object? sender, RoutedEventArgs e)
    {
        // Hide the choice panel
        var choicePanel = this.FindControl<Border>("AddBoundaryChoicePanel");
        if (choicePanel != null)
        {
            choicePanel.IsVisible = false;
        }
    }

    // Close boundary panel
    private void BtnCloseBoundaryPanel_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsBoundaryPanelVisible = false;
        }
    }

    // Select outer boundary type
    private void BtnOuterBoundary_Click(object? sender, RoutedEventArgs e)
    {
        _currentBoundaryType = BoundaryType.Outer;
        if (ViewModel != null)
        {
            ViewModel.StatusMessage = "Outer boundary selected";
        }
    }

    // Select inner boundary type
    private void BtnInnerBoundary_Click(object? sender, RoutedEventArgs e)
    {
        _currentBoundaryType = BoundaryType.Inner;
        if (ViewModel != null)
        {
            ViewModel.StatusMessage = "Inner boundary selected";
        }
    }

    // Determine which boundary type is selected
    private BoundaryType GetSelectedBoundaryType()
    {
        return _currentBoundaryType;
    }

    // Start/Resume recording boundary
    private void BtnRecordBoundary_Click(object? sender, RoutedEventArgs e)
    {
        if (BoundaryRecordingService == null) return;

        var state = BoundaryRecordingService.State;
        if (state == BoundaryRecordingState.Idle)
        {
            // Start new recording
            var boundaryType = GetSelectedBoundaryType();
            BoundaryRecordingService.StartRecording(boundaryType);
            UpdateBoundaryStatusDisplay();

            // Show recording status panel
            var recordingStatusPanel = this.FindControl<Border>("RecordingStatusPanel");
            var recordingControlsPanel = this.FindControl<Grid>("RecordingControlsPanel");
            if (recordingStatusPanel != null) recordingStatusPanel.IsVisible = true;
            if (recordingControlsPanel != null) recordingControlsPanel.IsVisible = true;

            // Update accept button
            var acceptBtn = this.FindControl<Button>("BtnAcceptBoundary");
            if (acceptBtn != null) acceptBtn.IsEnabled = true;

            if (ViewModel != null)
            {
                ViewModel.StatusMessage = $"Recording {boundaryType} boundary - drive around the perimeter";
            }
        }
        else if (state == BoundaryRecordingState.Paused)
        {
            // Resume recording
            BoundaryRecordingService.ResumeRecording();
            UpdateBoundaryStatusDisplay();
            if (ViewModel != null)
            {
                ViewModel.StatusMessage = "Resumed boundary recording";
            }
        }
    }

    // Pause recording
    private void BtnPauseBoundary_Click(object? sender, RoutedEventArgs e)
    {
        if (BoundaryRecordingService == null) return;

        if (BoundaryRecordingService.State == BoundaryRecordingState.Recording)
        {
            BoundaryRecordingService.PauseRecording();
            UpdateBoundaryStatusDisplay();
            if (ViewModel != null)
            {
                ViewModel.StatusMessage = "Boundary recording paused";
            }
        }
    }

    // Stop recording and save boundary
    private void BtnStopBoundary_Click(object? sender, RoutedEventArgs e)
    {
        if (BoundaryRecordingService == null || App.Services == null || ViewModel == null) return;

        if (BoundaryRecordingService.State != BoundaryRecordingState.Idle)
        {
            // Get the current boundary type before stopping
            var isOuter = BoundaryRecordingService.CurrentBoundaryType == BoundaryType.Outer;
            var polygon = BoundaryRecordingService.StopRecording();

            if (polygon != null && polygon.Points.Count >= 3)
            {
                // Save the boundary to the current field
                var settingsService = App.Services.GetRequiredService<ISettingsService>();
                var boundaryFileService = App.Services.GetRequiredService<BoundaryFileService>();

                if (!string.IsNullOrEmpty(ViewModel.CurrentFieldName))
                {
                    var fieldPath = Path.Combine(settingsService.Settings.FieldsDirectory, ViewModel.CurrentFieldName);

                    // Load existing boundary or create new one
                    var boundary = boundaryFileService.LoadBoundary(fieldPath) ?? new Models.Boundary();

                    if (isOuter)
                    {
                        boundary.OuterBoundary = polygon;
                    }
                    else
                    {
                        boundary.InnerBoundaries.Add(polygon);
                    }

                    // Save boundary
                    boundaryFileService.SaveBoundary(boundary, fieldPath);

                    // Update map display
                    if (MapControl != null)
                    {
                        MapControl.SetBoundary(boundary);
                    }

                    ViewModel.StatusMessage = $"Saved {(isOuter ? "outer" : "inner")} boundary ({polygon.Points.Count} points, {polygon.AreaHectares:F2} ha)";

                    // Refresh the boundary list
                    ViewModel?.RefreshBoundaryList();
                }
                else
                {
                    ViewModel.StatusMessage = "No field open - boundary not saved";
                }
            }
            else
            {
                ViewModel.StatusMessage = "Boundary cancelled (not enough points)";
            }

            // Hide recording panels
            var recordingStatusPanel = this.FindControl<Border>("RecordingStatusPanel");
            var recordingControlsPanel = this.FindControl<Grid>("RecordingControlsPanel");
            if (recordingStatusPanel != null) recordingStatusPanel.IsVisible = false;
            if (recordingControlsPanel != null) recordingControlsPanel.IsVisible = false;

            // Update accept button
            var acceptBtn = this.FindControl<Button>("BtnAcceptBoundary");
            if (acceptBtn != null) acceptBtn.IsEnabled = false;

            UpdateBoundaryStatusDisplay();
        }
    }

    // Undo last boundary point
    private void BtnUndoBoundaryPoint_Click(object? sender, RoutedEventArgs e)
    {
        if (BoundaryRecordingService == null) return;

        if (BoundaryRecordingService.RemoveLastPoint())
        {
            UpdateBoundaryStatusDisplay();
            UpdateRecordingDisplay(); // Update map display
            if (ViewModel != null)
            {
                ViewModel.StatusMessage = $"Removed point ({BoundaryRecordingService.PointCount} remaining)";
            }
        }
    }

    // Clear all boundary points
    private void BtnClearBoundary_Click(object? sender, RoutedEventArgs e)
    {
        if (BoundaryRecordingService == null) return;

        BoundaryRecordingService.ClearPoints();
        UpdateBoundaryStatusDisplay();
        if (ViewModel != null)
        {
            ViewModel.StatusMessage = "Boundary points cleared";
        }
    }

    // Update the boundary status display text blocks
    private void UpdateBoundaryStatusDisplay()
    {
        if (BoundaryRecordingService == null) return;

        var statusText = this.FindControl<TextBlock>("BoundaryStatusLabel");
        var pointsText = this.FindControl<TextBlock>("BoundaryPointsLabel");
        var areaText = this.FindControl<TextBlock>("BoundaryAreaLabel");

        if (statusText != null)
        {
            statusText.Text = BoundaryRecordingService.State.ToString();
        }

        if (pointsText != null)
        {
            pointsText.Text = BoundaryRecordingService.PointCount.ToString();
        }

        if (areaText != null)
        {
            areaText.Text = $"{BoundaryRecordingService.AreaHectares:F2} Ha";
        }

        // Update button enabled states
        var recordBtn = this.FindControl<Button>("BtnRecordBoundary");
        var pauseBtn = this.FindControl<Button>("BtnPauseBoundary");
        var stopBtn = this.FindControl<Button>("BtnStopBoundary");

        var state = BoundaryRecordingService.State;
        if (recordBtn != null)
        {
            recordBtn.IsEnabled = state == BoundaryRecordingState.Idle || state == BoundaryRecordingState.Paused;
        }
        if (pauseBtn != null)
        {
            pauseBtn.IsEnabled = state == BoundaryRecordingState.Recording;
        }
        if (stopBtn != null)
        {
            stopBtn.IsEnabled = state != BoundaryRecordingState.Idle;
        }
    }

    // Boundary Panel drag handlers
    private void BoundaryPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (BoundaryRecordingPanel != null && sender is Grid header)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(header);
            ToolTip.SetIsOpen(header, false);
            e.Handled = true;
        }
    }

    private void BoundaryPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (BoundaryRecordingPanel != null && e.Pointer.Captured == sender && sender is Grid header)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            if (!_isDraggingBoundary && distance > TapDistanceThreshold)
            {
                _isDraggingBoundary = true;
                ToolTip.SetIsOpen(header, false);
            }

            if (_isDraggingBoundary)
            {
                var delta = currentPoint - _dragStartPoint;

                double newLeft = Canvas.GetLeft(BoundaryRecordingPanel) + delta.X;
                double newTop = Canvas.GetTop(BoundaryRecordingPanel) + delta.Y;

                double maxLeft = Bounds.Width - BoundaryRecordingPanel.Bounds.Width;
                double maxTop = Bounds.Height - BoundaryRecordingPanel.Bounds.Height;

                newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
                newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

                Canvas.SetLeft(BoundaryRecordingPanel, newLeft);
                Canvas.SetTop(BoundaryRecordingPanel, newTop);

                _dragStartPoint = currentPoint;
            }

            e.Handled = true;
        }
    }

    private void BoundaryPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (BoundaryRecordingPanel != null && e.Pointer.Captured == sender)
        {
            _isDraggingBoundary = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    #region BoundaryPlayerPanel Event Handlers

    // Drag support for BoundaryPlayerPanel
    private void BoundaryPlayerPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (BoundaryPlayerPanel != null)
        {
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture((Control)sender!);
            e.Handled = true;
        }
    }

    private void BoundaryPlayerPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (BoundaryPlayerPanel != null && e.Pointer.Captured == sender)
        {
            var currentPoint = e.GetPosition(this);
            double distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) + Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            if (!_isDraggingBoundaryPlayer && distance > TapDistanceThreshold)
            {
                _isDraggingBoundaryPlayer = true;
            }

            if (_isDraggingBoundaryPlayer)
            {
                var offset = currentPoint - _dragStartPoint;
                double currentLeft = Canvas.GetLeft(BoundaryPlayerPanel);
                double currentTop = Canvas.GetTop(BoundaryPlayerPanel);

                if (double.IsNaN(currentLeft)) currentLeft = 100;
                if (double.IsNaN(currentTop)) currentTop = 100;

                double newLeft = Math.Max(0, Math.Min(currentLeft + offset.X, Bounds.Width - BoundaryPlayerPanel.Bounds.Width));
                double newTop = Math.Max(0, Math.Min(currentTop + offset.Y, Bounds.Height - BoundaryPlayerPanel.Bounds.Height));

                Canvas.SetLeft(BoundaryPlayerPanel, newLeft);
                Canvas.SetTop(BoundaryPlayerPanel, newTop);
                _dragStartPoint = currentPoint;
            }
            e.Handled = true;
        }
    }

    private void BoundaryPlayerPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (BoundaryPlayerPanel != null && e.Pointer.Captured == sender)
        {
            _isDraggingBoundaryPlayer = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }


    // BtnBoundaryRestart now handled by ViewModel's ClearBoundaryCommand
    // BtnBoundaryOffset now handled by ViewModel's ShowBoundaryOffsetDialogCommand
    // BtnBoundarySectionControl now handled by ViewModel's IsBoundarySectionControlOn binding
    // BtnBoundaryLeftRight now handled by ViewModel's ToggleBoundaryLeftRightCommand
    // BtnBoundaryAntennaTool now handled by ViewModel's ToggleBoundaryAntennaToolCommand
    // BtnBoundaryDeleteLast now handled by ViewModel's UndoBoundaryPointCommand

    // Calculate offset position perpendicular to heading
    // Returns (easting, northing) with offset applied
    private (double easting, double northing) CalculateOffsetPosition(double easting, double northing, double headingRadians)
    {
        var boundaryOffset = ViewModel?.BoundaryOffset ?? 0;
        if (boundaryOffset == 0)
            return (easting, northing);

        // Offset in meters (input is cm)
        double offsetMeters = boundaryOffset / 100.0;

        // If drawing on left side, negate the offset
        if (ViewModel != null && !ViewModel.IsDrawRightSide)
            offsetMeters = -offsetMeters;

        // Calculate perpendicular offset (90 degrees to the right of heading)
        // Right is +90 degrees (π/2 radians) from heading direction
        double perpAngle = headingRadians + Math.PI / 2.0;

        double offsetEasting = easting + offsetMeters * Math.Sin(perpAngle);
        double offsetNorthing = northing + offsetMeters * Math.Cos(perpAngle);

        return (offsetEasting, offsetNorthing);
    }

    // BtnBoundaryAddPoint now handled by ViewModel's AddBoundaryPointCommand

    #endregion
}