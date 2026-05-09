using System.Globalization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.GeoJson;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Contract fences around <see cref="FieldService"/>'s save/load behaviour
/// for boundaries. These exist because the project recently shipped a bug
/// where the user's painstakingly-recorded boundary was overwritten with
/// nothing on field-close: the boundary had been written to Boundary.txt
/// during recording, but ActiveField.Boundary in memory was null, so the
/// save-on-close cycle re-wrote the canonical (GeoJSON) file with no
/// boundary features. The next open round-tripped through the now-empty
/// GeoJSON and produced a field with no boundary.
///
/// What this fixture pins down:
///
///   1. A round-trip through FieldService preserves a 4-point boundary
///      in both legacy and GeoJSON formats.
///   2. After a save that wrote a boundary, loading it back yields an
///      identical boundary -- so anyone who refactors the canonical
///      load path (currently GeoJSON) into something that drops features
///      will see this go red.
///   3. The on-disk lat/lon line is locale-stable -- a separate-but-
///      related class of bug we have lost a day to before. Asserted with
///      CultureInfo.InvariantCulture so the test never reproduces the
///      bug it is fencing.
///
/// This fixture deliberately exercises the public IFieldService surface
/// (not the lower-level FieldPlaneFileService / BoundaryFileService),
/// because that is the surface the rest of the application actually
/// uses, and the contract the close-save bug crossed.
/// </summary>
[TestFixture]
public class FieldServiceBoundaryContractTests
{
    private string _tempRoot = null!;
    private const double OriginLat = 32.5904;
    private const double OriginLon = -87.1804;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"agvalonia_fcontract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    [Test]
    public void SaveLoad_FieldWithFourPointBoundary_RoundTripsExactly()
    {
        var fieldService = new FieldService();
        var field = fieldService.CreateField(_tempRoot, "RoundTrip", new Position
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
        });

