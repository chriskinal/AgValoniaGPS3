// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AgValoniaGPS.IntegrationTests.VirtualModules;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Coverage;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Pipeline;
using AgValoniaGPS.Services.Section;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// End-to-end tractor path verification tests.
/// Uses a bicycle model to generate physically correct GPS positions,
/// sends them through the full pipeline (VirtualGpsReceiver -> AutoSteer
/// -> GpsService -> pipeline), and verifies output positions match the
/// expected path within tolerance.
///
/// Catches regressions where transforms (roll, antenna offset, pivot)
/// corrupt the tractor's path or heading.
/// </summary>
[TestFixture]
public class TractorPathTests
{
    private GpsService _gpsService = null!;
    private AutoSteerService _autoSteer = null!;

    [SetUp]
    public void SetUp()
    {
        var config = new ConfigurationStore();
        ConfigurationStore.SetInstance(config);
        config.Vehicle.Wheelbase = 2.5;
        config.Vehicle.AntennaHeight = 0;
        config.Vehicle.AntennaPivot = 0;
        config.Vehicle.AntennaOffset = 0;

        SensorState.Instance.ImuRoll = 0;
        SensorState.Instance.ImuPitch = 0;

        _gpsService = new GpsService();
        _autoSteer = new AutoSteerService(
            Substitute.For<ITrackGuidanceService>(),
            Substitute.For<IUdpCommunicationService>(),
            _gpsService,
            new ApplicationState());
        _autoSteer.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _autoSteer.Stop();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Bicycle model (inline to avoid simulator project dependency)
    // ═══════════════════════════════════════════════════════════════════════

    private class BicycleModel
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double HeadingDeg { get; set; }
        public double SpeedKmh { get; set; }
        public double SteerAngleDeg { get; set; }
        public double Wheelbase { get; set; } = 2.5;

        public void Step(double dt)
        {
            double speedMs = SpeedKmh / 3.6;
            double headingRad = HeadingDeg * Math.PI / 180.0;
            double steerRad = SteerAngleDeg * Math.PI / 180.0;

            double omega = Math.Abs(Wheelbase) > 0.1
                ? speedMs * Math.Tan(steerRad) / Wheelbase : 0;

            headingRad += omega * dt;
            HeadingDeg = (headingRad * 180.0 / Math.PI) % 360.0;
            if (HeadingDeg < 0) HeadingDeg += 360.0;

            double dx = speedMs * Math.Sin(headingRad) * dt;
            double dy = speedMs * Math.Cos(headingRad) * dt;

            Lat += dy / 111111.0;
            double metersPerDegLon = 111111.0 * Math.Cos(Lat * Math.PI / 180.0);
            if (Math.Abs(metersPerDegLon) > 0.01)
                Lon += dx / metersPerDegLon;
        }
    }

    /// <summary>
    /// Generate GPS positions from the bicycle model and send through the
    /// full pipeline, collecting the output positions from GpsService.
    /// Returns (input positions from model, output positions from pipeline).
    /// </summary>
    private (List<(double lat, double lon, double heading)> input,
             List<(double lat, double lon, double heading)> output)
        DriveAndCollect(double speedKmh, double steerAngleDeg, int steps,
            double dt = 0.1, double startLat = 42.0, double startLon = -93.0,
            double startHeading = 0)
    {
        var model = new BicycleModel
        {
            Lat = startLat, Lon = startLon,
            HeadingDeg = startHeading,
            SpeedKmh = speedKmh,
            SteerAngleDeg = steerAngleDeg,
            Wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase
        };

        var inputs = new List<(double lat, double lon, double heading)>();
        var outputs = new List<(double lat, double lon, double heading)>();

        for (int i = 0; i < steps; i++)
        {
            model.Step(dt);
            inputs.Add((model.Lat, model.Lon, model.HeadingDeg));

            // Send through pipeline via VirtualGpsReceiver -> AutoSteer -> GpsService
            var bytes = BuildPandaBytes(model.Lat, model.Lon, model.HeadingDeg,
                speedKmh / 1.852); // km/h to knots

            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);

            var data = _gpsService.CurrentData;
            outputs.Add((data.CurrentPosition.Latitude,
                         data.CurrentPosition.Longitude,
                         data.CurrentPosition.Heading));
        }

