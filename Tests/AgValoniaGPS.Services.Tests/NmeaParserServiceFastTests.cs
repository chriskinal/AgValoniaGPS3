// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Globalization;
using System.Text;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Unit tests for <see cref="NmeaParserServiceFast"/>, the sole NMEA parser
/// after Phase B C5. Ported from the deleted <c>NmeaParserServiceTests</c>;
/// tests that exercised heading fusion, fix-quality filtering, or the
/// ConsecutiveBadFixes counter were dropped because those responsibilities
/// moved to <see cref="Gps.GpsHeadingFusionService"/> and
/// <see cref="Gps.GpsFixQualityValidator"/> (Phase B C2) and are covered
/// by those services' own test suites.
/// </summary>
[TestFixture]
[NonParallelizable]
public class NmeaParserServiceFastTests
{
    private IGpsService _gpsService = null!;
    private NmeaParserServiceFast _parser = null!;
    private GpsData? _lastGpsData;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _gpsService = Substitute.For<IGpsService>();
        _gpsService.When(x => x.UpdateGpsData(Arg.Any<GpsData>()))
                   .Do(ci => _lastGpsData = ci.Arg<GpsData>());
        _lastGpsData = null;

        _parser = new NmeaParserServiceFast(_gpsService);
    }

    // ── Checksum validation ─────────────────────────────────────────────

    [Test]
    public void ParseSpan_ValidChecksum_Parses()
    {
        byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        _gpsService.Received(1).UpdateGpsData(Arg.Any<GpsData>());
    }

    [Test]
    public void ParseSpan_InvalidChecksum_DoesNotParse()
    {
        byte[] sentence = Encoding.ASCII.GetBytes(
            "$PANDA,123456.00,4807.038,N,01131.000,E,4,12,0.9,100,0,5.5,90.0,0,0,0*FF");

        _parser.ParseSpan(sentence);

        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    [Test]
    public void ParseSpan_TooShort_DoesNotParse()
    {
        byte[] sentence = Encoding.ASCII.GetBytes("$PANDA,1");

        _parser.ParseSpan(sentence);

        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    [Test]
    public void ParseSpan_NoDollar_DoesNotParse()
    {
        byte[] sentence = Encoding.ASCII.GetBytes(
            "PANDA,123456.00,4807.038,N,01131.000,E,4,12,0.9,100,0,5.5,90.0,0,0,0*48");

        _parser.ParseSpan(sentence);

        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    [Test]
    public void ParseSpan_UnknownSentenceType_DoesNotParse()
    {
        byte[] sentence = BuildSentence("GPGGA,123456.00,4807.038,N,01131.000,E,1,12,0.9,100,M,0,M,,");

        _parser.ParseSpan(sentence);

        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    // ── Latitude / Longitude parsing ───────────────────────────────────

    [Test]
    public void ParseSpan_NorthernLatitude_Positive()
    {
        byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Latitude, Is.EqualTo(48.1173).Within(0.001));
    }

    [Test]
    public void ParseSpan_SouthernLatitude_Negative()
    {
        byte[] sentence = BuildPandaBytes(3352.128, "S", 15112.556, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Latitude, Is.LessThan(0));
    }

    [Test]
    public void ParseSpan_WesternLongitude_Negative()
    {
        byte[] sentence = BuildPandaBytes(4807.038, "N", 07400.360, "W", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Longitude, Is.LessThan(0));
    }

    [Test]
    public void ParseSpan_EasternLongitude_Positive()
    {
        byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Longitude, Is.GreaterThan(0));
    }

    // ── Field passthrough ──────────────────────────────────────────────

    [Test]
    public void ParseSpan_FixQuality_PassesThrough()
    {
        byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.FixQuality, Is.EqualTo(4));
        Assert.That(_lastGpsData.SatellitesInUse, Is.EqualTo(12));
    }

    [Test]
    public void ParseSpan_SpeedInKnots_ConvertedToMs()
    {
        byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 10.0, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Speed, Is.EqualTo(5.14444).Within(0.01));
    }

    // ── Sentence-type coverage ─────────────────────────────────────────

    [Test]
    public void ParseSpan_PAOGI_ParsesLikePANDA()
    {
        byte[] sentence = BuildPaogiBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSpan(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Latitude, Is.EqualTo(48.1173).Within(0.001));
    }

    // ── IMU heading + roll wire-format scaling (zero-copy ParseIntoState) ─

    [Test]
    public void ParseIntoState_PANDA_DividesHeadingBy10()
    {
        // Wire "905" represents 90.5° on the AiO PANDA encoding.
        byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5,
            heading: 90.5, roll: 0, pitch: 0, yawRate: 0);
        var state = new VehicleState();

        bool ok = NmeaParserServiceFast.ParseIntoState(sentence, ref state);

        Assert.That(ok, Is.True);
        Assert.That(state.ImuValid, Is.True);
        Assert.That(state.ImuHeading, Is.EqualTo(90.5).Within(1e-6));
        // Primary heading is seeded from IMU at parse time (pipeline overrides at speed).
        Assert.That(state.Heading, Is.EqualTo(90.5).Within(1e-6));
    }

    [Test]
    public void ParseIntoState_PANDA_DividesRollBy10()
    {
        // Wire "57" represents 5.7° on PANDA encoding.
        byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5,
            heading: 90.0, roll: 5.7, pitch: 0, yawRate: 0);
        var state = new VehicleState();

        NmeaParserServiceFast.ParseIntoState(sentence, ref state);

        Assert.That(state.Roll, Is.EqualTo(5.7).Within(1e-6));
    }

    [Test]
    public void ParseIntoState_PANDA_65535SentinelMarksImuInvalid()
    {
        byte[] sentence = BuildPandaBytesNoImu(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5);
        var state = new VehicleState();

        bool ok = NmeaParserServiceFast.ParseIntoState(sentence, ref state);

        Assert.That(ok, Is.True);
        Assert.That(state.ImuValid, Is.False);
        Assert.That(state.ImuHeading, Is.EqualTo(0));
        Assert.That(state.Roll, Is.EqualTo(0));
        Assert.That(state.Heading, Is.EqualTo(0),
            "primary heading should fall to 0 when IMU is invalid; pipeline picks up fix-to-fix");
    }

    [Test]
    public void ParseIntoState_PANDA_AppliesAhrsIsRollInvert()
    {
        // The Roll-calibration wizard step's IsRollInvert toggle must
        // take effect on live NMEA roll, not just on the values the
        // wizard captures via OnLeaving. Without this post-process the
        // operator's "Invert Roll" toggle has no visible effect on the
        // gauge.
        var prior = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(
            new AgValoniaGPS.Models.Configuration.ConfigurationStore());
        try
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Ahrs.IsRollInvert = true;

            byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5,
                heading: 90.0, roll: 5.7, pitch: 0, yawRate: 0);
            var state = new VehicleState();
            NmeaParserServiceFast.ParseIntoState(sentence, ref state);

            Assert.That(state.Roll, Is.EqualTo(-5.7).Within(1e-6),
                "IsRollInvert flips the sign of the parsed roll before downstream consumers.");
        }
        finally
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(prior);
        }
    }

    [Test]
    public void ParseIntoState_PANDA_SubtractsAhrsRollZero()
    {
        // Zero Roll calibration captures the current reading as the
        // zero point; subsequent parses subtract it so the gauge centres.
        var prior = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(
            new AgValoniaGPS.Models.Configuration.ConfigurationStore());
        try
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Ahrs.RollZero = 2.0;

            byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5,
                heading: 90.0, roll: 5.7, pitch: 0, yawRate: 0);
            var state = new VehicleState();
            NmeaParserServiceFast.ParseIntoState(sentence, ref state);

            Assert.That(state.Roll, Is.EqualTo(3.7).Within(1e-6),
                "RollZero offset is subtracted from the raw IMU roll.");
        }
        finally
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(prior);
        }
    }

    [Test]
    public void ParseIntoState_PANDA_InvertAndZero_CompositeOrder()
    {
        // Order matters: invert first, then subtract zero. Mirrors the
        // legacy AiO firmware pipeline so the operator's calibration
        // gives the same readings as the original hardware path.
        var prior = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(
            new AgValoniaGPS.Models.Configuration.ConfigurationStore());
        try
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Ahrs.IsRollInvert = true;
            AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Ahrs.RollZero = 2.0;

            byte[] sentence = BuildPandaBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5,
                heading: 90.0, roll: 5.7, pitch: 0, yawRate: 0);
            var state = new VehicleState();
            NmeaParserServiceFast.ParseIntoState(sentence, ref state);

            // -5.7 - 2.0 = -7.7
            Assert.That(state.Roll, Is.EqualTo(-7.7).Within(1e-6),
                "Composite: invert first (-5.7), then subtract zero (-7.7).");
        }
        finally
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(prior);
        }
    }

    [Test]
    public void ParseIntoState_PAOGI_AlsoAppliesAhrsCalibration()
    {
        // Same post-process must mirror in the PAOGI branch — its roll
        // field is float decimal degrees but the calibration semantics
        // are identical.
        var prior = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance;
        AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(
            new AgValoniaGPS.Models.Configuration.ConfigurationStore());
        try
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Ahrs.IsRollInvert = true;
            AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Ahrs.RollZero = 1.0;

            byte[] sentence = BuildPaogiBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5,
                heading: 90.5, roll: 1.25, pitch: 0, yawRate: 0);
            var state = new VehicleState();
            NmeaParserServiceFast.ParseIntoState(sentence, ref state);

            // -1.25 - 1.0 = -2.25
            Assert.That(state.Roll, Is.EqualTo(-2.25).Within(1e-6));
        }
        finally
        {
            AgValoniaGPS.Models.Configuration.ConfigurationStore.SetInstance(prior);
        }
    }

    [Test]
    public void ParseIntoState_PAOGI_HeadingAsFloatNoScaling_ImuStaysInvalid()
    {
        // PAOGI emits heading as float decimal degrees — no ×10 scaling.
        byte[] sentence = BuildPaogiBytes(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5,
            heading: 90.5, roll: 1.25, pitch: 0, yawRate: 0);
        var state = new VehicleState();

        NmeaParserServiceFast.ParseIntoState(sentence, ref state);

        Assert.That(state.Heading, Is.EqualTo(90.5).Within(1e-6));
        Assert.That(state.Roll, Is.EqualTo(1.25).Within(1e-6));
        Assert.That(state.ImuValid, Is.False,
            "PAOGI is dual-antenna ground truth — fusion isn't needed, ImuValid stays false");
        Assert.That(state.ImuHeading, Is.EqualTo(0));
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static byte[] BuildPandaBytes(
        double lat, string latDir, double lon, string lonDir,
        int fixQuality, int sats, double hdop, double alt,
        double diffAge, double speedKnots, double heading,
        double roll, double pitch, double yawRate)
    {
        // PANDA wire format: heading and roll are int scaled ×10 (with 65535
        // sentinel for "no IMU" on heading); pitch is rounded int; yawRate
        // is float. Mirrors AiO firmware's NAVProcessor::formatPANDAMessage.
        int headingX10 = (int)System.Math.Round(heading * 10.0);
        int rollX10 = (int)System.Math.Round(roll * 10.0);
        int pitchInt = (int)System.Math.Round(pitch);
        string body = string.Format(CultureInfo.InvariantCulture,
            "PANDA,123456.00,{0:F3},{1},{2:F3},{3},{4},{5},{6:F1},{7:F1},{8:F1},{9:F1},{10},{11},{12},{13:F2}",
            lat, latDir, lon, lonDir, fixQuality, sats, hdop, alt, diffAge, speedKnots,
            headingX10, rollX10, pitchInt, yawRate);
        return BuildSentence(body);
    }

    /// <summary>
    /// Builds a PANDA sentence with the "no IMU" sentinel (65535) in the
    /// heading field. Used to test that parser detects the sentinel and
    /// leaves <c>state.ImuValid = false</c>.
    /// </summary>
    private static byte[] BuildPandaBytesNoImu(
        double lat, string latDir, double lon, string lonDir,
        int fixQuality, int sats, double hdop, double alt,
        double diffAge, double speedKnots)
    {
        string body = string.Format(CultureInfo.InvariantCulture,
            "PANDA,123456.00,{0:F3},{1},{2:F3},{3},{4},{5},{6:F1},{7:F1},{8:F1},{9:F1},65535,0,0,0.00",
            lat, latDir, lon, lonDir, fixQuality, sats, hdop, alt, diffAge, speedKnots);
        return BuildSentence(body);
    }

    /// <summary>
    /// Builds a PAOGI sentence (dual antenna). Heading and roll are float
    /// decimal degrees, no scaling — matches AiO firmware's
    /// NAVProcessor::formatPAOGIMessage.
    /// </summary>
    private static byte[] BuildPaogiBytes(
        double lat, string latDir, double lon, string lonDir,
        int fixQuality, int sats, double hdop, double alt,
        double diffAge, double speedKnots, double heading,
        double roll, double pitch, double yawRate)
    {
        int pitchInt = (int)System.Math.Round(pitch);
        string body = string.Format(CultureInfo.InvariantCulture,
            "PAOGI,123456.00,{0:F3},{1},{2:F3},{3},{4},{5},{6:F1},{7:F1},{8:F1},{9:F1},{10:F1},{11:F2},{12},{13:F2}",
            lat, latDir, lon, lonDir, fixQuality, sats, hdop, alt, diffAge, speedKnots,
            heading, roll, pitchInt, yawRate);
        return BuildSentence(body);
    }

    private static byte[] BuildSentence(string body)
    {
        byte checksum = 0;
        foreach (char c in body) checksum ^= (byte)c;
        return Encoding.ASCII.GetBytes($"${body}*{checksum:X2}");
    }
}
