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

# Threading Phase A — Foundation: State-Flow Primitives

**Parent plan:** [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) (§5 Phase A, §6.2, §6.4)
**Branch:** `feature/threading-phase-a`
**Scope:** Scaffolding only. No behavior change. No call-site moves.

This phase lays down the *shapes* — working-state POCOs, snapshot records,
and the intent surface — that Phases C and D will consume. After this
phase, the new types exist and are wired into the cycle worker's
entry/exit points, but nothing reads or writes them yet. Equivalent code
paths keep running; new types are inert scaffolding.

---

## 1. Goal

At the end of Phase A:

1. `YouTurnWorkingState` and `GuidanceWorkingState` POCOs exist under
   `Shared/AgValoniaGPS.Models/Pipeline/`.
2. `YouTurnSnapshot` and `GuidanceSnapshot` records exist in the same
   namespace, and `GpsCycleResult` carries them as optional fields.
3. `IPipelineIntents` interface exists (Models), with a concrete
   `PipelineIntents` service (Services.Pipeline), registered in DI.
4. `GpsPipelineService.ProcessCycle` calls `_intents.Drain()` at the top
   of each tick. The returned batch is held but not yet consumed.
5. Unit tests cover working-state construction and intent-drain
   thread-safety / last-wins semantics.
6. The whole solution builds, all existing 111 tests pass, and no
   behavior has changed.

This phase ships as one PR against `develop`. It is reviewable in
isolation — no logic changes, only new types and one no-op wiring line.

---

## 2. Decisions locked before Phase A starts

Two cross-cutting decisions from the parent plan must be pinned to make
Phase A concrete. The others (§6.1, §6.3, §6.5, §6.6) are deferred.

### 2.1 §6.4 — Where working state lives → **`Models/Pipeline/`**

Working states and snapshot records go in `Shared/AgValoniaGPS.Models/Pipeline/`,
co-located with `GpsCycleResult`. Rationale:

- Testability — working-state POCO tests in `AgValoniaGPS.Models.Tests`
  need zero service dependencies.
- Proximity — `GpsCycleResult` already lives in Models under `State/`;
  snapshots are the same kind of thing and belong next to it.
- The interface `IPipelineIntents` also lives in Models.Pipeline. The
  *implementation* `PipelineIntents` lives in `AgValoniaGPS.Services/Pipeline/`
  next to `GpsPipelineService`.

We will **not** move `GpsCycleResult` itself in this phase. It stays in
`State/` so no downstream `using` directives break. New types live in
`Pipeline/`. A later cleanup pass (post-Phase F) can consolidate.

### 2.2 §6.2 — Intent batching → **per-intent, last-wins for YouTurn**

Decision is per-intent, not global:

| Intent | Semantics | Storage |
|---|---|---|
| `RequestManualYouTurn(bool left)` | last-wins | volatile nullable bool (`bool?`), set via `Interlocked.Exchange` |
| `RequestClearYouTurn()` | last-wins | volatile bool flag, set via `Volatile.Write` |

Rationale: manual U-turn clicks at >10 Hz don't exist in practice. If a
user double-clicks, the second click either finds the machine already in
`IsExecuting` (guard handles it) or overrides a stale request from the
same tick window. Both outcomes match current UI-thread behavior.

`Drain()` returns a `PipelineIntentBatch` record-struct capturing both
values, then clears them atomically. FIFO intents (nudges, snap-to-line)
are deferred to Phase D and will be added as `ConcurrentQueue<T>` fields
when Guidance intents are introduced.

---

## 3. Out of scope for Phase A

Explicitly *not* done in this phase — each belongs to a later phase:

- Moving `YouTurnStateMachine.Tick()` off the UI thread (Phase C).
- Changing the state machine's signature to take `YouTurnWorkingState`
  (Phase C).
- Populating `GpsCycleResult.YouTurn` / `Guidance` snapshots in
  `ProcessCycle` (Phase C / D).
- Reading intents in the cycle worker and reacting to them (Phase C).
- Migrating `TriggerManualYouTurn*` commands to push intents (Phase C).
- Removing the existing flat YouTurn fields on `GpsCycleResult`
  (`IsInYouTurn`, `YouTurnTriggered`, `YouTurnCompleted`) — kept in place
  for backward compat until Phase C swaps consumers to the new
  `YouTurnSnapshot`.
- Removing ObservableObject from `YouTurnState` / `GuidanceState` — per
  §3 of parent plan, these stay as one-way mirrors.
- Guidance intents (nudge, snap-to-line, reverse heading) — Phase D.
- FieldState / ConnectionState working states — Phases E / F.

---

## 4. Current-state anchors

