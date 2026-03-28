using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Logging;
using AgValoniaGPS.Views.Controls.Dialogs;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Screenshot capture tests for Phase 2 Quick Win features (#17, #22, #23, #29).
/// Each test renders a dialog in a headless window, verifies dialog state,
/// and attempts to save a PNG screenshot to the test output directory.
///
/// Screenshot capture requires Skia rendering; in headless-only mode (CI)
/// the state assertions still pass while the PNG is skipped gracefully.
/// </summary>
[TestFixture]
public class QuickWinScreenshotTests
{
    private string _screenshotDir = null!;

    [SetUp]
    public void SetUp()
    {
        _screenshotDir = Path.Combine(
            TestContext.CurrentContext.TestDirectory, "screenshots");
        Directory.CreateDirectory(_screenshotDir);
    }

    /// <summary>
    /// Pumps the dispatcher to process pending layout, binding, and render jobs.
    /// Cycles through measure/arrange/render passes to force data-bound content
    /// (ListBox items, ItemsControl, TextBox values) to materialize.
    /// </summary>
    private static void PumpFrames(Window window, int passes = 5)
    {
        for (int i = 0; i < passes; i++)
        {
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
            window.InvalidateMeasure();
            window.InvalidateArrange();
            window.InvalidateVisual();
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
        }
    }

    /// <summary>
    /// Attempts to capture a screenshot. Returns true if file was saved.
    /// Does not throw if headless rendering does not support pixel capture.
    /// </summary>
    private static bool TryCaptureScreenshot(Window window, string fileName, string outputDir)
    {
        PumpFrames(window);

        var path = Path.Combine(outputDir, fileName);
        try
        {
            var frame = window.CaptureRenderedFrame();
            if (frame != null)
            {
                frame.Save(path);
                return File.Exists(path);
            }
        }
        catch (NotSupportedException)
        {
            // Headless without Skia -- fall through to RenderTargetBitmap
        }

        try
        {
            var size = new PixelSize(
                Math.Max(1, (int)window.ClientSize.Width),
                Math.Max(1, (int)window.ClientSize.Height));
            var bitmap = new RenderTargetBitmap(size);
            bitmap.Render(window);
            bitmap.Save(path);
            return File.Exists(path);
        }
        catch
        {
            // Pixel rendering not available in this environment
            TestContext.WriteLine($"[screenshot] Could not capture {fileName} (headless without Skia)");
            return false;
        }
    }

    // --- Theme (#17) ---
    // Uses FlagByLatLon dialog because its TextBoxes respond visibly to FluentTheme variant

    [AvaloniaTest]
    public void Theme_LightMode_Screenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new FlagByLatLonDialogPanel { DataContext = vm };

        var window = new Window
        {
            Content = dialog,
            Width = 800,
            Height = 600
        };
        window.Show();

        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;

        vm.ShowFlagByLatLonDialogCommand!.Execute(null);
        vm.FlagLatitudeInput = "43.653225";
        vm.FlagLongitudeInput = "-79.383186";

        var captured = TryCaptureScreenshot(window, "theme_light.png", _screenshotDir);

        Assert.That(vm.State.UI.IsFlagByLatLonDialogVisible, Is.True);
        if (captured)
            TestContext.WriteLine("[screenshot] theme_light.png saved");
    }

    [AvaloniaTest]
    public void Theme_DarkMode_Screenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new FlagByLatLonDialogPanel { DataContext = vm };

        var window = new Window
        {
            Content = dialog,
            Width = 800,
            Height = 600
        };
        window.Show();

        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;

        vm.ShowFlagByLatLonDialogCommand!.Execute(null);
        vm.FlagLatitudeInput = "43.653225";
        vm.FlagLongitudeInput = "-79.383186";

        var captured = TryCaptureScreenshot(window, "theme_dark.png", _screenshotDir);

        Assert.That(vm.State.UI.IsFlagByLatLonDialogVisible, Is.True);
        if (captured)
            TestContext.WriteLine("[screenshot] theme_dark.png saved");
    }

    // --- Log Viewer (#22) ---

    [AvaloniaTest]
    public void LogViewer_WithEntries_Screenshot()
    {
        LogStore.Instance.Clear();
        LogStore.Instance.Add(new LogEntry
        {
            Timestamp = DateTime.Now.AddSeconds(-10),
            Level = LogLevel.Debug,
            Category = "GpsService",
            Message = "GPS data received: 12 satellites"
        });
        LogStore.Instance.Add(new LogEntry
        {
            Timestamp = DateTime.Now.AddSeconds(-8),
            Level = LogLevel.Information,
            Category = "FieldService",
            Message = "Field 'North40' loaded successfully"
        });
        LogStore.Instance.Add(new LogEntry
        {
            Timestamp = DateTime.Now.AddSeconds(-5),
            Level = LogLevel.Warning,
            Category = "NtripService",
            Message = "NTRIP connection timeout, retrying..."
        });
        LogStore.Instance.Add(new LogEntry
        {
            Timestamp = DateTime.Now.AddSeconds(-2),
            Level = LogLevel.Error,
            Category = "UdpService",
            Message = "Failed to bind UDP port 9999: Address already in use"
        });
        LogStore.Instance.Add(new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Information,
            Category = "MainViewModel",
            Message = "AutoSteer engaged on track 'AB Line 1'"
        });

        var vm = new MainViewModelBuilder().Build();
        var dialog = new LogViewerDialogPanel { DataContext = vm };

        var window = new Window
        {
            Content = dialog,
            Width = 800,
            Height = 600
        };
        window.Show();

        vm.ShowLogViewerDialogCommand!.Execute(null);

        var captured = TryCaptureScreenshot(window, "log_viewer.png", _screenshotDir);

        Assert.That(vm.State.UI.IsLogViewerDialogVisible, Is.True);
        Assert.That(vm.FilteredLogEntries, Has.Count.GreaterThanOrEqualTo(5),
            "Should show all 5 seeded log entries");
        if (captured)
            TestContext.WriteLine("[screenshot] log_viewer.png saved");

        vm.CloseLogViewerDialogCommand!.Execute(null);
    }

    // --- Flag By Lat/Lon (#23) ---

    [AvaloniaTest]
    public void FlagByLatLon_WithCoordinates_Screenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new FlagByLatLonDialogPanel { DataContext = vm };

        var window = new Window
        {
            Content = dialog,
            Width = 800,
            Height = 600
        };
        window.Show();

        vm.ShowFlagByLatLonDialogCommand!.Execute(null);
        vm.FlagLatitudeInput = "43.653225";
        vm.FlagLongitudeInput = "-79.383186";

        var captured = TryCaptureScreenshot(window, "flag_by_latlon.png", _screenshotDir);

        Assert.That(vm.State.UI.IsFlagByLatLonDialogVisible, Is.True);
        Assert.That(vm.FlagLatitudeInput, Is.EqualTo("43.653225"));
        Assert.That(vm.FlagLongitudeInput, Is.EqualTo("-79.383186"));
        if (captured)
            TestContext.WriteLine("[screenshot] flag_by_latlon.png saved");
    }

    // --- View All Settings (#29) ---

    [AvaloniaTest]
    public void ViewAllSettings_Populated_Screenshot()
    {
        var store = ConfigurationStore.Instance;
        store.Vehicle.AntennaHeight = 2.5;
        store.Vehicle.Wheelbase = 3.2;
        store.Tool.Width = 12.0;
        store.Guidance.UTurnRadius = 6.0;
        store.Display.IsDayMode = true;
        store.Display.GridVisible = true;
        store.IsMetric = true;
        store.NumSections = 5;

        var vm = new MainViewModelBuilder().Build();
        var dialog = new ViewSettingsDialogPanel { DataContext = vm };

        var window = new Window
        {
            Content = dialog,
            Width = 800,
            Height = 600
        };
        window.Show();

        vm.ShowViewSettingsDialogCommand!.Execute(null);

        var captured = TryCaptureScreenshot(window, "view_all_settings.png", _screenshotDir);

        Assert.That(vm.State.UI.IsViewSettingsDialogVisible, Is.True);
        Assert.That(vm.SettingsTree, Has.Count.GreaterThan(0),
            "Settings tree should be populated with config groups");
        // Verify specific groups are present
        var groupNames = vm.SettingsTree.Select(g => g.Name).ToList();
        Assert.That(groupNames, Does.Contain("Vehicle"));
        Assert.That(groupNames, Does.Contain("Display"));
        Assert.That(groupNames, Does.Contain("Global"));
        if (captured)
            TestContext.WriteLine("[screenshot] view_all_settings.png saved");
    }

    // --- Light vs Dark comparison for multiple dialogs ---

    [AvaloniaTest]
    public void About_LightMode_Screenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new AboutDialogPanel { DataContext = vm };

        var window = new Window { Content = dialog, Width = 800, Height = 600 };
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        window.Show();

        vm.State.UI.ShowDialog(DialogType.About);
        var captured = TryCaptureScreenshot(window, "about_light.png", _screenshotDir);

        Assert.That(vm.State.UI.IsAboutDialogVisible, Is.True);
        if (captured) TestContext.WriteLine("[screenshot] about_light.png saved");
    }

    [AvaloniaTest]
    public void About_DarkMode_Screenshot()
    {
        var vm = new MainViewModelBuilder().Build();
        var dialog = new AboutDialogPanel { DataContext = vm };

        var window = new Window { Content = dialog, Width = 800, Height = 600 };
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
        window.Show();

        vm.State.UI.ShowDialog(DialogType.About);
        var captured = TryCaptureScreenshot(window, "about_dark.png", _screenshotDir);

        Assert.That(vm.State.UI.IsAboutDialogVisible, Is.True);
        if (captured) TestContext.WriteLine("[screenshot] about_dark.png saved");
    }

    [AvaloniaTest]
    public void LogViewer_LightMode_Screenshot()
    {
        LogStore.Instance.Clear();
        LogStore.Instance.Add(new LogEntry { Timestamp = DateTime.Now.AddSeconds(-6), Level = LogLevel.Debug, Category = "GpsService", Message = "GPS data received: 12 satellites" });
        LogStore.Instance.Add(new LogEntry { Timestamp = DateTime.Now.AddSeconds(-4), Level = LogLevel.Information, Category = "FieldService", Message = "Field 'North40' loaded successfully" });
        LogStore.Instance.Add(new LogEntry { Timestamp = DateTime.Now.AddSeconds(-2), Level = LogLevel.Warning, Category = "NtripService", Message = "NTRIP connection timeout, retrying..." });
        LogStore.Instance.Add(new LogEntry { Timestamp = DateTime.Now, Level = LogLevel.Error, Category = "UdpService", Message = "Failed to bind UDP port 9999: Address already in use" });

        var vm = new MainViewModelBuilder().Build();
        var dialog = new LogViewerDialogPanel { DataContext = vm };

        var window = new Window { Content = dialog, Width = 800, Height = 600 };
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        window.Show();

        vm.ShowLogViewerDialogCommand!.Execute(null);
        var captured = TryCaptureScreenshot(window, "log_viewer_light.png", _screenshotDir);

        Assert.That(vm.FilteredLogEntries, Has.Count.GreaterThanOrEqualTo(4));
        if (captured) TestContext.WriteLine("[screenshot] log_viewer_light.png saved");
        vm.CloseLogViewerDialogCommand!.Execute(null);
    }

    [AvaloniaTest]
    public void ViewAllSettings_LightMode_Screenshot()
    {
        var store = ConfigurationStore.Instance;
        store.Vehicle.AntennaHeight = 2.5;
        store.Vehicle.Wheelbase = 3.2;
        store.IsMetric = true;
        store.NumSections = 5;

        var vm = new MainViewModelBuilder().Build();
        var dialog = new ViewSettingsDialogPanel { DataContext = vm };

        var window = new Window { Content = dialog, Width = 800, Height = 600 };
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        window.Show();

        vm.ShowViewSettingsDialogCommand!.Execute(null);
        var captured = TryCaptureScreenshot(window, "view_all_settings_light.png", _screenshotDir);

        Assert.That(vm.SettingsTree, Has.Count.GreaterThan(0));
        if (captured) TestContext.WriteLine("[screenshot] view_all_settings_light.png saved");
    }

    // --- Whole UI screenshots using shared Full UI layout ---

    private static void CaptureWholeUI(string fileName, ThemeVariant theme, string screenshotDir)
    {
        // Set theme and pump dispatcher so ActualThemeVariant updates
        // BEFORE ThemeBrush() calls in CreateUIOnly()
        if (Application.Current != null)
            Application.Current.RequestedThemeVariant = theme;
        Dispatcher.UIThread.RunJobs();

        var (window, vm) = ScreenshotCaptureTests.CreateUIOnly(theme);
        window.RequestedThemeVariant = theme;
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

        var path = Path.Combine(screenshotDir, fileName);
        ScreenshotCaptureTests.CaptureScreenshot(window,
            ScreenshotCaptureTests.WindowWidth, ScreenshotCaptureTests.WindowHeight, path);

        Assert.That(File.Exists(path), Is.True);
        TestContext.WriteLine($"[screenshot] {fileName} saved ({new FileInfo(path).Length} bytes)");
    }

    [AvaloniaTest]
    public void WholeUI_LightMode_Screenshot()
    {
        CaptureWholeUI("whole_ui_light.png", ThemeVariant.Light, _screenshotDir);
    }

    [AvaloniaTest]
    public void WholeUI_DarkMode_Screenshot()
    {
        CaptureWholeUI("whole_ui_dark.png", ThemeVariant.Dark, _screenshotDir);
    }
}
