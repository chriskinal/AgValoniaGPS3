# AgValoniaGPS3 Restructure Plan: 80/20 Architecture

## Overview

Moving from attempted 95/5 shared code to a realistic 80/20 split:
- **80% Shared**: Models, Services, ViewModels, Assets (icons)
- **20% Platform-specific**: Views, Controls, Platform services

## Problem Statement

The current approach tried to share Views (XAML) between Desktop and iOS. This failed because:
1. **Touch vs Mouse**: iOS touch handling differs significantly from desktop pointer events
2. **Canvas positioning**: Absolute positioning causes hit-testing issues on mobile
3. **Drag patterns**: Desktop drag handles conflict with mobile touch gestures
4. **Buttons in sub-panels**: Commands don't execute reliably on iOS

## New Architecture

```
AgValoniaGPS3/
├── Shared/                              # 80% - Platform-agnostic
│   ├── AgValoniaGPS.Models/            # Data models (100% shared)
│   ├── AgValoniaGPS.Services/          # Business logic (100% shared)
│   ├── AgValoniaGPS.ViewModels/        # MVVM ViewModels (100% shared)
│   └── AgValoniaGPS.Views/             # SHARED ASSETS ONLY
│       └── Assets/Icons/               # PNG icons used by both platforms
│
├── Platforms/
│   ├── AgValoniaGPS.Desktop/           # 10% - Desktop-specific
│   │   ├── Views/                      # Desktop XAML (floating panels, drag)
│   │   ├── Controls/                   # OpenGLMapControl, etc.
│   │   └── ...
│   │
│   └── AgValoniaGPS.iOS/               # 10% - iOS-specific
│       ├── Views/                      # iOS XAML (touch-native, modal sheets)
│       ├── Controls/                   # SkiaMapControl
│       └── ...
```

## What Stays Shared

| Component | Location | Notes |
|-----------|----------|-------|
| Models | `Shared/AgValoniaGPS.Models/` | 100% shared |
| Services | `Shared/AgValoniaGPS.Services/` | 100% shared |
| ViewModels | `Shared/AgValoniaGPS.ViewModels/` | 100% shared |
| Icons/Assets | `Shared/AgValoniaGPS.Views/Assets/` | PNG icons referenced by both |
| Styles | TBD | May share color constants |

## What Becomes Platform-Specific

| Component | Desktop | iOS |
|-----------|---------|-----|
| MainWindow/MainView | Floating panels, Canvas positioning | Bottom tab bar, modal sheets |
| File Menu | Draggable popup panel | Modal bottom sheet |
| Field Tools | Draggable popup panel | Modal bottom sheet |
| Boundary Recording | Draggable popup panel | Full-screen modal |
| Configuration | Draggable popup panel | Settings-style navigation |
| Simulator | Inline controls | Simple slider controls |
| Map Control | OpenGLMapControl | SkiaMapControlLabs |

## iOS Design Patterns

Instead of draggable floating panels, iOS will use:
1. **Bottom Tab Bar** - Main navigation (File, Tools, Settings)
2. **Modal Sheets** - Sub-panels slide up from bottom
3. **Standard Buttons** - No custom touch handling needed
4. **No drag-to-move** - Fixed positions, native gestures

---

# Implementation Checklist

