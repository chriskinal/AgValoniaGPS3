using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.GeoJson;

using MTrack = AgValoniaGPS.Models.Track.Track;
using MTrackType = AgValoniaGPS.Models.Track.TrackType;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// End-to-end verification of field save/load through FieldService.
/// Simulates: create field -> add boundary -> add tracks -> save -> close -> reopen.
/// </summary>
[TestFixture]
public class FieldSaveLoadVerificationTests
{
    private string _tempDir = null!;
    private const double OriginLat = 32.5904;
    private const double OriginLon = -87.1804;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agvalonia_verify_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void FullWorkflow_CreateField_SaveWithBoundary_Reopen()
    {
        var fieldService = new FieldService();

        // 1. Create field
        var field = fieldService.CreateField(_tempDir, "TestField", new Position
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
        });

        Assert.That(field.DirectoryPath, Does.Contain("TestField"));
        var fieldDir = field.DirectoryPath;

        // 2. Add outer boundary (100m square)
        field.Boundary = new Boundary
        {
            OuterBoundary = CreateSquarePolygon(0, 0, 100),
            InnerBoundaries = new List<BoundaryPolygon>
            {
                CreateSquarePolygon(30, 30, 20), // pond
            },
            HeadlandPolygon = CreateSquarePolygon(10, 10, 80), // headland inset
        };

        // 3. Save (writes legacy + GeoJSON)
        fieldService.SaveField(field);

        // 4. Verify files exist
        Assert.That(File.Exists(Path.Combine(fieldDir, "Field.txt")), Is.True, "Legacy Field.txt");
        Assert.That(File.Exists(Path.Combine(fieldDir, "Boundary.txt")), Is.True, "Legacy Boundary.txt");
        Assert.That(File.Exists(Path.Combine(fieldDir, "field.geojson")), Is.True, "GeoJSON");

        // 5. Reopen via FieldService (should prefer GeoJSON)
        var loaded = fieldService.LoadField(fieldDir);

