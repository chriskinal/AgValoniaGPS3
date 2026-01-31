# Coverage Rendering Performance Tests

This document tracks performance testing of different coverage rendering approaches.

**Naming Convention**: Each test has an ID (PERF-001, PERF-002, etc.) that appears in both this document and the git commit message for traceability.

## Test Environment

### Development Machine
- **Field**: 520ha test field
- **Avalonia Version**: 11.3.9
- **Test Speed**: 44 kph (accelerated for faster coverage accumulation)

### Target Test Devices
Devices selected as realistic farmer-purchase hardware (~$150 used):
- **iPad Pro 12.9" 2nd Gen (2017)** - A10X Fusion, 64GB - iOS target
- **Samsung Galaxy Tab S7 FE 12.4" (2021)** - Snapdragon 750G, 64GB - Android target

These represent large-screen tablets suitable for tractor cab use at price points farmers would pay.

## Metrics
- **FPS**: Frames per second (use Avalonia DevTools F12 for accurate measurement)
- **Points**: Total geometry points being rendered
- **Polygons/Strokes**: Number of geometry objects
- **Rebuild Time**: Time to rebuild geometry cache (ms)

---

## Test Results

### PERF-001: Polygon Multi-Pass (5m/10m decimation)
- **Date**: 2026-01-28
- **Commit**: `0979454`
- **Branch**: `coverage-dual-buffer`
- **Method**: Filled polygons, one per pass per section
- **Decimation**: Source 5m, Render 10m

| Coverage | Points | Polygons | FPS (zoomed out) | FPS (mid-zoom) | FPS (zoomed in) | Notes |
|----------|--------|----------|------------------|----------------|-----------------|-------|
| 10% | 83,678 | 304 | ~43 | 18-20 | ~32 | Narrow "bad" zoom range |

**Observations**:
- FPS varies significantly with zoom level
- Mid-zoom has worst performance (18-20 FPS)
- Zoomed out and zoomed in both better (~30-43 FPS)

---

### PERF-002: Stroke Rendering (5m decimation)
- **Date**: 2026-01-28
- **Commit**: `402de47`
- **Branch**: `coverage-dual-buffer`
- **Method**: Thick stroked centerlines (stroke width = section width)
- **Decimation**: 5m

| Coverage | Points | Strokes | FPS | Notes |
|----------|--------|---------|-----|-------|
| 30% | 289,802 | 1,072 | 7 | Much worse than polygons |

**Observations**:
- Strokes generated MORE points than polygons
- Thick stroke expansion is expensive on GPU
- **Result**: Not viable - reverted to polygons

---

### PERF-003: Current Polygon Baseline
- **Date**: 2026-01-28
- **Commit**: `57ea35c`
- **Branch**: `coverage-dual-buffer`
- **Method**: Polygons (strokes disabled)
- **Decimation**: Source 5m, Render 10m

| Coverage | Points | Polygons | FPS (zoomed out) | FPS (mid-zoom) | FPS (zoomed in) | Notes |
|----------|--------|----------|------------------|----------------|-----------------|-------|
| 7% | 69,341 | 256 | ? | 17 | ? | Wide bad zoom range |

**Observations**:
- WORSE than PERF-001 - something regressed
- Wide range of zoom settings cause FPS loss (not narrow like before)
- Need to find what changed or try more aggressive decimation

---

## Template for New Tests

```markdown
### PERF-XXX: [Description]
- **Date**: YYYY-MM-DD
- **Commit**: `hash`
- **Branch**: `branch-name`
- **Method**: [Description of rendering approach]
- **Decimation**: [Source Xm, Render Ym]

| Coverage | Points | Polygons | FPS (zoomed out) | FPS (mid-zoom) | FPS (zoomed in) | Notes |
|----------|--------|----------|------------------|----------------|-----------------|-------|
| 10% | | | | | | |

**Observations**:
-
```

---

## Ideas to Try

1. ~~**PERF-004**: More aggressive decimation~~ - Causes accuracy issues at headlands
2. **PERF-005**: Zoom-dependent detail - Coarser geometry when zoomed out
3. **PERF-006**: Tile-based rendering - Divide into tiles with cached bitmaps
4. **PERF-007**: Douglas-Peucker simplification on polygons
5. **PERF-008**: Merge adjacent same-color polygons

---

### PERF-004: WriteableBitmap + Bit Array (Complete)
- **Date**: 2026-01-28 to 2026-01-31
- **Commits**: b45c0a6 → current
- **Branch**: `coverage-dual-buffer`
- **Method**: Bit array detection + WriteableBitmap display
- **Status**: COMPLETE (Phase 3 Final)

**Architecture**:
- **Detection**: Bit array at fixed 0.1m resolution (~65MB for 520ha)
- **Display**: WriteableBitmap with dynamic resolution scaling
- **Rendering**: Bilinear interpolation for smooth edges

**Display Resolution Scaling** (50M pixel limit):

| Field Size | Display Resolution |
|------------|-------------------|
| ≤ 50 ha | 0.1m |
| 50 - 200 ha | 0.2m |
| 200 - 312 ha | 0.25m |
| 312 - 612 ha | 0.35m |
| 612 - 1250 ha | 0.5m |
| 1250 - 2812 ha | 0.75m |
| 2812+ ha | 1.0m+ |

**Final Production Build Results (520ha field, macOS)**:

| Coverage | Real Memory | CPU | FPS |
|----------|-------------|-----|-----|
| 15% | 367 MB | 41% | 29 |
| 30% | 367 MB | 41% | 29 |
| 50% | 362 MB | 45% | 29 |
| 73% | 350-395 MB | 51% | 29 |
| 91% | 510-525 MB | 47% | 29-30 |

**Key Achievements**:
- Memory FLAT from 15% to 73% coverage (~360-395MB Real Memory)
- Small bump at 91% (~510-525MB) - Avalonia rendering overhead
- FPS rock solid at 29-30 (timer-limited)
- CPU reasonable for tablet deployment (41-51%)
- 611 lines of polygon code removed
- Binary Coverage.bin format (RLE compressed)

**Bug Fixes (Phase 3)**:
- Triple SetFieldBounds allocation guard
- Iterator memory leak (yield return → direct return)
- Bitmap clear fix (GetCoverageBounds null check)
- Reusable buffers to reduce allocation pressure

## Historical Data (from transcripts, pre-polygon)

| Coverage | FPS | Method | Notes |
|----------|-----|--------|-------|
| 6.3% | 59→33 | Triangles | Dropping fast |
| 8.3% | 31 | Triangles | |
| 10% | 23-24 | Triangles | |
| 3% | 44 | Early polygon | After initial opt |
| 10% | ~30 | Early polygon | With zoom dip to 18-20 |
