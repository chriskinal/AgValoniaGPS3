# Unified 100 Hz Host Control Loop

**Closes:** #313 (sub-frame section control)

**Branch:** `feature/unified-control-loop`

**Status:** Planning

## Summary

Decouple the host's control rate from the GPS update rate. Today every control concern (section state machine, coverage strip extension, autosteer PGN sends, machine PGN sends) runs once per GPS frame at 10 Hz. The proposal is to run a fixed-rate 100 Hz control loop on the host that consumes interpolated pose from a position estimator, with GPS as an event-driven sensor that updates the estimator. This matches the firmware's existing 100 Hz autosteer cadence and gives sub-frame section control as a natural consequence.

This subsumes #313's bolt-on sub-frame scheduler proposal — every control tick re-evaluates state, so sub-frame edge accuracy is the default, not a special case.

## Goals

- Host control loop runs at 100 Hz on a dedicated thread, independent of GPS arrival rate.
- Section control, coverage strip extension, autosteer PGN sends, machine PGN sends all driven by control loop ticks.
- Coverage edge accuracy improves from ~0.7 m worst case at 25 km/h to ~0.05 m, bounded by RTK noise rather than frame period.
- Vehicle position display interpolates smoothly between GPS samples (visual stutter at 10 Hz eliminated).
- Authority and safety remain in firmware: watchdogs, sleep-mode override, ethernet-link checks unchanged.

## Non-goals

- Raw IMU plumbing on the host. Filed firmware issue #29 for higher-rate IMU PGN; estimator uses GPS-bundled `ImuYawRate` until then.
- Confirmed-state coverage from firmware. Filed firmware issue #28 for PGN 237; coverage continues to be driven by host commanded state.
- Steering controller retuning. Gains are expected to remain valid; if real-hardware testing shows otherwise, retuning is a follow-up.
- Per-tick guidance recompute. v1 keeps `_state.SteerAngle` updated only at GPS rate (10 Hz) and resends the same target 10× per second. Tighter per-tick steering is a future enhancement.

## Architecture

### Data flow

```
┌──────────────────┐    on GPS     ┌──────────────────┐
│ GpsService       │ ────receive──▶│ PositionEstimator│
│ (UDP rx thread)  │   sample      │ (atomic snapshot)│
└──────────────────┘               └──────────────────┘
                                            ▲
                                            │ pose(t) on demand
                                            │
                ┌───────────────────────────┼───────────────────────────┐
                │                           │                           │
                ▼                           ▼                           ▼
┌──────────────────┐         ┌──────────────────┐          ┌──────────────────┐
│ ControlLoop      │         │ Map renderer     │          │ MainViewModel    │
│ (100 Hz, own     │         │ (30 FPS draw)    │          │ ApplyResults     │
│  thread)         │         │                  │          │ (UI thread,      │
│                  │         │ Pulls live state │          │  on GPS event)   │
│ Tick:            │         │ at draw time:    │          │                  │
│  - read pose     │         │  - pose          │          │ Updates bound    │
│  - section update│         │  - section colors│          │ properties for:  │
│  - guidance      │         │  - coverage bmp  │          │  - section bar   │
│  - send PGN 254  │         │                  │          │  - fix quality   │
│  - send PGN 239  │         │                  │          │  - dialogs etc.  │
│  - extend cov.   │         │                  │          │                  │
└──────────────────┘         └──────────────────┘          └──────────────────┘
        │
        │ UDP
        ▼
┌──────────────────┐
│ Firmware         │
│  - autosteer     │  100 Hz, own clock, watchdog on PGN 254
│  - sections      │  every-loop, reactive to PGN 239 arrival
└──────────────────┘
```

Three independent rates, three independent threads, freshest-message-wins between them. Authority over physical machine stays in firmware.

### Why these rates

- **100 Hz control loop** matches the firmware's `taskAutosteer` 100 Hz cadence exactly. Firmware never sees stale PGN 254 unless host stalls. Sections respond within UDP latency since `MachineProcessor::handlePGN239` is event-driven on receive.
- **30 FPS render** is what the renderer already does. Pulling live state at draw time gives smooth visual updates without any extra publish work.
- **10 Hz UI bound-property updates** is unchanged from today. Low-frequency UI state (current track name, dialogs, fix quality, section bar) doesn't need higher rate.

