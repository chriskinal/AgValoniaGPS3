# User-Selectable Display Resolution

## Overview

Allow users to manually control the coverage display resolution to optimize performance on constrained devices (e.g., iPad) or maximize quality on powerful hardware.

## Background

Currently, display resolution is automatically determined based on field size using breakpoints in `DrawingContextMapControl.cs`:
- Fields < 500m: 0.1m resolution
- Fields < 1000m: 0.2m resolution
- Fields < 1500m: 0.25m resolution
- etc.

This works well for balancing quality vs memory, but doesn't account for device capabilities. A 150ha field on iPad shows:
- CPU: 150%
- Memory: 890MB - 1.4GB (GC sawtooth)
- FPS: 21

Users on constrained devices would benefit from manually lowering resolution for better performance.

## Proposed Solution

### User Setting

Add a "Display Quality" setting with options:
- **High** - Use finest resolution (1.0x multiplier)
- **Medium** - Reduce resolution (1.5x multiplier on cell size)
- **Low** - Further reduce resolution (2.0x multiplier on cell size)
- **Auto** - Current behavior (default)

Or alternatively, a slider from 0.5x to 2.0x multiplier.

### Implementation

1. **Add setting to ConfigurationStore or Settings**
   ```csharp
   public double DisplayResolutionMultiplier { get; set; } = 1.0; // 1.0 = auto, >1.0 = lower quality
   ```

2. **Modify DrawingContextMapControl.CalculateDynamicCellSize()**
   - Apply multiplier after calculating base cell size
   - `actualCellSize = baseCellSize * multiplier`

3. **Add UI control**
   - In Configuration dialog or View Settings panel
   - Show current resolution and estimated memory usage

### Detection Resolution

The detection bit array remains at 0.1m regardless of display setting - this ensures accurate coverage detection and area calculations. Only the visual display is affected.

## Impact

| Setting | Cell Size (500m field) | Memory (approx) | Visual Quality |
|---------|------------------------|-----------------|----------------|
| High    | 0.1m                   | 100%            | Best           |
| Medium  | 0.15m                  | ~44%            | Good           |
| Low     | 0.2m                   | ~25%            | Acceptable     |

## Priority

Low - Enhancement for alpha/beta testing. Current auto-scaling works for most cases.

## Related

- `DrawingContextMapControl.cs` - CalculateDynamicCellSize()
- `PERF-005_DYNAMIC_DISPLAY_RESOLUTION.md` - Current dynamic resolution implementation
