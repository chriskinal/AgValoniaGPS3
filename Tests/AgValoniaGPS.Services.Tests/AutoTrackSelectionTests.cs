using System;
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Services.Track;
using TrackModel = AgValoniaGPS.Models.Track.Track;
using TrackType = AgValoniaGPS.Models.Track.TrackType;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests for auto track selection algorithm (#143).
/// Matches legacy AgOpenGPS CTrack.FindClosestRefTrack() behavior.
/// </summary>
[TestFixture]
public class AutoTrackSelectionTests
{
    private static TrackModel MakeABLine(string name, double ax, double ay, double bx, double by, bool visible = true)
    {
        return new TrackModel
        {
            Name = name,
            Type = TrackType.ABLine,
            IsVisible = visible,
            Points = new List<Vec3>
            {
                new Vec3(ax, ay, 0),
                new Vec3(bx, by, 0)
            }
        };
    }

    private static TrackModel MakeCurve(string name, List<Vec3> points, bool visible = true)
    {
        return new TrackModel
        {
            Name = name,
            Type = TrackType.Curve,
            IsVisible = visible,
            Points = points
        };
    }

    [Test]
    public void FindClosestTrack_SingleABLine_ReturnsIt()
    {
        var tracks = new List<TrackModel> { MakeABLine("AB1", 0, 0, 0, 100) };
        var pos = new Vec2(5, 50); // 5m east of line
        double heading = 0; // Heading north, same as line

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("AB1"));
    }

    [Test]
    public void FindClosestTrack_MultipleABLines_ReturnsClosest()
    {
        var tracks = new List<TrackModel>
        {
            MakeABLine("AB1", 0, 0, 0, 100),   // Along Y axis at X=0
            MakeABLine("AB2", 10, 0, 10, 100),  // Along Y axis at X=10
        };
        var pos = new Vec2(8, 50); // Closer to AB2 (2m vs 8m)
        double heading = 0;

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result!.Name, Is.EqualTo("AB2"));
    }

    [Test]
    public void FindClosestTrack_HeadingAligned_Selected()
    {
        // Track runs north (heading 0). Vehicle heading ~30 degrees (within 57 degree limit)
        var tracks = new List<TrackModel> { MakeABLine("AB1", 0, 0, 0, 100) };
        var pos = new Vec2(5, 50);
        double heading = 0.5; // ~29 degrees

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void FindClosestTrack_HeadingPerpendicular_Rejected()
    {
        // Track runs north. Vehicle heading east (~90 degrees = PI/2)
        var tracks = new List<TrackModel> { MakeABLine("AB1", 0, 0, 0, 100) };
        var pos = new Vec2(5, 50);
        double heading = Math.PI / 2; // 90 degrees - perpendicular

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result, Is.Null, "Perpendicular heading should reject the track");
    }

    [Test]
    public void FindClosestTrack_OppositeHeading_Selected()
    {
        // Track runs north. Vehicle heading south (PI radians) - valid for return pass
        var tracks = new List<TrackModel> { MakeABLine("AB1", 0, 0, 0, 100) };
        var pos = new Vec2(5, 50);
        double heading = Math.PI; // 180 degrees - opposite

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result, Is.Not.Null, "Opposite heading should be accepted (return pass)");
    }

    [Test]
    public void FindClosestTrack_InvisibleTrack_Skipped()
    {
        var tracks = new List<TrackModel>
        {
            MakeABLine("Hidden", 0, 0, 0, 100, visible: false),
            MakeABLine("Visible", 20, 0, 20, 100, visible: true),
        };
        var pos = new Vec2(1, 50); // Much closer to hidden track
        double heading = 0;

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result!.Name, Is.EqualTo("Visible"), "Should skip invisible track");
    }

    [Test]
    public void FindClosestTrack_CurveTrack_NoHeadingFilter()
    {
        // Curve at any heading should be considered
        var curve = MakeCurve("Curve1", new List<Vec3>
        {
            new Vec3(0, 0, 0),
            new Vec3(5, 10, 0),
            new Vec3(10, 20, 0),
            new Vec3(15, 30, 0),
        });
        var tracks = new List<TrackModel> { curve };
        var pos = new Vec2(2, 15); // Near curve
        double heading = Math.PI / 2; // 90 degrees - would reject AB line

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result, Is.Not.Null, "Curves should not be filtered by heading");
    }

    [Test]
    public void FindClosestTrack_NoTracks_ReturnsNull()
    {
        var tracks = new List<TrackModel>();
        var pos = new Vec2(0, 0);

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, 0);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindClosestTrack_MixedABAndCurve_ReturnsClosest()
    {
        var ab = MakeABLine("AB1", 20, 0, 20, 100); // At X=20
        var curve = MakeCurve("Curve1", new List<Vec3>
        {
            new Vec3(5, 0, 0),
            new Vec3(5, 50, 0),
            new Vec3(5, 100, 0),
        }); // At X=5

        var tracks = new List<TrackModel> { ab, curve };
        var pos = new Vec2(3, 50); // Closer to curve (2m vs 17m)
        double heading = 0;

        var result = AutoTrackSelectionService.FindClosestTrack(tracks, pos, heading);

        Assert.That(result!.Name, Is.EqualTo("Curve1"));
    }
}
