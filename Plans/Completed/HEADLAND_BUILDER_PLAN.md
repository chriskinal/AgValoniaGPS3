# Headland Builder Implementation Plan

## Overview

Implement a headland builder that creates offset polygons from field boundaries. The headland defines the turn area at the edge of the field where the implement is lifted/disabled.

## How AgOpenGPS Headland Works

Based on analysis of [AgOpenGPS](https://github.com/AgOpenGPS-Official/AgOpenGPS) and [QtAgOpenGPS](https://github.com/torriem/QtAgOpenGPS):

1. **Headland Line**: An inward offset polygon from the field boundary
2. **Offset Distance**: Typically based on tool width or user-specified distance
3. **Turn Lines**: Created from headland for U-turn path planning
4. **Detection**: Vehicle/section corners checked against headland polygon for section control

## Current Codebase Status

### Existing Components (Ready to Use)
- `HeadlandLine` / `HeadlandPath` models in `AgValoniaGPS.Models.Guidance`
- `HeadlandLineSerializer` for file I/O (`Headlines.txt` format)
- `HeadlandDetectionService` for point-in-headland checks
- `HeadlandDetectionInput/Output` models
- UI buttons in `FieldToolsPanel.axaml` (not wired up)
- Icons: `HeadlandBuild.png`, `HeadlandOn.png`

### Missing Components (Need to Implement)
- **Polygon offset algorithm** (core headland builder)
- **HeadlandBuilderService** - Service to create headland from boundary
- **HeadlandBuilderDialogPanel** - UI for headland creation
- **ViewModel commands** for headland operations
- **Map rendering** of headland line

---

## Implementation Plan

### Phase 1: Polygon Offset Algorithm

**File**: `Shared/AgValoniaGPS.Services/Geometry/PolygonOffsetService.cs`

Create an inward polygon offset algorithm:

```csharp
public class PolygonOffsetService
{
    /// <summary>
    /// Create an inward offset polygon from a boundary
    /// </summary>
    /// <param name="boundaryPoints">Outer boundary points (clockwise)</param>
    /// <param name="offsetDistance">Inward offset distance in meters</param>
    /// <returns>Offset polygon points, or null if offset collapses</returns>
    public List<Vec2>? CreateInwardOffset(List<Vec2> boundaryPoints, double offsetDistance);
}
```

**Algorithm Options**:

1. **Simple perpendicular offset** (easier, less robust)
   - Move each point inward along bisector of adjacent edges
   - Handle self-intersections by finding and removing loops

2. **Clipper Library** (robust, industry standard)
   - Add NuGet: `Clipper2Lib` by Angus Johnson
   - Handles all edge cases (sharp corners, self-intersections)
   - Used by AgOpenGPS

**Recommendation**: Use Clipper2Lib for robustness.

### Phase 2: HeadlandBuilderService

**File**: `Shared/AgValoniaGPS.Services/Headland/HeadlandBuilderService.cs`

```csharp
public interface IHeadlandBuilderService
{
    /// <summary>
    /// Build headland from boundary with specified distance
    /// </summary>
    HeadlandBuildResult BuildHeadland(Boundary boundary, HeadlandBuildOptions options);

    /// <summary>
    /// Preview headland without saving
    /// </summary>
    List<Vec2>? PreviewHeadland(List<Vec2> boundaryPoints, double distance);
}

public class HeadlandBuildOptions
{
    public double Distance { get; set; }           // Offset distance in meters
    public bool UseToolWidth { get; set; }         // Use tool width as distance
    public int Passes { get; set; } = 1;           // Number of headland passes
    public double CornerSmoothing { get; set; }    // Corner rounding radius
}

public class HeadlandBuildResult
{
    public bool Success { get; set; }
    public List<Vec2>? HeadlandPoints { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Phase 3: ViewModel Integration

**File**: `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs`

Add properties and commands:

```csharp
// Headland state
[Reactive] public bool IsHeadlandOn { get; set; }
[Reactive] public List<Vec2>? CurrentHeadlandLine { get; set; }
[Reactive] public double HeadlandDistance { get; set; } = 10.0;

// Dialog visibility
[Reactive] public bool IsHeadlandBuilderDialogVisible { get; set; }

// Preview state (for real-time preview in dialog)
[Reactive] public List<Vec2>? HeadlandPreviewLine { get; set; }

// Commands
public ICommand ShowHeadlandBuilderCommand { get; }
public ICommand BuildHeadlandCommand { get; }
public ICommand ClearHeadlandCommand { get; }
public ICommand ToggleHeadlandCommand { get; }
```

### Phase 4: Headland Builder Dialog

**File**: `Shared/AgValoniaGPS.Views/Controls/Dialogs/HeadlandBuilderDialogPanel.axaml`

UI Features:
- **Distance input**: NumericUpDown for headland distance (meters)
- **Tool width button**: Set distance to current tool width
- **Passes selector**: 1-3 passes for multi-pass headlands
- **Preview**: Real-time preview on map as distance changes
- **Boundary selector**: Choose which boundary (outer/inner) to offset
- **Build button**: Create and save headland
- **Clear button**: Remove existing headland

```xml
<Border Classes="DialogPanel" IsVisible="{Binding IsHeadlandBuilderDialogVisible}">
    <StackPanel Spacing="12" Width="350">
        <TextBlock Text="Headland Builder" FontSize="18" FontWeight="Bold"/>

        <!-- Distance Input -->
        <StackPanel>
            <TextBlock Text="Headland Distance (m)"/>
            <Grid ColumnDefinitions="*,Auto">
                <NumericUpDown Value="{Binding HeadlandDistance}"
                               Minimum="1" Maximum="100" Increment="0.5"/>
                <Button Content="Tool Width" Command="{Binding SetHeadlandToToolWidthCommand}"/>
            </Grid>
        </StackPanel>

        <!-- Passes -->
        <StackPanel>
            <TextBlock Text="Number of Passes"/>
            <ComboBox SelectedIndex="{Binding HeadlandPasses}">
                <ComboBoxItem Content="1 Pass"/>
                <ComboBoxItem Content="2 Passes"/>
                <ComboBoxItem Content="3 Passes"/>
            </ComboBox>
        </StackPanel>

        <!-- Actions -->
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Button Content="Build" Command="{Binding BuildHeadlandCommand}"/>
            <Button Content="Clear" Command="{Binding ClearHeadlandCommand}"/>
            <Button Content="Close" Command="{Binding CloseHeadlandBuilderCommand}"/>
        </StackPanel>
    </StackPanel>
</Border>
```

### Phase 5: Map Rendering

**File**: `Shared/AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs`

Add headland line rendering:

```csharp
private void DrawHeadlandLine(DrawingContext context)
{
    if (_headlandLine == null || _headlandLine.Count < 3) return;

    var pen = new Pen(Brushes.Orange, 2, DashStyle.Dash);

    var geometry = new StreamGeometry();
    using (var ctx = geometry.Open())
    {
        var firstPoint = WorldToScreen(_headlandLine[0]);
        ctx.BeginFigure(firstPoint, false);

        for (int i = 1; i < _headlandLine.Count; i++)
        {
            ctx.LineTo(WorldToScreen(_headlandLine[i]));
        }
        ctx.LineTo(firstPoint); // Close polygon
    }

    context.DrawGeometry(null, pen, geometry);
}
```

### Phase 6: Wire Up FieldToolsPanel

**File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FieldToolsPanel.axaml`

Connect the Headland buttons:

```xml
<!-- Headland toggle button -->
<Button Command="{Binding ToggleHeadlandCommand}"
        Classes.Active="{Binding IsHeadlandOn}">
    <Image Source="...HeadlandOn.png"/>
    <TextBlock Text="Headland"/>
</Button>

<!-- Headland Builder button -->
<Button Command="{Binding ShowHeadlandBuilderCommand}">
    <Image Source="...HeadlandBuild.png"/>
    <TextBlock Text="Headland Builder"/>
</Button>
```

### Phase 7: Headland Detection Integration

Integrate with existing `HeadlandDetectionService` for section control:

```csharp
// In GPS update loop
if (IsHeadlandOn && CurrentHeadlandLine != null)
{
    var input = new HeadlandDetectionInput
    {
        Boundaries = new List<BoundaryData>
        {
            new BoundaryData { HeadlandLine = CurrentHeadlandLine.Select(p => new Vec3(p.X, p.Y, 0)).ToList() }
        },
        VehiclePosition = currentPosition,
        Sections = sectionCorners,
        IsHeadlandOn = true
    };

    var result = _headlandDetectionService.DetectHeadland(input);

    // Update section states based on headland detection
    foreach (var section in result.SectionStatus)
    {
        // Disable section if in headland
    }
}
```

---

## File Summary

| File | Type | Purpose |
|------|------|---------|
| `Services/Geometry/PolygonOffsetService.cs` | New | Polygon offset algorithm |
| `Services/Headland/HeadlandBuilderService.cs` | New | Build headland from boundary |
| `Services/Headland/IHeadlandBuilderService.cs` | New | Service interface |
| `Views/Controls/Dialogs/HeadlandBuilderDialogPanel.axaml` | New | Builder dialog UI |
| `Views/Controls/Dialogs/HeadlandBuilderDialogPanel.axaml.cs` | New | Dialog code-behind |
| `ViewModels/MainViewModel.cs` | Modify | Add headland properties/commands |
| `Views/Controls/DrawingContextMapControl.cs` | Modify | Render headland line |
| `Views/Controls/Panels/FieldToolsPanel.axaml` | Modify | Wire up buttons |
| `DependencyInjection/ServiceCollectionExtensions.cs` | Modify | Register services |

---

## NuGet Dependencies

```xml
<!-- Add to AgValoniaGPS.Services.csproj -->
<PackageReference Include="Clipper2Lib" Version="1.3.0" />
```

---

## Testing Checklist

- [ ] Create headland from simple rectangular boundary
- [ ] Create headland from complex boundary with curves
- [ ] Handle inner boundaries (islands) - offset outward
- [ ] Preview updates in real-time as distance changes
- [ ] Headland persists after field reload
- [ ] Section control responds to headland detection
- [ ] Map renders headland line correctly at all zoom levels
- [ ] Multi-pass headlands work correctly
- [ ] Edge cases: very small boundaries, sharp corners

---

## Future Enhancements

1. **Drive-through gates**: Allow sections to stay on through specific boundary segments
2. **Asymmetric headland**: Different distances for different sides
3. **Headland from recorded path**: Create headland by driving the turn path
4. **Auto U-turn integration**: Use headland for automatic turn path planning
