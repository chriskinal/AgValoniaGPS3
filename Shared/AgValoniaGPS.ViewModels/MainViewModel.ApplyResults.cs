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
        Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyGpsCycleResult(result));
    }

    /// <summary>
    /// Apply a GPS cycle result snapshot to all bound ViewModel properties.
    /// Called on the UI thread after the service pipeline computes results on a background thread.
    /// This is the ONLY place where GPS-derived properties are set during normal operation.
    /// </summary>
    public void ApplyGpsCycleResult(GpsCycleResult result)
    {
        // Record for debug dump (ring buffer, last 60 seconds at 10Hz)
        AgValoniaGPS.Services.Logging.GpsDataRecorder.Instance.Record(result);

        // Mark GPS as received (updates timeout tracking for connection status)
        if (result.GpsValid)
            _gpsService.MarkGpsReceived();

        // GPS position
        Latitude = result.Latitude;
        Longitude = result.Longitude;
        Easting = result.Easting;
        Northing = result.Northing;
        Heading = result.Heading;
        _speed = result.Speed;
        OnPropertyChanged(nameof(SpeedKmh));
        RollDegrees = result.RollDegrees;

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
        FixQuality = GetFixQualityString(result.FixQuality);

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
            _mapService.SetNextTrack(yt.NextTrack);
            _mapService.SetIsInYouTurn(yt.IsExecuting);
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

        // Map service position update (single atomic call)
        _mapService.SetAllPositions(
            result.Easting, result.Northing, result.Heading * Math.PI / 180.0,
            result.ToolEasting, result.ToolNorthing, result.ToolHeadingRadians,
            result.ToolWidth, result.HitchEasting, result.HitchNorthing,
            result.IsToolPositionReady);

        // Live wheel angle for the front-wheel sprite (#336). Real WAS reading
        // when an autosteer module is attached, simulator slider value when
        // the sim is driving GPS. Both are in degrees and signed (+right).
        double steerDeg = _isSimulatorEnabled
            ? _simulatorService.SteerAngle
            : _autoSteerService.LastSteerData.ActualSteerAngle;
        _mapService.SetVehicleSteerAngle(steerDeg * Math.PI / 180.0);
    }

    private void UpdateSectionPropertiesFromResult(bool[] states, int[]? colorCodes)
    {
        int count = Math.Min(states.Length, 16);
        for (int i = 0; i < count; i++)
        {
            switch (i)
            {
                case 0: Section1Active = states[0]; break;
                case 1: Section2Active = states[1]; break;
                case 2: Section3Active = states[2]; break;
                case 3: Section4Active = states[3]; break;
                case 4: Section5Active = states[4]; break;
                case 5: Section6Active = states[5]; break;
                case 6: Section7Active = states[6]; break;
                case 7: Section8Active = states[7]; break;
                case 8: Section9Active = states[8]; break;
                case 9: Section10Active = states[9]; break;
                case 10: Section11Active = states[10]; break;
                case 11: Section12Active = states[11]; break;
                case 12: Section13Active = states[12]; break;
                case 13: Section14Active = states[13]; break;
                case 14: Section15Active = states[14]; break;
                case 15: Section16Active = states[15]; break;
            }
        }

        if (colorCodes != null)
        {
            for (int i = 0; i < Math.Min(colorCodes.Length, count); i++)
            {
                switch (i)
                {
                    case 0: Section1ColorCode = colorCodes[0]; break;
                    case 1: Section2ColorCode = colorCodes[1]; break;
                    case 2: Section3ColorCode = colorCodes[2]; break;
                    case 3: Section4ColorCode = colorCodes[3]; break;
                    case 4: Section5ColorCode = colorCodes[4]; break;
                    case 5: Section6ColorCode = colorCodes[5]; break;
                    case 6: Section7ColorCode = colorCodes[6]; break;
                    case 7: Section8ColorCode = colorCodes[7]; break;
                    case 8: Section9ColorCode = colorCodes[8]; break;
                    case 9: Section10ColorCode = colorCodes[9]; break;
                    case 10: Section11ColorCode = colorCodes[10]; break;
                    case 11: Section12ColorCode = colorCodes[11]; break;
                    case 12: Section13ColorCode = colorCodes[12]; break;
                    case 13: Section14ColorCode = colorCodes[13]; break;
                    case 14: Section15ColorCode = colorCodes[14]; break;
                    case 15: Section16ColorCode = colorCodes[15]; break;
                }
            }
        }
    }
}
