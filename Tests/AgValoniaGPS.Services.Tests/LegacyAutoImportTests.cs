using System.Globalization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.GeoJson;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests that legacy AgOpenGPS field files are automatically converted
/// to GeoJSON on first load (#66).
/// </summary>
[TestFixture]
public class LegacyAutoImportTests
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

    private void WriteLegacyFieldTxt(double lat = 47.1234567, double lon = -93.9876543, double convergence = 0.5)
    {
        File.WriteAllLines(Path.Combine(_fieldDir, "Field.txt"), new[]
        {
            "2025-06-15 10:30:00",           // Timestamp
            "$FieldDir",                      // Header
            "TestField",                      // Field name
            "$Offsets",                       // Header
            "0,0",                            // Offsets X,Y
            "Convergence",                    // Header
            convergence.ToString(CultureInfo.InvariantCulture),
            "StartFix",                       // Header
            $"{lat.ToString(CultureInfo.InvariantCulture)},{lon.ToString(CultureInfo.InvariantCulture)}"
        });
    }

    private void WriteLegacyBoundary(double centerE = 100, double centerN = 200, double size = 50)
    {
        // Write a simple square boundary in legacy format
        var lines = new List<string>
        {
            "$Boundary",
            "False",  // isDriveThru
            "4",      // point count
            $"{centerE - size},{centerN - size},0",
            $"{centerE + size},{centerN - size},0",
            $"{centerE + size},{centerN + size},0",
            $"{centerE - size},{centerN + size},0"
        };
        File.WriteAllLines(Path.Combine(_fieldDir, "Boundary.txt"), lines);
    }

    [Test]
    public void LoadField_LegacyOnly_CreatesGeoJson()
    {
        WriteLegacyFieldTxt();

        _service.LoadField(_fieldDir);

        Assert.That(File.Exists(Path.Combine(_fieldDir, "field.geojson")), Is.True,
            "GeoJSON file should be created after loading legacy field");
    }

    [Test]
    public void LoadField_LegacyOnly_PreservesOrigin()
    {
        WriteLegacyFieldTxt(lat: 47.5, lon: -93.2);

        var field = _service.LoadField(_fieldDir);

        Assert.That(field.Origin.Latitude, Is.EqualTo(47.5).Within(0.0001));
        Assert.That(field.Origin.Longitude, Is.EqualTo(-93.2).Within(0.0001));
    }

    [Test]
    public void LoadField_LegacyOnly_PreservesConvergence()
    {
        WriteLegacyFieldTxt(convergence: 1.23);

        var field = _service.LoadField(_fieldDir);

        Assert.That(field.Convergence, Is.EqualTo(1.23).Within(0.01));
    }

    [Test]
    public void LoadField_LegacyWithBoundary_PreservesBoundaryInGeoJson()
    {
        WriteLegacyFieldTxt();
        WriteLegacyBoundary();

        _service.LoadField(_fieldDir);

        // Verify GeoJSON was created
        Assert.That(GeoJsonFieldService.Exists(_fieldDir), Is.True);

        // Load from GeoJSON and verify boundary survived
        var (geoField, _) = GeoJsonFieldService.Load(_fieldDir);
        Assert.That(geoField.Boundary, Is.Not.Null);
        Assert.That(geoField.Boundary!.OuterBoundary, Is.Not.Null);
        Assert.That(geoField.Boundary.OuterBoundary!.Points.Count, Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void LoadField_SecondLoad_UsesGeoJson()
    {
        WriteLegacyFieldTxt(lat: 47.0, lon: -93.0);

        // First load: reads legacy, creates GeoJSON
        _service.LoadField(_fieldDir);
        Assert.That(GeoJsonFieldService.Exists(_fieldDir), Is.True);

        // Modify the legacy file to have different coordinates
        WriteLegacyFieldTxt(lat: 99.0, lon: -99.0);

        // Second load: should use GeoJSON (not the modified legacy)
        var field = _service.LoadField(_fieldDir);
        Assert.That(field.Origin.Latitude, Is.EqualTo(47.0).Within(0.001),
            "Second load should use GeoJSON, not the modified legacy file");
    }

    [Test]
    public void LoadField_GeoJsonAlreadyExists_DoesNotReConvert()
    {
        WriteLegacyFieldTxt(lat: 47.0, lon: -93.0);

        // Create GeoJSON with different origin
        var geoField = new Field
        {
            Name = "TestField",
            DirectoryPath = _fieldDir,
            Origin = new Position { Latitude = 48.0, Longitude = -94.0 },
        };
        GeoJsonFieldService.Save(geoField, new List<AgValoniaGPS.Models.Track.Track>());

        // Load should use existing GeoJSON, not re-convert from legacy
        var field = _service.LoadField(_fieldDir);
        Assert.That(field.Origin.Latitude, Is.EqualTo(48.0).Within(0.001),
            "Should load from existing GeoJSON, not re-convert from legacy");
    }

    [Test]
    public void LoadField_LegacyOnly_OriginalFilesUntouched()
    {
        WriteLegacyFieldTxt();
        var originalContent = File.ReadAllText(Path.Combine(_fieldDir, "Field.txt"));

        _service.LoadField(_fieldDir);

        var afterContent = File.ReadAllText(Path.Combine(_fieldDir, "Field.txt"));
        Assert.That(afterContent, Is.EqualTo(originalContent),
            "Legacy files should not be modified during auto-conversion");
    }

    [Test]
    public void LoadField_EmptyFieldDir_Throws()
    {
        // No Field.txt, no field.geojson
        Assert.Throws<FileNotFoundException>(() => _service.LoadField(_fieldDir));
    }

    [Test]
    public void LoadField_GeoJsonRoundTrip_PreservesOriginAccuracy()
    {
        double lat = 47.1234567;
        double lon = -93.9876543;
        WriteLegacyFieldTxt(lat: lat, lon: lon);

        // First load: legacy -> auto-convert to GeoJSON
        _service.LoadField(_fieldDir);

        // Second load: from GeoJSON
        var field = _service.LoadField(_fieldDir);

        Assert.That(field.Origin.Latitude, Is.EqualTo(lat).Within(0.0000001),
            "Origin latitude should survive legacy->GeoJSON round-trip with 7dp accuracy");
        Assert.That(field.Origin.Longitude, Is.EqualTo(lon).Within(0.0000001),
            "Origin longitude should survive legacy->GeoJSON round-trip with 7dp accuracy");
    }

    [Test]
    public void FieldExists_LegacyOnly_ReturnsTrue()
    {
        WriteLegacyFieldTxt();

        Assert.That(_service.FieldExists(_fieldDir), Is.True);
    }

    [Test]
    public void FieldExists_GeoJsonOnly_ReturnsTrue()
    {
        var field = new Field
        {
            Name = "TestField",
            DirectoryPath = _fieldDir,
            Origin = new Position { Latitude = 47.0, Longitude = -93.0 },
        };
        GeoJsonFieldService.Save(field, new List<AgValoniaGPS.Models.Track.Track>());

        Assert.That(_service.FieldExists(_fieldDir), Is.True);
    }

    [Test]
    public void FieldExists_EmptyDir_ReturnsFalse()
    {
        Assert.That(_service.FieldExists(_fieldDir), Is.False);
    }
}
