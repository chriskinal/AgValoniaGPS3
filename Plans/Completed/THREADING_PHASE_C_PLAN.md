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

# Threading Phase C — YouTurn End-to-End

**Parent plan:** [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) (§5 Phase C)
**Branch:** `feature/threading-phase-a` (shared across phases A–F per PR #259)
**Scope:** YouTurn state machine migrates from UI thread to the cycle worker.

Phase A added `YouTurnWorkingState`, `YouTurnSnapshot`, and `IPipelineIntents`
as inert scaffolding. Phase B unified the GPS pipeline so the cycle
worker does the real work. Phase C finally consumes Phase A's scaffolding:
the state machine runs on the cycle worker mutating a POCO; the UI
thread only mirrors the published snapshot via `ApplyGpsCycleResult`.

Addresses current-state problems **1, 4, and 5** from
[`threading_model_current.svg`](threading_model_current.svg). This is
the **proof-of-pattern phase** — if it works, Phases D, E, F become
mechanical applications of the same template.

---

## 1. Goal

At the end of Phase C:

1. **TMP-008 fixed.** Manual U-turn actually executes the turn path
   (not just generate it). Phase C cannot verify its migration doesn't
   regress behavior without a working baseline. Resolving this is the
   first commit.
2. **`YouTurnStateMachine.Tick` and `TriggerManual` take
   `YouTurnWorkingState`**, not `YouTurnState`. No observable state
   type appears in the state-machine signatures.
3. **The cycle worker owns the tick.** `MainViewModel.GpsHandling.cs`
   no longer calls `TickYouTurnStateMachine`. `GpsPipelineService.ProcessCycle`
   runs the tick on the background worker.
4. **`ApplyGpsCycleResult` is the sole writer of `State.YouTurn`.**
   It consumes `GpsCycleResult.YouTurn` (populated for the first time
   this phase) and mirrors the snapshot onto the observable state on
   the UI thread. Every `State.YouTurn.*` assignment outside this
   method is removed.
5. **UI commands push intents.** `TriggerManualYouTurnLeft/Right` and
   `ClearYouTurnState` call `IPipelineIntents.Request*`; the cycle
   worker drains and reacts on the next tick.
6. **Flat YouTurn fields on `GpsCycleResult` deleted.** `IsInYouTurn`,
   `YouTurnTriggered`, `YouTurnCompleted` are subsumed by
   `YouTurnSnapshot` (TMP-005 partial resolution).
7. **Map effects flow through the snapshot.** `_mapService.SetYouTurnPath`,
   `SetNextTrack`, `SetIsInYouTurn` move out of the state-machine
   effects path into `ApplyGpsCycleResult`'s snapshot handling.
8. **`MainViewModel.YouTurn.cs` shrinks to under 100 lines.** Target
   from Phase A acceptance (TMP-007 baseline was 206 lines).
9. All existing tests pass plus new unit/integration coverage.
   Smoke test drives a full field cycle (field open → pass → auto
   U-turn → next pass → manual U-turn → close field) without
   regression on simulator and real AiO hardware.

After Phase C, the YouTurn state machine never mutates an
`ObservableObject`. PropertyChanged fires exactly once per cycle
per property that changed, from the `ApplyGpsCycleResult` call on the
UI thread.

---

## 2. Decisions locked before Phase C starts

### 2.1 TMP-001 — Snapshot identity vs equality → **reuse list references**

The cycle worker's `YouTurnWorkingState` holds `TurnPath` and
`SnakeSequence` as `List<T>`. When the state machine mutates the
working state, it replaces the list reference only on turn start /
completion (once per turn, not per tick). During turn execution the
list reference is stable.

`YouTurnSnapshot` points at the same list reference (via
`IReadOnlyList<T>`). When `ApplyGpsCycleResult` does
`State.YouTurn.TurnPath = snapshot.TurnPath`,
`ObservableObject.SetProperty` uses `EqualityComparer<T>.Default` which
for `List<T>` is reference equality. Unchanged references elide
`PropertyChanged`. Changed references (on turn start / end) fire
`PropertyChanged` normally.

No explicit value-equality code is required. The behavior emerges from
reuse + the default equality contract. This closes TMP-001.

### 2.2 `YouTurnEffects` migration — **dissolve into snapshot + implicit map sync**

Pre-Phase-C, the state machine returns a `YouTurnEffects` record with
five fields (`StatusMessage`, `SyncTurnPathToMap`, `SyncNextTrackToMap`,
`IsInYouTurnMapFlag`, `TurnCompleted`). The UI thread's `ApplyEffects`
handler reads those flags and calls `_mapService.*` / sets
`StatusMessage` accordingly.

Post-Phase-C, the state machine's return value is not directly
consumed by the UI. Instead:

- `StatusMessage` → goes into `GpsCycleResult.StatusMessage` (existing
  field) when the state machine produces one.
- `SyncTurnPathToMap` / `SyncNextTrackToMap` → deleted. Every
  `ApplyGpsCycleResult` call pushes `snapshot.TurnPath` and
  `snapshot.NextTrack` to `_mapService` unconditionally (idempotent;
  no-op when values didn't change because map service can short-circuit
  on identity).
- `IsInYouTurnMapFlag` → deleted. `ApplyGpsCycleResult` pushes
  `snapshot.IsExecuting` to `_mapService.SetIsInYouTurn` every cycle.
- `TurnCompleted` → becomes a new flag on `YouTurnSnapshot`
  (`JustCompleted`, one-cycle-true). `ApplyGpsCycleResult` triggers
  guidance reset when observed.

`YouTurnEffects` itself is retired. The state machine's return value
becomes `void` (it mutates the working state in place; the cycle
worker reads the working state after the call).

### 2.3 Execution ownership — state machine still mutates in place

The state machine keeps its "write to the passed-in parameter" style.
Pre-Phase-C: takes `YouTurnState` (observable), writes properties,
`SetProperty` fires `PropertyChanged` on the UI thread.

Post-Phase-C: takes `YouTurnWorkingState` (POCO), writes fields, no
events fire. The cycle worker, after the tick, builds a fresh
`YouTurnSnapshot` from the working state and attaches it to
`GpsCycleResult`.

No architectural change to the state machine's internal algorithm.

---

## 3. Out of scope for Phase C

Explicitly *not* done — each belongs to a later phase or a separate
follow-up:

- **Guidance state migration** (Phase D). `GuidanceState` still gets
  mutated directly by several services. Phase D addresses.
- **Removing flat Guidance fields** from `GpsCycleResult`. Phase D.
- **`FieldState` audit** (Phase E).
- **NTRIP / UDP connection state** (Phase F).
- **Enforcement mechanism** (TMP-006). Optional, latest at Phase F.
- **Map service's idempotency** check — current `_mapService.SetYouTurnPath(list)`
  may allocate or do work even when unchanged. Out of scope; if it
  shows up as a perf issue, separate ticket.
- **Moving `IsTrackOnBoundary` / `DistanceToBoundary` out of
  `MainViewModel.YouTurn.cs`** — they're shared utilities, not
  YouTurn-specific. A cleanup that would further shrink the file, but
  it's orthogonal and can wait.

---

## 4. Current-state anchors

Verified before this plan was drafted.

| Reference | File:line | Notes |
|---|---|---|
| Tick call | `MainViewModel.GpsHandling.cs:238` | `TickYouTurnStateMachine(guidancePos)` — moves to cycle worker in C4 |
| Tick guards | `GpsHandling.cs:236` | `IsYouTurnEnabled && _currentHeadlandLine != null && _currentHeadlandLine.Count >= 3` — migrate with the tick |
| Counter increment | `GpsHandling.cs:234` | `State.YouTurn.YouTurnCounter++` before tick. Cycle worker takes over |
| State push to pipeline | `GpsHandling.cs:242–243` | `_gpsPipelineService.SetYouTurnState(...)` — vestigial after C4; cycle owns the state directly |
| `Tick` / `TriggerManual` / `ClearState` | `Shared/AgValoniaGPS.Services/YouTurn/YouTurnStateMachine.cs:89,237,311` | public surface to migrate |
| `YouTurnEffects` | `YouTurnStateMachine.cs:587–612` | retires per §2.2 |
| `ApplyEffects` | `MainViewModel.YouTurn.cs:177–203` | retires per §2.2 |
| `TriggerManual*` commands | `MainViewModel.Commands.Track.cs:75, 863–864` | become intent pushes in C6 |
| `ClearYouTurnState` call sites | `MainViewModel.YouTurn.cs:79`, plus track-deselect and field-close paths | become intent push in C7 |
| Current `ApplyGpsCycleResult` YouTurn handling | `MainViewModel.ApplyResults.cs:57–60` | flat fields read but **NOT assigned to `State.YouTurn.*`** — C5 adds those assignments |
| Working state types | `Shared/AgValoniaGPS.Models/Pipeline/YouTurnWorkingState.cs`, `YouTurnSnapshot.cs` | from Phase A, ready to consume |
| Intent API | `Shared/AgValoniaGPS.Models/Pipeline/IPipelineIntents.cs` | from Phase A; `RequestManualYouTurn(bool)` and `RequestClearYouTurn()` already defined |

---

## 5. Commit-by-commit plan

Nine commits. Each independently reviewable, each leaves the app
working, each commit's smoke test passes before moving on.

### Commit 1 — Investigate TMP-008 (no code fix)

**Goal:** Establish whether manual U-turn is a real blocker for the
thread migration. Spoiler (filled in after investigation): it isn't.

**Outcome of the investigation (2026-04-19):**
- Diagnostic logging (`[C1-DBG:...]` prefixes, commit-stripped after
  the repro) in `ProcessCycle`, `TriggerManual`, and
  `CreatePathAndSync` showed that:
  - Path coordinates are internally consistent within 0.001m
  - Manual U-turn path is anchored at the tractor's current position;
    when the tractor is off the magenta pass line at click time, the
    path is offset from the displayed tracks by exactly the tractor's
    cross-track error at that moment
  - Tracks, tractor, and U-turn path are all rendered in the same
    coordinate frame — no frame mismatch
- Compared to AgOpenGPS-original: the reference implementation plots
  an **immediate** Dubins-like arc starting at the tractor's position
  (no headland-based entry-arc-exit). The user's original TMP-008
  report ("the tractor doesn't execute the turn") was a misread of
  AgOpen parity behavior they expected — our manual U-turn does
  execute, it just follows a different model.
- Working as designed. Not a bug; a missing feature.

**Result:**
- TMP-008 closed as "works as designed, AgOpen parity deferred"
- AgOpen-style immediate manual U-turn → GitHub issue #260 (Project
  "AgValoniaGPS", Planning column)
