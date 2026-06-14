// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Linq;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// Handler for <see cref="Services.Interfaces.IGpsPipelineService.CycleCompleted"/>.
    /// Marshals the result from the background thread to the UI thread for property updates.
    /// </summary>
    private void OnGpsCycleCompleted(GpsCycleResult result)
    {
        _dispatcher.Post(() => ApplyGpsCycleResult(result));
    }

    /// <summary>
    /// Apply a GPS cycle result snapshot to all bound ViewModel properties.
    /// Called on the UI thread after the service pipeline computes results on a background thread.
    /// This is the ONLY place where GPS-derived properties are set during normal operation.
    /// </summary>
    public void ApplyGpsCycleResult(GpsCycleResult result)
    {
        // PERF-05 Phase 2a. Cycle = one ApplyGpsCycleResult invocation on
        // the UI thread (one per GpsCycleCompleted dispatch from the
        // background pipeline). Captures everything from GpsDataRecorder
        // through the final SetVehicleSteerAngle — all of the UI-thread
        // state mirror + property change + binding-triggering work.
        // Suspected source of the iPad "+13 ms outside OnRender" cost.
        bool perfAgc = AgValoniaGPS.Models.Diagnostics.DiagFlags.PerfApplyGpsCycle;
        long perfAgcT0 = perfAgc ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        long perfAgcA0 = perfAgc ? GC.GetAllocatedBytesForCurrentThread() : 0;

        // Record for debug dump (ring buffer, last 60 seconds at 10Hz)
        AgValoniaGPS.Services.Logging.GpsDataRecorder.Instance.Record(result);

        // Mark GPS as received (updates timeout tracking for connection status)
        if (result.GpsValid)
            _gpsService.MarkGpsReceived();

        // PERF-05 Phase 2c #1: stop driving display-property PropertyChanged
        // from sensor arrival. The MainViewModel.{Latitude, Longitude, Easting,
        // Northing, Heading, _speed/SpeedKmh, RollDegrees, FixQuality} setters
        // that used to live here have moved to OnDisplayTick (10 Hz, decoupled
        // from sensor arrival). The cycle still writes State.Vehicle below —
        // that's the canonical system of record the display tick samples from.
        // For values not yet on State.Vehicle (RollDegrees), cache here.
        _latestRollDegrees = result.RollDegrees;

        // Sole writer to State.Vehicle — Phase B completion. Was previously
        // also written from MainViewModel.HandleGpsUiUpdates on the
        // _gpsService.GpsDataUpdated callback path (Rule 2 violation).
        State.Vehicle.UpdateFromGps(
            new AgValoniaGPS.Models.Position
            {
                Latitude = result.Latitude,
                Longitude = result.Longitude,
                Altitude = result.Altitude,
                Easting = result.Easting,
                Northing = result.Northing,
                Heading = result.Heading,
                Speed = result.Speed
            },
            result.FixQuality,
            result.SatelliteCount,
            result.Hdop,
            result.DifferentialAge);

        // Tool position — set ToolEasting LAST to trigger map update
        ToolNorthing = result.ToolNorthing;
        ToolHeadingRadians = result.ToolHeadingRadians;
        ToolWidth = result.ToolWidth;
        HitchEasting = result.HitchEasting;
        HitchNorthing = result.HitchNorthing;
        // IsToolPositionReady is a computed property from _toolPositionService — just notify
        OnPropertyChanged(nameof(IsToolPositionReady));
        ToolEasting = result.ToolEasting;

        // Phase D D7: guidance outputs come from GuidanceSnapshot (mirror block
        // below). The flat SteerAngle / CrossTrackError / GoalPoint* / HasGuidance /
        // DisplayTrack / BaseTrack fields stay populated on GpsCycleResult until D8
        // deletes them, but this method no longer reads them.

        // Autosteer state
        if (result.AutoSteerDisengagedThisCycle)
        {
            IsAutoSteerEngaged = false;
            _autoSteerService.Disengage();
            _audioService.Play(Services.Interfaces.SoundEffect.AutoSteerOff);
            StatusMessage = result.DisengageReason ?? "AutoSteer disengaged";
        }

        // Phase C C5: YouTurn + Guidance snapshot mirror. The cycle worker
        // runs the YouTurn state machine on its own POCO working state; this
        // is the single UI-thread point where those writes become PropertyChanged
        // events on State.YouTurn / State.Guidance, and where map-service side
        // effects (turn path, next track, in-youturn flag) land.
        if (result.YouTurn is { } yt)
        {
            var sy = State.YouTurn;
            sy.IsEnabled = yt.IsEnabled;
            sy.IsTriggered = yt.IsTriggered;
            sy.IsExecuting = yt.IsExecuting;
            // TurnPath / SnakeSequence: reference equality elides PropertyChanged
            // when the cycle reuses the list across cycles (list ref replaced
            // only on turn start/end) — see TMP-001 resolution.
            sy.TurnPath = yt.TurnPath is List<AgValoniaGPS.Models.Base.Vec3> tp ? tp : yt.TurnPath?.ToList();
            sy.PathIndex = yt.PathIndex;
            sy.IsTurnLeft = yt.IsTurnLeft;
            sy.LastTurnWasLeft = yt.LastTurnWasLeft;
            sy.DistanceToHeadland = yt.DistanceToHeadland;
            sy.DistanceToTrigger = yt.DistanceToTrigger;
            sy.NextTrack = yt.NextTrack;
            sy.LastCompletionPosition = yt.LastCompletionPosition;
            sy.HasCompletedFirstTurn = yt.HasCompletedFirstTurn;
            sy.YouTurnCounter = yt.YouTurnCounter;
            sy.WasHeadingSameWayAtTurnStart = yt.WasHeadingSameWayAtTurnStart;
            sy.NextTrackTurnOffset = yt.NextTrackTurnOffset;
            sy.ReturnPassTargetPath = yt.ReturnPassTargetPath;
            sy.SnakeSequence = yt.SnakeSequence is List<int> ss ? ss : yt.SnakeSequence?.ToList();
            sy.SnakeIndex = yt.SnakeIndex;
            sy.CurrentZone = yt.CurrentZone;

            _mapService.SetYouTurnPath(yt.TurnPath?.Select(p => (p.Easting, p.Northing)).ToList());
            // Reference-gate to match DisplayTrack/BaseTrack pattern below — yt.NextTrack
            // is usually null and reused across cycles, so an unconditional push would
            // re-dirty the GL track VBO every cycle and force a per-frame rebuild.
            if (!ReferenceEquals(_lastMirroredNextTrack, yt.NextTrack))
            {
                _lastMirroredNextTrack = yt.NextTrack;
                _mapService.SetNextTrack(yt.NextTrack);
            }
            _mapService.SetIsInYouTurn(yt.IsExecuting);

            // One-shot direction override: the state machine consumes
            // and clears it inside the cycle, so once the snapshot
            // reports null the UI cache must follow. Without this the
            // UI's stale value would be re-written into the working
            // state by GpsPipelineService.ProcessCycle every tick and
            // bias every subsequent auto-armed turn with the operator's
            // already-consumed intent.
            if (!yt.NextUTurnDirectionLeftOverride.HasValue
                && NextUTurnDirectionLeftOverride.HasValue)
            {
                NextUTurnDirectionLeftOverride = null;
            }
        }

        if (result.Guidance is { } g)
        {
            // Phase D D7: full GuidanceSnapshot mirror. The cycle is the
            // sole writer of these working-state fields; this block is the
            // single UI-thread point where they become PropertyChanged events
            // on State.Guidance.
            var sg = State.Guidance;
            sg.ActiveTrack = g.ActiveTrack;
            sg.IsGuidanceActive = g.IsGuidanceActive;
            sg.CrossTrackError = g.CrossTrackError;
            sg.HeadingError = g.HeadingError;
            sg.SteerAngle = g.SteerAngle;
            sg.SteerAngleRaw = g.SteerAngleRaw;
            sg.DistanceOffRaw = g.DistanceOffRaw;
            sg.PpIntegral = g.PpIntegral;
            sg.PpPivotDistanceError = g.PpPivotDistanceError;
            sg.PpPivotDistanceErrorLast = g.PpPivotDistanceErrorLast;
            sg.PpCounter = g.PpCounter;
            sg.GoalPoint = g.GoalPoint;
            sg.RadiusPoint = g.RadiusPoint;
            sg.PurePursuitRadius = g.PurePursuitRadius;
            sg.IsHeadingSameWay = g.IsHeadingSameWay;
            sg.IsReverse = g.IsReverse;
            sg.HowManyPathsAway = g.HowManyPathsAway;
            sg.NudgeOffset = g.NudgeOffset;
            sg.CurrentLineLabel = g.CurrentLineLabel;
            sg.IsContourMode = g.IsContourMode;

            // Map-service pushes with reference / value gating to avoid per-cycle
            // SendStateToHandler churn. The cycle reuses DisplayTrack / BaseTrack
            // references across cycles when they haven't changed, so ReferenceEquals
            // elides the call in steady state. Guidance points are a Vec2 (value
            // type) and change every cycle during active guidance — no gating
            // would help; the gate below is structural.
            //
            // SimulatorSteerAngle and CrossTrackError must ALSO be gated on
            // HasGuidance: SimulatorSteerAngle is the user's simulator steering
            // input, and if we unconditionally wrote g.SteerAngle (=0 when no
            // guidance) every cycle we'd overwrite the user's L/R keypress
            // before the simulator tick consumed it.
            if (g.HasGuidance)
            {
                SimulatorSteerAngle = g.SteerAngle;
                CrossTrackError = g.CrossTrackError * 100;
                _mapService.SetGuidancePoints(g.GoalPoint.Easting, g.GoalPoint.Northing, isActive: true);
            }
            if (!ReferenceEquals(_lastMirroredDisplayTrack, g.DisplayTrack))
            {
                _lastMirroredDisplayTrack = g.DisplayTrack;
                _mapService.SetActiveTrack(g.DisplayTrack);
            }
            if (!ReferenceEquals(_lastMirroredBaseTrack, g.BaseTrack))
            {
                _lastMirroredBaseTrack = g.BaseTrack;
                _mapService.SetBaseTrack(g.BaseTrack);
            }
        }

        // Turn-completion signal: YouTurn snapshot's JustCompleted is set on
        // the single cycle the state machine (or the YouTurn guidance branch)
        // reports turn-complete. Reset the TrackGuidanceState cache so the new
        // offset track is searched globally instead of resumed from the
        // pre-turn CurrentLocationIndex.
        if (result.YouTurn is { JustCompleted: true })
        {
            _trackGuidanceState = null;
            SyncGuidanceStateToPipeline();
        }

        // Headland proximity
        State.Field.HeadlandProximityDistance = result.HeadlandProximityDistance;
        State.Field.HeadlandProximityWarning = result.HeadlandProximityWarning;

        // Phase E E1: the cycle's first-fix LocalPlane auto-create arrives
        // here. The cycle already holds a cache of it for coord conversion;
        // we commit to the observable so subsequent UI readers see it. Only
        // first-wins — if the user explicitly opened a field in the meantime
        // (SetFieldOrigin), their instance stays.
        if (result.FirstFixLocalPlane != null && State.Field.LocalPlane == null)
        {
            State.Field.LocalPlane = result.FirstFixLocalPlane;
        }

        // Origin guard: live GPS jumped beyond the temp-origin threshold while
        // no field was loaded. Overwrite the existing observable plane (the
        // cycle has already swapped its own cache) and surface a status toast.
        if (result.ReplacementLocalPlane != null)
        {
            State.Field.LocalPlane = result.ReplacementLocalPlane;
            StatusMessage =
                $"GPS source moved {result.ReplacementDistanceKm:F0} km " +
                $"from local origin; origin re-anchored.";
        }

        // Origin guard: live GPS far from the loaded field. Drop autosteer
        // and prompt the operator for a close/keep-driving decision.
        if (result.FarFromFieldWarning is { } w)
        {
            HandleFarFromFieldWarning(w);
        }

        // Section states
        if (result.SectionStates != null)
        {
            UpdateSectionPropertiesFromResult(result.SectionStates, result.SectionColorCodes);

            // Push section layout/state to the renderer every cycle. The
            // platform views' per-property bridge only fires on
            // SectionXActive/ColorCode change, which doesn't happen at app
            // start when everything's at default — that left the tool drawn
            // as one undivided rectangle until the first auto-on flip.
            if (NumSections > 0)
            {
                _mapService.SetSectionStates(
                    GetSectionStates(),
                    GetSectionWidths(),
                    NumSections,
                    result.SectionColorCodes ?? GetSectionButtonStates());
            }

            // Skip-and-fill bookkeeping: any active section means the current
            // path is being worked. Was previously inside the legacy
            // UpdateCoveragePainting on the UI-thread tool-position path
            // (Phase B completion).
            if (SelectedTrack != null && result.SectionStates.Any(s => s))
            {
                SelectedTrack.MarkPathWorked(State.Guidance.HowManyPathsAway);
            }
        }

        // Status message (only if set — don't overwrite existing)
        if (result.StatusMessage != null)
            StatusMessage = result.StatusMessage;

        // Map vehicle/tool/hitch positions are pushed by OnRenderPullTick at
        // 30 Hz (vehicle dead-reckoned to "now" from the estimator, tool/hitch
        // from the live ToolPositionService snapshot updated at 100 Hz by the
        // control loop). The pipeline result captures stale values at
        // pipeline-run time; pushing them here as well caused the implement to
        // jitter back and forth at GPS rate as the stale write fought the
        // live render-pull write.

        // Live wheel angle for the front-wheel sprite (#336). Real WAS reading
        // when an autosteer module is attached, simulator slider value when
        // the sim is driving GPS. Both are in degrees and signed (+right).
        double steerDeg = _isSimulatorEnabled
            ? _simulatorService.SteerAngle
            : _autoSteerService.LastSteerData.ActualSteerAngle;
        _mapService.SetVehicleSteerAngle(steerDeg * Math.PI / 180.0);

        if (perfAgc)
        {
            _perfAgcTicks += System.Diagnostics.Stopwatch.GetTimestamp() - perfAgcT0;
            _perfAgcAllocs += GC.GetAllocatedBytesForCurrentThread() - perfAgcA0;
            _perfAgcCount++;
            var elapsed = (DateTime.UtcNow - _perfAgcWindowStart).TotalSeconds;
            if (elapsed >= 1.0 && _perfAgcCount > 0)
            {
                double ticksPerUs = System.Diagnostics.Stopwatch.Frequency / 1_000_000.0;
                Console.WriteLine(
                    $"[ApplyGpsCycle-PERF] cycles={_perfAgcCount}"
                    + $" us/cycle={(_perfAgcTicks / ticksPerUs / _perfAgcCount):F1}"
                    + $" alloc/cycle={(_perfAgcAllocs / _perfAgcCount)}B"
                    + $" total_us={(long)(_perfAgcTicks / ticksPerUs)}"
                    + $" total_alloc={_perfAgcAllocs}B"
                    + $" window={elapsed:F2}s");
                _perfAgcTicks = 0;
                _perfAgcAllocs = 0;
                _perfAgcCount = 0;
                _perfAgcWindowStart = DateTime.UtcNow;
            }
        }
    }

    // PERF-05 Phase 2a accumulators (gated by DiagFlags.PerfApplyGpsCycle).
    private long _perfAgcTicks;
    private long _perfAgcAllocs;
    private int _perfAgcCount;
    private DateTime _perfAgcWindowStart = DateTime.UtcNow;

    private void UpdateSectionPropertiesFromResult(bool[] states, int[]? colorCodes)
    {
        // The section bar binds to per-button ColorCode. The pipeline supplies
        // the authoritative 6-state codes each cycle; apply them to the stable
        // button objects (sized to NumSections by RebuildSectionRows).
        if (colorCodes == null) return;
        int count = Math.Min(_sectionButtons.Count, colorCodes.Length);
        for (int i = 0; i < count; i++)
            _sectionButtons[i].ColorCode = colorCodes[i];
    }
}
