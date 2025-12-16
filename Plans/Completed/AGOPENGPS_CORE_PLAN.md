# AgOpenGPS.Core Integration Plan

## Decision: Do NOT integrate AgOpenGPS.Core directly

AgValoniaGPS3 already has its own services in `Shared/AgValoniaGPS.Services/` that largely duplicate what's in AgOpenGPS.Core. Rather than maintaining two sets of similar services, we will:

## Action Items

### 1. Revert AgOpenGPS.Core from AgValoniaGPS3 ✅ DONE
- [x] Remove `Shared/AgOpenGPS.Core/` directory
- [x] Remove AgOpenGPS.Core from solution file
- [x] Verify build still works

### 2. Audit AgValoniaGPS.Services against AgOpenGPS.Core ✅ DONE
Compare implementations to ensure correctness. Key services to audit:

| AgValoniaGPS.Services | AgOpenGPS.Core Reference | Status |
|-----------------------|--------------------------|--------|
| Guidance/PurePursuitGuidanceService.cs | Services/Guidance/PurePursuitGuidanceService.cs | ✅ PASS (identical) |
| Guidance/StanleyGuidanceService.cs | Services/Guidance/StanleyGuidanceService.cs | ✅ PASS (identical) |
| Guidance/CurvePurePursuitGuidanceService.cs | Services/Guidance/CurvePurePursuitGuidanceService.cs | ✅ PASS (identical) |
| Guidance/ContourPurePursuitGuidanceService.cs | Services/Guidance/ContourPurePursuitGuidanceService.cs | ✅ PASS (identical) |
| Track/TrackNudgingService.cs | Services/Track/TrackNudgingService.cs | ✅ PASS (identical) |
| YouTurn/YouTurnGuidanceService.cs | Services/YouTurn/YouTurnGuidanceService.cs | ✅ PASS (identical) |
| YouTurn/YouTurnCreationService.cs | Services/YouTurn/YouTurnCreationService.cs | ✅ PASS (identical) |
| Geometry/WorkedAreaService.cs | Services/Geometry/WorkedAreaService.cs | ✅ PASS (identical) |
| Geometry/FenceLineService.cs | Services/Geometry/FenceLineService.cs | ✅ PASS (identical) |
| Geometry/FenceAreaService.cs | Services/Geometry/FenceAreaService.cs | ✅ PASS (identical) |
| Geometry/TurnAreaService.cs | Services/Geometry/TurnAreaService.cs | ✅ PASS (identical) |
| Geometry/TurnLineService.cs | Services/Geometry/TurnLineService.cs | ✅ PASS (identical) |
| TramlineService.cs | Services/TramlineService.cs | ✅ PASS (identical) |
| GpsService.cs | Services/GpsService.cs | ✅ PASS (identical) |
| GpsSimulationService.cs | Services/GpsSimulationService.cs | ⚠️ ENHANCED (faster acceleration) |
| FieldStatisticsService.cs | Services/FieldStatisticsService.cs | ⚠️ ENHANCED (extra methods) |
| Boundary/HeadlandDetectionService.cs | Services/Boundary/HeadlandDetectionService.cs | ✅ PASS (identical) |
| PathPlanning/DubinsPathService.cs | Services/PathPlanning/DubinsPathService.cs | ✅ PASS (identical) |
| AgShare/* | Services/AgShare/* | N/A (not implemented) |
| IsoXml/* | Services/IsoXml/* | N/A (not implemented) |

### Audit Notes

**GpsSimulationService.cs** - AgValoniaGPS3 version has enhanced acceleration constants:
- Faster acceleration (0.03 vs 0.02 step)
- Higher max speed (25 kph vs ~4.8 kph stepDistance limit)
- Full reverse support (-10 kph)
- Better documented constants

**FieldStatisticsService.cs** - AgValoniaGPS3 version has additional features:
- `WorkedAreaSquareMeters`, `UserDistance`, `BoundaryAreaSquareMeters` properties
- `UpdateBoundaryArea(Boundary?)` overload for direct boundary object
- `CalculateOverlap()`, `GetRemainingAreaHectares()`, `FormatArea()`, `FormatDistance()` methods
- Different `GetDescription()` formatting

Both enhancements in AgValoniaGPS3 are improvements over the original - no action needed.

### 3. Use AgOpenGPS.Core as Reference
- AgOpenGPS.Core lives in `../AgValoniaGPS2/Shared/AgOpenGPS.Core/`
- When creating new services in AgValoniaGPS3, check AgOpenGPS.Core for proven implementations
- Copy algorithms/logic as needed, adapting to AgValoniaGPS3 patterns

## Rationale

1. **Duplication**: Most services already exist in AgValoniaGPS.Services
2. **Maintenance burden**: Two sets of similar services is confusing
3. **WPF baggage**: AgOpenGPS.Core has WPF-specific code that needs stripping
4. **AgValoniaGPS3 has evolved**: The codebase has its own patterns and DI setup

## Reference Location

AgOpenGPS.Core source (for comparison/reference):
```
/Users/chris/Code/AgValoniaGPS2/SourceCode/AgOpenGPS.Core/
```
