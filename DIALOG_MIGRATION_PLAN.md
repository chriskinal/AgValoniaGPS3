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
- [x] `NewFieldDialogPanel` - Create new field (with TextBox dark theme fix)
- [x] `FromExistingFieldDialogPanel` - Copy from existing field (with toggle buttons for copy options)
- [x] `KmlImportDialogPanel` - Import from KML file (scans Documents/AgValoniaGPS/Import for *.kml files)
- [x] `IsoXmlImportDialogPanel` - Import from ISO-XML (scans Import folder for TASKDATA.xml directories)
- [x] `BoundaryMapDialogPanel` - Draw boundary on satellite map using Mapsui (Esri World Imagery tiles)

### In Progress
- [x] `BoundaryRecordingPanel` - Drive-around boundary recording (completed)
- [x] `BoundaryPlayerPanel` - Boundary playback/editing (completed)
- [ ] `BuildFromTracksDialogPanel` - Build boundary from AB lines/curves (blocked - needs BoundaryBuilder service, see MISSING_SERVICES.md)

### Pending (All needed for iOS/Android tablet replacement)
- [ ] `BrowserMapDialogPanel` - Map browser for field location
- [x] `AgShareUploadDialogPanel` - Cloud sync upload
- [x] `AgShareDownloadDialogPanel` - Cloud sync download
- [x] `AgShareSettingsDialogPanel` - Cloud sync configuration (with AlphanumericKeyboardPanel)
- [ ] `DataIODialogPanel` - Data import/export operations

### Shared Controls Created
- `AlphanumericKeyboardPanel` - Reusable QWERTY keyboard for text input (URL, API keys, etc.)

### Notes on Mobile Map Dialogs
**UPDATE**: Mapsui.Avalonia 5.0.0 works on iOS with SkiaSharp 3.119.1. The BoundaryMapDialogPanel
uses Mapsui's MapControl with Esri World Imagery satellite tiles (free, no API key needed).

Key findings:
- Mapsui.Avalonia targets net8.0/net9.0 but works via Avalonia's iOS renderer
- Required SkiaSharp upgrade from 2.88.9 to 3.119.1 (Mapsui dependency)
- Uses `Mapsui.UI.Avalonia.MapControl` for map display
- SphericalMercator projection for coordinate conversion
- WritableLayer for points/polygons

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
