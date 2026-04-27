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
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests for asymmetric section layouts during turns.
/// Reproduces bug: 5+1+1+1+1 implement misses application on the narrow
/// sections side when turning sharply.
/// </summary>
[TestFixture]
public class AsymmetricSectionTurnTests
{
    private const double ORIGIN_LAT = 43.712800;
    private const double ORIGIN_LON = -74.006000;
    private const double FIELD_SIZE = 200.0;
    private const double HEADLAND = 12.0;

    private static readonly double MetersPerDegLat = 111320.0;
    private static readonly double MetersPerDegLon = 111320.0 * Math.Cos(ORIGIN_LAT * Math.PI / 180.0);

    private GpsService _gpsService = null!;
    private AutoSteerService _autoSteer = null!;
    private GpsPipelineService _pipeline = null!;
    private SectionControlService _sectionControl = null!;
    private CoverageMapService _coverage = null!;
    private ApplicationState _appState = null!;
    private List<GpsCycleResult> _results = null!;

    [SetUp]
    public void SetUp()
    {
        var config = new ConfigurationStore();
        ConfigurationStore.SetInstance(config);
        config.Vehicle.Wheelbase = 2.5;
        config.Vehicle.MaxSteerAngle = 35;

        // 2x5m = 10m total, symmetric for clearer testing
        config.Tool.Width = 10.0;
        config.NumSections = 2;
        config.Tool.SetSectionWidth(0, 500); // 5m left (inner during left turn)
        config.Tool.SetSectionWidth(1, 500); // 5m right (outer during left turn)

        // Fixed rear at rear axle (worst case for inner edge reversal)
        config.Tool.HitchLength = 0;
        config.Tool.TrailingHitchLength = 0;
        config.Tool.IsToolRearFixed = true;
        config.Tool.IsToolTrailing = false;

        config.Guidance.GoalPointLookAheadHold = 4.0;
        config.Guidance.GoalPointLookAheadMult = 1.4;
        config.Guidance.MinLookAheadDistance = 2.0;

        SensorState.Instance.ImuRoll = 0;
        _appState = new ApplicationState();

        _gpsService = new GpsService();
        _gpsService.Start();

        var toolPosition = new ToolPositionService();
        _coverage = new CoverageMapService();
        _sectionControl = new SectionControlService(toolPosition, _coverage, _appState);
        _sectionControl.MasterState = SectionMasterState.Auto;
        _sectionControl.SetAllAuto();
        _coverage.SetFieldBounds(-10, FIELD_SIZE + 10, -10, FIELD_SIZE + 10);

        var headingFusion = Substitute.For<IGpsHeadingFusionService>();
        headingFusion.FuseHeading(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<bool>(),
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(0));

        _autoSteer = new AutoSteerService(new TrackGuidanceService(),
            Substitute.For<IUdpCommunicationService>(),
            _gpsService, _appState);

        _pipeline = new GpsPipelineService(
            _gpsService, toolPosition, new TrackGuidanceService(),
            _sectionControl, _coverage,
            _autoSteer, new YouTurnGuidanceService(),
            new YouTurnStateMachine(
                new YouTurnCreationService(NullLogger<YouTurnCreationService>.Instance,
                    Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>()),
                new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance),
                NullLogger<YouTurnStateMachine>.Instance),
            Substitute.For<IAudioService>(),
            new PipelineIntents(),
            headingFusion,
            NullLogger<GpsPipelineService>.Instance, _appState);

