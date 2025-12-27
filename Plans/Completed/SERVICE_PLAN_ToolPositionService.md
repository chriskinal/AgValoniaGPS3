# Feature: ToolPositionService

## Overview

Calculate the position and heading of the tool/implement relative to the vehicle pivot point. Handles different tool configurations: fixed (front/rear), trailing, and TBT (Tow-Between-Tractor with tank trailer).

## WinForms Reference

### Files
- `GPS/Forms/Position.designer.cs` - Lines 1280-1375: Tool position calculations
- `GPS/Classes/CTool.cs` - Tool configuration wrapper
- `AgOpenGPS.Core/Models/Tool/ToolConfiguration.cs` - Core tool settings

### Key Position Variables
```csharp
// From Position.designer.cs
public vec3 hitchPos = new vec3(0, 0, 0);      // Hitch point on vehicle
public vec3 tankPos = new vec3(0, 0, 0);       // Tank trailer position (for TBT)
public vec3 toolPivotPos = new vec3(0, 0, 0);  // Tool pivot/hitch position
public vec3 toolPos = new vec3(0, 0, 0);       // Final tool center position
```

### Tool Types

1. **Fixed Front** (`isToolFrontFixed`): Tool rigidly attached in front of pivot
   - `hitchLength` is positive
   - Tool follows vehicle heading exactly

2. **Fixed Rear** (`isToolRearFixed`): Tool rigidly attached behind pivot
   - `hitchLength` is negative
   - Tool follows vehicle heading exactly

3. **Trailing** (`isToolTrailing`): Tool trails behind on a hitch
   - Tool heading calculated from movement history
   - Uses `trailingHitchLength` and `trailingToolToPivotLength`
   - Tool can swing side-to-side during turns

4. **TBT - Tow Between Tractor** (`isToolTBT` + `isToolTrailing`): Two-stage trailing
   - Tank trailer follows vehicle
   - Tool follows tank trailer
   - Uses `tankTrailingHitchLength` for tank, `trailingHitchLength` for tool

### Core Algorithm (Trailing Tool)

```csharp
// From Position.designer.cs lines 1292-1374

// 1. Calculate hitch point (where tool attaches to vehicle)
hitchPos.easting = fix.easting + Sin(fixHeading) * (hitchLength - AntennaPivot);
hitchPos.northing = fix.northing + Cos(fixHeading) * (hitchLength - AntennaPivot);

// 2. For TBT: Calculate tank position first
if (isToolTBT)
{
    // Tank heading follows movement (Torriem's algorithm)
    tankPos.heading = Atan2(hitchPos.easting - tankPos.easting,
                           hitchPos.northing - tankPos.northing);

    // Move tank to trail behind hitch
    tankPos.easting = hitchPos.easting + Sin(tankPos.heading) * tankTrailingHitchLength;
    tankPos.northing = hitchPos.northing + Cos(tankPos.heading) * tankTrailingHitchLength;
}

// 3. Calculate tool heading from movement
toolPivotPos.heading = Atan2(tankPos.easting - toolPivotPos.easting,
                             tankPos.northing - toolPivotPos.northing);

// 4. Move tool to trail behind tank (or hitch for non-TBT)
toolPivotPos.easting = tankPos.easting + Sin(toolPivotPos.heading) * trailingHitchLength;
toolPivotPos.northing = tankPos.northing + Cos(toolPivotPos.heading) * trailingHitchLength;

// 5. Calculate final tool center position
toolPos.easting = tankPos.easting +
    Sin(toolPivotPos.heading) * (trailingHitchLength - trailingToolToPivotLength);
toolPos.northing = tankPos.northing +
    Cos(toolPivotPos.heading) * (trailingHitchLength - trailingToolToPivotLength);
```

### Jackknife Protection

The algorithm includes protection against jackknifing:
```csharp
// If angle between tool and vehicle exceeds ~115 degrees, snap tool inline
double over = Abs(PI - Abs(Abs(toolHeading - vehicleHeading) - PI));
if (over > 2.0)  // ~115 degrees
{
    // Force tool directly behind vehicle
    toolPivotPos.heading = vehicleHeading;
    // Recalculate position...
}
```

