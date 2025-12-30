# U-Turn Boundary Issue

## Problem
AgValonia continues plotting U-turns and autosteer keeps steering even when the path goes outside the outer boundary.

## AgOpenGPS Behavior (correct)
- Stops plotting U-turns that would extend outside the boundary
- Autosteer disengages when vehicle leaves the boundary
- Only plots the portion of the path that stays inside

## AgValonia Behavior (incorrect)
- Plots full U-turn arc even when it extends outside boundary
- Autosteer continues steering outside the boundary

## Screenshot
See: `Plans/Uturn_Boundary_Issue.png` - shows orange U-turn arc extending past yellow outer boundary

## Fix Needed
1. Check if U-turn path points are inside outer boundary before plotting
2. Truncate or stop plotting when path would go outside
3. Disengage autosteer when vehicle position is outside outer boundary

## Investigation Areas
- `MainViewModel.CreateSimpleUTurnPath()` - U-turn generation
- `YouTurnGuidanceService` - U-turn path following
- Boundary point-in-polygon checks

## Priority
Medium - affects edge-of-field behavior

## Date Noted
2025-12-29
