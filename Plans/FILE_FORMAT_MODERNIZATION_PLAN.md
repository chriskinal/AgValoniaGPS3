# File Format Modernization Plan

## Overview

Modernize AgValoniaGPS file formats from legacy AgOpenGPS text/XML formats to **GeoJSON (WGS84)** for geospatial data and **JSON** for configuration, improving maintainability, GIS interoperability, and developer experience while providing one-way import from legacy formats.

## Design Philosophy

> AgValoniaGPS may use different/improved formats from AgOpenGPS when it benefits code simplicity or features. Provide one-way import from AgOpenGPS formats rather than maintaining full backwards compatibility.

### Coordinate System Architecture

**Storage Format:** GeoJSON with WGS84 coordinates (latitude/longitude in degrees)
- Industry standard for geospatial data exchange
- Compatible with GIS tools (QGIS, ArcGIS, Google Earth)
- Portable between systems without reference point dependencies

**Runtime Format:** Local plane geometry (meters)
- Simpler math (no spherical trigonometry)
- Better performance for real-time guidance calculations
- Consistent with AgOpenGPS internal architecture

**Conversion Strategy:**
```
┌─────────────┐     Load      ┌──────────────┐
│  GeoJSON    │  ──────────►  │ Plane Coords │
│  (WGS84)    │               │  (meters)    │
│  on disk    │  ◄──────────  │  in memory   │
└─────────────┘     Save      └──────────────┘
```

- Convert WGS84 → plane geometry on field load
- All guidance calculations use plane geometry
- Convert plane geometry → WGS84 on field save
- Origin point stored in GeoJSON for reference

---

## Current State Analysis

### Fields (Legacy Text Files)

| File | Format | Purpose |
|------|--------|---------|
| `Field.txt` | Fixed-line text | Origin coordinates, convergence, timestamp |
| `Boundary.txt` | Text with point lists | Outer boundary + inner holes |
| `Headland.Txt` | Text with point lists | Headland boundary |
| `TrackLines.txt` | Multi-line text blocks | AB lines and curves |
| `BackPic.Txt` | Text | Background image bounds |
| `BackPic.png` | Binary | Satellite/aerial image |

**Issues:**
- Fixed-line parsing (fragile, order-dependent)
- No schema versioning
- Angles stored in radians, displayed in degrees
- Duplicate `isDriveThru` line handling (legacy quirk)
- No graceful handling of unknown fields
- Not compatible with GIS tools

### Coverage Data (Legacy Text Files)

| File | Format | Purpose |
|------|--------|---------|
| `Sections.txt` | Triangle strip text | Applied/worked area coverage |

**Current AgValonia format:**
```
5                              <- vertex count (includes color vertex)
152.000,251.000,152.00000      <- RGB color as first vertex
3.675,-0.854,0.00000           <- triangle strip vertices...
4.650,-0.635,0.00000
3.630,-0.696,0.00000
4.604,-0.470,0.00000
```

**Current AgOpenGPS format:**
```
97                             <- vertex pair count
187,250,250                    <- RGB color
-7.635,-3.967,0                <- vertex pairs (much larger merged polygons)
6.99,-3.548,0
...
```

**Issues:**
- **Massive file size**: AgValonia 1.2MB vs AgOpenGPS 4KB for same 0.62ha field
- **Tiny patches**: Each section start/stop creates new patch (often only 2 quads)
- **No merging**: Adjacent patches with same color stored separately
- **No simplification**: Every vertex stored at full precision
- **Triangle strips**: Format optimized for OpenGL rendering, not storage
- **Per-section colors**: AgValonia unique feature - each section can have different color (AgOpenGPS uses single color for all sections)

### Vehicle Profiles (XML)

| File | Format | Purpose |
|------|--------|---------|
| `*.XML` | AgOpenGPS XML | Vehicle + tool + guidance configuration |

**Issues:**
- 100+ flat settings (no hierarchy)
- 17 hardcoded section positions
- String-based booleans ("True"/"False")
- Mixed concerns (simulator settings in vehicle config)
- Magic setting names scattered across codebase

