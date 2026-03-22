using System.Globalization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
[NonParallelizable]
public class NmeaParserServiceTests
{
    private IGpsService _gpsService = null!;
    private NmeaParserService _parser = null!;
    private GpsData? _lastGpsData;

    [SetUp]
    public void SetUp()
    {
        // Isolate ConfigurationStore singleton for each test
        ConfigurationStore.SetInstance(new ConfigurationStore());

        _gpsService = Substitute.For<IGpsService>();
        _gpsService.When(x => x.UpdateGpsData(Arg.Any<GpsData>()))
            .Do(ci => _lastGpsData = ci.Arg<GpsData>());
        _lastGpsData = null;

        _parser = new NmeaParserService(_gpsService);
    }

    #region Checksum Validation

    [Test]
    public void ParseSentence_ValidChecksum_Parses()
    {
        string sentence = BuildPandaSentence(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);

        _parser.ParseSentence(sentence);

        _gpsService.Received(1).UpdateGpsData(Arg.Any<GpsData>());
    }

    [Test]
    public void ParseSentence_InvalidChecksum_DoesNotParse()
    {
        string sentence = "$PANDA,123456.00,4807.038,N,01131.000,E,4,12,0.9,100,0,5.5,90.0,0,0,0*FF";

        _parser.ParseSentence(sentence);

        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    [Test]
    public void ParseSentence_NullOrEmpty_DoesNothing()
    {
        _parser.ParseSentence(null!);
        _parser.ParseSentence("");
        _parser.ParseSentence("   ");

        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    [Test]
    public void ParseSentence_NoAsterisk_DoesNotParse()
    {
        _parser.ParseSentence("$PANDA,no,checksum,here");
        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    #endregion

    #region Latitude / Longitude Parsing

    [Test]
    public void ParseSentence_NorthernLatitude_Positive()
    {
        // 48 degrees 07.038 minutes N = 48.1173
        string sentence = BuildPandaSentence(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Latitude, Is.EqualTo(48.1173).Within(0.001));
    }

    [Test]
    public void ParseSentence_SouthernLatitude_Negative()
    {
        string sentence = BuildPandaSentence(3352.128, "S", 15112.556, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Latitude, Is.LessThan(0));
    }

    [Test]
    public void ParseSentence_WesternLongitude_Negative()
    {
        string sentence = BuildPandaSentence(4807.038, "N", 07400.360, "W", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Longitude, Is.LessThan(0));
    }

    [Test]
    public void ParseSentence_EasternLongitude_Positive()
    {
        string sentence = BuildPandaSentence(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Longitude, Is.GreaterThan(0));
    }

    #endregion

    #region Fix Quality Filtering

    [Test]
    public void ParseSentence_GoodFix_IsValid()
    {
        // Default min fix quality is typically 1; fix=4 should pass
        string sentence = BuildPandaSentence(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.IsValid, Is.True);
        Assert.That(_parser.ConsecutiveBadFixes, Is.EqualTo(0));
    }

    [Test]
    public void ParseSentence_BadFix_IsNotValid()
    {
        // Set minimum fix quality to 4 (RTK)
        ConfigurationStore.Instance.Connections.MinFixQuality = 4;

        // Send fix quality 1 (GPS only)
        string sentence = BuildPandaSentence(4807.038, "N", 01131.000, "E", 1, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.IsValid, Is.False);
        Assert.That(_parser.ConsecutiveBadFixes, Is.EqualTo(1));
    }

    [Test]
    public void ParseSentence_ConsecutiveBadFixes_Increments()
    {
        ConfigurationStore.Instance.Connections.MinFixQuality = 4;

        string bad = BuildPandaSentence(4807.038, "N", 01131.000, "E", 1, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(bad);
        _parser.ParseSentence(bad);
        _parser.ParseSentence(bad);

        Assert.That(_parser.ConsecutiveBadFixes, Is.EqualTo(3));
    }

    [Test]
    public void ParseSentence_GoodFixAfterBad_ResetsCounter()
    {
        ConfigurationStore.Instance.Connections.MinFixQuality = 2;

        string bad = BuildPandaSentence(4807.038, "N", 01131.000, "E", 1, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(bad);
        Assert.That(_parser.ConsecutiveBadFixes, Is.EqualTo(1));

        string good = BuildPandaSentence(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(good);
        Assert.That(_parser.ConsecutiveBadFixes, Is.EqualTo(0));
    }

    [Test]
    public void ParseSentence_FixQualityBelowMinimum_EventFired()
    {
        ConfigurationStore.Instance.Connections.MinFixQuality = 4;
        bool eventFired = false;
        _parser.FixQualityBelowMinimum += (s, q) => eventFired = true;

        string bad = BuildPandaSentence(4807.038, "N", 01131.000, "E", 1, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        _parser.ParseSentence(bad);

        Assert.That(eventFired, Is.True);
    }

    #endregion

    #region Speed Parsing

    [Test]
    public void ParseSentence_SpeedInKnots_ConvertedToMs()
    {
        // 10 knots = 5.14444 m/s
        string sentence = BuildPandaSentence(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 10.0, 90.0, 0, 0, 0);
        _parser.ParseSentence(sentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Speed, Is.EqualTo(5.14444).Within(0.01));
    }

    #endregion

    #region PAOGI Format

    [Test]
    public void ParseSentence_PAOGI_ParsesLikePANDA()
    {
        string pandaSentence = BuildPandaSentence(4807.038, "N", 01131.000, "E", 4, 12, 0.9, 100, 0, 5.5, 90.0, 0, 0, 0);
        string paogiSentence = pandaSentence.Replace("$PANDA", "$PAOGI");
        // Recalculate checksum for PAOGI
        paogiSentence = RecalculateChecksum(paogiSentence);

        _parser.ParseSentence(paogiSentence);

        Assert.That(_lastGpsData, Is.Not.Null);
        Assert.That(_lastGpsData!.CurrentPosition.Latitude, Is.EqualTo(48.1173).Within(0.001));
    }

    #endregion

    #region Sentence Too Short

    [Test]
    public void ParseSentence_TooFewFields_DoesNotParse()
    {
        string sentence = "$PANDA,123";
        sentence = RecalculateChecksum(sentence);
        _parser.ParseSentence(sentence);
        _gpsService.DidNotReceive().UpdateGpsData(Arg.Any<GpsData>());
    }

    #endregion

    #region Helpers

    private static string BuildPandaSentence(
        double lat, string latDir, double lon, string lonDir,
        int fixQuality, int sats, double hdop, double alt,
        double diffAge, double speedKnots, double heading,
        double roll, double pitch, double yawRate)
    {
        string body = string.Format(CultureInfo.InvariantCulture,
            "PANDA,123456.00,{0:F3},{1},{2:F3},{3},{4},{5},{6:F1},{7:F1},{8:F1},{9:F1},{10:F1},{11:F1},{12:F1},{13:F1}",
            lat, latDir, lon, lonDir, fixQuality, sats, hdop, alt, diffAge, speedKnots, heading, roll, pitch, yawRate);

        byte checksum = 0;
        foreach (char c in body)
            checksum ^= (byte)c;

        return $"${body}*{checksum:X2}";
    }

    private static string RecalculateChecksum(string sentence)
    {
        int dollar = sentence.IndexOf('$');
        int asterisk = sentence.IndexOf('*');
        if (dollar < 0) return sentence;

        string body;
        if (asterisk > 0)
            body = sentence.Substring(dollar + 1, asterisk - dollar - 1);
        else
            body = sentence.Substring(dollar + 1);

        byte checksum = 0;
        foreach (char c in body)
            checksum ^= (byte)c;

        return $"${body}*{checksum:X2}";
    }

    #endregion
}
