using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Tests;

[TestFixture]
public class GeometryMathTests
{
    #region Angle Conversions

    [TestCase(0, 0)]
    [TestCase(Math.PI, 180)]
    [TestCase(Math.PI / 2, 90)]
    [TestCase(-Math.PI, -180)]
    public void ToDegrees_KnownValues(double radians, double expectedDegrees)
    {
        Assert.That(GeometryMath.ToDegrees(radians), Is.EqualTo(expectedDegrees).Within(1e-6));
    }

    [TestCase(0, 0)]
    [TestCase(180, Math.PI)]
    [TestCase(90, Math.PI / 2)]
    [TestCase(-180, -Math.PI)]
    public void ToRadians_KnownValues(double degrees, double expectedRadians)
    {
        Assert.That(GeometryMath.ToRadians(degrees), Is.EqualTo(expectedRadians).Within(1e-6));
    }

    [Test]
    public void ToRadians_ToDegrees_RoundTrip()
    {
        for (double deg = -360; deg <= 360; deg += 15)
        {
            double result = GeometryMath.ToDegrees(GeometryMath.ToRadians(deg));
            Assert.That(result, Is.EqualTo(deg).Within(1e-10),
                $"Round-trip failed for {deg} degrees");
        }
    }

    [TestCase(0, Math.PI, Math.PI)]
    [TestCase(0.1, 6.1, 0.1 + (2 * Math.PI - 6.1))]
    [TestCase(0, 0, 0)]
    [TestCase(1.0, 1.0, 0)]
    public void AngleDiff_ReturnsAbsoluteSmallestDifference(double a1, double a2, double expected)
    {
        Assert.That(GeometryMath.AngleDiff(a1, a2), Is.EqualTo(expected).Within(1e-6));
    }

