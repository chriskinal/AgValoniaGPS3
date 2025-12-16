# Continuation Prompt for Desktop Shared Panels Migration

## Context
AgValoniaGPS3 is a cross-platform GPS guidance application with 95% shared code. We just completed adding the Data I/O dialog with working NTRIP connection for iOS. All shared panels and dialogs are working on iOS.

## Current State
- **iOS**: Fully working with all shared panels (10) and dialogs (12) from `Shared/AgValoniaGPS.Views/Controls/`
- **Desktop**: Still using desktop-only `LeftNavigationPanel` at `Platforms/AgValoniaGPS.Desktop/Controls/Panels/`, no dialogs registered

## Task
Migrate Desktop to use the shared panels and dialogs, then retire desktop-only panels.

## Migration Plan
See `/Users/chris/Code/AgValoniaGPS3/DESKTOP_SHARED_PANELS_MIGRATION.md` for full plan.

**Summary of phases:**
1. Update Desktop MainWindow.axaml namespaces from `AgValoniaGPS.Desktop.Controls.Panels` to `AgValoniaGPS.Views.Controls.Panels`
2. Add `xmlns:dialogs` namespace for shared dialogs
3. Register all 12 shared dialogs in MainWindow.axaml
4. Build and test Desktop
5. Delete desktop-only panels at `Platforms/AgValoniaGPS.Desktop/Controls/Panels/`
6. Final verification and commit

## Key Files
- **MainWindow.axaml**: `Platforms/AgValoniaGPS.Desktop/Views/MainWindow.axaml` - needs namespace changes and dialog registration
- **iOS MainView (reference)**: `Platforms/AgValoniaGPS.iOS/Views/MainView.axaml` - shows how dialogs are registered
- **Desktop-only to delete**: `Platforms/AgValoniaGPS.Desktop/Controls/Panels/LeftNavigationPanel.axaml(.cs)`

## Build Commands
```bash
# Build Desktop
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj

# Run Desktop
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj

# Build iOS
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj -c Debug -f net10.0-ios
```

## Recent Commits
- `fae6961` - Add Data I/O dialog with NTRIP connection and scrollable layout
- `3855919` - Update migration plan
- `eb97d13` - Add AlphanumericKeyboardPanel control

## Shared Panels Available
Located in `Shared/AgValoniaGPS.Views/Controls/Panels/`:
- LeftNavigationPanel, FileMenuPanel, SimulatorPanel, ConfigurationPanel
- FieldToolsPanel, JobMenuPanel, ToolsPanel, ViewSettingsPanel
- BoundaryPlayerPanel, BoundaryRecordingPanel

## Shared Dialogs Available
Located in `Shared/AgValoniaGPS.Views/Controls/Dialogs/`:
- SimCoordsDialogPanel, FieldSelectionDialogPanel, NewFieldDialogPanel
- FromExistingFieldDialogPanel, KmlImportDialogPanel, IsoXmlImportDialogPanel
- BoundaryMapDialogPanel, NumericInputDialogPanel, DataIODialogPanel
- AgShareSettingsDialogPanel, AgShareUploadDialogPanel, AgShareDownloadDialogPanel