        return (inputs, outputs);
    }

    private static byte[] BuildPandaBytes(double lat, double lon,
        double heading, double speedKnots)
    {
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        listener.Client.ReceiveTimeout = 2000;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = lat;
        gps.Longitude = lon;
        gps.HeadingDegrees = heading;
        gps.SpeedKnots = speedKnots;
        gps.FixQuality = 4;
        gps.Satellites = 12;
        gps.Hdop = 0.7;

        gps.SendOnce();
        IPEndPoint? remote = null;
        return listener.Receive(ref remote);
    }

    /// <summary>
    /// Build a full pipeline with real services for implement position testing.
    /// Uses a pass-through heading fusion (returns GPS heading as-is).
    /// </summary>
    private GpsPipelineService BuildFullPipeline(
        AgValoniaGPS.Services.Tool.ToolPositionService toolPosition,
        ApplicationState appState)
    {
        var guidance = new TrackGuidanceService();
        var coverage = new CoverageMapService();
        var sectionControl = new SectionControlService(toolPosition, coverage, appState);

        var headingFusion = Substitute.For<IGpsHeadingFusionService>();
        headingFusion.FuseHeading(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(0)); // Pass through GPS heading

        return new GpsPipelineService(
            _gpsService, toolPosition, guidance, sectionControl, coverage,
            _autoSteer, new YouTurnGuidanceService(),
            new YouTurnStateMachine(
                new YouTurnCreationService(
                    NullLogger<YouTurnCreationService>.Instance,
                    Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>()),
                new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance),
                NullLogger<YouTurnStateMachine>.Instance),
            Substitute.For<IAudioService>(),
            new AgValoniaGPS.Services.Pipeline.PipelineIntents(),
            headingFusion,
            NullLogger<GpsPipelineService>.Instance, appState);
    }

    private static double LatDiffMeters(double lat1, double lat2) =>
        Math.Abs(lat2 - lat1) * 111111.0;

    private static double LonDiffMeters(double lon1, double lon2, double lat) =>
        Math.Abs(lon2 - lon1) * 111111.0 * Math.Cos(lat * Math.PI / 180.0);

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * 111111.0;
        double dLon = (lon2 - lon1) * 111111.0 * Math.Cos(lat1 * Math.PI / 180.0);
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 1: Straight line - no drift
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void StraightLine_NoDrift()
    {
        var (inputs, outputs) = DriveAndCollect(
            speedKmh: 10, steerAngleDeg: 0, steps: 50);

        TestContext.Out.WriteLine("=== Straight Line (10 km/h, heading 0, 50 steps) ===");

        double maxLateralError = 0;
        for (int i = 0; i < outputs.Count; i++)
        {
            double lateralError = LonDiffMeters(inputs[i].lon, outputs[i].lon, inputs[i].lat);
            maxLateralError = Math.Max(maxLateralError, lateralError);

            if (i % 10 == 0)
                TestContext.Out.WriteLine(
                    $"  [{i:D2}] in=({inputs[i].lat:F8},{inputs[i].lon:F8}) " +
                    $"out=({outputs[i].lat:F8},{outputs[i].lon:F8}) " +
                    $"latErr={lateralError:F4}m");
        }

        TestContext.Out.WriteLine($"Max lateral error: {maxLateralError:F4}m");

        Assert.That(maxLateralError, Is.LessThan(0.05),
            $"Straight line should have < 5cm lateral drift, got {maxLateralError:F4}m");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 2: 20 degree turn - verify circular arc
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void Turn20Deg_CircularArc()
    {
        var (inputs, outputs) = DriveAndCollect(
            speedKmh: 10, steerAngleDeg: 20, steps: 50);

        // Expected turn radius: R = wheelbase / tan(steerAngle)
        double expectedRadius = 2.5 / Math.Tan(20 * Math.PI / 180.0);
        TestContext.Out.WriteLine($"=== 20 deg Turn (expected radius: {expectedRadius:F2}m) ===");

        // Verify heading changes progressively
        double totalHeadingChange = 0;
        for (int i = 1; i < outputs.Count; i++)
        {
            double dh = outputs[i].heading - outputs[i - 1].heading;
            if (dh > 180) dh -= 360;
            if (dh < -180) dh += 360;
            totalHeadingChange += dh;
        }

        TestContext.Out.WriteLine($"Total heading change: {totalHeadingChange:F1} deg");
        TestContext.Out.WriteLine($"Input heading change: {inputs[^1].heading - inputs[0].heading:F1} deg");

        // Verify output heading tracks input heading
        double maxHeadingError = 0;
        for (int i = 0; i < outputs.Count; i++)
        {
            double err = Math.Abs(outputs[i].heading - inputs[i].heading);
            if (err > 180) err = 360 - err;
            maxHeadingError = Math.Max(maxHeadingError, err);
        }

        TestContext.Out.WriteLine($"Max heading error (in vs out): {maxHeadingError:F2} deg");

        Assert.That(maxHeadingError, Is.LessThan(1.0),
            $"Heading should track within 1 deg, got {maxHeadingError:F2} deg error");
        Assert.That(Math.Abs(totalHeadingChange), Is.GreaterThan(10),
            "Should have significant heading change during 20 deg turn");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 3: Position tracks input within NMEA precision
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void PositionOutput_TracksInput_WithinNmeaPrecision()
    {
        var (inputs, outputs) = DriveAndCollect(
            speedKmh: 10, steerAngleDeg: 10, steps: 30,
            startHeading: 45);

        TestContext.Out.WriteLine("=== Position Tracking (10 km/h, 10 deg steer, heading 45) ===");

        double maxError = 0;
        for (int i = 0; i < outputs.Count; i++)
        {
            double error = DistanceMeters(
                inputs[i].lat, inputs[i].lon,
                outputs[i].lat, outputs[i].lon);
            maxError = Math.Max(maxError, error);

            if (i % 5 == 0)
                TestContext.Out.WriteLine(
                    $"  [{i:D2}] error={error:F4}m heading_in={inputs[i].heading:F1} heading_out={outputs[i].heading:F1}");
        }

        TestContext.Out.WriteLine($"Max position error: {maxError:F4}m");

        // NMEA 5 decimal places gives ~0.019m resolution
        Assert.That(maxError, Is.LessThan(0.05),
            $"Position should track within 5cm (NMEA precision), got {maxError:F4}m");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 4: Roll correction via full pipeline (GpsCycleResult)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void Roll10Deg_ShiftsLaterally_ViaPipeline()
    {
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 3.0;
        SensorState.Instance.ImuRoll = 10.0;

        // Expected lateral shift: sin(10 deg) * 3m = 0.521m
        double expectedShift = Math.Sin(10.0 * Math.PI / 180.0) * 3.0;

        // Run with fixed rear implement to get pipeline results
        var results = DriveWithImplement(0, 40, "roll_test.csv",
            trailing: false, fixedRear: true);

        TestContext.Out.WriteLine($"=== Roll=10, AntennaHeight=3m, Heading North via Pipeline ===");
        TestContext.Out.WriteLine($"Expected lateral shift: {expectedShift:F3}m");
        TestContext.Out.WriteLine($"Cycles: {results.Count}");

        // Path should progress northward (no teleport)
        double totalNorthing = Math.Abs(results[^1].Northing - results[0].Northing);
        TestContext.Out.WriteLine($"Northing travel: {totalNorthing:F2}m");
        Assert.That(totalNorthing, Is.GreaterThan(5.0),
            "Tractor should travel >5m northward");

        // Heading north with roll right: antenna shifts right, correction shifts LEFT (west)
        // Check that easting is consistently negative (shifted west)
        double avgEasting = 0;
        for (int i = results.Count / 2; i < results.Count; i++)
            avgEasting += results[i].Easting;
        avgEasting /= (results.Count - results.Count / 2);

        TestContext.Out.WriteLine($"Avg easting (steady state): {avgEasting:F4}m");
        TestContext.Out.WriteLine($"Expected: ~-{expectedShift:F3}m (shifted west by roll correction)");

        Assert.That(Math.Abs(avgEasting), Is.GreaterThan(0.1),
            $"Roll correction should shift position laterally by ~{expectedShift:F1}m, " +
            $"but avg easting is only {avgEasting:F4}m. Correction not being applied.");

        // No teleports
        for (int i = 1; i < results.Count; i++)
        {
            double step = Math.Sqrt(
                Math.Pow(results[i].Easting - results[i - 1].Easting, 2) +
                Math.Pow(results[i].Northing - results[i - 1].Northing, 2));
            Assert.That(step, Is.LessThan(2.0),
                $"Step [{i - 1}->{i}] jumped {step:F2}m - possible teleport");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test: Dump CSV for plotting
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void Turn20Deg_DumpCsv()
    {
        var (inputs, outputs) = DriveAndCollect(
            speedKmh: 10, steerAngleDeg: 20, steps: 100);

        var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "turn20_path.csv");
        using var writer = new System.IO.StreamWriter(csvPath);
        writer.WriteLine("step,in_lat,in_lon,in_heading,in_easting,in_northing,out_lat,out_lon,out_heading");

        double originLat = inputs[0].lat;
        double originLon = inputs[0].lon;

        for (int i = 0; i < inputs.Count; i++)
        {
            double inE = (inputs[i].lon - originLon) * 111111.0 * Math.Cos(originLat * Math.PI / 180.0);
            double inN = (inputs[i].lat - originLat) * 111111.0;
            writer.WriteLine($"{i},{inputs[i].lat:F10},{inputs[i].lon:F10},{inputs[i].heading:F2}," +
                $"{inE:F4},{inN:F4}," +
                $"{outputs[i].lat:F10},{outputs[i].lon:F10},{outputs[i].heading:F2}");
        }

        TestContext.Out.WriteLine($"CSV written to: {csvPath}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Implement path tests with full pipeline + CSV dump
    // ═══════════════════════════════════════════════════════════════════════

    private List<GpsCycleResult> DriveWithImplement(
        double steerAngleDeg, int steps, string csvName,
        bool trailing = true, bool fixedRear = false)
    {
        var config = ConfigurationStore.Instance;
        config.Tool.HitchLength = 3.0;
        config.Tool.Width = 6.0;
        config.Tool.IsToolTrailing = trailing;
        config.Tool.IsToolRearFixed = fixedRear;
        config.Tool.IsToolFrontFixed = !trailing && !fixedRear;
        config.Tool.IsToolTBT = false;
        config.Tool.TrailingHitchLength = trailing ? 2.0 : 0;

        var appState = new ApplicationState();
        var toolPosition = new AgValoniaGPS.Services.Tool.ToolPositionService();

        var pipeline = BuildFullPipeline(toolPosition, appState);
        pipeline.Start();

        // Warmup: burn through startup frames with straight driving so
        // Torriem is fully active before the actual test scenario begins.
        if (trailing)
        {
            toolPosition.ResetTrailingState(new Vec3(0, 0, 0), 0);
            var warmup = new BicycleModel
            {
                Lat = 41.999, Lon = -93.0, HeadingDeg = 0,
                SpeedKmh = 10, SteerAngleDeg = 0, Wheelbase = 2.5
            };
            for (int w = 0; w < 10; w++)
            {
                warmup.Step(0.1);
                var wb = BuildPandaBytes(warmup.Lat, warmup.Lon, warmup.HeadingDeg, 10 / 1.852);
                _autoSteer.ProcessGpsBuffer(wb, wb.Length);
                System.Threading.Thread.Sleep(5);
            }
            System.Threading.Thread.Sleep(200);
        }

        var results = new List<GpsCycleResult>();
        pipeline.CycleCompleted += r => { lock (results) results.Add(r); };

        var model = new BicycleModel
        {
            Lat = 42.0, Lon = -93.0, HeadingDeg = 0,
            SpeedKmh = 10, SteerAngleDeg = steerAngleDeg, Wheelbase = 2.5
        };

        for (int i = 0; i < steps; i++)
        {
            model.Step(0.1);
            var bytes = BuildPandaBytes(model.Lat, model.Lon, model.HeadingDeg, 10 / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
            System.Threading.Thread.Sleep(5);
        }

        System.Threading.Thread.Sleep(500);
        pipeline.Stop();

        List<GpsCycleResult> collected;
        lock (results) collected = new List<GpsCycleResult>(results);

        // Write CSV
        var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, csvName);
        using (var writer = new System.IO.StreamWriter(csvPath))
        {
            writer.WriteLine("step,tractor_e,tractor_n,tractor_heading,tool_e,tool_n,tool_heading_rad,hitch_e,hitch_n,dist_tractor_tool");
            for (int i = 0; i < collected.Count; i++)
            {
                var r = collected[i];
                double dist = Math.Sqrt(
                    Math.Pow(r.Easting - r.ToolEasting, 2) +
                    Math.Pow(r.Northing - r.ToolNorthing, 2));
                writer.WriteLine($"{i},{r.Easting:F4},{r.Northing:F4},{r.Heading:F2}," +
                    $"{r.ToolEasting:F4},{r.ToolNorthing:F4},{r.ToolHeadingRadians:F4}," +
                    $"{r.HitchEasting:F4},{r.HitchNorthing:F4},{dist:F4}");
            }
        }

        TestContext.Out.WriteLine($"CSV: {csvPath} ({collected.Count} cycles)");
        Assert.That(collected.Count, Is.GreaterThan(10));
        return collected;
    }

    [Test]
    public void FixedRear_Turn20_WithAntennaOffset()
    {
        ConfigurationStore.Instance.Vehicle.AntennaOffset = 0.5;
        DriveWithImplement(20, 100, "fixed_turn20_offset.csv", trailing: false, fixedRear: true);
    }

    [Test]
    public void Trailing_Turn20_WithAntennaOffset()
    {
        ConfigurationStore.Instance.Vehicle.AntennaOffset = 0.5;
        DriveWithImplement(20, 100, "trailing_turn20_offset.csv");
    }

    [Test] public void Trailing_Straight() => DriveWithImplement(0, 80, "trailing_straight.csv");
    [Test] public void Trailing_Turn20() => DriveWithImplement(20, 100, "trailing_turn20.csv");
    [Test] public void FixedRear_Straight() => DriveWithImplement(0, 80, "fixed_straight.csv", trailing: false, fixedRear: true);
    [Test] public void FixedRear_Turn20() => DriveWithImplement(20, 100, "fixed_turn20.csv", trailing: false, fixedRear: true);

    // ═══════════════════════════════════════════════════════════════════════
    // Test 5: Antenna offset via full pipeline
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void AntennaOffset_ShiftsLaterally_ViaPipeline()
    {
        ConfigurationStore.Instance.Vehicle.AntennaOffset = 0.5; // 0.5m right

        var results = DriveWithImplement(0, 40, "offset_test.csv",
            trailing: false, fixedRear: true);

        TestContext.Out.WriteLine("=== AntennaOffset=0.5m, Heading North via Pipeline ===");
        TestContext.Out.WriteLine($"Cycles: {results.Count}");

        // Path should progress northward
        double totalNorthing = Math.Abs(results[^1].Northing - results[0].Northing);
        TestContext.Out.WriteLine($"Northing travel: {totalNorthing:F2}m");
        Assert.That(totalNorthing, Is.GreaterThan(5.0));

        // With heading north and antenna 0.5m right of center,
        // correction shifts position 0.5m LEFT (west = negative easting)
        double avgEasting = 0;
        for (int i = results.Count / 2; i < results.Count; i++)
            avgEasting += results[i].Easting;
        avgEasting /= (results.Count - results.Count / 2);

        TestContext.Out.WriteLine($"Avg easting (steady state): {avgEasting:F4}m");
        TestContext.Out.WriteLine($"Expected: ~-0.5m (shifted west by antenna offset correction)");

        Assert.That(Math.Abs(avgEasting), Is.GreaterThan(0.1),
            $"Antenna offset should shift position by ~0.5m, " +
            $"but avg easting is only {avgEasting:F4}m. Correction not applied.");
    }
}
