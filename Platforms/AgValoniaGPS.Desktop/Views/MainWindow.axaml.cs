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
using AgValoniaGPS.Desktop.Controls.Panels;

namespace AgValoniaGPS.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private IMapControl? MapControl;
    private bool _isDraggingSection = false;
    private Avalonia.Point _dragStartPoint;
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

        // Wire up shared LeftNavigationPanel drag events
        // All sub-panels are now children of LeftNavPanel, so only one drag handler is needed
        if (LeftNavPanel != null)
        {
            LeftNavPanel.DragMoved += LeftNavPanel_DragMoved;
        }
    }

    private void MovePanel(Control panel, Vector delta)
    {
        double newLeft = Canvas.GetLeft(panel) + delta.X;
        double newTop = Canvas.GetTop(panel) + delta.Y;
        double maxLeft = Bounds.Width - panel.Bounds.Width;
        double maxTop = Bounds.Height - panel.Bounds.Height;
        newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
        newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));
        Canvas.SetLeft(panel, newLeft);
        Canvas.SetTop(panel, newTop);
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

        // Note: SimulatorPanel is now a child of LeftNavigationPanel
        // Panel positions are managed by the shared controls themselves
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

        // Note: SimulatorPanel is now a child of LeftNavigationPanel
        // Panel positions are managed by the shared controls

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
            if (LeftNavPanel != null)
            {
                double currentLeft = Canvas.GetLeft(LeftNavPanel);
                double currentTop = Canvas.GetTop(LeftNavPanel);

                if (double.IsNaN(currentLeft)) currentLeft = 20; // Default initial position
                if (double.IsNaN(currentTop)) currentTop = 100;

                double maxLeft = Bounds.Width - LeftNavPanel.Bounds.Width;
                double maxTop = Bounds.Height - LeftNavPanel.Bounds.Height;

                double newLeft = Math.Clamp(currentLeft, 0, Math.Max(0, maxLeft));
                double newTop = Math.Clamp(currentTop, 0, Math.Max(0, maxTop));

                Canvas.SetLeft(LeftNavPanel, newLeft);
                Canvas.SetTop(LeftNavPanel, newTop);
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

    // Handler for shared LeftNavigationPanel drag events
    private void LeftNavPanel_DragMoved(object? sender, Vector delta)
    {
        if (LeftNavPanel == null) return;

        // Calculate new position
        double currentLeft = Canvas.GetLeft(LeftNavPanel);
        double currentTop = Canvas.GetTop(LeftNavPanel);

        if (double.IsNaN(currentLeft)) currentLeft = 20;
        if (double.IsNaN(currentTop)) currentTop = 100;

        double newLeft = currentLeft + delta.X;
        double newTop = currentTop + delta.Y;

        // Constrain to window bounds
        double maxLeft = Bounds.Width - LeftNavPanel.Bounds.Width;
        double maxTop = Bounds.Height - LeftNavPanel.Bounds.Height;

        newLeft = Math.Clamp(newLeft, 0, Math.Max(0, maxLeft));
        newTop = Math.Clamp(newTop, 0, Math.Max(0, maxTop));

        // Update position
        Canvas.SetLeft(LeftNavPanel, newLeft);
        Canvas.SetTop(LeftNavPanel, newTop);
    }

    // NOTE: All panel drag handlers are now handled by shared controls
    // via DragMoved events wired up in constructor

    // Helper method to check if pointer is over any UI panel
    private bool IsPointerOverUIPanel(PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        // Check left navigation panel
        if (LeftNavPanel != null && LeftNavPanel.IsVisible && LeftNavPanel.Bounds.Width > 0 && LeftNavPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(LeftNavPanel);
            double top = Canvas.GetTop(LeftNavPanel);

            if (double.IsNaN(left)) left = 20;
            if (double.IsNaN(top)) top = 100;

            var panelBounds = new Rect(left, top, LeftNavPanel.Bounds.Width, LeftNavPanel.Bounds.Height);

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

        // NOTE: All sub-panels (FileMenu, ViewSettings, Tools, Configuration,
        // JobMenu, FieldTools, Simulator, BoundaryRecording, BoundaryPlayer)
        // are now children of LeftNavPanel, so checking LeftNavPanel bounds
        // above is sufficient to cover all of them.

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

    // NOTE: BoundaryRecordingPanel and BoundaryPlayerPanel drag handlers removed.
    // These panels are now children of LeftNavigationPanel (shared control) and
    // handle their own dragging internally.

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
}