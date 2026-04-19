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
using AgValoniaGPS.ViewModels;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.UI.Tests;

/// <summary>
/// Tests the full GPS pipeline through the Avalonia UI dispatcher.
/// Uses headless Avalonia to get a real dispatcher, then measures how many
/// GPS updates actually reach ApplyGpsCycleResult via Dispatcher.Post.
/// This reproduces the real-app environment where the UI thread processes
/// both render frames and posted GPS updates.
/// </summary>
[TestFixture]
public class PipelineUiThroughputTests
{
    /// <summary>
    /// Build a $PANDA sentence with correct NMEA checksum.
    /// Inlined here to avoid dependency on IntegrationTests project.
    /// </summary>
    private static string BuildPandaString(double lat, double lon,
        double heading = 0, double speedKnots = 5)
    {
        double absLat = Math.Abs(lat);
        int latDeg = (int)absLat;
        double latMin = (absLat - latDeg) * 60.0;
        string latStr = $"{latDeg:D2}{latMin:00.0000}";
        string ns = lat >= 0 ? "N" : "S";

        double absLon = Math.Abs(lon);
        int lonDeg = (int)absLon;
        double lonMin = (absLon - lonDeg) * 60.0;
        string lonStr = $"{lonDeg:D3}{lonMin:00.0000}";
        string ew = lon >= 0 ? "E" : "W";

        string time = DateTime.UtcNow.ToString("HHmmss.ff",
            System.Globalization.CultureInfo.InvariantCulture);
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        string body = $"PANDA,{time},{latStr},{ns},{lonStr},{ew},4,12,0.7,100.0,1.0," +
                       $"{speedKnots.ToString("F2", ci)},{heading.ToString("F2", ci)},0.00,0.00,0.00";

        byte checksum = 0;
        foreach (char c in body) checksum ^= (byte)c;

        return $"${body}*{checksum:X2}\r\n";
    }

