using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests that AppSettings validation catches and fixes corrupt values.
/// </summary>
[TestFixture]
public class SettingsValidationTests
{
    [Test]
    public void ValidateAndFix_ValidSettings_NoFixes()
    {
        var settings = new AppSettings();
        var fixes = settings.ValidateAndFix();
        Assert.That(fixes, Is.Empty);
    }

    [Test]
    public void ValidateAndFix_CorruptLongitude_ResetsToDefault()
    {
        var settings = new AppSettings { SimulatorLongitude = 71280000 };
        var fixes = settings.ValidateAndFix();

        Assert.That(fixes, Has.Count.GreaterThan(0));
        Assert.That(settings.SimulatorLongitude, Is.EqualTo(-74.006).Within(0.001));
    }

    [Test]
    public void ValidateAndFix_CorruptLatitude_ResetsToDefault()
    {
        var settings = new AppSettings { SimulatorLatitude = -100 };
        var fixes = settings.ValidateAndFix();

        Assert.That(fixes, Has.Count.GreaterThan(0));
        Assert.That(settings.SimulatorLatitude, Is.EqualTo(40.7128).Within(0.001));
    }

    [Test]
    public void ValidateAndFix_ValidCoordinates_Preserved()
    {
        var settings = new AppSettings
        {
            SimulatorLatitude = 51.5074,
            SimulatorLongitude = -0.1278
        };
        var fixes = settings.ValidateAndFix();

        Assert.That(fixes, Is.Empty);
        Assert.That(settings.SimulatorLatitude, Is.EqualTo(51.5074));
        Assert.That(settings.SimulatorLongitude, Is.EqualTo(-0.1278));
    }

    [Test]
    public void ValidateAndFix_ZeroWindowSize_ResetsToDefault()
    {
        var settings = new AppSettings { WindowWidth = 0, WindowHeight = 50 };
        var fixes = settings.ValidateAndFix();

        Assert.That(fixes, Has.Count.EqualTo(2));
        Assert.That(settings.WindowWidth, Is.EqualTo(1200));
        Assert.That(settings.WindowHeight, Is.EqualTo(800));
    }

    [Test]
    public void ValidateAndFix_NegativeGpsRate_ResetsToDefault()
    {
        var settings = new AppSettings { GpsUpdateRate = -5 };
        var fixes = settings.ValidateAndFix();

        Assert.That(settings.GpsUpdateRate, Is.EqualTo(10));
    }

    [Test]
    public void ValidateAndFix_InvalidPort_ResetsToDefault()
    {
        var settings = new AppSettings { NtripCasterPort = 999999 };
        var fixes = settings.ValidateAndFix();

        Assert.That(settings.NtripCasterPort, Is.EqualTo(2101));
    }

    [Test]
    public void SimulatorConfig_ClampLatitude()
    {
        var config = new AgValoniaGPS.Models.Configuration.SimulatorConfig();
        config.Latitude = 100;
        Assert.That(config.Latitude, Is.EqualTo(90));

        config.Latitude = -100;
        Assert.That(config.Latitude, Is.EqualTo(-90));
    }

    [Test]
    public void SimulatorConfig_ClampLongitude()
    {
        var config = new AgValoniaGPS.Models.Configuration.SimulatorConfig();
        config.Longitude = 71280000;
        Assert.That(config.Longitude, Is.EqualTo(180));

        config.Longitude = -200;
        Assert.That(config.Longitude, Is.EqualTo(-180));
    }
}
