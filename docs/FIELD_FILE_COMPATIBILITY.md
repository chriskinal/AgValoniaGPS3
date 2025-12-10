# Field File Compatibility Analysis

This document compares file formats between AgOpenGPS WinForms and AgValoniaGPS to ensure compatibility.

## Summary of Issues

| File Type | WinForms Name | AgValoniaGPS Name | Format Compatible? | Critical? |
|-----------|---------------|-------------------|-------------------|-----------|
| Tracks/AB Lines | `TrackLines.txt` | `ABLines.txt` | **NO** | **YES** |
| Boundary | `Boundary.txt` | `Boundary.txt` | Needs verification | YES |
| Headland | `Headlines.txt` | `Headland.txt`? | Needs verification | YES |
| Field Info | `Field.txt` | `Field.txt` | Needs verification | YES |

## Track/AB Lines File - CRITICAL INCOMPATIBILITY

### WinForms Format (`TrackLines.txt`)

**Location:** `GPS/IO/TrackFiles.cs`

**File Structure:**
```
$TrackLines                     ← Header (required, starts with $)
<track name>                    ← Name (one line)
<heading>                       ← Heading in radians
<easting_A>,<northing_A>        ← Point A (comma-separated)
<easting_B>,<northing_B>        ← Point B (comma-separated)
<nudge_distance>                ← Nudge offset
<mode>                          ← Integer: 0=ABLine, 1=Curve, etc.
<is_visible>                    ← Boolean: True/False
<curve_count>                   ← Number of curve points
<easting>,<northing>,<heading>  ← Repeated curve_count times
... (repeat for each track)
```

**Key Properties:**
- Header with `$` prefix
- Multi-line format (each field on separate line)
- Heading in **radians**
- Supports both AB lines and curves in same file
- Includes nudge distance, mode, visibility

### AgValoniaGPS Format (`ABLines.txt`)

**Location:** `MainViewModel.cs:6557` (SaveABLinesToFile), `6593` (LoadABLinesFromField)

**File Structure:**
```
Name,Heading,PointA_Easting,PointA_Northing,PointB_Easting,PointB_Northing
... (one line per track)
```

**Key Properties:**
- No header
- Single-line CSV format (comma-separated)
- Heading in **degrees**
- Only stores AB lines (no curve support)
- Missing: nudge distance, mode, visibility, curve points

### Compatibility Issues

1. **Filename mismatch**: `TrackLines.txt` vs `ABLines.txt`
2. **Format mismatch**: Multi-line vs single-line CSV
3. **Heading units**: Radians vs degrees
4. **Missing fields in AgValoniaGPS**:
   - `nudgeDistance`
   - `mode` (TrackMode enum)
   - `isVisible`
   - `curvePts` (curve point list)
5. **Missing header**: AgValoniaGPS doesn't use `$` header

### Impact

- **AgValoniaGPS cannot read WinForms fields** - different filename and format
- **WinForms cannot read AgValoniaGPS fields** - different filename and format
- **Curve tracks lost** - AgValoniaGPS only stores AB lines, not curves

## Proposed Fix

### Option A: Full Compatibility (Recommended)

1. Rename file to `TrackLines.txt`
2. Match WinForms format exactly
3. Add curve support to ABLine model
4. Create proper TrackFiles service in AgValoniaGPS.Services

### Option B: Dual Format Support

1. On Load: Try `TrackLines.txt` first (WinForms format), fallback to `ABLines.txt`
2. On Save: Save to both files, or save to `TrackLines.txt` in WinForms format

### Implementation Steps for Option A

1. **Update ABLine model** to include:
   ```csharp
   public double NudgeDistance { get; set; }
   public TrackMode Mode { get; set; }
   public bool IsVisible { get; set; } = true;
   public List<Vec3> CurvePoints { get; set; }
   ```

2. **Create TrackFilesService** in `AgValoniaGPS.Services`:
   - Port from `GPS/IO/TrackFiles.cs`
   - Use same format as WinForms

3. **Update MainViewModel**:
   - Replace `SaveABLinesToFile` → call `TrackFilesService.Save()`
   - Replace `LoadABLinesFromField` → call `TrackFilesService.Load()`

4. **Convert heading**:
   - When loading: Convert radians to degrees for display
   - When saving: Convert degrees to radians for file

## Other Field Files to Audit

### Boundary.txt
- WinForms: Uses `BoundaryStreamer.cs`
- AgValoniaGPS: Uses `BoundaryFileService.cs`
- **Action**: Compare formats

### Headlines.txt / Headland.txt
- WinForms: Uses `HeadlandLineSerializer.cs` → `Headlines.txt`
- AgValoniaGPS: Check what filename/format we use
- **Action**: Compare formats

### Field.txt
- WinForms: Uses `OverviewStreamer.cs`
- AgValoniaGPS: Uses `FieldPlaneFileService.cs`?
- **Action**: Compare formats

## File Naming Reference (WinForms)

From `AgOpenGPS.Core/Streamers/Field/`:
| Streamer | Filename |
|----------|----------|
| BoundaryStreamer | `Boundary.txt` |
| ContourStreamer | `Contour.txt` |
| RecordedPathStreamer | `RecPath.txt` |
| FlagListStreamer | `Flags.txt` |
| OverviewStreamer | `Field.txt` |
| WorkAreaStreamer | `Sections.txt` |
| TramLinesStreamer | `Tram.txt` |
| BingMapStreamer | `BackPic.txt` |

From `AgOpenGPS.Core/Services/`:
| Service | Filename |
|---------|----------|
| HeadlandLineSerializer | `Headlines.txt` |

From `GPS/IO/`:
| Service | Filename |
|---------|----------|
| TrackFiles | `TrackLines.txt` |

From `AgOpenGPS.Core/Services/AgShare/`:
| Operation | Files Created |
|-----------|---------------|
| Field download | `Field.txt`, `Boundary.txt`, `TrackLines.txt`, `Flags.txt`, `Headland.txt`, `Contour.txt`, `Sections.txt`, `agshare.txt` |