- Free-drive line-follows-tractor → GitHub issue #261 (same)
- Diagnostic logging stripped (this commit)
- Phase C C2+ proceeds unchanged

**Ships:** the diagnostic-log removal + parking-lot TMP-008 resolution
+ this plan amendment. No runtime behavior change. The manual U-turn
model can be changed independently of threading in a future commit
driven by issue #260.

### Commit 2 — Cycle worker owns `YouTurnWorkingState` instance

**Goal:** Scaffold the cycle-worker storage. No behavior change.

**Modifies:**
- `GpsPipelineService` — add `private readonly YouTurnWorkingState _youTurn = new();`
  field. Not referenced outside this line yet.

**Verification:** Solution builds green; all tests pass; smoke test
unchanged from C1.

### Commit 3 — State machine takes `YouTurnWorkingState`

**Goal:** Migrate `Tick`, `TriggerManual`, `ClearState` to take
`YouTurnWorkingState` instead of `YouTurnState`. Internal implementation
stays identical (POCO has the same fields as the observable type).
Return type becomes `void` — per §2.2 the effects surface dissolves.

**Modifies:**
- `YouTurnStateMachine.cs` — signature changes. `turn.IsExecuting = …`
  etc. work identically because field names match.
- `MainViewModel.YouTurn.cs` — `TickYouTurnStateMachine` and
  `TriggerManualYouTurn` become *bridges*: copy `State.YouTurn` →
  temp `YouTurnWorkingState`, call state machine, copy temp →
  `State.YouTurn`. Ugly but temporary (C4 removes).
