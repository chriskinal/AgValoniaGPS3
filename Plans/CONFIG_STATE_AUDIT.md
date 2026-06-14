# Config & State Storage Audit + Remediation Design

**Branch:** `audit/config-state` (off `develop`, commit `faf12a8`)
**Date:** 2026-05-26
**Status:** Design for review — no code changes yet. Full remediation (incl. building
the persistent application-state tier) to follow on this branch once approved.

---

## 1. Purpose

We keep hitting the same class of bug: a user-meaningful value isn't sourced from the
central store, so it silently fails to persist or gets clobbered on save. This is not a
series of independent bugs — it is a **foundational architecture gap**. This document
audits every config/state value's lifecycle, catalogs every violation with `file:line`,
identifies the root cause, and specifies a remediation that makes the central stores the
*enforced* single source of truth.

## 2. Target architecture — three central stores, one rule

> **Rule: read from the central store at point of use; never keep a local copy, and never
> write to a private/DTO shadow.**

| # | Tier | Store | Backing | Question it answers |
|---|------|-------|---------|---------------------|
| 1 | **Configuration** (persisted) | `ConfigurationStore` | `appsettings.json` (+ vehicle/tool profile JSON, AutoSteer sidecar) | *How should the app behave?* (user preferences/settings) |
| 2 | **Application state** (persisted) | **(new) `ApplicationStateStore`** | **(new) `appstate.json`** | *Where was the app when it closed?* (restore for continuity) |
| 3 | **Application state** (ephemeral) | `ApplicationState` | none (in-memory) | *What is true right now?* (runtime telemetry/flags) |

**Classification rule used throughout this doc:**
- The user deliberately sets it to control behavior and expects it stable → **tier 1 (config)**.
- It reflects the app's last position/view/selection and is restored for continuity → **tier 2 (persistent state)**.
- It is recomputed every run and needn't survive a restart → **tier 3 (ephemeral)**.

The canonical tier-2 example (per review): **window size & position**. You don't "configure"
your window geometry as a preference — the app restores where it was. Same logic applies to
panel positions, last camera view, last-opened field pointer, and last simulator position.

## 3. Current reality vs. target

| Tier | Exists? | Reality on `develop` |
|------|---------|----------------------|
| 1 Config | ✅ | `ConfigurationStore` + `ConfigurationService` ↔ `appsettings.json`. Works, but mapping is hand-written and asymmetric (see §5). |
| 2 Persistent state | ❌ **MISSING as a store** | Its contents are scattered: window geometry, panel pos, last field pointer, sim last-position, first-run/last-run all crammed into the **`AppSettings` config DTO**; field geometry/coverage in field files. |
| 3 Ephemeral state | ✅ | `ApplicationState` — explicitly "single source of truth for ALL runtime state." Correctly transient. |

**The missing tier-2 store is the structural root.** Because persistent *state* has no home,
it's jammed into the config DTO (`AppSettings`), and the guard test has to special-case it as
"managed elsewhere." Two tiers are conflated; one is absent.

## 4. Root cause of the recurring bug

`Tests/AgValoniaGPS.Services.Tests/ConfigurationServiceMappingTests.cs`
→ `Completeness_AllPersistableProperties_AreMapped` enumerates **`typeof(AppSettings).GetProperties()`**.
It only proves every *AppSettings* field round-trips. It is **structurally blind to store
properties that have no AppSettings backing at all.** So you can add a `DisplayConfig` toggle,
wire it to the UI, and it silently never persists — the guard stays green. That blind spot is
exactly how the recurring failures slip through.

## 5. Violations catalog

### A. Store properties with no persistence home (reset every launch)
The live form of "mapping color never persisted."

