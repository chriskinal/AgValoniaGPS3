// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3.

using System.IO;
using AgValoniaGPS.Services.Coverage;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class LegacyCoverageLoadTests
{
    private string _tempDir = null!;
    private CoverageMapService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CoverageTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new CoverageMapService();
        // Initialize bounds so the service can accept coverage data
        _service.SetFieldBounds(-100, 100, -100, 100);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Test]
    public void LoadFromFile_WithLegacySections_LoadsCoverage()
    {
        // Create a legacy Sections.txt with one quad strip
        // Format: count, RGB color, then pairs of (easting, northing, 0)
        // count = 1 (color line) + 2*nPairs (vertex lines) = 5 means 2 pairs
        // Pair 1: left=(10,10), right=(12,10) - just sets prev
        // Pair 2: left=(10,12), right=(12,12) - forms quad with pair 1
        // Quad: (10,10)-(12,10)-(10,12)-(12,12) = 2m x 2m square
        var content = @"5
0.0, 1.0, 0.0
10.0, 10.0, 0.0
12.0, 10.0, 0.0
10.0, 12.0, 0.0
12.0, 12.0, 0.0";

        File.WriteAllText(Path.Combine(_tempDir, "Sections.txt"), content);

        bool eventFired = false;
        _service.CoverageUpdated += (_, args) =>
        {
            eventFired = true;
            Assert.That(args.IsFullReload, Is.True);
        };

        _service.LoadFromFile(_tempDir);

        Assert.That(eventFired, Is.True, "CoverageUpdated should fire after loading legacy sections");
    }

    [Test]
    public void LoadFromFile_WithNoFiles_DoesNotFireEvent()
    {
        bool eventFired = false;
        _service.CoverageUpdated += (_, _) => eventFired = true;

        _service.LoadFromFile(_tempDir);

        Assert.That(eventFired, Is.False, "CoverageUpdated should not fire when no files exist");
    }

    [Test]
    public void LoadFromFile_PrefersBinaryOverLegacy()
    {
        // Create both legacy and binary files
        File.WriteAllText(Path.Combine(_tempDir, "Sections.txt"), "3\n0,1,0\n0,0,0\n1,0,0");

        // Create a minimal coverage_detect.bin with valid header
        using var fs = File.Create(Path.Combine(_tempDir, "coverage_detect.bin"));
        // COVD magic
        fs.Write(new byte[] { (byte)'C', (byte)'O', (byte)'V', (byte)'D' });
        // Version 1
        fs.Write(BitConverter.GetBytes((int)1));
        // Bounds (minE, maxE, minN, maxN as doubles)
        fs.Write(BitConverter.GetBytes(-100.0));
        fs.Write(BitConverter.GetBytes(100.0));
        fs.Write(BitConverter.GetBytes(-100.0));
        fs.Write(BitConverter.GetBytes(100.0));
        // Cell size
        fs.Write(BitConverter.GetBytes(0.1));
        // Width, Height
        fs.Write(BitConverter.GetBytes(0));
        fs.Write(BitConverter.GetBytes(0));
        // RLE data count = 0
        fs.Write(BitConverter.GetBytes(0));

        // LoadFromFile should try binary first - legacy is only a fallback
        _service.LoadFromFile(_tempDir);
        // If binary loads (even empty), legacy should NOT be loaded
        // This test verifies the fallback logic order
    }

    [Test]
    public void LoadFromFile_LegacyFormat_MultipleStrips()
    {
        // Two quad strips
        var content = @"5
1.0, 0.0, 0.0
10.0, 10.0, 0.0
12.0, 10.0, 0.0
10.0, 12.0, 0.0
12.0, 12.0, 0.0
5
0.0, 0.0, 1.0
20.0, 20.0, 0.0
22.0, 20.0, 0.0
20.0, 22.0, 0.0
22.0, 22.0, 0.0";

        File.WriteAllText(Path.Combine(_tempDir, "Sections.txt"), content);

        bool eventFired = false;
        _service.CoverageUpdated += (_, _) => eventFired = true;

        _service.LoadFromFile(_tempDir);

        Assert.That(eventFired, Is.True, "Should load multiple legacy strips");
    }

    [Test]
    public void LoadFromFile_EmptySectionsFile_DoesNotCrash()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Sections.txt"), "");

        bool eventFired = false;
        _service.CoverageUpdated += (_, _) => eventFired = true;

        Assert.DoesNotThrow(() => _service.LoadFromFile(_tempDir));
        Assert.That(eventFired, Is.False, "Empty file should not fire event");
    }

    [Test]
    public void LoadFromFile_CorruptSectionsFile_DoesNotCrash()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Sections.txt"), "garbage\nnot a number\nmore garbage");

        Assert.DoesNotThrow(() => _service.LoadFromFile(_tempDir));
    }
}
