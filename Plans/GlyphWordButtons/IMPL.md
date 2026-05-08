# Glyph + Words — Implementation plan, Phase 1: top-level buttons

**Design plan:** `PLAN.md` (this folder)
**Scope:** First wave only — all top-level buttons in the three
navigation panels (Left, Right, Bottom). Sub-menu buttons inside
expanders stay icon-only for now (separate phase).
**Branch:** `feature/glyph-buttons-phase1` proposed

## Inventory

Audit done against develop @ `5c72f1c`. Counts may shift slightly as
we touch the panels.

- **`LeftNavigationPanel.axaml`** — 7 top-level buttons
  Settings · View · Tools · Vehicle · Fields · Field Tools · Steer Config
- **`RightNavigationPanel.axaml`** — 7 top-level buttons
  Contour · Sections · Auto Sect · U-Turn · U-Turn L · U-Turn R · AutoSteer
- **`BottomNavigationPanel.axaml`** — ~12 top-level buttons
  Skip Rows · Color · Reset Tool · HD Sect · Headland · Flags · Snap L
  · Snap R · Snap Pivot · Tram · AB Line · Auto Track

Total: **~26 buttons** in this phase.

## Pre-work decisions (lock these before coding)

### 1. Icon source format

Two options:

| Option | Pros | Cons |
| --- | --- | --- |
| **Reuse existing PNG assets** (`Assets/Icons/*.png`) | Zero asset work; immediate progress | Raster — blurs at scale; can't recolor for state |
| **Hand-rolled vector `PathIcon`** | Crisp at any DPI; recolorable for state; matches the mockup | One-time cost: ~26 `StreamGeometry` definitions |

**Recommendation: vector `PathIcon`.** We're touching every button anyway,
the count is bounded, and the FZ-G1 / iPad Pro target resolutions make
DPI-independence worth the asset-authoring cost. Existing PNGs stay in
place for the sub-menu phase.

### 2. Reusable shape: Style vs UserControl

Two valid Avalonia patterns:

- **Style class only** — keep `<Button>`, give it `Classes="GlyphButton"`,
  put a hand-written `<StackPanel><PathIcon/><TextBlock/></StackPanel>`
  in `Content`.
- **UserControl** — `<controls:GlyphButton Glyph="..." Label="..."
  Command="..."/>` with DPs.

**Recommendation: UserControl.** XAML at the call site is dramatically
shorter (one line vs. five), parameterization is type-safe, and the
ContentTemplate approach for parameterizing both a `Geometry` and a
localized string through a single Style is awkward in Avalonia. We pay
~1 day of plumbing for `GlyphButton` and reuse it 26+ times.

### 3. Localization

Existing `Shared/AgValoniaGPS.Views/Localization/`:
- `Strings.resx` (neutral / English) plus 8 language files (de, fr, es,
  da, no, et, ko, lv, uk)

Strategy:
- Add `Btn.*` keys to `Strings.resx` only in this phase.
- Other language files inherit English fallback automatically through
  `ResourceManager`.
- Translation PRs follow as the existing translators get to them — not
  blocking on this work.

### 4. Tooltip retention

