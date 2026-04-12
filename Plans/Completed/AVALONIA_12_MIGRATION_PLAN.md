# Avalonia 12 Migration Plan

## Context

Avalonia 12 released April 7, 2026 with 3x Android performance improvement. Our test confirmed Android idle FPS jumps from 18 → 29-30. However, the upgrade exposed blocking issues with ReactiveUI's threading model under Avalonia 12's multi-dispatcher architecture.

**Decision**: Replace ReactiveUI with CommunityToolkit.MVVM as part of this migration. This eliminates the threading crash, removes the Avalonia-ReactiveUI version coupling, and reduces the dependency stack.

### Dependencies Being Removed
- `Avalonia.ReactiveUI` / `ReactiveUI.Avalonia` — replaced by `CommunityToolkit.Mvvm`
- `ReactiveUI` (transitive) — no longer needed
- `System.Reactive` (transitive) — no longer needed
- `Avalonia.Labs.Controls` — unused
- `Avalonia.Labs.Gif` — replaced by static Image (3 usages)
- `Avalonia.Diagnostics` — replaced by `AvaloniaUI.DiagnosticsSupport`

### Why CommunityToolkit.MVVM Over ReactiveUI

| Factor | ReactiveUI | CommunityToolkit.MVVM |
|--------|-----------|----------------------|
| **Avalonia 12 compat** | Ongoing pain — namespace changes, dispatcher crashes, package renamed twice | No Avalonia-specific package — framework agnostic |
| **Boilerplate** | Manual `RaiseAndSetIfChanged` for every property (614 instances) | `[ObservableProperty]` attribute — source generator writes it |
| **Commands** | `ReactiveCommand.Create(() => { })` in constructor | `[RelayCommand]` attribute on method — no constructor wiring |
| **Threading** | Complex Rx scheduler model, multi-dispatcher crashes on Android | Standard `ICommand` — no threading assumptions |
| **Dependencies** | Pulls in System.Reactive, ReactiveUI, ReactiveUI.Avalonia | Single NuGet package, no Rx dependency |
| **Learning curve** | Steep (Rx concepts even when unused) | Minimal — standard MVVM patterns |
| **Maintenance** | ReactiveUI.Avalonia team must track each Avalonia release | Microsoft maintained, no Avalonia coupling |
| **Performance** | Rx overhead for simple property changes | Source-generated, zero reflection |
| **Contributor impact** | Must understand Rx to modify commands/properties | Standard C# patterns — lower barrier |

### ReactiveUI Usage Audit (April 7, 2026)

| Feature | Count | Replacement |
|---------|-------|-------------|
| `ReactiveCommand.Create` | 469 | `[RelayCommand]` attribute |
| `RaiseAndSetIfChanged` | 614 | `[ObservableProperty]` or `SetProperty()` |
| `: ReactiveObject` | 31 classes | `: ObservableObject` |
| `WhenAnyValue` | 4 (WizardViewModel only) | `[RelayCommand(CanExecute=...)]` |
| `WhenActivated` | 0 | N/A |
| Complex Rx pipelines | 0 | N/A |
| Subjects/BehaviorSubjects | 0 | N/A |

Usage is shallow — ReactiveUI is serving as a fancy INotifyPropertyChanged + ICommand library.

## Root Cause Analysis

### Threading Crash (Eliminated by CommunityToolkit switch)
ReactiveUI's `ReactiveCommand` uses Rx schedulers for `CanExecuteChanged` notifications. In Avalonia 12's multi-dispatcher model, these fire on the wrong thread → crash. CommunityToolkit's `RelayCommand` uses standard `ICommand` with no scheduler dependency — no threading issue.

### FPS Drops with Field Loaded
Coverage bitmap updates (`UpdateCoverageBitmapIncremental`, LOD rebuilds) run synchronously inside `Render()`. On Avalonia 12's new compositor, this blocks the render pipeline.

**Fix**: Move bitmap updates to background thread, post dirty flag to UI thread.

### ANR Dialogs
Same root cause as FPS — heavy work on UI thread blocks Android's message queue.

## Phased Approach

### Phase 1: CommunityToolkit.MVVM Migration (On Avalonia 11) ✅ Done

Do this first on `develop`. It works on Avalonia 11 and eliminates the ReactiveUI blocker for Avalonia 12.

#### 1A: Add CommunityToolkit.MVVM Package

