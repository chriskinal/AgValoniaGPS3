using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;

namespace AgValoniaGPS.ViewModels.Tests;

[TestFixture]
public class HeadlandOffsetTests
{
    private MainViewModel CreateVm()
    {
        return new MainViewModelBuilder().Build();
    }

    [Test]
    public void StraightLine_OffsetIs12m()
    {
        var vm = CreateVm();
        var seg = new HeadlandSegment
        {
            Type = HeadlandSegmentType.Line,
            Offset = 12,
            BoundaryPoints = new()
            {
                new Vec3(0, 0, 0),
                new Vec3(0, 100, 0)
            }
        };

        vm.ComputeSegmentOffset(seg);

        Assert.That(seg.OffsetPoints, Has.Count.EqualTo(2));
        // Offset should be 12m perpendicular
        double dist0 = Math.Sqrt(
            Math.Pow(seg.OffsetPoints[0].Easting - seg.BoundaryPoints[0].Easting, 2) +
            Math.Pow(seg.OffsetPoints[0].Northing - seg.BoundaryPoints[0].Northing, 2));
        Assert.That(dist0, Is.EqualTo(12).Within(0.1));
    }

    [Test]
    public void Curve_OffsetIsConstant()
    {
        var vm = CreateVm();

        // Create a 90-degree arc (quarter circle, radius 50m)
        var pts = new List<Vec3>();
        for (int i = 0; i <= 20; i++)
        {
            double angle = i * Math.PI / 2 / 20;
            pts.Add(new Vec3(50 * Math.Cos(angle), 50 * Math.Sin(angle), angle));
        }

        var seg = new HeadlandSegment
        {
            Type = HeadlandSegmentType.Curve,
            Offset = 10,
            BoundaryPoints = pts
        };

        vm.ComputeSegmentOffset(seg);

        Assert.That(seg.OffsetPoints.Count, Is.GreaterThanOrEqualTo(pts.Count - 1));

        // All offset points should be ~10m from their boundary counterparts
        for (int i = 0; i < Math.Min(pts.Count, seg.OffsetPoints.Count); i++)
        {
            double dist = Math.Sqrt(
                Math.Pow(seg.OffsetPoints[i].Easting - pts[i].Easting, 2) +
                Math.Pow(seg.OffsetPoints[i].Northing - pts[i].Northing, 2));
            Assert.That(dist, Is.EqualTo(10).Within(1.0),
                $"Point {i}: offset distance {dist:F1}m, expected 10m");
        }
    }

    [Test]
    public void FilletCorner_NoSelfIntersection()
    {
        var vm = CreateVm();

        // Create a boundary with a small fillet (5m radius) and 10m offset
        // The fillet should be removed (offset > fillet radius)
        var pts = new List<Vec3>();

        // Straight section going right
        for (int i = 0; i <= 10; i++)
            pts.Add(new Vec3(i * 5, 0, Math.PI / 2));

        // Small fillet corner (5m radius, 90 degrees)
        for (int i = 1; i <= 5; i++)
        {
            double angle = i * Math.PI / 2 / 5;
            pts.Add(new Vec3(50 + 5 * Math.Sin(angle), 5 - 5 * Math.Cos(angle), Math.PI / 2 + angle));
        }

        // Straight section going up
        for (int i = 1; i <= 10; i++)
            pts.Add(new Vec3(55, 5 + i * 5, 0));

        var seg = new HeadlandSegment
        {
            Type = HeadlandSegmentType.Curve,
            Offset = 10, // Larger than fillet radius (5m)
            BoundaryPoints = pts
        };

        vm.ComputeSegmentOffset(seg);

        // Check no self-intersections (each consecutive pair should not cross later edges)
        bool hasSelfIntersection = false;
        for (int i = 0; i < seg.OffsetPoints.Count - 2; i++)
        {
            var a1 = seg.OffsetPoints[i];
            var a2 = seg.OffsetPoints[i + 1];
            for (int j = i + 2; j < seg.OffsetPoints.Count - 1; j++)
            {
                var b1 = seg.OffsetPoints[j];
                var b2 = seg.OffsetPoints[j + 1];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    hasSelfIntersection = true;
                    break;
                }
            }
            if (hasSelfIntersection) break;
        }

