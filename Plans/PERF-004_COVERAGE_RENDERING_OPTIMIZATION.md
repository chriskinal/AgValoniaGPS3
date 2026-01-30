# PERF-004: Coverage Rendering Optimization

## Status: Phase 1 Complete, Phase 2 Planned

## Problem Statement

Coverage rendering was causing FPS to degrade as coverage increased:
- Started at 28 FPS
- Dropped to 9 FPS after a few passes on 520ha field
- Full rebuilds were iterating ALL coverage cells every frame
- HashSet-based detection using 7.5GB RAM at 66% coverage

## Phase 1: Achieved (Complete)

### Performance Improvements
| Coverage | Before | After |
|----------|--------|-------|
| 10% | Degrading | 37 FPS |
| 31% | Degrading | 37 FPS |
| 55% | Degrading | 36-37 FPS |
| 66% | ~9 FPS | 37 FPS |

### Key Optimizations

1. **Incremental bitmap updates** - O(new cells) not O(total coverage)
   - Only update ~175 new cells per frame instead of millions
   - Full rebuild only on initial load or bounds change

2. **Direct framebuffer writes** - No buffer copying
   - Before: Copy 190MB buffer → modify → copy back (380MB/update)
   - After: Write directly to framebuffer memory with unsafe code

3. **Spatial queries** - O(viewport) not O(total coverage)
   - Query cells within bounds using O(1) HashSet lookups
   - Iterate coordinate grid, not HashSet entries

4. **Dynamic display resolution** - Scale for large fields
   - Small fields: 0.1m resolution (RTK precision)
   - Large fields: Scale up to 0.35m+ to fit in memory
   - 520ha field: 0.35m display resolution (~47M pixels)

### Current Memory Usage: 7.5GB at 66% coverage
- Detection HashSet: 343M cells × ~20 bytes = ~7GB
- Display bitmap: 47M pixels × 4 bytes = ~190MB

## Phase 2: Memory Optimization (Planned)

### Target: ~110MB instead of 7.5GB (68x reduction)

### Approach

**Detection Layer (0.1m for RTK precision):**
- Replace HashSet with bit array: 65MB
- Per-zone cell counters for acreage tracking: negligible

**Display Layer (0.35m for 520ha):**
- Byte array storing zone index per cell: 47MB
- Render to RGBA using zone → color lookup
- Supports per-section coloring for seed/product tracking

### Implementation Tasks
1. [ ] Replace detection HashSet with bit array
2. [ ] Add per-zone cell counters for acreage calculation
3. [ ] Replace display HashSet iteration with byte array for zone storage
4. [ ] Update rendering to map zone index → color

### Benefits
- Per-zone acreage tracking for seed/fertilizer inventory
- Per-section coloring on display
- 68x memory reduction (7.5GB → 110MB)
- Same O(1) detection and rendering performance

## Technical Details

### Internal Detection (0.1m cells)
- Matches RTK GPS accuracy (~2cm)
- Used for section control on/off decisions
- O(1) lookup: Is this point already covered?

### Display Bitmap (dynamic resolution)
- Small fields: 0.1m (matches detection)
- Large fields: Scales to fit 50M pixel limit
- 520ha: 0.35m resolution = ~47M pixels

### Render Timer
- 30 FPS target (33ms interval)
- Actual GPU rate: 37 FPS (DevTools)
- App counter shows ~29 FPS (timer-limited)

## Files Modified (Phase 1)

- `CoverageMapService.cs` - Spatial queries, fixed 0.1m detection
- `DrawingContextMapControl.cs` - Incremental updates, direct framebuffer writes, dynamic scaling
- `AgValoniaGPS.Views.csproj` - AllowUnsafeBlocks for direct memory access
- Platform MainView/MainWindow files - Updated provider signatures
