// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AgValoniaGPS.IntegrationTests.VirtualModules;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// End-to-end field workflow test using virtual UDP modules:
/// 1. Create field
/// 2. Drive boundary (rectangle)
/// 3. Build headland
/// 4. Create AB line
/// 5. Engage autosteer - vehicle steers itself via PGN 254 feedback loop
/// 6. Execute U-turn at headland
///
/// Run: dotnet run --project Tests/AgValoniaGPS.IntegrationTests/ -- --headless --field-test
/// </summary>
public static class FieldWorkflowTest
{
    private static string _screenshotDir = "";
    private static VirtualModuleHub? _hub;
    private static readonly System.Collections.Generic.List<string> _gifFrames = new();

    // Field geometry
    private const double ORIGIN_LAT = 43.712800;
    private const double ORIGIN_LON = -74.006000;
    private static readonly double MetersPerDegLat = 111320.0;
    private static readonly double MetersPerDegLon = 111320.0 * Math.Cos(ORIGIN_LAT * Math.PI / 180.0);

    public static async Task Run(Window window, MainViewModel vm)
    {
        _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots", "field-test");
        Directory.CreateDirectory(_screenshotDir);
        Console.WriteLine($"[FieldTest] Screenshots: {_screenshotDir}");

        // Disable built-in simulator and close its panel
        vm.IsSimulatorEnabled = false;
        vm.IsSimulatorPanelVisible = false;
        vm.State.UI.IsSimulatorPanelVisible = false;
        vm.State.UI.CloseDialog();
        await Pump(30);

        // Set up virtual module hub (GPS + steer + machine on real UDP ports)
        _hub = new VirtualModuleHub(hostReceivePort: 9999, moduleListenPort: 8888);
        _hub.Gps.Latitude = ORIGIN_LAT;
        _hub.Gps.Longitude = ORIGIN_LON;
        _hub.Gps.HeadingDegrees = 0;
        _hub.Gps.SpeedKnots = 0;
        _hub.Gps.FixQuality = 4;
        _hub.Gps.Satellites = 14;
        _hub.Steer.SimulateSteerResponse = false; // We control steer response via hub
        _hub.Steer.Start();
        _hub.Machine.Start();

        // Configure tool: 12m sprayer with 6 sections
        var config = ConfigurationStore.Instance;
        config.Tool.Width = 12.0;
        config.NumSections = 6;
        for (int i = 0; i < 6; i++)
            config.Tool.SetSectionWidth(i, 200.0);

        try
        {
            await Step1_CreateField(vm);
            await Step2_DriveBoundary(vm, window);
            await Step3_BuildHeadland(vm, window);
            await Step4_CreateABLine(vm, window);
            await Step5_EngageAutoSteer(vm, window);
            await Step6_DriveWithAutoSteerAndUTurn(vm, window);

            Console.WriteLine("[FieldTest] ALL STEPS PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FieldTest] FAILED: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Capture(window, "FAILED");
            throw;
        }
        finally
        {
            _hub?.Dispose();
        }
    }

    private static async Task Step1_CreateField(MainViewModel vm)
    {
        Console.Write("[Step 1] Create field... ");

        vm.NewFieldName = $"E2E_Test_{DateTime.Now:yyyyMMdd_HHmmss}";
        vm.NewFieldLatitude = ORIGIN_LAT;
        vm.NewFieldLongitude = ORIGIN_LON;
        vm.ConfirmNewFieldDialogCommand?.Execute(null);
        await Pump(50);

        Assert(vm.IsFieldOpen, "Field should be open");

        // Initialize local plane for WGS84 -> local coordinate conversion
        var origin = new Wgs84(ORIGIN_LAT, ORIGIN_LON);
        var sharedProps = new SharedFieldProperties();
        ApplicationState.Instance.Field.LocalPlane = new LocalPlane(origin, sharedProps);
        ApplicationState.Instance.Field.OriginLatitude = ORIGIN_LAT;
        ApplicationState.Instance.Field.OriginLongitude = ORIGIN_LON;

        // Send initial GPS to establish position
        await SendGpsFrames(0, 0, heading: 90, count: 20);

        Console.WriteLine($"OK ({vm.CurrentFieldName})");
    }

    private static async Task Step2_DriveBoundary(MainViewModel vm, Window window)
    {
        Console.Write("[Step 2] Drive boundary... ");

        vm.StartBoundaryRecordingCommand?.Execute(null);
        await Pump(30);
        Assert(vm.IsBoundaryRecording, "Should be recording boundary");

        _gifFrames.Clear();

        // Drive a 500m x 300m rectangle at 90 km/h (25 m/s, 2.5m per frame)
        // East side (500m = 200 frames)
        await DriveSegmentWithCapture(window, heading: 90, speedKmh: 90, frames: 200);
        Console.Write($"[E:{vm.BoundaryPointCount}pts] ");
        // North side (300m = 120 frames)
        await DriveSegmentWithCapture(window, heading: 0, speedKmh: 90, frames: 120);
        Console.Write($"[N:{vm.BoundaryPointCount}pts] ");
        // West side (500m = 200 frames)
        await DriveSegmentWithCapture(window, heading: 270, speedKmh: 90, frames: 200);
        Console.Write($"[W:{vm.BoundaryPointCount}pts] ");
        // South side (300m = 120 frames)
        await DriveSegmentWithCapture(window, heading: 180, speedKmh: 90, frames: 120);
        Console.Write($"[S:{vm.BoundaryPointCount}pts] ");

        Capture(window, "02_boundary_driven");

        vm.StopBoundaryRecordingCommand?.Execute(null);
        await Pump(50);

        Console.Write($"[points={vm.BoundaryPointCount}] ");
        Assert(vm.HasBoundary, $"Should have boundary (points: {vm.BoundaryPointCount})");
        Capture(window, "02b_boundary_closed");
        CaptureGifFrame(window);
        Console.WriteLine($"OK ({vm.BoundaryAreaHectares:F2} ha)");
    }

    private static async Task Step3_BuildHeadland(MainViewModel vm, Window window)
    {
        Console.Write("[Step 3] Build headland... ");

        vm.HeadlandDistance = 15.0;
        vm.BuildHeadlandCommand?.Execute(null);
        await Pump(50);

        Assert(vm.HasHeadland, "Should have headland");
        Capture(window, "03_headland_built");
        CaptureGifFrame(window); CaptureGifFrame(window); // Hold 2 frames
        Console.WriteLine("OK");
    }

    private static async Task Step4_CreateABLine(MainViewModel vm, Window window)
    {
        Console.Write("[Step 4] Create AB line... ");

        // Position at south-west inside headland
        await SendGpsFrames(30, 30, heading: 90, count: 20);

        // Start AB line creation
        vm.StartNewABLineCommand?.Execute(null);
        await Pump(30);

        // Set Point A
        vm.SetABPointCommand?.Execute(null);
        await Pump(30);
        Capture(window, "04a_point_a");

        // Drive east to Point B (~440m at 90 km/h = 176 frames)
        await DriveSegment(heading: 90, speedKmh: 90, frames: 176);

        // Set Point B
        vm.SetABPointCommand?.Execute(null);
        await Pump(50);

        Assert(vm.HasActiveTrack, "Should have active track");
        Capture(window, "04b_ab_line");
        CaptureGifFrame(window); CaptureGifFrame(window); // Hold 2 frames
        Console.WriteLine($"OK ({vm.SelectedTrack?.Name})");
    }

    private static async Task Step5_EngageAutoSteer(MainViewModel vm, Window window)
    {
        Console.Write("[Step 5] Engage autosteer... ");

        // Position near the AB line but slightly offset to test correction
        // AB line is at northing ~30, position at ~35 (5m off)
        await SendGpsFrames(30, 35, heading: 90, count: 20);

        vm.ToggleAutoSteerCommand?.Execute(null);
        await Pump(50);

        Assert(vm.IsAutoSteerEngaged, "Autosteer should be engaged");
        double initialXTE = vm.CrossTrackError;
        Console.Write($"[initialXTE={initialXTE:F2}m] ");
        Capture(window, "05_autosteer_engaged");
        CaptureGifFrame(window); CaptureGifFrame(window);
        Console.WriteLine("OK");
    }

    private static async Task Step6_DriveWithAutoSteerAndUTurn(MainViewModel vm, Window window)
    {
        Console.Write("[Step 6] Drive with autosteer + U-turn... ");

        vm.IsYouTurnEnabled = true;

        // Turn on sections (auto mode)
        vm.ToggleSectionMasterCommand?.Execute(null);
        await Pump(20);

        int frameCounter = 0;
        async Task OnFrame()
        {
            await Pump(10);
            if (++frameCounter % 10 == 0)
                CaptureGifFrame(window);
        }

        // Steer angle provider: read from ViewModel (since PGN 254 goes to 192.168.5.x, not localhost)
        Func<double> steerAngle = () => vm.SimulatorSteerAngle;

        // Drive east - autosteer should correct toward the AB line
        await _hub!.DriveWithAutoSteerAsync(speedKmh: 25, frames: 40,
            steerAngleProvider: steerAngle, onFrame: OnFrame);
        double xteAfterCorrection = vm.CrossTrackError;
        Console.Write($"[XTE={xteAfterCorrection:F2}cm] ");
        Capture(window, "06a_driving_autosteer");

        // Drive toward the east headland (~450m)
        await _hub.DriveWithAutoSteerAsync(speedKmh: 25, frames: 150,
            steerAngleProvider: steerAngle, onFrame: OnFrame);

        double headingBefore = _hub.Gps.HeadingDegrees;
        Console.Write($"[heading={headingBefore:F0}] ");
        Capture(window, "06b_approaching_headland");

        // Trigger manual U-turn at headland
        vm.ManualYouTurnRightCommand?.Execute(null);
        await Pump(10);
        Capture(window, "06c_uturn_triggered");

        // Drive the U-turn arc with autosteer
        await _hub.DriveWithAutoSteerAsync(speedKmh: 15, frames: 60,
            steerAngleProvider: steerAngle, onFrame: OnFrame);

        double headingAfter = _hub.Gps.HeadingDegrees;
        Console.Write($"[heading after uturn={headingAfter:F0}] ");
        Capture(window, "06d_uturn_arc");

        // Drive west on the next pass
        await _hub.DriveWithAutoSteerAsync(speedKmh: 25, frames: 50,
            steerAngleProvider: steerAngle, onFrame: OnFrame);
        Capture(window, "06e_next_pass");

        // Save video
        SaveGif(Path.Combine(_screenshotDir, "autosteer_drive.gif"));

        Console.WriteLine("OK");
    }

    #region GPS Helpers

    private static async Task SendGpsFrames(double eastMeters, double northMeters,
        double heading, int count)
    {
        if (_hub == null) return;

        _hub.Gps.Latitude = ORIGIN_LAT + northMeters / MetersPerDegLat;
        _hub.Gps.Longitude = ORIGIN_LON + eastMeters / MetersPerDegLon;
        _hub.Gps.HeadingDegrees = heading;
        _hub.Gps.SpeedKnots = 10.0;

        for (int i = 0; i < count; i++)
        {
            _hub.Gps.SendOnce();
            await Pump(50);
        }
    }

    private static async Task DriveSegmentWithCapture(Window window, double heading, double speedKmh, int frames)
    {
        if (_hub == null) return;

        _hub.Gps.HeadingDegrees = heading;
        _hub.Gps.SpeedKnots = speedKmh / 1.852;
        double stepTime = 1.0 / _hub.Gps.UpdateRateHz;

        for (int i = 0; i < frames; i++)
        {
            _hub.Gps.Step(stepTime);
            _hub.Gps.SendOnce();
            await Pump(10);
            // Capture every 20 frames (~2 seconds)
            if (i % 20 == 0)
                CaptureGifFrame(window);
        }
    }

    private static async Task DriveSegment(double heading, double speedKmh, int frames)
    {
        if (_hub == null) return;

        _hub.Gps.HeadingDegrees = heading;
        _hub.Gps.SpeedKnots = speedKmh / 1.852;
        double stepTime = 1.0 / _hub.Gps.UpdateRateHz;

        for (int i = 0; i < frames; i++)
        {
            _hub.Gps.Step(stepTime);
            _hub.Gps.SendOnce();
            await Pump(10); // Minimal delay - just pump UI thread
        }
    }

    #endregion

    #region Utilities

    private static void Capture(Window window, string name)
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

    private static void CaptureGifFrame(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var pixelSize = new PixelSize(
            Math.Max((int)window.Bounds.Width, 1),
            Math.Max((int)window.Bounds.Height, 1));
        var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        bitmap.Render(window);

        var framePath = Path.Combine(_screenshotDir, $"gif_frame_{_gifFrames.Count:D4}.png");
        bitmap.Save(framePath);
        _gifFrames.Add(framePath);
    }

    private static void SaveGif(string outputPath)
    {
        if (_gifFrames.Count == 0) return;

        try
        {
            using var writer = new System.IO.FileStream(outputPath, System.IO.FileMode.Create);
            var images = new System.Collections.Generic.List<byte[]>();

            // Use PIL via process to assemble GIF (avoid adding imageio dependency)
            var script = $@"
from PIL import Image
import sys
frames = []
for path in sys.argv[1:]:
    img = Image.open(path).convert('RGB').resize((640, 480), Image.LANCZOS)
    frames.append(img)
if frames:
    frames[0].save('{outputPath.Replace("'", "\\'")}', save_all=True, append_images=frames[1:], duration=1000, loop=0)
    print(f'GIF: {{len(frames)}} frames')
";
            var scriptPath = Path.Combine(_screenshotDir, "_gif.py");
            File.WriteAllText(scriptPath, script);

            var psi = new System.Diagnostics.ProcessStartInfo("python3", scriptPath + " " + string.Join(" ", _gifFrames))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(30000);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            Console.Write($"[{output.Trim()}] ");

            // Clean up frame files
            foreach (var f in _gifFrames)
                try { File.Delete(f); } catch { }
            try { File.Delete(scriptPath); } catch { }
        }
        catch (Exception ex)
        {
            Console.Write($"[GIF error: {ex.Message}] ");
        }
    }

    private static async Task Pump(int ms)
    {
        await Task.Delay(ms);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"Assertion failed: {message}");
    }

    #endregion
}
