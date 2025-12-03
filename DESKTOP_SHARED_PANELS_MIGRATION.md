# Desktop Migration to Shared Panels

## Overview
Migrate Desktop to use shared panels and dialogs from `Shared/AgValoniaGPS.Views/Controls/` instead of desktop-only implementations.

## Current State

### Shared Library (Already Built for iOS)
**Panels** (`Shared/AgValoniaGPS.Views/Controls/Panels/`):
- [x] LeftNavigationPanel.axaml
- [x] FileMenuPanel.axaml
- [x] SimulatorPanel.axaml
- [x] ConfigurationPanel.axaml
- [x] FieldToolsPanel.axaml
- [x] JobMenuPanel.axaml
- [x] ToolsPanel.axaml
- [x] ViewSettingsPanel.axaml
- [x] BoundaryPlayerPanel.axaml
- [x] BoundaryRecordingPanel.axaml

**Dialogs** (`Shared/AgValoniaGPS.Views/Controls/Dialogs/`):
- [x] SimCoordsDialogPanel.axaml
- [x] FieldSelectionDialogPanel.axaml
- [x] NewFieldDialogPanel.axaml
- [x] FromExistingFieldDialogPanel.axaml
- [x] KmlImportDialogPanel.axaml
- [x] IsoXmlImportDialogPanel.axaml
- [x] BoundaryMapDialogPanel.axaml
- [x] NumericInputDialogPanel.axaml
- [x] AgShareSettingsDialogPanel.axaml
- [x] AgShareUploadDialogPanel.axaml
- [x] AgShareDownloadDialogPanel.axaml
- [x] DataIODialogPanel.axaml

**Other Controls**:
- [x] AlphanumericKeyboardPanel.axaml
- [x] NumericKeyboardPanel.axaml
- [x] DrawingContextMapControl.cs

### Desktop (Currently)
**Uses Desktop-only**:
- LeftNavigationPanel (at `Platforms/AgValoniaGPS.Desktop/Controls/Panels/`)
- No dialogs currently registered in MainWindow

**MainWindow.axaml references**:
- `xmlns:panels="using:AgValoniaGPS.Desktop.Controls.Panels"` → needs to change to shared
- Only `<panels:LeftNavigationPanel>` is used

---

## Migration Plan

### Phase 1: Add Shared Project Reference
- [ ] 1.1 Verify Desktop.csproj references AgValoniaGPS.Views project
- [ ] 1.2 Add namespace aliases in MainWindow.axaml for shared controls

### Phase 2: Update MainWindow.axaml
- [ ] 2.1 Change `xmlns:panels` from Desktop to Shared namespace
- [ ] 2.2 Add `xmlns:dialogs` for shared dialogs
- [ ] 2.3 Add `xmlns:controls` for shared controls (keyboard, etc.)
- [ ] 2.4 Update LeftNavigationPanel reference to use shared version

### Phase 3: Register All Shared Dialogs
Add to MainWindow.axaml (similar to iOS MainView):
- [ ] 3.1 SimCoordsDialogPanel
- [ ] 3.2 FieldSelectionDialogPanel
- [ ] 3.3 NewFieldDialogPanel
- [ ] 3.4 FromExistingFieldDialogPanel
- [ ] 3.5 KmlImportDialogPanel
- [ ] 3.6 IsoXmlImportDialogPanel
- [ ] 3.7 BoundaryMapDialogPanel
- [ ] 3.8 NumericInputDialogPanel
- [ ] 3.9 AgShareSettingsDialogPanel
- [ ] 3.10 AgShareUploadDialogPanel
- [ ] 3.11 AgShareDownloadDialogPanel
- [ ] 3.12 DataIODialogPanel

### Phase 4: Build and Test Desktop
- [ ] 4.1 Build Desktop project
- [ ] 4.2 Test LeftNavigationPanel appears and works
- [ ] 4.3 Test each dialog opens correctly:
  - [ ] File Menu opens FileMenuPanel
  - [ ] Simulator button opens SimulatorPanel
  - [ ] Data I/O button opens DataIODialogPanel
  - [ ] Fields button opens FieldSelectionDialogPanel
  - [ ] Field tools work
  - [ ] AgShare dialogs work

### Phase 5: Remove Desktop-Only Panels
After Desktop works with shared panels:
- [ ] 5.1 Delete `Platforms/AgValoniaGPS.Desktop/Controls/Panels/LeftNavigationPanel.axaml`
- [ ] 5.2 Delete `Platforms/AgValoniaGPS.Desktop/Controls/Panels/LeftNavigationPanel.axaml.cs`
- [ ] 5.3 Remove empty Panels folder if no other files
- [ ] 5.4 Update Desktop.csproj if needed (remove any panel-specific items)

### Phase 6: Final Verification
- [ ] 6.1 Build both iOS and Desktop
- [ ] 6.2 Test Desktop functionality matches iOS
- [ ] 6.3 Commit changes

---

## File Changes Summary

### MainWindow.axaml Changes
```xml
<!-- OLD -->
xmlns:panels="using:AgValoniaGPS.Desktop.Controls.Panels"

<!-- NEW -->
xmlns:panels="using:AgValoniaGPS.Views.Controls.Panels"
xmlns:dialogs="using:AgValoniaGPS.Views.Controls.Dialogs"
xmlns:sharedControls="using:AgValoniaGPS.Views.Controls"
```

### Files to Delete After Migration
- `Platforms/AgValoniaGPS.Desktop/Controls/Panels/LeftNavigationPanel.axaml`
- `Platforms/AgValoniaGPS.Desktop/Controls/Panels/LeftNavigationPanel.axaml.cs`

---

## Notes
- The shared panels were designed for iOS mobile touch interface but should work on desktop
- Some panels may need minor adjustments for desktop mouse interactions
- Keyboard panels (AlphanumericKeyboardPanel) are designed for touch but can be used on desktop
- Desktop may want to show/hide keyboard panels differently than iOS
