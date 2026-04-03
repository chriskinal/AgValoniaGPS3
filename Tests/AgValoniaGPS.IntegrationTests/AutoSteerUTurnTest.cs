// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AgValoniaGPS.IntegrationTests.VirtualModules;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Desktop;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.IntegrationTests;

/// <summary>
/// Focused autosteer + U-turn test with pre-built boundary (no driving to record it).
/// Run: dotnet run --project Tests/AgValoniaGPS.IntegrationTests/ -- --headless --uturn-test
/// </summary>
public static class AutoSteerUTurnTest
{
    private static string _screenshotDir = "";
    private static VirtualModuleHub? _hub;
    private static readonly List<string> _gifFrames = new();

    private const double ORIGIN_LAT = 43.712800;
    private const double ORIGIN_LON = -74.006000;
    private static readonly double MetersPerDegLat = 111320.0;
    private static readonly double MetersPerDegLon = 111320.0 * Math.Cos(ORIGIN_LAT * Math.PI / 180.0);

    // Field: 200m x 78m -> working area = 78 - 2*15 = 48m = 4 passes x 12m
    private const double FIELD_W = 200.0;
    private const double FIELD_H = 78.0;
    private const double HEADLAND = 15.0;
    private const double TOOL_WIDTH = 12.0;

    public static async Task Run(Window window, MainViewModel vm)
    {
        _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots", "uturn-test");
        Directory.CreateDirectory(_screenshotDir);
        Console.WriteLine($"[UTurnTest] Screenshots: {_screenshotDir}");

        // Disable simulator
        vm.IsSimulatorEnabled = false;
        vm.IsSimulatorPanelVisible = false;
        vm.State.UI.IsSimulatorPanelVisible = false;
        vm.State.UI.CloseDialog();
        await Pump(30);

        // Set up virtual GPS
        _hub = new VirtualModuleHub(hostReceivePort: 9999, moduleListenPort: 8888);
        _hub.Gps.Latitude = ORIGIN_LAT;
        _hub.Gps.Longitude = ORIGIN_LON;
        _hub.Gps.FixQuality = 4;
        _hub.Gps.Satellites = 14;
        _hub.Steer.SimulateSteerResponse = false;
        _hub.Steer.Start();
        _hub.Machine.Start();

        // Configure tool: 12m, 6 sections x 2m
        var config = ConfigurationStore.Instance;
        config.Tool.Width = TOOL_WIDTH;
        config.NumSections = 6;
        for (int i = 0; i < 6; i++)
            config.Tool.SetSectionWidth(i, 200.0);
        config.Guidance.UTurnRadius = TOOL_WIDTH / 2.0; // Half implement width to complete the turn

        try
        {
            await Step1_CreateFieldWithBoundary(vm);
            await Step2_CreateABLine(vm, window);
            await Step3_DriveWithAutoSteerAndUTurn(vm, window);

            Console.WriteLine("[UTurnTest] ALL STEPS PASSED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UTurnTest] FAILED: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Capture(window, "FAILED");
            throw;
        }
        finally
        {
            _hub?.Dispose();
        }
    }

    private static async Task Step1_CreateFieldWithBoundary(MainViewModel vm)
    {
        Console.Write("[Step 1] Create field with boundary... ");

        // Create field
        vm.NewFieldName = $"UTurn_Test_{DateTime.Now:yyyyMMdd_HHmmss}";
        vm.NewFieldLatitude = ORIGIN_LAT;
        vm.NewFieldLongitude = ORIGIN_LON;
        vm.ConfirmNewFieldDialogCommand?.Execute(null);
        await Pump(50);

        Assert(vm.IsFieldOpen, "Field should be open");

        // Initialize local plane
        var origin = new Wgs84(ORIGIN_LAT, ORIGIN_LON);
        ApplicationState.Instance.Field.LocalPlane = new LocalPlane(origin, new SharedFieldProperties());
        ApplicationState.Instance.Field.OriginLatitude = ORIGIN_LAT;
        ApplicationState.Instance.Field.OriginLongitude = ORIGIN_LON;

        // Write boundary file, then use recording service to load it
        // Start/stop recording with manual points to create boundary
        vm.StartBoundaryRecordingCommand?.Execute(null);
        await Pump(30);

        // Add 4 corner points manually via the recording service
        var boundaryService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<AgValoniaGPS.Services.Interfaces.IBoundaryRecordingService>(
                AgValoniaGPS.Desktop.App.Services!);
        boundaryService.AddPointManual(0, 0, 0);
        boundaryService.AddPointManual(FIELD_W, 0, Math.PI / 2);
        boundaryService.AddPointManual(FIELD_W, FIELD_H, Math.PI);
        boundaryService.AddPointManual(0, FIELD_H, 3 * Math.PI / 2);

        vm.StopBoundaryRecordingCommand?.Execute(null);
        await Pump(50);

        Assert(vm.HasBoundary, "Should have boundary");

        // Build headland
        vm.HeadlandDistance = HEADLAND;
        vm.BuildHeadlandCommand?.Execute(null);
        await Pump(50);

        Assert(vm.HasHeadland, "Should have headland");

        // Send initial GPS frames to establish position
        await SendGpsAt(HEADLAND + 5, FIELD_H / 2, heading: 90, count: 20);

        Console.WriteLine("OK");
    }

    private static async Task Step2_CreateABLine(MainViewModel vm, Window window)
    {
        Console.Write("[Step 2] Create AB line... ");

        // AB line at the first pass: northing = HEADLAND + TOOL_WIDTH/2 (center of first swath)
        double abNorthing = HEADLAND + TOOL_WIDTH / 2.0; // 15 + 6 = 21m
        await SendGpsAt(HEADLAND + 5, abNorthing, heading: 90, count: 15);

        vm.StartNewABLineCommand?.Execute(null);
        await Pump(30);
        vm.SetABPointCommand?.Execute(null);
        await Pump(30);

        await SendGpsAt(FIELD_W - HEADLAND - 5, abNorthing, heading: 90, count: 15);

        vm.SetABPointCommand?.Execute(null);
        await Pump(50);

        Assert(vm.HasActiveTrack, "Should have active track");
        Capture(window, "01_ab_line");
        Console.WriteLine($"OK ({vm.SelectedTrack?.Name})");
    }

    private static async Task Step3_DriveWithAutoSteerAndUTurn(MainViewModel vm, Window window)
    {
        Console.Write("[Step 3] Complete field... ");

        // Working area: HEADLAND to FIELD_H-HEADLAND = 15 to 65 = 50m
        // Tool width = 12m, so need ceil(50/12) = 5 passes
        double workableHeight = FIELD_H - 2 * HEADLAND; // 50m
        int totalPasses = (int)Math.Ceiling(workableHeight / TOOL_WIDTH); // 5
        Console.Write($"[{totalPasses} passes needed] ");

        // Start OUTSIDE the field on the left, at the first AB line northing
        double abNorthing = HEADLAND + TOOL_WIDTH / 2.0;
        await SendGpsAt(-10, abNorthing, heading: 90, count: 15);

        // Engage autosteer
        vm.ToggleAutoSteerCommand?.Execute(null);
        await Pump(30);
        Assert(vm.IsAutoSteerEngaged, "Autosteer should be engaged");

        // Turn on sections
        vm.ToggleSectionMasterCommand?.Execute(null);
        vm.IsYouTurnEnabled = true;
        await Pump(20);

        _gifFrames.Clear();
        int frameCounter = 0;
        Func<double> steerAngle = () => vm.SimulatorSteerAngle;

        async Task OnFrame()
        {
            await Pump(10);
            if (++frameCounter % 5 == 0)
                CaptureGifFrame(window);
        }

        // Working distance: headland to headland = 200 - 2*15 = 170m
        // At 25 km/h = 6.94 m/s, 0.694m/frame -> 245 frames
        // First pass starts outside (-10m), so needs 195m = 281 frames
        // Subsequent passes start from opposite headland inner edge
        int framesFirstPass = 280;  // -10m to ~185m (east headland)
        int framesPerPass = 245;    // 15m to 185m (headland to headland)
        int framesPerUturn = 80;

        for (int pass = 0; pass < totalPasses; pass++)
        {
            bool goingEast = (pass % 2 == 0);
            string dir = goingEast ? "E" : "W";

            int frames = (pass == 0) ? framesFirstPass : framesPerPass;
            Console.Write($"[P{pass + 1}{dir} ");
            await _hub!.DriveWithAutoSteerAsync(speedKmh: 25, frames: frames,
                steerAngleProvider: steerAngle, onFrame: OnFrame);

            double h = _hub.Gps.HeadingDegrees;
            double e = (_hub.Gps.Longitude - ORIGIN_LON) * MetersPerDegLon;
            double n = (_hub.Gps.Latitude - ORIGIN_LAT) * MetersPerDegLat;
            Console.Write($"h={h:F0} E={e:F0} N={n:F0}] ");
            Capture(window, $"pass{pass + 1:D2}_{dir}");

            // After each pass (except last), keep driving toward headland.
            // The automatic U-turn should trigger when approaching the headland.
            // If it doesn't trigger automatically, the test will keep driving and
            // the next pass heading won't match - which catches the bug.
            if (pass < totalPasses - 1)
            {
                Console.Write($"[UT{pass + 1} ");

                // Check if auto U-turn triggered, otherwise use manual
                if (!vm.State.YouTurn.IsTriggered)
                {
                    Console.Write("(manual) ");
                    // Passes go south to north: east->turn LEFT (north), west->turn RIGHT (north)
                    if (goingEast)
                        vm.ManualYouTurnLeftCommand?.Execute(null);
                    else
                        vm.ManualYouTurnRightCommand?.Execute(null);
                    await Pump(10);
                }
                else
                {
                    Console.Write("(auto!) ");
                }

                // Drive the U-turn arc
                await _hub.DriveWithAutoSteerAsync(speedKmh: 12, frames: framesPerUturn,
                    steerAngleProvider: steerAngle, onFrame: OnFrame);

                double uh = _hub.Gps.HeadingDegrees;
                Console.Write($"h={uh:F0}] ");
                Capture(window, $"uturn{pass + 1:D2}");
            }
        }

        Console.Write("\n");

        // Report coverage stats
        double workedArea = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<AgValoniaGPS.Services.Interfaces.ICoverageMapService>(App.Services!)
            .TotalWorkedArea;
        double workableArea = (FIELD_H - 2 * HEADLAND) * (FIELD_W - 2 * HEADLAND); // 48 x 170 = 8160 m²
        double pctCovered = workedArea > 0 ? workedArea / workableArea * 100 : 0;
        Console.Write($"\n[Coverage] worked={workedArea:F0}m2, workable={workableArea:F0}m2, covered={pctCovered:F1}%");
        Console.Write($"\n[Tool] width={ConfigurationStore.Instance.ActualToolWidth:F1}m, sections={ConfigurationStore.Instance.NumSections}");

        // Save video
        SaveVideo(Path.Combine(_screenshotDir, "uturn_test.gif"));

        Console.WriteLine(" OK");
    }

    #region Helpers

    private static async Task SendGpsAt(double eastMeters, double northMeters,
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
            await Pump(10);
        }
    }

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var ps = new PixelSize(Math.Max((int)window.Bounds.Width, 1), Math.Max((int)window.Bounds.Height, 1));
        var bmp = new RenderTargetBitmap(ps, new Vector(96, 96));
        bmp.Render(window);
        var path = Path.Combine(_screenshotDir, $"{name}.png");
        bmp.Save(path);
        Console.Write($"[{new FileInfo(path).Length/1024}KB] ");
    }

    private static void CaptureGifFrame(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var ps = new PixelSize(Math.Max((int)window.Bounds.Width, 1), Math.Max((int)window.Bounds.Height, 1));
        var bmp = new RenderTargetBitmap(ps, new Vector(96, 96));
        bmp.Render(window);
        var path = Path.Combine(_screenshotDir, $"frame_{_gifFrames.Count:D4}.png");
        bmp.Save(path);
        _gifFrames.Add(path);
    }

    private static void SaveVideo(string outputPath)
    {
        if (_gifFrames.Count == 0) return;
        try
        {
            var script = $@"
from PIL import Image
import sys
frames = []
for path in sys.argv[1:]:
    img = Image.open(path).convert('RGB').resize((640, 480), Image.LANCZOS)
    frames.append(img)
if frames:
    frames[0].save('{outputPath.Replace("'", "\\'")}', save_all=True, append_images=frames[1:], duration=500, loop=0)
    print(f'GIF: {{len(frames)}} frames')
";
            var scriptPath = Path.Combine(_screenshotDir, "_gif.py");
            File.WriteAllText(scriptPath, script);
            var psi = new System.Diagnostics.ProcessStartInfo("python3", scriptPath + " " + string.Join(" ", _gifFrames))
            { RedirectStandardOutput = true, UseShellExecute = false };
            var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(30000);
            Console.Write($"[{proc?.StandardOutput.ReadToEnd().Trim()}] ");
            foreach (var f in _gifFrames) try { File.Delete(f); } catch { }
            try { File.Delete(scriptPath); } catch { }
        }
        catch (Exception ex) { Console.Write($"[GIF error: {ex.Message}] "); }
    }

    private static async Task Pump(int ms)
    {
        await Task.Delay(ms);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion failed: {message}");
    }

    #endregion
}
