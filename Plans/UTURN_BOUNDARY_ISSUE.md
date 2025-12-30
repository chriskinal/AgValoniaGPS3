# U-Turn Boundary Constraint Issue

## Problem
U-turn arcs can extend outside the outer field boundary when:
- The headland zone is narrow
- The turn radius is large relative to headland width

## Observed Behavior
- Orange U-turn arc swings beyond the yellow outer boundary
- Should stay within the headland zone (between green headland line and yellow outer boundary)

## Screenshot Reference
See: `/Users/chris/Desktop/Screenshot 2025-12-29 at 8.04.51 PM.png`

## Investigation Areas
- `MainViewModel.CreateSimpleUTurnPath()` - U-turn generation logic
- Arc positioning relative to headland and boundary
- May need to check if arc fits before generating, or clamp to boundary

## Priority
Low - Edge case behavior, doesn't affect normal operation

## Date Noted
2025-12-29