    [AvaloniaTest]
    public async Task DispatcherThroughput_MeasureGpsUpdatesReachingUiThread()
    {
        // Setup singletons
        var configStore = new ConfigurationStore();
        ConfigurationStore.SetInstance(configStore);
        var appState = new ApplicationState();

        // Build real pipeline stack
        var gpsService = new GpsService();
        gpsService.Start();
        var nmeaParser = new NmeaParserService(gpsService);

        var toolPosition = new ToolPositionService();
        var guidance = new TrackGuidanceService();
        var coverage = new CoverageMapService();
        var sectionControl = new SectionControlService(toolPosition, coverage, appState);
        var autoSteer = new AutoSteerService(guidance, Substitute.For<IUdpCommunicationService>());

        var pipeline = new GpsPipelineService(
            gpsService, toolPosition, guidance, sectionControl, coverage,
            autoSteer, new YouTurnGuidanceService(),
            Substitute.For<IAudioService>(),
            NullLogger<GpsPipelineService>.Instance, appState);

        autoSteer.Start();
        pipeline.Start();

        // Track updates that reach the UI thread via Dispatcher.Post
        // (this is what ApplyGpsCycleResult does in the real app)
        long cycleCompletedCount = 0;
        long uiThreadApplyCount = 0;
        var uiPositions = new List<(double E, double N)>();

        pipeline.CycleCompleted += result =>
        {
            Interlocked.Increment(ref cycleCompletedCount);

            // This mirrors MainViewModel.OnGpsCycleCompleted exactly:
            Dispatcher.UIThread.Post(() =>
            {
                Interlocked.Increment(ref uiThreadApplyCount);
                lock (uiPositions) uiPositions.Add((result.Easting, result.Northing));
            });
        };

        // Pre-build sentences
        var sentences = new string[50];
        for (int i = 0; i < 50; i++)
        {
            sentences[i] = BuildPandaString(
                lat: 42.0 + i * 0.00001,
                lon: -93.0, heading: 0, speedKnots: 5);
        }

        // Send at 10Hz from a background thread (simulating UDP receive)
        var sendTask = Task.Run(async () =>
        {
            for (int i = 0; i < sentences.Length; i++)
            {
                // Simulate receive callback: AutoSteer + NmeaParser
                var bytes = Encoding.ASCII.GetBytes(sentences[i]);
                autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
                nmeaParser.ParseSentence(sentences[i]);

                if (i < sentences.Length - 1)
                    await Task.Delay(100);
            }
        });

        // While sending, pump the dispatcher to process posted updates
        // This simulates the Avalonia render loop processing queued work
        var pumpStart = Stopwatch.StartNew();
        while (!sendTask.IsCompleted || pumpStart.Elapsed.TotalSeconds < 6)
        {
            // Process any queued dispatcher work items
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(33); // ~30 FPS render loop timing

            if (pumpStart.Elapsed.TotalSeconds > 7) break;
        }

        // Final pump to catch stragglers
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        // Report
        long cycles = Interlocked.Read(ref cycleCompletedCount);
        long uiApplied = Interlocked.Read(ref uiThreadApplyCount);
        long uiDropped = cycles - uiApplied;

        List<(double E, double N)> positions;
        lock (uiPositions) positions = new List<(double E, double N)>(uiPositions);

        TestContext.Out.WriteLine($"=== UI Dispatcher Throughput @ 10Hz ===");
        TestContext.Out.WriteLine($"Sentences sent:      {sentences.Length}");
        TestContext.Out.WriteLine($"CycleCompleted:      {cycles}");
        TestContext.Out.WriteLine($"UI thread applied:   {uiApplied}");
        TestContext.Out.WriteLine($"UI thread missed:    {uiDropped}");
        TestContext.Out.WriteLine($"UI drop rate:        {(cycles > 0 ? (double)uiDropped / cycles * 100 : 0):F1}%");
        TestContext.Out.WriteLine($"Effective UI Hz:     {uiApplied / 5.0:F1}");

        if (positions.Count >= 2)
        {
            TestContext.Out.WriteLine($"\nUI position updates received: {positions.Count}");
            TestContext.Out.WriteLine($"  First: E={positions[0].E:F2} N={positions[0].N:F2}");
            TestContext.Out.WriteLine($"  Last:  E={positions[^1].E:F2} N={positions[^1].N:F2}");

            var dists = new List<double>();
            for (int i = 1; i < positions.Count; i++)
            {
                double dE = positions[i].E - positions[i - 1].E;
                double dN = positions[i].N - positions[i - 1].N;
                dists.Add(Math.Sqrt(dE * dE + dN * dN));
            }
            dists.Sort();
            TestContext.Out.WriteLine($"  Steps: min={dists[0]:F3}m avg={dists.Average():F3}m max={dists[^1]:F3}m");
            TestContext.Out.WriteLine($"  Expected step: 1.111m (if no drops)");

            if (dists[^1] > 2.0)
            {
                TestContext.Out.WriteLine($"  >>> Large steps detected ({dists[^1]:F1}m) - updates dropped at UI layer");
            }
        }

        // Verdict
        TestContext.Out.WriteLine();
        if (uiDropped > cycles * 0.1)
        {
            TestContext.Out.WriteLine($">>> VERDICT: UI dispatcher is the bottleneck");
            TestContext.Out.WriteLine($">>> {uiDropped}/{cycles} updates lost between CycleCompleted and UI apply");
            TestContext.Out.WriteLine($">>> Fix: store latest result in volatile field, read from render timer");
        }
        else
        {
            TestContext.Out.WriteLine($">>> VERDICT: UI dispatcher keeps up ({uiApplied}/{cycles})");
            TestContext.Out.WriteLine($">>> Headless dispatcher has no render contention");
            TestContext.Out.WriteLine($">>> Real app may still bottleneck under 30 FPS render load");
        }

        Assert.That(cycles, Is.GreaterThan(0), "Pipeline should produce cycles");
        Assert.That(uiApplied, Is.GreaterThan(0), "UI thread should process some updates");

        pipeline.Stop();
        autoSteer.Stop();
        gpsService.Stop();
    }
}
