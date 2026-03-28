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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.Views.Controls.Panels;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Captures before/after screenshot pairs for display toggle verification.
///
/// Three capture modes available per toggle (each test picks what it needs):
///   map/  -- bare map control only (isolates rendering changes)
///   full/ -- map + status bar + all navigation panels (toggle in context)
///   ui/   -- panels and status bar only, no map (checks UI state changes)
/// </summary>
[TestFixture]
public class ScreenshotCaptureTests
{
    internal const int WindowWidth = 1024;
    internal const int WindowHeight = 768;
    private const int MapOnlyWidth = 800;
    private const int MapOnlyHeight = 600;

    [Flags]
    private enum CaptureMode
    {
        Map  = 1,
        Full = 2,
        UI   = 4,
        All  = Map | Full | UI
    }

    private static string ScreenshotBaseDir
    {
        get
        {
            var dir = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "screenshots");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // ---------------------------------------------------------------
    // Layout builders
    // ---------------------------------------------------------------

    private static (Window window, DrawingContextMapControl map) CreateFullUI()
    {
        var vm = new MainViewModelBuilder().Build();
        var mapControl = CreateMapControlWithMockData();

        var panelCanvas = CreatePanelCanvas(vm);
        var zoomButtons = CreateZoomButtons();

        var mapArea = new Grid();
        mapArea.Children.Add(mapControl);
        mapArea.Children.Add(panelCanvas);
        mapArea.Children.Add(zoomButtons);

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = ThemeBrush("SystemControlBackgroundAltHighBrush", "#1a1a1a")
        };
        var statusBar = CreateStatusBar();
        Grid.SetRow(statusBar, 0);
        Grid.SetRow(mapArea, 1);
        rootGrid.Children.Add(statusBar);
        rootGrid.Children.Add(mapArea);

        var window = new Window
        {
            Content = rootGrid,
            Width = WindowWidth,
            Height = WindowHeight,
            SizeToContent = SizeToContent.Manual
        };

        return (window, mapControl);
    }

    private static (Window window, DrawingContextMapControl map) CreateMapOnly()
    {
        var mapControl = CreateMapControlWithMockData();

        var window = new Window
        {
            Content = mapControl,
            Width = MapOnlyWidth,
            Height = MapOnlyHeight,
            SizeToContent = SizeToContent.Manual
        };

        return (window, mapControl);
    }

    internal static (Window window, MainViewModel vm) CreateUIOnly()
    {
        var vm = new MainViewModelBuilder().Build();

        var panelCanvas = CreatePanelCanvas(vm);
        var zoomButtons = CreateZoomButtons();

        var placeholder = new Border
        {
            Background = ThemeBrush("SystemControlBackgroundAltHighBrush", "#1a1a1a")
        };

        var mapArea = new Grid();
        mapArea.Children.Add(placeholder);
        mapArea.Children.Add(panelCanvas);
        mapArea.Children.Add(zoomButtons);

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = ThemeBrush("SystemControlBackgroundAltHighBrush", "#1a1a1a")
        };
        var statusBar = CreateStatusBar();
        Grid.SetRow(statusBar, 0);
        Grid.SetRow(mapArea, 1);
        rootGrid.Children.Add(statusBar);
        rootGrid.Children.Add(mapArea);

        var window = new Window
        {
            Content = rootGrid,
            Width = WindowWidth,
            Height = WindowHeight,
            SizeToContent = SizeToContent.Manual
        };

