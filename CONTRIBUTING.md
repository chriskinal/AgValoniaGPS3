<!--
AgValoniaGPS
Copyright (C) 2024-2025 AgValoniaGPS Contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
-->

# Contributing to AgValoniaGPS

Thank you for your interest in contributing to AgValoniaGPS! This document lists features that need implementation and provides guidance for contributors.

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Build the project following instructions in `CLAUDE.md`
4. Pick a feature from the list below
5. Create a feature branch off `develop` and implement
6. Submit a pull request targeting the `develop` branch

## Branch Strategy

- **`master`** - Stable releases only
- **`develop`** - Active development branch, PR target for all new work
- **Feature branches** - Create `feature/your-feature` off `develop` for each issue

## Key Documents

- **[Plans/ARCHITECTURE.md](Plans/ARCHITECTURE.md)** - Full architecture documentation: service communication, state management, domain models, data flow diagrams
- **[CLAUDE.md](CLAUDE.md)** - Build commands, key files reference, common development tasks
- **[PGN.md](PGN.md)** - UDP packet protocol for hardware communication

## Architecture Overview

- **Shared code** (~92%): Located in `Shared/` folder
  - `AgValoniaGPS.Models/` - Data models
  - `AgValoniaGPS.Services/` - Business logic
  - `AgValoniaGPS.ViewModels/` - MVVM ViewModels (ReactiveUI)
  - `AgValoniaGPS.Views/` - Shared UI controls, panels, dialogs

- **Platform code** (~8%): Located in `Platforms/`
  - `AgValoniaGPS.Desktop/` - Windows/macOS/Linux
  - `AgValoniaGPS.iOS/` - iOS/iPadOS
  - `AgValoniaGPS.Android/` - Android

### Cross-Platform Parity Rule

**All code MUST go in `Shared/` unless it requires platform-specific APIs.** This is a hard architectural requirement, not a preference. Putting code in a single platform folder means the other platforms lose that feature.

**What goes in `Shared/`:** UI controls, panels, dialogs, view models, services, models, converters, styles, icons, localization strings.

**What goes in `Platforms/`:** Application entry point (`App.axaml.cs`), DI container setup, `MainWindow`/`MainView` (layout shell + drag handlers), `MapService` (map control registration), platform-specific APIs (file pickers, notifications).

**Example violations fixed in #187-192:**
- Status bar indicators were in Desktop `MainWindow.axaml` instead of a shared `StatusBarPanel`
- Localization init was Desktop-only in `App.axaml.cs` instead of all three platforms
- Flag placement banner existed only in Desktop
- Screenshot capture code was duplicated across all three platforms instead of a shared helper

## MVVM Discipline

AgValoniaGPS follows strict MVVM layering. An earlier refactor cleaned up significant violations of this pattern — regressions are not welcome.

> **Pipeline computes, ViewModel coordinates, View binds.**

### What goes where

- **Services / pipeline** (`AgValoniaGPS.Services/`): all domain computation. Geometry, guidance math, coordinate conversion, coverage painting, section logic, pathing, state machines. These are the units under test.
- **Models** (`AgValoniaGPS.Models/`): data shapes. `*WorkingState` POCOs, `*State : ObservableObject` mirrors, records, DTOs, geometry primitives. Behavior is limited to pure helpers (e.g., `GeometryMath`).
- **ViewModels** (`AgValoniaGPS.ViewModels/`): orchestration only. Expose bindable properties, wire commands to services, translate user intent into intents/service calls. ViewModels are thin.
- **Views** (`AgValoniaGPS.Views/`): AXAML bindings and presentation. Code-behind is limited to view concerns (pointer handlers for dragging, focus, keyboard routing).

### Rules

1. **No domain computation in the ViewModel.** If you find yourself writing geometry, distance math, pathing, or multi-step business logic inside a `*ViewModel.cs`, stop — move it to a service and call the service from the VM. The VM's job is to coordinate, not to compute.
2. **No service calls from code-behind.** Views bind to ViewModel properties and commands. Don't inject services into a `View.axaml.cs`.
3. **Commands stay thin.** A `ReactiveCommand` delegate should read as *"ask service X to do Y, optionally push an intent, optionally show a dialog."* If it's longer than that, the body belongs in a service method.
4. **No direct `State.*` mutation from commands for pipeline-owned state.** Push an intent through `IPipelineIntents` — see the Threading Model section. UI-only state (dialog visibility, panel position) is fine to mutate directly.
5. **No ViewModel references from services.** Services expose interfaces, raise events, or return results. The ViewModel subscribes/consumes. Dependency flows one direction.

### Why this matters

Services are unit-testable without a dispatcher, a view, or a mocked VM. Once computation leaks into the VM, that testability is gone and the VM becomes a 3000-line god object — the exact problem the earlier refactor fixed. Keep the layers clean.

## Threading Model