**12 `DisplayConfig` toggles mapped in NEITHER direction of `ConfigurationService`** (these
stay tier-1 config — they're behavior preferences):

| Store property | Defined | Written from UI |
|----------------|---------|-----------------|
| `Is2DMode` | DisplayConfig.cs:66 | DisplaySettingsService / ViewSettings / Navigation |
| `IsNorthUp` | DisplayConfig.cs:73 | DisplaySettingsService / ViewSettings / Navigation |
| `PolygonsVisible` | DisplayConfig.cs:153 | ConfigurationViewModel.cs:1581 |
| `SpeedometerVisible` | DisplayConfig.cs:160 | ConfigurationViewModel.cs:1587 |
| `DayStartHour` | DisplayConfig.cs:188 | (no writer wired) |
| `NightStartHour` | DisplayConfig.cs:195 | (no writer wired) |
| `LineSmoothEnabled` | DisplayConfig.cs:257 | ConfigurationViewModel.cs:1659 |
| `DirectionMarkersVisible` | DisplayConfig.cs:264 | ConfigurationViewModel.cs:1665 |
| `SectionLinesVisible` | DisplayConfig.cs:271 | ConfigurationViewModel.cs:1671 |
| `UTurnButtonVisible` | DisplayConfig.cs:279 | ConfigurationViewModel.cs:1700 |
| `LateralButtonVisible` | DisplayConfig.cs:286 | ConfigurationViewModel.cs:1706 |
| `HardwareMessagesEnabled` | DisplayConfig.cs:323 | ConfigurationViewModel.cs:1738 |

**Entire `ConfigStore.Tram` section** — not in `AppSettings`, not in profile JSON. Written from
`MainViewModel.cs:226,233`, `Commands.Track.cs:1036/1060/1070/1080/1102/1110/1122`,
`FieldBuilderDialogPanel.axaml.cs:861`. Lost on restart. *(Tram is tier-1 config — needs a home.)*

### B. DTO-bypass writes the store later clobbers
Pattern: write `settings.X =` + raw `Save()`, then the next `SaveAppSettings()` overwrites `X`
from a stale store value (`ApplyStoreToAppSettings` re-derives `X` from the store).

| Value | Site | Why it clobbers |
|-------|------|-----------------|
| AgShare server / API key / enabled | `MainViewModel.Commands.Boundary.cs:194-197` | store `Connections.AgShare*` never updated; `ApplyStoreToAppSettings:501-503` overwrites from store |
| `SimulatorEnabled` | `MainViewModel.Simulator.cs:150-151` | writes DTO + `State.Simulator.IsEnabled`, never `store.Simulator.Enabled`; `:508` overwrites from store |

### C. Multi-home values kept in sync by hand
- **Simulator start coords** live in **four** places: live `_simulatorService`, `AppSettings`
  DTO, `ConfigStore.Simulator`, and `SimulatorState`. `SetSimulatorCoordinates`
  (`MainViewModel.Simulator.cs:261-294`) writes three of them and is correct **only** because of
  an explicit defensive mirror (comment at `:279-281`). Remove either write and it silently
  diverges. The kickoff's "sim coords clobbered" bug is currently *defended, not absent* — and
  the defense is the exact SoT smell we're eliminating.

### D. Save-path inconsistency
- Desktop **`App.axaml.cs:119` exit uses raw `settingsService.Save()`** (no store→DTO sync),
  relying on `MainWindow.OnClosing` having already run `SaveAppSettings()`
  (`MainWindow.axaml.cs:337,350`). Mobile correctly uses `SaveAppSettings()`
  (iOS `MainView.axaml.cs:77`/`AppDelegate.cs:88`, Android `MainView.axaml.cs:65`/`MainActivity.cs:78`).
  If the closing path is skipped, store edits since the last explicit save are lost on Desktop.

### E. NOT violations (corrects the kickoff's priors)
- **`DisplaySettingsService`** on `develop` is **already a clean delegating facade** over
  `ConfigStore.Display` (`DisplaySettingsService.cs:32`) — no shadow state. Only smell: it emits
  its own change events instead of relying on `DisplayConfig`'s `INotifyPropertyChanged`. The UI
  branch deletes it; this branch should **not** touch it to avoid colliding with that work.
- **`AudioServiceBase.IsEnabled`** (`Audio/AudioServiceBase.cs:23-29`) is a private toggle with
  no persistence, but **nothing ever assigns it** — dead, not divergent. Per-sound gates correctly
  read `ConfigStore.Display.*Sound`.
- **`ApplicationState`** is overwhelmingly correct ephemeral state. Borderline items below.

## 6. Inventory: where every value should live

### 6a. `AppSettings` (today) → target tier

| AppSettings property | Target | Notes |
|----------------------|--------|-------|
| WindowWidth/Height/X/Y, WindowMaximized | **Tier 2** | "where the app was" |
| SimulatorPanelX/Y, SimulatorPanelVisible | **Tier 2** | panel position/visibility |
| CameraZoom, CameraPitch, CameraMode | **Tier 2** | last view (`CameraMode` confirmed tier-2) |
| IsDayMode | **Tier 2** | last resolved/chosen mode (the `AutoDayNight` *toggle* is config) |
| SimulatorLatitude/Longitude/Speed/SteerAngle | **Tier 2** | last sim position |
| CurrentFieldName, LastOpenedField | **Tier 2** | pointer only; field data stays in files (per review) |
| IsFirstRun, LastRunDate, HasMigratedIsMetric | **Tier 2** | app-lifecycle/meta |
| StartFullscreen, KeyboardEnabled | Tier 1 | behavior preferences |
| GridVisible, CompassVisible, SpeedVisible, HeadlandDistanceVisible, SvennArrowVisible, ElevationLogEnabled, FieldTextureVisible/Moveable, ExtraGuidelines(+Count) | Tier 1 | display preferences |
| AutoDayNight, DisplayResolutionMultiplier | Tier 1 | preferences |
| AutoSteerSound, UTurnSound, HydraulicSound, SectionsSound | Tier 1 | preferences |
| IsMetric | Tier 1 | device preference (already source-of-truth here) |
| Ntrip*, AgShare*, GpsUpdateRate, UseRtk, SimulatorEnabled | Tier 1 | connection/mode config |
| Language | Tier 1 | preference |
| FieldsDirectory | Tier 1 | storage-location preference |
| LastUsedVehicleProfile, LastUsedToolProfile | Tier 1 | active-config pointer (stays with config) |
| HotkeyBindings | Tier 1 | preference |

⚠ = ambiguous, needs sign-off during review.

### 6b. `DisplayConfig` not-yet-mapped → target tier
Of the 12 toggles in §5A, **`Is2DMode` and `IsNorthUp` are tier-2** (view orientation = last view);
the remaining 10 stay **Tier 1** (display preferences).

### 6c. `ApplicationState` sub-states → tier
| Sub-state | Verdict |
|-----------|---------|
| Vehicle, Guidance, Connection, RecordedPath, UI, Section | ✅ Tier 3 (ephemeral) — correct |
| Field | Tier 3, but pointer (which field) → Tier 2; geometry stays in files |
| Simulator (lat/lon/speed/steer/enabled) | Currently mirrors a separate `ConfigStore.Simulator`+`AppSettings` pair → reroute: persistable bits to **Tier 2**, this object stays the ephemeral live mirror |
| `YouTurnState.IsEnabled` | **Tier 3** (ephemeral) — stays as-is |
| `BoundaryRecState.IsDrawRightSide/IsDrawAtPivot/BoundaryOffset` | **Tier 2** — last-used recording setup, restored for continuity |

## 7. Remediation plan

Ships atomically (build order is dev convenience only, per project convention). DI/persistence
changes touch **Desktop + iOS + Android** in the same commit.

### Step 1 — Build the persistent application-state tier (store #2)
- New model `ApplicationStateSnapshot` (POCO/DTO of tier-2 values) in `AgValoniaGPS.Models`.
- New `IApplicationStateService` + `ApplicationStateService` in `AgValoniaGPS.Services`, persisting
  `appstate.json` in `Documents/AgValoniaGPS/` via the existing `AtomicJsonFile` (same `.bak`
  crash-safe recovery, camelCase, `Populate` creation-handling as `SettingsService`).
- Load on startup (alongside `LoadAppSettings`); save on the same hooks that already call
  `SaveAppSettings` (Desktop `MainWindow.OnClosing`, iOS/Android lifecycle).
- The *runtime* mirror stays `ApplicationState` (tier 3); the service hydrates tier-3 from the
  snapshot on load and projects tier-3 → snapshot on save — symmetric, like `ConfigurationService`.
- Register in all three `ServiceCollectionExtensions.cs`.

### Step 2 — Migrate tier-2 values out of `AppSettings`
- Move the §6a Tier-2 rows into `ApplicationStateSnapshot`; remove them from `AppSettings` and from
  `ApplyAppSettingsToStore`/`ApplyStoreToAppSettings`.
- **One-way migration** (per File Format Philosophy): on first load where `appstate.json` is absent
  but a legacy `appsettings.json` still carries these fields, seed the snapshot from them, then stop
  writing them to `appsettings.json`. No data loss for existing users.
- Field pointer (`CurrentFieldName`/`LastOpenedField`) → snapshot; field data remains in files.

### Step 3 — Give config orphans a home
- Add the **10 tier-1** `DisplayConfig` toggles (all of §5A except `Is2DMode`/`IsNorthUp`) to
  `AppSettings` + both `Apply` methods. `Is2DMode`/`IsNorthUp` go to the tier-2 snapshot (Step 2).
- **`ConfigStore.Tram` is field-scoped** → persist it in the field/job files (not `AppSettings`).
  Tram lanes belong to a field, so they save and restore with the field, alongside boundary/track
  data — not as a global setting.

### Step 4 — Eliminate DTO-bypass + multi-home writes
- AgShare confirm (`Boundary.cs:194-197`): write `ConfigStore.Connections.AgShare*`, then
  `SaveAppSettings()`. Remove the raw DTO write + `Save()`.
- `SimulatorEnabled` (`Simulator.cs:150-151`): write `ConfigStore.Simulator.Enabled`, then save.
- Sim coords (`Simulator.cs:261-294`): single path — dialog → tier-2 snapshot (or
  `ConfigStore.Simulator` if classified config) → save. All readers read the central store; drop the
  manual mirror and the redundant DTO write.

### Step 5 — Save-path parity
- Desktop `App.axaml.cs:119`: replace raw `settingsService.Save()` with the proper
  `SaveAppSettings()` + new `SaveState()`.

### Step 6 — Make the contract enforceable (the real fix)
Replace the AppSettings-only completeness test with **bidirectional, store-driven guards**:
1. **Config completeness:** enumerate every public property of every `ConfigurationStore`
   sub-config; assert each is either (a) round-tripped through `Apply*`, (b) on an explicit
   `PersistedViaProfileJson` allowlist, or (c) on an explicit `IntentionallyTransient` allowlist.
   A new store property with no home ⇒ **red**.
2. **State completeness:** same shape for `ApplicationStateSnapshot` ↔ tier-2 fields.
3. **No-bypass guard:** a test/analyzer check that production code outside `ConfigurationService`/
   `ApplicationStateService` does not assign `*.Settings.<X>` for store-backed properties
   (grep-based CI check acceptable, mirroring existing CI greps).

## 8. Risks & mitigations
- **Existing user data** in `appsettings.json` (window pos, last field, sim pos): handled by the
  one-way migration in Step 2 — read once, seed `appstate.json`, then drop from `appsettings.json`.
- **UI branch rebase:** `feature/ui-collapse-screen-alerts` (paused at `9e3987e`) will rebase onto
  this. It already deletes `DisplaySettingsService` and moves display state to `ConfigStore.Display`;
  this branch leaves that file alone (see §5E) to minimize conflict. Coordinate the 12-toggle
  additions so they don't double-apply.
- **Three-platform DI drift:** every new registration lands in Desktop + iOS + Android together.

## 9. Resolved decisions (signed off 2026-05-26)
1. **CameraMode** → **Tier 2** (last view).
2. **IsDayMode / Is2DMode / IsNorthUp** → **Tier 2** (last view).
3. **Tram** → **field-scoped** — persist in field/job files, not AppSettings.
4. **YouTurnState.IsEnabled** → **ephemeral** (tier 3, unchanged). **BoundaryRec draw prefs** → **Tier 2**.
5. **`appstate.json`** → **separate file** (own `AtomicJsonFile`, mirrors `appsettings.json`).

## 10. Deliverables checklist — COMPLETE (2026-05-27)
Implemented as named below (the snapshot/service ended up as `PersistentAppState` +
`PersistentStateService`, serializing the store object directly rather than a separate DTO).
- [x] `PersistentAppState` store + `IPersistentStateService`/`PersistentStateService`
- [x] `appstate.json` via `AtomicJsonFile`; load/save wired on all 3 platforms
- [x] One-way migration from legacy `appsettings.json`
- [x] Tier-2 values removed from `AppSettings`/`DisplayConfig`/`SimulatorConfig` + `Apply*`
- [x] 10 tier-1 Display toggles given AppSettings homes; **Tram persisted per-field**
      (`TramConfigFileService` → `TramConfig.json`, loaded on field-open / saved on field-close,
      alongside the pre-existing per-field `TramSystems.json`)
- [x] AgShare / SimulatorEnabled / sim-coords routed through the central store; `Language`
      uses `SaveAppSettings`
- [x] Desktop exit → `SaveAppSettings()` + state `Save()`
- [x] Bidirectional config (`DisplayConfig`/`SimulatorConfig`) + state completeness guards;
      no-bypass guard (`NoBypassWritesTests` scans VM/Views for `*.Settings.<X> =`)
- [x] All tests green; new tier-2 round-trip + legacy-migration tests

**Note on Tram:** the audit assumed all of `ConfigStore.Tram` was unpersisted. In fact tram
*Systems* already persisted per-field, and `Passes`/`DisplayMode` mirror `ConfigStore.Guidance`
(vehicle-profile config). Only the remaining scalars (`TramWidth`, `StartPass`, `IsOuterInverted`,
`Alpha`, `IsDisplayTramControl`, `IsEnabled`) lacked a home; those now persist per-field.
`Passes`/`DisplayMode` were intentionally left on the Guidance sync to avoid double-sourcing —
a smaller follow-up could consolidate them if desired.

---

## 11. Follow-up scope — de-ambient the VM (added 2026-06-14, not scheduled)

**Different axis from §1–§10.** Everything above is about *data single-source-of-truth* — which
of the three stores owns a value. This item is about *ambient framework coupling*: the ViewModel
reaching into framework/global statics **out of the air** instead of receiving them through its
constructor. It's the same "not injected, just grabbed" smell, one layer down, and it's exactly
what `Plans/ARCHITECTURE.md` flags under "Tight Coupling Points → Historical, needs refactor" —
which the completed storage audit deliberately did **not** touch.

### 11.1 Injectable `IDispatcher` (the concrete item)
The VM depends directly on `Avalonia.Threading.Dispatcher.UIThread` — a static from the
*presentation* framework — in **~10 `MainViewModel` partials**: ~18 `Post`, 5 `InvokeAsync`,
7 `CheckAccess`. By the project's own MVVM rule (VM coordinates, doesn't bind itself to the View
framework), this should be an injected `IDispatcher`:
- Define `IDispatcher` (Post / InvokeAsync / CheckAccess) in `AgValoniaGPS.Services` (or Models).
- Avalonia-backed implementation registered in **Desktop + iOS + Android** (three-platform DI rule).
- VM partials take `IDispatcher` via ctor; replace every `Dispatcher.UIThread.*` call.
- Behavior-preserving for the current apps; independently shippable; no data-tier change.