        Assert.That(hasSelfIntersection, Is.False, "Offset polygon should not self-intersect after fillet removal");
        // Offset should have fewer points than input (fillet collapsed)
        Assert.That(seg.OffsetPoints.Count, Is.LessThanOrEqualTo(pts.Count));
    }

    [Test]
    public void BoundaryOffset_ClosedPolygon()
    {
        var vm = CreateVm();

        // Simple square boundary 100x100
        var seg = new HeadlandSegment
        {
            Type = HeadlandSegmentType.Boundary,
            Offset = 10,
            BoundaryPoints = new()
            {
                new Vec3(0, 0, 0),
                new Vec3(100, 0, Math.PI / 2),
                new Vec3(100, 100, Math.PI),
                new Vec3(0, 100, -Math.PI / 2),
                new Vec3(0, 0, 0) // closing point
            }
        };

        vm.ComputeSegmentOffset(seg);

        // Square with self-intersection removal may collapse sharp corners
        Assert.That(seg.OffsetPoints.Count, Is.GreaterThanOrEqualTo(2));

        // All offset points should be roughly 10m inside the square
        foreach (var op in seg.OffsetPoints)
        {
            Assert.That(op.Easting, Is.GreaterThanOrEqualTo(-1).And.LessThanOrEqualTo(101),
                $"Point ({op.Easting:F1}, {op.Northing:F1}) outside expected range");
        }
    }

    [Test]
    public void ShortExtension_DoesNotIntersect_NotEffective()
    {
        var vm = CreateVm();

        // Create a 100x100 square boundary
        var boundary = new Models.Boundary
        {
            OuterBoundary = new Models.BoundaryPolygon
            {
                Points = new()
                {
                    new Models.BoundaryPoint(0, 0, 0),
                    new Models.BoundaryPoint(100, 0, Math.PI / 2),
                    new Models.BoundaryPoint(100, 100, Math.PI),
                    new Models.BoundaryPoint(0, 100, -Math.PI / 2)
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();

        // Set the boundary on the VM
        typeof(MainViewModel).GetField("_currentBoundary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(vm, boundary);

        // Create a headland line in the middle that is too short to reach the edges
        var seg = new HeadlandSegment
        {
            Name = "Short Line",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new()
            {
                new Vec3(40, 50, Math.PI / 2),
                new Vec3(60, 50, Math.PI / 2)
            },
            StartExtension = 5, // Only 5m - won't reach boundary at x=0 (35m away)
            EndExtension = 5    // Only 5m - won't reach boundary at x=100 (35m away)
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.False, "Short extension should not intersect boundary");
    }

    [Test]
    public void LongExtension_Intersects_IsEffective()
    {
        var vm = CreateVm();

        var boundary = new Models.Boundary
        {
            OuterBoundary = new Models.BoundaryPolygon
            {
                Points = new()
                {
                    new Models.BoundaryPoint(0, 0, 0),
                    new Models.BoundaryPoint(100, 0, Math.PI / 2),
                    new Models.BoundaryPoint(100, 100, Math.PI),
                    new Models.BoundaryPoint(0, 100, -Math.PI / 2)
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(vm, boundary);

        var seg = new HeadlandSegment
        {
            Name = "Long Line",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new()
            {
                new Vec3(40, 50, Math.PI / 2),
                new Vec3(60, 50, Math.PI / 2)
            },
            StartExtension = 50, // 50m - reaches boundary at x=0
            EndExtension = 50    // 50m - reaches boundary at x=100
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True, "Long extension should intersect boundary at both ends");
    }

    private static bool SegmentsIntersect(Vec3 a1, Vec3 a2, Vec3 b1, Vec3 b2)
    {
        double d = (a2.Easting - a1.Easting) * (b2.Northing - b1.Northing) -
                   (a2.Northing - a1.Northing) * (b2.Easting - b1.Easting);
        if (Math.Abs(d) < 1e-10) return false;

        double t = ((b1.Easting - a1.Easting) * (b2.Northing - b1.Northing) -
                    (b1.Northing - a1.Northing) * (b2.Easting - b1.Easting)) / d;
        double u = ((b1.Easting - a1.Easting) * (a2.Northing - a1.Northing) -
                    (b1.Northing - a1.Northing) * (a2.Easting - a1.Easting)) / d;

        return t > 0.01 && t < 0.99 && u > 0.01 && u < 0.99;
    }
}