        Assert.That(loaded.Name, Is.EqualTo("TestField"));
        Assert.That(loaded.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-6));
        Assert.That(loaded.Origin.Longitude, Is.EqualTo(OriginLon).Within(1e-6));

        // 6. Verify boundary round-tripped
        Assert.That(loaded.Boundary, Is.Not.Null);
        Assert.That(loaded.Boundary!.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.Boundary.OuterBoundary!.Points, Has.Count.EqualTo(4));

        // Corner accuracy within 1mm
        Assert.That(loaded.Boundary.OuterBoundary.Points[0].Easting, Is.EqualTo(0).Within(0.001));
        Assert.That(loaded.Boundary.OuterBoundary.Points[0].Northing, Is.EqualTo(0).Within(0.001));
        Assert.That(loaded.Boundary.OuterBoundary.Points[2].Easting, Is.EqualTo(100).Within(0.001));
        Assert.That(loaded.Boundary.OuterBoundary.Points[2].Northing, Is.EqualTo(100).Within(0.001));

        // Inner boundary (pond)
        Assert.That(loaded.Boundary.InnerBoundaries, Has.Count.EqualTo(1));
        Assert.That(loaded.Boundary.InnerBoundaries[0].Points, Has.Count.EqualTo(4));

        // Headland
        Assert.That(loaded.Boundary.HeadlandPolygon, Is.Not.Null);
        Assert.That(loaded.Boundary.HeadlandPolygon!.Points, Has.Count.EqualTo(4));
        Assert.That(loaded.Boundary.HeadlandPolygon.Points[0].Easting, Is.EqualTo(10).Within(0.001));

        // 7. Verify boundary is functional (point-in-polygon works after load)
        Assert.That(loaded.Boundary.OuterBoundary.IsValid, Is.True);
        Assert.That(loaded.Boundary.OuterBoundary.IsPointInside(50, 50), Is.True, "Center should be inside");
        Assert.That(loaded.Boundary.OuterBoundary.IsPointInside(200, 200), Is.False, "Outside should be outside");
    }

    [Test]
    public void FullWorkflow_GeoJsonWithTracks_RoundTrip()
    {
        // Save field + tracks via GeoJsonFieldService directly
        var field = new Models.Field
        {
            Name = "TrackField",
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
            Boundary = new Boundary
            {
                OuterBoundary = CreateSquarePolygon(0, 0, 500),
            },
        };

        var tracks = new List<MTrack>
        {
            MTrack.FromABLine("North-South",
                new Vec3(250, 0, 0),
                new Vec3(250, 500, 0)),
            MTrack.FromCurve("S-Curve", new List<Vec3>
            {
                new(100, 0, 0),
                new(120, 100, 0.3),
                new(100, 200, 0),
                new(80, 300, -0.3),
                new(100, 400, 0),
            }),
        };
        tracks[0].NudgeDistance = 2.5;
        tracks[0].IsVisible = true;
        tracks[1].IsVisible = false;

        GeoJsonFieldService.Save(field, tracks);

        // Reload
        var (loaded, loadedTracks) = GeoJsonFieldService.Load(_tempDir);

        // Field
        Assert.That(loaded.Name, Is.EqualTo("TrackField"));
        Assert.That(loaded.Boundary!.OuterBoundary!.Points, Has.Count.EqualTo(4));

        // Tracks
        Assert.That(loadedTracks, Has.Count.EqualTo(2));

        // AB Line
        var ab = loadedTracks[0];
        Assert.That(ab.Name, Is.EqualTo("North-South"));
        Assert.That(ab.Type, Is.EqualTo(MTrackType.ABLine));
        Assert.That(ab.Points, Has.Count.EqualTo(2));
        Assert.That(ab.Points[0].Easting, Is.EqualTo(250).Within(0.001));
        Assert.That(ab.Points[0].Northing, Is.EqualTo(0).Within(0.001));
        Assert.That(ab.Points[1].Northing, Is.EqualTo(500).Within(0.001));
        Assert.That(ab.NudgeDistance, Is.EqualTo(2.5).Within(0.001));
        Assert.That(ab.IsVisible, Is.True);

        // Curve
        var curve = loadedTracks[1];
        Assert.That(curve.Name, Is.EqualTo("S-Curve"));
        Assert.That(curve.Type, Is.EqualTo(MTrackType.Curve));
        Assert.That(curve.Points, Has.Count.EqualTo(5));
        Assert.That(curve.IsVisible, Is.False);

        // Curve point accuracy
        for (int i = 0; i < 5; i++)
        {
            Assert.That(curve.Points[i].Easting, Is.EqualTo(tracks[1].Points[i].Easting).Within(0.001),
                $"Curve point {i} easting");
            Assert.That(curve.Points[i].Northing, Is.EqualTo(tracks[1].Points[i].Northing).Within(0.001),
                $"Curve point {i} northing");
        }
    }

    [Test]
    public void LegacyField_SaveReopen_MigratesGeoJson()
    {
        // Simulate a legacy field (Field.txt + Boundary.txt, no GeoJSON)
        var fieldPlane = new FieldPlaneFileService();
        var boundaryService = new BoundaryFileService();

        var field = new Models.Field
        {
            Name = "LegacyField",
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
        };
        fieldPlane.SaveField(field, _tempDir);

        var boundary = new Boundary { OuterBoundary = CreateSquarePolygon(0, 0, 200) };
        boundaryService.SaveBoundary(boundary, _tempDir);

        Assert.That(File.Exists(Path.Combine(_tempDir, "field.geojson")), Is.False, "No GeoJSON yet");

        // Open via FieldService (legacy path since no GeoJSON)
        var fieldService = new FieldService();
        var loaded = fieldService.LoadField(_tempDir);
        Assert.That(loaded.Boundary!.OuterBoundary!.Points, Has.Count.EqualTo(4));

        // Save again -> should now create GeoJSON alongside legacy
        fieldService.SaveField(loaded);
        Assert.That(File.Exists(Path.Combine(_tempDir, "field.geojson")), Is.True, "GeoJSON created on save");

        // Third open -> should now use GeoJSON path
        var reopened = fieldService.LoadField(_tempDir);
        Assert.That(reopened.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-6));
        Assert.That(reopened.Boundary!.OuterBoundary!.Points, Has.Count.EqualTo(4));
        Assert.That(reopened.Boundary.OuterBoundary.Points[2].Easting, Is.EqualTo(200).Within(0.001));
    }

    [Test]
    public void HeadingValues_SurviveGeoJsonRoundTrip()
    {
        var field = new Models.Field
        {
            Name = "HeadingTest",
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
            Boundary = new Boundary
            {
                OuterBoundary = CreateSquarePolygon(0, 0, 100),
            },
        };

        // Boundary points have specific headings
        var original = field.Boundary.OuterBoundary!.Points;
        Assert.That(original[0].Heading, Is.EqualTo(0).Within(1e-10));
        Assert.That(original[1].Heading, Is.EqualTo(Math.PI / 2).Within(1e-10));
        Assert.That(original[2].Heading, Is.EqualTo(Math.PI).Within(1e-10));

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        var loadedPts = loaded.Boundary!.OuterBoundary!.Points;
        for (int i = 0; i < 4; i++)
        {
            Assert.That(loadedPts[i].Heading, Is.EqualTo(original[i].Heading).Within(0.0001),
                $"Point {i} heading");
        }
    }

    [Test]
    public void BoundaryPolygon_IsPointInside_WorksAfterGeoJsonLoad()
    {
        var field = new Models.Field
        {
            Name = "PointInPoly",
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
            Boundary = new Boundary
            {
                OuterBoundary = CreateSquarePolygon(0, 0, 100),
                InnerBoundaries = new List<BoundaryPolygon>
                {
                    CreateSquarePolygon(40, 40, 20), // hole in center
                },
            },
        };

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        var bnd = loaded.Boundary!;

        // Outer boundary functional
        Assert.That(bnd.OuterBoundary!.IsPointInside(50, 50), Is.True, "Center is inside outer");
        Assert.That(bnd.OuterBoundary.IsPointInside(150, 150), Is.False, "Far away is outside");

        // Inner boundary (hole) functional
        Assert.That(bnd.InnerBoundaries[0].IsPointInside(50, 50), Is.True, "Center is inside hole");

        // Composite check: point in hole should be outside usable area
        Assert.That(bnd.IsPointInside(50, 50), Is.False, "Center of hole is not in usable area");
        Assert.That(bnd.IsPointInside(10, 10), Is.True, "Corner is in usable area");
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
