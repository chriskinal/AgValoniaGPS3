using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Tool;
using AgValoniaGPS.Services.Profile;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class ProfileJsonServiceTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agvalonia_profile_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

#pragma warning disable CS0612 // Type or member is obsolete (VehicleProfile/VehicleConfiguration/ToolConfiguration/YouTurnConfiguration)

    [Test]
    public void SaveAndLoad_DefaultProfile_RoundTrip()
    {
        var profile = CreateTestProfile("TestTractor");

        ProfileJsonService.Save(_tempDir, profile);

        Assert.That(File.Exists(Path.Combine(_tempDir, "TestTractor.json")), Is.True);

        var loaded = ProfileJsonService.Load(_tempDir, "TestTractor");

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Name, Is.EqualTo("TestTractor"));
    }

    [Test]
    public void SaveAndLoad_VehicleConfig_AllProperties()
    {
        var profile = CreateTestProfile("VehicleTest");
        profile.Vehicle.AntennaHeight = 4.2;
        profile.Vehicle.AntennaPivot = 1.1;
        profile.Vehicle.AntennaOffset = -0.3;
        profile.Vehicle.Wheelbase = 3.5;
        profile.Vehicle.TrackWidth = 2.1;
        profile.Vehicle.MaxSteerAngle = 40.0;
        profile.Vehicle.MaxAngularVelocity = 30.0;

        ProfileJsonService.Save(_tempDir, profile);
        var loaded = ProfileJsonService.Load(_tempDir, "VehicleTest")!;

        Assert.That(loaded.Vehicle.AntennaHeight, Is.EqualTo(4.2).Within(1e-6));
        Assert.That(loaded.Vehicle.AntennaPivot, Is.EqualTo(1.1).Within(1e-6));
        Assert.That(loaded.Vehicle.AntennaOffset, Is.EqualTo(-0.3).Within(1e-6));
        Assert.That(loaded.Vehicle.Wheelbase, Is.EqualTo(3.5).Within(1e-6));
        Assert.That(loaded.Vehicle.TrackWidth, Is.EqualTo(2.1).Within(1e-6));
        Assert.That(loaded.Vehicle.MaxSteerAngle, Is.EqualTo(40.0).Within(1e-6));
        Assert.That(loaded.Vehicle.MaxAngularVelocity, Is.EqualTo(30.0).Within(1e-6));
    }

    [Test]
    public void SaveAndLoad_ToolConfig_AllProperties()
    {
        var profile = CreateTestProfile("ToolTest");
        profile.Tool.Width = 12.0;
        profile.Tool.Overlap = 0.15;
        profile.Tool.Offset = -0.5;
        profile.Tool.HitchLength = -2.5;
        profile.Tool.IsToolTrailing = true;
        profile.Tool.IsToolTBT = false;
        profile.Tool.NumOfSections = 4;
        profile.NumSections = 4;
        profile.Tool.MinCoverage = 80;
        profile.Tool.IsHeadlandSectionControl = true;

        ProfileJsonService.Save(_tempDir, profile);
        var loaded = ProfileJsonService.Load(_tempDir, "ToolTest")!;

        Assert.That(loaded.Tool.Width, Is.EqualTo(12.0).Within(1e-6));
        Assert.That(loaded.Tool.Overlap, Is.EqualTo(0.15).Within(1e-6));
        Assert.That(loaded.Tool.Offset, Is.EqualTo(-0.5).Within(1e-6));
        Assert.That(loaded.Tool.HitchLength, Is.EqualTo(-2.5).Within(1e-6));
        Assert.That(loaded.Tool.IsToolTrailing, Is.True);
        Assert.That(loaded.Tool.IsToolTBT, Is.False);
        Assert.That(loaded.Tool.NumOfSections, Is.EqualTo(4));
        Assert.That(loaded.Tool.MinCoverage, Is.EqualTo(80));
        Assert.That(loaded.Tool.IsHeadlandSectionControl, Is.True);
    }

    [Test]
    public void SaveAndLoad_GuidanceConfig_RoundTrip()
    {
        var profile = CreateTestProfile("GuidanceTest");
        profile.Vehicle.GoalPointLookAheadHold = 5.0;
        profile.Vehicle.StanleyDistanceErrorGain = 1.2;
        profile.Vehicle.StanleyHeadingErrorGain = 0.9;
        profile.Vehicle.PurePursuitIntegralGain = 0.05;
        profile.IsPurePursuit = false;

        ProfileJsonService.Save(_tempDir, profile);
        var loaded = ProfileJsonService.Load(_tempDir, "GuidanceTest")!;

        Assert.That(loaded.Vehicle.GoalPointLookAheadHold, Is.EqualTo(5.0).Within(1e-6));
        Assert.That(loaded.Vehicle.StanleyDistanceErrorGain, Is.EqualTo(1.2).Within(1e-6));
        Assert.That(loaded.Vehicle.StanleyHeadingErrorGain, Is.EqualTo(0.9).Within(1e-6));
        Assert.That(loaded.Vehicle.PurePursuitIntegralGain, Is.EqualTo(0.05).Within(1e-6));
        Assert.That(loaded.IsPurePursuit, Is.False);
    }

    [Test]
    public void SaveAndLoad_SectionPositions_DynamicArray()
    {
        var profile = CreateTestProfile("SectionTest");
        profile.NumSections = 4;
        profile.SectionPositions = new double[17];
        profile.SectionPositions[0] = -6.0;
        profile.SectionPositions[1] = -3.0;
        profile.SectionPositions[2] = 0.0;
        profile.SectionPositions[3] = 3.0;
        profile.SectionPositions[4] = 6.0;

        ProfileJsonService.Save(_tempDir, profile);
        var loaded = ProfileJsonService.Load(_tempDir, "SectionTest")!;

        Assert.That(loaded.NumSections, Is.EqualTo(4));
        Assert.That(loaded.SectionPositions[0], Is.EqualTo(-6.0).Within(1e-6));
        Assert.That(loaded.SectionPositions[1], Is.EqualTo(-3.0).Within(1e-6));
        Assert.That(loaded.SectionPositions[2], Is.EqualTo(0.0).Within(1e-6));
        Assert.That(loaded.SectionPositions[3], Is.EqualTo(3.0).Within(1e-6));
        Assert.That(loaded.SectionPositions[4], Is.EqualTo(6.0).Within(1e-6));
    }

    [Test]
    public void SaveAndLoad_YouTurnConfig_RoundTrip()
    {
        var profile = CreateTestProfile("UTurnTest");
        profile.YouTurn.TurnRadius = 10.0;
        profile.YouTurn.ExtensionLength = 25.0;
        profile.YouTurn.DistanceFromBoundary = 3.0;
        profile.YouTurn.SkipWidth = 2;
        profile.YouTurn.Style = 1;
        profile.YouTurn.Smoothing = 20;

        ProfileJsonService.Save(_tempDir, profile);
        var loaded = ProfileJsonService.Load(_tempDir, "UTurnTest")!;

        Assert.That(loaded.YouTurn.TurnRadius, Is.EqualTo(10.0).Within(1e-6));
        Assert.That(loaded.YouTurn.ExtensionLength, Is.EqualTo(25.0).Within(1e-6));
        Assert.That(loaded.YouTurn.DistanceFromBoundary, Is.EqualTo(3.0).Within(1e-6));
        Assert.That(loaded.YouTurn.SkipWidth, Is.EqualTo(2));
        Assert.That(loaded.YouTurn.Style, Is.EqualTo(1));
        Assert.That(loaded.YouTurn.Smoothing, Is.EqualTo(20));
    }

    [Test]
    public void SaveAndLoad_GeneralSettings_RoundTrip()
    {
        var profile = CreateTestProfile("GeneralTest");
        profile.IsMetric = true;
        profile.IsSimulatorOn = false;
        profile.SimLatitude = 48.8566;
        profile.SimLongitude = 2.3522;

        ProfileJsonService.Save(_tempDir, profile);
        var loaded = ProfileJsonService.Load(_tempDir, "GeneralTest")!;

        Assert.That(loaded.IsMetric, Is.True);
        Assert.That(loaded.IsSimulatorOn, Is.False);
        Assert.That(loaded.SimLatitude, Is.EqualTo(48.8566).Within(1e-6));
        Assert.That(loaded.SimLongitude, Is.EqualTo(2.3522).Within(1e-6));
    }

    [Test]
    public void Load_MissingFile_ReturnsNull()
    {
        var loaded = ProfileJsonService.Load(_tempDir, "NonExistent");
        Assert.That(loaded, Is.Null);
    }

    [Test]
    public void Exists_ReturnsFalse_WhenNoFile()
    {
        Assert.That(ProfileJsonService.Exists(_tempDir, "Nope"), Is.False);
    }

    [Test]
    public void Exists_ReturnsTrue_AfterSave()
    {
        ProfileJsonService.Save(_tempDir, CreateTestProfile("ExistsTest"));
        Assert.That(ProfileJsonService.Exists(_tempDir, "ExistsTest"), Is.True);
    }

    [Test]
    public void JsonOutput_IsValidAndReadable()
    {
        ProfileJsonService.Save(_tempDir, CreateTestProfile("JsonCheck"));
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

#pragma warning restore CS0612

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static VehicleProfile CreateTestProfile(string name)
    {
        return new VehicleProfile
        {
            Name = name,
            Vehicle = new VehicleConfiguration(),
            Tool = new ToolConfiguration { Width = 6.0, NumOfSections = 1 },
            YouTurn = new YouTurnConfiguration(),
            SectionPositions = new double[17] { -3.0, 3.0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            NumSections = 1,
            IsMetric = false,
            IsPurePursuit = true,
            IsSimulatorOn = true,
        };
    }
}
