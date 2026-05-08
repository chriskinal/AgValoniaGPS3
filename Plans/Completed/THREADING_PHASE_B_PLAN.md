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

# Threading Phase B — Unify the GPS Pipeline

**Parent plan:** [THREADING_MIGRATION_PLAN.md](THREADING_MIGRATION_PLAN.md) (§5 Phase B)
**Origin spec:** [`docs/superpowers/plans/2026-04-19-unify-gps-pipeline.md`](../docs/superpowers/plans/2026-04-19-unify-gps-pipeline.md) (historical reference; this plan is authoritative)
**Branch:** `feature/threading-phase-a` (shared across phases A–F per PR #259)
**Scope:** Runtime behavior change. One parser. One `LocalPlane`. One cycle owner. Receive thread does parse-and-return only.

Phase A added scaffolding without touching the execution path. Phase B
does the opposite: it removes duplication from the execution path
without touching the Phase A scaffolding yet. The four execution-path
files Phase A kept byte-identical (`MainViewModel.YouTurn.cs`,
`MainViewModel.GpsHandling.cs`, `YouTurnStateMachine.cs`,
`MainViewModel.ApplyResults.cs`) stay byte-identical through Phase B
too — Phase C is the first phase that touches them.

Addresses current-state problems **2, 3, and 6** from
[`threading_model_current.svg`](threading_model_current.svg).

---

## 1. Goal

At the end of Phase B:

1. **One NMEA parser, pure.** `NmeaParserServiceFast` is the only
   parser. `NmeaParserService` (string-based) is deleted. The parser
   does bytes → state and nothing else — no heading fusion, no
   fix-quality filtering, no IMU side-effects.
2. **Heading fusion and fix-quality filtering live in the cycle
   worker.** Extracted from `NmeaParserService` into
   `GpsHeadingFusionService` and `GpsFixQualityValidator`. Called by
   `GpsPipelineService.ProcessCycle` as its first post-handoff stages,
   so every cycle gets validated-and-fused data regardless of which
   parser produced it.
3. **One `LocalPlane`.** Owned by `ApplicationState.Field.LocalPlane`,
   initialized in exactly one place (the cycle worker on first valid
   fix, or the field-open command — never on the receive thread).
   `AutoSteerService._localPlane` no longer exists.
4. **Receive thread does parse-and-return only.** `AutoSteerService.ProcessGpsBuffer`
   reduces to: parse NMEA into a `Position`, hand off, return. No
   coordinate conversion, no guidance, no PGN build, no `SendPgns`
   call — none of that work executes on the UDP receive callback.
5. **One cycle owner.** `GpsPipelineService.ProcessCycle` runs
   fusion, fix-quality validation, tool position, guidance, section
   control, coverage, AutoSteer guidance, and PGN build — all of it,
   once per tick, on the background `Task.Run` worker that already
   exists.
6. **PGN cadence is cycle-driven.** PGNs (253 / 254 / 239) build inside
   the cycle at end-of-cycle and get handed to the UDP send path. No
   "send per packet arrival".
7. Solution builds green on Desktop/iOS/Android. All existing tests
   pass plus new integration coverage for the unified path. Smoke test
   drives a full field cycle including auto U-turn without regression.

Phase B ends with a coherent runtime: pure parser, one coordinate
frame, one cycle worker, fusion/filtering in the right place, one
PGN emitter. Phase C lands cleanly on top of it.

---

## 2. Decisions locked before Phase B starts

### 2.1 TMP-003 — Unified service name → **keep `GpsPipelineService`**

The cycle owner class keeps the name `GpsPipelineService`. Reasoning:

- Phase B expands the service's responsibility (absorbs AutoSteer's
  cycle work) but doesn't change what it *is* — a pipeline orchestrator
  on a background worker.
- The name already describes the role. Renaming to `CycleWorker` or
  similar would be pure cosmetic churn against an already-meaningful
  name.
- `AutoSteerService` stays as a collaborating service that
  `GpsPipelineService` calls once per cycle. Separation of concerns:
  AutoSteer computes steer + builds PGNs; the pipeline decides when
  those are called.

Post-Phase-F cleanup can revisit the name if a better one emerges.
This decision closes TMP-003.

### 2.2 TMP-004 — `UdpGpsQueue` shape → **keep the event handoff**

