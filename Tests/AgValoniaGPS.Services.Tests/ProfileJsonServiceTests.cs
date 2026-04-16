using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Profile;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
[NonParallelizable] // Modifies ConfigurationStore.Instance
public class ProfileJsonServiceTests
{
    private string _tempDir = null!;
    private ConfigurationStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agvalonia_profile_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new ConfigurationStore();
        ConfigurationStore.SetInstance(_store);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void SaveAndLoad_DefaultProfile_RoundTrip()
    {
        SetupTestStore("TestTractor");

        ProfileJsonService.Save(_tempDir, "TestTractor", _store);

        Assert.That(File.Exists(Path.Combine(_tempDir, "TestTractor.json")), Is.True);

        var loadStore = new ConfigurationStore();
        var loaded = ProfileJsonService.Load(_tempDir, "TestTractor", loadStore);

        Assert.That(loaded, Is.True);
        Assert.That(loadStore.ActiveProfileName, Is.EqualTo("TestTractor"));
    }

    [Test]
    public void SaveAndLoad_VehicleConfig_AllProperties()
    {
        SetupTestStore("VehicleTest");
        _store.Vehicle.AntennaHeight = 4.2;
        _store.Vehicle.AntennaPivot = 1.1;
        _store.Vehicle.AntennaOffset = -0.3;
        _store.Vehicle.Wheelbase = 3.5;
        _store.Vehicle.TrackWidth = 2.1;
        _store.Vehicle.MaxSteerAngle = 40.0;
        _store.Vehicle.MaxAngularVelocity = 30.0;

        ProfileJsonService.Save(_tempDir, "VehicleTest", _store);
        var loadStore = new ConfigurationStore();
        ProfileJsonService.Load(_tempDir, "VehicleTest", loadStore);

        Assert.That(loadStore.Vehicle.AntennaHeight, Is.EqualTo(4.2).Within(1e-6));
        Assert.That(loadStore.Vehicle.AntennaPivot, Is.EqualTo(1.1).Within(1e-6));
        Assert.That(loadStore.Vehicle.AntennaOffset, Is.EqualTo(-0.3).Within(1e-6));
        Assert.That(loadStore.Vehicle.Wheelbase, Is.EqualTo(3.5).Within(1e-6));
        Assert.That(loadStore.Vehicle.TrackWidth, Is.EqualTo(2.1).Within(1e-6));
        Assert.That(loadStore.Vehicle.MaxSteerAngle, Is.EqualTo(40.0).Within(1e-6));
        Assert.That(loadStore.Vehicle.MaxAngularVelocity, Is.EqualTo(30.0).Within(1e-6));
    }

    [Test]
    public void SaveAndLoad_ToolConfig_AllProperties()
    {
        SetupTestStore("ToolTest");
        _store.Tool.Width = 12.0;
        _store.Tool.Overlap = 0.15;
        _store.Tool.Offset = -0.5;
        _store.Tool.HitchLength = 2.5;
        _store.Tool.IsToolTrailing = true;
        _store.Tool.IsToolTBT = false;
        _store.NumSections = 4;
        _store.Tool.MinCoverage = 80;
        _store.Tool.IsHeadlandSectionControl = true;

        ProfileJsonService.Save(_tempDir, "ToolTest", _store);
        var loadStore = new ConfigurationStore();
        ProfileJsonService.Load(_tempDir, "ToolTest", loadStore);

        Assert.That(loadStore.Tool.Width, Is.EqualTo(12.0).Within(1e-6));
        Assert.That(loadStore.Tool.Overlap, Is.EqualTo(0.15).Within(1e-6));
        Assert.That(loadStore.Tool.Offset, Is.EqualTo(-0.5).Within(1e-6));
        Assert.That(loadStore.Tool.HitchLength, Is.EqualTo(2.5).Within(1e-6));
        Assert.That(loadStore.Tool.IsToolTrailing, Is.True);
        Assert.That(loadStore.Tool.IsToolTBT, Is.False);
        Assert.That(loadStore.NumSections, Is.EqualTo(4));
        Assert.That(loadStore.Tool.MinCoverage, Is.EqualTo(80));
        Assert.That(loadStore.Tool.IsHeadlandSectionControl, Is.True);
    }

    [Test]
    public void SaveAndLoad_GuidanceConfig_RoundTrip()
    {
        SetupTestStore("GuidanceTest");
        _store.Guidance.GoalPointLookAheadHold = 5.0;
        _store.Guidance.StanleyDistanceErrorGain = 1.2;
        _store.Guidance.StanleyHeadingErrorGain = 0.9;
        _store.Guidance.PurePursuitIntegralGain = 0.05;
        _store.Guidance.IsPurePursuit = false;

        ProfileJsonService.Save(_tempDir, "GuidanceTest", _store);
        var loadStore = new ConfigurationStore();
        ProfileJsonService.Load(_tempDir, "GuidanceTest", loadStore);

        Assert.That(loadStore.Guidance.GoalPointLookAheadHold, Is.EqualTo(5.0).Within(1e-6));
        Assert.That(loadStore.Guidance.StanleyDistanceErrorGain, Is.EqualTo(1.2).Within(1e-6));
        Assert.That(loadStore.Guidance.StanleyHeadingErrorGain, Is.EqualTo(0.9).Within(1e-6));
        Assert.That(loadStore.Guidance.PurePursuitIntegralGain, Is.EqualTo(0.05).Within(1e-6));
        Assert.That(loadStore.Guidance.IsPurePursuit, Is.False);
    }

