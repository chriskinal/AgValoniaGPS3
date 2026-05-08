# Track work sessions on each field (per-job coverage, notes, history)

**Issue:** #349
**Branch:** `feature/fields-and-jobs` (rename of `feature/field-job-hub-parity`)
**Status:** Draft — supersedes the prior "Field/Job hub parity" plan

## Goal

Split persistent **Field** geometry from per-session **Job** state, and
build a two-column "Start Work Session" UX (Fields list + selected-field
detail / Jobs panel) styled like the existing Vehicles/Tools tabs.

Field/Job split is desired in the AgOpen ecosystem but no real standard
exists — AgOpen calls every field-entry verb "Job" while having no `Job`
entity at all. This plan treats it as **green-field**: file format,
schema, and naming are ours to define.

## Why now

1. Coverage is per-field cumulative today. An operator who sprays the
   same field twice in a season can't tell pass 1 from pass 2 on the
   map. Per-job coverage layers fix this.
2. AgOpen 6.8.2's `FormJob` is the only existing reference and it's a
   menu of verbs, not a data model — copying it gets us a button hub
   (already shipped via `JobMenuPanel`) but no session history.
3. Issue #349 confirmed there's appetite for a real Field/Job split and
   no ecosystem-level format to be backwards-compatible with.

## Domain model

### Field — persistent geometry, edited over the lifetime of the field
- `Name`
- `Origin` (lat / lon)
- `OuterBoundary`, `InnerBoundaries[]`
- `Headland`
- `Tracks[]` (AB lines + curves — track is field geometry, not session
  state)
- `Flags[]`
- `ElevationLog` — additive across all jobs
- `CreatedAt`, `LastOpenedAt`

### Job — one work session against a field
- `TaskName` — default `<YYYY-MM-DD>_<work_type>[_<vehicle_or_sim>]`,
  user-editable
