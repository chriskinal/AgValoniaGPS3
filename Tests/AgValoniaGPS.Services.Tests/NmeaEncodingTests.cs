// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AgValoniaGPS.IntegrationTests.VirtualModules;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Unit tests for NMEA sentence encoding in VirtualGpsReceiver.
/// Verifies that $PANDA sentences have correct format, checksum,
/// and sufficient coordinate precision.
/// </summary>
[TestFixture]
public class NmeaEncodingTests
{
    /// <summary>
    /// Capture the raw $PANDA string from VirtualGpsReceiver via UDP.
    /// </summary>
    private static string CapturePanda(double lat, double lon,
        double heading = 0, double speedKnots = 5, int fixQuality = 4,
        double roll = 0, double pitch = 0, double yawRate = 0)
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
        gps.RollDegrees = roll;
        gps.PitchDegrees = pitch;
        gps.YawRateDegPerSec = yawRate;

        gps.SendOnce();
        IPEndPoint? remote = null;
        var bytes = listener.Receive(ref remote);
        return Encoding.ASCII.GetString(bytes).Trim();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Format tests
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void PandaSentence_StartsWithDollarPANDA()
    {
        var sentence = CapturePanda(42.0, -93.0);
        Assert.That(sentence, Does.StartWith("$PANDA,"),
            "Sentence must start with $PANDA,");
    }

    [Test]
    public void PandaSentence_HasCorrectFieldCount()
    {
        var sentence = CapturePanda(42.0, -93.0);
        int asterisk = sentence.IndexOf('*');
        string body = sentence.Substring(0, asterisk);
        string[] fields = body.Split(',');

        // $PANDA,time,lat,N/S,lon,E/W,fix,sats,hdop,alt,age,speed,heading,roll,pitch,yaw
        Assert.That(fields.Length, Is.EqualTo(16),
            $"$PANDA should have 16 fields, got {fields.Length}: {body}");
    }

    [Test]
    public void PandaSentence_HasValidChecksum()
    {
        var sentence = CapturePanda(42.0, -93.0);
        int asterisk = sentence.IndexOf('*');
        Assert.That(asterisk, Is.GreaterThan(0), "Sentence must contain *");

        // Calculate XOR checksum of chars between $ and *
        byte computed = 0;
        for (int i = 1; i < asterisk; i++)
            computed ^= (byte)sentence[i];

        string providedHex = sentence.Substring(asterisk + 1, 2);
        byte provided = byte.Parse(providedHex, NumberStyles.HexNumber);

        Assert.That(computed, Is.EqualTo(provided),
            $"Checksum mismatch: computed 0x{computed:X2}, sentence has 0x{provided:X2}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Coordinate precision tests
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void LatitudeFormat_HasAtLeast5DecimalPlaces()
    {
        var sentence = CapturePanda(42.123456, -93.0);
        var fields = sentence.Split(',');
        string latField = fields[2]; // DDMM.MMMMM

        // Find decimal point
        int dot = latField.IndexOf('.');
        Assert.That(dot, Is.GreaterThan(0), "Latitude must contain decimal point");

        int decimalPlaces = latField.Length - dot - 1;
        TestContext.Out.WriteLine($"Latitude field: {latField} ({decimalPlaces} decimal places)");

        Assert.That(decimalPlaces, Is.GreaterThanOrEqualTo(5),
            $"Latitude has {decimalPlaces} decimal places, need >= 5 for sub-5cm resolution. " +
            $"4 places = 0.185m resolution (causes stepping at low speed).");
    }

    [Test]
    public void LongitudeFormat_HasAtLeast5DecimalPlaces()
    {
        var sentence = CapturePanda(42.0, -93.654321);
        var fields = sentence.Split(',');
        string lonField = fields[4]; // DDDMM.MMMMM

        int dot = lonField.IndexOf('.');
        Assert.That(dot, Is.GreaterThan(0), "Longitude must contain decimal point");

        int decimalPlaces = lonField.Length - dot - 1;
        TestContext.Out.WriteLine($"Longitude field: {lonField} ({decimalPlaces} decimal places)");

        Assert.That(decimalPlaces, Is.GreaterThanOrEqualTo(5),
            $"Longitude has {decimalPlaces} decimal places, need >= 5 for sub-5cm resolution.");
    }

    [Test]
    public void LatitudeEncoding_PreservesSubMeterDifferences()
    {
        // Two positions 0.05m apart
        double lat1 = 42.0;
        double lat2 = lat1 + 0.05 / 111320.0;

        var s1 = CapturePanda(lat1, -93.0);
        var s2 = CapturePanda(lat2, -93.0);

        string latField1 = s1.Split(',')[2];
        string latField2 = s2.Split(',')[2];

        TestContext.Out.WriteLine($"Position 1: {latField1}");
        TestContext.Out.WriteLine($"Position 2: {latField2}");
        TestContext.Out.WriteLine($"Input difference: 0.05m");

        Assert.That(latField1, Is.Not.EqualTo(latField2),
            "Two positions 5cm apart must encode to different NMEA strings. " +
            "If identical, the NMEA format resolution is too low.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Hemisphere and sign tests
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void NorthernLatitude_HasN()
    {
        var sentence = CapturePanda(42.0, -93.0);
        Assert.That(sentence.Split(',')[3], Is.EqualTo("N"));
    }

    [Test]
    public void SouthernLatitude_HasS()
    {
        var sentence = CapturePanda(-33.5, -93.0);
        Assert.That(sentence.Split(',')[3], Is.EqualTo("S"));
    }

    [Test]
    public void EasternLongitude_HasE()
    {
        var sentence = CapturePanda(42.0, 11.5);
        Assert.That(sentence.Split(',')[5], Is.EqualTo("E"));
    }

    [Test]
    public void WesternLongitude_HasW()
    {
        var sentence = CapturePanda(42.0, -93.0);
        Assert.That(sentence.Split(',')[5], Is.EqualTo("W"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Data field tests
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public void FixQuality_EncodedCorrectly()
    {
        var sentence = CapturePanda(42.0, -93.0, fixQuality: 4);
        Assert.That(sentence.Split(',')[6], Is.EqualTo("4"));
    }

    [Test]
    public void ImuFields_EncodedWithInvariantCulture()
    {
        // Roll=1.5, pitch=-0.3, yaw=0.2 must use dot decimal, not comma
        var sentence = CapturePanda(42.0, -93.0, roll: 1.5, pitch: -0.3, yawRate: 0.2);
        var fields = sentence.Split(',');

        // Last field has checksum: "0.20*XX"
        string yawField = fields[15].Split('*')[0];

        Assert.That(fields[13], Is.EqualTo("1.50"), $"Roll should be 1.50, got {fields[13]}");
        Assert.That(fields[14], Is.EqualTo("-0.30"), $"Pitch should be -0.30, got {fields[14]}");
        Assert.That(yawField, Is.EqualTo("0.20"), $"Yaw should be 0.20, got {yawField}");
    }

    [Test]
    public void SpeedField_UsesInvariantCulture()
    {
        var sentence = CapturePanda(42.0, -93.0, speedKnots: 5.5);
        var fields = sentence.Split(',');

        Assert.That(fields[11], Is.EqualTo("5.50"),
            $"Speed should use dot decimal: expected 5.50, got {fields[11]}");
    }
}
