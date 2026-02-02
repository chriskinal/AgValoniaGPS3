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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Behaviors;
using AgValoniaGPS.Android.Services;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Android.Views;

/// <summary>
/// Android MainView with ViewModel - wires up map control to ViewModel commands
/// </summary>
public partial class MainView : UserControl
{
    private DrawingContextMapControl? _mapControl;
    private MainViewModel? _viewModel;

    // Panel references for position save/restore
    private Control? _leftNavPanel;
    private Control? _rightNavPanel;
    private Control? _bottomNavPanel;
    private Control? _sectionControlPanel;

    public MainView()
    {
        Console.WriteLine("[MainView] Constructor starting...");
        InitializeComponent();
        Console.WriteLine("[MainView] InitializeComponent completed.");

        // Get reference to map control
        _mapControl = this.FindControl<DrawingContextMapControl>("MapControl");

        // Get references to panels for position save/restore
        _leftNavPanel = this.FindControl<Control>("LeftNavPanel");
        _rightNavPanel = this.FindControl<Control>("RightNavPanel");
        _bottomNavPanel = this.FindControl<Control>("BottomNavPanel");
        _sectionControlPanel = this.FindControl<Control>("SectionControlPanel");

        // Restore panel positions from ConfigurationStore
        RestorePanelPositions();
    }

    private void RestorePanelPositions()
    {
        var display = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display;

        if (!double.IsNaN(display.LeftNavPanelX) && !double.IsNaN(display.LeftNavPanelY) && _leftNavPanel != null)
        {
            Canvas.SetLeft(_leftNavPanel, display.LeftNavPanelX);
            Canvas.SetTop(_leftNavPanel, display.LeftNavPanelY);
        }

        if (!double.IsNaN(display.RightNavPanelX) && !double.IsNaN(display.RightNavPanelY) && _rightNavPanel != null)
        {
            Canvas.SetLeft(_rightNavPanel, display.RightNavPanelX);
            Canvas.SetTop(_rightNavPanel, display.RightNavPanelY);
        }

        if (!double.IsNaN(display.BottomNavPanelX) && !double.IsNaN(display.BottomNavPanelY) && _bottomNavPanel != null)
        {
            Canvas.SetLeft(_bottomNavPanel, display.BottomNavPanelX);
            Canvas.SetTop(_bottomNavPanel, display.BottomNavPanelY);
        }

        if (!double.IsNaN(display.SectionPanelX) && !double.IsNaN(display.SectionPanelY) && _sectionControlPanel != null)
        {
            Canvas.SetLeft(_sectionControlPanel, display.SectionPanelX);
            Canvas.SetTop(_sectionControlPanel, display.SectionPanelY);
        }
    }

    public void SavePanelPositions()
    {
        var display = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display;

        if (_leftNavPanel != null)
        {
            display.LeftNavPanelX = Canvas.GetLeft(_leftNavPanel);
            display.LeftNavPanelY = Canvas.GetTop(_leftNavPanel);
        }

        if (_rightNavPanel != null)
        {
            display.RightNavPanelX = Canvas.GetLeft(_rightNavPanel);
            display.RightNavPanelY = Canvas.GetTop(_rightNavPanel);
        }

        if (_bottomNavPanel != null)
        {
            display.BottomNavPanelX = Canvas.GetLeft(_bottomNavPanel);
            display.BottomNavPanelY = Canvas.GetTop(_bottomNavPanel);
        }

        if (_sectionControlPanel != null)
        {
            display.SectionPanelX = Canvas.GetLeft(_sectionControlPanel);
            display.SectionPanelY = Canvas.GetTop(_sectionControlPanel);
        }
    }

    public MainView(MainViewModel viewModel, MapService mapService, ICoverageMapService coverageService) : this()
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

