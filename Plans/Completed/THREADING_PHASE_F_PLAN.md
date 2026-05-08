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

# Threading Phase F — `ConnectionState` Audit & Migration Close

**Parent plan:** [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) (§5 Phase F)
**Branch:** `feature/threading-phase-a` (shared across phases A–F per PR #259)
**Scope:** Verify `ConnectionState` read/write thread boundaries. Add the
final structural guard + documentation. Promote PR #259 from draft
to ready-for-review.

The parent plan predicted Phase F would migrate NTRIP/UDP state
updates from service-thread writes to a marshaled-UI pattern. That
migration is **already done** — the audit finds zero service-side
references to `ConnectionState`, and the VM event handlers that do
write it explicitly marshal via `Dispatcher.UIThread.Post` before
touching any field.

Two grep checks confirm:

```
$ grep -rn 'ConnectionState\|\.Connections\.\w\+\s*=' Shared/AgValoniaGPS.Services
(no output)

$ grep -rn 'State\.Connections\.\w\+\s*=' Shared/AgValoniaGPS.ViewModels
Shared/AgValoniaGPS.ViewModels/MainViewModel.cs:514-518      (UI hello-timer)
Shared/AgValoniaGPS.ViewModels/MainViewModel.Ntrip.cs:195-196 (Dispatcher-Posted)
Shared/AgValoniaGPS.ViewModels/MainViewModel.Ntrip.cs:218    (Dispatcher-Posted)
```

Every writer is on the UI thread. Every service-originated event
path goes through a `Dispatcher.UIThread.CheckAccess()` guard that
posts to the UI thread when the event fires from a background worker.

Phase F is therefore audit + documentation + a final structural guard.
Once this is merged, the whole migration is done and PR #259 can leave
draft status.

---

## 1. Goal

At the end of Phase F:

1. **Thread ownership of `ConnectionState` is documented** on the
   type, same pattern as `FieldState` in Phase E E3.
2. **Final structural guard.** `ConnectionStateCycleTests` confirms
   `IGpsPipelineService`, `INtripClientService`, and
   `IUdpCommunicationService` don't expose any method that takes a
   `ConnectionState` parameter — the pattern is one-way: services
   raise events, VM marshals and writes.
3. **Parking-lot final review.** All remaining open items are
   either post-migration cleanup (TMP-006 analyzer / CI grep) or
   external-domain (TMP-009 hardware heading verification, TMP-010
   Avalonia tooltip freeze). None block closing the migration.
4. **PR #259 promoted** from draft to ready-for-review.
5. **Handoff document cleaned up** — `Plans/THREADING_HANDOFF.md`
   either gets a final "migration complete" entry or moves to
   `Plans/Completed/`.

After Phase F, the §0 invariant is satisfied for every mutable
`ObservableObject` in `ApplicationState`: `FieldState`,
`YouTurnState`, `GuidanceState`, `VehicleState`, `UIState`,
`SimulatorState`, `ConnectionState`. The entire cycle runs without
touching any observable. UI updates happen exactly once per cycle,
at the marshal point.

---

## 2. Decisions locked before Phase F starts

### 2.1 No snapshot-path migration needed for ConnectionState

The parent plan §5 anticipated "working state in the service,
snapshot to a service-specific result, VM mirrors on UI thread via
a dispatcher post." The audit confirms the snapshot-and-dispatcher-
post pattern is already the shape of the code:

- `NtripClientService` raises `ConnectionStatusChanged` events.
  `MainViewModel.OnNtripConnectionChanged` handles them; the event
  args are the "snapshot" (immutable `NtripConnectionEventArgs`).
  The handler checks `Dispatcher.UIThread.CheckAccess()` and posts
  if needed before touching `State.Connections`.
- `UdpCommunicationService` exposes poll-style queries
  (`IsModuleDataOk`, `IsModuleHelloOk`) that the VM's
  `StartHelloTimerAsync` reads from inside an `await Task.Delay(100)`
  loop. Because the loop was started from the UI thread constructor,
  Avalonia's `SynchronizationContext` continues each `await` on the
  UI thread — writes to `State.Connections` land correctly.

No new snapshot record type is needed; the existing event args
already serve that role.

### 2.2 Structural guard scope

The Phase C/D/E guards each test `IGpsPipelineService` for methods
taking the relevant working-state / snapshot type. Phase F extends
that to three interfaces because ConnectionState is cross-cutting:

- `IGpsPipelineService` — no `ConnectionState` parameter allowed.
- `INtripClientService` — no `ConnectionState` parameter allowed.
- `IUdpCommunicationService` — no `ConnectionState` parameter allowed.

If anything ever needs a `ConnectionState` reference to write to, the
test will flag it and force the author to pick the event/marshal
path instead.

---

## 3. Inventory

### 3.1 Current writers to ConnectionState

| Location | Thread | Mechanism |
|---|---|---|
| `MainViewModel.cs:514–518` | UI (awaited continuation of UI-started async) | Hello-timer reads `IsModuleDataOk`/`IsModuleHelloOk` from UDP service, writes Connection state |
| `MainViewModel.Ntrip.cs:195–196` | UI (Dispatcher-Posted) | NTRIP `ConnectionStatusChanged` handler |
| `MainViewModel.Ntrip.cs:218` | UI (Dispatcher-Posted) | NTRIP `RtcmDataReceived` handler |

### 3.2 Services that observe connection status

- `NtripClientService` — owns its own socket lifecycle; exposes
  events. No direct state writes.
- `UdpCommunicationService` — owns receive threads; exposes polling
  API (`IsModuleDataOk`, etc.) and hello-response tracking. No
  direct state writes.
- `AutoSteerService` — reads `_udpService.IsConnected` for its
  hello-send logic; doesn't touch `ConnectionState`.

### 3.3 Out of scope

- **`VehicleState`, `UIState`, `SimulatorState`, `SectionState`**
  weren't in the parent plan's phase list but the same audit logic
  applies. Grep for `State\.\w+\.\w+\s*=` from
  `Shared/AgValoniaGPS.Services/`:

  ```
  $ grep -rn 'State\.\w\+\.\w\+\s*=\|_appState\.\w\+\.\w\+\s*='
       Shared/AgValoniaGPS.Services
  (no output after Phase E)
  ```

  The §0 invariant is already satisfied for all observable state
  types — service code writes zero of them.

- **Cleanup / consolidation of legacy VM connection properties**
  (`IsAutoSteerDataOk`, `IsMachineDataOk`, etc.) that duplicate
  `State.Connections.*` — noted in the MainViewModel comments as
  "will be removed in Phase 5". Not a threading concern; belongs
  in a separate legacy-cleanup PR.

---

## 4. Commit-by-commit plan

### Commit 1 — Documentation + structural guard

- Add the thread-ownership doc block to `ConnectionState.cs` listing
  each property's writer + source path.
- New `ConnectionStateCycleTests.cs` in
  `Tests/AgValoniaGPS.Services.Tests/Pipeline/`:
  - `IGpsPipelineService_has_no_ConnectionState_parameter`
  - `INtripClientService_has_no_ConnectionState_parameter`
  - `IUdpCommunicationService_has_no_ConnectionState_parameter`
  - (Optionally) a meta-guard scanning every public method on
    every service interface for a `ConnectionState` parameter.

### Commit 2 — Phase F close + migration final review

- Run full test suite.
- Parking-lot final review — every open item gets one of:
  "resolved by Phase F", "deferred to post-migration with explicit
  reason", or "external / out of scope".
- Update `Plans/THREADING_HANDOFF.md` with a "migration complete"
  entry or move it to `Plans/Completed/`.
- Reconcile the parent `Plans/THREADING_MIGRATION_PLAN.md` with what
  actually shipped.
- Final line-count baseline capture on TMP-007.

### Commit 3 — Promote PR #259 to ready-for-review

- `gh pr ready 259` (or equivalent).
- PR description updated to reflect the full A–F arc, test count,
  and the §0 invariant end state.

---

## 5. Risks / interim freezes

None — Phase F is documentation and a static test. No behavior
change possible.

---

## 6. Acceptance gate

Phase F is done when:

1. `grep -rn 'State\.\w+\.\w+\s*=\|_appState\.\w+\.\w+\s*='
   Shared/AgValoniaGPS.Services` returns **zero** (already true
   as of Phase E close; Phase F formalizes the end state).
2. Full test suite passes.
3. `ConnectionState.cs` has a thread-ownership doc block.
4. PR #259 is ready-for-review.

---

## 7. Linked work

- [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) — parent.
- [THREADING_PHASE_E_PLAN.md](THREADING_PHASE_E_PLAN.md) — closest
  template; FieldState got the same doc + guard treatment.
- [THREADING_MIGRATION_PARKING_LOT.md](THREADING_MIGRATION_PARKING_LOT.md)
  — final review happens here.
- PR [#259](https://github.com/AgOpenGPS-Official/AgValoniaGPS/pull/259)
  — draft → ready-for-review.
