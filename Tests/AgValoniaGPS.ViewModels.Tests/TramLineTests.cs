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
}
