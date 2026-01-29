# Coverage Rendering Performance Tests

This document tracks performance testing of different coverage rendering approaches.

**Naming Convention**: Each test has an ID (PERF-001, PERF-002, etc.) that appears in both this document and the git commit message for traceability.

## Test Environment
- **Field**: 520ha test field
- **Machine**: [Your machine specs]
- **Avalonia Version**: 11.3.9
- **Test Speed**: 44 kph (accelerated for faster coverage accumulation)

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

### PERF-004: WriteableBitmap Rendering (1.0m cells)
- **Date**: 2026-01-28
- **Commit**: (pending)
- **Branch**: `coverage-dual-buffer`
- **Method**: Render coverage to WriteableBitmap, blit each frame
- **Resolution**: 1.0m per pixel (~5.2M pixels for 520ha, ~21MB)

**Approach**:
- Create WriteableBitmap sized to field bounds
- When coverage added: paint new pixels incrementally
- Each frame: single DrawImage() call
- Expected: O(1) render time regardless of coverage amount

| Coverage | Points | Bitmap Size | FPS (zoomed out) | FPS (mid-zoom) | FPS (zoomed in) | Notes |
|----------|--------|-------------|------------------|----------------|-----------------|-------|
| 10% | N/A | ~21MB | | | | |
| 30% | N/A | ~21MB | | | | |
| 50% | N/A | ~21MB | | | | |

**Observations**:
- (pending test results)

## Historical Data (from transcripts, pre-polygon)

| Coverage | FPS | Method | Notes |
|----------|-----|--------|-------|
| 6.3% | 59→33 | Triangles | Dropping fast |
| 8.3% | 31 | Triangles | |
| 10% | 23-24 | Triangles | |
| 3% | 44 | Early polygon | After initial opt |
| 10% | ~30 | Early polygon | With zoom dip to 18-20 |
