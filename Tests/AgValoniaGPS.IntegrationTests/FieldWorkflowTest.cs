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
        vm.State.UI.IsSimulatorPanelVisible = false;
        vm.State.UI.CloseDialog();
        await Pump(300);

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
        await Pump(500);

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
        await Pump(300);
        Assert(vm.IsBoundaryRecording, "Should be recording boundary");

        // Drive a 500m x 300m rectangle at 36 km/h (10 m/s)
        // At 10Hz GPS, each frame = 1m. Need 500+300+500+300 = 1600 frames total.
        // East side (500m)
        await DriveSegment(heading: 90, speedKmh: 36, frames: 500);
        Console.Write($"[E:{vm.BoundaryPointCount}pts] ");
        // North side (300m)
        await DriveSegment(heading: 0, speedKmh: 36, frames: 300);
        Console.Write($"[N:{vm.BoundaryPointCount}pts] ");
        // West side (500m)
        await DriveSegment(heading: 270, speedKmh: 36, frames: 500);
        Console.Write($"[W:{vm.BoundaryPointCount}pts] ");
        // South side (300m back to start)
        await DriveSegment(heading: 180, speedKmh: 36, frames: 300);
        Console.Write($"[S:{vm.BoundaryPointCount}pts] ");

        Capture(window, "02_boundary_driven");

        vm.StopBoundaryRecordingCommand?.Execute(null);
        await Pump(500);

        Console.Write($"[points={vm.BoundaryPointCount}] ");
        Assert(vm.HasBoundary, $"Should have boundary (points: {vm.BoundaryPointCount})");
        Capture(window, "02b_boundary_closed");
        Console.WriteLine($"OK ({vm.BoundaryAreaHectares:F2} ha)");
    }

    private static async Task Step3_BuildHeadland(MainViewModel vm, Window window)
    {
        Console.Write("[Step 3] Build headland... ");

        vm.HeadlandDistance = 15.0;
        vm.BuildHeadlandCommand?.Execute(null);
        await Pump(500);

        Assert(vm.HasHeadland, "Should have headland");
        Capture(window, "03_headland_built");
        Console.WriteLine("OK");
    }

    private static async Task Step4_CreateABLine(MainViewModel vm, Window window)
    {
        Console.Write("[Step 4] Create AB line... ");

        // Position at south-west inside headland
        await SendGpsFrames(30, 30, heading: 90, count: 20);

        // Start AB line creation
        vm.StartNewABLineCommand?.Execute(null);
        await Pump(300);

        // Set Point A
        vm.SetABPointCommand?.Execute(null);
        await Pump(300);
        Capture(window, "04a_point_a");

        // Drive east to Point B (~440m)
        await DriveSegment(heading: 90, speedKmh: 36, frames: 440);

        // Set Point B
        vm.SetABPointCommand?.Execute(null);
        await Pump(500);

        Assert(vm.HasActiveTrack, "Should have active track");
        Capture(window, "04b_ab_line");
        Console.WriteLine($"OK ({vm.SelectedTrack?.Name})");
    }

    private static async Task Step5_EngageAutoSteer(MainViewModel vm, Window window)
    {
        Console.Write("[Step 5] Engage autosteer... ");

        // Position near the AB line
        await SendGpsFrames(30, 42, heading: 90, count: 20);

        vm.ToggleAutoSteerCommand?.Execute(null);
        await Pump(500);

        Assert(vm.IsAutoSteerEngaged, "Autosteer should be engaged");
        Capture(window, "05_autosteer_engaged");
        Console.WriteLine($"OK (XTE: {vm.CrossTrackError:F2}m)");
    }

    private static async Task Step6_DriveWithAutoSteerAndUTurn(MainViewModel vm, Window window)
    {
        Console.Write("[Step 6] Drive with autosteer + U-turn... ");

        vm.IsYouTurnEnabled = true;

        // Turn on sections (auto mode)
        vm.ToggleSectionMasterCommand?.Execute(null);
        await Pump(200);

        // Drive east with autosteer controlling heading via PGN 254
        // The hub reads steer commands and applies bicycle model to GPS heading
        await _hub!.DriveWithAutoSteerAsync(
            speedKmh: 18, frames: 80,
            onFrame: () => Pump(50));

        Capture(window, "06a_driving_autosteer");
        Console.Write($"[XTE={vm.CrossTrackError:F2}m] ");

        // Continue toward headland
        await _hub.DriveWithAutoSteerAsync(
            speedKmh: 18, frames: 60,
            onFrame: () => Pump(50));

        Capture(window, "06b_near_headland");

        // Trigger manual U-turn
        vm.ManualYouTurnRightCommand?.Execute(null);
        await Pump(500);
        Capture(window, "06c_uturn_triggered");

        // Drive the U-turn with autosteer
        await _hub.DriveWithAutoSteerAsync(
            speedKmh: 12, frames: 80,
            onFrame: () => Pump(50));

        Capture(window, "06d_uturn_complete");

        // Continue on next pass
        await _hub.DriveWithAutoSteerAsync(
            speedKmh: 18, frames: 40,
            onFrame: () => Pump(50));

        Capture(window, "06e_next_pass");
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
            await Pump(100); // Allow UI thread to process GPS event
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