### Position estimator

GPS arrives at 10 Hz with bundled IMU values:

```csharp
public sealed record PoseSnapshot(
    Vec2 Position,           // Easting/Northing at GPS sample time
    double Heading,          // Fused heading at GPS sample time (radians)
    double SpeedMps,         // Ground speed
    double YawRateRadPerSec, // ImuYawRate from PANDA payload
    double Roll,
    long TimestampTicks      // From IClock
);

public interface IPositionEstimator
{
    void UpdateFromGps(PoseSnapshot snapshot);  // Called on UDP rx thread
    PoseSnapshot GetLatestSnapshot();
    InterpolatedPose GetPose(long nowTicks);    // Reads atomic snapshot
}

public readonly record struct InterpolatedPose(
    Vec2 Position, double Heading, double SpeedMps, double Roll);
```

Mid-frame interpolation:

```
Δt = (now - snapshot.TimestampTicks) / Frequency  [seconds]
heading(t)  = snapshot.Heading + snapshot.YawRateRadPerSec * Δt
position(t) = snapshot.Position + (cos(heading(t)) * speed * Δt,
                                   sin(heading(t)) * speed * Δt)
```

Atomic snapshot swap via `Interlocked.Exchange<PoseSnapshot>`. Single writer (UDP rx thread), many readers (control loop, renderer, anyone). No locks.

Quality bound: `ImuYawRate` is itself sampled at 10 Hz (bundled with GPS), so the yaw rate value driving prediction is up to ~50 ms old. For tractor dynamics this gives heading error in the tenths of a degree mid-frame and position error of 5–10 cm during transients. Acceptable. Improves further once firmware issue #29 ships.

### Control loop

```csharp
public interface IControlLoopService
{
    void Start();
    void Stop();
    bool IsRunning { get; }
    void Tick(long timestampTicks);  // Test hook
    event Action<ControlTickResult>? TickCompleted;
}

public sealed class ControlLoopService : IControlLoopService
{
    // Production: PeriodicTimer at 100 Hz on dedicated thread.
    // Each tick:
    //   1. pose = positionEstimator.GetPose(now)
    //   2. sectionControl.Update(pose, speed)        — mutates SectionControlState[]
    //   3. coverage extension on active sections      — calls AddCoveragePoint
    //   4. autoSteer.SendPgn254()                    — UDP send
    //   5. autoSteer.SendPgn239()                    — UDP send
    //   6. raise TickCompleted (for diagnostics)
}

public sealed class ManualControlLoop : IControlLoopService
{
    // Test: no timer; tests call Tick(timestampTicks) directly.
}
```

Timer choice: `PeriodicTimer` on a dedicated `Thread` set to `Highest` priority (or default — measure first). `System.Threading.Timer` is also viable but its scheduling jitter is wider. For 10 ms periods we want sub-millisecond jitter.

### Renderer changes

`DrawingContextMapControl` already has its own 30 FPS dispatcher timer and reads coverage bitmap directly. Extend the same pattern:

```csharp
// In DrawingContextMapControl.OnRender or composition handler:
var pose = _positionEstimator.GetPose(Clock.Current.GetTimestamp());
var sectionStates = _sectionControlService.SnapshotStates();
// draw chevron at pose.Position with pose.Heading
// draw section color overlays from sectionStates
// blit coverage bitmap (unchanged)
```

What gets pulled at draw time vs. what stays as MVVM bindings:

| Data | Source | Update mechanism |
|---|---|---|
| Vehicle pose for chevron | `IPositionEstimator.GetPose(now)` | Pulled at draw time (30 FPS) |
| Section state colors on map | `ISectionControlService.SnapshotStates()` | Pulled at draw time (30 FPS) |
| Coverage bitmap | `ICoverageMapService.LatestBitmap()` | Already pulled at draw time |
| Section bar (top of screen) | `Section1ColorCode` etc. bound props | `RaisePropertyChanged` from ApplyResults at GPS rate (10 Hz). Could bump to 30 Hz if user feedback warrants — small cost. |
| Fix quality, NTRIP status, current track name, dialogs | Bound props | Unchanged — low frequency, event-driven |