The I/O-to-cycle handoff stays as the existing `GpsService.GpsDataUpdated`
event feeding `GpsPipelineService.OnGpsDataUpdated` with `Task.Run`
kick-off and `Interlocked` back-pressure. No new `Channel<T>` or
dedicated queue type.

Reasoning:

- The current pattern already satisfies the §0 invariant (work runs
  on `Task.Run`, not the callback thread). The SVG's "UdpGpsQueue"
  label is a *visual* name for this handoff, not a demand for a new
  type.
- Introducing an explicit queue would couple cycle cadence to a
  decision about bounded vs unbounded, last-wins vs FIFO — decisions
  worth making only if the event-based approach proves insufficient.
  It hasn't.
- The Phase B test plan includes cycle-rate measurement. If cycle is
  demonstrably not keeping up with packet arrival after Phase B
  (currently ~10 Hz), we reopen TMP-004 and upgrade to a real queue.
  Otherwise close.

**TMP-004 stays open** — flip to "in review" when cycle-rate
measurement lands, then resolve.

### 2.3 `LocalPlane` creation — **in the cycle worker or field-open, never on receive thread**

`ApplicationState.Field.LocalPlane` is an `ObservableObject` property.
Setting it from the UDP receive thread would fire `PropertyChanged`
on the receive thread — the exact anti-pattern Phase 0 forbids.

Two legitimate creation paths post-Phase-B:

- **Field open** — user opens a field; `MainViewModel.Commands.Fields.cs`
  creates `LocalPlane` from the field's origin. Runs on UI thread.
  Correct today.
- **First valid fix with no field loaded** — the cycle worker
  creates it inside `ProcessCycle` on its first tick where the fix is
  valid and no `LocalPlane` exists yet. Runs on the background `Task.Run`
  worker. This path moves from AutoSteerService (receive thread) to
  GpsPipelineService (cycle worker) during Phase B.

Receive thread never touches `ApplicationState.Field.LocalPlane`.

---

## 3. Out of scope for Phase B

Explicitly *not* done — each belongs to a later phase:

- Moving `YouTurnStateMachine.Tick` off the UI thread (Phase C).
- Populating Phase A's `YouTurnSnapshot` / `GuidanceSnapshot` fields
  on `GpsCycleResult` (Phase C / D).
- Migrating `TriggerManualYouTurn*` / nudge / snap commands to the
  intent queue (Phase C / D).
