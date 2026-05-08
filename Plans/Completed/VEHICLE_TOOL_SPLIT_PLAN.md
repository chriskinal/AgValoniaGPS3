# Vehicle / Tool Profile Split Plan

**Issue:** #346
**Branch:** `feature/vehicle-tool-split`
**Status:** Draft

## Goal

Mirror AgOpenGPS 6.8.2's split of the merged "Vehicle" profile into two
independently-selectable profiles — **Vehicle** and **Tool** — so operators can
mix-and-match (e.g. one tractor with multiple implements, or one implement
moved between tractors) without retyping configs. Operator muscle memory is
already aligned with this model in AgOpen, so AgValonia should match it
rather than diverge.

## Non-goals

- Cloud sync / multi-device sync.
- Splitting **Guidance**, **AutoSteer**, or **YouTurn** out of the merged
  profile. The AgOpen split keeps these tied to the vehicle, and we follow
  suit.
- AgOpen XML export. Legacy XML import stays one-way.
- Per-tool section layout vs per-vehicle section layout (sections move with
  the tool, same as AgOpen — see §3).

## 1. Current state

- `Vehicles/<name>.json` holds **everything**: vehicle, guidance, tool,
  sections, you-turn, general. Schema lives in
  `Shared/AgValoniaGPS.Services/Profile/ProfileJsonService.cs` (`ProfileDto`).
- `ConfigurationStore` already keeps `Vehicle`, `Tool`, `Guidance`, etc.
  as separate sub-stores in memory. The split exists in RAM but not on disk.
- `AppSettings.LastUsedVehicleProfile` is a single string. There is no
  `LastUsedToolProfile`.
- `IVehicleProfileService` exposes `Load`, `Save`, `CreateDefaultProfile`,
  `GetAvailableProfiles`. There is **no** `IToolProfileService`.
- The `<name>.AutoSteer.json` sidecar mentioned in CLAUDE.md does not
  actually exist on disk — the filter in `GetAvailableProfiles` is dead
  code. No need to migrate it; just remove the filter.
- `MainViewModel.Commands.Configuration.cs` and `Commands.Settings.cs` reference
  vehicle profile commands; tool-side commands don't exist yet.

## 2. Target state

```
~/Documents/AgValoniaGPS/
├── Vehicles/
│   ├── TractorA.json       # Vehicle + Guidance + YouTurn fields
│   ├── TractorA.xml        # legacy AOG import (still readable)
│   └── Default.json
└── Tools/
    ├── PlowWide.json       # Tool + Sections fields
    ├── PlowNarrow.json
    └── Default.json
```

- `AppSettings`: `LastUsedVehicleProfile` (existing) + `LastUsedToolProfile`
  (new).
- Two services: `IVehicleProfileService`, `IToolProfileService`. Both share
  a small base utility for case-insensitive file resolution and atomic rename.
- One unified picker dialog (two side-by-side panels, AgOpen-style).
- `ConfigurationService.LoadProfile(...)` becomes `LoadProfiles(vehicle, tool)`,
  loading both into the singleton `ConfigurationStore`.

## 3. File format split

**Decision: sections live with the Tool.** Section count and positions are
implement geometry, not tractor geometry. A 3-section sprayer is a 3-section
sprayer regardless of which tractor pulls it. AgOpen places sections in
`ToolSettings`; we match.

### Vehicle profile (`Vehicles/<name>.json`)

| Field group | Keys |
|---|---|
| Vehicle | AntennaHeight, AntennaPivot, AntennaOffset, Wheelbase, TrackWidth, Type, MaxSteerAngle, MaxAngularVelocity |
| Guidance | IsPurePursuit, GoalPointLookAheadHold, GoalPointLookAheadMult, GoalPointAcquireFactor, StanleyDistanceErrorGain, StanleyHeadingErrorGain, StanleyIntegralGainAB, PurePursuitIntegralGain, UTurnCompensation |
| YouTurn | TurnRadius, ExtensionLength, DistanceFromBoundary, SkipWidth, Style, Smoothing |
| General | IsMetric, IsSimulatorOn, SimLatitude, SimLongitude |

### Tool profile (`Tools/<name>.json`)

| Field group | Keys |
|---|---|
| Tool | Width, Overlap, Offset, HitchLength, TrailingHitchLength, TankTrailingHitchLength, TrailingToolToPivotLength, IsToolTrailing, IsToolTBT, IsToolRearFixed, IsToolFrontFixed, MinCoverage, IsMultiColoredSections, IsSectionsNotZones, IsSectionOffWhenOut, IsHeadlandSectionControl, LookAheadOn, LookAheadOff, TurnOffDelay |
| Sections | Count, Positions[] |

Both files carry `FormatVersion: 2`. The `General` block stays in the vehicle
profile because `IsMetric` is operator preference, not implement geometry.

