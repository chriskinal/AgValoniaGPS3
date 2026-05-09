using System.Globalization;
using System.Threading;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Fields;
using AgValoniaGPS.Services.GeoJson;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Locale-stability fences for field-file I/O.
///
/// Background: a recent regression created new fields whose lat/lon were
/// formatted with the current culture, so on fi-FI / de-DE locales the
/// decimal point became a comma and Field.txt could not be re-parsed by
/// any other (or even the same) machine. The production code now uses
/// CultureInfo.InvariantCulture for both write and parse, but nothing
/// proved that — so a future copy-paste could regress it silently.
///
/// These tests swap Thread.CurrentThread.CurrentCulture and
/// CurrentUICulture before each I/O round-trip and assert the values
/// come back exactly. en-US is the baseline; fi-FI is the locale that
/// actually broke; de-DE is added to cover another comma-decimal locale.
/// The original culture is always restored in the [TearDown].
///
/// All assertion-side string formatting in this fixture explicitly uses
/// CultureInfo.InvariantCulture so the test can never accidentally
/// reproduce the bug it is fencing.
/// </summary>
[TestFixture]
public class FieldIoLocaleMatrixTests
{
    private string _tempDir = null!;
    private CultureInfo _originalCulture = null!;
    private CultureInfo _originalUiCulture = null!;

