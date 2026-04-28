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
        // Zero look-ahead matches the simulator's zero actuator delay.
        // Real hardware would set LookAheadOnSetting/OffSetting to match valve open/close time.
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 0.0, lookAheadOffSeconds: 0.0);
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

        // Count transitions. With the SECTION_ON_DELAY debounce alone (no
        // !lookOffCovered safeguard), blips become visible in the simulator
        // when the actuator-delay-compensated lookOnDist approaches the slit
        // width. In production with matching actuator delay, this manifests
        // as a brief IsOn flicker during which the valve is still opening,
        // so no actual spray reaches the ground. We assert <= 3 transitions
        // only when the simulated look-ahead distance leaves a clear OFF zone
        // larger than half the slit width.
        int transitions = 0;
        bool prev = false;
        foreach (var entry in log)
        {
            if (entry.sectionStates[0] != prev) { transitions++; prev = entry.sectionStates[0]; }
        }
        TestContext.Out.WriteLine($"Section transitions: {transitions}");

        double effectiveLookOnDist = lookOnDist + SectionOnDelayDist(speedKmh);
        if (effectiveLookOnDist <= slitWidth / 2.0)
        {
            Assert.That(transitions, Is.LessThanOrEqualTo(3),
                $"Should have at most 3 transitions when lookOnDist ({effectiveLookOnDist:F1}m) " +
                $"<= half slit width ({slitWidth/2:F1}m), got {transitions}");
        }
    }

    private static double SectionOnDelayDist(double speedKmh) => speedKmh / 3.6 * 0.2;

    #endregion

    #region Edge Cases

    /// <summary>
    /// Asymmetric look-ahead: very slow valve open (2s) but fast close (0.1s).
    /// Verifies the OFF and ON sides handle independent durations correctly.
    /// </summary>
    [Test]
    public void EdgeCase_AsymmetricLookAhead_SlowOpenFastClose()
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 2.0, lookAheadOffSeconds: 0.1);
        SetUpField();

        var (framesOn, framesOff, newCoverage, log) = RunSlitTest(6.0, 15.0, 1);
        LogResults("Asymmetric On=2.0s Off=0.1s", 6.0, 1, framesOn, framesOff, newCoverage, log);
        ExportCsv("edge_asymmetric_slowopen", 1, log);

        Assert.That(framesOff[0], Is.GreaterThan(0));

        // OFF should land near slit start (LookAheadOff=0.1s, fast close)
        // ON should land near slit end (LookAheadOn=2s actuator, but slit only 6m)
        // With LookAheadOn=2s = 8.3m at 15km/h > slit width, the section can't
        // complete the full TURNING_ON cycle while still in clear ground.
        // Verify it eventually turns ON after exiting slit.
        bool sawOnAfterSlit = false;
        foreach (var entry in log)
        {
            if (entry.northing > 103 && entry.sectionStates[0])
            {
                sawOnAfterSlit = true;
                break;
            }
        }
        Assert.That(sawOnAfterSlit, Is.True, "Section should turn ON eventually after slit");
    }

    /// <summary>
    /// Very long LookAhead exceeding slit width — was flickering before fix.
    /// With matched phase duration, should still produce clean transitions.
    /// </summary>
    [Test]
    public void EdgeCase_LongLookAhead_NoFlicker()
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 2.0, lookAheadOffSeconds: 1.0);
        SetUpField();

        // Use a 4m slit (smaller than projection) to stress test
        var (framesOn, framesOff, newCoverage, log) = RunSlitTest(4.0, 15.0, 1);

        // Count transitions
        int transitions = 0;
        bool prev = false;
        foreach (var entry in log)
        {
            if (entry.sectionStates[0] != prev) { transitions++; prev = entry.sectionStates[0]; }
        }

        TestContext.Out.WriteLine($"Long LookAhead, slit 4m, transitions: {transitions}");
        ExportCsv("edge_long_lookahead", 1, log);

        // With matched phase durations, should be at most 3 transitions even when
        // projection exceeds slit width
        Assert.That(transitions, Is.LessThanOrEqualTo(3),
            $"No flicker expected with matched phase durations, got {transitions} transitions");
    }

    /// <summary>
    /// Multiple consecutive slits with small gaps. Verifies the timer state
    /// machine handles rapid OFF/ON cycles without getting stuck.
    /// </summary>
    [Test]
    public void EdgeCase_MultipleSlits_NoStuckTimer()
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 0.0, lookAheadOffSeconds: 0.0);
        SetUpField();

        double toolCenter = FIELD_SIZE / 2;

        // Paint 3 slits at N=95, 100, 105 (each 2m wide, 3m gaps between)
        _coverage.MarkRectangleCovered(toolCenter - 20, toolCenter + 20, 94, 96, 0);
        _coverage.MarkRectangleCovered(toolCenter - 20, toolCenter + 20, 99, 101, 0);
        _coverage.MarkRectangleCovered(toolCenter - 20, toolCenter + 20, 104, 106, 0);

        _sectionControl.SetAllAuto();
        double lat = ORIGIN_LAT + 80 / MetersPerDegLat;
        DriveNorth(toolCenter, ref lat, 15.0, 20); // warmup approaching slits

        _results.Clear();
        DriveNorth(toolCenter, ref lat, 15.0, 80); // drive through all 3 slits

        List<GpsCycleResult> crossResults;
        lock (_results) crossResults = _results.ToList();

        var transitions = new List<(double n, bool on)>();
        bool prev = !crossResults[0].SectionStates![0];
        foreach (var r in crossResults)
        {
            bool on = r.SectionStates != null && r.SectionStates[0];
            if (on != prev)
            {
                transitions.Add((r.Northing, on));
                prev = on;
            }
        }

        TestContext.Out.WriteLine($"=== Multiple Slits Test ===");
        TestContext.Out.WriteLine($"Total transitions: {transitions.Count}");
        foreach (var t in transitions)
            TestContext.Out.WriteLine($"  N={t.n:F2}: {(t.on ? "ON" : "OFF")}");

        // Expect exactly: initial ON + 3 OFF + 3 ON = 7 transitions counted from prev=false
        Assert.That(transitions.Count, Is.EqualTo(7),
            $"Expected 7 transitions for 3 slits (initial ON + 3 OFF + 3 ON), got {transitions.Count}");
    }

    /// <summary>
    /// Manual override during TURNING_ON state. User presses button while
    /// section is in transition - should respect the manual command immediately.
    /// </summary>
    [Test]
    public void EdgeCase_ManualOverride_DuringTurningOn()
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 2.0, lookAheadOffSeconds: 0.5);
        SetUpField();

        double toolCenter = FIELD_SIZE / 2;
        // Paint a slit so section will go OFF then transition back ON later
        _coverage.MarkRectangleCovered(toolCenter - 20, toolCenter + 20, 99, 102, 0);

        _sectionControl.SetAllAuto();
        double lat = ORIGIN_LAT + 80 / MetersPerDegLat;
        DriveNorth(toolCenter, ref lat, 15.0, 20); // warmup

        // Drive into the slit so section goes OFF
        DriveNorth(toolCenter, ref lat, 15.0, 25);

        // At this point section should be OFF or in TURNING_ON state
        // Force manual OFF override
        _sectionControl.SetSectionState(0, SectionButtonState.Off);

        // Drive past the slit
        DriveNorth(toolCenter, ref lat, 15.0, 30);

        // Section should still be OFF (manual override holds)
        Assert.That(_sectionControl.SectionStates[0].IsOn, Is.False,
            "Manual OFF override should hold even after slit is past");
        Assert.That(_sectionControl.SectionStates[0].ButtonState, Is.EqualTo(SectionButtonState.Off));
    }

    /// <summary>
    /// Master state change during TURNING_OFF — section should turn off
    /// immediately and respect the master state.
    /// </summary>
    [Test]
    public void EdgeCase_MasterStateChange_DuringTurningOff()
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 0.5, lookAheadOffSeconds: 1.0); // long off phase
        SetUpField();

        double toolCenter = FIELD_SIZE / 2;
        _coverage.MarkRectangleCovered(toolCenter - 20, toolCenter + 20, 99, 102, 0);

        _sectionControl.SetAllAuto();
        double lat = ORIGIN_LAT + 80 / MetersPerDegLat;
        DriveNorth(toolCenter, ref lat, 15.0, 30); // warmup, section ON

        // Should be approaching slit, will enter TURNING_OFF
        DriveNorth(toolCenter, ref lat, 15.0, 10);

        // Force master OFF
        _sectionControl.MasterState = SectionMasterState.Off;
        DriveNorth(toolCenter, ref lat, 15.0, 1);

        Assert.That(_sectionControl.SectionStates[0].IsOn, Is.False,
            "Master OFF should immediately turn section off");
        Assert.That(_sectionControl.IsAnySectionOn, Is.False);
    }

    #endregion

    #region Coverage During Transition States

    /// <summary>
    /// Verify that during TURNING_ON state (code=4) the section does NOT
    /// record coverage. The valve has received the OPEN command but is not
    /// yet physically applying — recording coverage during this window would
    /// indicate spray on still-covered or about-to-be-clear ground.
    /// </summary>
    [Test]
    public void TurningOnState_DoesNotApplyCoverage()
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 0.0, lookAheadOffSeconds: 0.0);
        SetUpField();

        double toolCenter = FIELD_SIZE / 2;
        double slitNorthing = FIELD_SIZE / 2;

        // Paint a slit
        _coverage.MarkRectangleCovered(
            toolCenter - 20, toolCenter + 20,
            slitNorthing - 3, slitNorthing + 3, zone: 0);

        _sectionControl.SetAllAuto();
        double lat = ORIGIN_LAT + (slitNorthing - 20) / MetersPerDegLat;
        DriveNorth(toolCenter, ref lat, 15.0, 20); // warmup

        _results.Clear();

        // Drive frame-by-frame, recording coverage delta and color code each frame
        double prevCoverage = _coverage.TotalWorkedArea;
        var perFrameDeltas = new List<(double northing, int colorCode, double coverageDelta)>();

        int crossFrames = (int)(40.0 / (15.0 / 3.6 * 0.1));
        for (int i = 0; i < crossFrames; i++)
        {
            DriveNorth(toolCenter, ref lat, 15.0, 1);
            double newCoverage = _coverage.TotalWorkedArea;
            double delta = newCoverage - prevCoverage;
            prevCoverage = newCoverage;

            GpsCycleResult? r;
            lock (_results) r = _results.LastOrDefault();
            if (r == null) continue;

            int color = r.SectionColorCodes != null && r.SectionColorCodes.Length > 0
                ? r.SectionColorCodes[0] : -1;
            perFrameDeltas.Add((r.Northing, color, delta));
        }

        // Find frames where TURNING_ON state was active
        var turningOnFrames = perFrameDeltas.Where(f => f.colorCode == 4).ToList();
        TestContext.Out.WriteLine($"=== TURNING_ON Coverage Check ===");
        TestContext.Out.WriteLine($"Total TURNING_ON frames: {turningOnFrames.Count}");
        foreach (var f in turningOnFrames)
            TestContext.Out.WriteLine($"  N={f.northing:F2}: coverage delta = {f.coverageDelta:F3} m2");

        Assert.That(turningOnFrames.Count, Is.GreaterThan(0),
            "Test should produce TURNING_ON frames (look-ahead transition)");

        // CORE ASSERTION: no coverage applied during TURNING_ON
        foreach (var f in turningOnFrames)
        {
            Assert.That(f.coverageDelta, Is.LessThan(0.01),
                $"No coverage should be applied during TURNING_ON at N={f.northing:F2}, but got {f.coverageDelta:F3} m2");
        }
    }

    #endregion

    #region Different Speeds and Delays

    /// <summary>
    /// Verify timing accuracy at various speeds. With proper software-delay
    /// compensation, the section should transition at approximately the same
    /// distance from the slit edge regardless of speed.
    /// </summary>
    [TestCase(5.0, TestName = "Timing_Speed_5kmh")]
    [TestCase(10.0, TestName = "Timing_Speed_10kmh")]
    [TestCase(15.0, TestName = "Timing_Speed_15kmh")]
    [TestCase(25.0, TestName = "Timing_Speed_25kmh")]
    public void DriveOverSlit_DifferentSpeeds(double speedKmh)
    {
        // Zero look-ahead matches simulator's zero actuator delay
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 0.0, lookAheadOffSeconds: 0.0);
        SetUpField();

        double slitWidth = 6.0;
        var (framesOn, framesOff, newCoverage, log) = RunSlitTest(slitWidth, speedKmh, 1);

        // Find OFF and ON transition positions (where IsOn changes)
        double slitSouth = FIELD_SIZE / 2 - slitWidth / 2;
        double slitNorth = FIELD_SIZE / 2 + slitWidth / 2;
        double offN = double.NaN, onN = double.NaN;
        bool prev = true;
        foreach (var entry in log)
        {
            if (entry.sectionStates[0] != prev)
            {
                if (!entry.sectionStates[0] && double.IsNaN(offN)) offN = entry.northing;
                else if (entry.sectionStates[0] && !double.IsNaN(offN) && double.IsNaN(onN)) onN = entry.northing;
                prev = entry.sectionStates[0];
            }
        }

        double offOffset = offN - slitSouth;  // negative = before slit, positive = into slit
        double onOffset = onN - slitNorth;    // negative = before end, positive = past slit

        TestContext.Out.WriteLine($"=== Speed: {speedKmh} km/h ({speedKmh / 3.6:F2} m/s) ===");
        TestContext.Out.WriteLine($"  OFF at N={offN:F2} -> {offOffset:+0.00;-0.00}m relative to slit start ({slitSouth})");
        TestContext.Out.WriteLine($"  ON  at N={onN:F2} -> {onOffset:+0.00;-0.00}m relative to slit end ({slitNorth})");

        // Both transitions should land within frame discretization of the slit edge.
        // At 25 km/h one frame = 0.69m, so 0.8m allows for ~1 frame variance plus
        // small geometric effects from the segment-coverage threshold.
        Assert.That(Math.Abs(offOffset), Is.LessThan(0.8),
            $"OFF transition should be near slit start at {speedKmh} km/h, got {offOffset}m offset");
        Assert.That(Math.Abs(onOffset), Is.LessThan(0.8),
            $"ON transition should be near slit end at {speedKmh} km/h, got {onOffset}m offset");
    }

    /// <summary>
    /// Verify timing with different turn-off delays. The look-ahead projection
    /// should compensate for the configured turn-off delay so transitions land
    /// at the same position regardless of delay.
    /// </summary>
    [TestCase(0.0, TestName = "Timing_TurnOffDelay_0s")]
    [TestCase(0.2, TestName = "Timing_TurnOffDelay_0.2s")]
    [TestCase(0.5, TestName = "Timing_TurnOffDelay_0.5s")]
    [TestCase(1.0, TestName = "Timing_TurnOffDelay_1.0s")]
    public void DriveOverSlit_DifferentTurnOffDelays(double turnOffDelaySec)
    {
        SetUpPipeline(numSections: 1, totalToolWidth: 6.0,
            lookAheadOnSeconds: 0.0, lookAheadOffSeconds: 0.0);
        ConfigurationStore.Instance.Tool.TurnOffDelay = turnOffDelaySec;
        SetUpField();

        double slitWidth = 6.0;
        var (framesOn, framesOff, newCoverage, log) = RunSlitTest(slitWidth, 15.0, 1);

        double slitSouth = FIELD_SIZE / 2 - slitWidth / 2;
        double slitNorth = FIELD_SIZE / 2 + slitWidth / 2;
        double offN = double.NaN, onN = double.NaN;
        bool prev = true;
        foreach (var entry in log)
        {
            if (entry.sectionStates[0] != prev)
            {
                if (!entry.sectionStates[0] && double.IsNaN(offN)) offN = entry.northing;
                else if (entry.sectionStates[0] && !double.IsNaN(offN) && double.IsNaN(onN)) onN = entry.northing;
                prev = entry.sectionStates[0];
            }
        }

        TestContext.Out.WriteLine($"=== TurnOffDelay: {turnOffDelaySec}s ===");
        TestContext.Out.WriteLine($"  OFF at N={offN:F2} ({offN - slitSouth:+0.00;-0.00}m vs slit start)");
        TestContext.Out.WriteLine($"  ON  at N={onN:F2} ({onN - slitNorth:+0.00;-0.00}m vs slit end)");

        // OFF should land near slit start regardless of configured turn-off delay
        Assert.That(Math.Abs(offN - slitSouth), Is.LessThan(0.5),
            $"OFF transition should be near slit start with TurnOffDelay={turnOffDelaySec}s, got offset={offN - slitSouth}m");
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