### Application Settings (JSON - Already Modern)

| File | Format | Purpose |
|------|--------|---------|
| `appsettings.json` | JSON | Window state, NTRIP, paths, UI preferences |

**Status:** Already using modern format. No changes needed.

### Models with Duplication

| Model | Status | Replacement |
|-------|--------|-------------|
| `ABLine` | Marked `[Obsolete]` | `Track` (unified) |
| `VehicleProfile` | Marked `[Obsolete]` | `ConfigurationStore` serialization |

---

## Proposed New Formats

### 1. Field Format: GeoJSON (`field.geojson`)

Use standard GeoJSON (RFC 7946) with WGS84 coordinates. The field is a FeatureCollection containing boundaries, headland, and tracks as features.

```json
{
  "type": "FeatureCollection",
  "properties": {
    "name": "MyField",
    "version": "1.0",
    "createdDate": "2025-12-19T10:30:00Z",
    "lastModifiedDate": "2025-12-19T10:30:00Z",
    "backgroundImage": {
      "filename": "background.png",
      "bounds": {
        "west": -74.0070,
        "east": -74.0050,
        "south": 40.7120,
        "north": 40.7140
      }
    }
  },
  "features": [
    {
      "type": "Feature",
      "id": "boundary-outer",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[
          [-74.0060, 40.7128],
          [-74.0050, 40.7128],
          [-74.0050, 40.7138],
          [-74.0060, 40.7138],
          [-74.0060, 40.7128]
        ]]
      },
      "properties": {
        "featureType": "boundary",
        "boundaryType": "outer"
      }
    },
    {
      "type": "Feature",
      "id": "boundary-inner-1",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[
          [-74.0058, 40.7130],
          [-74.0055, 40.7130],
          [-74.0055, 40.7133],
          [-74.0058, 40.7133],
          [-74.0058, 40.7130]
        ]]
      },
      "properties": {
        "featureType": "boundary",
        "boundaryType": "inner",
        "isDriveThrough": false
      }
    },
    {
      "type": "Feature",
      "id": "headland",
      "geometry": {
        "type": "Polygon",
        "coordinates": [[
          [-74.0059, 40.7129],
          [-74.0051, 40.7129],
          [-74.0051, 40.7137],
          [-74.0059, 40.7137],
          [-74.0059, 40.7129]
        ]]
      },
      "properties": {
        "featureType": "headland",
        "distance": 15.0
      }
    },
    {
      "type": "Feature",
      "id": "track-1",
      "geometry": {
        "type": "LineString",
        "coordinates": [
          [-74.0060, 40.7128],
          [-74.0050, 40.7138]
        ]
      },
      "properties": {
        "featureType": "track",
        "name": "AB Line 1",
        "nudgeDistance": 0,
        "isVisible": true,
        "isActive": false
      }
    },
    {
      "type": "Feature",
      "id": "track-2",
      "geometry": {
        "type": "LineString",
        "coordinates": [
          [-74.0060, 40.7128],
          [-74.0057, 40.7131],
          [-74.0054, 40.7135],
          [-74.0050, 40.7138]
        ]
      },
      "properties": {
        "featureType": "track",
        "name": "Curve 1",
        "nudgeDistance": 0,
        "isVisible": true,
        "isActive": false
      }
    }
  ]
}
```

**Key improvements:**
- **GIS compatible**: Open in QGIS, ArcGIS, Google Earth, etc.
- **WGS84 coordinates**: No origin point dependency
- **Standard format**: RFC 7946 compliant GeoJSON
- Single file instead of 5+ separate files
- Schema version for future migrations
- Unified track format (AB lines have 2 points, curves have N)

**Directory structure:**
```
Fields/
├── MyField/
│   ├── field.geojson    # All metadata, boundaries, tracks (WGS84)
│   └── background.png   # Optional satellite image
```

### 2. Vehicle/Tool Profile Format (`profile.json`)

Vehicle profiles remain as regular JSON (not GeoJSON) since they're configuration, not geospatial data:

