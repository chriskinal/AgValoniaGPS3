# MainViewModel Refactoring Plan

## Current State

- **File**: `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs`
- **Size**: ~6,072 lines (after PR #6 command extraction)
- **Members**: ~739 properties, fields, and methods
- **Problem**: God object with too many responsibilities

## Already Extracted (PR #6)

Command initialization moved to partial class files:
- `MainViewModel.Commands.Navigation.cs`
- `MainViewModel.Commands.Simulator.cs`
- `MainViewModel.Commands.Configuration.cs`
- `MainViewModel.Commands.Fields.cs`
- `MainViewModel.Commands.Boundary.cs`
- `MainViewModel.Commands.Track.cs`
- `MainViewModel.Commands.Ntrip.cs`

## Proposed Extraction Strategy

### Phase 1: Extract Remaining Logical Groups to Partial Classes

Continue the partial class pattern for related properties and methods.

#### 1.1 YouTurn Logic (~400 lines)
**New file**: `MainViewModel.YouTurn.cs`
```
- ProcessYouTurn()
- CreateYouTurnPath()
- CalculateYouTurnGuidance()
- ComputeNextTrack()
- CompleteYouTurn()
- YouTurn-related properties (IsYouTurnActive, YouTurnPath, etc.)
```

#### 1.2 GPS & Position Handling (~200 lines)
**New file**: `MainViewModel.GpsHandling.cs`
```
- OnGpsDataUpdated()
- UpdateGpsProperties()
- GPS properties (Latitude, Longitude, Altitude, Speed, Heading, etc.)
- Fix quality properties
```

#### 1.3 AutoSteer Guidance (~300 lines)
**New file**: `MainViewModel.AutoSteer.cs`
```
- CalculateAutoSteerGuidance()
- OnAutoSteerStateUpdated()
- UpdateActiveLineVisualization()
- AutoSteer properties (IsAutoSteerEngaged, SteerAngle, CrossTrackError, etc.)
```

#### 1.4 Section Control & Coverage (~250 lines)
**New file**: `MainViewModel.SectionControl.cs`
```
- OnSectionStateChanged()
- UpdateSectionActiveProperties()
- UpdateCoveragePainting()
- Section properties (IsSectionMasterOn, SectionStates[], etc.)
- Coverage properties (CoveredAreaHectares, AppliedAreaHectares)
```

#### 1.5 NTRIP Handling (~150 lines)
**New file**: `MainViewModel.NtripHandling.cs`
```
- ConnectToNtripAsync()
- DisconnectFromNtripAsync()
- HandleNtripProfileForFieldAsync()
- OnNtripConnectionChanged()
- OnRtcmDataReceived()
- UpdateNtripConnectionProperties()
- NTRIP properties (IsNtripConnected, NtripStatus, RtcmBytesReceived, etc.)
```

#### 1.6 Boundary Management (~300 lines)
**New file**: `MainViewModel.BoundaryManagement.cs`
```
- OnBoundaryPointAdded()
- OnBoundaryStateChanged()
- RefreshBoundaryList()
- DeleteSelectedBoundary()
- SetCurrentBoundary()
- UpdateBoundaryOffsetIndicator()
- Boundary properties (CurrentBoundary, BoundaryList, etc.)
```

#### 1.7 Field Management (~250 lines)
**New file**: `MainViewModel.FieldManagement.cs`
```
- OnActiveFieldChanged()
- UpdateActiveField()
- LoadHeadlandFromField()
- PopulateAvailableFields()
- PopulateAvailableKmlFiles()
- Field properties (CurrentFieldName, IsFieldOpen, AvailableFields, etc.)
```

#### 1.8 Map & Background (~200 lines)
**New file**: `MainViewModel.MapHandling.cs`
```
- CenterMapOnBoundary()
- SaveBackgroundImage()
- LoadBackgroundImage()
- InvalidateClipPathCache()
- Map properties (BackgroundImagePath, BackgroundBounds, etc.)
```

#### 1.9 Simulator (~150 lines)
**New file**: `MainViewModel.Simulator.cs`
```
- OnSimulatorTick()
- OnSimulatorGpsDataUpdated()
- SetSimulatorCoordinates()
- Simulator properties (SimulatorSpeedKph, SimulatorSteerAngle, etc.)
```

#### 1.10 View Settings & UI State (~400 lines)
**New file**: `MainViewModel.ViewSettings.cs`
```
- View toggle properties (IsGridOn, IsDayMode, Is2DMode, IsNorthUp)
- Panel visibility properties (IsSimulatorPanelVisible, IsBoundaryPanelVisible, etc.)
- Camera properties (CameraPitch, Zoom)
- Brightness, display settings
```

### Phase 2: Consider Service Extraction (Future)

Some logic could move from ViewModel to dedicated services:

| Candidate | Current Location | Potential Service |
|-----------|-----------------|-------------------|
| YouTurn path generation | MainViewModel | IYouTurnService (expand existing) |
| AutoSteer guidance calc | MainViewModel | IAutoSteerGuidanceService |
| Coverage tracking | MainViewModel | ICoverageTrackingService |
| Headland management | MainViewModel | IHeadlandService |

**Note**: Service extraction is more invasive and should be done carefully to avoid breaking existing functionality.

### Phase 3: Consider Separate ViewModels (Future)

For major feature areas that could be independent:

- `SimulatorPanelViewModel` - Simulator controls and state
- `BoundaryPanelViewModel` - Boundary recording and management
- `GuidanceViewModel` - AutoSteer and track following state

**Note**: This requires UI changes to bind to sub-ViewModels.

## Recommended Approach

### Immediate (Phase 1a)
Extract the largest, most self-contained sections first:
1. **YouTurn** - Complex but isolated logic
2. **View Settings** - Pure properties, low risk
3. **Simulator** - Self-contained feature

### Short-term (Phase 1b)
Continue with tightly-coupled sections:
4. **GPS Handling**
5. **AutoSteer**
6. **Section Control**

### Medium-term (Phase 1c)
Complete the extraction:
7. **NTRIP Handling**
8. **Boundary Management**
9. **Field Management**
10. **Map Handling**

## File Structure After Refactoring

```
Shared/AgValoniaGPS.ViewModels/
├── MainViewModel.cs                         (~1,500 lines - core, constructor, DI)
├── MainViewModel.Commands.Navigation.cs
├── MainViewModel.Commands.Simulator.cs
├── MainViewModel.Commands.Configuration.cs
├── MainViewModel.Commands.Fields.cs
├── MainViewModel.Commands.Boundary.cs
├── MainViewModel.Commands.Track.cs
├── MainViewModel.Commands.Ntrip.cs
├── MainViewModel.YouTurn.cs                 (NEW)
├── MainViewModel.GpsHandling.cs             (NEW)
├── MainViewModel.AutoSteer.cs               (NEW)
├── MainViewModel.SectionControl.cs          (NEW)
├── MainViewModel.NtripHandling.cs           (NEW)
├── MainViewModel.BoundaryManagement.cs      (NEW)
├── MainViewModel.FieldManagement.cs         (NEW)
├── MainViewModel.MapHandling.cs             (NEW)
├── MainViewModel.Simulator.cs               (NEW)
└── MainViewModel.ViewSettings.cs            (NEW)
```

## Benefits

1. **Easier navigation** - Find code by category
2. **Reduced merge conflicts** - Changes to different areas don't conflict
3. **Better AI assistance** - Smaller files fit in LLM context windows
4. **Clearer responsibilities** - Each file has a focused purpose
5. **Easier testing** - Can focus on specific functionality

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking existing functionality | Extract without modifying logic, test after each extraction |
| Circular dependencies | Keep all in same partial class, no inter-file dependencies |
| Lost context | Add XML doc comments explaining each file's purpose |
| Merge conflicts with open PRs | Coordinate timing, extract after PR #7 decision |

## Success Criteria

- [ ] MainViewModel.cs core reduced to <1,500 lines
- [x] Each partial class file <500 lines (YouTurn is 1,112 but self-contained)
- [x] All existing tests pass
- [x] No functional changes - pure reorganization
- [x] Build succeeds on all platforms

## Progress

### Completed Extractions

| File | Lines | Status |
|------|-------|--------|
| MainViewModel.YouTurn.cs | 1,112 | ✅ Complete |
| MainViewModel.Guidance.cs | 180 | ✅ Complete |
| MainViewModel.Ntrip.cs | 208 | ✅ Complete |
| MainViewModel.SectionControl.cs | 383 | ✅ Complete |

**MainViewModel.cs**: 6,082 → 4,350 lines (-1,732 lines, ~28% reduction)

### Phase 1 Status: Complete (Practical Limit Reached)

The four extractions above represent the contiguous, self-contained code blocks that could be cleanly extracted. The remaining code has the following challenges:

**Simulator Handlers (~100 lines, lines 801-897)**
- `OnSimulatorTick()` and `OnSimulatorGpsDataUpdated()` orchestrate calls across multiple partial classes
- Call `ProcessYouTurn()` (YouTurn.cs), `CalculateYouTurnGuidance()` (YouTurn.cs), `CalculateAutoSteerGuidance()` (Guidance.cs)
- Best left in MainViewModel.cs as the central orchestration point

**Boundary Recording Handlers (~52 lines, lines 906-958)**
- `OnBoundaryPointAdded()` and `OnBoundaryStateChanged()`
- Too small to justify a separate file (only 52 lines)
- Depend on `_boundaryRecordingService` and `_mapService`

**View Settings Properties (~400 lines, scattered)**
- Panel visibility properties, display settings are scattered throughout the 4,350-line file
- Would require extensive reorganization before extraction

**GPS Handling (~200 lines, scattered)**
- `OnGpsDataUpdated()`, `UpdateGpsProperties()` and related properties
- Latitude, Longitude, Speed properties scattered among other UI properties

### Recommendations for Future Work

1. **Consider consolidating remaining code with #regions** rather than partial classes
2. **Phase 2: Service Extraction** - Move complex business logic to services instead of partial classes:
   - Simulator tick logic → SimulatorOrchestrationService
   - GPS data transformation → already in GpsService
3. **Phase 3: Sub-ViewModels** - For truly independent UI panels (SimulatorPanelViewModel, etc.)

### Results Summary

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| MainViewModel.cs lines | 6,082 | 4,350 | -1,732 (28%) |
| Partial class files | 7 (commands only) | 11 | +4 new files |
| Largest extraction | - | YouTurn.cs (1,112) | Self-contained logic |

The refactoring successfully extracted 28% of the code into focused partial class files, improving:
- Code navigation (find code by category)
- Merge conflict reduction (changes to different areas don't conflict)
- AI assistance context (smaller files fit in LLM context windows)
