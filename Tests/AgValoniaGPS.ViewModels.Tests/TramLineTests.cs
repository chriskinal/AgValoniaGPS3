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

    // ---------------------------------------------------------------
    // TramSystemFileService save/load round-trip tests
    // ---------------------------------------------------------------

    [Test]
    public void TramSystemFileService_SaveAndLoad_PreservesAllProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tram_sys_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var systems = new List<AgValoniaGPS.Models.Tram.TramSystem>
            {
                new()
                {
                    Name = "Sprayer Tracks",
                    ReferenceTrackName = "AB Line 1",
                    ReferenceBoundaryIndex = -1,
                    TramWidth = 36.0,
                    Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
                    Offset = 2.5,
                    Direction = AgValoniaGPS.Models.Tram.TramDirection.Left,
                    PassCount = 5,
                    IsEnabled = true
                },
                new()
                {
                    Name = "Fertilizer",
                    ReferenceTrackName = "Curve 1",
                    ReferenceBoundaryIndex = -1,
                    TramWidth = 18.0,
                    Mode = AgValoniaGPS.Models.Tram.TramSystemMode.Edge,
                    Offset = -1.0,
                    Direction = AgValoniaGPS.Models.Tram.TramDirection.Right,
                    PassCount = 3,
                    IsEnabled = false
                }
            };

            TramSystemFileService.Save(tempDir, systems);
            var loaded = TramSystemFileService.Load(tempDir);

            Assert.That(loaded.Count, Is.EqualTo(2));

            Assert.That(loaded[0].Name, Is.EqualTo("Sprayer Tracks"));
            Assert.That(loaded[0].ReferenceTrackName, Is.EqualTo("AB Line 1"));
            Assert.That(loaded[0].ReferenceBoundaryIndex, Is.EqualTo(-1));
            Assert.That(loaded[0].TramWidth, Is.EqualTo(36.0).Within(0.001));
            Assert.That(loaded[0].Mode, Is.EqualTo(AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine));
            Assert.That(loaded[0].Offset, Is.EqualTo(2.5).Within(0.001));
            Assert.That(loaded[0].Direction, Is.EqualTo(AgValoniaGPS.Models.Tram.TramDirection.Left));
            Assert.That(loaded[0].PassCount, Is.EqualTo(5));
            Assert.That(loaded[0].IsEnabled, Is.True);

            Assert.That(loaded[1].Name, Is.EqualTo("Fertilizer"));
            Assert.That(loaded[1].Mode, Is.EqualTo(AgValoniaGPS.Models.Tram.TramSystemMode.Edge));
            Assert.That(loaded[1].Direction, Is.EqualTo(AgValoniaGPS.Models.Tram.TramDirection.Right));
            Assert.That(loaded[1].IsEnabled, Is.False);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void TramSystemFileService_SaveEmptyList_LoadReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tram_sys_empty_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            TramSystemFileService.Save(tempDir, new List<AgValoniaGPS.Models.Tram.TramSystem>());
            var loaded = TramSystemFileService.Load(tempDir);

            Assert.That(loaded.Count, Is.EqualTo(0));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void TramSystemFileService_LoadNonexistentFile_ReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tram_sys_nofile_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var loaded = TramSystemFileService.Load(tempDir);
            Assert.That(loaded.Count, Is.EqualTo(0));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void TramSystemFileService_BoundarySystem_PreservesBoundaryIndex()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tram_sys_bnd_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var systems = new List<AgValoniaGPS.Models.Tram.TramSystem>
            {
                new()
                {
                    Name = "Boundary Tram",
                    ReferenceTrackName = null,
                    ReferenceBoundaryIndex = 0,
                    TramWidth = 24.0,
                    Mode = AgValoniaGPS.Models.Tram.TramSystemMode.Edge,
                    PassCount = 2,
                    IsEnabled = true
                }
            };

            TramSystemFileService.Save(tempDir, systems);
            var loaded = TramSystemFileService.Load(tempDir);

            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].ReferenceBoundaryIndex, Is.EqualTo(0));
            Assert.That(loaded[0].ReferenceTrackName, Is.Null);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void TramSystemFileService_TrackLineVsEdgeMode_PersistsCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "tram_sys_mode_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var systems = new List<AgValoniaGPS.Models.Tram.TramSystem>
            {
                new()
                {
                    Name = "TrackLineSystem",
                    Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
                    TramWidth = 24.0
                },
                new()
                {
                    Name = "EdgeSystem",
                    Mode = AgValoniaGPS.Models.Tram.TramSystemMode.Edge,
                    TramWidth = 24.0
                }
            };

            TramSystemFileService.Save(tempDir, systems);
            var loaded = TramSystemFileService.Load(tempDir);

            Assert.That(loaded[0].Mode, Is.EqualTo(AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine));
            Assert.That(loaded[1].Mode, Is.EqualTo(AgValoniaGPS.Models.Tram.TramSystemMode.Edge));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ---------------------------------------------------------------
    // GenerateForSystem generation tests
    // ---------------------------------------------------------------

    private static Track MakeNorthSouthABLine()
    {
        return new Track
        {
            Name = "NS Line",
            Points = new List<Vec3> { new Vec3(0, 0, 0), new Vec3(0, 200, 0) },
            Type = TrackType.ABLine
        };
    }

    [Test]
    public void GenerateForSystem_TrackMode_Pass0AtOffset0_GeneratesLinesAtReference()
    {
        // Track mode pass 0 has baseOffset = tramWidth*0 + 0 = 0
        // So wheel tracks are at +/- halfWheelTrack from the reference line (easting=0)
        var track = MakeNorthSouthABLine();
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Test",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 1,
            IsEnabled = true
        };

        var lines = _service.GenerateForSystem(system, track, 200);

        Assert.That(lines.Count, Is.GreaterThan(0), "Should generate lines for pass 0");

        // Pass 0 in TrackLine mode: baseOffset = 0, wheel tracks at +/- 0.9
        double halfWheelTrack = ConfigurationStore.Instance.Vehicle.TrackWidth / 2.0;
        bool hasNearCenter = lines.Any(line =>
            line.Count > 0 && Math.Abs(line[line.Count / 2].Easting) < halfWheelTrack + 1.0);
        Assert.That(hasNearCenter, Is.True,
            "Track mode pass 0 should have lines near the reference (within halfWheelTrack)");
    }

    [Test]
    public void GenerateForSystem_EdgeMode_OffsetByHalfTramWidth()
    {
        // Edge mode pass 0 has baseOffset = tramWidth/2 = 12.0
        var track = MakeNorthSouthABLine();
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Test",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.Edge,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 1,
            IsEnabled = true
        };

        var lines = _service.GenerateForSystem(system, track, 200);

        Assert.That(lines.Count, Is.GreaterThan(0));

        // Edge mode pass 0: baseOffset=12, wheel tracks at 12-0.9=11.1 and 12+0.9=12.9
        double halfWheelTrack = ConfigurationStore.Instance.Vehicle.TrackWidth / 2.0;
        double expectedCenter = 24.0 / 2.0;
        bool hasAtExpectedOffset = lines.Any(line =>
            line.Count > 0 &&
            Math.Abs(Math.Abs(line[line.Count / 2].Easting) - (expectedCenter - halfWheelTrack)) < 2.0);
        Assert.That(hasAtExpectedOffset, Is.True,
            "Edge mode pass 0 should have lines near tramWidth/2 from reference");
    }

    [Test]
    public void GenerateForSystem_LeftDirection_OnlyNegativeSideAndCenter()
    {
        var track = MakeNorthSouthABLine();
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Left Only",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Left,
            PassCount = 3,
            IsEnabled = true
        };

        var lines = _service.GenerateForSystem(system, track, 200);

        Assert.That(lines.Count, Is.GreaterThan(0));

        // Left direction generates negative offsets. In TrackLine mode the reference
        // line heading is 0 (north), so perpendicular is east. Negative offset = west (easting < 0).
        // Pass 0 at offset 0 produces lines at +/- halfWheelTrack.
        // Passes 1,2 at -24, -48 produce lines further negative.
        // No line midpoint should be far positive.
        double halfWheelTrack = ConfigurationStore.Instance.Vehicle.TrackWidth / 2.0;
        foreach (var line in lines)
        {
            if (line.Count > 0)
            {
                double midEasting = line[line.Count / 2].Easting;
                // Allow pass 0 center lines near 0 but no large positive offsets
                Assert.That(midEasting, Is.LessThan(halfWheelTrack + 2.0),
                    $"Left-only should not have lines at large positive offset, got easting={midEasting:F1}");
            }
        }
    }

    [Test]
    public void GenerateForSystem_RightDirection_OnlyPositiveSide()
    {
        var track = MakeNorthSouthABLine();
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Right Only",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Right,
            PassCount = 3,
            IsEnabled = true
        };

        var lines = _service.GenerateForSystem(system, track, 200);

        Assert.That(lines.Count, Is.GreaterThan(0));

        double halfWheelTrack = ConfigurationStore.Instance.Vehicle.TrackWidth / 2.0;
        foreach (var line in lines)
        {
            if (line.Count > 0)
            {
                double midEasting = line[line.Count / 2].Easting;
                Assert.That(midEasting, Is.GreaterThan(-halfWheelTrack - 2.0),
                    $"Right-only should not have lines at large negative offset, got easting={midEasting:F1}");
            }
        }
    }

    [Test]
    public void GenerateForSystem_Symmetric_GeneratesBothSides()
    {
        var track = MakeNorthSouthABLine();
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Symmetric",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.Edge,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 2,
            IsEnabled = true
        };

        var lines = _service.GenerateForSystem(system, track, 200);

        Assert.That(lines.Count, Is.GreaterThan(0));

        bool hasPositive = false, hasNegative = false;
        foreach (var line in lines)
        {
            if (line.Count > 0)
            {
                double mid = line[line.Count / 2].Easting;
                if (mid > 5.0) hasPositive = true;
                if (mid < -5.0) hasNegative = true;
            }
        }
        Assert.That(hasPositive, Is.True, "Symmetric should have lines on positive side");
        Assert.That(hasNegative, Is.True, "Symmetric should have lines on negative side");
    }

    [Test]
    public void GenerateForSystem_PassCount2_GeneratesMoreLinesThanPassCount1()
    {
        var track = MakeNorthSouthABLine();

        var system1 = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "1 Pass",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 1,
            IsEnabled = true
        };

        var system2 = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "2 Pass",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 2,
            IsEnabled = true
        };

        var lines1 = _service.GenerateForSystem(system1, track, 200);
        var lines2 = _service.GenerateForSystem(system2, track, 200);

        Assert.That(lines2.Count, Is.GreaterThan(lines1.Count),
            $"PassCount=2 ({lines2.Count} lines) should generate more lines than PassCount=1 ({lines1.Count} lines)");
    }

    [Test]
    public void GenerateForSystem_DisabledSystem_IsNotUsedDirectly()
    {
        // GenerateForSystem itself does not check IsEnabled -- callers do.
        // But we verify a disabled system object still has IsEnabled=false.
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Disabled",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 1,
            IsEnabled = false
        };

        Assert.That(system.IsEnabled, Is.False,
            "Disabled system should have IsEnabled=false for callers to filter on");

        // If callers check IsEnabled before calling GenerateForSystem,
        // no lines are produced. Simulate that pattern:
        var lines = system.IsEnabled
            ? _service.GenerateForSystem(system, MakeNorthSouthABLine(), 200)
            : new List<List<Vec2>>();

        Assert.That(lines.Count, Is.EqualTo(0),
            "Disabled system should produce no lines when caller checks IsEnabled");
    }

    [Test]
    public void GenerateForSystem_EmptyReferenceTrack_ProducesNoLines()
    {
        var emptyTrack = new Track
        {
            Name = "Empty",
            Points = new List<Vec3>(),
            Type = TrackType.ABLine
        };

        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Test",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 2,
            IsEnabled = true
        };

        var lines = _service.GenerateForSystem(system, emptyTrack, 200);

        Assert.That(lines.Count, Is.EqualTo(0),
            "Empty reference track should produce no lines");
    }

    [Test]
    public void GenerateForSystem_NullReferenceTrack_ProducesNoLines()
    {
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Test",
            TramWidth = 24.0,
            PassCount = 2,
            IsEnabled = true
        };

        var lines = _service.GenerateForSystem(system, null!, 200);

        Assert.That(lines.Count, Is.EqualTo(0),
            "Null reference track should produce no lines");
    }

    // ---------------------------------------------------------------
    // Boundary tram track mode and pass count tests
    // ---------------------------------------------------------------

    private static List<Vec3> MakeCircularBoundary(double radius = 100, int n = 60)
    {
        var fence = new List<Vec3>();
        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n;
            fence.Add(new Vec3(radius * Math.Cos(angle), radius * Math.Sin(angle),
                angle + Math.PI / 2));
        }
        fence.Add(fence[0]); // close
        return fence;
    }

    [Test]
    public void GenerateBoundaryTramTracks_TrackMode_OuterWheelAtBoundaryEdge()
    {
        // Track mode: pass center at halfWheelTrack from boundary,
        // so outer wheel is at halfWheelTrack - halfWheelTrack = 0 (right at boundary).
        // The outer track offset = passCenter - halfWheelTrack = 0, meaning it follows boundary.
        var fence = MakeCircularBoundary(100);

        ConfigurationStore.Instance.Tram.TramWidth = 24.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;

        _service.GenerateBoundaryTramTracks(fence, passCount: 1,
            mode: AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine);

        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(0));
        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(0));

        // Outer track should be very close to the original boundary (offset ~0)
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            double distFromCenter = Math.Sqrt(pt.Easting * pt.Easting + pt.Northing * pt.Northing);
            // Outer track is on or very near boundary (radius 100), inset by ~0
            Assert.That(distFromCenter, Is.LessThanOrEqualTo(101.0),
                $"Outer track point should be near boundary, dist={distFromCenter:F1}");
        }

        // Inner track should be offset inward by the full wheel track width
        foreach (var pt in _service.InnerBoundaryTrack)
        {
            double distFromCenter = Math.Sqrt(pt.Easting * pt.Easting + pt.Northing * pt.Northing);
            // Inner wheel at halfWheelTrack + halfWheelTrack = 1.8m inward from boundary
            Assert.That(distFromCenter, Is.LessThan(100.0),
                $"Inner track should be inset from boundary, dist={distFromCenter:F1}");
        }
    }

    [Test]
    public void GenerateBoundaryTramTracks_EdgeMode_CenterAtHalfTramWidth()
    {
        // Edge mode: pass center at tramWidth/2 from boundary
        var fence = MakeCircularBoundary(100);

        ConfigurationStore.Instance.Tram.TramWidth = 24.0;
        ConfigurationStore.Instance.Vehicle.TrackWidth = 1.8;

        _service.GenerateBoundaryTramTracks(fence, passCount: 1,
            mode: AgValoniaGPS.Models.Tram.TramSystemMode.Edge);

        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(0));
        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(0));

        double halfTramWidth = 12.0;
        double halfWheelTrack = 0.9;

        // Outer track at passCenter - halfWheelTrack = 12 - 0.9 = 11.1 inward
        foreach (var pt in _service.OuterBoundaryTrack)
        {
            double distFromCenter = Math.Sqrt(pt.Easting * pt.Easting + pt.Northing * pt.Northing);
            double expectedDist = 100.0 - (halfTramWidth - halfWheelTrack);
            Assert.That(distFromCenter, Is.EqualTo(expectedDist).Within(3.0),
                $"Edge mode outer track should be ~{expectedDist:F1}m from center, got {distFromCenter:F1}");
        }

        // Inner track at passCenter + halfWheelTrack = 12 + 0.9 = 12.9 inward
        foreach (var pt in _service.InnerBoundaryTrack)
        {
            double distFromCenter = Math.Sqrt(pt.Easting * pt.Easting + pt.Northing * pt.Northing);
            double expectedDist = 100.0 - (halfTramWidth + halfWheelTrack);
            Assert.That(distFromCenter, Is.EqualTo(expectedDist).Within(3.0),
                $"Edge mode inner track should be ~{expectedDist:F1}m from center, got {distFromCenter:F1}");
        }
    }

    [Test]
    public void GenerateBoundaryTramTracks_PassCount1_ExactlyTwoTracks()
    {
        var fence = MakeCircularBoundary(100);

        _service.GenerateBoundaryTramTracks(fence, passCount: 1,
            mode: AgValoniaGPS.Models.Tram.TramSystemMode.Edge);

        // passCount=1: pass 0 goes to outer+inner boundary tracks
        // No additional parallel lines added
        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(0),
            "Should have outer boundary track");
        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(0),
            "Should have inner boundary track");
        Assert.That(_service.ParallelTramLines.Count, Is.EqualTo(0),
            "PassCount=1 should have no additional parallel tram lines (only boundary tracks)");
    }

    [Test]
    public void GenerateBoundaryTramTracks_PassCount2_FourTracksTotal()
    {
        _service.Clear();
        var fence = MakeCircularBoundary(100);

        _service.GenerateBoundaryTramTracks(fence, passCount: 2,
            mode: AgValoniaGPS.Models.Tram.TramSystemMode.Edge);

        // passCount=2: pass 0 -> outer+inner boundary tracks, pass 1 -> 2 parallel lines
        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(0),
            "Should have outer boundary track");
        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(0),
            "Should have inner boundary track");
        Assert.That(_service.BoundaryExtraLines.Count, Is.EqualTo(2),
            "PassCount=2 should add 2 boundary extra lines for the second pass");
    }

    [Test]
    public void GenerateBoundaryTramTracks_MultiPass_GeneratesConcentricTracks()
    {
        _service.Clear();
        var fence = MakeCircularBoundary(100);

        _service.GenerateBoundaryTramTracks(fence, passCount: 3,
            mode: AgValoniaGPS.Models.Tram.TramSystemMode.Edge);

        // Pass 0: outer + inner boundary tracks
        // Pass 1: 2 parallel lines
        // Pass 2: 2 more parallel lines
        Assert.That(_service.OuterBoundaryTrack.Count, Is.GreaterThan(0));
        Assert.That(_service.InnerBoundaryTrack.Count, Is.GreaterThan(0));
        Assert.That(_service.BoundaryExtraLines.Count, Is.EqualTo(4),
            "PassCount=3 should add 4 boundary extra lines (2 per additional pass)");

        // Verify concentric: later passes should be further inward (smaller radius)
        if (_service.BoundaryExtraLines.Count >= 4)
        {
            double pass1Dist = Math.Sqrt(
                _service.BoundaryExtraLines[0][0].Easting * _service.BoundaryExtraLines[0][0].Easting +
                _service.BoundaryExtraLines[0][0].Northing * _service.BoundaryExtraLines[0][0].Northing);
            double pass2Dist = Math.Sqrt(
                _service.BoundaryExtraLines[2][0].Easting * _service.BoundaryExtraLines[2][0].Easting +
                _service.BoundaryExtraLines[2][0].Northing * _service.BoundaryExtraLines[2][0].Northing);

            Assert.That(pass2Dist, Is.LessThan(pass1Dist),
                $"Pass 2 track ({pass2Dist:F1}m) should be more inward than pass 1 ({pass1Dist:F1}m)");
        }
    }

    // ---------------------------------------------------------------
    // Integration: missing reference and multiple systems
    // ---------------------------------------------------------------

    [Test]
    public void GenerateForSystem_MissingReferenceTrack_ProducesNoLines()
    {
        // When the system references a track by name but the caller passes
        // null or an empty track because the name did not resolve,
        // no lines should be produced.
        var system = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Missing Ref",
            ReferenceTrackName = "Nonexistent Track",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 3,
            IsEnabled = true
        };

        // Simulate: track lookup returned null
        var lines = _service.GenerateForSystem(system, null!, 200);
        Assert.That(lines.Count, Is.EqualTo(0),
            "Null reference track (missing) should produce no lines");

        // Simulate: track lookup returned empty track
        var emptyTrack = new Track { Name = "Empty", Points = new List<Vec3>(), Type = TrackType.ABLine };
        lines = _service.GenerateForSystem(system, emptyTrack, 200);
        Assert.That(lines.Count, Is.EqualTo(0),
            "Empty reference track (missing) should produce no lines");
    }

    [Test]
    public void MultipleSystems_GenerateIndependentLineSets()
    {
        var track = MakeNorthSouthABLine();

        var system1 = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Sprayer",
            TramWidth = 24.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Symmetric,
            PassCount = 2,
            IsEnabled = true
        };

        var system2 = new AgValoniaGPS.Models.Tram.TramSystem
        {
            Name = "Fertilizer",
            TramWidth = 36.0,
            Mode = AgValoniaGPS.Models.Tram.TramSystemMode.Edge,
            Direction = AgValoniaGPS.Models.Tram.TramDirection.Right,
            PassCount = 3,
            IsEnabled = true
        };

        var lines1 = _service.GenerateForSystem(system1, track, 200);
        var lines2 = _service.GenerateForSystem(system2, track, 200);

        Assert.That(lines1.Count, Is.GreaterThan(0), "System 1 should produce lines");
        Assert.That(lines2.Count, Is.GreaterThan(0), "System 2 should produce lines");

        // Different tram widths and modes should produce different line positions
        // Collect midpoint eastings from each system
        var eastings1 = lines1.Where(l => l.Count > 0)
            .Select(l => Math.Round(l[l.Count / 2].Easting, 0)).OrderBy(e => e).ToList();
        var eastings2 = lines2.Where(l => l.Count > 0)
            .Select(l => Math.Round(l[l.Count / 2].Easting, 0)).OrderBy(e => e).ToList();

        // They should not be identical sets (different widths, different modes, different directions)
        Assert.That(eastings1, Is.Not.EqualTo(eastings2),
            "Two different systems should produce different line positions");
    }

    [Test]
    public void TramConfig_Systems_IsObservableCollection()
    {
        var config = new TramConfig();
        Assert.That(config.Systems, Is.Not.Null);
        Assert.That(config.Systems.Count, Is.EqualTo(0));

        config.Systems.Add(new AgValoniaGPS.Models.Tram.TramSystem { Name = "Test" });
        Assert.That(config.Systems.Count, Is.EqualTo(1));
        Assert.That(config.Systems[0].Name, Is.EqualTo("Test"));
    }

    [Test]
    public void TramSystem_DefaultValues_AreCorrect()
    {
        var system = new AgValoniaGPS.Models.Tram.TramSystem();

        Assert.That(system.Name, Is.EqualTo(""));
        Assert.That(system.ReferenceTrackName, Is.Null);
        Assert.That(system.ReferenceBoundaryIndex, Is.EqualTo(-1));
        Assert.That(system.TramWidth, Is.EqualTo(24.0));
        Assert.That(system.Mode, Is.EqualTo(AgValoniaGPS.Models.Tram.TramSystemMode.TrackLine));
        Assert.That(system.Offset, Is.EqualTo(0.0));
        Assert.That(system.Direction, Is.EqualTo(AgValoniaGPS.Models.Tram.TramDirection.Symmetric));
        Assert.That(system.PassCount, Is.EqualTo(0));
        Assert.That(system.IsEnabled, Is.True);
    }

    [Test]
    public void TramSystem_PassCount_ClampsToZero()
    {
        var system = new AgValoniaGPS.Models.Tram.TramSystem();
        system.PassCount = -5;

        Assert.That(system.PassCount, Is.EqualTo(0),
            "PassCount should clamp negative values to 0");
    }
}
