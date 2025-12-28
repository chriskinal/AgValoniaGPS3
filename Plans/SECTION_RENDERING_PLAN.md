# Feature: Individual Section Rendering

## Overview

Enhance tool rendering to display individual sections with state-based coloring, matching AgOpenGPS behavior. Currently the tool renders as a single solid block; this plan subdivides it into individual sections that reflect on/off state.

## Current State

**Screenshot Analysis:** The tool currently renders as 6 red triangular sections that appear as a single solid block - no visual separation between sections, no state-based coloring.

**Current Implementation:**
- `DrawingContextMapControl.DrawTool()` (lines 587-613) renders tool as single rectangle
- Uses `_toolBrush` (semi-transparent brownish-red) for all rendering
- No section subdivision or state awareness

## WinForms Reference

**File:** `/Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/Classes/CTool.cs` (lines 499-534)

**Key Implementation Details:**

1. **Section Shape:** Triangle fan creating pentagonal shape per section
   - Front edge at tool position
   - Back edge with center point deeper for visual perspective

2. **Color States:**
   | State | Button | Mapping | Color | RGB |
   |-------|--------|---------|-------|-----|
   | ON | Auto | Yes | Bright Green | (0, 242, 0) |
   | ON | Auto | No | Magenta | (247, 77, 247) |
   | ON | Manual | - | Yellow | (247, 247, 0) |
   | OFF | - | No | Red | (242, 51, 51) |
   | OFF | - | Yes | Blue | (0, 64, 247) |

3. **Section Positioning:**
   - Each section has `positionLeft` and `positionRight` (meters from center)
   - Left side negative, right side positive
   - Sections distributed across total tool width

## Existing Infrastructure

### UI Panel (SectionControlPanel.axaml)

