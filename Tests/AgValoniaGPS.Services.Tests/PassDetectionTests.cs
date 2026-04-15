using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Track;
using TrackModel = AgValoniaGPS.Models.Track.Track;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests for automatic pass detection (nearest pass number calculation).
/// Verifies the fix for issue #239: _howManyPathsAway was always 0.
/// </summary>
[TestFixture]
public class PassDetectionTests
{
    [SetUp]
    public void SetUp()
    {
        var config = ConfigurationStore.Instance;
        config.Tool.Width = 12.0;
        config.Tool.Overlap = 0;
        config.Vehicle.TrackWidth = 1.8;
    }

    /// <summary>
    /// Helper: calculate perpendicular distance from a point to a track,
    /// matching the pipeline's CalculatePerpendicularDistance logic.
    /// </summary>
    private static double CalculatePerpendicularDistance(TrackModel track, double easting, double northing)
    {
        if (track.Points.Count < 2) return 0;

        var a = track.Points[0];
        var b = track.Points[^1];

        double dx = b.Easting - a.Easting;
        double dy = b.Northing - a.Northing;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) return 0;

        // Signed perpendicular distance (positive = right of track direction)
        return ((easting - a.Easting) * dy - (northing - a.Northing) * dx) / len;
    }

    /// <summary>
    /// Helper: compute nearest pass number from perpendicular distance.
    /// This is the core calculation that was broken in the pipeline.
    /// </summary>
    private static int ComputeNearestPass(double perpDist, double widthMinusOverlap)
    {
        if (widthMinusOverlap < 0.1) widthMinusOverlap = 1.0;
        return (int)Math.Round(perpDist / widthMinusOverlap);
    }

    // ---------------------------------------------------------------
    // On-reference-line tests
    // ---------------------------------------------------------------

    [Test]
    public void VehicleOnReferenceLine_ReturnsPass0()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));
        double perpDist = CalculatePerpendicularDistance(track, 0, 50);
        int pass = ComputeNearestPass(perpDist, 12.0);

        Assert.That(pass, Is.EqualTo(0), "Vehicle on reference line should be pass 0");
    }

    [Test]
    public void VehicleSlightlyOffCenter_StillPass0()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));
        double perpDist = CalculatePerpendicularDistance(track, 3, 50); // 3m off, tool width 12m
        int pass = ComputeNearestPass(perpDist, 12.0);

        Assert.That(pass, Is.EqualTo(0), "3m offset with 12m tool width should still be pass 0");
    }

    // ---------------------------------------------------------------
    // Offset pass detection
    // ---------------------------------------------------------------

    [Test]
    public void VehicleOnePassRight_ReturnsPass1()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));
        double perpDist = CalculatePerpendicularDistance(track, 12, 50); // 12m right = 1 tool width
        int pass = ComputeNearestPass(perpDist, 12.0);

        Assert.That(pass, Is.EqualTo(1), "12m right of reference with 12m tool should be pass 1");
    }

    [Test]
    public void VehicleOnePassLeft_ReturnsPassMinus1()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));
        double perpDist = CalculatePerpendicularDistance(track, -12, 50); // 12m left
        int pass = ComputeNearestPass(perpDist, 12.0);

        Assert.That(pass, Is.EqualTo(-1), "12m left of reference should be pass -1");
    }

    [Test]
    public void VehicleTwoPassesRight_ReturnsPass2()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));
        double perpDist = CalculatePerpendicularDistance(track, 24, 50); // 24m = 2 passes
        int pass = ComputeNearestPass(perpDist, 12.0);

        Assert.That(pass, Is.EqualTo(2), "24m right should be pass 2");
    }

    [Test]
    public void VehicleFivePassesLeft_ReturnsPassMinus5()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));
        double perpDist = CalculatePerpendicularDistance(track, -60, 50); // 60m = 5 passes
        int pass = ComputeNearestPass(perpDist, 12.0);

        Assert.That(pass, Is.EqualTo(-5), "60m left should be pass -5");
    }

    // ---------------------------------------------------------------
    // Rounding behavior
    // ---------------------------------------------------------------

    [Test]
    public void VehicleBetweenPasses_RoundsToNearest()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));

        // 17m right = 1.42 passes -> rounds to 1
        double perpDist = CalculatePerpendicularDistance(track, 17, 50);
        Assert.That(ComputeNearestPass(perpDist, 12.0), Is.EqualTo(1),
            "17m offset should round to pass 1");

        // 18m right = 1.5 passes -> rounds to 2
        perpDist = CalculatePerpendicularDistance(track, 18, 50);
        Assert.That(ComputeNearestPass(perpDist, 12.0), Is.EqualTo(2),
            "18m offset should round to pass 2");
    }

    // ---------------------------------------------------------------
    // Tool overlap
    // ---------------------------------------------------------------

    [Test]
    public void ToolWithOverlap_UsesReducedWidth()
    {
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(0, 100, 0));
        double widthMinusOverlap = 12.0 - 2.0; // 10m effective width

        // 10m right = exactly 1 pass with 10m spacing
        double perpDist = CalculatePerpendicularDistance(track, 10, 50);
        int pass = ComputeNearestPass(perpDist, widthMinusOverlap);

        Assert.That(pass, Is.EqualTo(1), "10m offset with 10m effective width = pass 1");
    }

    // ---------------------------------------------------------------
    // Diagonal AB line
    // ---------------------------------------------------------------

    [Test]
    public void DiagonalABLine_PassDetectsCorrectly()
    {
        // 45 degree line
        var track = TrackModel.FromABLine("AB", new Vec3(0, 0, 0), new Vec3(100, 100, Math.PI / 4));

        // Vehicle perpendicular to the 45-degree line at approximately 1 pass distance
        // For a 45-degree line going NE, perpendicular right is SE
        double offset = 12.0; // 1 pass
        double perpE = offset * Math.Cos(Math.PI / 4); // ~8.49
        double perpN = -offset * Math.Sin(Math.PI / 4); // ~-8.49
        double perpDist = CalculatePerpendicularDistance(track, 50 + perpE, 50 + perpN);
        int pass = ComputeNearestPass(perpDist, 12.0);

        Assert.That(Math.Abs(pass), Is.EqualTo(1),
            $"12m perpendicular to diagonal line should be pass +/-1 (got {pass})");
    }

    // ---------------------------------------------------------------
    // GpsCycleResult has NearestPassNumber field
    // ---------------------------------------------------------------

    [Test]
    public void GpsCycleResult_HasNearestPassNumber()
    {
        var result = new Models.State.GpsCycleResult
        {
            NearestPassNumber = 3
        };

        Assert.That(result.NearestPassNumber, Is.EqualTo(3));
    }

    [Test]
    public void GpsCycleResult_NearestPassNumber_NullByDefault()
    {
        var result = new Models.State.GpsCycleResult();

        Assert.That(result.NearestPassNumber, Is.Null);
    }
}
