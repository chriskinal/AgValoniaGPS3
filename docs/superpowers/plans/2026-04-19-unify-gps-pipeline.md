# Unify GPS Pipeline - Eliminate Dual Path Technical Debt

**Goal:** Merge the two parallel GPS processing paths into a single pipeline. Currently NMEA data is parsed twice, coordinates converted twice, and state maintained in two separate services.

**Priority:** Medium - not blocking any features but causes bugs (GPS timeout, tractor not moving without field) and wastes CPU cycles parsing the same sentence twice.

---

## Current Architecture (Problem)

```
UDP Port 9999
    |
    v
UdpCommunicationService.ReceiveCallback
    |
    +-- Path 1 (Zero-copy, low-latency):
    |   AutoSteerService.ProcessGpsBuffer
    |     -> NmeaParserServiceFast.ParseIntoState (fast Span-based parser)
    |     -> VehicleState (lat/lon/heading/speed)
    |     -> Auto-create LocalPlane
    |     -> ConvertWgs84ToGeoCoord -> Easting/Northing
    |     -> CalculateGuidance
    |     -> SendPgns (PGN 254)
    |     -> StateUpdated event -> MainViewModel.Guidance
    |
    +-- Path 2 (Pipeline, UI updates):
        ProcessReceivedData -> DataReceived event
          -> MainViewModel.OnUdpDataReceived
            -> NmeaParserService.ParseSentence (old string-based parser)
              -> GpsService.UpdateGpsData
                -> GpsDataUpdated event
                  -> GpsPipelineService.OnGpsDataUpdated
                    -> Auto-create LocalPlane (separate instance!)
                    -> ConvertWgs84ToGeoCoord (separate conversion!)
                    -> Tool position calculation
                    -> Section control
                    -> Coverage painting
                    -> GpsCycleResult
                      -> ApplyGpsCycleResult -> UI properties + map
```

### Problems This Causes
1. NMEA parsed twice per cycle (waste)
2. Two separate LocalPlane instances (coordinate divergence)
3. GpsService timeout not updated by zero-copy path (GPS Timeout bug)
4. Position not updated without field (pipeline had no LocalPlane)
5. State split across VehicleState + GpsData + GpsCycleResult
6. NmeaParserService (old) + NmeaParserServiceFast (new) both maintained

---

## Target Architecture

**Threading principle (from Chris):** Receive threads parse only and return
immediately. All heavy per-cycle work runs on a dedicated background worker
with single-cycle-in-flight back-pressure. UI thread only applies result
snapshots.

```
UDP Port 9999
    |
    v
UdpCommunicationService.ReceiveCallback        [I/O thread - fast return]
    |
    v
NmeaParserServiceFast.ParseIntoState (~0.2ms)  [I/O thread - parse only]
    -> Parsed GPS struct (lat/lon/heading/speed/roll)
    |
    v
Hand off to background worker                  [Task.Run, one-in-flight]
    |
    v
GpsCycleWorker.ProcessCycle                     [Background thread]
    -> Auto-create LocalPlane (single instance, shared)
    -> ConvertWgs84ToGeoCoord (once)
    -> CalculateGuidance + SendPgns (PGN 254)
    -> Tool position calculation
    -> Section control update
    -> Coverage painting
    -> Emit unified GpsCycleResult
      |
      v
    UI thread: ApplyGpsCycleResult -> all UI updates
```

---

## Implementation Phases

### Phase 1: Split AutoSteerService receive from processing
- Extract NMEA parsing out of ProcessGpsBuffer into a thin receive handler
- Receive handler: parse NMEA (~0.2ms), hand off parsed struct, return
- ProcessCycle: runs on Task.Run with Interlocked back-pressure (same pattern as current GpsPipelineService)
- ProcessCycle does: coordinate conversion, guidance, PGN send
- This fixes the current violation where guidance runs on the receive thread

### Phase 2: Merge pipeline work into AutoSteerService.ProcessCycle
- Move ToolPositionService calls from GpsPipelineService into AutoSteerService.ProcessCycle
- Move SectionControlService.Update from GpsPipelineService into AutoSteerService.ProcessCycle
- Move coverage painting trigger from GpsPipelineService into AutoSteerService.ProcessCycle
- AutoSteerService now emits a complete GpsCycleResult (not just StateUpdated)
- GpsPipelineService becomes a thin wrapper that just forwards

### Phase 3: Remove GpsPipelineService and old parser path
- MainViewModel subscribes directly to AutoSteerService.CycleCompleted
- Remove GpsPipelineService class entirely
- Remove NmeaParserService (old string-based parser)
- Remove GpsService.UpdateGpsData (no longer called)
- Remove DataReceived -> OnUdpDataReceived -> ParseSentence chain
- GpsService becomes just timeout/connection tracking
- UdpCommunicationService only routes NMEA to AutoSteerService

### Phase 4: Clean up GpsService + single LocalPlane
- Move timeout tracking into AutoSteerService
- GpsService either removed or kept as pure status query interface
- Remove auto-create from both services, add to unified pipeline (one place)
- LocalPlane shared via ApplicationState.Field.LocalPlane

---

## Files Affected

**Remove:**
- Shared/AgValoniaGPS.Services/NmeaParserService.cs (~260 lines)
- Shared/AgValoniaGPS.Services/Pipeline/GpsPipelineService.cs (~550 lines)

**Modify heavily:**
- Shared/AgValoniaGPS.Services/AutoSteer/AutoSteerService.cs (add tool/section/coverage)
- Shared/AgValoniaGPS.ViewModels/MainViewModel.cs (subscribe to AutoSteerService instead of pipeline)
- Shared/AgValoniaGPS.ViewModels/MainViewModel.ApplyResults.cs
- Shared/AgValoniaGPS.Services/UdpCommunicationService.cs (simplify receive path)
- Shared/AgValoniaGPS.Services/GpsService.cs (simplify or remove)

**Modify lightly:**
- Shared/AgValoniaGPS.Services/Interfaces/IGpsService.cs
- Shared/AgValoniaGPS.Services/Interfaces/IGpsPipelineService.cs (remove)
- Platform DI setup files (remove pipeline registration)

**Estimated net:** -500 to -700 lines removed

---

## Risks

- AutoSteerService becomes larger (mitigate: keep tool/section as separate services called from AutoSteerService)
- Thread safety: ProcessCycle runs on background thread, must not touch UI-bound state directly
- Back-pressure: if ProcessCycle > 100ms, GPS updates will be dropped (same as current GpsPipelineService behavior, but now also affects PGN 254 timing)
- Test coverage: need to verify all UI properties still update correctly
- Breaking change for any code subscribing to GpsPipelineService events

## Prerequisites

- Vehicle Simulator working (for testing the refactoring)
- Good integration test coverage of the GPS data flow