A floating panel already exists showing sections 1-6 as buttons:
- Binds to `Section1Active` through `Section6Active` in MainViewModel
- Green (#2ECC71) when active, dark gray (#2C3E50) when inactive
- Currently **display-only** - no click handlers for manual toggle

**Current Binding:**
```xml
<Border Classes="SectionButton" Classes.Active="{Binding Section1Active}">
    <TextBlock Text="1"/>
</Border>
```

### Data Flow Gap

**Problem:** The existing components are not connected:

```
┌─────────────────────┐     ┌──────────────────────┐     ┌─────────────────────┐
│ SectionControlPanel │     │ MainViewModel        │     │ SectionControlService│
│ (UI Display)        │────▶│ Section1Active...    │     │ SectionStates[]      │
│                     │     │ (backing fields only)│  X  │ (not connected)      │
└─────────────────────┘     └──────────────────────┘     └─────────────────────┘
                                      │
                                      ▼
                            ┌──────────────────────┐
                            │DrawingContextMapControl│
                            │ (no section state)    │
                            └──────────────────────┘
```

**Required Integration:**
1. MainViewModel needs `ISectionControlService` injected
2. Section*Active properties should reflect service state
3. Manual toggle commands need to update service state
4. DrawingContextMapControl needs section state for rendering

**Target Data Flow (after implementation):**

```
┌─────────────────────┐     ┌──────────────────────┐     ┌─────────────────────┐
│ SectionControlPanel │◀───▶│ MainViewModel        │◀───▶│ SectionControlService│
│ (UI Display+Toggle) │     │ Section1Active...    │     │ SectionStates[]      │
│                     │     │ ToggleSectionCommand │     │ SetManualState()     │
└─────────────────────┘     └──────────────────────┘     └─────────────────────┘
                                      │
                                      ▼
                            ┌──────────────────────┐
                            │DrawingContextMapControl│
                            │ SetSectionStates()    │
                            │ DrawSections()        │
                            └──────────────────────┘
```

### Data Sources Available

1. **Section Count & Widths:**
   - `ConfigurationStore.Instance.NumSections` (1-16)
   - `ConfigurationStore.Instance.Tool.GetSectionWidth(index)` (cm)
   - `ConfigurationStore.Instance.ActualToolWidth` (meters)

2. **Section State (service - needs wiring):**
   - `SectionControlService.SectionStates[index].IsOn`
   - `SectionControlService.SectionStates[index].IsMappingOn`
   - Service is registered in DI but NOT injected into MainViewModel

3. **Section State (ViewModel - disconnected):**
   - `MainViewModel.Section1Active` through `Section7Active`
   - Currently just backing fields, not connected to service

4. **Position Calculation:**
   - `ISectionControlService.GetSectionWorldPosition(index, toolPos, heading)` returns `(Vec2 left, Vec2 right)`

### Key Files

| Component | Location |
|-----------|----------|
| UI Panel | `Shared/AgValoniaGPS.Views/Controls/Panels/SectionControlPanel.axaml` |
| Rendering | `Shared/AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs` |
| Tool Config | `Shared/AgValoniaGPS.Models/Configuration/ToolConfig.cs` |
| Section State | `Shared/AgValoniaGPS.Models/State/SectionState.cs` |
| Section Service | `Shared/AgValoniaGPS.Services/Section/SectionControlService.cs` |
| Config Store | `Shared/AgValoniaGPS.Models/Configuration/ConfigurationStore.cs` |

## Implementation Plan

### Phase 0: Service Integration & Manual Control

**Goal:** Connect SectionControlService to MainViewModel and enable manual toggle

1. [ ] Inject ISectionControlService into MainViewModel
   - Add constructor parameter `ISectionControlService sectionControlService`
   - Store as `_sectionControlService` field
   - Update DI registrations in both Desktop and iOS

2. [ ] Sync Section*Active properties with service state
   - In GPS update loop, read `_sectionControlService.SectionStates[i].IsOn`
   - Update `Section1Active` through `Section7Active` accordingly
   - This makes UI panel reflect actual service state

3. [ ] Add manual toggle commands
   - Add `ToggleSection1Command` through `ToggleSection6Command` (or parameterized command)
   - Command toggles `_sectionControlService.SetManualState(index, !current)`
   - Update SectionControlPanel.axaml to add PointerPressed handlers

4. [ ] Update SectionControlPanel UI for tappable sections
   ```xml
   <Border Classes="SectionButton" Classes.Active="{Binding Section1Active}"
           PointerPressed="Section1_PointerPressed">
   ```
   Or use Command binding with CommandParameter for section index

### Phase 1: Section Data to Rendering

**Goal:** Pass section configuration to DrawingContextMapControl

1. [ ] Add section state interface to DrawingContextMapControl
   - Add `SetSectionStates(bool[] sectionActive, int numSections)` method
   - Add `SetSectionWidths(double[] widths)` method (or calculate from config)
   - Store section state array for rendering

2. [ ] Wire up section state from MainViewModel
   - Subscribe to section state changes in platform code
   - Call `SetSectionStates()` when any section changes
   - Initial state from `ConfigurationStore.Instance`

3. [ ] Calculate section positions locally
   - Compute `positionLeft` and `positionRight` for each section
   - Based on section widths from configuration
   - Center at tool position (0,0 local coordinates)

### Phase 2: Individual Section Rendering

**Goal:** Draw each section as a separate shape with gaps

1. [ ] Replace `DrawTool()` with `DrawSections()`
   - Loop through `numSections`
   - Draw each section individually
   - Use consistent shape (pentagon/triangle fan style)

2. [ ] Section shape geometry
   - Front edge: `positionLeft` to `positionRight` at tool front
   - Back edge: Tapered or straight at tool depth
   - Use `StreamGeometry` for efficient path building

3. [ ] Add visual separation
   - Small gap (1-2 pixels) between adjacent sections
   - Or use outline stroke to differentiate

### Phase 3: State-Based Coloring

**Goal:** Color sections based on on/off/mapping state

1. [ ] Create section color brushes
   ```csharp
   _sectionOnBrush = new SolidColorBrush(Color.FromRgb(0, 242, 0));      // Green
   _sectionOffBrush = new SolidColorBrush(Color.FromRgb(242, 51, 51));   // Red
   _sectionManualBrush = new SolidColorBrush(Color.FromRgb(247, 247, 0)); // Yellow
   _sectionMappingBrush = new SolidColorBrush(Color.FromRgb(0, 64, 247)); // Blue
   ```

2. [ ] Implement color selection logic
   ```csharp
   IBrush GetSectionBrush(int index)
   {
       var state = _sectionStates[index];
       if (state.IsOn)
           return state.IsMappingOn ? _sectionOnBrush : _sectionMagentaBrush;
       else
           return state.IsMappingOn ? _sectionMappingBrush : _sectionOffBrush;
   }
   ```

3. [ ] Apply colors during rendering
   - Each section uses its own brush based on state
   - Update colors each frame (state may change)

### Phase 4: Polish & Integration

**Goal:** Match AgOpenGPS visual fidelity

1. [ ] Add section outlines
   - Black outline when zoomed in (camera distance check)
   - 1px stroke around each section shape

2. [ ] Perspective depth effect (optional)
   - Vary back edge depth based on camera zoom
   - Center back point deeper than corners (pentagon shape)

3. [ ] Performance optimization
   - Cache section geometries when widths don't change
   - Only rebuild when configuration changes
   - Reuse brushes (don't create per-frame)

## Testing Checklist

**Phase 0 - Service Integration:**
- [ ] SectionControlService injected into MainViewModel
- [ ] UI panel buttons reflect service state
- [ ] Tapping section button toggles that section
- [ ] Section state persists across GPS updates

**Phase 1-3 - Rendering:**
- [ ] Sections display correct count from configuration
- [ ] Section widths match configuration (visual inspection)
- [ ] Sections change color when toggled on/off via UI panel
- [ ] Map rendering matches UI panel state (both green/red together)
- [ ] All configured sections visible (test 1, 6, 16 sections)
- [ ] Visual separation between adjacent sections
- [ ] Performance acceptable at 30 FPS
- [ ] Works on both Desktop and iOS

## Implementation Notes

### Coordinate System
- Tool position is in world coordinates (meters)
- Section positions are relative to tool center
- Transform matrix handles world-to-screen conversion

### Section Position Calculation
```csharp
// Calculate section positions from widths
double runningPosition = -totalWidth / 2.0;
for (int i = 0; i < numSections; i++)
{
    double sectionWidth = sectionWidths[i];
    sectionLeft[i] = runningPosition;
    sectionRight[i] = runningPosition + sectionWidth;
    runningPosition += sectionWidth;
}
```

### Shape Drawing Pattern
```csharp
// Pentagon shape for each section (like AgOpenGPS)
using var geometry = new StreamGeometry();
using (var ctx = geometry.Open())
{
    double mid = (left + right) / 2;
    ctx.BeginFigure(new Point(left, front), true);   // Left front
    ctx.LineTo(new Point(left, back));               // Left back
    ctx.LineTo(new Point(mid, back - depth));        // Center back (deeper)
    ctx.LineTo(new Point(right, back));              // Right back
    ctx.LineTo(new Point(right, front));             // Right front
    ctx.EndFigure(true);
}
context.DrawGeometry(brush, pen, geometry);
```

## Dependencies

- No new packages required
- Uses existing Avalonia DrawingContext API
- Uses existing SectionControlService and ConfigurationStore

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Performance with 16 sections | Cache geometries, minimize allocations |
| State synchronization lag | Use direct property access, not events |
| Configuration changes mid-render | Rebuild section data on config change |
