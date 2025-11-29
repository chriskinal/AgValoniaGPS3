# MVVM Refactoring Plan

## Goal
Refactor MainWindow.axaml.cs (3500 lines, 89 methods) to proper MVVM architecture, enabling true 95/5 code sharing between Desktop and iOS.

## Principles
- **ViewModel has zero references to Avalonia.Controls**
- ViewModel only knows interfaces and models
- View binds to ViewModel commands and properties
- Services handle business logic
- Behaviors handle reusable UI interactions (like drag/drop)

## Current State Analysis

### MainWindow.axaml.cs Method Categories

| Category | Count | Current Location | Target Location |
|----------|-------|------------------|-----------------|
| Window lifecycle | 4 | Code-behind | Keep (platform-specific) |
| Panel drag handlers | 30 | Code-behind | DraggableBehavior |
| Button click handlers | 35 | Code-behind | ViewModel Commands |
| Map control interaction | 5 | Code-behind | ViewModel via IMapService |
| Boundary recording logic | 10 | Code-behind | ViewModel via IBoundaryRecordingService |
| Utility/helpers | 5 | Code-behind | Services or ViewModel |

---

## Phase 1: Create Shared Interfaces

- [x] Create `IMapService` in Shared/Services/Interfaces
- [x] Create `IDialogService` in Shared/Services/Interfaces
  - Methods for showing dialogs (returns Task with result)
  - ShowDataIODialog, ShowFieldSelectionDialog, ShowNewFieldDialog, etc.
  - View layer implements and injects into ViewModel
  - Result types: FieldSelectionResult, NewFieldResult, FromExistingFieldResult, etc.

---

## Phase 2: Create Shared Behaviors

- [x] Create `Shared/AgValoniaGPS.Views` project
- [x] Create `DraggableBehavior` in Shared/AgValoniaGPS.Views/Behaviors
  - Attach to any Border/Panel in XAML
  - Handles PointerPressed/Moved/Released
  - Supports optional DragHandleName for handle-only dragging
  - ConstrainToParent property to keep within bounds
  - Replaces 30 identical drag handler methods
  - Pure View concern, no ViewModel involvement

---

## Phase 3: Move Button Handlers to ViewModel Commands

For each `Btn*_Click` handler:
1. Create corresponding `ICommand` property in MainViewModel
2. Move logic from handler to command's Execute method
3. Update XAML to bind `Command="{Binding SomeCommand}"`
4. Delete handler from code-behind

### Commands Created in MainViewModel

**Dialog Commands:**
- [x] `ShowDataIODialogCommand`
- [x] `ShowSimCoordsDialogCommand`
- [x] `ShowFieldSelectionDialogCommand`
- [x] `ShowNewFieldDialogCommand`
- [x] `ShowFromExistingFieldDialogCommand`
- [x] `ShowIsoXmlImportDialogCommand`
- [x] `ShowKmlImportDialogCommand`
- [x] `ShowAgShareDownloadDialogCommand`
- [x] `ShowAgShareUploadDialogCommand`
- [x] `ShowAgShareSettingsDialogCommand`
- [x] `ShowBoundaryDialogCommand`

**Field Commands:**
- [x] `CloseFieldCommand`
- [x] `DriveInCommand`
- [x] `ResumeFieldCommand`

**Map Commands:**
- [x] `Toggle3DModeCommand`
- [x] `ZoomInCommand`
- [x] `ZoomOutCommand`

**Boundary Recording Commands:**
- [x] `ToggleBoundaryPanelCommand`
- [x] `StartBoundaryRecordingCommand`
- [x] `PauseBoundaryRecordingCommand`
- [x] `StopBoundaryRecordingCommand`
- [x] `UndoBoundaryPointCommand`
- [x] `ClearBoundaryCommand`
- [x] `AddBoundaryPointCommand`
- [x] `DeleteBoundaryCommand`
- [x] `ImportKmlBoundaryCommand`

**Added Infrastructure:**
- [x] `AsyncRelayCommand` class for async operations
- [x] Constructor updated to inject `IDialogService`, `IMapService`, `IBoundaryRecordingService`
- [x] `CenterMapOnBoundary()` helper method

### Remaining Steps for Phase 3
- [x] Update XAML to bind buttons to new commands
- [ ] Remove old click handlers from code-behind (deferred to Phase 7)

---

## Phase 4: Move Map Interaction to IMapService

- [x] Create platform-specific `MapService` implementations
  - Desktop: Wraps OpenGLMapControl (`Platforms/AgValoniaGPS.Desktop/Services/MapService.cs`)
  - iOS: Will wrap SkiaMapControl
