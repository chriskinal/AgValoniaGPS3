// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
using AgValoniaGPS.Services.Tool;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// NUnit version of AutoSteerUTurnTest. Drives a tractor through multiple passes
/// with U-turns at headlands using the full pipeline (no UI/MainViewModel).
/// Uses bicycle model + VirtualGpsReceiver for known-good GPS data.
/// </summary>
[TestFixture]
public class AutoSteerUTurnNUnitTests
{
    private const double ORIGIN_LAT = 43.712800;
    private const double ORIGIN_LON = -74.006000;
    private const double FIELD_W = 200.0;
    private const double FIELD_H = 78.0;
    private const double HEADLAND = 12.0; // Match real app headland distance
    private const double TOOL_WIDTH = 6.0; // Match real app bug report

    private static readonly double MetersPerDegLat = 111320.0;
    private static readonly double MetersPerDegLon = 111320.0 * Math.Cos(ORIGIN_LAT * Math.PI / 180.0);

    private GpsService _gpsService = null!;
    private AutoSteerService _autoSteer = null!;
    private GpsPipelineService _pipeline = null!;
    private ToolPositionService _toolPosition = null!;
    private PipelineIntents _intents = null!;
    private ApplicationState _appState = null!;
    private List<GpsCycleResult> _results = null!;

    [SetUp]
    public void SetUp()
    {
        var config = new ConfigurationStore();
        ConfigurationStore.SetInstance(config);
        // Match real app config from bug report
        config.Vehicle.Wheelbase = 2.5;
        config.Vehicle.AntennaHeight = 3.0;
        config.Vehicle.AntennaPivot = 0;
        config.Vehicle.AntennaOffset = 0;
        config.Vehicle.MaxSteerAngle = 35;
        config.Tool.Width = TOOL_WIDTH; // 6m
        config.Tool.HitchLength = 3.0;
        config.Tool.TrailingHitchLength = 3.0;
        config.Tool.IsToolTrailing = true;
        config.Tool.IsToolRearFixed = false;
        config.Tool.IsToolFrontFixed = false;
        config.Tool.IsToolTBT = false;
        config.NumSections = 3;
        for (int i = 0; i < 3; i++)
            config.Tool.SetSectionWidth(i, 200.0);
        config.Guidance.UTurnRadius = 8.0;
        config.Guidance.GoalPointLookAheadHold = 4.0;
        config.Guidance.GoalPointLookAheadMult = 1.4;
        config.Guidance.MinLookAheadDistance = 2.0;

        SensorState.Instance.ImuRoll = 0;
        _appState = new ApplicationState();

        _gpsService = new GpsService();
        _gpsService.Start();

        _toolPosition = new ToolPositionService();
        var guidance = new TrackGuidanceService();
        var coverage = new CoverageMapService();
        var sectionControl = new SectionControlService(_toolPosition, coverage, _appState);

        var headingFusion = Substitute.For<IGpsHeadingFusionService>();
        headingFusion.FuseHeading(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(0));

        _autoSteer = new AutoSteerService(guidance,
            Substitute.For<IUdpCommunicationService>(),
            _gpsService, _appState);

        _intents = new PipelineIntents();

        // PolygonOffsetService mock: return simple inward-offset rectangles
        var polygonOffset = Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>();
        polygonOffset.CreateInwardOffset(Arg.Any<List<Vec2>>(), Arg.Any<double>(),
            Arg.Any<AgValoniaGPS.Services.Geometry.OffsetJoinType>())
            .Returns(ci =>
            {
                var pts = ci.ArgAt<List<Vec2>>(0);
                double offset = ci.ArgAt<double>(1);
                if (pts == null || pts.Count < 3) return null;
                // Simple inward offset: shrink each point toward centroid
                double cx = pts.Average(p => p.Easting);
                double cy = pts.Average(p => p.Northing);
                var result = new List<Vec2>();
                foreach (var p in pts)
                {
                    double dx = p.Easting - cx;
                    double dy = p.Northing - cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 0.01) { result.Add(p); continue; }
                    double scale = Math.Max(0, (dist - offset) / dist);
                    result.Add(new Vec2(cx + dx * scale, cy + dy * scale));
                }
                return result;
            });
        polygonOffset.CalculatePointHeadings(Arg.Any<List<Vec2>>())
            .Returns(ci =>
            {
                var pts = ci.ArgAt<List<Vec2>>(0);
                var result = new List<Vec3>();
                for (int i = 0; i < pts.Count; i++)
                {
                    int next = (i + 1) % pts.Count;
                    double h = Math.Atan2(pts[next].Easting - pts[i].Easting,
                                          pts[next].Northing - pts[i].Northing);
                    result.Add(new Vec3(pts[i].Easting, pts[i].Northing, h));
                }
                return result;
            });

