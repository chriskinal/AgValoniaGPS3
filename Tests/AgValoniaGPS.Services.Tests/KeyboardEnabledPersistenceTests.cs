using System.Text.Json;
using System.Text.Json.Serialization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests that KeyboardEnabled and SvennArrowVisible persist through
/// JSON serialization and ConfigurationStore round-trips (#114, #115).
/// </summary>
[TestFixture]
public class DisplayTogglePersistenceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    [Test]
    public void AppSettings_KeyboardEnabled_DefaultIsFalse()
    {
        var settings = new AppSettings();
        Assert.That(settings.KeyboardEnabled, Is.False);
    }

    [Test]
    public void AppSettings_SvennArrowVisible_DefaultIsFalse()
    {
        var settings = new AppSettings();
        Assert.That(settings.SvennArrowVisible, Is.False);
    }

    [Test]
    public void AppSettings_KeyboardEnabled_SurvivesJsonRoundTrip()
    {
        var original = new AppSettings { KeyboardEnabled = true };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.KeyboardEnabled, Is.True);
    }

    [Test]
    public void AppSettings_SvennArrowVisible_SurvivesJsonRoundTrip()
    {
        var original = new AppSettings { SvennArrowVisible = true };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.SvennArrowVisible, Is.True);
    }

    [Test]
    public void AppSettings_KeyboardEnabled_FalsePreservedInJson()
    {
        var original = new AppSettings { KeyboardEnabled = false };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        Assert.That(deserialized!.KeyboardEnabled, Is.False);
    }

    [Test]
    public void AppSettings_MissingProperty_DeserializesToDefault()
    {
        // Simulate loading old settings file without the new property
        var json = """{"WindowWidth":1200,"WindowHeight":800}""";
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.KeyboardEnabled, Is.False);
        Assert.That(deserialized!.SvennArrowVisible, Is.False);
    }

    [Test]
    public void DisplayConfig_KeyboardEnabled_IsReactive()
    {
        var display = new DisplayConfig();
        bool changed = false;
        display.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DisplayConfig.KeyboardEnabled))
                changed = true;
        };

        display.KeyboardEnabled = true;

        Assert.That(changed, Is.True);
        Assert.That(display.KeyboardEnabled, Is.True);
    }

    [Test]
    public void DisplayConfig_SvennArrowVisible_IsReactive()
    {
        var display = new DisplayConfig();
        bool changed = false;
        display.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DisplayConfig.SvennArrowVisible))
                changed = true;
        };

        display.SvennArrowVisible = true;

        Assert.That(changed, Is.True);
        Assert.That(display.SvennArrowVisible, Is.True);
    }
}
