# Feature: CoverageMapService

## Overview

Track and render coverage (worked area) as the tool/implement moves across the field. Coverage is displayed as colored polygons (triangle strips) showing where the implement has been active. Calculates total worked area for statistics.

## WinForms Reference

### Files
- `GPS/Classes/CPatches.cs` - Triangle strip management for coverage patches
- `GPS/IO/SectionFiles.cs` - Sections.txt file I/O
- `GPS/Forms/Position.designer.cs` - Lines 1651-1667: AddMappingPoint calls
- `GPS/Forms/OpenGL.Designer.cs` - Lines 126-310: Coverage rendering

### Data Structure

Coverage is stored as triangle strips, which efficiently render quads (2 triangles per segment):

```csharp
// From CPatches.cs
public class CPatches
{
    // Current triangle strip being built
    public List<vec3> triangleList = new List<vec3>();

    // All completed patches for this section
    public List<List<vec3>> patchList = new List<List<vec3>>();

    // Section edge tracking
    public vec2 leftPoint, rightPoint;

    public bool isDrawing = false;
    public int numTriangles = 0;
}
```

### Triangle Strip Format

Each patch (triangleList) contains:
1. **First element**: Color (R, G, B stored in easting, northing, heading)
2. **Subsequent elements**: Alternating left/right edge points

```
triangleList[0] = (R, G, B, 0)           // Color
triangleList[1] = (leftEasting, leftNorthing, 0)   // Left edge 1
triangleList[2] = (rightEasting, rightNorthing, 0) // Right edge 1
triangleList[3] = (leftEasting, leftNorthing, 0)   // Left edge 2
triangleList[4] = (rightEasting, rightNorthing, 0) // Right edge 2
... continues...
```

### Core Algorithm

```csharp
// From CPatches.cs

public void TurnMappingOn(int sectionIndex)
{
    if (!isDrawing)
    {
        isDrawing = true;

        // Create new triangle list
        triangleList = new List<vec3>(64);
        patchList.Add(triangleList);

        // First point is the color
        if (!isMultiColoredSections)
            triangleList.Add(new vec3(sectionColorDay.R, sectionColorDay.G, sectionColorDay.B));
        else
            triangleList.Add(new vec3(secColors[sectionIndex].R, G, B));

        // Initial edge points
        leftPoint = section[currentStartSectionNum].leftPoint;
        rightPoint = section[currentEndSectionNum].rightPoint;

        triangleList.Add(new vec3(leftPoint.easting, leftPoint.northing, 0));
        triangleList.Add(new vec3(rightPoint.easting, rightPoint.northing, 0));
    }
}

public void AddMappingPoint(int sectionIndex)
{
    // Get current section edge positions
    leftPoint = section[currentStartSectionNum].leftPoint;
    rightPoint = section[currentEndSectionNum].rightPoint;

    // Add two vertices for next quad
    triangleList.Add(new vec3(leftPoint.easting, leftPoint.northing, 0));
    triangleList.Add(new vec3(rightPoint.easting, rightPoint.northing, 0));

    numTriangles++;

    // Calculate area of new triangles
    int c = triangleList.Count - 1;
    if (c >= 3)
    {
        double area = CalculateTriangleStripArea(triangleList, c - 3);
        workedAreaTotal += area;
    }

    // Break into chunks for rendering efficiency (every 62 triangles)
    if (numTriangles > 61)
    {
        numTriangles = 0;
        patchSaveList.Add(triangleList);

        triangleList = new List<vec3>(64);
        patchList.Add(triangleList);

        // Add color + last two points to start new strip
        triangleList.Add(new vec3(color.R, color.G, color.B));
        triangleList.Add(new vec3(leftPoint.easting, leftPoint.northing, 0));
        triangleList.Add(new vec3(rightPoint.easting, rightPoint.northing, 0));
    }
}

public void TurnMappingOff()
{
    AddMappingPoint(0);  // Final point
    isDrawing = false;
    numTriangles = 0;

    // Save patch if it has enough points
    if (triangleList.Count > 4)
        patchSaveList.Add(triangleList);
    else
        patchList.RemoveAt(patchList.Count - 1);
}
```

### Area Calculation

Uses the shoelace formula to calculate area of triangles:

```csharp
// From WorkedAreaService.cs (already in AgValoniaGPS.Services)
public double CalculateTriangleStripArea(Vec3[] points, int startIndex)
{
    int c = startIndex + 3;

    // First triangle: c, c-1, c-2
    double area1 = Math.Abs(
        (points[c].Easting * (points[c - 1].Northing - points[c - 2].Northing))
        + (points[c - 1].Easting * (points[c - 2].Northing - points[c].Northing))
        + (points[c - 2].Easting * (points[c].Northing - points[c - 1].Northing)));

    // Second triangle: c-1, c-2, c-3
    double area2 = Math.Abs(
        (points[c - 1].Easting * (points[c - 2].Northing - points[c - 3].Northing))
        + (points[c - 2].Easting * (points[c - 3].Northing - points[c - 1].Northing))
        + (points[c - 3].Easting * (points[c - 1].Northing - points[c - 2].Northing)));

    return (area1 + area2) * 0.5;
}
```