- Tests that exercise the state machine — update to pass
  `YouTurnWorkingState`.

**Does not modify:** Cycle worker. Call site stays on UI thread. Real
migration happens in C4.

**Verification:** Solution builds; tests pass; smoke test passes —
behavior should be identical because the bridge copies state both ways.

**Risk:** The bridge must copy every field both ways. The shape-mirror
test from Phase A locks the field list, so bridge completeness is
checkable at code-review time.

### Commit 4 — Move the tick call to the cycle worker

**Goal:** `MainViewModel.GpsHandling.cs:238`'s `TickYouTurnStateMachine`
call moves into `GpsPipelineService.ProcessCycle`. The UI thread stops
running state-machine code.

**Modifies:**
- `GpsPipelineService.ProcessCycle` — build `TickContext` from cycle-owned
  data; drain intents (manual-trigger, clear-state); call
  `_youTurnStateMachine.Tick(ctx, ..., _youTurn)` and
  `_youTurnStateMachine.TriggerManual(...)` when intents say so.
- `MainViewModel.GpsHandling.cs` — delete lines 234–243 (counter
  increment, tick call, state push).
- `MainViewModel.YouTurn.cs` — `TickYouTurnStateMachine`,
  `BuildTickContext`, `GetCurrentGpsPosition` removed (cycle builds
  its own context). `ApplyEffects` removed (no effects to apply).

