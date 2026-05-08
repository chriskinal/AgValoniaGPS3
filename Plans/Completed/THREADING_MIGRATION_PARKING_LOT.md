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

# Threading Migration Parking Lot

The single index of open decisions, deferred work, and cross-phase
follow-ups for the threading migration. Supplements
[THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) and every
per-phase plan under `Plans/`.

---

## 1. Purpose

Per-phase plans intentionally *don't* resolve every open question — they
lock only what that phase needs to ship. Everything else gets parked
here so it can't fall through the cracks between phases.

Two failure modes this document exists to prevent:

- **Silent deferral.** A phase plan says "decided later" and "later"
  never arrives because no one remembers.
- **Silent creep.** A decision gets made ad hoc mid-phase without being
  traced back to the architectural concern that motivated it.

Every deferred item lives here with a **why**, a **who/when owns it**,
and a **dated review log**. Nothing gets dropped silently — items are
either resolved and archived (§5) or kept open with an explicit reason.

---

## 2. Review protocol

At the end of every phase, before the phase's PR merges:

1. Walk §4 (Open items) top to bottom.
2. For each open item, answer three questions:
   - **Did this phase surface new information that changes the answer?**
     If yes, update the "Why parked" field or move to §5 as resolved.
   - **Should this be resolved in the phase we're closing?**
     If yes, pull it into the current PR. A deferred item is
     cheaper to close when the relevant code is already touched.
   - **Does the next phase need this resolved before it can start?**
     If yes, the next phase's plan opens with "resolve TMP-NNN" as
     its first step.
3. Append a dated line to the item's review log, even if the answer is
   "still parked" — the log proves the item was considered, not
   ignored.

Review happens in the PR description, inline against the item IDs. A
merge without a parking-lot review is a convention violation; same
weight as merging without tests.

---

## 3. Entry shape

Every open item carries:

- **ID** — `TMP-NNN` (threading migration parking). Stable across the
  lifetime of the item — once assigned, never reused, even after
  resolution.
- **Title** — short phrase.
- **Status** — one of: `Open`, `In review`, `Resolved` (resolved items
  move to §5).
- **Raised in** — phase where the item was first deferred.
- **Decide by** — the phase where the item *must* be resolved, or
  `Optional` if it's a nice-to-have.
- **Source** — pointer to the originating discussion (parent plan
  section, per-phase plan section, conversation artifact).
- **Why parked** — the reason, concrete. Not "we'll figure it out" —
  "this becomes testable only when X exists" or "this depends on the
  shape of Y which isn't visible yet."
- **What the decision is** — the specific question to answer, phrased
  so someone walking in cold knows what they're choosing between.
- **Review log** — dated notes from each end-of-phase review.

---

## 4. Open items

### TMP-001 — Snapshot identity vs equality

**Status:** Resolved (Phase C close, 2026-04-20). Moved to §5 Resolved.

---

### TMP-002 — Synchronous UI command carve-outs

- **Status:** Open
- **Raised in:** Phase A
- **Decide by:** Per migration phase (Phase C for YouTurn commands,
  Phase D for Guidance commands)
- **Source:** parent plan §6.3; Phase A plan §8

**Why parked.** Can only be answered by walking every UI command whose
current behavior writes to `State.*` and asking "is a ≤100 ms delay
(one cycle tick) visible to the user?" That inventory is
phase-specific: Phase C covers `TriggerManualYouTurn*`, `ClearYouTurnState`;
Phase D covers nudge / snap / reverse-heading. Phase A migrates nothing,
so the question has no subject.

**What the decision is.** For each migrated command, either:
- (a) it goes through the intent queue (default), accepting up to one
  tick of latency, or
- (b) it's added to an explicit carve-out list with a written rationale
  for why sub-tick latency is required.

Carve-outs are the only sanctioned bypass of the one-way flow.

**Review log.**
- 2026-04-19 — Parked during Phase A planning. No commands migrated
  yet.
- 2026-04-19 (Phase A close) — Still nothing migrated; no commands
  touched in Phase A. Phase C's plan must open with the carve-out
  inventory for `TriggerManualYouTurnLeft/Right` and `ClearYouTurnState`.
- 2026-04-19 (Phase B close) — Phase B migrated no UI commands.
  Still parked for Phase C (YouTurn commands) and D (guidance commands).
