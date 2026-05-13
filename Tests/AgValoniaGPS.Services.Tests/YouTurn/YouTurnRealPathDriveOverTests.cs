// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.YouTurn;

namespace AgValoniaGPS.Services.Tests.YouTurn;

/// <summary>
/// Regression fence using the production omega path that produced the
/// v12 drive-over (dump <c>debug_dump_20260513_231602.zip</c>, recorder
/// commit <c>f992c609</c>). The path JSON is captured verbatim from the
/// turn_path.json sidecar emitted by GpsDataRecorder; it has the exact
/// 56-point geometry (5-pt entry + 47-pt arc + 4-pt exit) the operator
/// was driving on.
///
/// The bug: pivot reaches the path's final segment (A=54, B=55,
/// ptCount=56), the lookahead-walk can't advance past index 55, so the
/// goal stays at the path endpoint while the pivot keeps driving. Goal
/// distance shrinks 3.76 → 0.32 m over 8 cycles. Pure-pursuit's
/// <c>steer = atan(2·L·sin(α)/D)</c> explodes as D → 0; the recorded
/// run shows a −31.9° steer spike on the cycle where D crossed 0.32 m.
///
/// Without the collapsed-goal extension to the post-walk guard, the
/// anti-tangent <c>goalFwd &lt; 0</c> condition doesn't catch this
/// scenario — the goal stays forward, just very close to the pivot.
/// The fix extends the guard to also fire when goal-distance falls
/// below a calibrated fraction of the configured lookahead.
/// </summary>
[TestFixture]
public class YouTurnRealPathDriveOverTests
{
    private const string FixtureFile = "v12_drive_over_turn_path.json";
    private const double Lookahead = 4.0;
    private const double Wheelbase = 2.5;
    private const double SteerSpikeAbsThreshold = 25.0;

    /// <summary>
    /// Pivot positions and headings captured from the dump's CSV
    /// (gps_data_log.csv rows 377-384 of <c>debug_dump_20260513_231602</c>).
    /// Each row is `(easting, northing, headingDegrees)`. Row 384 is the
    /// recorded spike (steer −31.937°); pre-spike rows 377-383 are the
    /// approach where goal-distance shrank from 3.76 m to 0.81 m.
    /// </summary>
    private static readonly (double E, double N, double HeadingDeg)[] PivotsAcrossDriveOver =
    {
        (28.791, 24.861, 232.72), // row 377 — gd was 3.757 in the dump
        (28.596, 24.713, 232.73), // row 378 — gd 3.266
        (28.400, 24.564, 232.74), // row 379 — gd 2.776
        (28.205, 24.416, 232.73), // row 380 — gd 2.285
        (28.009, 24.267, 232.69), // row 381 — gd 1.794
        (27.814, 24.119, 232.61), // row 382 — gd 1.303
        (27.619, 23.969, 232.42), // row 383 — gd 0.813
        (27.425, 23.819, 232.17), // row 384 — gd 0.322, steer −31.937
    };

    private static List<Vec3> LoadPath()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        var path = Path.Combine(dir, "YouTurn", "Fixtures", FixtureFile);
        if (!File.Exists(path))
        {
            // Fall back to walking up to find the source-tree copy when
            // the build hasn't yet copied the file to the output dir
            // (some IDE quick-test runs skip the Content copy step).
            for (var probe = new DirectoryInfo(dir); probe != null; probe = probe.Parent)
            {
                var candidate = Path.Combine(probe.FullName, "Tests",
                    "AgValoniaGPS.Services.Tests", "YouTurn", "Fixtures", FixtureFile);
                if (File.Exists(candidate)) { path = candidate; break; }
            }
        }
        Assert.That(File.Exists(path), Is.True,
            $"Fixture {FixtureFile} not found. Searched from {dir}.");

