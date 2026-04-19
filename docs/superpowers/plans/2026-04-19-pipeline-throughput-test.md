# Pipeline Throughput Test Infrastructure

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an end-to-end test that wires the full GPS pipeline with mocked dependencies, sends GPS updates at 10Hz, and measures how many reach each stage -- reproducing and diagnosing the "tractor moves in big steps" bug.

**Architecture:** A single test class `PipelineThroughputTests` in the existing Services.Tests project. Constructs the real NmeaParserService -> GpsService -> GpsPipelineService chain with NSubstitute mocks for heavy dependencies (sections, coverage, guidance). Sends real $PANDA sentences built by VirtualGpsReceiver. Counts events at each stage, measures ProcessCycle time.

**Tech Stack:** NUnit 4, NSubstitute, VirtualGpsReceiver (from integration tests), Stopwatch, ManualResetEventSlim for async synchronization.

---

## File Structure

| File | Responsibility |
|------|---------------|
| `Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs` | New test class with pipeline throughput measurements |

Only one new file. All service classes used are existing production code.

---

### Task 1: Scaffold test class with full pipeline construction

**Files:**
- Create: `Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs`

- [ ] **Step 1: Write the test class with SetUp that constructs the full pipeline**

```csharp
// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    private readonly List<double> _cycleTimes = new();

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
        _cycleTimes.Clear();

        _gpsService.GpsDataUpdated += (_, _) =>
            Interlocked.Increment(ref _gpsDataUpdatedCount);

        _pipeline.CycleCompleted += result =>
        {
            Interlocked.Increment(ref _cycleCompletedCount);
            lock (_cycleResults) _cycleResults.Add(result);
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
}
```

- [ ] **Step 2: Verify the test class compiles**

Run: `~/.dotnet/dotnet build Tests/AgValoniaGPS.Services.Tests/`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs
git commit -m "Scaffold PipelineThroughputTests with full pipeline wiring"
```

---

### Task 2: Synchronous throughput test (no timing delays)

**Files:**
- Modify: `Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs`

- [ ] **Step 1: Add the synchronous throughput test**

Add this test method to `PipelineThroughputTests`:

```csharp
    [Test]
    public void SynchronousThroughput_AllUpdatesReachPipeline()
    {
        // Pre-build 100 PANDA sentences with incrementing positions
        var sentences = new string[100];
        for (int i = 0; i < 100; i++)
        {
            sentences[i] = BuildPandaString(
                lat: 42.0 + i * 0.00001, // ~1.1m steps north
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

        // Report drop rate but don't fail - this test measures, not enforces
        TestContext.Out.WriteLine($"\n>>> DROP RATE: {(double)dropped / parsed * 100:F1}% <<<");
        if (dropped > 0)
        {
            TestContext.Out.WriteLine($">>> Back-pressure is dropping {dropped} out of {parsed} updates");
            TestContext.Out.WriteLine($">>> This confirms GpsPipelineService.ProcessCycle is the bottleneck");
        }
    }
```

- [ ] **Step 2: Run the test**

Run: `~/.dotnet/dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "SynchronousThroughput" -v n`
Expected: PASS. Output shows how many cycles completed vs dropped.

- [ ] **Step 3: Commit**

```bash
git add Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs
git commit -m "Add synchronous throughput test measuring pipeline drop rate"
```

---

### Task 3: Timed throughput test at 10Hz (reproduces real scenario)

**Files:**
- Modify: `Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs`

- [ ] **Step 1: Add the 10Hz timed throughput test**

Add this test method:

```csharp
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
                await Task.Delay(100); // 10Hz
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
        TestContext.Out.WriteLine($"Effective Hz:        {completed / (sw.Elapsed.TotalSeconds):F1}");
        TestContext.Out.WriteLine($"Wall time:           {sw.Elapsed.TotalSeconds:F1}s");

        // Verify all parsed
        Assert.That(parsed, Is.EqualTo(50), "All sentences should parse");

        // At 10Hz with mocked services, most should get through
        // If drop rate > 20%, pipeline is too slow even with mocks
        double dropRate = (double)dropped / parsed;
        TestContext.Out.WriteLine($"\n>>> EFFECTIVE UPDATE RATE: {completed / sw.Elapsed.TotalSeconds:F1} Hz <<<");

        if (dropRate > 0.2)
        {
            TestContext.Out.WriteLine(">>> WARNING: >20% drop rate even with mocked dependencies!");
            TestContext.Out.WriteLine(">>> ProcessCycle overhead is too high for 10Hz GPS");
        }
    }
```

- [ ] **Step 2: Run the test**

Run: `~/.dotnet/dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "TimedThroughput" -v n`
Expected: PASS. Output shows effective Hz and drop rate at realistic 10Hz.

- [ ] **Step 3: Commit**

```bash
git add Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs
git commit -m "Add timed 10Hz throughput test reproducing real GPS rate"
```

---

### Task 4: ProcessCycle timing measurement

**Files:**
- Modify: `Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs`

- [ ] **Step 1: Add the ProcessCycle timing test**

Add this test method:

```csharp
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
                heading: i * 12.0, // Vary heading
                speedKnots: 5);
        }

        // Track per-cycle timing via CycleCompleted timestamps
        var cycleTimes = new List<long>();
        var lastCycleTime = Stopwatch.GetTimestamp();

        _pipeline.CycleCompleted += _ =>
        {
            long now = Stopwatch.GetTimestamp();
            long elapsed = now - Volatile.Read(ref lastCycleTime);
            Volatile.Write(ref lastCycleTime, now);
            lock (cycleTimes) cycleTimes.Add(elapsed);
        };

        // Send at 10Hz
        for (int i = 0; i < sentences.Length; i++)
        {
            _nmeaParser.ParseSentence(sentences[i]);
            if (i < sentences.Length - 1)
                await Task.Delay(100);
        }

        // Wait for pipeline to drain
        await Task.Delay(500);

        // Analyze cycle intervals
        if (cycleTimes.Count > 1)
        {
            // Skip the first (unreliable initial interval)
            var intervals = cycleTimes.GetRange(1, cycleTimes.Count - 1);
            var intervalsMs = intervals.ConvertAll(t =>
                t * 1000.0 / Stopwatch.Frequency);

            intervalsMs.Sort();
            double median = intervalsMs[intervalsMs.Count / 2];
            double p95 = intervalsMs[(int)(intervalsMs.Count * 0.95)];
            double max = intervalsMs[^1];
            double min = intervalsMs[0];
            double avg = 0;
            foreach (var t in intervalsMs) avg += t;
            avg /= intervalsMs.Count;

            TestContext.Out.WriteLine($"=== ProcessCycle Interval Analysis ===");
            TestContext.Out.WriteLine($"Cycles completed:    {cycleTimes.Count}");
            TestContext.Out.WriteLine($"Min interval:        {min:F1}ms");
            TestContext.Out.WriteLine($"Avg interval:        {avg:F1}ms");
            TestContext.Out.WriteLine($"Median interval:     {median:F1}ms");
            TestContext.Out.WriteLine($"P95 interval:        {p95:F1}ms");
            TestContext.Out.WriteLine($"Max interval:        {max:F1}ms");
            TestContext.Out.WriteLine($"Expected @ 10Hz:     100ms");

            // If median interval > 200ms, cycles are running way too slow
            if (median > 200)
            {
                TestContext.Out.WriteLine($"\n>>> BOTTLENECK FOUND: Median cycle interval {median:F0}ms >> 100ms target");
                TestContext.Out.WriteLine($">>> ProcessCycle is too slow, causing back-pressure drops");
            }
            // If intervals are clustered near 100ms, pipeline keeps up fine
            else if (median < 150)
            {
                TestContext.Out.WriteLine($"\n>>> Pipeline keeps up with 10Hz (median {median:F0}ms)");
                TestContext.Out.WriteLine($">>> Bottleneck is likely elsewhere (UI thread, receive thread blocking)");
            }
        }
        else
        {
            TestContext.Out.WriteLine(">>> Only 0-1 cycles completed - pipeline is severely broken");
        }
    }
