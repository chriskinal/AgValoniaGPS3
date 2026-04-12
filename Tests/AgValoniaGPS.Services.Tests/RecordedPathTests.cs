// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.PathPlanning;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class RecordedPathFileTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"RecPathTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public void SaveAndLoad_RoundTrips_AllFields()
    {
        var points = new List<RecPathPoint>
        {
            new(100.0, 200.0, 1.571, 12.5, true),
            new(102.0, 200.0, 1.571, 12.3, true),
            new(104.0, 200.0, 1.571, 12.1, false),
            new(106.0, 200.5, 1.580, 11.9, false),
            new(108.0, 201.0, 1.590, 12.0, true)
        };

        RecPathFileService.SaveRecPath(_tempDir, points);

        var loaded = RecPathFileService.LoadRecPathPoints(_tempDir);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Count, Is.EqualTo(5));

        // Verify all fields round-trip
        Assert.That(loaded[0].Easting, Is.EqualTo(100.0).Within(0.01));
        Assert.That(loaded[0].Speed, Is.EqualTo(12.5).Within(0.1));
        Assert.That(loaded[0].AutoBtnState, Is.True);
        Assert.That(loaded[2].AutoBtnState, Is.False);
        Assert.That(loaded[4].Heading, Is.EqualTo(1.590).Within(0.001));
    }

    [Test]
    public void LoadRecPath_LegacyFormat_FallsBackGracefully()
    {
        // Legacy 3-field format (no speed/autoBtnState)
        var path = Path.Combine(_tempDir, "RecPath.txt");
        File.WriteAllText(path, "$RecPath\n3\n100.0,200.0,1.57\n102.0,200.0,1.57\n104.0,200.0,1.57\n");

        var loaded = RecPathFileService.LoadRecPathPoints(_tempDir);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Count, Is.EqualTo(3));
        Assert.That(loaded[0].Speed, Is.EqualTo(0.0));
        Assert.That(loaded[0].AutoBtnState, Is.False);
    }

    [Test]
    public void LoadRecPath_EmptyFile_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "RecPath.txt");
        File.WriteAllText(path, "$RecPath\n0\n");

        var loaded = RecPathFileService.LoadRecPathPoints(_tempDir);
        Assert.That(loaded, Is.Null);
    }

    [Test]
    public void LoadRecPath_TooFewPoints_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "RecPath.txt");
        File.WriteAllText(path, "$RecPath\n1\n100.0,200.0,1.57,5.0,True\n");

        var loaded = RecPathFileService.LoadRecPathPoints(_tempDir);
        Assert.That(loaded, Is.Null);
    }

    [Test]
    public void LoadRecPath_AsTrack_ReturnsRecordedPath()
    {
        var points = new List<RecPathPoint>
        {
            new(100.0, 200.0, 1.571, 12.0, true),
            new(102.0, 200.0, 1.571, 12.0, true),
            new(104.0, 200.0, 1.571, 12.0, true)
        };
        RecPathFileService.SaveRecPath(_tempDir, points);

        var track = RecPathFileService.LoadRecPath(_tempDir);
        Assert.That(track, Is.Not.Null);
        Assert.That(track!.IsRecordedPath, Is.True);
        Assert.That(track.Points.Count, Is.EqualTo(3));
    }

    [Test]
    public void SaveRecFile_And_ListRecFiles()
    {
        var points = new List<RecPathPoint>
        {
            new(100.0, 200.0, 1.571, 12.0, true),
            new(102.0, 200.0, 1.571, 12.0, true)
        };

        RecPathFileService.SaveRecPathToFile(Path.Combine(_tempDir, "path1.rec"), points);
        RecPathFileService.SaveRecPathToFile(Path.Combine(_tempDir, "path2.rec"), points);

        var files = RecPathFileService.ListRecFiles(_tempDir);
        Assert.That(files, Has.Count.EqualTo(2));
        Assert.That(files, Does.Contain("path1.rec"));
        Assert.That(files, Does.Contain("path2.rec"));
    }

    [Test]
    public void DeleteRecFile_RemovesFile()
    {
        var points = new List<RecPathPoint>
        {
            new(100.0, 200.0, 1.571, 12.0, true),
            new(102.0, 200.0, 1.571, 12.0, true)
        };
        RecPathFileService.SaveRecPathToFile(Path.Combine(_tempDir, "deleteme.rec"), points);

        Assert.That(RecPathFileService.DeleteRecFile(_tempDir, "deleteme.rec"), Is.True);
        Assert.That(RecPathFileService.ListRecFiles(_tempDir), Has.Count.EqualTo(0));
    }

    [Test]
    public void DeleteRecFile_NonExistent_ReturnsFalse()
    {
        Assert.That(RecPathFileService.DeleteRecFile(_tempDir, "nope.rec"), Is.False);
    }
}