        using var stream = File.OpenRead(path);
        // turn_path.json uses lowercase e/n/h to match the sidecar
        // emitted by DebugDumpService; deserialise case-insensitively
        // so the DTO's PascalCase property names match.
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dtos = JsonSerializer.Deserialize<List<PathPointDto>>(stream, opts)!;
        var result = new List<Vec3>(dtos.Count);
        foreach (var p in dtos) result.Add(new Vec3(p.E, p.N, p.H));
        return result;
    }

    private sealed class PathPointDto
    {
        public double E { get; set; }
        public double N { get; set; }
        public double H { get; set; }
    }

    private static YouTurnGuidanceInput BuildInput(
        List<Vec3> path, double pivotE, double pivotN, double pivotHeadingRad)
    {
        return new YouTurnGuidanceInput
        {
            TurnPath = path,
            PivotPosition = new Vec3(pivotE, pivotN, pivotHeadingRad),
            SteerPosition = new Vec3(
                pivotE + Math.Sin(pivotHeadingRad) * Wheelbase,
                pivotN + Math.Cos(pivotHeadingRad) * Wheelbase,
                pivotHeadingRad),
            Wheelbase = Wheelbase,
            MaxSteerAngle = 35,
            UseStanley = false,
            GoalPointDistance = Lookahead,
            UTurnCompensation = 1.0,
            FixHeading = pivotHeadingRad,
            AvgSpeed = 10,
            IsReverse = false,
            UTurnStyle = 0,
        };
    }

    [Test]
    public void Fixture_LoadsExpected56PointPath()
    {
        var path = LoadPath();
        Assert.That(path.Count, Is.EqualTo(56),
            "v12 turn_path.json sidecar should be the production 56-point "
            + "omega geometry. If this fails the fixture has been replaced "
            + "with a different dump.");
    }

    /// <summary>
    /// Replays the v12 drive-over window. At every recorded pivot,
    /// assert |steer| stays under the spike threshold — the row-384
    /// spike of −31.937° was the visible drive-over, and absent the
    /// collapsed-goal guard extension would still occur on the same
    /// pivot position.
    /// </summary>
    [TestCaseSource(nameof(PivotsAcrossDriveOver))]
    public void Replay_ProductionPivot_NoSteerSpike(
        (double E, double N, double HeadingDeg) sample)
    {
        var path = LoadPath();
        double headingRad = sample.HeadingDeg * Math.PI / 180.0;
        var input = BuildInput(path, sample.E, sample.N, headingRad);

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(input);

        Assert.That(output.IsTurnComplete, Is.False,
            "service flagged completion at recorded executing pivot.");
        Assert.That(Math.Abs(output.SteerAngle), Is.LessThan(SteerSpikeAbsThreshold),
            $"pivot ({sample.E:F3},{sample.N:F3}) heading {sample.HeadingDeg:F2}°: "
            + $"steer = {output.SteerAngle:F2}°. Full-lock spike at the path-end "
            + "collapse is the v12 drive-over signature.");
    }

    /// <summary>
    /// The collapsed-goal guard must fire on the recorded spike-cycle
    /// (row 384, pivot (27.425, 23.819) heading 232.17°). If it doesn't,
    /// the fix has regressed or its threshold has been weakened past
    /// the v12 data point.
    /// </summary>
    [Test]
    public void Spike_Cycle_TriggersAntiTangentGuard()
    {
        var path = LoadPath();
        var spike = PivotsAcrossDriveOver[^1];
        double headingRad = spike.HeadingDeg * Math.PI / 180.0;
        var input = BuildInput(path, spike.E, spike.N, headingRad);

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(input);

        Assert.That(output.AntiTangentGuardFired, Is.True,
            "Collapsed-goal cycle (gd~0.32 m in dump) must fire the "
            + "post-walk guard.");
    }

    /// <summary>
    /// Sanity: at a healthy pivot well inside the arc, the guard should
    /// NOT fire (the goal is at the configured lookahead, forward of
    /// the pivot, no collapse). Confirms the fix isn't over-aggressive.
    /// </summary>
    [Test]
    public void HealthyMidArc_DoesNotTriggerGuard()
    {
        var path = LoadPath();
        // Pivot on the path at index 15 (mid-arc) with that point's
        // stored heading. Should produce a clean lookahead at full distance.
        var p = path[15];
        var input = BuildInput(path, p.Easting, p.Northing, p.Heading);

        var svc = new YouTurnGuidanceService();
        var output = svc.CalculateGuidance(input);

        Assert.That(output.AntiTangentGuardFired, Is.False,
            "Healthy mid-arc pivot must not trigger the guard — the goal "
            + "lookahead is satisfied normally and gd ≈ lookahead.");
        Assert.That(output.IsTurnComplete, Is.False);
    }
}
