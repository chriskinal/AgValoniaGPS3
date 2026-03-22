using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Tests;

[TestFixture]
public class GeoConversionTests
{
    #region ToLocal / ToWgs84 Round-Trip

    [Test]
    public void ToLocal_OriginPoint_ReturnsZero()
    {
        var geo = new GeoConversion(48.0, 11.0);
        var local = geo.ToLocal(48.0, 11.0);

        Assert.That(local.Easting, Is.EqualTo(0).Within(1e-6));
        Assert.That(local.Northing, Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void ToLocal_ToWgs84_RoundTrip()
    {
        var geo = new GeoConversion(48.1234, 11.5678);

        double lat = 48.1244;
        double lon = 11.5688;

        var local = geo.ToLocal(lat, lon);
        var (roundLat, roundLon) = geo.ToWgs84(local);

        Assert.That(roundLat, Is.EqualTo(lat).Within(1e-8),
            "Latitude round-trip error exceeds 1e-8 degrees (~1mm)");
        Assert.That(roundLon, Is.EqualTo(lon).Within(1e-8),
            "Longitude round-trip error exceeds 1e-8 degrees (~1mm)");
    }

    [TestCase(40.7128, -74.0060)]   // New York
    [TestCase(61.4978, 23.7610)]    // Tampere
    [TestCase(-33.8688, 151.2093)]  // Sydney
    [TestCase(0.0, 0.0)]            // Equator/prime meridian
    public void ToLocal_ToWgs84_RoundTrip_MultipleLocations(double originLat, double originLon)
    {
        var geo = new GeoConversion(originLat, originLon);

        // 100m offset in each direction
        double offsetLat = originLat + 0.001;
        double offsetLon = originLon + 0.001;

        var local = geo.ToLocal(offsetLat, offsetLon);
        var (roundLat, roundLon) = geo.ToWgs84(local);

        Assert.That(roundLat, Is.EqualTo(offsetLat).Within(1e-8));
        Assert.That(roundLon, Is.EqualTo(offsetLon).Within(1e-8));
    }

    #endregion

    #region ToLocal Distance Accuracy

    [Test]
    public void ToLocal_OneDegreeLat_ApproximatelyCorrectMeters()
    {
        // At 45 degrees latitude, 1 degree lat ~ 111km
        var geo = new GeoConversion(45.0, 0.0);
        var local = geo.ToLocal(46.0, 0.0);

        Assert.That(local.Northing, Is.EqualTo(111000).Within(500),
            "1 degree latitude should be approximately 111km");
        Assert.That(local.Easting, Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void ToLocal_NorthIsPositiveNorthing()
    {
        var geo = new GeoConversion(48.0, 11.0);
        var local = geo.ToLocal(48.001, 11.0);
        Assert.That(local.Northing, Is.GreaterThan(0));
    }

    [Test]
    public void ToLocal_EastIsPositiveEasting()
    {
        var geo = new GeoConversion(48.0, 11.0);
        var local = geo.ToLocal(48.0, 11.001);
        Assert.That(local.Easting, Is.GreaterThan(0));
    }

    [Test]
    public void ToLocal_SouthIsNegativeNorthing()
    {
        var geo = new GeoConversion(48.0, 11.0);
        var local = geo.ToLocal(47.999, 11.0);
        Assert.That(local.Northing, Is.LessThan(0));
    }

    [Test]
    public void ToLocal_WestIsNegativeEasting()
    {
        var geo = new GeoConversion(48.0, 11.0);
        var local = geo.ToLocal(48.0, 10.999);
        Assert.That(local.Easting, Is.LessThan(0));
    }

    #endregion

    #region HeadingFromPoints

    [Test]
    public void HeadingFromPoints_DueNorth_ReturnsZero()
    {
        var a = new Vec2(0, 0);
        var b = new Vec2(0, 10);
        Assert.That(GeoConversion.HeadingFromPoints(a, b), Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void HeadingFromPoints_DueEast_ReturnsPiOver2()
    {
        var a = new Vec2(0, 0);
        var b = new Vec2(10, 0);
        Assert.That(GeoConversion.HeadingFromPoints(a, b), Is.EqualTo(Math.PI / 2).Within(1e-6));
    }

    [Test]
    public void HeadingFromPoints_DueSouth_ReturnsPi()
    {
        var a = new Vec2(0, 0);
        var b = new Vec2(0, -10);
        Assert.That(GeoConversion.HeadingFromPoints(a, b), Is.EqualTo(Math.PI).Within(1e-6));
    }

    [Test]
    public void HeadingFromPoints_DueWest_Returns3PiOver2()
    {
        var a = new Vec2(0, 0);
        var b = new Vec2(-10, 0);
        Assert.That(GeoConversion.HeadingFromPoints(a, b), Is.EqualTo(3 * Math.PI / 2).Within(1e-6));
    }

    [Test]
    public void HeadingFromPoints_AlwaysInRange_0_to_2Pi()
    {
        var rng = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var a = new Vec2(rng.NextDouble() * 100, rng.NextDouble() * 100);
            var b = new Vec2(rng.NextDouble() * 100, rng.NextDouble() * 100);
            if (GeometryMath.Distance(a, b) < 0.01) continue;

            double heading = GeoConversion.HeadingFromPoints(a, b);
            Assert.That(heading, Is.GreaterThanOrEqualTo(0));
            Assert.That(heading, Is.LessThan(GeometryMath.twoPI));
        }
    }

    #endregion

    #region OriginProperties

    [Test]
    public void OriginProperties_MatchConstructor()
    {
        var geo = new GeoConversion(61.4978, 23.7610);
        Assert.That(geo.OriginLatitude, Is.EqualTo(61.4978));
        Assert.That(geo.OriginLongitude, Is.EqualTo(23.7610));
    }

    #endregion
}

[TestFixture]
public class BoundaryUtilsTests
{
    [Test]
    public void WithHeadings_Vec2_ClosedLoop_AllPointsGetHeading()
    {
        var points = new List<Vec2>
        {
            new(0, 0), new(10, 0), new(10, 10), new(0, 10)
        };

        var result = BoundaryUtils.WithHeadings(points);

        Assert.That(result, Has.Count.EqualTo(4));
        // First point -> second point is due east
        Assert.That(result[0].Heading, Is.EqualTo(Math.PI / 2).Within(1e-6));
    }

    [Test]
    public void WithHeadings_TooFewPoints_ReturnsEmpty()
    {
        var result = BoundaryUtils.WithHeadings(new List<Vec2> { new(0, 0) });
        Assert.That(result, Is.Empty);
    }
}

[TestFixture]
public class CurveUtilsTests
{
    [Test]
    public void CalculateHeadings_Vec2_LastPointUsesSecondToLastHeading()
    {
        var points = new List<Vec2>
        {
            new(0, 0), new(0, 10), new(0, 20)
        };

        var result = CurveUtils.CalculateHeadings(points);

        Assert.That(result, Has.Count.EqualTo(3));
        // All heading north (atan2(0,10) = 0)
        Assert.That(result[2].Heading, Is.EqualTo(result[1].Heading).Within(1e-10));
    }

    [Test]
    public void CalculateHeadings_TooFewPoints_ReturnsEmpty()
    {
        var result = CurveUtils.CalculateHeadings(new List<Vec2> { new(0, 0) });
        Assert.That(result, Is.Empty);
    }
}

[TestFixture]
public class GeoCalculationsTests
{
    [Test]
    public void CalculateAreaInHectares_Vec2_UnitSquare100m()
    {
        // 100m x 100m = 10,000 m^2 = 1 hectare
        var square = new List<Vec2>
        {
            new(0, 0), new(100, 0), new(100, 100), new(0, 100)
        };

        double area = GeoCalculations.CalculateAreaInHectares(square);
        Assert.That(area, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void CalculateAreaInHectares_Vec2_TooFewPoints_ReturnsZero()
    {
        var line = new List<Vec2> { new(0, 0), new(10, 0) };
        Assert.That(GeoCalculations.CalculateAreaInHectares(line), Is.EqualTo(0));
    }

    [Test]
    public void CalculateAreaInHectares_Vec2_Null_ReturnsZero()
    {
        Assert.That(GeoCalculations.CalculateAreaInHectares((IReadOnlyList<Vec2>)null!), Is.EqualTo(0));
    }
}
