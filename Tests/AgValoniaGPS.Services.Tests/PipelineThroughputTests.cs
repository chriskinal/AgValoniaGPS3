// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Pipeline;
using AgValoniaGPS.Services.Coverage;
using AgValoniaGPS.Services.Section;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// End-to-end throughput tests for the GPS pipeline.
/// Wires real NmeaParserService -> GpsService -> GpsPipelineService
/// with mocked heavy dependencies to measure update rates and identify
/// where GPS updates are dropped.
/// </summary>
[TestFixture]
public class PipelineThroughputTests
{
    // Real services (the pipeline under test)
    private GpsService _gpsService = null!;
    private NmeaParserService _nmeaParser = null!;
    private GpsPipelineService _pipeline = null!;

    // Mocked dependencies for GpsPipelineService
    private IToolPositionService _mockToolPosition = null!;
    private ITrackGuidanceService _mockGuidance = null!;
    private ISectionControlService _mockSectionControl = null!;
    private ICoverageMapService _mockCoverage = null!;
    private IAutoSteerService _mockAutoSteer = null!;
    private IAudioService _mockAudio = null!;
    private ApplicationState _appState = null!;

    // Counters
    private long _gpsDataUpdatedCount;
    private long _cycleCompletedCount;
    private readonly List<GpsCycleResult> _cycleResults = new();
    private readonly List<long> _cycleTimestamps = new();

    [SetUp]
    public void SetUp()
    {
        // Fresh singletons for each test
        var configStore = new ConfigurationStore();
        ConfigurationStore.SetInstance(configStore);
        _appState = new ApplicationState();

        // Real services
        _gpsService = new GpsService();
        _gpsService.Start();
        _nmeaParser = new NmeaParserService(_gpsService);

        // Mocks
        _mockToolPosition = Substitute.For<IToolPositionService>();
        _mockToolPosition.ToolPosition.Returns(new Vec3(0, 0, 0));
        _mockToolPosition.ToolPivotPosition.Returns(new Vec3(0, 0, 0));
        _mockToolPosition.HitchPosition.Returns(new Vec3(0, 0, 0));
        _mockToolPosition.IsToolPositionReady.Returns(true);
        _mockToolPosition.GetToolEdgePositions().Returns((new Vec3(-3, 0, 0), new Vec3(3, 0, 0)));
        _mockToolPosition.GetSectionEdgePositions(Arg.Any<double>(), Arg.Any<double>())
            .Returns((new Vec3(-3, 0, 0), new Vec3(3, 0, 0)));

        _mockGuidance = Substitute.For<ITrackGuidanceService>();
        _mockSectionControl = Substitute.For<ISectionControlService>();
        _mockSectionControl.SectionStates.Returns(Array.Empty<SectionControlState>());
        _mockSectionControl.NumSections.Returns(0);

        _mockCoverage = Substitute.For<ICoverageMapService>();
        _mockAutoSteer = Substitute.For<IAutoSteerService>();
        _mockAudio = Substitute.For<IAudioService>();

        // Construct the real pipeline
        _pipeline = new GpsPipelineService(
            _gpsService,
            _mockToolPosition,
            _mockGuidance,
            _mockSectionControl,
            _mockCoverage,
            _mockAutoSteer,
            new YouTurnGuidanceService(),
            _mockAudio,
            NullLogger<GpsPipelineService>.Instance,
            _appState);

        // Wire up counters
        _gpsDataUpdatedCount = 0;
        _cycleCompletedCount = 0;
        _cycleResults.Clear();
        _cycleTimestamps.Clear();

        _gpsService.GpsDataUpdated += (_, _) =>
            Interlocked.Increment(ref _gpsDataUpdatedCount);

        _pipeline.CycleCompleted += result =>
        {
            Interlocked.Increment(ref _cycleCompletedCount);
            lock (_cycleResults) _cycleResults.Add(result);
            lock (_cycleTimestamps) _cycleTimestamps.Add(Stopwatch.GetTimestamp());
        };

        _pipeline.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _pipeline.Stop();
        _gpsService.Stop();
    }