Add `CommunityToolkit.Mvvm` to ViewModels and Models projects. Keep ReactiveUI temporarily — migrate incrementally.

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
```

#### 1B: Migrate Model Classes (24 classes)

Convert `ReactiveObject` → `ObservableObject`, `RaiseAndSetIfChanged` → `SetProperty` or `[ObservableProperty]`.

**Files** in `Shared/AgValoniaGPS.Models/Configuration/` and `Shared/AgValoniaGPS.Models/State/`:
- VehicleConfig, ToolConfig, GuidanceConfig, AutoSteerConfig, DisplayConfig
- HotkeyConfig, NtripConfig, UDPConfig
- GuidanceState, FieldState, VehicleState, UIState, RecordedPathState
- ConfigurationStore

**Pattern**:
```csharp
// Before (ReactiveUI)
public class VehicleConfig : ReactiveObject
{
    private double _antennaHeight;
    public double AntennaHeight
    {
        get => _antennaHeight;
        set => this.RaiseAndSetIfChanged(ref _antennaHeight, value);
    }
}

// After (CommunityToolkit — source generator)
public partial class VehicleConfig : ObservableObject
{
    [ObservableProperty]
    private double _antennaHeight;
}
```

Note: `[ObservableProperty]` generates the public property, PropertyChanged, and PropertyChanging automatically. The class must be `partial`.

#### 1C: Migrate ViewModel Commands (7 ViewModel classes)

Convert `ReactiveCommand` → `[RelayCommand]` attributes.

**Files** in `Shared/AgValoniaGPS.ViewModels/`:
- `MainViewModel.cs` + all `MainViewModel.Commands.*.cs` partials (~469 commands)
- `ConfigurationViewModel.cs`
- `AutoSteerConfigViewModel.cs`
- `Wizards/WizardViewModel.cs` (most complex — has canExecute)

**Pattern for simple commands** (vast majority):
```csharp
// Before
public ReactiveCommand<Unit, Unit> OpenFieldCommand { get; private set; }
// In constructor:
OpenFieldCommand = ReactiveCommand.Create(() => State.UI.ShowDialog(DialogType.FieldSelection));

// After
[RelayCommand]
private void OpenField() => State.UI.ShowDialog(DialogType.FieldSelection);
```

**Pattern for async commands**:
```csharp
// Before
SaveCommand = ReactiveCommand.CreateFromTask(async () => { ... });

// After
[RelayCommand]
private async Task Save() { ... }
```

**Pattern for commands with canExecute** (4 in WizardViewModel):
```csharp
// Before
var canGoNext = this.WhenAnyValue(x => x.CanGoNext).ObserveOn(RxApp.MainThreadScheduler);
NextCommand = ReactiveCommand.CreateFromTask(GoNextAsync, canGoNext);