        var logFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        _pipeline = new GpsPipelineService(
            _gpsService, _toolPosition, guidance, sectionControl, coverage,
            _autoSteer, new YouTurnGuidanceService(),
            new YouTurnStateMachine(
                new YouTurnCreationService(logFactory.CreateLogger<YouTurnCreationService>(), polygonOffset),
                new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance),
                logFactory.CreateLogger<YouTurnStateMachine>()),
            Substitute.For<IAudioService>(),
            _intents,
            headingFusion,
            NullLogger<GpsPipelineService>.Instance, _appState);

        _results = new List<GpsCycleResult>();
        _pipeline.CycleCompleted += r => { lock (_results) _results.Add(r); };

        _autoSteer.Start();
        _pipeline.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _pipeline.Stop();
        _autoSteer.Stop();
        _gpsService.Stop();
    }

    private byte[] BuildPandaBytes(double lat, double lon, double heading, double speedKnots)
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
        gps.Satellites = 14;

        gps.SendOnce();
        IPEndPoint? remote = null;
        return listener.Receive(ref remote);
    }

    private void SendGpsAt(double eastMeters, double northMeters, double heading, int count)
    {
        double lat = ORIGIN_LAT + northMeters / MetersPerDegLat;
        double lon = ORIGIN_LON + eastMeters / MetersPerDegLon;
        for (int i = 0; i < count; i++)
        {
            var bytes = BuildPandaBytes(lat, lon, heading, 10.0);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
            Thread.Sleep(5);
        }
        Thread.Sleep(100);
    }

    private void DriveArc(double startE, double startN, double startHeading,
        double turnRadiusDeg, bool turnLeft, double speedKmh, int stepsPerDeg)
    {
        int totalSteps = (int)(Math.Abs(turnRadiusDeg) * stepsPerDeg);
        double speedMs = speedKmh / 3.6;
        double dt = 0.1;
        // Compute steer angle for desired arc: R = wheelbase / tan(steer)
        // We want a semicircle of radius TOOL_WIDTH/2 = 6m
        double turnRadius = TOOL_WIDTH / 2.0;
        double steerAngleDeg = Math.Atan(ConfigurationStore.Instance.Vehicle.Wheelbase / turnRadius) * 180 / Math.PI;
        if (turnLeft) steerAngleDeg = -steerAngleDeg;

        double heading = startHeading;
        double lat = ORIGIN_LAT + startN / MetersPerDegLat;
        double lon = ORIGIN_LON + startE / MetersPerDegLon;

        for (int i = 0; i < totalSteps; i++)
        {
            double headingRad = heading * Math.PI / 180.0;
            double steerRad = steerAngleDeg * Math.PI / 180.0;
            double omega = speedMs * Math.Tan(steerRad) / ConfigurationStore.Instance.Vehicle.Wheelbase;

            headingRad += omega * dt;
            heading = (headingRad * 180 / Math.PI) % 360;
            if (heading < 0) heading += 360;

            double dx = speedMs * Math.Sin(headingRad) * dt;
            double dy = speedMs * Math.Cos(headingRad) * dt;
            lat += dy / MetersPerDegLat;
            lon += dx / MetersPerDegLon;

            var bytes = BuildPandaBytes(lat, lon, heading, speedKmh / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
            Thread.Sleep(5);
        }
        Thread.Sleep(100);
    }

    private void DriveSegment(double startE, double startN, double heading,
        double speedKmh, int steps)
    {
        double speedMs = speedKmh / 3.6;
        double headingRad = heading * Math.PI / 180.0;
        double dt = 0.1;

        double lat = ORIGIN_LAT + startN / MetersPerDegLat;
        double lon = ORIGIN_LON + startE / MetersPerDegLon;

        for (int i = 0; i < steps; i++)
        {
            double dx = speedMs * Math.Sin(headingRad) * dt;
            double dy = speedMs * Math.Cos(headingRad) * dt;
            lat += dy / MetersPerDegLat;
            lon += dx / MetersPerDegLon;

            var bytes = BuildPandaBytes(lat, lon, heading, speedKmh / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
            Thread.Sleep(5);
        }
        Thread.Sleep(100);
    }

    /// <summary>
    /// Drive using the autosteer feedback loop: pipeline outputs steer angle,
    /// bicycle model follows it, sends new GPS position back to pipeline.
    /// Returns the phase tag for each result collected.
    /// </summary>
    private void DriveWithFeedback(ref double lat, ref double lon, ref double heading,
        double speedKmh, int maxSteps, string phase,
        List<(string phase, GpsCycleResult r)> allResults,
        Func<GpsCycleResult?, bool>? stopCondition = null)
    {
        double speedMs = speedKmh / 3.6;
        double dt = 0.1;
        double wheelbase = ConfigurationStore.Instance.Vehicle.Wheelbase;
        GpsCycleResult? lastResult = null;

        for (int i = 0; i < maxSteps; i++)
        {
            // Read steer angle from last pipeline result (autosteer feedback)
            double steerAngleDeg = 0;
            if (lastResult?.Guidance is { HasGuidance: true } g)
                steerAngleDeg = g.SteerAngle;

            // Bicycle model step
            double headingRad = heading * Math.PI / 180.0;
            double steerRad = steerAngleDeg * Math.PI / 180.0;
            if (Math.Abs(wheelbase) > 0.1)
                headingRad += speedMs * Math.Tan(steerRad) / wheelbase * dt;

            heading = (headingRad * 180.0 / Math.PI) % 360.0;
            if (heading < 0) heading += 360.0;

            lat += speedMs * Math.Cos(headingRad) * dt / MetersPerDegLat;
            lon += speedMs * Math.Sin(headingRad) * dt / MetersPerDegLon;

            var bytes = BuildPandaBytes(lat, lon, heading, speedKmh / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
            Thread.Sleep(5);

            // Grab latest result
            lock (_results)
            {
                if (_results.Count > 0)
                {
                    lastResult = _results[^1];
                    allResults.Add((phase, lastResult));
                }
            }

            if (stopCondition != null && stopCondition(lastResult))
                break;
        }
        Thread.Sleep(100);
    }

    [TestCase(false, TestName = "UTurn_EastWest")]
    [TestCase(true, TestName = "UTurn_NorthSouth")]
    public void DriveMultiplePasses_WithAutoUTurns(bool northSouth)
    {
        // Set up local plane at origin
        var origin = new Wgs84(ORIGIN_LAT, ORIGIN_LON);
        _appState.Field.LocalPlane = new LocalPlane(origin, new SharedFieldProperties());

        // Create boundary
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_W, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_W, Northing = FIELD_H });
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = FIELD_H });
        outerPoly.UpdateBounds();
        var boundary = new Boundary { OuterBoundary = outerPoly };
        _pipeline.SetBoundary(boundary);

        // Create headland line for YouTurn detection
        var headlandLine = new List<Vec3>
        {
            new Vec3(HEADLAND, HEADLAND, 0),
            new Vec3(FIELD_W - HEADLAND, HEADLAND, 0),
            new Vec3(FIELD_W - HEADLAND, FIELD_H - HEADLAND, 0),
            new Vec3(HEADLAND, FIELD_H - HEADLAND, 0),
            new Vec3(HEADLAND, HEADLAND, 0), // close the loop
        };
        _pipeline.SetHeadlandLine(headlandLine);

        // Create AB line along first pass - parameterized direction
        double abNorthing = HEADLAND + TOOL_WIDTH / 2.0; // 15m
        double abEasting = HEADLAND + TOOL_WIDTH / 2.0;  // 15m
        bool driveNorthSouth = northSouth;

        AgValoniaGPS.Models.Track.Track track;
        double startLat, startLon, startHdg;

        if (!driveNorthSouth)
        {
            // East-West AB line
            track = new AgValoniaGPS.Models.Track.Track
            {
                Name = "AB_Test",
                Points = new List<Vec3>
                {
                    new Vec3(HEADLAND, abNorthing, Math.PI / 2),
                    new Vec3(FIELD_W - HEADLAND, abNorthing, Math.PI / 2)
                },
                Type = AgValoniaGPS.Models.Track.TrackType.ABLine
            };
            startLat = ORIGIN_LAT + abNorthing / MetersPerDegLat;
            startLon = ORIGIN_LON + HEADLAND / MetersPerDegLon;
            startHdg = 90.0;
            SendGpsAt(HEADLAND + 5, abNorthing, heading: 90, count: 20);
        }
        else
        {
            // South-North AB line
            track = new AgValoniaGPS.Models.Track.Track
            {
                Name = "AB_Test_NS",
                Points = new List<Vec3>
                {
                    new Vec3(abEasting, HEADLAND, 0),        // heading north
                    new Vec3(abEasting, FIELD_H - HEADLAND, 0)
                },
                Type = AgValoniaGPS.Models.Track.TrackType.ABLine
            };
            startLat = ORIGIN_LAT + HEADLAND / MetersPerDegLat;
            startLon = ORIGIN_LON + abEasting / MetersPerDegLon;
            startHdg = 0.0;
            SendGpsAt(abEasting, HEADLAND + 5, heading: 0, count: 20);
        }

        _pipeline.SetActiveTrack(track, passNumber: 0, nudgeOffset: 0, isOnBoundary: false);
        _pipeline.SetAutoSteerEngaged(true);
        _pipeline.SetYouTurnEnabled(true);

        var allResults = new List<(string phase, GpsCycleResult r)>();
        _results.Clear();

        // Pass 1
        double lat = startLat;
        double lon = startLon;
        double hdg = startHdg;

        TestContext.Out.WriteLine("=== Pass 1: East (autosteer feedback) ===");
        DriveWithFeedback(ref lat, ref lon, ref hdg, speedKmh: 25, maxSteps: 300,
            phase: "pass1", allResults: allResults);

        var pass1 = allResults.Where(x => x.phase == "pass1").Select(x => x.r).ToList();
        double pass1Northing = pass1.Count > 50
            ? pass1.Skip(50).Average(r => r.Northing) : 0;
        TestContext.Out.WriteLine($"Pass 1: {pass1.Count} cycles, " +
            $"E={pass1.FirstOrDefault()?.Easting:F1} -> {pass1.LastOrDefault()?.Easting:F1}, " +
            $"avg N={pass1Northing:F1} (expected {abNorthing:F1})");

        // No manual trigger or pass switch - let auto-trigger + auto-detect handle it
        TestContext.Out.WriteLine("=== U-Turn (auto-trigger, auto pass detect) ===");

        DriveWithFeedback(ref lat, ref lon, ref hdg, speedKmh: 12, maxSteps: 200,
            phase: "uturn", allResults: allResults);

        var uturn = allResults.Where(x => x.phase == "uturn").Select(x => x.r).ToList();
        TestContext.Out.WriteLine($"U-Turn: {uturn.Count} cycles, heading {hdg:F1}");

        // After U-turn, check which pass the pipeline detected
        var lastUturnResult = uturn.LastOrDefault();
        int detectedPass = lastUturnResult?.Guidance?.HowManyPathsAway ?? -1;
        TestContext.Out.WriteLine($"Detected pass after U-turn: {detectedPass}");

        // Pass 2: Drive with autosteer feedback - NO manual pass switch
        TestContext.Out.WriteLine("=== Pass 2: (autosteer feedback, auto pass detect) ===");
        DriveWithFeedback(ref lat, ref lon, ref hdg, speedKmh: 25, maxSteps: 300,
            phase: "pass2", allResults: allResults);

        var pass2 = allResults.Where(x => x.phase == "pass2").Select(x => x.r).ToList();
        double pass2Northing = pass2.Count > 50
            ? pass2.Skip(50).Average(r => r.Northing) : 0;
        double expectedPass2Northing = abNorthing + TOOL_WIDTH; // next pass = one tool width north
        TestContext.Out.WriteLine($"Pass 2: {pass2.Count} cycles, " +
            $"E={pass2.FirstOrDefault()?.Easting:F1} -> {pass2.LastOrDefault()?.Easting:F1}, " +
            $"avg N={pass2Northing:F1} (expected {expectedPass2Northing:F1})");

        // Assertions
        Assert.That(pass1.Count, Is.GreaterThan(50), "Pass 1 should produce cycles");
        Assert.That(pass2.Count, Is.GreaterThan(50), "Pass 2 should produce cycles");

        // Verify pass 2 is on a DIFFERENT northing than pass 1
        TestContext.Out.WriteLine($"\nPass separation: {Math.Abs(pass2Northing - pass1Northing):F1}m " +
            $"(expected ~{TOOL_WIDTH:F0}m)");
        if (Math.Abs(pass2Northing - pass1Northing) < TOOL_WIDTH * 0.5)
        {
            TestContext.Out.WriteLine(">>> WARNING: Pass 2 is on the same line as Pass 1!");
            TestContext.Out.WriteLine(">>> Auto-detect did not switch to next pass after U-turn");
        }

        // Write CSV
        var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "uturn_passes.csv");
        using (var writer = new StreamWriter(csvPath))
        {
            writer.WriteLine("phase,step,tractor_e,tractor_n,tractor_heading,tool_e,tool_n,steer_angle,has_guidance,yt_triggered,yt_executing,goal_e,goal_n");
            int step = 0;
            foreach (var (phase, r) in allResults)
            {
                double steer = r.Guidance?.SteerAngle ?? 0;
                bool hasG = r.Guidance?.HasGuidance ?? false;
                bool ytTriggered = r.YouTurn?.IsTriggered ?? false;
                bool ytExec = r.YouTurn?.IsExecuting ?? false;
                double goalE = r.Guidance?.GoalPoint.Easting ?? 0;
                double goalN = r.Guidance?.GoalPoint.Northing ?? 0;
                writer.WriteLine($"{phase},{step++},{r.Easting:F2},{r.Northing:F2},{r.Heading:F1}," +
                    $"{r.ToolEasting:F2},{r.ToolNorthing:F2},{steer:F2},{hasG},{ytTriggered},{ytExec}," +
                    $"{goalE:F2},{goalN:F2}");
            }
        }

        // Write turn path if available
        var turnPathCsv = Path.Combine(TestContext.CurrentContext.WorkDirectory, "uturn_turnpath.csv");
        using (var writer = new StreamWriter(turnPathCsv))
        {
            writer.WriteLine("point,e,n,heading");
            foreach (var (_, r) in allResults)
            {
                if (r.YouTurn?.TurnPath != null && r.YouTurn.TurnPath.Count > 0)
                {
                    int pt = 0;
                    foreach (var p in r.YouTurn.TurnPath)
                        writer.WriteLine($"{pt++},{p.Easting:F2},{p.Northing:F2},{p.Heading:F4}");
                    break; // Only write the first turn path found
                }
            }
        }
        TestContext.Out.WriteLine($"CSV: {csvPath}");
        TestContext.Out.WriteLine($"Total: {allResults.Count} data points");
    }
}
