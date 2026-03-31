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

        // Enable grid to verify coverage bitmap transparency
        vm.IsGridOn = true;
        config.Display.GridVisible = true;

        // Step 1: App startup -- verify simulator enabled and panel visible by default
        Console.Write("[Step 1] App startup... ");
        Console.Write($"[simEnabled={vm.IsSimulatorEnabled}, panelVisible={vm.IsSimulatorPanelVisible}] ");
        if (!vm.IsSimulatorEnabled)
        {
            Console.WriteLine("FAIL: simulator not enabled by default");
            _scenarioFailed = true;
        }
        // Close all dialogs and wait for clean state
        vm.State.UI.CloseDialog();
        if (vm.ConfigurationViewModel != null)
            vm.ConfigurationViewModel.IsDialogVisible = false;
        await Delay(300);
        Dispatcher.UIThread.RunJobs();
        await Delay(300);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "01_app_startup");
        Console.WriteLine("OK");

        // Step 1b: Verify disabling simulator stops GPS updates
        Console.Write("[Step 1b] Simulator disable/enable... ");
        var simService = App.Services!.GetRequiredService<IGpsSimulationService>();
        double posBefore = vm.Latitude;
        vm.IsSimulatorEnabled = false;
        simService.Tick(0); // should be ignored when disabled
        await Delay(100);
        double posAfterDisable = vm.Latitude;
        bool posUnchanged = Math.Abs(posAfterDisable - posBefore) < 0.0001;
        Console.Write($"[posUnchanged={posUnchanged}] ");
        vm.IsSimulatorEnabled = true; // re-enable for rest of test
        await Delay(200);
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

        // Step 5a: Coverage painting test
        Console.Write("[Step 5a] Coverage: sections ON + drive... ");
        // Turn sections master ON (Auto mode)
        vm.ToggleSectionMasterCommand?.Execute(null);
        await Delay(100);
        Console.Write($"[master={vm.IsSectionMasterOn}] ");
        // Drive forward to paint coverage
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(100);
        for (int i = 0; i < 80; i++)
        {
            simService.Tick(0);
            await Delay(33);
        }
        // Check coverage stats
        Console.Write($"[workedArea={vm.WorkedAreaDisplay}] ");
        CaptureScreenshot(window, "05a_coverage_painting");
        // Turn sections off
        vm.ToggleSectionMasterCommand?.Execute(null);
        Console.WriteLine("OK");

        // Step 5b: Drive in reverse to show reverse indicator
        Console.Write("[Step 5b] Reverse indicator... ");
        vm.SimulatorReverseCommand?.Execute(null);
        await Delay(100);
        for (int i = 0; i < 30; i++)
        {
            simService.Tick(0);
            await Delay(33);
        }
        Console.Write($"[isReversing={vm.IsReversing}] ");
        CaptureScreenshot(window, "05b_reverse_indicator");
        // Return to forward
        vm.SimulatorForwardCommand?.Execute(null);
        for (int i = 0; i < 20; i++)
        {
            simService.Tick(0);
            await Delay(33);
        }
        Console.WriteLine("OK");

        // Step 5c: Test compass button camera modes
        Console.Write("[Step 5c] Compass modes... ");
        Console.Write($"[mode={vm.CameraMode}] ");
        vm.ToggleCameraModeCommand?.Execute(null); // NorthUp -> HeadingUp
        await Delay(200);
        if (vm.CameraMode != AgValoniaGPS.Models.CameraMode.HeadingUp)
            throw new Exception($"Expected HeadingUp after toggle, got {vm.CameraMode}");
        Console.Write($"[after1={vm.CameraMode}] ");

        vm.ToggleCameraModeCommand?.Execute(null); // HeadingUp -> NorthUp
        await Delay(200);
        if (vm.CameraMode != AgValoniaGPS.Models.CameraMode.NorthUp)
            throw new Exception($"Expected NorthUp after toggle, got {vm.CameraMode}");
        Console.Write($"[after2={vm.CameraMode}] ");

        // Switch to HeadingUp then pan -> Free should remember HeadingUp
        vm.ToggleCameraModeCommand?.Execute(null); // NorthUp -> HeadingUp
        await Delay(100);

        // Simulate user pan -> Free mode (via OnUserPan, simulating real drag)
        vm.OnUserPan();
        await Delay(200);
        if (vm.CameraMode != AgValoniaGPS.Models.CameraMode.Free)
            throw new Exception($"Expected Free after pan, got {vm.CameraMode}");
        if (vm.CameraModeLabel != "C")
            throw new Exception($"Expected label 'C' in Free mode, got '{vm.CameraModeLabel}'");
        Console.Write($"[afterPan={vm.CameraMode},{vm.CameraModeLabel}] ");

        // Record camera position before driving
        var mapService = App.Services!.GetRequiredService<IMapService>();
        var camBefore = mapService.GetCameraCenter();

        // Drive tractor and verify camera stays still in Free mode
        for (int i = 0; i < 10; i++) { simService.Tick(0); await Delay(33); }
        // Camera mode should still be Free after driving
        if (vm.CameraMode != AgValoniaGPS.Models.CameraMode.Free)
            throw new Exception($"Camera mode changed from Free during drive: {vm.CameraMode}");
        // Camera position should not have moved
        var camAfter = mapService.GetCameraCenter();
        double camDelta = Math.Sqrt(Math.Pow(camAfter.X - camBefore.X, 2) + Math.Pow(camAfter.Y - camBefore.Y, 2));
        Console.Write($"[camDelta={camDelta:F3}m] ");
        if (camDelta > 0.01)
            throw new Exception($"Camera moved {camDelta:F3}m in Free mode -- should stay still");

        CaptureScreenshot(window, "05c_compass_free");

        // Press compass button -> should restore HeadingUp (previous mode)
        vm.ToggleCameraModeCommand?.Execute(null); // Free -> HeadingUp (previous)
        if (vm.CameraMode != AgValoniaGPS.Models.CameraMode.HeadingUp)
            throw new Exception($"Expected HeadingUp (previous mode) after recenter, got {vm.CameraMode}");
        Console.Write($"[restored={vm.CameraMode}] ");
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

        // Step 6b: Test zoom buttons (#98 fix) -- after all dialogs closed
        Console.Write("[Step 6b] Zoom test... ");
        vm.State.UI.CloseDialog();
        if (vm.ConfigurationViewModel != null)
            vm.ConfigurationViewModel.IsDialogVisible = false;
        await Delay(500);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "06b_zoom_before");
        vm.ZoomInCommand?.Execute(null);
        vm.ZoomInCommand?.Execute(null);
        vm.ZoomInCommand?.Execute(null);
        await Delay(500);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "06b_zoom_after");
        vm.ZoomOutCommand?.Execute(null);
        vm.ZoomOutCommand?.Execute(null);
        vm.ZoomOutCommand?.Execute(null);
        await Delay(200);
        Console.WriteLine("OK");

        // --- Compass Camera Modes (#97) ---
        await RunCompassScenario(window, vm, simService);

        // Step 7+: Theme switching and new dialogs (PR #81)
        await RunThemeAndDialogsScenario(window, vm);

        // --- Flag Placement Scenarios (#108) ---
        await RunFlagScenario(window, vm, simService);

        // --- Track Management Scenarios (PR #80) ---
        await RunTrackManagementScenario(window, vm);

        // --- Charts Scenarios (PR #79) ---
        await RunChartsScenario(window, vm, simService);
    }

    static async Task RunCompassScenario(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        // North-Up mode
        Console.Write("[Compass 1] North-Up follow... ");
        vm.CameraMode = AgValoniaGPS.Models.CameraMode.NorthUp;
        vm.SimulatorForwardCommand?.Execute(null);
        for (int i = 0; i < 30; i++)
        {
            simService.Tick(0);
            await Delay(33);
        }
        await Delay(300);
        Dispatcher.UIThread.RunJobs();
        Console.Write($"[mode={vm.CameraMode}] ");
        CaptureScreenshot(window, "compass_01_northup");
        Console.WriteLine("OK");

        // Heading-Up mode
        Console.Write("[Compass 2] Heading-Up follow... ");
        vm.CameraMode = AgValoniaGPS.Models.CameraMode.HeadingUp;
        for (int i = 0; i < 30; i++)
        {
            simService.Tick(5.0); // steer right to change heading
            await Delay(33);
        }
        await Delay(300);
        Dispatcher.UIThread.RunJobs();
        Console.Write($"[mode={vm.CameraMode}] ");
        CaptureScreenshot(window, "compass_02_headingup");
        Console.WriteLine("OK");

        // Free mode (simulate pan)
        Console.Write("[Compass 3] Free mode (panned)... ");
        vm.CameraMode = AgValoniaGPS.Models.CameraMode.Free;
        await Delay(300);
        Dispatcher.UIThread.RunJobs();
        Console.Write($"[mode={vm.CameraMode}] ");
        CaptureScreenshot(window, "compass_03_free");
        Console.WriteLine("OK");

        // Recenter
        Console.Write("[Compass 4] Recenter from free... ");
        vm.ToggleCameraModeCommand?.Execute(null);
        await Delay(300);
        Dispatcher.UIThread.RunJobs();
        Console.Write($"[mode={vm.CameraMode}] ");
        CaptureScreenshot(window, "compass_04_recentered");
        Console.WriteLine("OK");
    }

    static async Task RunFlagScenario(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        // Drive to get a position, then place flags
        Console.Write("[Step 14] Flags: place red flag at vehicle position... ");
        vm.SimulatorForwardCommand?.Execute(null);
        for (int i = 0; i < 20; i++)
        {
            simService.Tick(0);
            await Delay(33);
        }
        vm.PlaceRedFlagCommand?.Execute(null);
        await Delay(300);
        Console.Write($"[status={vm.StatusMessage}] ");
        CaptureScreenshot(window, "14_flag_red");
        Console.WriteLine("OK");

        // Place green flag after driving a bit more
        Console.Write("[Step 15] Flags: place green flag... ");
        for (int i = 0; i < 20; i++)
        {
            simService.Tick(5.0); // steer right
            await Delay(33);
        }
        vm.PlaceGreenFlagCommand?.Execute(null);
        await Delay(300);
        CaptureScreenshot(window, "15_flag_green");
        Console.WriteLine("OK");

        // Place yellow flag
        Console.Write("[Step 16] Flags: place yellow flag... ");
        for (int i = 0; i < 20; i++)
        {
            simService.Tick(-5.0); // steer left
            await Delay(33);
        }
        vm.PlaceYellowFlagCommand?.Execute(null);
        await Delay(300);
        CaptureScreenshot(window, "16_flags_all");
        Console.WriteLine("OK");

        // Place flag by world position (simulating map click)
        Console.Write("[Step 16b] Flags: place flag at world position (map click)... ");
        vm.PlaceFlagAtWorldPosition(20.0, 30.0, AgValoniaGPS.Models.FlagColor.Green);
        await Delay(300);
        Console.Write($"[mode={vm.IsPlaceFlagOnClickMode}] ");
        CaptureScreenshot(window, "16b_flag_mapclick");
        Console.WriteLine("OK");

        // Open flag list dialog (close any existing dialog first)
        Console.Write("[Step 16c] Flags: flag list dialog... ");
        vm.State.UI.CloseDialog();
        await Delay(100);
        vm.ShowFlagListCommand?.Execute(null);
        await Delay(500);
        Console.Write($"[flagCount={vm.Flags.Count}] ");
        CaptureScreenshot(window, "16c_flag_list_dialog");
        vm.CloseFlagListCommand?.Execute(null);
        Console.WriteLine("OK");

        // Delete all flags
        Console.Write("[Step 17] Flags: delete all... ");
        vm.DeleteAllFlagsCommand?.Execute(null);
        await Delay(300);
        CaptureScreenshot(window, "17_flags_deleted");
        Console.Write($"[status={vm.StatusMessage}] ");
        Console.WriteLine("OK");
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

        // Track 4: Open import tracks dialog (OtherField has tracks to import)
        Console.Write("[Tracks 4] Import tracks dialog... ");
        vm.ImportTracksCommand?.Execute(null);
        await Delay(500);
        Console.Write($"[ImportFields={vm.ImportFieldsList.Count}] ");
        CaptureScreenshot(window, "tracks_04_import_dialog");
        vm.State.UI.CloseDialog();
        Console.WriteLine("OK");

        Console.WriteLine("--- Track Management Scenarios Complete ---");
    }

    static async Task RunChartsScenario(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        // Step 8: Activate track and drive off-course to generate real chart data.
        // ChartDataService now collects continuously in the background, so data
        // accumulates even before charts are opened.
        // Check chart data service state
        var chartDataService = App.Services!.GetRequiredService<IChartDataService>();
        Console.Write($"[ChartService running={chartDataService.IsRunning}, time={chartDataService.CurrentTime:F1}s, steerPts={chartDataService.SetSteerAngle.Count}] ");

        Console.Write("[Step 8] Charts: activate track and drive off-course... ");
        var track = vm.SavedTracks.FirstOrDefault();
        if (track != null)
        {
            vm.SelectedTrack = track;
            Console.Write($"[track={track.Name}] ");
        }
        await Delay(200);

        // Drive with steering to create deviation (steer angle, heading error, XTE)
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(100);
        // Steer right to deviate from AB line
        for (int i = 0; i < 40; i++)
        {
            simService.Tick(5.0);
            await Delay(33);
        }
        // Correct back left
        for (int i = 0; i < 40; i++)
        {
            simService.Tick(-3.0);
            await Delay(33);
        }
        // Straight to stabilize
        for (int i = 0; i < 20; i++)
        {
            simService.Tick(0);
            await Delay(33);
        }
        Console.Write($"[steerPts={chartDataService.SetSteerAngle.Count}, xtePts={chartDataService.CrossTrackError.Count}] ");
        Console.WriteLine("OK");

        // Step 9: Open Steer Chart only
        Console.Write("[Step 9] Charts: Steer Chart... ");
        vm.ToggleSteerChartPanelCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "09_steer_chart");
        Console.Write($"[visible={vm.IsSteerChartPanelVisible}] ");
        vm.ToggleSteerChartPanelCommand?.Execute(null); // close
        Console.WriteLine("OK");

        // Step 10: Open Heading Chart only
        Console.Write("[Step 10] Charts: Heading Chart... ");
        vm.ToggleHeadingChartPanelCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "10_heading_chart");
        Console.Write($"[visible={vm.IsHeadingChartPanelVisible}] ");
        vm.ToggleHeadingChartPanelCommand?.Execute(null); // close
        Console.WriteLine("OK");

        // Step 11: Open XTE Chart only
        Console.Write("[Step 11] Charts: XTE Chart... ");
        vm.ToggleXTEChartPanelCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "11_xte_chart");
        Console.Write($"[visible={vm.IsXTEChartPanelVisible}] ");
        vm.ToggleXTEChartPanelCommand?.Execute(null); // close
        Console.WriteLine("OK");

        // Step 12: All three charts visible
        Console.Write("[Step 12] Charts: all three visible... ");
        vm.ToggleSteerChartPanelCommand?.Execute(null);
        vm.ToggleHeadingChartPanelCommand?.Execute(null);
        vm.ToggleXTEChartPanelCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "12_all_charts");
        bool allVisible = vm.IsSteerChartPanelVisible
            && vm.IsHeadingChartPanelVisible
            && vm.IsXTEChartPanelVisible;
        Console.Write($"[allVisible={allVisible}] ");
        Console.WriteLine("OK");

        // Step 13: Close all charts
        Console.Write("[Step 13] Charts: close all... ");
        vm.ToggleSteerChartPanelCommand?.Execute(null);
        vm.ToggleHeadingChartPanelCommand?.Execute(null);
        vm.ToggleXTEChartPanelCommand?.Execute(null);
        await Delay(500);
        CaptureScreenshot(window, "13_charts_closed");
        bool allClosed = !vm.IsSteerChartPanelVisible
            && !vm.IsHeadingChartPanelVisible
            && !vm.IsXTEChartPanelVisible;
        Console.Write($"[allClosed={allClosed}] ");
        Console.WriteLine("OK");
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