### DTOs

Split `ProfileDto` into two new DTOs:
- `VehicleProfileDto` — Vehicle + Guidance + YouTurn + General
- `ToolProfileDto` — Tool + Sections

Move `ProfileJsonService` aside as the legacy v1 reader (rename to
`ProfileJsonServiceV1`); add `VehicleProfileJsonService` and
`ToolProfileJsonService` for v2.

## 4. Service architecture

```csharp
// New
public interface IToolProfileService {
    string ToolsDirectory { get; }
    List<string> GetAvailableProfiles();
    bool Load(string profileName, ConfigurationStore store);
    void Save(string profileName, ConfigurationStore store);
    void CreateDefaultProfile(string profileName, ConfigurationStore store);
    bool Rename(string oldName, string newName);
    bool Delete(string profileName);
}

// Changed: same shape, plus Rename / Delete
public interface IVehicleProfileService {
    // existing members ...
    bool Rename(string oldName, string newName);
    bool Delete(string profileName);
}
```

Rename and Delete return `bool` and **never throw on the rule violations**
(active profile, name collision); UI renders the error. They throw only on
unexpected I/O failure.

`ConfigurationService`:
- Inject both services.
- Replace `LoadProfile(name)` with `LoadProfiles(vehicleName, toolName)`.
- On `SaveSettings(...)`, persist both `LastUsedVehicleProfile` and
  `LastUsedToolProfile` from `Store.ActiveVehicleProfileName` /
  `Store.ActiveToolProfileName`.
- On startup, load whichever profiles exist; if `Tools/` is empty (fresh
  install or pre-split user), trigger migration (§5).

Add `Store.ActiveToolProfileName` and `Store.ActiveToolProfilePath` alongside
the existing `ActiveProfileName`/`ActiveProfilePath` (rename those to
`ActiveVehicleProfile*` for clarity — purely a refactor inside the store).

## 5. Migration

**Trigger:** on app startup, if `Tools/` directory does not exist or is empty
**and** `Vehicles/` contains any v1 JSON profiles.

**Action:** for each `Vehicles/<name>.json` whose `FormatVersion` is 1 (or
absent):
1. Read into a `ProfileDto` using the legacy reader.
2. Write `Vehicles/<name>.json` (overwrite) with v2 vehicle-only schema.
3. Write `Tools/<name>.json` with v2 tool-only schema, copying
   `Tool` + `Sections` blocks. Same name as the vehicle so the operator's
   first experience is "my tractor and tool both still load."
4. If `LastUsedVehicleProfile` is set, also set
   `LastUsedToolProfile = LastUsedVehicleProfile` so the active pairing is
   preserved.
5. Log each migrated file via `ILogger`.

Legacy AgOpen XML import (`*.XML`) keeps using the existing parser but writes
v2 split files on first save. Same name pairing rule.

**No phased migration.** A user opening the new build gets all profiles
migrated on first launch. Old v1 files are overwritten in place — they're
not preserved, since the v2 vehicle file is a strict superset of the v1
"vehicle/guidance/youturn/general" subset.

## 6. UI: unified picker dialog

**Dialog:** `LoadVehicleToolDialogPanel.axaml` in
`Shared/AgValoniaGPS.Views/Controls/Dialogs/`.

**Registered in:** `DialogOverlayHost.axaml`.

**DialogType:** `LoadVehicleTool`.

**Layout** (mirrors AgOpen `FormLoadVehicleTool`):

```
┌────────────────────────────┬────────────────────────────┐
│ Current Vehicle: TractorA  │ Current Tool: PlowWide     │
│ Selected: -                │ Selected: -                │
│ ┌──────────────────────┐   │ ┌──────────────────────┐   │
│ │ Default              │   │ │ Default              │   │
│ │ TractorA  (orange)   │   │ │ PlowNarrow           │   │
│ │ TractorB             │   │ │ PlowWide  (orange)   │   │
│ └──────────────────────┘   │ └──────────────────────┘   │
│ Type: Tractor              │ Width: 6.00 m              │
│ Wheelbase: 2.50 m          │ Overlap: 0.00 m            │
│ Antenna Pivot: 0.00 m      │ Offset: 0.00 m             │
│ Track Width: 1.80 m        │ Sections: 5                │
│                            │ Attach: Rear Fixed         │
│ [New] [Delete] [Rename]    │ [New] [Delete] [Rename]    │
│ [Reset to Default]         │ [Reset to Default]         │
├────────────────────────────┴────────────────────────────┤
│ [Convert Old]              [Cancel]    [Load]           │
└─────────────────────────────────────────────────────────┘
```

### Behavior rules (copied from AgOpen, condensed)