            // Wire up coverage updates
            coverageService.CoverageUpdated += (sender, args) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Skip full rebuild if pixels were loaded directly from file
                    if (args.PixelsAlreadyLoaded)
                    {
                        // Just mark dirty to refresh display - pixels already in bitmap
                        _mapControl?.MarkCoverageDirty();
                    }
                    else if (args.IsFullReload)
                        _mapControl?.MarkCoverageFullRebuildNeeded();
                    else
                        _mapControl?.MarkCoverageDirty();
                    _viewModel?.RefreshCoverageStatistics();
                });
            };

            // Set up bitmap-based coverage rendering (PERF-004)
            // allCellsProvider takes viewport bounds for spatial queries - O(viewport) not O(total coverage)
            _mapControl.SetCoverageBitmapProviders(
                coverageService.GetCoverageBounds,
                (cellSize, minE, maxE, minN, maxN) => coverageService.GetCoverageBitmapCells(cellSize, minE, maxE, minN, maxN),
                coverageService.GetNewCoverageBitmapCells);

            // Set up unified bitmap pixel access (PERF-004 Phase 4)
            // Service writes directly to map control's WriteableBitmap
            coverageService.SetPixelAccessCallbacks(
                _mapControl.GetCoveragePixel,
                _mapControl.SetCoveragePixel,
                _mapControl.ClearCoveragePixels);

            // Set up buffer callbacks for save/load
            coverageService.GetPixelBufferCallback = _mapControl.GetCoveragePixelBuffer;
            coverageService.SetPixelBufferCallback = _mapControl.SetCoveragePixelBuffer;
            coverageService.GetDisplayBitmapInfoCallback = _mapControl.GetDisplayBitmapInfo;

            // Mark dirty in case field was already loaded with coverage
            _mapControl.MarkCoverageDirty();
        }

        // Wire up position updates - when ViewModel properties change, update map control
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Subscribe to track collection changes to update active track display
        viewModel.SavedTracks.CollectionChanged += SavedTracks_CollectionChanged;

        // Update active track immediately in case field was already loaded
        UpdateActiveTrack();

        // Note: Tool/vehicle position sync happens when simulator is enabled
        // (see IsSimulatorEnabled handler in OnViewModelPropertyChanged)

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
        // For Curve mode, tap finishes recording
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
        else if (_viewModel.CurrentABCreationMode == ABCreationMode.Curve)
        {
            // In Curve mode, tap finishes recording
            _viewModel.SetABPointCommand?.Execute(null);
        }
        else if (_viewModel.CurrentABCreationMode == ABCreationMode.DrawCurve)
        {
            // In DrawCurve mode, pass the clicked map coordinates to add a point
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
            // Only show track on map if explicitly active (no fallback)
            var activeTrack = _viewModel.SavedTracks.FirstOrDefault(t => t.IsActive);
            _mapControl.SetActiveTrack(activeTrack);
        }
    }

    private void SyncInitialPositions()
    {
        if (_mapControl == null || _viewModel == null) return;

        // Sync vehicle position
        double headingRadians = _viewModel.Heading * Math.PI / 180.0;
        _mapControl.SetVehiclePosition(_viewModel.Easting, _viewModel.Northing, headingRadians);

        // Get tool config (ViewModel's tool values are 0 until first simulator update)
        var configStore = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        double toolWidth = _viewModel.ToolWidth > 0 ? _viewModel.ToolWidth : configStore.ActualToolWidth;

        // Calculate tool position from vehicle position if not yet set
        double toolX = _viewModel.ToolEasting;
        double toolY = _viewModel.ToolNorthing;
        double toolHeading = _viewModel.ToolHeadingRadians;
        double hitchX = _viewModel.HitchEasting;
        double hitchY = _viewModel.HitchNorthing;

        if (Math.Abs(toolX) < 0.001 && Math.Abs(toolY) < 0.001)
        {
            // Tool position not yet calculated - compute from vehicle position
            var tool = configStore.Tool;
            double hitchDist = tool.IsToolRearFixed || tool.IsToolTrailing || tool.IsToolTBT
                ? -Math.Abs(tool.HitchLength)
                : Math.Abs(tool.HitchLength);

            hitchX = _viewModel.Easting + Math.Sin(headingRadians) * hitchDist;
            hitchY = _viewModel.Northing + Math.Cos(headingRadians) * hitchDist;
            toolX = hitchX;
            toolY = hitchY;
            toolHeading = headingRadians;
        }

        // Sync tool position and section states
        _mapControl.SetToolPosition(toolX, toolY, toolHeading, toolWidth, hitchX, hitchY);
        _mapControl.SetSectionStates(
            _viewModel.GetSectionStates(),
            _viewModel.GetSectionWidths(),
            _viewModel.NumSections,
            _viewModel.GetSectionButtonStates());
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
            else if (e.PropertyName == nameof(MainViewModel.ToolEasting) ||
                     e.PropertyName == nameof(MainViewModel.ToolNorthing))
            {
                // Tool position updated - update map control
                _mapControl.SetToolPosition(
                    _viewModel.ToolEasting,
                    _viewModel.ToolNorthing,
                    _viewModel.ToolHeadingRadians,
                    _viewModel.ToolWidth,
                    _viewModel.HitchEasting,
                    _viewModel.HitchNorthing);
                // Also update section states for rendering
                _mapControl.SetSectionStates(
                    _viewModel.GetSectionStates(),
                    _viewModel.GetSectionWidths(),
                    _viewModel.NumSections,
                    _viewModel.GetSectionButtonStates());
            }
            else if (e.PropertyName?.StartsWith("Section") == true &&
                     (e.PropertyName.EndsWith("Active") || e.PropertyName.EndsWith("ColorCode")))
            {
                // Section state or color code changed - update map control
                _mapControl.SetSectionStates(
                    _viewModel.GetSectionStates(),
                    _viewModel.GetSectionWidths(),
                    _viewModel.NumSections,
                    _viewModel.GetSectionButtonStates());
            }
            else if (e.PropertyName == nameof(MainViewModel.EnableABClickSelection))
            {
                // Update map control click selection mode
                _mapControl.EnableClickSelection = _viewModel.EnableABClickSelection;
            }
            else if (e.PropertyName == nameof(MainViewModel.Is2DMode))
            {
                // Is2DMode = true means 3D is off, so invert the value
                _mapControl.Set3DMode(!_viewModel.Is2DMode);
            }
            else if (e.PropertyName == nameof(MainViewModel.CameraPitch))
            {
                // CameraPitch is in degrees from -90 (looking straight down) to -10 (nearly horizontal)
                // Map control expects radians
                double pitchRadians = -_viewModel.CameraPitch * Math.PI / 180.0;
                _mapControl.SetPitchAbsolute(pitchRadians);
            }
            else if (e.PropertyName == nameof(MainViewModel.IsSimulatorEnabled))
            {
                // When simulator is enabled, sync positions after a short delay
                // to ensure the first simulator tick has updated tool position
                if (_viewModel.IsSimulatorEnabled)
                {
                    // Wait for simulator's first tick (runs at ~10Hz = 100ms interval)
                    // Use 300ms to ensure at least one update has occurred
                    _ = Task.Delay(300).ContinueWith(_ =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(SyncInitialPositions);
                    });
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.PendingPointA))
            {
                // Update map with pending Point A marker
                _mapControl.SetPendingPointA(_viewModel.PendingPointA);
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
}