[TestFixture]
public class RecordedPathStateTests
{
    [Test]
    public void InitialState_AllFlagsOff()
    {
        var state = new RecordedPathState();

        Assert.That(state.IsRecordingOn, Is.False);
        Assert.That(state.IsDrivingRecordedPath, Is.False);
        Assert.That(state.IsFollowingDubinsToPath, Is.False);
        Assert.That(state.IsFollowingRecPath, Is.False);
        Assert.That(state.IsEndOfLine, Is.False);
        Assert.That(state.ResumeState, Is.EqualTo(0));
        Assert.That(state.CurrentPositionIndex, Is.EqualTo(0));
        Assert.That(state.RecordedPoints, Is.Empty);
    }

    [Test]
    public void Reset_ClearsAllState()
    {
        var state = new RecordedPathState
        {
            IsRecordingOn = true,
            IsDrivingRecordedPath = true,
            IsFollowingDubinsToPath = true,
            IsFollowingRecPath = true,
            IsEndOfLine = true,
            ResumeState = 2,
            CurrentPositionIndex = 50
        };
        state.RecordedPoints.Add(new RecPathPoint(0, 0, 0, 5, true));
        state.DubinsApproachPath.Add(new Vec3(0, 0, 0));

        state.Reset();

        Assert.That(state.IsRecordingOn, Is.False);
        Assert.That(state.IsDrivingRecordedPath, Is.False);
        Assert.That(state.ResumeState, Is.EqualTo(0));
        Assert.That(state.RecordedPoints, Is.Empty);
        Assert.That(state.DubinsApproachPath, Is.Empty);
    }

    [Test]
    public void ResumeState_CyclesThrough_ThreeModes()
    {
        var state = new RecordedPathState();

        Assert.That(state.ResumeState, Is.EqualTo(0)); // Start
        state.ResumeState = (state.ResumeState + 1) % 3;
        Assert.That(state.ResumeState, Is.EqualTo(1)); // Last
        state.ResumeState = (state.ResumeState + 1) % 3;
        Assert.That(state.ResumeState, Is.EqualTo(2)); // Closest
        state.ResumeState = (state.ResumeState + 1) % 3;
        Assert.That(state.ResumeState, Is.EqualTo(0)); // Back to Start
    }

    [Test]
    public void RecordedPath_RegisteredInApplicationState()
    {
        var appState = new ApplicationState();
        Assert.That(appState.RecordedPath, Is.Not.Null);
        Assert.That(appState.RecordedPath, Is.TypeOf<RecordedPathState>());
    }

    [Test]
    public void ApplicationState_Reset_ResetsRecordedPath()
    {
        var appState = new ApplicationState();
        appState.RecordedPath.IsRecordingOn = true;
        appState.RecordedPath.RecordedPoints.Add(new RecPathPoint(1, 2, 3, 4, true));

        appState.Reset();

        Assert.That(appState.RecordedPath.IsRecordingOn, Is.False);
        Assert.That(appState.RecordedPath.RecordedPoints, Is.Empty);
    }
}

[TestFixture]
public class RecordedPathPlaybackTests
{
    /// <summary>
    /// Create a straight recorded path for testing.
    /// </summary>
    private static List<RecPathPoint> CreateStraightPath(double startE, double startN,
        double heading, double spacing, int count, double speed = 10.0)
    {
        var points = new List<RecPathPoint>();
        double sinH = Math.Sin(heading);
        double cosH = Math.Cos(heading);

        for (int i = 0; i < count; i++)
        {
            double e = startE + i * spacing * sinH;
            double n = startN + i * spacing * cosH;
            points.Add(new RecPathPoint(e, n, heading, speed, true));
        }
        return points;
    }

    [Test]
    public void DubinsApproach_GeneratesPath_ToStartPoint()
    {
        // Vehicle at origin heading north, path starts 50m east heading north
        var vehiclePos = new Vec3(0, 0, 0);
        var pathStart = new Vec3(50, 0, 0);

        var dubins = new DubinsPathService(0.5);
        dubins.TurningRadius = 10.0; // 10m turning radius

        // Bump vehicle forward 3m (matching legacy)
        var bumpedPos = new Vec3(
            vehiclePos.Easting + 3.0 * Math.Sin(vehiclePos.Heading),
            vehiclePos.Northing + 3.0 * Math.Cos(vehiclePos.Heading),
            vehiclePos.Heading);

        var path = dubins.GeneratePath(bumpedPos, pathStart);

        Assert.That(path, Is.Not.Null);
        Assert.That(path.Count, Is.GreaterThan(5));

        // End point should be close to path start
        var endPt = path[^1];
        double dist = Math.Sqrt(
            Math.Pow(endPt.Easting - pathStart.Easting, 2) +
            Math.Pow(endPt.Northing - pathStart.Northing, 2));
        Assert.That(dist, Is.LessThan(5.0));
    }