- Fixing TMP-008 (manual U-turn doesn't execute). Pre-existing bug,
  Phase C's problem — Phase C needs a working manual U-turn to verify
  its migration.
- `FieldState` audit (Phase E).
- NTRIP / UDP connection state migration (Phase F).
- Renaming `GpsPipelineService` (TMP-003 closed; cosmetic rename only
  after Phase F if still desired).
- Replacing `GpsDataUpdated` event with explicit queue (TMP-004 stays
  open pending measurement).

---

## 4. Current-state anchors

Verified before this plan was drafted.

| Reference | File / symbol | Notes |
|---|---|---|
| String parser | `Shared/AgValoniaGPS.Services/NmeaParserService.cs:25–437` — `ParseSentence(string)` | string-based, allocates per parse; called from `MainViewModel.cs:985` in `OnUdpDataReceived` |
| Span parser | `Shared/AgValoniaGPS.Services/NmeaParserServiceFast.cs:30–493` — `ParseIntoState(ReadOnlySpan<byte>, ref VehicleState)` | zero-copy; called from `AutoSteerService.cs:308` in `ProcessGpsBuffer` |
| AutoSteer receive-thread work | `Shared/AgValoniaGPS.Services/AutoSteer/AutoSteerService.cs:299–350` — `ProcessGpsBuffer` | parse + LocalPlane convert + guidance + PGN send, all on UDP callback thread |
| AutoSteer's LocalPlane | `AutoSteerService.cs:47, 318` — `_localPlane` field, constructed at line 318 | independent instance; auto-creates on first valid fix |
| Pipeline's LocalPlane | `GpsPipelineService.cs:292` — `_appState.Field.LocalPlane = new LocalPlane(...)` | the shared observable instance; also auto-creates on first valid fix |
| Cycle owner | `GpsPipelineService.cs:238–552` — `ProcessCycle` | `Task.Run` per tick, `Interlocked` back-pressure at `OnGpsDataUpdated:210` |
| Pipeline-to-autosteer callback | `GpsPipelineService.cs:453` — `_autoSteerService.ProcessSimulatedPosition()` | simulator path; also receives cycle work via this entry today |
| PGN 253 / 239 build+send | `AutoSteerService.cs:446–462` — `SendPgns` | fires from `ProcessGpsBuffer:341`, i.e. per packet, receive thread |
| UDP send path | `_udpService.SendToModules(pgn)` — called from `AutoSteerService.SendPgns` | ships pre-built PGN bytes |
| `ApplicationState.Field.LocalPlane` | `Shared/AgValoniaGPS.Models/State/FieldState.cs:157–162` — `ObservableObject` property | the one slot; currently competing with `AutoSteerService._localPlane` |
| MainViewModel UDP entry | `MainViewModel.cs:985` — `OnUdpDataReceived` → `_nmeaParser.ParseSentence` | the string-path origin |
| Field-open `LocalPlane` creation | `MainViewModel.cs:117, 3337, 3785, 3837` | field origin → `LocalPlane`; stays as-is through Phase B |

---

## 5. Commit-by-commit plan

Six commits. Each is independently reviewable, builds cleanly, and
leaves the app in a working state (smoke-testable after every commit).

**Revision note (2026-04-19, post-C1):** The original Commit 2 assumed
the two parsers were drop-in equivalents. They're not — `NmeaParserService`
does load-bearing work the fast parser doesn't: heading fusion
(`ProcessHeading`), fix-quality filtering (`MinFixQuality` / `MaxHdop` /
`MaxDifferentialAge`), and IMU data mirroring into `SensorState.Instance`.
Dropping the string parser without relocating that logic would delete
real features. The commit plan now has **six** commits instead of five,
with a new Commit 2 that extracts fusion and filtering into
cycle-worker-callable services before any parser deletion.

### Commit 1 — Share `LocalPlane` via `ApplicationState.Field`

**Goal:** Eliminate the dual-LocalPlane bug. Both paths read from the
same instance.

**Modifies:**
- `Shared/AgValoniaGPS.Services/AutoSteer/AutoSteerService.cs` — remove
  `_localPlane` field; take `ApplicationState` through the constructor;
  replace all reads of `_localPlane` with `_appState.Field.LocalPlane`.
- `AutoSteerService.ProcessGpsBuffer` — remove the "auto-create my own
  LocalPlane" block (currently line 318). Rely on the pipeline or
  field-open path to have created one. If `_appState.Field.LocalPlane`
  is `null`, skip coordinate conversion this tick and let the pipeline
  handle auto-create on the cycle worker.
- DI registration sites (Desktop / iOS / Android) — add `ApplicationState`
  to the `AutoSteerService` resolution if not already injected.

**Does not modify:** receive-thread handling (still runs guidance +
PGN); `GpsPipelineService.ProcessCycle` (keeps its own auto-create at
line 292). Phase B is still dual-path after this commit — just no
longer dual-`LocalPlane`.

**Verification:**
- Solution builds green.
- All 583 tests pass.
- Smoke test: open a field, drive a pass, close field. No coordinate
  glitches.
- `grep "new LocalPlane" Shared/AgValoniaGPS.Services/AutoSteer/` returns
  zero matches.

**Risk:** If auto-create ordering differs between paths (AutoSteer
used to create early on first UDP packet; now it defers to the
pipeline), there's a one-tick window where AutoSteer skips coordinate
conversion. Expected behavior; the UI doesn't render that tick
differently from any other missed-fix tick.

---

### Commit 2 — Extract fusion and fix-quality filtering from the string parser

**Goal:** Move `NmeaParserService.ProcessHeading` and the fix-quality
guard into cycle-worker-callable services. Also mirror parsed IMU
fields (Roll/Pitch/YawRate) into `SensorState.Instance` from the
cycle so existing consumers keep working after the string parser
retires. The string parser continues to exist and run in the MVM path
— we're *not* touching parser callsites yet. This commit only adds
the relocation targets.

**Adds:**
- `Shared/AgValoniaGPS.Services/Gps/GpsHeadingFusionService.cs`
  implementing `IGpsHeadingFusionService`. Owns `_previousEasting`,
  `_previousNorthing`, `_previousHeading`, `_hasPreviousPosition`.
  Single public method `FuseHeading(double gpsHeading, double speedMs,
  double easting, double northing) → double` with the exact logic from
  `NmeaParserService.ProcessHeading`.
- `Shared/AgValoniaGPS.Services/Gps/GpsFixQualityValidator.cs`. Static
  helper: `bool IsAcceptable(int fixQuality, double hdop, double
  differentialAge, out string? rejectionReason)`. Reads the same
  `ConfigurationStore.Instance.Connections` fields the string parser
  reads today.
- Interface `IGpsHeadingFusionService`.
- Unit tests for both: fusion covers dual-GPS mode, single-GPS mode,
  IMU fusion, low-speed fix-to-fix; validator covers each rejection
  reason.

**Modifies:**
- `GpsPipelineService.ProcessCycle` — wire fusion + validator as new
  first stages (right after intent drain, before LocalPlane check):
  1. Validate fix quality; if rejected, set a status-message field on
     `GpsCycleResult`, emit, abort the cycle.
  2. Apply fusion — replace the heading on a local GpsData copy before
     downstream stages use it.
  3. Mirror Roll/Pitch/YawRate into `SensorState.Instance` (until
     `SensorState` itself is retired, out of scope).
- DI registration across all three platforms for `IGpsHeadingFusionService`.

**Does not modify:** `NmeaParserService.ProcessHeading` stays in place
(still runs in the MVM path). Commit 3 removes the MVM path; Commit 5
deletes the old parser. Running both fusion paths in the interim is
acceptable — the MVM path's fused heading is consumed by the cycle
via `GpsDataUpdated`, and the cycle's own fusion then re-fuses (a
no-op since the state is already fused). Slightly wasted work for one
commit. Safe.

