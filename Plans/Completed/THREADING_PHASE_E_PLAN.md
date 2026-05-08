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

# Threading Phase E — `FieldState` Audit

**Parent plan:** [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) (§5 Phase E)
**Branch:** `feature/threading-phase-a` (shared across phases A–F per PR #259)
**Scope:** Audit `FieldState` read/write boundaries. Fix the one remaining
cycle-thread write to `ObservableObject`. Delete dead UI-thread headland-
proximity code.

The parent plan predicted this phase would be "mostly verification —
confirm no service writes to `FieldState` from a background thread,
document the read/write boundaries. Likely small or no-op." That
prediction holds. The grep

```
grep -rn 'State\.\w+\.\w+\s*=\|_appState\.\w+\.\w+\s*=' Shared/AgValoniaGPS.Services
```

returns exactly **one** match in all of `Shared/AgValoniaGPS.Services`:

```
Shared/AgValoniaGPS.Services/Pipeline/GpsPipelineService.cs:383
    _appState.Field.LocalPlane = new LocalPlane(...)
```

That single write is the one structural fix Phase E has to ship.
Everything else is cleanup and documentation.

---

## 1. Goal

At the end of Phase E:

1. **Zero cycle-thread writes to any `ObservableObject`.** The
   `_appState.Field.LocalPlane` auto-create in `GpsPipelineService`
   moves into a cycle-thread POCO cache + a `GpsCycleResult` emission
   path. `ApplyGpsCycleResult` writes the observable on the UI thread,
   same pattern as YouTurn/Guidance snapshots.
2. **Dead UI-thread headland-proximity code deleted.**
   `MainViewModel.GpsHandling.UpdateHeadlandProximity` + its
   `_headlandDetector` field are removed — the cycle's
   `ComputeHeadlandProximity` has been authoritative since Phase B,
   and the UI consumer reads from `GpsCycleResult.HeadlandProximity*`
   via `ApplyGpsCycleResult`. The duplicate UI-thread path was left
   in place by oversight during Phase B.
3. **FieldState read/write boundaries documented.** A comment block
   on `FieldState` lists, for each property, which thread writes and
   which reads it. Forces future work to declare a writer explicitly.
4. **Structural guard extended.** The existing
   `IGpsPipelineService_has_no_direct_*_writethrough_methods` tests
   (YouTurn from Phase C C9, Guidance from Phase D D10) gain a
   counterpart that covers `LocalPlane`, `Boundary`, and `FieldState`
   parameters to ensure no future cross-thread writer slips in.
5. All existing tests pass plus new structural coverage for the
   LocalPlane emission path. Smoke test verifies no regression on the
   simulator-bootstrapping case (first GPS fix with no field open).

After Phase E, the §0 invariant is satisfied for every
`ObservableObject` in `ApplicationState`. Phase F (NTRIP/UDP
`ConnectionState`) is the last remaining migration.

---

## 2. Decisions locked before Phase E starts

### 2.1 LocalPlane auto-create — **cycle caches, UI writes**

Three options considered:

- **(a) Move auto-create fully to UI thread.** The first GPS cycle
  posts a "first fix" event; UI responds by creating the LocalPlane
  and pushing it back via a setter. The cycle's coordinate conversion
  for that *first* cycle would use uncoverted coords — a visible glitch
  for one frame.
- **(b) Cycle-owned POCO cache, emit via `GpsCycleResult`.** Cycle
  creates a local-only `LocalPlane` reference on first fix, uses it
  for coord conversion immediately, and ships it in the cycle result.
  UI's `ApplyGpsCycleResult` mirrors it onto `State.Field.LocalPlane`
  on the UI thread (same pattern as the YouTurn/Guidance snapshots).
  No visible glitch; one cycle of delay on the observable update is
  imperceptible.
- **(c) Keep cross-thread write.** Lock-protected on cycle. Violates
  §0 — every `SetProperty` fires `PropertyChanged` on the cycle
  thread, and any binding listener runs there. Not an option.

**Decision:** (b). Mirrors the pattern established in Phase C/D.

### 2.2 Scope of the structural guard

The Phase C/D guards test `IGpsPipelineService` for `Set*`/`Push*`/
`Apply*` methods taking `YouTurnWorkingState`/`YouTurnSnapshot` (and
the Guidance twin). Phase E extends the scan to include `LocalPlane`
as a parameter type — flagging any future writer that accepts a
LocalPlane from the UI thread.

`FieldState` itself is never passed around by reference today (every
read goes through `_appState.Field.*`), so we don't add a
`FieldState`-parameter guard.

---

## 3. Inventory

### 3.1 Current writes to FieldState members

From `grep -rn 'State\.Field\.\w+\s*=' Shared/`:

| Location | Field | Thread | Notes |
|---|---|---|---|
| `MainViewModel.cs:115–117` | OriginLatitude/Longitude, LocalPlane | UI | `SetFieldOrigin` — user action |
| `MainViewModel.cs:1169` | ActiveField | UI | `OnActiveFieldChanged` — field load |
| `MainViewModel.cs:1458–1505` | HeadlandLine, HeadlandDistance | UI | `LoadHeadlandFromField` — file load |
| `MainViewModel.cs:1696, 1748` | ActiveTrack | UI | `SelectedTrack` setter |
| `MainViewModel.cs:2646` | HeadlandLine | UI | Headland edit |
| `MainViewModel.cs:3541, 3560, 3569, 4014` | CurrentBoundary, HeadlandLine | UI | Boundary setter + headland commands |
| `ApplyResults.cs:172–173` | HeadlandProximityDistance/Warning | UI (mirror) | **Correct** — from GpsCycleResult |
| `GpsHandling.cs:310–311, 335–336` | HeadlandProximityDistance/Warning | UI | **Dead code** (`UpdateHeadlandProximity` has no callers) |
| `Commands.Settings.cs:307–308, 360–361` | DriftEasting, DriftNorthing | UI | Offset-fix / reset-drift commands |
| `Commands.Boundary.cs:392, 399, 422` | HeadlandLine | UI | Headland preview / confirm paths |
| `Headland.cs:251, 263, 709` | HeadlandLine | UI | Headland build commands |
| **`GpsPipelineService.cs:383`** | **LocalPlane** | **Cycle** | **§0 violation — fixed in E1** |

### 3.2 Cycle-side reads of FieldState

From `grep -rn '_appState\.Field' Shared/AgValoniaGPS.Services`:

| Location | Field | Notes |
|---|---|---|
| `GpsPipelineService.cs:381, 388` | LocalPlane | Auto-create + subsequent read |
| `AutoSteerService.cs:~50` (Phase B C1) | LocalPlane | Receive-thread parse uses shared instance |
| `GpsHeadingFusionService` | — | Doesn't touch FieldState |

Only `LocalPlane` is read cross-thread. Everything else FieldState-ish
that the cycle needs (boundary, headland, drift) is explicitly pushed
via `SyncGuidanceStateToPipeline → Set*` setters on
`IGpsPipelineService`, which copy under lock — those stay.

### 3.3 Out of scope

- **`_appState.Guidance` / `_appState.YouTurn` writes** — those are
  Phases C and D territory, already resolved.
- **`SensorState` writes from IMU/GPS services** — Phase F territory
  (sensor data flow). The one read in the cycle (`SensorState.Instance.
  ImuRoll` on line 671 of GpsPipelineService) is a primitive `double`
  read, torn-read-safe.
- **`ApplicationState.Field.Boundaries` ObservableCollection** —
  only the UI thread adds/removes items (boundary editing commands).
  No cycle-thread touch. Fine.

---

## 4. Commit-by-commit plan

### Commit 1 — Move LocalPlane auto-create off the cycle thread

**Goal.** Cycle caches a local `LocalPlane` reference on first fix,
uses it for coord conversion in the same tick, and emits it on
`GpsCycleResult`. UI mirrors it to `State.Field.LocalPlane` on the
UI thread.

- Add `LocalPlane? LocalPlane { get; init; }` to `GpsCycleResult`
  (or a nullable wrapper to distinguish "no change" from "null").
  Because most cycles don't need to emit a LocalPlane, use
  `GpsCycleResult.FirstFixLocalPlane` (non-null only on the cycle
  that auto-creates).
- In `GpsPipelineService`:
  - Replace the direct `_appState.Field.LocalPlane = new LocalPlane(...)`
    write with a cycle-local `_cycleLocalPlane` field (nullable).
  - On the first fix where `_cycleLocalPlane == null` AND
    `_appState.Field.LocalPlane == null`, create a local instance,
    store it in `_cycleLocalPlane`, and flag it on the result.
  - Subsequent cycles read `_appState.Field.LocalPlane ?? _cycleLocalPlane`
    (UI may have committed our emission by now, or may not). Either
    reference is equivalent.
  - Clear `_cycleLocalPlane` once the UI-committed reference matches
    (i.e., `ReferenceEquals(_appState.Field.LocalPlane, _cycleLocalPlane)`)
    so we don't hold a second copy indefinitely.
- In `ApplyGpsCycleResult`:
  - If `result.FirstFixLocalPlane != null` and
    `State.Field.LocalPlane == null`, assign it. Sole UI-thread writer
    for the auto-create path.
- Smoke-test: simulator bootstraps on first fix → LocalPlane assigned
  → coord conversion works in the first cycle and every cycle after.

**Acceptance.** Simulator opens without a field, first tick creates
a LocalPlane, map renders correctly. `grep -rn '_appState\.Field\.\w+\s*='
Shared/AgValoniaGPS.Services` returns zero.

### Commit 2 — Delete dead `UpdateHeadlandProximity`

**Goal.** Remove the orphan UI-thread headland-proximity path and
its `_headlandDetector` field.

- Delete `UpdateHeadlandProximity` method in `MainViewModel.GpsHandling.cs`
  (~45 lines).
- Delete `_headlandDetector` static field.
- `HeadlandDetectionService` itself stays — it's used by the cycle's
  `ComputeHeadlandProximity` path.

**Acceptance.** No references to `_headlandDetector` or
`UpdateHeadlandProximity` in `Shared/`. Tests pass.

### Commit 3 — Structural guard for LocalPlane

**Goal.** Add the guard to the existing reflection-based tests.

- Extend `YouTurnCycleTests` or add a new
  `FieldStateCycleTests.cs` in `Services.Tests/Pipeline/`:
  - `IGpsPipelineService_has_no_direct_LocalPlane_writethrough_methods`
    — reflection scan for `Set*`/`Push*`/`Apply*` methods taking
    `LocalPlane`.
- Add documentation comment block to `FieldState.cs` listing the
  read/write thread for each property.

**Acceptance.** New test passes. Doc comment merged.

### Commit 4 — Phase E close: review + parking-lot update

**Goal.** Phase E acceptance gate.

- Re-run full test suite.
- Re-measure `MainViewModel` line counts.
- Parking-lot review:
  - Mark the §0 / FieldState audit items resolved.
  - Confirm TMP-006 analyzer/CI-grep remains post-migration.
  - Update TMP-007 line-count entry if any drift.

**Acceptance.** Build + 793+ tests pass. Smoke-test simulator cold
start (no field open), simulator with field open, real GPS via UDP.

---

## 5. Risks / interim freezes

Phase E is much smaller than C/D — two structural changes and some
doc. The main risk is getting the LocalPlane handoff wrong:

- **First-cycle race.** Cycle emits `FirstFixLocalPlane` in result N;
  UI posts `ApplyGpsCycleResult(N)` to the dispatcher. Cycle runs
  again (N+1) before the UI handler runs. Cycle reads
  `_appState.Field.LocalPlane == null` (still not committed) and
  sees `_cycleLocalPlane != null` (its own cached copy). Falls back
  to the cached copy — correct. No race.
- **Two threads compete to create the plane.** Can't happen — cycle
  is single-threaded (Task.Run per tick), and the UI thread only
  creates the plane from `SetFieldOrigin` (explicit user action).
  The cycle's auto-create and the UI's explicit setup don't overlap.
- **Dead-code deletion hidden dependency.** `UpdateHeadlandProximity`
  has no callers per grep. If something reflects on it (unlikely —
  it's private), that'd break. Tests catch.

---

## 6. Acceptance gate

Phase E is done when:

1. `grep -rn '_appState\.\w+\.\w+\s*=\|State\.\w+\.\w+\s*='
   Shared/AgValoniaGPS.Services` returns zero.
2. Full test suite (all 793+ tests) passes.
3. Simulator cold start (no field) → first tick creates LocalPlane,
   subsequent ticks convert correctly. No observable difference from
   pre-Phase-E.
4. Simulator with field open → no change (UI had already set
   LocalPlane via `SetFieldOrigin`).
5. Real AiO drive → no change (Phase B already proved this path).

---

## 7. Linked work

- [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) — parent.
- [THREADING_PHASE_C_PLAN.md](THREADING_PHASE_C_PLAN.md),
  [THREADING_PHASE_D_PLAN.md](THREADING_PHASE_D_PLAN.md) — snapshot-
  emission pattern this phase reuses.
- [THREADING_MIGRATION_PARKING_LOT.md](THREADING_MIGRATION_PARKING_LOT.md)
  — TMP-006 extends here.
- `Shared/AgValoniaGPS.Services/AutoSteer/AutoSteerService.cs`
  (commit `5d6bccd`) — Phase B introduced the shared LocalPlane;
  Phase E finishes the write-thread story.