// After
[RelayCommand(CanExecute = nameof(CanGoNext))]
private async Task GoNext() { ... }
// Call NotifyCanExecuteChanged() when CanGoNext changes
```

#### 1D: Update AXAML Command Bindings

AXAML bindings change slightly — ReactiveCommand properties are typically named `SomeCommand`, while `[RelayCommand]` generates `SomeCommand` from a method named `Some`. Verify naming matches.

If the method is `OpenField()`, the generated command is `OpenFieldCommand` — same convention we already use. **Most AXAML should need zero changes.**

#### 1E: Remove ReactiveUI Packages

After all classes are migrated:
- Remove `ReactiveUI` from ViewModels csproj
- Remove `Avalonia.ReactiveUI` from all platform csprojs
- Remove `using ReactiveUI` from all files
- Remove `.UseReactiveUI()` from Program.cs and AppDelegate.cs

### Phase 2: Compiled Bindings & Rendering ✅ Done

#### 2A: Fix Compiled Bindings in Views Project

Enable `AvaloniaUseCompiledBindingsByDefault` in Views csproj. Fix ~8 AXAML files missing `x:DataType`:

| File | Issue |
|------|-------|
| `AgShareDownloadDialogPanel.axaml` | Missing DataType in DataTemplate |
| `AgShareUploadDialogPanel.axaml` | Missing DataType in DataTemplate |
| `AutoSteerConfigPanel.axaml` | Missing EditSidehillCompensationCommand |
| `ConfigurationDialog.axaml` | Missing IsMetric property |
| `NewFieldDialogPanel.axaml` | Missing x:DataType |
| `NumericStepView.axaml` | Missing x:DataType |
| `HelpDialogPanel.axaml` | Missing x:DataType |
| `LanguageDialogPanel.axaml` | Missing x:DataType |

#### 2B: Move Coverage Bitmap Updates Off UI Thread

Current: `UpdateCoverageBitmapIfNeeded()` runs inside `Render()` → blocks render.

Change to:
1. `MarkCoverageDirty()` sets a flag
2. Background task picks up the flag, does the bitmap update
3. When done, `Dispatcher.UIThread.Post(() => InvalidateVisual())`
4. LOD rebuilds also move to background

**File**: `Shared/AgValoniaGPS.Views/Controls/DrawingContextMapControl.cs`

### Phase 3: Avalonia 12 Package Swap ✅ Done

With ReactiveUI gone and compiled bindings working, this phase is mechanical.

#### 3A: Package Updates
- All Avalonia packages: 11.3.x → 12.0.0
- `Avalonia.Diagnostics` → `AvaloniaUI.DiagnosticsSupport` 2.2.0
- NUnit: 4.3.2 → 4.5.1
- Remove `Avalonia.Labs.Controls` and `Avalonia.Labs.Gif`
- Remove explicit SkiaSharp reference from iOS

#### 3B: Code Changes
- Remove `DisableAvaloniaDataAnnotationValidation()` from Desktop and iOS (binding plugins removed in Av12)
- `new Binding(...)` → `new ReflectionBinding(...)` in LocalizeExtension.cs
- GifImage → static Image in ToolTimingSubTab.axaml

#### 3C: Android Init Migration
- `AvaloniaMainActivity<App>` → `AvaloniaMainActivity` (non-generic)
- New `AndroidApp.cs` with `AvaloniaAndroidApplication<App>`
- `ISingleViewApplicationLifetime` → `IActivityApplicationLifetime` + `MainViewFactory`
- ViewModel creation stays in `OnFrameworkInitializationCompleted` (UI thread), factory just wraps it

#### 3D: DispatcherTimer Audit
Verify all timers created on UI thread. In Avalonia 12, `DispatcherTimer` binds to creating thread's dispatcher.

### Phase 4: Validation ✅ Done

#### Platform Testing Matrix

| Test | Desktop | iPad | Android |
|------|---------|------|---------|
| App launches | ✅ | ✅ | ✅ |
| All buttons/dialogs work | ✅ | ✅ | ✅ |
| Simulator runs | ✅ | ✅ | ✅ |
| Field load/close | ✅ | ✅ | ✅ |
| Coverage painting FPS | ✅ 60 | ✅ 20-22 | ✅ 11 |
| Charts show live data | ✅ | ✅ | ✅ |
| AutoSteer guidance | ✅ | ✅ | ✅ |
| Day/Night toggle | ✅ | ✅ | ✅ |
| Zoom all levels | ✅ | ✅ | ✅ |

#### FPS Results (Compositor frames, actual rendered)

| Scenario | Desktop (M4) | iPad Pro 2nd gen | Android |
|----------|-------------|-----------------|---------|
| Idle | 60 | 20-22 | 11 |
| Painting coverage | 60 | 20-22 | 11 |
| 490-acre field zoomed out | 60 | — | — |

## File Change Summary

| Phase | Files | Risk | Can Revert |
|-------|-------|------|-----------|
| 1A | 2 csproj | None | Yes |
| 1B | ~24 model classes | Low — mechanical | Yes |
| 1C | ~7 ViewModel files | Medium — many commands | Yes |
| 1D | Verify AXAML only | None | N/A |
| 1E | ~10 csproj + cs files | Low — remove packages | Yes |
| 2A | ~8 AXAML + 1 csproj | Low | Yes |
| 2B | DrawingContextMapControl.cs | Medium | Yes |
| 3A-D | ~15 files | Low — mostly done on branch | Yes |

## Key Principles

1. **Phase 1 (CommunityToolkit migration) is the critical path.** It eliminates the ReactiveUI threading blocker and can be done safely on Avalonia 11.
2. **Migrate incrementally** — one ViewModel at a time, test after each.
3. **Phase 3 becomes trivial** — just package versions + namespace cleanup.
4. **No Rx knowledge needed going forward** — simpler onboarding for contributors.