## Config Settings Used

From `ConfigurationStore.Instance.Tool`:
- `NumSections` - Number of active sections (for zone grouping)
- `IsMultiColoredSections` - Use different colors per section
- `IsSectionsNotZones` - Individual section colors vs uniform

From `ConfigurationStore.Instance.Display`:
- `SectionColor` - Default coverage color

## File Format (Sections.txt)

```
<vertex_count>
<easting>,<northing>,<heading>
<easting>,<northing>,<heading>
...
<vertex_count>
<easting>,<northing>,<heading>
...
```

First vertex of each patch contains the color (RGB as easting, northing, heading).

## Proposed Interface

```csharp
public interface ICoverageMapService
{
    // Current state
    double TotalWorkedArea { get; }
    double TotalWorkedAreaUser { get; }  // User-resettable counter
    int PatchCount { get; }

    // Start/stop mapping for a zone
    void StartMapping(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge);
    void StopMapping(int zoneIndex);

    // Add coverage point (call each GPS update when mapping is on)
    void AddCoveragePoint(int zoneIndex, Vec2 leftEdge, Vec2 rightEdge);

    // Query coverage
    bool IsPointCovered(double easting, double northing);
    IReadOnlyList<CoveragePatch> GetPatches();

    // Reset
    void ClearAll();
    void ResetUserArea();

    // File I/O
    void SaveToFile(string fieldDirectory);
    void LoadFromFile(string fieldDirectory);

    // Events
    event EventHandler<CoverageUpdatedEventArgs> CoverageUpdated;
}

public class CoveragePatch
{
    public Color Color { get; set; }
    public List<Vec3> Vertices { get; set; }  // Triangle strip vertices
}

public class CoverageUpdatedEventArgs : EventArgs
{
    public double TotalArea { get; set; }
    public int PatchCount { get; set; }
}
```

## Dependencies

### Required Services
- `IToolPositionService` - Get section edge positions
- `ISectionControlService` - Know when sections are on/off
- `IWorkedAreaService` - Area calculations (already exists)

### Required Config
- `ConfigurationStore.Instance.Tool` for section settings
- `ConfigurationStore.Instance.Display` for colors

## Implementation Steps

1. [ ] Create `ICoverageMapService` interface in `Services/Interfaces/`
2. [ ] Create `CoveragePatch` model in `Models/Coverage/`
3. [ ] Create `CoverageMapService` class in `Services/Coverage/`
4. [ ] Implement zone/section mapping state management
5. [ ] Implement triangle strip building (AddCoveragePoint)
6. [ ] Implement area calculation using existing WorkedAreaService
7. [ ] Implement patch chunking for rendering efficiency
8. [ ] Implement point-in-coverage query (for section control)
9. [ ] Implement file I/O (Sections.txt format)
10. [ ] Add DI registration
11. [ ] Integrate with DrawingContextMapControl for rendering
12. [ ] Wire to MainViewModel GPS update loop
13. [ ] Add UI for coverage display toggle and area statistics
14. [ ] Test with simulator

## Rendering Considerations

For Avalonia's DrawingContext (not OpenGL), we'll render patches as:
- Filled polygons with semi-transparency
- Convert triangle strips to proper polygon paths
- Use frustum culling to skip off-screen patches

```csharp
// Example rendering in DrawingContext
public void RenderCoverage(DrawingContext dc, ICoverageMapService coverageService)
{
    var patches = coverageService.GetPatches();
    foreach (var patch in patches)
    {
        if (patch.Vertices.Count < 4) continue;

        var color = patch.Color;
        var brush = new SolidColorBrush(Color.FromArgb(152, color.R, color.G, color.B));

        // Build polygon from triangle strip
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Triangle strip: alternating left/right edges
            // Convert to polygon outline: left edges forward, right edges backward
            ctx.BeginFigure(ToPoint(patch.Vertices[1]), true);

            // All left edges (odd indices)
            for (int i = 3; i < patch.Vertices.Count; i += 2)
                ctx.LineTo(ToPoint(patch.Vertices[i]));

            // All right edges backward (even indices)
            for (int i = patch.Vertices.Count - (patch.Vertices.Count % 2 == 0 ? 2 : 1); i >= 2; i -= 2)
                ctx.LineTo(ToPoint(patch.Vertices[i]));

            ctx.EndFigure(true);
        }

        dc.DrawGeometry(brush, null, geometry);
    }
}
```

## Priority

**Medium** - Required for visual feedback of worked area and accurate section control. Essential for production sprayer/planter operation.

## Notes

- The existing `WorkedAreaService` in `Services/Geometry/` handles area calculation
- Triangle strip format is efficient for both memory and rendering
- Patch chunking (62 triangles max) helps with culling and file I/O
- Multi-colored sections allow visual distinction per boom section
- Coverage query is needed for look-ahead section control

## Existing Code to Reuse

- `Shared/AgValoniaGPS.Services/Geometry/WorkedAreaService.cs` - Area calculations
- `Shared/AgValoniaGPS.Models/Base/Vec3.cs` - Vertex type

