# Fields & Jobs — Implementation Plan (#349)

**Companion to:** [`FIELDS_AND_JOBS_PLAN.md`](FIELDS_AND_JOBS_PLAN.md) (the *what / why*)
**This doc:** the *how*. Phased, shippable, with concrete files and signatures.
**Branch:** `feature/fields-and-jobs` (rename of `feature/field-job-hub-parity` per the parent plan)

## Decisions (formerly TBDs)

The parent plan ended with three TBDs. Locked in conversation 2026-05-05; the
parent `FIELDS_AND_JOBS_PLAN.md` is the canonical source — this section mirrors it.

1. **Work types — free-text with autocomplete.** Stored as `Job.WorkType : string`.
   Suggestion list = `JobWorkTypeSuggestions.Seed` (`fertilizing`, `spraying`,
   `seeding`, `cultivating`, `tillage`, `harvesting`) plus the distinct set of
   prior `WorkType` values across known jobs (case-insensitive, recency-ordered).
   Operators can type anything — no enum, no `OtherLabel`.

2. **Field-only open — allowed.** `OpenFieldOnly()` loads geometry but writes no
   coverage and no section-log events. Any attempt to enter `Auto` sections or
   paint coverage prompts to start a job. Useful for editing boundaries / tracks
   between sessions without polluting coverage history.

3. **Job archival — editable on resume.** Closing a job sets `Status=Done` and
   stamps `EndedAt`. Resuming flips `Status` back to `InProgress`, clears
   `EndedAt`, and appends to the existing `coverage.bin` and `sections.log`.
   No `ParentTaskName` / continuation chain — one logical session can span
   multiple physical resumes. Files stay writable.

## Milestones

Per the parent plan, all work lands on `feature/fields-and-jobs` and merges to
`develop` as one PR once M1–M5 are complete and verified on Desktop + iPad
(see `feedback_feature_branch_merge`). The "milestone" boundaries below are
sequencing checkpoints on the branch — each one keeps the branch buildable and
testable in isolation, but no milestone is merged to `develop` on its own.

### Milestone 1 — Domain model + serializers (no behavior change)

**Goal:** define `Job`, `JobSummary`, `JobStatus`, `JobWorkTypeSuggestions`, plus the `field.json` / `job.json` shapes. The app behaves identically; new files appear silently when fields are saved/loaded.

**New files**
- `Shared/AgValoniaGPS.Models/Job/Job.cs`
- `Shared/AgValoniaGPS.Models/Job/JobSummary.cs`
- `Shared/AgValoniaGPS.Models/Job/JobStatus.cs`
- `Shared/AgValoniaGPS.Models/Job/JobWorkTypeSuggestions.cs` (seed list + helper for distinct prior labels)
- `Shared/AgValoniaGPS.Services/Fields/FieldJsonService.cs` — write/read `field.json` (folder is `Fields/` plural; `Services.Field` namespace collides with the `Field` class)
- `Shared/AgValoniaGPS.Services/Fields/JobJsonService.cs` — write/read `<field>/jobs/<task>/job.json`

