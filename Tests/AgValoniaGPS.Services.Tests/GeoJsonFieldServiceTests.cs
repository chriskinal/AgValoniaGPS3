using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.GeoJson;

using MTrack = AgValoniaGPS.Models.Track.Track;
using MTrackType = AgValoniaGPS.Models.Track.TrackType;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class GeoJsonFieldServiceTests
{
    private string _tempDir = null!;

    // Origin near Tuscaloosa, AL (matches default sim position)
    private const double OriginLat = 32.5904;
    private const double OriginLon = -87.1804;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agvalonia_geojson_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void SaveAndLoad_EmptyField_RoundTrip()
    {
        var field = CreateTestField();

        GeoJsonFieldService.Save(field, tracks: null);

        Assert.That(File.Exists(Path.Combine(_tempDir, "field.geojson")), Is.True);

        var (loaded, tracks) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loaded.Name, Is.EqualTo(field.Name));
        Assert.That(loaded.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-6));
        Assert.That(loaded.Origin.Longitude, Is.EqualTo(OriginLon).Within(1e-6));
        Assert.That(tracks, Is.Empty);
    }

    [Test]
    public void SaveAndLoad_WithBoundary_CoordinateAccuracy()
    {
        var field = CreateTestField();
        field.Boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 100)
        };

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loaded.Boundary, Is.Not.Null);
        Assert.That(loaded.Boundary!.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.Boundary.OuterBoundary!.Points, Has.Count.EqualTo(4));

        // Verify coordinate round-trip accuracy within 1mm (local -> WGS84 -> local)
        var originalPoints = field.Boundary.OuterBoundary!.Points;
        var loadedPoints = loaded.Boundary.OuterBoundary.Points;
        for (int i = 0; i < originalPoints.Count; i++)
        {
            Assert.That(loadedPoints[i].Easting, Is.EqualTo(originalPoints[i].Easting).Within(0.001),
                $"Point {i} Easting accuracy");
            Assert.That(loadedPoints[i].Northing, Is.EqualTo(originalPoints[i].Northing).Within(0.001),
                $"Point {i} Northing accuracy");
        }
    }

    [Test]
    public void SaveAndLoad_WithInnerBoundaries_RoundTrip()
    {
        var field = CreateTestField();
        field.Boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 200),
            InnerBoundaries = new List<BoundaryPolygon>
            {
                CreateSquarePolygon(50, 50, 30),
                CreateSquarePolygon(120, 120, 20),
            }
        };

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loaded.Boundary!.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.Boundary.InnerBoundaries, Has.Count.EqualTo(2));
    }

    [Test]
    public void SaveAndLoad_WithHeadland_RoundTrip()
    {
        var field = CreateTestField();
        field.Boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 200),
            HeadlandPolygon = CreateSquarePolygon(20, 20, 160),
        };

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loaded.Boundary!.HeadlandPolygon, Is.Not.Null);
        Assert.That(loaded.Boundary.HeadlandPolygon!.Points, Has.Count.EqualTo(4));
        Assert.That(loaded.Boundary.HeadlandPolygon.Points[0].Easting,
            Is.EqualTo(20).Within(0.001));
    }

    [Test]
    public void SaveAndLoad_WithTracks_RoundTrip()
    {
        var field = CreateTestField();
        var tracks = new List<MTrack>
        {
            new()
            {
                Name = "AB Line 1",
                Type = MTrackType.ABLine,
                Points = new List<Vec3> { new(0, 0, 0), new(0, 100, 0) },
                IsVisible = true,
                NudgeDistance = 1.5,
            },
            new()
            {
                Name = "Curve 1",
                Type = MTrackType.Curve,
                Points = new List<Vec3> { new(10, 0, 0.1), new(15, 50, 0.2), new(20, 100, 0.3) },
                IsVisible = false,
                IsClosed = false,
            },
        };

        GeoJsonFieldService.Save(field, tracks);
        var (_, loadedTracks) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loadedTracks, Has.Count.EqualTo(2));

        Assert.That(loadedTracks[0].Name, Is.EqualTo("AB Line 1"));
        Assert.That(loadedTracks[0].Type, Is.EqualTo(MTrackType.ABLine));
        Assert.That(loadedTracks[0].Points, Has.Count.EqualTo(2));
        Assert.That(loadedTracks[0].NudgeDistance, Is.EqualTo(1.5).Within(0.001));
        Assert.That(loadedTracks[0].IsVisible, Is.True);

        Assert.That(loadedTracks[1].Name, Is.EqualTo("Curve 1"));
        Assert.That(loadedTracks[1].Type, Is.EqualTo(MTrackType.Curve));
        Assert.That(loadedTracks[1].Points, Has.Count.EqualTo(3));
        Assert.That(loadedTracks[1].IsVisible, Is.False);
    }

    [Test]
    public void SaveAndLoad_TrackHeadings_Preserved()
    {
        var field = CreateTestField();
        var tracks = new List<MTrack>
        {
            new()
            {
                Name = "Heading Test",
                Type = MTrackType.Curve,
                Points = new List<Vec3>
                {
                    new(0, 0, 0.7854),    // ~45 deg
                    new(50, 50, 1.5708),   // ~90 deg
                    new(100, 50, 3.1416),  // ~180 deg
                },
            }
        };

        GeoJsonFieldService.Save(field, tracks);
        var (_, loadedTracks) = GeoJsonFieldService.Load(_tempDir);

        for (int i = 0; i < 3; i++)
        {
            Assert.That(loadedTracks[0].Points[i].Heading,
                Is.EqualTo(tracks[0].Points[i].Heading).Within(0.0001),
                $"Point {i} heading");
        }
    }

    [Test]
    public void SaveAndLoad_WithBackgroundImage_RoundTrip()
    {
        var field = CreateTestField();
        field.BackgroundImage = new BackgroundImage
        {
            MinEasting = -200,
            MaxEasting = 200,
            MinNorthing = -150,
            MaxNorthing = 150,
            IsEnabled = true,
        };

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loaded.BackgroundImage, Is.Not.Null);
        Assert.That(loaded.BackgroundImage!.MinEasting, Is.EqualTo(-200).Within(0.01));
        Assert.That(loaded.BackgroundImage.MaxEasting, Is.EqualTo(200).Within(0.01));
        Assert.That(loaded.BackgroundImage.MinNorthing, Is.EqualTo(-150).Within(0.01));
        Assert.That(loaded.BackgroundImage.MaxNorthing, Is.EqualTo(150).Within(0.01));
    }

    [Test]
    public void SaveAndLoad_FieldMetadata_Preserved()
    {
        var field = CreateTestField();
        field.Convergence = 1.23;
        field.CreatedDate = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Local);
        field.LastModifiedDate = new DateTime(2025, 6, 16, 14, 0, 0, DateTimeKind.Local);

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loaded.Convergence, Is.EqualTo(1.23).Within(1e-6));
        Assert.That(loaded.CreatedDate.Year, Is.EqualTo(2025));
        Assert.That(loaded.CreatedDate.Month, Is.EqualTo(6));
        Assert.That(loaded.CreatedDate.Day, Is.EqualTo(15));
    }

    [Test]
    public void Exists_ReturnsFalse_WhenNoFile()
    {
        Assert.That(GeoJsonFieldService.Exists(_tempDir), Is.False);
    }

    [Test]
    public void Exists_ReturnsTrue_AfterSave()
    {
        var field = CreateTestField();
        GeoJsonFieldService.Save(field, tracks: null);

        Assert.That(GeoJsonFieldService.Exists(_tempDir), Is.True);
    }

    [Test]
    public void Load_ThrowsOnMissingFile()
    {
        Assert.Throws<FileNotFoundException>(() => GeoJsonFieldService.Load(_tempDir));
    }

    [Test]
    public void GeoJsonOutput_IsValidJson()
    {
        var field = CreateTestField();
        field.Boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 100)
        };

        GeoJsonFieldService.Save(field, tracks: null);

        var json = File.ReadAllText(Path.Combine(_tempDir, "field.geojson"));
        // Should not throw
        var doc = System.Text.Json.JsonDocument.Parse(json);

        var root = doc.RootElement;
        Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("FeatureCollection"));
        Assert.That(root.GetProperty("features").GetArrayLength(), Is.GreaterThan(0));
    }

    // ---------------------------------------------------------------
    // FieldService integration (auto-detect path)
    // ---------------------------------------------------------------

    [Test]
    public void FieldService_SaveCreatesGeoJson_LoadPrefersIt()
    {
        // Save via FieldService (writes legacy + GeoJSON)
        var fieldService = new FieldService();
        var field = CreateTestField();
        field.Boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 100)
        };

        // Create Field.txt so legacy path is valid
        fieldService.SaveField(field);

        // Verify both formats exist
        Assert.That(File.Exists(Path.Combine(_tempDir, "Field.txt")), Is.True, "Legacy Field.txt");
        Assert.That(File.Exists(Path.Combine(_tempDir, "field.geojson")), Is.True, "GeoJSON file");

        // Load via FieldService -- should prefer GeoJSON
        var loaded = fieldService.LoadField(_tempDir);
        Assert.That(loaded.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-6));
        Assert.That(loaded.Boundary, Is.Not.Null);
        Assert.That(loaded.Boundary!.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.Boundary.OuterBoundary!.Points, Has.Count.EqualTo(4));
    }

    [Test]
    public void FieldService_LoadsFallbackToLegacy_WhenNoGeoJson()
    {
        // Create legacy field only (no GeoJSON)
        var fieldService = new FieldService();
        var field = CreateTestField();
        fieldService.SaveField(field);

        // Delete the GeoJSON file to force legacy path
        var geoJsonPath = Path.Combine(_tempDir, "field.geojson");
        if (File.Exists(geoJsonPath))
            File.Delete(geoJsonPath);

        var loaded = fieldService.LoadField(_tempDir);
        Assert.That(loaded.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-6));
    }

    [Test]
    public void FieldService_FieldExists_DetectsGeoJson()
    {
        var fieldService = new FieldService();

        Assert.That(fieldService.FieldExists(_tempDir), Is.False);

        // Create only a GeoJSON file (no Field.txt)
        var field = CreateTestField();
        GeoJsonFieldService.Save(field, tracks: null);

        Assert.That(fieldService.FieldExists(_tempDir), Is.True);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private Models.Field CreateTestField()
    {
        return new Models.Field
        {
            Name = Path.GetFileName(_tempDir),
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
            CreatedDate = DateTime.Now,
            LastModifiedDate = DateTime.Now,
        };
    }

    private static BoundaryPolygon CreateSquarePolygon(double originE, double originN, double size)
    {
        var polygon = new BoundaryPolygon
        {
            IsDriveThrough = false,
            Points = new List<BoundaryPoint>
            {
                new(originE, originN, 0),
                new(originE + size, originN, Math.PI / 2),
                new(originE + size, originN + size, Math.PI),
                new(originE, originN + size, 3 * Math.PI / 2),
            }
        };
        polygon.UpdateBounds();
        return polygon;
    }
}
