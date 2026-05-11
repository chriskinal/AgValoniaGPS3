# Page-based navigation — design plan

**Status:** draft, not yet started
**Supersedes (partially):** `Plans/Perspectives/` (parked), and the
"navigation chrome" assumptions baked into Phase-1 of
`Plans/GlyphWordButtons/`. The GlyphButton work survives — it just
mostly applies to the **Moving Map** page now, not the whole app.

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
> Additional Options sub-tabs → Application Settings (Home).

### Implement page

Everything tool/implement-specific.

- **Implement profile management** — load / save / delete
- **Implement configuration** — Tool, Tool Hitch, Tool Offset, Tool
  Pivot, Tool Switches, Tool Timing, Tool Type. All the `ToolSubTabs/*`
  content from current Configuration dialog.
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
| ConfigurationDialog (MachineModule) | Tractor page |
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

Each phase leaves the app shippable. The current panel-based UI keeps
working while we build the page shell next to it.

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
