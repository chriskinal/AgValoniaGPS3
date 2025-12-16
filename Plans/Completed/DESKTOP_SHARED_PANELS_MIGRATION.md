# Desktop Migration to Shared Panels

## Status: COMPLETED ✓

Migration completed on 2025-12-04.

## Overview
Migrate Desktop to use shared panels and dialogs from `Shared/AgValoniaGPS.Views/Controls/` instead of desktop-only implementations.

## Results

### Code Distribution (After Migration)
| Category | Lines | Percentage |
|----------|-------|------------|
| **Shared** | 28,101 | **91.5%** |
| Desktop | 1,778 | 5.7% |
| iOS | 824 | 2.6% |

**Target achieved**: Both platforms are now under 6% platform-specific code.

### Lines Removed
- **8,443 lines deleted** from Desktop platform
- 14 legacy dialog files removed
- OpenGL and Skia map controls removed
- DialogService simplified to stub methods

---

## What Was Done

### Phase 1: Add Shared Panels to MainWindow ✓
- Changed namespaces from `AgValoniaGPS.Desktop.Controls.Panels` to `AgValoniaGPS.Views.Controls.Panels`
- Added `xmlns:dialogs` for shared dialog panels
- Added all 12 shared dialog overlays to MainWindow.axaml

### Phase 2: Remove Legacy Desktop Code ✓

**Deleted Dialog Files (14 dialogs):**
- AgShareDownloadDialog.axaml/.cs
- AgShareSettingsDialog.axaml/.cs
- AgShareUploadDialog.axaml/.cs
- AlphanumericKeyboard.axaml/.cs
- BrowserMapDialog.axaml/.cs
- DataIODialog.axaml/.cs
- FieldSelectionDialog.axaml/.cs
- FromExistingFieldDialog.axaml/.cs
- IsoXmlImportDialog.axaml/.cs
- KmlImportDialog.axaml/.cs
- MapsuiBoundaryDialog.axaml/.cs
- NewFieldDialog.axaml/.cs
- OnScreenKeyboard.axaml/.cs
- SimCoordsDialog.axaml/.cs

**Deleted Map Controls:**
- OpenGLMapControl.cs (1,800 lines)
- SkiaMapControl.cs (824 lines)
- IMapControl.cs (16 lines)

**Simplified Services:**
- DialogService.cs - Now stubs most methods (dialogs handled by ViewModel commands)
- MapService.cs - Removed reference to deleted controls

### Phase 3: Verify Build and Test ✓
- Both iOS and Desktop build successfully
- Desktop uses shared DrawingContextMapControl at 30 FPS
- All shared dialog overlays work on Desktop

---

## Current Desktop Structure

```
Platforms/AgValoniaGPS.Desktop/
├── App.axaml / App.axaml.cs        # App entry point
├── Program.cs                       # Main entry
├── ViewLocator.cs                   # View resolution
├── Converters/
│   └── BoolToColorConverter.cs     # UI converter
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs  # DI setup
├── Services/
│   ├── DialogService.cs            # Simplified (stubs)
│   └── MapService.cs               # Wraps shared map control
└── Views/
    ├── MainWindow.axaml            # Main UI with shared panels
    └── MainWindow.axaml.cs         # Code-behind
```

Total: ~1,778 lines (5.7% of codebase)

---

## Notes
- Desktop and iOS now share the same UI panels and dialogs
- DrawingContextMapControl runs at 30 FPS on both platforms
- ARM64 Mac simulator runs efficiently (93% idle at 30 FPS)
- Intel Mac simulator requires lower FPS (~10) due to emulation overhead
