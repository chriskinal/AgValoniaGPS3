using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Tram;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgValoniaGPS.ViewModels.Tests;

/// <summary>
/// Tests for boundary tram track generation using real field data.
/// The user's 9.62ha field has concave sections that stress the offset algorithm.
/// </summary>
[TestFixture]
public class TramBoundaryTrackTests
{
    private TramLineService _service = null!;
    private List<Vec3> _boundary = null!;

    [SetUp]
    public void SetUp()
    {
        var offsetService = new TramLineOffsetService();
        var logger = NullLogger<TramLineService>.Instance;
        _service = new TramLineService(offsetService, logger);

        ConfigurationStore.Instance.Tram.TramWidth = 24.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;

        // Load the real field boundary from TestData
        _boundary = LoadBoundary();
    }

    private static List<Vec3> LoadBoundary()
    {
        // Try loading from TestData directory
        var testDataPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "TestData", "Fields", "UserField", "Boundary.txt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestData", "Fields", "UserField", "Boundary.txt"),
        };

        string? filePath = testDataPaths.FirstOrDefault(File.Exists);
        if (filePath == null)
        {
            // Fallback: create a synthetic concave boundary
            return CreateConcaveBoundary();
        }

        var pts = new List<Vec3>();
        var lines = File.ReadAllLines(filePath);
        bool reading = false;
        int count = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("Boundary")) { reading = true; continue; }
            if (reading && count == 0)
            {
                if (trimmed is "True" or "False") continue;
                int.TryParse(trimmed, out count);
                continue;
            }
            if (reading && count > 0)
            {
                var parts = trimmed.Split(',');
                if (parts.Length >= 3)
                    pts.Add(new Vec3(
                        double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                        double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                        double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        // Close the polygon
        if (pts.Count > 2)
        {
            double d = Math.Pow(pts[0].Easting - pts[^1].Easting, 2) +
                       Math.Pow(pts[0].Northing - pts[^1].Northing, 2);
            if (d > 1.0) pts.Add(pts[0]);
        }

        return pts;
    }

    /// <summary>
    /// Create a synthetic concave (star-shaped) boundary for testing
    /// when the real file isn't available.
    /// </summary>
    private static List<Vec3> CreateConcaveBoundary()
    {
        var pts = new List<Vec3>();
        int n = 100;
        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n;
            // Star shape: radius varies between 80 and 150
            double radius = (i % 10 < 5) ? 150 : 80;
            double e = radius * Math.Cos(angle);
            double northing = radius * Math.Sin(angle);
            pts.Add(new Vec3(e, northing, angle + Math.PI / 2));
        }
        pts.Add(pts[0]); // close
        return pts;
    }

    private static bool IsPointInPolygon(double px, double py, List<Vec3> polygon)
    {
        bool inside = false;
        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            double yi = polygon[i].Northing, yj = polygon[j].Northing;
            double xi = polygon[i].Easting, xj = polygon[j].Easting;
            if (((yi > py) != (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }

    private static bool SegmentsIntersect(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2)
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

    // ---------------------------------------------------------------
    // All points inside boundary
    // ---------------------------------------------------------------

    [Test]
    public void OuterTrack_AllPointsInsideBoundary()
    {
        _service.GenerateBoundaryTramTracks(_boundary);

        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(2),
            "Should generate outer boundary track");

        int outsideCount = 0;
        var outsidePts = new List<string>();
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            if (!IsPointInPolygon(pt.Easting, pt.Northing, _boundary))
            {
                outsideCount++;
                if (outsidePts.Count < 5)
                    outsidePts.Add($"({pt.Easting:F1},{pt.Northing:F1})");
            }
        }

        Assert.That(outsideCount, Is.EqualTo(0),
            $"Outer track has {outsideCount} points outside boundary: {string.Join(", ", outsidePts)}");
    }

    [Test]
    public void InnerTrack_AllPointsInsideBoundary()
    {
        _service.GenerateBoundaryTramTracks(_boundary);

        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(2),
            "Should generate inner boundary track");

        int outsideCount = 0;
        var outsidePts = new List<string>();
        foreach (var pt in _service.InnerBoundaryTrack)
        {
            if (!IsPointInPolygon(pt.Easting, pt.Northing, _boundary))
            {
                outsideCount++;
                if (outsidePts.Count < 5)
                    outsidePts.Add($"({pt.Easting:F1},{pt.Northing:F1})");
            }
        }

        Assert.That(outsideCount, Is.EqualTo(0),
            $"Inner track has {outsideCount} points outside boundary: {string.Join(", ", outsidePts)}");
    }

    // ---------------------------------------------------------------
    // No self-intersections
    // ---------------------------------------------------------------

    [Test]
    public void OuterTrack_NoSelfIntersections()
    {
        _service.GenerateBoundaryTramTracks(_boundary);
        var track = _service.OuterBoundaryTrack;
        if (track.Count < 4) return;

        int crossings = 0;
        for (int i = 0; i < track.Count - 2; i++)
        {
            for (int j = i + 2; j < track.Count - 1; j++)
            {
                // Skip adjacent and closing edge
                if (i == 0 && j == track.Count - 2) continue;
                if (SegmentsIntersect(track[i], track[i + 1], track[j], track[j + 1]))
                    crossings++;
            }
        }

        Assert.That(crossings, Is.EqualTo(0),
            $"Outer track has {crossings} self-intersections");
    }

    [Test]
    public void InnerTrack_NoSelfIntersections()
    {
        _service.GenerateBoundaryTramTracks(_boundary);
        var track = _service.InnerBoundaryTrack;
        if (track.Count < 4) return;

        int crossings = 0;
        for (int i = 0; i < track.Count - 2; i++)
        {
            for (int j = i + 2; j < track.Count - 1; j++)
            {
                if (i == 0 && j == track.Count - 2) continue;
                if (SegmentsIntersect(track[i], track[i + 1], track[j], track[j + 1]))
                    crossings++;
            }
        }

        Assert.That(crossings, Is.EqualTo(0),
            $"Inner track has {crossings} self-intersections");
    }

    // ---------------------------------------------------------------
    // Closed loop
    // ---------------------------------------------------------------

    [Test]
    public void BothTracks_FormClosedLoops()
    {
        _service.GenerateBoundaryTramTracks(_boundary);

        if (_service.OuterBoundaryTrack.Count > 2)
        {
            var first = _service.OuterBoundaryTrack[0];
            var last = _service.OuterBoundaryTrack[^1];
            double dist = Math.Sqrt(Math.Pow(first.Easting - last.Easting, 2) +
                                    Math.Pow(first.Northing - last.Northing, 2));
            Assert.That(dist, Is.LessThan(0.1), $"Outer track gap: {dist:F3}m");
        }

        if (_service.InnerBoundaryTrack.Count > 2)
        {
            var first = _service.InnerBoundaryTrack[0];
            var last = _service.InnerBoundaryTrack[^1];
            double dist = Math.Sqrt(Math.Pow(first.Easting - last.Easting, 2) +
                                    Math.Pow(first.Northing - last.Northing, 2));
            Assert.That(dist, Is.LessThan(0.1), $"Inner track gap: {dist:F3}m");
        }
    }

    // ---------------------------------------------------------------
    // Offset distance validation
    // ---------------------------------------------------------------

    [Test]
    public void OuterTrack_CorrectOffsetDistance()
    {
        // Outer offset = (tramWidth * 0.5) - halfWheelTrack = 12 - 0.9 = 11.1m
        double expectedOffset = (24.0 * 0.5) - (1.8 / 2.0);

        _service.GenerateBoundaryTramTracks(_boundary);

        // Sample some outer track points and check distance to nearest boundary point
        int checked_pts = 0;
        int badDist = 0;
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            // Find nearest boundary point
            double minDist = double.MaxValue;
            foreach (var bp in _boundary)
            {
                double d = Math.Sqrt(Math.Pow(pt.Easting - bp.Easting, 2) +
                                     Math.Pow(pt.Northing - bp.Northing, 2));
                if (d < minDist) minDist = d;
            }

            checked_pts++;
            // Allow some tolerance for concave areas where offset collapses
            if (minDist > expectedOffset * 1.5 || minDist < expectedOffset * 0.3)
                badDist++;
        }

        // Most points should be at roughly the expected offset
        double badRatio = (double)badDist / checked_pts;
        Assert.That(badRatio, Is.LessThan(0.15),
            $"{badDist}/{checked_pts} points ({badRatio:P0}) have unexpected offset distance (expected ~{expectedOffset:F1}m)");
    }

    // ---------------------------------------------------------------
    // Segment-level boundary check (line segments, not just points)
    // ---------------------------------------------------------------

    /// <summary>
    /// Check if a line segment from p1 to p2 goes outside the boundary.
    /// Samples the segment at regular intervals and checks each sample point.
    /// </summary>
    private bool SegmentStaysInsideBoundary(Vec2 p1, Vec2 p2, List<Vec3> boundary, double sampleSpacing = 1.0)
    {
        double dx = p2.Easting - p1.Easting;
        double dy = p2.Northing - p1.Northing;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.1) return true;

        int samples = Math.Max(2, (int)(len / sampleSpacing));
        for (int i = 0; i <= samples; i++)
        {
            double t = (double)i / samples;
            double px = p1.Easting + t * dx;
            double py = p1.Northing + t * dy;
            if (!IsPointInPolygon(px, py, boundary))
                return false;
        }
        return true;
    }

    [Test]
    public void OuterTrack_AllSegmentsInsideBoundary()
    {
        _service.GenerateBoundaryTramTracks(_boundary);
        var track = _service.OuterBoundaryTrack;
        if (track.Count < 2) return;

        int badSegments = 0;
        var badDetails = new List<string>();
        for (int i = 0; i < track.Count - 1; i++)
        {
            if (!SegmentStaysInsideBoundary(track[i], track[i + 1], _boundary))
            {
                badSegments++;
                if (badDetails.Count < 3)
                    badDetails.Add($"seg[{i}] ({track[i].Easting:F0},{track[i].Northing:F0})->({track[i+1].Easting:F0},{track[i+1].Northing:F0})");
            }
        }

        Assert.That(badSegments, Is.EqualTo(0),
            $"Outer track has {badSegments} segments going outside boundary: {string.Join("; ", badDetails)}");
    }

    [Test]
    public void InnerTrack_AllSegmentsInsideBoundary()
    {
        _service.GenerateBoundaryTramTracks(_boundary);
        var track = _service.InnerBoundaryTrack;
        if (track.Count < 2) return;

        int badSegments = 0;
        var badDetails = new List<string>();
        for (int i = 0; i < track.Count - 1; i++)
        {
            if (!SegmentStaysInsideBoundary(track[i], track[i + 1], _boundary))
            {
                badSegments++;
                if (badDetails.Count < 3)
                    badDetails.Add($"seg[{i}] ({track[i].Easting:F0},{track[i].Northing:F0})->({track[i+1].Easting:F0},{track[i+1].Northing:F0})");
            }
        }

        Assert.That(badSegments, Is.EqualTo(0),
            $"Inner track has {badSegments} segments going outside boundary: {string.Join("; ", badDetails)}");
    }

    // ---------------------------------------------------------------
    // Offset distance from boundary (must be significantly inward)
    // ---------------------------------------------------------------

    [Test]
    public void OuterTrack_MinimumDistanceFromBoundary()
    {
        // The outer offset should be at (tramWidth/2 - halfWheelTrack) = 11.1m from boundary
        // Every point should be at least 5m inside (allowing some tolerance for concave collapse)
        double minExpectedDist = 5.0;

        _service.GenerateBoundaryTramTracks(_boundary);

        int tooClose = 0;
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            double minDist = double.MaxValue;
            for (int i = 0; i < _boundary.Count - 1; i++)
            {
                double dist = DistToSegment(pt.Easting, pt.Northing,
                    _boundary[i].Easting, _boundary[i].Northing,
                    _boundary[i + 1].Easting, _boundary[i + 1].Northing);
                if (dist < minDist) minDist = dist;
            }
            if (minDist < minExpectedDist) tooClose++;
        }

        double ratio = (double)tooClose / _service.OuterBoundaryTrack.Count;
        Assert.That(ratio, Is.LessThan(0.05),
            $"{tooClose}/{_service.OuterBoundaryTrack.Count} outer track points are < {minExpectedDist}m from boundary");
    }

    private static double DistToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 0.0001) return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        double t = Math.Max(0, Math.Min(1, ((px - ax) * dx + (py - ay) * dy) / lenSq));
        double projX = ax + t * dx, projY = ay + t * dy;
        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }

    // ---------------------------------------------------------------
    // Headland offset (ComputeSegmentOffset) stays inside boundary
    // ---------------------------------------------------------------

    [Test]
    public void HeadlandOffset_AllPointsInsideBoundary()
    {
        // Use ComputeSegmentOffset with Clipper2 on the real field
        var vm = new MainViewModelBuilder().Build();
        var bndModel = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = _boundary.Select(p => new BoundaryPoint(p.Easting, p.Northing, p.Heading)).ToList()
            }
        };
        bndModel.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, bndModel);

        var seg = new AgValoniaGPS.Models.Headland.HeadlandSegment
        {
            Name = "Test Boundary",
            Type = AgValoniaGPS.Models.Headland.HeadlandSegmentType.Curve,
            Offset = 12,
            BoundaryPoints = new List<Vec3>(_boundary)
        };

        vm.ComputeSegmentOffset(seg);

        Assert.That(seg.OffsetPoints.Count, Is.GreaterThan(10),
            "Should produce offset points");

        int outsideCount = 0;
        var outsidePts = new List<string>();
        foreach (var pt in seg.OffsetPoints)
        {
            if (!IsPointInPolygon(pt.Easting, pt.Northing, _boundary))
            {
                outsideCount++;
                if (outsidePts.Count < 5)
                    outsidePts.Add($"({pt.Easting:F1},{pt.Northing:F1})");
            }
        }

        Assert.That(outsideCount, Is.EqualTo(0),
            $"Headland offset has {outsideCount} points outside boundary: {string.Join(", ", outsidePts)}");
    }

    [Test]
    public void HeadlandOffset_AllSegmentsInsideBoundary()
    {
        var vm = new MainViewModelBuilder().Build();
        var bndModel = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = _boundary.Select(p => new BoundaryPoint(p.Easting, p.Northing, p.Heading)).ToList()
            }
        };
        bndModel.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, bndModel);

        var seg = new AgValoniaGPS.Models.Headland.HeadlandSegment
        {
            Name = "Test Boundary",
            Type = AgValoniaGPS.Models.Headland.HeadlandSegmentType.Curve,
            Offset = 12,
            BoundaryPoints = new List<Vec3>(_boundary)
        };

        vm.ComputeSegmentOffset(seg);
        var pts = seg.OffsetPoints;

        int badSegs = 0;
        var badDetails = new List<string>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            if (!SegmentStaysInsideBoundary(
                new Vec2(pts[i].Easting, pts[i].Northing),
                new Vec2(pts[i + 1].Easting, pts[i + 1].Northing),
                _boundary, 0.5))
            {
                badSegs++;
                if (badDetails.Count < 3)
                    badDetails.Add($"seg[{i}] ({pts[i].Easting:F0},{pts[i].Northing:F0})->({pts[i+1].Easting:F0},{pts[i+1].Northing:F0})");
            }
        }

        Assert.That(badSegs, Is.EqualTo(0),
            $"Headland offset has {badSegs} segments going outside boundary: {string.Join("; ", badDetails)}");
    }

    // ---------------------------------------------------------------
    // Offset must be inward (closer to centroid than boundary)
    // ---------------------------------------------------------------

    [Test]
    public void HeadlandOffset_NoPointFurtherFromCentroidThanBoundary()
    {
        // The offset should always be INWARD - every offset point should be
        // closer to the field centroid than the nearest boundary point.
        // This catches the "spike" bug where offset follows a narrow concavity
        // deeper than the boundary edge (technically inside polygon but outward).
        var vm = new MainViewModelBuilder().Build();
        var bndModel = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = _boundary.Select(p => new BoundaryPoint(p.Easting, p.Northing, p.Heading)).ToList()
            }
        };
        bndModel.OuterBoundary.UpdateBounds();
        typeof(MainViewModel).GetField("_currentBoundary",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(vm, bndModel);

        var seg = new AgValoniaGPS.Models.Headland.HeadlandSegment
        {
            Name = "Test",
            Type = AgValoniaGPS.Models.Headland.HeadlandSegmentType.Curve,
            Offset = 12,
            BoundaryPoints = new List<Vec3>(_boundary)
        };
        vm.ComputeSegmentOffset(seg);

        // Compute centroid
        double cx = _boundary.Average(p => p.Easting);
        double cy = _boundary.Average(p => p.Northing);

        int outward = 0;
        var outwardPts = new List<string>();
        foreach (var pt in seg.OffsetPoints)
        {
            double distToCentroid = Math.Sqrt(Math.Pow(pt.Easting - cx, 2) + Math.Pow(pt.Northing - cy, 2));

            // Find nearest boundary point's distance to centroid
            double nearestBndDistToCentroid = double.MaxValue;
            double nearestBndDist = double.MaxValue;
            foreach (var bp in _boundary)
            {
                double d = Math.Sqrt(Math.Pow(bp.Easting - pt.Easting, 2) + Math.Pow(bp.Northing - pt.Northing, 2));
                if (d < nearestBndDist)
                {
                    nearestBndDist = d;
                    nearestBndDistToCentroid = Math.Sqrt(Math.Pow(bp.Easting - cx, 2) + Math.Pow(bp.Northing - cy, 2));
                }
            }

            // Offset point should be closer to centroid than its nearest boundary point
            // Allow 2m tolerance for Clipper2 rounding at corners
            if (distToCentroid > nearestBndDistToCentroid + 2.0)
            {
                outward++;
                if (outwardPts.Count < 5)
                    outwardPts.Add($"({pt.Easting:F0},{pt.Northing:F0}) d={distToCentroid:F0} > bnd={nearestBndDistToCentroid:F0}");
            }
        }

        Assert.That(outward, Is.EqualTo(0),
            $"{outward}/{seg.OffsetPoints.Count} offset points are further from centroid than boundary: {string.Join("; ", outwardPts)}");
    }

    // ---------------------------------------------------------------
    // Different tram widths
    // ---------------------------------------------------------------

    [TestCase(6.0)]
    [TestCase(12.0)]
    [TestCase(24.0)]
    [TestCase(36.0)]
    public void VariousTramWidths_AllPointsInsideBoundary(double tramWidth)
    {
        ConfigurationStore.Instance.Tram.TramWidth = tramWidth;
        _service.GenerateBoundaryTramTracks(_boundary);

        foreach (var pt in _service.OuterBoundaryTrack)
        {
            Assert.That(IsPointInPolygon(pt.Easting, pt.Northing, _boundary), Is.True,
                $"Outer pt ({pt.Easting:F1},{pt.Northing:F1}) outside boundary at tramWidth={tramWidth}");
        }
        foreach (var pt in _service.InnerBoundaryTrack)
        {
            Assert.That(IsPointInPolygon(pt.Easting, pt.Northing, _boundary), Is.True,
                $"Inner pt ({pt.Easting:F1},{pt.Northing:F1}) outside boundary at tramWidth={tramWidth}");
        }
    }
}
