using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Section;
using NSubstitute;

namespace AgValoniaGPS.Services.Tests;

/// <summary>
/// Tests for SectionControlService - section on/off logic, manual overrides,
/// position calculations, and bitmask generation.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore is a singleton
public class SectionControlServiceTests
{
    private ICoverageMapService _coverageMap = null!;
    private IToolPositionService _toolPosition = null!;
    private ApplicationState _appState = null!;
    private SectionControlService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Isolate ConfigurationStore singleton
        ConfigurationStore.SetInstance(new ConfigurationStore());

        // Configure 3 sections, 200cm (2m) each = 6m total tool width
        var config = ConfigurationStore.Instance;
        config.NumSections = 3;
        config.Tool.SetSectionWidth(0, 200);
        config.Tool.SetSectionWidth(1, 200);
        config.Tool.SetSectionWidth(2, 200);
        config.Tool.Offset = 0;

        _coverageMap = Substitute.For<ICoverageMapService>();
        _toolPosition = Substitute.For<IToolPositionService>();
        _appState = new ApplicationState();

        _service = new SectionControlService(_toolPosition, _coverageMap, _appState);
    }

    #region Slow Speed

    [Test]
    public void Update_SlowSpeed_AutoSectionsOff_ManualStaysOn()
    {
        // Set sections: 0=Manual ON, 1=Manual ON, 2=Auto
        _service.SetAllAuto();
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(1, SectionButtonState.On);

        // Update at very slow speed (below 0.5 m/s cutoff)
        _service.Update(new Vec3(50, 50, 0), 0, 0, 0.1);

        // Manual ON sections stay on (prevents coverage gap on stop/restart)
        Assert.That(_service.SectionStates[0].IsOn, Is.True);
        Assert.That(_service.SectionStates[1].IsOn, Is.True);
        // Auto sections turn off
        Assert.That(_service.SectionStates[2].IsOn, Is.False);
    }

    [Test]
    public void Update_SlowSpeed_AutoSection_InsideBoundary_DoesNotReArmOnRequest()
    {
        // Regression for #324: at standstill inside the boundary, Auto
        // sections were stuck displaying "Turning ON" (orange, code 4)
        // because the slow-speed cutoff cleared the request, then
        // UpdateSection's look-ahead re-armed it the same frame
        // (lookOnDist=0 at speed=0 evaluates the section center, which
        // sits inside the boundary → shouldBeOn=true → SectionOnRequest=true).
        // Expected: section settles to Auto OFF (code 5) — IsOn=false AND
        // SectionOnRequest=false.
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 200, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 200, 0));
        outerPoly.UpdateBounds();
        _appState.Field.CurrentBoundary = new Boundary { OuterBoundary = outerPoly };

        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;

        // Tick a few frames at standstill so any re-arm bug compounds.
        // Use a position well inside the boundary so all section centers are inside.
        for (int frame = 0; frame < 5; frame++)
        {
            _service.Update(new Vec3(100, 100, 0), 0, 0, 0.1);
        }

        for (int i = 0; i < 3; i++)
        {
            Assert.That(_service.SectionStates[i].IsOn, Is.False,
                $"Section {i} IsOn should be false at standstill");
            Assert.That(_service.SectionStates[i].SectionOnRequest, Is.False,
                $"Section {i} SectionOnRequest should not be re-armed at standstill (would render as orange Turning ON)");
            Assert.That(_service.SectionStates[i].SectionOnTimer, Is.EqualTo(0),
                $"Section {i} SectionOnTimer should be 0 at standstill");
        }
    }

    #endregion

    #region Tick rate (#313 commit 5a)

    [Test]
    public void TickHz_Default_Is10Hz()
    {
        Assert.That(_service.TickHz, Is.EqualTo(10.0));
    }

    [Test]
    public void TickHz_100_TurnOnDelayScalesUp()
    {
        // With LookAheadOn = 0.2 s configured, at 100 Hz that's 20 ticks of
        // wait. Verify by counting Update calls — section flips on tick 20.
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 200, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 200, 0));
        outerPoly.UpdateBounds();
        _appState.Field.CurrentBoundary = new Boundary { OuterBoundary = outerPoly };

        ConfigurationStore.Instance.Tool.LookAheadOnSetting = 0.2;  // 200 ms wait

        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;
        _service.TickHz = 100.0;

        // At 100 Hz, ON delay = 0.2 s × 100 = 20 ticks. With >= semantics
        // the section flips on the tick that completes the debounce, so
        // tick 20 is the first that turns IsOn = true (200 ms exactly).
        for (int i = 0; i < 19; i++)
        {
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
            Assert.That(_service.SectionStates[0].IsOn, Is.False,
                $"Should not turn on yet at tick {i + 1} of 20 (100 Hz, 0.2s delay)");
        }
        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Should turn on at tick 20 (completing 200 ms debounce)");
    }

    [Test]
    public void TickHz_10_TurnOnDelayMatchesLegacyBehavior()
    {
        // Same scenario as above but at the legacy 10 Hz default. Should
        // flip on tick 2 (completing the 200 ms wait).
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 200, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 200, 0));
        outerPoly.UpdateBounds();
        _appState.Field.CurrentBoundary = new Boundary { OuterBoundary = outerPoly };

        ConfigurationStore.Instance.Tool.LookAheadOnSetting = 0.2;

        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;
        // TickHz default = 10.

        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.False,
            "Should not turn on yet at tick 1 of 2 (10 Hz, 0.2s delay)");
        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Should turn on at tick 2 (completing 200 ms debounce)");
    }

    #endregion

    #region Coverage thresholds (#345 hysteresis + #348 gap-fill)

    [Test]
    public void Coverage_StaysOnUntilFullyCovered_TurnsOnBelowMinCoverage()
    {
        // Combined repro for #345 (slow-speed flicker — needs hysteresis gap
        // between ON and OFF thresholds) and #348 (sections turn off too
        // early over partly-painted strips, leaving gaps unfilled).
        //
        // Design: ON threshold = MinCoverage (user's overlap preference);
        // OFF threshold = 0.99 (only when essentially fully covered).
        // The ~30 percentage-point gap between them is the hysteresis.
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 200, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 200, 0));
        outerPoly.UpdateBounds();
        _appState.Field.CurrentBoundary = new Boundary { OuterBoundary = outerPoly };

        ConfigurationStore.Instance.Tool.MinCoverage = 70; // ON=0.70, OFF=0.99
        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;

        void SetCoverage(double pct)
        {
            var r = new CoverageResult(pct, pct > 0, pct >= 1.0, 0);
            _coverageMap.GetSegmentCoverageMulti(default, default, default, default, default)
                .ReturnsForAnyArgs((r, r, r));
        }

        // Phase 1: 0 % coverage → section turns ON (well below MinCoverage).
        SetCoverage(0.0);
        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Phase 1: section should turn ON at 0 % coverage");

        // Phase 2: 65 % coverage — below the ON threshold. Section is already
        // ON, stays ON. (Exercises the slow-speed-flicker scenario from #345
        // where coverage % bobs around MinCoverage; the natural ON↔OFF gap
        // means neither boolean flips.)
        SetCoverage(0.65);
        for (int i = 0; i < 5; i++)
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Phase 2: section should stay ON below ON threshold (65 %)");

        // Phase 3: 85 % coverage — past MinCoverage but well short of full.
        // This is the #348 case: under the old logic the section would have
        // turned OFF here, leaving the remaining 15 % of cells unsprayed.
        // With the new OFF threshold it stays ON to fill the remaining gaps.
        SetCoverage(0.85);
        for (int i = 0; i < 5; i++)
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Phase 3: section should stay ON over partly-painted area (85 %) to fill gaps (#348)");

        // Phase 4: 99 % coverage hits the OFF threshold → section turns OFF.
        SetCoverage(0.99);
        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.False,
            "Phase 4: section should turn OFF when coverage reaches 99 % (full)");

        // Phase 5: 75 % coverage — above MinCoverage, in the hysteresis
        // dead-zone. Section just turned OFF; stays OFF. Without the gap the
        // section would flap back on the moment coverage dropped below 99 %.
        SetCoverage(0.75);
        for (int i = 0; i < 5; i++)
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.False,
            "Phase 5: section should stay OFF in the hysteresis gap (75 %)");

        // Phase 6: 60 % coverage — below MinCoverage (the ON threshold). The
        // section turns back on: there's enough uncovered area to be worth
        // spraying.
        SetCoverage(0.60);
        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Phase 6: section should turn ON below MinCoverage (60 %)");
    }

    [Test]
    public void StaleRequestFlags_ClearedOnSteadyState_NoStuckTransitionColor()
    {
        // Regression: user reported sections staying "blue" (cyan, code 3 =
        // Turning OFF) or "orange" (code 4 = Turning ON) for longer than they
        // are supposed to. The state machine sets SectionOnRequest /
        // SectionOffRequest in the transition branches but the steady-state
        // branches (`else if (section.IsOn)` and trailing `else`) only reset
        // the timers — not the request flags. So a single flicker tick of
        // shouldBeOff while IsOn=true leaves SectionOffRequest stuck true
        // forever (rendering as cyan), and a flicker of shouldBeOn while
        // IsOn=false leaves SectionOnRequest stuck (rendering as orange).
        //
        // This test drives both flicker patterns and asserts the request
        // flags are cleared on the next steady tick so the section renders
        // the correct steady-state color (Auto ON green, code 2; Auto OFF
        // gray, code 5).
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 200, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 200, 0));
        outerPoly.UpdateBounds();
        _appState.Field.CurrentBoundary = new Boundary { OuterBoundary = outerPoly };

        // Use non-zero LookAheadOff so a single tick of shouldBeOff doesn't
        // accumulate enough phase ticks to actually flip IsOn — that's the
        // window where the bug shows up.
        ConfigurationStore.Instance.Tool.LookAheadOffSetting = 0.5; // 500 ms
        ConfigurationStore.Instance.Tool.LookAheadOnSetting = 0.5;
        ConfigurationStore.Instance.Tool.MinCoverage = 70;

        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;
        _service.TickHz = 100.0; // matches host control loop

        void SetCoverage(double pct)
        {
            var r = new CoverageResult(pct, pct > 0, pct >= 1.0, 0);
            _coverageMap.GetSegmentCoverageMulti(default, default, default, default, default)
                .ReturnsForAnyArgs((r, r, r));
        }

        // Phase 1: Drive to steady-on (uncovered, in boundary, in headland-free).
        // turnOnPhaseTicks = 0.5 s × 100 Hz = 50 ticks of shouldBeOn needed.
        SetCoverage(0.0);
        for (int i = 0; i < 60; i++)
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Phase 1: section should be ON after exhausting the turn-on debounce");
        Assert.That(_service.SectionStates[0].SectionOnRequest, Is.False);
        Assert.That(_service.SectionStates[0].SectionOffRequest, Is.False);

        // Phase 2: One tick of "fully covered" (shouldBeOff = true) — sets
        // SectionOffRequest=true and SectionOffTimer=1. Far below the 50-tick
        // threshold, so IsOn stays true. This is the trigger for the bug.
        SetCoverage(1.0);
        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Phase 2: IsOn stays true after a single shouldBeOff tick");
        Assert.That(_service.SectionStates[0].SectionOffRequest, Is.True,
            "Phase 2: SectionOffRequest is set during the (incomplete) turn-off transition");

        // Phase 3: Coverage drops back below the OFF threshold (shouldBeOff
        // becomes false) and we're still ON → falls into the steady-on
        // branch. The bug: SectionOffRequest was previously NOT cleared here,
        // leaving the section rendered as code 3 (Turning OFF, cyan) forever.
        SetCoverage(0.5); // below 0.99 OFF threshold, above 0.70 ON threshold
        for (int i = 0; i < 5; i++)
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.True,
            "Phase 3: section stays ON in the hysteresis gap");
        Assert.That(_service.SectionStates[0].SectionOffRequest, Is.False,
            "Phase 3: SectionOffRequest must be cleared in the steady-on branch — " +
            "otherwise the UI renders as code 3 (Turning OFF / cyan) indefinitely");
        Assert.That(_service.SectionStates[0].SectionOffTimer, Is.EqualTo(0));

        // Phase 4: Push to fully covered for the full debounce so IsOn flips off.
        SetCoverage(1.0);
        for (int i = 0; i < 60; i++)
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.False,
            "Phase 4: section should be OFF after exhausting the turn-off debounce");

        // Phase 5: One tick of "uncovered" (shouldBeOn = true) — sets
        // SectionOnRequest=true and SectionOnTimer=1. Below the 50-tick
        // threshold, so IsOn stays false. Symmetric trigger to Phase 2.
        SetCoverage(0.0);
        _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.False,
            "Phase 5: IsOn stays false after a single shouldBeOn tick");
        Assert.That(_service.SectionStates[0].SectionOnRequest, Is.True,
            "Phase 5: SectionOnRequest is set during the (incomplete) turn-on transition");

        // Phase 6: Coverage rises into the hysteresis gap (above ON threshold,
        // below OFF threshold). shouldBeOn becomes false; section falls into
        // the steady-off branch. The bug: SectionOnRequest was previously NOT
        // cleared here, leaving the section rendered as code 4
        // (Turning ON / orange) indefinitely.
        SetCoverage(0.85); // above 0.70 ON threshold, below 0.99 OFF threshold
        for (int i = 0; i < 5; i++)
            _service.Update(new Vec3(100, 100, 0), 0, 0, 5.0);
        Assert.That(_service.SectionStates[0].IsOn, Is.False,
            "Phase 6: section stays OFF in the hysteresis gap");
        Assert.That(_service.SectionStates[0].SectionOnRequest, Is.False,
            "Phase 6: SectionOnRequest must be cleared in the steady-off branch — " +
            "otherwise the UI renders as code 4 (Turning ON / orange) indefinitely");
        Assert.That(_service.SectionStates[0].SectionOnTimer, Is.EqualTo(0));
    }

    [Test]
    public void LookAheadDistance_ClampedToMinimum_AtSlowSpeedAndZeroSetting()
    {
        // Repro for the structural part of #345: with LookAheadOn/Off=0 and
        // slow speed, lookAhead distance = speed × 0 = 0. The look-on/off
        // sample point collapses onto the section center, where it samples
        // the section's own freshly-painted swath and shouldBeOff fires off
        // the section's own coverage every tick. The fix clamps the FORWARD
        // distance to a minimum so the sample point clears the painted area
        // regardless of speed or LookAheadSetting.
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 200, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 200, 0));
        outerPoly.UpdateBounds();
        _appState.Field.CurrentBoundary = new Boundary { OuterBoundary = outerPoly };

        ConfigurationStore.Instance.Tool.LookAheadOnSetting = 0;
        ConfigurationStore.Instance.Tool.LookAheadOffSetting = 0;
        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;

        // Slow speed (~5 km/h ≈ 1.4 m/s). With LookAheadSetting=0, the
        // un-clamped look-ahead distance would be 0 in both directions.
        _service.Update(new Vec3(100, 100, 0), 0, 0, 1.4);

        // Both look-on and look-off distances passed to the coverage service
        // must be ≥ the floor (~0.3 m) so the sample point clears the swath.
        _coverageMap.Received().GetSegmentCoverageMulti(
            Arg.Any<Vec2>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Is<double>(d => d >= 0.3),
            Arg.Is<double>(d => d >= 0.3));
    }

    [Test]
    public void LookAheadDistance_NotClamped_WhenSpeedTimeExceedsFloor()
    {
        // Inverse of the slow-speed case: at normal speed and a non-zero
        // LookAheadSetting, the user's anticipation is preserved exactly —
        // the floor only kicks in when speed × time would otherwise be
        // smaller than it.
        var outerPoly = new BoundaryPolygon();
        outerPoly.Points.Add(new BoundaryPoint(0, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 0, 0));
        outerPoly.Points.Add(new BoundaryPoint(200, 200, 0));
        outerPoly.Points.Add(new BoundaryPoint(0, 200, 0));
        outerPoly.UpdateBounds();
        _appState.Field.CurrentBoundary = new Boundary { OuterBoundary = outerPoly };

        ConfigurationStore.Instance.Tool.LookAheadOnSetting = 0.5;  // 500 ms
        ConfigurationStore.Instance.Tool.LookAheadOffSetting = 0.4; // 400 ms
        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;

        // 4 m/s × 0.5 s = 2.0 m for ON; 4 × 0.4 = 1.6 m for OFF.
        // Both well above the 0.3 m floor — no clamping should occur.
        _service.Update(new Vec3(100, 100, 0), 0, 0, 4.0);

        _coverageMap.Received().GetSegmentCoverageMulti(
            Arg.Any<Vec2>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Is<double>(d => Math.Abs(d - 2.0) < 0.001),
            Arg.Is<double>(d => Math.Abs(d - 1.6) < 0.001));
    }

    #endregion

    #region No Boundary

    [Test]
    public void Update_NoBoundary_AutoSectionsStayOff()
    {
        // Issue #347: no field open (boundary null) must mean "no spray".
        // GetSegmentBoundaryStatus returns FullyOutside in that case, so the
        // strict isInBoundary check fails for every section and the early
        // UpdateSectionOff path forces IsOn=false. Spraying outside a defined
        // field is never the intended operation — matches firmware behavior.
        _service.SetAllAuto();
        _service.MasterState = SectionMasterState.Auto;

        // Run several ticks to outlast any TURNING_ON debounce; with no
        // boundary, IsAnySectionOn must remain false.
        for (int i = 0; i < 5; i++)
            _service.Update(new Vec3(50, 50, 0), 0, 0, 5.0);

        Assert.That(_service.IsAnySectionOn, Is.False);
    }

    #endregion

    #region Manual Override

    [Test]
    public void SetSectionState_ManualOn_TurnsOnImmediately()
    {
        _service.SetSectionState(0, SectionButtonState.On);

        Assert.That(_service.SectionStates[0].IsOn, Is.True);
        Assert.That(_service.SectionStates[0].ButtonState, Is.EqualTo(SectionButtonState.On));
    }

    [Test]
    public void SetSectionState_ManualOff_TurnsOffImmediately()
    {
        // First turn on
        _service.SetSectionState(1, SectionButtonState.On);
        Assert.That(_service.SectionStates[1].IsOn, Is.True);

        // Then force off
        _service.SetSectionState(1, SectionButtonState.Off);

        Assert.That(_service.SectionStates[1].IsOn, Is.False);
        Assert.That(_service.SectionStates[1].ButtonState, Is.EqualTo(SectionButtonState.Off));
    }

    [Test]
    public void SetAllAuto_ResetsAllSections()
    {
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(1, SectionButtonState.Off);

        _service.SetAllAuto();

        Assert.That(_service.SectionStates[0].ButtonState, Is.EqualTo(SectionButtonState.Auto));
        Assert.That(_service.SectionStates[1].ButtonState, Is.EqualTo(SectionButtonState.Auto));
        Assert.That(_service.SectionStates[2].ButtonState, Is.EqualTo(SectionButtonState.Auto));
        Assert.That(_service.MasterState, Is.EqualTo(SectionMasterState.Auto));
    }

    #endregion

    #region TurnAllOff / MasterState

    [Test]
    public void TurnAllOff_SetsAllOff()
    {
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(1, SectionButtonState.On);
        _service.SetSectionState(2, SectionButtonState.On);

        _service.TurnAllOff();

        Assert.That(_service.SectionStates[0].IsOn, Is.False);
        Assert.That(_service.SectionStates[1].IsOn, Is.False);
        Assert.That(_service.SectionStates[2].IsOn, Is.False);
        Assert.That(_service.IsAnySectionOn, Is.False);
    }

    [Test]
    public void MasterState_Off_TurnsAllOff()
    {
        // First set to Auto so we can transition to Off
        _service.MasterState = SectionMasterState.Auto;
        _service.SetSectionState(0, SectionButtonState.On);
        Assert.That(_service.SectionStates[0].IsOn, Is.True);

        _service.MasterState = SectionMasterState.Off;

        Assert.That(_service.SectionStates[0].IsOn, Is.False);
        Assert.That(_service.MasterState, Is.EqualTo(SectionMasterState.Off));
    }

    #endregion

    #region GetSectionBits

    [Test]
    public void GetSectionBits_ReturnsCorrectBitmask()
    {
        // Turn on sections 0 and 2 (bits 0b101 = 5)
        _service.SetSectionState(0, SectionButtonState.On);
        _service.SetSectionState(2, SectionButtonState.On);

        ushort bits = _service.GetSectionBits();

        Assert.That(bits, Is.EqualTo(0b101));
        Assert.That(bits & (1 << 0), Is.Not.Zero, "Section 0 bit should be set");
        Assert.That(bits & (1 << 1), Is.Zero, "Section 1 bit should not be set");
        Assert.That(bits & (1 << 2), Is.Not.Zero, "Section 2 bit should be set");
    }

    [Test]
    public void GetSectionBits_AllOff_ReturnsZero()
    {
        Assert.That(_service.GetSectionBits(), Is.EqualTo(0));
    }

    #endregion

    #region Section Positions

    [Test]
    public void GetSectionWorldPosition_ReturnsCorrectLeftRight()
    {
        // Tool at origin, heading north (0 radians)
        // 3 sections × 2m = 6m total, centered: [-3, -1, -1, 1, 1, 3]
        var toolPos = new Vec3(0, 0, 0);
        double heading = 0; // North

        var (left0, right0) = _service.GetSectionWorldPosition(0, toolPos, heading);
        var (left1, right1) = _service.GetSectionWorldPosition(1, toolPos, heading);
        var (left2, right2) = _service.GetSectionWorldPosition(2, toolPos, heading);

        // Section 0: left=-3m, right=-1m (to the left of center)
        Assert.That(left0.Easting, Is.EqualTo(-3.0).Within(0.01));
        Assert.That(right0.Easting, Is.EqualTo(-1.0).Within(0.01));

        // Section 1: left=-1m, right=1m (centered)
        Assert.That(left1.Easting, Is.EqualTo(-1.0).Within(0.01));
        Assert.That(right1.Easting, Is.EqualTo(1.0).Within(0.01));

        // Section 2: left=1m, right=3m (to the right of center)
        Assert.That(left2.Easting, Is.EqualTo(1.0).Within(0.01));
        Assert.That(right2.Easting, Is.EqualTo(3.0).Within(0.01));
    }

    [Test]
    public void RecalculateSectionPositions_UpdatesOnConfigChange()
    {
        // Change section width
        ConfigurationStore.Instance.Tool.SetSectionWidth(0, 400); // 4m

        // Recalculate
        _service.RecalculateSectionPositions();

        // Section 0 should now be 4m wide: total = 4+2+2 = 8m, centered at -4
        var (left0, right0) = _service.GetSectionWorldPosition(0, new Vec3(0, 0, 0), 0);
        Assert.That(right0.Easting - left0.Easting, Is.EqualTo(4.0).Within(0.01));
    }

    #endregion

    #region Event Firing

    [Test]
    public void SetSectionState_FiresSectionStateChangedEvent()
    {
        bool eventFired = false;
        _service.SectionStateChanged += (s, e) => eventFired = true;

        _service.SetSectionState(0, SectionButtonState.On);

        Assert.That(eventFired, Is.True);
    }

    #endregion
}
