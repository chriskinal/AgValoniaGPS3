// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Timing;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.Gps;
using AgValoniaGPS.Services.Headland;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.Services.Pipeline;

/// <summary>
/// Orchestrates the GPS processing pipeline on a background thread.
/// Subscribes to <see cref="IGpsService.GpsDataUpdated"/>, runs all heavy computation
/// off the UI thread, and fires <see cref="CycleCompleted"/> with an immutable result snapshot.
/// </summary>
public sealed class GpsPipelineService : IGpsPipelineService
{
    // Lookahead time (seconds) used for auto-track-select in free-drive mode.
    // Matches AgOpen's setAS_guidanceLookAheadTime default. See #261.
    private const double GuidanceLookAheadSeconds = 2.0;

    // Re-anchor the temporary first-fix LocalPlane when the live GPS jumps
    // farther than this from the existing origin (no field loaded). The
    // flat-earth approximation gets noisy at very large local-plane offsets
    // and the camera/render math goes weird, so silently re-anchor.
    private const double TempOriginReinitDistanceM = 50_000.0;

    // Surface a "GPS far from field" warning when the live GPS reports a
    // position farther than this from the loaded field's origin. Autosteer
    // is dropped immediately as a safety measure on the UI thread.
    private const double FieldOriginWarnDistanceM = 10_000.0;

    // ── Dependencies ────────────────────────────────────────────────────
    private readonly IGpsService _gpsService;
    private readonly IToolPositionService _toolPositionService;
    private readonly ITrackGuidanceService _trackGuidanceService;
    private readonly ISectionControlService _sectionControlService;
    private readonly ICoverageMapService _coverageMapService;
    private readonly IAutoSteerService _autoSteerService;
    private readonly YouTurnGuidanceService _youTurnGuidanceService;
    private readonly YouTurnStateMachine _youTurnStateMachine;
    private readonly IAudioService _audioService;
    private readonly IPipelineIntents _intents;
    private readonly IGpsHeadingFusionService _headingFusion;
    private readonly ILogger<GpsPipelineService> _logger;
    private readonly ApplicationState _appState;

    // ── Events ──────────────────────────────────────────────────────────
    public event Action<GpsCycleResult>? CycleCompleted;

    /// <summary>
    /// Fired inside ProcessCycle just after the canonical pose is published
    /// to <see cref="IPositionEstimator"/>, before the cycle reads section
    /// state to build <see cref="GpsCycleResult"/>. Argument is the timestamp
    /// of the publish (matches the snapshot's TimestampTicks).
    ///
    /// Production: no subscriber — the host control loop runs on its own
    /// thread and ticks at a fixed cadence regardless of GPS arrivals.
    /// Tests: subscribe a synchronous control-loop tick so GpsCycleResult.
    /// SectionStates reflects the section state at this GPS frame's pose
    /// (no one-frame lag).
    /// </summary>
    public event Action<long>? PoseEstimatorUpdated;

    // ── Re-entrancy guard ───────────────────────────────────────────────
    private int _processing; // 0 = idle, 1 = processing

    // ── Subscription tracking ───────────────────────────────────────────
    private bool _isStarted;

    // ── Operational state (written from UI thread, read from background) ─
    private readonly object _stateLock = new();

    private bool _autoSteerEngaged;
    private Models.Track.Track? _activeTrack;
    private bool _isTrackOnBoundary;
    // Phase D D3: passNumber / nudgeOffset live on _guidanceWorking as the single
    // source of truth. The separate _passNumber / _nudgeOffset fields used to be
    // pushed here by SetActiveTrack; that path now writes directly to the
    // working state (still UI-thread under lock — retires fully in D4/D5/D6
    // when snap / nudge / set-active-track all become intents).

    private bool _youTurnEnabled;
    private int _uTurnSkipRows;
    private bool _isSkipWorkedMode;
    private double _headlandCalculatedWidth;
    private double _headlandDistanceConfig;
    private List<Vec3>? _headlandLine;
    private Boundary? _boundary;
    private double _driftE;
    private double _driftN;
    private bool _hasActiveField;

    // One-shot latch so the field-far warning fires once per loaded field.
    // SetHasActiveField clears it on the false->true transition (new field
    // opened) so the next field gets a fresh chance to warn.
    private bool _farFromFieldWarned;

    // ── Pipeline-owned guidance state (only touched on background thread) ─
    private Models.Track.TrackGuidanceState? _trackGuidanceState;
    private double _simulatorSteerAngle;

    // Phase E: cycle-local cache of a LocalPlane auto-created from the first
    // GPS fix. The cycle uses this for coord conversion in the same tick it
    // emits it on GpsCycleResult.FirstFixLocalPlane; ApplyGpsCycleResult then
    // commits it to _appState.Field.LocalPlane on the UI thread. Once the UI
    // commit catches up (next cycle we see _appState.Field.LocalPlane match
    // _cycleLocalPlane), we drop our reference.
    private LocalPlane? _cycleLocalPlane;

    // ── YouTurn + Guidance working state (Phase C) ──────────────────────
    // POCOs mirroring State.YouTurn and the subset of State.Guidance that
    // the YouTurn state machine reads/writes. Single-writer contract —
    // only mutated on the cycle worker thread, via state-machine Tick /
    // TriggerManual / ClearState. ApplyGpsCycleResult mirrors back to the
    // observable State.* on the UI thread.
    private readonly YouTurnWorkingState _youTurn = new();
    private readonly GuidanceWorkingState _guidanceWorking = new();

    // ── Headland proximity ──────────────────────────────────────────────
    private readonly HeadlandDetectionService _headlandDetector = new();

    // ── RTK quality tracking ────────────────────────────────────────────
    private int _previousFixQuality;

    // ── Curve limit warning throttling ──────────────────────────────────
    private DateTime _lastCurveLimitWarning = DateTime.MinValue;
    private double? _lastHeadlandDistance;
    private bool _lastHeadlandWarning;
    private int _lastWarnedPathsAway = int.MinValue;

    // ── Logging throttle ────────────────────────────────────────────────
    private int _cycleCounter;

    private readonly IPositionEstimator? _positionEstimator;

