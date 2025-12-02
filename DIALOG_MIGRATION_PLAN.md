# Dialog Migration Plan: Window-based to Shared Panel-based

## Goal
Convert Desktop's Window-based dialogs to shared UserControl-based panels that work on both iOS and Desktop.

## Architecture

### Current State
- **Desktop**: Uses `Window`-based dialogs in `Platforms/AgValoniaGPS.Desktop/Views/`
  - Opened via `IDialogService` returning `Task<Result>`
  - Not compatible with iOS (no Window support)

- **iOS**: Has stub `IDialogService` returning null for all methods
  - Buttons that depend on dialogs don't work

### Target State
- **Shared panels** in `Shared/AgValoniaGPS.Views/Controls/Dialogs/`
  - `UserControl`-based overlays with visibility bindings
  - Work on both iOS and Desktop
  - Desktop can optionally keep Window dialogs or migrate to panels

## Migration Status

### Completed
- [x] `SimCoordsDialogPanel` - Set simulator coordinates
- [x] `FieldSelectionDialogPanel` - Open existing field
- [x] `NewFieldDialogPanel` - Create new field

### In Progress
- [ ] `FromExistingFieldDialogPanel` - Copy from existing field
- [ ] `KmlImportDialogPanel` - Import from KML file
- [ ] `IsoXmlImportDialogPanel` - Import from ISO-XML
- [ ] `BoundaryDialogPanel` - Record/edit boundary (may need platform-specific map)

### Desktop-Only (no iOS equivalent needed)
- `BrowserMapDialog` - Uses Mapsui (desktop only)
- `MapsuiBoundaryDialog` - Uses Mapsui (desktop only)
- `AgShareUploadDialog` - Cloud sync (future)
- `AgShareDownloadDialog` - Cloud sync (future)
- `AgShareSettingsDialog` - Cloud sync (future)
- `DataIODialog` - Desktop file operations

## Pattern for Each Dialog

### 1. ViewModel Properties (in MainViewModel.cs)
```csharp
// Visibility flag
private bool _isXxxDialogVisible;
public bool IsXxxDialogVisible
{
    get => _isXxxDialogVisible;
    set => this.RaiseAndSetIfChanged(ref _isXxxDialogVisible, value);
}

// Dialog-specific data properties
private string _xxxFieldName = string.Empty;
public string XxxFieldName { get => ...; set => ...; }

// Commands
public ICommand? CancelXxxDialogCommand { get; private set; }
public ICommand? ConfirmXxxDialogCommand { get; private set; }
```

### 2. Command Implementation (in InitializeCommands)
```csharp
ShowXxxDialogCommand = new RelayCommand(() =>
{
    // Initialize dialog data
    XxxFieldName = "";
    IsXxxDialogVisible = true;
});

CancelXxxDialogCommand = new RelayCommand(() =>
{
    IsXxxDialogVisible = false;
});

ConfirmXxxDialogCommand = new RelayCommand(() =>
{
    // Do the work
    IsXxxDialogVisible = false;
    IsJobMenuPanelVisible = false; // Close parent menu too
});
```

### 3. Panel AXAML (in Shared/AgValoniaGPS.Views/Controls/Dialogs/)
```xml
<UserControl ...
    IsVisible="{Binding IsXxxDialogVisible}"
    IsHitTestVisible="{Binding IsXxxDialogVisible}">

    <Grid>
        <!-- Backdrop -->
        <Border Background="#80000000" PointerPressed="Backdrop_PointerPressed"/>

        <!-- Dialog content -->
        <Border Background="#2C3E50" CornerRadius="12" ...>
            <!-- Content here -->
        </Border>
    </Grid>
</UserControl>
```

### 4. Add to MainView
```xml
<!-- Modal Dialog Overlays (rendered on top of everything) -->
<dialogs:SimCoordsDialogPanel/>
<dialogs:FieldSelectionDialogPanel/>
<dialogs:NewFieldDialogPanel/>
<!-- etc -->
```

## File Locations

| Component | Location |
|-----------|----------|
| Shared Panels | `Shared/AgValoniaGPS.Views/Controls/Dialogs/` |
| ViewModel | `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` |
| iOS MainView | `Platforms/AgValoniaGPS.iOS/Views/MainView.axaml` |
| Desktop MainView | `Platforms/AgValoniaGPS.Desktop/Views/MainView.axaml` |

## Notes

- Desktop can continue using Window dialogs OR switch to shared panels
- Shared panels use MainViewModel directly (no separate dialog ViewModels)
- All dialog state is managed via ViewModel properties
- Backdrop click dismisses dialog (calls Cancel command)
