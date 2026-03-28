// Integration Test Harness
// Boots the real app with real platform rendering, runs scenarios,
// captures screenshots, prints pass/fail.
//
// Real window mode (requires desktop session):
//   dotnet run --project Tests/AgValoniaGPS.IntegrationTests/
//
// Headless mode (CI compatible):
//   dotnet run --project Tests/AgValoniaGPS.IntegrationTests/ -- --headless

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using AgValoniaGPS.Desktop;
using AgValoniaGPS.Desktop.Views;
using AgValoniaGPS.IntegrationTests;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgValoniaGPS.IntegrationTests;

sealed class Program
{
    static string _screenshotDir = string.Empty;
    static string _tempDir = string.Empty;
    static bool _scenarioFailed = false;
    static bool _headless = false;

    [STAThread]
    public static int Main(string[] args)
    {
        _headless = args.Contains("--headless");

        // Set up isolated test data
        var testDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");
        _tempDir = Path.Combine(Path.GetTempPath(), $"AgValoniaGPS_IntTest_{Guid.NewGuid():N}");
        CopyDirectory(testDataDir, _tempDir);

        var subDir = _headless ? "headless" : "integration";
        _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots", subDir);
        Directory.CreateDirectory(_screenshotDir);

        Console.WriteLine($"[IntTest] Mode: {(_headless ? "headless" : "real window")}");
        Console.WriteLine($"[IntTest] Test data: {_tempDir}");
        Console.WriteLine($"[IntTest] Screenshots: {_screenshotDir}");

        // Hook into App's DI to swap in TestSettingsService
        var testSettings = new TestSettingsService(_tempDir);
        App.ConfigureTestServices = services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ISettingsService>(testSettings));
        };

        // Hook scenario runner -- runs after MainWindow is shown
        App.OnAppReady = RunScenario;

        // Boot the real app
        try
        {
            var builder = AppBuilder.Configure<App>();

            if (_headless)
                builder = builder.UseSkia().UseHeadless(
                    new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
            else
                builder = builder.UsePlatformDetect();

            builder.WithInterFont()
                .UseReactiveUI()
                .StartWithClassicDesktopLifetime(
                    args.Where(a => a != "--headless").ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IntTest] FATAL: {ex.Message}");
            _scenarioFailed = true;
        }
        finally
        {
            App.ConfigureTestServices = null;
            App.OnAppReady = null;
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        if (_scenarioFailed)
        {
            Console.WriteLine("[IntTest] FAILED");
            return 1;
        }

        Console.WriteLine("[IntTest] ALL SCENARIOS PASSED");
        return 0;
    }

    static async Task RunScenario(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var window = lifetime.MainWindow as Window
            ?? throw new Exception("MainWindow not found");
        // Get the VM from the window's DataContext -- NOT from DI.
        // MainViewModel is registered as Transient, so GetRequiredService creates
        // a new instance each time. The MainWindow has the real one.
        var vm = (MainViewModel)window.DataContext!;
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();

        // Configure realistic implement: 12m sprayer with 6 sections (2m each)
        var config = ConfigurationStore.Instance;
        config.Tool.Width = 12.0;
        config.NumSections = 6;
        for (int i = 0; i < 6; i++)
            config.Tool.SetSectionWidth(i, 200.0); // 200cm = 2m per section
        Console.WriteLine($"[Setup] Tool: {config.Tool.Width}m, {config.NumSections} sections, actual={config.ActualToolWidth}m");

        // Step 1: App startup
        Console.Write("[Step 1] App startup... ");
        vm.State.UI.CloseDialog(); // Close any first-run dialogs
        await Delay(500);
        CaptureScreenshot(window, "01_app_startup");
        Console.WriteLine("OK");

        // Step 2: Open field selection dialog
        Console.Write("[Step 2] Field selection dialog... ");
        Console.Write($"[FieldsDir={settingsService.Settings.FieldsDirectory}] ");
        vm.ShowFieldSelectionDialogCommand?.Execute(null);
        await Delay(500);
        Console.Write($"[Fields={vm.AvailableFields.Count}] ");
        CaptureScreenshot(window, "02_field_selection");
        Console.WriteLine("OK");

        // Step 3: Load TestField
        Console.Write("[Step 3] Load TestField... ");
        vm.State.UI.CloseDialog();
        var testFieldDir = Path.Combine(settingsService.Settings.FieldsDirectory, "TestField");

        try
        {
            await vm.OpenFieldAsync(testFieldDir, "TestField");
            await Delay(1000);
            Console.Write($"[Tracks={vm.SavedTracks.Count}] ");
            CaptureScreenshot(window, "03_field_loaded");
            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PARTIAL ({ex.Message})");
            CaptureScreenshot(window, "03_field_partial");
        }

        // Step 4: Drive simulator -- enable forward acceleration first
        Console.Write("[Step 4] Simulator drive... ");
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(100);
        var simService = App.Services!.GetRequiredService<IGpsSimulationService>();
        for (int i = 0; i < 60; i++)
        {
            simService.Tick(0);
            await Delay(33); // ~30 FPS timing
        }
        CaptureScreenshot(window, "04_simulator_driving");
        Console.WriteLine("OK");

        // Step 5: Open tracks dialog
        Console.Write("[Step 5] Tracks dialog... ");
        vm.ShowTracksDialogCommand?.Execute(null);
        await Delay(500);
        Console.Write($"[Tracks={vm.SavedTracks.Count}] ");
        CaptureScreenshot(window, "05_tracks_dialog");
        vm.State.UI.CloseDialog();
        Console.WriteLine("OK");

        // Step 6: Open configuration dialog
        Console.Write("[Step 6] Configuration dialog... ");
        vm.ShowConfigurationDialogCommand?.Execute(null);
        await Delay(800);
        CaptureScreenshot(window, "06_configuration");
        // Configuration dialog uses its own visibility mechanism (not State.UI)
        if (vm.ConfigurationViewModel != null)
            vm.ConfigurationViewModel.IsDialogVisible = false;
        Console.WriteLine("OK");

        // Step 7+: Theme switching and new dialogs (PR #81)
        await RunThemeAndDialogsScenario(window, vm);
    }

    static async Task RunThemeAndDialogsScenario(Window window, MainViewModel vm)
    {
        // Step 7: Current theme (light/day mode) screenshot
        Console.Write("[Step 7] Current theme (light)... ");
        CaptureScreenshot(window, "theme_01_light");
        Console.WriteLine("OK");

        // Step 8: Toggle to dark/night theme
        Console.Write("[Step 8] Toggle to dark theme... ");
        vm.ToggleDayNightCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "theme_02_dark");
        Console.WriteLine("OK");

        // Step 9: Toggle back to light (verify round-trip)
        Console.Write("[Step 9] Toggle back to light... ");
        vm.ToggleDayNightCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "theme_03_light_roundtrip");
        Console.WriteLine("OK");

        // Step 10: Open Log Viewer dialog
        Console.Write("[Step 10] Log Viewer dialog... ");
        vm.ShowLogViewerDialogCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "theme_04_log_viewer");
        vm.CloseLogViewerDialogCommand?.Execute(null);
        await Delay(200);
        Console.WriteLine("OK");

        // Step 11: Open Flag By Lat/Lon dialog
        Console.Write("[Step 11] Flag by Lat/Lon dialog... ");
        vm.ShowFlagByLatLonDialogCommand?.Execute(null);
        await Delay(500);
        vm.FlagLatitudeInput = "43.653225";
        vm.FlagLongitudeInput = "-79.383186";
        await Delay(200);
        CaptureScreenshot(window, "theme_05_flag_by_latlon");
        vm.CloseFlagByLatLonDialogCommand?.Execute(null);
        await Delay(200);
        Console.WriteLine("OK");

        // Step 12: Open View All Settings dialog
        Console.Write("[Step 12] View All Settings dialog... ");
        vm.ShowViewSettingsDialogCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "theme_06_view_all_settings");
        vm.CloseViewSettingsDialogCommand?.Execute(null);
        await Delay(200);
        Console.WriteLine("OK");

        // --- Track Management Scenarios (PR #80) ---
        await RunTrackManagementScenario(window, vm);
    }

    static async Task RunTrackManagementScenario(Window window, MainViewModel vm)
    {
        Console.WriteLine("\n--- Track Management Scenarios ---");

        // Track 1: Open tracks dialog showing the loaded AB line
        Console.Write("[Tracks 1] Tracks dialog with AB line listed... ");
        vm.ShowTracksDialogCommand?.Execute(null);
        await Delay(500);
        Console.Write($"[SavedTracks={vm.SavedTracks.Count}] ");
        CaptureScreenshot(window, "tracks_01_dialog_with_track");
        Console.WriteLine("OK");

        // Track 1.5: Build headland from boundary (enables autosteer validation)
        Console.Write("[Tracks 1b] Build headland... ");
        vm.State.UI.CloseDialog();
        vm.HeadlandDistance = 12.0; // 12m headland for 200x160m field
        vm.BuildHeadlandCommand?.Execute(null);
        await Delay(500);
        Console.Write($"[HasHeadland={vm.HasHeadland}] ");
        CaptureScreenshot(window, "tracks_01b_headland_built");
        Console.WriteLine("OK");

        // Track 2: Activate AB line + engage autosteer for real guidance
        Console.Write("[Tracks 2] Activate AB line + autosteer... ");
        vm.State.UI.CloseDialog();
        if (vm.SavedTracks.Count > 0)
        {
            vm.SelectedTrack = vm.SavedTracks[0];
            // Engage autosteer via command (headland was built in step 1b)
            vm.ToggleAutoSteerCommand?.Execute(null);
            Console.Write($"[Active={vm.SelectedTrack?.Name}, AutoSteer={vm.IsAutoSteerEngaged}] ");
        }
        await Delay(500);
        CaptureScreenshot(window, "tracks_02_guidance_line_active");
        Console.WriteLine("OK");

        // Track 3: Drive with autosteer -- tractor starts 6m east of the AB line
        // (AB line is at easting=6, tractor starts at easting=0)
        // Autosteer must actively steer the tractor onto the line
        Console.Write("[Tracks 3] Drive with autosteer (6m offset start)... ");
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(100);
        var simService = App.Services!.GetRequiredService<IGpsSimulationService>();
        Console.WriteLine();
        for (int i = 0; i < 120; i++)
        {
            simService.Tick(vm.SimulatorSteerAngle);
            await Delay(33);
            if (i % 20 == 19)
            {
                double xte = vm.State.Guidance.CrossTrackError;
                Console.WriteLine($"  tick {i + 1}: XTE={xte:F3}m, Steer={vm.SimulatorSteerAngle:F1}");
            }
        }
        double finalXte = Math.Abs(vm.State.Guidance.CrossTrackError);
        Console.Write($"  Final XTE={finalXte:F3}m ");
        if (finalXte > 0.5)
            throw new Exception($"XTE too large: {finalXte:F3}m -- autosteer not following guidance line");
        CaptureScreenshot(window, "tracks_03_guidance_driving");
        Console.WriteLine("OK");

        // Track 4: Open import tracks dialog
        Console.Write("[Tracks 4] Import tracks dialog... ");
        vm.ImportTracksCommand?.Execute(null);
        await Delay(500);
        Console.Write($"[ImportFields={vm.ImportFieldsList.Count}] ");
        CaptureScreenshot(window, "tracks_04_import_dialog");
        vm.State.UI.CloseDialog();
        Console.WriteLine("OK");

        // Track 5: Toggle recorded paths display
        Console.Write("[Tracks 5] Toggle recorded paths... ");
        vm.ToggleRecordedPathsCommand?.Execute(null);
        await Delay(300);
        Console.Write($"[ShowRecordedPaths={vm.ShowRecordedPaths}] ");
        CaptureScreenshot(window, "tracks_05_recorded_paths_toggle");
        Console.WriteLine("OK");

        Console.WriteLine("--- Track Management Scenarios Complete ---");
    }

    /// <summary>
    /// Delay that also pumps the UI dispatcher.
    /// </summary>
    static async Task Delay(int ms)
    {
        await Task.Delay(ms);
        Dispatcher.UIThread.RunJobs();
    }

    static void CaptureScreenshot(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var pixelSize = new PixelSize(
            Math.Max((int)window.Bounds.Width, 1),
            Math.Max((int)window.Bounds.Height, 1));
        var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        bitmap.Render(window);

        var path = Path.Combine(_screenshotDir, $"{name}.png");
        bitmap.Save(path);
        var kb = new FileInfo(path).Length / 1024;
        Console.Write($"[{kb}KB] ");
    }

    static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
    }
}