    [Test]
    public void SaveAndLoad_SectionPositions_DynamicArray()
    {
        SetupTestStore("SectionTest");
        _store.NumSections = 4;
        _store.SectionPositions = new double[17];
        _store.SectionPositions[0] = -6.0;
        _store.SectionPositions[1] = -3.0;
        _store.SectionPositions[2] = 0.0;
        _store.SectionPositions[3] = 3.0;
        _store.SectionPositions[4] = 6.0;

        ProfileJsonService.Save(_tempDir, "SectionTest", _store);
        var loadStore = new ConfigurationStore();
        ProfileJsonService.Load(_tempDir, "SectionTest", loadStore);

        Assert.That(loadStore.NumSections, Is.EqualTo(4));
        Assert.That(loadStore.SectionPositions[0], Is.EqualTo(-6.0).Within(1e-6));
        Assert.That(loadStore.SectionPositions[1], Is.EqualTo(-3.0).Within(1e-6));
        Assert.That(loadStore.SectionPositions[2], Is.EqualTo(0.0).Within(1e-6));
        Assert.That(loadStore.SectionPositions[3], Is.EqualTo(3.0).Within(1e-6));
        Assert.That(loadStore.SectionPositions[4], Is.EqualTo(6.0).Within(1e-6));
    }

    [Test]
    public void SaveAndLoad_YouTurnConfig_RoundTrip()
    {
        SetupTestStore("UTurnTest");
        _store.Guidance.UTurnRadius = 10.0;
        _store.Guidance.UTurnExtension = 25.0;
        _store.Guidance.UTurnDistanceFromBoundary = 3.0;
        _store.Guidance.UTurnSkipWidth = 2;
        _store.Guidance.UTurnStyle = 1;
        _store.Guidance.UTurnSmoothing = 20;

        ProfileJsonService.Save(_tempDir, "UTurnTest", _store);
        var loadStore = new ConfigurationStore();
        ProfileJsonService.Load(_tempDir, "UTurnTest", loadStore);

        Assert.That(loadStore.Guidance.UTurnRadius, Is.EqualTo(10.0).Within(1e-6));
        Assert.That(loadStore.Guidance.UTurnExtension, Is.EqualTo(25.0).Within(1e-6));
        Assert.That(loadStore.Guidance.UTurnDistanceFromBoundary, Is.EqualTo(3.0).Within(1e-6));
        Assert.That(loadStore.Guidance.UTurnSkipWidth, Is.EqualTo(2));
        Assert.That(loadStore.Guidance.UTurnStyle, Is.EqualTo(1));
        Assert.That(loadStore.Guidance.UTurnSmoothing, Is.EqualTo(20));
    }

    [Test]
    public void SaveAndLoad_GeneralSettings_RoundTrip()
    {
        SetupTestStore("GeneralTest");
        _store.IsMetric = true;
        _store.Simulator.Enabled = false;
        _store.Simulator.Latitude = 48.8566;
        _store.Simulator.Longitude = 2.3522;

        ProfileJsonService.Save(_tempDir, "GeneralTest", _store);
        var loadStore = new ConfigurationStore();
        ProfileJsonService.Load(_tempDir, "GeneralTest", loadStore);

        Assert.That(loadStore.IsMetric, Is.True);
    }

    [Test]
    public void Load_MissingFile_ReturnsFalse()
    {
        var loadStore = new ConfigurationStore();
        var result = ProfileJsonService.Load(_tempDir, "NonExistent", loadStore);
        Assert.That(result, Is.False);
    }

    [Test]
    public void Exists_ReturnsFalse_WhenNoFile()
    {
        Assert.That(ProfileJsonService.Exists(_tempDir, "Nope"), Is.False);
    }

    [Test]
    public void Exists_ReturnsTrue_AfterSave()
    {
        SetupTestStore("ExistsTest");
        ProfileJsonService.Save(_tempDir, "ExistsTest", _store);
        Assert.That(ProfileJsonService.Exists(_tempDir, "ExistsTest"), Is.True);
    }

    [Test]
    public void JsonOutput_IsValidAndReadable()
    {
        SetupTestStore("JsonCheck");
        ProfileJsonService.Save(_tempDir, "JsonCheck", _store);
        var json = File.ReadAllText(Path.Combine(_tempDir, "JsonCheck.json"));

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.GetProperty("formatVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(root.TryGetProperty("vehicle", out _), Is.True);
        Assert.That(root.TryGetProperty("guidance", out _), Is.True);
        Assert.That(root.TryGetProperty("tool", out _), Is.True);
        Assert.That(root.TryGetProperty("sections", out _), Is.True);
        Assert.That(root.TryGetProperty("youTurn", out _), Is.True);
        Assert.That(root.TryGetProperty("general", out _), Is.True);
    }

    [Test]
    public void UTurnCompensation_RoundTrips()
    {
        SetupTestStore("UTurnComp");
        _store.Guidance.UTurnCompensation = 1.75;

        ProfileJsonService.Save(_tempDir, "UTurnComp", _store);
        var loadStore = new ConfigurationStore();
        ProfileJsonService.Load(_tempDir, "UTurnComp", loadStore);

        Assert.That(loadStore.Guidance.UTurnCompensation, Is.EqualTo(1.75).Within(1e-6));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void SetupTestStore(string name)
    {
        _store.ActiveProfileName = name;
        _store.Vehicle.Name = name;
        _store.Tool.Width = 6.0;
        _store.NumSections = 1;
        _store.SectionPositions = new double[17] { -3.0, 3.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        _store.IsMetric = false;
        _store.Guidance.IsPurePursuit = true;
        _store.Simulator.Enabled = true;
    }
}
