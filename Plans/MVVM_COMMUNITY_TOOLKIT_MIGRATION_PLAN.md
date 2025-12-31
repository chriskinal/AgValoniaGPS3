# MVVM Community Toolkit Migration Plan

## Overview
Migrate from ReactiveUI to MVVM Community Toolkit for a lighter, more modern MVVM implementation using source generators instead of IL weaving.

## Status: COMPLETE

## Why Migrate?

| Aspect | ReactiveUI (Current) | MVVM Community Toolkit |
|--------|---------------------|------------------------|
| Property change | Fody IL weaving | Source generators |
| Build complexity | Requires Fody weaver | Pure compilation |
| Package size | Heavier (Rx.NET dependency) | Lightweight |
| Learning curve | Steeper (Rx concepts) | Simpler |
| Debugging | IL weaving can obscure | Source generators are transparent |
| Maintenance | Active but complex | Microsoft-backed, simple |

## Current State Assessment

### ViewModels to Migrate
| File | Base Class | Properties | Commands |
|------|------------|------------|----------|
| `MainViewModel.cs` | `ReactiveObject` | ~170 | ~100 |
| `ConfigurationViewModel.cs` | `ReactiveObject` | ~18 | ~5 |

### ReactiveUI Usage
- `this.RaiseAndSetIfChanged()` - ~190 occurrences
- `ReactiveObject` base class - 2 classes
- No advanced Rx.NET features (`WhenAnyValue`, `ObservableAsPropertyHelper`, etc.)

### Custom Code
- `RelayCommand.cs` - Custom implementation (can be deleted after migration)

---

## Migration Phases

### Phase 1: Package Changes
**Files to modify:**
- `Shared/AgValoniaGPS.ViewModels/AgValoniaGPS.ViewModels.csproj`

**Changes:**
```xml
<!-- Remove these packages -->
<PackageReference Include="ReactiveUI" Version="20.1.1" />
<PackageReference Include="ReactiveUI.Fody" Version="19.5.41" />

<!-- Add this package -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
```

**Files to delete:**
- `Shared/AgValoniaGPS.ViewModels/FodyWeavers.xml`
- `Shared/AgValoniaGPS.ViewModels/FodyWeavers.xsd`

---

### Phase 2: Migrate ConfigurationViewModel (Pilot)

Migrate the smaller ViewModel first to validate the approach.

**File:** `Shared/AgValoniaGPS.ViewModels/ConfigurationViewModel.cs`

#### 2a. Update using statements
```csharp
// Remove
using ReactiveUI;

// Add
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
```

#### 2b. Update class declaration
```csharp
// Before
public class ConfigurationViewModel : ReactiveObject

// After
public partial class ConfigurationViewModel : ObservableObject
```

#### 2c. Convert properties (Option A - Source Generators)
```csharp
// Before
private string _someValue = string.Empty;
public string SomeValue
{
    get => _someValue;
    set => this.RaiseAndSetIfChanged(ref _someValue, value);
}

// After
[ObservableProperty]
private string _someValue = string.Empty;
// Auto-generates: public string SomeValue { get; set; }
```

#### 2c. Convert properties (Option B - Manual, less refactoring)
```csharp
// Before
set => this.RaiseAndSetIfChanged(ref _someValue, value);

// After
set => SetProperty(ref _someValue, value);
```

#### 2d. Convert commands
```csharp
// Before
public ICommand? SomeCommand { get; private set; }
SomeCommand = new RelayCommand(() => { ... });

// After (Option A - Attributes)
[RelayCommand]
private void Some()
{
    // implementation
}

// After (Option B - Manual)
public IRelayCommand SomeCommand { get; }
SomeCommand = new RelayCommand(Some);
```

---

### Phase 3: Migrate MainViewModel

Same process as Phase 2, but larger scope.

**File:** `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs`

#### Recommended approach for large file:
1. Update using statements and class declaration first
2. Use find/replace for property conversion:
   - Find: `this.RaiseAndSetIfChanged(ref `
   - Replace: `SetProperty(ref `
3. Commands can remain using manual `RelayCommand`/`AsyncRelayCommand` from toolkit
4. Optionally convert commands to `[RelayCommand]` attributes later

#### Special considerations:
- Constructor injection remains unchanged
- Event handlers remain unchanged
- Service calls remain unchanged

---

### Phase 4: Delete Custom RelayCommand

**File to delete:** `Shared/AgValoniaGPS.ViewModels/RelayCommand.cs`

**Update imports in ViewModels:**
```csharp
using CommunityToolkit.Mvvm.Input;  // Provides RelayCommand, AsyncRelayCommand
```

---

### Phase 5: Cleanup and Validation

1. Build solution and fix any compilation errors
2. Run application and test:
   - Field loading/closing
   - NTRIP connection
   - Boundary recording
   - Track guidance
   - All dialogs open/close properly
3. Verify property bindings still work in all views

---

## Find/Replace Patterns

### Property Setters (Quick Migration)
| Find | Replace |
|------|---------|
| `this.RaiseAndSetIfChanged(ref ` | `SetProperty(ref ` |

### Using Statements
| Find | Replace |
|------|---------|
| `using ReactiveUI;` | `using CommunityToolkit.Mvvm.ComponentModel;` |

### Base Class
| Find | Replace |
|------|---------|
| `: ReactiveObject` | `: ObservableObject` |
| `public class ` (in ViewModel) | `public partial class ` |

---

## Command Type Mapping

| ReactiveUI / Custom | Community Toolkit |
|--------------------|-------------------|
| `RelayCommand` | `RelayCommand` |
| `RelayCommand<T>` | `RelayCommand<T>` |
| `AsyncRelayCommand` | `AsyncRelayCommand` |
| `ReactiveCommand` | `RelayCommand` or `AsyncRelayCommand` |
| `ICommand` | `IRelayCommand` or `IAsyncRelayCommand` |

---

## Rollback Plan

If issues arise:
1. Revert package changes in `.csproj`
2. Restore `FodyWeavers.xml` and `FodyWeavers.xsd`
3. Revert ViewModel changes
4. Restore `RelayCommand.cs` if deleted

Git makes this easy - create a feature branch before starting.

---

## Estimated Effort

| Phase | Time Estimate |
|-------|---------------|
| Phase 1: Package changes | 15 minutes |
| Phase 2: ConfigurationViewModel | 30 minutes |
| Phase 3: MainViewModel | 2-3 hours |
| Phase 4: Delete RelayCommand | 10 minutes |
| Phase 5: Testing | 1-2 hours |
| **Total** | **4-6 hours** |

---

## References

- [MVVM Community Toolkit Documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [MVVM Toolkit Samples](https://github.com/CommunityToolkit/MVVM-Samples)
- [Migration from other frameworks](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/migratingfrommvvmlight)

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| | | |

---

## Checklist

- [x] Create feature branch `feature/mvvm-toolkit-migration`
- [x] Phase 1: Update packages
- [x] Phase 2: Migrate ConfigurationViewModel
- [x] Phase 3: Migrate MainViewModel
- [x] Phase 4: Delete RelayCommand.cs
- [x] Phase 5: Build succeeded
- [ ] Full application testing
- [ ] Merge to master
