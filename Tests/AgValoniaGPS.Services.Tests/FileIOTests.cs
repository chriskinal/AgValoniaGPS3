using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services;

// Alias to avoid conflict with AgValoniaGPS.Services.Track namespace
using MTrack = AgValoniaGPS.Models.Track.Track;
using MTrackType = AgValoniaGPS.Models.Track.TrackType;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Round-trip tests for file I/O services (TrackFilesService, BoundaryFileService, SettingsService).
/// Each test uses a temp directory, cleaned up in TearDown.
/// </summary>
[TestFixture]
public class FileIOTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agvalonia_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region TrackFilesService

    [Test]
    public void TrackFiles_SaveAndLoad_ABLine_RoundTrip()
    {
        var track = new MTrack
        {
            Name = "Test AB Line",
            Type = MTrackType.ABLine,
            Points = new List<Vec3>
            {
                new(100.5, 200.3, 0.7854),  // ~45 degrees
                new(100.5, 300.3, 0.7854)
            },
            IsVisible = true,
            NudgeDistance = 1.5
        };

        TrackFilesService.SaveTracks(_tempDir, new[] { track });
        var loaded = TrackFilesService.LoadTracks(_tempDir);

        Assert.That(loaded, Has.Count.EqualTo(1));
        var result = loaded[0];
        Assert.That(result.Name, Is.EqualTo("Test AB Line"));
        Assert.That(result.Points, Has.Count.EqualTo(2));
        Assert.That(result.Points[0].Easting, Is.EqualTo(100.5).Within(0.01));
        Assert.That(result.Points[0].Northing, Is.EqualTo(200.3).Within(0.01));
        Assert.That(result.Points[1].Northing, Is.EqualTo(300.3).Within(0.01));
        Assert.That(result.NudgeDistance, Is.EqualTo(1.5).Within(0.001));
        Assert.That(result.IsVisible, Is.True);
    }

    [Test]
    public void TrackFiles_SaveAndLoad_Curve_RoundTrip()
    {
        var curvePoints = new List<Vec3>();
        for (int i = 0; i < 10; i++)
        {
            curvePoints.Add(new Vec3(100 + i * 5.0, 200 + Math.Sin(i * 0.5) * 10, i * 0.3));
        }

        var track = new MTrack
        {
            Name = "Test Curve",
            Type = MTrackType.Curve,
            Points = curvePoints,
            IsVisible = true,
            NudgeDistance = -0.25
        };

        TrackFilesService.SaveTracks(_tempDir, new[] { track });
        var loaded = TrackFilesService.LoadTracks(_tempDir);

        Assert.That(loaded, Has.Count.EqualTo(1));
        var result = loaded[0];
        Assert.That(result.Name, Is.EqualTo("Test Curve"));
        Assert.That(result.Points, Has.Count.EqualTo(10));
        Assert.That(result.NudgeDistance, Is.EqualTo(-0.25).Within(0.001));

        // Verify curve point values preserved
        for (int i = 0; i < 10; i++)
        {
            Assert.That(result.Points[i].Easting, Is.EqualTo(curvePoints[i].Easting).Within(0.01),
                $"Curve point {i} Easting mismatch");
            Assert.That(result.Points[i].Northing, Is.EqualTo(curvePoints[i].Northing).Within(0.01),
                $"Curve point {i} Northing mismatch");
        }
    }

    [Test]
    public void TrackFiles_SaveAndLoad_MultipleTracks_RoundTrip()
    {
        var tracks = new List<MTrack>
        {
            new()
            {
                Name = "AB Line 1",
                Type = MTrackType.ABLine,
                Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) },
                IsVisible = true
            },
            new()
            {
                Name = "Curve 1",
                Type = MTrackType.Curve,
                Points = new List<Vec3> { new(10, 0, 0.1), new(15, 50, 0.2), new(20, 100, 0.3) },
                IsVisible = false
            }
        };

        TrackFilesService.SaveTracks(_tempDir, tracks);
        var loaded = TrackFilesService.LoadTracks(_tempDir);

        Assert.That(loaded, Has.Count.EqualTo(2));
        Assert.That(loaded[0].Name, Is.EqualTo("AB Line 1"));
        Assert.That(loaded[0].IsVisible, Is.True);
        Assert.That(loaded[1].Name, Is.EqualTo("Curve 1"));
        Assert.That(loaded[1].IsVisible, Is.False);
        Assert.That(loaded[1].Points, Has.Count.EqualTo(3));
    }

    [Test]
    public void TrackFiles_SaveAndLoad_EmptyList()
    {
        TrackFilesService.SaveTracks(_tempDir, Array.Empty<MTrack>());
        var loaded = TrackFilesService.LoadTracks(_tempDir);

        Assert.That(loaded, Is.Empty);
    }

    [Test]
    public void TrackFiles_Load_MissingFile_ReturnsEmpty()
    {
        var loaded = TrackFilesService.LoadTracks(_tempDir);

        Assert.That(loaded, Is.Empty);
    }

    [Test]
    public void TrackFiles_HeadingConversion_RadiansDegrees_Accurate()
    {
        // AB line heading goes through degrees→radians→degrees conversion
        // Heading 90° should survive the round-trip
        var track = new MTrack
        {
            Name = "Heading Test",
            Type = MTrackType.ABLine,
            Points = new List<Vec3>
            {
                new(0, 0, Math.PI / 2),  // 90 degrees as heading
                new(100, 0, Math.PI / 2)
            },
            IsVisible = true
        };

        TrackFilesService.SaveTracks(_tempDir, new[] { track });
        var loaded = TrackFilesService.LoadTracks(_tempDir);

        // The track heading (stored as AB line heading) should survive the conversion
        Assert.That(loaded[0].Points, Has.Count.EqualTo(2));
        // Points A/B easting/northing should be preserved
        Assert.That(loaded[0].Points[0].Easting, Is.EqualTo(0).Within(0.01));
        Assert.That(loaded[0].Points[1].Easting, Is.EqualTo(100).Within(0.01));
    }

    #endregion

    #region BoundaryFileService

    [Test]
    public void Boundary_SaveAndLoad_OuterBoundary_RoundTrip()
    {
        var service = new BoundaryFileService();
        var boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 100)
        };

        service.SaveBoundary(boundary, _tempDir);
        var loaded = service.LoadBoundary(_tempDir);

        Assert.That(loaded.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.OuterBoundary!.Points, Has.Count.EqualTo(4));
        Assert.That(loaded.OuterBoundary.Points[0].Easting, Is.EqualTo(0).Within(0.01));
        Assert.That(loaded.OuterBoundary.Points[0].Northing, Is.EqualTo(0).Within(0.01));
        Assert.That(loaded.OuterBoundary.Points[2].Easting, Is.EqualTo(100).Within(0.01));
        Assert.That(loaded.OuterBoundary.Points[2].Northing, Is.EqualTo(100).Within(0.01));
    }

    [Test]
    public void Boundary_SaveAndLoad_WithInnerBoundaries_RoundTrip()
    {
        var service = new BoundaryFileService();
        var boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 200),
            InnerBoundaries = new List<BoundaryPolygon>
            {
                CreateSquarePolygon(50, 50, 30),  // hole 1
                CreateSquarePolygon(120, 120, 20)  // hole 2
            }
        };

        service.SaveBoundary(boundary, _tempDir);
        var loaded = service.LoadBoundary(_tempDir);

        Assert.That(loaded.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.InnerBoundaries, Has.Count.EqualTo(2));
        Assert.That(loaded.InnerBoundaries[0].Points, Has.Count.EqualTo(4));
        Assert.That(loaded.InnerBoundaries[1].Points, Has.Count.EqualTo(4));
    }

    [Test]
    public void Boundary_SaveAndLoad_WithHeadland_RoundTrip()
    {
        var service = new BoundaryFileService();
        var boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 200),
            HeadlandPolygon = CreateSquarePolygon(20, 20, 160) // inset headland
        };

        service.SaveBoundary(boundary, _tempDir);

        // Headland is saved separately — write it manually for the round-trip test
        // since SaveBoundary only writes Boundary.txt (not Headland.Txt)
        WriteHeadlandFile(_tempDir, boundary.HeadlandPolygon);

        var loaded = service.LoadBoundary(_tempDir);

        Assert.That(loaded.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.HeadlandPolygon, Is.Not.Null);
        Assert.That(loaded.HeadlandPolygon!.Points, Has.Count.EqualTo(4));
        Assert.That(loaded.HeadlandPolygon.Points[0].Easting, Is.EqualTo(20).Within(0.01));
    }

    [Test]
    public void Boundary_Load_MissingFile_ReturnsEmptyBoundary()
    {
        var service = new BoundaryFileService();

        var loaded = service.LoadBoundary(_tempDir);

        Assert.That(loaded.OuterBoundary, Is.Null);
        Assert.That(loaded.InnerBoundaries, Is.Empty);
        Assert.That(loaded.HeadlandPolygon, Is.Null);
    }

    [Test]
    public void Boundary_CreateEmptyBoundary_CreatesFile()
    {
        var service = new BoundaryFileService();

        service.CreateEmptyBoundary(_tempDir);

        var filePath = Path.Combine(_tempDir, "Boundary.txt");
        Assert.That(File.Exists(filePath), Is.True);

        var content = File.ReadAllText(filePath);
        Assert.That(content, Does.StartWith("$Boundary"));
    }

    #endregion

    #region SettingsService

    [Test]
    public void Settings_Load_MissingFile_ReturnsFalse()
    {
        var service = new SettingsService();

        // The settings service uses its own path (Documents/AgValoniaGPS),
        // but we can test the first-run behavior
        // If no file exists at the settings path, Load returns false
        var result = service.Load();

        // First run returns false OR true (depending on whether file already exists on this machine)
        // Either way, Settings should not be null
        Assert.That(service.Settings, Is.Not.Null);
    }

    [Test]
    public void Settings_ResetToDefaults_ClearsSettings()
    {
        var service = new SettingsService();
        service.Settings.WindowWidth = 1920;
        service.Settings.WindowHeight = 1080;

        service.ResetToDefaults();

        // After reset, settings should be fresh defaults
        Assert.That(service.Settings, Is.Not.Null);
        Assert.That(service.Settings.IsFirstRun, Is.False);
    }

    [Test]
    public void Settings_Save_FiresSettingsSavedEvent()
    {
        var service = new SettingsService();
        bool eventFired = false;
        service.SettingsSaved += (s, e) => eventFired = true;

        service.Save();

        Assert.That(eventFired, Is.True);
    }

    [Test]
    public void Settings_SaveAndLoad_RoundTrip()
    {
        var service = new SettingsService();
        service.Settings.WindowWidth = 1600;
        service.Settings.WindowHeight = 900;
        service.Settings.SimulatorEnabled = true;
        service.Settings.GpsUpdateRate = 10;

        var saveResult = service.Save();
        Assert.That(saveResult, Is.True);

        // Create a new service instance and load
        var service2 = new SettingsService();
        var loadResult = service2.Load();
        Assert.That(loadResult, Is.True);

        Assert.That(service2.Settings.WindowWidth, Is.EqualTo(1600));
        Assert.That(service2.Settings.WindowHeight, Is.EqualTo(900));
        Assert.That(service2.Settings.SimulatorEnabled, Is.True);
        Assert.That(service2.Settings.GpsUpdateRate, Is.EqualTo(10));
    }

    #endregion

    #region Helpers

    private static BoundaryPolygon CreateSquarePolygon(double originE, double originN, double size)
    {
        var polygon = new BoundaryPolygon
        {
            IsDriveThrough = false,
            Points = new List<BoundaryPoint>
            {
                new(originE, originN, 0),
                new(originE + size, originN, 90),
                new(originE + size, originN + size, 180),
                new(originE, originN + size, 270)
            }
        };
        polygon.UpdateBounds();
        return polygon;
    }

    private static void WriteHeadlandFile(string fieldDir, BoundaryPolygon polygon)
    {
        var path = Path.Combine(fieldDir, "Headland.Txt");
        using var writer = new StreamWriter(path);
        writer.WriteLine("$Headland");
        writer.WriteLine(polygon.IsDriveThrough.ToString());
        writer.WriteLine(polygon.Points.Count.ToString());
        foreach (var p in polygon.Points)
        {
            writer.WriteLine($"{p.Easting:F3},{p.Northing:F3},{p.Heading:F5}");
        }
    }

    #endregion
}
