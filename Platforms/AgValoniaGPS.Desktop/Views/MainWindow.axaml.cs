// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Controls.Panels;
using AgValoniaGPS.Views.Behaviors;

namespace AgValoniaGPS.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private ISharedMapControl? MapControl;
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
            ViewModel.SavedTracks.CollectionChanged += SavedTracks_CollectionChanged;
        }

        // Subscribe to FPS updates from map control
        DrawingContextMapControl.FpsUpdated += fps =>
        {
            if (ViewModel != null)
                ViewModel.CurrentFps = fps;
        };

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

        // Wire up RightNavigationPanel drag events
        if (RightNavPanel != null)
        {
            RightNavPanel.DragMoved += RightNavPanel_DragMoved;
        }

        // Note: BottomNavigationPanel is now a fixed-position panel without drag support
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
        // Use the shared DrawingContextMapControl (cross-platform)
        var mapControl = new DrawingContextMapControl();
        MapControl = mapControl;
        Console.WriteLine("Using DrawingContextMapControl (cross-platform)");

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

            // Wire up coverage updates
            var coverageService = App.Services.GetRequiredService<ICoverageMapService>();
            coverageService.CoverageUpdated += (sender, args) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    MapControl?.MarkCoverageDirty();
                    ViewModel?.RefreshCoverageStatistics();
                });
            };

            // Set up bitmap-based coverage rendering (PERF-004)
            // allCellsProvider takes viewport bounds for spatial queries - O(viewport) not O(total coverage)
            MapControl.SetCoverageBitmapProviders(
                coverageService.GetCoverageBounds,
                (cellSize, minE, maxE, minN, maxN) => coverageService.GetCoverageBitmapCells(cellSize, minE, maxE, minN, maxN),
                coverageService.GetNewCoverageBitmapCells);

            // Mark dirty in case field was already loaded with coverage
            MapControl.MarkCoverageDirty();
        }

        // Wire up MapClicked event for AB line creation
        mapControl.MapClicked += OnMapClicked;
    }

    private void OnMapClicked(object? sender, MapClickEventArgs e)
    {
        if (ViewModel == null) return;

        Console.WriteLine($"[OnMapClicked] Mode={ViewModel.CurrentABCreationMode}, Step={ViewModel.CurrentABPointStep}, Easting={e.Easting:F2}, Northing={e.Northing:F2}");

        // For DriveAB mode, we use current GPS position (not the clicked position)
        // For DrawAB mode, we use the clicked map position
        // For Curve mode, tap finishes recording
        if (ViewModel.CurrentABCreationMode == ABCreationMode.DriveAB)
        {
            // In DriveAB mode, any tap triggers setting the point at current GPS position
            Console.WriteLine($"[OnMapClicked] DriveAB - Using GPS position: E={ViewModel.Easting:F2}, N={ViewModel.Northing:F2}");
            ViewModel.SetABPointCommand?.Execute(null);
        }
        else if (ViewModel.CurrentABCreationMode == ABCreationMode.DrawAB)
        {
            // In DrawAB mode, pass the clicked map coordinates
            var mapPosition = new Position
            {
                Easting = e.Easting,
                Northing = e.Northing
            };
            Console.WriteLine($"[OnMapClicked] DrawAB - Using map position: E={e.Easting:F2}, N={e.Northing:F2}");
            ViewModel.SetABPointCommand?.Execute(mapPosition);
        }
        else if (ViewModel.CurrentABCreationMode == ABCreationMode.Curve)
        {
            // In Curve mode, tap finishes recording
            Console.WriteLine($"[OnMapClicked] Curve - Finishing with {ViewModel.RecordedCurvePointCount} points");
            ViewModel.SetABPointCommand?.Execute(null);
        }
        else if (ViewModel.CurrentABCreationMode == ABCreationMode.DrawCurve)
        {
            // In DrawCurve mode, pass the clicked map coordinates to add a point
            var mapPosition = new Position
            {
                Easting = e.Easting,
                Northing = e.Northing
            };
            Console.WriteLine($"[OnMapClicked] DrawCurve - Adding point: E={e.Easting:F2}, N={e.Northing:F2}");
            ViewModel.SetABPointCommand?.Execute(mapPosition);
        }
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        // Load settings after window is opened
        LoadWindowSettings();
    }

    private void LoadWindowSettings()
    {
        // Load all settings from ConfigurationStore.Display (synced from AppSettings at startup)
        var display = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display;

        // Apply window size and position
        if (display.WindowWidth > 0 && display.WindowHeight > 0)
        {
            Width = display.WindowWidth;
            Height = display.WindowHeight;
        }

        if (display.WindowX >= 0 && display.WindowY >= 0)
        {
            Position = new PixelPoint((int)display.WindowX, (int)display.WindowY);
        }

        if (display.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        // Restore panel positions

        if (!double.IsNaN(display.LeftNavPanelX) && !double.IsNaN(display.LeftNavPanelY) && LeftNavPanel != null)
        {
            Canvas.SetLeft(LeftNavPanel, display.LeftNavPanelX);
            Canvas.SetTop(LeftNavPanel, display.LeftNavPanelY);
        }

        if (!double.IsNaN(display.RightNavPanelX) && !double.IsNaN(display.RightNavPanelY) && RightNavPanel != null)
        {
            // Clear Canvas.Right (set in XAML) when restoring to Canvas.Left position
            RightNavPanel.ClearValue(Canvas.RightProperty);
            Canvas.SetLeft(RightNavPanel, display.RightNavPanelX);
            Canvas.SetTop(RightNavPanel, display.RightNavPanelY);
        }

        if (!double.IsNaN(display.BottomNavPanelX) && !double.IsNaN(display.BottomNavPanelY) && BottomNavPanel != null)
        {
            Canvas.SetLeft(BottomNavPanel, display.BottomNavPanelX);
            Canvas.SetTop(BottomNavPanel, display.BottomNavPanelY);
        }

        if (!double.IsNaN(display.SectionPanelX) && !double.IsNaN(display.SectionPanelY) && SectionControlPanel != null)
        {
            Canvas.SetLeft(SectionControlPanel, display.SectionPanelX);
            Canvas.SetTop(SectionControlPanel, display.SectionPanelY);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.Services == null) return;

        // Save all settings to ConfigurationStore.Display (which then syncs to AppSettings)
        var display = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display;

        // Save window state
        display.WindowMaximized = WindowState == WindowState.Maximized;

        if (WindowState == WindowState.Normal)
        {
            display.WindowWidth = Width;
            display.WindowHeight = Height;
            display.WindowX = Position.X;
            display.WindowY = Position.Y;
        }

        // Save panel positions
        if (LeftNavPanel != null)
        {
            display.LeftNavPanelX = Canvas.GetLeft(LeftNavPanel);
            display.LeftNavPanelY = Canvas.GetTop(LeftNavPanel);
        }

        if (RightNavPanel != null)
        {
            display.RightNavPanelX = Canvas.GetLeft(RightNavPanel);
            display.RightNavPanelY = Canvas.GetTop(RightNavPanel);
        }

        if (BottomNavPanel != null)
        {
            display.BottomNavPanelX = Canvas.GetLeft(BottomNavPanel);
            display.BottomNavPanelY = Canvas.GetTop(BottomNavPanel);
        }

        if (SectionControlPanel != null)
        {
            display.SectionPanelX = Canvas.GetLeft(SectionControlPanel);
            display.SectionPanelY = Canvas.GetTop(SectionControlPanel);
        }

        // Save UI state to ConfigurationStore
        if (ViewModel != null)
        {
            display.GridVisible = ViewModel.IsGridOn;
        }

        // Save configuration (syncs ConfigurationStore to AppSettings and saves to disk)
        var configService = App.Services.GetRequiredService<IConfigurationService>();
        configService.SaveAppSettings();

        // Save coverage to active field before closing
        var fieldService = App.Services.GetRequiredService<IFieldService>();
        var coverageService = App.Services.GetRequiredService<ICoverageMapService>();
        if (fieldService.ActiveField != null && !string.IsNullOrEmpty(fieldService.ActiveField.DirectoryPath))
        {
            try
            {
                coverageService.SaveToFile(fieldService.ActiveField.DirectoryPath);
                Console.WriteLine($"[Coverage] Saved coverage on app close to {fieldService.ActiveField.DirectoryPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Coverage] Error saving coverage on close: {ex.Message}");
            }
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

            // Constrain all panels using shared helper
            PanelConstraintHelper.ConstrainPanelWithExtent(LeftNavPanel, Bounds.Width, Bounds.Height,
                subPanelExtent: 410, defaultLeft: 20, defaultTop: 100);
            PanelConstraintHelper.ConstrainLeftTopPanel(BottomNavPanel, Bounds.Width, Bounds.Height,
                defaultLeft: 200, defaultTop: 600);
            PanelConstraintHelper.ConstrainRightTopPanel(RightNavPanel, Bounds.Width, Bounds.Height,
                defaultTop: 100);
            PanelConstraintHelper.ConstrainSubPanels(LeftNavPanel, Bounds.Width, Bounds.Height,
                PanelConstraintHelper.LeftNavSubPanelNames);
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
        if (sender is Control control)
        {
            _isDraggingSection = true;
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(control);
        }
    }

    private void SectionControl_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingSection && sender is Control control)
        {
            var currentPoint = e.GetPosition(this);
            var delta = currentPoint - _dragStartPoint;

            // Calculate new position
            double newLeft = Canvas.GetLeft(control) + delta.X;
            double newTop = Canvas.GetTop(control) + delta.Y;

            // Constrain to window bounds
            double maxLeft = Bounds.Width - control.Bounds.Width;
            double maxTop = Bounds.Height - control.Bounds.Height;

            newLeft = Math.Clamp(newLeft, 0, maxLeft);
            newTop = Math.Clamp(newTop, 0, maxTop);

            // Update position
            Canvas.SetLeft(control, newLeft);
            Canvas.SetTop(control, newTop);

            _dragStartPoint = currentPoint;
        }
    }

    private void SectionControl_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingSection)
        {
            _isDraggingSection = false;
            e.Pointer.Capture(null);
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
                // NOTE: Boundary point recording is now handled by MainViewModel
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.ToolEasting) ||
                 e.PropertyName == nameof(MainViewModel.ToolNorthing))
        {
            // Tool position updated - update map control
            if (ViewModel != null && MapControl != null)
            {
                MapControl.SetToolPosition(
                    ViewModel.ToolEasting,
                    ViewModel.ToolNorthing,
                    ViewModel.ToolHeadingRadians,
                    ViewModel.ToolWidth,
                    ViewModel.HitchEasting,
                    ViewModel.HitchNorthing);
                // Also update section states for rendering
                MapControl.SetSectionStates(
                    ViewModel.GetSectionStates(),
                    ViewModel.GetSectionWidths(),
                    ViewModel.NumSections,
                    ViewModel.GetSectionButtonStates());
            }
        }
        else if (e.PropertyName?.StartsWith("Section") == true &&
                 (e.PropertyName.EndsWith("Active") || e.PropertyName.EndsWith("ColorCode")))
        {
            // Section state or color code changed - update map control
            if (ViewModel != null && MapControl != null)
            {
                MapControl.SetSectionStates(
                    ViewModel.GetSectionStates(),
                    ViewModel.GetSectionWidths(),
                    ViewModel.NumSections,
                    ViewModel.GetSectionButtonStates());
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
            // TODO: Implement theme switching (background color, grid color, etc.)
        }
        else if (e.PropertyName == nameof(MainViewModel.IsNorthUp))
        {
            // TODO: Implement camera rotation locking to north
        }
        else if (e.PropertyName == nameof(MainViewModel.Brightness))
        {
            // Brightness control depends on platform-specific implementation
            // Currently marked as not supported in DisplaySettingsService
        }
        else if (e.PropertyName == nameof(MainViewModel.EnableABClickSelection))
        {
            if (MapControl is DrawingContextMapControl dcMapControl)
            {
                dcMapControl.EnableClickSelection = ViewModel?.EnableABClickSelection ?? false;
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.PendingPointA))
        {
            // Update map with pending Point A marker
            if (MapControl is DrawingContextMapControl dcMapControl)
            {
                dcMapControl.SetPendingPointA(ViewModel?.PendingPointA);
            }
        }
    }

    private void SavedTracks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // When a new track is added, show the most recently active one
        UpdateActiveTrack();
    }

    private void UpdateActiveTrack()
    {
        if (MapControl is DrawingContextMapControl dcMapControl && ViewModel != null)
        {
            // Only show track on map if explicitly active (no fallback)
            var activeTrack = ViewModel.SavedTracks.FirstOrDefault(t => t.IsActive);
            dcMapControl.SetActiveTrack(activeTrack);
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
                // In AB creation mode, handle tap for setting points instead of panning
                if (ViewModel?.EnableABClickSelection == true && MapControl is DrawingContextMapControl dcMapControl)
                {
                    // Get the world position from the click and fire MapClicked event
                    var worldPos = dcMapControl.ScreenToWorld(point.Position.X, point.Position.Y);
                    OnMapClicked(dcMapControl, new MapClickEventArgs(worldPos.Easting, worldPos.Northing));
                    e.Handled = true;
                    return;
                }

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
    private void LeftNavPanel_DragMoved(object? sender, Point newPosition)
    {
        if (LeftNavPanel == null) return;

        // Constrain to window bounds
        // Account for sub-panels that extend ~410px to the right (offset 90 + width ~320)
        const double subPanelExtent = 410;
        double maxLeft = Bounds.Width - LeftNavPanel.Bounds.Width - subPanelExtent;
        double maxTop = Bounds.Height - LeftNavPanel.Bounds.Height;

        double newLeft = Math.Clamp(newPosition.X, 0, Math.Max(0, maxLeft));
        double newTop = Math.Clamp(newPosition.Y, 0, Math.Max(0, maxTop));

        // Update position
        Canvas.SetLeft(LeftNavPanel, newLeft);
        Canvas.SetTop(LeftNavPanel, newTop);
    }

    // Handler for shared RightNavigationPanel drag events
    private void RightNavPanel_DragMoved(object? sender, Point newPosition)
    {
        if (RightNavPanel == null) return;

        // Clear the Right property on first drag since we're switching to Left
        if (!double.IsNaN(Canvas.GetRight(RightNavPanel)))
        {
            RightNavPanel.ClearValue(Canvas.RightProperty);
        }

        // Constrain to window bounds
        double maxLeft = Bounds.Width - RightNavPanel.Bounds.Width;
        double maxTop = Bounds.Height - RightNavPanel.Bounds.Height;

        double newLeft = Math.Clamp(newPosition.X, 0, Math.Max(0, maxLeft));
        double newTop = Math.Clamp(newPosition.Y, 0, Math.Max(0, maxTop));

        // Update position
        Canvas.SetLeft(RightNavPanel, newLeft);
        Canvas.SetTop(RightNavPanel, newTop);
    }

    // NOTE: BottomNavigationPanel is now a fixed-position panel with flyout menu
    // (no longer draggable - follows AgOpenGPS pattern)

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

        // Check bottom navigation panel
        if (BottomNavPanel != null && BottomNavPanel.IsVisible && BottomNavPanel.Bounds.Width > 0 && BottomNavPanel.Bounds.Height > 0)
        {
            double left = Canvas.GetLeft(BottomNavPanel);
            double top = Canvas.GetTop(BottomNavPanel);

            if (double.IsNaN(left)) left = 400;
            if (double.IsNaN(top)) top = 650;

            var panelBounds = new Rect(left, top, BottomNavPanel.Bounds.Width, BottomNavPanel.Bounds.Height);

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
    // NOTE: Boundary recording service is now handled entirely by MainViewModel via _mapService.
    // The handlers below are legacy code-behind that should be migrated to ViewModel commands.

    private BoundaryType _currentBoundaryType = BoundaryType.Outer;

    // Helper to get BoundaryRecordingService from DI (legacy - prefer ViewModel commands)
    private IBoundaryRecordingService? GetBoundaryRecordingService()
        => App.Services?.GetService<IBoundaryRecordingService>();

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
        if (GetBoundaryRecordingService() == null) return;

        var state = GetBoundaryRecordingService().State;
        if (state == BoundaryRecordingState.Idle)
        {
            // Start new recording
            var boundaryType = GetSelectedBoundaryType();
            GetBoundaryRecordingService().StartRecording(boundaryType);
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
            GetBoundaryRecordingService().ResumeRecording();
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
        if (GetBoundaryRecordingService() == null) return;

        if (GetBoundaryRecordingService().State == BoundaryRecordingState.Recording)
        {
            GetBoundaryRecordingService().PauseRecording();
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
        if (GetBoundaryRecordingService() == null || App.Services == null || ViewModel == null) return;

        if (GetBoundaryRecordingService().State != BoundaryRecordingState.Idle)
        {
            // Get the current boundary type before stopping
            var isOuter = GetBoundaryRecordingService().CurrentBoundaryType == BoundaryType.Outer;
            var polygon = GetBoundaryRecordingService().StopRecording();

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
        var service = GetBoundaryRecordingService();
        if (service == null) return;

        if (service.RemoveLastPoint())
        {
            UpdateBoundaryStatusDisplay();
            // Map display is updated by ViewModel via StateChanged event
            if (ViewModel != null)
            {
                ViewModel.StatusMessage = $"Removed point ({service.PointCount} remaining)";
            }
        }
    }

    // Clear all boundary points
    private void BtnClearBoundary_Click(object? sender, RoutedEventArgs e)
    {
        if (GetBoundaryRecordingService() == null) return;

        GetBoundaryRecordingService().ClearPoints();
        UpdateBoundaryStatusDisplay();
        if (ViewModel != null)
        {
            ViewModel.StatusMessage = "Boundary points cleared";
        }
    }

    // Update the boundary status display text blocks
    private void UpdateBoundaryStatusDisplay()
    {
        if (GetBoundaryRecordingService() == null) return;

        var statusText = this.FindControl<TextBlock>("BoundaryStatusLabel");
        var pointsText = this.FindControl<TextBlock>("BoundaryPointsLabel");
        var areaText = this.FindControl<TextBlock>("BoundaryAreaLabel");

        if (statusText != null)
        {
            statusText.Text = GetBoundaryRecordingService().State.ToString();
        }

        if (pointsText != null)
        {
            pointsText.Text = GetBoundaryRecordingService().PointCount.ToString();
        }

        if (areaText != null)
        {
            areaText.Text = $"{GetBoundaryRecordingService().AreaHectares:F2} Ha";
        }

        // Update button enabled states
        var recordBtn = this.FindControl<Button>("BtnRecordBoundary");
        var pauseBtn = this.FindControl<Button>("BtnPauseBoundary");
        var stopBtn = this.FindControl<Button>("BtnStopBoundary");

        var state = GetBoundaryRecordingService().State;
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
    // Legacy button handlers above should be migrated to ViewModel commands:
    // - BtnBoundaryOffset -> ViewModel's ShowBoundaryOffsetDialogCommand
    // - BtnBoundarySectionControl -> ViewModel's IsBoundarySectionControlOn binding
    // - BtnBoundaryLeftRight -> ViewModel's ToggleBoundaryLeftRightCommand
    // - BtnBoundaryAntennaTool -> ViewModel's ToggleBoundaryAntennaToolCommand
    // - BtnBoundaryDeleteLast -> ViewModel's UndoBoundaryPointCommand
    // - BtnBoundaryAddPoint -> ViewModel's AddBoundaryPointCommand
    // NOTE: CalculateOffsetPosition is now in MainViewModel
}