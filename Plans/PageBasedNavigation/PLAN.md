# Page-based navigation — design plan

**Status:** draft, not yet started
**Supersedes (partially):** `Plans/Perspectives/` (parked), and the
"navigation chrome" assumptions baked into Phase-1 of
`Plans/GlyphWordButtons/`. The GlyphButton work survives — it just
mostly applies to the **Moving Map** page now, not the whole app.

## Revisions from the navigation audits (2026-05-25)

Folded in after auditing the current AgValonia nav, the classic WinForms
AgOpenGPS nav, and a side-by-side (see `NAVIGATION_STRUCTURE.md`,
`NAVIGATION_AGOPENGPS.md`, `CLICK_COMPARISON.md` in this folder):

- **U-Turn, Tram, and Machine Module → Implement page** (not Tractor). The
  tool dictates the turn (jack-knife / tank clearance) and the machine
  module controls the implement. Corrects the absorption map *and* the
  current PoC `TractorPage`, which still hosts all three.
- **Avoid scrolling on dense config pages** — chunk Tractor/Implement with
  section sub-nav like AgOpenGPS's `FormConfig`, so the 1-tap-to-page win
  isn't lost to a scroll-hunt. See *In-page navigation* below.
- **Add a global Settings search** — a name→location index, since support
  advice is always "change setting X" with no idea which page X is on. See
  *Settings search* below.
- **Shipping model clarified** — this is all-or-nothing, not phased
  increments (see Migration phases).

