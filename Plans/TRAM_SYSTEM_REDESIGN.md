# Tram System Redesign Plan

## Current State
Single tram configuration with one set of parallel lines + boundary tracks. All settings are global.

## New Design
Multiple tram "systems" - each system is an independent configuration that generates its own set of tram lines. Systems can coexist (e.g., one for sprayer wheel tracks, another for fertilizer tracks).

## Data Model

### Global Settings (TramConfig)
```
WheelTrackWidth: double (m) - vehicle wheel-to-wheel distance
DisplayMode: Off | All | Lines | Boundary
IsDisplayTramControl: bool - show wheel detection indicators
```

### TramSystem (new model, list item)
```
Name: string
ReferenceTrackName: string (or null for boundary)
ReferenceBoundaryIndex: int (0 = outer, 1+ = inner)
TramWidth: double (m) - sprayer/spreader boom width
Mode: TrackLine | Edge
  - TrackLine: tram lines at center of wheel tracks
  - Edge: tram lines at edge of implement
Offset: double (m) - lateral shift from reference
Direction: Left | Right | Symmetric
PassCount: int (0 = unlimited, fills field width)
IsEnabled: bool
```

### Generated Output (per system)
```
ParallelLines: List<List<Vec2>> - the tram line pairs
BoundaryTracks: (outer: List<Vec2>, inner: List<Vec2>) - boundary wheel tracks
```

## Architecture

```
TramConfig (global)
  |-- WheelTrackWidth
  |-- DisplayMode
  |-- Systems: ObservableCollection<TramSystem>
  
TramSystem (per-system)
  |-- Name, Reference, TramWidth, Mode, Offset, Direction, PassCount
  |-- GeneratedLines (computed, not persisted)

TramLineService
  |-- GenerateForSystem(TramSystem, boundary, tracks) -> lines
  |-- GenerateAllSystems() -> combined lines
  |-- DetectTramWheels() -> checks all systems' lines

Field Builder Dialog (Tram Tab)
  |-- System list (like headland lines list)
  |-- Create / Edit / Delete buttons
  |-- Per-system settings panel (shown when editing)
  |-- Canvas preview shows all systems' lines
```

## File Changes

### New Files
| File | Purpose |
|------|---------|
| `Models/Tram/TramSystem.cs` | TramSystem data model with INotifyPropertyChanged |
| `Services/Tram/TramSystemFileService.cs` | JSON save/load for TramSystems.json |

### Modified Files
| File | Changes |
|------|---------|
| `TramConfig.cs` | Remove per-system fields, add Systems collection, keep global WheelTrackWidth + DisplayMode |
| `TramLineService.cs` | Accept TramSystem parameter, generate per-system, combine results |
| `ITramLineService.cs` | Update interface for multi-system |
| `MainViewModel.cs` | TramSystems collection, SelectedTramSystem, CRUD commands |
| `MainViewModel.Commands.Track.cs` | New system management commands |
| `FieldBuilderDialogPanel.axaml` | System list UI, per-system settings panel |
| `FieldBuilderDialogPanel.axaml.cs` | System selection, preview rendering |

## UI Layout (Tram Tab)

```
+-----------------------------------+
| Tram Systems               [+ New]|
|-----------------------------------|
| > Sprayer 24m          [Edit][Del]|
|   Fertilizer 12m       [Edit][Del]|
|-----------------------------------|
| Wheel Track (m): [1.80]          |
| Display: [Off][All][Lines][Bnd]  |
|                                   |
| === Editing: Sprayer 24m ===     |
| Reference: [AB_0.0 Test    v]   |
| Tram Width (m): [24.0]          |
| Mode: [Track Line] [Edge]       |
| Offset (m): [0.0]               |
| Direction: [Left][Right][Symm]  |
| Pass Count: [0] (unlimited)     |
|                                   |
| [Build All]              [Delete] |
+-----------------------------------+
```

## Generation Algorithm (per system)

```
For each TramSystem:
  1. Get reference (track or boundary)
  2. Calculate base offset:
     - TrackLine mode: tramWidth/2 +/- halfWheelTrack
     - Edge mode: tramWidth/2 (no wheel track split)
  3. Apply system offset
  4. Determine passes:
     - Unlimited: fill field width
     - Limited: generate exactly N passes
  5. Apply direction:
     - Symmetric: both sides of reference
     - Left/Right: one side only
  6. For each pass:
     - Generate inner + outer wheel tracks (TrackLine mode)
     - Or single line (Edge mode)
  7. Clip to boundary
  8. Generate boundary tracks if reference is boundary
```

## Migration
- Existing TramConfig.TramWidth/Passes/StartPass -> create one default TramSystem
- Existing TramLines.txt -> load as legacy, convert to system on save
- New format: TramSystems.json alongside TramLines.txt

## PGN 239 Detection
- DetectTramWheels checks ALL systems' generated lines
- Same bit 0=right, bit 1=left
- No per-system detection (hardware module doesn't distinguish)

## Implementation Phases

### Phase 1: Data Model + Service
1. Create TramSystem model
2. Add Systems collection to TramConfig
3. Update TramLineService for per-system generation
4. File service for save/load
5. Migration from old format

### Phase 2: UI
1. System list in Tram tab
2. Create/Edit/Delete flows
3. Per-system settings panel
4. Canvas preview per system (different colors?)
5. Build All button

### Phase 3: Advanced
1. Edge mode implementation
2. Direction modes (left/right/symmetric)
3. Limited pass count
4. Per-system enable/disable
