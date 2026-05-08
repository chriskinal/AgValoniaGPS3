# Tabbed perspectives — task-driven UI customization

**Issue:** TBD
**Branch:** TBD (`feature/perspectives` proposed)
**Status:** **On hold** — dev-chat reception was lukewarm. Parking the
plan in case it comes back; the lower-risk glyph-over-word treatment
(see `../GlyphWordButtons/PLAN.md`) is what's moving forward instead.

## Goal

Replace the current "everything is always visible" toolbar layout with a
mobile-style **left-side tab strip**. Each tab is a named **perspective**
that selectively hides UI elements the operator doesn't need for the task
at hand. The map, grid, vehicle, and a small set of safety-critical
overlays stay constant across all tabs.

## Why now

1. The toolbar redesign (PRs #155 onward) reduced cognitive load but
   the cockpit still has too many simultaneously-visible controls for a
   tablet operator running one job.
2. AgValoniaGPS targets tablets used in landscape inside a tractor cab.
   Mobile apps almost universally use task-scoped tabs for this exact
   reason; a vertical tab strip is the natural shape on landscape
   tablets.
3. With #349 (per-job Fields/Jobs) merged, we now have an obvious place
   for a job to remember "this is the view I want when I run this
   task" — turning perspectives from a toy preference into a real
   workflow accelerator.

## Concepts

- **Perspective** — a named saved set of "which UI elements are visible
  in which bar." User-facing word: **tab**.
- **All tab** — the locked first tab. Always shows every element.
  Cannot be edited or deleted. The safety net: any operator can always
  return to the full UI in one tap.
- **Named tab** — a user-created perspective. Can be renamed, deleted,
  reordered, and customized.
- **Config mode** — a per-tab editing state. Every toggleable element
  shows a tap target; tapping toggles its visibility for *this* tab.
  Default state for a brand-new named tab is config mode with everything
  visible (subtractive: user hides what they don't want).
- **User mode** — the normal operating state. Only elements enabled for
  the active tab are visible.
- **Element** — a single toggleable unit. Granularity is button-level
  for individual buttons and group-level for tightly-coupled controls
  (e.g. zoom in / zoom out / fit are one "Zoom Controls" element).
- **Reserved zone** — elements that are *not* toggleable. They live in
  fixed positions on the map overlay or in pinned slots on the bars
  (see "Reserved elements" below).

## Data model

```csharp
// Shared/AgValoniaGPS.Models/Perspectives/

public class Perspective : ObservableObject
{
    public string Id { get; init; }                  // GUID, or "all" for the locked tab
    public string Name { get; set; }                 // user-chosen
    public bool IsLocked { get; init; }              // true only for the All tab
    public int Order { get; set; }                   // 0..N for tab strip order
    public Dictionary<string, bool> ElementVisibility { get; }
        = new();                                     // element ID -> visible
}

public class PerspectivesConfig : ObservableObject
{
    public ObservableCollection<Perspective> Perspectives { get; } = new();
    public string ActivePerspectiveId { get; set; } = "all";
}
```

Persisted via `ConfigurationStore.Display` and round-tripped through
`AppSettings` in `ConfigurationService`, following the existing
`DisplayConfig` pattern.

### Job linkage (#349 follow-on)

```csharp
public class Job
{
    // existing fields...
    public string? LinkedPerspectiveId { get; set; }
}
```

Behavior:
- Job created with `LinkedPerspectiveId = null` (no linkage).
- User can assign a tab to a job from the Job detail panel.
- When a job starts, if `LinkedPerspectiveId != null`, switch to that
  tab **once**. If the user manually switches tabs after that, manual
  wins for the rest of the session. Next time the job starts, the link
  applies again.

### Element registry

A static catalog of every toggleable element, owned by
`Shared/AgValoniaGPS.Models/Perspectives/ElementRegistry.cs`.

```csharp
public static class ElementRegistry
{
    public static readonly IReadOnlyList<ElementDef> All = new[]
    {
        // Top-left system status bar
        new ElementDef("status.fix_quality",    Bar.TopLeft,    "GPS Fix Quality",   reserved: true),
        new ElementDef("status.satellites",     Bar.TopLeft,    "Satellite Count"),
        new ElementDef("status.ntrip",          Bar.TopLeft,    "NTRIP Status"),
        new ElementDef("status.clock",          Bar.TopLeft,    "Clock"),
        new ElementDef("status.field_name",     Bar.TopLeft,    "Field Name"),
        // Top-right field status bar
        new ElementDef("field.area_worked",     Bar.TopRight,   "Area Worked"),
        new ElementDef("field.distance",        Bar.TopRight,   "Distance Traveled"),
        new ElementDef("field.headland_dist",   Bar.TopRight,   "Headland Distance"),
        // Right tool bar
        new ElementDef("tool.uturn",            Bar.Right,      "U-Turn Button"),
        new ElementDef("tool.lateral",          Bar.Right,      "Lateral Adjust"),
        new ElementDef("tool.steer_chart",      Bar.Right,      "Steer Chart"),
        // Bottom tool bar
        new ElementDef("bottom.section_control",Bar.Bottom,     "Section Control"),
        new ElementDef("bottom.simulator",      Bar.Bottom,     "Simulator Panel"),
        // Left tool bar (LeftNavigationPanel sub-buttons)
        new ElementDef("nav.tracks",            Bar.Left,       "Tracks"),
        new ElementDef("nav.fields",            Bar.Left,       "Fields"),
        new ElementDef("nav.boundary",          Bar.Left,       "Boundary"),
        new ElementDef("nav.flags",             Bar.Left,       "Flags"),
        new ElementDef("nav.config",            Bar.Left,       "Configuration"),
        // ... etc
    };
}

public enum Bar { TopLeft, TopRight, Right, Bottom, Left }
public record ElementDef(string Id, Bar Bar, string DisplayName, bool reserved = false);
```

### Reserved elements (always visible, never toggleable)

These are safety-critical or navigation-critical and bypass the
visibility map entirely:

- Map, grid, vehicle sprite, tool sprite — the constants the user named
- **Compass / camera-mode button** (N/H/M/C) — orientation
- **AutoSteer active indicator** — safety
- **GPS fix-quality dot** — safety
- **Tab strip itself** — otherwise the user is stuck
- **Leave-config-mode button** (only shown while in config mode)

## UI / wireframe (landscape tablet)

```
+----+------------------------------------------------------------+----+
|All | top-left status bar           |       top-right field bar  |    |
|----|-------------------------------+----------------------------|    |
|Spr |                                                            |    |
|----|                                                            | R  |
|Til |                                                            | i  |
|----|                          MAP                               | g  |
|Bnd |                  (always visible: grid,                    | h  |
|----|                   vehicle, tool, overlays)                 | t  |
|Sca |                                                            |    |
|----|                                                            | b  |
|+   |                                                            | a  |
+----+------------------------------------------------------------+ r  |
| L  |                                                            |    |
| e  |                                                            |    |
| f  |                                                            |    |
| t  |                                                            |    |
+----+------------------------------------------------------------+----+
                       bottom tool bar
```

- Vertical tab strip on the far left (about 64 px wide). Active tab
  highlighted. Locked "All" tab pinned to top.
- `+` button at bottom of the strip adds a new named tab (entering
  config mode immediately, all elements visible).
- Long-press / right-click on a named tab → rename, delete, reorder,
  link/unlink to current job.
- Soft cap of 8 named tabs + the locked All tab. Past 8, show a hint
  rather than hard-block; we'll evaluate after real-world use.

### Target resolutions

Mockups assume a 16:10 landscape tablet. The layout must work down to a
**minimum logical resolution of 1280×800 DIPs** (≈1366×768 on a 16:9
device). Reference points:

- **Panasonic FZ-G1** (popular ag rugged tablet, Win) — 10.1", 1920×1200
  native 16:10. Comfortable headroom; UI scale up to 125–150% reads well
  at arm's length.
- **Getac F110** — 11.6", 1366×768 16:9. The squeezy floor — the bottom
  bar with all top-level buttons visible has to fit, or rely on a
  perspective with fewer buttons.
- **Consumer 10" Android** — frequently 1280×800 native 16:10. Same
  floor as Getac height-wise but slightly more horizontal slack.
- **iPad Pro 12.9"** — 1366×1024 logical (4:3). Vertical room is
  generous; horizontal room is tighter than the 16:10 reference because
  of the squarer aspect ratio. The bottom bar should reflow / wrap if
  needed.

Tap targets stay ≥44pt regardless of physical pixel density. Buttons
size in DIPs, not pixels, so the FZ-G1's high PPI doesn't shrink them.

### Config mode

```
+--------------------------------------------------------------------+
|  CONFIG MODE: Spraying — tap elements to hide  [Reset all] [Done]  |
+--------------------------------------------------------------------+
| ...same layout, every toggleable element shows a small overlay     |
|    badge: filled checkmark = visible, empty circle = hidden.       |
|    Tapping toggles. Reserved elements show no badge.               |
+--------------------------------------------------------------------+
```

## Behavior rules

- **First launch:** `Perspectives = [All]`, `ActivePerspectiveId = "all"`.
  No visible UI difference from today's app.
- **Switching tabs:** instantaneous. No animation in v1 (revisit if it
  feels jarring).
- **Persistence:** active tab persists across launches via
  `DisplayConfig.PerspectivesConfig.ActivePerspectiveId`. On launch we
  restore the last active tab, *unless* the about-to-start job has a
  linked tab, in which case the link wins (but only once — see Job
  linkage above).
- **Hotkeys:** add `NextTab` / `PrevTab` / `JumpToAll` to the existing
  `HotkeyConfig`. `JumpToAll` is the universal escape when an operator
  feels lost.
- **Concurrent operations:** running operations (autosteer, recording,
  NTRIP) are unaffected by tab switches. Their state cues live in the
  Reserved zone so they're never hidden.

## Implementation phases

Each phase leaves a working, releasable app.

### Phase 1 — Element registry + identifiers
- Define `ElementRegistry` and assign IDs to every existing toggleable
  element via attached properties on the relevant XAML controls.
- No UI behavior change. PR is mostly mechanical tagging.
- Validates: catalog completeness; registry covers everything.

### Phase 2 — Data model + persistence
- Add `Perspective`, `PerspectivesConfig` to `Models/`.
- Wire round-trip through `DisplayConfig` ↔ `AppSettings` ↔
  `ConfigurationService`.
- Seed the locked All tab on first run.
- Add the `Completeness_AllPersistableProperties_AreMapped` test entry.
- No UI behavior change yet.

### Phase 3 — Tab strip UI (read-only)
- New `LeftTabStrip` control. Shows just the All tab initially.
- `MainViewModel.ActivePerspectiveId` exposed for binding.
- Tapping a tab sets active. (Only one tab to tap at this stage.)
- No element hiding wired yet.

### Phase 4 — Element visibility
- Each toggleable XAML control binds `IsVisible` to an attached behavior
  that resolves through `ActivePerspective.ElementVisibility[Id]`.
- Reserved elements bypass the binding.
- All tab is hardcoded all-true so its behavior matches today exactly.
- Validates the wiring end-to-end before any user-facing config UI.

### Phase 5 — Tab CRUD + config mode
- Add tab (`+` button → config mode).
- Config-mode banner with Done / Reset to all.
- Tap-to-toggle on every toggleable element.
- Rename / delete / reorder via long-press menu.
- Soft-cap warning at 8 named tabs.

### Phase 6 — Job linkage
- Add `LinkedPerspectiveId` to `Job`.
- "Link to current tab" action in the Job detail panel.
- On `JobService.StartJob`, switch to linked tab once (manual override
  rule from above).

### Phase 7 — Polish
- Hotkeys (`NextTab` / `PrevTab` / `JumpToAll`).
- Optional swipe-between-tabs gesture on iOS/Android.
- Telemetry or logging on tab usage to inform whether the soft cap of
  8 is right.

## Open questions

1. **Sub-tab grouping.** Should named tabs be flat (just a list) or
   support a small grouping (e.g. "Spring" / "Summer / "Fall")? Lean:
   flat. Revisit if users start naming "Spr_Spray_Field1" etc.
2. **Element granularity.** This plan treats each XAML control as one
   element. Some users may want finer control inside multi-button panels
   (e.g. hide just one section button). We can subdivide elements later
   without breaking the data model.
3. **Cross-platform behavior.** Long-press menus need to work cleanly on
   touch (iOS/Android) and right-click on Desktop. Existing code already
   handles this in the boundary/track context menus — reuse.
4. **Migration.** No migration needed: existing users get the All tab
   only on first run after the upgrade, which behaves identically to
   today's UI.
5. **Renderer hooks.** Some "elements" are actually map overlays drawn
   in `DrawingContextMapControl` (e.g. flags labels, tracks). Are those
   toggleable per-tab too, or are they always-on map content like the
   grid? Lean: **always-on**, since they're field data, not chrome.

## Out of scope

- Moving elements between bars. Each element has a fixed home bar; tabs
  only control on/off in that bar.
- Per-job UI customization beyond the tab link.
- Renderer-level customization (toggling map layers stays in the
  existing Display settings).
