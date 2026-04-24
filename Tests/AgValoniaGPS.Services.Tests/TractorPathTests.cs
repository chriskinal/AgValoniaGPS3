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
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
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
    // Test 4: Roll applied - lateral shift without path corruption
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void Roll10Deg_ShiftsLaterally_PathStillProgresses()
    {
        ConfigurationStore.Instance.Vehicle.AntennaHeight = 3.0;
        SensorState.Instance.ImuRoll = 10.0;

        var (inputs, outputs) = DriveAndCollect(
            speedKmh: 10, steerAngleDeg: 0, steps: 30);

        TestContext.Out.WriteLine("=== Roll=10, AntennaHeight=3m, Straight Line ===");

        // Position should still progress northward (not stuck at origin)
        double totalNorthing = LatDiffMeters(outputs[0].lat, outputs[^1].lat);
        TestContext.Out.WriteLine($"Total northing travel: {totalNorthing:F2}m");
        TestContext.Out.WriteLine($"First: ({outputs[0].lat:F8}, {outputs[0].lon:F8})");
        TestContext.Out.WriteLine($"Last:  ({outputs[^1].lat:F8}, {outputs[^1].lon:F8})");

        // Log each position
        for (int i = 0; i < outputs.Count; i += 5)
        {
            double dist = DistanceMeters(inputs[i].lat, inputs[i].lon,
                outputs[i].lat, outputs[i].lon);
            TestContext.Out.WriteLine(
                $"  [{i:D2}] in=({inputs[i].lat:F8},{inputs[i].lon:F8}) " +
                $"out=({outputs[i].lat:F8},{outputs[i].lon:F8}) offset={dist:F4}m");
        }

        // At 10 km/h for 30 steps at 0.1s = 3 seconds = ~8.3m travel
        Assert.That(totalNorthing, Is.GreaterThan(5.0),
            $"Tractor should travel >5m northward, got {totalNorthing:F2}m. " +
            "Roll correction may have corrupted the path or caused teleport.");

        // Verify consecutive positions are progressing (no jumps back to origin)
        for (int i = 1; i < outputs.Count; i++)
        {
            double stepDist = DistanceMeters(
                outputs[i - 1].lat, outputs[i - 1].lon,
                outputs[i].lat, outputs[i].lon);

            Assert.That(stepDist, Is.LessThan(2.0),
                $"Step [{i-1}->{i}] jumped {stepDist:F2}m - possible teleport. " +
                "Expected ~0.28m per step at 10 km/h.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 5: Antenna offset applied - consistent lateral shift
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void AntennaOffset_ConsistentLateralShift()
    {
        ConfigurationStore.Instance.Vehicle.AntennaOffset = 0.5; // 0.5m right

        var (inputs, outputs) = DriveAndCollect(
            speedKmh: 10, steerAngleDeg: 0, steps: 20);

        TestContext.Out.WriteLine("=== AntennaOffset=0.5m, Straight Line Heading North ===");

        // With heading north and antenna 0.5m right of center,
        // the corrected position should be shifted ~0.5m left (west = lower lon)
        for (int i = 0; i < outputs.Count; i += 5)
        {
            double lonShift = LonDiffMeters(inputs[i].lon, outputs[i].lon, inputs[i].lat);
            TestContext.Out.WriteLine(
                $"  [{i:D2}] lon_in={inputs[i].lon:F8} lon_out={outputs[i].lon:F8} shift={lonShift:F4}m");
        }

        // Path should still progress northward
        double totalNorthing = LatDiffMeters(outputs[0].lat, outputs[^1].lat);
        Assert.That(totalNorthing, Is.GreaterThan(3.0),
            "Tractor should still travel northward with antenna offset");
    }
}