```json
{
  "version": "1.0",
  "name": "John Deere 5055E",
  "createdDate": "2025-12-19T10:30:00Z",

  "vehicle": {
    "type": "tractor",
    "wheelbase": 2.5,
    "trackWidth": 1.8,
    "antenna": {
      "height": 3.0,
      "pivot": 0,
      "offset": 0
    },
    "steering": {
      "maxAngle": 35.0,
      "maxAngularVelocity": 35.0
    }
  },

  "tool": {
    "type": "sprayer",
    "width": 6.0,
    "overlap": 0.05,
    "offset": 0,
    "hitch": {
      "length": -1.8,
      "isTrailing": true,
      "isTBT": false
    },
    "sections": [
      { "position": -2.5, "width": 1.0 },
      { "position": -1.5, "width": 1.0 },
      { "position": -0.5, "width": 1.0 },
      { "position": 0.5, "width": 1.0 },
      { "position": 1.5, "width": 1.0 },
      { "position": 2.5, "width": 1.0 }
    ],
    "timing": {
      "lookAheadOn": 0,
      "lookAheadOff": 0,
      "turnOffDelay": 0
    }
  },

  "guidance": {
    "algorithm": "purePursuit",
    "goalPointLookAhead": 4.0,
    "goalPointLookAheadMult": 1.4,
    "goalPointAcquireFactor": 1.5,
    "stanley": {
      "distanceErrorGain": 0.8,
      "headingErrorGain": 1.0,
      "integralGain": 0.0
    },
    "purePursuit": {
      "integralGain": 0.0
    },
    "uTurn": {
      "radius": 8.0,
      "extensionLength": 20.0,
      "compensation": 1.0
    }
  },

  "simulator": {
    "latitude": 32.59,
    "longitude": -87.18
  }
}
```

**Key improvements:**
- Hierarchical structure (vehicle → tool → guidance)
- Dynamic sections array (no 17-element limit)
- Section width per section (not just position)
- Clear separation of concerns
- Tool type field for future implement-specific logic

### 3. Coverage Format: GeoJSON (`coverage.geojson`)

Coverage data stored as GeoJSON MultiPolygons, grouped by color. This supports AgValonia's per-section color feature while dramatically reducing file size through polygon merging.

```json
{
  "type": "FeatureCollection",
  "properties": {
    "version": "1.0",
    "totalAreaHa": 0.62,
    "lastModified": "2025-12-30T14:00:00Z"
  },
  "features": [
    {
      "type": "Feature",
      "id": "coverage-color-0",
      "geometry": {
        "type": "MultiPolygon",
        "coordinates": [
          [[[-74.0060, 40.7128], [-74.0055, 40.7128], [-74.0055, 40.7135], [-74.0060, 40.7135], [-74.0060, 40.7128]]],
          [[[-74.0050, 40.7130], [-74.0045, 40.7130], [-74.0045, 40.7138], [-74.0050, 40.7138], [-74.0050, 40.7130]]]
        ]
      },
      "properties": {
        "featureType": "coverage",
        "color": "#98FB98",
        "sectionIndices": [0, 1, 2],
        "areaHa": 0.31
      }
    },
    {
      "type": "Feature",
      "id": "coverage-color-1",
      "geometry": {
        "type": "MultiPolygon",
        "coordinates": [
          [[[-74.0058, 40.7132], [-74.0052, 40.7132], [-74.0052, 40.7140], [-74.0058, 40.7140], [-74.0058, 40.7132]]]
        ]
      },
      "properties": {
        "featureType": "coverage",
        "color": "#87CEEB",
        "sectionIndices": [3, 4, 5],
        "areaHa": 0.31
      }
    }
  ]
}
```

**Key design decisions:**

1. **Group by color, not section**: Sections with same color are merged into one feature
   - Single-color mode: One feature with all coverage
   - Multi-color mode: One feature per unique color

2. **MultiPolygon geometry**: Allows non-contiguous areas with same color
   - Separate passes on same swath merge into single feature
   - Handles gaps from skipped areas

3. **Polygon simplification on save**:
   - Douglas-Peucker algorithm to reduce vertex count
   - Configurable tolerance (e.g., 0.1m for field work)
   - Preserves visual accuracy while reducing file size

