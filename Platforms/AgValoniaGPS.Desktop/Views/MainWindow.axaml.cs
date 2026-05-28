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
using System.Linq;
using System.Threading.Tasks;
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

namespace AgValoniaGPS.Desktop.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private SkiaMapControl? MapControl;
    private bool _isDraggingRecPath = false;
    private Avalonia.Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();

#if DEBUG
        // DEBUG-only viewport lock: pin to the 1200×720 design size
        // (Android tablet landscape) so layout work simulates the
        // constrained device. Release builds resize and maximize freely.
        MinWidth = MaxWidth = 1200;
        MinHeight = MaxHeight = 720;
        Width = 1200;
        Height = 720;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
#endif

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

            // Wire screenshot provider for debug dump (#127)
            ViewModel.ScreenshotProvider = CaptureScreenshotPng;

            // Wire fullscreen toggle for immediate effect (ConfigurationViewModel
            // is created lazily, so subscribe when it appears)
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ConfigurationViewModel)
                    && ViewModel?.ConfigurationViewModel != null)
                {
                    ViewModel.ConfigurationViewModel.FullscreenChanged += (isFullscreen) =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            WindowState = isFullscreen
                                ? WindowState.FullScreen
                                : WindowState.Normal;
                        });
                    };
                }
            };
        }

        if (MapControl != null)
        {
            MapControl.FpsUpdated += fps =>
            {
                if (ViewModel != null)
                    ViewModel.CurrentFps = fps;
            };
        }

        // Add keyboard shortcut for 3D mode toggle (F3)
        this.KeyDown += MainWindow_KeyDown;

        // Save window settings on close
        this.Closing += MainWindow_Closing;

        // Wire up chart panel drag events (charts remain draggable)
        if (SteerChartPanel != null)
            SteerChartPanel.DragMoved += (_, delta) => MovePanel(SteerChartPanel, delta);
        if (HeadingChartPanel != null)
            HeadingChartPanel.DragMoved += (_, delta) => MovePanel(HeadingChartPanel, delta);
        if (XTEChartPanel != null)
            XTEChartPanel.DragMoved += (_, delta) => MovePanel(XTEChartPanel, delta);
        // SimulatorBarPanel is a fixed layout child of the bottom stack — no drag/positioning needed.
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
        MapControl = new SkiaMapControl();
        MapControlContainer.Content = MapControl;

        // Note: ViewModel is null here (DataContext set after CreateMapControl).
        // Initial view state applied in MainWindow_Opened after settings load.

        if (App.Services != null)
        {
            var mapService = App.Services.GetRequiredService<AgValoniaGPS.Desktop.Services.MapService>();
            mapService.RegisterMapControl(MapControl);

            var coverageService = App.Services.GetRequiredService<ICoverageMapService>();
            coverageService.CoverageUpdated += (sender, args) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CoverageRefreshDispatcher.Apply(MapControl, args.IsFullReload);
                    ViewModel?.RefreshCoverageStatistics();
                });
            };

            MapControl.SetCoverageBitmapProviders(
                coverageService.GetCoverageBounds,
                (cellSize, minE, maxE, minN, maxN) => coverageService.GetCoverageBitmapCells(cellSize, minE, maxE, minN, maxN),
                coverageService.GetNewCoverageBitmapCells);
            MapControl.MarkCoverageDirty();

            MapControl.MapClicked += OnMapClicked;
            MapControl.UserPanned += () => ViewModel?.OnUserPan();
        }
    }

    private void OnMapClicked(object? sender, MapClickEventArgs e)
    {
        if (ViewModel == null) return;

        // Flag placement mode takes priority
        if (ViewModel.IsPlaceFlagOnClickMode)
        {
            ViewModel.PlaceFlagAtWorldPosition(e.Easting, e.Northing);
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[OnMapClicked] Mode={ViewModel.CurrentABCreationMode}, Step={ViewModel.CurrentABPointStep}, Easting={e.Easting:F2}, Northing={e.Northing:F2}");

        // For DriveAB mode, we use current GPS position (not the clicked position)
        // For DrawAB mode, we use the clicked map position
        // For Curve mode, tap finishes recording
        if (ViewModel.CurrentABCreationMode == ABCreationMode.DriveAB)
        {
            // In DriveAB mode, any tap triggers setting the point at current GPS position
            System.Diagnostics.Debug.WriteLine($"[OnMapClicked] DriveAB - Using GPS position: E={ViewModel.Easting:F2}, N={ViewModel.Northing:F2}");
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
            System.Diagnostics.Debug.WriteLine($"[OnMapClicked] DrawAB - Using map position: E={e.Easting:F2}, N={e.Northing:F2}");
            ViewModel.SetABPointCommand?.Execute(mapPosition);
        }
        else if (ViewModel.CurrentABCreationMode == ABCreationMode.Curve)
        {
            // In Curve mode, tap finishes recording
            System.Diagnostics.Debug.WriteLine($"[OnMapClicked] Curve - Finishing with {ViewModel.RecordedCurvePointCount} points");
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
            System.Diagnostics.Debug.WriteLine($"[OnMapClicked] DrawCurve - Adding point: E={e.Easting:F2}, N={e.Northing:F2}");
            ViewModel.SetABPointCommand?.Execute(mapPosition);
        }
    }

    private byte[]? CaptureScreenshotPng() =>
        AgValoniaGPS.Views.ScreenshotHelper.CaptureScreenshotPng(this);

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        // Load settings after window is opened
        LoadWindowSettings();

        // Apply initial camera state to map (ViewModel is available now)
        if (ViewModel != null && MapControl != null)
        {
            MapControl.IsGridVisible = ViewModel.IsGridOn;

            // Sync Is2DMode with saved pitch to avoid state mismatch
            if (ViewModel.CameraPitch <= -89.0)
                ViewModel.Is2DMode = true;

            MapControl.Set3DMode(!ViewModel.Is2DMode);
            double pitchRadians = (90.0 + ViewModel.CameraPitch) * Math.PI / 180.0;
            MapControl.SetPitchAbsolute(pitchRadians);

            // Push the loaded camera follow mode to the map control.
            // VM.CameraMode setter fires _mapService.SetCameraFollowMode during
            // LoadSettings, but the call is a no-op when MapControl hasn't been
            // registered yet (iOS) — and the setter only runs ApplyCameraMode
            // when the value changes from its default. Either path can leave
            // MapControl in default Map(3) mode while VM shows H/N/etc.
            MapControl.CameraFollowMode = ViewModel.CameraMode switch
            {
                AgValoniaGPS.Models.CameraMode.NorthUp => 0,
                AgValoniaGPS.Models.CameraMode.HeadingUp => 1,
                AgValoniaGPS.Models.CameraMode.Free => 2,
                _ => 3,
            };

            // Fire OnPropertyChanged for properties that ConfigurationService
            // loaded directly into the backing field (bypassing the setter,
            // so no binding refresh happened). The display panel binding
            // otherwise stays on its empty default until the first user tap.
            ViewModel.NotifyDisplayLabelsAfterStartup();
        }
    }

    private void LoadWindowSettings()
    {
#if !DEBUG
        // StartFullscreen is a preference (config); window geometry is
        // persistent state (appstate.json).
        // In DEBUG the constructor pins the window to 1200×720 and centers it,
        // so skip the saved-size/state restore to leave the lock uncontested.
        var display = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display;
        var state = AgValoniaGPS.Models.State.PersistentAppState.Instance;

        // Apply window size and position
        if (state.WindowWidth > 0 && state.WindowHeight > 0)
        {
            Width = state.WindowWidth;
            Height = state.WindowHeight;
        }

        if (state.WindowX >= 0 && state.WindowY >= 0)
        {
            Position = new PixelPoint((int)state.WindowX, (int)state.WindowY);
        }

        if (display.StartFullscreen)
        {
            WindowState = WindowState.FullScreen;
        }
        else if (state.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
#endif

        // Panel positions are now anchored (no saved positions needed)
    }

    // Set to true while we're re-entering Closing after programmatically calling Close()
    // at the end of SaveAndCloseAsync. The flag lets that second close proceed without
    // re-firing the save path.
    private bool _closeSaveInProgress;

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.Services == null) return;

        // Window geometry + UI toggles — capture synchronously on every close attempt.
        // Cheap and small, never a good reason to skip.
        var display = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display;
        var state = AgValoniaGPS.Models.State.PersistentAppState.Instance;
        state.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            state.WindowWidth = Width;
            state.WindowHeight = Height;
            state.WindowX = Position.X;
            state.WindowY = Position.Y;
        }
        if (ViewModel != null)
            display.GridVisible = ViewModel.IsGridOn;

        // Second-pass close triggered by SaveAndCloseAsync → let it through.
        if (_closeSaveInProgress)
            return;

        // If a field is open, we need to run CloseFieldAsync (saves boundary / tracks /
        // headland / tram / elevation / coverage) before actually exiting. The old path
        // only saved coverage via fire-and-forget Task.Run, which (a) missed the rest and
        // (b) raced the process exit — users who closed without explicit File → Close Field
        // lost session work (#291).
        //
        // Closing is a synchronous event, so we cancel it, run the async save, then call
        // Close() a second time; the _closeSaveInProgress flag tells this handler to let
        // the second close proceed.
        if (ViewModel?.HasActiveField == true)
        {
            e.Cancel = true;

            // Warn before quitting drops coverage painted with no active job
            // (same loss vector as the in-app Close Field command). If the guard
            // shows its prompt, the quit resumes from the Save/Discard button;
            // Cancel leaves the window open.
            if (ViewModel.TryShowUnsavedCoverageGuard(() =>
                {
                    _closeSaveInProgress = true;
                    _ = SaveAndCloseAsync();
                }))
            {
                return;
            }

            _closeSaveInProgress = true;
            _ = SaveAndCloseAsync();
            return;
        }

        // No field open — just persist AppSettings (cheap, synchronous).
        try
        {
            App.Services.GetRequiredService<IConfigurationService>().SaveAppSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Save] Error saving settings on close: {ex.Message}");
        }
    }

    private async Task SaveAndCloseAsync()
    {
        try
        {
            if (App.Services != null)
                App.Services.GetRequiredService<IConfigurationService>().SaveAppSettings();

            if (ViewModel != null)
                await ViewModel.CloseFieldAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Save] Error during SaveAndCloseAsync: {ex.Message}");
        }
        finally
        {
            // Re-initiate the close; _closeSaveInProgress lets it through.
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Close());
        }
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // Skip when typing in text fields
        if (e.Source is TextBox) return;

        // Fixed keys (not configurable)
        switch (e.Key)
        {
            case Key.F3:
                MapControl?.Toggle3DMode();
                e.Handled = true;
                return;
            case Key.PageUp:
                MapControl?.SetPitch(0.05);
                e.Handled = true;
                return;
            case Key.PageDown:
                MapControl?.SetPitch(-0.05);
                e.Handled = true;
                return;
        }

        // Configurable hotkeys
        var keyStr = KeyToString(e.Key);
        if (keyStr != null && ViewModel?.HandleHotkey(keyStr) == true)
            e.Handled = true;
    }

    private static string? KeyToString(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return ((char)('A' + (key - Key.A))).ToString();
        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        return null;
    }

    private void MainWindow_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        // Panels are now anchored via alignment - no constraint logic needed
    }

    // Removed: BtnNtripConnect_Click, BtnNtripDisconnect_Click, BtnDataIO_Click
    // These are now handled by ViewModel commands

    // Removed: BtnEnterSimCoords_Click, Btn3DToggle_Click
    // These are now handled by ViewModel commands (ShowSimCoordsDialogCommand, Toggle3DModeCommand)

    // Removed: BtnFields_Click, BtnNewField_Click, BtnOpenField_Click, BtnCloseField_Click, BtnFromExisting_Click, CopyFileIfExists
    // These are now handled by ViewModel commands

    // Removed: BtnIsoXml_Click, BtnKml_Click, BtnDriveIn_Click, BtnResumeField_Click
    // These are now handled by ViewModel commands

    // Section control is now anchored (no drag needed)

    // --- Recorded Path Panel Drag ---
    private void RecordedPath_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            _isDraggingRecPath = true;
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(control);
        }
    }

    private void RecordedPath_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingRecPath && sender is Control control)
        {
            var currentPoint = e.GetPosition(this);
            var delta = currentPoint - _dragStartPoint;
            double newLeft = Math.Clamp(Canvas.GetLeft(control) + delta.X, 0, Bounds.Width - control.Bounds.Width);
            double newTop = Math.Clamp(Canvas.GetTop(control) + delta.Y, 0, Bounds.Height - control.Bounds.Height);
            Canvas.SetLeft(control, newLeft);
            Canvas.SetTop(control, newTop);
            _dragStartPoint = currentPoint;
        }
    }

    private void RecordedPath_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingRecPath)
        {
            _isDraggingRecPath = false;
            e.Pointer.Capture(null);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Vehicle/tool/hitch positions are pushed atomically via
        // _mapService.SetAllPositions(...) at the end of ApplyGpsCycleResult,
        // so we must NOT also push them per-property here. Doing both caused
        // visible tool/hitch oscillation: SetVehiclePosition moves the camera
        // and triggers a render with stale tool state, then SetAllPositions
        // snaps the tool forward — once per GPS tick.
        // Section state/layout is pushed to the map directly by the ViewModel
        // (MainViewModel.UpdateSectionStates) and every GPS cycle by
        // ApplyGpsCycleResult, so there is no per-property section bridge here.
        if (e.PropertyName == nameof(MainViewModel.IsGridOn))
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
                // Map expects positive radians (0 = overhead, PI/2.5 = horizontal)
                // Convert: -90 -> 0 rad (overhead), -10 -> 1.4 rad (horizontal)
                double pitchRadians = (90.0 + ViewModel.CameraPitch) * Math.PI / 180.0;
                MapControl.SetPitchAbsolute(pitchRadians);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.Is2DMode))
        {
            if (ViewModel != null && MapControl != null)
            {
                // SkiaMap toggles pitch in-place; nothing to swap in the visual tree.
                MapControl.Set3DMode(!ViewModel.Is2DMode);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsDayMode))
        {
            if (ViewModel != null && MapControl != null)
            {
                MapControl.SetDayMode(ViewModel.IsDayMode);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsNorthUp))
        {
            if (ViewModel != null && MapControl != null)
            {
                MapControl.SetNorthUp(ViewModel.IsNorthUp);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.EnableABClickSelection))
        {
            if (MapControl != null)
                MapControl.EnableClickSelection = ViewModel?.EnableABClickSelection ?? false;
        }
        else if (e.PropertyName == nameof(MainViewModel.PendingPointA))
        {
            MapControl?.SetPendingPointA(ViewModel?.PendingPointA);
        }
        else if (e.PropertyName == nameof(MainViewModel.CrossTrackError))
        {
            // Update light bar with cross-track error
            if (ViewModel != null && LightBarPanel != null)
            {
                bool hasGuidance = ViewModel.HasActiveTrack
                    || ViewModel.IsContourModeOn
                    || ViewModel.State.RecordedPath.IsDrivingRecordedPath;
                LightBarPanel.Update(
                    ViewModel.CrossTrackError / 100.0, // cm -> meters
                    ViewModel.SimulatorSteerAngle,
                    hasGuidance, false);
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
        if (MapControl != null && ViewModel != null)
        {
            var activeTrack = ViewModel.SavedTracks.FirstOrDefault(t => t.IsActive);
            MapControl.SetActiveTrack(activeTrack);
        }
    }

    // --- Custom window controls (frameless build). The status strip's
    // background drags the window; the three buttons at its right end do
    // Min / Max-toggle / Close. ---

    private void StatusBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A button or other interactive element inside the strip already
        // handled the press — don't intercept it as a window drag.
        if (e.Handled) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        BeginMoveDrag(e);
    }

    private void WindowMinimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void WindowMaximize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void WindowClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
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
                if (ViewModel?.EnableABClickSelection == true && MapControl != null)
                {
                    var worldPos = MapControl.ScreenToWorld(point.Position.X, point.Position.Y);
                    OnMapClicked(MapControl, new MapClickEventArgs(worldPos.Easting, worldPos.Northing));
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

    // Left, Right, Bottom panels are now anchored (no drag handlers needed)

    // Helper method to check if pointer is over any UI panel
    private bool IsPointerOverUIPanel(PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        // Check anchored panels using their actual rendered bounds
        Control[] panels = { LeftNavPanel, RightNavPanel, SectionControlPanel, BottomNavPanel, SimulatorBarPanel };
        foreach (var panel in panels)
        {
            if (panel?.IsVisible == true && panel.Bounds.Width > 0)
            {
                // TranslatePoint converts from panel-local to window coordinates
                var topLeft = panel.TranslatePoint(new Point(0, 0), this);
                if (topLeft.HasValue)
                {
                    var rect = new Rect(topLeft.Value, panel.Bounds.Size);
                    if (rect.Contains(position))
                        return true;
                }
            }
        }

        return false;
    }

    // AgShare Settings button click
    // Removed: BtnAgShareSettings_Click, BtnAgShareDownload_Click, BtnAgShareUpload_Click
    // These are now handled by ViewModel commands

}