**Open question at this point:** state machine writes to
`_youTurn` (cycle-owned working state), but the UI still reads
`State.YouTurn`. Until C5 wires the snapshot, the UI is stale. Smoke
test may show frozen YouTurn indicators. Acceptable for one commit —
C5 follows immediately.

**Verification:** Solution builds; tests pass. Smoke test: YouTurn
indicators (turn path overlay, in-turn flag) freeze — this is
expected because C5 hasn't landed yet. Spot-check: no exceptions,
cycle runs, no dispatcher-thread errors from the state machine
(because it no longer touches `ObservableObject`).

**Risk:** Interim smoke test shows broken UI. Document in commit
message so a reviewer doesn't revert thinking it's a regression.

### Commit 5 — Emit and consume `YouTurnSnapshot`

**Goal:** `GpsCycleResult.YouTurn` finally populated by the cycle
worker. `ApplyGpsCycleResult` mirrors it to `State.YouTurn` and pushes
map effects.

**Modifies:**
- `GpsPipelineService.ProcessCycle` — after state-machine calls,
  build `YouTurnSnapshot` from `_youTurn`, assign to
  `GpsCycleResult.YouTurn`.
- `YouTurnSnapshot` — add `JustCompleted` bool field per §2.2.
- `ApplyGpsCycleResult` — new block handling `result.YouTurn`:
  - Mirror all fields to `State.YouTurn.*` (properties fire
    `PropertyChanged` normally on UI thread).
  - Call `_mapService.SetYouTurnPath(snapshot.TurnPath as List<Vec3>)`.
  - Call `_mapService.SetNextTrack(snapshot.NextTrack)`.
  - Call `_mapService.SetIsInYouTurn(snapshot.IsExecuting)`.
  - If `snapshot.JustCompleted`: reset guidance state for post-turn pass.
  - Set `StatusMessage` from `result.StatusMessage` (already a
    top-level field).

**Does not modify:** state machine signatures (C3 did), tick location
(C4 did).

**Verification:** Solution builds; tests pass. Smoke test: YouTurn
indicators work again — turn paths render, in-turn flag toggles,
status messages appear.

### Commit 6 — Migrate manual-trigger commands to intents

**Goal:** `TriggerManualYouTurnLeft/Right` stop calling the state
machine. They push an intent via `IPipelineIntents.RequestManualYouTurn(bool)`.
The cycle worker drains and calls `TriggerManual` on the next tick.

**Modifies:**
- `MainViewModel.YouTurn.cs` — `TriggerManualYouTurn(bool)` becomes
  `_intents.RequestManualYouTurn(turnLeft)` and nothing else.
  `TriggerManualYouTurnLeft` / `-Right` remain as thin wrappers.
- `GpsPipelineService.ProcessCycle` — the intent drain (already there
  from Phase A, but discarded) now calls
  `_youTurnStateMachine.TriggerManual(turnLeft, ...)` on the cycle
  worker when `batch.ManualYouTurn` is non-null. Needs
  `isAutoSteerEngaged` / current track access (cycle already has both).

**Does not modify:** command wiring in `Commands.Track.cs` — the
buttons still call the same MVM methods; those methods just do less.

**TMP-002 resolution for YouTurn commands:** 100ms intent latency is
acceptable for manual U-turn — humans don't notice sub-100ms and the
turn-path computation is the visible feedback. No carve-out needed.
Commit message notes this explicitly.

**Verification:** Solution builds; tests pass. Smoke test: press
manual-turn button; tractor enters turn path within one GPS cycle
(~100ms). Compare latency to pre-C6 behavior — should feel identical.

**Risk:** The intent queue was last-wins (single slot). If the user
double-clicks manual-turn rapidly, the second click overrides the
first. Behaviorally equivalent to pre-C6 where the second click hit
the `IsExecuting` guard and printed "U-turn already in progress."
Acceptable.