4. **Triangle strip → Polygon conversion**:
   ```
   Runtime (rendering)          Storage (GeoJSON)
   ┌─────────────────┐          ┌─────────────────┐
   │ Triangle Strips │  ─────►  │ Merged Polygons │
   │ (per section)   │  Save    │ (per color)     │
   │                 │  ◄─────  │                 │
   └─────────────────┘  Load    └─────────────────┘
   ```

5. **sectionIndices property**: Records which sections contributed to this color
   - Enables reconstruction of per-section data if needed
   - Useful for reports ("Section 3 covered X hectares")

**Storage optimization algorithm:**

```
On Save:
1. Group all patches by color
2. For each color group:
   a. Convert triangle strips to polygons
   b. Union overlapping polygons (handles re-sprayed areas)
   c. Simplify polygon boundaries (Douglas-Peucker)
   d. Store as MultiPolygon feature
3. Convert plane coords → WGS84
4. Write GeoJSON

On Load:
1. Read GeoJSON
2. Convert WGS84 → plane coords
3. For each feature:
   a. Triangulate polygons back to triangle strips
   b. Create CoveragePatch objects for rendering
```

**Expected file size improvement:**

| Scenario | Current | New Format | Reduction |
|----------|---------|------------|-----------|
| 0.62 ha, 16 sections, single color | 1.2 MB | ~8 KB | 99% |
| 0.62 ha, 16 sections, multi-color | 1.2 MB | ~15 KB | 98% |
| 10 ha field, heavy coverage | ~20 MB | ~50 KB | 99% |

**Directory structure (updated):**
```
Fields/
├── MyField/
│   ├── field.geojson       # Metadata, boundaries, tracks (WGS84)
│   ├── coverage.geojson    # Applied area coverage (WGS84)
│   └── background.png      # Optional satellite image
```

---

## Coordinate Conversion

### WGS84 ↔ Plane Geometry

The conversion uses a simple plane projection centered on the field origin:

```csharp
public class CoordinateConverter
{
    private readonly double _originLat;
    private readonly double _originLon;
    private readonly double _metersPerDegreeLat;
    private readonly double _metersPerDegreeLon;

    public CoordinateConverter(double originLat, double originLon)
    {
        _originLat = originLat;
        _originLon = originLon;

        // Meters per degree varies with latitude
        _metersPerDegreeLat = 111132.92 - 559.82 * Math.Cos(2 * originLat * Math.PI / 180);
        _metersPerDegreeLon = 111412.84 * Math.Cos(originLat * Math.PI / 180);
    }

    // WGS84 (lon, lat) → Plane (easting, northing) in meters
    public (double Easting, double Northing) ToPlane(double lon, double lat)
    {
        double easting = (lon - _originLon) * _metersPerDegreeLon;
        double northing = (lat - _originLat) * _metersPerDegreeLat;
        return (easting, northing);
    }

    // Plane (easting, northing) → WGS84 (lon, lat)
    public (double Lon, double Lat) ToWgs84(double easting, double northing)
    {
        double lon = _originLon + easting / _metersPerDegreeLon;
        double lat = _originLat + northing / _metersPerDegreeLat;
        return (lon, lat);
    }
}
```

### Origin Point Selection

When loading a GeoJSON field:
1. Use the centroid of the outer boundary as the origin
2. Store this origin in memory for runtime conversions
3. All points converted to plane coordinates relative to this origin

When saving:
1. Convert all plane coordinates back to WGS84 using stored origin
2. Origin is implicit (centroid) - no need to store separately

---

## Implementation Phases

### Phase 1: GeoJSON Infrastructure

1. Create `GeoJsonFieldService` for new field format
2. Create `CoordinateConverter` for WGS84 ↔ plane conversion
3. Create `ProfileJsonService` for new profile format
4. Keep legacy services unchanged

**Files to create:**
- `Shared/AgValoniaGPS.Services/Field/GeoJsonFieldService.cs`
- `Shared/AgValoniaGPS.Services/Coordinates/CoordinateConverter.cs`
- `Shared/AgValoniaGPS.Services/Profile/ProfileJsonService.cs`

