# Tram Lines Implementation Plan

## Goal
Complete tram lines functionality in the Field Builder dialog, matching legacy AgOpenGPS features.

## Current State
- **Models**: TramConfig, TramLines, TramDisplayMode - complete
- **Services**: TramLineService, TramLineOffsetService - generation and file I/O work
- **Rendering**: DrawTramLinesSk() renders tram lines on main map
- **UI**: Field Builder Tram tab has basic passes/mode/build controls
- **Commands**: Build, display mode, passes +/- all wired

## Missing Features (Priority Order)

### Phase 1: Core Builder in Field Builder Dialog

**1.1 Tram Width Input**
- Add editable tram width field (sprayer/spreader boom width) to Tram tab
- Currently hardcoded default 12.0m, legacy default 24.0m
- Display in current units (m/ft)
- Save to ConfigurationStore.Tram.TramWidth

**1.2 Track Selection for Tram Generation**
- Currently shows selected track name but "Build" uses whatever is selected
- Add track dropdown/selector in the Tram tab (list all saved tracks)
- Each track generates different tram line patterns
- Legacy supported both AB lines and curves

**1.3 Canvas Preview of Tram Lines**
- Draw generated tram lines on the Field Builder canvas (pink/rose color like main map)
- Show boundary tracks (outer/inner) distinctly from parallel lines
- Respect display mode (Off/All/Lines/Outer) in preview
- Update preview when passes, tram width, or track selection changes

**1.4 Swap Side Button**
- Legacy: Swap A/B button to flip which side tram lines generate on
- For AB lines: swap ptA and ptB, reverse heading
- For curves: reverse point order and headings
- Regenerate tram lines after swap

**1.5 Start Pass Offset**
- Legacy: startPass setting (0 or 1) to offset tram pattern
- Useful when field entrance doesn't align with first tram pass
- +/- buttons for start pass number

### Phase 2: Advanced Features

**2.1 Outer/Inner Auto-Detection**
- Legacy: `isOuter = ((int)(tramWidth / toolWidth + 0.5)) % 2 == 0`
- Auto-detect whether the first pass has outer or inner wheel on reference line
- Override checkbox (IsOuterInverted) to flip

**2.2 Display Tram Control Indicators**
- Left/right dot indicators on main map showing tram detection state
- Green dot = wheel on tram line, black = not on tram line
- Flash when manual override is active
- Legacy: colored dots at bottom of screen near tool display

**2.3 Alpha/Transparency Slider**
- Already in TramConfig (alpha property)
- Add slider to Tram tab for visual adjustment

**2.4 Delete Tram Lines**
- Button to clear all tram lines with confirmation
- Currently no delete in Field Builder tram tab

### Phase 3: PGN Integration

**3.1 Real-Time Tram Detection**
- Port pixel-based detection from legacy to geometric detection
- Use IsOnTramLine() with proper tolerance based on wheel width
- Separate left/right wheel detection
- Determine left/right based on vehicle heading and tram line position

**3.2 PGN 239 Tram Byte**
- Wire tram controlByte into BuildMachinePgn()
- Bit 0 = right wheel on tram, Bit 1 = left wheel on tram
- Clear tram byte when in headland
- Already sending PGN 239 via AutoSteerService (byte currently always 0)

**3.3 Manual Override**
- Left/right manual override buttons (isLeftManualOn, isRightManualOn)
- Override forces the tram bit on regardless of detection
- Toggle behavior (click to activate, click again to deactivate)
- Clear both on track change

## Architecture

```
Field Builder Dialog (Tram Tab)
  |-- Track selector (dropdown of saved tracks)
  |-- Tram Width input (sprayer boom width)
  |-- Passes input (+/-)
  |-- Start Pass input (+/-)
  |-- Swap Side button
  |-- Display Mode buttons (Off/All/Lines/Outer)
  |-- Alpha slider
  |-- Build button -> calls TramLineService
  |-- Delete button
  |-- Canvas preview (pink tram lines over boundary)

Runtime (GPS cycle):
  Vehicle position -> IsOnTramLine(left wheel) -> bit 1
                   -> IsOnTramLine(right wheel) -> bit 0
                   -> controlByte -> PGN 239 byte 8
```

## Key Formulas

```
halfWheelTrack = Vehicle.TrackWidth / 2.0
passWidth = toolWidth * passes  (for parallel line spacing)

For each pass i (from startPass to startPass + passes - 1):
  Inner wheel offset = (tramWidth * 0.5) + halfWheelTrack + (tramWidth * i)
  Outer wheel offset = (tramWidth * 0.5) - halfWheelTrack + (tramWidth * i)

isOuter = ((int)(tramWidth / toolWidth + 0.5)) % 2 == 0
if (IsOuterInverted) isOuter = !isOuter

Left wheel position  = vehicle position + perpendicular * halfWheelTrack
Right wheel position = vehicle position - perpendicular * halfWheelTrack
```

## Files to Modify

| File | Changes |
|------|---------|
| `FieldBuilderDialogPanel.axaml` | Tram tab UI: width input, track selector, swap, start pass, alpha, delete |
| `FieldBuilderDialogPanel.axaml.cs` | Canvas tram preview rendering, build logic |
| `MainViewModel.cs` | Tram width property, start pass, swap command |
| `MainViewModel.Commands.Track.cs` | New tram commands |
| `TramConfig.cs` | Add StartPass property |
| `AutoSteerService.cs` | Wire tram detection into PGN 239 |
| `TramLineService.cs` | Left/right wheel detection methods |
| `DrawingContextMapControl.cs` | Tram control dot indicators |

## Open Issues
- #34 Tram Lines
- #35 Tram Lines Builder
