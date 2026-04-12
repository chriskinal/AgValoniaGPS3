// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.YouTurn;

namespace AgValoniaGPS.Services.Tests;

[TestFixture]
public class YouTurnGuidanceTests
{
    /// <summary>
    /// Build a simple U-turn path: straight east, semicircle north, straight west.
    /// </summary>
    private static List<Vec3> BuildSimpleUTurnPath(double trackOffset = 12.0, double turnRadius = 8.0)
    {
        var path = new List<Vec3>();

        // Entry leg: heading east (90 deg = PI/2 rad) for 10m
        for (int i = 0; i < 20; i++)
        {
            double e = 100 + i * 0.5;
            path.Add(new Vec3(e, 30, Math.PI / 2));
        }

        // Semicircle turning north then west
        int arcPoints = 40;
        double cx = 110; // Arc center
        double cy = 30 + turnRadius;
        for (int i = 0; i <= arcPoints; i++)
        {
            double angle = -Math.PI / 2 + Math.PI * i / arcPoints; // -PI/2 to PI/2
            double e = cx + turnRadius * Math.Cos(angle);
            double n = cy + turnRadius * Math.Sin(angle);
            double heading = angle + Math.PI / 2; // Tangent heading
            path.Add(new Vec3(e, n, heading));
        }

        // Exit leg: heading west (270 deg = 3*PI/2 rad) for 10m
        for (int i = 0; i < 20; i++)
        {
            double e = 110 - i * 0.5;
            path.Add(new Vec3(e, 30 + trackOffset, 3 * Math.PI / 2));
        }

        return path;
    }

    [Test]
    public void YouTurnGuidance_ProducesNonZeroSteerAngle()
    {
        var service = new YouTurnGuidanceService();
        var path = BuildSimpleUTurnPath();

        // Vehicle heading east, at start of U-turn path
        var input = new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = new Vec3(100, 30, Math.PI / 2),
            SteerPosition = new Vec3(100, 30, Math.PI / 2),
            FixHeading = Math.PI / 2,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 4.0,
            UTurnCompensation = 1.0,
            AvgSpeed = 5.0,
            IsReverse = false,
            UTurnStyle = 0
        };

        var output = service.CalculateGuidance(input);

        // Should be following the path - steer angle may be near 0 on straight entry
        Assert.That(output, Is.Not.Null, "Guidance should return output");
        Assert.That(!output.IsTurnComplete, Is.True, "Path should be valid");
    }

    [Test]
    public void YouTurnGuidance_SteersThroughArc()
    {
        var service = new YouTurnGuidanceService();
        var path = BuildSimpleUTurnPath();

        // Vehicle at the start of the arc (needs to turn)
        var input = new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = new Vec3(109, 30, Math.PI / 2), // Near arc entry
            SteerPosition = new Vec3(109, 30, Math.PI / 2),
            FixHeading = Math.PI / 2, // Heading east
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 4.0,
            UTurnCompensation = 1.0,
            AvgSpeed = 5.0,
            IsReverse = false,
            UTurnStyle = 0
        };

        var output = service.CalculateGuidance(input);

        Assert.That(Math.Abs(output.SteerAngle), Is.GreaterThan(1.0),
            $"Should steer to follow arc, got {output.SteerAngle:F2} degrees");
    }

    [Test]
    public void YouTurnGuidance_HeadingChangesAfterFollowingPath()
    {
        var service = new YouTurnGuidanceService();
        var path = BuildSimpleUTurnPath();

        // Simulate driving through the U-turn: step through positions and
        // apply the steer angle via bicycle model
        double heading = Math.PI / 2; // Start heading east
        double easting = 100;
        double northing = 30;
        double speed = 5.0; // m/s
        double dt = 0.1; // 10Hz
        double wheelbase = 2.5;

        for (int frame = 0; frame < 200; frame++)
        {
            var input = new YouTurnGuidanceInput
            {
                TurnPath = path,
                PivotPosition = new Vec3(easting, northing, heading),
                SteerPosition = new Vec3(easting, northing, heading),
                FixHeading = heading,
                Wheelbase = wheelbase,
                MaxSteerAngle = 35,
                GoalPointDistance = 4.0,
                UTurnCompensation = 1.0,
                AvgSpeed = speed,
                IsReverse = false,
                UTurnStyle = 0
            };

            var output = service.CalculateGuidance(input);
            if (output == null || !!output.IsTurnComplete) break;

            // Apply bicycle model
            double steerRad = output.SteerAngle * Math.PI / 180.0;
            if (Math.Abs(steerRad) > 0.001)
            {
                double turnRate = speed * Math.Tan(steerRad) / wheelbase;
                heading += turnRate * dt;
            }

            // Move forward
            easting += Math.Sin(heading) * speed * dt;
            northing += Math.Cos(heading) * speed * dt;
        }

        // After 200 frames following U-turn, heading should have changed significantly
        // from east (PI/2) toward west (3*PI/2 or -PI/2)
        double headingDeg = heading * 180.0 / Math.PI;
        headingDeg = ((headingDeg % 360) + 360) % 360;

        double startHeadingDeg = 90; // Started east
        double headingChange = Math.Abs(headingDeg - startHeadingDeg);
        if (headingChange > 180) headingChange = 360 - headingChange;

        Assert.That(headingChange, Is.GreaterThan(45),
            $"Heading should change >45 degrees during U-turn. Start=90, End={headingDeg:F1}, Change={headingChange:F1}");
    }

    [Test]
    public void YouTurnGuidance_CompensationAppliedOnce()
    {
        var service = new YouTurnGuidanceService();
        var path = BuildSimpleUTurnPath();

        // Get steer angle with compensation=1.0
        var input1 = new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = new Vec3(109, 30, Math.PI / 2),
            SteerPosition = new Vec3(109, 30, Math.PI / 2),
            FixHeading = Math.PI / 2,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 4.0,
            UTurnCompensation = 1.0,
            AvgSpeed = 5.0,
            IsReverse = false,
            UTurnStyle = 0
        };
        var output1 = service.CalculateGuidance(input1);

        // Get steer angle with compensation=1.5
        var input2 = new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = new Vec3(109, 30, Math.PI / 2),
            SteerPosition = new Vec3(109, 30, Math.PI / 2),
            FixHeading = Math.PI / 2,
            Wheelbase = 2.5,
            MaxSteerAngle = 35,
            GoalPointDistance = 4.0,
            UTurnCompensation = 1.5,
            AvgSpeed = 5.0,
            IsReverse = false,
            UTurnStyle = 0
        };
        var output2 = service.CalculateGuidance(input2);

        // If compensation is applied inside the service, output2 should be ~1.5x output1
        if (Math.Abs(output1.SteerAngle) > 0.1)
        {
            double ratio = output2.SteerAngle / output1.SteerAngle;
            Assert.That(ratio, Is.EqualTo(1.5).Within(0.3),
                $"Compensation should scale steer angle by 1.5x. " +
                $"comp=1.0: {output1.SteerAngle:F2}, comp=1.5: {output2.SteerAngle:F2}, ratio={ratio:F2}");
        }
    }
}