**Verification:**
- Solution builds green.
- All existing tests pass.
- Unit tests for the new fusion service match `ProcessHeading`'s
  behavior numerically (parity tests pick representative inputs,
  run both the old method and the new service, assert equality to
  1e-9).
- Smoke test: field open, drive a pass, no regression in heading
  behavior on screen.

**Risk:**
- Fusion-state divergence between the string parser's instance state
  and the new service's instance state during the interim (Commits 2–4).
  Both track `_previousEasting/Northing/Heading`. In the interim, they
  diverge by one cycle. When the string parser retires in Commit 5,
  only the new service remains, so the divergence goes away. During
  the interim, the second fusion pass in the cycle may compute a
  slightly different fix-to-fix heading. Accept; flag if the smoke
  test shows it.

---

### Commit 3 — Consolidate UDP NMEA dispatch

**Goal:** One parser callsite per packet. After this commit, incoming
UDP NMEA bytes hit `NmeaParserServiceFast` exactly once per packet;
`NmeaParserService` is no longer invoked (still exists — deleted in
Commit 5).

**Modifies:**
- `MainViewModel.cs:985` (`OnUdpDataReceived`) — remove the
  `if (e.PGN == 0)` NMEA-parse branch. Keep the rest of the handler
  for non-NMEA PGNs.
- `MainViewModel.cs:59, 253` — remove the `_nmeaParser` field and its
  `new NmeaParserService(...)` construction.
- `AutoSteerService.ProcessGpsBuffer` — add a call to
  `_gpsService.UpdateGpsData(gpsData)` where `gpsData` is built from
  the parsed `VehicleState`. This fires `GpsDataUpdated`, which kicks
  the cycle. Without this, removing MVM's parse breaks the cycle.
- `AutoSteerService` constructor — inject `IGpsService`.

**Does not modify:** `NmeaParserServiceFast`; `NmeaParserService` (still
alive, unused now); the cycle path; guidance/PGN work.

**Verification:**
- Solution builds green.
- All tests pass.
- **Parity test (risk mitigation):** a new test in
  `Tests/AgValoniaGPS.Services.Tests/Pipeline/NmeaParserParityTests.cs`
  that parses a representative PANDA corpus through both
  `NmeaParserService.ParseSentence` and
  `NmeaParserServiceFast.ParseIntoState`, maps the resulting
  `VehicleState` to a `GpsData`, and asserts field-for-field equality
  (within 1e-9 for doubles). Headings compared raw — fusion is a
  cycle-worker concern after Commit 2.
- `grep -r "_nmeaParser" Shared/` returns zero matches.
- Smoke test: full field open → pass → close with no regression.

