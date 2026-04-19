// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AgValoniaGPS.IntegrationTests.VirtualModules;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Integration tests verifying the Vehicle Simulator's data flow:
/// VirtualGpsReceiver -> UDP -> AutoSteerService -> VehicleState
/// VirtualSteerModule -> PGN 253 -> AutoSteerService -> steer data
/// </summary>
[TestFixture]
public class SimulatorDataFlowTests
{
    private ITrackGuidanceService _mockGuidance = null!;
    private IUdpCommunicationService _mockUdp = null!;
    private AutoSteerService _autoSteer = null!;

    [SetUp]
    public void SetUp()
    {
        _mockGuidance = Substitute.For<ITrackGuidanceService>();
        _mockUdp = Substitute.For<IUdpCommunicationService>();
        _autoSteer = new AutoSteerService(_mockGuidance, _mockUdp);
        _autoSteer.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _autoSteer.Stop();
    }

    [Test]
    public void GpsData_FromSimulator_ParsedByAutoSteerService()
    {
        // 1. Create a VirtualGpsReceiver sending to an ephemeral port
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = 43.712800;
        gps.Longitude = -74.006000;
        gps.HeadingDegrees = 127.5;
        gps.SpeedKnots = 5.0;
        gps.FixQuality = 4;
        gps.Satellites = 14;
        gps.Hdop = 0.7;
        gps.RollDegrees = 1.5;
        gps.PitchDegrees = -0.3;
        gps.YawRateDegPerSec = 0.2;

        // 2. Send a $PANDA sentence and capture the raw bytes
        gps.SendOnce();

        listener.Client.ReceiveTimeout = 2000;
        IPEndPoint? remote = null;
        var rawBytes = listener.Receive(ref remote);

        // 3. Capture the snapshot from AutoSteerService
        VehicleStateSnapshot? snapshot = null;
        _autoSteer.StateUpdated += (_, s) => snapshot = s;

        // 4. Feed the raw GPS bytes directly to AutoSteerService (same path as UdpCommunicationService)
        _autoSteer.ProcessGpsBuffer(rawBytes, rawBytes.Length);

        // 5. Verify the state was parsed correctly
        Assert.That(snapshot, Is.Not.Null, "StateUpdated should fire after GPS processing");
        Assert.That(snapshot!.Value.Latitude, Is.EqualTo(43.712800).Within(0.001),
            "Latitude should match sent value");
        Assert.That(snapshot.Value.Longitude, Is.EqualTo(-74.006000).Within(0.001),
            "Longitude should match sent value");
        Assert.That(snapshot.Value.Heading, Is.EqualTo(127.5).Within(0.5),
            "Heading should match sent value");
        Assert.That(snapshot.Value.FixQuality, Is.EqualTo(4),
            "Fix quality should be RTK Fixed");
        Assert.That(snapshot.Value.Satellites, Is.EqualTo(14));
        Assert.That(snapshot.Value.Hdop, Is.EqualTo(0.7).Within(0.1));
        Assert.That(snapshot.Value.GpsValid, Is.True,
            "GPS should be marked valid after successful parse");
    }

    [Test]
    public void SteerModule_PGN253_ProcessedByAutoSteerService()
    {
        // Build PGN 253 steer feedback data (same format VirtualSteerModule uses)
        double steerAngleDeg = 12.5;
        var data = new byte[8];

        // Bytes 0-1: Actual steer angle (degrees * 100, int16 LE)
        short angleRaw = (short)(steerAngleDeg * 100);
        data[0] = (byte)(angleRaw & 0xFF);
        data[1] = (byte)((angleRaw >> 8) & 0xFF);

        // Bytes 2-3: IMU heading
        data[2] = 0;
        data[3] = 0;

        // Bytes 4-5: IMU roll
        data[4] = 0;
        data[5] = 0;

        // Byte 6: Switch status (steer switch active)
        data[6] = 0x02;

        // Byte 7: PWM display
        data[7] = 128;

        // Wrap in full PGN packet format
        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_STEER_DATA, data);

