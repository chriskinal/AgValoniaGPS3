# Glyph-over-word buttons — chrome icon refresh

**Issue:** TBD
**Branch:** TBD (`feature/glyph-word-buttons` proposed)
**Status:** Draft — leading candidate from UX dev-chat (positive
reception). Independent of the parked perspectives plan.

## Goal

Replace the current chunky icon-only buttons across the app shell with a
stacked **glyph + word** treatment: a small monochrome glyph above a
short text label. Status indicators (read-only data fields) stay as
they are.

Visual reference: `mockups/glyph_over_word.png` (and `.svg`).

## Why now

1. AgOpen-style icon-only chrome is memorization-heavy. New operators
   spend their first hours hovering over tooltips. Pairing every action
   with a label removes that tax.
2. Operators glance between the field and the screen. A labeled button
   is recognized in one eye-flick; an unlabeled icon often takes two.
3. Reception in the dev chat was strongly positive — low-risk, high
   user-value, ships independently of any other UX work.
4. Sets up a reusable `GlyphButton` style that the rest of the codebase
   can adopt incrementally without big-bang rewrites.

## Scope

**In:**
- LeftNavigationPanel top-level buttons (~7).
- RightNavigationPanel top-level buttons (~7).
- BottomNavigationPanel top-level buttons (~12 visible at any time;
  more inside expandable sub-menus that we'll touch in a later pass).

**Out (this pass):**
- Status indicators in top-left / top-right bars (RTK fix, satellite
  count, NTRIP state, area/distance/headland readouts, AutoSteer
  indicator). They're data, not actions.
- Inside-dialog buttons. Dialogs get their own design pass.
- Map overlays (compass / camera-mode button) — already a single-glyph
  treatment that works as-is.
- Sub-menus inside expandable bottom-bar groups (AB-line nudge panel,
  flag list, etc.). Same pattern, separate PR.

## Glyph style

- **Monochrome line glyphs** at ~32 DIP, sized for a 96×80 DIP button.
- **Stroke ~2 pt** — slightly chunkier than Windows' Fluent (1.5 pt) to
  read clearly on a vibrating tablet at arm's length.
- **Simple geometric forms** — folder, gear, wrench, tractor outline,
  etc. The starter palette in `mockups/glyph_over_word.svg` is
  representative; final SVGs to be hand-rolled or sourced from a
  permissive library (see "Icon source" below).
- **State color goes on the *background*, not the glyph.** A toggled-on
  button gets an accent-tinted background; the glyph itself stays
  monochrome so it remains legible against any state. Exception:
  safety-critical indicators (AutoSteer, fix quality) keep colored
  glyphs because color carries the meaning.

## Label style

- **11–12 DIP**, sans-serif, sentence case (not ALL CAPS).
- **One line** — abbreviate when needed (e.g. "Auto Sect" instead of
  "All Sections Auto"; "U-Turn L" instead of "Manual U-Turn Left").
- **Localized** through the existing `Localization/Strings.resx`. New
  resource entries per element ID — short keys like
  `Btn.Vehicle`, `Btn.AutoSteer`.

## Icon source

Two options:

1. **Hand-rolled SVGs** in `Shared/AgValoniaGPS.Views/Icons/`, one file
   per glyph, named after the element ID. Maximum control over the
   2 pt stroke and visual consistency.
2. **External library** — Lucide (MIT), Tabler Icons (MIT), or Fluent
   System Icons (MIT). Saves drawing time but locks our visual tone to
   the library's choices.

Recommendation: **ship with hand-rolled** because the catalog is small
(~25 glyphs) and our requirements (2 pt stroke, vibration-friendly) are
specific. Revisit a library only if the catalog grows past ~50.

## Reusable control

Add `GlyphButton` (or a `Style Selector="Button.Glyph"` against the
existing `Button` — to be decided during Phase 2):

```xml
<Button Classes="Glyph"
        Glyph="{StaticResource glyph.vehicle}"
        Label="{l:Localize Btn.Vehicle}"
        Command="{Binding ShowVehicleConfigCommand}"/>
```

Provides:
- Vertical stack: glyph at top (~32 DIP square), label below.
- Built-in `:pressed` and `:checked` visual states using
  background-tint, not glyph color.
- Tap target ≥44 pt regardless of theme.

## Target resolutions

The current chrome works down to **1280×800 DIPs** (16:10) or
**1366×768 DIPs** (16:9). Glyph+word buttons widen the toolbars
slightly compared to icon-only. Bottom bar at ~12 buttons × 96 DIP is
~1152 DIPs wide — fits 1280 with margin, fits 1366 comfortably.

Reference targets:
- Panasonic FZ-G1 (1920×1200, 16:10) — comfortable; UI-scale of 125%
  reads well at arm's length.
- Getac F110 (1366×768, 16:9) — the squeezy floor; bottom bar fits
  exactly without padding loss.
- Consumer 10" Android (1280×800) — same horizontal slack as 1280×800
  reference.
- iPad Pro 12.9" (1366×1024) — generous vertical, slightly tighter
  horizontal because of the 4:3 aspect ratio. Bottom bar may need to
  reflow / wrap if many buttons are visible.

## Implementation phases

Each phase leaves a working, releasable app.

### Phase 1 — Icon assets + naming
- Create `Shared/AgValoniaGPS.Views/Icons/` and a naming convention
  (e.g. `glyph.vehicle.svg`).
- Hand-roll the ~25 starter glyphs per the mockup.
- Add to project as embedded resources.

### Phase 2 — `GlyphButton` style
- Decide: subclass `Button` vs styled-button via class. Lean: styled
  via class, since we don't need new properties beyond what `Button`
  already supports — pass icon path through `Tag` or as a content
  template.
- Implement vertical-stack content template, 44 pt min height,
  state-as-background-tint.
- Unit test: light/dark theme, enabled/disabled, pressed, checked.

### Phase 3 — Localization keys
- Add `Btn.*` resource entries in `Strings.resx` for every element in
  the catalog.
- Wire localized labels through the existing `{l:Localize}` pattern.

### Phase 4 — Migrate LeftNavigationPanel
- Convert each button to the `GlyphButton` style.
- Verify behavior on Desktop, iOS (physical iPad), Android.
- Check FZ-G1 / 1366×768 logical layout.

### Phase 5 — Migrate RightNavigationPanel + BottomNavigationPanel
- Same pattern. Bottom panel may need reflow logic if width gets tight.

### Phase 6 — Audit + polish
- Walk through every panel, confirm no orphaned old-style buttons.
- Verify `Strings.resx` localizations for at least en, de, fr (the
  three most-active translations).
- A11y pass: focus visuals, keyboard navigation, screen-reader labels.

## Open questions

1. **Tooltip strategy.** Buttons now self-document with the label. Do
   we keep the existing `ToolTip.Tip` text as expanded help (e.g.
   "Manual U-Turn Left — triggers a left-side U-turn ignoring the
   guidance line"), or drop tooltips entirely? Lean: keep them, but
   shorten — they become the secondary description rather than the
   primary identifier.
2. **Compact mode.** Some operators on smaller screens (Getac F110
   1366×768) may want icon-only mode back to save space. Add a
   "Show button labels" toggle in Display config? Adds complexity but
   is cheap if `GlyphButton` is the right control. Lean: defer; revisit
   after first round of in-cab user feedback.
3. **Icon vendor commitment.** If hand-rolled glyphs become a
   maintenance drag (every new feature needs a new glyph), we revisit
   the library decision at Phase 5.

## Out of scope

- Toggling individual button visibility per-task — that's the parked
  perspectives plan.
- UI-wide font/scale control — separate plan, but the two compose
  cleanly (glyph buttons scale through the same `LayoutTransform`).
- Animated state transitions on buttons — punt to a separate polish
  pass.