### Phase 2: Auto-Detection & Import

1. On field load: detect format (GeoJSON vs legacy text)
2. If legacy: import, convert to plane geometry, then save as GeoJSON
3. Save only in new GeoJSON format
4. Same for profiles: detect XML vs JSON

**Detection logic:**
```csharp
public async Task<Field> LoadField(string path)
{
    var geoJsonPath = Path.Combine(path, "field.geojson");
    if (File.Exists(geoJsonPath))
    {
        return await LoadGeoJsonField(geoJsonPath);
    }

    // Legacy import - loads as plane coords using embedded origin
    var legacyField = await LoadLegacyField(path);

    // Convert plane → WGS84 and save as GeoJSON
    await SaveGeoJsonField(legacyField, geoJsonPath);
    return legacyField;
}
```

### Phase 3: Coverage Format Modernization

1. Create `GeoJsonCoverageService` for new coverage format
2. Implement polygon merging algorithm (union by color)
3. Implement Douglas-Peucker simplification
4. Implement triangulation for load (polygon → triangle strips)
5. Auto-detect and migrate legacy `Sections.txt` files

**Files to create:**
- `Shared/AgValoniaGPS.Services/Coverage/GeoJsonCoverageService.cs`
- `Shared/AgValoniaGPS.Services/Geometry/PolygonMerger.cs`
- `Shared/AgValoniaGPS.Services/Geometry/PolygonSimplifier.cs`
- `Shared/AgValoniaGPS.Services/Geometry/PolygonTriangulator.cs`

**Key algorithms needed:**

1. **Triangle Strip → Polygon**: Convert rendering format to boundary polygon
   ```csharp
   // Triangle strip vertices form a ribbon - extract outer boundary
   public List<Vec2> TriangleStripToPolygon(List<Vec3> vertices)
   ```

2. **Polygon Union**: Merge overlapping/adjacent polygons
   ```csharp
   // Use Clipper library or implement Weiler-Atherton
   public MultiPolygon UnionPolygons(List<Polygon> polygons)
   ```

3. **Douglas-Peucker Simplification**: Reduce vertex count
   ```csharp
   public List<Vec2> Simplify(List<Vec2> points, double tolerance)
   ```

4. **Polygon Triangulation**: Convert polygon back to triangle strips for rendering
   ```csharp
   // Ear clipping or similar algorithm
   public List<Vec3> TriangulatePolygon(List<Vec2> boundary)
   ```

### Phase 4: Model Consolidation

1. Remove `ABLine` class entirely (use `Track`)
2. Serialize `ConfigurationStore` directly for profiles
3. Update all references

### Phase 5: Cleanup (Optional)

1. Remove legacy file services
2. Remove obsolete model classes
3. Delete legacy format tests

---

## Migration Strategy

### For End Users

- **Transparent migration**: App detects legacy files and converts automatically
- **No data loss**: All legacy data preserved in new format
- **One-way**: Once converted, fields use new format only
- **Backup recommended**: Users should backup before first run of new version

### For Developers

- **Gradual transition**: Both formats supported during transition
- **Feature flags**: Can toggle new format on/off during development
- **Test coverage**: Unit tests for both legacy import and new format

---

## Compatibility Notes

### GIS Tool Interoperability

With GeoJSON format, AgValoniaGPS fields can be:
- Opened directly in QGIS, ArcGIS, Google Earth
- Edited in any GeoJSON editor
- Visualized on web maps (Leaflet, Mapbox, etc.)
- Converted to other formats (Shapefile, KML) using standard tools

### AgOpenGPS Interoperability

After migration, AgValoniaGPS fields will NOT be directly readable by AgOpenGPS. Options:

1. **Export feature**: Add "Export to AgOpenGPS format" menu option
2. **Standalone converter**: Small utility to convert GeoJSON → legacy text
3. **Documentation**: Clear notes that formats are incompatible

### Sharing Between AgValoniaGPS Users

GeoJSON format is the standard for sharing between AgValoniaGPS installations.

---