AgValoniaGPS uses a strict one-way data flow driven by a dedicated background cycle worker. This is a hard architectural requirement, not a convention — violating it reintroduces the AgOpenGPS/WinForms failure mode where domain logic races on the UI thread.

![Threading model](Plans/Completed/threading_model.svg)

See [`Plans/Completed/threading_model_overview.svg`](Plans/Completed/threading_model_overview.svg) for the full picture (current → phases → target in one frame) and [`Plans/Completed/THREADING_MIGRATION_PLAN.md`](Plans/Completed/THREADING_MIGRATION_PLAN.md) for the historical migration plan (now complete).

### The invariant (non-negotiable)

> **Per-cycle GPS pipeline work runs on a dedicated cycle worker** (`Task.Run` per tick, with single-cycle-in-flight back-pressure via `Interlocked`). It does **not** run on the UI dispatcher, and it does **not** run on any I/O thread — UDP receive, NMEA parse, file watcher. I/O threads parse, hand off, and return immediately.

Only the cycle-worker failure mode is survivable: back-pressure drops the *next* tick, the current cycle completes uninterrupted, I/O and UI keep running. Running cycle work on the UI dispatcher drops frames during turns (violating the 24 FPS floor); running it on the UDP receive thread stalls packet ingestion and can lose fixes.

### Two state types per domain

- **`*WorkingState`** — plain POCO/record. Owned by the cycle worker. Mutated freely on the background thread. No `ObservableObject`, no `PropertyChanged`, no UI awareness. Single-writer.
- **`*State : ObservableObject`** — the UI-bound type. A one-way mirror. **The only writer is `ApplyGpsCycleResult` on the UI thread.** No service writes to it directly.

### Data flow

**Cycle → UI (snapshots):** Cycle worker mutates `*WorkingState` during a tick → builds an immutable `GpsCycleResult` snapshot at end of tick → posts to the UI dispatcher → `ApplyGpsCycleResult` writes the snapshot fields onto `State.*`, firing `PropertyChanged` and updating bindings.

**UI → Cycle (intents):** UI command writes to a thread-safe intent field/queue (`IPipelineIntents`) → cycle worker drains intents at the start of each tick → reacts on that tick → result appears in the UI on the same cycle's snapshot.

### Three rules, no exceptions

1. **Cycle worker never touches `*State : ObservableObject`.** Only `*WorkingState`.
2. **ViewModel never mutates `*State` from a service callback.** Only `ApplyGpsCycleResult`.
3. **UI commands push intents, they don't reach into cycle-worker state.**

### Common patterns to follow

- Adding a new domain state: create a `FooWorkingState` POCO, extend `GpsCycleResult` with a `Foo` snapshot record, mirror it in `ApplyGpsCycleResult`.
- Adding a new UI command that changes pipeline behavior: define a method on `IPipelineIntents`, push from the command, drain at the start of the cycle.
- Adding a new service that reads GPS/position: take `*WorkingState` as a parameter, don't inject `ApplicationState`.
- Avoid writing to `State.YouTurn`, `State.Guidance`, `State.Vehicle`, `State.Section` from anywhere except `ApplyGpsCycleResult`.

## What Needs Doing

Open work is tracked on the [AgValoniaGPS project board](https://github.com/orgs/AgOpenGPS-Official/projects/16). Pick a card, comment on the linked issue to claim it, and open your PR against `develop`.

## Translations

Translation contributions are **on hold** until we decide how AgValoniaGPS will connect to the shared [AgOpenGPS Weblate project](https://hosted.weblate.org/engage/agopengps/). AgOpen has used Weblate for over a year; our goal is to let one Weblate contribution benefit both projects rather than maintain parallel hand-edited `.resx` files.

Until that workflow is decided, please do not open PRs that add or edit files under `Shared/AgValoniaGPS.Views/Localization/`. If you'd like to translate, watch this section — we'll link to the Weblate project here once it's set up.

## How to Implement a Button Feature

1. **Find the button** in the relevant AXAML file under `Shared/AgValoniaGPS.Views/Controls/`

2. **Add a Command binding** to the button:
   ```xml
   <Button Content="My Feature" Command="{Binding MyFeatureCommand}" />
   ```

3. **Create the command** in `MainViewModel.cs` or appropriate ViewModel:
   ```csharp
   public ReactiveCommand<Unit, Unit> MyFeatureCommand { get; }

   // In constructor:
   MyFeatureCommand = ReactiveCommand.Create(ExecuteMyFeature);

   private void ExecuteMyFeature()
   {
       // Implementation here
   }
   ```

4. **For dialogs**, follow the existing pattern:
   - Add `IsMyDialogVisible` property to ViewModel
   - Create dialog panel in `Shared/AgValoniaGPS.Views/Controls/Dialogs/`
   - Add dialog to `MainWindow.axaml` (Desktop) and `MainView.axaml` (iOS/Android)

## Questions?

Open an issue on GitHub or reach out to the maintainers.