**Risk:** Parity failures. If the corpus surfaces real divergence,
decide per-case — fix the fast parser, or park the divergence as a
TMP-0NN item and proceed only if the divergence doesn't touch the
smoke-test path.

---

### Commit 4 — Move guidance + PGN work off the receive thread

**Goal:** `AutoSteerService.ProcessGpsBuffer` shrinks to parse + state
update + signal. Everything else moves to the cycle worker.

**Modifies:**
- `AutoSteerService.ProcessGpsBuffer` — reduces to:
  1. `NmeaParserServiceFast.ParseIntoState(buffer, ref _state)`
  2. `_gpsService.UpdateGpsData(...)` (already added in Commit 3)
  3. Return
- Methods `CalculateGuidance`, `SendPgns`, coordinate conversion, tram
  update, latency recording — these become methods that the cycle
  worker calls, not the receive-thread entry point.
- `GpsPipelineService.ProcessCycle` — add the moved calls at the
  appropriate stages. Guidance computation sits alongside existing
  `_trackGuidanceService.CalculateGuidance`; PGN build+send becomes
  the new end-of-cycle stage (before `CycleCompleted` fires).

**Does not modify:** `NmeaParserServiceFast`; the Phase A scaffolding;
any UI binding; `YouTurnStateMachine`.

**Verification:**
- Solution builds green.
- All tests pass (some may need updating).
- Smoke test: full field cycle including auto U-turn. Critical check:
  the tractor follows guidance correctly (proves guidance calls from
  the cycle work); PGNs emit at the expected cadence (proves PGN work
  from cycle reaches the hardware).
- Log inspection: receive-thread timing should drop. Cycle-thread
  timing may rise. Neither should trigger the 24 FPS floor.

**Risk — highest in Phase B (unchanged from original C3):**
- **PGN cadence shift.** PGN sent at end of cycle instead of per
  packet. Net additional latency ≈ cycle duration (typically <20 ms
  on the test hardware). Monitor hardware behavior.
- **Guidance now computes once per cycle.** The AutoSteer-on-receive
  computation is redundant today and its output was never used
  downstream of the cycle for steering.

---

### Commit 5 — Delete `NmeaParserService`

**Goal:** The string-based parser is gone. Only `NmeaParserServiceFast`
remains.

**Modifies:**
- Deletes `Shared/AgValoniaGPS.Services/NmeaParserService.cs`.
- Deletes `Tests/AgValoniaGPS.Services.Tests/NmeaParserServiceTests.cs`
  (or migrates any unique coverage into `NmeaParserServiceFastTests` if
  that doesn't already exist — inventory during implementation).
- Removes any DI registration for `NmeaParserService`.
- Removes any `using` / construction reference.

**Verification:**
- Solution builds green. If anything still referenced the deleted
  class, the build breaks and the reference must be cleaned up.
- All tests pass.
- `grep -r "NmeaParserService[^F]" .` returns zero matches outside
  the commit's own deletion.

**Risk:** Low. If Commit 3 did its job, this is pure cleanup. If
something outside the grep caught still references the string parser,
the build breaks — fix and move on.

---

### Commit 6 — Integration tests + acceptance

**Goal:** Lock the Phase B invariants with tests that will fail loudly
if any phase regresses them.

**Adds:**
- `Tests/AgValoniaGPS.Services.Tests/Pipeline/UnifiedPipelineTests.cs`
  covering:
  1. `LocalPlane_is_a_single_instance_across_paths` — construct the
     pipeline through DI (or a test-builder), drive one GPS packet,
     assert both `AutoSteerService`'s view of LocalPlane (now via
     `ApplicationState.Field.LocalPlane`) and the pipeline's view point
     to the same object reference.
  2. `ProcessGpsBuffer_does_not_call_guidance_or_PGN` — with a mock
     `ITrackGuidanceService` and `IUdpCommunicationService` via
     NSubstitute, call `AutoSteerService.ProcessGpsBuffer` directly
     and assert neither mock was invoked. This locks the parse-only
     contract.
  3. `Cycle_emits_PGNs_end_of_tick` — drive a cycle, assert
     `SendToModules` was called once with PGN 253 and once with PGN 239
     (or equivalent).

**Modifies:**
- `Tests/AgValoniaGPS.Services.Tests/SimulatorDataFlowTests.cs` — if
  it exercises the old receive-thread-does-everything flow, update to
  the new parse-only + cycle-driven flow.

