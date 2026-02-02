# PERF-005: Dynamic Display Resolution with Two-File Save Format

## Status: Phase 1 & 2 Complete

## Background

PERF-004 achieved excellent performance for coverage rendering:
- 520ha field: ~360-525MB memory, 29-30 FPS
- Detection at 0.1m (RTK precision)
- Display bitmap scales dynamically based on field size

However, the current coverage.bin save format stores raw RGB565 pixel data at display resolution. This creates issues:
1. Display resolution changes between sessions (field re-opened at different zoom)
2. Multi-section colors may not be preserved correctly across different display resolutions
3. Large files for high-resolution coverage

## Problem Statement

Current approach:
- Detection: 0.1m bit array (accurate, compact)
- Display: WriteableBitmap at dynamic resolution (RGB565 pixels)
- Save: RLE-compressed RGB565 pixels from display bitmap

Issues:
1. **Resolution mismatch**: If display resolution changes, loaded coverage doesn't align
2. **Color storage**: RGB565 values are resolution-dependent, not semantically meaningful
3. **File size**: RGB565 at 0.1m for 200ha = ~1.4GB uncompressed

## Proposed Solution: Two-File Save Format

### File 1: `coverage_detect.bin` (Detection Bits)
```
Header:
  - Magic: "COVD" (4 bytes)
  - Version: 1 (uint8)
  - Resolution: 0.1m (float32) - always 0.1m for RTK precision
  - Origin E: (float64)
  - Origin N: (float64)
  - Width: cells (uint32)
  - Height: cells (uint32)

Data:
  - RLE-compressed bit array (1 bit per cell)
```

**Purpose**: Accurate coverage detection regardless of display resolution
**Size estimate**: 200ha at 0.1m = ~5M cells = ~625KB bits, RLE likely ~50-200KB

### File 2: `coverage_disp.bin` (Section Colors)
```
Header:
  - Magic: "COVS" (4 bytes)
  - Version: 1 (uint8)
  - Section count: (uint8)
  - Palette: [section_index -> RGB565 color] (section_count * 2 bytes)
  - Resolution: (float32) - display resolution when saved
  - Origin E: (float64)
  - Origin N: (float64)
  - Width: cells (uint32)
  - Height: cells (uint32)

Data:
  - RLE-compressed section indices (1 byte per covered cell, 0 = not covered)
```

**Purpose**: Preserve which section covered each area + original colors
**Size estimate**: 200ha at 0.2m display = ~1.25M cells, ~1.25MB uncompressed, RLE ~200-500KB

## Load Behavior

### On Field Open:
1. Load `coverage_detect.bin` → restore `_detectionBits` at 0.1m
2. Load `coverage_disp.bin`:
   - Read palette (section colors from when coverage was recorded)
   - Read section indices at saved resolution
   - Scale/interpolate to current display resolution if needed
   - Convert section indices → RGB565 using saved palette
3. Copy pixels to WriteableBitmap

### Color Handling Decision: **Hybrid Approach**
- **Loaded coverage**: Use saved palette for rendering (preserves historical appearance)
- **New coverage**: Use current tool config section colors
- **Visual distinction**: Old vs new coverage may have different colors - this is informative
- **No config modification**: User's current section colors remain unchanged

This approach:
- Preserves accurate historical record
- Doesn't surprise users by changing their settings
- Visual difference between old/new can help users see session boundaries

## Save Behavior

### On Field Close:
1. Save `coverage_detect.bin`:
   - Write header with 0.1m resolution
   - RLE-compress `_detectionBits`

2. Save `coverage_disp.bin`:
   - Build palette from current tool config section colors
   - For each covered cell in display bitmap:
     - Find closest matching section color
     - Store section index (1 byte)
   - RLE-compress section indices

## Memory Savings

| Field Size | Current (RGB565 display) | New (Section indices) |
|------------|--------------------------|----------------------|
| 50ha | ~30MB bitmap | ~3MB indices + ~150KB bits |
| 200ha | ~190MB bitmap | ~12MB indices + ~625KB bits |
| 500ha | ~475MB bitmap | ~30MB indices + ~1.5MB bits |

**Note**: Display bitmap in RAM remains the same (needed for rendering). Savings are in file I/O and the ability to reconstruct at different resolutions.

## Implementation Steps

### Phase 1: Detection Bits Save/Load ✓
- [x] Implement `SaveDetectionBits()` in CoverageMapService
- [x] Implement `LoadDetectionBits()` in CoverageMapService
- [x] Add RLE compression for bit array
- [ ] Test: Save field, reopen, detection bits match

### Phase 2: Section Index Save/Load ✓
- [x] Add section color palette to CoverageMapService
- [x] Implement `SaveSectionDisplay()` with palette header
- [x] Implement `LoadSectionDisplay()` with palette extraction
- [x] Map RGB565 pixels → section indices on save
- [x] Map section indices → RGB565 using saved palette on load
- [ ] Test: Multi-color coverage preserved through save/load

### Phase 3: Resolution Independence
- [ ] Handle display resolution mismatch on load
- [ ] Scale section indices from saved resolution to current
- [ ] Use nearest-neighbor for upscaling (preserve sharp edges)
- [ ] Test: Save at 0.2m, load at 0.1m display resolution

### Phase 4: Migration
- [ ] Detect old `coverage.bin` format
- [ ] Auto-convert to new two-file format on first save
- [ ] Remove old file after successful migration

## Files to Modify

1. **CoverageMapService.cs**
   - Add `SaveDetectionBits()`, `LoadDetectionBits()`
   - Add `SaveSectionIndices()`, `LoadSectionIndices()`
   - Add section palette management
   - Modify `SaveToFile()` to use new format
   - Modify `LoadFromFile()` to use new format

2. **ICoverageMapService.cs**
   - Add palette-related interface members if needed

3. **DrawingContextMapControl.cs**
   - May need method to reconstruct display from section indices + palette

## Testing Checklist

- [ ] Save/load detection bits with RLE
- [ ] Save/load multi-section coverage with correct colors
- [ ] Load coverage at different display resolution than saved
- [ ] Migrate old coverage.bin to new format
- [ ] Large field performance (500ha)
- [ ] New coverage after loading old (mixed colors work)

## Dependencies

- PERF-004 complete (current state)
- Current detection bit array working
- Current multi-color save/load working (baseline to improve)

## Risks

1. **Palette size**: Limited to 255 sections (uint8 index) - should be sufficient
2. **Color matching on save**: Need efficient RGB565 → section index lookup
3. **Resolution scaling**: Artifacts at section boundaries when scaling

## Future Considerations

- Could extend palette to include timestamps for coverage aging visualization
- Could add overlap count per cell for application rate mapping
- Could compress further with delta encoding for smooth coverage patterns