    [Test]
    public void AngleDiff_NeverExceedsPi()
    {
        var rng = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            double a = rng.NextDouble() * GeometryMath.twoPI;
            double b = rng.NextDouble() * GeometryMath.twoPI;
            Assert.That(GeometryMath.AngleDiff(a, b), Is.LessThanOrEqualTo(Math.PI + 1e-10));
        }
    }

    #endregion

    #region IsPointInPolygon

    private static readonly List<Vec2> UnitSquare = new()
    {
        new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10)
    };

    private static readonly List<Vec3> UnitSquareVec3 = new()
    {
        new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(10, 10, 0), new Vec3(0, 10, 0)
    };

    [Test]
    public void IsPointInPolygon_Vec2_InsidePoint_ReturnsTrue()
    {
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquare, new Vec2(5, 5)), Is.True);
    }

    [Test]
    public void IsPointInPolygon_Vec2_OutsidePoint_ReturnsFalse()
    {
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquare, new Vec2(15, 5)), Is.False);
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquare, new Vec2(-1, 5)), Is.False);
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquare, new Vec2(5, -1)), Is.False);
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquare, new Vec2(5, 11)), Is.False);
    }

    [Test]
    public void IsPointInPolygon_Vec3Polygon_Vec3Point_InsideReturnsTrue()
    {
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquareVec3, new Vec3(5, 5, 0)), Is.True);
    }

    [Test]
    public void IsPointInPolygon_Vec3Polygon_Vec3Point_OutsideReturnsFalse()
    {
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquareVec3, new Vec3(15, 5, 0)), Is.False);
    }

    [Test]
    public void IsPointInPolygon_Vec3Polygon_Vec2Point_InsideReturnsTrue()
    {
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquareVec3, new Vec2(5, 5)), Is.True);
    }

    [Test]
    public void IsPointInPolygon_Vec2Polygon_Vec3Point_InsideReturnsTrue()
    {
        Assert.That(GeometryMath.IsPointInPolygon(UnitSquare, new Vec3(5, 5, 0)), Is.True);
    }

    [Test]
    public void IsPointInPolygon_AllOverloads_Agree()
    {
        var testPoints = new[] { new Vec2(5, 5), new Vec2(15, 5), new Vec2(0.1, 0.1) };
        foreach (var pt in testPoints)
        {
            bool r1 = GeometryMath.IsPointInPolygon(UnitSquare, pt);
            bool r2 = GeometryMath.IsPointInPolygon(UnitSquareVec3, pt);
            bool r3 = GeometryMath.IsPointInPolygon(UnitSquare, new Vec3(pt.Easting, pt.Northing, 0));
            bool r4 = GeometryMath.IsPointInPolygon(UnitSquareVec3, new Vec3(pt.Easting, pt.Northing, 0));

            Assert.That(r2, Is.EqualTo(r1), $"Vec3Poly/Vec2Pt disagrees at ({pt.Easting},{pt.Northing})");
            Assert.That(r3, Is.EqualTo(r1), $"Vec2Poly/Vec3Pt disagrees at ({pt.Easting},{pt.Northing})");
            Assert.That(r4, Is.EqualTo(r1), $"Vec3Poly/Vec3Pt disagrees at ({pt.Easting},{pt.Northing})");
        }
    }

    [Test]
    public void IsPointInPolygon_ConcavePolygon()
    {
        // L-shaped polygon
        var lShape = new List<Vec2>
        {
            new(0, 0), new(10, 0), new(10, 5), new(5, 5), new(5, 10), new(0, 10)
        };

        Assert.That(GeometryMath.IsPointInPolygon(lShape, new Vec2(2, 2)), Is.True);
        Assert.That(GeometryMath.IsPointInPolygon(lShape, new Vec2(8, 2)), Is.True);
        Assert.That(GeometryMath.IsPointInPolygon(lShape, new Vec2(2, 8)), Is.True);
        // Inside the cutout
        Assert.That(GeometryMath.IsPointInPolygon(lShape, new Vec2(8, 8)), Is.False);
    }

    #endregion

    #region Distance

    [Test]
    public void Distance_SamePoint_ReturnsZero()
    {
        Assert.That(GeometryMath.Distance(5.0, 5.0, 5.0, 5.0), Is.EqualTo(0).Within(1e-10));
    }

    [Test]
    public void Distance_KnownTriangle_3_4_5()
    {
        Assert.That(GeometryMath.Distance(0.0, 0.0, 3.0, 4.0), Is.EqualTo(5.0).Within(1e-10));
    }

    [Test]
    public void Distance_Vec2_MatchesScalar()
    {
        var a = new Vec2(1, 2);
        var b = new Vec2(4, 6);
        double expected = GeometryMath.Distance(1.0, 2.0, 4.0, 6.0);
        Assert.That(GeometryMath.Distance(a, b), Is.EqualTo(expected).Within(1e-10));
    }

    [Test]
    public void Distance_Vec3_IgnoresHeading()
    {
        var a = new Vec3(0, 0, 1.5);
        var b = new Vec3(3, 4, 0.5);
        Assert.That(GeometryMath.Distance(a, b), Is.EqualTo(5.0).Within(1e-10));
    }

    [Test]
    public void DistanceSquared_IsSquareOfDistance()
    {
        var a = new Vec2(1, 2);
        var b = new Vec2(4, 6);
        double dist = GeometryMath.Distance(a, b);
        double distSq = GeometryMath.DistanceSquared(a, b);
        Assert.That(distSq, Is.EqualTo(dist * dist).Within(1e-10));
    }

    #endregion

    #region InRangeBetweenAB

    [Test]
    public void InRangeBetweenAB_PointOnSegment_ReturnsTrue()
    {
        Assert.That(GeometryMath.InRangeBetweenAB(0, 0, 10, 0, 5, 0), Is.True);
    }

    [Test]
    public void InRangeBetweenAB_PointBeyondEnd_ReturnsFalse()
    {
        Assert.That(GeometryMath.InRangeBetweenAB(0, 0, 10, 0, 15, 0), Is.False);
    }

    [Test]
    public void InRangeBetweenAB_PointBeforeStart_ReturnsFalse()
    {
        Assert.That(GeometryMath.InRangeBetweenAB(0, 0, 10, 0, -5, 0), Is.False);
    }

    [Test]
    public void InRangeBetweenAB_Endpoints_ReturnTrue()
    {
        Assert.That(GeometryMath.InRangeBetweenAB(0, 0, 10, 0, 0, 0), Is.True);
        Assert.That(GeometryMath.InRangeBetweenAB(0, 0, 10, 0, 10, 0), Is.True);
    }

    #endregion

    #region Raycasting

    [Test]
    public void TryRaySegmentIntersection_HitsSegment()
    {
        var origin = new Vec2(0, 0);
        var dir = new Vec2(1, 0); // east
        var segA = new Vec2(5, -5);
        var segB = new Vec2(5, 5);

        bool hit = GeometryMath.TryRaySegmentIntersection(origin, dir, segA, segB, out Vec2 intersection);

        Assert.That(hit, Is.True);
        Assert.That(intersection.Easting, Is.EqualTo(5).Within(1e-6));
        Assert.That(intersection.Northing, Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void TryRaySegmentIntersection_MissesSegment()
    {
        var origin = new Vec2(0, 0);
        var dir = new Vec2(1, 0); // east
        var segA = new Vec2(5, 10);
        var segB = new Vec2(5, 20);

        bool hit = GeometryMath.TryRaySegmentIntersection(origin, dir, segA, segB, out _);
        Assert.That(hit, Is.False);
    }

    [Test]
    public void TryRaySegmentIntersection_ParallelRay_ReturnsFalse()
    {
        var origin = new Vec2(0, 0);
        var dir = new Vec2(1, 0);
        var segA = new Vec2(0, 5);
        var segB = new Vec2(10, 5);

        bool hit = GeometryMath.TryRaySegmentIntersection(origin, dir, segA, segB, out _);
        Assert.That(hit, Is.False);
    }

    #endregion

    #region ToLocalCoords

    [Test]
    public void ToLocalCoords_ZeroHeading_ReturnsOffset()
    {
        var world = new Vec2(3, 4);
        var center = new Vec2(0, 0);
        var local = GeometryMath.ToLocalCoords(world, center, 0);

        Assert.That(local.Easting, Is.EqualTo(3).Within(1e-6));
        Assert.That(local.Northing, Is.EqualTo(4).Within(1e-6));
    }

    [Test]
    public void ToLocalCoords_90DegreeHeading_RotatesCorrectly()
    {
        var world = new Vec2(5, 0);
        var center = new Vec2(0, 0);
        var local = GeometryMath.ToLocalCoords(world, center, Math.PI / 2);

        // ToLocalCoords rotates by -heading. With heading=PI/2, a point at (5,0)
        // maps to (0, -5) in local coords (east world -> negative Y in local)
        Assert.That(local.Easting, Is.EqualTo(0).Within(1e-6));
        Assert.That(local.Northing, Is.EqualTo(-5).Within(1e-6));
    }

    [Test]
    public void ToLocalCoords_PrecomputedSinCos_MatchesHeadingOverload()
    {
        var world = new Vec2(7, 3);
        var center = new Vec2(1, 2);
        double heading = 0.7;

        var result1 = GeometryMath.ToLocalCoords(world, center, heading);
        double cos = Math.Cos(-heading);
        double sin = Math.Sin(-heading);
        var result2 = GeometryMath.ToLocalCoords(world, center, cos, sin);

        Assert.That(result2.Easting, Is.EqualTo(result1.Easting).Within(1e-10));
        Assert.That(result2.Northing, Is.EqualTo(result1.Northing).Within(1e-10));
    }

    #endregion

    #region GetXInterceptAtY

    [Test]
    public void GetXInterceptAtY_CrossesThreshold_ReturnsX()
    {
        var p1 = new Vec2(0, -5);
        var p2 = new Vec2(0, 5);
        double? x = GeometryMath.GetXInterceptAtY(p1, p2, 0);

        Assert.That(x, Is.Not.Null);
        Assert.That(x!.Value, Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void GetXInterceptAtY_DoesNotCross_ReturnsNull()
    {
        var p1 = new Vec2(0, 5);
        var p2 = new Vec2(0, 10);
        double? x = GeometryMath.GetXInterceptAtY(p1, p2, 0);

        Assert.That(x, Is.Null);
    }

    [Test]
    public void GetXInterceptAtY_DiagonalEdge_CorrectIntercept()
    {
        var p1 = new Vec2(0, -2);
        var p2 = new Vec2(4, 2);
        double? x = GeometryMath.GetXInterceptAtY(p1, p2, 0);

        Assert.That(x, Is.Not.Null);
        Assert.That(x!.Value, Is.EqualTo(2).Within(1e-6));
    }

    #endregion

    #region MergeIntervals

    [Test]
    public void MergeIntervals_EmptyList_ReturnsEmpty()
    {
        var result = GeometryMath.MergeIntervals(new List<(double, double)>());
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void MergeIntervals_NonOverlapping_ReturnsAll()
    {
        var intervals = new List<(double, double)> { (0, 1), (3, 4), (6, 7) };
        var result = GeometryMath.MergeIntervals(intervals);
        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public void MergeIntervals_FullyOverlapping_ReturnsSingle()
    {
        var intervals = new List<(double, double)> { (0, 10), (2, 5), (3, 7) };
        var result = GeometryMath.MergeIntervals(intervals);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Start, Is.EqualTo(0));
        Assert.That(result[0].End, Is.EqualTo(10));
    }

    [Test]
    public void MergeIntervals_Adjacent_Merges()
    {
        var intervals = new List<(double, double)> { (0, 5), (5, 10) };
        var result = GeometryMath.MergeIntervals(intervals);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Start, Is.EqualTo(0));
        Assert.That(result[0].End, Is.EqualTo(10));
    }

    [Test]
    public void MergeIntervals_PartialOverlap()
    {
        var intervals = new List<(double, double)> { (0, 5), (3, 8), (10, 15) };
        var result = GeometryMath.MergeIntervals(intervals);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo((0.0, 8.0)));
        Assert.That(result[1], Is.EqualTo((10.0, 15.0)));
    }

    #endregion

    #region Catmull-Rom Spline

    [Test]
    public void Catmull_AtT0_ReturnsP1()
    {
        var p0 = new Vec3(-1, 0, 0);
        var p1 = new Vec3(0, 0, 0);
        var p2 = new Vec3(1, 0, 0);
        var p3 = new Vec3(2, 0, 0);

        var result = GeometryMath.Catmull(0, p0, p1, p2, p3);
        Assert.That(result.Easting, Is.EqualTo(p1.Easting).Within(1e-6));
        Assert.That(result.Northing, Is.EqualTo(p1.Northing).Within(1e-6));
    }

    [Test]
    public void Catmull_AtT1_ReturnsP2()
    {
        var p0 = new Vec3(-1, 0, 0);
        var p1 = new Vec3(0, 0, 0);
        var p2 = new Vec3(1, 1, 0);
        var p3 = new Vec3(2, 1, 0);

        var result = GeometryMath.Catmull(1, p0, p1, p2, p3);
        Assert.That(result.Easting, Is.EqualTo(p2.Easting).Within(1e-6));
        Assert.That(result.Northing, Is.EqualTo(p2.Northing).Within(1e-6));
    }

    #endregion

    #region Unit Constants

    [Test]
    public void UnitConstants_InversesAreConsistent()
    {
        Assert.That(GeometryMath.in2m * GeometryMath.m2in, Is.EqualTo(1.0).Within(1e-4));
        Assert.That(GeometryMath.ft2m * GeometryMath.m2ft, Is.EqualTo(1.0).Within(1e-4));
        Assert.That(GeometryMath.ha2ac * GeometryMath.ac2ha, Is.EqualTo(1.0).Within(1e-4));
        Assert.That(GeometryMath.L2Gal * GeometryMath.Gal2L, Is.EqualTo(1.0).Within(1e-4));
    }

    #endregion
}