```

- [ ] **Step 2: Run the test**

Run: `~/.dotnet/dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "ProcessCycleTiming" -v n`
Expected: PASS. Shows per-cycle latency distribution.

- [ ] **Step 3: Commit**

```bash
git add Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs
git commit -m "Add ProcessCycle timing test with latency percentiles"
```

---

### Task 5: Position delta verification test

**Files:**
- Modify: `Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs`

- [ ] **Step 1: Add position delta test that verifies output positions match input**

Add this test method:

```csharp
    [Test]
    public async Task PositionOutput_MatchesInput_NoDrift()
    {
        // Send 20 positions at known intervals heading north
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
                    $"  [{i-1}->{i}] dE={dE:F3} dN={dN:F3} dist={dist:F3}m dLat={dLat:F8}");
            }

            // If positions are incrementing smoothly, distance between consecutive
            // results should be ~1.1m (latStep * 111320m/deg).
            // If updates are being dropped, distance will be multiples of 1.1m.
            var firstDelta = Math.Sqrt(
                Math.Pow(results[1].Easting - results[0].Easting, 2) +
                Math.Pow(results[1].Northing - results[0].Northing, 2));

            if (results.Count < sentences.Length)
            {
                TestContext.Out.WriteLine(
                    $"\n>>> {sentences.Length - results.Count} updates dropped " +
                    $"({(double)(sentences.Length - results.Count) / sentences.Length * 100:F0}% loss)");
                TestContext.Out.WriteLine(
                    $">>> Remaining updates have {firstDelta:F1}m gaps " +
                    $"(expected ~1.1m if no drops)");
            }
        }

        Assert.That(completed, Is.GreaterThan(0), "At least some cycles should complete");
    }
```

- [ ] **Step 2: Run the test**

Run: `~/.dotnet/dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "PositionOutput" -v n`
Expected: PASS. Shows per-position output with deltas and gap analysis.

- [ ] **Step 3: Run all throughput tests together**

Run: `~/.dotnet/dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "PipelineThroughputTests" -v n`
Expected: All 4 tests PASS.

- [ ] **Step 4: Commit**

```bash
git add Tests/AgValoniaGPS.Services.Tests/PipelineThroughputTests.cs
git commit -m "Add position delta verification test for pipeline output"
```
