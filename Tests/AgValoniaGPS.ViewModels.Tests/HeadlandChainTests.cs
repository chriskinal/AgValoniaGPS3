using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;

namespace AgValoniaGPS.ViewModels.Tests;

/// <summary>
/// Tests for chained/multiple headland segment configurations.
/// Focus areas: chain merging, multi-line boundary intersection, polygon splitting.
/// </summary>
[TestFixture]
public class HeadlandChainTests
{
    /// <summary>
    /// Creates a VM with a rectangular boundary.
    /// </summary>
    private static MainViewModel CreateVmWithBoundary(double width = 200, double height = 200)
    {
        var vm = new MainViewModelBuilder().Build();
        var boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = new()
                {
                    new BoundaryPoint(0, 0, 0),
                    new BoundaryPoint(width, 0, Math.PI / 2),
                    new BoundaryPoint(width, height, Math.PI),
                    new BoundaryPoint(0, height, -Math.PI / 2)
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, boundary);
        return vm;
    }

    /// <summary>
    /// Helper: get the headland polygon after building.
    /// </summary>
    private static List<Vec3>? GetHeadlandLine(MainViewModel vm)
    {
        return (List<Vec3>?)typeof(MainViewModel).GetField("_currentHeadlandLine",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(vm);
    }

    // ---------------------------------------------------------------
    // TEST GROUP 1: Two lines forming an L-shape
    // ---------------------------------------------------------------

    [Test]
    public void TwoLines_LShape_BottomAndRight_BothEffective()
    {
        // Line A: along bottom edge (horizontal), touches left and right boundary
        // Line B: along right edge (vertical), touches bottom and top boundary
        // Both individually reach boundary on both ends -> no chaining needed
        var vm = CreateVmWithBoundary(200, 200);

        var segA = new HeadlandSegment
        {
            Name = "Bottom Line",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(40, 10, Math.PI / 2), new Vec3(160, 10, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };
        var segB = new HeadlandSegment
        {
            Name = "Right Line",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(190, 40, 0), new Vec3(190, 160, 0) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        Assert.That(segA.IsEffective, Is.True, "Bottom line should be effective (both ends reach boundary)");
        Assert.That(segB.IsEffective, Is.True, "Right line should be effective (both ends reach boundary)");

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        // Headland should be smaller than the full boundary (two cuts applied)
        double headArea = Math.Abs(CalculateArea(headland!));
        Assert.That(headArea, Is.LessThan(200 * 200), "Headland should be smaller than full boundary");
    }

    [Test]
    public void TwoLines_LShape_ChainedOnly_MergesAndCuts()
    {
        // Line A: vertical, only reaches bottom boundary, other end meets Line B
        // Line B: horizontal, only reaches right boundary, other end meets Line A
        // They must chain together to cut the headland
        var vm = CreateVmWithBoundary(200, 200);

        // Line A: vertical at x=30, from y=10 to y=80
        // Start extension reaches y=0 (bottom boundary), end extension reaches y~95
        var segA = new HeadlandSegment
        {
            Name = "Line A (vertical)",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(30, 10, 0), new Vec3(30, 80, 0) },
            StartExtension = 20,
            EndExtension = 30
        };

        // Line B: horizontal at y=90, from x=20 to x=180
        // Start extension reaches x~0 (overlaps with Line A), end extension reaches x=200 (right boundary)
        var segB = new HeadlandSegment
        {
            Name = "Line B (horizontal)",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(20, 90, Math.PI / 2), new Vec3(180, 90, Math.PI / 2) },
            StartExtension = 30,
            EndExtension = 30
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        // The merged chain should be effective
        bool anyEffective = segA.IsEffective || segB.IsEffective;
        Assert.That(anyEffective, Is.True,
            $"L-shaped chain should be effective. A={segA.IsEffective}, B={segB.IsEffective}");
        Assert.That(vm.HasHeadland, Is.True);
    }

    // ---------------------------------------------------------------
    // TEST GROUP 2: Two parallel lines (no chaining)
    // ---------------------------------------------------------------

    [Test]
    public void TwoParallelLines_IndependentCuts()
    {
        // Two horizontal lines that both reach left and right boundary independently
        var vm = CreateVmWithBoundary(200, 200);

        var segA = new HeadlandSegment
        {
            Name = "Lower Line",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(20, 30, Math.PI / 2), new Vec3(180, 30, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };
        var segB = new HeadlandSegment
        {
            Name = "Upper Line",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(20, 170, Math.PI / 2), new Vec3(180, 170, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        Assert.That(segA.IsEffective, Is.True, "Lower parallel line should be effective");
        Assert.That(segB.IsEffective, Is.True, "Upper parallel line should be effective");

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        // Both lines cut the boundary, headland should be smaller
        double headArea = Math.Abs(CalculateArea(headland!));
        Assert.That(headArea, Is.LessThan(200 * 200 * 0.8),
            "Two parallel cuts should reduce area significantly");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 3: Chain merge direction - A's boundary end is at END (not start)
    // ---------------------------------------------------------------

    [Test]
    public void ChainMerge_AEndTouchesBoundary_StillWorks()
    {
        // Line A: horizontal, right end reaches right boundary, left end meets Line B
        // Line B: vertical, bottom end reaches bottom boundary, top end meets Line A
        // After merge, chain should go: B_bottom_boundary -> B -> intersection -> A -> A_right_boundary
        var vm = CreateVmWithBoundary(200, 200);

        // Line A: horizontal at y=50, from x=50 to x=170
        // Both extensions long enough: start reaches past left boundary, end reaches right boundary
        // After merge, the combined chain should use A's far-end to reach right boundary
        var segA = new HeadlandSegment
        {
            Name = "Line A (right-reaching)",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(50, 50, Math.PI / 2), new Vec3(170, 50, Math.PI / 2) },
            StartExtension = 80,
            EndExtension = 50
        };

        // Line B: vertical at x=40, from y=10 to y=60
        // StartExtension reaches past bottom boundary, EndExtension meets Line A
        var segB = new HeadlandSegment
        {
            Name = "Line B (bottom-reaching)",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(40, 10, 0), new Vec3(40, 60, 0) },
            StartExtension = 30,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        bool anyEffective = segA.IsEffective || segB.IsEffective;
        Assert.That(anyEffective, Is.True,
            $"Chain where A's END touches boundary should work. A={segA.IsEffective}, B={segB.IsEffective}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 4: Three lines forming a U-shape
    // ---------------------------------------------------------------

    [Test]
    public void ThreeLines_UShape_FullChain()
    {
        // Line A: vertical on left, touches top boundary, meets Line B at bottom
        // Line B: horizontal at bottom, connects A and C
        // Line C: vertical on right, touches top boundary, meets Line B at bottom
        // Together they form a U that cuts off the bottom of the field
        var vm = CreateVmWithBoundary(200, 200);

        var segA = new HeadlandSegment
        {
            Name = "Left Vertical",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(30, 190, 0), new Vec3(30, 40, 0) },
            StartExtension = 20, // Reaches y=210, past top boundary
            EndExtension = 30    // Reaches y=10, meets Line B
        };
        var segB = new HeadlandSegment
        {
            Name = "Bottom Horizontal",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(25, 30, Math.PI / 2), new Vec3(175, 30, Math.PI / 2) },
            StartExtension = 20,  // Meets Line A
            EndExtension = 20     // Meets Line C
        };
        var segC = new HeadlandSegment
        {
            Name = "Right Vertical",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(170, 40, 0), new Vec3(170, 190, 0) },
            StartExtension = 30,  // Reaches y=10, meets Line B
            EndExtension = 20     // Reaches y=210, past top boundary
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.ComputeSegmentOffset(segC);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.HeadlandSegments.Add(segC);
        vm.BuildHeadlandFromSegments();

        bool anyEffective = segA.IsEffective || segB.IsEffective || segC.IsEffective;
        Assert.That(anyEffective, Is.True,
            $"U-shape (3 lines) should form effective chain. " +
            $"A={segA.IsEffective}, B={segB.IsEffective}, C={segC.IsEffective}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 5: Two lines at acute angle (V-shape)
    // ---------------------------------------------------------------

    [Test]
    public void TwoLines_VShape_AcuteAngle()
    {
        // Two lines meeting at a sharp angle at the bottom,
        // each reaching boundary on their other end
        var vm = CreateVmWithBoundary(200, 200);

        // Line A: from left boundary area down to bottom-center
        var segA = new HeadlandSegment
        {
            Name = "Left Arm",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(10, 100, 0), new Vec3(90, 30, 0) },
            StartExtension = 50,
            EndExtension = 50
        };

        // Line B: from bottom-center up to right boundary area
        var segB = new HeadlandSegment
        {
            Name = "Right Arm",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(110, 30, 0), new Vec3(190, 100, 0) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        // Both lines should individually reach boundary, or chain together
        bool anyEffective = segA.IsEffective || segB.IsEffective;
        Assert.That(anyEffective, Is.True,
            $"V-shape lines should be effective. A={segA.IsEffective}, B={segB.IsEffective}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 6: Chain where intersection is in middle of A (not near end)
    // ---------------------------------------------------------------

    [Test]
    public void ChainMerge_IntersectionAtMiddleOfA_PreservesChain()
    {
        // Line A: long line across the field
        // Line B: crosses A at its midpoint
        // After merge, half of A gets cut off - the merged chain should still work
        var vm = CreateVmWithBoundary(200, 200);

        // Line A: diagonal from bottom-left to top-right
        var segA = new HeadlandSegment
        {
            Name = "Diagonal A",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new() { new Vec3(10, 10, Math.PI / 4), new Vec3(190, 190, Math.PI / 4) },
            StartExtension = 20,
            EndExtension = 20
        };

        // Line B: horizontal crossing A near the middle
        var segB = new HeadlandSegment
        {
            Name = "Horizontal B",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new() { new Vec3(10, 100, Math.PI / 2), new Vec3(190, 100, Math.PI / 2) },
            StartExtension = 20,
            EndExtension = 20
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        // Both should individually reach boundary (long extensions + spans full field)
        // If they get merged, the merge shouldn't break them
        Assert.That(segA.IsEffective || segB.IsEffective, Is.True,
            $"Crossing lines should produce at least one effective segment. A={segA.IsEffective}, B={segB.IsEffective}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 7: Two lines each touching boundary on ONE end, meeting each other on the other
    // ---------------------------------------------------------------

    [Test]
    public void TwoLines_EachOneSideBoundary_ChainConnects()
    {
        // Two lines that each touch boundary on one end and meet at an angle in the middle
        // Line A goes from left boundary down to center-bottom
        // Line B goes from center-bottom up to right boundary
        // Together they form a V-shaped chain cutting the bottom of the field
        var vm = CreateVmWithBoundary(200, 200);

        // Line A: from left-center down to bottom-center
        var segA = new HeadlandSegment
        {
            Name = "Left Arm",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(10, 80, 0), new Vec3(90, 30, 0) },
            StartExtension = 50,  // Past left boundary
            EndExtension = 50     // Into field, meets B
        };

        // Line B: from bottom-center up to right-center
        var segB = new HeadlandSegment
        {
            Name = "Right Arm",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(110, 30, 0), new Vec3(190, 80, 0) },
            StartExtension = 50,  // Into field, meets A
            EndExtension = 50     // Past right boundary
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        bool anyEffective = segA.IsEffective || segB.IsEffective;
        Assert.That(anyEffective, Is.True,
            $"V-shaped chain should be effective. A={segA.IsEffective}, B={segB.IsEffective}");

        if (anyEffective)
        {
            var headland = GetHeadlandLine(vm);
            Assert.That(headland, Is.Not.Null);
            double headArea = Math.Abs(CalculateArea(headland!));
            Assert.That(headArea, Is.LessThan(200 * 200 * 0.8),
                $"Chain should cut area. Head area: {headArea:F0}, full: {200 * 200}");
        }
    }

    // ---------------------------------------------------------------
    // TEST GROUP 8: Merged chain still not reaching boundary
    // ---------------------------------------------------------------

    [Test]
    public void TwoLines_ChainedButShort_NotEffective()
    {
        // Two short lines that chain together but the combined result
        // still doesn't reach the boundary on both ends
        var vm = CreateVmWithBoundary(200, 200);

        var segA = new HeadlandSegment
        {
            Name = "Short A",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new() { new Vec3(60, 90, Math.PI / 2), new Vec3(90, 90, Math.PI / 2) },
            StartExtension = 10,  // Reaches x=50, not boundary
            EndExtension = 20     // Reaches x=110, meets B
        };
        var segB = new HeadlandSegment
        {
            Name = "Short B",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new() { new Vec3(100, 90, Math.PI / 2), new Vec3(130, 90, Math.PI / 2) },
            StartExtension = 20,  // Reaches x=80, meets A
            EndExtension = 10     // Reaches x=140, not boundary
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        // Even merged, the chain shouldn't reach boundary on both sides
        bool anyEffective = segA.IsEffective || segB.IsEffective;
        Assert.That(anyEffective, Is.False,
            $"Short chained lines shouldn't reach boundary. A={segA.IsEffective}, B={segB.IsEffective}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 9: Line + Curve chaining
    // ---------------------------------------------------------------

    [Test]
    public void LineAndCurve_ChainTogether()
    {
        // A straight line meets a curve, forming a combined headland cut
        var vm = CreateVmWithBoundary(200, 200);

        // Line: vertical near left side, from y=10 to y=80
        // Start extension long enough to reach bottom boundary after offset
        var segLine = new HeadlandSegment
        {
            Name = "Vertical Line",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(20, 10, 0), new Vec3(20, 80, 0) },
            StartExtension = 50,  // Past bottom boundary
            EndExtension = 50     // Into field, meets curve
        };

        // Curve: quarter circle from left-center toward top
        var curvePts = new List<Vec3>();
        for (int i = 0; i <= 20; i++)
        {
            double angle = i * Math.PI / 2 / 20;
            curvePts.Add(new Vec3(20 + 80 * Math.Sin(angle), 80 + 80 * (1 - Math.Cos(angle)), angle));
        }
        var segCurve = new HeadlandSegment
        {
            Name = "Corner Curve",
            Type = HeadlandSegmentType.Curve,
            Offset = 15,
            BoundaryPoints = curvePts,
            StartExtension = 50,  // Meets line
            EndExtension = 50     // Past top boundary
        };

        vm.ComputeSegmentOffset(segLine);
        vm.ComputeSegmentOffset(segCurve);
        vm.HeadlandSegments.Add(segLine);
        vm.HeadlandSegments.Add(segCurve);
        vm.BuildHeadlandFromSegments();

        bool anyEffective = segLine.IsEffective || segCurve.IsEffective;
        Assert.That(anyEffective, Is.True,
            $"Line+curve chain should be effective. Line={segLine.IsEffective}, Curve={segCurve.IsEffective}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 10: Single long line across entire field
    // ---------------------------------------------------------------

    [Test]
    public void SingleLongLine_CutsFieldInHalf()
    {
        var vm = CreateVmWithBoundary(200, 200);

        var seg = new HeadlandSegment
        {
            Name = "Full Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(10, 100, Math.PI / 2), new Vec3(190, 100, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True, "Line spanning full field should be effective");

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        double headArea = Math.Abs(CalculateArea(headland!));
        double fullArea = 200 * 200;
        // Should cut off roughly bottom ~40% (offset shifts line by 20m toward centroid)
        Assert.That(headArea, Is.LessThan(fullArea * 0.85).And.GreaterThan(fullArea * 0.3),
            $"Area after cut: {headArea:F0}, full: {fullArea:F0}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 11: Two cuts from opposite sides
    // ---------------------------------------------------------------

    [Test]
    public void TwoLines_OpposingSides_BothCut()
    {
        // One line along the bottom, one along the top
        // Both should cut the headland independently
        var vm = CreateVmWithBoundary(200, 200);

        var segBottom = new HeadlandSegment
        {
            Name = "Bottom Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 30,
            BoundaryPoints = new() { new Vec3(10, 10, Math.PI / 2), new Vec3(190, 10, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };
        var segTop = new HeadlandSegment
        {
            Name = "Top Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 30,
            BoundaryPoints = new() { new Vec3(10, 190, Math.PI / 2), new Vec3(190, 190, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(segBottom);
        vm.ComputeSegmentOffset(segTop);
        vm.HeadlandSegments.Add(segBottom);
        vm.HeadlandSegments.Add(segTop);
        vm.BuildHeadlandFromSegments();

        Assert.That(segBottom.IsEffective, Is.True, "Bottom cut should be effective");
        Assert.That(segTop.IsEffective, Is.True, "Top cut should be effective");

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        double headArea = Math.Abs(CalculateArea(headland!));
        double fullArea = 200 * 200;
        // Both sides cut off ~30m each, leaving ~140/200 = 70% of area
        Assert.That(headArea, Is.LessThan(fullArea * 0.8),
            $"Two opposing cuts should reduce area. Area: {headArea:F0}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 12: All four sides (complete headland ring)
    // ---------------------------------------------------------------

    [Test]
    public void FourLines_AllSides_CompleteRing()
    {
        // One line along each edge of the 200x200 field
        // Should produce a smaller square headland inside
        var vm = CreateVmWithBoundary(200, 200);

        var segments = new[]
        {
            new HeadlandSegment
            {
                Name = "Bottom", Type = HeadlandSegmentType.Line, Offset = 25,
                BoundaryPoints = new() { new Vec3(10, 10, Math.PI / 2), new Vec3(190, 10, Math.PI / 2) },
                StartExtension = 50, EndExtension = 50
            },
            new HeadlandSegment
            {
                Name = "Right", Type = HeadlandSegmentType.Line, Offset = 25,
                BoundaryPoints = new() { new Vec3(190, 10, 0), new Vec3(190, 190, 0) },
                StartExtension = 50, EndExtension = 50
            },
            new HeadlandSegment
            {
                Name = "Top", Type = HeadlandSegmentType.Line, Offset = 25,
                BoundaryPoints = new() { new Vec3(190, 190, -Math.PI / 2), new Vec3(10, 190, -Math.PI / 2) },
                StartExtension = 50, EndExtension = 50
            },
            new HeadlandSegment
            {
                Name = "Left", Type = HeadlandSegmentType.Line, Offset = 25,
                BoundaryPoints = new() { new Vec3(10, 190, Math.PI), new Vec3(10, 10, Math.PI) },
                StartExtension = 50, EndExtension = 50
            }
        };

        foreach (var seg in segments)
        {
            vm.ComputeSegmentOffset(seg);
            vm.HeadlandSegments.Add(seg);
        }
        vm.BuildHeadlandFromSegments();

        int effectiveCount = segments.Count(s => s.IsEffective);
        Assert.That(effectiveCount, Is.GreaterThanOrEqualTo(2),
            $"At least 2 of 4 sides should be effective. " +
            $"Results: {string.Join(", ", segments.Select(s => $"{s.Name}={s.IsEffective}"))}");

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        double headArea = Math.Abs(CalculateArea(headland!));
        double fullArea = 200 * 200;
        // Each cut reduces area; with 4 sides and 25m offset, expect significant reduction
        Assert.That(headArea, Is.LessThan(fullArea),
            $"Cuts should reduce area. Area: {headArea:F0}, full: {fullArea:F0}, effective: {effectiveCount}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 13: Headland polygon validity checks
    // ---------------------------------------------------------------

    [Test]
    public void HeadlandPolygon_IsClosed()
    {
        var vm = CreateVmWithBoundary(200, 200);

        var seg = new HeadlandSegment
        {
            Name = "Cut Line",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(10, 100, Math.PI / 2), new Vec3(190, 100, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        Assert.That(headland!.Count, Is.GreaterThanOrEqualTo(4), "Polygon must have at least 4 points");

        // First and last point should be the same (closed polygon)
        double closeDist = Math.Sqrt(
            Math.Pow(headland[0].Easting - headland[^1].Easting, 2) +
            Math.Pow(headland[0].Northing - headland[^1].Northing, 2));
        Assert.That(closeDist, Is.LessThan(0.1),
            $"Polygon should be closed. Gap: {closeDist:F3}m");
    }

    [Test]
    public void HeadlandPolygon_NoSelfIntersection()
    {
        // After a single cut, the resulting polygon should not self-intersect
        var vm = CreateVmWithBoundary(200, 200);

        var seg = new HeadlandSegment
        {
            Name = "Diagonal Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(10, 10, Math.PI / 4), new Vec3(190, 190, Math.PI / 4) },
            StartExtension = 30,
            EndExtension = 30
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);

        // Check for self-intersections
        bool hasSelfIntersection = false;
        for (int i = 0; i < headland!.Count - 2 && !hasSelfIntersection; i++)
        {
            for (int j = i + 2; j < headland.Count - 1 && !hasSelfIntersection; j++)
            {
                if (i == 0 && j == headland.Count - 2) continue; // Skip closing edge pair
                if (SegmentsIntersect(headland[i], headland[i + 1], headland[j], headland[j + 1]))
                    hasSelfIntersection = true;
            }
        }
        Assert.That(hasSelfIntersection, Is.False,
            $"Headland polygon ({headland.Count} pts) should not self-intersect");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 14: Centroid-based polygon selection
    // ---------------------------------------------------------------

    [Test]
    public void CutNearEdge_KeepsLargerWorkingArea()
    {
        // A cut near the bottom edge should keep the larger top portion
        var vm = CreateVmWithBoundary(200, 200);

        var seg = new HeadlandSegment
        {
            Name = "Near-Edge Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new() { new Vec3(10, 20, Math.PI / 2), new Vec3(190, 20, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True);

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        double headArea = Math.Abs(CalculateArea(headland!));
        double fullArea = 200 * 200;
        // Cut near bottom (at y~30 after offset) should keep ~85% of area
        Assert.That(headArea, Is.GreaterThan(fullArea * 0.7),
            $"Near-edge cut should keep most of the area. Area: {headArea:F0}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 15: Edge cases
    // ---------------------------------------------------------------

    [Test]
    public void IdenticalLines_NoDoubleCut()
    {
        // Two identical lines should not cause problems
        var vm = CreateVmWithBoundary(200, 200);

        var seg1 = new HeadlandSegment
        {
            Name = "Line 1",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(10, 100, Math.PI / 2), new Vec3(190, 100, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };
        var seg2 = new HeadlandSegment
        {
            Name = "Line 2",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(10, 100, Math.PI / 2), new Vec3(190, 100, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg1);
        vm.ComputeSegmentOffset(seg2);
        vm.HeadlandSegments.Add(seg1);
        vm.HeadlandSegments.Add(seg2);

        // Should not crash
        Assert.DoesNotThrow(() => vm.BuildHeadlandFromSegments());
        Assert.That(vm.HasHeadland, Is.True);
    }

    [Test]
    public void ZeroOffsetLine_StillWorks()
    {
        var vm = CreateVmWithBoundary(200, 200);

        var seg = new HeadlandSegment
        {
            Name = "Zero Offset",
            Type = HeadlandSegmentType.Line,
            Offset = 0,
            BoundaryPoints = new() { new Vec3(10, 100, Math.PI / 2), new Vec3(190, 100, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);

        Assert.DoesNotThrow(() => vm.BuildHeadlandFromSegments());
    }

    [Test]
    public void VeryCloseParallelChainLines_DontMergeIncorrectly()
    {
        // Two nearly parallel offset lines very close together
        // They should NOT merge (parallel lines don't intersect)
        var vm = CreateVmWithBoundary(200, 200);

        var segA = new HeadlandSegment
        {
            Name = "Near-Parallel A",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(10, 98, Math.PI / 2), new Vec3(190, 98, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };
        var segB = new HeadlandSegment
        {
            Name = "Near-Parallel B",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(10, 102, Math.PI / 2), new Vec3(190, 102, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(segA);
        vm.ComputeSegmentOffset(segB);
        vm.HeadlandSegments.Add(segA);
        vm.HeadlandSegments.Add(segB);
        vm.BuildHeadlandFromSegments();

        // Both should be individually effective (each spans the full field width)
        Assert.That(segA.IsEffective, Is.True, "Parallel A should be effective independently");
        Assert.That(segB.IsEffective, Is.True, "Parallel B should be effective independently");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 16: Realistic field with notch (irregular shape)
    // ---------------------------------------------------------------

    [Test]
    public void IrregularField_TwoChainedLines()
    {
        // L-shaped field with a horizontal cut through the wide part
        var vm = new MainViewModelBuilder().Build();

        // L-shaped boundary: wide bottom (200x100) + narrow left column (100x200)
        var boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = new()
                {
                    new BoundaryPoint(0, 0, 0),
                    new BoundaryPoint(200, 0, Math.PI / 2),
                    new BoundaryPoint(200, 100, Math.PI),
                    new BoundaryPoint(100, 100, Math.PI),
                    new BoundaryPoint(100, 200, Math.PI),
                    new BoundaryPoint(0, 200, -Math.PI / 2)
                }
            }
        };
        boundary.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, boundary);

        // Horizontal line through the bottom wide section at y=50
        // Should cross left boundary at x=0 and right boundary at x=200
        var seg = new HeadlandSegment
        {
            Name = "Wide Section Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 15,
            BoundaryPoints = new() { new Vec3(20, 50, Math.PI / 2), new Vec3(180, 50, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg);
        vm.HeadlandSegments.Add(seg);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg.IsEffective, Is.True,
            "Horizontal line across wide section of L-shaped field should be effective");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 17: Sequential cuts (order matters)
    // ---------------------------------------------------------------

    [Test]
    public void SequentialCuts_OrderDoesNotBreakResult()
    {
        // Two lines: first cuts bottom, second cuts right
        // Both should be effective regardless of order
        var vm1 = CreateVmWithBoundary(200, 200);
        var vm2 = CreateVmWithBoundary(200, 200);

        var segBottom = () => new HeadlandSegment
        {
            Name = "Bottom",
            Type = HeadlandSegmentType.Line,
            Offset = 25,
            BoundaryPoints = new() { new Vec3(10, 15, Math.PI / 2), new Vec3(190, 15, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };
        var segRight = () => new HeadlandSegment
        {
            Name = "Right",
            Type = HeadlandSegmentType.Line,
            Offset = 25,
            BoundaryPoints = new() { new Vec3(185, 10, 0), new Vec3(185, 190, 0) },
            StartExtension = 50,
            EndExtension = 50
        };

        // Order 1: Bottom then Right
        var s1b = segBottom(); var s1r = segRight();
        vm1.ComputeSegmentOffset(s1b); vm1.ComputeSegmentOffset(s1r);
        vm1.HeadlandSegments.Add(s1b); vm1.HeadlandSegments.Add(s1r);
        vm1.BuildHeadlandFromSegments();

        // Order 2: Right then Bottom
        var s2r = segRight(); var s2b = segBottom();
        vm2.ComputeSegmentOffset(s2r); vm2.ComputeSegmentOffset(s2b);
        vm2.HeadlandSegments.Add(s2r); vm2.HeadlandSegments.Add(s2b);
        vm2.BuildHeadlandFromSegments();

        Assert.That(s1b.IsEffective && s1r.IsEffective, Is.True,
            $"Order 1: Bottom={s1b.IsEffective}, Right={s1r.IsEffective}");
        Assert.That(s2r.IsEffective && s2b.IsEffective, Is.True,
            $"Order 2: Right={s2r.IsEffective}, Bottom={s2b.IsEffective}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 18: Regression - dividing line follows merged chain shape
    // ---------------------------------------------------------------

    [Test]
    public void LShapeChain_HeadlandFollowsCorner_NotDiagonal()
    {
        // Regression: two chained lines forming an L-shape produced a diagonal headland
        // path because the dividing line used the original segment's 2 points instead
        // of the merged chain's interior points (the corner).
        //
        // Setup: horizontal line across top + vertical line down right side
        // Expected: headland cuts an L-shaped corner from the top-right
        // Bug: headland cut diagonally from top-left to bottom-right
        var vm = CreateVmWithBoundary(200, 200);

        // Horizontal line along top: from left toward right
        var segH = new HeadlandSegment
        {
            Name = "Top Horizontal",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(20, 180, Math.PI / 2), new Vec3(160, 180, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 60
        };

        // Vertical line along right: from top down toward bottom
        var segV = new HeadlandSegment
        {
            Name = "Right Vertical",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(180, 170, Math.PI), new Vec3(180, 20, Math.PI) },
            StartExtension = 60,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(segH);
        vm.ComputeSegmentOffset(segV);
        vm.HeadlandSegments.Add(segH);
        vm.HeadlandSegments.Add(segV);
        vm.BuildHeadlandFromSegments();

        bool anyEffective = segH.IsEffective || segV.IsEffective;
        Assert.That(anyEffective, Is.True,
            $"L-shape chain should be effective. H={segH.IsEffective}, V={segV.IsEffective}");

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);

        // Key assertion: the headland path should have a point near the L-corner
        // (approximately at (160, 160) after offset). If the bug is present,
        // no headland point will be near this corner - the path goes diagonal.
        bool hasCornerPoint = false;
        foreach (var pt in headland!)
        {
            // The corner should be roughly where the two offset lines meet
            // Horizontal offset at y~160, vertical offset at x~160
            double distToCorner = Math.Sqrt(
                Math.Pow(pt.Easting - 160, 2) + Math.Pow(pt.Northing - 160, 2));
            if (distToCorner < 30) // Within 30m of expected corner
            {
                hasCornerPoint = true;
                break;
            }
        }

        Assert.That(hasCornerPoint, Is.True,
            "Headland path should have a point near the L-corner (~160,160), not cut diagonally. " +
            $"Points: {string.Join("; ", headland.Select(p => $"({p.Easting:F0},{p.Northing:F0})"))}");

        // Additional: area should reflect an L-cut (not a diagonal triangle cut)
        // L-cut removes ~20m from top + ~20m from right = roughly 200*180 - 20*180 = smaller than diagonal
        double headArea = Math.Abs(CalculateArea(headland));
        double fullArea = 200 * 200;
        // An L-cut from two 20m offsets removes ~7600 (top strip + right strip - corner overlap)
        // A diagonal cut would remove roughly half the field (~20000)
        Assert.That(headArea, Is.GreaterThan(fullArea * 0.6),
            $"L-cut should keep most area (not diagonal). Area: {headArea:F0}, full: {fullArea:F0}");
    }

    [Test]
    public void TwoSequentialCuts_SecondCutOnSameHeadlandSegment_Works()
    {
        // Regression: after first cut creates a long headland edge, second line
        // intersects that edge at two points. Old code rejected this because
        // startIntersectIdx == endIntersectIdx (same segment).
        var vm = CreateVmWithBoundary(200, 200);

        // First cut: horizontal across bottom
        var seg1 = new HeadlandSegment
        {
            Name = "Bottom Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 30,
            BoundaryPoints = new() { new Vec3(10, 15, Math.PI / 2), new Vec3(190, 15, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        // Second cut: vertical down right side - both endpoints will hit the
        // new bottom edge (a single long segment created by the first cut)
        var seg2 = new HeadlandSegment
        {
            Name = "Right Cut",
            Type = HeadlandSegmentType.Line,
            Offset = 30,
            BoundaryPoints = new() { new Vec3(185, 190, Math.PI), new Vec3(185, 50, Math.PI) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg1);
        vm.ComputeSegmentOffset(seg2);
        vm.HeadlandSegments.Add(seg1);
        vm.HeadlandSegments.Add(seg2);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg1.IsEffective, Is.True, "First cut should be effective");
        Assert.That(seg2.IsEffective, Is.True,
            "Second cut should be effective even when both intersections are on same headland segment");

        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        double headArea = Math.Abs(CalculateArea(headland!));
        double fullArea = 200 * 200;
        // Two cuts: 30m from bottom + 30m from right
        Assert.That(headArea, Is.LessThan(fullArea * 0.8),
            $"Two perpendicular cuts should reduce area. Area: {headArea:F0}");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 19: One-ended curve should not break other lines
    // ---------------------------------------------------------------

    [Test]
    public void OneEndedCurve_DoesNotBreakEffectiveLine()
    {
        // Regression: a curve that only reaches boundary on one end was being merged
        // with a working line, breaking the line's effectiveness.
        // The working line should still produce a valid headland cut.
        var vm = CreateVmWithBoundary(200, 200);

        // Working line: horizontal across the full field
        var segLine = new HeadlandSegment
        {
            Name = "Full Line",
            Type = HeadlandSegmentType.Line,
            Offset = 20,
            BoundaryPoints = new() { new Vec3(10, 100, Math.PI / 2), new Vec3(190, 100, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        // One-ended curve: only reaches boundary on one side, short extension on other
        var curvePts = new List<Vec3>();
        for (int i = 0; i <= 10; i++)
        {
            double angle = i * Math.PI / 2 / 10;
            curvePts.Add(new Vec3(150 + 30 * Math.Sin(angle), 80 + 30 * (1 - Math.Cos(angle)), angle));
        }
        var segCurve = new HeadlandSegment
        {
            Name = "One-Ended Curve",
            Type = HeadlandSegmentType.Curve,
            Offset = 15,
            BoundaryPoints = curvePts,
            StartExtension = 5,   // Too short to reach boundary
            EndExtension = 50     // Reaches top boundary
        };

        vm.ComputeSegmentOffset(segLine);
        vm.ComputeSegmentOffset(segCurve);
        vm.HeadlandSegments.Add(segLine);
        vm.HeadlandSegments.Add(segCurve);
        vm.BuildHeadlandFromSegments();

        // The full line should still be effective regardless of the one-ended curve
        Assert.That(segLine.IsEffective, Is.True,
            "Working line should remain effective even when a one-ended curve is present");

        // The one-ended curve should NOT be effective
        Assert.That(segCurve.IsEffective, Is.False,
            "One-ended curve should not be effective");

        // Headland should reflect only the line's cut
        var headland = GetHeadlandLine(vm);
        Assert.That(headland, Is.Not.Null);
        double headArea = Math.Abs(CalculateArea(headland!));
        double fullArea = 200 * 200;
        Assert.That(headArea, Is.LessThan(fullArea * 0.85).And.GreaterThan(fullArea * 0.3),
            $"Only the line's cut should apply. Area: {headArea:F0}");
    }

    [Test]
    public void MultipleLines_OneNonEffective_OthersStillWork()
    {
        // Multiple lines where one doesn't reach boundary - should not affect the others
        var vm = CreateVmWithBoundary(200, 200);

        // Working line 1: horizontal bottom
        var seg1 = new HeadlandSegment
        {
            Name = "Bottom",
            Type = HeadlandSegmentType.Line,
            Offset = 25,
            BoundaryPoints = new() { new Vec3(10, 15, Math.PI / 2), new Vec3(190, 15, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        // Non-effective line: in the middle, too short
        var seg2 = new HeadlandSegment
        {
            Name = "Floating",
            Type = HeadlandSegmentType.Line,
            Offset = 10,
            BoundaryPoints = new() { new Vec3(80, 100, Math.PI / 2), new Vec3(120, 100, Math.PI / 2) },
            StartExtension = 5,
            EndExtension = 5
        };

        // Working line 2: horizontal top
        var seg3 = new HeadlandSegment
        {
            Name = "Top",
            Type = HeadlandSegmentType.Line,
            Offset = 25,
            BoundaryPoints = new() { new Vec3(10, 185, Math.PI / 2), new Vec3(190, 185, Math.PI / 2) },
            StartExtension = 50,
            EndExtension = 50
        };

        vm.ComputeSegmentOffset(seg1);
        vm.ComputeSegmentOffset(seg2);
        vm.ComputeSegmentOffset(seg3);
        vm.HeadlandSegments.Add(seg1);
        vm.HeadlandSegments.Add(seg2);
        vm.HeadlandSegments.Add(seg3);
        vm.BuildHeadlandFromSegments();

        Assert.That(seg1.IsEffective, Is.True, "Bottom line should be effective");
        Assert.That(seg2.IsEffective, Is.False, "Floating line should not be effective");
        Assert.That(seg3.IsEffective, Is.True, "Top line should be effective");
    }

    // ---------------------------------------------------------------
    // TEST GROUP 20: Extension save/load regression
    // ---------------------------------------------------------------

    [Test]
    public void ExtensionsSavedAndLoaded()
    {
        // Regression: StartExtension and EndExtension were not included in the DTO,
        // so they always reverted to 50m default on save/load.
        var tempDir = Path.Combine(Path.GetTempPath(), "headland_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var segments = new List<HeadlandSegment>
            {
                new()
                {
                    Name = "Custom Extensions",
                    Type = HeadlandSegmentType.Line,
                    Offset = 12,
                    StartExtension = 25,
                    EndExtension = 75,
                    BoundaryPoints = new() { new Vec3(10, 50, 0), new Vec3(90, 50, 0) }
                },
                new()
                {
                    Name = "Default Extensions",
                    Type = HeadlandSegmentType.Curve,
                    Offset = 8,
                    StartExtension = 50,
                    EndExtension = 50,
                    BoundaryPoints = new() { new Vec3(0, 0, 0), new Vec3(50, 50, 0), new Vec3(100, 0, 0) }
                }
            };

            AgValoniaGPS.Services.Headland.HeadlandSegmentFileService.Save(tempDir, segments);
            var loaded = AgValoniaGPS.Services.Headland.HeadlandSegmentFileService.Load(tempDir);

            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded[0].StartExtension, Is.EqualTo(25),
                "StartExtension should survive save/load");
            Assert.That(loaded[0].EndExtension, Is.EqualTo(75),
                "EndExtension should survive save/load");
            Assert.That(loaded[1].StartExtension, Is.EqualTo(50));
            Assert.That(loaded[1].EndExtension, Is.EqualTo(50));
            Assert.That(loaded[0].Offset, Is.EqualTo(12));
            Assert.That(loaded[0].Name, Is.EqualTo("Custom Extensions"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static double CalculateArea(List<Vec3> polygon)
    {
        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            area += (p2.Easting - p1.Easting) * (p2.Northing + p1.Northing);
        }
        return area / 2.0;
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