Net visual effect: vehicle chevron stops stuttering at 10 Hz and glides smoothly between GPS samples. Section state changes appear within 33 ms of an actual flip. Costs nothing extra because the render frame is happening anyway.

### Authority and safety

Unchanged in spirit. Firmware retains positive control:
- PGN 239 watchdog (2 s) — host stall = sections off.
- Ethernet-link check — link drop = sections off.
- Sleep-mode override — section bits forced to 0 in firmware regardless of PGN 239 content.
- Hydraulic auto-shutoff timer — firmware-enforced max active duration.

Host publishes desired state. Firmware decides what to act on. Independent clocks. Network is the rendezvous.

## File changes

### New files

| File | Purpose |
|---|---|
| `Shared/AgValoniaGPS.Services/Pipeline/IPositionEstimator.cs` | Interface for the estimator |
| `Shared/AgValoniaGPS.Services/Pipeline/PositionEstimator.cs` | Implementation; atomic snapshot swap |
| `Shared/AgValoniaGPS.Models/Pipeline/PoseSnapshot.cs` | Snapshot record |
| `Shared/AgValoniaGPS.Models/Pipeline/InterpolatedPose.cs` | Interpolation result struct |
| `Shared/AgValoniaGPS.Services/Pipeline/IControlLoopService.cs` | Control loop interface |
| `Shared/AgValoniaGPS.Services/Pipeline/ControlLoopService.cs` | Production 100 Hz timer-driven implementation |
| `Shared/AgValoniaGPS.Services/Pipeline/ManualControlLoop.cs` | Test implementation |
| `Tests/AgValoniaGPS.Services.Tests/Pipeline/PositionEstimatorTests.cs` | Estimator math + atomicity |
| `Tests/AgValoniaGPS.Services.Tests/Pipeline/ControlLoopServiceTests.cs` | Manual loop drives section/coverage updates correctly |

### Modified files

| File | Change |
|---|---|
| `Shared/AgValoniaGPS.Services/GpsService.cs` | On GPS sample receive: build `PoseSnapshot`, call `_positionEstimator.UpdateFromGps`. Keep raising `GpsDataUpdated` for everything else (UI flows). |
| `Shared/AgValoniaGPS.Services/Pipeline/GpsPipelineService.cs` | Shrink. `ProcessCycle` stops calling `SectionControlService.Update` and `AutoSteerService.SendPgns`. Keep building the `GpsCycleResult` snapshot for `MainViewModel.ApplyResults` (UI/section-bar data). Consider rename in a follow-up commit. |
| `Shared/AgValoniaGPS.Services/Section/SectionControlService.cs` | `Update(toolPos, heading, speed)` is now called by `ControlLoopService` at 100 Hz. Phase counters that were "frames at 10 Hz" become "ticks at 100 Hz". `SECTION_ON_DELAY = 2` (frames at 10 Hz = 200 ms) becomes `SECTION_ON_DELAY_TICKS = 20` (ticks at 100 Hz = 200 ms). `LookAheadOnSetting * 10` (frames) becomes `LookAheadOnSetting * 100` (ticks). Keep the seconds-based math centralized so the constants are derived, not hardcoded. |
| `Shared/AgValoniaGPS.Services/Coverage/CoverageMapService.cs` | No structural change. `AddCoveragePoint` gets called from control loop at 100 Hz. Existing `MIN_COVERAGE_POINT_DISTANCE_SQ = 0.0144 m²` (0.12 m) self-throttles rasterization to ~one quad per 12 cm of travel — at 100 Hz that's at most one rasterize per 12 cm × 16 sections. At 25 km/h (7 m/s) per section that's ~58 quads/sec, total ~930/sec across all sections. Same order of magnitude as today. Profile to confirm; add a hard upper bound if needed. |
| `Shared/AgValoniaGPS.Services/AutoSteer/AutoSteerService.cs` | `SendPgns()` becomes called from `ControlLoopService` at 100 Hz instead of `GpsPipelineService.ProcessCycle`. v1: `_state.SteerAngle` continues to update only on GPS samples (via existing `ProcessGpsBuffer` path); we just resend the same value 10× per second. The firmware autosteer task uses the freshest. |
| `Shared/AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs` | Inject `IPositionEstimator` and `ISectionControlService`. In the render path, pull live pose and section snapshot at draw time instead of reading bound properties. Coverage bitmap path unchanged. |
| `Shared/AgValoniaGPS.ViewModels/MainViewModel.ApplyResults.cs` | Unchanged behavior — still fires on `CycleCompleted` (GPS rate) for section bar / fix quality / non-map UI. |
| `Platforms/AgValoniaGPS.Desktop/DependencyInjection/ServiceCollectionExtensions.cs` | Register `IPositionEstimator`, `IControlLoopService` (production = `ControlLoopService`). Same for iOS/Android DI. |
| `Tests/AgValoniaGPS.Services.Tests/LookAheadSlitTests.cs` | Replace `pipeline.SynchronousMode = true` driving with `ManualControlLoop` + `TestClock`. Each test step: feed GPS sample → estimator, advance clock by N×10 ms, tick loop N times. Add new tests at 25 and 40 km/h with thresholds tightened to ≤ 0.05 m. |
| `Tests/AgValoniaGPS.Services.Tests/SectionControlServiceTests.cs` | Update tests that assumed 10 Hz tick. Existing 13 tests should still pass after the constants are adjusted (logic unchanged, just denominated in ticks instead of frames). |
| `Tests/AgValoniaGPS.Services.Tests/AsymmetricSectionTurnTests.cs` | Same as above. |

