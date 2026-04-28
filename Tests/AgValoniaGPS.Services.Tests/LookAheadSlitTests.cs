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

    /// <summary>
    /// Configure and create the full pipeline with specified section/look-ahead config.
    /// </summary>
    private void SetUpPipeline(int numSections, double totalToolWidth,
        double lookAheadOnSeconds = 1.0, double lookAheadOffSeconds = 0.5)
    {
        var config = new ConfigurationStore();
        ConfigurationStore.SetInstance(config);
        config.Vehicle.Wheelbase = 2.5;
        config.Vehicle.MaxSteerAngle = 35;

        config.Tool.Width = totalToolWidth;
        config.NumSections = numSections;
        double sectionWidthCm = (totalToolWidth / numSections) * 100.0;
        for (int i = 0; i < numSections; i++)
            config.Tool.SetSectionWidth(i, (int)sectionWidthCm);
        config.Tool.HitchLength = 0;
        config.Tool.TrailingHitchLength = 0;
        config.Tool.IsToolRearFixed = true;
        config.Tool.IsToolTrailing = false;
        config.Tool.LookAheadOnSetting = lookAheadOnSeconds;
        config.Tool.LookAheadOffSetting = lookAheadOffSeconds;

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
        _pipeline?.Stop();
        _autoSteer?.Stop();
        _gpsService?.Stop();
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

    private void DriveNorth(double easting, ref double lat, double speedKmh, int frames)
    {
        double speedMs = speedKmh / 3.6;
        double dt = 0.1;
        for (int i = 0; i < frames; i++)
        {
            _sectionControl.InvalidateCoverageCache();
            lat += speedMs * dt / MetersPerDegLat;
            double lon = ORIGIN_LON + easting / MetersPerDegLon;
            var bytes = BuildPandaBytes(lat, lon, 0.0, speedKmh / 1.852);
            _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
        }
    }

    private void SetUpField()
    {
        _appState.Field.LocalPlane = new LocalPlane(
            new Wgs84(ORIGIN_LAT, ORIGIN_LON), new SharedFieldProperties());

        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_SIZE, Northing = 0 });
        outerPoly.Points.Add(new BoundaryPoint { Easting = FIELD_SIZE, Northing = FIELD_SIZE });
        outerPoly.Points.Add(new BoundaryPoint { Easting = 0, Northing = FIELD_SIZE });
        outerPoly.UpdateBounds();
        _pipeline.SetBoundary(new Boundary { OuterBoundary = outerPoly });
    }

    /// <summary>
    /// Core test logic: paint a slit, drive north across it, analyze section response.
    /// Returns (framesOn, framesOff, newCoverage, sectionLog per section).
    /// </summary>
    private (int[] framesOn, int[] framesOff, double newCoverage,
             List<(double northing, bool[] sectionStates, int[] colorCodes)> log)
        RunSlitTest(double slitWidthMeters, double speedKmh, int numSections)
    {
        double toolCenter = FIELD_SIZE / 2;
        double slitNorthing = FIELD_SIZE / 2;
        double slitHalf = slitWidthMeters / 2.0;

        // Paint the slit
        _coverage.MarkRectangleCovered(
            toolCenter - 20, toolCenter + 20,
            slitNorthing - slitHalf, slitNorthing + slitHalf,
            zone: 0);

        _sectionControl.SetAllAuto();

        // Start south of slit, drive north across it
        double startNorthing = slitNorthing - 20;
        double lat = ORIGIN_LAT + startNorthing / MetersPerDegLat;

        // Warmup
        DriveNorth(toolCenter, ref lat, speedKmh, 20);

        double covBefore = _coverage.TotalWorkedArea;
        _results.Clear();

        // Drive through slit (40m)
        int crossFrames = (int)(40.0 / (speedKmh / 3.6 * 0.1));
        DriveNorth(toolCenter, ref lat, speedKmh, crossFrames);

        double newCoverage = _coverage.TotalWorkedArea - covBefore;

        List<GpsCycleResult> crossResults;
        lock (_results) crossResults = _results.ToList();

        int[] framesOn = new int[numSections];
        int[] framesOff = new int[numSections];
        var log = new List<(double northing, bool[] sectionStates, int[] colorCodes)>();

        foreach (var r in crossResults)
        {
            bool[] states = new bool[numSections];
            int[] colors = new int[numSections];
            for (int s = 0; s < numSections; s++)
            {
                bool on = r.SectionStates != null && s < r.SectionStates.Length && r.SectionStates[s];
                states[s] = on;
                colors[s] = r.SectionColorCodes != null && s < r.SectionColorCodes.Length
                    ? r.SectionColorCodes[s] : (on ? 2 : 0);
                if (on) framesOn[s]++; else framesOff[s]++;
            }
            log.Add((r.Northing, states, colors));
        }

        return (framesOn, framesOff, newCoverage, log);
    }

    // Color code names for logging
    private static readonly string[] ColorNames = { "OFF", "MANUAL", "AUTO_ON", "TURNING_OFF", "TURNING_ON", "AUTO_OFF" };

    private void LogResults(string testName, double slitWidthMeters, int numSections,
        int[] framesOn, int[] framesOff, double newCoverage,
        List<(double northing, bool[] sectionStates, int[] colorCodes)> log)
    {
        TestContext.Out.WriteLine($"=== {testName} ===");
        TestContext.Out.WriteLine($"Slit: {slitWidthMeters}m, Sections: {numSections}");

        for (int s = 0; s < numSections; s++)
        {
            TestContext.Out.WriteLine($"  Section {s}: {framesOn[s]} ON, {framesOff[s]} OFF");

            // Log transitions with color codes
            int prevColor = -1;
            for (int i = 0; i < log.Count; i++)
            {
                int color = log[i].colorCodes[s];
                if (color != prevColor)
                {
                    string colorName = color >= 0 && color < ColorNames.Length ? ColorNames[color] : $"?{color}";
                    TestContext.Out.WriteLine($"    N={log[i].northing:F1}: {colorName} (code={color})");
                    prevColor = color;
                }
            }
        }

        double toolWidth = ConfigurationStore.Instance.Tool.Width;
        TestContext.Out.WriteLine($"New coverage: {newCoverage:F1}m2");
        TestContext.Out.WriteLine($"Expected (no overlap): {(40.0 - slitWidthMeters) * toolWidth:F0}m2");
    }

    private string ExportCsv(string name, int numSections,
        List<(double northing, bool[] sectionStates, int[] colorCodes)> log)
    {
        var csvPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{name}.csv");
        using (var writer = new StreamWriter(csvPath))
        {
            var header = "step,northing";
            for (int s = 0; s < numSections; s++)
                header += $",section_{s},color_{s}";
            writer.WriteLine(header);

            for (int i = 0; i < log.Count; i++)
            {
                var line = $"{i},{log[i].northing:F2}";
                for (int s = 0; s < numSections; s++)
                    line += $",{(log[i].sectionStates[s] ? 1 : 0)},{log[i].colorCodes[s]}";
                writer.WriteLine(line);
            }
        }
        return csvPath;
    }

    #region Single Section, Default Look-Ahead

    [TestCase(6.0, TestName = "LookAhead_Slit_6m")]
    [TestCase(3.0, TestName = "LookAhead_Slit_3m")]
    [TestCase(1.0, TestName = "LookAhead_Slit_1m")]
    public void DriveOverAppliedSlit_SingleSection(double slitWidthMeters)
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0);
        SetUpField();

        var (framesOn, framesOff, newCoverage, log) = RunSlitTest(slitWidthMeters, 15.0, 1);

        LogResults($"Single section, slit={slitWidthMeters}m", slitWidthMeters, 1,
            framesOn, framesOff, newCoverage, log);
        ExportCsv($"lookahead_slit_{slitWidthMeters:F0}m", 1, log);

        Assert.That(framesOff[0], Is.GreaterThan(0),
            "Section should turn OFF over already-covered slit");
        Assert.That(framesOn[0], Is.GreaterThan(framesOff[0]),
            "Section should be ON for most of the pass");

        // Verify no brief ON blip in the middle of the slit
        int transitions = 0;
        bool prev = false;
        foreach (var entry in log)
        {
            if (entry.sectionStates[0] != prev)
            {
                transitions++;
                prev = entry.sectionStates[0];
            }
        }
        Assert.That(transitions, Is.LessThanOrEqualTo(3),
            "Should have at most 3 transitions (ON -> OFF -> ON), no blips");
    }

    #endregion

    #region Different Look-Ahead Times

    [TestCase(0.5, 0.25, TestName = "LookAhead_Short_0.5s_0.25s")]
    [TestCase(1.0, 0.5, TestName = "LookAhead_Default_1.0s_0.5s")]
    [TestCase(2.0, 1.0, TestName = "LookAhead_Long_2.0s_1.0s")]
    [TestCase(0.5, 0.5, TestName = "LookAhead_Equal_0.5s_0.5s")]
    public void DriveOverSlit_DifferentLookAheadTimes(double lookOnSec, double lookOffSec)
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: lookOnSec, lookAheadOffSeconds: lookOffSec);
        SetUpField();

        double slitWidth = 6.0;
        double speedKmh = 15.0;
        double speedMs = speedKmh / 3.6;

        var (framesOn, framesOff, newCoverage, log) = RunSlitTest(slitWidth, speedKmh, 1);

        double lookOnDist = speedMs * lookOnSec;
        double lookOffDist = speedMs * lookOffSec;

        TestContext.Out.WriteLine($"=== Look-Ahead: ON={lookOnSec}s ({lookOnDist:F1}m), OFF={lookOffSec}s ({lookOffDist:F1}m) ===");
        LogResults($"LookAhead ON={lookOnSec}s OFF={lookOffSec}s", slitWidth, 1,
            framesOn, framesOff, newCoverage, log);
        ExportCsv($"lookahead_on{lookOnSec:F1}s_off{lookOffSec:F1}s", 1, log);

        Assert.That(framesOff[0], Is.GreaterThan(0),
            "Section should turn OFF over already-covered slit");

        // Verify no blips
        int transitions = 0;
        bool prev = false;
        foreach (var entry in log)
        {
            if (entry.sectionStates[0] != prev) { transitions++; prev = entry.sectionStates[0]; }
        }
        Assert.That(transitions, Is.LessThanOrEqualTo(3),
            $"Should have at most 3 transitions, got {transitions}");
    }

    #endregion

    #region Multiple Sections

    [TestCase(3, 6.0, TestName = "LookAhead_3Sections_6m")]
    [TestCase(6, 12.0, TestName = "LookAhead_6Sections_12m")]
    public void DriveOverSlit_MultipleSections(int numSections, double toolWidth)
    {
        SetUpPipeline(numSections: numSections, totalToolWidth: toolWidth);
        SetUpField();

        double slitWidth = 6.0;
        var (framesOn, framesOff, newCoverage, log) = RunSlitTest(slitWidth, 15.0, numSections);

        LogResults($"{numSections} sections, tool={toolWidth}m", slitWidth, numSections,
            framesOn, framesOff, newCoverage, log);
        ExportCsv($"lookahead_{numSections}sec_{toolWidth:F0}m", numSections, log);

        // All sections should respond to the slit
        for (int s = 0; s < numSections; s++)
        {
            Assert.That(framesOff[s], Is.GreaterThan(0),
                $"Section {s} should turn OFF over already-covered slit");
            Assert.That(framesOn[s], Is.GreaterThan(framesOff[s]),
                $"Section {s} should be ON for most of the pass");
        }
    }

    [Test]
    public void DriveOverPartialSlit_OuterSectionsStayOn()
    {
        // 3 sections x 2m = 6m tool. Slit only covers center 2m.
        // Center section should turn off, outer sections should stay on.
        SetUpPipeline(numSections: 3, totalToolWidth: 6.0);
        SetUpField();

        double toolCenter = FIELD_SIZE / 2;
        double slitNorthing = FIELD_SIZE / 2;

        // Paint a narrow slit that only covers the center section (E=99..101)
        _coverage.MarkRectangleCovered(
            toolCenter - 1, toolCenter + 1,  // 2m wide, centered
            slitNorthing - 3, slitNorthing + 3,  // 6m tall
            zone: 0);

        _sectionControl.SetAllAuto();

        double lat = ORIGIN_LAT + (slitNorthing - 20) / MetersPerDegLat;
        DriveNorth(toolCenter, ref lat, 15.0, 20); // warmup

        _results.Clear();
        int crossFrames = (int)(40.0 / (15.0 / 3.6 * 0.1));
        DriveNorth(toolCenter, ref lat, 15.0, crossFrames);

        List<GpsCycleResult> crossResults;
        lock (_results) crossResults = _results.ToList();

        // Count OFF frames per section
        int[] offFrames = new int[3];
        foreach (var r in crossResults)
        {
            for (int s = 0; s < 3; s++)
            {
                bool on = r.SectionStates != null && s < r.SectionStates.Length && r.SectionStates[s];
                if (!on) offFrames[s]++;
            }
        }

        TestContext.Out.WriteLine($"=== Partial Slit (center 2m only) ===");
        TestContext.Out.WriteLine($"Section 0 (left): {offFrames[0]} OFF frames");
        TestContext.Out.WriteLine($"Section 1 (center): {offFrames[1]} OFF frames");
        TestContext.Out.WriteLine($"Section 2 (right): {offFrames[2]} OFF frames");

        // Center section should have the most OFF frames
        Assert.That(offFrames[1], Is.GreaterThan(0),
            "Center section should turn OFF over covered slit");
        Assert.That(offFrames[1], Is.GreaterThan(offFrames[0]),
            "Center section should have more OFF frames than left section");
        Assert.That(offFrames[1], Is.GreaterThan(offFrames[2]),
            "Center section should have more OFF frames than right section");
    }

    #endregion
}