- `WorkType` — free-text string with autocomplete (see Decision #1)
- `Notes` — free-text, multi-line
- `StartedAt`, `EndedAt`, `LastOpenedAt`
- `Status` — `in_progress` | `done` | `abandoned`
- `Coverage` — per-job triangle strips (binary)
- `SectionLog` — per-job section on/off events
- `DistanceTraveledMeters`, `AreaWorkedHa`, `UTurnCount` (computed)

### File layout

```
<FieldsRoot>/<FieldName>/
  field.json              -- name, origin, metadata (new format)
  boundary.json           -- outer + inner polygons
  headland.json
  tracks/                 -- existing per-track files
  flags.json
  elevation.bin
  jobs/
    <TaskName>/
      job.json            -- metadata (work type, notes, timestamps)
      coverage.bin        -- per-job coverage
      sections.log        -- per-job section events
```

Legacy `Field.txt`, `Boundary.txt`, etc. are still imported by the
existing legacy importer; on first open we convert and write the new
JSON shape (consistent with `FILE_FORMAT_MODERNIZATION_PLAN.md`).

## UI

Two columns, modeled on `Configuration/VehicleConfigTab.axaml` (left
strip + right detail). Replaces the wizard mock from issue #349 — same
information, no Back/Next paging, map stays visible behind a draggable
floating dialog.

### `StartWorkSessionDialogPanel`

```
ColumnDefinitions = "*, 1.5*"
```

**Left column — Fields list**
- DataGrid: `Name` | `Distance (km)` | `Boundary (ha)`
- Sortable header (Name / Distance / Area)
- Row select highlights the field in the right column
- Footer: `[+ New Field]`

**Right column — selected-field detail**
- Header: field name, boundary area, last opened
- `Jobs` history grid: `Task Name` | `Work Type` | `Last Opened` |
  `Status`. Tap row → resume that job.
- `New Job` mini-form below the grid:
  - `Work Type` text box with autocomplete dropdown (Decision #1)
  - `Notes` multi-line box (`[Use Last]` button copies notes from most
    recent job for this field)
  - `Task Name` text box, auto-populated from the format above,
    user-editable
  - Buttons: `[Open Field Only]` (Decision #2), `[Start New Job]`

### `ResumeTaskDialogPanel`

- Same right-column "Jobs history" rendering, but unfiltered across
  fields, ordered by `LastOpenedAt DESC`.
- Row text matches screencap 3: `<FieldName> – <TaskName>` headline,
  `<WorkType> | Last opened: <ts>` sub-line, `Notes: <truncated>`.
- Row tap opens both the field and the job.

### `JobMenuPanel` rewire

The current 11-button hub flattens to:

| Button | Action |
|---|---|
| New Field | open `StartWorkSessionDialog` with new-field form prefilled |
| Open Field / Job | open `StartWorkSessionDialog` |
| Resume Last Job | one-tap reopen most recent job for `LastOpenedField` |
| Resume Task | open `ResumeTaskDialog` (cross-field history) |
| InField | nearby-fields shortcut (replaces the `DriveIn` stub) |
| Close | close current job + field, with opt-in close hooks |
| AgShare Download | unchanged |
| KML Import | unchanged |
| ISOXML Import | unchanged |

`AgShare Upload` and `Export KML` / `Export ISOXML` move from buttons to
opt-in close hooks (carried over from the prior plan).

## Service work

- New `IJobService`:
  - `IReadOnlyList<JobSummary> ListJobs(string fieldName)`
  - `Job? GetJob(string fieldName, string taskName)`
  - `Job CreateJob(string fieldName, WorkType type, string notes, string taskName)`
  - `void ResumeJob(string fieldName, string taskName)`
  - `void CloseCurrentJob()`
- `IFieldService.FindFieldsNear(double lat, double lon, double maxKm)`
  for the Distance column + InField sort (carried over).
- `ICoverageMapService` re-keyed by `JobId` instead of `FieldId`. Save
  path becomes `<Field>/jobs/<TaskName>/coverage.bin`.
- `MainViewModel.CloseFieldAsync` becomes `CloseJobAsync` + optional
  `CloseFieldAsync`; opt-in hooks (`AgShare.AutoUploadOnClose`,
  `Field.AutoExportKmlOnClose`, `Field.AutoExportIsoXmlOnClose`) fire on
  job close.

## Migration

For each existing field directory with coverage but no `jobs/`:
1. Create `jobs/imported-<field-mtime>/job.json` with
   `WorkType=Unknown`, `Notes="Imported from legacy field"`,
   `Status=done`.
2. Move `Coverage.bin` (or equivalent) into that job folder.
3. Leave field-level files in place.

Run on first open of a legacy field. Idempotent (skip if `jobs/`
already exists).

## Decisions (formerly TBDs)

1. **Work types — free-text with autocomplete.** Stored as a string on
   `Job.WorkType`. Suggestion list is the distinct set of `WorkType`
   values seen across all known jobs (recency-ordered). New labels are
   accepted as-is; case-insensitive de-duping for suggestions. The seed
   list (`fertilizing`, `spraying`, `seeding`, `cultivating`, `tillage`,
   `harvesting`) ships as starter suggestions but is not enforced.
2. **Field-only open — allowed.** Opening a Field with no active Job
   shows geometry (boundary, headland, tracks, flags) and is read-only
   for session data: coverage paint and section-log writes are blocked
   until a Job is created or resumed. UI surface: `[Open Field Only]`
   button on `StartWorkSessionDialog`.
3. **Job archival — editable on resume.** Closing a Job sets
   `Status=done` and `EndedAt`, but resuming the same Job clears
   `EndedAt`, sets `Status=in_progress`, and appends to the existing
   `coverage.bin` and `sections.log`. One logical work session can span
   multiple physical resumes. No continuation/parent-job concept.

## Tests

- `JobServiceTests`: CRUD, listing order, default task-name format.
- `FieldServiceTests`: `FindFieldsNear` distance / order (carried over).
- `CoverageMapServiceTests`: per-job path resolution, two jobs in one
  field don't share coverage.
- `LegacyFieldMigrationTests`: legacy field with coverage → synthetic
  imported job, idempotent on rerun.
- `StartWorkSessionDialogTests` (UI): both columns render, row select
  populates right pane, `Start New Job` creates job + opens it.
- `ResumeTaskDialogTests` (UI): cross-field ordering, row tap opens
  field+job.

## Sequencing on the feature branch

1. Domain model: `Job`, `JobSummary`, `WorkType`, `Field` metadata.
   No UI yet.
2. File layout + serializers (`field.json`, `job.json`).
3. Legacy field migration + tests.
4. `IJobService` + tests.
5. `ICoverageMapService` re-keyed by `JobId`; existing tests adjusted.
6. `IFieldService.FindFieldsNear` + tests.
7. `StartWorkSessionDialogPanel` (replaces `FieldSelectionDialogPanel`
   and `NewFieldDialogPanel` for the create-flow case).
8. `ResumeTaskDialogPanel`.
9. `JobMenuPanel` rewire to the reduced button set.
10. Opt-in close hooks (`AgShare.AutoUploadOnClose`,
    `Field.AutoExportKmlOnClose`, `Field.AutoExportIsoXmlOnClose`).
11. Manual verification on Desktop + iPad before merge per
    `feedback_app_testing` and `feedback_feature_branch_merge`.

## Out of scope

- Multi-vehicle simultaneous jobs against the same field.
- Cloud sync of in-progress jobs (one-shot AgShare upload at close
  stays as the only network egress).
- `Resume.txt`-style mid-session restore (last cursor position on
  track). Track separately if asked for.
- Re-skinning as a modal startup wizard. Hub stays a draggable floating
  dialog so the map remains visible.