        _pipeline.SynchronousMode = true;
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
        gps.Latitude = lat; gps.Longitude = lon;
        gps.HeadingDegrees = heading; gps.SpeedKnots = speedKnots;
        gps.FixQuality = 4; gps.Satellites = 14;
        gps.SendOnce();
        IPEndPoint? remote = null;
        return listener.Receive(ref remote);
    }

    [Test]
    public void InnerSection_SharpTurn_CoveragePaints()
    {
        // Set up field with boundary
        _appState.Field.LocalPlane = new LocalPlane(
            new Wgs84(ORIGIN_LAT, ORIGIN_LON), new SharedFieldProperties());

        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_SIZE, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_SIZE, Northing = FIELD_SIZE });
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = FIELD_SIZE });
        outerPoly.UpdateBounds();
        _pipeline.SetBoundary(new Boundary { OuterBoundary = outerPoly });

        // AB line in center of field heading east
        double abN = FIELD_SIZE / 2;
        var track = new AgValoniaGPS.Models.Track.Track
        {
            Name = "AB_Center",
            Points = new List<Vec3>
            {
                new Vec3(HEADLAND, abN, Math.PI / 2),
                new Vec3(FIELD_SIZE - HEADLAND, abN, Math.PI / 2)
            },
            Type = AgValoniaGPS.Models.Track.TrackType.ABLine
        };
        _pipeline.SetActiveTrack(track, passNumber: 0, nudgeOffset: 0, isOnBoundary: false);
        _pipeline.SetAutoSteerEngaged(true);

        // Set inner section (0) to manual ON, outer section (1) to OFF
        _sectionControl.SetSectionState(0, SectionButtonState.On);
        _sectionControl.SetSectionState(1, SectionButtonState.Off);

        double lat = ORIGIN_LAT + abN / MetersPerDegLat;
        double lon = ORIGIN_LON + 50 / MetersPerDegLon;
        double hdg = 90.0;
        double speedKmh = 15.0;
        double speedMs = speedKmh / 3.6;
        double dt = 0.1;
        double wheelbase = 2.5;

        // Phase 1: Drive 30m straight (baseline coverage)
        double coverageBefore = _coverage.TotalWorkedArea;
        for (int i = 0; i < 50; i++)
        {
            double headingRad = hdg * Math.PI / 180.0;
            lat += speedMs * Math.Cos(headingRad) * dt / MetersPerDegLat;
            lon += speedMs * Math.Sin(headingRad) * dt / MetersPerDegLon;
            var bytes = BuildPandaBytes(lat, lon, hdg, speedKmh / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
        }
        double coverageAfterStraight = _coverage.TotalWorkedArea;
        double straightCoverage = coverageAfterStraight - coverageBefore;

        // Phase 2: Sharp left turn (-25 deg) - inner section is on the turn-center side
        double coverageBeforeTurn = _coverage.TotalWorkedArea;
        var turnEdgePositions = new List<(double leftE, double leftN, double rightE, double rightN)>();

        for (int i = 0; i < 60; i++)
        {
            double steerDeg = -30.0; // Sharp enough for 5m half-section to reverse inner edge
            double headingRad = hdg * Math.PI / 180.0;
            double steerRad = steerDeg * Math.PI / 180.0;
            headingRad += speedMs * Math.Tan(steerRad) / wheelbase * dt;
            hdg = (headingRad * 180.0 / Math.PI) % 360.0;
            if (hdg < 0) hdg += 360.0;

            lat += speedMs * Math.Cos(headingRad) * dt / MetersPerDegLat;
            lon += speedMs * Math.Sin(headingRad) * dt / MetersPerDegLon;

            var bytes = BuildPandaBytes(lat, lon, hdg, speedKmh / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);

            // Record section edge positions for analysis
            lock (_results)
            {
                if (_results.Count > 0)
                {
                    var r = _results[^1];
                    // Get the tool heading and compute inner section edges
                    double toolH = r.ToolHeadingRadians;
                    double perpH = toolH + Math.PI / 2;
                    double toolE = r.ToolEasting;
                    double toolN = r.ToolNorthing;
                    // Section 0 (inner) goes from -5m to 0m (left half)
                    double leftE = toolE + Math.Sin(perpH) * (-5.0);
                    double leftN = toolN + Math.Cos(perpH) * (-5.0);
                    double rightE = toolE;
                    double rightN = toolN;
                    turnEdgePositions.Add((leftE, leftN, rightE, rightN));
                }
            }
        }
        double coverageAfterTurn = _coverage.TotalWorkedArea;
        double turnCoverage = coverageAfterTurn - coverageBeforeTurn;

        // Report
        TestContext.Out.WriteLine("=== Inner Section Sharp Turn Coverage Test ===");
        TestContext.Out.WriteLine($"Layout: 2x5m, inner section (left) ON only");
        TestContext.Out.WriteLine($"Straight: {straightCoverage:F1}m2 in 50 frames");
        TestContext.Out.WriteLine($"Turn (-25 deg): {turnCoverage:F1}m2 in 60 frames");
        TestContext.Out.WriteLine($"Turn/Straight ratio: {(straightCoverage > 0 ? turnCoverage / straightCoverage : 0):F2}");

        // Analyze edge movement during turn
        if (turnEdgePositions.Count > 1)
        {
            var innerEdgeDists = new List<double>();
            var outerEdgeDists = new List<double>();
            for (int i = 1; i < turnEdgePositions.Count; i++)
            {
                var prev = turnEdgePositions[i - 1];
                var curr = turnEdgePositions[i];
                double innerDist = Math.Sqrt(Math.Pow(curr.leftE - prev.leftE, 2) + Math.Pow(curr.leftN - prev.leftN, 2));
                double outerDist = Math.Sqrt(Math.Pow(curr.rightE - prev.rightE, 2) + Math.Pow(curr.rightN - prev.rightN, 2));
                innerEdgeDists.Add(innerDist);
                outerEdgeDists.Add(outerDist);
            }
            TestContext.Out.WriteLine($"\nInner edge movement: avg={innerEdgeDists.Average():F3}m/frame min={innerEdgeDists.Min():F3}m");
            TestContext.Out.WriteLine($"Outer edge movement: avg={outerEdgeDists.Average():F3}m/frame min={outerEdgeDists.Min():F3}m");
        }

        // Export CSV for plotting
        var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "inner_section_turn.csv");
        using (var writer = new StreamWriter(csvPath))
        {
            writer.WriteLine("step,tractor_e,tractor_n,heading,tool_e,tool_n,s0,s1");
            int step = 0;
            lock (_results)
            {
                foreach (var r in _results)
                {
                    var ss = r.SectionStates;
                    writer.WriteLine($"{step++},{r.Easting:F2},{r.Northing:F2},{r.Heading:F1}," +
                        $"{r.ToolEasting:F2},{r.ToolNorthing:F2}," +
                        $"{(ss != null && ss.Length > 0 && ss[0] ? 1 : 0)}," +
                        $"{(ss != null && ss.Length > 1 && ss[1] ? 1 : 0)}");
                }
            }
        }
        TestContext.Out.WriteLine($"\nCSV: {csvPath}");

        // The turn should produce SOME coverage - if it produces zero, the bug is confirmed
        if (turnCoverage < 0.1)
        {
            TestContext.Out.WriteLine($"\n>>> BUG CONFIRMED: Inner section produces ZERO coverage during sharp turn");
            TestContext.Out.WriteLine($">>> The coverage triangle strip becomes degenerate when the inner edge barely moves");
        }

        Assert.That(turnCoverage, Is.GreaterThan(1.0),
            $"Inner section should paint during turn, got {turnCoverage:F1}m2 (straight painted {straightCoverage:F1}m2)");
    }
}
