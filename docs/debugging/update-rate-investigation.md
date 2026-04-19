# Tractor Update Rate Investigation

## Problem
Tractor moves in big steps (~1 per second) instead of smooth 10Hz updates
when using the vehicle simulator.

## Hypothesis
GPS updates are being dropped somewhere in the pipeline. The simulator
sends at 10Hz but only ~1 update per second reaches the map renderer.

## Pipeline stages (each numbered for instrumentation)

```
[1] Simulator sends $PANDA via UDP (10Hz)
     |
[2] UdpCommunicationService.ReceiveCallback
     |
     +--[3] AutoSteerService.ProcessGpsBuffer (BLOCKING on receive thread)
     |       Parse + Guidance + PGN send
     |
     +--[4] NmeaParserService.ParseSentence (BLOCKING on receive thread)
     |       |
     |       +--[5] GpsService.UpdateGpsData -> GpsDataUpdated event
     |               |
     |               +--[6] GpsPipelineService.OnGpsDataUpdated
     |                       |
     |                       +-- Back-pressure check (DROPS if busy)
     |                       |
     |                       +--[7] Task.Run -> ProcessCycle
     |                               |
     |                               +--[8] CycleCompleted event
     |                                       |
     |                                       +--[9] Dispatcher.Post -> ApplyGpsCycleResult
     |                                               |
     |                                               +--[10] SetAllPositions -> map render
```

## Potential drop points
- **[3]**: ProcessGpsBuffer blocks receive thread for 50-200ms, delaying next receive
- **[6]**: Back-pressure drops update if previous ProcessCycle still running
- **[7]**: ProcessCycle slow (>100ms) causing back-pressure
- **[9]**: Dispatcher.Post queued behind render frames

## Instrumentation added (current)
- [3] AutoSteerService: logs every 10th cycle with E/N/H
- [6] GpsPipelineService: logs received/dropped counts + cycleMs
- [9] ApplyGpsCycleResult: logs every 10th apply with positions

## How to diagnose
1. Run the main app in Debug mode
2. Start the simulator at 10Hz, set speed to 5 km/h
3. Watch Debug output for 10 seconds
4. Check:
   - [AutoSteer] cycle count: should increment by ~100 in 10s (10Hz)
   - [Pipeline] dropped count: if high, ProcessCycle is the bottleneck
   - [Pipeline] cycleMs: if >100ms, this confirms back-pressure drops
   - [ApplyResult] count: should match Pipeline processed count

## Expected finding
ProcessGpsBuffer (step 3) blocks the receive thread for too long,
AND ProcessCycle (step 7) takes >100ms causing back-pressure drops.
Combined: only a fraction of updates reach the UI.

## Fix options (aligned with THREADING_MIGRATION_PLAN)
1. **Quick fix**: Make ProcessGpsBuffer only parse, hand off heavy work
   to Task.Run (Phase B of threading plan)
2. **Full fix**: Unify pipelines per THREADING_MIGRATION_PLAN Phase B -
   single cycle worker, receive thread returns immediately
