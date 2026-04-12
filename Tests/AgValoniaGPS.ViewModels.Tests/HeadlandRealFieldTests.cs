using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;

namespace AgValoniaGPS.ViewModels.Tests;

/// <summary>
/// Tests using real field data from user bug reports.
/// Field: ~22.58 ha, 621 boundary points, irregular shape with notch at top.
/// </summary>
[TestFixture]
public class HeadlandRealFieldTests
{
    private MainViewModel _vm = null!;

    [SetUp]
    public void SetUp()
    {
        _vm = new MainViewModelBuilder().Build();

        // Load the real boundary from the dump
        // Simplified to key corner points that define the field shape
        var boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = new()
                {
                    // Approximate corners from the 621-point boundary
                    new BoundaryPoint(1.1, 82.6, 1.2),       // 0: top-right area
                    new BoundaryPoint(65.9, -31.8, 3.1),      // ~97: right side
                    new BoundaryPoint(71.5, -205.1, 3.1),     // ~128: right-bottom
                    new BoundaryPoint(67.9, -93.9, -2.3),     // ~107: mid-right
                    new BoundaryPoint(-75.5, -390.8, 4.86),   // ~261: bottom
                    new BoundaryPoint(-230.2, -367.5, -2.3),  // ~286: bottom-left
                    new BoundaryPoint(-418.0, -14.5, 0.39),   // ~412: left side
                    new BoundaryPoint(-300.6, 41.5, 1.63),    // ~461: upper-left
                    new BoundaryPoint(-175.7, 34.0, 1.63),    // ~481: upper-middle
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_vm, boundary);
    }

