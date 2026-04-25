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
using Microsoft.Extensions.Logging.Abstractions;
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
    private const double HEADLAND = 15.0;
    private const double TOOL_WIDTH = 12.0;

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
        config.Vehicle.Wheelbase = 2.5;
        config.Vehicle.AntennaHeight = 0;
        config.Vehicle.AntennaPivot = 0;
        config.Vehicle.AntennaOffset = 0;
        config.Tool.Width = TOOL_WIDTH;
        config.Tool.HitchLength = 0;
        config.Tool.IsToolRearFixed = true;
        config.Tool.IsToolTrailing = false;
        config.Tool.IsToolFrontFixed = false;
        config.Tool.IsToolTBT = false;
        config.NumSections = 6;
        for (int i = 0; i < 6; i++)
            config.Tool.SetSectionWidth(i, 200.0); // 2m each
        config.Guidance.UTurnRadius = TOOL_WIDTH / 2.0;

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

        _pipeline = new GpsPipelineService(
            _gpsService, _toolPosition, guidance, sectionControl, coverage,
            _autoSteer, new YouTurnGuidanceService(),
            new YouTurnStateMachine(
                new YouTurnCreationService(
                    NullLogger<YouTurnCreationService>.Instance,
                    Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>()),
                new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance),
                NullLogger<YouTurnStateMachine>.Instance),
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

    [Test]
    public void DriveMultiplePasses_WithManualUTurns()
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

        // Create AB line along first pass
        double abNorthing = HEADLAND + TOOL_WIDTH / 2.0; // 21m
        var track = new AgValoniaGPS.Models.Track.Track
        {
            Name = "AB_Test",
            Points = new List<Vec3>
            {
                new Vec3(HEADLAND, abNorthing, 0),
                new Vec3(FIELD_W - HEADLAND, abNorthing, 0)
            },
            Type = AgValoniaGPS.Models.Track.TrackType.ABLine
        };
        _pipeline.SetActiveTrack(track, passNumber: 0, nudgeOffset: 0, isOnBoundary: false);
        _pipeline.SetAutoSteerEngaged(true);

        // Send initial position
        SendGpsAt(HEADLAND + 5, abNorthing, heading: 90, count: 20);

        // Collect ALL results (pass1 + uturn + pass2) for plotting
        var allResults = new List<(string phase, GpsCycleResult r)>();
        _results.Clear();

        // Drive first pass east
        TestContext.Out.WriteLine("=== Pass 1: East ===");
        DriveSegment(startE: HEADLAND, startN: abNorthing, heading: 90,
            speedKmh: 25, steps: 250);

        int pass1Count;
        lock (_results) pass1Count = _results.Count;
        TestContext.Out.WriteLine($"Pass 1 cycles: {pass1Count}");

        List<GpsCycleResult> pass1Results;
        lock (_results) pass1Results = _results.ToList();
        foreach (var r in pass1Results) allResults.Add(("pass1", r));

        if (pass1Results.Count > 10)
        {
            double startE = pass1Results[5].Easting;
            double endE = pass1Results[^1].Easting;
            TestContext.Out.WriteLine($"Pass 1: E={startE:F1} -> {endE:F1} ({endE - startE:F1}m east)");
            Assert.That(endE - startE, Is.GreaterThan(50),
                "Should travel >50m east on first pass");
        }

        // Drive U-turn arc using bicycle model (semicircle from east to west)
        TestContext.Out.WriteLine("=== U-Turn 1 ===");
        _pipeline.SetActiveTrack(track, passNumber: 1, nudgeOffset: 0, isOnBoundary: false);

        double uturnStartE = FIELD_W - HEADLAND;
        double uturnStartN = abNorthing;
        double pass2Northing = abNorthing + TOOL_WIDTH; // 33m
        double uturnRadius = TOOL_WIDTH / 2.0; // 6m

        // Drive a semicircle: heading 90 -> 270 (left turn, northward)
        // 180 degrees of arc at speed 12 km/h
        DriveArc(uturnStartE, uturnStartN, startHeading: 90,
            turnRadiusDeg: 180, turnLeft: true, speedKmh: 12, stepsPerDeg: 1);

        List<GpsCycleResult> uturnResults;
        lock (_results) uturnResults = _results.Skip(pass1Results.Count).ToList();
        foreach (var r in uturnResults) allResults.Add(("uturn", r));

        _results.Clear();

        // Drive second pass west
        TestContext.Out.WriteLine("=== Pass 2: West ===");
        DriveSegment(startE: FIELD_W - HEADLAND, startN: pass2Northing, heading: 270,
            speedKmh: 25, steps: 250);

        List<GpsCycleResult> pass2Results;
        lock (_results) pass2Results = _results.ToList();
        foreach (var r in pass2Results) allResults.Add(("pass2", r));

        if (pass2Results.Count > 10)
        {
            double startE = pass2Results[5].Easting;
            double endE = pass2Results[^1].Easting;
            TestContext.Out.WriteLine($"Pass 2: E={startE:F1} -> {endE:F1} ({startE - endE:F1}m west)");
            Assert.That(startE - endE, Is.GreaterThan(50),
                "Should travel >50m west on second pass");
        }

        // Verify both passes produced pipeline output
        TestContext.Out.WriteLine($"\nPass 1: {pass1Count} cycles, Pass 2: {pass2Results.Count} cycles");
        Assert.That(pass1Count, Is.GreaterThan(50), "Pass 1 should produce >50 cycles");
        Assert.That(pass2Results.Count, Is.GreaterThan(50), "Pass 2 should produce >50 cycles");

        // Write CSV for all phases
        var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "uturn_passes.csv");
        using (var writer = new StreamWriter(csvPath))
        {
            writer.WriteLine("phase,step,tractor_e,tractor_n,tractor_heading,tool_e,tool_n");
            int step = 0;
            foreach (var (phase, r) in allResults)
                writer.WriteLine($"{phase},{step++},{r.Easting:F2},{r.Northing:F2},{r.Heading:F1},{r.ToolEasting:F2},{r.ToolNorthing:F2}");
        }
        TestContext.Out.WriteLine($"CSV: {csvPath}");
    }
}
