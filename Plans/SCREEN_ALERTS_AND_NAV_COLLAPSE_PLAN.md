# Screen & Alerts + Navigation Collapse — design plan

**Status:** implemented — all sections done, Desktop builds, full test suite
green (109 + 221 + 146 + 911). Pending final device verification (iPad/Android)
before the single PR. Also added (from testing): zoom-level persistence and
edge-to-edge layout (removed the 8px frame insets + the mobile 5px map border;
nav buttons flush to their outer edge with the inner gap preserved).
**Branch:** `feature/ui-collapse-screen-alerts`
**Delivery:** ONE complete change, one PR at the end. No incremental
shipping of partial pieces — build order below is internal dev
convenience only.

## Motivation

From Brian's UI review:

1. *"AgV needs to hide the windows when you make a selection. Too many
   windows is not the best look."*
2. *"It shouldn't take 2 pushes to get to settings, and when there after
   the second push, the first push is still there. Make a selection and
   collapse behind you."*
3. *"Other decision is what should be modal and what should be
   non-modal."*

Today the app is "map + overlay everything." Nav fly-out menus are
independent booleans that stack and never collapse; selecting an item in
a fly-out opens a dialog but leaves the fly-out visible behind it (only
`ShowHotkeyConfigDialogCommand` bothers to close its parent). Application
settings are split awkwardly: a generic **App Settings** dialog (two tabs:
*Display* + *Additional Options*) plus a small **View Settings** fly-out,
with Grid and Day/Night duplicated across both.

## Strategic decision: map-centric "soft separation", NOT Pages-v2

We evaluated folding this into `feature/pages-v2` (a full destination
model: a Home launcher of full-screen task pages, with the map demoted to
one destination). **Rejected** — pages are "too different for the user
base," and demoting the map conflicts with AgValonia's non-negotiable
map-centric stance.

Instead we take the **roominess** of pages without the **navigation
paradigm**:

- The **map stays the home/shell** you always return to.
- Settings surfaces like **Screen & Alerts** are **near-fullscreen
  overlays** that take over the screen while open and **collapse back to
  the map** — they are not destinations launched from a Home page.
- At ~18 controls, Screen & Alerts effectively covers the map, so while
  open it behaves modally (you open it stopped, configure, return to a
  clean map). That's fine: you don't change perspective/theme/grid while
  bumping along a field, and the map real estate is too valuable to spend
  on permanent quick-toggle buttons.

## Modal vs non-modal — governing rule (Brian #3)

> **Non-modal / over the live map** = used while guiding, keeps the map
> visible (simulator bar, section control, charts).
> **Modal / takes over** = set up while stopped (Screen & Alerts, App
> Settings, vehicle/tool config, field create/select/import, NTRIP),
> plus confirmations/errors.

Screen & Alerts is a near-fullscreen "takes over" surface that collapses
back to the persistent map — the map-centric middle ground.

## Mobile-first constraint

- **No scrolling** on dense config. Screen & Alerts uses a fixed-fit
  layout with **section sub-nav** sized to fit the design target.
- **Design target = 1200×720** (Android tablet landscape, "Droid S7"
  form factor). Restored as a DEBUG-only viewport lock on Desktop so
  layout work matches the constrained device.

---

## Scope (all delivered together)

### 1. Design-target window cap — DONE on branch
`Platforms/AgValoniaGPS.Desktop/Views/MainWindow.axaml.cs`
- `#if DEBUG` viewport lock in the ctor: `MinWidth=MaxWidth=1200`,
  `MinHeight=MaxHeight=720`, `CanResize=false`, centered.
- `#if !DEBUG` guard around the saved-size/state restore in
  `LoadWindowSettings()` so the lock is uncontested.
- Restores what commit `6c28057` removed; matches `pages-v2`.

### 2. Menu / window behavior
- **Mutual exclusion:** add `CloseAllMenuPanels()` on `MainViewModel`;
  each `Toggle*PanelCommand` in
  `MainViewModel.Commands.Navigation.cs` closes the others before
  opening itself. Affected: View Settings (→ Screen & Alerts), File,
  Tools, Configuration, Field Operations, Field Tools.
- **Collapse-behind-you:** subscribe to `State.UI.DialogChanged` in the
  `MainViewModel` ctor; when `Current != DialogType.None`, call
  `CloseAllMenuPanels()`. One hook covers every dialog-launching command.
- Delete the now-redundant `IsFileMenuPanelVisible = false` in
  `MainViewModel.Commands.Hotkeys.cs`.

### 2a. Idle auto-close (Brian: AgOpen closes dialogs after 7s)
- `ConfigStore.Display.DialogAutoCloseSeconds` (default **7**, 0 = disabled),
  persisted via AppSettings like everything else. No UI yet — wired so a
  toggle can be added on demand.
- `MainViewModel` runs a single `DispatcherTimer`: started when a dialog or
  nav fly-out opens (DialogChanged hook + the toggles), reset on each
  **interaction with that surface**, closes it on timeout (returns to map).
- Interaction reset: tunnel PointerPressed/KeyDown/Wheel handlers on the
  shared `DialogOverlayHost` (dialogs) and `LeftNavigationPanel` (fly-outs)
  call `NotifyDialogInteraction()`. Map interaction does NOT reset it.
- **Exempt** (never auto-close): Confirmation, Error, NumericInput,
  NtripProfileEditor (explicit decision / in-progress data entry). Wizards
  and the Busy overlay aren't `DialogType`s so are exempt automatically.

### 2b. Single source of truth for display state (foundational)
**Requirement:** changing a setting takes effect immediately, is saved to
the configuration store, and is restored on next launch — with ONE source
of truth.