    [Test]
    public void Line3_RightSide_TouchesBoundary()
    {
        // Line 3: vertical on right side
        var seg = new HeadlandSegment
        {
            Name = "Line 3",
            Type = HeadlandSegmentType.Line,
            Offset = 12,
            BoundaryPoints = new()
            {
                new Vec3(65.9, -31.8, 3.1095),
                new Vec3(71.5, -205.1, 3.1095)
            },
            StartExtension = 50,
            EndExtension = 50
        };

        _vm.ComputeSegmentOffset(seg);
        _vm.HeadlandSegments.Add(seg);
        _vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True,
            $"Line 3 should intersect boundary at both ends. Offset pts: {seg.OffsetPoints.Count}");
    }

    [Test]
    public void Line4_Diagonal_TouchesBoundary()
    {
        // Line 4: diagonal from right to bottom-left
        var seg = new HeadlandSegment
        {
            Name = "Line 4",
            Type = HeadlandSegmentType.Line,
            Offset = 12,
            BoundaryPoints = new()
            {
                new Vec3(67.9, -93.9, -2.3132),
                new Vec3(-230.2, -367.5, -2.3132)
            },
            StartExtension = 50,
            EndExtension = 50
        };

        _vm.ComputeSegmentOffset(seg);
        _vm.HeadlandSegments.Add(seg);
        _vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True,
            $"Line 4 diagonal should intersect boundary at both ends");
    }

    [Test]
    public void AllFourSegments_ChainTogether()
    {
        // Add all 4 segments from the user's field
        var segments = new HeadlandSegment[]
        {
            new()
            {
                Name = "Line 4 (diagonal)",
                Type = HeadlandSegmentType.Line,
                Offset = 12,
                BoundaryPoints = new()
                {
                    new Vec3(67.9, -93.9, -2.3132),
                    new Vec3(-230.2, -367.5, -2.3132)
                },
                StartExtension = 50,
                EndExtension = 50
            },
            new()
            {
                Name = "Line 3 (right)",
                Type = HeadlandSegmentType.Line,
                Offset = 12,
                BoundaryPoints = new()
                {
                    new Vec3(65.9, -31.8, 3.1095),
                    new Vec3(71.5, -205.1, 3.1095)
                },
                StartExtension = 50,
                EndExtension = 50
            },
            new()
            {
                Name = "Line 4 (upper-left)",
                Type = HeadlandSegmentType.Line,
                Offset = 12,
                BoundaryPoints = new()
                {
                    new Vec3(-175.7, 34.0, 1.6304),
                    new Vec3(-300.6, 41.5, 1.6304)
                },
                StartExtension = 50,
                EndExtension = 50
            },
            new()
            {
                Name = "Curve 4 (bottom-left)",
                Type = HeadlandSegmentType.Curve,
                Offset = 32,
                BoundaryPoints = new()
                {
                    new Vec3(-75.5, -390.8, 4.86324),
                    new Vec3(-230.2, -367.5, 4.86324),
                    new Vec3(-418.0, -14.5, 0.39020)
                },
                StartExtension = 50,
                EndExtension = 50
            }
        };

        foreach (var seg in segments)
        {
            _vm.ComputeSegmentOffset(seg);
            _vm.HeadlandSegments.Add(seg);
        }

        _vm.BuildHeadlandFromSegments();

        int effectiveCount = segments.Count(s => s.IsEffective);
        Assert.That(effectiveCount, Is.GreaterThan(0),
            $"At least some segments should be effective. " +
            $"Effective: {string.Join(", ", segments.Select(s => $"{s.Name}={s.IsEffective}"))}");
    }

    [Test]
    public void TwoLinesConnecting_FormChain()
    {
        // Line 3 (right side) and Line 4 (diagonal) should connect
        // Line 3 goes from upper-right to lower-right
        // Line 4 goes from mid-right to bottom-left
        // They should intersect near the right side
        var seg1 = new HeadlandSegment
        {
            Name = "Line 3",
            Type = HeadlandSegmentType.Line,
            Offset = 12,
            BoundaryPoints = new()
            {
                new Vec3(65.9, -31.8, 3.1095),
                new Vec3(71.5, -205.1, 3.1095)
            },
            StartExtension = 50,
            EndExtension = 50
        };

        var seg2 = new HeadlandSegment
        {
            Name = "Line 4",
            Type = HeadlandSegmentType.Line,
            Offset = 12,
            BoundaryPoints = new()
            {
                new Vec3(67.9, -93.9, -2.3132),
                new Vec3(-230.2, -367.5, -2.3132)
            },
            StartExtension = 50,
            EndExtension = 50
        };

        _vm.ComputeSegmentOffset(seg1);
        _vm.ComputeSegmentOffset(seg2);

        // Check if offset lines actually intersect each other
        bool linesIntersect = false;
        var line1 = BuildOffsetWithExt(seg1);
        var line2 = BuildOffsetWithExt(seg2);

        for (int i = 0; i < line1.Count - 1 && !linesIntersect; i++)
        {
            for (int j = 0; j < line2.Count - 1 && !linesIntersect; j++)
            {
                double denom = (line1[i + 1].Easting - line1[i].Easting) * (line2[j + 1].Northing - line2[j].Northing) -
                               (line1[i + 1].Northing - line1[i].Northing) * (line2[j + 1].Easting - line2[j].Easting);
                if (Math.Abs(denom) < 1e-10) continue;
                double t = ((line2[j].Easting - line1[i].Easting) * (line2[j + 1].Northing - line2[j].Northing) -
                            (line2[j].Northing - line1[i].Northing) * (line2[j + 1].Easting - line2[j].Easting)) / denom;
                double u = ((line2[j].Easting - line1[i].Easting) * (line1[i + 1].Northing - line1[i].Northing) -
                            (line2[j].Northing - line1[i].Northing) * (line1[i + 1].Easting - line1[i].Easting)) / denom;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
                    linesIntersect = true;
            }
        }

        Assert.That(linesIntersect, Is.True,
            $"Line 3 offset ({line1.Count} pts) and Line 4 offset ({line2.Count} pts) should intersect each other");
    }

    [Test]
    public void LongCurve_EndIntersectsBoundary()
    {
        // Reproduces bug: long curve where end search went backward
        // and couldn't find boundary intersection near the tip
        var vm = new MainViewModelBuilder().Build();

        // Simple 200x200 square
        var boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = new()
                {
                    new BoundaryPoint(0, 0, 0),
                    new BoundaryPoint(200, 0, Math.PI / 2),
                    new BoundaryPoint(200, 200, Math.PI),
                    new BoundaryPoint(0, 200, -Math.PI / 2)
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, boundary);

        // Long curve along the bottom + right edges (many points)
        var curvePts = new List<Vec3>();
        // Bottom edge: x from 10 to 190 (many points)
        for (int i = 0; i <= 100; i++)
            curvePts.Add(new Vec3(10 + i * 1.8, 0, Math.PI / 2));
        // Right edge: y from 0 to 190
        for (int i = 1; i <= 100; i++)
            curvePts.Add(new Vec3(200, i * 1.9, 0));

        var seg = new HeadlandSegment
        {
            Name = "Long L-Curve",
            Type = HeadlandSegmentType.Curve,
            Offset = 15,
            BoundaryPoints = curvePts,
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg);
        Assert.That(seg.OffsetPoints.Count, Is.GreaterThan(100),
            "Should have many offset points");

        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True,
            $"Long curve should be effective - ends near boundary edges. " +
            $"OffsetPts: {seg.OffsetPoints.Count}");
    }

    private static List<Vec3> BuildOffsetWithExt(HeadlandSegment seg)
    {
        var line = new List<Vec3>();
        if (seg.StartExtension > 0 && seg.OffsetPoints.Count >= 2)
        {
            var s0 = seg.OffsetPoints[0]; var s1 = seg.OffsetPoints[1];
            double sdx = s0.Easting - s1.Easting, sdy = s0.Northing - s1.Northing;
            double slen = Math.Sqrt(sdx * sdx + sdy * sdy);
            if (slen > 0.01)
                line.Add(new Vec3(s0.Easting + sdx / slen * seg.StartExtension, s0.Northing + sdy / slen * seg.StartExtension, 0));
        }
        line.AddRange(seg.OffsetPoints);
        if (seg.EndExtension > 0 && seg.OffsetPoints.Count >= 2)
        {
            var e0 = seg.OffsetPoints[^2]; var e1 = seg.OffsetPoints[^1];
            double edx = e1.Easting - e0.Easting, edy = e1.Northing - e0.Northing;
            double elen = Math.Sqrt(edx * edx + edy * edy);
            if (elen > 0.01)
                line.Add(new Vec3(e1.Easting + edx / elen * seg.EndExtension, e1.Northing + edy / elen * seg.EndExtension, 0));
        }
        return line;
    }
}