Existing buttons have descriptive `ToolTip.Tip` (e.g. *"YouTurn Auto
U-Turn"*). The label now carries the primary identifier. Keep the
tooltip as an expanded *secondary description* — shorten where the old
text is now redundant with the label. Don't drop them; they help new
operators learn what the button actually does.

## Implementation phases

Each phase leaves the app working; commits should be reviewable in
isolation.

### Phase A — Icon resource library

`Shared/AgValoniaGPS.Views/Icons/Glyphs.axaml`

A `ResourceDictionary` of named `StreamGeometry` resources, one per
glyph. Reference the mockup
(`mockups/glyph_over_word.svg`) for the visual targets. Examples:

```xml
<StreamGeometry x:Key="Glyph.File">
  M5 4 L5 20 L19 20 L19 9 L14 4 Z M14 4 L14 9 L19 9
</StreamGeometry>

<StreamGeometry x:Key="Glyph.AutoSteer">
  M12 1 A11 11 0 1 0 12 23 A11 11 0 1 0 12 1 M12 9 A3 3 0 1 0 12 15 A3 3 0 1 0 12 9
</StreamGeometry>
```

26 entries to author in a single sitting for visual consistency. Paths
are stroke-friendly (rendered with `Path` + `Stroke`, not filled) using
a 24×24 coordinate space. Stroke thickness is set in the `GlyphButton`
control style, not baked into the path.

Register the dictionary in `App.axaml` so resources are app-global.

This phase is authored standalone — geometries can be inspected with a
throwaway `Path` test page before the control exists. Doing this first
means Phase B has real glyphs to render against from the very first
build.

### Phase B — Build `GlyphButton` UserControl

`Shared/AgValoniaGPS.Views/Controls/GlyphButton.axaml(.cs)`

DPs needed:
- `Glyph` (`Geometry`) — the path icon
- `Label` (`string`) — text below
- `Command` (`ICommand`) + `CommandParameter` — forwarded to inner Button
- `IsActive` (`bool`) — toggled-on visual state
- Inherit `ToolTip.Tip` via attached property (works automatically)

Visual:
- Width 80, Height 80 (was 64×64 — accommodates label without
  squashing the icon)
- Vertical `StackPanel`: `Path` 32×32 above (Stroke=Foreground,
  StrokeThickness=2), `TextBlock` 11pt below
- Padding 4 top, 6 bottom, 4 sides
- `:pointerover`, `:pressed`, `IsActive=true` change *background*, not
  glyph color
- Tap target ≥44pt by virtue of 80×80 outer

Unit tests: render light theme, dark theme, Active=true, Active=false,
disabled. All four panels currently render at desktop and iOS — UI
tests via `Avalonia.Headless.NUnit` already cover that pattern.

### Phase C — Localization keys

Add to `Shared/AgValoniaGPS.Views/Localization/Strings.resx`:

```
Btn.Settings       = Settings           (left panel — application settings)
Btn.View           = View
Btn.Tools          = Tools
Btn.Vehicle        = Vehicle
Btn.Fields         = Fields
Btn.FieldTools     = Field Tools
Btn.SteerConfig    = Steer Config       (left panel — opens AutoSteer config)
Btn.Contour        = Contour
Btn.Sections       = Sections
Btn.SectionsAuto   = Auto Sect
Btn.UTurn          = U-Turn
Btn.UTurnLeft      = U-Turn L
Btn.UTurnRight     = U-Turn R
Btn.AutoSteer      = AutoSteer          (right panel — runtime engage toggle)
Btn.SkipRows       = Skip Rows
Btn.SectionColor   = Color
Btn.ResetTool      = Reset Tool
Btn.HeadlandSect   = HD Sect
Btn.Headland       = Headland
Btn.Flags          = Flags
Btn.SnapLeft       = Snap L
Btn.SnapRight      = Snap R
Btn.SnapPivot      = Snap Pivot
Btn.TramLines      = Tram
Btn.ABLine         = AB Line
Btn.AutoTrack      = Auto Track
```

(Final wording stays subject to dev-chat review — short labels are the
priority since the toolbar is horizontal-bandwidth-bound.)

### Phase D — Migrate `LeftNavigationPanel.axaml`

Per button:

```xml
<!-- before -->
<Button Classes="LeftPanelButton" ToolTip.Tip="File Menu"
        Command="{Binding ToggleFileMenuPanelCommand}">
  <Image Source="avares://AgValoniaGPS.Views/Assets/Icons/fileMenu.png"
         Stretch="Uniform"/>
</Button>

<!-- after -->
<controls:GlyphButton Glyph="{StaticResource Glyph.File}"
                      Label="{loc:Localize Btn.File}"
                      ToolTip.Tip="Open the file menu"
                      Command="{Binding ToggleFileMenuPanelCommand}"/>
```

Update `Style Selector="Button.LeftPanelButton"` → drop or keep as
no-op fallback for any leftover (none expected once migration completes).

Adjust the `StackPanel` spacing — buttons are now 80×80 vs the old
64×64; total panel height grows by ~110px. Likely fine on all targets
but verify against the 1366×768 floor (Getac F110).

### Phase E — Migrate `RightNavigationPanel.axaml`

Same pattern.

### Phase F — Migrate `BottomNavigationPanel.axaml` (top-level only)

Same pattern. **Sub-menu expanders** (`Button x:Name="ABLineMenuButton"`,
`Button x:Name="FlagMenuButton"`) stay icon-only — they get the glyph
treatment in a follow-up phase. Verify the bottom bar still fits the
horizontal floor of 1280 DIPs (it should — 12 × 80 = 960 + spacing
fits comfortably).

### Phase G — Real-device verification

Per `feedback_app_testing.md` and the test-device note:
- macOS desktop (Mac mini M4): full smoke test
- iPad Pro 2nd gen (physical device, `-r ios-arm64`): visual + tap
  target check
- Android tablet: visual + tap target check
- Resize window to 1366×768 on desktop to simulate Getac F110 — does
  the Bottom panel still fit? Does the Left panel scroll if needed?

Wait for the user to report back after each device pass before moving
on (per `feedback_app_testing.md`).

### Phase H — Cleanup

- Remove the now-unused `LeftPanelButton`/`RightPanelButton`/
  `BottomPanelButton` style entries (or leave for the sub-menu phase if
  they're still referenced inside expanders).
- Audit `Assets/Icons/` for the seven PNG files that are now orphaned
  (fileMenu.png, NavigationSettings.png, etc.). Delete only the ones
  with zero remaining references — some are used by sub-menus that
  haven't been migrated yet.

## Risks and mitigations

- **Hand-rolled glyphs may look inconsistent** if multiple authors
  touch them. Mitigation: one person drafts all 26 in a single sitting
  using the same stroke conventions, others review for consistency.
- **Bottom bar overflow at 1280 DIPs.** Mitigation: prototype Phase F
  early and test against the floor; if 12 × 80 doesn't fit, drop the
  outer padding or compress label sizes by 1pt rather than truncate.
- **Localization regressions** — translators have to re-translate
  shorter labels; some current `ToolTip.Tip` text is itself localized
  (verify before changing). Mitigation: keep tooltips in English in
  this phase; flag for translation in a separate PR.
- **Test churn** — UI tests via `Avalonia.Headless.NUnit` may reference
  buttons by `Classes="LeftPanelButton"` selectors. Mitigation: grep
  before refactor; update selectors in lockstep.

## Acceptance criteria

- [ ] All 26 top-level buttons render with glyph + label.
- [ ] Light + dark theme both look right.
- [ ] iPad Pro (physical), Android tablet, Mac desktop all show buttons
      at correct size.
- [ ] Window at 1366×768 fits all bars without truncation.
- [ ] Tooltips still display useful expanded help text.
- [ ] No `Classes="LeftPanelButton"` (etc.) usages remain except inside
      sub-menus.
- [ ] No regression in existing UI tests.

## Out of scope (revisit in Phase 2)

- Sub-menu buttons inside expanders (AB Line nudge panel, Flag list,
  etc.).
- Status indicators in top-left / top-right bars.
- Map overlays (compass, camera-mode button).
- Dialog buttons.
- Compact-mode toggle (icon-only fallback) — wait for in-cab feedback
  before implementing.
- UI-wide font scale control — separate plan.
