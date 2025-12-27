# Feature: Tool/Implement Rendering

## Overview

Add visual rendering of the tool/implement behind the tractor on the map display. The tool should accurately reflect the configured tool type (Fixed Front/Rear, Trailing, TBT), width, sections, and hitch geometry.

## WinForms Reference

- **File**: `GPS/Classes/CTool.cs` - `DrawTool()` method (lines 406-576)
- **Called from**: `GPS/Forms/OpenGL.Designer.cs` line 435: `tool.DrawTool()`
- **Order**: Tool is drawn BEFORE vehicle: `tool.DrawTool(); vehicle.DrawVehicle();`

### Key Rendering Elements

1. **Hitch Lines** (triangle shapes connecting vehicle → tank → tool)
   - `DrawHitch()` - Vehicle hitch to tank (for TBT)
   - `DrawTrailingHitch()` - Hitch to tool pivot

2. **Tool Wheels** (for trailing implements with pivot offset)
   - Rendered when `trailingToolToPivotLength > 1m` and camera close

3. **Sections** (colored arrow shapes)
   - Each section drawn as a filled triangle fan
   - Color indicates state: green=on+auto, yellow=on+manual, red=off, purple=on-not-mapping, blue=off+mapping

4. **Lookahead Lines** (optional, when job started)
   - Green line: section turn-on lookahead
   - Red line: section turn-off lookahead

5. **Zone Separators** (optional, when using zones not sections)

6. **Tram Markers** (optional, when tram control enabled)

## State Variables Required

Already available via `IToolPositionService`:
- `ToolPosition` (Vec3) - Tool center position
- `ToolPivotPosition` (Vec3) - Tool pivot point
- `ToolHeading` (double) - Tool heading in radians
- `TankPosition` (Vec3) - Tank position (for TBT)
- `HitchPosition` (Vec3) - Hitch point on vehicle

From `ConfigurationStore.Instance.Tool`:
- `Width` - Total tool width in meters
- `HitchLength` - Distance from vehicle pivot to hitch
- `TrailingHitchLength` - Hitch to tool pivot distance
- `TankTrailingHitchLength` - Hitch to tank distance (TBT)
- `TrailingToolToPivotLength` - Pivot to tool center offset
- `IsToolFrontFixed`, `IsToolRearFixed`, `IsToolTrailing`, `IsToolTBT` - Tool type flags
- `Offset` - Lateral offset
- `NumSections` - Number of sections
- `GetSectionWidth(index)` - Width of each section

## Service Dependencies

- `IToolPositionService` - Already implemented, provides all position data
- `ISectionControlService` - Section on/off states (if rendering section states)
- `ConfigurationStore` - Tool configuration

## Implementation Steps

### Phase 1: Basic Tool Rectangle (MVP) ✅ COMPLETE
1. [x] Add tool position properties to `MainViewModel` (ToolEasting, ToolNorthing, ToolHeadingRadians, ToolWidth, HitchEasting, HitchNorthing)
2. [x] Subscribe to `IToolPositionService.PositionUpdated` event in MainViewModel
3. [x] Add `SetToolPosition()` method to `DrawingContextMapControl` and `ISharedMapControl`
4. [x] Add `DrawTool()` method to `DrawingContextMapControl`
5. [x] Draw simple rectangle representing tool width at tool position with hitch line
6. [x] Call `DrawTool()` before `DrawVehicle()` in render loop
7. [x] Wire platform views (Desktop/iOS/Android) to call `SetToolPosition` on property changes

### Phase 2: Hitch Lines
1. [ ] Draw hitch line from vehicle pivot to hitch point
2. [ ] For trailing tools: draw hitch triangle (hitch → tool pivot)
3. [ ] For TBT: draw hitch triangle (hitch → tank), then (tank → tool)
4. [ ] Test with trailing tool configuration

### Phase 3: Sections
1. [ ] Draw individual sections as colored rectangles
2. [ ] Calculate section positions using `GetSectionWidth()`
3. [ ] Color sections based on on/off state (requires SectionControlService)
4. [ ] Test section rendering with multiple sections

### Phase 4: Polish (Future)
- [ ] Add tool wheel rendering for trailing implements
- [ ] Add lookahead lines when job active
- [ ] Add tram markers
- [ ] Add zone separators

## Rendering Approach

### Coordinate System
- Tool renders in world coordinates (easting/northing)
- Must apply rotation to tool heading (may differ from vehicle heading for trailing)
- Tool width extends perpendicular to tool heading

### Drawing Primitives Needed
- **Rectangle/Polygon**: Tool body and sections
- **Lines**: Hitch connections
- **Optional**: Arrowhead shapes for sections (like WinForms)

### Transform Order
```
1. Translate to tool position
2. Rotate to tool heading
3. Draw tool centered at origin (width extends left/right)
```

### Color Scheme (from WinForms)
| State | Color |
|-------|-------|
| Section On (Auto) | Green (#00F200) |
| Section On (Manual) | Yellow (#F8F800) |
| Section Off | Red (#F23232) |
| Section Off (Mapping) | Blue (#0040F8) |
| Section On (Not Mapping) | Purple (#F84CF8) |
| Hitch Line | Orange/Yellow |
| Tool Outline | Black |

## Data Flow

```
GPS Update
    ↓
MainViewModel updates vehicle position
    ↓
ToolPositionService.Update(vehiclePivot, vehicleHeading)
    ↓
ToolPositionService calculates tool position based on type
    ↓
DrawingContextMapControl.Render()
    ↓
DrawTool() - uses ToolPositionService.ToolPosition, ToolHeading
    ↓
DrawVehicle() - uses vehicle position
```

## Test Scenarios

1. **Fixed Rear Tool**: Tool should be directly behind tractor, same heading
2. **Fixed Front Tool**: Tool should be in front of tractor
3. **Trailing Tool**: Tool should lag behind during turns, different heading
4. **TBT Tool**: Tank between tractor and tool, both trail during turns
5. **Wide Tool**: Verify tool width renders correctly (e.g., 12m sprayer)
6. **Multiple Sections**: Verify all sections render with correct widths
7. **Offset Tool**: Verify lateral offset is visible

## Files to Modify

| File | Changes |
|------|---------|
| `DrawingContextMapControl.cs` | Add `DrawTool()` method, inject `IToolPositionService` |
| Platform DI files | Wire up `IToolPositionService` to map control (if not already) |

## Notes

- Start simple: just a rectangle showing tool position and width
- Sections and colors can be added incrementally
- WinForms uses OpenGL transforms; we use Avalonia's `Matrix.CreateRotation/Translation`
- Tool is drawn BEFORE vehicle (vehicle appears on top)
