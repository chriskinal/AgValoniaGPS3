using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Tram;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgValoniaGPS.ViewModels.Tests;

/// <summary>
/// Tests for tram line generation, detection, and PGN integration.
/// </summary>
[TestFixture]
public class TramLineTests
{
    private TramLineService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var offsetService = new TramLineOffsetService();
        var logger = NullLogger<TramLineService>.Instance;
        _service = new TramLineService(offsetService, logger);

        // Set up config for tests
        var config = ConfigurationStore.Instance;
        config.Tram.TramWidth = 24.0;
        config.Tram.Passes = 3;
        config.Tram.StartPass = 0;
        config.Vehicle.TrackWidth = 1.8;
        config.Tool.Width = 12.0;
    }

    // ---------------------------------------------------------------
    // Generation tests
    // ---------------------------------------------------------------

    [Test]
    public void GenerateParallelTramLines_ABLine_ProducesLines()
    {
        var track = new Track
        {
            Name = "Test AB",
            Points = new List<Vec3>
            {
                new Vec3(0, 0, 0),
                new Vec3(0, 200, 0)
            },
            Type = TrackType.ABLine
        };

        _service.GenerateParallelTramLines(track, 200);

        Assert.That(_service.ParallelTramLines.Count, Is.GreaterThan(0),
            "Should generate parallel tram lines from AB line");
        Assert.That(_service.HasTramLines, Is.True);
    }

    [Test]
    public void GenerateParallelTramLines_Curve_ProducesLines()
    {
        var pts = new List<Vec3>();
        for (int i = 0; i <= 20; i++)
        {
            double angle = i * Math.PI / 20;
            pts.Add(new Vec3(100 * Math.Sin(angle), 100 * Math.Cos(angle), angle));
        }

        var track = new Track
        {
            Name = "Test Curve",
            Points = pts,
            Type = TrackType.Curve
        };

        _service.GenerateParallelTramLines(track, 200);

        Assert.That(_service.ParallelTramLines.Count, Is.GreaterThan(0),
            "Should generate parallel tram lines from curve");
    }

    [Test]
    public void GenerateBoundaryTramTracks_ProducesTwoTracks()
    {
        // Create a simple square fence line
        var fence = new List<Vec3>();
        int n = 40;
        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n;
            fence.Add(new Vec3(100 * Math.Cos(angle), 100 * Math.Sin(angle), angle + Math.PI / 2));
        }

        _service.GenerateBoundaryTramTracks(fence);

        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(0),
            "Should generate outer boundary track");
        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(0),
            "Should generate inner boundary track");
    }

    [Test]
    public void Clear_RemovesAllTramLines()
    {
        var track = new Track
        {
            Name = "Test",
            Points = new List<Vec3> { new Vec3(0, 0, 0), new Vec3(0, 200, 0) },
            Type = TrackType.ABLine
        };

        _service.GenerateParallelTramLines(track, 200);
        Assert.That(_service.HasTramLines, Is.True);

        _service.Clear();

        Assert.That(_service.HasTramLines, Is.False);
        Assert.That(_service.ParallelTramLines.Count, Is.EqualTo(0));
        Assert.That(_service.IsLeftManualOn, Is.False);
        Assert.That(_service.IsRightManualOn, Is.False);
    }

    // ---------------------------------------------------------------
    // Detection tests
    // ---------------------------------------------------------------

    [Test]
    public void IsOnTramLine_NearLine_ReturnsTrue()
    {
        // Add a manual tram line along x=0 from y=0 to y=100
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(0, 0), new Vec2(0, 100)
        });

        Assert.That(_service.IsOnTramLine(new Vec3(0.3, 50, 0), 0.5), Is.True,
            "Position 0.3m from tram line should be detected within 0.5m tolerance");
    }

    [Test]
    public void IsOnTramLine_FarFromLine_ReturnsFalse()
    {
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(0, 0), new Vec2(0, 100)
        });

        Assert.That(_service.IsOnTramLine(new Vec3(5.0, 50, 0), 0.5), Is.False,
            "Position 5m from tram line should NOT be detected within 0.5m tolerance");
    }

    [Test]
    public void DetectTramWheels_BothOnLine_Returns3()
    {
        // Vehicle at (0, 50) heading north, track width 1.8m
        // Left wheel at (-0.9, 50), right wheel at (0.9, 50)
        // Add tram lines at both wheel positions
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(-0.9, 0), new Vec2(-0.9, 100) // Left wheel track
        });
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(0.9, 0), new Vec2(0.9, 100) // Right wheel track
        });

        byte result = _service.DetectTramWheels(new Vec3(0, 50, 0), 0, 0.5);

        Assert.That(result & 1, Is.EqualTo(1), "Right wheel should be detected (bit 0)");
        Assert.That(result & 2, Is.EqualTo(2), "Left wheel should be detected (bit 1)");
        Assert.That(result, Is.EqualTo(3), "Both wheels on tram = 3");
    }

    [Test]
    public void DetectTramWheels_RightOnly_Returns1()
    {
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(0.9, 0), new Vec2(0.9, 100) // Right wheel track only
        });

        byte result = _service.DetectTramWheels(new Vec3(0, 50, 0), 0, 0.5);

        Assert.That(result, Is.EqualTo(1), "Only right wheel on tram = 1");
    }

    [Test]
    public void DetectTramWheels_LeftOnly_Returns2()
    {
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(-0.9, 0), new Vec2(-0.9, 100) // Left wheel track only
        });

        byte result = _service.DetectTramWheels(new Vec3(0, 50, 0), 0, 0.5);

        Assert.That(result, Is.EqualTo(2), "Only left wheel on tram = 2");
    }

    [Test]
    public void DetectTramWheels_NeitherOnLine_Returns0()
    {
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(10, 0), new Vec2(10, 100) // Far away
        });

        byte result = _service.DetectTramWheels(new Vec3(0, 50, 0), 0, 0.5);

        Assert.That(result, Is.EqualTo(0), "No wheels on tram = 0");
    }

    [Test]
    public void DetectTramWheels_ManualOverride_ForcesOn()
    {
        // No tram lines at all
        _service.IsRightManualOn = true;

        byte result = _service.DetectTramWheels(new Vec3(0, 50, 0), 0, 0.5);

        Assert.That(result & 1, Is.EqualTo(1), "Manual right override forces bit 0");
        Assert.That(result & 2, Is.EqualTo(0), "Left not overridden");
    }

    // ---------------------------------------------------------------
    // Distance tests
    // ---------------------------------------------------------------

    [Test]
    public void DistanceToNearestTramLine_ReturnsCorrectDistance()
    {
        _service.AddTramLine(new List<Vec2>
        {
            new Vec2(0, 0), new Vec2(0, 100)
        });

        double dist = _service.DistanceToNearestTramLine(new Vec3(5, 50, 0));

        Assert.That(dist, Is.EqualTo(5.0).Within(0.01),
            "Distance to tram line at x=0 from x=5 should be 5m");
    }

    [Test]
    public void DistanceToNearestTramLine_NoLines_ReturnsMaxValue()
    {
        double dist = _service.DistanceToNearestTramLine(new Vec3(0, 0, 0));

        Assert.That(dist, Is.EqualTo(double.MaxValue));
    }

    // ---------------------------------------------------------------
    // Config tests
    // ---------------------------------------------------------------

    [Test]
    public void TramConfig_StartPass_ClampsToZero()
    {
        var config = new TramConfig();
        config.StartPass = -5;

        Assert.That(config.StartPass, Is.EqualTo(0));
    }

    [Test]
    public void TramConfig_Passes_ClampsToOne()
    {
        var config = new TramConfig();
        config.Passes = 0;

        Assert.That(config.Passes, Is.EqualTo(1));
    }

    [Test]
    public void TramConfig_Alpha_ClampsToRange()
    {
        var config = new TramConfig();
        config.Alpha = 1.5;
        Assert.That(config.Alpha, Is.EqualTo(1.0));

        config.Alpha = -0.5;
        Assert.That(config.Alpha, Is.EqualTo(0.0));
    }

    // ---------------------------------------------------------------
    // File I/O tests
    // ---------------------------------------------------------------

    [Test]
    public void SaveAndLoad_PreservesData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tram_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            // Add some tram lines
            _service.AddTramLine(new List<Vec2>
            {
                new Vec2(10, 0), new Vec2(10, 100), new Vec2(10, 200)
            });
            _service.AddTramLine(new List<Vec2>
            {
                new Vec2(20, 0), new Vec2(20, 100)
            });

            _service.SaveToFile(tempDir);

            // Create new service and load
            var offsetService2 = new TramLineOffsetService();
            var logger2 = NullLogger<TramLineService>.Instance;
            var service2 = new TramLineService(offsetService2, logger2);

            service2.LoadFromFile(tempDir);

            Assert.That(service2.ParallelTramLines.Count, Is.EqualTo(2),
                "Should load 2 tram lines");
            Assert.That(service2.ParallelTramLines[0].Count, Is.EqualTo(3),
                "First line should have 3 points");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ---------------------------------------------------------------
    // Integration with ViewModel
    // ---------------------------------------------------------------

    [Test]
    public void GenerateParallelTramLines_ProducesSymmetricLines()
    {
        // Tram lines on both sides of reference track should be symmetric
        var track = new Track
        {
            Name = "Center",
            Points = new List<Vec3> { new Vec3(0, 0, 0), new Vec3(0, 200, 0) },
            Type = TrackType.ABLine
        };

        _service.GenerateParallelTramLines(track, 100);

        // Should have lines on both sides
        Assert.That(_service.ParallelTramLines.Count, Is.GreaterThanOrEqualTo(2),
            "Should have tram lines on both sides of reference");

        // Check that lines exist at positive and negative easting
        bool hasPositive = false, hasNegative = false;
        foreach (var line in _service.ParallelTramLines)
        {
            if (line.Count > 0)
            {
                if (line[0].Easting > 1) hasPositive = true;
                if (line[0].Easting < -1) hasNegative = true;
            }
        }
        Assert.That(hasPositive && hasNegative, Is.True,
            "Tram lines should exist on both sides of the reference track");
    }

    // ---------------------------------------------------------------
    // Boundary tram track tests
    // ---------------------------------------------------------------

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

    [Test]
    public void BoundaryTramTracks_AllPointsInsideBoundary()
    {
        // 200x200 square boundary
        var boundary = new List<Vec3>
        {
            new Vec3(0, 0, 0), new Vec3(200, 0, Math.PI / 2),
            new Vec3(200, 200, Math.PI), new Vec3(0, 200, 3 * Math.PI / 2),
            new Vec3(0, 0, 0) // closed
        };

        ConfigurationStore.Instance.Tram.TramWidth = 24.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;

        _service.GenerateBoundaryTramTracks(boundary);

        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(2),
            "Should have outer boundary track");
        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(2),
            "Should have inner boundary track");

        // ALL outer track points must be inside the boundary
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            Assert.That(IsPointInPolygon(pt.Easting, pt.Northing, boundary), Is.True,
                $"Outer track point ({pt.Easting:F1}, {pt.Northing:F1}) should be inside boundary");
        }

        // ALL inner track points must be inside the boundary
        foreach (var pt in _service.InnerBoundaryTrack)
        {
            Assert.That(IsPointInPolygon(pt.Easting, pt.Northing, boundary), Is.True,
                $"Inner track point ({pt.Easting:F1}, {pt.Northing:F1}) should be inside boundary");
        }
    }

    [Test]
    public void BoundaryTramTracks_InsideHeadlandNotBoundary()
    {
        // Headland is 20m inside the 200x200 boundary
        // Boundary tram tracks should be inside the headland, not the boundary edge
        var headland = new List<Vec3>
        {
            new Vec3(20, 20, 0), new Vec3(180, 20, Math.PI / 2),
            new Vec3(180, 180, Math.PI), new Vec3(20, 180, 3 * Math.PI / 2),
            new Vec3(20, 20, 0)
        };

        ConfigurationStore.Instance.Tram.TramWidth = 24.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;

        _service.GenerateBoundaryTramTracks(headland);

        // All points should be inside the headland polygon (not just boundary)
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            Assert.That(IsPointInPolygon(pt.Easting, pt.Northing, headland), Is.True,
                $"Outer track ({pt.Easting:F1}, {pt.Northing:F1}) must be inside headland (20-180)");
        }

        foreach (var pt in _service.InnerBoundaryTrack)
        {
            Assert.That(IsPointInPolygon(pt.Easting, pt.Northing, headland), Is.True,
                $"Inner track ({pt.Easting:F1}, {pt.Northing:F1}) must be inside headland (20-180)");
        }

        // Outer track should be at roughly (tramWidth/2 - halfWheelTrack) = 11.1m from headland
        // So points should be within [31, 169] range approximately
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            Assert.That(pt.Easting, Is.GreaterThan(25).And.LessThan(175),
                $"Outer track easting {pt.Easting:F1} should be well inside headland");
            Assert.That(pt.Northing, Is.GreaterThan(25).And.LessThan(175),
                $"Outer track northing {pt.Northing:F1} should be well inside headland");
        }
    }

    [Test]
    public void BoundaryTramTracks_FormClosedLoop()
    {
        var fence = new List<Vec3>
        {
            new Vec3(0, 0, 0), new Vec3(100, 0, Math.PI / 2),
            new Vec3(100, 100, Math.PI), new Vec3(0, 100, 3 * Math.PI / 2),
            new Vec3(0, 0, 0)
        };

        ConfigurationStore.Instance.Tram.TramWidth = 12.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;

        _service.GenerateBoundaryTramTracks(fence);

        // Both tracks should form closed loops (first point == last point)
        if (_service.OuterBoundaryTrack.Count > 2)
        {
            var first = _service.OuterBoundaryTrack[0];
            var last = _service.OuterBoundaryTrack[^1];
            double dist = Math.Sqrt(Math.Pow(first.Easting - last.Easting, 2) +
                                    Math.Pow(first.Northing - last.Northing, 2));
            Assert.That(dist, Is.LessThan(0.1),
                $"Outer track should be closed. Gap: {dist:F3}m");
        }

        if (_service.InnerBoundaryTrack.Count > 2)
        {
            var first = _service.InnerBoundaryTrack[0];
            var last = _service.InnerBoundaryTrack[^1];
            double dist = Math.Sqrt(Math.Pow(first.Easting - last.Easting, 2) +
                                    Math.Pow(first.Northing - last.Northing, 2));
            Assert.That(dist, Is.LessThan(0.1),
                $"Inner track should be closed. Gap: {dist:F3}m");
        }
    }

    // ---------------------------------------------------------------
    // U-shaped field clipping tests
    // ---------------------------------------------------------------

    [Test]
    public void UShapedField_HorizontalTramLines_SplitAtBoundaryCrossings()
    {
        // U-shaped field: open at top, concave
        //   (0,100)---(40,100)    (60,100)---(100,100)
        //      |         |            |          |
        //      |         |            |          |
        //      |         (40,60)---(60,60)       |
        //      |                                 |
        //   (0,0)-----------------------------(100,0)
        var boundary = new List<Vec3>
        {
            new Vec3(0, 0, 0),
            new Vec3(100, 0, Math.PI / 2),
            new Vec3(100, 100, Math.PI / 2),
            new Vec3(60, 100, Math.PI),
            new Vec3(60, 60, 3 * Math.PI / 2),
            new Vec3(40, 60, Math.PI),
            new Vec3(40, 100, Math.PI / 2),
            new Vec3(0, 100, Math.PI),
            new Vec3(0, 0, 3 * Math.PI / 2),
        };

        _service.SetBoundaryFence(boundary);

        // Horizontal AB line through the middle (y-axis direction)
        // heading = PI/2 means pointing east, so perpendicular offset goes north/south
        var track = new Track
        {
            Name = "Horizontal",
            Points = new List<Vec3>
            {
                new Vec3(0, 50, Math.PI / 2),
                new Vec3(100, 50, Math.PI / 2)
            },
            Type = TrackType.ABLine
        };

        ConfigurationStore.Instance.Tram.TramWidth = 24.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;
        ConfigurationStore.Instance.Tram.DisplayMode = TramDisplayMode.All;

        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Test",
            TramWidth = 24.0,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            ReferenceTrackName = "Horizontal"
        };

        var lines = _service.GenerateForSystem(system, track, 200);

        // Lines at y~80 would cross the U-gap (40-60 range at y>60)
        // These should be split into separate segments, not a single line
        // that crosses outside the boundary
        foreach (var line in lines)
        {
            for (int i = 0; i < line.Count - 1; i++)
            {
                var p1 = line[i];
                var p2 = line[i + 1];

                // Check midpoint of each segment is inside boundary
                var mid = new Vec2((p1.Easting + p2.Easting) / 2, (p1.Northing + p2.Northing) / 2);
                bool midInside = IsPointInPolygon(mid.Easting, mid.Northing, boundary);

                // Allow small tolerance: segments near boundary edge might have
                // midpoints barely outside due to discretization
                double segLen = Math.Sqrt(Math.Pow(p2.Easting - p1.Easting, 2) +
                                          Math.Pow(p2.Northing - p1.Northing, 2));
                if (segLen > 5.0) // Only check segments longer than 5m
                {
                    Assert.That(midInside, Is.True,
                        $"Segment midpoint ({mid.Easting:F1}, {mid.Northing:F1}) should be inside " +
                        $"U-shaped boundary (segment length: {segLen:F1}m)");
                }
            }
        }

        Assert.That(lines.Count, Is.GreaterThan(0), "Should produce tram lines");
    }

    [Test]
    public void GenerateForSystem_WithBoundaryFence_AllPointsInsideFence()
    {
        // Square boundary
        var boundary = new List<Vec3>
        {
            new Vec3(0, 0, 0), new Vec3(200, 0, Math.PI / 2),
            new Vec3(200, 200, Math.PI), new Vec3(0, 200, 3 * Math.PI / 2),
            new Vec3(0, 0, 0)
        };

        _service.SetBoundaryFence(boundary);

        var track = new Track
        {
            Name = "Center",
            Points = new List<Vec3> { new Vec3(100, 0, 0), new Vec3(100, 200, 0) },
            Type = TrackType.ABLine
        };

        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Test",
            TramWidth = 24.0,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
        };

        var lines = _service.GenerateForSystem(system, track, 200);

        Assert.That(lines.Count, Is.GreaterThan(0));

        foreach (var line in lines)
        {
            foreach (var pt in line)
            {
                Assert.That(pt.Easting, Is.GreaterThanOrEqualTo(-0.5).And.LessThanOrEqualTo(200.5),
                    $"Point ({pt.Easting:F1}, {pt.Northing:F1}) easting should be within boundary");
                Assert.That(pt.Northing, Is.GreaterThanOrEqualTo(-0.5).And.LessThanOrEqualTo(200.5),
                    $"Point ({pt.Easting:F1}, {pt.Northing:F1}) northing should be within boundary");
            }
        }
    }
}