## File Size Comparison (Estimated)

| Data | Legacy | GeoJSON | Change |
|------|--------|---------|--------|
| Simple field (boundary + 1 track) | ~2 KB (5 files) | ~2 KB (1 file) | Same |
| Complex field (100 tracks) | ~50 KB (5 files) | ~55 KB (1 file) | +10% |
| Vehicle profile | ~15 KB (XML) | ~3 KB (JSON) | -80% |
| Coverage 0.62 ha, single color | ~1.2 MB | ~8 KB | **-99%** |
| Coverage 0.62 ha, 16 colors | ~1.2 MB | ~15 KB | **-98%** |
| Coverage 10 ha field | ~20 MB | ~50 KB | **-99%** |

GeoJSON field data is slightly larger than custom formats due to verbose coordinate arrays, but the GIS interoperability benefits outweigh the minimal size increase. Coverage data sees dramatic reductions due to polygon merging and simplification.

---

## Success Criteria

### Field & Profile Formats
- [ ] Fields load/save in GeoJSON format with WGS84 coordinates
- [ ] Legacy AgOpenGPS fields import correctly
- [ ] Coordinate conversion (WGS84 ↔ plane) works accurately
- [ ] Fields can be opened in QGIS/ArcGIS
- [ ] Vehicle profiles load/save in JSON format
- [ ] Legacy XML profiles import correctly
- [ ] No 17-section limit
- [ ] All angles stored in degrees
- [ ] Schema version field present for future migrations

### Coverage Format
- [ ] Coverage saves as GeoJSON with merged polygons per color
- [ ] Legacy `Sections.txt` files import correctly
- [ ] Per-section colors preserved (AgValonia unique feature)
- [ ] File size reduction of 90%+ achieved
- [ ] Coverage can be viewed in QGIS/ArcGIS
- [ ] Triangle strip rendering works after load (polygon → triangles)
- [ ] Area calculations remain accurate after format conversion

### Testing
- [ ] Unit tests pass for all format conversions
- [ ] Round-trip tests (save → load → save produces identical output)
- [ ] Performance tests for large coverage files

---

## Open Questions

1. **Background images**: Keep as separate PNG or embed as base64 in JSON?
   - Recommendation: Keep separate (large files shouldn't bloat JSON)

2. **Field sharing**: Support compressed `.agfield` package (zip)?
   - Could bundle `field.geojson` + `coverage.geojson` + `background.png` for easy sharing

3. **Schema validation**: Use JSON Schema for validation?
   - Nice to have, not required for MVP

4. **Profile inheritance**: Allow profiles to extend base profiles?
   - Future enhancement, not in initial scope

5. **GeoJSON extensions**: Use GeoJSON-T for timestamped track recording?
   - Could enable track history/replay features

6. **Coverage polygon library**: Use existing library (Clipper2, NetTopologySuite) or implement custom?
   - Clipper2: Fast, well-tested, MIT license, C# native
   - NetTopologySuite: Full GIS toolkit, may be overkill
   - Custom: More control, but significant development effort
   - Recommendation: Clipper2 for polygon operations

7. **Coverage simplification tolerance**: What's the right balance between file size and visual accuracy?
   - 0.05m: Very accurate, larger files
   - 0.1m: Good balance for typical field work
   - 0.5m: Smaller files, may show visible artifacts
   - Recommendation: Default 0.1m, configurable in settings

8. **Incremental coverage saves**: Save only new coverage or full merge each time?
   - Full merge: Simpler, always optimized file
   - Incremental: Faster saves, periodic full merge
   - Recommendation: Full merge on field close, consider incremental for auto-save

---

## References

- [RFC 7946 - The GeoJSON Format](https://tools.ietf.org/html/rfc7946)
- [GeoJSON.io - Online editor](https://geojson.io/)
- [QGIS Documentation](https://qgis.org/)
- [Clipper2 Library](https://github.com/AngusJohnson/Clipper2) - Polygon clipping/union operations
- [Douglas-Peucker Algorithm](https://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm) - Line simplification
- [Ear Clipping Triangulation](https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf) - Polygon triangulation