### Commit 7 — Migrate `ClearYouTurnState` to an intent

**Goal:** Field-close and track-deselection paths push a clear-state
intent instead of calling the state machine directly.

**Modifies:**
- `MainViewModel.YouTurn.cs` — `ClearYouTurnState()` method body
  becomes `_intents.RequestClearYouTurn();`. Map-service calls removed
  (they happen in `ApplyGpsCycleResult` on the next cycle, which sees
  null path / null track after state-machine reset).
- `GpsPipelineService.ProcessCycle` — intent drain calls
  `YouTurnStateMachine.ClearState(_youTurn)` when `batch.ClearYouTurn`.

**Does not modify:** callers of `ClearYouTurnState()` — they continue
to call the MVM method, which now just requests an intent.

**TMP-002 resolution for ClearYouTurnState:** same as C6 — 100ms
latency is imperceptible; user-facing result (no turn path on map) is
the observable effect.

**Verification:** Smoke test: open field, trigger turn, deselect
track, verify turn path clears from map. Close field, reopen, verify
clean state.

### Commit 8 — Remove flat YouTurn fields from `GpsCycleResult`

**Goal:** TMP-005 partial resolution. `IsInYouTurn`, `YouTurnTriggered`,
`YouTurnCompleted` removed. All readers switch to
`result.YouTurn?.IsExecuting` / `IsTriggered` / `JustCompleted`.

**Modifies:**
- `GpsCycleResult.cs` — delete three fields.
- `ApplyGpsCycleResult` — readers switched to snapshot.
- `GpsPipelineService.ProcessCycle` — cycle no longer sets flat
  fields.
- Any other consumer of these fields — grep during implementation.

**Verification:** Solution builds; tests pass; smoke test unchanged
from C7. The `grep -rn "result\.IsInYouTurn\|YouTurnTriggered\|YouTurnCompleted"` returns zero matches in `Shared/`.

### Commit 9 — Tests, line-count check, parking-lot review

**Goal:** Lock the new invariants and close Phase C's open items.

**Adds / modifies:**
- `Tests/AgValoniaGPS.Services.Tests/Pipeline/YouTurnCycleTests.cs` —
  unit tests around the cycle-worker's YouTurn handling:
  - `Tick_mutates_working_state_not_observable_state` — confirms the
    state machine's signature migration.
  - `Intent_drain_triggers_manual_turn_on_next_cycle` — locks the
    intent flow.
  - `YouTurnSnapshot_emitted_when_state_changes` — verifies the
    cycle fills the snapshot.
- `Tests/AgValoniaGPS.UI.Tests/ApplyGpsCycleResultTests.cs` (or
  extend existing) — `State.YouTurn` gets written from
  `result.YouTurn` on the UI thread; `_mapService.SetYouTurnPath`
  called with the snapshot's path.
- `Plans/THREADING_MIGRATION_PARKING_LOT.md` — Phase C close review:
  resolve TMP-001, TMP-008; move to §5. Update TMP-002 for YouTurn
  (Phase D still owes Guidance commands). Update TMP-005 (YouTurn
  done; Guidance pending). TMP-007 re-measure
  `MainViewModel.YouTurn.cs` — assert under 100 lines.

**Verification:** All tests pass. Smoke test on simulator + real AiO
hardware: full field cycle (field open → track follow → auto U-turn →
next pass → manual U-turn → close field) with no exceptions, FPS ≥
24 floor, latency display continues updating through turns.

---

## 6. Acceptance criteria (Phase C-wide)

- [ ] `dotnet build AgValoniaGPS.sln -p:DesktopOnly=true` green
- [ ] `dotnet test Tests/` passes; count increases by C9 additions
- [ ] `grep "State\.YouTurn\." Shared/AgValoniaGPS.ViewModels/ Shared/AgValoniaGPS.Services/`
      returns **only** reads outside `ApplyGpsCycleResult`. Zero writes
      outside the apply-results path.
- [ ] `MainViewModel.YouTurn.cs` line count < 100 (Phase A TMP-007
      acceptance target)
- [ ] `YouTurnStateMachine` public methods take `YouTurnWorkingState`,
      never `YouTurnState`
- [ ] `GpsCycleResult` has no flat YouTurn fields; `YouTurnSnapshot`
      is the sole source