**Payoff:** (a) VM unit tests stop needing `Avalonia.Headless` *just to supply a dispatcher* — an
inline/synchronous `IDispatcher` suffices; (b) it unblocks the headless web-UI host, which has a
VM consumer with no UI thread (see `Plans/REMOTE_WEB_UI_SPLIT.md` §9.1 + the
`Spikes/HeadlessHostSpike` proof — the spike had to pull in `Avalonia.Headless` purely for this).

**Why it wasn't already done (and wasn't wrong to defer):** until the headless host, every VM
consumer was an Avalonia View on a real UI thread, so `Dispatcher.UIThread` was correct everywhere
and an abstraction would have had exactly one implementation. Defensible YAGNI; the web UI is the
forcing function that makes it earn its keep.

### 11.2 Sibling: static store access (optional, same pass)
The same "ambient, not injected" smell applies to `ConfigurationStore.Instance` and
`ApplicationState.Instance`, read statically across services and VMs (also flagged in
`ARCHITECTURE.md`). The storage audit (§1–§10) made these the enforced SoT but left them as
**statics**. De-static-ing them (inject the store instances) is the natural companion to §11.1 and
would complete the "de-ambient the VM" pass — but it's larger blast-radius and can be sequenced
separately. Keep §11.1 as the committed first step.

**Status:** scoped, not scheduled. Surfaced by the remote/web-UI Phase 0 spike.