**Modified**
- `Shared/AgValoniaGPS.Models/Field.cs` — add `LastOpenedDate` (separate from `LastModifiedDate` so view-only opens don't bump modified).
- `Shared/AgValoniaGPS.Services/FieldService.cs` — on save, write `field.json` alongside legacy `Field.txt` / `field.geojson`. On load, prefer `field.json` if present.

**Key shapes** (see `Shared/AgValoniaGPS.Models/Job/` for the canonical source):

```csharp
public enum JobStatus { InProgress, Done, Abandoned }

public class Job
{
    public Guid Id { get; set; }                  // stable; coverage/section files key off this
    public string FieldName { get; set; }         // parent field folder
    public string TaskName { get; set; }          // <YYYY-MM-DD>_<work_type>[_<vehicle_or_sim>]
    public string WorkType { get; set; }          // free-text (Decision #1)
    public string Notes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }        // set on close, cleared on resume (Decision #3)
    public DateTime LastOpenedAt { get; set; }
    public JobStatus Status { get; set; }
    public double DistanceTraveledMeters { get; set; }
    public double AreaWorkedHectares { get; set; }
    public int UTurnCount { get; set; }
}

public sealed record JobSummary(
    Guid Id, string FieldName, string TaskName, string WorkType, string Notes,
    DateTime StartedAt, DateTime? EndedAt, DateTime LastOpenedAt, JobStatus Status);
```

**Tests**
- `JobJsonServiceTests`: round-trip a `Job`; unknown future fields are tolerated (forward-compat).
- `FieldJsonServiceTests`: round-trip metadata; legacy `Field.txt`-only field upgrades to `field.json` on first save.
- `JobTaskNameTests`: default task-name format `2026-05-05_spraying_Sim` (lowercase work type, underscores, no spaces).

**Risks**
- Json migration must not regress existing field load. Keep `Field.txt` write path until Milestone 2 to allow rollback.

### Milestone 2 — `IJobService` + per-job coverage backend (no UI)

**Goal:** every field-load flow now goes through a default auto-created job. Coverage path becomes `<Field>/jobs/<TaskName>/coverage.bin`. No visible UX change yet — the dialog still says "Open Field"; under the hood a job is created.

**New files**
- `Shared/AgValoniaGPS.Services/Interfaces/IJobService.cs`
- `Shared/AgValoniaGPS.Services/JobService.cs`
- `Tests/AgValoniaGPS.Services.Tests/JobServiceTests.cs`

**Modified**
- `Shared/AgValoniaGPS.Services/Coverage/CoverageMapService.cs` — coverage save/load keyed by `(fieldName, taskName)`, not just field name. Add `SetActiveJob(string fieldName, string taskName)`.
- `Shared/AgValoniaGPS.Models/Configuration/ConfigurationStore.cs` — add `ActiveJobTaskName` (string, parallels `ActiveVehicleProfileName`).
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` — on field open, call `JobService.GetOrCreateDefaultJob(fieldName)` and `_coverageMapService.SetActiveJob(...)`.
- `Shared/AgValoniaGPS.Services/FieldService.cs` — `OpenField` now returns the loaded field *and* a job context (or fires an event the VM picks up).

**Key shapes**
```csharp
public interface IJobService
{
    IReadOnlyList<JobSummary> ListJobs(string fieldName);
    IReadOnlyList<JobSummary> ListAllJobs();   // for ResumeTaskDialog

    Job? GetJob(string fieldName, string taskName);
    Job CreateJob(string fieldName, string workType, string notes,
                  string? taskName = null);
    Job GetOrCreateDefaultJob(string fieldName);  // M2 silent path
    IReadOnlyList<string> SuggestWorkTypes();     // seed + distinct prior labels

    void ResumeJob(string fieldName, string taskName);
    void CloseCurrentJob(JobStatus closingStatus = JobStatus.Done);

    Job? ActiveJob { get; }
    event EventHandler<Job?>? ActiveJobChanged;
}
```

**Tests**
- `JobServiceTests`:
    - `CreateJob` writes `job.json` and returns a Job.
    - `ListJobs` orders by `LastOpenedAt DESC`.
    - `GetOrCreateDefaultJob` returns existing in-progress job if one exists, else creates new with default name.
    - `CloseCurrentJob` flips `Status` and stamps `EndedAt`.
- `CoverageMapServiceTests`:
    - Two jobs in same field do not share coverage.
    - Switching `SetActiveJob` swaps the in-memory bitmap.
    - Save path resolves to `<Field>/jobs/<Task>/coverage.bin`.

**Migration step (one-shot, runs in M2)**
For each field directory with `Coverage.bin` but no `jobs/`:
1. Create `jobs/imported-<field-mtime>/job.json` (`WorkType="imported"`, `Notes="Imported from legacy field"`, `Status=Done`).
2. Move `Coverage.bin` → `jobs/imported-<...>/coverage.bin`.
3. Idempotent (check for `jobs/` first).

`Tests/AgValoniaGPS.Services.Tests/LegacyFieldMigrationTests.cs` — covers idempotency, mtime parsing, missing-coverage fields untouched.

**Risks**
- The active-job lifecycle (when does default open trigger?) needs to be exactly one place — `MainViewModel.OpenFieldAsync` — to avoid coverage being written under the wrong job during edge flows (cancelled load, error).
- Coverage save race: closing a job while a coverage-write is in flight. `JobService.CloseCurrentJob` must await `_coverageMapService.FlushAsync()` before flipping status.

### Milestone 3 — `StartWorkSessionDialogPanel` UI (replaces `FieldSelectionDialog`)

**Goal:** the new two-column picker is the default "Open Field / Job" entry. Operator can pick a field and either `Open Field Only`, start a `New Job`, or resume an existing job from the per-field history grid.

**New files**
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/StartWorkSessionDialogPanel.axaml`
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/StartWorkSessionDialogPanel.axaml.cs`
- `Shared/AgValoniaGPS.ViewModels/StartWorkSessionDialogViewModel.cs`
- `Tests/AgValoniaGPS.UI.Tests/StartWorkSessionDialogTests.cs`

**Modified**
- `Shared/AgValoniaGPS.Models/State/UIState.cs` — add `DialogType.StartWorkSession`.
- `Shared/AgValoniaGPS.Views/Controls/DialogOverlayHost.axaml` — register the new panel.
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` — add `StartWorkSessionDialogVm` property, `ShowStartWorkSessionDialogCommand`.
- `Shared/AgValoniaGPS.Views/Controls/Panels/JobMenuPanel.axaml` — re-point `Open Field` button at `ShowStartWorkSessionDialogCommand` (the legacy `FieldSelectionDialog` is left wired to its own button until M5 cleanup so nothing breaks if M3 is reverted).

**Layout** (mirrors picker convention from #346)
```
ColumnDefinitions = "*, 1.5*"
Row 0: Active banner — "Working: <field> / <job>" (or "No active job")
Row 1: Left column        | Right column
       Fields list (Grid) | Selected field detail
                            - Header: name, area, last opened
                            - Jobs history grid
                            - "+ New Job" mini-form
Row 2: Status / error message
Row 3: Cancel buttons (no Apply — actions inline)
```

**ViewModel sketch**
```csharp
public partial class StartWorkSessionDialogViewModel : ObservableObject
{
    public ObservableCollection<FieldRow> Fields { get; }     // Name, DistanceKm, AreaHa
    public ObservableCollection<JobSummary> JobsForSelectedField { get; }

    [ObservableProperty] private FieldRow? _selectedField;
    [ObservableProperty] private string _newJobWorkType = "";   // free-text + autocomplete
    [ObservableProperty] private string _newJobNotes = "";
    [ObservableProperty] private string _newJobTaskName = "";
    [ObservableProperty] private string? _statusMessage;
    public ObservableCollection<string> WorkTypeSuggestions { get; }

    public ICommand OpenFieldOnlyCommand { get; }
    public ICommand StartNewJobCommand { get; }
    public ICommand ResumeJobCommand { get; }   // takes JobSummary
    public ICommand UseLastNotesCommand { get; }
    public ICommand CancelCommand { get; }
}
```

**Tests** (Avalonia.Headless)
- Dialog renders both columns; row select populates right pane.
- `Start New Job` invokes `JobService.CreateJob` with the typed Notes / WorkType.
- `Use Last` copies notes from the most recent job for the selected field.
- `Resume Job` fires `JobService.ResumeJob` and closes the dialog.

**Risks**
- DataGrid binding semantics in Avalonia 12 — verify that `ItemsSource` + `SelectedItem` two-way works for the Fields grid. If not, fall back to ListBox + DataTemplate (`Configuration/VehicleConfigTab.axaml` uses ListBox successfully).
- "Default task name" auto-fill must update when `WorkType` changes (PartialOnPropertyChanged).

### Milestone 4 — `ResumeTaskDialogPanel` + Resume Last Job shortcut

**Goal:** cross-field resume UX. `Resume Last Job` button on the JobMenu reopens the most recent job in one tap. `Resume Task` button opens a flat list across all fields, ordered by `LastOpenedAt DESC`.

**New files**
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/ResumeTaskDialogPanel.axaml(.cs)`
- `Shared/AgValoniaGPS.ViewModels/ResumeTaskDialogViewModel.cs`
- `Tests/AgValoniaGPS.UI.Tests/ResumeTaskDialogTests.cs`

**Modified**
- `Shared/AgValoniaGPS.Models/State/UIState.cs` — add `DialogType.ResumeTask`.
- `Shared/AgValoniaGPS.Views/Controls/DialogOverlayHost.axaml`.
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` — `ResumeLastJobCommand`, `ShowResumeTaskDialogCommand`.

**Row format** (per the parent plan / screencap 3)
```
<FieldName> – <TaskName>                       <-- 14pt SemiBold
<WorkType> | Last opened: 2026-04-30 14:22     <-- 12pt SubHeader
Notes: First pass, north section…              <-- 12pt SubHeader, truncated 80 chars
```

**Tests**
- Cross-field ordering: 3 jobs across 2 fields, ordered by `LastOpenedAt DESC`.
- Row tap: `JobService.ResumeJob(field, task)` called *and* the field opens (existing `OpenFieldAsync` invoked).
- `Resume Last Job` short-circuits the dialog and opens the topmost entry.

**Risks**
- `ResumeJob` must atomically: open the field if not currently active, switch active job, restore that job's coverage. Test the "different field than currently open" case explicitly.

### Milestone 5 — `JobMenuPanel` rewire + opt-in close hooks

**Goal:** the operator-facing button hub matches the spec in the parent plan. Closing a job offers AgShare upload / KML export / ISOXML export based on per-profile opt-in flags.

**Modified**
- `Shared/AgValoniaGPS.Views/Controls/Panels/JobMenuPanel.axaml` — reduce to the 9-button set in the parent plan §UI.
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.Commands.Fields.cs` — new commands:
    - `ResumeLastJobCommand` (M4 already wires this — keep)
    - `InFieldCommand` (existing `DriveInCommand` rewire — uses `IFieldService.FindFieldsNear` from §service work)
    - `CloseJobCommand` — fires hooks then `JobService.CloseCurrentJob(Done)`
- `Shared/AgValoniaGPS.Models/Configuration/AppSettings.cs`:
    - `bool AutoUploadOnClose { get; set; }` (AgShare)
    - `bool AutoExportKmlOnClose { get; set; }`
    - `bool AutoExportIsoXmlOnClose { get; set; }`
- New `Shared/AgValoniaGPS.Services/JobCloseHookRunner.cs` — sequenced runner so hook failure on AgShare doesn't block local KML/ISOXML export.

**Removed (after verification)**
- `FieldSelectionDialogPanel.axaml` and its VM (replaced by `StartWorkSessionDialogPanel` in M3 — left in place during M3/M4 for safety, deleted in M5 cleanup).
- `NewFieldDialogPanel.axaml` (its create-flow is now the inline form on the right column of `StartWorkSessionDialog`).

**Tests**
- `JobCloseHookRunnerTests`: each hook runs only when its flag is true; an exception in one hook doesn't prevent the others.
- UI: each removed dialog has no remaining bindings or AXAML references (smoke test via grep in CI).

**Risks**
- Deleting the legacy dialogs is a one-way move. Verify all callsites land on the new dialog before deletion. The `git grep` smoke test belongs in CI as a guard against re-introduction.

## Cross-cutting work (shared across milestones)

- **`IFieldService.FindFieldsNear(double lat, double lon, double maxKm)`** — needed for the Distance column (M3) *and* InField shortcut (M5). Implement once in M3 with tests; M5 just consumes it.

- **Active state plumbing.** `ConfigurationStore.ActiveVehicleProfileName` already exists; `ActiveJobTaskName` follows the same `SetProperty` + `RaisePropertyChanged` pattern. The Active banner on `StartWorkSessionDialog` and the Configuration pill (#346) both bind to a new `MainViewModel.CurrentJobSummary` derived property `"<field> / <task>"`.

- **Migration order.** M1 introduces the new `field.json` write path alongside legacy `Field.txt` / `field.geojson` (read either, write both). M2 adds the legacy-coverage-to-job migration. The legacy `Field.txt` write path stays alive after this feature ships; dropping it is a follow-up tracked separately under `FILE_FORMAT_MODERNIZATION_PLAN.md`.

## Test strategy summary

| Layer | Tool | Milestone(s) |
|---|---|---|
| Models / serializers | NUnit | M1 |
| Services (`IJobService`, coverage re-key, migration) | NUnit | M2 |
| ViewModels | NUnit + NSubstitute | M3, M4, M5 |
| UI (headless Avalonia) | NUnit + Avalonia.Headless | M3, M4, M5 |
| Integration smoke (manual) | iPad + Desktop + Android | end of each milestone |

Per `feedback_app_testing` and `feedback_feature_branch_merge`: each milestone gets manual verification on at least Desktop + iPad before the next starts on the branch. Branch stays open until M5 ships end-to-end.

## Out of scope (carried over from parent plan)

- Multi-vehicle simultaneous jobs against the same field.
- Cloud sync of in-progress jobs (one-shot AgShare upload at close stays as the only network egress).
- `Resume.txt`-style mid-session restore (last cursor position on track).
- Re-skinning as a modal startup wizard. Hub stays a draggable floating dialog so the map remains visible.