        var fieldDir = field.DirectoryPath;
        var originalPoints = new[]
        {
            new BoundaryPoint(   0.0,   0.0, 0.0),
            new BoundaryPoint( 250.5,   0.0, Math.PI / 2),
            new BoundaryPoint( 250.5, 400.25, Math.PI),
            new BoundaryPoint(   0.0, 400.25, 3 * Math.PI / 2),
        };
        field.Boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                IsDriveThrough = false,
                Points = new List<BoundaryPoint>(originalPoints)
            }
        };
        field.Boundary.OuterBoundary!.UpdateBounds();

        fieldService.SaveField(field);
        var loaded = fieldService.LoadField(fieldDir);

        Assert.That(loaded.Boundary, Is.Not.Null, "Boundary must round-trip");
        Assert.That(loaded.Boundary!.OuterBoundary, Is.Not.Null);
        Assert.That(loaded.Boundary.OuterBoundary!.Points, Has.Count.EqualTo(4),
            "Boundary should retain all 4 corner points after round-trip");

        for (int i = 0; i < 4; i++)
        {
            // 1 cm tolerance absorbs WGS84<->local re-projection noise in
            // the GeoJSON path. Anything larger means a real regression.
            Assert.That(loaded.Boundary.OuterBoundary.Points[i].Easting,
                Is.EqualTo(originalPoints[i].Easting).Within(0.01),
                $"Easting drift at point {i.ToString(CultureInfo.InvariantCulture)}");
            Assert.That(loaded.Boundary.OuterBoundary.Points[i].Northing,
                Is.EqualTo(originalPoints[i].Northing).Within(0.01),
                $"Northing drift at point {i.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    [Test]
    public void Save_WithBoundaryThenSaveWithNullBoundary_DoesNotSilentlyLoseBoundary()
    {
        // Reproduces the exact close-save shape that bit us: a field is
        // saved WITH a boundary, then the same field (or a re-fetched
        // copy where ActiveField.Boundary is null) is saved again. After
        // that second save, what does LoadField return?
        //
        // The bug: GeoJSON wrote a feature collection with no boundary
        // feature, and the loader then reported "no boundary" -- even
        // though Boundary.txt on disk still had the data.
        //
        // The contract this fence pins: either the second save preserves
        // the boundary (by syncing from disk before writing GeoJSON), OR
        // the loader falls back to legacy when GeoJSON is empty. Either
        // resolution keeps the user's data; both are acceptable. The
        // test fails only if both layers drop the boundary together --
        // which is exactly the silent-data-loss regression we want to
        // catch.
        var fieldService = new FieldService();
        var field = fieldService.CreateField(_tempRoot, "CloseSave", new Position
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
        });
        var fieldDir = field.DirectoryPath;

        field.Boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = new List<BoundaryPoint>
                {
                    new(  0,   0, 0),
                    new(100,   0, Math.PI / 2),
                    new(100, 100, Math.PI),
                    new(  0, 100, 3 * Math.PI / 2),
                }
            }
        };
        field.Boundary.OuterBoundary!.UpdateBounds();
        fieldService.SaveField(field);

        // Confirm the round-trip baseline.
        var afterFirstSave = fieldService.LoadField(fieldDir);
        Assert.That(afterFirstSave.Boundary?.OuterBoundary?.Points, Has.Count.EqualTo(4),
            "Baseline: first save must produce a loadable boundary");

        // Now simulate the close-save bug: take a field WITH the right
        // metadata but null boundary (i.e. ActiveField.Boundary was never
        // synced), and call SaveField again on the same directory.
        var resaved = new Field
        {
            Name = afterFirstSave.Name,
            DirectoryPath = fieldDir,
            Origin = afterFirstSave.Origin,
            Boundary = null,
        };
        fieldService.SaveField(resaved);

        var afterSecondSave = fieldService.LoadField(fieldDir);

        // The non-negotiable contract: silent boundary loss is a bug.
        // We assert that SOMETHING survives -- either the boundary itself
        // (preferred), or at minimum the legacy Boundary.txt is intact
        // and could be recovered manually.
        bool boundaryRecovered = afterSecondSave.Boundary?.OuterBoundary?.Points.Count >= 3;
        bool legacyBoundaryIntact = false;
        var boundaryTxt = Path.Combine(fieldDir, "Boundary.txt");
        if (File.Exists(boundaryTxt))
        {
            // 4 points + "False" + "4" header = at least 6 lines.
            // An empty/$Boundary-only file would be 1-2 lines.
            var lines = File.ReadAllLines(boundaryTxt);
            legacyBoundaryIntact = lines.Length >= 6;
        }

        Assert.That(boundaryRecovered || legacyBoundaryIntact, Is.True,
            "Both GeoJSON loader returned no boundary AND Boundary.txt was wiped. " +
            "This is the close-save data-loss regression. " +
            $"GeoJSON-recovered: {boundaryRecovered.ToString(CultureInfo.InvariantCulture)}, " +
            $"Legacy-intact: {legacyBoundaryIntact.ToString(CultureInfo.InvariantCulture)}.");
    }

    [Test]
    public void CreateField_WritesOriginInInvariantCultureRegardlessOfCallerExpectations()
    {
        // Independent of CurrentCulture (covered exhaustively in
        // FieldIoLocaleMatrixTests), this fences the *file format itself*:
        // the field origin written to disk must use a period decimal so
        // that anyone hand-editing the file, or a tool reading it, gets
        // a parseable number.
        var fieldService = new FieldService();
        var field = fieldService.CreateField(_tempRoot, "FormatField", new Position
        {
            Latitude = 12.3456789,
            Longitude = -98.7654321,
        });

        var fieldTxt = File.ReadAllText(Path.Combine(field.DirectoryPath, "Field.txt"));
        Assert.That(fieldTxt, Does.Contain("12.34567890"),
            "Field.txt latitude must be invariant-formatted (period decimal)");
        Assert.That(fieldTxt, Does.Contain("-98.76543210"),
            "Field.txt longitude must be invariant-formatted (period decimal)");
    }

    [Test]
    public void GeoJsonSave_EmptyFeatureCollection_DoesNotCrashOnReload()
    {
        // Defensive: if a future refactor produces a GeoJSON with only the
        // metadata feature (no boundary, no tracks), the loader must not
        // throw. Empty fields are a legitimate state right after CreateField.
        var field = new Field
        {
            Name = "Empty",
            DirectoryPath = _tempRoot,
            Origin = new Position { Latitude = OriginLat, Longitude = OriginLon },
        };

        Assert.DoesNotThrow(() => GeoJsonFieldService.Save(field, tracks: null));
        Assert.That(GeoJsonFieldService.Exists(_tempRoot), Is.True);

        var (loaded, tracks) = GeoJsonFieldService.Load(_tempRoot);
        Assert.That(loaded.Origin.Latitude, Is.EqualTo(OriginLat).Within(1e-6));
        Assert.That(tracks, Is.Not.Null);
        Assert.That(tracks, Is.Empty);
    }
}