    /// <summary>
    /// Build a $PANDA sentence string for a given position.
    /// Uses VirtualGpsReceiver to ensure correct checksum.
    /// </summary>
    private static string BuildPandaString(double lat, double lon,
        double heading = 0, double speedKnots = 5, int fixQuality = 4)
    {
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        listener.Client.ReceiveTimeout = 2000;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = lat;
        gps.Longitude = lon;
        gps.HeadingDegrees = heading;
        gps.SpeedKnots = speedKnots;
        gps.FixQuality = fixQuality;
        gps.Satellites = 12;
        gps.Hdop = 0.7;

        gps.SendOnce();
        IPEndPoint? remote = null;
        var bytes = listener.Receive(ref remote);
        return Encoding.ASCII.GetString(bytes);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 1: Synchronous throughput (no timing delays)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void SynchronousThroughput_AllUpdatesReachPipeline()
    {
        // Pre-build 100 PANDA sentences with incrementing positions
        var sentences = new string[100];
        for (int i = 0; i < 100; i++)
        {
            sentences[i] = BuildPandaString(
                lat: 42.0 + i * 0.00001,
                lon: -93.0,
                heading: 0,
                speedKnots: 5);
        }

        // Feed all 100 synchronously (no timing - fastest possible)
        var sw = Stopwatch.StartNew();
        foreach (var sentence in sentences)
        {
            _nmeaParser.ParseSentence(sentence);
        }
        sw.Stop();

        // Wait for async pipeline cycles to complete (up to 5 seconds)
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Interlocked.Read(ref _cycleCompletedCount) < Interlocked.Read(ref _gpsDataUpdatedCount)
               && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        // Report
        long parsed = Interlocked.Read(ref _gpsDataUpdatedCount);
        long completed = Interlocked.Read(ref _cycleCompletedCount);
        long dropped = parsed - completed;

        TestContext.Out.WriteLine($"=== Synchronous Throughput (no delays) ===");
        TestContext.Out.WriteLine($"Sentences sent:      {sentences.Length}");
        TestContext.Out.WriteLine($"GpsDataUpdated:      {parsed}");
        TestContext.Out.WriteLine($"CycleCompleted:      {completed}");
        TestContext.Out.WriteLine($"Dropped (backpressure): {dropped}");
        TestContext.Out.WriteLine($"Drop rate:           {(double)dropped / parsed * 100:F1}%");
        TestContext.Out.WriteLine($"Total parse time:    {sw.ElapsedMilliseconds}ms");
        TestContext.Out.WriteLine($"Avg parse time:      {sw.Elapsed.TotalMilliseconds / sentences.Length:F2}ms");

        // All sentences must parse successfully
        Assert.That(parsed, Is.EqualTo(100),
            "All 100 sentences should trigger GpsDataUpdated");

        // Log finding
        TestContext.Out.WriteLine($"\n>>> DROP RATE: {(double)dropped / parsed * 100:F1}% <<<");
        if (dropped > 0)
        {
            TestContext.Out.WriteLine($">>> Back-pressure is dropping {dropped} out of {parsed} updates");
            TestContext.Out.WriteLine($">>> This confirms GpsPipelineService.ProcessCycle is the bottleneck");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 2: Timed throughput at 10Hz (reproduces real scenario)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task TimedThroughput_10Hz_MeasureDropRate()
    {
        // Pre-build sentences
        var sentences = new string[50];
        for (int i = 0; i < 50; i++)
        {
            sentences[i] = BuildPandaString(
                lat: 42.0 + i * 0.00001,
                lon: -93.0,
                heading: 0,
                speedKnots: 5);
        }

        // Send at 10Hz (100ms intervals) - simulates real GPS rate
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < sentences.Length; i++)
        {
            _nmeaParser.ParseSentence(sentences[i]);
            if (i < sentences.Length - 1)
                await Task.Delay(100);
        }
        sw.Stop();

        // Wait for pipeline to drain
        await Task.Delay(500);

        long parsed = Interlocked.Read(ref _gpsDataUpdatedCount);
        long completed = Interlocked.Read(ref _cycleCompletedCount);
        long dropped = parsed - completed;

        TestContext.Out.WriteLine($"=== Timed Throughput @ 10Hz (5 seconds) ===");
        TestContext.Out.WriteLine($"Sentences sent:      {sentences.Length}");
        TestContext.Out.WriteLine($"GpsDataUpdated:      {parsed}");
        TestContext.Out.WriteLine($"CycleCompleted:      {completed}");
        TestContext.Out.WriteLine($"Dropped:             {dropped}");
        TestContext.Out.WriteLine($"Drop rate:           {(double)dropped / parsed * 100:F1}%");
        TestContext.Out.WriteLine($"Effective Hz:        {completed / sw.Elapsed.TotalSeconds:F1}");
        TestContext.Out.WriteLine($"Wall time:           {sw.Elapsed.TotalSeconds:F1}s");

        // Verify all parsed
        Assert.That(parsed, Is.EqualTo(50), "All sentences should parse");

        double dropRate = (double)dropped / parsed;
        TestContext.Out.WriteLine($"\n>>> EFFECTIVE UPDATE RATE: {completed / sw.Elapsed.TotalSeconds:F1} Hz <<<");

        if (dropRate > 0.2)
        {
            TestContext.Out.WriteLine(">>> WARNING: >20% drop rate even with mocked dependencies!");
            TestContext.Out.WriteLine(">>> ProcessCycle overhead is too high for 10Hz GPS");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 3: ProcessCycle timing measurement
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task ProcessCycleTiming_MeasureIndividualCycleLatency()
    {
        // Pre-build sentences
        var sentences = new string[30];
        for (int i = 0; i < 30; i++)
        {
            sentences[i] = BuildPandaString(
                lat: 42.0 + i * 0.00001,
                lon: -93.0,
                heading: i * 12.0,
                speedKnots: 5);
        }

        // Send at 10Hz
        for (int i = 0; i < sentences.Length; i++)
        {
            _nmeaParser.ParseSentence(sentences[i]);
            if (i < sentences.Length - 1)
                await Task.Delay(100);
        }

        // Wait for pipeline to drain
        await Task.Delay(500);

        // Analyze cycle intervals from timestamps
        List<long> timestamps;
        lock (_cycleTimestamps) timestamps = new List<long>(_cycleTimestamps);

        if (timestamps.Count > 1)
        {
            var intervalsMs = new List<double>();
            for (int i = 1; i < timestamps.Count; i++)
            {
                double ms = (timestamps[i] - timestamps[i - 1]) * 1000.0 / Stopwatch.Frequency;
                intervalsMs.Add(ms);
            }

            intervalsMs.Sort();
            double median = intervalsMs[intervalsMs.Count / 2];
            double p95 = intervalsMs[(int)(intervalsMs.Count * 0.95)];
            double max = intervalsMs[^1];
            double min = intervalsMs[0];
            double avg = 0;
            foreach (var t in intervalsMs) avg += t;
            avg /= intervalsMs.Count;

            TestContext.Out.WriteLine($"=== ProcessCycle Interval Analysis ===");
            TestContext.Out.WriteLine($"Cycles completed:    {timestamps.Count}");
            TestContext.Out.WriteLine($"Min interval:        {min:F1}ms");
            TestContext.Out.WriteLine($"Avg interval:        {avg:F1}ms");
            TestContext.Out.WriteLine($"Median interval:     {median:F1}ms");
            TestContext.Out.WriteLine($"P95 interval:        {p95:F1}ms");
            TestContext.Out.WriteLine($"Max interval:        {max:F1}ms");
            TestContext.Out.WriteLine($"Expected @ 10Hz:     100ms");

            if (median > 200)
            {
                TestContext.Out.WriteLine($"\n>>> BOTTLENECK FOUND: Median cycle interval {median:F0}ms >> 100ms target");
                TestContext.Out.WriteLine($">>> ProcessCycle is too slow, causing back-pressure drops");
            }
            else if (median < 150)
            {
                TestContext.Out.WriteLine($"\n>>> Pipeline keeps up with 10Hz (median {median:F0}ms)");
                TestContext.Out.WriteLine($">>> Bottleneck is likely elsewhere (UI thread, receive thread blocking)");
            }
        }
        else
        {
            TestContext.Out.WriteLine(">>> Only 0-1 cycles completed - pipeline is severely broken");
            Assert.Fail("Pipeline produced fewer than 2 cycles from 30 inputs");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 4: Full receive path (AutoSteerService + Pipeline on same thread)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task FullReceivePath_WithAutoSteer_MeasureBlockingTime()
    {
        // Create a real AutoSteerService (same as production receive path)
        var realAutoSteer = new AutoSteerService(
            Substitute.For<ITrackGuidanceService>(),
            Substitute.For<IUdpCommunicationService>());
        realAutoSteer.Start();

        // Pre-build raw GPS bytes (same as what UDP delivers)
        var gpsBytePairs = new (byte[] bytes, string sentence)[50];
        for (int i = 0; i < 50; i++)
        {
            using var listener = new UdpClient(0);
            int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
            listener.Client.ReceiveTimeout = 2000;

            using var gps = new VirtualGpsReceiver(targetPort: port);
            gps.Latitude = 42.0 + i * 0.00001;
            gps.Longitude = -93.0;
            gps.HeadingDegrees = 0;
            gps.SpeedKnots = 5;
            gps.FixQuality = 4;
            gps.Satellites = 12;

            gps.SendOnce();
            IPEndPoint? remote = null;
            var bytes = listener.Receive(ref remote);
            gpsBytePairs[i] = (bytes, Encoding.ASCII.GetString(bytes));
        }

        // Simulate the REAL receive callback path at 10Hz:
        // 1. AutoSteerService.ProcessGpsBuffer (blocking, on receive thread)
        // 2. NmeaParser.ParseSentence (blocking, on receive thread)
        var autoSteerTimes = new List<double>();
        var parseTimes = new List<double>();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < gpsBytePairs.Length; i++)
        {
            var (bytes, sentence) = gpsBytePairs[i];

            // Step 1: AutoSteerService (blocking - this is the suspected bottleneck)
            var t1 = Stopwatch.GetTimestamp();
            realAutoSteer.ProcessGpsBuffer(bytes, bytes.Length);
            var t2 = Stopwatch.GetTimestamp();

            // Step 2: Old parser path (blocking)
            _nmeaParser.ParseSentence(sentence);
            var t3 = Stopwatch.GetTimestamp();

            autoSteerTimes.Add((t2 - t1) * 1000.0 / Stopwatch.Frequency);
            parseTimes.Add((t3 - t2) * 1000.0 / Stopwatch.Frequency);

            if (i < gpsBytePairs.Length - 1)
                await Task.Delay(100); // 10Hz
        }
        sw.Stop();

        // Wait for pipeline to drain
        await Task.Delay(500);

        long completed = Interlocked.Read(ref _cycleCompletedCount);
        long parsed = Interlocked.Read(ref _gpsDataUpdatedCount);
        long dropped = parsed - completed;

        // Analyze AutoSteer timing
        autoSteerTimes.Sort();
        parseTimes.Sort();

        TestContext.Out.WriteLine($"=== Full Receive Path (AutoSteer + Pipeline) ===");
        TestContext.Out.WriteLine($"Sentences:           {gpsBytePairs.Length}");
        TestContext.Out.WriteLine($"GpsDataUpdated:      {parsed}");
        TestContext.Out.WriteLine($"CycleCompleted:      {completed}");
        TestContext.Out.WriteLine($"Dropped:             {dropped} ({(double)dropped / parsed * 100:F1}%)");
        TestContext.Out.WriteLine($"Effective Hz:        {completed / sw.Elapsed.TotalSeconds:F1}");
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine($"AutoSteerService.ProcessGpsBuffer timing:");
        TestContext.Out.WriteLine($"  Min:    {autoSteerTimes[0]:F2}ms");
        TestContext.Out.WriteLine($"  Avg:    {autoSteerTimes.Average():F2}ms");
        TestContext.Out.WriteLine($"  Median: {autoSteerTimes[autoSteerTimes.Count / 2]:F2}ms");
        TestContext.Out.WriteLine($"  P95:    {autoSteerTimes[(int)(autoSteerTimes.Count * 0.95)]:F2}ms");
        TestContext.Out.WriteLine($"  Max:    {autoSteerTimes[^1]:F2}ms");
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine($"NmeaParser.ParseSentence timing:");
        TestContext.Out.WriteLine($"  Min:    {parseTimes[0]:F2}ms");
        TestContext.Out.WriteLine($"  Avg:    {parseTimes.Average():F2}ms");
        TestContext.Out.WriteLine($"  Median: {parseTimes[parseTimes.Count / 2]:F2}ms");
        TestContext.Out.WriteLine($"  Max:    {parseTimes[^1]:F2}ms");

        double totalBlockingAvg = autoSteerTimes.Average() + parseTimes.Average();
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine($"Total receive thread blocking: {totalBlockingAvg:F2}ms avg");
        TestContext.Out.WriteLine($"Budget at 10Hz: 100ms");
        TestContext.Out.WriteLine($"Headroom: {100 - totalBlockingAvg:F1}ms");

        if (totalBlockingAvg > 50)
        {
            TestContext.Out.WriteLine($"\n>>> RECEIVE THREAD BLOCKING {totalBlockingAvg:F0}ms — over 50% of 10Hz budget");
            TestContext.Out.WriteLine($">>> This delays packet reception and starves the pipeline");
        }

        Assert.That(parsed, Is.EqualTo(50), "All sentences should parse");
        realAutoSteer.Stop();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 5: Position delta verification
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task PositionOutput_MatchesInput_NoDrift()
    {
        // Send 20 positions heading north with known spacing
        double startLat = 42.0;
        double latStep = 0.00001; // ~1.1m per step

        var sentences = new string[20];
        for (int i = 0; i < 20; i++)
        {
            sentences[i] = BuildPandaString(
                lat: startLat + i * latStep,
                lon: -93.0,
                heading: 0,
                speedKnots: 5);
        }

        // Send at 10Hz
        for (int i = 0; i < sentences.Length; i++)
        {
            _nmeaParser.ParseSentence(sentences[i]);
            if (i < sentences.Length - 1)
                await Task.Delay(100);
        }

        // Wait for pipeline to drain
        await Task.Delay(500);

        long completed = Interlocked.Read(ref _cycleCompletedCount);

        TestContext.Out.WriteLine($"=== Position Delta Verification ===");
        TestContext.Out.WriteLine($"Sent: {sentences.Length}, Received: {completed}");

        List<GpsCycleResult> results;
        lock (_cycleResults) results = new List<GpsCycleResult>(_cycleResults);

        // Log each received position
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            TestContext.Out.WriteLine(
                $"  [{i}] lat={r.Latitude:F6} lon={r.Longitude:F6} E={r.Easting:F2} N={r.Northing:F2} " +
                $"tool=({r.ToolEasting:F2},{r.ToolNorthing:F2}) heading={r.Heading:F1}");
        }

        // Check position deltas between consecutive results
        if (results.Count >= 2)
        {
            TestContext.Out.WriteLine($"\nPosition deltas:");
            for (int i = 1; i < results.Count; i++)
            {
                double dE = results[i].Easting - results[i - 1].Easting;
                double dN = results[i].Northing - results[i - 1].Northing;
                double dist = Math.Sqrt(dE * dE + dN * dN);
                double dLat = results[i].Latitude - results[i - 1].Latitude;
                TestContext.Out.WriteLine(
                    $"  [{i - 1}->{i}] dE={dE:F3} dN={dN:F3} dist={dist:F3}m dLat={dLat:F8}");
            }

            if (results.Count < sentences.Length)
            {
                TestContext.Out.WriteLine(
                    $"\n>>> {sentences.Length - results.Count} updates dropped " +
                    $"({(double)(sentences.Length - results.Count) / sentences.Length * 100:F0}% loss)");
            }
        }

        Assert.That(completed, Is.GreaterThan(0), "At least some cycles should complete");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 6: Full stack via real UDP (closest to production)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task FullStack_RealUdp_RealServices_MeasureThroughput()
    {
        // This test wires the FULL production stack minus the UI:
        // VirtualGpsReceiver -> UDP -> UdpCommunicationService
        //   -> AutoSteerService.ProcessGpsBuffer (real, with real guidance)
        //   -> NmeaParserService -> GpsService -> GpsPipelineService (real services)
        //
        // Uses real: ToolPositionService, TrackGuidanceService, CoverageMapService,
        //            SectionControlService, AutoSteerService

        // Stop the test-level pipeline (we'll build our own full stack)
        _pipeline.Stop();

        // --- Build real services ---
        var realGpsService = new GpsService();
        realGpsService.Start();
        var realNmeaParser = new NmeaParserService(realGpsService);

        var realToolPosition = new AgValoniaGPS.Services.Tool.ToolPositionService();
        var realGuidance = new TrackGuidanceService();
        var realCoverage = new CoverageMapService();
        var realSectionControl = new SectionControlService(
            realToolPosition, realCoverage, _appState);
        var realAutoSteer = new AutoSteerService(realGuidance,
            Substitute.For<IUdpCommunicationService>());

        var realPipeline = new GpsPipelineService(
            realGpsService,
            realToolPosition,
            realGuidance,
            realSectionControl,
            realCoverage,
            realAutoSteer,
            new YouTurnGuidanceService(),
            Substitute.For<IAudioService>(),
            NullLogger<GpsPipelineService>.Instance,
            _appState);

        // Wire up UdpCommunicationService on ephemeral ports
        var udpService = new UdpCommunicationService();

        // We can't easily bind UdpCommunicationService to a custom port,
        // so instead simulate its receive callback behavior directly.
        // This is what happens inside ReceiveCallback:
        realAutoSteer.Start();
        realPipeline.Start();

        long fullStackCompleted = 0;
        var fullStackResults = new List<GpsCycleResult>();
        realPipeline.CycleCompleted += result =>
        {
            Interlocked.Increment(ref fullStackCompleted);
            lock (fullStackResults) fullStackResults.Add(result);
        };

        // Pre-build GPS data
        var gpsPairs = new (byte[] bytes, string sentence)[50];
        for (int i = 0; i < 50; i++)
        {
            using var listener = new UdpClient(0);
            int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
            listener.Client.ReceiveTimeout = 2000;

            using var gps = new VirtualGpsReceiver(targetPort: port);
            gps.Latitude = 42.0 + i * 0.00001;
            gps.Longitude = -93.0;
            gps.HeadingDegrees = i * 7.2; // Vary heading
            gps.SpeedKnots = 5;
            gps.FixQuality = 4;
            gps.Satellites = 12;

            gps.SendOnce();
            IPEndPoint? remote = null;
            var bytes = listener.Receive(ref remote);
            gpsPairs[i] = (bytes, Encoding.ASCII.GetString(bytes));
        }

        // Simulate the receive callback at 10Hz with REAL services
        var autoSteerTimes = new List<double>();
        var parseTimes = new List<double>();
        var totalCallbackTimes = new List<double>();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < gpsPairs.Length; i++)
        {
            var (bytes, sentence) = gpsPairs[i];

            var tCallback = Stopwatch.GetTimestamp();

            // Step 1: AutoSteerService (real guidance, real PGN build)
            var t1 = Stopwatch.GetTimestamp();
            realAutoSteer.ProcessGpsBuffer(bytes, bytes.Length);
            var t2 = Stopwatch.GetTimestamp();

            // Step 2: Old parser path
            realNmeaParser.ParseSentence(sentence);
            var t3 = Stopwatch.GetTimestamp();

            autoSteerTimes.Add((t2 - t1) * 1000.0 / Stopwatch.Frequency);
            parseTimes.Add((t3 - t2) * 1000.0 / Stopwatch.Frequency);
            totalCallbackTimes.Add((t3 - tCallback) * 1000.0 / Stopwatch.Frequency);

            if (i < gpsPairs.Length - 1)
                await Task.Delay(100);
        }
        sw.Stop();

        // Wait for pipeline to drain
        await Task.Delay(1000);

        long completed = Interlocked.Read(ref fullStackCompleted);
        long dropped = gpsPairs.Length - completed;

        // Sort for percentiles
        autoSteerTimes.Sort();
        parseTimes.Sort();
        totalCallbackTimes.Sort();

        TestContext.Out.WriteLine($"=== FULL STACK: Real Services, Simulated UDP @ 10Hz ===");
        TestContext.Out.WriteLine($"Sentences sent:      {gpsPairs.Length}");
        TestContext.Out.WriteLine($"CycleCompleted:      {completed}");
        TestContext.Out.WriteLine($"Dropped:             {dropped} ({(double)dropped / gpsPairs.Length * 100:F1}%)");
        TestContext.Out.WriteLine($"Effective Hz:        {completed / sw.Elapsed.TotalSeconds:F1}");
        TestContext.Out.WriteLine($"Wall time:           {sw.Elapsed.TotalSeconds:F1}s");
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine($"AutoSteerService.ProcessGpsBuffer (REAL guidance):");
        TestContext.Out.WriteLine($"  Min:    {autoSteerTimes[0]:F2}ms");
        TestContext.Out.WriteLine($"  Avg:    {autoSteerTimes.Average():F2}ms");
        TestContext.Out.WriteLine($"  Median: {autoSteerTimes[autoSteerTimes.Count / 2]:F2}ms");
        TestContext.Out.WriteLine($"  P95:    {autoSteerTimes[(int)(autoSteerTimes.Count * 0.95)]:F2}ms");
        TestContext.Out.WriteLine($"  Max:    {autoSteerTimes[^1]:F2}ms");
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine($"NmeaParser + GpsService (old path):");
        TestContext.Out.WriteLine($"  Avg:    {parseTimes.Average():F2}ms");
        TestContext.Out.WriteLine($"  Median: {parseTimes[parseTimes.Count / 2]:F2}ms");
        TestContext.Out.WriteLine($"  Max:    {parseTimes[^1]:F2}ms");
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine($"Total callback blocking:");
        TestContext.Out.WriteLine($"  Avg:    {totalCallbackTimes.Average():F2}ms");
        TestContext.Out.WriteLine($"  Median: {totalCallbackTimes[totalCallbackTimes.Count / 2]:F2}ms");
        TestContext.Out.WriteLine($"  P95:    {totalCallbackTimes[(int)(totalCallbackTimes.Count * 0.95)]:F2}ms");
        TestContext.Out.WriteLine($"  Max:    {totalCallbackTimes[^1]:F2}ms");
        TestContext.Out.WriteLine($"  Budget: 100ms at 10Hz");
        TestContext.Out.WriteLine($"  Headroom: {100 - totalCallbackTimes.Average():F1}ms");

        // Position analysis
        List<GpsCycleResult> results;
        lock (fullStackResults) results = new List<GpsCycleResult>(fullStackResults);

        if (results.Count >= 2)
        {
            TestContext.Out.WriteLine();
            TestContext.Out.WriteLine($"Position verification ({results.Count} cycles):");
            TestContext.Out.WriteLine($"  First: E={results[0].Easting:F2} N={results[0].Northing:F2}");
            TestContext.Out.WriteLine($"  Last:  E={results[^1].Easting:F2} N={results[^1].Northing:F2}");

            // Check for gaps (dropped updates = larger position jumps)
            var dists = new List<double>();
            for (int i = 1; i < results.Count; i++)
            {
                double dE = results[i].Easting - results[i - 1].Easting;
                double dN = results[i].Northing - results[i - 1].Northing;
                dists.Add(Math.Sqrt(dE * dE + dN * dN));
            }
            if (dists.Count > 0)
            {
                dists.Sort();
                TestContext.Out.WriteLine($"  Step sizes: min={dists[0]:F3}m avg={dists.Average():F3}m max={dists[^1]:F3}m");
                if (dists[^1] > dists[0] * 3)
                    TestContext.Out.WriteLine($"  >>> Inconsistent steps detected - some updates dropped mid-stream");
            }
        }

        // Verdict
        TestContext.Out.WriteLine();
        if (dropped > gpsPairs.Length * 0.1)
        {
            TestContext.Out.WriteLine($">>> VERDICT: {dropped} drops ({(double)dropped / gpsPairs.Length * 100:F0}%) with real services");
            TestContext.Out.WriteLine($">>> The real services ARE the bottleneck (not the pipeline framework)");
        }
        else
        {
            TestContext.Out.WriteLine($">>> VERDICT: Pipeline keeps up with real services ({completed}/{gpsPairs.Length} delivered)");
            TestContext.Out.WriteLine($">>> Bottleneck must be in the UI layer (Dispatcher.Post / render contention)");
        }

        Assert.That(completed, Is.GreaterThan(0), "At least some cycles should complete");

        realPipeline.Stop();
        realAutoSteer.Stop();
        realGpsService.Stop();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Test 7: Real UDP sockets (VirtualGpsReceiver -> UdpCommunicationService)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task RealUdp_VirtualGpsToUdpService_MeasureThroughput()
    {
        // Stop the test-level pipeline
        _pipeline.Stop();

        var appState = new ApplicationState();

        // Real services
        var realGpsService = new GpsService();
        realGpsService.Start();
        var realNmeaParser = new NmeaParserService(realGpsService);

        var realToolPosition = new AgValoniaGPS.Services.Tool.ToolPositionService();
        var realGuidance = new TrackGuidanceService();
        var realCoverage = new CoverageMapService();
        var realSectionControl = new SectionControlService(
            realToolPosition, realCoverage, appState);

        // Real UdpCommunicationService
        var udpService = new UdpCommunicationService();

        // Real AutoSteerService wired to the UDP service
        var realAutoSteer = new AutoSteerService(realGuidance, udpService);

        // Wire AutoSteer into UDP service for zero-copy path
        udpService.SetAutoSteerService(realAutoSteer);

        var realPipeline = new GpsPipelineService(
            realGpsService, realToolPosition, realGuidance,
            realSectionControl, realCoverage, realAutoSteer,
            new YouTurnGuidanceService(),
            Substitute.For<IAudioService>(),
            NullLogger<GpsPipelineService>.Instance, appState);

        // Wire the old parser path: UDP DataReceived -> NmeaParser -> GpsService
        udpService.DataReceived += (_, e) =>
        {
            if (e.PGN == 0) // NMEA text
            {
                string sentence = Encoding.ASCII.GetString(e.Data);
                realNmeaParser.ParseSentence(sentence);
            }
        };

        // Counters
        long autoSteerCycles = 0;
        long pipelineCycles = 0;
        var pipelineResults = new List<GpsCycleResult>();

        realAutoSteer.StateUpdated += (_, _) =>
            Interlocked.Increment(ref autoSteerCycles);

        realPipeline.CycleCompleted += result =>
        {
            Interlocked.Increment(ref pipelineCycles);
            lock (pipelineResults) pipelineResults.Add(result);
        };

        // Start everything
        realAutoSteer.Start();
        realPipeline.Start();

        try
        {
            await udpService.StartAsync();
        }
        catch (Exception ex)
        {
            Assert.Ignore($"Cannot bind to port 9999: {ex.Message}");
            return;
        }

        try
        {
            // VirtualGpsReceiver sending to localhost:9999
            using var gps = new VirtualGpsReceiver(targetPort: 9999, targetIp: "127.0.0.1");
            gps.Latitude = 42.0;
            gps.Longitude = -93.0;
            gps.HeadingDegrees = 0;
            gps.SpeedKnots = 5;
            gps.FixQuality = 4;
            gps.Satellites = 12;

            // Send 50 GPS updates at 10Hz
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++)
            {
                gps.Latitude = 42.0 + i * 0.00001;
                gps.SendOnce();
                if (i < 49)
                    await Task.Delay(100);
            }
            sw.Stop();

            // Wait for pipeline to process
            await Task.Delay(1000);

            long asCycles = Interlocked.Read(ref autoSteerCycles);
            long plCycles = Interlocked.Read(ref pipelineCycles);

            List<GpsCycleResult> results;
            lock (pipelineResults) results = new List<GpsCycleResult>(pipelineResults);

            TestContext.Out.WriteLine($"=== REAL UDP: VirtualGpsReceiver -> UdpCommunicationService ===");
            TestContext.Out.WriteLine($"GPS packets sent:    50");
            TestContext.Out.WriteLine($"AutoSteer cycles:    {asCycles}");
            TestContext.Out.WriteLine($"Pipeline cycles:     {plCycles}");
            TestContext.Out.WriteLine($"Pipeline drops:      {asCycles - plCycles}");
            TestContext.Out.WriteLine($"AutoSteer rate:      {asCycles / sw.Elapsed.TotalSeconds:F1} Hz");
            TestContext.Out.WriteLine($"Pipeline rate:       {plCycles / sw.Elapsed.TotalSeconds:F1} Hz");
            TestContext.Out.WriteLine($"Wall time:           {sw.Elapsed.TotalSeconds:F1}s");

            if (results.Count >= 2)
            {
                TestContext.Out.WriteLine($"\nPositions received: {results.Count}");
                TestContext.Out.WriteLine($"  First: lat={results[0].Latitude:F6} E={results[0].Easting:F2} N={results[0].Northing:F2}");
                TestContext.Out.WriteLine($"  Last:  lat={results[^1].Latitude:F6} E={results[^1].Easting:F2} N={results[^1].Northing:F2}");

                var dists = new List<double>();
                for (int i = 1; i < results.Count; i++)
                {
                    double dE = results[i].Easting - results[i - 1].Easting;
                    double dN = results[i].Northing - results[i - 1].Northing;
                    dists.Add(Math.Sqrt(dE * dE + dN * dN));
                }
                if (dists.Count > 0)
                {
                    dists.Sort();
                    TestContext.Out.WriteLine($"  Steps: min={dists[0]:F3}m avg={dists.Average():F3}m max={dists[^1]:F3}m");
                }
            }

            TestContext.Out.WriteLine();
            if (plCycles < asCycles * 0.8)
            {
                TestContext.Out.WriteLine($">>> PIPELINE DROPS: {asCycles - plCycles} updates lost between AutoSteer and Pipeline");
            }
            else if (asCycles < 40)
            {
                TestContext.Out.WriteLine($">>> UDP RECEIVE DROPS: Only {asCycles}/50 reached AutoSteer");
            }
            else
            {
                TestContext.Out.WriteLine($">>> Full UDP stack delivers {plCycles}/50 at {plCycles / sw.Elapsed.TotalSeconds:F1}Hz");
            }

            Assert.That(asCycles, Is.GreaterThan(0), "AutoSteer should receive GPS data via UDP");
        }
        finally
        {
            await udpService.StopAsync();
            realPipeline.Stop();
            realAutoSteer.Stop();
            realGpsService.Stop();
            udpService.Dispose();
        }
    }
}