## Threading boundaries

Three threads. Be explicit (per the user's "concurrency bugs: ask why twice first" rule):

1. **UDP receive thread** (background, OS-driven). Single writer to `PositionEstimator.UpdateFromGps`. Atomic snapshot swap.
2. **Control loop thread** (background, 100 Hz timer). Reads `PositionEstimator`, mutates `SectionControlState[]`, mutates `_autoSteerState.SectionStates`, calls `_udpService.SendToModules`. Single mutator of section state.
3. **UI thread** (Avalonia). Reads `SectionControlState[]` via:
   - Map renderer pulling `SnapshotStates()` directly at draw time.
   - `MainViewModel.ApplyResults` reading from `GpsCycleResult` (already a snapshot today).

### Synchronization rules

- `PositionEstimator`: single immutable snapshot swapped via `Interlocked.Exchange<PoseSnapshot>`. Readers receive a consistent record. No locks.
- `SectionControlState[]`: mutated only by control loop thread. UI snapshots return a fresh array (already does today via `MainViewModel.GetSectionStates`); add a `SnapshotStates()` method to `ISectionControlService` for the renderer that returns a defensive copy.
- `_autoSteerState.SectionStates`: mutated only by control loop thread. UDP sends are also from the control loop thread. No race within the host. Firmware reads from latest UDP packet — that's the intended pub/sub model.
- `CoverageMapService` already locks internally (`_coverageLock`). Per-tick `AddCoveragePoint` calls go through it; UI render reads bitmap with the same lock. Unchanged.

### What we are NOT doing

Not adding cross-thread queues, dispatcher marshaling for control work, or `ConcurrentDictionary` lookups in the hot path. The three threads have clearly separated mutation domains.

## Implementation order

Single feature branch `feature/unified-control-loop`. Commits in this order; each commit leaves the tree green (build + tests pass).

1. **Position estimator (standalone)**
   - Add `PoseSnapshot`, `InterpolatedPose`, `IPositionEstimator`, `PositionEstimator`.
   - Tests: dead-reckoning math, atomic snapshot swap under concurrent reads/writes.
   - No wiring yet.

2. **Control loop abstraction (standalone)**
   - Add `IControlLoopService`, `ManualControlLoop` (test impl), `ControlLoopService` (production).
   - Tests: manual loop fires `TickCompleted`, manual loop respects `Stop`.
   - No wiring yet.

3. **Wire `GpsService` to `PositionEstimator`**
   - On GPS sample receive, build `PoseSnapshot` and call `UpdateFromGps`.
   - Estimator now has live data; nothing reads it yet except tests.

4. **Move autosteer PGN sends into control loop**
   - DI: register `ControlLoopService` as `IControlLoopService` for production.
   - `MainViewModel` ctor: start the control loop after the GPS pipeline starts. Subscribe handler that calls `_autoSteer.SendPgnsForControlTick()`.
   - `AutoSteerService.ProcessSimulatedPosition` stops calling `SendPgns()` — PGNs come from the loop.
   - All existing tests must pass.
   - **Note**: split out from the original commit 4 because moving the section state machine has wider blast radius than initially scoped (LookAheadSlitTests drives the pipeline synchronously and assumes the pipeline calls `SectionControlService.Update`). Section move + test rewrites land together as commit 5.

5. **Move section state machine into control loop + rewrite tests**
   - `ControlLoopService.Ticked` handler calls `_toolPositionService.Update(interpolatedPose)` and `_sectionControl.Update(toolPos, ...)` on each tick.
   - `GpsPipelineService.ProcessCycle` stops calling `_sectionControlService.Update`.
   - `SectionControlService` constants retuned for 100 Hz tick rate (derive from seconds-based config, don't hardcode tick counts).
   - `ToolPositionService` made thread-safe (single writer = control loop, multiple readers).
   - `LookAheadSlitTests` and `AsymmetricSectionTurnTests` rewritten to drive via `ManualControlLoop` + `TestClock`. Add 25 and 40 km/h tests with edge accuracy ≤ 0.05 m. Existing slit tests at 5 / 10 / 15 km/h continue to pass.
   - All existing tests must pass.

6. **Renderer pulls live state at draw time**
   - Inject `IPositionEstimator` and `ISectionControlService` into `DrawingContextMapControl`.
   - Add `SnapshotStates()` to `ISectionControlService` (defensive copy of `_sectionStates`).
   - Replace property-bound chevron position/heading with `_positionEstimator.GetPose(now)`.
   - Replace property-bound section colors on map with snapshot read.
   - Headless UI tests in `AgValoniaGPS.UI.Tests` must still pass.

7. **Coverage timing audit** ✓
   - Static analysis: `GetSegmentCoverageMulti` is cache-throttled at 150 ms (~6.7 Hz) so the most expensive per-section op is unchanged from 10 Hz.
   - `RasterizeQuadToBitmap` at 16 sections × 100 Hz = 1600 calls/sec × ~60 cells each = ~96 k cell writes/sec, memory-bound, sub-ms CPU.
   - Boundary/headland polygon checks at 5 × 16 × 100 Hz = 8000/sec ≈ 5–10 ms CPU/sec at typical field complexity.
   - Net added CPU vs 10 Hz baseline: ~10–20 ms/sec ≈ 1–2% of one core. Within budget for iPad Pro 2nd gen and Android tablet.
   - The pre-emptive min-distance throttle the original plan called for would re-introduce a fix that was deliberately removed (`UpdateMapping` comment at SectionControlService.cs:634 — "near-degenerate triangles preferable to gaps").
   - **No throttling added.** Real-hardware FPS verification in commits 8–9 is the proof-test; if iPad/Android drops below 24 FPS we add a time-budgeted throttle then.

8. **End-to-end smoke in app**
   - Drive in simulator, verify section state machine behaves as before.
   - Verify coverage paints cleanly on boundary entry/exit and pre-applied slits.
   - Verify FPS holds (24 floor on iPad, 30 on Android, 60 on Mac).
   - Verify vehicle chevron interpolates smoothly (no 10 Hz stutter).

9. **Real-hardware smoke on tractor**
   - Confirm autosteer tracks AB line normally — controller hasn't gone unstable from 100 Hz PGN 254.
   - Confirm sections fire on boundary entry/exit at expected positions.
   - Confirm coverage matches actual ground covered.
   - **Branch stays open until this gate passes.**

10. **(Optional commit) Rename `GpsPipelineService`** if the responsibilities have shrunk enough to warrant. Do as its own commit so the diff is reviewable.

11. **Merge** — squash the branch into develop with an updated CHANGELOG-style commit message. Bump `sys/version.h` patch.

## Test strategy

### Unit tests
- `PositionEstimatorTests`: dead-reckoning math at varying yaw rates, varying speeds, varying intervals; concurrent writer/many-reader stress to verify atomic snapshot.
- `ControlLoopServiceTests`: `ManualControlLoop` orchestrates section + autosteer + coverage in the right order each tick.
- `SectionControlServiceTests`: existing 14 tests (including #324 regression) continue to pass after constant retuning.

### Integration tests
- `LookAheadSlitTests` rewritten to `ManualControlLoop` + `TestClock`. Existing 5/10/15 km/h tests retained. New 25 km/h and 40 km/h tests added with ≤ 0.05 m edge threshold.
- `AsymmetricSectionTurnTests` rewritten similarly.
- `HydraulicLiftTests` likely unaffected (operates on per-frame state) but verify after constant retuning.

### Headless UI tests
- `AgValoniaGPS.UI.Tests` (123 tests) must continue to pass — no behavioral change visible at the MVVM/UI layer except smoother map updates.

### Manual / hardware
- Simulator smoke (FPS floor, smooth chevron, clean coverage edges).
- Real-hardware smoke (autosteer stable, sections fire at expected boundary positions, no observed regression).

## Risks

| Risk | Mitigation |
|---|---|
| Steering controller chatter from 100 Hz PGN 254 with interpolated targets | v1 keeps `_state.SteerAngle` at GPS-rate updates; only the send rate goes to 100 Hz. Same target value sent 10× per second. Firmware sees no novel oscillation. |
| Coverage rasterization load at 100 Hz × 16 sections | `MIN_COVERAGE_POINT_DISTANCE_SQ` self-throttles. Profile during commit 6; add hard upper bound if needed. |
| Test rewrites mask subtle regressions that surface only on real hardware | Mandatory real-hardware smoke gate before merge. Branch stays open until verified. |
| Thread-safety of `SectionControlState` reads from renderer mid-update | Renderer reads via new `SnapshotStates()` returning defensive copy. Per-section bool fields are atomic on x86/ARM anyway. |
| Position estimator drift during GPS dropout | Estimator uses last known yaw rate + velocity. After ~1 sec of dropout (10 GPS frames missed) the prediction is unreliable; stale-snapshot detection should freeze pose updates and let the watchdog disengage normally. Add a `MaxStaleSeconds` clamp. |
| `PeriodicTimer` jitter on iOS / Android | Test on real devices. Falling back to a tighter `Stopwatch`-driven busy-loop on a dedicated thread is an option if `PeriodicTimer` jitter exceeds 2 ms. |

## Acceptance criteria

- All existing tests pass: 104 models + 194 viewmodels + 123 UI + 497 services + new control-loop and estimator tests.
- New `LookAheadSlitTests` at 25 km/h: edge accuracy ≤ 0.05 m (vs ~0.18–0.42 m today).
- New `LookAheadSlitTests` at 40 km/h: edge accuracy ≤ 0.05 m.
- Existing slit tests at 5/10/15 km/h continue to pass.
- App runs at 24+ FPS minimum on iPad Pro 2nd gen, 30+ on Android tablet, 60 on Mac mini M4.
- Vehicle chevron interpolates smoothly between GPS samples (no 10 Hz visible stutter).
- Real-hardware smoke: autosteer tracks AB line, sections fire on boundary entry/exit at expected positions, coverage matches reality, no observed regression vs today.
- Control loop tick budget < 5 ms p99 (50% headroom on 10 ms period).
- No new allocations per tick in the hot path (verify with allocation profiler).

## Open items at start of commit 1

1. Branch name: `feature/unified-control-loop` — confirm.
2. `GpsPipelineService` rename: defer to commit 10 or skip entirely. Default: defer.
3. Section bar update rate (10 Hz vs 30 Hz): default 10 Hz (unchanged); revisit if user feedback flags lag.
4. `MaxStaleSeconds` clamp on estimator: propose 1.0 s; revisit during real-hardware testing.

## References

- Issue #313 — original sub-frame section control proposal
- Firmware issue #28 — PGN 237 confirmed-state reply (future, enables hybrid coverage)
- Firmware issue #29 — raw IMU at higher rate (future, improves estimator quality)
- `Plans/PERFORMANCE_STRATEGY.md` — FPS floor and perf discipline
- `Plans/IMU_HEADING_WIRING_PLAN.md` — current IMU heading flow on host
- Firmware: `/Users/chris/Code/Firmware_Teensy_AiO_26/src/main.cpp` — task scheduler with 100 Hz autosteer
- Firmware: `/Users/chris/Code/Firmware_Teensy_AiO_26/lib/aio_system/MachineProcessor.cpp` — every-loop reactive section handling, 2 s watchdog
