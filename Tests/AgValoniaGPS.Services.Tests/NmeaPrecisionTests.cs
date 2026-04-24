// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AgValoniaGPS.IntegrationTests.VirtualModules;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests that GPS data survives the NMEA encode -> transmit -> decode round trip
/// with sufficient precision for real-time navigation. These tests catch resolution
/// bugs like DDMM.MMMM (0.185m) vs DDMM.MMMMM (0.019m) formatting.
///
/// Root cause of the "tractor stepping" bug: the VirtualGpsReceiver formatted
/// coordinates with only 4 decimal places on NMEA minutes, giving 0.185m
/// resolution. At 1 km/h the position only changed once every ~0.7 seconds.
/// </summary>
[TestFixture]
public class NmeaPrecisionTests
{
    private AutoSteerService _autoSteer = null!;
    private GpsService _gpsService = null!;

    [SetUp]
    public void SetUp()
    {
        var configStore = new ConfigurationStore();
        ConfigurationStore.SetInstance(configStore);

        _gpsService = new GpsService();
        _autoSteer = new AutoSteerService(
            Substitute.For<ITrackGuidanceService>(),
            Substitute.For<IUdpCommunicationService>(),
            _gpsService,
            new ApplicationState());
        _autoSteer.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _autoSteer.Stop();
    }

    /// <summary>
    /// Send a GPS position via VirtualGpsReceiver, receive via UDP,
    /// parse through AutoSteerService → GpsService, return the parsed lat/lon.
    /// After Phase B, ProcessGpsBuffer publishes to GpsService (not StateUpdated).
    /// </summary>
    private (double lat, double lon) RoundTrip(double lat, double lon)
    {
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        listener.Client.ReceiveTimeout = 2000;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = lat;
        gps.Longitude = lon;
        gps.HeadingDegrees = 0;
        gps.SpeedKnots = 5;
        gps.FixQuality = 4;
        gps.Satellites = 12;

        gps.SendOnce();
        IPEndPoint? remote = null;
        var bytes = listener.Receive(ref remote);

        _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);

        var data = _gpsService.CurrentData;
        Assert.That(data, Is.Not.Null, "GpsService should have data after ProcessGpsBuffer");
        Assert.That(data.FixQuality, Is.GreaterThan(0), "Fix quality should be valid");
        return (data.CurrentPosition.Latitude, data.CurrentPosition.Longitude);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Round-trip precision tests
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void NmeaRoundTrip_LatitudePrecision_BetterThan5cm()
    {
        double inputLat = 42.123456789;
        double inputLon = -93.654321;

        var (parsedLat, _) = RoundTrip(inputLat, inputLon);

        double errorMeters = Math.Abs(parsedLat - inputLat) * 111320; // deg to meters
        TestContext.Out.WriteLine($"Latitude round-trip error: {errorMeters:F4}m");
        TestContext.Out.WriteLine($"  Input:  {inputLat:F10}");
        TestContext.Out.WriteLine($"  Parsed: {parsedLat:F10}");

        Assert.That(errorMeters, Is.LessThan(0.05),
            $"Latitude round-trip error ({errorMeters:F4}m) exceeds 5cm. " +
            "Check NMEA minute format decimal places (need >= 5 for sub-5cm).");
    }

