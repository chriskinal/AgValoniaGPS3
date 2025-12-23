# U-Turn Configuration Enhancement Plan

**Date:** December 21, 2025
**Goal:** Align U-Turn configuration tab with AgOpenGPS implementation, add proper graphics

## Executive Summary

The current U-Turn configuration has 5 parameters but AgOpenGPS's U-turn config dialog has **4 visual parameters** with corresponding diagrams. The extra "Skip Width" parameter exists in AgOpenGPS but is controlled via **buttons on the main screen** (skip row left/right), not in the configuration dialog.

**Current state:** 5 settings in a plain list, no graphics
**Target state:** 4 settings with inline diagrams matching AgOpenGPS

---

## AgOpenGPS U-Turn Configuration Analysis

### Parameters in AgOpen Config Dialog (4 total)

| Parameter | XML Setting | Default | Unit | Graphic |
|-----------|-------------|---------|------|---------|
| **Turn Radius** | `set_youTurnRadius` | 8.0 | meters | `ConU_UturnRadius.png` |
| **Extension Length** | `set_youTurnExtensionLength` | 20.0 | meters | `ConU_UturnLength.png` |
| **Distance from Boundary** | `set_youTurnDistanceFromBoundary` | 2.0 | meters | `ConU_UturnDistance.png` |
| **Smoothing** | `setAS_uTurnSmoothing` | 14 | integer (1-50) | `ConU_UturnSmooth.png` |

### Parameters NOT in Config Dialog

| Parameter | XML Setting | Default | Where Controlled |
|-----------|-------------|---------|------------------|
| **Skip Width** | `set_youSkipWidth` | 1 | Main screen buttons (skip row left/right) |
| **Style** | `set_uTurnStyle` | 0 | Probably deprecated or advanced |
| **Compensation** | `setAS_uTurnCompensation` | 1.0 | AutoSteer settings (advanced) |

---

## Graphic Analysis

### ConU_UturnRadius.png
Shows the U-turn arc with radius measurement indicator. The radius determines how tight the U-turn is - smaller radius = tighter turn (requires more steering angle).

### ConU_UturnLength.png
Shows the extension legs of the U-turn path extending beyond the headland. Longer extension = more room before starting the actual turn arc.

### ConU_UturnDistance.png
Shows how far from the boundary/headland line the vehicle should be when the U-turn path calculation starts. This is the trigger distance.

### ConU_UturnSmooth.png
Shows the smoothing applied to the U-turn path. Higher values create smoother curves but may make turns less precise.

---

## UI Layout Design

Following the Tool Config sub-tab pattern (graphics on left, settings on right):

```
┌─────────────────────────────────────────────────────────────────┐
│ [160x120 Radius Graphic]  │  Turn Radius                        │
│                           │  Radius of the U-turn arc           │
│                           │  [████████ 8.0 m ████████]          │
├───────────────────────────┼─────────────────────────────────────┤
│ [160x120 Length Graphic]  │  Extension Length                   │
│                           │  Path extension beyond headland     │
│                           │  [███████ 20.0 m ████████]          │
├───────────────────────────┼─────────────────────────────────────┤
│ [160x120 Distance Graphic]│  Distance from Boundary             │
│                           │  Trigger distance from headland     │
│                           │  [████████ 2.0 m ████████]          │
├───────────────────────────┼─────────────────────────────────────┤
│ [160x120 Smooth Graphic]  │  Path Smoothing                     │
│                           │  Smoothing factor for turn path     │
│                           │  [██████████ 14 ███████████]        │
├─────────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ Usage Tips:                                                  │ │
│ │ • Turn Radius - Match to vehicle turning capability          │ │
│ │ • Extension - Increase if turns start too abruptly           │ │
│ │ • Distance - How far from boundary to trigger turn calc      │ │
│ │ • Smoothing - Higher = smoother but less precise (1-50)      │ │
│ └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

---

## Implementation Changes

### 1. Remove Skip Width from U-Turn Config Tab

Skip Width belongs on the main screen as a runtime control (like AgOpenGPS), not a configuration setting. The current implementation already has `UTurnSkipRows` in MainViewModel controlled by skip row buttons.

**Action:** Remove the Skip Width row from `UTurnConfigTab.axaml`

### 2. Add Graphics to Each Setting

Use the existing ConU_*.png graphics, displayed at 160x120 (matching Tool Config timing graphics).

**Layout per setting:**
```xml
<Grid ColumnDefinitions="Auto,*">
    <Border Background="#1A1A2E" CornerRadius="6" Padding="4"
            Width="160" Height="120" Margin="0,0,12,0">
        <Image Source="avares://AgValoniaGPS.Views/Assets/Icons/Config/ConU_UturnRadius.png"
               Stretch="Uniform"/>
    </Border>
    <StackPanel Grid.Column="1" VerticalAlignment="Center">
        <TextBlock Text="Turn Radius" FontWeight="SemiBold" Foreground="White"/>
        <TextBlock Text="Radius of the U-turn arc" Foreground="#888" FontSize="11"/>
        <Button Classes="TappableValue" Command="{Binding EditUTurnRadiusCommand}">
            <TextBlock Text="{Binding Guidance.UTurnRadius, StringFormat='{}{0:F1} m'}" .../>
        </Button>
    </StackPanel>
