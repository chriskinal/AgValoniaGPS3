using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Coverage;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests that coverage works without field boundaries (#138).
/// </summary>
[TestFixture]
public class CoverageNoBoundaryTests
{
    private CoverageMapService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new CoverageMapService();
    }

    [Test]
    public void IsFieldBoundsSet_Initially_False()
    {
        Assert.That(_service.IsFieldBoundsSet, Is.False);
    }

    [Test]
    public void SetFieldBoundsFromPosition_SetsFieldBounds()
    {
        _service.SetFieldBoundsFromPosition(100.0, 200.0);

        Assert.That(_service.IsFieldBoundsSet, Is.True);
    }

    [Test]
    public void SetFieldBoundsFromPosition_CreatesCorrectArea()
    {
        _service.SetFieldBoundsFromPosition(1000.0, 2000.0, halfSize: 250.0);

        // Drive a short strip within the area
        var left1 = new Vec2(999.0, 2000.0);
        var right1 = new Vec2(1001.0, 2000.0);
        var left2 = new Vec2(999.0, 2005.0);
        var right2 = new Vec2(1001.0, 2005.0);
        _service.StartMapping(0, left1, right1);
        _service.AddCoveragePoint(0, left2, right2); // Second point forms quad
        _service.StopMapping(0);

        Assert.That(_service.TotalWorkedArea, Is.GreaterThan(0),
            "Coverage should be recorded within auto-initialized bounds");
    }

    [Test]
    public void SetFieldBoundsFromPosition_CoverageOutsideBounds_NotRecorded()
    {
        _service.SetFieldBoundsFromPosition(1000.0, 2000.0, halfSize: 50.0);

        // Try to mark cells FAR outside the area
        var left = new Vec2(5000.0, 5000.0);
        var right = new Vec2(5002.0, 5000.0);
        _service.StartMapping(0, left, right);
        _service.AddCoveragePoint(0, left, right);
        _service.StopMapping(0);

        Assert.That(_service.TotalWorkedArea, Is.EqualTo(0),
            "Coverage outside bounds should not be recorded");
    }

    [Test]
    public void WithoutBounds_CoverageNotRecorded()
    {
        // No SetFieldBounds called
        var left = new Vec2(100.0, 200.0);
        var right = new Vec2(102.0, 200.0);
        _service.StartMapping(0, left, right);
        _service.AddCoveragePoint(0, left, right);
        _service.StopMapping(0);

        Assert.That(_service.TotalWorkedArea, Is.EqualTo(0),
            "Without bounds set, no coverage should be recorded");
    }

    [Test]
    public void SetFieldBounds_ThenClear_ThenAutoInit_Works()
    {
        // Set bounds from boundary
        _service.SetFieldBounds(0, 500, 0, 500);
        Assert.That(_service.IsFieldBoundsSet, Is.True);

        // Close field
        _service.ClearFieldBounds();
        Assert.That(_service.IsFieldBoundsSet, Is.False);

        // Auto-init from GPS
        _service.SetFieldBoundsFromPosition(100.0, 100.0, halfSize: 250.0);
        Assert.That(_service.IsFieldBoundsSet, Is.True);
    }

    [Test]
    public void SetFieldBoundsFromPosition_DefaultHalfSize_Is250m()
    {
        _service.SetFieldBoundsFromPosition(0, 0);

        // Drive a strip at 200m from center (within 250m default half-size)
        var left1 = new Vec2(199.0, 0);
        var right1 = new Vec2(201.0, 0);
        var left2 = new Vec2(199.0, 5.0);
        var right2 = new Vec2(201.0, 5.0);
        _service.StartMapping(0, left1, right1);
        _service.AddCoveragePoint(0, left2, right2);
        _service.StopMapping(0);

        Assert.That(_service.TotalWorkedArea, Is.GreaterThan(0));
    }
}