        return (window, vm);
    }

    // ---------------------------------------------------------------
    // Shared UI component builders
    // ---------------------------------------------------------------

    /// <summary>
    /// Resolves a theme-aware brush by resource key using the active theme variant.
    /// Falls back to the provided default if the resource is not found.
    /// </summary>
    internal static IBrush ThemeBrush(string key, string fallback = "#1a1a1a")
    {
        var app = Application.Current;
        if (app != null)
        {
            var variant = app.ActualThemeVariant;
            if (app.TryGetResource(key, variant, out var value) && value is IBrush brush)
                return brush;
        }
        return new SolidColorBrush(Color.Parse(fallback));
    }

    private static Border CreateStatusBar()
    {
        return new Border
        {
            Background = ThemeBrush("SystemControlBackgroundChromeMediumBrush", "#2d2d2d"),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 15,
                Children =
                {
                    new TextBlock { Text = "Ready", Foreground = Brushes.LimeGreen, FontSize = 14 },
                    new TextBlock { Text = "RTK Fix", Foreground = ThemeBrush("SystemControlHighlightAccentBrush", "#3498DB"), FontSize = 12 },
                    new TextBlock { Text = "FPS: 30", Foreground = new SolidColorBrush(Color.Parse("#F1C40F")), FontSize = 12 },
                    new TextBlock { Text = "Lat: 2.1ms", Foreground = new SolidColorBrush(Color.Parse("#F39C12")), FontSize = 12 },
                    new Border { Width = 200 },
                    new TextBlock { Text = "12.4 ha", Foreground = ThemeBrush("SystemControlForegroundBaseHighBrush", "#FFFFFF"), FontSize = 12 },
                    new TextBlock { Text = "5.2 ha done", Foreground = Brushes.LimeGreen, FontSize = 12, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = "58%", Foreground = new SolidColorBrush(Color.Parse("#F39C12")), FontSize = 12 },
                }
            }
        };
    }

    private static Canvas CreatePanelCanvas(MainViewModel vm)
    {
        var leftNav = new LeftNavigationPanel { DataContext = vm };
        Canvas.SetLeft(leftNav, 10);
        Canvas.SetTop(leftNav, 10);

        var rightNav = new RightNavigationPanel { DataContext = vm };
        Canvas.SetLeft(rightNav, WindowWidth - 80);
        Canvas.SetTop(rightNav, 10);

        var bottomNav = new BottomNavigationPanel { DataContext = vm };
        Canvas.SetLeft(bottomNav, 150);
        Canvas.SetTop(bottomNav, WindowHeight - 160);

        var sectionCtrl = new SectionControlPanel { DataContext = vm };
        Canvas.SetLeft(sectionCtrl, 150);
        Canvas.SetTop(sectionCtrl, WindowHeight - 220);

        var canvas = new Canvas { ZIndex = 10 };
        canvas.Children.Add(leftNav);
        canvas.Children.Add(rightNav);
        canvas.Children.Add(bottomNav);
        canvas.Children.Add(sectionCtrl);
        return canvas;
    }

    private static StackPanel CreateZoomButtons()
    {
        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 20, 20),
            Spacing = 10,
            Children =
            {
                new Button
                {
                    Content = "+", Width = 50, Height = 50, FontSize = 28,
                    FontWeight = FontWeight.Bold,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = ThemeBrush("SystemControlBackgroundBaseLowBrush", "#4A5568"),
                    Foreground = ThemeBrush("SystemControlForegroundBaseHighBrush", "#FFFFFF"),
                    CornerRadius = new CornerRadius(8)
                },
                new Button
                {
                    Content = "-", Width = 50, Height = 50, FontSize = 28,
                    FontWeight = FontWeight.Bold,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = ThemeBrush("SystemControlBackgroundBaseLowBrush", "#4A5568"),
                    Foreground = ThemeBrush("SystemControlForegroundBaseHighBrush", "#FFFFFF"),
                    CornerRadius = new CornerRadius(8)
                },
            }
        };
    }

    // ---------------------------------------------------------------
    // Mock data
    // ---------------------------------------------------------------

    private static DrawingContextMapControl CreateMapControlWithMockData()
    {
        var control = new DrawingContextMapControl();

        var boundary = new Boundary
        {
            OuterBoundary = CreateRectangularPolygon(-50, -40, 50, 40)
        };
        control.SetBoundary(boundary);

        control.SetVehiclePosition(0, 0, 0);
        control.SetToolPosition(0, -3, 0, 6.0, 0, -3);

        control.SetSectionStates(
            sectionOn: new[] { true, true, true },
            sectionWidths: new[] { 2.0, 2.0, 2.0 },
            numSections: 3,
            buttonStates: new[] { 1, 1, 1 });

        control.SetCoveragePatches(CreateMockCoveragePatches());

        // AB line offset to easting=10 so it doesn't hide the red vertical axis line
        var track = Track.FromABLine("Test AB",
            new Vec3(10, -50, 0),
            new Vec3(10, 50, 0));
        control.SetActiveTrack(track);

        control.SetCamera(0, 0, 2.0, 0);
        control.SetDayMode(true);
        control.SetGridVisible(true);

        return control;
    }

    private static BoundaryPolygon CreateRectangularPolygon(
        double minE, double minN, double maxE, double maxN)
    {
        var polygon = new BoundaryPolygon();
        polygon.Points.Add(new BoundaryPoint(minE, minN, 0));
        polygon.Points.Add(new BoundaryPoint(maxE, minN, Math.PI / 2));
        polygon.Points.Add(new BoundaryPoint(maxE, maxN, Math.PI));
        polygon.Points.Add(new BoundaryPoint(minE, maxN, 3 * Math.PI / 2));
        polygon.UpdateBounds();
        return polygon;
    }

    private static List<CoveragePatch> CreateMockCoveragePatches()
    {
        var patches = new List<CoveragePatch>();
        // 3 passes with 2m gaps between them.
        // Each pass is 6m wide (matching 6m tool: 3 sections x 2m).
        // Spacing = 8m so passes don't touch: gap = 8 - 6 = 2m.
        double[] passEastings = { -8, 0, 8 };

        foreach (var easting in passEastings)
        {
            var patch = new CoveragePatch
            {
                Color = CoverageColor.Default,
                IsActive = false
            };

            // First vertex encodes the patch color (convention used by the renderer)
            patch.Vertices.Add(patch.Color.ToVec3());

            // halfWidth = 3m matches total tool width of 6m
            double halfWidth = 3.0;
            for (double n = -30; n <= 30; n += 5)
            {
                patch.Vertices.Add(new Vec3(easting - halfWidth, n, 0));
                patch.Vertices.Add(new Vec3(easting + halfWidth, n, 0));
            }

            patches.Add(patch);
        }

        return patches;
    }

    // ---------------------------------------------------------------
    // Screenshot capture helpers
    // ---------------------------------------------------------------

    internal static void CaptureScreenshot(Window window, int width, int height, string filePath)
    {
        window.UpdateLayout();

        var renderTarget = new RenderTargetBitmap(
            new PixelSize(width, height), new Vector(96, 96));
        renderTarget.Render(window);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        renderTarget.Save(filePath);
    }

    private static void AssertScreenshotExists(string path, string label)
    {
        Assert.That(File.Exists(path), Is.True, $"{label} screenshot not created: {path}");
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), $"{label} screenshot is empty");
    }

    /// <summary>
    /// Captures ON/OFF screenshots in the requested modes for a map toggle.
    /// </summary>
    private static void CaptureToggle(
        string toggleName,
        CaptureMode modes,
        Action<DrawingContextMapControl> setOn,
        Action<DrawingContextMapControl> setOff)
    {
        var baseDir = ScreenshotBaseDir;

        if (modes.HasFlag(CaptureMode.Map))
        {
            var (window, map) = CreateMapOnly();
            window.Show();
            window.UpdateLayout();

            setOn(map);
            map.InvalidateVisual();
            var onPath = Path.Combine(baseDir, "map", $"{toggleName}_ON.png");
            CaptureScreenshot(window, MapOnlyWidth, MapOnlyHeight, onPath);

            setOff(map);
            map.InvalidateVisual();
            var offPath = Path.Combine(baseDir, "map", $"{toggleName}_OFF.png");
            CaptureScreenshot(window, MapOnlyWidth, MapOnlyHeight, offPath);

            window.Close();
            AssertScreenshotExists(onPath, "map/ON");
            AssertScreenshotExists(offPath, "map/OFF");
            TestContext.Out.WriteLine($"[{toggleName}] map/ON:  {onPath}");
            TestContext.Out.WriteLine($"[{toggleName}] map/OFF: {offPath}");
        }

        if (modes.HasFlag(CaptureMode.Full))
        {
            var (window, map) = CreateFullUI();
            window.Show();
            window.UpdateLayout();

            setOn(map);
            map.InvalidateVisual();
            var onPath = Path.Combine(baseDir, "full", $"{toggleName}_ON.png");
            CaptureScreenshot(window, WindowWidth, WindowHeight, onPath);

            setOff(map);
            map.InvalidateVisual();
            var offPath = Path.Combine(baseDir, "full", $"{toggleName}_OFF.png");
            CaptureScreenshot(window, WindowWidth, WindowHeight, offPath);

            window.Close();
            AssertScreenshotExists(onPath, "full/ON");
            AssertScreenshotExists(offPath, "full/OFF");
            TestContext.Out.WriteLine($"[{toggleName}] full/ON:  {onPath}");
            TestContext.Out.WriteLine($"[{toggleName}] full/OFF: {offPath}");
        }

        if (modes.HasFlag(CaptureMode.UI))
        {
            var (window, _) = CreateUIOnly();
            window.Show();
            window.UpdateLayout();

            var onPath = Path.Combine(baseDir, "ui", $"{toggleName}_ON.png");
            CaptureScreenshot(window, WindowWidth, WindowHeight, onPath);

            var offPath = Path.Combine(baseDir, "ui", $"{toggleName}_OFF.png");
            CaptureScreenshot(window, WindowWidth, WindowHeight, offPath);

            window.Close();
            AssertScreenshotExists(onPath, "ui/ON");
            AssertScreenshotExists(offPath, "ui/OFF");
            TestContext.Out.WriteLine($"[{toggleName}] ui/ON:  {onPath}");
            TestContext.Out.WriteLine($"[{toggleName}] ui/OFF: {offPath}");
        }
    }

    // ---------------------------------------------------------------
    // Toggle tests -- implemented in DrawingContextMapControl
    // Map-only toggles: capture map + full (UI panels don't change)
    // ---------------------------------------------------------------

    [AvaloniaTest]
    public void Capture_GridVisible_Toggle()
    {
        CaptureToggle(
            "GridVisible",
            CaptureMode.Map | CaptureMode.Full,
            ctrl => ctrl.SetGridVisible(true),
            ctrl => ctrl.SetGridVisible(false));
    }

    [AvaloniaTest]
    public void Capture_IsDayMode_Toggle()
    {
        CaptureToggle(
            "IsDayMode",
            CaptureMode.Map | CaptureMode.Full | CaptureMode.UI,
            ctrl => ctrl.SetDayMode(true),
            ctrl => ctrl.SetDayMode(false));
    }

    [AvaloniaTest]
    public void Capture_IsNorthUp_Toggle()
    {
        // Heading PI/4 (45 deg) so north-up vs track-up rotation is clearly visible.
        // Tool heading must match vehicle heading so the assembly looks correct.
        double heading = Math.PI / 4;
        CaptureToggle(
            "IsNorthUp",
            CaptureMode.Map | CaptureMode.Full,
            ctrl =>
            {
                ctrl.SetVehiclePosition(0, 0, heading);
                ctrl.SetToolPosition(0, -3, heading, 6.0, 0, -3);
                ctrl.SetNorthUp(true);
            },
            ctrl =>
            {
                ctrl.SetVehiclePosition(0, 0, heading);
                ctrl.SetToolPosition(0, -3, heading, 6.0, 0, -3);
                ctrl.SetNorthUp(false);
            });
    }

    // ---------------------------------------------------------------
    // Toggle tests -- NOT YET implemented in DrawingContextMapControl
    // These exist in DisplayConfig but the renderer does not read them yet.
    // Each test is marked [Explicit] so it does not fail CI.
    // Remove [Explicit] once the renderer implements the toggle.
    //
    // These will need all 3 modes once implemented because some may
    // also affect UI button states.
    // ---------------------------------------------------------------

    [AvaloniaTest]
    [Explicit("SectionLinesVisible not yet wired in DrawingContextMapControl")]
    public void Capture_SectionLinesVisible_Toggle()
    {
        CaptureToggle(
            "SectionLinesVisible",
            CaptureMode.Map | CaptureMode.Full,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.SectionLinesVisible = true,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.SectionLinesVisible = false);
    }

    [AvaloniaTest]
    [Explicit("SvennArrowVisible not yet wired in DrawingContextMapControl")]
    public void Capture_SvennArrowVisible_Toggle()
    {
        CaptureToggle(
            "SvennArrowVisible",
            CaptureMode.Map | CaptureMode.Full,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.SvennArrowVisible = true,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.SvennArrowVisible = false);
    }

    [AvaloniaTest]
    [Explicit("PolygonsVisible not yet wired in DrawingContextMapControl")]
    public void Capture_PolygonsVisible_Toggle()
    {
        CaptureToggle(
            "PolygonsVisible",
            CaptureMode.Map | CaptureMode.Full,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.PolygonsVisible = true,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.PolygonsVisible = false);
    }

    [AvaloniaTest]
    [Explicit("DirectionMarkersVisible not yet wired in DrawingContextMapControl")]
    public void Capture_DirectionMarkersVisible_Toggle()
    {
        CaptureToggle(
            "DirectionMarkersVisible",
            CaptureMode.Map | CaptureMode.Full,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.DirectionMarkersVisible = true,
            ctrl => Models.Configuration.ConfigurationStore.Instance.Display.DirectionMarkersVisible = false);
    }
}
