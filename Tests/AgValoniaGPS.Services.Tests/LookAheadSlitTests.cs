// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using AgValoniaGPS.Services.Tool;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Look-ahead test: drive the tractor vertically (north) over a horizontal
/// slit of already-applied coverage with sections in auto mode.
/// Measures the resulting applied area to verify the look-ahead correctly
/// turns sections off over already-covered ground and back on after.
/// </summary>
[TestFixture]
public class LookAheadSlitTests
{
    private const double ORIGIN_LAT = 43.712800;
    private const double ORIGIN_LON = -74.006000;
    private const double FIELD_SIZE = 200.0;

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

        // Single 6m section, fixed rear
        config.Tool.Width = 6.0;
        config.NumSections = 1;
        config.Tool.SetSectionWidth(0, 600); // 6m
        config.Tool.HitchLength = 0;
        config.Tool.TrailingHitchLength = 0;
        config.Tool.IsToolRearFixed = true;
        config.Tool.IsToolTrailing = false;

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

    /// <summary>
    /// Drive north at a given easting for a given number of frames.
    /// </summary>
    private void DriveNorth(double easting, ref double lat, double speedKmh, int frames)
    {
        double speedMs = speedKmh / 3.6;
        double dt = 0.1;
        for (int i = 0; i < frames; i++)
        {
            // Invalidate coverage cache since wall-clock time doesn't advance in sync mode
            _sectionControl.InvalidateCoverageCache();
            lat += speedMs * dt / MetersPerDegLat;
            double lon = ORIGIN_LON + easting / MetersPerDegLon;
            var bytes = BuildPandaBytes(lat, lon, 0.0, speedKmh / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
        }
    }

    [TestCase(6.0, TestName = "LookAhead_Slit_6m")]
    [TestCase(3.0, TestName = "LookAhead_Slit_3m")]
    [TestCase(1.0, TestName = "LookAhead_Slit_1m")]
    public void DriveOverAppliedSlit_MeasureCoverage(double slitWidthMeters)
    {
        // Set up field
        _appState.Field.LocalPlane = new LocalPlane(
            new Wgs84(ORIGIN_LAT, ORIGIN_LON), new SharedFieldProperties());

        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_SIZE, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_SIZE, Northing = FIELD_SIZE });
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = FIELD_SIZE });
        outerPoly.UpdateBounds();
        _pipeline.SetBoundary(new Boundary { OuterBoundary = outerPoly });

        double speedKmh = 15.0;
        double toolCenter = FIELD_SIZE / 2; // E=100
        double slitNorthing = FIELD_SIZE / 2; // N=100

        // Phase 1: Mark a rectangular slit of pre-applied coverage directly
        // Slit centered at N=100, spanning full field width, height = slitWidthMeters
        double slitHalf = slitWidthMeters / 2.0;
        int cellsMarked = _coverage.MarkRectangleCovered(
            toolCenter - 20, toolCenter + 20,  // wide enough to cover the vertical pass
            slitNorthing - slitHalf, slitNorthing + slitHalf,
            zone: 0);

        double covAfterSlit = cellsMarked * 0.1 * 0.1; // cell area = 0.01 m2
        TestContext.Out.WriteLine($"=== Look-Ahead Slit Test (slit width = {slitWidthMeters}m) ===");
        TestContext.Out.WriteLine($"Slit coverage painted: {covAfterSlit:F1}m2");

        // Diagnostic: verify coverage was actually written to the detection layer
        bool slitCenterCovered = _coverage.IsPointCovered(toolCenter, slitNorthing);
        bool outsideSlitCovered = _coverage.IsPointCovered(toolCenter, slitNorthing + slitWidthMeters);
        TestContext.Out.WriteLine($"Slit center covered: {slitCenterCovered} (expected: true)");
        TestContext.Out.WriteLine($"Outside slit covered: {outsideSlitCovered} (expected: false)");

        // Also check coverage query used by section control
        var segResult = _coverage.GetSegmentCoverageMulti(
            new Models.Base.Vec2(toolCenter, slitNorthing), 0, 3.0, 0, 0);
        TestContext.Out.WriteLine($"Segment coverage at slit: current={segResult.Current.CoveragePercent:P0} lookOn={segResult.LookOn.CoveragePercent:P0} lookOff={segResult.LookOff.CoveragePercent:P0}");

        // Phase 2: Switch to auto mode and drive north over the slit
        _sectionControl.SetAllAuto();

        // Start south of slit, drive north across it
        double startNorthing = slitNorthing - 20; // 20m south of slit
        double lat2 = ORIGIN_LAT + startNorthing / MetersPerDegLat;

        // Warmup: establish position and auto section ON
        DriveNorth(toolCenter, ref lat2, speedKmh, 20);

        double covBeforeCross = _coverage.TotalWorkedArea;
        _results.Clear();

        // Drive north through the slit (40m total = 20m before + slit + 20m after)
        int crossFrames = (int)(40.0 / (speedKmh / 3.6 * 0.1)); // ~96 frames
        DriveNorth(toolCenter, ref lat2, speedKmh, crossFrames);

        double covAfterCross = _coverage.TotalWorkedArea;
        double newCoverage = covAfterCross - covBeforeCross;

        // Analyze section states during crossing
        List<GpsCycleResult> crossResults;
        lock (_results) crossResults = _results.ToList();

        int framesOn = 0, framesOff = 0;
        var sectionLog = new List<(double northing, bool isOn)>();

        foreach (var r in crossResults)
        {
            bool on = r.SectionStates != null && r.SectionStates.Length > 0 && r.SectionStates[0];
            if (on) framesOn++; else framesOff++;
            sectionLog.Add((r.Northing, on));
        }

        TestContext.Out.WriteLine($"\nPhase 2: Drive north across slit at E={toolCenter}");
        TestContext.Out.WriteLine($"Frames: {crossResults.Count} ({framesOn} ON, {framesOff} OFF)");
        TestContext.Out.WriteLine($"New coverage added: {newCoverage:F1}m2");

        // Find where section turns off and on
        bool prevOn = false;
        for (int i = 0; i < sectionLog.Count; i++)
        {
            if (sectionLog[i].isOn != prevOn)
            {
                TestContext.Out.WriteLine($"  N={sectionLog[i].northing:F1}: section {(sectionLog[i].isOn ? "ON" : "OFF")}");
                prevOn = sectionLog[i].isOn;
            }
        }

        // Expected: section ON before slit, OFF over slit (already covered), ON after slit
        // The total travel is ~40m, slit is ~6m wide
        // Without look-ahead: covered area = (40m - slitWidth) * 6m_toolWidth
        // With look-ahead: section turns off BEFORE reaching the slit and ON AFTER
        double expectedCoveredLength = 40.0 - slitWidthMeters; // only new ground
        double expectedCoverage = expectedCoveredLength * 6.0;

        TestContext.Out.WriteLine($"\nExpected new coverage (no overlap): {expectedCoverage:F0}m2");
        TestContext.Out.WriteLine($"Actual new coverage: {newCoverage:F1}m2");
        TestContext.Out.WriteLine($"Overlap ratio: {newCoverage / (40 * 6) * 100:F1}% of total pass");

        // Export CSV
        var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"lookahead_slit_{slitWidthMeters:F0}m.csv");
        using (var writer = new StreamWriter(csvPath))
        {
            writer.WriteLine("step,northing,easting,section_on");
            for (int i = 0; i < sectionLog.Count; i++)
                writer.WriteLine($"{i},{sectionLog[i].northing:F2},{toolCenter:F1},{(sectionLog[i].isOn ? 1 : 0)}");
        }
        TestContext.Out.WriteLine($"CSV: {csvPath}");

        // Verify section actually responded to the slit
        Assert.That(framesOff, Is.GreaterThan(0),
            "Section should turn OFF over already-covered slit");
        Assert.That(framesOn, Is.GreaterThan(framesOff),
            "Section should be ON for most of the pass (slit is narrow)");
    }
}