Verified before the plan was drafted. Referenced by commits below.

| Reference | File | Notes |
|---|---|---|
| Cycle owner | `Shared/AgValoniaGPS.Services/Pipeline/GpsPipelineService.cs` | `ProcessCycle` runs on `Task.Run` per tick; `Interlocked` back-pressure at line ~210 |
| Cycle result | `Shared/AgValoniaGPS.Models/State/GpsCycleResult.cs` | 26 init-only fields; pattern to extend |
| UI marshal | `Shared/AgValoniaGPS.ViewModels/MainViewModel.ApplyResults.cs` | `Dispatcher.UIThread.Post(() => ApplyGpsCycleResult(result))` |
| State machine | `Shared/AgValoniaGPS.Services/YouTurn/YouTurnStateMachine.cs` | `Tick(ctx, GuidanceState, YouTurnState) → YouTurnEffects` |
| Observable state | `Shared/AgValoniaGPS.Models/State/YouTurnState.cs`, `GuidanceState.cs` | shape to mirror into working states |
| DI registration | `Platforms/AgValoniaGPS.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` (+ iOS / Android equivalents) | where `PipelineIntents` will register |

---

## 5. Commit-by-commit plan

Five commits. Each is independently reviewable, builds cleanly, and
leaves the solution in a working state.

### Commit 1 — Working-state POCOs

**Adds:**
- `Shared/AgValoniaGPS.Models/Pipeline/YouTurnWorkingState.cs`
- `Shared/AgValoniaGPS.Models/Pipeline/GuidanceWorkingState.cs`