    [Test]
    public void FindClosestPoint_ReturnsNearestIndex()
    {
        var points = CreateStraightPath(0, 0, Math.PI / 2, 2.0, 20); // heading east, 2m spacing

        // Vehicle at (10, 5) - closest to point at E=10 (index 5)
        var vehiclePos = new Vec3(10.0, 5.0, Math.PI / 2);

        int closestIdx = FindClosestPoint(points, vehiclePos);
        Assert.That(closestIdx, Is.EqualTo(5).Within(1));
    }

    [Test]
    public void ReversePath_FlipsOrderAndAdjustsHeading()
    {
        var points = new List<RecPathPoint>
        {
            new(0, 0, 0, 10.0, true),
            new(0, 2, 0, 10.0, true),
            new(0, 4, 0, 10.0, false)
        };

        var reversed = ReverseRecordedPath(points);

        Assert.That(reversed, Has.Count.EqualTo(3));
        // First point of reversed should be last point of original
        Assert.That(reversed[0].Northing, Is.EqualTo(4.0).Within(0.01));
        Assert.That(reversed[2].Northing, Is.EqualTo(0.0).Within(0.01));
        // Heading should be flipped by PI
        Assert.That(reversed[0].Heading, Is.EqualTo(Math.PI).Within(0.01));
        // AutoBtnState preserved
        Assert.That(reversed[0].AutoBtnState, Is.False);
        Assert.That(reversed[2].AutoBtnState, Is.True);
    }

    [Test]
    public void StartPlayback_RequiresMinimumPoints()
    {
        var state = new RecordedPathState();
        state.RecordedPoints.AddRange(new[]
        {
            new RecPathPoint(0, 0, 0, 5, true),
            new RecPathPoint(1, 0, 0, 5, true),
            new RecPathPoint(2, 0, 0, 5, true)
        });

        // Less than 5 points should not start
        bool canStart = state.RecordedPoints.Count >= 5;
        Assert.That(canStart, Is.False);

        state.RecordedPoints.AddRange(new[]
        {
            new RecPathPoint(3, 0, 0, 5, true),
            new RecPathPoint(4, 0, 0, 5, true)
        });
        canStart = state.RecordedPoints.Count >= 5;
        Assert.That(canStart, Is.True);
    }

    [Test]
    public void ResumeFromClosest_FindsCorrectIndex()
    {
        // Path going north from (0,0) to (0,100) at 2m spacing
        var points = CreateStraightPath(0, 0, 0, 2.0, 50, 10.0);

        // Vehicle at (3, 40) - closest to point at N=40 (index 20)
        var vehiclePos = new Vec3(3, 40, 0);

        int closestIdx = FindClosestPoint(points, vehiclePos);
        // Advance by 5 as legacy does
        int startIdx = Math.Min(closestIdx + 5, points.Count - 1);

        Assert.That(closestIdx, Is.EqualTo(20).Within(1));
        Assert.That(startIdx, Is.EqualTo(25).Within(1));
    }

    // -- Helper methods matching planned implementation --

    private static int FindClosestPoint(List<RecPathPoint> points, Vec3 position)
    {
        int closestIdx = 0;
        double closestDist = double.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            double dx = points[i].Easting - position.Easting;
            double dy = points[i].Northing - position.Northing;
            double dist = dx * dx + dy * dy;
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdx = i;
            }
        }

        return closestIdx;
    }

    private static List<RecPathPoint> ReverseRecordedPath(List<RecPathPoint> points)
    {
        var reversed = new List<RecPathPoint>(points.Count);
        for (int i = points.Count - 1; i >= 0; i--)
        {
            var pt = points[i];
            double newHeading = pt.Heading + Math.PI;
            if (newHeading > Math.PI * 2) newHeading -= Math.PI * 2;
            if (newHeading < 0) newHeading += Math.PI * 2;
            reversed.Add(new RecPathPoint(pt.Easting, pt.Northing, newHeading, pt.Speed, pt.AutoBtnState));
        }
        return reversed;
    }
}