**Does not modify:** anything in production.

**Verification:**
- All existing tests plus the three new ones pass.
- Smoke test passes per §6 below.

**Risk:** Test flakiness from thread-timing if any concurrent path is
tested — keep the tests deterministic by driving the cycle
synchronously via a test-mode flag, or mocking the `Task.Run` indirection
so the cycle runs inline.

---

## 6. Acceptance criteria (Phase B-wide)

Independent of individual commits — these must all hold when the
Phase B portion of the PR is ready to advance (Phase C still follows
on the same branch):

- [ ] `dotnet build AgValoniaGPS.sln -p:DesktopOnly=true` green; same
      for the native multi-target build on macOS.
- [ ] `dotnet test Tests/` passes. Count increases by Commit 2's
      fusion/validator unit tests, Commit 3's parity test, and Commit 6's
      integration tests (minus whatever Commit 5 deletes with
      `NmeaParserServiceTests.cs`).
- [ ] `grep -r "NmeaParserService[^F]" .` returns zero matches in the
      working tree (outside of git history).
- [ ] `grep -r "new LocalPlane" Shared/ Platforms/` returns at most
      two production sites: the field-open path in `MainViewModel.Commands.Fields.cs`,
      and the cycle-worker auto-create in `GpsPipelineService.ProcessCycle`.
      Both read/write via `ApplicationState.Field.LocalPlane`.
- [ ] `AutoSteerService._localPlane` field does not exist.
- [ ] `AutoSteerService.ProcessGpsBuffer` body is parse-only — no
      `CalculateGuidance`, no `SendPgns`, no coordinate conversion.
- [ ] Phase A untouched: `git diff phase-a-tip..HEAD --stat -- Shared/AgValoniaGPS.Models/Pipeline/` returns zero changes.
- [ ] Phase C's four target files still byte-identical to `develop`:
      `MainViewModel.YouTurn.cs`, `MainViewModel.GpsHandling.cs`,
      `YouTurnStateMachine.cs`, `MainViewModel.ApplyResults.cs`.
- [ ] Smoke test on Mac mini M4: open a field, drive a simulated
      pass, auto U-turn (or manual if TMP-008 is resolved in time),
      complete the turn, drive the next pass, close the field. No
      exceptions. FPS floor 24 held.
- [ ] Parking-lot review: every open item gets a dated Phase-B-close
      note. TMP-003 moves to §5 resolved. TMP-004 stays open if cycle
      rate wasn't measured, or moves to resolved if it was.

---

## 7. Risk register

Phase B has real runtime-behavior changes. Risks ranked by severity.

### 7.1 PGN cadence regression (HIGH)

**What could go wrong.** Hardware modules (steer board, machine board)
expect PGN 253 / 239 at a roughly predictable cadence. Phase B moves
PGN emission from per-packet (receive thread) to end-of-cycle. If the
cycle rate differs meaningfully from packet rate, or if cycle latency
adds noticeable delay before the PGN hits the wire, steer hold time
or section control may glitch.

**Mitigation.**
- In Commit 3, measure the old cadence (per-packet timestamps of
  `SendToModules` calls over 30 s of operation) and the new cadence.
  Target: within ±10 ms variance, same mean rate.
- Add a log line inside `SendToModules` with a timestamp, enabled by
  a build flag, so field testing can confirm cadence without code
  changes.