</Grid>
```

### 3. Remove Title/Header Lines

Following the established pattern from Tool Config enhancements, remove:
- "U-Turn Settings" title
- Any unnecessary explanatory text at top

### 4. Add Info Box at Bottom

Add a green-tinted info box with usage tips (matching Tool Config pattern).

---

## Files to Modify

| File | Changes |
|------|---------|
| `UTurnConfigTab.axaml` | Complete rewrite with new layout |
| `ConfigurationViewModel.cs` | Remove `EditUTurnSkipWidthCommand` (optional - can leave for future use) |

---

## Graphics Available

All graphics already exist in `Assets/Icons/Config/`:

| File | Purpose | Size |
|------|---------|------|
| `ConU_UturnRadius.png` | Turn radius diagram | Display at 160x120 |
| `ConU_UturnLength.png` | Extension length diagram | Display at 160x120 |
| `ConU_UturnDistance.png` | Boundary distance diagram | Display at 160x120 |
| `ConU_UturnSmooth.png` | Smoothing diagram | Display at 160x120 |

---

## Settings Specification

### Turn Radius
- **Type:** double
- **Unit:** meters
- **Range:** 2.0 - 50.0
- **Default:** 8.0
- **Description:** Radius of the U-turn arc. Smaller = tighter turns. Must match vehicle's actual turning capability.

### Extension Length
- **Type:** double
- **Unit:** meters
- **Range:** 0.0 - 100.0
- **Default:** 20.0
- **Description:** How far the U-turn path extends beyond the headland before starting the arc. Increase if U-turns feel too abrupt.

### Distance from Boundary
- **Type:** double
- **Unit:** meters
- **Range:** 0.0 - 20.0
- **Default:** 2.0
- **Description:** Distance from the headland boundary at which the U-turn path calculation triggers. Increase for earlier path generation.

### Path Smoothing
- **Type:** integer
- **Unit:** none
- **Range:** 1 - 50
- **Default:** 14
- **Description:** Smoothing factor applied to the generated U-turn path. Higher values create smoother curves but may reduce precision.

---

## Validation Rules

1. Turn Radius must be positive and reasonable for a vehicle (2-50m)
2. Extension Length cannot be negative
3. Distance from Boundary cannot be negative
4. Smoothing must be clamped to 1-50 range

---

## Testing Checklist

- [ ] Turn Radius graphic displays correctly
- [ ] Extension Length graphic displays correctly
- [ ] Distance from Boundary graphic displays correctly
- [ ] Smoothing graphic displays correctly
- [ ] All values editable via numeric input dialog
- [ ] Values persist after closing/reopening config
- [ ] Values save/load correctly from AgOpenGPS XML profiles
- [ ] Skip Width removed from U-Turn tab
- [ ] Skip Width still works via main screen buttons
- [ ] Info box displays with helpful tips
- [ ] No scrolling needed on tablet-sized screens

---

## Notes

### Skip Width Handling

Skip Width (`set_youSkipWidth`) is handled separately from this config dialog:
- Stored in `Guidance.UTurnSkipWidth`
- Used by `MainViewModel.UTurnSkipRows` for runtime control
- Controlled via skip row left/right buttons on main screen
- Still saved/loaded in AgOpenGPS profiles for compatibility

This matches AgOpenGPS behavior where skip width is a quick-access control during field operation, not a setup-time configuration.

### Future Enhancements

Consider adding animated GIFs like the Tool Timing tab if suitable animations become available. The static PNG diagrams are sufficient for initial implementation.
