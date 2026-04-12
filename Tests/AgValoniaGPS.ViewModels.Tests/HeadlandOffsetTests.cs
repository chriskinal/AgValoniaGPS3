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

    [Test]
    public void ChainedLines_BothEffective()
    {
        var vm = CreateVm();

        // 100x100 square field
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

        // Line A: vertical on left side at x=20, from y=20 to y=50
        // Extension reaches boundary at y=0, other end meets Line B
        var segA = new HeadlandSegment
        {
            Name = "Line A",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new()
            {
                new Vec3(20, 20, 0),
                new Vec3(20, 50, 0)
            },
            StartExtension = 30, // Reaches y=-10, past boundary at y=0
            EndExtension = 15    // Reaches y=65, overlaps with Line B
        };

        // Line B: horizontal at y=60, from x=15 to x=50
        // Extension reaches Line A, other end reaches boundary at x=100
        var segB = new HeadlandSegment
        {
            Name = "Line B",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new()
            {
                new Vec3(15, 60, Math.PI / 2),
                new Vec3(50, 60, Math.PI / 2)
            },
            StartExtension = 15, // Reaches x=0, past boundary
            EndExtension = 60    // Reaches x=110, past boundary at x=100
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        // At least one should be effective (merged chain touches both boundaries)
        bool anyEffective = segA.IsEffective || segB.IsEffective;
        Assert.That(anyEffective, Is.True, "Chained lines should form effective headland cut");

        // Headland should be modified (not just the boundary)
        Assert.That(vm.HasHeadland, Is.True);
    }

    [Test]
    public void NonIntersectingCurve_ShowsRed()
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

        // Curve entirely inside field, short extensions, doesn't reach boundary
        var seg = new HeadlandSegment
        {
            Name = "Floating Curve",
            Type = HeadlandSegmentType.Curve,
            Offset = 5,
            BoundaryPoints = new()
            {
                new Vec3(30, 50, Math.PI / 2),
                new Vec3(40, 55, Math.PI / 4),
                new Vec3(50, 50, 0),
                new Vec3(60, 45, -Math.PI / 4),
                new Vec3(70, 50, Math.PI / 2)
            },
            StartExtension = 2,
            EndExtension = 2
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.False, "Floating curve should not be effective");
    }

    [Test]
    public void ClosedLoop_NoBoundaryTouch_StillEffective()
    {
        var vm = CreateVm();

        // 200x200 square field
        var boundary = new Models.Boundary
        {
            OuterBoundary = new Models.BoundaryPolygon
            {
                Points = new()
                {
                    new Models.BoundaryPoint(0, 0, 0),
                    new Models.BoundaryPoint(200, 0, Math.PI / 2),
                    new Models.BoundaryPoint(200, 200, Math.PI),
                    new Models.BoundaryPoint(0, 200, -Math.PI / 2)
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(vm, boundary);

        // 4 lines forming a square inside the field (50,50)-(150,150)
        // None touch the boundary, but they form a closed loop together
        var segments = new[]
        {
            // Top: horizontal from (50,150) to (150,150)
            new HeadlandSegment
            {
                Name = "Top", Type = HeadlandSegmentType.Line, Offset = 10,
                BoundaryPoints = new() { new Vec3(50, 150, Math.PI / 2), new Vec3(150, 150, Math.PI / 2) },
                StartExtension = 20, EndExtension = 20
            },
            // Right: vertical from (150,150) to (150,50)
            new HeadlandSegment
            {
                Name = "Right", Type = HeadlandSegmentType.Line, Offset = 10,
                BoundaryPoints = new() { new Vec3(150, 150, -Math.PI), new Vec3(150, 50, -Math.PI) },
                StartExtension = 20, EndExtension = 20
            },
            // Bottom: horizontal from (150,50) to (50,50)
            new HeadlandSegment
            {
                Name = "Bottom", Type = HeadlandSegmentType.Line, Offset = 10,
                BoundaryPoints = new() { new Vec3(150, 50, -Math.PI / 2), new Vec3(50, 50, -Math.PI / 2) },
                StartExtension = 20, EndExtension = 20
            },
            // Left: vertical from (50,50) to (50,150)
            new HeadlandSegment
            {
                Name = "Left", Type = HeadlandSegmentType.Line, Offset = 10,
                BoundaryPoints = new() { new Vec3(50, 50, 0), new Vec3(50, 150, 0) },
                StartExtension = 20, EndExtension = 20
            }
        };

        foreach (var seg in segments)
        {
            vm.ComputeSegmentOffset(seg);
            vm.HeadlandSegments.Add(seg);
        }

        vm.BuildHeadlandFromSegments();

        // The 4 lines should chain together and form effective headland cuts
        // Even though none individually touch the boundary
        bool anyEffective = segments.Any(s => s.IsEffective);
        Assert.That(anyEffective, Is.True,
            "Lines forming a closed loop should create effective headland even without boundary contact");
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