- If cadence regresses: the fix is not to revert — it's to speed the
  cycle up or decouple PGN send from cycle (fire PGNs eagerly inside
  cycle, don't batch to end). Revert is last resort.

**Who verifies.** Manual smoke test on real hardware if available;
otherwise the log-based cadence check.

### 7.2 LocalPlane write from receive thread (MEDIUM)

**What could go wrong.** Commit 1 removes `AutoSteerService._localPlane`,
but some code path may still try to write `ApplicationState.Field.LocalPlane`
from the UDP receive thread. That fires `PropertyChanged` on the
receive thread → UI binding attempts layout on a background thread →
Avalonia throws.

**Mitigation.**
- §2.3 above pins the creation paths: field-open (UI thread) and
  cycle-worker auto-create. Audit during Commit 1 to prove no other
  writer exists.
- Add an assertion in a `#if DEBUG` block inside
  `ApplicationState.Field.LocalPlane`'s setter that throws if called
  on a thread that is neither the UI dispatcher nor the cycle worker.
  Keeps the invariant enforced at runtime during development.

**Who verifies.** The debug-build assertion; grep for
`Field.LocalPlane =`; smoke test.

### 7.3 Parser divergence surfaces a latent bug (LOW after Commit 2 lands)

**What could go wrong.** The two parsers differ beyond edge-case field
parsing — `NmeaParserService` also does heading fusion (`ProcessHeading`),
fix-quality filtering, and IMU mirroring into `SensorState.Instance`.
`NmeaParserServiceFast` does not. Removing the string parser without
relocating these drops real behavior.

**Mitigation.**
- Commit 2 extracts fusion + filtering into cycle-worker-callable
  services (`GpsHeadingFusionService`, `GpsFixQualityValidator`)
  *before* any parser callsites change. Unit tests verify numerical
  parity with `ProcessHeading` on representative inputs.
- Commit 3's parity test compares the raw per-field parse output
  (lat/lon/altitude/speed/heading/sats/hdop/diff-age). Fusion is
  explicitly out of scope of the parity test — it's a cycle concern
  post-Commit 2.
- `NmeaParserService` stays alive through Commits 2, 3, and 4; only
  Commit 5 deletes it. That leaves a three-commit window to revert if
  smoke testing surfaces unexpected divergence.

**Who verifies.** Unit tests for the extracted services (Commit 2);
the field-level parity test (Commit 3); smoke tests after Commits 3
and 4.

### 7.4 AutoSteerService callbacks on wrong thread (LOW-MEDIUM)

**What could go wrong.** `AutoSteerService` today fires `StateUpdated`
on the receive thread. UI code listening to that event may have been
relying on specific timing. Post-Phase-B, `StateUpdated` fires from
the cycle worker instead.

**Mitigation.**
- Audit `StateUpdated` subscribers during Commit 3.
- If any subscriber does UI work directly in the handler (instead of
  marshaling via `Dispatcher.UIThread.Post`), that's a pre-existing
  bug and needs a fix in the same commit or a parking-lot ticket.

**Who verifies.** Grep for `StateUpdated +=` and audit each callback.

### 7.5 Tests relying on receive-thread-does-everything pattern (LOW)

**What could go wrong.** `SimulatorDataFlowTests.cs` and possibly
others test `AutoSteerService.ProcessGpsBuffer` with an assumption
that one call produces a parsed-updated-guided-transmitted cycle.
Post-Phase-B, that one call is parse-only.

**Mitigation.**
- Commit 6 explicitly covers updating these tests.
- If updates prove substantial, split them into a separate commit
  before Commit 6 rather than lumping in.

**Who verifies.** `dotnet test` failure surfaces the breaks;
mechanical test fix.

### 7.6 Scope creep toward Phase C (LOW)

**What could go wrong.** The temptation to also populate
`YouTurnSnapshot` / `GuidanceSnapshot` while the cycle worker is being
edited — since both are cycle-worker concerns. That's Phase C.

**Mitigation.** Phase C's plan is Phase C's plan. If Phase B's PR
touches Phase A's four byte-identical execution-path files, reviewer
flags it and the diff rolls back.

---

## 8. Deferred decisions (pointer to parking lot)

All deferred items live in
[`THREADING_MIGRATION_PARKING_LOT.md`](THREADING_MIGRATION_PARKING_LOT.md).

Phase B resolves **TMP-003** (service name). Phase B *may* resolve
**TMP-004** (queue introduction) pending cycle-rate measurement.
Still-parked items after Phase B close: TMP-001, TMP-002, TMP-005,
TMP-006, TMP-007, TMP-008, plus TMP-004 if measurement deferred.

End-of-phase parking-lot review is mandatory per Phase B acceptance.

---

## 9. Follow-up phases (reminder only)

- **Phase C** — YouTurn end-to-end on the cycle worker. Consumes the
  Phase A scaffolding. Phase B's unified cycle is its execution
  substrate. Phase C also owns resolving TMP-008 (manual U-turn
  regression investigation).
- **Phases D–F** — Guidance, Field, Connection. See parent plan §5.

Phase B hand-off to Phase C is clean when: one parser, one LocalPlane,
one cycle owner, PGN cadence cycle-driven, and the four Phase C
target files still untouched.