        // Fire the DataReceived event on the mock (simulating UdpCommunicationService receiving this)
        _mockUdp.DataReceived += Raise.EventWith(
            _mockUdp,
            new UdpDataReceivedEventArgs
            {
                Data = packet,
                PGN = PgnNumbers.AUTOSTEER_DATA, // 253
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 8888),
                Timestamp = DateTime.Now
            });

        // Verify AutoSteerService processed the steer data
        var steerData = _autoSteer.LastSteerData;
        Assert.That(steerData.ActualSteerAngle, Is.EqualTo(12.5).Within(0.1),
            "Actual steer angle should match sent value");
        Assert.That(steerData.SteerSwitchActive, Is.True,
            "Steer switch should be active");
    }

    [Test]
    public void AutoLocalPlane_CreatedFromFirstGpsFix()
    {
        // Position 1: First GPS fix at a known location
        using var listener1 = new UdpClient(0);
        int port1 = ((IPEndPoint)listener1.Client.LocalEndPoint!).Port;

        using var gps = new VirtualGpsReceiver(targetPort: port1);
        gps.Latitude = 43.712800;
        gps.Longitude = -74.006000;
        gps.HeadingDegrees = 0.0;
        gps.SpeedKnots = 0.0;
        gps.FixQuality = 4; // RTK Fixed - triggers local plane creation
        gps.Satellites = 12;

        gps.SendOnce();
        listener1.Client.ReceiveTimeout = 2000;
        IPEndPoint? remote1 = null;
        var bytes1 = listener1.Receive(ref remote1);

        VehicleStateSnapshot? snapshot1 = null;
        EventHandler<VehicleStateSnapshot> handler1 = (_, s) => snapshot1 = s;
        _autoSteer.StateUpdated += handler1;

        // Process first fix - should auto-create local plane
        _autoSteer.ProcessGpsBuffer(bytes1, bytes1.Length);

        // Unsubscribe first handler before second fix
        _autoSteer.StateUpdated -= handler1;

        Assert.That(snapshot1, Is.Not.Null, "Should get snapshot from first fix");
        // First fix creates the local plane at this position,
        // so Easting/Northing should be near zero (origin)
        double firstEasting = snapshot1!.Value.Easting;
        double firstNorthing = snapshot1.Value.Northing;
        Assert.That(Math.Abs(firstEasting) < 1.0,
            "First fix Easting should be near origin");
        Assert.That(Math.Abs(firstNorthing) < 1.0,
            "First fix Northing should be near origin");

        // Position 2: Move slightly north (increase latitude)
        using var listener2 = new UdpClient(0);
        int port2 = ((IPEndPoint)listener2.Client.LocalEndPoint!).Port;

        using var gps2 = new VirtualGpsReceiver(targetPort: port2);
        gps2.Latitude = 43.713800;  // ~111m north
        gps2.Longitude = -74.006000;
        gps2.HeadingDegrees = 0.0;
        gps2.SpeedKnots = 0.0;
        gps2.FixQuality = 4;
        gps2.Satellites = 12;

        gps2.SendOnce();
        listener2.Client.ReceiveTimeout = 2000;
        IPEndPoint? remote2 = null;
        var bytes2 = listener2.Receive(ref remote2);

        VehicleStateSnapshot? snapshot2 = null;
        _autoSteer.StateUpdated += (_, s) => snapshot2 = s;

        // Process second fix - should use existing local plane
        _autoSteer.ProcessGpsBuffer(bytes2, bytes2.Length);

        Assert.That(snapshot2, Is.Not.Null, "Should get snapshot from second fix");
        // Moving 0.001 degrees north ~ 111m, so Northing should be significantly non-zero
        Assert.That(Math.Abs(snapshot2!.Value.Northing), Is.GreaterThan(50.0),
            "Second fix should have significant Northing offset from origin");
        Assert.That(snapshot2.Value.Northing, Is.Not.EqualTo(firstNorthing),
            "Northing should change between fixes at different positions");
    }

    [Test]
    public void NoLocalPlane_WhenFixQualityZero()
    {
        // Send GPS data with FixQuality=0 (no fix) - should NOT create local plane
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = 43.712800;
        gps.Longitude = -74.006000;
        gps.FixQuality = 0; // No fix
        gps.Satellites = 0;

        gps.SendOnce();
        listener.Client.ReceiveTimeout = 2000;
        IPEndPoint? remote = null;
        var rawBytes = listener.Receive(ref remote);

        VehicleStateSnapshot? snapshot = null;
        _autoSteer.StateUpdated += (_, s) => snapshot = s;

        _autoSteer.ProcessGpsBuffer(rawBytes, rawBytes.Length);

        // FixQuality=0 should still parse but Easting/Northing stay 0 (no local plane)
        if (snapshot != null)
        {
            Assert.That(snapshot.Value.Easting, Is.EqualTo(0.0),
                "No local plane should be created with no fix");
            Assert.That(snapshot.Value.Northing, Is.EqualTo(0.0),
                "No local plane should be created with no fix");
        }
    }

    [Test]
    public void GpsData_MultipleFrames_AllParsedSuccessfully()
    {
        // Simulate driving: send multiple GPS frames and verify metrics
        using var listener = new UdpClient(0);
        int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        listener.Client.ReceiveTimeout = 2000;

        using var gps = new VirtualGpsReceiver(targetPort: port);
        gps.Latitude = 43.712800;
        gps.Longitude = -74.006000;
        gps.HeadingDegrees = 90.0; // East
        gps.SpeedKnots = 10.0;
        gps.FixQuality = 4;
        gps.Satellites = 12;

        int snapshotCount = 0;
        _autoSteer.StateUpdated += (_, _) => snapshotCount++;

        // Send 10 frames, stepping position between each
        for (int i = 0; i < 10; i++)
        {
            gps.SendOnce();
            IPEndPoint? remote = null;
            var rawBytes = listener.Receive(ref remote);
            _autoSteer.ProcessGpsBuffer(rawBytes, rawBytes.Length);
            gps.Step(0.1); // 100ms at 10Hz
        }

        // Verify all frames were processed
        Assert.That(snapshotCount, Is.EqualTo(10),
            "All 10 GPS frames should produce snapshots");

        var metrics = _autoSteer.GetLatencyMetrics();
        Assert.That(metrics.CycleCount, Is.EqualTo(10),
            "Cycle count should match frames sent");
        Assert.That(metrics.ParseFailures, Is.EqualTo(0),
            "No parse failures expected for valid $PANDA sentences");
    }

    [Test]
    public void SteerModule_SensorData_PGN250_ProcessedCorrectly()
    {
        // Build PGN 250 sensor data
        byte sensorValue = 128; // ~50%
        var data = new byte[8];
        data[0] = sensorValue;

        var packet = PgnProtocol.BuildPacket(PgnProtocol.PGN_SENSOR_DATA, data);

        // Fire PGN 250 via the mock event
        _mockUdp.DataReceived += Raise.EventWith(
            _mockUdp,
            new UdpDataReceivedEventArgs
            {
                Data = packet,
                PGN = PgnNumbers.SENSOR_DATA, // 250
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 8888),
                Timestamp = DateTime.Now
            });

        // Verify sensor data was processed
        Assert.That(_autoSteer.SensorPercent, Is.EqualTo(128.0 / 255.0 * 100.0).Within(0.5),
            "Sensor percent should be approximately 50%");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tractor Movement Tests
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Helper: send a VirtualGpsReceiver PANDA sentence via UDP and capture the raw bytes.
    /// </summary>
    private static byte[] BuildPandaBytes(double lat, double lon, double heading = 0,
        double speedKnots = 5, int fixQuality = 4)
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
        return listener.Receive(ref remote);
    }

    [Test]
    public void TractorMoves_WithoutField_AutoCreatedPlane()
    {
        // First GPS fix at origin -- auto-creates local plane, so Easting/Northing near 0
        var bytes1 = BuildPandaBytes(lat: 42.0, lon: -93.0, heading: 0, speedKnots: 5);

        VehicleStateSnapshot? snap1 = null;
        EventHandler<VehicleStateSnapshot> h1 = (_, s) => snap1 = s;
        _autoSteer.StateUpdated += h1;
        _autoSteer.ProcessGpsBuffer(bytes1, bytes1.Length);
        _autoSteer.StateUpdated -= h1;

        Assert.That(snap1, Is.Not.Null, "Should produce snapshot from first fix");
        Assert.That(Math.Abs(snap1!.Value.Easting), Is.LessThan(1.0),
            "First fix Easting should be near origin");
        Assert.That(Math.Abs(snap1.Value.Northing), Is.LessThan(1.0),
            "First fix Northing should be near origin");

        // Second fix ~11m north (0.0001 degrees latitude)
        var bytes2 = BuildPandaBytes(lat: 42.0001, lon: -93.0, heading: 0, speedKnots: 5);

        VehicleStateSnapshot? snap2 = null;
        _autoSteer.StateUpdated += (_, s) => snap2 = s;
        _autoSteer.ProcessGpsBuffer(bytes2, bytes2.Length);

        Assert.That(snap2, Is.Not.Null, "Should produce snapshot from second fix");
        Assert.That(snap2!.Value.Northing, Is.GreaterThan(5.0),
            "Northing should increase when moving north");
        Assert.That(Math.Abs(snap2.Value.Easting), Is.LessThan(1.0),
            "Easting should stay near zero when only latitude changes");
    }

    [Test]
    public void TractorMoves_WithField_FieldOriginPlane()
    {
        // Set up a field local plane at lat=42.0, lon=-93.0 BEFORE sending GPS
        var fieldOrigin = new Wgs84(42.0, -93.0);
        var sharedProps = new SharedFieldProperties();
        var fieldPlane = new LocalPlane(fieldOrigin, sharedProps);
        _autoSteer.SetLocalPlane(fieldPlane, sharedProps);

        // First GPS fix at origin -- should be near (0,0)
        var bytes1 = BuildPandaBytes(lat: 42.0, lon: -93.0, heading: 0, speedKnots: 5);

        VehicleStateSnapshot? snap1 = null;
        EventHandler<VehicleStateSnapshot> h1 = (_, s) => snap1 = s;
        _autoSteer.StateUpdated += h1;
        _autoSteer.ProcessGpsBuffer(bytes1, bytes1.Length);
        _autoSteer.StateUpdated -= h1;

        Assert.That(snap1, Is.Not.Null, "Should produce snapshot from first fix");
        Assert.That(Math.Abs(snap1!.Value.Easting), Is.LessThan(1.0),
            "Easting at field origin should be near zero");
        Assert.That(Math.Abs(snap1.Value.Northing), Is.LessThan(1.0),
            "Northing at field origin should be near zero");

        // Second fix at lat=42.001 (~111m north)
        var bytes2 = BuildPandaBytes(lat: 42.001, lon: -93.0, heading: 0, speedKnots: 5);

        VehicleStateSnapshot? snap2 = null;
        _autoSteer.StateUpdated += (_, s) => snap2 = s;
        _autoSteer.ProcessGpsBuffer(bytes2, bytes2.Length);

        Assert.That(snap2, Is.Not.Null, "Should produce snapshot from second fix");
        Assert.That(snap2!.Value.Northing, Is.GreaterThan(50.0),
            "Northing should be significant (~111m) after moving 0.001 degrees north");
        Assert.That(snap2.Value.Northing, Is.Not.EqualTo(snap1.Value.Northing),
            "Position should change between fixes");
    }

    [Test]
    public void TractorMoves_FieldOriginDifferentFromGps_CorrectOffset()
    {
        // Field origin at lat=42.0, lon=-93.0
        var fieldOrigin = new Wgs84(42.0, -93.0);
        var sharedProps = new SharedFieldProperties();
        var fieldPlane = new LocalPlane(fieldOrigin, sharedProps);
        _autoSteer.SetLocalPlane(fieldPlane, sharedProps);

        // GPS fix at lat=42.001 (0.001 degrees north of field origin ~ 111m)
        var bytes = BuildPandaBytes(lat: 42.001, lon: -93.0, heading: 0, speedKnots: 5);

        VehicleStateSnapshot? snap = null;
        _autoSteer.StateUpdated += (_, s) => snap = s;
        _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);

        Assert.That(snap, Is.Not.Null, "Should produce snapshot");

        // Northing should be approximately 111m (not 0, proving field origin is used)
        Assert.That(snap!.Value.Northing, Is.EqualTo(111.0).Within(5.0),
            "Northing should be ~111m when GPS is 0.001 deg north of field origin");
        Assert.That(Math.Abs(snap.Value.Easting), Is.LessThan(1.0),
            "Easting should be near zero (same longitude as field origin)");
    }

    /// <summary>Get a random ephemeral port by binding to 0 and reading the assigned port.</summary>
    private static int GetEphemeralPort()
    {
        using var tmp = new UdpClient(0);
        return ((IPEndPoint)tmp.Client.LocalEndPoint!).Port;
    }

    [Test]
    public void TractorMoves_AtEquator_LatZeroLonZero()
    {
        // First GPS fix at lat=0, lon=0 (equator) -- must not be skipped
        var bytes1 = BuildPandaBytes(lat: 0.0, lon: 0.0, heading: 90, speedKnots: 5);

        VehicleStateSnapshot? snap1 = null;
        EventHandler<VehicleStateSnapshot> h1 = (_, s) => snap1 = s;
        _autoSteer.StateUpdated += h1;
        _autoSteer.ProcessGpsBuffer(bytes1, bytes1.Length);
        _autoSteer.StateUpdated -= h1;

        Assert.That(snap1, Is.Not.Null, "Should produce snapshot at lat=0");
        Assert.That(snap1!.Value.GpsValid, Is.True, "GPS should be valid at lat=0");
        Assert.That(Math.Abs(snap1.Value.Easting), Is.LessThan(1.0),
            "First fix Easting should be near origin at lat=0");
        Assert.That(Math.Abs(snap1.Value.Northing), Is.LessThan(1.0),
            "First fix Northing should be near origin at lat=0");

        // Second fix moved east (0.001 deg longitude at equator ~ 111m)
        var bytes2 = BuildPandaBytes(lat: 0.0, lon: 0.001, heading: 90, speedKnots: 5);

        VehicleStateSnapshot? snap2 = null;
        EventHandler<VehicleStateSnapshot> h2 = (_, s) => snap2 = s;
        _autoSteer.StateUpdated += h2;
        _autoSteer.ProcessGpsBuffer(bytes2, bytes2.Length);
        _autoSteer.StateUpdated -= h2;

        Assert.That(snap2, Is.Not.Null, "Should produce snapshot for second fix at lat=0");
        Assert.That(Math.Abs(snap2!.Value.Easting), Is.GreaterThan(50),
            "Easting should change when longitude changes at equator");
        Assert.That(Math.Abs(snap2.Value.Northing), Is.LessThan(5),
            "Northing should stay near 0 when only longitude changed");
    }

    [Test]
    public void TractorMoves_AtEquator_WithField()
    {
        // Set field origin at equator
        var fieldOrigin = new Wgs84(0.0, 0.0);
        var sharedProps = new SharedFieldProperties();
        _autoSteer.SetLocalPlane(new LocalPlane(fieldOrigin, sharedProps), sharedProps);

        // GPS fix slightly north of equator
        var bytes = BuildPandaBytes(lat: 0.001, lon: 0.0, heading: 0, speedKnots: 5);

        VehicleStateSnapshot? snap = null;
        EventHandler<VehicleStateSnapshot> h = (_, s) => snap = s;
        _autoSteer.StateUpdated += h;
        _autoSteer.ProcessGpsBuffer(bytes, bytes.Length);
        _autoSteer.StateUpdated -= h;

        Assert.That(snap, Is.Not.Null, "Should produce snapshot at equator with field");
        Assert.That(snap!.Value.Northing, Is.GreaterThan(50).And.LessThan(200),
            $"Northing should be ~111m for 0.001 deg at equator, got {snap.Value.Northing:F1}");
    }
}