- [ ] TMP-008 regression test passes
- [ ] Smoke test: simulator drive + real AiO hardware, full field
      cycle with auto U-turn AND manual U-turn, no exceptions, FPS ≥
      24, latency updates throughout
- [ ] Parking-lot review: TMP-001 and TMP-008 move to Resolved; other
      items updated with dated Phase-C-close notes

---

## 7. Risk register

### 7.1 TMP-008 investigation outcome (HIGH until C1 lands)

**What could go wrong.** Manual U-turn doesn't execute is the phase's
gating bug. If the root cause turns out to require significant
refactor of the guidance follow path (not just a simple state bug),
Phase C's scope balloons.

**Mitigation.** Phase C C1 is specifically allocated to investigation
with scope to uncover the cause. If it's large, pause Phase C and
scope the fix separately before continuing C2+.

### 7.2 Interim UI freeze between C4 and C5 (MEDIUM)

**What could go wrong.** C4 moves the tick off the UI thread and stops
writing `State.YouTurn`. C5 adds the snapshot → state mirror. Between
the two commits, YouTurn UI indicators freeze. If someone lands C4 and
merges to develop before C5 is ready, develop ships broken.

**Mitigation.** Commit C4 + C5 together in practice (one logical
change, two commits for reviewability), or gate C4's merge on C5 being
ready. Commit message on C4 is explicit that it's half-done.

### 7.3 Map-service idempotency (LOW-MEDIUM)

**What could go wrong.** `ApplyGpsCycleResult` now pushes
`snapshot.TurnPath` to `_mapService.SetYouTurnPath` on every cycle
(~10Hz). If `SetYouTurnPath` does work unconditionally — allocates, redraws,
invalidates tiles — it becomes a per-tick cost multiplier.

**Mitigation.** During C5 implementation, verify `SetYouTurnPath`
either short-circuits on reference equality or is cheap. If not, add
a C5b commit wrapping with a "last-applied" check.

### 7.4 TMP-001 wrong decision (LOW)

**What could go wrong.** §2.1 says "reuse list references" avoids
`PropertyChanged` noise. If the state machine internally mutates the
list in place instead of replacing (e.g., `turn.TurnPath.Add(...)`),
the reference stays the same but contents change, and `SetProperty`
won't fire — bindings see stale data.

**Mitigation.** Grep during C3: any `.Add(` / `.RemoveAt(` /
`.Clear()` on `turn.TurnPath` means in-place mutation. Audit and
confirm the state machine only replaces the list reference. If it
mutates in place, switch to copy-on-snapshot (§2.1 option b).

### 7.5 Scope creep into Phase D (LOW)

**What could go wrong.** The temptation to also migrate
`GuidanceState` while editing cycle-worker code. That's Phase D.

**Mitigation.** Phase C acceptance explicitly names flat **YouTurn**
fields for removal, not Guidance. If C8's diff touches Guidance
fields, reviewer flags it.

---

## 8. Deferred decisions (pointer to parking lot)

All deferred items live in
[`THREADING_MIGRATION_PARKING_LOT.md`](THREADING_MIGRATION_PARKING_LOT.md).

Phase C resolves **TMP-001** (snapshot identity — reuse list refs)
and **TMP-008** (manual U-turn bug, if fix ships in C1). Partially
resolves **TMP-002** (YouTurn commands through intents) and **TMP-005**
(flat YouTurn fields removed).

Still-parked after Phase C close: remaining parts of TMP-002 (Guidance
commands in Phase D), TMP-005 (Guidance flat fields in Phase D),
TMP-006 (enforcement mechanism), TMP-007 (line-count baseline,
re-measured at close), TMP-009 (fix-to-fix heading activation
verification — Phase D's real-hardware test could cover).

End-of-phase parking-lot review is mandatory per Phase C acceptance.

---

## 9. Follow-up phases (reminder only)

- **Phase D — Guidance state migration.** Same template, applied to
  `GuidanceState`. Mechanical once Phase C proves the pattern.
- **Phase E — `FieldState` audit.** Mostly verification.
- **Phase F — `ConnectionState` for NTRIP / UDP.**

Phase C hand-off to Phase D is clean when: YouTurn state machine runs
on the cycle worker, manual/clear intents work end-to-end, `State.YouTurn`
has exactly one writer (`ApplyGpsCycleResult`), and the smoke test
passes on both simulator and real hardware.