- 2026-04-20 (Phase C close) — **No carve-outs needed for the YouTurn
  command set.** C6 migrated `TriggerManualYouTurnLeft/Right` to
  `IPipelineIntents.RequestManualYouTurn`; C7 migrated `ClearYouTurnState`
  to `RequestClearYouTurn`. Both tolerate the ~1-cycle (50–100 ms)
  latency well. Smoke-verified: manual turn plots promptly; clear
  on field-close makes the path disappear imperceptibly after the
  next cycle. YouTurn half of this item resolved; Guidance half
  (nudge / snap / reverse-heading / `HowManyPathsAway` writes) still
  owned by Phase D.
- 2026-04-20 (Phase D close) — **Guidance half resolved (D4/D5/D6).**
  Every guidance-writing UI command became an intent:
  `SnapLeft`/`SnapRight` → `RequestGuidanceSnap(left)` (D4, commit
  `88dbeb7`); all nudge commands (Nudge, FineNudge, HalfToolNudge,
  SnapToPivot via NudgeTrack) → `RequestGuidanceNudge(meters)` (D5,
  `e95d40c`); `ResetNudge` → `RequestGuidanceResetNudge`. The
  `SelectedTrack` setter + `DeleteContours` + `DeleteAppliedArea`
  paths stopped writing `State.Guidance.*` directly — they seed
  `_pendingInitialPathsAway`/`_pendingInitialNudgeOffset` and let
  `SyncGuidanceStateToPipeline → SetActiveTrack` carry the values
  into `_guidanceWorking` (D6, `807fd45`). No carve-outs needed;
  ~1-cycle latency imperceptible on smoke. **Fully resolved.**

---

### TMP-005 — Removal of flat YouTurn / Guidance fields on `GpsCycleResult`

- **Status:** Open
- **Raised in:** Phase A
- **Decide by:** Phase C (for YouTurn fields) / Phase D (for Guidance
  fields)
- **Source:** Phase A plan §3 ("Out of scope")

**Why parked.** Phase A adds `YouTurnSnapshot? YouTurn` and
`GuidanceSnapshot? Guidance` to `GpsCycleResult` as new nullable fields.
Existing flat fields (`IsInYouTurn`, `YouTurnTriggered`,
`YouTurnCompleted`, `SteerAngle`, `CrossTrackError`, `GoalPointEasting`,
`GoalPointNorthing`, `HasGuidance`) remain for backward compatibility
so nothing breaks mid-migration. Removal happens when all consumers are
swapped to read the snapshot.

**What the decision is.** Confirm Phase C's final commit removes
YouTurn flat fields and Phase D's final commit removes Guidance flat
fields, not sooner (breaks mid-branch builds) and not later (leaves
dead fields in the snapshot).

**Review log.**
- 2026-04-19 — Parked during Phase A planning. Owned by Phase C / D
  explicitly in their plans.
- 2026-04-19 (Phase A close) — Flat fields on `GpsCycleResult` remain
  (`IsInYouTurn`, `YouTurnTriggered`, `YouTurnCompleted`, `SteerAngle`,
  `CrossTrackError`, `GoalPointEasting`, `GoalPointNorthing`,
  `HasGuidance`). New `YouTurn` / `Guidance` snapshot fields sit
  alongside them as `null` placeholders. No removal in Phase A; still
  parked.
- 2026-04-19 (Phase B close) — Flat fields still in place; `null`
  placeholders still unused. Phase C populates the YouTurn snapshot and
  removes the three flat YouTurn fields. Phase D does the same for
  Guidance.
- 2026-04-20 (Phase C close) — **YouTurn half resolved (C8, commit
  `f50ef51`).** `IsInYouTurn`, `YouTurnTriggered`, `YouTurnCompleted`
  deleted from `GpsCycleResult`. New `YouTurnSnapshot.JustCompleted`
  carries the one-cycle completion signal that the UI uses to reset
  its `_trackGuidanceState` cache. `IGpsPipelineService.IsInYouTurn`
  also deleted (no callers). Guidance half (`SteerAngle`,
  `CrossTrackError`, `GoalPointEasting`, `GoalPointNorthing`,
  `HasGuidance`, `NearestPassNumber`, `DisplayTrack`, `BaseTrack`,
  `AutoSteerDisengagedThisCycle`, `DisengageReason`) remains —
  owned by Phase D. Keep open until then.
