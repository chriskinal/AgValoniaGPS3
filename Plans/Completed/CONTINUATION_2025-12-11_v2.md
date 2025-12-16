# Continuation Prompt - December 11, 2025 (Session 2)

## Session Summary

This session focused on architectural analysis and creating three major refactoring plans based on a tip from Brian (AgOpenGPS creator) about unifying the curves/track classes.

## Key Insight from Brian

> "If you tweak the curves class you can use that for a track class and completely remove ABLine, CCurve and about 87 different guidance functions."

This led to a broader analysis of duplication and architectural issues across the codebase.

## Three Refactoring Plans Created

### 1. UNIFIED_TRACK_REFACTOR_PLAN.md
**Target:** Guidance code consolidation
**Reduction:** ~65% (3,393 → ~1,200 lines)

Key insight: An AB line is just a 2-point curve. All track types can use `List<Vec3>`:
- AB Line = 2 points
- Curve = N points
- Contour = N points
- Boundary track = N points (closed)

**Files to delete:** 5 guidance services, 5 interfaces, 10+ models
**Files to create:** `Track.cs`, `TrackGuidanceService.cs`, unified input/output models

### 2. CONFIGURATION_REFACTOR_PLAN.md
**Target:** Configuration single source of truth
**Reduction:** ~60% (1,458 → ~600 lines)

Problems found:
- `Vehicle.cs` duplicates `VehicleConfiguration.cs`
- Simulator settings in both `AppSettings` and `VehicleProfile`
- `ConfigurationViewModel` has 685 lines of property mapping boilerplate
- `AhrsConfiguration` mixes runtime state with config

**Solution:** `ConfigurationStore` with sub-configs (VehicleConfig, ToolConfig, GuidanceConfig, DisplayConfig, SimulatorConfig, ConnectionConfig)

**Biggest win:** ConfigurationViewModel 685 → ~80 lines by binding directly to models

### 3. STATE_CENTRALIZATION_PLAN.md
**Target:** MainViewModel God Object elimination
**Reduction:** ~93% (7,045 → ~500 lines)

Problems found:
- MainViewModel has **207 private fields**
- 25+ dialog visibility boolean flags
- GPS state duplicated in 3 places
- State scattered across services

**Solution:** `ApplicationState` with domain states:
- VehicleState (position, heading, speed, IMU)
- GuidanceState (XTE, steer angle, PP state)
- SectionState (section on/off, coverage)
- ConnectionState (GPS/NTRIP/AutoSteer status)
- FieldState (boundaries, tracks, headlands)
- YouTurnState (turn path, trigger, direction)
- BoundaryState (recording state)
- UIState (ActiveDialog enum replaces 25+ booleans)

## Meta-Plan: Execution Order

### Phase 1: Configuration (Do First)
- Smallest scope, lowest risk
- Establishes "single source of truth" pattern
- Template for ApplicationState

### Phase 2: Track Unification (Can Parallel with Phase 1)
- Independent code area
- Brian-validated approach
- Self-contained

### Phase 3: State Centralization (Do Last)
- Largest scope, highest risk
- Benefits from Phase 1 lessons
- Do incrementally:
  1. UIState (dialog flags → enum)
  2. VehicleState (GPS consolidation)
  3. ConnectionState
  4. GuidanceState (after track unification)
  5. YouTurnState
  6. Remaining (Boundary, Field, Sections)

## Quick Wins (Pre-Refactor)

1. Delete `Vehicle.cs` (duplicate of VehicleConfiguration)
2. Remove simulator settings from VehicleProfile
3. Extract dialog flags to DialogState class (prep work)

## Current Code Statistics

| Area | Lines | Notes |
|------|-------|-------|
| Shared | 37,530 | 93.7% of codebase |
| Desktop | 1,644 | 4.1% |
| iOS | 873 | 2.2% |
| MainViewModel | 7,045 | God Object - 207 fields |
| Guidance code | 3,393 | Significant duplication |
| Config code | 1,458 | Multiple sources of truth |

## Combined Potential Reduction

| Plan | Before | After | Savings |
|------|--------|-------|---------|
| Track Unification | 3,393 | ~1,200 | ~2,200 lines |
| Configuration | 1,458 | ~600 | ~850 lines |
| State Centralization | 7,045 | ~500 | ~6,500 lines |
| **Total** | **11,896** | **~2,300** | **~9,600 lines** |

## Earlier Session Work (Same Day)

From CONTINUATION_2025-12-11.md:
- Refactored ConfigurationDialog to use TabControl (747 → 152 lines)
- Improved cross-platform code reuse (93.1% → 93.7%)
- Created DialogOverlayHost, SharedStyles, SharedResources, DragBehavior

## Files Created This Session

```
UNIFIED_TRACK_REFACTOR_PLAN.md   - Track/guidance consolidation plan
CONFIGURATION_REFACTOR_PLAN.md   - Config single source of truth plan
STATE_CENTRALIZATION_PLAN.md     - ApplicationState centralization plan
CONTINUATION_2025-12-11_v2.md    - This file
```

## Control Library Research

Evaluated Dock (wieslawsoltes) and Actipro for potential use:
- **Conclusion:** Not suitable - iOS not officially supported
- Cross-platform parity is a deal-killer for third-party control libraries
- Stick with core Avalonia primitives for shared code

## Git Status

Branch: master
No new commits this session (planning/documentation only)

## Build Commands

```bash
# Desktop
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj

# iOS (requires macOS)
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj -c Debug -f net10.0-ios -r iossimulator-arm64 -t:Run
```

## Next Steps

1. Review the three plan documents
2. Decide which phase to start (recommend: Configuration)
3. Get Brian's feedback on track unification approach if possible
4. Consider quick wins before major refactoring
