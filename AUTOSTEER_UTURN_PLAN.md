# Autosteer and Zig-Zag U-Turn Implementation Plan

## Current State Analysis

### What's Working
1. **Autosteer/Pure Pursuit Guidance** - Fully functional
   - `PurePursuitGuidanceService` calculates steering angles
   - `CalculateAutoSteerGuidance()` in MainViewModel integrates guidance
   - Dynamic path offset via `_howManyPathsAway` for parallel passes
   - Cross-track error display and steering angle application to simulator

2. **U-Turn Path Creation** - Basic implementation working
   - `YouTurnCreationService` creates Omega and Wide turns (AlbinStyle)
   - KStyle turns also available
   - Boundary detection and path validation
   - Dubins path integration for smooth curves

3. **U-Turn Execution** - Working with issues
   - `ProcessYouTurn()` detects headland approach
   - Turn triggering at 2m from path start
   - Path completion detection
   - Line switching via `CompleteYouTurn()`

### Current Issues to Address
1. **No zig-zag pattern** - Only single-arc U-turns exist
2. **Turn path guidance** - Uses simple distance-to-end check, not proper path following
3. **Turn direction alternation** - May need improvement for consistent patterns

## Implementation Plan

### Phase 1: Add Zig-Zag Turn Type to YouTurnCreationService

**File:** `Shared/AgValoniaGPS.Services/YouTurn/YouTurnCreationService.cs`

1. Add new `ZigZagStyle` value to `YouTurnType` enum:
   ```csharp
   public enum YouTurnType
   {
       AlbinStyle = 0,  // Omega/Wide
       KStyle = 1,
       ZigZagStyle = 2  // NEW: Multi-leg zig-zag
   }
   ```

2. Add `CreateZigZagTurn()` method that:
   - Calculates available headland width
   - Determines number of legs needed (width / turn_radius)
   - Creates alternating left-right arcs with straight connecting segments
   - Pattern: Entry → Arc1 (90°) → Straight → Arc2 (-90°) → Straight → Arc3 (90°) → Exit

3. Zig-zag geometry:
   ```
   Entry leg (on current AB line)
        |
        v
   Arc 1 (turn 90° toward next line)
        |
        v
   Straight segment (perpendicular to AB, toward next line)
        |
        v
   Arc 2 (turn 90° back toward AB direction)
        |
        v
   [Repeat if needed]
        |
        v
   Exit leg (on next AB line)
   ```

### Phase 2: Improve YouTurn Path Following During Turn

**File:** `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs`

Currently when `_isInYouTurn` is true, the code uses `YouTurnGuidanceService.CalculateGuidance()`. This needs integration:

1. Wire up `YouTurnGuidanceService` properly in the guidance loop:
   ```csharp
   if (_isInYouTurn && _youTurnPath != null)
   {
       var ytInput = BuildYouTurnGuidanceInput(currentPosition, _youTurnPath);
       var ytOutput = _youTurnGuidanceService.CalculateGuidance(ytInput);

       if (ytOutput.IsTurnComplete)
       {
           CompleteYouTurn();
       }
       else
       {
           SimulatorSteerAngle = ytOutput.SteerAngle;
           CrossTrackError = ytOutput.DistanceFromCurrentLine * 100;
       }
   }
   ```

2. Build proper `YouTurnGuidanceInput` with:
   - Turn path points
   - Current vehicle position (steer and pivot)
   - Stanley/Pure Pursuit parameters
   - Speed and heading data

### Phase 3: Zig-Zag Turn Algorithm Details

**Geometry for each leg:**

```
Parameters:
- turnRadius: minimum turn radius (from vehicle config)
- toolWidth: implement width
- headlandWidth: distance from boundary to field
- legCount: ceil((toolWidth) / (turnRadius * 2))

For each zig-zag:
1. Entry point: where AB line meets headland boundary
2. First arc: 90° turn in desired direction, radius = turnRadius
3. Straight leg: length = min(headlandWidth, toolWidth / legCount)
4. Second arc: 90° turn opposite direction
5. Repeat until lateral offset = toolWidth
6. Final arc: align with exit AB line
7. Exit leg: straight on next AB line
```

**Point generation:**
- Arc points: step through angle at pointSpacing = turnRadius * 0.1
- Straight points: every 0.5m
- Calculate heading at each point for guidance

### Phase 4: Add UI Control for Turn Type Selection

**File:** Create or modify turn type selector

1. Add `YouTurnSelectedType` property to MainViewModel
2. Add UI toggle in settings or toolbar:
   - Omega/Wide (AlbinStyle)
   - K-Style
   - Zig-Zag (new)

### Phase 5: Testing and Refinement

1. Test with simulator:
   - Create field with boundary and headland
   - Create AB line
   - Enable autosteer and YouTurn
   - Verify zig-zag creates proper multi-leg path
   - Verify guidance follows path smoothly
   - Verify line switching at completion

2. Edge cases:
   - Very narrow headlands (fall back to omega)
   - Very wide headlands (single leg sufficient)
   - Curved AB lines (use curve-specific methods)

## Files to Modify

| File | Changes |
|------|---------|
| `Models/YouTurn/YouTurnType.cs` | Add ZigZagStyle enum value |
| `Services/YouTurn/YouTurnCreationService.cs` | Add CreateZigZagTurn methods |
| `ViewModels/MainViewModel.cs` | Wire up YouTurnGuidanceService properly |
| `Views/Controls/Panels/*` | Add turn type selector UI (optional) |

## Implementation Order

1. Add ZigZagStyle to YouTurnType enum
2. Implement CreateZigZagTurnAB() in YouTurnCreationService
3. Wire up YouTurnGuidanceService in MainViewModel for turn path following
4. Test basic zig-zag turn creation
5. Test path following during zig-zag turn
6. Add UI for turn type selection
7. Test full cycle: multiple U-turns across field

## Success Criteria

1. Zig-zag path is created when headland is narrow
2. Vehicle follows zig-zag path smoothly with proper steering
3. Vehicle completes turn and switches to next AB line
4. Pattern repeats correctly for subsequent turns
5. Cross-track error shows reasonable values during turn
