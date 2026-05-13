// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgValoniaGPS.Services.Tests.YouTurn;

/// <summary>
/// Fence for the U-turn early-completion path in
/// <see cref="YouTurnStateMachine.Tick"/>.
///
/// The architectural fix for the drive-over: when the pivot has traversed
/// most of the recorded turn-path, complete execution so normal track
/// guidance picks up before the pure-pursuit lookahead can collapse
/// against the path's final segment.
///
/// The "how far along has the pivot progressed" check must be ARC-LENGTH
/// along the path, NOT Euclidean distance to the path's last point. On
/// omega U-turns the path's start and end are physically close (~3 m
/// apart on the v12 production geometry), so a Euclidean check fires
/// the moment the path is plotted — the v14 break the operator hit on
/// the bench (turn never executed, paths_away just incremented and the
/// tractor drove straight through xte=3 m).
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class YouTurnEarlyCompletionTests
{
    private const string V12FixtureFile = "v12_drive_over_turn_path.json";

    private YouTurnStateMachine _stateMachine = null!;
    private Boundary _boundary = null!;
    private List<Vec3> _headlandLine = null!;
    private Models.Track.Track _track = null!;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());
        var config = ConfigurationStore.Instance;
        config.Vehicle.Wheelbase = 2.5;
        config.Tool.Width = 6;
        config.Tool.Overlap = 0;
        config.NumSections = 1;
        config.Tool.SetSectionWidth(0, 600);

        var polygonOffset = new PolygonOffsetService();
        var creation = new YouTurnCreationService(
            NullLogger<YouTurnCreationService>.Instance, polygonOffset);
        var pathing = new YouTurnPathingService(
            NullLogger<YouTurnPathingService>.Instance);
        _stateMachine = new YouTurnStateMachine(
            creation, pathing, NullLogger<YouTurnStateMachine>.Instance);

        _boundary = new Boundary
        {
            OuterBoundary = new BoundaryPolygon
            {
                Points = new List<Vec2>
                {
                    new(-200, -200), new(200, -200),
                    new(200, 200), new(-200, 200),
                }.Select(p => new BoundaryPoint(p.Easting, p.Northing, 0)).ToList(),
            },
        };
        _headlandLine = new List<Vec3>
        {
            new(-150, -150, 0), new(150, -150, 0),
            new(150, 150, 0), new(-150, 150, 0),
        };
        _track = Models.Track.Track.FromABLine("AB",
            new Vec3(0, -1000, 0), new Vec3(0, 1000, 0));
    }

    private YouTurnStateMachine.TickContext Ctx(Position pos) =>
        new(pos, _track, _boundary, _headlandLine,
            UTurnSkipRows: 0, IsSkipWorkedMode: false,
            HeadlandCalculatedWidth: 10.0, HeadlandDistance: 5.0);

    private static YouTurnWorkingState SeedExecuting(List<Vec3> path) =>
        new()
        {
            IsEnabled = true,
            IsTriggered = true,
            IsExecuting = true,
            TurnPath = path,
            YouTurnCounter = 0,
            PreviousDistToTurnEnd = double.MaxValue,
        };

    // ── v14 reproducer (using the v12 production path verbatim) ──────
    // The synthetic BuildOmegaPath() loop above produces a path whose
    // start and end are ~6 m apart — not tight enough to reproduce the
    // v14 break, which requires start/end physically &lt; 4 m apart
    // (only happens on certain offset configurations). The v12 dump's
    // turn_path.json sidecar has the exact production geometry: start
    // (30.700, 22.484) and end (28.916, 24.896) are 3 m apart, which
    // is what makes the Euclidean-distance-to-end check fire at path[0]
    // and break the operator's bench-test.

    /// <summary>
    /// The v14 break verbatim. Fresh production omega path, pivot at
    /// path[0], YouTurnCounter bumped past any sensible threshold. The
    /// arc-length-aware check must NOT fire early-completion: the
    /// tractor has full arc-length (~42 m) ahead, no matter what the
    /// Euclidean distance to path end (~3 m) says.
    /// </summary>
    [Test]
    public void V12Path_PivotAtPathStart_HighCounter_DoesNotComplete()
    {
        var path = LoadV12Path();
        var turn = SeedExecuting(path);
        turn.YouTurnCounter = 300;

        // path[0] = (30.700, 22.484); path[N-1] = (28.916, 24.896) —
        // physically 3 m apart. distToTurnEnd at this pivot ≈ 3 m,
        // well under the 4 m lookahead. v14's gate fires; arc-length
        // gate rejects (remaining arc ≈ 42 m).
        var p0 = path[0];
        var pos = new Position
        {
            Easting = p0.Easting,
            Northing = p0.Northing,
            Heading = p0.Heading * 180.0 / Math.PI,
        };
        var ctx = Ctx(pos);
        _stateMachine.Tick(in ctx, new GuidanceWorkingState(), turn);

        Assert.That(turn.IsExecuting, Is.True,
            "v14 reproducer: pivot at production omega path's start "
            + "with high counter must NOT trigger early-completion. "
            + "Operator bench-test showed v14 firing here, the arc "
            + "never executed, paths_away just incremented and xte "
            + "jumped 3 m. Arc-length-based gating correctly rejects.");
    }

    [Test]
    public void V12Path_PivotMidArc_DoesNotComplete()
    {
        // Pivot at the apex of the v12 omega (closest match in the
        // production trajectory: row 314, the recorded max-distFromStart).
        // Remaining arc-length is roughly half the path — well above
        // lookahead.
        var path = LoadV12Path();
        var turn = SeedExecuting(path);
        turn.YouTurnCounter = 200;

        var pos = new Position
        {
            Easting = 44.654,
            Northing = 35.353,
            Heading = 319.62,
        };
        var ctx = Ctx(pos);
        _stateMachine.Tick(in ctx, new GuidanceWorkingState(), turn);

        Assert.That(turn.IsExecuting, Is.True,
            "Pivot at v12 omega apex must not trigger early-completion: "
            + "still ~half the arc ahead.");
    }

    [Test]
    public void V12Path_PivotOnLastSegment_DoesComplete()
    {
        // Pivot near the path's last index. Remaining arc-length is
        // just one segment (~1 m) — early-completion must fire.
        var path = LoadV12Path();
        var turn = SeedExecuting(path);
        turn.YouTurnCounter = 400;

        var near = path[path.Count - 2];
        var pos = new Position
        {
            Easting = near.Easting,
            Northing = near.Northing,
            Heading = near.Heading * 180.0 / Math.PI,
        };
        var ctx = Ctx(pos);
        _stateMachine.Tick(in ctx, new GuidanceWorkingState(), turn);

        Assert.That(turn.IsExecuting, Is.False,
            "Pivot on path's penultimate index — remaining arc is one "
            + "segment (~1 m), well below the 4 m lookahead. Early-"
            + "completion must fire here.");
    }

    // ── Original v12 production path replay ───────────────────────────

    private static readonly (double E, double N, double HeadingDeg)[] V12Trajectory =
    {
        (29.270, 21.418, 53.43),
        (33.247, 24.356, 53.61),
        (40.316, 28.538, 68.07),
        (44.654, 35.353, 319.62),
        (39.088, 34.141, 218.55),
        (34.165, 28.838, 233.17),
        (31.927, 27.224, 233.83),
        (30.159, 25.900, 232.76),
        (28.205, 24.416, 232.73),
        (27.425, 23.819, 232.17),
    };

    private static List<Vec3> LoadV12Path()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        var path = Path.Combine(dir, "YouTurn", "Fixtures", V12FixtureFile);
        if (!File.Exists(path))
        {
            for (var probe = new DirectoryInfo(dir); probe != null; probe = probe.Parent)
            {
                var candidate = Path.Combine(probe.FullName, "Tests",
                    "AgValoniaGPS.Services.Tests", "YouTurn", "Fixtures", V12FixtureFile);
                if (File.Exists(candidate)) { path = candidate; break; }
            }
        }
        Assert.That(File.Exists(path), Is.True,
            $"Fixture {V12FixtureFile} not found from {dir}.");

        using var stream = File.OpenRead(path);
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

    [Test]
    public void Replay_v12DriveOverPath_CompletionFiresBeforeGoalCollapse()
    {
        var path = LoadV12Path();
        var turn = SeedExecuting(path);
        var guidance = new GuidanceWorkingState();

        int completedAtIdx = -1;
        for (int i = 0; i < V12Trajectory.Length; i++)
        {
            var (e, n, hd) = V12Trajectory[i];
            turn.YouTurnCounter += 13; // match production cycle density
            var pos = new Position { Easting = e, Northing = n, Heading = hd };
            var ctx = Ctx(pos);
            _stateMachine.Tick(in ctx, guidance, turn);

            if (!turn.IsExecuting) { completedAtIdx = i; break; }
        }

        Assert.That(completedAtIdx, Is.GreaterThanOrEqualTo(0),
            "Early-completion never fired on the v12 trajectory — the "
            + "fix isn't catching the original drive-over case.");

        // Original v12 dump's gd crossed 3 m at trajectory idx 8.
        // Completion must fire strictly before that sample.
        const int CollapseSampleIdx = 8;
        Assert.That(completedAtIdx, Is.LessThan(CollapseSampleIdx),
            $"Completion fired at idx {completedAtIdx}; must fire before "
            + $"idx {CollapseSampleIdx} (the v12 collapse window).");
    }

    // ── Belt-and-suspenders ────────────────────────────────────────────

    [Test]
    public void ShortPath_FreshlyPlotted_DoesNotImmediateComplete()
    {
        // 5-point near-omega with start and end ~3 m apart, total arc
        // ~6 m. Pivot at path[0]. Even with a high counter, the arc-
        // length-based check should leave 6 m remaining and not fire.
        var shortPath = new List<Vec3>
        {
            new(0, 0, 0),
            new(0, 1, 0),
            new(0, 2, 0),
            new(0.5, 2.5, Math.PI / 2),
            new(3, 1, Math.PI),
        };

        var turn = SeedExecuting(shortPath);
        turn.YouTurnCounter = 500;
        var pos = new Position { Easting = 0, Northing = 0, Heading = 0 };
        var ctx = Ctx(pos);
        _stateMachine.Tick(in ctx, new GuidanceWorkingState(), turn);

        Assert.That(turn.IsExecuting, Is.True,
            "Freshly plotted path with pivot at start must not "
            + "immediately complete, regardless of counter.");
    }

    [Test]
    public void Regression_ClosestApproachStillFires_OnPivotOvershoot()
    {
        // 5-segment straight path, total length 5 m. Pivot drives along
        // the path, lands on the end, then overshoots. The arc-length
        // check will fire first on this geometry (remaining arc shrinks
        // as pivot advances). This regression test pins that completion
        // happens one way or another so the pipeline can't deadlock with
        // IsExecuting stuck true.
        var linearPath = new List<Vec3>();
        for (int i = 0; i <= 5; i++)
            linearPath.Add(new Vec3(0, i, 0));

        var turn = SeedExecuting(linearPath);
        var guidance = new GuidanceWorkingState();

        for (int i = 0; i < 15 && turn.IsExecuting; i++)
        {
            turn.YouTurnCounter++;
            double n = -1.0 + i * 0.6; // -1 → +8 over 15 ticks
            var pos = new Position { Easting = 0, Northing = n, Heading = 0 };
            var ctx = Ctx(pos);
            _stateMachine.Tick(in ctx, guidance, turn);
        }

        Assert.That(turn.IsExecuting, Is.False,
            "Pivot drove fully past the path end — one of the two "
            + "completion paths (arc-length or closest-approach) must "
            + "close the turn before tick 15.");
    }
}
