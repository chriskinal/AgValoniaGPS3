using AgValoniaGPS.Services.Track;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class SwathOrderingTests
{
    [Test]
    public void Boustrophedon_ReturnsSequential()
    {
        var result = SwathOrderingService.GenerateSequence(5, SwathPattern.Boustrophedon);
        Assert.That(result, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public void Snake_5Tracks_CorrectPattern()
    {
        var result = SwathOrderingService.GenerateSequence(5, SwathPattern.Snake);
        // Every adjacent pair should differ by 2 (except boundary transition)
        // No adjacent tracks worked consecutively
        Assert.That(result, Has.Count.EqualTo(5));
        Assert.That(result.Distinct().Count(), Is.EqualTo(5), "All tracks visited exactly once");

        // Log for debugging
        Console.WriteLine($"Snake(5): [{string.Join(", ", result)}]");
    }

    [Test]
    public void Snake_10Tracks_CorrectPattern()
    {
        var result = SwathOrderingService.GenerateSequence(10, SwathPattern.Snake);
        Assert.That(result, Has.Count.EqualTo(10));
        Assert.That(result.Distinct().Count(), Is.EqualTo(10), "All tracks visited exactly once");

        // Verify no adjacent tracks are worked consecutively (except possibly at boundary)
        int adjacentPairs = 0;
        for (int i = 0; i < result.Count - 1; i++)
        {
            if (Math.Abs(result[i] - result[i + 1]) == 1)
                adjacentPairs++;
        }
        // At most 1 adjacent pair (the boundary transition)
        Assert.That(adjacentPairs, Is.LessThanOrEqualTo(1),
            $"Should have at most 1 adjacent transition, got {adjacentPairs}. Sequence: [{string.Join(", ", result)}]");

        Console.WriteLine($"Snake(10): [{string.Join(", ", result)}]");
    }

    [Test]
    public void Snake_6Tracks_AllVisited()
    {
        var result = SwathOrderingService.GenerateSequence(6, SwathPattern.Snake);
        Assert.That(result, Has.Count.EqualTo(6));
        Assert.That(result.Distinct().Count(), Is.EqualTo(6));
        Console.WriteLine($"Snake(6): [{string.Join(", ", result)}]");
    }

    [Test]
    public void Snake_SingleTrack_ReturnsSingle()
    {
        var result = SwathOrderingService.GenerateSequence(1, SwathPattern.Snake);
        Assert.That(result, Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void Snake_TwoTracks_ReturnsBoth()
    {
        var result = SwathOrderingService.GenerateSequence(2, SwathPattern.Snake);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Distinct().Count(), Is.EqualTo(2));
        Console.WriteLine($"Snake(2): [{string.Join(", ", result)}]");
    }

    [Test]
    public void Spiral_Size2_6Tracks()
    {
        var result = SwathOrderingService.GenerateSequence(6, SwathPattern.Spiral, spiralSize: 2);
        Assert.That(result, Has.Count.EqualTo(6));
        Assert.That(result.Distinct().Count(), Is.EqualTo(6));
        Console.WriteLine($"Spiral(6, size=2): [{string.Join(", ", result)}]");
    }

    [Test]
    public void Spiral_Size3_9Tracks()
    {
        var result = SwathOrderingService.GenerateSequence(9, SwathPattern.Spiral, spiralSize: 3);
        Assert.That(result, Has.Count.EqualTo(9));
        Assert.That(result.Distinct().Count(), Is.EqualTo(9));
        Console.WriteLine($"Spiral(9, size=3): [{string.Join(", ", result)}]");
    }

    [Test]
    public void GeneratePathSequence_OffsetsCorrectly()
    {
        // Tracks from path -3 to path 5 (9 tracks total)
        var result = SwathOrderingService.GeneratePathSequence(-3, 5, SwathPattern.Snake);
        Assert.That(result, Has.Count.EqualTo(9));
        Assert.That(result.Min(), Is.EqualTo(-3));
        Assert.That(result.Max(), Is.EqualTo(5));
        Console.WriteLine($"Snake paths [-3..5]: [{string.Join(", ", result)}]");
    }

    [Test]
    public void Snake_WideTurns_MinimumSpacing()
    {
        // For skip=1 equivalent, every transition should be >= 2 tracks apart
        // except at most 1 boundary transition
        for (int n = 4; n <= 20; n++)
        {
            var result = SwathOrderingService.GenerateSequence(n, SwathPattern.Snake);
            int tightTurns = 0;
            for (int i = 0; i < result.Count - 1; i++)
            {
                if (Math.Abs(result[i] - result[i + 1]) == 1)
                    tightTurns++;
            }
            Assert.That(tightTurns, Is.LessThanOrEqualTo(1),
                $"Snake({n}) has {tightTurns} tight turns: [{string.Join(", ", result)}]");
        }
    }
}