- Active profile row → orange background + bold; can't be deleted.
- Selected (non-active) row → green background.
- **Load** disabled until vehicle or tool selection differs from active.
- **New** prompts for a name and saves a copy of the *currently loaded*
  store state — same pattern as AgOpen "New" (= Save As). No separate
  Save As button.
- **Rename** prompts for a new name. Allow case-only renames. Reject
  collision with a different existing file. If renaming the active profile,
  update `AppSettings.LastUsedVehicleProfile` / `LastUsedToolProfile`.
- **Delete** uses `ShowConfirmationDialog(...)` callback pattern. Blocks
  while a field is open.
- **Reset to Default** re-creates a `Default` profile with built-in defaults
  and loads it. Confirms before overwriting an existing `Default`.
- **Convert Old** opens a sub-dialog that scans for legacy AOG `*.XML` and
  converts them. Optional in the first iteration; can be added if there's
  demand.
- On Load: save current vehicle/tool settings before switching (don't lose
  edits the user made on the config screen but didn't explicitly save).
- After Load: full re-init via existing `ConfigurationStore` notification
  pipeline; no need to recreate VM.

### ViewModel

`LoadVehicleToolDialogViewModel` in `Shared/AgValoniaGPS.ViewModels/`:
- `ObservableCollection<string> Vehicles`, `Tools`
- `string? SelectedVehicle`, `SelectedTool`
- `string CurrentVehicle`, `CurrentTool` (read from store)
- Preview properties for both panels (computed from selection)
- Commands: `New{Vehicle,Tool}`, `Delete{V,T}`, `Rename{V,T}`,
  `Reset{V,T}`, `Load`, `Cancel`, `ConvertOld`

Wire it through DI in `ServiceCollectionExtensions`.

### Launch points

- `MainViewModel.Commands.Configuration.cs`: replace
  `OpenVehicleProfilesCommand` with `OpenVehicleToolPickerCommand` (rename
  bound XAML).
- Optional: prompt the picker on first launch when no profile is set.

## 7. Tests

Add to `Tests/AgValoniaGPS.Services.Tests/`:
- `VehicleProfileServiceTests`: round-trip v2 save/load, rename
  (case-only included), delete-active-blocked, delete-non-active-allowed,
  rename-collision-blocked.
- `ToolProfileServiceTests`: same matrix.
- `ProfileMigrationTests`: v1 file becomes a paired v2 vehicle + tool,
  same name, fields preserved, `LastUsedToolProfile` initialized.

Add to `Tests/AgValoniaGPS.UI.Tests/`:
- `LoadVehicleToolDialogTests`: dialog visibility, Load disabled until
  selection differs, delete-active disabled, rename round-trip, picker
  closes on successful Load. Use `MainViewModelBuilder` for VM
  construction.

## 8. Acceptance criteria

1. Fresh install creates `Vehicles/Default.json` and `Tools/Default.json`
   on first save.
2. Existing v1 user upgrading: on first launch, every v1 profile becomes a
   paired v2 vehicle + tool with the same name; no data lost.
3. Picker dialog opens from the vehicle config screen; both lists populate
   from disk; current pair shown in orange.
4. Load swaps the active pair; UI re-renders; `AppSettings` records both
   `LastUsed*Profile` strings.
5. Rename of an active profile preserves the active pointer (the store
   does not "lose" the active profile).
6. Delete of the active profile is blocked with a clear message.
7. Tests above pass.
8. No breaking change to AOG XML import: a single `<name>.XML` still
   imports cleanly and ends up as a paired vehicle+tool in v2.

## 9. Sequencing on the feature branch

Single feature branch, single PR target. Commits in this order so the branch
is bisectable:

1. Add `IToolProfileService` + `ToolProfileService` (parallel to
   `VehicleProfileService`, talking to `Tools/` directory). New v2 schema
   only — no migration yet.
2. Split `ProfileDto` → `VehicleProfileDto` + `ToolProfileDto`; rename old
   service to `ProfileJsonServiceV1` and keep it read-only.
3. Add `LastUsedToolProfile` to `AppSettings`; rename store fields
   `ActiveProfile*` → `ActiveVehicleProfile*`; add tool counterparts.
4. `ConfigurationService.LoadProfiles(vehicle, tool)`; update startup path
   to load both.
5. One-time migration on startup; tests for migration.
6. Add `Rename` / `Delete` to both profile services; tests.
7. `LoadVehicleToolDialogPanel` + ViewModel; register in
   `DialogOverlayHost`.
8. Wire `MainViewModel.Commands.Configuration.cs` to open the picker; remove
   the old single-profile picker if present.
9. UI tests; manual verification on Desktop + iPad (per `feedback_app_testing`,
   wait for user confirmation).

Per `feedback_feature_branch_merge`: branch does not merge to develop until
all of the above land and the user has tested it end-to-end on real hardware.