## Config Settings Used

From `ConfigurationStore.Instance.Tool`:
- `HitchLength` - Distance from pivot to hitch point (negative = rear)
- `TrailingHitchLength` - Length of trailing hitch
- `TankTrailingHitchLength` - Length of tank trailer hitch (TBT only)
- `TrailingToolToPivotLength` - Offset from hitch to tool center
- `IsToolTrailing` - Tool trails behind vehicle
- `IsToolTBT` - Two-stage trailing (tank + tool)
- `IsToolRearFixed` - Rigidly attached at rear
- `IsToolFrontFixed` - Rigidly attached at front
- `Offset` - Lateral offset of tool from centerline

## Proposed Interface

```csharp
public interface IToolPositionService
{
    // Current tool state
    Vec3 ToolPosition { get; }
    Vec3 ToolPivotPosition { get; }
    double ToolHeading { get; }
    Vec3 TankPosition { get; }  // For TBT mode

    // Update tool position based on vehicle position
    void Update(Vec3 vehiclePivot, double vehicleHeading, double distanceTraveled);

    // Get section edge positions in world coordinates
    (Vec3 left, Vec3 right) GetToolEdgePositions();

    // Get specific section position
    Vec3 GetSectionPosition(int sectionIndex, double sectionLeft, double sectionRight);

    // Reset trailing state (e.g., when starting new field)
    void ResetTrailingState(Vec3 vehiclePivot, double vehicleHeading);

    // Events
    event EventHandler<ToolPositionUpdatedEventArgs> PositionUpdated;
}

public class ToolPositionUpdatedEventArgs : EventArgs
{
    public Vec3 ToolPosition { get; set; }
    public double ToolHeading { get; set; }
    public Vec3 TankPosition { get; set; }  // Null if not TBT
}
```

## Dependencies

### Required Services
- None (standalone calculation)

### Required Config
- `ConfigurationStore.Instance.Tool` for all hitch/length settings
- `ConfigurationStore.Instance.Vehicle` for AntennaPivot

## Implementation Steps

1. [ ] Create `IToolPositionService` interface in `Services/Interfaces/`
2. [ ] Create `ToolPositionService` class in `Services/Tool/`
3. [ ] Implement fixed tool position calculation (simple offset)
4. [ ] Implement trailing tool position calculation (Torriem algorithm)
5. [ ] Implement TBT two-stage trailing calculation
6. [ ] Implement jackknife protection and reset
7. [ ] Add section position calculations
8. [ ] Add DI registration
9. [ ] Integrate with MainViewModel GPS update loop
10. [ ] Update tool rendering to use service position
11. [ ] Test with simulator in different tool configurations

## Tool Position Diagram

```
Fixed Rear Tool:
    [TRACTOR]----[TOOL]
         ^pivot   ^hitchLength (negative)

Trailing Tool:
    [TRACTOR]----o~~~~[TOOL]
         ^pivot  ^hitch  ^trailingHitchLength
                         ^trailingToolToPivotLength

TBT (Tow-Between-Tractor):
    [TRACTOR]----o~~~~[TANK]~~~~[TOOL]
         ^pivot  ^hitch ^tankTrailingHitchLength
                              ^trailingHitchLength
```

## State Management

For trailing tools, the service must maintain state between updates:
- Previous tool position (for heading calculation)
- Previous tank position (for TBT)
- Start counter (for jackknife protection during startup)

```csharp
private Vec3 _lastToolPivotPos;
private Vec3 _lastTankPos;
private int _startCounter;
private const int STARTUP_FRAMES = 50;
```

## Priority

**Medium** - Required for accurate coverage mapping with trailing implements. Most tractors use trailing tools, so this is important for real-world usage.

## Notes

- The "Torriem algorithm" for trailing tool heading is elegant - it calculates heading from the vector between current and previous position
- Must handle startup case where we have no previous position (snap tool behind vehicle)
- Tool offset (lateral) is applied separately from the position calculation
- This service should be called every GPS update for smooth trailing behavior