- 2026-04-20 (Phase D close) — **Guidance half resolved.** D3 (commit
  `f205845`) deleted `NearestPassNumber` — the cycle is now the sole
  writer of `_guidanceWorking.HowManyPathsAway` including the
  not-autosteering auto-detect path. D8 (commit `d86a3a4`) deleted
  the rest: `SteerAngle`, `CrossTrackError`, `GoalPointEasting`,
  `GoalPointNorthing`, `HasGuidance`, `DisplayTrack`, `BaseTrack`.
  All moved onto `GuidanceSnapshot` (D1/D2) and consumed through
  the full snapshot mirror in `ApplyGpsCycleResult` (D7, `04ad231`).
  `AutoSteerDisengagedThisCycle` / `DisengageReason` stay flat
  intentionally (one-shot transition signals — no Guidance equivalent
  of `YouTurn.JustCompleted`). **Fully resolved.**

---

### TMP-006 — Enforcement of "no direct `State.*` writes from services"

- **Status:** Open
- **Raised in:** Phase A (from parent plan acceptance criteria)
- **Decide by:** Optional — latest viable is Phase F (final
  whole-effort acceptance)
- **Source:** parent plan §7 ("Enforce via a Roslyn analyzer if
  practical, otherwise via grep in CI")

**Why parked.** Useful but not load-bearing for any phase to ship.
Enforcement matters at steady state to keep the invariant from eroding
— a Roslyn analyzer is the structural answer, a CI grep is the
pragmatic answer. Either works; neither needs to be in place before
Phases C or D prove the pattern. Worth doing once the pattern is
established, because the analyzer rules become unambiguous.

**What the decision is.** Roslyn analyzer (compile-time enforcement,
higher build cost, false-positive risk) vs CI grep (zero build cost,
can miss aliased writes). Or both.

**Review log.**
- 2026-04-19 — Parked during Phase A planning. No phase currently
  owns it.
- 2026-04-19 (Phase A close) — No services migrated in Phase A; the
  invariant has nothing to enforce yet. Still parked — revisit at the
  end of Phase C when the first migrated service exists.
- 2026-04-19 (Phase B close) — Phase B established more invariants to
  enforce: receive thread is parse-only, AutoSteerService holds no
  `_localPlane`, `NmeaParserService` deleted. The `UnifiedPipelineTests`
  (C6) enforce the first two at unit-test level but an analyzer or CI
  grep would catch structural drift earlier. Still parked.
- 2026-04-20 (Phase C close) — Phase C added a third unit-test-level
  structural guard: `YouTurnCycleTests.IGpsPipelineService_has_no_
  direct_YouTurn_writethrough_methods` (reflection-based; catches any
  future `SetYouTurn*`/`PushYouTurn*`/`ApplyYouTurn*` method that
  bypasses the intent channel). Phase D will add a similar guard for
  Guidance. An analyzer / CI grep is still worthwhile post-migration
  to catch cross-cutting cases the reflection tests don't cover.
  Still parked; the tests are a useful stopgap.
- 2026-04-20 (Phase D close) — Phase D added the guidance twin:
  `GuidanceCycleTests.IGpsPipelineService_has_no_direct_Guidance_
  writethrough_methods` (same reflection pattern, scans for methods
  taking `GuidanceWorkingState` or `GuidanceSnapshot`). YouTurn
  + Guidance now both covered at the unit-test level. An analyzer
  / CI grep is still the right answer for post-migration enforcement
  (catches cross-cutting patterns like `State.*` writes from any
  service, which the reflection guard doesn't address). Still parked
  for post-migration.
- 2026-04-20 (Phase E close) — Third structural guard shipped:
  `FieldStateCycleTests.IGpsPipelineService_has_no_direct_LocalPlane_
  writethrough_methods` (scans for methods taking `LocalPlane`).
  All three mutable `ObservableObject` types in `ApplicationState`
  now have reflection-level cycle-to-UI direction enforcement. The
  final acceptance grep — `grep -rn '_appState\.\w+\.\w+\s*='
  Shared/AgValoniaGPS.Services` — returns **zero** after E1 moved
  the LocalPlane auto-create off the cycle thread. §0 invariant
  satisfied end-to-end for `FieldState`/`YouTurnState`/`GuidanceState`.
  Only `ConnectionState` remains (Phase F). Post-migration analyzer
  / CI grep is still the right final answer but is no longer urgent —
  the current test suite catches every architectural regression we
  know to look for.
- 2026-04-20 (Phase F close) — Fourth and final structural guard:
  `ConnectionStateCycleTests` covers `IGpsPipelineService`,
  `INtripClientService`, and `IUdpCommunicationService` — none may
  take a `ConnectionState` parameter. ConnectionState was never
  service-written (audit found zero matches), so F1 is documentation
  + test rather than a migration. The final acceptance grep over
  every service-side write to any observable state returns **zero**:
  ```
  grep -rn 'State\.\w+\.\w+\s*=\|_appState\.\w+\.\w+\s*='
       Shared/AgValoniaGPS.Services
  ```
  The migration is complete. Post-migration TMP-006 resolution
  (analyzer / CI grep) is now a **deliberate choice** rather than
  an urgent need — the four reflection-level guards + the
  already-established `UnifiedPipelineTests` catch every
  architectural drift scenario we've actually encountered during
  Phases A–F. Leaving open as a "would be nice" for a future
  house-keeping PR.

---

### TMP-007 — `MainViewModel` line-count baseline

- **Status:** Open
- **Raised in:** Phase A (from parent plan acceptance criteria)
- **Decide by:** Before Phase C starts (its acceptance references the
  baseline)
- **Source:** parent plan §7 ("baseline captured before Phase C")

**Why parked.** The parent plan asserts `MainViewModel` partials'
total line count will drop measurably. That's only verifiable against a
recorded baseline. Capture is trivial — one `wc -l` over the
`MainViewModel.*.cs` files — but needs to happen before Phase C so the
number is real.

**What the decision is.** When and where to record the baseline.
Suggest: add a line to the parent plan §7 with the recorded number the
day Phase C's branch is cut; commit alongside the Phase C PR.

**Review log.**
- 2026-04-19 — Parked during Phase A planning. Trivial but will be
  forgotten if not tracked.
- 2026-04-19 (Phase A close) — **Baseline captured** at end of Phase A
  (same commit base Phase C will branch from): `MainViewModel.cs` =
  5,148 lines, 23 partial files totaling 14,136 lines overall.
  Notable per-phase targets:
  - `MainViewModel.YouTurn.cs` = 206 lines (Phase A acceptance target:
    under 100 lines after Phase C).
  - `MainViewModel.GpsHandling.cs` = 436 lines (Phase C removes the
    `YouTurnStateMachine.Tick` call from this file).
  Phase C's final commit compares against this baseline.
- 2026-04-19 (Phase B close) — Post-Phase-B snapshot: `MainViewModel.cs`
  = 5,139 lines (−9 from A; C3 removed `_nmeaParser` field,
  construction, and the NMEA-parse branch in `OnUdpDataReceived`).
  Total across 23 partials: 14,127 lines (−9). Phase C's reduction
  target is still measured against the 14,136 Phase-A baseline.
- 2026-04-20 (Phase C close) — Post-Phase-C snapshot:
  - `MainViewModel.YouTurn.cs` = **132 lines** (Phase A baseline: 206,
    target <100). Bridge helpers, `ApplyEffects`, `BuildTickContext`,
    `GetCurrentGpsPosition`, `_youTurnBridge`, `_guidanceBridge` all
    gone. The residual ~32 lines are `IsTrackOnBoundary` and
    `DistanceToBoundary` helpers that the file itself calls out as
    "used outside YouTurn too" — moving them would push the file
    comfortably under 100, but cross-cutting. Deferred as a pure
    cleanup (not a threading decision).
  - `MainViewModel.GpsHandling.cs` = **418 lines** (Phase A baseline:
    436, −18). C4 removed the inline YouTurn tick block; the remaining
    file size is GPS data flow + track-selection auto-fit.
  - `MainViewModel.cs` = **5,150 lines** (Phase A: 5,148; +2 net —
    added `IPipelineIntents _intents` field, constructor parameter,
    and assignment; offset by `_youTurnStateMachine` DI removal in
    C7). Net neutral.
  - Total across 23 partials: **14,112 lines** (−24 from Phase A
    baseline, −15 from Phase B).
  Phase C meets the parent-plan intent ("drops measurably") and hits
  its YouTurn-specific target within a documented cross-cutting
  exception. Phase D will re-measure.
- 2026-04-20 (Phase E close) — Post-Phase-E snapshot:
  - `MainViewModel.GpsHandling.cs` = **399 lines** (−45 from D, E2
    deleted the dead `UpdateHeadlandProximity` method +
    `_headlandDetector` field).
  - Other VM files unchanged from D close.
  - Total across 23 partials: **14,003 lines** (−33 from D, **−133
    from the Phase A baseline of 14,136**).
  Phase E is almost entirely audit + deletion — the delta comes
  from dead-code removal rather than new threading scaffolding.
  Phase F owns the final `ConnectionState` migration; no VM-file
  changes expected there.
- 2026-04-20 (Phase F close) — Final migration snapshot:
  - Total across 23 partials: **14,003 lines** (unchanged from E;
    Phase F was documentation + a new test, no VM-file deltas).
  - Full Phase A→F arc: `MainViewModel.cs` held at 5,160 lines,
    `MainViewModel.YouTurn.cs` dropped from 206 → 132
    (−74, 36%), `MainViewModel.GpsHandling.cs` 436 → 399
    (−37, 8%), `MainViewModel.TrackManagement.cs` 529 → 423
    (−106, 20%). Total repo VM-partials: −133 lines (about 1%).
  The migration's real payoff is structural — observable state is
  no longer touched on background threads — rather than line-count
  reduction. The line-count drop was a side effect of consolidating
  bridge helpers (C7) and deleting dead code (D9, E2).
- 2026-04-20 (Phase D close) — Post-Phase-D snapshot:
  - `MainViewModel.YouTurn.cs` = **132 lines** (unchanged from C —
    Phase D didn't touch it).
  - `MainViewModel.GpsHandling.cs` = **444 lines** (+26 from C close
    — D6 added `_pendingInitialPathsAway`/`_pendingInitialNudgeOffset`
    fields + the Sync consumption logic; D7 added `_lastMirrored
    DisplayTrack`/`BaseTrack` fields).
  - `MainViewModel.TrackManagement.cs` = **423 lines** (−106 from
    pre-D — D9 deleted `CalculateContourGuidance` + `FindNearestContour`
    dead code).
  - `MainViewModel.Commands.Track.cs` = **1,565 lines** (−1 net
    from pre-D; D4/D5/D6 simplified snap/nudge/reset commands but
    `DeleteContours` and `DeleteAppliedArea` grew slightly from the
    intent-seeding pattern).
  - `MainViewModel.cs` = **5,160 lines** (+10 from C; D6 rewrote the
    SelectedTrack setter block).
  - Total across 23 partials: **14,036 lines** (−100 from Phase C
    close, −91 from Phase B, and **−100 from the Phase A baseline
    of 14,136**).
  The net −100-line reduction lines up with D9's dead-code deletion
  dominating the diff; D4–D7 were roughly size-neutral (intent
  plumbing balances out direct-write removals). Phase D hits the
  parent-plan intent ("drops measurably"). Phase E / F will
  re-measure if they touch VM files.

---

### TMP-008 — Manual U-turn does not execute when triggered

**Status:** Resolved as "works as designed, AgOpen-parity deferred"
(Phase C C1 investigation, 2026-04-19). Moved to §5 Resolved.

---

### TMP-009 — Fix-to-fix heading behavior change activates in Phase B

- **Status:** Open
- **Raised in:** Phase B C2 planning
- **Decide by:** Phase B C6 (acceptance smoke test) — if the behavior
  change is noticeable on real hardware, decide whether to keep,
  temporarily disable, or back out.
- **Source:** `Shared/AgValoniaGPS.Services/NmeaParserService.cs:209` —
  `ProcessHeading(gpsHeading, speedMs, gpsData.CurrentPosition.Easting, gpsData.CurrentPosition.Northing)`

**Why parked.** Today `NmeaParserService.ProcessHeading` is called with
`gpsData.CurrentPosition.Easting` / `Northing` that are both **0.0** at
that moment — UTM conversion runs later in `GpsService.TransformAntennaToPivot`,
after `ParsePANDA` returns. That means `CalculateFixToFixHeading` has
always computed `distance = sqrt(0² + 0²) = 0 < Connections.FixToFixDistance`
and returned `-1`, i.e. the fix-to-fix branch never fired in production.

When Phase B C2 moves `ProcessHeading` into the cycle worker, it will
be called with real local easting/northing from the post-conversion
stage. Fix-to-fix heading starts actually working. Users in single-GPS
mode (or dual-GPS below the switch speed) will see a heading source
they've never seen before:
- Single-GPS users: now get fix-to-fix heading above `Connections.MinGpsStep`
  instead of the raw NMEA heading. Likely more stable for most cases.
- Dual-GPS users at low speed: now get fix-to-fix instead of dual-antenna
  heading. Could swing the tractor if dual-antenna was more accurate.

**What the decision is.** After Phase B C6 smoke test, confirm whether
the activated fix-to-fix behavior is an improvement, a regression, or
imperceptible. If regression, options:
1. Disable fix-to-fix by defaulting `Connections.MinGpsStep` to a
   value that never triggers (matches current dead-code behavior).
2. Leave dual-GPS dominant at all speeds (remove the DualSwitchSpeed
   branch) — tracks legacy behavior.
3. Keep as-is — the code did what the config said it should, which
   is now honored.

**Review log.**
- 2026-04-19 — Discovered while planning Phase B C2's fusion extraction.
  Parker decided to port `ProcessHeading` faithfully (with correct
  inputs) rather than preserve the 0,0 bug. The cycle worker passes
  real local easting/northing. Smoke test reveals behavior impact.
- 2026-04-19 (Phase B close) — Phase B smoke test on simulated GPS
  driving showed no visible regression. **Real-hardware verification
  pending.** Open until a user drives with real GPS and confirms
  heading behavior. If regression, switch `Connections.MinGpsStep`
  default to effectively-disabled or refactor `GpsHeadingFusionService`
  to gate fix-to-fix behind a config toggle.

### TMP-010 — UI freeze synchronised with tooltip balloon show

- **Status:** Open, not blocking Phase C.
- **Raised in:** Phase C C6 smoke test, 2026-04-20.
- **Decide by:** Post-threading-migration — investigated separately from
  the threading work since it is not caused by it.
- **Tracked as:** [Issue #267 — UI stalls for a fraction of a second on
  tooltip balloon appearance](https://github.com/AgOpenGPS-Official/AgValoniaGPS/issues/267)

**Symptom.** Mouse hovers over any button (sidebar, bottom bar, or
floating panel); after the normal tooltip delay, the balloon appears
and the entire UI stalls for a fraction of a second at the exact moment
the balloon shows. Dwell time before the balloon is smooth — only the
balloon-appearance frame freezes. Reproducible on every button,
independent of autosteer state, field open state, or session age
(happens on fresh launch).

**Why parked.** Tested on `develop` (commit `951865f`, pre-Phase-A)
using a git worktree on 2026-04-20. Freeze reproduces there too,
confirming this is a pre-existing Avalonia / tooltip-popup interaction,
not a threading-migration regression. Phase C adds more work to
`ApplyGpsCycleResult` (snapshot mirror) and more allocations per cycle
(TickContext, Position.With), which may make the freeze more or less
pronounced, but it is not the root cause — a pristine develop binary
with zero Phase-C code on the UI thread exhibits the same stall.

**Possible next-step investigations** (not to be done in-phase):
1. Run Avalonia with `LogRenderTiming` and `LogSendStateFrequency`
   diag flags while hovering; check whether the freeze is GC-induced
   (Gen2) or render/layout-induced (popup-root creation cost).
2. Compare against Avalonia 12 behavior — see `reference_avalonia12.md`
   in auto-memory; tooltip popup-root may be cheaper there.
3. Profile the popup-creation path with a managed profiler to see
   whether the cost is in resource resolution, font loading, or
   style application.

**Review log.**
- 2026-04-20 — Reported during Phase C C6 smoke test. Initial
  hypothesis: `_mapService.Set*` unconditional calls in the YouTurn
  snapshot mirror flooding `SendStateToHandler`. Added reference-
  equality gating speculatively; freeze persisted. Tested `develop`
  baseline via worktree — freeze reproduces there. Hypothesis
  falsified; gating change reverted. Parked for post-migration
  investigation.
- 2026-04-20 — Filed as issue #267 so the investigation has a
  tracker outside the parking lot.

---

## 5. Resolved items

### TMP-001 — Snapshot identity vs equality

- **Status:** Resolved (Phase C close, 2026-04-20)
- **Resolution:** Option (a) — reuse list references. The
  `YouTurnStateMachine` swaps `TurnPath` / `NextTrack` refs only at
  turn-start (creation) and turn-end (clear / complete). Between
  those points, every cycle's snapshot carries the same list
  references. `ApplyGpsCycleResult` assigns via `ObservableObject`
  setters which compare refs before firing `PropertyChanged`, so
  steady-state ticks don't churn UI bindings. No explicit
  value-equality code needed on the UI thread. Verified on the
  Bing Test field across auto + manual U-turns.
- **Decided in:** Phase C plan §2.1; validated by smoke tests across
  C4/C5/C6/C7
- **Closing PR:** #259 (Phase C commit range `25c2042..f50ef51`)

### TMP-003 — Phase B unified service name

- **Status:** Resolved (Phase B close, 2026-04-19)
- **Resolution:** Keeps the name `GpsPipelineService`. Phase B expanded
  its responsibilities (absorbed AutoSteer cycle work, fusion, and
  fix-quality validation) but "pipeline orchestration" still fits. Any
  cosmetic rename deferred until post-Phase-F if still desired.
- **Decided in:** Phase B plan §2.1
- **Closing PR:** #259 (Phase B commit range `5d6bccd..d04bfc6`)

### TMP-004 — `UdpGpsQueue` introduction

- **Status:** Resolved (Phase B close, 2026-04-19)
- **Resolution:** Keep the existing `GpsDataUpdated` event + `Task.Run`
  handoff. Real-hardware smoke test on the AiO board over UDP showed
  autosteer engaged and held through the full drive, 60 FPS avg, no
  cycle back-pressure drops, and latency display updating at GPS
  cadence (after the rejection-gate fix in `9fe4dc9`). An explicit
  `Channel<Position>` would be overhead with no observable benefit.
  Reopen only if a future phase's cycle work lengthens enough to miss
  GPS ticks — the §0 invariant and existing `Interlocked` back-pressure
  remain in place either way.
- **Decided in:** Phase B plan §2.2; validated by real-hardware smoke test
- **Closing PR:** #259 (Phase B commit `9fe4dc9`)

### TMP-008 — Manual U-turn does not execute when triggered

- **Status:** Resolved as "works as designed, AgOpen parity deferred"
  (Phase C C1 investigation, 2026-04-19)
- **Resolution:** Original Phase A smoke-test symptom ("tractor didn't
  execute the turn") was misread. The tractor **does** execute the
  manual U-turn; the visual offset the user reported came from clicking
  the manual-trigger button before the tractor had settled on the
  magenta pass line — the generated path anchors at the tractor's
  current position, producing entry/exit legs offset from the visible
  tracks by the tractor's cross-track error at click time.
  Diagnostic logging (commit stripped in Phase C C1) confirmed path
  coordinates are mathematically consistent within 0.001m.

  Reference behavior in AgOpenGPS-original differs in two ways: (a) it
  plots an **immediate** Dubins-like arc at the tractor's current
  position rather than a headland-based entry-arc-exit, and (b) the
  guidance line visually shifts to the new post-turn pass. That's not
  a bug in AgValoniaGPS — it's a missing feature. Tracked as a real
  feature request, not a threading-migration blocker:
  - [Issue #260 — Manual U-turn: immediate turn at tractor position
    (AgOpen parity)](https://github.com/AgOpenGPS-Official/AgValoniaGPS/issues/260)
    (GitHub project "AgValoniaGPS", Planning column)
  - [Issue #261 — Free-drive: guidance line follows the tractor](https://github.com/AgOpenGPS-Official/AgValoniaGPS/issues/261)
    (same project, Planning column)

  Phase C C2 proceeds on the existing manual-U-turn behavior; the
  threading migration is independent of which path-generation
  algorithm the manual trigger uses.
- **Investigated in:** Phase C C1 (2026-04-19)
- **Closing PR:** #259 (diagnostic logs stripped, no code fix)

---

## 6. Cross-references

- [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) — the
  parent plan; every item here originates from a section there or from
  a per-phase plan.
- [THREADING_PHASE_A_PLAN.md](THREADING_PHASE_A_PLAN.md) — Phase A
  implementation plan; its §8 points back to this document.
- [threading_model.svg](threading_model.svg) — target threading model;
  names referenced here should match names shown there.