**Shape of `YouTurnWorkingState`** — mirror `YouTurnState` data, drop the
observable machinery. Auto-properties, no `ObservableObject` base, no
`SetProperty`. Include `Reset()` and `CompleteTurn()` methods verbatim
(they're pure data mutations). Same property names, types, and defaults:

```
IsEnabled, IsTriggered, IsExecuting,
TurnPath (List<Vec3>?), PathIndex,
IsTurnLeft, LastTurnWasLeft,
DistanceToHeadland, DistanceToTrigger,
NextTrack (Track?), LastCompletionPosition (Vec2?),
HasCompletedFirstTurn, YouTurnCounter,
WasHeadingSameWayAtTurnStart, NextTrackTurnOffset,
ReturnPassTargetPath (int?), SnakeSequence (List<int>?), SnakeIndex,
CurrentZone (TractorZone)
```

**Shape of `GuidanceWorkingState`** — same treatment for `GuidanceState`:

```
ActiveTrack (Track?), IsGuidanceActive,
CrossTrackError, HeadingError,
SteerAngle, SteerAngleRaw (short), DistanceOffRaw (short),
PpIntegral, PpPivotDistanceError, PpPivotDistanceErrorLast, PpCounter,
GoalPoint (Vec2), RadiusPoint (Vec2), PurePursuitRadius,
IsHeadingSameWay, IsReverse,
HowManyPathsAway, NudgeOffset, CurrentLineLabel (string, default "1L"),
IsContourMode
```

**Not added yet:** no callers, no constructors from `YouTurnState`, no
snapshot builders. Pure data holders.

**Verification:** `dotnet build AgValoniaGPS.sln` green. No existing code
references these types, so no test regressions are possible.

---

### Commit 2 — Snapshot records + `GpsCycleResult` extension

**Adds:**
- `Shared/AgValoniaGPS.Models/Pipeline/YouTurnSnapshot.cs`
- `Shared/AgValoniaGPS.Models/Pipeline/GuidanceSnapshot.cs`

**Shape:** immutable `record` types matching the working-state fields
one-for-one. Records give value equality for the future §6.1 decision
(Phase C) without committing to it now. All properties `init`-only.

**Modifies:**
- `Shared/AgValoniaGPS.Models/State/GpsCycleResult.cs` — add two nullable
  fields:

```
public YouTurnSnapshot? YouTurn { get; init; }
public GuidanceSnapshot? Guidance { get; init; }
```

Nullable because nothing populates them yet — existing cycle runs will
leave them `null`. Existing flat YouTurn/Guidance fields on
`GpsCycleResult` (`IsInYouTurn`, `SteerAngle`, etc.) stay untouched —
Phase C migrates consumers, Phase D removes.

**Verification:** build green, all 111 tests pass. `ApplyGpsCycleResult`
untouched — it reads the flat fields, ignores the new nullable
snapshots.

---

### Commit 3 — `IPipelineIntents` interface + concrete implementation

**Adds:**
- `Shared/AgValoniaGPS.Models/Pipeline/IPipelineIntents.cs`
- `Shared/AgValoniaGPS.Models/Pipeline/PipelineIntentBatch.cs`
- `Shared/AgValoniaGPS.Services/Pipeline/PipelineIntents.cs`

**Interface surface:**

```
public interface IPipelineIntents
{
    void RequestManualYouTurn(bool turnLeft);
    void RequestClearYouTurn();
    PipelineIntentBatch Drain();
}
```

**`PipelineIntentBatch`** — readonly record struct:

```
public readonly record struct PipelineIntentBatch
{
    // null = no request; true = left; false = right
    public bool? ManualYouTurn { get; init; }
    public bool ClearYouTurn { get; init; }
}
```

**Concrete `PipelineIntents` implementation:**

- Storage: two private fields backing the two intents. Use
  `Interlocked.Exchange` on a boxed `object?` for the nullable-bool
  slot, and `Volatile.Write` / `Interlocked.Exchange` for the clear
  flag.
- `Request*` methods are lock-free, O(1), thread-safe.
- `Drain()` atomically reads-and-clears both slots, returning the batch.
  Implementation will use `Interlocked.Exchange(ref slot, null)` and
  `Interlocked.Exchange(ref flag, 0)` so concurrent Request + Drain
  can't lose events or double-fire.

**Not added yet:** no consumers. The service is instantiated but nothing
calls `Drain()` or the `Request*` methods in production code after this
commit.

**Verification:** build green. New type is unreferenced by production
code — tests come in Commit 5.

---

### Commit 4 — DI wiring + Drain hook in `GpsPipelineService`

**Modifies:**
- `Platforms/AgValoniaGPS.Desktop/DependencyInjection/ServiceCollectionExtensions.cs`
- `Platforms/AgValoniaGPS.iOS/` DI setup (grep for the equivalent file)
- `Platforms/AgValoniaGPS.Android/` DI setup
- `Shared/AgValoniaGPS.Services/Pipeline/GpsPipelineService.cs`

**DI registration:** register `PipelineIntents` as a singleton bound to
`IPipelineIntents`. Singleton because UI commands (on UI thread) and the
cycle worker (on background thread) must share the same instance.

**`GpsPipelineService` changes:**
- Constructor takes `IPipelineIntents intents`, stores in field.
- At the top of `ProcessCycle(GpsData data)`, call:
  ```csharp
  // Stage 1: Drain intents — see threading_model.svg cycle worker lane.
  // Consumers land in Phase C (YouTurn) and Phase D (Guidance).
  var intents = _intents.Drain();
  ```
- Assign to a local and **do nothing with it**. The comment is the one
  that earns its keep — it traces the code location back to the
  numbered stage in the Ideal Threading Model diagram, and the variable
  would otherwise look like dead code in review.

**Verification:** build green, all 111 tests pass. `ProcessCycle`
behavior unchanged — `Drain()` returns an empty batch every tick
because no callers push intents yet.

**Risk:** platforms with separate DI setups (iOS, Android). Each must be
updated or the app will fail to start on that platform. Confirm by
running or at least building each platform project.

---

### Commit 5 — Tests

**Adds:**
- `Tests/AgValoniaGPS.Models.Tests/Pipeline/YouTurnWorkingStateTests.cs`
- `Tests/AgValoniaGPS.Models.Tests/Pipeline/GuidanceWorkingStateTests.cs`
- `Tests/AgValoniaGPS.Services.Tests/Pipeline/PipelineIntentsTests.cs`

**Working-state tests (thin):**
- Default values match those of the corresponding `*State : ObservableObject`.
- `Reset()` on `YouTurnWorkingState` clears the same fields `YouTurnState.Reset()` clears.
- `CompleteTurn()` on `YouTurnWorkingState` mirrors `YouTurnState.CompleteTurn()`.

Purpose: lock the invariant that the POCO and the observable type stay
in sync field-for-field. If someone adds a property to `YouTurnState`
later, the test catches it.

**Intent tests (the important ones):**

1. `Drain_returns_empty_when_no_requests` — fresh instance returns
   `ManualYouTurn == null`, `ClearYouTurn == false`.
2. `RequestManualYouTurn_last_wins` — three consecutive requests
   (left, right, left); `Drain()` returns `ManualYouTurn == true` (last
   one).
3. `Drain_clears_state` — after `Drain()`, a second `Drain()` returns
   empty.
4. `RequestClearYouTurn_latches_until_drained` — request, drain, drain
   again: first returns `ClearYouTurn == true`, second returns false.
5. `Concurrent_request_and_drain_does_not_lose_events` — stress test:
   spawn a writer task that calls `RequestManualYouTurn` in a loop
   10,000 times; spawn a reader task that calls `Drain` in a loop and
   counts non-null results. Sum of counted drains + final `Drain()` must
   equal 10,000 (or close to it — the guarantee is "no events lost",
   not "each event observable separately"; for a last-wins field this
   reduces to "at least one drain observed non-null if at least one
   request fired after the last drain"). The concrete assertion is that
   no drained value is outside `{null, true, false}` and no exceptions
   fire.

**Verification:** all 111 existing tests still pass; new tests pass.
`dotnet test Tests/` reports 111 + N new tests green.

---

## 6. Acceptance criteria (Phase A-wide)

Independent of individual commits — these must all hold when the branch
is ready to merge:

- [ ] `dotnet build AgValoniaGPS.sln` green on Desktop, iOS, Android.
- [ ] `dotnet test Tests/` passes; count increases by the new tests
      from Commit 5, no existing test changes status.
- [ ] `MainViewModel.YouTurn.cs` is byte-identical to its state before
      the branch (no accidental call-site migration).
- [ ] `MainViewModel.GpsHandling.cs` is byte-identical.
- [ ] `YouTurnStateMachine.cs` is byte-identical.
- [ ] `ApplyGpsCycleResult` is byte-identical.
- [ ] Smoke test: launch Desktop on Mac mini M4, open a field, drive a
      simulated pass, trigger a manual U-turn, complete the turn. No
      exceptions, no visual regressions. FPS unchanged from baseline on
      the same hardware.
- [ ] `grep -r "IPipelineIntents" Shared/AgValoniaGPS.Services/` returns
      exactly one production call site (`GpsPipelineService`'s
      `Drain()`) plus DI registration.
- [ ] No property on `YouTurnState` exists that isn't also on
      `YouTurnWorkingState`, and vice versa. (Enforced by Commit 5
      tests.)
- [ ] Parking-lot review complete: every open item in
      [THREADING_MIGRATION_PARKING_LOT.md](THREADING_MIGRATION_PARKING_LOT.md)
      §4 has a dated entry in its review log stamped within this PR's
      window. Items either moved to §5 (resolved), stayed open with a
      reason, or got pulled into this PR.

Hand-off to Phase C is clean when: the cycle worker has a live
`_intents` field and calls `Drain()` every tick; the working-state types
exist and compile; snapshot records exist and `GpsCycleResult` carries
them as `null` fields. Phase C plugs in the population.

---

## 7. Risk register

Small phase, small risks. Listed for completeness.

1. **Platform DI drift.** iOS / Android DI setup can diverge from
   Desktop's. If only Desktop is updated in Commit 4, iOS starts
   throwing on resolution. Mitigated by building all three platforms in
   CI before merge.
2. **`ObservableObject` fields missed.** If `YouTurnState` adds a
   property in develop after Phase A starts, the mirror test in
   Commit 5 catches drift at merge time but the branch needs a rebase.
   Low-frequency risk — the state types are stable.
3. **Thread-safety bugs in `PipelineIntents.Drain`.** Lock-free code is
   easy to get subtly wrong. The stress test in Commit 5 is the primary
   defense; reviewers should specifically audit the `Interlocked`
   pattern. If in doubt, switch to a plain `lock` — the contention is
   `10 Hz × 2 fields`, so a lock costs nothing.
4. **Scope creep into Phase C.** The temptation is to also populate the
   new snapshot fields in `ProcessCycle` or wire up the Drain output.
   Do not. That's Phase C. Keep this PR small enough that a reviewer
   can read every new line in one sitting.

---

## 8. Deferred decisions

All deferred items live in
[THREADING_MIGRATION_PARKING_LOT.md](THREADING_MIGRATION_PARKING_LOT.md).
Phase A defers: **TMP-001** (snapshot identity vs equality), **TMP-002**
(synchronous UI command carve-outs), **TMP-003** (Phase B service name),
**TMP-004** (`UdpGpsQueue` introduction), **TMP-005** (removal of flat
YouTurn/Guidance fields), **TMP-006** (enforcement mechanism),
**TMP-007** (MainViewModel line-count baseline).

End-of-phase review (§6 acceptance) walks every open item and either
pulls it into the closing PR, hands it to the next phase, or
explicitly keeps it parked with a dated note.

---

## 9. Follow-up phases (reminder only)

- **Phase B** — unify the GPS pipeline (two parsers → one, single
  `LocalPlane`, single cycle owner). Can run in parallel with or after
  Phase A; Phase A does not block Phase B because the scaffolding it
  adds is agnostic to how many parsers feed the cycle.
- **Phase C** — YouTurn end-to-end on the cycle worker. Consumes
  everything Phase A produces.
- **Phases D–F** — Guidance, Field, Connection. See parent plan §5.