Still unresolved (the audits reinforce but can't decide it): **map-as-
destination vs. map-centric** — making Home the launcher demotes the map
from "always primary" to one destination. That's the core strategic bet
everything else hangs on.

## Problem

The app today is "map + overlay everything." A persistent map is the
home view; ~30 modal dialogs and 4 drawer panels float over it. The
mental model an operator actually uses is **task-based** ("I'm
configuring the tractor", "I'm setting up a field", "I'm driving"), not
"which dialog do I open next." The current UI forces them to maintain
a map of dialog-to-task in their heads.

## Concept

Replace map-as-primary with a **destination model**: a Home page acts
as launcher, and discrete task pages own everything related to that
task. The map view is one specific destination — the only one where
driving happens.

**Operational guardrail (free with the physics):** swapping implements
or changing tasks requires physically stopping and climbing out of the
cab. Software navigation events naturally cluster in the
"stopped, configuring" state — so we don't have to design for
"navigated away while autosteer engaged." Leaving the Moving Map page
disengages autosteer as a clean default.

## Pages

### Home (app startup destination)

Pure launcher + minimal dashboard.

- **Launcher tiles:** Operator Profile · Tractor · Implement · Fields &
  Jobs · NTRIP / Networking · Application Settings · AgShare · Log Viewer
- **Dashboard area** (small, below tiles): last field worked, total
  acres covered today / this session, any active alerts
- **Top bar:** GPS status · Module status · Date / Time

Tiles are large tap targets (≥120×120 DIP) since this is the entry
point — operators tap once, not surgically.

### Tractor page

Everything tractor-specific, nothing implement-specific.

- **Tractor profile management** — load / save / delete profiles
- **Tractor configuration** — antenna, vehicle geometry, IMU, GPS
  source. Inherits content from current `VehicleConfigTab` minus
  Tool/Implement panels (those move to Implement page)
- **Steer Configuration** — content of current `AutoSteerConfigPanel`
- **Steer Wizard** launcher (wizard takes over the screen full-frame
  when running)
- **Top bar:** GPS · Module · **speed · heading · roll** · Date / Time
  (the extra sensor data is relevant when validating tractor setup)

> **Out of Vehicle Config and moved elsewhere:** Display Options +
> Additional Options sub-tabs → Application Settings (Home). **U-Turn,
> Tram, and Machine Module → Implement page** — they're implement
> concerns (tool dictates turn radius; machine module controls the
> implement), not tractor ones. NB: the current PoC `TractorPage` still
> hosts these three; they need to move to the Implement page.

### Implement page

Everything tool/implement-specific.

- **Implement profile management** — load / save / delete
- **Implement configuration** — Tool, Tool Hitch, Tool Offset, Tool
  Pivot, Tool Switches, Tool Timing, Tool Type. All the `ToolSubTabs/*`
  content from current Configuration dialog.
- **U-Turn** — turn radius, extension, smoothing, trigger distance. Lives
  here because the **tool dictates the turn**: jack-knife angle and tank/
  clearance limits are implement properties, not tractor ones.
- **Tram lines** — passes, display mode, tram-line config. Tram geometry
  is a function of the implement width.
- **Machine Module** — pin config, relays, raise/lower timing, section
  on/off control. The machine module **controls the implement** (lifting
  it and switching sections), so it belongs with the implement.
- **Top bar:** GPS · Module · Date / Time (no sensor info — implement
  config doesn't need live driving data)

### Fields & Jobs page

All field- and job-related management.

- **Field operations** — open / create / close field, manage jobs,
  boundaries, headlands. Content of current `FieldOperationsPanel`
  minus AgShare (AgShare button stays on Home).
- **Tracks management** — manage AB lines and curves. Absorbs current
  `TracksDialogPanel`, `DrawABDialogPanel`, `QuickABSelectorPanel`.
- **Field creation flows** — absorbs `NewFieldDialogPanel`,
  `FromExistingFieldDialogPanel`, `KmlImportDialogPanel`,
  `IsoXmlImportDialogPanel`, `FieldBuilderDialogPanel`,
  `BoundaryMapDialogPanel`.
- **Recorded paths** — absorbs `RecordedPathDialogPanel`.
- **Top bar:** GPS · Module · Date / Time **·** Field / Job name ·
  field size · work left / done · work rate

### Moving Map page

The driving destination. Current driving experience preserved.

- Map (`DrawingContextMapControl`) + LightBar
- Bottom navigation bar (skip rows, color, reset tool, snap L/R/pivot,
  headland, tram, AB Line flyout, Flags flyout)
- Right navigation bar (contour, sections, sections auto, U-Turn,
  AutoSteer engage)
- Camera-mode + zoom buttons
- **Home button** (top-left or floating) — single tap returns to Home
  page. Pressing it disengages autosteer first.
- **Top bar:** full set — GPS · Module · Date / Time · Field / Job ·
  size · work · rate

The left navigation panel from today disappears here — its buttons all
launched dialogs that now live in their respective pages. Operators
navigate via Home button → task page.

## Cross-cutting

### Top bar

A single shared control with a page-aware "mode" property that decides
which info slots render. Replaces current `StatusBarPanel` and
`FieldStatsPanel` (which would become sub-views of the top bar).

| Slot | Home | Tractor | Implement | F&J | Map |
|---|---|---|---|---|---|
| GPS status | ✓ | ✓ | ✓ | ✓ | ✓ |
| Module status | ✓ | ✓ | ✓ | ✓ | ✓ |
| Date / Time | ✓ | ✓ | ✓ | ✓ | ✓ |
| Speed | | ✓ | | | ✓ |
| Heading | | ✓ | | | ✓ |
| Roll | | ✓ | | | ✓ |
| Field / Job name | | | | ✓ | ✓ |
| Field size | | | | ✓ | ✓ |
| Work left/done | | | | ✓ | ✓ |
| Work rate | | | | ✓ | ✓ |

Active page indicator: page title in the center of the top bar, with a
Home glyph that's only shown when not on Home.

### Bottom bar

Driving-only — present on Moving Map, absent everywhere else.

### Side panels

`LeftNavigationPanel` and `RightNavigationPanel` are Moving-Map-only.

### In-page navigation — avoid scrolling

Distributing settings across task pages buys a 1-tap path *to* the page,
but that win evaporates if the page is a long scroll. On mobile/tablet,
scrolling to hunt for a field is the thing to avoid. Dense config pages
(Tractor, Implement) must **chunk** their content the way AgOpenGPS's
`FormConfig` does — section sub-nav / sticky section headers / a sub-tab
strip — so a setting is "tap page → tap section", not "tap page → scroll
past 40 fields". Target: any single setting reachable without a scroll, or
with one short scroll within its section.

### Settings search (cross-cutting)

A global "find a setting by name" affordance, callable from anywhere (like
the modal utilities). Type/scan a setting name → it routes to the page +
section that owns it and highlights the control.

Motivation: support reality. On the Telegram chat the advice is constantly
"change setting X" — useless if the operator doesn't know *which page* X
lives on. AgOpenGPS's `FormAllSettings` (and today's AgValonia "View All
Settings") provide this flat findability; the page model must not lose it.
A name→location index also keeps support instructions stable as pages get
reorganized.

## Dialog absorption map

Mapping today's ~30 dialogs to the new structure. **Utility dialogs**
(Confirm, Error, Info, NumericInput) stay as modal overlays callable
from any page — they're cross-cutting.

| Current dialog | Destination |
|---|---|
| ConfigurationDialog (VehicleConfigTab) | Tractor page |
| ConfigurationDialog (Tool*SubTab) | Implement page |
| ConfigurationDialog (Display, Additional) | Application Settings (Home) |
| ConfigurationDialog (Sources/Gps,Roll) | Tractor page |
| ConfigurationDialog (Tram, UTurn) | Implement page |
| ConfigurationDialog (MachineModule) | Implement page |
| AutoSteerConfigPanel | Tractor page (Steer Config section) |
| FieldBuilderDialog | F&J page |
| BoundaryMapDialog | F&J page |
| TracksDialog | F&J page |
| DrawABDialog | F&J page |
| QuickABSelectorPanel | F&J page |
| RecordedPathDialog | F&J page |
| FromExistingField | F&J page (under New Field flow) |
| KmlImport | F&J page (under New Field flow) |
| IsoXmlImport | F&J page (under New Field flow) |
| NewFieldDialog | F&J page |
| FieldSelectionDialog | F&J page (default action) |
| LoadVehicleToolDialog | split: Tractor + Implement |
| NtripProfileEditor / NtripProfilesDialog | NTRIP page (Home) |
| AgShareSettingsDialog | AgShare button (Home) |
| HotkeysDialog | Application Settings (Home) |
| AppDirectoriesDialog | Application Settings (Home) |
| BugReportDialog | Application Settings (Home) or its own Home button |
| FlagByLatLonDialog | stays modal on Moving Map (Flags flyout) |
| FlagListDialog | stays modal on Moving Map (Flags flyout) |
| SimCoordsDialog | Tractor page (Simulator section) |
| Sim panel (LeftNav) | Tractor page |
| ImportTracksDialog | F&J page (Tracks section) |
| Confirm/Error/Info/Numeric | utility — modal-anywhere |

## Open design questions

### Operator Profile

**Decision (locked):** placeholder for now — two fields only:

- **Name** — free-text identifier ("Chris", "Hired hand", etc.)
- **Level** — enum `Operator` or `Installer`

`Level` is the future-facing hook: an `Installer` will have access to
deeper configuration (calibration, hardware setup, advanced tractor
geometry) that an `Operator` shouldn't see by default. We don't have
to *gate* features by level in Phase 1 — but the data model carries the
field so we can wire visibility/access rules in a later pass without a
schema migration.

For Phase 1 the Operator Profile page is two text/picker controls and
a save button. Persistence: append to `ConfigurationStore` via
`ConfigurationService`, same path as other persisted settings.

### Home page format

- **Pure launcher** (just buttons): cleaner, faster to navigate, less
  to design.
- **Launcher + dashboard widgets**: shows current field, last job,
  hours today — makes Home feel useful when not actively using the app.

**Recommendation:** launcher + minimal dashboard. ~3 widgets max: last
field worked, total acres today, any active alerts.

### Navigation history

- **No history** — every page-to-page move is direct, no back stack.
  Simpler, no "where am I" surprises.
- **Modal-style history** — Home is always one tap away; sub-flows
  inside a page can have local back stack.

**Recommendation:** no global history. Home button is always available
in the top bar (except on Home itself). Pages with internal sub-views
(e.g. F&J → New Field → KML Import) can keep a local breadcrumb.

### Mobile vs desktop patterns

The same shared code paints on desktop, iPad, and Android tablet.
Page transitions:

- **Desktop:** instant content swap (mouse users expect snap behavior)
- **Mobile/tablet:** could use Avalonia's `TabbedPage` or `DrawerPage`
  with platform-appropriate transitions

**Recommendation:** start with instant swap on all platforms.
Animations can come later if they don't hurt the FPS floor.

## Migration phases

**This ships all-at-once, not incrementally.** The page-based UI is a
complete replacement built as a WIP alongside the current panel UI — there
is no scenario where it ships partially done. The phases below are a
**build order for developer convenience only**; they do not represent
shippable increments, and there's no value in resequencing them to "ship a
fix sooner." The current navigation gaps (e.g. open-field being
hotkey-only) get fixed when the whole thing ships.

### Phase 1 — Page shell + Home + Moving Map redirect

- Build hand-rolled page navigation (a `PageHost` `ContentControl` that
  swaps its content based on a `CurrentPage` property on a new
  `NavigationService`).
- Build Home page with launcher tiles + minimal dashboard.
- Existing app behavior is reachable as the Moving Map page; tapping
  the Map tile on Home enters it.
- Other tiles initially show "Coming soon" placeholders.
- App-launch destination becomes Home.

### Phase 2 — Top bar refactor

- New shared `TopBar` control with page-aware mode.
- Absorbs current `StatusBarPanel` and `FieldStatsPanel`.
- Apply across Desktop, iOS, Android `MainView/MainWindow` shells.

### Phase 3 — Tractor page

- Move `VehicleConfigTab` content (minus tool bits) into `TractorPage`.
- Move `AutoSteerConfigPanel` content into `TractorPage` Steer section.
- Add Tractor profile management UI (load/save selector).
- Wire Steer Wizard launch.

### Phase 4 — Implement page

- Move all `Tool*SubTab` content into `ImplementPage`.
- Add Implement profile management.
- Profile switching reloads tool config.

### Phase 5 — Fields & Jobs page

- Move `FieldOperationsPanel` content (minus AgShare) into
  `FieldsAndJobsPage`.
- Absorb `FieldBuilderDialog`, `BoundaryMapDialog`, `TracksDialog`,
  `DrawABDialog`, `RecordedPathDialog`, `NewFieldDialog`, KML / ISOXML
  / FromExisting import flows.
- AgShare button stays on Home.

### Phase 6 — System pages

- NTRIP / Networking page (Home button).
- Application Settings page (Hotkeys, AppDirectories, Display Options,
  Additional Options, BugReport).
- Log Viewer page (currently a dialog).
- AgShare page (currently `AgShareSettingsDialog`).

### Phase 7 — Moving Map cleanup

- Remove the `LeftNavigationPanel` from the Moving Map page (its
  buttons have all been absorbed into task pages).
- Add Home button to top-left of Moving Map.
- Wire autosteer-disengage on leaving the Moving Map page.

### Phase 8 — Dialog cleanup

- Delete absorbed dialogs (`ConfigurationDialog`, `FieldSelectionDialog`,
  etc.).
- Keep utility dialogs (`Confirm`, `Error`, `Info`, `NumericInput`).

## Risks

- **Big refactor, lot of moving parts.** Every panel-bound `Command`
  on `MainViewModel` needs to map to a page context. Migration must be
  done in slices that keep the app working.
- **Dialog state machine churn.** `UIState.ShowDialog(DialogType.X)` is
  used in dozens of places. As dialogs absorb into pages, those callers
  switch to `NavigationService.Navigate(...)` + page-internal state.
- **Tests** — UI tests reference panels by name (`MainViewModelBuilder`
  builds the current panel layout). Refactor in lockstep.
- **MainView/MainWindow per platform** — `Platforms/.../Views/MainView`
  contains the layout shell on each of Desktop, iOS, Android. The page
  shell change applies to all three.
- **GlyphWordButtons feature branch overlap.** Phase 1 of
  GlyphWordButtons migrated the three nav panels' top-level buttons.
  Most of that work lives on the Moving Map page — survives. The
  LeftNavigationPanel was effectively the "task launcher" and gets
  retired by this plan, but its glyph work isn't wasted — those glyphs
  become the icons on Home tiles.

## Branch strategy

Start a fresh `feature/page-navigation` branch off `develop` **now** —
this work doesn't depend on the GlyphButton migration shipping first.
The two branches will touch some of the same files (the per-platform
`MainView` / `MainWindow` shells; some panel AXAML), but the work areas
are mostly disjoint: GlyphButton replaces button visuals inside
existing panels, this plan retires most of those panels and routes via
pages instead. Merge order is whichever lands first; the second one
resolves trivial conflicts.

## Out of scope (Phase 2+)

- Multi-operator data isolation, audit trails, work credits
- Cloud sync of profiles
- Per-page accent themes
- Animated page transitions
- Voice control / hotkey navigation between pages
- "Recent fields" / "Recent jobs" widgets on Home (could come in a
  later dashboard pass)

## Lessons learned (from the `feature/page-navigation` exploration)

The first attempt landed on `feature/page-navigation` (commits up to
`4c45e7a`). The architecture worked but the visual chrome was a mess.
Below is what to **avoid** on the v2 attempt and what was **worth
keeping**.

### Discipline for v2 — visual

- **No hardcoded chrome colors.** Every `Background`, `Foreground`,
  `BorderBrush` in the page chrome must resolve through a
  `DynamicResource` to a theme brush. Hex values like `#2d2d2d`,
  `#1a1a1a`, `#DD2ECC71` belong inside specific states (engaged
  toggles, AB-creation banners) — never in the surface chrome.
- **Don't override Fluent control templates** until the bare control
  is genuinely insufficient. The first attempt restyled `TabControl`,
  `Button`, and a custom `TabbedPage` UserControl with hand-rolled
  corner radii, colors, and margins before even seeing what Fluent
  gives us. Result: a "high-school first UI" look.
- **Prefer native Avalonia 12 page primitives** over hand-rolled
  `PageHost` + `ContentControl` swapping. `NavigationPage`,
  `TabbedPage`, `ContentPage`, `DrawerPage` are the documented
  navigation building blocks and they bring platform-adaptive defaults
  (mobile-bottom tabs, swipe gestures, page transitions) for free.
- **One design system, not three.** The first attempt had
  TopBar-language (dark strip), Home-tile-language (rounded chrome
  buttons with green press flash), and tab-language (custom tile-
  shaped tabs). Pick one Fluent surface model (probably `Card` +
  standard `Button`) and apply it everywhere.
- **Don't mix map-overlay assumptions with page chrome.** The
  existing nav-panel styles were designed to float over a dark map
  canvas. Don't reuse `FloatingPanel` / `ChromeMedium` brushes as
  page-background surfaces; those resolve to chrome tints that look
  fine over a map and terrible as a full-screen background.

### Discipline for v2 — architecture

- **The `NavigationService` + `PageType` enum + observable
  `CurrentPage` approach worked.** Keep that shape; consider whether
  Avalonia's `NavigationPage` (with its own back-stack + lifecycle
  events) supersedes the hand-rolled router.
- **One ctor parameter, three DI registrations.** Adding a new
  shared service to `MainViewModel` already updates Desktop, iOS, and
  Android `ServiceCollectionExtensions.cs` in lockstep — that pattern
  worked. Don't break it.
- **Existing modal panels have visibility baked in** (`IsPanelVisible`
  / `IsDialogVisible` flags on their VMs). For tab embedding you
  either: (a) extract the inner content into a clean reusable
  UserControl + leave the modal wrapper as a thin shell around it,
  or (b) refactor the panel so the visibility flag lives on a Border
  wrapper rather than the UserControl itself. (a) is the long-term
  right answer per the original plan §dialog cleanup.

### What worked and is worth cherry-picking from the v1 branch

- `Shared/AgValoniaGPS.Models/Navigation/PageType.cs` — enum of pages
- `Shared/AgValoniaGPS.Services/Interfaces/INavigationService.cs`
- `Shared/AgValoniaGPS.Services/Navigation/NavigationService.cs` —
  including the "leaving Moving Map disengages autosteer" guardrail
- `MainViewModel.Commands.Pages.cs` — observable `CurrentPage` +
  `GoHomeCommand` + `NavigateToPageCommand(PageType)`
- DI registrations in the 3 platforms' `ServiceCollectionExtensions`
- `MainViewModelBuilder.cs` test-builder update for the new ctor param

These are the architectural bones; the visual chrome layer
(`PageHost.axaml`, `TopBar.axaml`, `HomePage.axaml`, placeholder
pages, all the `Classes="HomeTile/PageTile/TabLauncher/PageTabs"`
styles, all the hardcoded backgrounds in the platform shells) gets
rebuilt from scratch with the discipline above.