## Phase 1: Restructure Shared Views Project
- [ ] Remove all Controls/*.axaml files from `Shared/AgValoniaGPS.Views/`
- [ ] Keep only `Assets/Icons/` folder in shared Views
- [ ] Update `AgValoniaGPS.Views.csproj` to only include Assets
- [ ] Verify Desktop still builds (may need to copy controls back)

## Phase 2: Move Desktop Views Back
- [ ] Copy `LeftNavigationPanel.axaml/.cs` to Desktop project
- [ ] Copy `FileMenuPanel.axaml/.cs` to Desktop project
- [ ] Copy `FieldToolsPanel.axaml/.cs` to Desktop project
- [ ] Copy `BoundaryRecordingPanel.axaml/.cs` to Desktop project
- [ ] Copy `BoundaryPlayerPanel.axaml/.cs` to Desktop project
- [ ] Copy `ConfigurationPanel.axaml/.cs` to Desktop project
- [ ] Copy `SimulatorPanel.axaml/.cs` to Desktop project
- [ ] Copy `ToolsPanel.axaml/.cs` to Desktop project
- [ ] Copy `ViewSettingsPanel.axaml/.cs` to Desktop project
- [ ] Copy `JobMenuPanel.axaml/.cs` to Desktop project
- [ ] Update Desktop .csproj to include new Controls
- [ ] Update namespace references in Desktop controls
- [ ] Verify Desktop builds and runs correctly

## Phase 3: Create iOS MainView (Touch-Native)
- [ ] Design bottom tab bar navigation
- [ ] Create main layout without Canvas positioning
- [ ] Implement status displays (GPS, Speed, Heading)
- [ ] Add map control (SkiaMapControlLabs)
- [ ] Wire up to MainViewModel

## Phase 4: Create iOS File Menu Panel
- [ ] Create `Views/Panels/FileMenuSheet.axaml`
- [ ] Use modal bottom sheet pattern
- [ ] Add buttons: New Field, Open Field, Close Field, Previous Field
- [ ] Use shared icons: `avares://AgValoniaGPS.Views/Assets/Icons/FileNew.png` etc.
- [ ] Wire to ViewModel commands
- [ ] Add open/close animation

## Phase 5: Create iOS Field Tools Panel
- [ ] Create `Views/Panels/FieldToolsSheet.axaml`
- [ ] Use modal bottom sheet pattern
- [ ] Add buttons: Boundary, Headland, Tramlines, etc.
- [ ] Use shared icons from Views project
- [ ] Wire to ViewModel commands

## Phase 6: Create iOS Boundary Recording Panel
- [ ] Create `Views/Panels/BoundaryRecordingSheet.axaml`
- [ ] Full-screen modal (not draggable)
- [ ] Add toolbar: Delete, Import KML, Draw, Build, Drive, Accept
- [ ] Use shared icons
- [ ] Add recording controls: Play, Pause, Stop
- [ ] Wire to ViewModel commands

## Phase 7: Create iOS Configuration Panel
- [ ] Create `Views/Panels/ConfigurationSheet.axaml`
- [ ] Use iOS Settings-style grouped list
- [ ] Add configuration sections
- [ ] Use shared icons
- [ ] Wire to ViewModel

## Phase 8: Create iOS Simulator Panel
- [ ] Create `Views/Panels/SimulatorSheet.axaml`
- [ ] Simple speed/steering sliders
- [ ] Start/Stop button
- [ ] Wire to ViewModel

## Phase 9: Create iOS View Settings Panel
- [ ] Create `Views/Panels/ViewSettingsSheet.axaml`
- [ ] Toggle switches for view options
- [ ] Use shared icons
- [ ] Wire to ViewModel

## Phase 10: Final Integration
- [ ] Verify all iOS panels open/close properly
- [ ] Verify all buttons execute commands
- [ ] Verify shared icons display correctly
- [ ] Test on iOS Simulator
- [ ] Verify Desktop still works unchanged

---

## Icon Reference (Shared)

All icons are in `avares://AgValoniaGPS.Views/Assets/Icons/`

| Icon | File |
|------|------|
| File Menu | fileMenu.png |
| New Field | FileNew.png |
| Open Field | FileOpen.png |
| Close Field | FileClose.png |
| Previous Field | FilePrevious.png |
| Existing Field | FileExisting.png |
| Field Tools | FieldTools.png |
| Boundary | Boundary.png |
| Boundary Record | BoundaryRecord.png |
| Settings | Settings48.png |
| Trash | Trash.png |
| OK | OK64.png |
| Cancel | Cancel64.png |
| Play | boundaryPlay.png |
| Pause | boundaryPause.png |
| Stop | boundaryStop.png |

---

## Notes

- ViewModels remain 100% shared - same commands work on both platforms
- Only the Views (XAML) differ per platform
- iOS uses standard touch patterns, no custom gesture handling
- Desktop keeps existing drag-and-drop functionality
- Both platforms use the same shared icon assets