    [Test]
    public void NmeaRoundTrip_LongitudePrecision_BetterThan5cm()
    {
        double inputLat = 42.0;
        double inputLon = -93.654321789;

        var (_, parsedLon) = RoundTrip(inputLat, inputLon);

        double cosLat = Math.Cos(inputLat * Math.PI / 180.0);
        double errorMeters = Math.Abs(parsedLon - inputLon) * 111320 * cosLat;
        TestContext.Out.WriteLine($"Longitude round-trip error: {errorMeters:F4}m");
        TestContext.Out.WriteLine($"  Input:  {inputLon:F10}");
        TestContext.Out.WriteLine($"  Parsed: {parsedLon:F10}");

        Assert.That(errorMeters, Is.LessThan(0.05),
            $"Longitude round-trip error ({errorMeters:F4}m) exceeds 5cm. " +
            "Check NMEA minute format decimal places (need >= 5 for sub-5cm).");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Resolution tests: can the system distinguish nearby positions?
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void TwoPositions_10cmApart_ProduceDifferentCoordinates()
    {
        // 10cm north = 0.10 / 111320 = 0.000000898 degrees
        double baseLat = 42.0;
        double offsetDeg = 0.10 / 111320.0;

        var (lat1, _) = RoundTrip(baseLat, -93.0);
        var (lat2, _) = RoundTrip(baseLat + offsetDeg, -93.0);

        double deltaMeters = Math.Abs(lat2 - lat1) * 111320;
        TestContext.Out.WriteLine($"10cm test: delta = {deltaMeters:F4}m (expected ~0.10m)");

        Assert.That(lat2, Is.Not.EqualTo(lat1).Within(1e-10),
            "Two positions 10cm apart must produce different parsed coordinates. " +
            "If they're identical, NMEA resolution is too low.");
        Assert.That(deltaMeters, Is.EqualTo(0.10).Within(0.05),
            $"Parsed delta ({deltaMeters:F4}m) should be close to 0.10m");
    }

    [Test]
    public void TwoPositions_3cmApart_ProduceDifferentCoordinates()
    {
        // 3cm = 0.03m. This is the RTK precision target.
        double baseLat = 42.0;
        double offsetDeg = 0.03 / 111320.0;

        var (lat1, _) = RoundTrip(baseLat, -93.0);
        var (lat2, _) = RoundTrip(baseLat + offsetDeg, -93.0);

        double deltaMeters = Math.Abs(lat2 - lat1) * 111320;
        TestContext.Out.WriteLine($"3cm test: delta = {deltaMeters:F4}m (expected ~0.03m)");

        Assert.That(lat2, Is.Not.EqualTo(lat1).Within(1e-10),
            "Two positions 3cm apart must produce different coordinates for RTK guidance.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Low-speed smoothness: verify position changes every GPS cycle
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void LowSpeedDriving_1kmh_PositionChangesEveryCycle()
    {
        // At 1 km/h heading north, 10Hz GPS:
        // distance per cycle = (1000/3600) * 0.1 = 0.0278m per 100ms
        // The NMEA format must resolve this without stalling.

        double lat = 42.0;
        double speedMs = 1000.0 / 3600.0; // 1 km/h in m/s
        double stepTime = 0.1; // 10Hz
        double stepMeters = speedMs * stepTime; // 0.0278m
        double stepDeg = stepMeters / 111320.0;

        var positions = new List<double>();
        for (int i = 0; i < 20; i++)
        {
            var (parsedLat, _) = RoundTrip(lat + i * stepDeg, -93.0);
            positions.Add(parsedLat);
        }

        // Count how many consecutive positions are identical (stalls)
        int stalls = 0;
        int maxConsecutiveStall = 0;
        int currentStall = 0;

        for (int i = 1; i < positions.Count; i++)
        {
            if (Math.Abs(positions[i] - positions[i - 1]) < 1e-10)
            {
                stalls++;
                currentStall++;
                maxConsecutiveStall = Math.Max(maxConsecutiveStall, currentStall);
            }
            else
            {
                currentStall = 0;
            }
        }

        TestContext.Out.WriteLine($"=== Low-Speed Smoothness (1 km/h, 10Hz) ===");
        TestContext.Out.WriteLine($"Step distance: {stepMeters:F4}m per cycle");
        TestContext.Out.WriteLine($"Total cycles: {positions.Count}");
        TestContext.Out.WriteLine($"Stalled cycles: {stalls} (position unchanged from previous)");
        TestContext.Out.WriteLine($"Max consecutive stall: {maxConsecutiveStall}");

        for (int i = 0; i < positions.Count; i++)
        {
            string marker = (i > 0 && Math.Abs(positions[i] - positions[i - 1]) < 1e-10) ? " STALL" : "";
            TestContext.Out.WriteLine($"  [{i:D2}] lat={positions[i]:F10}{marker}");
        }

        // At 0.028m per cycle with 0.019m resolution, we expect movement every cycle
        // Allow at most 1 stall (rounding at LSB boundary)
        Assert.That(maxConsecutiveStall, Is.LessThanOrEqualTo(1),
            $"At 1 km/h, position should change nearly every cycle. " +
            $"Got {maxConsecutiveStall} consecutive stalls. " +
            "NMEA resolution is likely too low (need 5+ decimal places on minutes).");
    }

    [Test]
    public void LowSpeedDriving_05kmh_NoMoreThan2ConsecutiveStalls()
    {
        // At 0.5 km/h: 0.0139m per cycle. With 0.019m resolution,
        // expect movement every 1-2 cycles.
        double lat = 42.0;
        double speedMs = 500.0 / 3600.0;
        double stepTime = 0.1;
        double stepDeg = speedMs * stepTime / 111320.0;

        var positions = new List<double>();
        for (int i = 0; i < 30; i++)
        {
            var (parsedLat, _) = RoundTrip(lat + i * stepDeg, -93.0);
            positions.Add(parsedLat);
        }

        int maxConsecutiveStall = 0;
        int currentStall = 0;
        for (int i = 1; i < positions.Count; i++)
        {
            if (Math.Abs(positions[i] - positions[i - 1]) < 1e-10)
            {
                currentStall++;
                maxConsecutiveStall = Math.Max(maxConsecutiveStall, currentStall);
            }
            else
            {
                currentStall = 0;
            }
        }

        TestContext.Out.WriteLine($"=== Low-Speed Smoothness (0.5 km/h, 10Hz) ===");
        TestContext.Out.WriteLine($"Step distance: {speedMs * stepTime:F4}m per cycle");
        TestContext.Out.WriteLine($"Max consecutive stall: {maxConsecutiveStall}");

        // At 0.014m per cycle with 0.019m resolution, max 2 consecutive stalls
        Assert.That(maxConsecutiveStall, Is.LessThanOrEqualTo(2),
            $"At 0.5 km/h, got {maxConsecutiveStall} consecutive stalls. " +
            "NMEA resolution too low for smooth low-speed display.");
    }
}