**Root cause found:** display state is duplicated across stores.
`DisplaySettingsService` (`_displaySettings`) holds `IsGridOn`, `IsDayMode`,
`CameraPitch`, `Is2DMode`, `IsNorthUp` and is what the **map actually reads**.
But every one of those already exists on **`ConfigStore.Display`**
(`GridVisible`, `IsDayMode`, `CameraPitch`, `Is2DMode`, `IsNorthUp`,
`DisplayResolutionMultiplier`, `CameraZoom`), which is the documented config
SoT and is *already* synced to AppSettings JSON by `ConfigurationService`
(persist + reload). The App Settings *Display* tab writes `ConfigStore.Display`
but the map never observes it — so those toggles are dead until reload.

**Fix (single source of truth = `ConfigStore.Display`):**
- The live map **subscribes to `ConfigStore.Display.PropertyChanged`** and
  pushes relevant changes to `MapControl`. Any writer (Screen & Alerts,
  hotkey, etc.) updates the map immediately — decoupled from "who toggled."
- `MainViewModel` display properties (`IsGridOn`, `IsDayMode`, `CameraPitch`,
  `Is2DMode`, `IsNorthUp`, display-resolution) become thin wrappers that
  read/write `ConfigStore.Display.*` (binding convenience only — no shadow
  state).
- **Delete `DisplaySettingsService` / `IDisplaySettingsService`** + its DI
  registration in all three platforms, the ctor param, and the
  `_displaySettings.LoadSettings()` / `RestoreSettings` shadow-copy block.
- Verify `ConfigurationService` maps all of these both directions
  (AppSettings ↔ ConfigStore.Display) so save-on-close / load-on-open covers
  them.

This is what makes Screen & Alerts "just work": every toggle binds to the
one store, takes effect live, and persists/restores automatically.

### 3. Screen & Alerts surface (core)
Replace the small `ViewSettingsPanel` fly-out with one near-fullscreen,
no-scroll surface with **section sub-nav** (chips/tabs). Sized to fit
1200×720 without scrolling.

Sections and contents:

| Section | Controls (source today) |
|---------|-------------------------|
| **Visual** | Tilt ↑/↓, 2D/3D, North-up, Quality *(from View Settings fly-out)*; Grid, Day/Night + Auto Day/Night (+ day/night hours), Light/Dark theme, Field Texture, Texture Moves, Extra Guides (+count), Svenn Arrow, Smoothing/AA, Headland-distance readout, Section Lines *(stays hidden, #118)* *(from Display tab)* |
| **On-Screen Buttons** | U-Turn button, Lateral button *(from Additional Options)* |
| **Alerts / Sounds** | Auto Steer, U-Turn, Hydraulic, Sections *(from Additional Options)* |
| **Hardware Messages** | AiO pop-up messages toggle *(from Additional Options)* |

- Removes the current Grid/Day-Night duplication (single home now).
- Nearly all bindings/commands already exist on `ConfigurationViewModel`
  / `MainViewModel`; this is primarily view-layer re-presentation + nav
  wiring, no new model computation in the VM (MVVM preserved).
- Rename nav-rail button **"View Settings" → "Screen & Alerts"**; keep
  the current screen icon as a placeholder (graphic artist to refine,
  possibly screen + horn).

### 4. App Settings — shrink to set-once system prefs
Modal App Settings dialog (off the File menu) keeps only: **Units**,
on-screen **Keyboard**, **Start Fullscreen**, **Elevation Log**. The
two-tab Display/Additional structure dissolves.

### 5. Cleanup (part of "complete")
- Retire `ViewSettingsPanel.axaml(.cs)`.
- Retire `DisplayConfigTab` / `AdditionalOptionsConfigTab` as App
  Settings tabs (content moved into Screen & Alerts; what remains in App
  Settings gets a small dedicated layout).
- Remove orphaned `DialogType` entries / commands left unused.
- Add/adjust localization strings for new labels + section headers.

---

## Files in play

- `Platforms/AgValoniaGPS.Desktop/Views/MainWindow.axaml.cs` — cap (done)
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.Commands.Navigation.cs` — mutual exclusion, `CloseAllMenuPanels()`
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` — `DialogChanged` subscription
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.Commands.Hotkeys.cs` — remove one-off close
- `Shared/AgValoniaGPS.Models/State/UIState.cs` — `DialogChanged` already exists; `AppSettings` DialogType stays
- `Shared/AgValoniaGPS.Views/Controls/Panels/ViewSettingsPanel.axaml(.cs)` — replaced by Screen & Alerts
- `Shared/AgValoniaGPS.Views/Controls/Panels/LeftNavigationPanel.axaml` — rename button → Screen & Alerts
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/AppSettingsDialogPanel.axaml` — shrink
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/Configuration/DisplayConfigTab.axaml`, `AdditionalOptionsConfigTab.axaml` — content sources, then retired as tabs
- `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` — App Settings entry label/wording

## Implementation order (dev convenience)
1. Behavior: `CloseAllMenuPanels()` + mutual exclusion + `DialogChanged` hook; drop the Hotkeys one-off.
2. Build the Screen & Alerts surface shell (sub-nav + 4 sections), fit to 1200×720, no scroll.
3. Move all content in from the View Settings fly-out + Display + Additional Options tabs.
4. Shrink App Settings to the four set-once prefs; retire the old tabs/fly-out.
5. Nav-rail rename + localization strings + remove dead types/commands.
6. Build (Desktop Debug at 1200×720), verify the whole flow, hand off for device testing (iPad physical, Android).

## Cross-platform note
Shared views/VMs cover all platforms. Verify the surface fits on iOS
(`MainView.axaml`) and Android as well as Desktop. The window cap is
Desktop-DEBUG-only (mobile is inherently constrained).
