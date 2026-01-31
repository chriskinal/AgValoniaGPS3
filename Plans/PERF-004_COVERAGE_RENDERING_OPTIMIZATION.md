# PERF-004: Coverage Rendering Optimization

## Status: Phase 2 Complete

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

### Phase 1 Memory Usage: 7.5GB at 66% coverage
- Detection HashSet: 343M cells × ~20 bytes = ~7GB
- Display bitmap: 47M pixels × 4 bytes = ~190MB

## Phase 2: Memory Optimization (Complete)

### Achieved: ~1.1GB baseline (flat across all coverage levels)

### Production Build Test Results (520ha field)

| Coverage | Memory Floor | Memory Peak | FPS | CPU |
|----------|--------------|-------------|-----|-----|
| 15% (load) | - | 1.38GB | - | - |
| 30% | 1.15GB | - | 29 | 51.5% |
| 50% | 1.12GB | 1.34GB | 29 | 55.1% |
| 90% | 1.16GB | 1.36GB | 29 | 52% |

**Key findings:**
- Memory is FLAT from 30% to 90% coverage (bit array working)
- GC sawtooth pattern: ~1.12GB floor, ~1.34GB ceiling
- FPS stable at 29 (timer-limited to 30)
- ~1GB overhead is legacy polygon data from saved coverage file

### Implementation (Complete)

**Detection Layer (0.1m for RTK precision):**
- Bit array: 65MB for 520ha field
- Per-cell detection in O(1) time

**Display Layer (0.35m for 520ha):**
- WriteableBitmap with incremental updates
- Direct framebuffer writes using unsafe code
- ~190MB for display bitmap

### Completed Tasks
1. [x] Replace detection HashSet with bit array
2. [x] Incremental bitmap updates (O(new cells) not O(total))
3. [x] Direct framebuffer writes (no buffer copying)
4. [x] Dynamic resolution scaling for large fields
5. [x] Remove polygon storage - bitmap-only rendering
6. [x] Binary coverage file format (Coverage.bin with RLE compression)

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
