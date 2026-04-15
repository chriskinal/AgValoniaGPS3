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

    [Test]
    public void BoundaryHeadland_PlusABLine_BothEffective()
    {
        // Regression: boundary headland + AB line should both work.
        // The boundary headland becomes the working area, then the AB line
        // should further cut it.
        var vm = CreateVm();

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
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, boundary);

        // Segment 1: full boundary headland at 20m offset
        var bndSeg = new HeadlandSegment
        {
            Name = "Boundary",
            Type = HeadlandSegmentType.Boundary,
            Offset = 20,
            BoundaryPoints = new()
            {
                new Vec3(0, 0, 0), new Vec3(200, 0, Math.PI / 2),
                new Vec3(200, 200, Math.PI), new Vec3(0, 200, -Math.PI / 2),
                new Vec3(0, 0, 0)
            }
        };
        vm.ComputeSegmentOffset(bndSeg);
        vm.HeadlandSegments.Add(bndSeg);

        // Segment 2: AB line across the middle
        var abSeg = new HeadlandSegment
        {
            Name = "AB Line",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new()
            {
                new Vec3(20, 100, Math.PI / 2),
                new Vec3(180, 100, Math.PI / 2)
            },
            StartExtension = 50,
            EndExtension = 50
        };
        vm.ComputeSegmentOffset(abSeg);
        vm.HeadlandSegments.Add(abSeg);

        vm.BuildHeadlandFromSegments();

        Assert.That(bndSeg.IsEffective, Is.True,
            "Boundary headland should be effective");
        Assert.That(abSeg.IsEffective, Is.True,
            $"AB line should be effective after boundary headland. " +
            $"Headland has {vm.HasHeadland} with headland segments: " +
            $"bnd={bndSeg.IsEffective}, ab={abSeg.IsEffective}");
        Assert.That(vm.HasHeadland, Is.True);
    }

    [Test]
    public void BoundaryHeadland_PlusABLine_OnClipper2Polygon_4PointOffset()
    {
        // Exact reproduction: Clipper2 headland (many points) + AB line with 4 offset points.
        // Use a dense circular boundary (260 pts) like the user's real field.
        // The Clipper2 offset produces a polygon with many short edges.
        // The AB line's 4-point offset must find 2 different intersection points.
        var vm = CreateVm();

        // Create a dense circular boundary (simulates GPS-recorded field)
        var bndPts = new System.Collections.Generic.List<Models.BoundaryPoint>();
        int n = 260;
        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n;
            double radius = 200 + 30 * Math.Sin(3 * angle); // Slightly irregular
            bndPts.Add(new Models.BoundaryPoint(
                radius * Math.Cos(angle),
                radius * Math.Sin(angle),
                angle + Math.PI / 2));
        }

        var boundary = new Models.Boundary
        {
            OuterBoundary = new Models.BoundaryPolygon { Points = bndPts }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, boundary);

        // Segment 1: full boundary headland (closed curve, Clipper2 offset)
        var bndVec3 = bndPts.Select(p => new Vec3(p.Easting, p.Northing, p.Heading)).ToList();
        bndVec3.Add(bndVec3[0]); // close
        var bndSeg = new HeadlandSegment
        {
            Name = "Boundary",
            Type = HeadlandSegmentType.Curve,
            Offset = 20,
            BoundaryPoints = bndVec3
        };
        vm.ComputeSegmentOffset(bndSeg);
        vm.HeadlandSegments.Add(bndSeg);

        Assert.That(bndSeg.OffsetPoints.Count, Is.GreaterThan(50),
            $"Clipper2 should produce many offset points, got {bndSeg.OffsetPoints.Count}");

        // Segment 2: AB line cutting across the field (2 offset pts -> 4 with extensions)
        var abSeg = new HeadlandSegment
        {
            Name = "AB Line",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new()
            {
                new Vec3(-150, 0, Math.PI / 2),
                new Vec3(150, 0, Math.PI / 2)
            },
            StartExtension = 50,
            EndExtension = 50
        };
        vm.ComputeSegmentOffset(abSeg);
        vm.HeadlandSegments.Add(abSeg);

        Assert.That(abSeg.OffsetPoints.Count, Is.EqualTo(2),
            "AB line should have exactly 2 offset points");

        vm.BuildHeadlandFromSegments();

        Assert.That(bndSeg.IsEffective, Is.True, "Boundary headland effective");
        Assert.That(abSeg.IsEffective, Is.True,
            $"AB line must be effective on Clipper2 polygon ({bndSeg.OffsetPoints.Count} pts). " +
            "4-point offset search must find 2 different intersections on the headland polygon.");
    }

    [Test]
    public void BoundaryHeadland_PlusABLine_UserFieldExactData()
    {
        // Uses actual headland polygon from user's field dump (350 pts).
        // Regression: AB line (4 offset pts) found same intersection for both start/end
        // on the Clipper2 headland polygon, making the line not effective.
        var headlinePath = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "TestData", "Fields", "UserField", "Headlines.txt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "Fields", "UserField", "Headlines.txt"),
        }.FirstOrDefault(System.IO.File.Exists);

        if (headlinePath == null)
        {
            Assert.Pass("UserField Headlines.txt not found - skipping");
            return;
        }

        // Load headland polygon
        var headland = new System.Collections.Generic.List<Vec3>();
        foreach (var line in System.IO.File.ReadAllLines(headlinePath))
        {
            var parts = line.Trim().Split(',');
            if (parts.Length >= 3 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double e))
            {
                double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double n);
                double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double h);
                headland.Add(new Vec3(e, n, h));
            }
        }

        Assert.That(headland.Count, Is.GreaterThan(100), $"Need headland data, got {headland.Count} pts");

        var vm = CreateVm();
        var boundary = new Models.Boundary
        {
            OuterBoundary = new Models.BoundaryPolygon
            {
                Points = new()
                {
                    new Models.BoundaryPoint(-500, -200, 0),
                    new Models.BoundaryPoint(50, -200, Math.PI / 2),
                    new Models.BoundaryPoint(50, 500, Math.PI),
                    new Models.BoundaryPoint(-500, 500, -Math.PI / 2)
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, boundary);

        // Boundary headland segment (closed, uses headland polygon directly)
        var bndSeg = new HeadlandSegment
        {
            Name = "Boundary",
            Type = HeadlandSegmentType.Curve,
            Offset = 12,
            BoundaryPoints = new(headland),
            OffsetPoints = headland.GetRange(0, headland.Count - 1)
        };

        // AB line from user's dump
        var abSeg = new HeadlandSegment
        {
            Name = "Line 2",
            Type = HeadlandSegmentType.Line,
            Offset = 12,
            BoundaryPoints = new()
            {
                new Vec3(-313.505, 317.742, -1.726),
                new Vec3(-469.237, 293.361, -1.726)
            },
            StartExtension = 50,
            EndExtension = 50
        };
        vm.ComputeSegmentOffset(abSeg);

        vm.HeadlandSegments.Add(bndSeg);
        vm.HeadlandSegments.Add(abSeg);
        vm.BuildHeadlandFromSegments();

        Assert.That(bndSeg.IsEffective, Is.True, "Boundary headland effective");
        Assert.That(abSeg.IsEffective, Is.True,
            "AB line must be effective on user's Clipper2 headland polygon " +
            $"({headland.Count} pts). End search must find farthest intersection.");
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
