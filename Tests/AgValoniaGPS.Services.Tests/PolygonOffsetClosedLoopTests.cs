using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.Geometry;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests guarding the closed-loop contract for inward polygon offset: the offset of
/// a closed boundary must form a closed polygon (the chain of edges, including the
/// closing edge from last point back to first, traces a single loop without gaps).
///
/// Repro for the headland-preview-not-closing bug: a 4-point boundary fed through
/// CreateInwardOffset must yield an offset that closes the loop. The renderer is
/// responsible for drawing the closing segment, but the geometry contract here is
/// that the first/last offset points are corners of the same closed polygon.
/// </summary>
[TestFixture]
public class PolygonOffsetClosedLoopTests
{
    private PolygonOffsetService _service = null!;

    [SetUp]
    public void Setup() => _service = new PolygonOffsetService();

    /// <summary>
    /// 4-point square boundary -> 4-point offset square. Vertex count must be
    /// preserved (no collapse, no extra interpolation), and the closing edge
    /// from offset[3] -> offset[0] must have the same length as the other edges
    /// (within tolerance) - i.e. it forms a real closed square, not an open path.
    /// </summary>
    [Test]
    public void FourPointSquare_OffsetClosesLoop()
    {
        // 100m x 100m square - representative of a user-drawn 4-point field
        var boundary = new List<Vec2>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100),
        };

        const double offsetDistance = 10.0;
        var result = _service.CreateInwardOffset(boundary, offsetDistance);

        Assert.That(result, Is.Not.Null, "Offset should not collapse for a 100x100 square shrunk by 10m");
        Assert.That(result!.Count, Is.EqualTo(4),
            "Inward offset of a 4-point square must produce exactly 4 corners (Clipper2 convention: not pre-closed)");

        // Compute all four edge lengths (including the closing edge from [3] to [0]).
        // For an 80x80 square (100 - 2*10) all four edges should be ~80m.
        var edges = new List<double>();
        for (int i = 0; i < result.Count; i++)
        {
            var a = result[i];
            var b = result[(i + 1) % result.Count];
            double dx = b.Easting - a.Easting;
            double dy = b.Northing - a.Northing;
            edges.Add(System.Math.Sqrt(dx * dx + dy * dy));
        }

        // Critical: the closing edge (last -> first) must have a real, non-zero length
        // matching the rest. If the offset failed to close the loop, this would be
        // either zero (duplicate point) or the diagonal across the square (~113m).
        const double expectedSide = 80.0;
        for (int i = 0; i < edges.Count; i++)
        {
            Assert.That(edges[i], Is.EqualTo(expectedSide).Within(0.5),
                $"Edge {i}->{(i + 1) % edges.Count} length {edges[i]:F2}m differs from expected square side {expectedSide:F2}m. " +
                $"If edge index {edges.Count - 1} is wrong, the closing edge is broken (preview rendering bug repro).");
        }
    }

    /// <summary>
    /// The offset result is NOT pre-closed (last point != first point). This
    /// documents the contract that callers (renderers, headland builders) must
    /// explicitly close the loop by appending the first point. Regressions in
    /// this contract would silently break consumers like BuildHeadlandFromSegments
    /// (which appends closedPoly[0]) and the field-builder offset preview.
    /// </summary>
    [Test]
    public void FourPointSquare_OffsetIsNotPreClosed()
    {
        var boundary = new List<Vec2>
        {
            new(0, 0),
            new(100, 0),
            new(100, 100),
            new(0, 100),
        };

        var result = _service.CreateInwardOffset(boundary, 10.0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.GreaterThanOrEqualTo(3));

        var first = result[0];
        var last = result[^1];
        double dx = first.Easting - last.Easting;
        double dy = first.Northing - last.Northing;
        double dist = System.Math.Sqrt(dx * dx + dy * dy);

        Assert.That(dist, Is.GreaterThan(1.0),
            "Offset polygon must NOT be pre-closed (first and last point should be distinct corners). " +
            "If this regresses, BuildHeadlandFromSegments and the field-builder offset preview will append a duplicate close-point.");
    }
}
