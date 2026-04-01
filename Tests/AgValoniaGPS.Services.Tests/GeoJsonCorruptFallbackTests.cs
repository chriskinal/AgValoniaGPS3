using System.Globalization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.GeoJson;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests that corrupt GeoJSON files fall back to legacy format loading (#124).
/// Simulates power-loss scenarios where field.geojson is truncated or empty.
/// </summary>
[TestFixture]
public class GeoJsonCorruptFallbackTests
{
    private string _testDir = null!;
    private string _fieldDir = null!;
    private FieldService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"AgValoniaGPS_Test_{Guid.NewGuid():N}");
        _fieldDir = Path.Combine(_testDir, "TestField");
        Directory.CreateDirectory(_fieldDir);
        _service = new FieldService();
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void WriteLegacyField(double originLat = 47.0, double originLon = -93.0)
    {
        // Write minimal Field.txt (legacy format)
        var fieldTxt = Path.Combine(_fieldDir, "Field.txt");
        File.WriteAllLines(fieldTxt, new[]
        {
            "$FieldDir",
            "TestField",
            "$Offsets",
            originLat.ToString(CultureInfo.InvariantCulture),
            originLon.ToString(CultureInfo.InvariantCulture),
            "Convergence",
            "0",
            "StartFix",
            $"{originLat.ToString(CultureInfo.InvariantCulture)},{originLon.ToString(CultureInfo.InvariantCulture)}"
        });
    }

    private void WriteValidGeoJson(double originLat = 48.0, double originLon = -94.0)
    {
        // Create a valid field and save as GeoJSON
        var field = new Field
        {
            Name = "TestField",
            DirectoryPath = _fieldDir,
            Origin = new Position { Latitude = originLat, Longitude = originLon },
        };
        GeoJsonFieldService.Save(field, new List<AgValoniaGPS.Models.Track.Track>());
    }

    private void WriteCorruptGeoJson(string content = "{\"type\":\"FeatureColl")
    {
        File.WriteAllText(Path.Combine(_fieldDir, "field.geojson"), content);
    }

    [Test]
    public void LoadField_ValidGeoJson_LoadsFromGeoJson()
    {
        WriteValidGeoJson(48.0, -94.0);
        WriteLegacyField(47.0, -93.0);

        var field = _service.LoadField(_fieldDir);

        // Should load from GeoJSON (lat 48), not legacy (lat 47)
        Assert.That(field.Origin.Latitude, Is.EqualTo(48.0).Within(0.001));
    }

    [Test]
    public void LoadField_CorruptGeoJson_FallsBackToLegacy()
    {
        WriteCorruptGeoJson("{truncated");
        WriteLegacyField(47.0, -93.0);

        var field = _service.LoadField(_fieldDir);

        Assert.That(field.Origin.Latitude, Is.EqualTo(47.0).Within(0.001));
    }

    [Test]
    public void LoadField_EmptyGeoJson_FallsBackToLegacy()
    {
        WriteCorruptGeoJson("");
        WriteLegacyField(47.0, -93.0);

        var field = _service.LoadField(_fieldDir);

        Assert.That(field.Origin.Latitude, Is.EqualTo(47.0).Within(0.001));
    }

    [Test]
    public void LoadField_CorruptGeoJson_RenamesCorruptFile()
    {
        WriteCorruptGeoJson("{bad json}");
        WriteLegacyField();

        _service.LoadField(_fieldDir);

        // Original should be renamed
        Assert.That(File.Exists(Path.Combine(_fieldDir, "field.geojson")), Is.False,
            "Corrupt file should be renamed");

        // Backup should exist with .corrupt. prefix
        var backups = Directory.GetFiles(_fieldDir, "field.geojson.corrupt.*");
        Assert.That(backups, Has.Length.EqualTo(1),
            "Should create exactly one backup of corrupt file");
    }

    [Test]
    public void LoadField_CorruptGeoJson_NoLegacy_ThrowsGracefully()
    {
        WriteCorruptGeoJson("{bad}");
        // No legacy files written

        // Should throw from legacy loader (no Field.txt), not from JSON parser
        Assert.Throws<FileNotFoundException>(() => _service.LoadField(_fieldDir));
    }

    [Test]
    public void LoadField_ValidGeoJson_DoesNotRenameFile()
    {
        WriteValidGeoJson();

        _service.LoadField(_fieldDir);

        Assert.That(File.Exists(Path.Combine(_fieldDir, "field.geojson")), Is.True,
            "Valid GeoJSON should not be renamed");
        var backups = Directory.GetFiles(_fieldDir, "field.geojson.corrupt.*");
        Assert.That(backups, Has.Length.EqualTo(0));
    }

    [Test]
    public void LoadField_NullJsonContent_FallsBackToLegacy()
    {
        // Valid JSON but not a valid GeoJSON FeatureCollection
        WriteCorruptGeoJson("null");
        WriteLegacyField(47.0, -93.0);

        var field = _service.LoadField(_fieldDir);

        Assert.That(field.Origin.Latitude, Is.EqualTo(47.0).Within(0.001));
    }

    [Test]
    public void LoadField_MissingMetadataFeature_FallsBackToLegacy()
    {
        // Valid JSON, valid FeatureCollection, but no metadata feature
        WriteCorruptGeoJson("""{"type":"FeatureCollection","features":[]}""");
        WriteLegacyField(47.0, -93.0);

        var field = _service.LoadField(_fieldDir);

        Assert.That(field.Origin.Latitude, Is.EqualTo(47.0).Within(0.001));
    }
}
