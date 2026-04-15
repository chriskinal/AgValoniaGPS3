using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.Geometry;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests that Clipper2 polygon offset stays inside the original boundary.
/// Verifies the intersection clipping added to PolygonOffsetService.
/// </summary>
[TestFixture]
public class PolygonOffsetClipperTests
{
    private PolygonOffsetService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = new PolygonOffsetService();
    }

    /// <summary>
    /// Simple convex rectangle - offset should shrink inward and stay inside.
    /// </summary>
    [Test]
    public void ConvexRectangle_OffsetStaysInside()
    {
        // 100m x 80m rectangle
        var boundary = new List<Vec2>
        {
            new(0, 0),
            new(100, 0),
            new(100, 80),
            new(0, 80)
        };

        var result = _service.CreateInwardOffset(boundary, 10.0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.GreaterThanOrEqualTo(3));

        // Every offset point must be inside the original boundary
        foreach (var pt in result)
        {
            Assert.That(pt.Easting, Is.GreaterThanOrEqualTo(-0.01), $"Point ({pt.Easting:F2}, {pt.Northing:F2}) outside west edge");
            Assert.That(pt.Easting, Is.LessThanOrEqualTo(100.01), $"Point ({pt.Easting:F2}, {pt.Northing:F2}) outside east edge");
            Assert.That(pt.Northing, Is.GreaterThanOrEqualTo(-0.01), $"Point ({pt.Easting:F2}, {pt.Northing:F2}) outside south edge");
            Assert.That(pt.Northing, Is.LessThanOrEqualTo(80.01), $"Point ({pt.Easting:F2}, {pt.Northing:F2}) outside north edge");
        }
    }

    /// <summary>
    /// Concave L-shape boundary - offset near concave corner must not leak outside.
    /// </summary>
    [Test]
    public void ConcaveLShape_OffsetStaysInside()
    {
        // L-shaped polygon (concave)
        //   +-----+
        //   |     |
        //   |  +--+
        //   |  |
        //   +--+
        var boundary = new List<Vec2>
        {
            new(0, 0),
            new(40, 0),
            new(40, 50),
            new(80, 50),
            new(80, 100),
            new(0, 100)
        };

        var result = _service.CreateInwardOffset(boundary, 8.0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.GreaterThanOrEqualTo(3));

        // All offset points must be inside the original L-shape
        foreach (var pt in result)
        {
            bool inside = IsPointInLShape(pt.Easting, pt.Northing);
            Assert.That(inside, Is.True,
                $"Offset point ({pt.Easting:F2}, {pt.Northing:F2}) is outside the L-shape boundary");
        }
    }

    /// <summary>
    /// Boundary with a narrow spike - Clipper2 intersection clipping should prevent
    /// offset points from extending outside the spike.
    /// </summary>
    [Test]
    public void NarrowSpike_OffsetClippedToBoundary()
    {
        // Triangle with a narrow spike pointing east
        var boundary = new List<Vec2>
        {
            new(0, 0),
            new(100, 0),
            new(120, 25),  // narrow spike tip
            new(100, 50),
            new(0, 50)
        };

        var result = _service.CreateInwardOffset(boundary, 5.0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.GreaterThanOrEqualTo(3));

        // Verify no point extends beyond the maximum X of the boundary (120)
        foreach (var pt in result)
        {
            Assert.That(pt.Easting, Is.LessThanOrEqualTo(120.01),
                $"Offset point ({pt.Easting:F2}, {pt.Northing:F2}) extends beyond boundary spike");
        }
    }

    /// <summary>
    /// GPS-recorded curved boundary (many points) should use Round join
    /// and still stay inside the boundary.
    /// </summary>
    [Test]
    public void CurvedBoundary_OffsetStaysInside()
    {
        // Generate a circular boundary (simulating GPS-recorded curve)
        var boundary = new List<Vec2>();
        int numPoints = 120;
        double radius = 50.0;
        double cx = 50.0, cy = 50.0;

        for (int i = 0; i < numPoints; i++)
        {
            double angle = 2.0 * Math.PI * i / numPoints;
            boundary.Add(new Vec2(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
        }

        var result = _service.CreateInwardOffset(boundary, 10.0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.GreaterThanOrEqualTo(3));

        // Every offset point should be inside the original circle (within tolerance)
        foreach (var pt in result)
        {
            double dist = Math.Sqrt(Math.Pow(pt.Easting - cx, 2) + Math.Pow(pt.Northing - cy, 2));
            Assert.That(dist, Is.LessThanOrEqualTo(radius + 0.1),
                $"Offset point ({pt.Easting:F2}, {pt.Northing:F2}) is {dist:F2}m from center, outside radius {radius}m");
        }

        // Offset should be approximately at radius - 10
        double expectedRadius = radius - 10.0;
        double avgDist = result.Average(p => Math.Sqrt(Math.Pow(p.Easting - cx, 2) + Math.Pow(p.Northing - cy, 2)));
        Assert.That(avgDist, Is.EqualTo(expectedRadius).Within(1.0),
            $"Average offset distance should be ~{expectedRadius}m, got {avgDist:F2}m");
    }

    /// <summary>
    /// Zero or negative offset should return the original boundary.
    /// </summary>
    [Test]
    public void ZeroOffset_ReturnsCopy()
    {
        var boundary = new List<Vec2>
        {
            new(0, 0), new(100, 0), new(100, 100), new(0, 100)
        };

        var result = _service.CreateInwardOffset(boundary, 0);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(4));
    }

    /// <summary>
    /// Offset larger than the polygon should return null (collapsed).
    /// </summary>
    [Test]
    public void LargeOffset_ReturnsNull()
    {
        // Small 10m square
        var boundary = new List<Vec2>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };

        // 20m offset on 10m square should collapse
        var result = _service.CreateInwardOffset(boundary, 20.0);

        Assert.That(result, Is.Null);
    }

    // Helper: point-in-L-shape test
    private static bool IsPointInLShape(double x, double y)
    {
        // L-shape: (0,0)-(40,0)-(40,50)-(80,50)-(80,100)-(0,100)
        // Bottom rectangle: 0<=x<=40, 0<=y<=50
        // Top rectangle: 0<=x<=80, 50<=y<=100
        const double tol = 0.1;
        bool inBottom = x >= -tol && x <= 40 + tol && y >= -tol && y <= 50 + tol;
        bool inTop = x >= -tol && x <= 80 + tol && y >= 50 - tol && y <= 100 + tol;
        return inBottom || inTop;
    }
}
