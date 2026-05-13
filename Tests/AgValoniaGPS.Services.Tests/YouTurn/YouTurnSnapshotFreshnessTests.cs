// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Coverage;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Pipeline;
using AgValoniaGPS.Services.Section;
using AgValoniaGPS.Services.Tool;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests.YouTurn;

/// <summary>
/// Cross-snapshot freshness contract: every public field on the cycle's
/// emitted YouTurnSnapshot must equal the post-tick value of the same field
/// on the pipeline's private cycle-owned YouTurnWorkingState. Same contract
/// for GuidanceSnapshot.HowManyPathsAway versus the private guidance
/// working state — that was the v5 exit-seam bug landed as commit 4563fcff:
/// the snapshot was built before the cycle re-read the post-completion
/// pass number, so the UI saw the pre-turn pass for one cycle and the
/// off-pass branch hard-spiked the steering.
///
/// The fixture asserts the freshness contract generically (reflection over
/// every property on the snapshot record) so a future field added on
/// YouTurnSnapshot or GuidanceSnapshot can never silently drift from its
/// working-state source — the test will fail until the BuildXSnapshot
/// builder is updated.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class YouTurnSnapshotFreshnessTests
{
    private GpsService _gpsService = null!;
    private GpsPipelineService _pipeline = null!;
    private ApplicationState _appState = null!;
    private List<GpsCycleResult> _results = null!;

    [SetUp]
    public void SetUp()
    {
        ConfigurationStore.SetInstance(new ConfigurationStore());
        var config = ConfigurationStore.Instance;
        config.Vehicle.AntennaPivot = 0;
        config.Vehicle.AntennaOffset = 0;
        config.Vehicle.AntennaHeight = 0;
        config.Vehicle.Wheelbase = 2.5;
        config.Tool.Width = 6;
        config.Tool.Overlap = 0;
        config.NumSections = 1;
        config.Tool.SetSectionWidth(0, 600);

        _appState = new ApplicationState();
        _appState.Field.LocalPlane = new LocalPlane(
            new Wgs84(43.7128, -74.006), new SharedFieldProperties());

        _gpsService = new GpsService();
        _gpsService.Start();

        var toolPosition = new ToolPositionService();
        var coverage = new CoverageMapService();
        var sectionControl = new SectionControlService(toolPosition, coverage, _appState);
        var autoSteer = new AutoSteerService(
            new TrackGuidanceService(),
            Substitute.For<IUdpCommunicationService>(),
            _gpsService, _appState);

        var headingFusion = Substitute.For<IGpsHeadingFusionService>();
        headingFusion.FuseHeading(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<bool>(),
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(0));

        _pipeline = new GpsPipelineService(
            _gpsService, toolPosition, new TrackGuidanceService(),
            sectionControl, coverage, autoSteer,
            new YouTurnGuidanceService(),
            new YouTurnStateMachine(
                new YouTurnCreationService(
                    NullLogger<YouTurnCreationService>.Instance,
                    new PolygonOffsetService()),
                new YouTurnPathingService(NullLogger<YouTurnPathingService>.Instance),
                NullLogger<YouTurnStateMachine>.Instance),
            Substitute.For<IAudioService>(),
            new PipelineIntents(),
            headingFusion,
            NullLogger<GpsPipelineService>.Instance, _appState,
            new PositionEstimator());

        _pipeline.SynchronousMode = true;
        _pipeline.Start();

        _results = new List<GpsCycleResult>();
        _pipeline.CycleCompleted += r => _results.Add(r);
    }

    [TearDown]
    public void TearDown()
    {
        _pipeline.Stop();
        _gpsService.Stop();
    }

    private GpsCycleResult Last => _results[^1];

    /// <summary>
    /// Reach into the pipeline's private cycle-owned working state. The
    /// freshness contract is between the snapshot the cycle emitted on
    /// CycleCompleted and the post-tick value of these private POCOs.
    /// </summary>
    private YouTurnWorkingState PrivateYouTurn() => GetPrivate<YouTurnWorkingState>("_youTurn");
    private GuidanceWorkingState PrivateGuidance() => GetPrivate<GuidanceWorkingState>("_guidanceWorking");

    private T GetPrivate<T>(string name)
    {
        var f = typeof(GpsPipelineService).GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(f, Is.Not.Null,
            $"Pipeline internal '{name}' was renamed or removed — freshness "
            + "test needs updating along with the field rename.");
        return (T)f!.GetValue(_pipeline)!;
    }

    /// <summary>
    /// Seed the cycle-owned YouTurnWorkingState mid-turn so non-default
    /// values flow through BuildYouTurnSnapshot. The fields here are the
    /// ones a real turn-in-progress would populate.
    /// </summary>
    private void SeedMidTurnState()
    {
        var turn = PrivateYouTurn();
        turn.IsEnabled = true;
        turn.IsTriggered = true;
        turn.IsExecuting = true;
        turn.IsTurnLeft = true;
        turn.LastTurnWasLeft = false;
        turn.DistanceToHeadland = 4.5;
        turn.DistanceToTrigger = 1.2;
        turn.PathIndex = 7;
        turn.YouTurnCounter = 17;
        turn.WasHeadingSameWayAtTurnStart = true;
        turn.NextTrackTurnOffset = 6.0;
        turn.HasCompletedFirstTurn = false;
        turn.LastCompletionPosition = new Vec2(12.5, 34.5);
        turn.CurrentZone = TractorZone.InCultivatedArea;
        turn.TurnPath = new List<Vec3>
        {
            new(0, 0, 0), new(1, 1, Math.PI / 4),
            new(2, 2, Math.PI / 2), new(3, 1, Math.PI * 3 / 4),
            new(4, 0, Math.PI),
        };
        turn.NextTrack = Models.Track.Track.FromABLine(
            "snap-next", new Vec3(6, -50, 0), new Vec3(6, 50, 0));

        var guidance = PrivateGuidance();
        guidance.HowManyPathsAway = 3;
        guidance.NudgeOffset = 0.04;
        guidance.IsHeadingSameWay = true;
        guidance.CurrentLineLabel = "4R";
    }

    private void TickOnce()
    {
        // A single GPS update inside a loaded LocalPlane area triggers one
        // synchronous ProcessCycle, which produces one GpsCycleResult.
        _gpsService.UpdateGpsData(new GpsData
        {
            CurrentPosition = new Position { Latitude = 43.7128, Longitude = -74.006 },
            FixQuality = 4,
            IsValid = true,
        });
        Assert.That(_results.Count, Is.GreaterThanOrEqualTo(1),
            "SynchronousMode pipeline must emit one cycle result per GPS update");
    }

    // ── Per-field locks for the most load-bearing YouTurnSnapshot fields ──

    [Test]
    public void Snapshot_IsEnabled_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.IsEnabled, Is.EqualTo(PrivateYouTurn().IsEnabled));
    }

    [Test]
    public void Snapshot_IsTriggered_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.IsTriggered, Is.EqualTo(PrivateYouTurn().IsTriggered));
    }

    [Test]
    public void Snapshot_IsExecuting_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.IsExecuting, Is.EqualTo(PrivateYouTurn().IsExecuting));
    }

    [Test]
    public void Snapshot_TurnPath_is_same_reference_as_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        // Ref equality matters: the snapshot exposes the working list to the
        // UI thread, and a defensive copy would mask any aliasing bug in the
        // producer.
        Assert.That(Last.YouTurn.TurnPath, Is.SameAs(PrivateYouTurn().TurnPath));
    }

    [Test]
    public void Snapshot_NextTrack_is_same_reference_as_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.NextTrack, Is.SameAs(PrivateYouTurn().NextTrack));
    }

    [Test]
    public void Snapshot_PathIndex_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.PathIndex, Is.EqualTo(PrivateYouTurn().PathIndex));
    }

    [Test]
    public void Snapshot_YouTurnCounter_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.YouTurnCounter, Is.EqualTo(PrivateYouTurn().YouTurnCounter));
    }

    [Test]
    public void Snapshot_IsTurnLeft_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.IsTurnLeft, Is.EqualTo(PrivateYouTurn().IsTurnLeft));
    }

    [Test]
    public void Snapshot_DistanceToTrigger_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.DistanceToTrigger,
            Is.EqualTo(PrivateYouTurn().DistanceToTrigger));
    }

    [Test]
    public void Snapshot_DistanceToHeadland_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.DistanceToHeadland,
            Is.EqualTo(PrivateYouTurn().DistanceToHeadland));
    }

    [Test]
    public void Snapshot_CurrentZone_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.YouTurn.CurrentZone, Is.EqualTo(PrivateYouTurn().CurrentZone));
    }

    // ── Cross-snapshot pin: the v5 exit-seam bug (4563fcff) ─────────────

    [Test]
    public void GuidanceSnapshot_HowManyPathsAway_matches_working_state()
    {
        // This is the field whose stale-read landed as commit 4563fcff —
        // pin it explicitly so a regression of the same kind is caught
        // unambiguously.
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.Guidance.HowManyPathsAway,
            Is.EqualTo(PrivateGuidance().HowManyPathsAway));
    }

    [Test]
    public void GuidanceSnapshot_NudgeOffset_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.Guidance.NudgeOffset,
            Is.EqualTo(PrivateGuidance().NudgeOffset));
    }

    [Test]
    public void GuidanceSnapshot_IsHeadingSameWay_matches_working_state()
    {
        SeedMidTurnState();
        TickOnce();
        Assert.That(Last.Guidance.IsHeadingSameWay,
            Is.EqualTo(PrivateGuidance().IsHeadingSameWay));
    }

    // ── Generic reflection fences ─────────────────────────────────────────

    /// <summary>
    /// For every property on YouTurnSnapshot that has a same-named property
    /// on YouTurnWorkingState, the snapshot value must equal the post-tick
    /// working-state value. A future field added to YouTurnSnapshot is
    /// automatically covered by this fence — and must be wired up in
    /// BuildYouTurnSnapshot to make the test pass.
    /// </summary>
    [Test]
    public void Snapshot_AllSharedFields_Match_YouTurnWorkingState()
    {
        SeedMidTurnState();
        TickOnce();
        AssertAllSharedFieldsMatch(typeof(YouTurnSnapshot), Last.YouTurn,
            typeof(YouTurnWorkingState), PrivateYouTurn(),
            // JustCompleted is a one-shot signal computed at snapshot time;
            // there is no corresponding working-state field by design.
            new HashSet<string> { "JustCompleted" });
    }

    [Test]
    public void GuidanceSnapshot_AllSharedFields_Match_GuidanceWorkingState()
    {
        SeedMidTurnState();
        TickOnce();
        AssertAllSharedFieldsMatch(typeof(GuidanceSnapshot), Last.Guidance,
            typeof(GuidanceWorkingState), PrivateGuidance(),
            // Snapshot-only fields with no working-state mirror by design.
            new HashSet<string> { "HasGuidance", "DisplayTrack", "BaseTrack" });
    }

    private static void AssertAllSharedFieldsMatch(
        Type snapshotType, object snapshot,
        Type workingType, object working,
        ISet<string> skipNames)
    {
        var workingProps = workingType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);

        var mismatches = new List<string>();
        int compared = 0;
        foreach (var sp in snapshotType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (skipNames.Contains(sp.Name)) continue;
            if (!workingProps.TryGetValue(sp.Name, out var wp)) continue;
            if (!sp.PropertyType.IsAssignableFrom(wp.PropertyType)
                && !wp.PropertyType.IsAssignableFrom(sp.PropertyType))
                continue;

            var sv = sp.GetValue(snapshot);
            var wv = wp.GetValue(working);
            compared++;
            if (!Equals(sv, wv))
                mismatches.Add($"{sp.Name}: snapshot={Format(sv)} working={Format(wv)}");
        }

        Assert.That(compared, Is.GreaterThan(0),
            $"Reflection found no shared fields between {snapshotType.Name} and "
            + $"{workingType.Name} — fence is silently a no-op.");
        Assert.That(mismatches, Is.Empty,
            $"Snapshot drift from working state ({snapshotType.Name}): "
            + string.Join("; ", mismatches));
    }

    private static string Format(object? v) => v switch
    {
        null => "null",
        string s => $"\"{s}\"",
        System.Collections.ICollection c => $"<{c.GetType().Name}[Count={c.Count}]>",
        _ => v.ToString() ?? "null",
    };
}