- [x] Register IMapService in DI container
- [x] Inject IMapService into MainViewModel (constructor updated)
- [x] Create ViewModel commands/methods for map operations (Toggle3DModeCommand, ZoomIn/OutCommand)
- [ ] View's pointer handlers call ViewModel methods (or bind via behaviors)

---

## Phase 5: Wire ViewModel to Services

- [x] Inject `IBoundaryRecordingService` into MainViewModel
- [x] Subscribe to service events in ViewModel
- [x] Expose boundary state as ViewModel properties
- [ ] Remove service subscriptions from code-behind (deferred to Phase 7)

---

## Phase 6: Create IDialogService Implementation

- [x] Define dialog service interface with async methods (`IDialogService.cs`)
- [x] Create Desktop implementation (`Platforms/AgValoniaGPS.Desktop/Services/DialogService.cs`)
- [ ] Create iOS implementation (shows appropriate dialogs)
- [x] Inject into ViewModel (constructor updated)
- [ ] Replace all `dialog.ShowDialog(this)` calls in code-behind with ViewModel commands

---

## Phase 7: Reduce Code-Behind

After all above, MainWindow.axaml.cs should contain only:
- Constructor with `InitializeComponent()`
- Window lifecycle (Opened, Closing) for settings save/load
- Map pointer event forwarding to IMapService (if not using behaviors)

Target: < 100 lines

### Progress
- [x] XAML buttons now use Command bindings
- [x] Removed unused click handlers - **907 lines removed** (from 3500 to 2593)
  - Removed: BtnNtripConnect/Disconnect, BtnDataIO, BtnEnterSimCoords, Btn3DToggle
  - Removed: BtnFields, BtnNewField, BtnOpenField, BtnCloseField, BtnFromExisting
  - Removed: BtnIsoXml, BtnKml, BtnDriveIn, BtnResumeField
  - Removed: BtnAgShareSettings, BtnAgShareDownload, BtnAgShareUpload
- [ ] Apply DraggableBehavior to panels to remove drag handlers (~30 methods)
- [ ] Final cleanup to reach target line count

**Handlers still in XAML (to keep):**
- `BtnTestOSK_Click` - Test button for on-screen keyboard
- `BtnBoundaryOffset_Click` - Numeric keypad dialog
- All `*Panel_PointerPressed/Moved/Released` - Panel dragging (until DraggableBehavior is applied)
- All `MapOverlay_Pointer*` - Map interaction
- All `SectionControl_Pointer*` - Section control dragging

**Remaining boundary handlers (have internal dependencies):**
- Several boundary handlers remain that call helper methods (RefreshBoundaryList, etc.)
- These require more careful refactoring to move to ViewModel

---

## Phase 8: Create Shared View Project

- [x] Create `Shared/AgValoniaGPS.Views` project (created in Phase 2)
- [x] Create shared styles in `Styles/AppStyles.axaml`
- [x] Create `MainView.axaml` as UserControl in Shared
- [x] Add shared Views project reference to Desktop
- [ ] Desktop MainWindow hosts shared MainView (optional - can use side-by-side)
- [ ] iOS directly uses shared MainView
- [ ] Move dialogs to Shared (future - as needed)

### Current Structure
```
Shared/AgValoniaGPS.Views/
├── AgValoniaGPS.Views.csproj
├── Behaviors/
│   └── DraggableBehavior.cs
├── Styles/
│   └── AppStyles.axaml
└── Views/
    ├── MainView.axaml
    └── MainView.axaml.cs
```

### Usage Pattern
The shared MainView demonstrates the 95/5 pattern:
- Core UI structure is in shared MainView.axaml
- Platform-specific content (map control) is injected via code-behind
- Desktop MainWindow can optionally host MainView or use its own detailed layout

---

## Phase 9: Build and Test

- [x] Build Desktop - builds successfully, runs correctly
- [x] Update iOS to use shared Views project reference
- [x] Update iOS MainView.axaml to use shared styles
- [x] Update iOS MainView buttons to use Command bindings
- [ ] Fix iOS build issues (Avalonia XAML generator not working properly)
- [ ] Test on iOS simulator

### iOS Build Issues (to investigate)
- XAML generator not properly inheriting from Application base class
- Possible net10.0-ios SDK compatibility issue with Avalonia 11.3.9
- May need to add explicit Avalonia.iOS generator packages

---

## Notes

- Work incrementally: complete one phase, test, commit, then next
- If a phase is too large, break into smaller PRs
- Keep Desktop building and functional throughout
