using System;
using System.Diagnostics;
using System.Text;
using AgValoniaGPS.Services;

namespace TestRunner;

/// <summary>
/// Benchmark comparing original string-based NMEA parser vs zero-copy Span-based parser.
/// </summary>
public static class NmeaParserBenchmark
{
    // Sample PANDA sentence
    private const string SamplePanda = "$PANDA,123519,4807.038,N,01131.000,E,4,08,0.9,545.4,1.2,5.5,270.5,1.2,-0.5,0.1*4A";

    public static void Run()
    {
        Console.WriteLine("=== NMEA Parser Benchmark ===\n");

        // Create mock GPS service
        var mockGpsService = new MockGpsService();

        // Create parsers
        var originalParser = new NmeaParserService(mockGpsService);
        var fastParser = new NmeaParserServiceFast(mockGpsService);

        // Prepare data
        byte[] sampleBytes = Encoding.ASCII.GetBytes(SamplePanda);

        // Warmup
        Console.WriteLine("Warming up...");
        for (int i = 0; i < 1000; i++)
        {
            originalParser.ParseSentence(SamplePanda);
            fastParser.ParseBuffer(sampleBytes, sampleBytes.Length);
        }

        // Benchmark parameters
        const int iterations = 100_000;

        // Benchmark original parser
        Console.WriteLine($"\nBenchmarking {iterations:N0} iterations...\n");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocsBefore = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            originalParser.ParseSentence(SamplePanda);
        }

        sw.Stop();
        long allocsAfter = GC.GetTotalAllocatedBytes(precise: true);
        long originalAllocs = allocsAfter - allocsBefore;
        double originalMs = sw.Elapsed.TotalMilliseconds;
        double originalPerParse = originalMs / iterations * 1000; // microseconds

        Console.WriteLine("Original Parser (string-based):");
        Console.WriteLine($"  Total time:    {originalMs:F2} ms");
        Console.WriteLine($"  Per parse:     {originalPerParse:F3} µs");
        Console.WriteLine($"  Allocations:   {originalAllocs:N0} bytes ({originalAllocs / iterations:N0} bytes/parse)");

        // Benchmark fast parser
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        allocsBefore = GC.GetTotalAllocatedBytes(precise: true);
        sw.Restart();

        for (int i = 0; i < iterations; i++)
        {
            fastParser.ParseBuffer(sampleBytes, sampleBytes.Length);
        }

        sw.Stop();
        allocsAfter = GC.GetTotalAllocatedBytes(precise: true);
        long fastAllocs = allocsAfter - allocsBefore;
        double fastMs = sw.Elapsed.TotalMilliseconds;
        double fastPerParse = fastMs / iterations * 1000; // microseconds

        Console.WriteLine("\nFast Parser (Span-based, zero-copy):");
        Console.WriteLine($"  Total time:    {fastMs:F2} ms");
        Console.WriteLine($"  Per parse:     {fastPerParse:F3} µs");
        Console.WriteLine($"  Allocations:   {fastAllocs:N0} bytes ({fastAllocs / iterations:N0} bytes/parse)");

        // Comparison
        Console.WriteLine("\n--- Comparison ---");
        Console.WriteLine($"  Speed improvement:      {originalMs / fastMs:F2}x faster");
        Console.WriteLine($"  Allocation reduction:   {(double)originalAllocs / fastAllocs:F2}x less");
        Console.WriteLine($"  Time saved per parse:   {originalPerParse - fastPerParse:F3} µs");

        // Check if we meet sub-millisecond target
        Console.WriteLine("\n--- Target Check ---");
        if (fastPerParse < 1000)
        {
            Console.WriteLine($"  ✓ Sub-millisecond achieved: {fastPerParse:F3} µs < 1000 µs");
        }
        else
        {
            Console.WriteLine($"  ✗ Sub-millisecond NOT achieved: {fastPerParse:F3} µs >= 1000 µs");
        }
    }
}

/// <summary>
/// Mock GPS service for benchmarking - does nothing with the data.
/// </summary>
public class MockGpsService : AgValoniaGPS.Services.Interfaces.IGpsService
{
    public AgValoniaGPS.Models.GpsData CurrentData { get; private set; } = new();

    public bool IsConnected => true;

    public event EventHandler<AgValoniaGPS.Models.GpsData>? GpsDataUpdated;

    public void UpdateGpsData(AgValoniaGPS.Models.GpsData data)
    {
        CurrentData = data;
        // Don't fire event in benchmark to isolate parser performance
    }

    public void Start() { }
    public void Stop() { }
    public void ProcessNmeaSentence(string sentence) { }
    public void UpdateImuData() { }
    public void MarkGpsReceived() { }
    public bool IsGpsDataOk() => true;
    public bool IsImuDataOk() => true;
}
