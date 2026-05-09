// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using System.Reflection;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Coverage;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Pipeline;
using AgValoniaGPS.Services.Section;
using AgValoniaGPS.Services.Tool;
using AgValoniaGPS.Services.Track;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests.Pipeline;

/// <summary>
/// Bug regression: when the operator toggles YouTurn off via the enable
/// button, any rendered U-turn arc must clear from the map. The state
/// machine's auto-tick is gated on youTurnEnabled, so without an explicit
/// clear the cycle-owned YouTurnWorkingState would freeze with
/// IsTriggered/IsExecuting=true and emit the same TurnPath every cycle.
/// ApplyGpsCycleResult would keep pushing it back to the map.
///
/// Mirrors <c>YouTurnDisengageClearTests</c> for the autosteer-disengage
/// path; both clears live on the cycle worker.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton.
public class YouTurnDisableClearTests
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
        config.Tool.Width = 6;
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
        var autoSteer = new AutoSteerService(new TrackGuidanceService(),
            Substitute.For<IUdpCommunicationService>(), _gpsService, _appState);

        var headingFusion = Substitute.For<IGpsHeadingFusionService>();
        headingFusion.FuseHeading(Arg.Any<double>(), Arg.Any<double>(), Arg.Any<bool>(),
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>())
            .Returns(ci => ci.ArgAt<double>(0));

        _pipeline = new GpsPipelineService(
            _gpsService, toolPosition, new TrackGuidanceService(),
            sectionControl, coverage, autoSteer,
            new YouTurnGuidanceService(),
            new YouTurnStateMachine(
                new YouTurnCreationService(NullLogger<YouTurnCreationService>.Instance,
                    Substitute.For<AgValoniaGPS.Services.Geometry.IPolygonOffsetService>()),
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
    /// Reach into the cycle-owned <see cref="YouTurnWorkingState"/> via reflection
    /// to simulate a turn already in progress. Production code mutates this only
    /// from the cycle thread (state-machine ticks, manual triggers, ClearState);
    /// the test reaches in directly to skip the state-machine geometry that this
    /// test isn't trying to exercise.
    /// </summary>
    private YouTurnWorkingState GetCycleYouTurn()
    {
        var fld = typeof(GpsPipelineService).GetField("_youTurn",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(fld, Is.Not.Null, "Reflection target _youTurn missing — pipeline internals changed");
        return (YouTurnWorkingState)fld!.GetValue(_pipeline)!;
    }

    private static GpsData ValidFix() => new()
    {
        CurrentPosition = new Position { Latitude = 43.7128, Longitude = -74.006 },
        FixQuality = 4,
        IsValid = true,
    };

    [Test]
    public void Cycle_WithYouTurnDisabled_ClearsActiveYouTurnSnapshot()
    {
        // Seed a turn-in-progress state on the cycle's working POCO. This is what
        // it'd look like mid-arc: triggered, executing, with an arc path queued.
        var youTurn = GetCycleYouTurn();
        youTurn.IsTriggered = true;
        youTurn.IsExecuting = true;
        youTurn.TurnPath = new List<Vec3>
        {
            new(0, 0, 0), new(1, 1, 0), new(2, 2, 0),
        };
        youTurn.YouTurnCounter = 7;

        // YouTurn enable toggle is off — this is the bug: the operator hit the
        // enable button mid-turn (or while a stale turn was rendered).
        _pipeline.SetYouTurnEnabled(false);

        _gpsService.UpdateGpsData(ValidFix());

        Assert.That(Last.YouTurn, Is.Not.Null,
            "Pipeline must always emit a YouTurn snapshot");
        Assert.Multiple(() =>
        {
            Assert.That(Last.YouTurn!.IsTriggered, Is.False,
                "Disabling YouTurn must clear IsTriggered so the map stops drawing the arc");
            Assert.That(Last.YouTurn!.IsExecuting, Is.False,
                "Disabling YouTurn must clear IsExecuting so SetIsInYouTurn(false) propagates");
            Assert.That(Last.YouTurn!.TurnPath, Is.Null,
                "Disabling YouTurn must clear TurnPath so SetYouTurnPath(null) propagates");
        });
    }

    /// <summary>
    /// Regression for the distance-widget bug: the pipeline must mirror the
    /// user's <c>SetYouTurnEnabled</c> toggle into the cycle-owned
    /// <see cref="YouTurnWorkingState.IsEnabled"/> flag every cycle. The
    /// snapshot mirror in <c>ApplyGpsCycleResult</c> reads this field, and the
    /// distance-to-trigger widget's <c>IsUTurnDistanceVisible</c> predicate in
    /// turn reads the snapshot's flag on <c>State.YouTurn</c>. Without this
    /// mirror, <c>IsEnabled</c> stays at its default <c>false</c> and the
    /// widget is hidden permanently.
    /// </summary>
    [Test]
    public void Cycle_WithYouTurnEnabled_PublishesIsEnabledTrueOnSnapshot()
    {
        _pipeline.SetYouTurnEnabled(true);

        _gpsService.UpdateGpsData(ValidFix());

        Assert.That(Last.YouTurn, Is.Not.Null);
        Assert.That(Last.YouTurn!.IsEnabled, Is.True,
            "Pipeline must mirror SetYouTurnEnabled(true) into YouTurnSnapshot.IsEnabled");
    }

    [Test]
    public void Cycle_WithYouTurnDisabled_PublishesIsEnabledFalseOnSnapshot()
    {
        // Start enabled then disable to verify the mirror tracks both transitions.
        _pipeline.SetYouTurnEnabled(true);
        _gpsService.UpdateGpsData(ValidFix());
        _pipeline.SetYouTurnEnabled(false);
        _gpsService.UpdateGpsData(ValidFix());

        Assert.That(Last.YouTurn!.IsEnabled, Is.False,
            "Pipeline must mirror SetYouTurnEnabled(false) into YouTurnSnapshot.IsEnabled");
    }

    [Test]
    public void Cycle_WithYouTurnEnabled_DoesNotForceClearYouTurn()
    {
        // YouTurn enabled — the disable-clear path must NOT fire. (No track is
        // set, so the auto-tick won't reach in either; this exercises only the
        // clear-on-disable branch we added.) Also engage autosteer so the
        // sibling clear-on-disengage block stays quiet too.
        _pipeline.SetYouTurnEnabled(true);
        _pipeline.SetAutoSteerEngaged(true);

        var youTurn = GetCycleYouTurn();
        youTurn.IsTriggered = true;
        youTurn.IsExecuting = true;
        youTurn.TurnPath = new List<Vec3>
        {
            new(0, 0, 0), new(1, 1, 0), new(2, 2, 0),
        };

        _gpsService.UpdateGpsData(ValidFix());

        Assert.Multiple(() =>
        {
            Assert.That(Last.YouTurn!.IsExecuting, Is.True,
                "With YouTurn enabled the disable-clear must not fire");
            Assert.That(Last.YouTurn!.TurnPath, Is.Not.Null,
                "With YouTurn enabled the seeded turn path must survive the cycle");
        });
    }
}
