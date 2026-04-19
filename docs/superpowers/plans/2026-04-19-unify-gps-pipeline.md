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

```
UDP Port 9999
    |
    v
UdpCommunicationService.ReceiveCallback
    |
    v
AutoSteerService.ProcessGpsBuffer (single entry point)
    -> NmeaParserServiceFast.ParseIntoState (only parser)
    -> VehicleState
    -> Auto-create LocalPlane (single instance, shared)
    -> ConvertWgs84ToGeoCoord (once)
    -> CalculateGuidance + SendPgns
    -> Tool position calculation (moved from pipeline)
    -> Section control update (moved from pipeline)
    -> Coverage painting (moved from pipeline)
    -> Emit unified GpsCycleResult
      -> UI thread: ApplyGpsCycleResult -> all UI updates
```

---

## Implementation Phases

### Phase 1: Move tool/section/coverage into AutoSteerService
- Move ToolPositionService calls from GpsPipelineService into AutoSteerService
- Move SectionControlService.Update from GpsPipelineService into AutoSteerService
- Move coverage painting trigger from GpsPipelineService into AutoSteerService
- AutoSteerService now emits a complete GpsCycleResult (not just StateUpdated)
- GpsPipelineService becomes a thin wrapper that just forwards

### Phase 2: Remove GpsPipelineService
- MainViewModel subscribes directly to AutoSteerService.CycleCompleted
- Remove GpsPipelineService class entirely
- Remove _gpsPipelineService from MainViewModel constructor

### Phase 3: Remove old parser path
- Remove NmeaParserService (old string-based parser)
- Remove GpsService.UpdateGpsData (no longer called)
- Remove DataReceived -> OnUdpDataReceived -> ParseSentence chain
- GpsService becomes just timeout/connection tracking
- UdpCommunicationService only routes NMEA to AutoSteerService

### Phase 4: Clean up GpsService
- Move timeout tracking into AutoSteerService
- GpsService either removed or kept as pure status query interface
- Remove _lastGpsDataReceived, MarkGpsReceived (no longer needed)

### Phase 5: Single LocalPlane
- Remove auto-create from both AutoSteerService and GpsPipelineService
- Add auto-create to the unified pipeline (one place)
- LocalPlane shared via ApplicationState.Field.LocalPlane
- Field open replaces it, field close removes it

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
- Thread safety: everything runs on the UDP receive thread (currently works, just more code there)
- Test coverage: need to verify all UI properties still update correctly
- Breaking change for any code subscribing to GpsPipelineService events

## Prerequisites

- Vehicle Simulator working (for testing the refactoring)
- Good integration test coverage of the GPS data flow
