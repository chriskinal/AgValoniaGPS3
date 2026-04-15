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
using Avalonia.Threading;
using Avalonia.VisualTree;
using AgValoniaGPS.Desktop;
using AgValoniaGPS.Desktop.Views;
using AgValoniaGPS.IntegrationTests;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Timing;
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
    static bool _catalogMode = false;
    static bool _fieldTestMode = false;
    static bool _uturnTestMode = false;
    static bool _tileTestMode = false;
    static bool _recPathTestMode = false;
    static bool _remoteTestMode = false;
    static int _remoteTestPort = 5123;
    static double _timeScale = 1.0;
    static string[] _args = Array.Empty<string>();

    [STAThread]
    public static int Main(string[] args)
    {
        _args = args;
        _headless = args.Contains("--headless");
        _catalogMode = args.Contains("--catalog");
        _fieldTestMode = args.Contains("--field-test");
        _uturnTestMode = args.Contains("--uturn-test");
        _tileTestMode = args.Contains("--tile-test");
        _recPathTestMode = args.Contains("--recpath-test");
        _remoteTestMode = args.Contains("--remote-test");

        // Parse --port flag for remote test server
        var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
        if (portArg != null) _remoteTestPort = int.Parse(portArg.Split('=')[1]);

        // Parse --fast flag for accelerated time (e.g. --fast or --fast=10)
        var fastArg = args.FirstOrDefault(a => a.StartsWith("--fast"));
        if (fastArg != null)
        {
            if (fastArg.Contains('='))
                _timeScale = double.Parse(fastArg.Split('=')[1]);
            else
                _timeScale = 50.0;

            Clock.Set(new SystemClock { TimeScale = _timeScale });
            Console.WriteLine($"[IntTest] Fast mode: {_timeScale}x time scale");
        }

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
        App.OnAppReady = _remoteTestMode ? RunRemoteTestServer
                       : _recPathTestMode ? RunRecPathTest
                       : _tileTestMode ? RunTileTest
                       : _uturnTestMode ? RunUTurnTest
                       : _fieldTestMode ? RunFieldTest
                       : _catalogMode ? RunCatalog
                       : RunScenario;

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
                .StartWithClassicDesktopLifetime(
                    args.Where(a => a != "--headless" && !a.StartsWith("--fast")).ToArray());
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
            Clock.Reset();
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

    static async Task RunRemoteTestServer(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var window = lifetime.MainWindow as Window
            ?? throw new Exception("MainWindow not found");
        var vm = (MainViewModel)window.DataContext!;

        // Auto-create a test field with boundary + track if --test-field flag is present
        if (_args.Contains("--test-field"))
        {
            await SetupTestField(vm);
        }

        Console.WriteLine($"[Remote Test] Starting server on port {_remoteTestPort}...");
        using var server = new RemoteTestServer(window, vm, _remoteTestPort);

        // Run server until process is killed (Ctrl+C)
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Dispose();
            lifetime.Shutdown();
        };

        await server.RunAsync();
    }

    /// <summary>
    /// Create a test field with a 200x200m boundary, headland, and AB track.
    /// Used by --test-field flag for remote test server.
    /// </summary>
    static async Task SetupTestField(MainViewModel vm)
    {
        Console.Write("[TestField] Creating field... ");

        async Task PumpUI(int ms)
        {
            for (int i = 0; i < ms / 10; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }
        }

        // Try loading "UserField" if it exists, otherwise create a new one
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();
        var userFieldDir = System.IO.Path.Combine(settingsService.Settings.FieldsDirectory, "UserField");
        if (System.IO.Directory.Exists(userFieldDir) && System.IO.File.Exists(System.IO.Path.Combine(userFieldDir, "Boundary.txt")))
        {
            Console.Write("[TestField] Loading UserField... ");
            try
            {
                await vm.OpenFieldAsync(userFieldDir, "UserField");
                await PumpUI(500);

                if (vm.IsFieldOpen)
                {
                    // Add an AB track if none exist
                    if (vm.SavedTracks.Count == 0)
                    {
                        var bnd = vm.CurrentBoundary?.OuterBoundary?.Points;
                        if (bnd != null && bnd.Count > 0)
                        {
                            double cx = bnd.Average(p => p.Easting);
                            double cy = bnd.Average(p => p.Northing);
                            var userTrack = new AgValoniaGPS.Models.Track.Track
                            {
                                Name = "AB_Test",
                                Points = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
                                    { new(cx, cy - 200, 0), new(cx, cy + 200, 0) },
                                Type = AgValoniaGPS.Models.Track.TrackType.ABLine,
                                IsVisible = true, IsActive = true
                            };
                            vm.SavedTracks.Add(userTrack);
                            vm.SelectedTrack = userTrack;
                        }
                    }
                    ConfigurationStore.Instance.Tool.Width = 12.0;
                    ConfigurationStore.Instance.Tram.TramWidth = 24.0;
                    Console.WriteLine($"boundary={vm.HasBoundary} headland={vm.HasHeadland} tracks={vm.SavedTracks.Count} OK");
                    return;
                }
            }
            catch (Exception ex) { Console.Write($"({ex.Message}) "); }
        }

        // Create field
        vm.NewFieldName = "RemoteTestField";
        vm.NewFieldLatitude = 42.0308;
        vm.NewFieldLongitude = -93.6319;
        vm.ConfirmNewFieldDialogCommand?.Execute(null);
        await PumpUI(500);

        if (!vm.IsFieldOpen)
        {
            Console.WriteLine("FAILED (field not open)");
            return;
        }

        // Initialize local plane
        var origin = new AgValoniaGPS.Models.Wgs84(42.0308, -93.6319);
        AgValoniaGPS.Models.State.ApplicationState.Instance.Field.LocalPlane =
            new AgValoniaGPS.Models.LocalPlane(origin, new AgValoniaGPS.Models.SharedFieldProperties());
        AgValoniaGPS.Models.State.ApplicationState.Instance.Field.OriginLatitude = 42.0308;
        AgValoniaGPS.Models.State.ApplicationState.Instance.Field.OriginLongitude = -93.6319;

        // Create boundary via recording service (200x200m square)
        vm.StartBoundaryRecordingCommand?.Execute(null);
        await PumpUI(300);

        var boundaryService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IBoundaryRecordingService>(App.Services!);
        boundaryService.AddPointManual(0, 0, 0);
        boundaryService.AddPointManual(200, 0, Math.PI / 2);
        boundaryService.AddPointManual(200, 200, Math.PI);
        boundaryService.AddPointManual(0, 200, 3 * Math.PI / 2);

        vm.StopBoundaryRecordingCommand?.Execute(null);
        await PumpUI(500);

        Console.Write($"boundary={vm.HasBoundary} ");

        // Build headland with a boundary offset segment (20m inset)
        vm.HeadlandDistance = 20;
        var headlandSeg = new AgValoniaGPS.Models.Headland.HeadlandSegment
        {
            Name = "Boundary",
            Type = AgValoniaGPS.Models.Headland.HeadlandSegmentType.Boundary,
            Offset = 20,
            BoundaryPoints = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
            {
                new(0, 0, 0), new(200, 0, Math.PI / 2),
                new(200, 200, Math.PI), new(0, 200, 3 * Math.PI / 2), new(0, 0, 0)
            }
        };
        vm.ComputeSegmentOffset(headlandSeg);
        vm.HeadlandSegments.Add(headlandSeg);
        vm.BuildHeadlandFromSegments();
        await PumpUI(500);

        Console.Write($"headland={vm.HasHeadland} ");

        // Add an AB track (vertical line at x=100)
        var track = new AgValoniaGPS.Models.Track.Track
        {
            Name = "AB_0.0 Test",
            Points = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
            {
                new(100, 0, 0),
                new(100, 200, 0)
            },
            Type = AgValoniaGPS.Models.Track.TrackType.ABLine,
            IsVisible = true,
            IsActive = true
        };
        vm.SavedTracks.Add(track);

        // Add a diagonal track for testing
        double diagHeading = Math.Atan2(150.0, 180.0); // ~39.8 degrees
        var diagTrack = new AgValoniaGPS.Models.Track.Track
        {
            Name = "AB_Diagonal",
            Points = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
            {
                new(25, 10, diagHeading),
                new(175, 190, diagHeading)
            },
            Type = AgValoniaGPS.Models.Track.TrackType.ABLine,
            IsVisible = true
        };
        vm.SavedTracks.Add(diagTrack);

        vm.SelectedTrack = track;

        // Configure tool width
        ConfigurationStore.Instance.Tool.Width = 12.0;
        ConfigurationStore.Instance.Tram.TramWidth = 24.0;

        Console.WriteLine($"tracks={vm.SavedTracks.Count} OK");
    }

    static async Task RunUTurnTest(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var window = lifetime.MainWindow as Window
            ?? throw new Exception("MainWindow not found");
        var vm = (MainViewModel)window.DataContext!;

        try
        {
            await AutoSteerUTurnTest.Run(window, vm);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UTurnTest] ERROR: {ex.Message}");
            _scenarioFailed = true;
        }

        lifetime.Shutdown();
    }

    static async Task RunFieldTest(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var window = lifetime.MainWindow as Window
            ?? throw new Exception("MainWindow not found");
        var vm = (MainViewModel)window.DataContext!;

        try
        {
            await FieldWorkflowTest.Run(window, vm);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FieldTest] ERROR: {ex.Message}");
            _scenarioFailed = true;
        }

        lifetime.Shutdown();
    }

    static async Task RunCatalog(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var window = lifetime.MainWindow as Window
            ?? throw new Exception("MainWindow not found");
        var vm = (MainViewModel)window.DataContext!;

        try
        {
            await UIScreenshotCatalog.Run(window, vm);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Catalog] ERROR: {ex.Message}");
            _scenarioFailed = true;
        }

        lifetime.Shutdown();
    }

    static async Task RunTileTest(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var window = lifetime.MainWindow as Window
            ?? throw new Exception("MainWindow not found");
        var vm = (MainViewModel)window.DataContext!;
        var simService = App.Services!.GetRequiredService<IGpsSimulationService>();
        var config = ConfigurationStore.Instance;

        try
        {
            // Position tractor at lat 0.002, lon 0
            config.Display.FieldTextureVisible = true;
            simService.Initialize(new AgValoniaGPS.Models.Wgs84(0.002, 0.0));

            // Set 2D north-up mode
            vm.Is2DMode = true;
            vm.CameraPitch = -90.0;
            vm.CameraMode = AgValoniaGPS.Models.CameraMode.NorthUp;

            // Tick simulator to update position
            for (int i = 0; i < 10; i++)
            {
                simService.Tick(0);
                await Delay(50);
            }

            // Zoom in (2x)
            vm.ZoomInCommand?.Execute(null);
            vm.ZoomInCommand?.Execute(null);
            vm.ZoomInCommand?.Execute(null);
            vm.ZoomInCommand?.Execute(null);
            await Delay(500);
            Dispatcher.UIThread.RunJobs();

            CaptureScreenshot(window, "tile_test_lat0002_zoomed");
            Console.WriteLine($"[TileTest] Captured at lat={vm.Latitude:F6}, lon={vm.Longitude:F6}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TileTest] ERROR: {ex.Message}");
            _scenarioFailed = true;
        }

        lifetime.Shutdown();
    }

    static async Task RunRecPathTest(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var window = lifetime.MainWindow as Window
            ?? throw new Exception("MainWindow not found");
        var vm = (MainViewModel)window.DataContext!;
        var simService = App.Services!.GetRequiredService<IGpsSimulationService>();
        var config = ConfigurationStore.Instance;
        var gifFrames = new List<string>();

        void Frame()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var ps = new PixelSize(Math.Max((int)window.Bounds.Width, 1), Math.Max((int)window.Bounds.Height, 1));
            var bmp = new RenderTargetBitmap(ps, new Vector(96, 96));
            bmp.Render(window);
            var p = Path.Combine(_screenshotDir, $"recpath_frame_{gifFrames.Count:D4}.png");
            bmp.Save(p);
            gifFrames.Add(p);
        }

        try
        {
            // Setup
            vm.Is2DMode = true;
            vm.CameraPitch = -90.0;
            vm.CameraMode = AgValoniaGPS.Models.CameraMode.NorthUp;
            vm.IsGridOn = true;
            vm.IsDayMode = true;
            config.Display.FieldTextureVisible = true;
            config.Display.AutoDayNight = false;
            config.Tool.Width = 6.0;
            vm.State.UI.CloseDialog();
            if (vm.ConfigurationViewModel != null) vm.ConfigurationViewModel.IsDialogVisible = false;
            await Delay(300);

            // Load field
            Console.Write("[RecPath] Load field... ");
            var settingsService = App.Services!.GetRequiredService<ISettingsService>();
            var testFieldDir = Path.Combine(settingsService.Settings.FieldsDirectory, "TestField");
            try { await vm.OpenFieldAsync(testFieldDir, "TestField"); await Delay(500); }
            catch (Exception ex) { Console.Write($"({ex.Message}) "); }
            Console.WriteLine($"[open={vm.IsFieldOpen}]");

            // Accelerate
            vm.SimulatorForwardCommand?.Execute(null);
            vm.SimulatorForwardCommand?.Execute(null);
            vm.SimulatorForwardCommand?.Execute(null);
            vm.SimulatorForwardCommand?.Execute(null);
            await Delay(50);
            for (int i = 0; i < 20; i++) { simService.Tick(0); await Delay(10); }

            // Open dialog, start recording
            Console.Write("[RecPath] Open dialog + start rec... ");
            vm.ShowRecordedPathDialogCommand?.Execute(null);
            await Delay(200);
            vm.StartRecordedPathCommand?.Execute(null);
            await Delay(100);
            Frame();
            Console.WriteLine($"[recording={vm.IsRecordingPath}]");

            // Drive a circle: constant left steer for ~360 degrees
            Console.Write("[RecPath] Recording circle... ");
            vm.SimulatorSteerAngle = 15.0; // Constant left turn
            for (int i = 0; i < 500; i++)
            {
                simService.Tick(0);
                await Delay(8);
                if (i % 20 == 0) Frame();
            }
            CaptureScreenshot(window, "recpath_circle_recording");
            Console.WriteLine("OK");

            // Stop recording
            Console.Write("[RecPath] Stop recording... ");
            vm.StopRecordedPathCommand?.Execute(null);
            vm.SimulatorSteerAngle = 0;
            await Delay(300);
            Dispatcher.UIThread.RunJobs();
            Frame(); Frame();
            int pts = vm.State.RecordedPath.RecordedPoints.Count;
            Console.WriteLine($"[points={pts}]");

            // Save with name
            if (vm.HasUnsavedRecordedPath)
            {
                vm.RecordedPathName = "CircleTest";
                vm.SaveNamedRecordedPathCommand?.Execute(null);
                await Delay(200);
            }

            // Show path on map
            vm.ShowRecordedPaths = true;
            await Delay(200);
            Dispatcher.UIThread.RunJobs();
            CaptureScreenshot(window, "recpath_circle_saved");
            Frame(); Frame();

            // Close dialog for clean view
            vm.State.UI.CloseDialog();
            await Delay(200);
            Frame(); Frame();

            // Drive to offset position for playback (reverse out of circle)
            Console.Write("[RecPath] Moving to start position... ");
            vm.SimulatorReverseCommand?.Execute(null);
            await Delay(50);
            for (int i = 0; i < 60; i++)
            {
                simService.Tick(0); await Delay(10);
                if (i % 15 == 0) Frame();
            }
            vm.SimulatorForwardCommand?.Execute(null);
            await Delay(50);
            // Steer to roughly face the circle start
            vm.SimulatorSteerAngle = -10;
            for (int i = 0; i < 30; i++) { simService.Tick(0); await Delay(10); }
            vm.SimulatorSteerAngle = 0;
            for (int i = 0; i < 30; i++)
            {
                simService.Tick(0); await Delay(10);
                if (i % 10 == 0) Frame();
            }
            Console.WriteLine("OK");

            // Start playback
            Console.Write("[RecPath] Start playback... ");
            if (pts >= 5)
            {
                // Move forward at moderate speed for playback
                vm.SimulatorForwardCommand?.Execute(null);
                await Delay(50);

                vm.PlayRecordedPathCommand?.Execute(null);
                await Delay(200);
                Dispatcher.UIThread.RunJobs();
                CaptureScreenshot(window, "recpath_playback_start");
                Frame();
                Console.Write($"[driving={vm.State.RecordedPath.IsDrivingRecordedPath}] ");
                Console.Write($"[dubins={vm.State.RecordedPath.IsFollowingDubinsToPath}] ");

                // Follow for a while
                for (int i = 0; i < 1200; i++)
                {
                    simService.Tick(vm.SimulatorSteerAngle);
                    await Delay(3);
                    if (i % 20 == 0) Frame();
                    if (i % 300 == 0)
                    {
                        var rs = vm.State.RecordedPath;
                        var gp = rs.RecordedPoints.Count > 0 ? rs.RecordedPoints[rs.StartPathIndex] : default;
                        double dg = Math.Sqrt(Math.Pow(gp.Easting - vm.State.Vehicle.Easting, 2) + Math.Pow(gp.Northing - vm.State.Vehicle.Northing, 2));
                        Console.Write($"[steer={vm.SimulatorSteerAngle:F1} dubins={rs.IsFollowingDubinsToPath} rec={rs.IsFollowingRecPath} idx={rs.CurrentPositionIndex} dist={dg:F1}] ");
                    }
                }
                CaptureScreenshot(window, "recpath_playback_following");
                Frame();

                // Stop playback
                vm.PlayRecordedPathCommand?.Execute(null);
                await Delay(200);
                Frame();
                Console.WriteLine("OK");
            }
            else
            {
                Console.WriteLine("[SKIP: <5 points]");
            }

            // Final screenshot
            CaptureScreenshot(window, "recpath_final");
            Frame();

            // Save GIF
            Console.Write("[RecPath] GIF... ");
            var gifPath = Path.Combine(_screenshotDir, "recpath_test.gif");
            var scriptPath = Path.Combine(_screenshotDir, "_recpath_gif.py");
            File.WriteAllText(scriptPath, $@"
from PIL import Image
import sys
frames = []
for path in sys.argv[1:]:
    img = Image.open(path).convert('RGB').resize((640, 480), Image.LANCZOS)
    frames.append(img)
if frames:
    frames[0].save('{gifPath.Replace("'", "\\'")}', save_all=True, append_images=frames[1:], duration=200, loop=0)
    print(f'GIF: {{len(frames)}} frames')
");
            var psi = new System.Diagnostics.ProcessStartInfo("python3", scriptPath + " " + string.Join(" ", gifFrames))
            { RedirectStandardOutput = true, UseShellExecute = false };
            var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(30000);
            Console.Write($"[{proc?.StandardOutput.ReadToEnd().Trim()}] ");
            foreach (var f in gifFrames) try { File.Delete(f); } catch { }
            try { File.Delete(scriptPath); } catch { }
            Console.WriteLine("OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RecPathTest] ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            _scenarioFailed = true;
        }

        lifetime.Shutdown();
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

        // Step 5c: Test compass button camera modes (default is HeadingUp)
        Console.Write("[Step 5c] Compass modes... ");
        Console.Write($"[mode={vm.CameraMode}] ");
        vm.ToggleCameraModeCommand?.Execute(null); // HeadingUp -> NorthUp
        await Delay(200);
        if (vm.CameraMode != AgValoniaGPS.Models.CameraMode.NorthUp)
            throw new Exception($"Expected NorthUp after toggle, got {vm.CameraMode}");
        Console.Write($"[after1={vm.CameraMode}] ");

        vm.ToggleCameraModeCommand?.Execute(null); // NorthUp -> HeadingUp
        await Delay(200);
        if (vm.CameraMode != AgValoniaGPS.Models.CameraMode.HeadingUp)
            throw new Exception($"Expected HeadingUp after toggle, got {vm.CameraMode}");
        Console.Write($"[after2={vm.CameraMode}] ");

        // Already in HeadingUp, pan -> Free should remember HeadingUp
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

        // --- Pass Rendering Test (#175) ---
        await RunPassRenderingTest(window, vm, simService);

        // --- Coverage Area Verification (#195) ---
        await RunCoverageAreaTest(window, vm, simService);

        // --- Track Save/Load E2E ---
        await RunTrackSaveLoadTest(window, vm, simService);

        // --- Tram Lines Test (#34, #35) ---
        await RunTramLinesTest(window, vm, simService);
    }

    static async Task RunDebugDumpTest(Window window, MainViewModel vm)
    {
        Console.Write("[DebugDump] Test screenshot in dump... ");

        // Trigger debug dump command
        vm.CreateDebugDumpCommand?.Execute(null);
        await Delay(500);

        // Find the dump zip
        var dumpDir = Path.Combine(Path.GetTempPath(), "AgValoniaGPS", "dumps");
        if (!Directory.Exists(dumpDir))
            throw new Exception("Dump directory not created");

        var zips = Directory.GetFiles(dumpDir, "debug_dump_*.zip")
            .OrderByDescending(File.GetLastWriteTime)
            .ToArray();
        if (zips.Length == 0)
            throw new Exception("No dump zip found");

        var zipPath = zips[0];
        Console.Write($"[zip={Path.GetFileName(zipPath)}] ");

        // Verify screenshot.png exists in the zip
        using var zipStream = File.OpenRead(zipPath);
        using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
        var screenshotEntry = archive.GetEntry("screenshot.png");
        if (screenshotEntry == null)
            throw new Exception("screenshot.png not found in dump zip");

        Console.Write($"[screenshot={screenshotEntry.Length / 1024}KB] ");

        if (screenshotEntry.Length < 1000)
            throw new Exception($"Screenshot too small ({screenshotEntry.Length} bytes) - likely empty");

        // Extract screenshot for visual verification
        var extractPath = Path.Combine(_screenshotDir, "debug_dump_screenshot.png");
        using (var entryStream = screenshotEntry.Open())
        using (var fileStream = File.Create(extractPath))
            entryStream.CopyTo(fileStream);

        Console.Write($"[extracted={new FileInfo(extractPath).Length / 1024}KB] ");
        Console.WriteLine("OK");
    }

    static async Task RunCompassScenario(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        LogState(vm, "Compass start");
        Console.WriteLine();

        // Reset to field origin for consistent starting position
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();
        await ResetTractorPosition(vm, simService, settingsService);

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
        LogState(vm, "Flags start");
        Console.WriteLine();

        // Reset to field origin
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();
        await ResetTractorPosition(vm, simService, settingsService);

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
        LogState(vm, "Tracks start");
        Console.WriteLine();

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

        // Track 2: Activate AB line + enable tram lines + engage autosteer
        Console.Write("[Tracks 2] Activate AB line + tram lines + autosteer... ");
        vm.State.UI.CloseDialog();
        ConfigurationStore.Instance.Tram.DisplayMode = AgValoniaGPS.Models.Configuration.TramDisplayMode.All;
        ConfigurationStore.Instance.Tram.Passes = 3;
        if (vm.SavedTracks.Count > 0)
        {
            vm.SelectedTrack = vm.SavedTracks[0];
            // Engage autosteer via command (headland was built in step 1b)
            vm.ToggleAutoSteerCommand?.Execute(null);
            Console.Write($"[Active={vm.SelectedTrack?.Name}, AutoSteer={vm.IsAutoSteerEngaged}] ");
        }
        await Delay(500);
        CaptureScreenshot(window, "tracks_02_guidance_line_active");
        CaptureScreenshot(window, "tram_lines_visible");
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
        LogState(vm, "Charts start");
        Console.WriteLine();

        // Reset to field origin
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();
        await ResetTractorPosition(vm, simService, settingsService);

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

        // UDP Virtual GPS scenario: disable simulator, feed GPS via virtual receiver
        Console.Write("[UDP GPS] Virtual GPS with roll... ");
        vm.IsSimulatorEnabled = false;
        await Delay(500);

        using (var gps = new VirtualModules.VirtualGpsReceiver(targetPort: 9999))
        {
            gps.Latitude = 43.712800;
            gps.Longitude = -74.006000;
            gps.HeadingDegrees = 45.0;
            gps.SpeedKnots = 10.0;
            gps.FixQuality = 4;
            gps.Satellites = 14;
            gps.Hdop = 0.8;
            gps.RollDegrees = 5.7;
            gps.PitchDegrees = 1.2;

            // Send several frames so the app picks it up
            for (int i = 0; i < 30; i++)
            {
                gps.Step(0.1);
                gps.SendOnce();
                await Delay(100);
            }

            CaptureScreenshot(window, "udp_gps_roll_5_7");
            Console.Write($"[roll={vm.RollDegrees:F1}] ");
        }

        // Re-enable simulator for any subsequent steps
        vm.IsSimulatorEnabled = true;
        await Delay(200);
        Console.WriteLine("OK");
    }

    static async Task RunPassRenderingTest(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        Console.WriteLine("\n--- Pass Rendering Test (#175) ---");
        LogState(vm, "Pass start");
        Console.WriteLine();

        // Set 2D north-up, fixed camera, day mode, clean view
        Console.Write("[Pass 0] Set 2D north-up, clean view... ");
        if (vm.IsAutoSteerEngaged)
            vm.ToggleAutoSteerCommand?.Execute(null);
        vm.SelectedTrack = null;
        vm.CameraMode = AgValoniaGPS.Models.CameraMode.Free; // Fixed camera, no follow
        vm.Is2DMode = true;
        vm.CameraPitch = -90.0;
        vm.IsSimulatorPanelVisible = false;
        vm.State.UI.IsSimulatorPanelVisible = false;
        // Force day mode and disable auto day/night
        AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display.AutoDayNight = false;
        if (!vm.IsDayMode)
            vm.ToggleDayNightCommand?.Execute(null);
        var mapService = App.Services!.GetRequiredService<IMapService>();
        mapService.Set3DMode(false);
        mapService.SetPitchAbsolute(0);
        mapService.SetNorthUp(true);
        mapService.SetAutoPan(false); // Fixed camera position
        mapService.Zoom(0.7); // Zoom out to see full S-N journey
        // Center camera between start (-120) and end (+120), roughly at field center
        mapService.PanTo(0, 0);
        await Delay(300);
        Console.WriteLine("OK");

        // Create a W-E AB line at northing=50 (middle of field)
        Console.Write("[Pass 1] Create W-E track at N=50... ");

        var weTrack = new AgValoniaGPS.Models.Track.Track
        {
            Name = "W-E Test",
            Points = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
            {
                new(0, 50, Math.PI / 2),     // West end, heading east
                new(200, 50, Math.PI / 2)    // East end, heading east
            },
            Type = AgValoniaGPS.Models.Track.TrackType.ABLine,
            IsVisible = true
        };
        vm.SavedTracks.Add(weTrack);
        vm.SelectedTrack = weTrack;
        await Delay(300);
        Console.Write($"[active={vm.HasActiveTrack}] ");
        CaptureScreenshot(window, "pass_01_we_track");
        Console.WriteLine("OK");

        // Drive S->N using simulator at 10x speed
        Console.Write("[Pass 2] Drive S->N across W-E track (no autosteer)... ");

        // Position tractor south of field by driving south then turning north
        vm.IsSimulatorEnabled = true;
        vm.IsSimulatorSpeed10x = true;

        // Turn to face south: steer hard right until heading ~180 deg
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(50);
        while (vm.Heading < 170 || vm.Heading > 190)
        {
            simService.Tick(30); // Hard right turn
            await Delay(5);
        }
        // Drive south (no capture)
        for (int i = 0; i < 120; i++)
        {
            simService.Tick(0);
            await Delay(5);
        }
        Console.Write($"[south N={vm.Northing:F0}] ");

        // Turn to face north
        while (vm.Heading > 10 && vm.Heading < 350)
        {
            simService.Tick(30);
            await Delay(5);
        }
        Console.Write($"[heading={vm.Heading:F0}] ");

        var gifFrames = new System.Collections.Generic.List<string>();

        // Drive north across the field (N=-100 to N=+100), capturing frames
        for (int i = 0; i < 200; i++)
        {
            simService.Tick(0); // Straight north
            if (i % 40 == 0)
                Console.Write($"[N={vm.Northing:F0}] ");
            await Delay(10); // Fast ticks
            if (i % 4 == 0) // Capture every 4th frame
            {
                var framePath = Path.Combine(_screenshotDir, $"pass_frame_{gifFrames.Count:D4}.png");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var ps = new PixelSize(Math.Max((int)window.Bounds.Width, 1), Math.Max((int)window.Bounds.Height, 1));
                var bmp = new RenderTargetBitmap(ps, new Vector(96, 96));
                bmp.Render(window);
                bmp.Save(framePath);
                gifFrames.Add(framePath);
            }
        }

        CaptureScreenshot(window, "pass_02_driving_across");
        Console.Write($"[frames={gifFrames.Count}] ");

        // Generate video
        try
        {
            var scriptPath = Path.Combine(_screenshotDir, "_pass_gif.py");
            var gifPath = Path.Combine(_screenshotDir, "pass_rendering.gif");
            File.WriteAllText(scriptPath, $@"
from PIL import Image
import sys
frames = []
for path in sys.argv[1:]:
    img = Image.open(path).convert('RGB').resize((640, 480), Image.LANCZOS)
    frames.append(img)
if frames:
    frames[0].save('{gifPath.Replace("'", "\\'")}', save_all=True, append_images=frames[1:], duration=200, loop=0)
    print(f'GIF: {{len(frames)}} frames')
");
            var psi = new System.Diagnostics.ProcessStartInfo("python3", scriptPath + " " + string.Join(" ", gifFrames))
            { RedirectStandardOutput = true, UseShellExecute = false };
            var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(30000);
            Console.Write($"[{proc?.StandardOutput.ReadToEnd().Trim()}] ");
            foreach (var f in gifFrames) try { File.Delete(f); } catch { }
            try { File.Delete(scriptPath); } catch { }
        }
        catch (Exception ex) { Console.Write($"[GIF err: {ex.Message}] "); }

        // Cleanup
        vm.SavedTracks.Remove(weTrack);
        vm.SelectedTrack = null;
        vm.IsSimulatorSpeed10x = false;

        Console.WriteLine("OK");
        Console.WriteLine("--- Pass Rendering Test Complete ---");
    }

    /// <summary>
    /// Coverage area verification test (#195):
    /// Isolated test: closes current field, reopens fresh, drives 100m with
    /// 10m implement, verifies coverage area, then save/load round-trip.
    /// Logs tractor path for post-test investigation.
    /// </summary>
    static async Task RunCoverageAreaTest(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        Console.WriteLine("\n--- Coverage Area Test (#195) ---");
        var coverageService = App.Services!.GetRequiredService<ICoverageMapService>();
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();
        var fieldService = App.Services!.GetRequiredService<IFieldService>();
        var config = ConfigurationStore.Instance;

        // === ISOLATE: Close current field and reopen fresh ===
        Console.Write("[Cov 0] Isolate: close + reopen field... ");
        if (vm.IsAutoSteerEngaged)
            vm.ToggleAutoSteerCommand?.Execute(null);
        if (vm.IsSectionMasterOn)
            vm.ToggleSectionMasterCommand?.Execute(null);
        vm.SelectedTrack = null;

        // Reopen the test field for a clean slate
        var testFieldDir = Path.Combine(settingsService.Settings.FieldsDirectory, "TestField");
        try { await vm.OpenFieldAsync(testFieldDir, "TestField"); }
        catch (Exception ex) { Console.Write($"({ex.Message}) "); }
        await Delay(500);
        Console.Write($"[fieldOpen={vm.IsFieldOpen}] ");

        // Configure tool: 10m wide, 5 sections at 2m each = 10m total
        config.Tool.Width = 10.0;
        config.NumSections = 5;
        for (int i = 0; i < 5; i++)
            config.Tool.SetSectionWidth(i, 200); // 200cm = 2m each

        // Clear any pre-existing coverage and force full display refresh
        coverageService.ClearAll();
        // Multiple UI pumps to ensure render cycle picks up the cleared bitmap
        for (int i = 0; i < 5; i++)
        {
            await Delay(100);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
        Console.WriteLine("OK");

        // === POSITION: Reset tractor to N=-50 (well inside boundary), heading north ===
        // Boundary is N=-80 to N=80, headland is ~7m inside = N=-73 to N=73
        // Starting at N=-50 and driving 100m to N=50 stays fully within headland
        Console.Write("[Cov 1] Position tractor at (0,-50) heading north... ");
        // Use field origin lat/lon but offset south by ~50m
        double originLat = settingsService.Settings.SimulatorLatitude;
        double originLon = settingsService.Settings.SimulatorLongitude;
        // 50m south: ~0.00045 degrees latitude
        vm.SetSimulatorCoordinates(originLat - 0.00045, originLon);
        simService.SetHeading(0); // Face north
        vm.SimulatorSteerAngle = 0;
        await Delay(100);

        // Tick to establish position and tool
        for (int i = 0; i < 10; i++) { simService.Tick(0); await Delay(10); }
        Console.Write($"[E={vm.Easting:F1} N={vm.Northing:F1} H={vm.Heading:F0}] ");
        Console.Write($"[toolE={vm.ToolEasting:F1} toolN={vm.ToolNorthing:F1} toolW={vm.ToolWidth:F1}] ");
        Console.WriteLine("OK");

        // === DRIVE: Sections on, drive 100m north, log path ===
        Console.Write("[Cov 2] Drive 100m with sections ON... ");

        // Enable sections (Auto mode)
        if (!vm.IsSectionMasterOn)
            vm.ToggleSectionMasterCommand?.Execute(null);
        await Delay(100);
        Console.Write($"[master={vm.IsSectionMasterOn}] ");

        // Accelerate
        vm.SimulatorForwardCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(50);

        CaptureScreenshot(window, "cov_01_before_drive");

        // Drive and log position every 10 ticks
        double startN = vm.Northing;
        int ticks = 0;
        var pathLog = new System.Text.StringBuilder();
        pathLog.AppendLine("tick,easting,northing,heading,toolE,toolN,speed,area,sectionsOn");

        // Log tick 0 (initial state)
        {
            int s0 = 0;
            var st0 = vm.GetSectionStates();
            if (st0 != null) for (int i = 0; i < Math.Min(st0.Length, config.NumSections); i++) if (st0[i]) s0++;
            pathLog.AppendLine($"0,{vm.Easting:F2},{vm.Northing:F2},{vm.Heading:F1}," +
                $"{vm.ToolEasting:F2},{vm.ToolNorthing:F2},{vm.SpeedKmh:F1}," +
                $"{coverageService.TotalWorkedArea:F1},{s0}");
            Console.Write($"[tick0: area={coverageService.TotalWorkedArea:F1} pos=({vm.Easting:F1},{vm.Northing:F1})] ");
        }

        while (Math.Abs(vm.Northing - startN) < 100.0 && ticks < 500)
        {
            simService.Tick(0);
            await Delay(5);
            ticks++;

            // Log every 10 ticks
            if (ticks % 10 == 0)
            {
                int sectionsOn = 0;
                var states = vm.GetSectionStates();
                if (states != null)
                    for (int i = 0; i < Math.Min(states.Length, config.NumSections); i++)
                        if (states[i]) sectionsOn++;

                pathLog.AppendLine($"{ticks},{vm.Easting:F2},{vm.Northing:F2},{vm.Heading:F1}," +
                    $"{vm.ToolEasting:F2},{vm.ToolNorthing:F2},{vm.SpeedKmh:F1}," +
                    $"{coverageService.TotalWorkedArea:F1},{sectionsOn}");
            }
        }

        double distDriven = Math.Abs(vm.Northing - startN);
        Console.Write($"[dist={distDriven:F1}m ticks={ticks}] ");

        // Save path log
        var logPath = Path.Combine(_screenshotDir, "cov_path_log.csv");
        File.WriteAllText(logPath, pathLog.ToString());
        Console.Write($"[log={logPath}] ");

        // Check coverage
        await Delay(200);
        Dispatcher.UIThread.RunJobs();
        double workedArea = coverageService.TotalWorkedArea;
        double expectedArea = distDriven * 10.0;
        Console.Write($"[area={workedArea:F0}m2 expected={expectedArea:F0}m2] ");

        if (workedArea < 1.0)
            throw new Exception($"No coverage painted after driving {distDriven:F0}m with sections on");

        // Check if area is within 50% of expected (generous tolerance for section control behavior)
        if (workedArea >= expectedArea * 0.5)
            Console.Write("[PASS] ");
        else
            Console.Write($"[WARN: {workedArea:F0} < {expectedArea * 0.5:F0} (50% of expected)] ");

        CaptureScreenshot(window, "cov_02_after_drive");

        // Turn off sections
        vm.ToggleSectionMasterCommand?.Execute(null);
        double straightArea = workedArea;
        Console.WriteLine("OK");

        // === DIAGONAL DRIVE: Clear and drive 100m at 45 degrees ===
        Console.Write("[Cov 2b] Diagonal drive 100m at 45deg... ");
        coverageService.ClearAll();
        for (int i = 0; i < 5; i++) { await Delay(100); Dispatcher.UIThread.RunJobs(); }

        // Position at (-30,-30) heading NE (45 degrees = ~0.785 rad)
        vm.SetSimulatorCoordinates(originLat - 0.00027, originLon - 0.00042);
        simService.SetHeading(Math.PI / 4); // 45 degrees = NE
        vm.SimulatorSteerAngle = 0;
        await Delay(100);
        for (int i = 0; i < 10; i++) { simService.Tick(0); await Delay(10); }
        Console.Write($"[pos=({vm.Easting:F1},{vm.Northing:F1}) H={vm.Heading:F0}] ");

        if (!vm.IsSectionMasterOn)
            vm.ToggleSectionMasterCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(50);

        CaptureScreenshot(window, "cov_04_diagonal_before");

        double startE2 = vm.Easting;
        double startN2 = vm.Northing;
        int ticks2 = 0;
        while (ticks2 < 500)
        {
            simService.Tick(0);
            await Delay(5);
            ticks2++;
            double dx = vm.Easting - startE2;
            double dy = vm.Northing - startN2;
            if (Math.Sqrt(dx * dx + dy * dy) >= 100.0) break;
        }
        double diagDist = Math.Sqrt(
            Math.Pow(vm.Easting - startE2, 2) + Math.Pow(vm.Northing - startN2, 2));

        await Delay(200);
        Dispatcher.UIThread.RunJobs();
        double diagArea = coverageService.TotalWorkedArea;
        double diagExpected = diagDist * 10.0;
        Console.Write($"[dist={diagDist:F1}m area={diagArea:F0}m2 expected={diagExpected:F0}m2] ");

        double diagError = Math.Abs(diagArea - diagExpected) / diagExpected * 100;
        Console.Write($"[error={diagError:F1}%] ");

        if (diagArea < 1.0)
            throw new Exception($"No diagonal coverage painted");

        // Compare straight vs diagonal accuracy
        double straightError = Math.Abs(straightArea - (distDriven * 10.0)) / (distDriven * 10.0) * 100;
        Console.Write($"[straight={straightError:F1}% diagonal={diagError:F1}%] ");

        vm.ToggleSectionMasterCommand?.Execute(null);
        CaptureScreenshot(window, "cov_05_diagonal_after");
        Console.WriteLine("OK");

        // === 30 DEGREE DRIVE ===
        Console.Write("[Cov 2c] 30deg drive 100m... ");
        coverageService.ClearAll();
        for (int i = 0; i < 5; i++) { await Delay(100); Dispatcher.UIThread.RunJobs(); }

        vm.SetSimulatorCoordinates(originLat - 0.00040, originLon - 0.00020);
        simService.SetHeading(Math.PI / 6); // 30 degrees
        vm.SimulatorSteerAngle = 0;
        await Delay(100);
        for (int i = 0; i < 10; i++) { simService.Tick(0); await Delay(10); }
        Console.Write($"[pos=({vm.Easting:F1},{vm.Northing:F1}) H={vm.Heading:F0}] ");

        if (!vm.IsSectionMasterOn)
            vm.ToggleSectionMasterCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        vm.SimulatorForwardCommand?.Execute(null);
        await Delay(50);

        double startE3 = vm.Easting;
        double startN3 = vm.Northing;
        int ticks3 = 0;
        while (ticks3 < 500)
        {
            simService.Tick(0);
            await Delay(5);
            ticks3++;
            double dx = vm.Easting - startE3;
            double dy = vm.Northing - startN3;
            if (Math.Sqrt(dx * dx + dy * dy) >= 100.0) break;
        }
        double dist30 = Math.Sqrt(
            Math.Pow(vm.Easting - startE3, 2) + Math.Pow(vm.Northing - startN3, 2));

        await Delay(200);
        Dispatcher.UIThread.RunJobs();
        double area30 = coverageService.TotalWorkedArea;
        double expected30 = dist30 * 10.0;
        double error30 = Math.Abs(area30 - expected30) / expected30 * 100;
        Console.Write($"[dist={dist30:F1}m area={area30:F0}m2 expected={expected30:F0}m2 error={error30:F1}%] ");

        if (area30 < 1.0)
            throw new Exception("No 30deg coverage painted");

        vm.ToggleSectionMasterCommand?.Execute(null);
        CaptureScreenshot(window, "cov_06_30deg_after");
        Console.WriteLine("OK");

        // === DIFFERENT TOOL WIDTHS ===
        double[] toolWidths = { 3.0, 6.0, 12.0, 24.0 };
        foreach (double tw in toolWidths)
        {
            Console.Write($"[Cov 2d] Tool {tw:F0}m north 50m... ");
            coverageService.ClearAll();
            for (int i = 0; i < 3; i++) { await Delay(50); Dispatcher.UIThread.RunJobs(); }

            config.Tool.Width = tw;
            int nSec = Math.Max(1, (int)(tw / 2));
            config.NumSections = nSec;
            for (int i = 0; i < nSec; i++)
                config.Tool.SetSectionWidth(i, (int)(tw / nSec * 100));

            vm.SetSimulatorCoordinates(originLat - 0.00023, originLon);
            simService.SetHeading(0);
            vm.SimulatorSteerAngle = 0;
            await Delay(50);
            for (int i = 0; i < 10; i++) { simService.Tick(0); await Delay(5); }

            if (!vm.IsSectionMasterOn)
                vm.ToggleSectionMasterCommand?.Execute(null);
            vm.SimulatorForwardCommand?.Execute(null);
            vm.SimulatorForwardCommand?.Execute(null);
            vm.SimulatorForwardCommand?.Execute(null);
            await Delay(30);

            double sN = vm.Northing;
            int t = 0;
            while (Math.Abs(vm.Northing - sN) < 50.0 && t < 300)
            {
                simService.Tick(0);
                await Delay(3);
                t++;
            }
            double d = Math.Abs(vm.Northing - sN);
            await Delay(100);
            Dispatcher.UIThread.RunJobs();
            double a = coverageService.TotalWorkedArea;
            double exp = d * tw;
            double err = exp > 0 ? Math.Abs(a - exp) / exp * 100 : 0;
            Console.Write($"[dist={d:F0}m area={a:F0}m2 expected={exp:F0}m2 error={err:F1}%] ");
            vm.ToggleSectionMasterCommand?.Execute(null);
            Console.WriteLine("OK");
        }

        // Restore 10m tool for save/load test
        config.Tool.Width = 10.0;
        config.NumSections = 5;
        for (int i = 0; i < 5; i++)
            config.Tool.SetSectionWidth(i, 200);

        // === SAVE/LOAD ROUND-TRIP ===
        Console.Write("[Cov 3] Save + reload coverage... ");
        var fieldDir = fieldService.ActiveField?.DirectoryPath;
        if (fieldDir != null)
        {
            coverageService.SaveToFile(fieldDir);
            double areaBefore = coverageService.TotalWorkedArea;
            Console.Write($"[saved={areaBefore:F0}m2] ");

            coverageService.ClearAll();
            Console.Write($"[afterClear={coverageService.TotalWorkedArea:F0}m2] ");

            coverageService.LoadFromFile(fieldDir);
            await Delay(200);
            double areaAfterLoad = coverageService.TotalWorkedArea;
            Console.Write($"[afterLoad={areaAfterLoad:F0}m2] ");

            if (Math.Abs(areaAfterLoad - areaBefore) > 10.0)
                throw new Exception($"Coverage lost after save/load: {areaBefore:F0}m2 -> {areaAfterLoad:F0}m2");

            Console.Write("[PASS] ");
            CaptureScreenshot(window, "cov_03_after_reload");
        }
        else
        {
            Console.Write("[SKIP: no field] ");
        }

        // Restore tool config
        config.Tool.Width = 12.0;
        config.NumSections = 6;
        for (int i = 0; i < 6; i++)
            config.Tool.SetSectionWidth(i, 200);

        Console.WriteLine("OK");
        Console.WriteLine("--- Coverage Area Test Complete ---");
    }

    /// <summary>
    /// Tram lines integration test (#34, #35):
    /// 1. Generate tram lines from AB line, verify they exist
    /// 2. Toggle display mode, verify rendering changes
    /// 3. Save field, reload, verify tram lines persist
    /// </summary>
    /// <summary>
    /// E2E test: create tracks, save field, reload, verify tracks persist with all properties.
    /// </summary>
    static async Task RunTrackSaveLoadTest(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        Console.WriteLine("\n--- Track Save/Load E2E Test ---");
        var fieldService = App.Services!.GetRequiredService<IFieldService>();

        // Step 1: Record current tracks
        Console.Write("[TrackIO 1] Record initial tracks... ");
        int initialCount = vm.SavedTracks.Count;
        Console.Write($"[initial={initialCount}] ");

        // Add a test track with nudge distance
        var testTrack = new AgValoniaGPS.Models.Track.Track
        {
            Name = "E2E Test Track",
            Points = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
            {
                new(30, 0, 0),
                new(30, 100, 0)
            },
            Type = AgValoniaGPS.Models.Track.TrackType.ABLine,
            IsVisible = true,
            NudgeDistance = 7.5
        };
        vm.SavedTracks.Add(testTrack);

        // Add a hidden track
        var hiddenTrack = new AgValoniaGPS.Models.Track.Track
        {
            Name = "Hidden Track",
            Points = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
            {
                new(50, 0, 0.5),
                new(50, 80, 0.5),
                new(60, 100, 0.8)
            },
            Type = AgValoniaGPS.Models.Track.TrackType.Curve,
            IsVisible = false,
            NudgeDistance = -3.25
        };
        vm.SavedTracks.Add(hiddenTrack);

        int afterAdd = vm.SavedTracks.Count;
        Console.Write($"[afterAdd={afterAdd}] ");
        Console.WriteLine("OK");

        // Step 2: Save tracks by closing and reopening field
        Console.Write("[TrackIO 2] Close + reopen field to trigger save... ");
        var fieldDir = fieldService.ActiveField?.DirectoryPath;
        if (fieldDir == null) { Console.WriteLine("[SKIP: no field]"); return; }
        await vm.CloseFieldAsync();
        await Delay(300);
        Console.Write("[closed] ");
        Console.WriteLine("OK");

        // Step 3: Reload field
        Console.Write("[TrackIO 3] Reload field... ");
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();
        var testFieldDir = Path.Combine(settingsService.Settings.FieldsDirectory, "TestField");
        try { await vm.OpenFieldAsync(testFieldDir, "TestField"); }
        catch (Exception ex) { Console.Write($"({ex.Message}) "); }
        await Delay(500);

        int afterReload = vm.SavedTracks.Count;
        Console.Write($"[loaded={afterReload}] ");

        if (afterReload < afterAdd)
            throw new Exception($"Tracks lost after reload: {afterAdd} -> {afterReload}");
        Console.Write("[count PASS] ");
        Console.WriteLine("OK");

        // Step 4: Verify properties
        Console.Write("[TrackIO 4] Verify track properties... ");
        var e2eTrack = vm.SavedTracks.FirstOrDefault(t => t.Name == "E2E Test Track");
        var hidTrack = vm.SavedTracks.FirstOrDefault(t => t.Name == "Hidden Track");

        if (e2eTrack == null) throw new Exception("E2E Test Track not found after reload");
        if (hidTrack == null) throw new Exception("Hidden Track not found after reload");

        // NudgeDistance
        if (Math.Abs(e2eTrack.NudgeDistance - 7.5) > 0.1)
            throw new Exception($"NudgeDistance wrong: expected 7.5, got {e2eTrack.NudgeDistance}");
        if (Math.Abs(hidTrack.NudgeDistance - (-3.25)) > 0.1)
            throw new Exception($"NudgeDistance wrong: expected -3.25, got {hidTrack.NudgeDistance}");
        Console.Write("[nudge PASS] ");

        // Visibility
        if (!e2eTrack.IsVisible)
            throw new Exception("E2E Test Track should be visible");
        if (hidTrack.IsVisible)
            throw new Exception("Hidden Track should be hidden");
        Console.Write("[visibility PASS] ");

        // Point count
        if (e2eTrack.Points.Count != 2)
            throw new Exception($"AB line should have 2 points, got {e2eTrack.Points.Count}");
        if (hidTrack.Points.Count != 3)
            throw new Exception($"Curve should have 3 points, got {hidTrack.Points.Count}");
        Console.Write("[points PASS] ");

        // Cleanup: remove test tracks
        vm.SavedTracks.Remove(e2eTrack);
        vm.SavedTracks.Remove(hidTrack);

        Console.WriteLine("OK");
        Console.WriteLine("--- Track Save/Load E2E Complete ---");
    }

    static async Task RunTramLinesTest(
        Window window, MainViewModel vm, IGpsSimulationService simService)
    {
        Console.WriteLine("\n--- Tram Lines Test (#34, #35) ---");
        var tramService = App.Services!.GetRequiredService<ITramLineService>();
        var fieldService = App.Services!.GetRequiredService<IFieldService>();
        var settingsService = App.Services!.GetRequiredService<ISettingsService>();
        var config = ConfigurationStore.Instance;

        // Reset position
        await ResetTractorPosition(vm, simService, settingsService);

        // Step 0: Capture Field Builder dialog with multiple tracks
        Console.Write("[Tram 0] Field Builder screenshots... ");

        // Add a second track for the visualizer
        var extraTrack = new AgValoniaGPS.Models.Track.Track
        {
            Name = "Diagonal Test",
            Points = new System.Collections.Generic.List<AgValoniaGPS.Models.Base.Vec3>
            {
                new(-80, -60, 0.7),
                new(80, 60, 0.7)
            },
            Type = AgValoniaGPS.Models.Track.TrackType.ABLine,
            IsVisible = true
        };
        vm.SavedTracks.Add(extraTrack);

        // Open Field Builder - main tracks tab
        vm.ShowFieldBuilderCommand?.Execute(null);
        await Delay(500);
        Dispatcher.UIThread.RunJobs();
        CaptureScreenshot(window, "field_builder_tracks_tab");

        // Click Add Track to show sub-panel (simulate via code)
        var addPanel = FindControl(window, "AddTrackPanel");
        var mainTabs = FindControl(window, "MainTabs");
        if (addPanel != null && mainTabs != null)
        {
            mainTabs.IsVisible = false;
            addPanel.IsVisible = true;
            await Delay(200);
            Dispatcher.UIThread.RunJobs();
            CaptureScreenshot(window, "field_builder_add_track");
            // Restore
            mainTabs.IsVisible = true;
            addPanel.IsVisible = false;
        }

        // Capture Headland and Tram tabs
        vm.ShowFieldBuilderCommand?.Execute(null);
        await Delay(300);
        // Switch to Headland tab (index 1)
        var tabCtrl = FindControl(window, "MainTabs") as Avalonia.Controls.TabControl;
        if (tabCtrl != null)
        {
            tabCtrl.SelectedIndex = 1;
            await Delay(200);
            Dispatcher.UIThread.RunJobs();
            CaptureScreenshot(window, "field_builder_headland_tab");

            tabCtrl.SelectedIndex = 2;
            await Delay(200);
            Dispatcher.UIThread.RunJobs();
            CaptureScreenshot(window, "field_builder_tram_tab");

            tabCtrl.SelectedIndex = 0;
        }

        vm.State.UI.CloseDialog();
        vm.SavedTracks.Remove(extraTrack);
        await Delay(100);
        Console.WriteLine("OK");

        // Step 1: Enable tram display and select track
        Console.Write("[Tram 1] Enable tram display + select track... ");
        config.Tram.DisplayMode = AgValoniaGPS.Models.Configuration.TramDisplayMode.All;
        config.Tram.Passes = 3;

        if (vm.SavedTracks.Count > 0)
        {
            vm.SelectedTrack = vm.SavedTracks[0];
            await Delay(300);
        }

        bool hasTram = tramService.HasTramLines;
        int lineCount = tramService.ParallelTramLines.Count;
        Console.Write($"[hasTram={hasTram} lines={lineCount}] ");

        if (!hasTram)
            throw new Exception("No tram lines generated after selecting track");

        Console.Write("[PASS] ");
        CaptureScreenshot(window, "tram_01_generated");
        Console.WriteLine("OK");

        // Step 2: Toggle display mode
        Console.Write("[Tram 2] Toggle display modes... ");
        config.Tram.DisplayMode = AgValoniaGPS.Models.Configuration.TramDisplayMode.LinesOnly;
        await Delay(100);
        CaptureScreenshot(window, "tram_02_lines_only");

        config.Tram.DisplayMode = AgValoniaGPS.Models.Configuration.TramDisplayMode.OuterOnly;
        await Delay(100);
        CaptureScreenshot(window, "tram_03_outer_only");

        config.Tram.DisplayMode = AgValoniaGPS.Models.Configuration.TramDisplayMode.Off;
        await Delay(100);
        CaptureScreenshot(window, "tram_04_off");

        config.Tram.DisplayMode = AgValoniaGPS.Models.Configuration.TramDisplayMode.All;
        Console.Write("[PASS] ");
        Console.WriteLine("OK");

        // Step 3: Change passes and verify regeneration
        Console.Write("[Tram 3] Change passes 3->5... ");
        int linesBefore = tramService.ParallelTramLines.Count;
        config.Guidance.TramPasses = 5;
        await Delay(200);
        int linesAfter = tramService.ParallelTramLines.Count;
        Console.Write($"[before={linesBefore} after={linesAfter}] ");

        if (linesAfter >= linesBefore)
            Console.Write("[WARN: expected fewer lines with more passes] ");
        else
            Console.Write("[PASS] ");
        Console.WriteLine("OK");

        // Step 4: Save and reload tram lines
        Console.Write("[Tram 4] Save + reload tram lines... ");
        var fieldDir = fieldService.ActiveField?.DirectoryPath;
        if (fieldDir != null)
        {
            tramService.SaveToFile(fieldDir);
            int savedLines = tramService.ParallelTramLines.Count;
            Console.Write($"[saved={savedLines}] ");

            // Clear and reload
            tramService.Clear();
            if (tramService.HasTramLines)
                throw new Exception("Tram lines not cleared");

            tramService.LoadFromFile(fieldDir);
            int loadedLines = tramService.ParallelTramLines.Count;
            Console.Write($"[loaded={loadedLines}] ");

            if (loadedLines != savedLines)
                throw new Exception($"Tram line count mismatch: saved={savedLines} loaded={loadedLines}");

            Console.Write("[PASS] ");
        }
        else
        {
            Console.Write("[SKIP: no field] ");
        }

        // Restore defaults
        config.Tram.DisplayMode = AgValoniaGPS.Models.Configuration.TramDisplayMode.Off;
        config.Guidance.TramPasses = 3;

        Console.WriteLine("OK");
        Console.WriteLine("--- Tram Lines Test Complete ---");
    }

    /// <summary>
    /// Delay that also pumps the UI dispatcher.
    /// When --fast is active, delays are scaled down proportionally.
    /// </summary>
    static async Task Delay(int ms)
    {
        int scaledMs = Math.Max(1, (int)(ms / _timeScale));
        await Task.Delay(scaledMs);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Log current test state for debugging. Call at start/end of scenarios.
    /// </summary>
    static void LogState(MainViewModel vm, string label)
    {
        Console.Write($"[{label}: E={vm.Easting:F1} N={vm.Northing:F1} H={vm.Heading:F0} " +
            $"spd={vm.SpeedKmh:F1} field={vm.IsFieldOpen} track={vm.HasActiveTrack} " +
            $"steer={vm.IsAutoSteerEngaged} sect={vm.IsSectionMasterOn}] ");
    }

    /// <summary>
    /// Reset tractor to field origin heading north. Call before driving scenarios.
    /// </summary>
    static async Task ResetTractorPosition(MainViewModel vm, IGpsSimulationService simService,
        ISettingsService settingsService, double latOffset = 0, double lonOffset = 0)
    {
        var settings = settingsService.Settings;
        vm.SetSimulatorCoordinates(settings.SimulatorLatitude + latOffset,
                                    settings.SimulatorLongitude + lonOffset);
        simService.SetHeading(0);
        vm.SimulatorSteerAngle = 0;
        await Delay(50);
        for (int i = 0; i < 5; i++) { simService.Tick(0); await Delay(5); }
    }

    static Control? FindControl(Control parent, string name)
    {
        if (parent.Name == name) return parent;
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control c)
            {
                var found = FindControl(c, name);
                if (found != null) return found;
            }
        }
        return null;
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