    private const double OriginLat = 60.16985212;   // Helsinki, intentionally many decimals
    private const double OriginLon = 24.93820100;
    private const double Convergence = 0.123456;
    private const double OffsetX = -1234.5678;
    private const double OffsetY = 4321.0987;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agvalonia_locale_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalCulture = Thread.CurrentThread.CurrentCulture;
        _originalUiCulture = Thread.CurrentThread.CurrentUICulture;
    }

    [TearDown]
    public void TearDown()
    {
        // Restore culture FIRST so test runner output stays clean even if
        // directory cleanup throws.
        Thread.CurrentThread.CurrentCulture = _originalCulture;
        Thread.CurrentThread.CurrentUICulture = _originalUiCulture;

        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    private static void SetCulture(string name)
    {
        var ci = CultureInfo.GetCultureInfo(name);
        Thread.CurrentThread.CurrentCulture = ci;
        Thread.CurrentThread.CurrentUICulture = ci;
    }

    [TestCase("en-US")]
    [TestCase("fi-FI")]   // The locale that triggered the original regression.
    [TestCase("de-DE")]
    public void FieldPlaneFile_RoundTrip_PreservesOriginAndOffsetsAcrossCultures(string cultureName)
    {
        SetCulture(cultureName);

        var service = new FieldPlaneFileService();
        var field = new Field
        {
            Name = "LocaleField",
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
            Convergence = Convergence,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            CreatedDate = new DateTime(2026, 5, 9, 12, 34, 56, DateTimeKind.Utc),
        };

        service.SaveField(field, _tempDir);

        // Sanity: the on-disk lat/lon line MUST contain a period decimal,
        // never a comma or other locale-dependent separator. This guards
        // the file format itself, not just the round-trip.
        var fieldTxt = File.ReadAllText(Path.Combine(_tempDir, "Field.txt"));
        Assert.That(fieldTxt, Does.Not.Contain("60,16985212"),
            "Field.txt was written with the current culture's decimal separator");
        Assert.That(fieldTxt, Does.Contain(OriginLat.ToString("F8", CultureInfo.InvariantCulture)),
            "Field.txt does not contain the expected invariant-formatted latitude");

        var loaded = service.LoadField(_tempDir);

        Assert.That(loaded.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-8),
            $"Latitude round-trip failed under culture {cultureName}");
        Assert.That(loaded.Origin.Longitude, Is.EqualTo(OriginLon).Within(1e-8),
            $"Longitude round-trip failed under culture {cultureName}");
        Assert.That(loaded.Convergence, Is.EqualTo(Convergence).Within(1e-9),
            $"Convergence round-trip failed under culture {cultureName}");
        Assert.That(loaded.OffsetX, Is.EqualTo(OffsetX).Within(1e-6),
            $"OffsetX round-trip failed under culture {cultureName}");
        Assert.That(loaded.OffsetY, Is.EqualTo(OffsetY).Within(1e-6),
            $"OffsetY round-trip failed under culture {cultureName}");
    }

    [TestCase("en-US")]
    [TestCase("fi-FI")]
    [TestCase("de-DE")]
    public void BoundaryFile_RoundTrip_PreservesPointCoordinatesAcrossCultures(string cultureName)
    {
        SetCulture(cultureName);

        var service = new BoundaryFileService();
        var boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                IsDriveThrough = false,
                Points = new List<BoundaryPoint>
                {
                    new(  0.000,   0.000,            0.0),
                    new(123.456,   0.000,  Math.PI / 2),
                    new(123.456, 789.012,  Math.PI),
                    new(  0.000, 789.012,  3 * Math.PI / 2),
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();

        service.SaveBoundary(boundary, _tempDir);

        var bndTxt = File.ReadAllText(Path.Combine(_tempDir, "Boundary.txt"));
        // The first point line is "0.000,0.000,0.00000". A culture-leak
        // would either turn the comma-separator into something else, or
        // write the decimal as a comma which collides with the field
        // separator and explodes the round-trip.
        Assert.That(bndTxt, Does.Contain("123.456"),
            $"Boundary.txt does not contain invariant-formatted easting under {cultureName}");
        Assert.That(bndTxt, Does.Not.Contain("123,456,"),
            $"Boundary.txt looks culture-leaked under {cultureName}");

        var loaded = service.LoadBoundary(_tempDir);

        Assert.That(loaded.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.OuterBoundary!.Points, Has.Count.EqualTo(4));
        Assert.That(loaded.OuterBoundary.Points[1].Easting, Is.EqualTo(123.456).Within(1e-3),
            $"Easting round-trip failed under {cultureName}");
        Assert.That(loaded.OuterBoundary.Points[2].Northing, Is.EqualTo(789.012).Within(1e-3),
            $"Northing round-trip failed under {cultureName}");
        Assert.That(loaded.OuterBoundary.Points[1].Heading, Is.EqualTo(Math.PI / 2).Within(1e-4),
            $"Heading round-trip failed under {cultureName}");
    }

    [TestCase("en-US")]
    [TestCase("fi-FI")]
    [TestCase("de-DE")]
    public void FieldJson_RoundTrip_PreservesOriginAcrossCultures(string cultureName)
    {
        SetCulture(cultureName);

        var field = new Field
        {
            Name = "JsonLocale",
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
            Convergence = Convergence,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
        };

        FieldJsonService.Save(field, _tempDir);

        // System.Text.Json should emit invariant numbers, but explicitly
        // assert the on-disk form so a serializer-options regression that
        // turns numbers into culture-formatted strings would fail here.
        var json = File.ReadAllText(FieldJsonService.PathFor(_tempDir));
        Assert.That(json, Does.Contain(OriginLat.ToString("R", CultureInfo.InvariantCulture)).Or
                              .Contain(OriginLat.ToString("G17", CultureInfo.InvariantCulture)).Or
                              .Contain("60.16985212"),
            $"field.json does not contain invariant origin under {cultureName}");

        var loaded = FieldJsonService.Load(_tempDir);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-9),
            $"FieldJson latitude round-trip failed under {cultureName}");
        Assert.That(loaded.Origin.Longitude, Is.EqualTo(OriginLon).Within(1e-9),
            $"FieldJson longitude round-trip failed under {cultureName}");
        Assert.That(loaded.Convergence, Is.EqualTo(Convergence).Within(1e-9));
        Assert.That(loaded.OffsetX, Is.EqualTo(OffsetX).Within(1e-9));
    }

    [TestCase("en-US")]
    [TestCase("fi-FI")]
    [TestCase("de-DE")]
    public void GeoJsonField_RoundTrip_PreservesGeometryAcrossCultures(string cultureName)
    {
        SetCulture(cultureName);

        var field = new Field
        {
            Name = "GeoLocale",
            DirectoryPath = _tempDir,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
            Boundary = new Boundary
            {
                OuterBoundary = new BoundaryPolygon
                {
                    Points = new List<BoundaryPoint>
                    {
                        new(  0.000,   0.000, 0),
                        new(100.500,   0.000, Math.PI / 2),
                        new(100.500, 200.250, Math.PI),
                        new(  0.000, 200.250, 3 * Math.PI / 2),
                    }
                }
            }
        };
        field.Boundary.OuterBoundary!.UpdateBounds();

        GeoJsonFieldService.Save(field, tracks: null);
        var (loaded, _) = GeoJsonFieldService.Load(_tempDir);

        Assert.That(loaded.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-9),
            $"GeoJSON origin lat round-trip failed under {cultureName}");
        Assert.That(loaded.Origin.Longitude, Is.EqualTo(OriginLon).Within(1e-9),
            $"GeoJSON origin lon round-trip failed under {cultureName}");
        Assert.That(loaded.Boundary?.OuterBoundary?.Points, Has.Count.EqualTo(4),
            $"GeoJSON boundary lost points under {cultureName}");
        // Re-projection through GeoConversion is sub-millimetre but not exact,
        // so allow ~1 cm to absorb round-trip projection error.
        Assert.That(loaded.Boundary!.OuterBoundary!.Points[1].Easting, Is.EqualTo(100.5).Within(0.01),
            $"GeoJSON easting drifted under {cultureName}");
        Assert.That(loaded.Boundary.OuterBoundary.Points[2].Northing, Is.EqualTo(200.25).Within(0.01),
            $"GeoJSON northing drifted under {cultureName}");
    }
}