    public GpsPipelineService(
        IGpsService gpsService,
        IToolPositionService toolPositionService,
        ITrackGuidanceService trackGuidanceService,
        ISectionControlService sectionControlService,
        ICoverageMapService coverageMapService,
        IAutoSteerService autoSteerService,
        YouTurnGuidanceService youTurnGuidanceService,
        YouTurnStateMachine youTurnStateMachine,
        IAudioService audioService,
        IPipelineIntents intents,
        IGpsHeadingFusionService headingFusion,
        ILogger<GpsPipelineService> logger,
        ApplicationState appState,
        IPositionEstimator? positionEstimator = null)
    {
        _gpsService = gpsService;
        _toolPositionService = toolPositionService;
        _trackGuidanceService = trackGuidanceService;
        _sectionControlService = sectionControlService;
        _coverageMapService = coverageMapService;
        _autoSteerService = autoSteerService;
        _youTurnGuidanceService = youTurnGuidanceService;
        _youTurnStateMachine = youTurnStateMachine;
        _audioService = audioService;
        _intents = intents;
        _headingFusion = headingFusion;
        _logger = logger;
        _appState = appState;
        _positionEstimator = positionEstimator;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════════════════

    public void Start()
    {
        if (_isStarted) return;
        _isStarted = true;
        _gpsService.GpsDataUpdated += OnGpsDataUpdated;
        _logger.LogInformation("GpsPipelineService started");
    }

    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;
        _gpsService.GpsDataUpdated -= OnGpsDataUpdated;
        _logger.LogInformation("GpsPipelineService stopped");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Operational state setters (called from UI thread)
    // ══════════════════════════════════════════════════════════════════════

    public void SetAutoSteerEngaged(bool engaged)
    {
        lock (_stateLock)
        {
            _autoSteerEngaged = engaged;
            // Reset guidance state when disengaging so we do global search on next engage
            if (!engaged) _trackGuidanceState = null;
        }
    }

    public void SetActiveTrack(Models.Track.Track? track, int passNumber, double nudgeOffset, bool isOnBoundary)
    {
        lock (_stateLock)
        {
            _activeTrack = track;
            _guidanceWorking.HowManyPathsAway = passNumber;
            _guidanceWorking.NudgeOffset = nudgeOffset;
            _isTrackOnBoundary = isOnBoundary;
            // Reset guidance state when track changes so we do a global search
            _trackGuidanceState = null;
        }
    }

    public void SetYouTurnEnabled(bool enabled)
    {
        lock (_stateLock) _youTurnEnabled = enabled;
    }

    /// <summary>
    /// Push YouTurn configuration values (skip-rows, skip-worked mode,
    /// headland geometry). Phase C C4 adds this so the cycle worker's
    /// YouTurn tick can build its own TickContext without reaching into
    /// the MVM.
    /// </summary>
    public void SetYouTurnConfig(int uTurnSkipRows, bool isSkipWorkedMode, double headlandCalculatedWidth, double headlandDistance)
    {
        lock (_stateLock)
        {
            _uTurnSkipRows = uTurnSkipRows;
            _isSkipWorkedMode = isSkipWorkedMode;
            _headlandCalculatedWidth = headlandCalculatedWidth;
            _headlandDistanceConfig = headlandDistance;
        }
    }

    public void SetHeadlandLine(IReadOnlyList<Vec3>? headlandLine)
    {
        lock (_stateLock)
        {
            // Copy to our own list so caller can mutate theirs safely
            _headlandLine = headlandLine != null ? new List<Vec3>(headlandLine) : null;
        }
    }

    public void SetBoundary(Boundary? boundary)
    {
        lock (_stateLock) _boundary = boundary;
    }

    public void SetDriftCompensation(double driftE, double driftN)
    {
        lock (_stateLock) { _driftE = driftE; _driftN = driftN; }
    }

    public void SetHasActiveField(bool hasActiveField)
    {
        lock (_stateLock)
        {
            // Reset the one-shot warning latch on the false->true transition so
            // re-opening (or opening a different) field gets a fresh shot at
            // the warning.
            if (hasActiveField && !_hasActiveField)
                _farFromFieldWarned = false;
            _hasActiveField = hasActiveField;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Read-back properties
    // ══════════════════════════════════════════════════════════════════════

    public bool IsAutoSteerEngaged { get { lock (_stateLock) return _autoSteerEngaged; } }
    public double SimulatorSteerAngle => Volatile.Read(ref _simulatorSteerAngle);

    // ══════════════════════════════════════════════════════════════════════
    // GPS event handler
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When true, ProcessCycle runs synchronously on the calling thread instead
    /// of Task.Run. Eliminates async timing issues in tests: every GPS frame
    /// produces its result before the next frame is sent.
    /// </summary>
    public bool SynchronousMode { get; set; }

    private void OnGpsDataUpdated(object? sender, GpsData data)
    {
        if (SynchronousMode)
        {
            // Test mode: process inline, no back-pressure, no threading
            try { ProcessCycle(data); }
            catch (Exception ex) { _logger.LogError(ex, "GpsPipelineService.ProcessCycle failed"); }
            return;
        }

        // Production mode: Task.Run with single-cycle-in-flight back-pressure
        if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0)
            return;

        Task.Run(() =>
        {
            try
            {
                ProcessCycle(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GpsPipelineService.ProcessCycle failed");
            }
            finally
            {
                Volatile.Write(ref _processing, 0);
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    // Core cycle — runs on background thread
    // ══════════════════════════════════════════════════════════════════════

    private void ProcessCycle(GpsData data)
    {
        _cycleCounter++;
        var config = ConfigurationStore.Instance;

        // Stage 1: Drain intents — see Plans/threading_model.svg cycle worker lane.
        // Phase C consumes ManualYouTurn + ClearYouTurn here; Phase D extends
        // with Guidance writers (snap in D4, nudge in D5). Clear runs before
        // any state-machine call this cycle so a clear + manual pair resolves
        // cleanly (clear first, then manual triggers a fresh turn from the
        // empty state). Snap applies to HowManyPathsAway before the display-
        // track computation so this cycle's visuals reflect the new pass.
        var intents = _intents.Drain();
        if (intents.ClearYouTurn)
            YouTurnStateMachine.ClearState(_youTurn);
        if (intents.GuidanceSnap.HasValue)
        {
            // Phase D D4. IsHeadingSameWay is the cycle's view of whether the
            // tractor is aligned with the track direction. Snap "left" from
            // the driver's perspective means "decrement pass number when
            // aligned, increment when reversed" — matches AgOpenGPS.
            int delta = intents.GuidanceSnap.Value
                ? (_guidanceWorking.IsHeadingSameWay ? -1 : 1)
                : (_guidanceWorking.IsHeadingSameWay ? 1 : -1);
            _guidanceWorking.HowManyPathsAway += delta;
            _guidanceWorking.NudgeOffset = 0;
            _trackGuidanceState = null;
        }
        // Phase D D5. Nudge accumulates (multiple clicks between drains sum).
        // Heading-same-way flips the sign so "left" always means left from the
        // driver's seat regardless of track direction. Reset wins if both
        // arrive in the same tick.
        if (intents.GuidanceNudgeMeters != 0)
        {
            double adjusted = _guidanceWorking.IsHeadingSameWay
                ? intents.GuidanceNudgeMeters
                : -intents.GuidanceNudgeMeters;
            _guidanceWorking.NudgeOffset += adjusted;
            _trackGuidanceState = null;
        }
        if (intents.GuidanceResetNudge)
        {
            _guidanceWorking.NudgeOffset = 0;
            _trackGuidanceState = null;
        }

        // Stage 2: Fix-quality status. The validator labels the fix; it does not
        // abort the cycle. Pre-Phase-B, NmeaParserService ran the same checks and
        // still fired GpsDataUpdated on rejection (with IsValid = false) so the
        // pipeline kept ticking and the UI — including latency display, PGN
        // heartbeat to modules, and fix-quality indicator — stayed live. An
        // earlier version of this gate early-returned here, which froze the
        // latency display and stopped PGN output whenever a real hardware fix
        // fell below MinFixQuality (default 4 = RTK Fixed). The rejection reason
        // propagates into the final GpsCycleResult's StatusMessage; downstream
        // consumers decide what to do with a low-quality fix by checking
        // data.IsValid or result.FixQuality.
        string? fixRejectionReason = null;
        if (!GpsFixQualityValidator.IsAcceptable(
                data.FixQuality, data.Hdop, data.DifferentialAge, out fixRejectionReason))
        {
            data.IsValid = false;
        }

        // ── Snapshot operational state under lock ────────────────────────
        bool autoSteerEngaged;
        Models.Track.Track? track;
        int passNumber;
        double nudgeOffset;
        bool isTrackOnBoundary;
        bool youTurnEnabled;
        int uTurnSkipRows;
        bool isSkipWorkedMode;
        double headlandCalculatedWidth;
        double headlandDistanceConfig;
        List<Vec3>? headlandLine;
        Boundary? boundary;
        double driftE, driftN;
        bool hasActiveField;

        lock (_stateLock)
        {
            autoSteerEngaged = _autoSteerEngaged;
            track = _activeTrack;
            // Phase D D3: pass number / nudge offset live on _guidanceWorking as
            // the single source of truth. Still read under lock here because
            // SetActiveTrack (UI-thread) writes them under the same lock.
            passNumber = _guidanceWorking.HowManyPathsAway;
            nudgeOffset = _guidanceWorking.NudgeOffset;
            isTrackOnBoundary = _isTrackOnBoundary;
            youTurnEnabled = _youTurnEnabled;
            uTurnSkipRows = _uTurnSkipRows;
            isSkipWorkedMode = _isSkipWorkedMode;
            headlandCalculatedWidth = _headlandCalculatedWidth;
            headlandDistanceConfig = _headlandDistanceConfig;
            headlandLine = _headlandLine;
            boundary = _boundary;
            driftE = _driftE;
            driftN = _driftN;
            hasActiveField = _hasActiveField;
        }

        // YouTurn working state is cycle-owned — no cross-thread writers.
        // TriggerManual (manual U-turn) and ClearState (field close / track
        // deselect) run on the cycle thread via intents drained above.
        bool isYouTurnTriggered = _youTurn.IsTriggered;
        bool isInYouTurn = _youTurn.IsExecuting;
        List<Vec3>? youTurnPath = _youTurn.TurnPath;

        var pos = data.CurrentPosition;
        bool hasTrack = track != null && track.Points.Count >= 2;

        // ── (1) Coordinate conversion for real GPS path ─────────────────
        double posEasting = pos.Easting;
        double posNorthing = pos.Northing;

        // Phase E: if a UI-committed LocalPlane already exists (user opened a
        // field, or we auto-created one on a previous cycle and ApplyGpsCycleResult
        // has since committed it), drop our cycle-local cache — the observable
        // instance is now the authoritative one.
        var committedLocalPlane = _appState.Field.LocalPlane;
        if (_cycleLocalPlane != null && ReferenceEquals(committedLocalPlane, _cycleLocalPlane))
            _cycleLocalPlane = null;

        LocalPlane? firstFixLocalPlane = null;
        if (Math.Abs(posEasting) < 0.001 && Math.Abs(posNorthing) < 0.001
            && data.FixQuality > 0)
        {
            // Auto-create local plane from first GPS fix if none exists.
            // Cycle-local cache so we don't cross-thread-write an ObservableObject;
            // the UI thread commits it via ApplyGpsCycleResult.
            var localPlane = committedLocalPlane ?? _cycleLocalPlane;
            if (localPlane == null)
            {
                localPlane = new LocalPlane(
                    new Wgs84(pos.Latitude, pos.Longitude),
                    new SharedFieldProperties());
                _cycleLocalPlane = localPlane;
                firstFixLocalPlane = localPlane; // emit to UI this cycle
            }

            var geoCoord = localPlane.ConvertWgs84ToGeoCoord(
                new Wgs84(pos.Latitude, pos.Longitude));
            posEasting = geoCoord.Easting;
            posNorthing = geoCoord.Northing;
        }

        // Origin guard: if the live GPS source has drifted very far from the
        // current LocalPlane origin, either silently re-anchor (no field) or
        // surface a warning (field loaded). Skip when only the cycle-local
        // cache exists — that means we just bootstrapped the plane this tick.
        LocalPlane? replacementLocalPlane = null;
        double replacementDistanceKm = 0.0;
        FarFromFieldWarning? farFromFieldWarning = null;
        if (committedLocalPlane != null && data.FixQuality > 0)
        {
            var converted = committedLocalPlane.ConvertWgs84ToGeoCoord(
                new Wgs84(pos.Latitude, pos.Longitude));
            double distFromOrigin = Math.Sqrt(
                converted.Easting * converted.Easting +
                converted.Northing * converted.Northing);

            if (!hasActiveField && distFromOrigin > TempOriginReinitDistanceM)
            {
                var newPlane = new LocalPlane(
                    new Wgs84(pos.Latitude, pos.Longitude),
                    new SharedFieldProperties());
                _cycleLocalPlane = newPlane;
                replacementLocalPlane = newPlane;
                replacementDistanceKm = distFromOrigin / 1000.0;
                posEasting = 0;
                posNorthing = 0;
            }
            else if (hasActiveField
                     && distFromOrigin > FieldOriginWarnDistanceM
                     && !_farFromFieldWarned)
            {
                farFromFieldWarning = new FarFromFieldWarning(
                    distFromOrigin, pos.Latitude, pos.Longitude);
                _farFromFieldWarned = true;
            }
        }

        // Stage 3 (Phase B C2): Heading fusion. Replaces the raw NMEA heading
        // with the dual-antenna-aware / fix-to-fix / IMU-blended value.
        // Receives real local easting/northing — see TMP-009 in the parking lot.
        double fusedHeading = _headingFusion.FuseHeading(
            pos.Heading, data.ImuHeading, data.ImuValid,
            pos.Speed, posEasting, posNorthing);
        pos = pos with { Heading = fusedHeading };

        // ── (1b) Antenna-to-pivot transform in local coordinates ────────
        // Single source of truth for the antenna-to-pivot transform. Runs
        // here on local-plane (E, N) so the math is consistent regardless
        // of how the GPS data arrived (real NMEA, simulator, replay).
        AntennaToPivotTransform.Apply(
            ref posEasting,
            ref posNorthing,
            pos.Heading * Math.PI / 180.0,
            ConfigurationStore.Instance.Vehicle,
            data.ImuRoll);

        // ── (2) Apply drift compensation ────────────────────────────────
        double driftedEasting = posEasting + driftE;
        double driftedNorthing = posNorthing + driftN;
        double headingRad = pos.Heading * Math.PI / 180.0;

        // ── (2b) Publish canonical pose to the position estimator ───────
        // The estimator is the bridge between GPS arrivals (10 Hz) and
        // the host control loop (100 Hz). Readers — control loop,
        // renderer — pull dead-reckoned pose between GPS samples.
        if (_positionEstimator is not null)
        {
            double yawRateRadPerSec = data.ImuValid
                ? data.ImuYawRate * Math.PI / 180.0
                : 0.0;
            double rollRad = data.ImuValid
                ? data.ImuRoll * Math.PI / 180.0
                : 0.0;
            long ts = Clock.Current.GetTimestamp();
            _positionEstimator.UpdateFromGps(new PoseSnapshot(
                new Vec2(driftedEasting, driftedNorthing),
                headingRad,
                pos.Speed,
                yawRateRadPerSec,
                rollRad,
                ts));
            // Test hook: fire so a synchronous control-loop tick can advance
            // the section state machine before this cycle's GpsCycleResult
            // captures section bits. No-op in production (loop runs on its
            // own thread).
            PoseEstimatorUpdated?.Invoke(ts);
        }

        // ── Phase C C4/C6: YouTurn state machine on the cycle worker ───
        // Two entry points, both running here on the background thread
        // against the cycle-owned _youTurn / _guidanceWorking POCOs:
        //   • Manual trigger via intent (C6 — drained above).
        //   • Auto tick, gated on autosteer + track + youturn-enabled +
        //     valid headland (matches pre-C4 MVM guards).
        // Snapshots built later in the cycle; the VM mirrors them back
        // via ApplyGpsCycleResult on the UI thread.
        YouTurnEffects? youTurnTickEffects = null;
        bool hasValidHeadlandLine = headlandLine != null && headlandLine.Count >= 3;
        bool hasTickableTrack = track != null && track.Points.Count >= 2;

        var tickPosition = pos with { Easting = driftedEasting, Northing = driftedNorthing };
        var tickCtx = new YouTurnStateMachine.TickContext(
            tickPosition,
            track,
            boundary,
            headlandLine,
            uTurnSkipRows,
            isSkipWorkedMode,
            headlandCalculatedWidth,
            headlandDistanceConfig);

        // Manual trigger — runs even when the auto gate would fail (e.g., YouTurn
        // toggle off). TriggerManual enforces its own preconditions (autosteer +
        // track + no turn already in progress) and sets a status message otherwise.
        if (intents.ManualYouTurn.HasValue && hasTickableTrack)
        {
            // IsHeadingSameWay is computed fresh by the state machine each tick;
            // HowManyPathsAway and NudgeOffset are already authoritative on
            // _guidanceWorking (D3 — SetActiveTrack writes directly).
            youTurnTickEffects = _youTurnStateMachine.TriggerManual(
                intents.ManualYouTurn.Value, autoSteerEngaged,
                in tickCtx, _guidanceWorking, _youTurn);
        }

        if (autoSteerEngaged && hasTickableTrack
            && youTurnEnabled && hasValidHeadlandLine)
        {
            _youTurn.YouTurnCounter++;

            var autoEffects = _youTurnStateMachine.Tick(in tickCtx, _guidanceWorking, _youTurn);
            // If a manual trigger already set effects this cycle, keep those —
            // state-machine branches make the auto tick a no-op mid-turn anyway.
            youTurnTickEffects ??= autoEffects;
        }

        // Refresh the locals the downstream guidance branch reads — the tick
        // (or a drained intent above) may have updated them.
        isYouTurnTriggered = _youTurn.IsTriggered;
        isInYouTurn = _youTurn.IsExecuting;
        youTurnPath = _youTurn.TurnPath;

        // ── (3) Tool position ───────────────────────────────────────────
        _toolPositionService.Update(
            new Vec3(driftedEasting, driftedNorthing, headingRad),
            headingRad);

        var toolPos = _toolPositionService.ToolPosition;
        var hitchPos = _toolPositionService.HitchPosition;
        double toolHeading = _toolPositionService.ToolHeading;
        bool isToolReady = _toolPositionService.IsToolPositionReady;
        double toolWidth = config.ActualToolWidth;


        // Map positions are now sent via GpsCycleResult → ViewModel → MapRenderState.
        // The pipeline does NOT call _mapService directly.

        // ── (5) Boundary check — auto-disengage if outside ──────────────
        bool autoSteerDisengaged = false;
        string? disengageReason = null;

        // During U-turns the tractor may go slightly outside the headland but
        // must NEVER leave the outer field boundary. Only skip for on-boundary pass 0.
        bool skipBoundaryCheck = isTrackOnBoundary && passNumber == 0;
        if (autoSteerEngaged && !skipBoundaryCheck
            && !IsPointInsideBoundary(boundary, driftedEasting, driftedNorthing))
        {
            autoSteerEngaged = false;
            autoSteerDisengaged = true;
            disengageReason = "AutoSteer disengaged - outside boundary";
            lock (_stateLock) _autoSteerEngaged = false;
        }

        // ── (6) Guidance calculation ────────────────────────────────────
        double steerAngle = 0;
        double crossTrackError = 0;
        double goalE = 0, goalN = 0;
        bool hasGuidance = false;
        bool youTurnCompleted = false;
        string? statusMessage = null;
        Models.Track.Track? displayTrack = null;
        Models.Track.Track? baseTrack = null;

        // Always compute the display track when we have a track (for map visualization)
        if (hasTrack)
        {
            var config2 = ConfigurationStore.Instance;
            double widthMinusOverlap = config2.ActualToolWidth - config2.Tool.Overlap;
            double distAway = widthMinusOverlap * passNumber + nudgeOffset;

            if (Math.Abs(distAway) < 0.01)
            {
                displayTrack = track;
            }
            else
            {
                var offsetPoints = CurveProcessing.CreateOffsetCurve(track!.Points, distAway);
                displayTrack = new Models.Track.Track
                {
                    Name = $"{track.Name} (path {passNumber})",
                    Points = offsetPoints,
                    Type = track.Type,
                    IsVisible = true,
                    IsActive = true
                };
                baseTrack = track;
            }
        }

        if (autoSteerEngaged && hasTrack)
        {
            if (isYouTurnTriggered && youTurnPath != null && youTurnPath.Count > 0)
            {
                // YouTurn guidance — steer along turn path
                // Use drifted local coordinates (not pos which has raw E=0,N=0)
                var ytPos = pos with { Easting = driftedEasting, Northing = driftedNorthing };
                var ytResult = CalculateYouTurnGuidance(ytPos, youTurnPath);
                if (ytResult != null)
                {
                    steerAngle = ytResult.Value.steerAngle;
                    crossTrackError = ytResult.Value.xte;
                    goalE = ytResult.Value.goalE;
                    goalN = ytResult.Value.goalN;
                    youTurnCompleted = ytResult.Value.turnComplete;
                    hasGuidance = !youTurnCompleted;
                }
            }
            else
            {
                // Normal track guidance
                var guidanceResult = CalculateTrackGuidance(
                    pos, track!, passNumber, nudgeOffset, driftedEasting, driftedNorthing, headingRad,
                    isYouTurnTriggered);
                if (guidanceResult != null)
                {
                    steerAngle = guidanceResult.Value.steerAngle;
                    crossTrackError = guidanceResult.Value.xte;
                    goalE = guidanceResult.Value.goalE;
                    goalN = guidanceResult.Value.goalN;
                    hasGuidance = true;
                    statusMessage = guidanceResult.Value.statusMessage;
                }
            }

            if (hasGuidance)
            {
                Volatile.Write(ref _simulatorSteerAngle, steerAngle);
                _autoSteerService.UpdateGuidanceResults(steerAngle, crossTrackError);
            }
        }
        if (!autoSteerEngaged && hasTrack)
        {
            // Display-only: auto-detect nearest pass and update visualization.
            // Phase D D3: write the detected pass directly into the cycle's
            // working state. Previously this was emitted as
            // GpsCycleResult.NearestPassNumber and the UI thread wrote it back
            // through SyncGuidanceStateToPipeline — two writers fighting each
            // other and causing a per-cycle oscillation (see commit 57920e0).
            // One writer now — the cycle.
            //
            // #261: match AgOpen — project a *lookahead* point (pivot + heading * lookDist)
            // onto the reference line instead of the raw pivot. Produces the "track jumps
            // ahead of the tractor" behavior operators expect in free-drive.
            double lookDist = Math.Max(
                ConfigurationStore.Instance.ActualToolWidth * 0.5,
                pos.Speed * GuidanceLookAheadSeconds);
            double hRad = pos.Heading * Math.PI / 180.0;
            double lookE = driftedEasting + Math.Sin(hRad) * lookDist;
            double lookN = driftedNorthing + Math.Cos(hRad) * lookDist;
            var (nearestPass, nearestDisplayTrack) = UpdateDisplayTrack(
                pos, track!, passNumber, nudgeOffset,
                driftedEasting, driftedNorthing,
                lookE, lookN);
            if (nearestDisplayTrack != null)
            {
                displayTrack = nearestDisplayTrack;
                // Explicitly clear baseTrack when returning to pass 0 — otherwise a
                // stale baseTrack set by the earlier block (when incoming passNumber
                // was non-zero) stays pointing at the reference track, and the UI
                // renders baseTrack + displayTrack at the same position (overlap).
                baseTrack = nearestPass != 0 ? track : null;
            }
            _guidanceWorking.HowManyPathsAway = nearestPass;
            passNumber = nearestPass;
        }

        // Phase D D2: write cycle-local guidance outputs into _guidanceWorking
        // so BuildGuidanceSnapshot can read them uniformly. D3 will make the
        // working state the authoritative source (today the flat GpsCycleResult
        // fields are still populated from locals and consumed by ApplyResults).
        _guidanceWorking.SteerAngle = steerAngle;
        _guidanceWorking.CrossTrackError = crossTrackError;
        _guidanceWorking.GoalPoint = new Vec2(goalE, goalN);

        // ── (7) Section control + coverage painting ─────────────────────
        // SectionControlService.Update is now driven by the host control
        // loop at 100 Hz (#313 commit 5c) for sub-frame edge accuracy.
        // The pipeline only reads the latest section states here for its
        // PGN-build step below.
        var sectionStates = _sectionControlService.SectionStates;
        int numSections = _sectionControlService.NumSections;

        // ── (8b) Hydraulic lift state (PGN 239 input) ───────────────────
        // Phase B completion: this used to live on the UI-thread legacy
        // path (MainViewModel.UpdateToolPositionProperties). Moved here so
        // SetMachineState's three fields (section bits, U-turn state,
        // hyd-lift state) are all written by the cycle, before the PGN
        // build in (9) reads them.
        byte hydLiftState = ComputeHydLiftState(toolPos, pos.Speed, headlandLine);
        _autoSteerService.SetMachineState(
            _sectionControlService.GetSectionBits(),
            isInYouTurn,
            hydLiftState);

        // ── (9) AutoSteer pipeline (PGN/latency) ────────────────────────
        _autoSteerService.ProcessSimulatedPosition(
            pos.Latitude, pos.Longitude, pos.Altitude,
            pos.Heading, pos.Speed, data.FixQuality,
            data.SatellitesInUse, data.Hdop,
            driftedEasting, driftedNorthing);

        // ── (10) Headland proximity ─────────────────────────────────────
        double? headlandDist = null;
        bool headlandWarning = false;
        ComputeHeadlandProximity(headlandLine, _toolPositionService.ToolPivotPosition,
            out headlandDist, out headlandWarning);

        // Hold last valid distance so the HUD doesn't disappear in gaps
        // (e.g., between crossing the headland line and entering the U-turn arc)
        if (headlandDist != null)
        {
            _lastHeadlandDistance = headlandDist;
            _lastHeadlandWarning = headlandWarning;
        }
        else if (_lastHeadlandDistance != null && headlandLine != null)
        {
            headlandDist = _lastHeadlandDistance;
            headlandWarning = _lastHeadlandWarning;
        }

        // ── (11) RTK quality sounds ─────────────────────────────────────
        CheckRtkQualityChange(data.FixQuality);

        // ── (12) Build section state arrays for result ──────────────────
        bool[]? secStatesArr = null;
        int[]? secColorCodes = null;
        if (numSections > 0)
        {
            secStatesArr = new bool[numSections];
            secColorCodes = new int[numSections];
            for (int i = 0; i < numSections; i++)
            {
                secStatesArr[i] = sectionStates[i].IsOn;
                secColorCodes[i] = GetSectionColorCode(sectionStates[i]);
            }
        }

        // ── (13) Build immutable result ─────────────────────────────────
        var result = new GpsCycleResult
        {
            // GPS position
            Latitude = pos.Latitude,
            Longitude = pos.Longitude,
            Altitude = pos.Altitude,
            Easting = driftedEasting,
            Northing = driftedNorthing,
            Heading = pos.Heading,
            Speed = pos.Speed,
            RollDegrees = data.ImuRoll,
            SatelliteCount = data.SatellitesInUse,
            Hdop = data.Hdop,
            DifferentialAge = data.DifferentialAge,
            FixQuality = data.FixQuality,
            GpsValid = data.IsValid,

            // Tool position
            ToolEasting = toolPos.Easting,
            ToolNorthing = toolPos.Northing,
            ToolHeadingRadians = toolHeading,
            ToolWidth = toolWidth,
            HitchEasting = hitchPos.Easting,
            HitchNorthing = hitchPos.Northing,
            IsToolPositionReady = isToolReady,

            // Autosteer
            IsAutoSteerEngaged = autoSteerEngaged,
            AutoSteerDisengagedThisCycle = autoSteerDisengaged,
            DisengageReason = disengageReason,

            // Per-cycle snapshots for UI-thread mirror via ApplyGpsCycleResult.
            // JustCompleted on the YouTurn snapshot carries the one-cycle turn
            // completion signal previously on the flat YouTurnCompleted field.
            // Guidance snapshot emits every cycle — Phase D D3 made the cycle
            // the sole writer of _guidanceWorking.HowManyPathsAway, so the
            // snapshot can no longer fight an opposing UI-thread writer.
            YouTurn = BuildYouTurnSnapshot(
                _youTurn,
                justCompleted: youTurnCompleted || (youTurnTickEffects?.TurnCompleted ?? false)),
            Guidance = BuildGuidanceSnapshot(_guidanceWorking, displayTrack, baseTrack, hasGuidance),

            // Sections
            SectionStates = secStatesArr,
            SectionColorCodes = secColorCodes,

            // Headland proximity
            HeadlandProximityDistance = headlandDist,
            HeadlandProximityWarning = headlandWarning,

            // Phase E: first-fix LocalPlane auto-create — non-null only on
            // the single cycle where the cycle bootstrapped the plane.
            FirstFixLocalPlane = firstFixLocalPlane,

            // Origin guard: replacement plane (silent re-anchor when no field
            // is loaded and the GPS source jumped > TempOriginReinitDistanceM)
            // or a far-from-field warning (field loaded, > FieldOriginWarnDistanceM).
            ReplacementLocalPlane = replacementLocalPlane,
            ReplacementDistanceKm = replacementDistanceKm,
            FarFromFieldWarning = farFromFieldWarning,

            // Status
            StatusMessage = statusMessage ?? youTurnTickEffects?.StatusMessage ?? fixRejectionReason
        };

        CycleCompleted?.Invoke(result);
    }

    private static YouTurnSnapshot BuildYouTurnSnapshot(YouTurnWorkingState src, bool justCompleted) => new()
    {
        IsEnabled = src.IsEnabled,
        IsTriggered = src.IsTriggered,
        IsExecuting = src.IsExecuting,
        TurnPath = src.TurnPath,
        PathIndex = src.PathIndex,
        IsTurnLeft = src.IsTurnLeft,
        LastTurnWasLeft = src.LastTurnWasLeft,
        DistanceToHeadland = src.DistanceToHeadland,
        DistanceToTrigger = src.DistanceToTrigger,
        NextTrack = src.NextTrack,
        LastCompletionPosition = src.LastCompletionPosition,
        HasCompletedFirstTurn = src.HasCompletedFirstTurn,
        YouTurnCounter = src.YouTurnCounter,
        WasHeadingSameWayAtTurnStart = src.WasHeadingSameWayAtTurnStart,
        NextTrackTurnOffset = src.NextTrackTurnOffset,
        ReturnPassTargetPath = src.ReturnPassTargetPath,
        SnakeSequence = src.SnakeSequence,
        SnakeIndex = src.SnakeIndex,
        CurrentZone = src.CurrentZone,
        JustCompleted = justCompleted,
    };

    private static GuidanceSnapshot BuildGuidanceSnapshot(
        GuidanceWorkingState src,
        Models.Track.Track? displayTrack,
        Models.Track.Track? baseTrack,
        bool hasGuidance) => new()
    {
        // GuidanceState-mirrored fields (all populated from the cycle's
        // working state — Phase D D2 writes them there at end-of-branch).
        ActiveTrack = src.ActiveTrack,
        IsGuidanceActive = src.IsGuidanceActive,
        CrossTrackError = src.CrossTrackError,
        HeadingError = src.HeadingError,
        SteerAngle = src.SteerAngle,
        SteerAngleRaw = src.SteerAngleRaw,
        DistanceOffRaw = src.DistanceOffRaw,
        PpIntegral = src.PpIntegral,
        PpPivotDistanceError = src.PpPivotDistanceError,
        PpPivotDistanceErrorLast = src.PpPivotDistanceErrorLast,
        PpCounter = src.PpCounter,
        GoalPoint = src.GoalPoint,
        RadiusPoint = src.RadiusPoint,
        PurePursuitRadius = src.PurePursuitRadius,
        IsHeadingSameWay = src.IsHeadingSameWay,
        IsReverse = src.IsReverse,
        HowManyPathsAway = src.HowManyPathsAway,
        NudgeOffset = src.NudgeOffset,
        CurrentLineLabel = src.CurrentLineLabel,
        IsContourMode = src.IsContourMode,

        // Cycle-only fields (not mirrored on GuidanceState) — passed in
        // from ProcessCycle's local computations.
        HasGuidance = hasGuidance,
        DisplayTrack = displayTrack,
        BaseTrack = baseTrack,
    };

    // ══════════════════════════════════════════════════════════════════════
    // Guidance helpers (run on background thread, no Avalonia types)
    // ══════════════════════════════════════════════════════════════════════

    private (double steerAngle, double xte, double goalE, double goalN, string? statusMessage)?
        CalculateTrackGuidance(
            Position currentPosition, Models.Track.Track track, int passNumber, double nudgeOffset,
            double driftedEasting, double driftedNorthing, double headingRad,
            bool isYouTurnTriggered)
    {
        var config = ConfigurationStore.Instance;

        // Calculate dynamic look-ahead
        double speedKmh = currentPosition.Speed * 3.6;
        double lookAhead = config.Guidance.GoalPointLookAheadHold;
        if (speedKmh > 1)
        {
            lookAhead = Math.Max(
                config.Guidance.MinLookAheadDistance,
                config.Guidance.GoalPointLookAheadHold + (speedKmh * config.Guidance.GoalPointLookAheadMult * 0.1));
        }

        // Steer axle position
        double steerE = driftedEasting + Math.Sin(headingRad) * config.Vehicle.Wheelbase;
        double steerN = driftedNorthing + Math.Cos(headingRad) * config.Vehicle.Wheelbase;

        // Calculate parallel offset
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        double distAway = widthMinusOverlap * passNumber + nudgeOffset;

        if (_cycleCounter % 50 == 0)
        {
            Console.WriteLine($"[Guidance] pass={passNumber} distAway={distAway:F1} pivot=({driftedEasting:F1},{driftedNorthing:F1}) h={headingRad*180/Math.PI:F1}");
        }

        Models.Track.Track currentTrack;
        string? statusMessage = null;

        if (Math.Abs(distAway) < 0.01)
        {
            currentTrack = track;
        }
        else
        {
            var (offsetPoints, percentRemoved) = CurveProcessing.CreateOffsetCurveWithInfo(track.Points, distAway);

            // Warn on tight curves
            if (percentRemoved > 10 && passNumber != _lastWarnedPathsAway)
            {
                if ((DateTime.Now - _lastCurveLimitWarning).TotalSeconds > 10)
                {
                    _lastCurveLimitWarning = DateTime.Now;
                    _lastWarnedPathsAway = passNumber;

                    double minRadius = CurveProcessing.CalculateMinRadiusOfCurvature(track.Points);
                    int maxPasses = CurveProcessing.CalculateMaxInwardPasses(track.Points, widthMinusOverlap);
                    statusMessage = $"Curve too tight! {percentRemoved:F0}% removed. Max ~{maxPasses} inward passes (min radius: {minRadius:F1}m)";
                }
            }

            currentTrack = new Models.Track.Track
            {
                Name = $"{track.Name} (path {passNumber})",
                Points = offsetPoints,
                Type = track.Type,
                IsVisible = true,
                IsActive = true
            };
        }

        if (_cycleCounter % 50 == 0 && currentTrack.Points.Count >= 2)
        {
            var p0 = currentTrack.Points[0];
            var pN = currentTrack.Points[^1];
            Console.WriteLine($"[Guidance] track: ({p0.Easting:F1},{p0.Northing:F1})->({pN.Easting:F1},{pN.Northing:F1})");
        }

        // Calculate heading alignment using the offset track we're actually following
        double trackHeading = FindNearestSegmentHeading(currentTrack.Points, driftedEasting, driftedNorthing);
        double headingDiff = headingRad - trackHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        bool isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Build guidance input
        var input = new Models.Track.TrackGuidanceInput
        {
            Track = currentTrack,
            PivotPosition = new Vec3(driftedEasting, driftedNorthing, headingRad),
            SteerPosition = new Vec3(steerE, steerN, headingRad),
            UseStanley = false,
            IsHeadingSameWay = isHeadingSameWay,
            Wheelbase = config.Vehicle.Wheelbase,
            MaxSteerAngle = config.Vehicle.MaxSteerAngle,
            GoalPointDistance = lookAhead,
            SideHillCompFactor = 0,
            PurePursuitIntegralGain = config.Guidance.PurePursuitIntegralGain,
            FixHeading = headingRad,
            AvgSpeed = speedKmh,
            IsReverse = false,
            IsAutoSteerOn = true,
            IsYouTurnTriggered = isYouTurnTriggered,
            ImuRoll = 88888,
            PreviousState = _trackGuidanceState,
            FindGlobalNearest = _trackGuidanceState == null,
            CurrentLocationIndex = _trackGuidanceState?.CurrentLocationIndex ?? 0
        };

        var output = _trackGuidanceService.CalculateGuidance(input);

        // Store state for next iteration
        _trackGuidanceState = output.State;
        if (_trackGuidanceState != null)
            _trackGuidanceState.CurrentLocationIndex = output.CurrentLocationIndex;

        return (output.SteerAngle, output.CrossTrackError, output.GoalPoint.Easting, output.GoalPoint.Northing,
                statusMessage);
    }

    /// <summary>
    /// Display-only track update: finds nearest pass and updates map, without steering.
    /// Returns the detected nearest pass number and the offset display track.
    /// </summary>
    private (int nearestPass, Models.Track.Track? displayTrack) UpdateDisplayTrack(
        Position pos, Models.Track.Track track, int passNumber, double nudgeOffset,
        double pivotEasting, double pivotNorthing,
        double sampleEasting, double sampleNorthing)
    {
        var config = ConfigurationStore.Instance;
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        if (widthMinusOverlap < 0.1) widthMinusOverlap = 1.0;

        // Nearest-pass selection uses the *lookahead* sample (hysteresis: line jumps
        // ahead of the tractor in free-drive). XTE display uses the *pivot* (actual
        // cross-track error from the tractor position). See #261.
        double sampleDist = CalculatePerpendicularDistance(track, sampleEasting, sampleNorthing);

        // Match AgOpen CABLine.BuildCurrentABLineList — subtract the accumulated nudge
        // before rounding to the nearest pass. Without this, nudging the line perpendicular
        // can cause the auto-select to fight the nudge each cycle.
        double refDist = (sampleDist - nudgeOffset) / widthMinusOverlap;
        int nearestPass = refDist < 0 ? (int)(refDist - 0.5) : (int)(refDist + 0.5);
        double distAway = widthMinusOverlap * nearestPass + nudgeOffset;

        Models.Track.Track? resultTrack = null;
        if (Math.Abs(distAway) < 0.01)
        {
            // On the base track — show reference directly
            resultTrack = track;
        }
        else
        {
            var (offsetPoints, _) = CurveProcessing.CreateOffsetCurveWithInfo(track.Points, distAway);
            resultTrack = new Models.Track.Track
            {
                Name = $"{track.Name} (pass {nearestPass})",
                Points = offsetPoints,
                Type = track.Type,
                IsVisible = true,
                IsActive = true
            };
        }

        // Update XTE display — actual pivot distance to the selected pass line.
        double pivotPerp = CalculatePerpendicularDistance(track, pivotEasting, pivotNorthing);
        double xte = pivotPerp - distAway;
        if (track.Points.Count >= 2)
        {
            var a = track.Points[0];
            var b = track.Points[track.Points.Count - 1];
            double trackHeading = Math.Atan2(b.Easting - a.Easting, b.Northing - a.Northing);
            double vehicleHeading = pos.Heading * Math.PI / 180.0;
            double headingDiff = Math.Abs(vehicleHeading - trackHeading);
            if (headingDiff > Math.PI) headingDiff = 2 * Math.PI - headingDiff;
            if (headingDiff > Math.PI / 2) xte = -xte;
        }

        _autoSteerService.UpdateGuidanceResults(0, xte);
        return (nearestPass, resultTrack);
    }

    /// <summary>
    /// Calculate YouTurn path-following guidance. Returns null if no path.
    /// </summary>
    private (double steerAngle, double xte, double goalE, double goalN, bool turnComplete)?
        CalculateYouTurnGuidance(Position currentPosition, List<Vec3> turnPath)
    {
        if (turnPath.Count == 0) return null;

        var config = ConfigurationStore.Instance;
        double headingRad = currentPosition.Heading * Math.PI / 180.0;
        double speedKmh = currentPosition.Speed * 3.6;

        var input = new YouTurnGuidanceInput
        {
            TurnPath = turnPath,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRad),
            SteerPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRad),
            Wheelbase = config.Vehicle.Wheelbase,
            MaxSteerAngle = config.Vehicle.MaxSteerAngle,
            UseStanley = false,
            GoalPointDistance = config.Guidance.GoalPointLookAheadHold,
            UTurnCompensation = config.Guidance.UTurnCompensation,
            FixHeading = headingRad,
            AvgSpeed = speedKmh,
            IsReverse = false,
            UTurnStyle = 0
        };

        var output = _youTurnGuidanceService.CalculateGuidance(input);

        if (output.IsTurnComplete)
            return (0, 0, 0, 0, true);

        return (output.SteerAngle, output.DistanceFromCurrentLine,
            output.GoalPoint.Easting, output.GoalPoint.Northing, false);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Geometry helpers (pure math, no UI)
    // ══════════════════════════════════════════════════════════════════════

    private static bool IsPointInsideBoundary(Boundary? boundary, double easting, double northing)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
            return true;

        var points = boundary.OuterBoundary.Points;
        int n = points.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = points[i];
            var pj = points[j];

            if (((pi.Northing > northing) != (pj.Northing > northing)) &&
                (easting < (pj.Easting - pi.Easting) * (northing - pi.Northing) / (pj.Northing - pi.Northing) + pi.Easting))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static double FindNearestSegmentHeading(List<Vec3> points, double easting, double northing)
    {
        if (points.Count < 2) return 0;
        if (points.Count == 2) return points[0].Heading;

        double minDist = double.MaxValue;
        int nearestIdx = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];

            double segDx = p2.Easting - p1.Easting;
            double segDy = p2.Northing - p1.Northing;
            double segLenSq = segDx * segDx + segDy * segDy;

            double dist;
            if (segLenSq < 0.0001)
            {
                dist = Math.Sqrt((easting - p1.Easting) * (easting - p1.Easting) +
                                 (northing - p1.Northing) * (northing - p1.Northing));
            }
            else
            {
                double t = Math.Clamp(
                    ((easting - p1.Easting) * segDx + (northing - p1.Northing) * segDy) / segLenSq,
                    0, 1);
                double projE = p1.Easting + t * segDx;
                double projN = p1.Northing + t * segDy;
                dist = Math.Sqrt((easting - projE) * (easting - projE) +
                                 (northing - projN) * (northing - projN));
            }

            if (dist < minDist)
            {
                minDist = dist;
                nearestIdx = i;
            }
        }

        return points[nearestIdx].Heading;
    }

    private static double CalculatePerpendicularDistance(Models.Track.Track track, double easting, double northing)
    {
        if (track.Points.Count == 2)
        {
            var a = track.Points[0];
            var b = track.Points[1];
            double dx = b.Easting - a.Easting;
            double dy = b.Northing - a.Northing;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01) return 0;
            return ((easting - a.Easting) * dy - (northing - a.Northing) * dx) / len;
        }
        else
        {
            double minDist = double.MaxValue;
            double signedDist = 0;
            for (int i = 0; i < track.Points.Count - 1; i++)
            {
                var a = track.Points[i];
                var b = track.Points[i + 1];
                double dx = b.Easting - a.Easting;
                double dy = b.Northing - a.Northing;
                double segLen = Math.Sqrt(dx * dx + dy * dy);
                if (segLen < 0.01) continue;
                double t = Math.Clamp(
                    ((easting - a.Easting) * dx + (northing - a.Northing) * dy) / (segLen * segLen),
                    0, 1);
                double projE = a.Easting + t * dx;
                double projN = a.Northing + t * dy;
                double dist = Math.Sqrt((easting - projE) * (easting - projE) + (northing - projN) * (northing - projN));
                if (dist < minDist)
                {
                    minDist = dist;
                    signedDist = ((easting - a.Easting) * dy - (northing - a.Northing) * dx) / segLen;
                }
            }
            return signedDist;
        }
    }

    private void ComputeHeadlandProximity(
        List<Vec3>? headlandLine, Vec3 toolPivot,
        out double? distance, out bool warning)
    {
        distance = null;
        warning = false;

        if (headlandLine == null || headlandLine.Count < 3)
            return;

        var input = new Models.Headland.HeadlandDetectionInput
        {
            IsHeadlandOn = true,
            VehiclePosition = toolPivot,
            Boundaries = new List<Models.Headland.BoundaryData>
            {
                new Models.Headland.BoundaryData
                {
                    HeadlandLine = new List<Vec3>(headlandLine)
                }
            }
        };

        var output = _headlandDetector.DetectHeadland(input);
        distance = output.HeadlandDistance;
        warning = output.ShouldTriggerWarning;
    }

    /// <summary>
    /// Compute PGN 239 hydraulic-lift state. Migrated from
    /// MainViewModel.CalculateHydLiftState (Phase B completion). Reads
    /// State.Field for the boundary/headland — read-only from cycle is
    /// §0-clean.
    ///
    /// Returns: 0 = off, 1 = lower (in cultivated area), 2 = raise (in headland zone).
    /// </summary>
    private byte ComputeHydLiftState(Vec3 toolPosition, double speed, List<Vec3>? headlandLine)
    {
        var machine = ConfigurationStore.Instance.Machine;
        if (!machine.HydraulicLiftEnabled) return 0;

        // Don't operate at very low speed or in reverse
        if (speed < 0.2 || speed < -0.1) return 0;

        if (headlandLine == null || headlandLine.Count < 3) return 0;

        var boundary = _appState.Field.CurrentBoundary;
        if (boundary == null || !boundary.IsValid) return 0;

        bool inBoundary = boundary.IsPointInside(toolPosition.Easting, toolPosition.Northing);
        if (!inBoundary) return 0;

        bool inCultivatedArea = Models.Base.GeometryMath.IsPointInPolygon(
            headlandLine, new Vec2(toolPosition.Easting, toolPosition.Northing));

        return inCultivatedArea ? (byte)1 : (byte)2;
    }

    private void CheckRtkQualityChange(int fixQuality)
    {
        if (fixQuality != _previousFixQuality)
        {
            bool wasRtk = _previousFixQuality >= 4;
            bool isRtk = fixQuality >= 4;
            if (wasRtk && !isRtk)
                _audioService.Play(SoundEffect.RtkLost);
            else if (!wasRtk && isRtk)
                _audioService.Play(SoundEffect.RtkRecovered);
            _previousFixQuality = fixQuality;
        }
    }

    /// <summary>
    /// Section display color codes matching legacy AgOpenGPS states:
    /// 0 = Off (red), 1 = Manual ON (yellow), 2 = Auto ON (green),
    /// 3 = Turning OFF (on but requested off - cyan), 4 = Turning ON (off but requested on - orange)
    /// </summary>
    private static int GetSectionColorCode(SectionControlState state)
    {
        // Manual override states
        if (state.ButtonState == SectionButtonState.Off)
            return 0; // Off (red)
        if (state.ButtonState == SectionButtonState.On)
            return 1; // Manual ON (yellow)

        // Auto mode transition states
        if (state.IsOn && state.SectionOffRequest)
            return 3; // Turning OFF: valve open but shutting down (cyan)
        if (!state.IsOn && state.SectionOnRequest)
            return 4; // Turning ON: valve closed but opening (orange)

        // Auto mode steady states
        if (state.IsOn)
            return 2; // Auto ON (green)

        return 5; // Auto OFF (gray)
    }
}
