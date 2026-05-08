<!--
AgValoniaGPS
Copyright (C) 2024-2026 AgValoniaGPS Contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
-->

# Threading Phase D — Guidance State Migration

**Parent plan:** [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) (§5 Phase D)
**Branch:** `feature/threading-phase-a` (shared across phases A–F per PR #259)
**Scope:** `GuidanceState` follows `YouTurnState` off the UI thread.
Guidance writes happen on the cycle worker against `GuidanceWorkingState`;
the UI thread only mirrors the published snapshot via `ApplyGpsCycleResult`.

Phase C proved the pattern end-to-end for YouTurn. Phase D is a
mechanical application of that pattern to the rest of `GuidanceState`,
plus the UI commands that currently write to it (nudge, snap, reset).
If Phase D introduces any novel design problem, it's a sign the Phase C
pattern has a hole — treat it as a Phase C retrospective.

---

## 1. Goal

At the end of Phase D:

1. **`ApplyGpsCycleResult` is the sole writer of `State.Guidance`.**
   Every `State.Guidance.*` assignment outside this method is removed.
   This matches the Phase C contract for `State.YouTurn`.
2. **`GuidanceWorkingState` covers every field the cycle writes.**
   Extended from the 3 fields Phase C touched (`IsHeadingSameWay`,
   `HowManyPathsAway`, `NudgeOffset`) to the full set produced by
   `TrackGuidanceService.CalculateGuidance` plus the cycle's own
   Pure-Pursuit state.
3. **`GuidanceSnapshot` mirrors the working state.** Populated
   every cycle (the "only when YouTurn tick ran" gate from Phase C
   goes away — Phase D resolves the underlying `NearestPassNumber`
   writer conflict by moving it into the cycle).
4. **Snap / nudge / reset-nudge commands push intents.**
   `SnapLeftCommand`, `SnapRightCommand`, the four `Nudge*Command`s,
   `HalfToolNudge*`, `ResetNudgeCommand` all call `IPipelineIntents`
   and return. Cycle drains and mutates `_guidanceWorking` on the
   cycle thread.
5. **`NearestPassNumber` auto-detect moves into the cycle.** The
   cycle writes `_guidanceWorking.HowManyPathsAway` directly when
   not autosteering. The flat `NearestPassNumber` field on
   `GpsCycleResult` is deleted; the UI's
   `if (!_isAutoSteerEngaged) State.Guidance.HowManyPathsAway = ...`
   block in `ApplyGpsCycleResult` is deleted. One writer, one
   reader — no more oscillation.
6. **Flat Guidance fields on `GpsCycleResult` deleted.**
   `SteerAngle`, `CrossTrackError`, `GoalPointEasting`,
   `GoalPointNorthing`, `HasGuidance`, `DisplayTrack`, `BaseTrack`,
   `NearestPassNumber` (TMP-005 full resolution).
7. **Map effects flow through the snapshot.**
   `_mapService.SetGuidancePoints` and `SetActiveTrack` / `SetBaseTrack`
   are driven by the snapshot, with reference-equality gating (same
   pattern as Phase C if/when we readopt it).
8. **Dead code deleted.** `CalculateContourGuidance` in
   `MainViewModel.TrackManagement.cs` (never called).
9. **Cycle owns `passNumber` as a derived read** of
   `_guidanceWorking.HowManyPathsAway` — the separate `_passNumber`
   field on `GpsPipelineService` is removed. Single source of truth.
10. All existing tests pass plus new unit coverage for the
    guidance-intent path and the full-snapshot mirror. Smoke test
    covers field open → autosteer → snap left/right, nudge left/right,
    reset nudge, auto U-turn, drive multiple passes, close field —
    without regression.

After Phase D, the entire cycle runs without touching any
`ObservableObject`. `PropertyChanged` fires exactly once per cycle
per changed property, from `ApplyGpsCycleResult` on the UI thread.
The §0 threading invariant is satisfied end-to-end for the guidance
subsystem.

---

## 2. Decisions locked before Phase D starts

### 2.1 `_passNumber` field vs deriving from `_guidanceWorking.HowManyPathsAway` → **derive**

Phase C left `GpsPipelineService._passNumber` and
`_guidanceWorking.HowManyPathsAway` as two copies of the same value —
UI pushes the first via `SetActiveTrack`, cycle seeds the second at
tick start. Phase D collapses them: `_passNumber` is deleted, every
cycle-side read uses `_guidanceWorking.HowManyPathsAway` directly.
Snap/nudge intents mutate `_guidanceWorking` on the cycle; no more
cross-thread push path for pass number.

Same treatment for `_nudgeOffset` on `GpsPipelineService`.

### 2.2 Guidance snapshot — emit **every cycle** (not gated)

Phase C gated `Guidance = youTurnTickEffects != null ? ... : null`
to avoid the UI's `NearestPassNumber` auto-detect writer fighting the
cycle's snapshot mirror. Phase D resolves the root cause by making
the cycle the sole writer, so the gate is unnecessary. The snapshot
is populated every cycle from `_guidanceWorking`, which is always
coherent.

### 2.3 `SelectedTrack` setter's `HowManyPathsAway` seeding

The VM's `SelectedTrack` setter currently writes
`State.Guidance.HowManyPathsAway` based on `track.NudgeDistance`
(line 1703 of `MainViewModel.cs`). Phase D replaces that with a
`RequestSetActiveTrack` intent carrying the track and its
persisted `NudgeDistance`; the cycle computes `HowManyPathsAway`
and `NudgeOffset` from it on the cycle thread.

### 2.4 Map-service reference-equality gating

Phase C C7 initially added ref-equality gating on the three YouTurn
map-service calls (reverted because it didn't fix the tooltip freeze,
not because it was wrong). Phase D adopts the same pattern for the
guidance-related map calls (`SetGuidancePoints`, `SetActiveTrack`,
`SetBaseTrack`) — it's a legit perf optimization on its own merits,
and the fields it tracks are longer-lived than the YouTurn ones.

---

## 3. Inventory

### 3.1 What migrates off the UI thread

**Currently-UI-thread writers to `State.Guidance` (to be removed):**

| Location | Writes | Replacement |
|---|---|---|
| `MainViewModel.cs:1703` (SelectedTrack setter, non-null) | `HowManyPathsAway`, `NudgeOffset` | `RequestSetActiveTrack(track, nudgeDist)` intent |
| `MainViewModel.cs:1709` (SelectedTrack setter, width guard) | `HowManyPathsAway=0`, `NudgeOffset=0` | Same intent handles both branches |
| `ApplyResults.cs:78` (NearestPassNumber block) | `HowManyPathsAway` | Deleted — cycle writes `_guidanceWorking.HowManyPathsAway` directly |
| `ApplyResults.cs:129,132` (snapshot mirror) | `IsHeadingSameWay`, `HowManyPathsAway` | Stays — this IS the Phase D mirror point |
| `Commands.Track.cs:47-48` (SnapLeft) | `HowManyPathsAway-=1`, `NudgeOffset=0` | `RequestGuidanceSnap(left)` intent |
| `Commands.Track.cs:62-63` (SnapRight) | `HowManyPathsAway+=1`, `NudgeOffset=0` | `RequestGuidanceSnap(right)` intent |
| `Commands.Track.cs:658` (ResetNudge) | `NudgeOffset=0` | `RequestGuidanceResetNudge()` intent |
| `Commands.Track.cs:924-925` (DeleteContours) | `HowManyPathsAway=0`, `NudgeOffset=0` | `RequestGuidanceReset()` intent |
| `Commands.Track.cs:967-968` (ClearAllTracks path) | `HowManyPathsAway=0`, `NudgeOffset=0` | Same reset intent |
| `NudgeTrack` (`Commands.Track.cs:1566`) | `NudgeOffset +=` | `RequestGuidanceNudge(meters)` intent |

**Currently-UI-thread dead code (delete outright):**

- `CalculateContourGuidance` in `MainViewModel.TrackManagement.cs:481`
  — defined but never called. Its `UpdateFromGuidance` call is the
  last remaining UI-side write through `State.Guidance.UpdateFromGuidance`.

### 3.2 What stays on the UI thread

- Everything in `ApplyGpsCycleResult`.
- `IsAutoSteerEngaged` toggling (UI user action → `Set*` on pipeline).
- Autosteer engagement preconditions (boundary / headland checks).
- Command-to-intent plumbing itself (posting is UI-thread; draining is cycle).

### 3.3 Flat Guidance fields on `GpsCycleResult` (deleted in D7)

- `SteerAngle`, `CrossTrackError` — move into `GuidanceSnapshot`.
- `GoalPointEasting`, `GoalPointNorthing` — become
  `GoalPoint` (Vec2) on `GuidanceSnapshot`.
- `HasGuidance` — move into `GuidanceSnapshot`.
- `DisplayTrack`, `BaseTrack` — move into `GuidanceSnapshot`
  (or a separate `DisplayTracksSnapshot` if cleaner — D1 decides).
- `NearestPassNumber` — deleted outright (no longer a cross-thread
  signal once the UI writer is gone).

Stays flat (not guidance state; one-shot or top-level cycle semantics):

- `IsAutoSteerEngaged` — bool read-back.
- `AutoSteerDisengagedThisCycle`, `DisengageReason` — one-shot
  completion signal (like `JustCompleted` on YouTurn).

---

## 4. Out of scope

- **Phase E / F.** FieldState and NTRIP/UDP state migrations are
  separate phases. Phase D keeps its blast radius to `GuidanceState`.
- **Contour guidance rehabilitation.** The `CalculateContourGuidance`
  path is dead; Phase D deletes it. If contour mode is ever wanted
  back, that's new feature work — not a threading concern.
- **Pure-Pursuit state shape changes.** `PpIntegral`,
  `PpPivotDistanceError`, etc. move onto `GuidanceWorkingState`
  verbatim. No algorithmic changes.
- **UI re-layout for the new nudge flow.** If the commands feel
  less responsive because of the 1-cycle intent latency, that's a
  Phase D close decision, not an upfront plan change. (Phase C
  confirmed 50–100 ms is imperceptible.)

---

## 5. Commit-by-commit plan

### Commit 1 — Extend `GuidanceWorkingState` + `GuidanceSnapshot`

**Goal.** Put every field the cycle writes onto
`GuidanceWorkingState`. Extend `GuidanceSnapshot` to mirror.

- Add to `GuidanceWorkingState`: `CrossTrackError`, `HeadingError`,
  `SteerAngle`, `SteerAngleRaw`, `DistanceOffRaw`, `PpIntegral`,
  `PpPivotDistanceError`, `PpPivotDistanceErrorLast`, `PpCounter`,
  `GoalPoint`, `RadiusPoint`, `PurePursuitRadius`, `IsReverse`,
  `IsGuidanceActive`, `CurrentLineLabel`, `IsContourMode`,
  `ActiveTrack`, `DisplayTrack`, `BaseTrack`, `HasGuidance`.
- Add a `HasNearestPass` + `NearestPassNumber` pair (or use `int?`).
- Mirror all onto `GuidanceSnapshot`.
- Shape-mirror test in `Models.Tests` extended to lock the new parity.
- No behavior change yet — Phase A-style scaffolding.

**Acceptance.** Build clean. Shape-mirror test passes. No other
test files change.

### Commit 2 — Cycle populates the extended working state

**Goal.** The cycle's guidance branch writes everything into
`_guidanceWorking` instead of locals. Snapshot built from the POCO.

- `GpsPipelineService.ProcessCycle` guidance branch: replace the
  `double steerAngle, crossTrackError, goalE, goalN, ...` locals
  with writes to `_guidanceWorking`. `_trackGuidanceState` stays
  cycle-local (it's PP internal state, not observable).
- `BuildGuidanceSnapshot` populates the extended snapshot.
- Snapshot emitted every cycle (un-gate from the Phase C C5/C8 check).
- `GpsCycleResult.Guidance` still shows alongside the flat fields
  (flat fields still populated for now — consumers not yet switched).

**Acceptance.** All existing tests pass. Smoke test shows no
regression — the snapshot and the flat fields carry matching
values this cycle (verifiable ad-hoc if a log is added).

### Commit 3 — `NearestPassNumber` becomes cycle-side `HowManyPathsAway`

**Goal.** Collapse the two `HowManyPathsAway` writers into one.

- Cycle's `UpdateDisplayTrack` path writes
  `_guidanceWorking.HowManyPathsAway = nearestPass` directly
  when not autosteering.
- `GpsCycleResult.NearestPassNumber` deleted.
- `ApplyGpsCycleResult`'s `if (!_isAutoSteerEngaged) State.Guidance.HowManyPathsAway = result.NearestPassNumber.Value`
  block deleted. The snapshot mirror handles it.
- Re-test the oscillation scenario that prompted the C7 fix: tractor
  positioned off pass 0, field opens, no autosteer — track offset
  should display and not flicker.

**Acceptance.** Tracks render stably. No oscillation regression.
The `_passNumber` field on `GpsPipelineService` can now be deleted
too — every read site uses `_guidanceWorking.HowManyPathsAway`
(deleted in this commit).

### Commit 4 — Snap commands become intents

**Goal.** `SnapLeftCommand` / `SnapRightCommand` post
`RequestGuidanceSnap(direction)`. Cycle drains and mutates
`_guidanceWorking.HowManyPathsAway` / `NudgeOffset` on the cycle
thread.

- Extend `IPipelineIntents` + `PipelineIntents` + `PipelineIntentBatch`.
- Encoding: `int _snap` with sentinels `0 = none, 1 = left, 2 = right`
  (same pattern as `ManualYouTurn`).
- Drain + apply at stage 1 of `ProcessCycle` (before the YouTurn tick,
  after `ClearYouTurn`).
- UI commands: replace `State.Guidance.HowManyPathsAway ±= ...;
  State.Guidance.NudgeOffset = 0; _trackGuidanceState = null;
  SyncGuidanceStateToPipeline();` with
  `_intents.RequestGuidanceSnap(left: true/false);`. The
  `_trackGuidanceState = null` is handled on the cycle (intent drain
  also clears it there).

**Acceptance.** Click Snap Left / Snap Right — pass number updates
within one cycle. Unit test posts snap intent, drains, asserts the
state machine update.

### Commit 5 — Nudge commands become intents

**Goal.** All six nudge commands post `RequestGuidanceNudge(meters)`.
`ResetNudge` posts `RequestGuidanceResetNudge`.

- Add two intent kinds:
  - `RequestGuidanceNudge(double meters)` — delta (positive right,
    negative left). Last-wins by sum (drain accumulates into the
    batch's nudge delta; subsequent `Drain()` returns 0).
  - `RequestGuidanceResetNudge()` — zero out.
- Cycle drains and mutates `_guidanceWorking.NudgeOffset` +
  clears `_trackGuidanceState`. Heading-same-way adjustment moves
  into the cycle (it needs the fresh heading anyway).
- `NudgeTrack` helper method in `Commands.Track.cs` retires —
  commands call the intent directly.

**Acceptance.** Nudge buttons shift the guidance line within one
cycle. Reset zeroes the offset. Fine nudge (2.5 mm) accumulates
correctly. Unit test drains two back-to-back nudge intents and
asserts the sum lands on the working state.

### Commit 6 — `SelectedTrack` setter + track commands use intents

**Goal.** Remove the VM-side writes on track change / reset.

- `SelectedTrack` setter's `State.Guidance.HowManyPathsAway = ...;
  State.Guidance.NudgeOffset = ...` block: replaced with
  `_intents.RequestSetActiveTrack(value, nudgeDistance)`.
  The pipeline's `SetActiveTrack` setter call in
  `SyncGuidanceStateToPipeline` stays (it's the track-reference
  push, separate from pass-number).
- `DeleteContoursCommand` and the "clear all tracks" path replace
  direct `State.Guidance.* = 0` with `RequestGuidanceReset()`.
  The VM no longer writes to `State.Guidance` outside
  `ApplyGpsCycleResult`.

**Acceptance.** Select a track — pass number and nudge offset
snap to the track's saved values within one cycle. Delete contours
— offset zeroes. `grep -rn "State\.Guidance\.\w\+\s*=" Shared/AgValoniaGPS.ViewModels`
returns only the `ApplyResults.cs` lines and the `Reset()` /
`UpdateFromGuidance` methods on `GuidanceState.cs`.

### Commit 7 — Full snapshot mirror + map-service gating

**Goal.** `ApplyGpsCycleResult` mirrors the extended snapshot onto
`State.Guidance`. Map-service calls driven by the snapshot with
ref-equality gating.

- Mirror block for `result.Guidance` extended to write every field
  the snapshot carries onto `State.Guidance`.
- `_mapService.SetGuidancePoints(result.GoalPointEasting, result.GoalPointNorthing, isActive: true)`
  → reads from `g.GoalPoint` and `g.HasGuidance`.
- `_mapService.SetActiveTrack(result.DisplayTrack); _mapService.SetBaseTrack(result.BaseTrack)`
  → reads from `g.DisplayTrack` / `g.BaseTrack`.
- All three calls gated on reference / value change vs the last
  pushed value (matches the pattern tried in C7 initially —
  justified on its own perf merits now).
- `SimulatorSteerAngle = result.SteerAngle` → reads from `g.SteerAngle`.
- `CrossTrackError = result.CrossTrackError * 100` → reads from
  `g.CrossTrackError`.

**Acceptance.** Drive — guidance dots, track render, steer angle,
XTE all behave identically to pre-Phase-D. Smoke-verify on a real
field.

### Commit 8 — Delete flat Guidance fields from `GpsCycleResult`

**Goal.** TMP-005 full resolution.

- Delete `SteerAngle`, `CrossTrackError`, `GoalPointEasting`,
  `GoalPointNorthing`, `HasGuidance`, `DisplayTrack`, `BaseTrack`
  from `GpsCycleResult`.
- Remove their writes in `GpsPipelineService.ProcessCycle`'s result
  builder.
- Remove their reads in `ApplyGpsCycleResult` (C7 already swapped
  them out; this is the source-tree cleanup).
- `grep -rn "result\.SteerAngle\|result\.CrossTrackError\|result\.GoalPoint"
  Shared/` returns zero.

**Acceptance.** Build clean. Tests pass. TMP-005 fully resolved
in the parking lot.

### Commit 9 — Delete `CalculateContourGuidance` dead code

**Goal.** Remove the unused UI-thread `UpdateFromGuidance` call.

- Delete `CalculateContourGuidance` method +
  `FindNearestContour` helper if unused elsewhere.
- Delete the `UpdateFromGuidance` method on `GuidanceState.cs`
  if nothing else references it (cycle writes via working state
  now; VM writes via snapshot mirror; neither needs this helper).

**Acceptance.** Build clean. Tests pass.

### Commit 10 — Tests + line-count + parking-lot review

**Goal.** Phase D acceptance gate.

- `GuidanceCycleTests.cs` in `Services.Tests/Pipeline/`:
  - Drained snap intent updates `HowManyPathsAway` on cycle thread.
  - Drained nudge intent accumulates correctly.
  - Drained reset intent zeroes nudge.
  - Structural: `IGpsPipelineService` has no `Set*`/`Push*` method
    taking `GuidanceWorkingState` or `GuidanceSnapshot` (mirror of
    the Phase C C9 structural guard).
- `YouTurnTests.cs` extensions in `ViewModels.Tests`:
  - `SnapLeftCommand_posts_snap_intent`.
  - `NudgeLeftCommand_posts_nudge_intent_with_negative_delta`.
  - `ResetNudgeCommand_posts_reset_intent`.
  - `ApplyGpsCycleResult_mirrors_full_Guidance_snapshot` — assert
    every mirrored field lands on `State.Guidance`.
- TMP-007 re-measure: record post-D line counts.
- Parking-lot review: mark TMP-002 (guidance half), TMP-005
  (guidance half), TMP-007 (post-D measure) resolved. Review TMP-006
  again — Phase D's structural tests cover guidance too.

**Acceptance.** All tests pass. Smoke-verified on Bing Test field
and real AiO hardware: field open → autosteer → snap both ways →
nudge both ways → fine nudge → half-tool nudge → reset nudge → auto
U-turn → manual U-turn → close field — no regression vs Phase C
close.

---

## 6. Risks / interim freezes

### Phase C pattern proved, so expect mechanical execution

Most of Phase D is template application. The few places to watch:

- **Commit 3 (`NearestPassNumber` collapse).** The UI-thread writer
  and the cycle-side writer are currently both live. C3 deletes the
  UI-thread one and activates the cycle-side one in the same commit.
  If split, one cycle has no writer → pass-number display freezes.
- **Commit 4/5 intent ordering.** If both a snap and a nudge arrive
  in the same cycle's drain, order matters: snap first (clears nudge
  to zero), then nudge (adds delta). Document the ordering and test
  it.
- **Commit 6 (SelectedTrack).** The setter writes happen *before*
  `SyncGuidanceStateToPipeline`, which means the cycle's current
  pass-number is out of date until after the Sync call. Phase D's
  intent-based flow must handle the case where the track changes
  mid-cycle (intent drained, but cycle's `SetActiveTrack(track)` push
  happens on UI thread slightly later). Worst case: one cycle of
  wrong-track guidance — same latency budget as every other intent.

### Perf regression monitoring

Phase D introduces more per-cycle state in `_guidanceWorking` (the
PP state adds ~5 fields). Per-cycle snapshot build overhead grows
from ~3 fields to ~20. Still trivial on modern hardware but watch
for FPS regression on Android tablet (test device — see
`reference_test_devices.md`).

### Tooltip-freeze context

TMP-010 (pre-existing Avalonia tooltip freeze) is independent of
Phase D. Do not chase it during Phase D smoke-testing — it's parked.

---

## 7. Acceptance gate

Phase D is done when, on a Bing Test field drive:

1. Field open with existing nudge + pass offset — tractor snaps to
   the saved offset track without flicker.
2. Engage autosteer — tracks lock in, HowManyPathsAway stable.
3. Snap left / snap right — pass updates, track re-renders, no
   oscillation.
4. Nudge left / right / fine / half-tool — cyan track shifts,
   steering follows, `State.Guidance.NudgeOffset` mirrors cycle.
5. Reset nudge — nudge zeroes.
6. Auto U-turn — triggers, executes, completes, new offset track
   acquired.
7. Manual U-turn — triggers, executes, completes.
8. Close field — autosteer disengages, all state clears.
9. `grep -rn "State\.Guidance\.\w\+\s*=" Shared/AgValoniaGPS.ViewModels`
   returns only `MainViewModel.ApplyResults.cs`.
10. Line-count delta from Phase C close recorded on TMP-007.

Parking-lot review after smoke test → commit the phase close.

---

## 8. Linked work

- [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) — parent.
- [THREADING_PHASE_C_PLAN.md](THREADING_PHASE_C_PLAN.md) — template
  this phase applies mechanically.
- [THREADING_MIGRATION_PARKING_LOT.md](THREADING_MIGRATION_PARKING_LOT.md)
  — open items owned by Phase D: TMP-002 (guidance half), TMP-005
  (guidance half).
- [threading_model.svg](threading_model.svg) — yellow WorkingState
  box for `GuidanceWorkingState`; purple intent arrow for snap /
  nudge / reset / set-active-track.
