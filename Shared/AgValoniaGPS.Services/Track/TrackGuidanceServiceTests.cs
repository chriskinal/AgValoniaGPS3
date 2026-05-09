// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.Services.Track;

/// <summary>
/// Simple test harness for TrackGuidanceService.
/// Call RunAllTests() to verify the service works correctly.
/// </summary>
public static class TrackGuidanceServiceTests
{
    /// <summary>
    /// Run all tests and return results.
    /// </summary>
    public static (bool success, string results) RunAllTests()
    {
        var results = new System.Text.StringBuilder();
        bool allPassed = true;

        results.AppendLine("=== TrackGuidanceService Tests ===\n");

        // Test 1: AB Line Pure Pursuit
        try
        {
            var (passed, msg) = TestABLinePurePursuit();
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 1 - AB Line Pure Pursuit: {(passed ? "PASS" : "FAIL")}");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {msg}\n");
            allPassed &= passed;
        }
        catch (Exception ex)
        {
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 1 - AB Line Pure Pursuit: FAIL (Exception)");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {ex.Message}\n");
            allPassed = false;
        }

        // Test 2: Curve Pure Pursuit
        try
        {
            var (passed, msg) = TestCurvePurePursuit();
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 2 - Curve Pure Pursuit: {(passed ? "PASS" : "FAIL")}");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {msg}\n");
            allPassed &= passed;
        }
        catch (Exception ex)
        {
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 2 - Curve Pure Pursuit: FAIL (Exception)");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {ex.Message}\n");
            allPassed = false;
        }

        // Test 3: AB Line Stanley
        try
        {
            var (passed, msg) = TestABLineStanley();
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 3 - AB Line Stanley: {(passed ? "PASS" : "FAIL")}");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {msg}\n");
            allPassed &= passed;
        }
        catch (Exception ex)
        {
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 3 - AB Line Stanley: FAIL (Exception)");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {ex.Message}\n");
            allPassed = false;
        }

        // Test 4: Vehicle on line (zero XTE)
        try
        {
            var (passed, msg) = TestVehicleOnLine();
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 4 - Vehicle On Line: {(passed ? "PASS" : "FAIL")}");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {msg}\n");
            allPassed &= passed;
        }
        catch (Exception ex)
        {
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 4 - Vehicle On Line: FAIL (Exception)");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {ex.Message}\n");
            allPassed = false;
        }

        // Test 5: Track properties
        try
        {
            var (passed, msg) = TestTrackProperties();
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 5 - Track Properties: {(passed ? "PASS" : "FAIL")}");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {msg}\n");
            allPassed &= passed;
        }
        catch (Exception ex)
        {
            results.AppendLine(CultureInfo.InvariantCulture, $"Test 5 - Track Properties: FAIL (Exception)");
            results.AppendLine(CultureInfo.InvariantCulture, $"  {ex.Message}\n");
            allPassed = false;
        }

        results.AppendLine(CultureInfo.InvariantCulture, $"=== Overall: {(allPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED")} ===");

        return (allPassed, results.ToString());
    }

    /// <summary>
    /// Test Pure Pursuit on a simple AB line with vehicle offset to the right.
    /// </summary>
    private static (bool passed, string message) TestABLinePurePursuit()
    {
        var service = new TrackGuidanceService();

        // Create a simple north-south AB line
        var track = Models.Track.Track.FromABLine(
            "Test AB Line",
            new Vec3(0, 0, 0),      // Point A at origin
            new Vec3(0, 100, 0));   // Point B 100m north

        // Vehicle is 2m to the right (east) of the line, heading north
        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(2, 50, 0),  // 2m east, 50m north
            SteerPosition = new Vec3(2, 52.5, 0), // 2.5m ahead (wheelbase)
            UseStanley = false,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            FixHeading = 0,  // Heading north
            AvgSpeed = 10,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = service.CalculateGuidance(input);

        // Verify: vehicle is to the right, should steer left (negative angle)
        bool xteCorrect = Math.Abs(output.CrossTrackError - 2.0) < 0.1;
        bool steerCorrect = output.SteerAngle < 0; // Should steer left
        bool steerReasonable = Math.Abs(output.SteerAngle) < 35; // Within limits

        string msg = $"XTE={output.CrossTrackError:F2}m (expect ~2.0), " +
                     $"Steer={output.SteerAngle:F1}° (expect negative)";

        return (xteCorrect && steerCorrect && steerReasonable, msg);
    }

    /// <summary>
    /// Test Pure Pursuit on a curved track.
    /// </summary>
    private static (bool passed, string message) TestCurvePurePursuit()
    {
        var service = new TrackGuidanceService();

        // Create a curve with multiple points (quarter circle turning east)
        var points = new List<Vec3>();
        for (int i = 0; i <= 10; i++)
        {
            double angle = i * Math.PI / 20;  // 0 to 90 degrees
            double x = 20 * Math.Sin(angle);
            double y = 20 * (1 - Math.Cos(angle));
            double heading = angle;  // Tangent heading
            points.Add(new Vec3(x, y, heading));
        }

        var track = Models.Track.Track.FromCurve("Test Curve", points);

        // Vehicle near start of curve
        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(1, 1, 0.1),
            SteerPosition = new Vec3(1, 3.5, 0.1),
            UseStanley = false,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            FixHeading = 0.1,
            AvgSpeed = 8,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = service.CalculateGuidance(input);

        // Verify: should get reasonable outputs
        bool hasOutput = !double.IsNaN(output.SteerAngle);
        bool steerReasonable = Math.Abs(output.SteerAngle) <= 35;
        bool hasGoalPoint = output.GoalPoint.Easting != 0 || output.GoalPoint.Northing != 0;

        string msg = $"XTE={output.CrossTrackError:F2}m, Steer={output.SteerAngle:F1}°, " +
                     $"GoalPt=({output.GoalPoint.Easting:F1},{output.GoalPoint.Northing:F1})";

        return (hasOutput && steerReasonable && hasGoalPoint, msg);
    }

    /// <summary>
    /// Test Stanley algorithm on AB line.
    /// </summary>
    private static (bool passed, string message) TestABLineStanley()
    {
        var service = new TrackGuidanceService();

        // Create a simple north-south AB line
        var track = Models.Track.Track.FromABLine(
            "Test AB Line",
            new Vec3(0, 0, 0),
            new Vec3(0, 100, 0));

        // Vehicle is 1.5m to the left (west) of the line
        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(-1.5, 50, 0),
            SteerPosition = new Vec3(-1.5, 52.5, 0),
            UseStanley = true,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            StanleyHeadingErrorGain = 1.0,
            StanleyDistanceErrorGain = 0.8,
            FixHeading = 0,
            AvgSpeed = 10,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = service.CalculateGuidance(input);

        // Verify: vehicle is to the left, should steer right (positive angle)
        bool xteCorrect = Math.Abs(output.CrossTrackError - (-1.5)) < 0.1;
        bool steerCorrect = output.SteerAngle > 0; // Should steer right
        bool steerReasonable = Math.Abs(output.SteerAngle) < 35;

        string msg = $"XTE={output.CrossTrackError:F2}m (expect ~-1.5), " +
                     $"Steer={output.SteerAngle:F1}° (expect positive)";

        return (xteCorrect && steerCorrect && steerReasonable, msg);
    }

    /// <summary>
    /// Test vehicle exactly on the line (should have near-zero XTE and steering).
    /// </summary>
    private static (bool passed, string message) TestVehicleOnLine()
    {
        var service = new TrackGuidanceService();

        var track = Models.Track.Track.FromABLine(
            "Test AB Line",
            new Vec3(0, 0, 0),
            new Vec3(0, 100, 0));

        // Vehicle exactly on line, heading north
        var input = new TrackGuidanceInput
        {
            Track = track,
            PivotPosition = new Vec3(0, 50, 0),
            SteerPosition = new Vec3(0, 52.5, 0),
            UseStanley = false,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 5,
            FixHeading = 0,
            AvgSpeed = 10,
            IsHeadingSameWay = true,
            FindGlobalNearest = true
        };

        var output = service.CalculateGuidance(input);

        // Should have near-zero XTE and steering
        bool xteNearZero = Math.Abs(output.CrossTrackError) < 0.01;
        bool steerNearZero = Math.Abs(output.SteerAngle) < 1.0;

        string msg = $"XTE={output.CrossTrackError:F4}m (expect ~0), " +
                     $"Steer={output.SteerAngle:F2}° (expect ~0)";

        return (xteNearZero && steerNearZero, msg);
    }

    /// <summary>
    /// Test Track property computations.
    /// </summary>
    private static (bool passed, string message) TestTrackProperties()
    {
        // Create an AB line track
        var track = Models.Track.Track.FromABLine(
            "Property Test",
            new Vec3(10, 20, 0.5),
            new Vec3(30, 40, 0.5));
        track.NudgeDistance = 1.5;
        track.IsVisible = true;

        // Verify computed properties
        bool isAB = track.IsABLine;
        bool notCurve = !track.IsCurve;
        bool nameOk = track.Name == "Property Test";
        bool nudgeOk = Math.Abs(track.NudgeDistance - 1.5) < 0.001;
        bool visOk = track.IsVisible;
        bool pointsOk = track.Points.Count == 2;

        bool pt0Match = Math.Abs(track.Points[0].Easting - 10) < 0.001 &&
                        Math.Abs(track.Points[0].Northing - 20) < 0.001;

        string msg = $"IsAB={isAB}, NotCurve={notCurve}, Name={nameOk}, Nudge={nudgeOk}, " +
                     $"Visible={visOk}, Points={pointsOk}, Coords={pt0Match}";

        return (isAB && notCurve && nameOk && nudgeOk && visOk && pointsOk && pt0Match, msg);
    }
}
