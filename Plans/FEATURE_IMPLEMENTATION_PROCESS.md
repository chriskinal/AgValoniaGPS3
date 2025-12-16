# Feature Implementation Process

This document outlines the standard process for implementing UI features in AgValoniaGPS by studying the WinForms reference implementation.

## Overview

AgValoniaGPS3 is built on services ported from AgOpenGPS.Core. When implementing UI features, we need to:
1. Understand how WinForms wires up the UI to the core services
2. Replicate that wiring in our MVVM architecture

## Reference Locations

| Component | Location |
|-----------|----------|
| WinForms UI | `/Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/Forms/` |
| AgOpenGPS.Core Services | `/Users/chris/Code/AgValoniaGPS2/SourceCode/AgOpenGPS.Core/` |
| AgValoniaGPS Services | `/Users/chris/Code/AgValoniaGPS3/Shared/AgValoniaGPS.Services/` |
| AgValoniaGPS ViewModels | `/Users/chris/Code/AgValoniaGPS3/Shared/AgValoniaGPS.ViewModels/` |
| AgValoniaGPS Views | `/Users/chris/Code/AgValoniaGPS3/Shared/AgValoniaGPS.Views/` |

## Implementation Process

### Step 1: Identify the Feature

- **UI Element**: Which button/control are we wiring up?
- **Icon Name**: What is the icon file name? (helps locate WinForms code)
- **Expected Behavior**: What should happen when activated?

### Step 2: Study WinForms Implementation

1. **Find the UI handler** in WinForms:
   ```bash
   # Search for icon name or button name
   grep -r "IconName" /Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/Forms/

   # Search for click handlers
   grep -r "btnFeatureName_Click" /Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/Forms/
   ```

2. **Trace the code path**:
   - UI event handler (e.g., `btnYouTurn_Click`)
   - Any intermediate logic (toggle states, validation)
   - Call to core service method

3. **Document key findings**:
   - What state variables are used?
   - What service methods are called?
   - What parameters are passed?
   - What side effects occur (UI updates, other state changes)?

### Step 3: Design Avalonia-Idiomatic Solution

Before implementing, research how to recreate the AgOpenGPS functionality using native Avalonia patterns. **We are replicating functionality, not copying the WinForms UI directly.**

1. **Identify the UI pattern needed**:
   - Is it a tabbed interface? → Use `TabControl`
   - Is it a list with selection? → Use `ListBox` with `ItemTemplate`
   - Is it a dialog with multiple pages? → Use `Carousel` or `TabControl`
   - Is it a toolbar? → Use `ToolBar` or styled `ItemsControl`

2. **Research Avalonia controls**:
   - Check [Avalonia documentation](https://docs.avaloniaui.net/)
   - Look at built-in controls before creating custom ones
   - Consider `ContentControl` with `DataTemplate` for dynamic content

3. **Avoid anti-patterns**:
   - ❌ Manual `IsVisible` bindings to simulate tabs
   - ❌ ViewLocator (not trim-safe)
   - ❌ Code-behind for logic that belongs in ViewModel
   - ❌ Hardcoded colors/styles (use Styles and resources)
   - ✅ Use `TabControl`, `ListBox`, `ItemsControl` with templates
   - ✅ Use `DataTemplate` for repeated UI patterns
   - ✅ Use Styles for consistent theming

4. **Cross-platform considerations**:
   - Avoid platform-specific APIs in shared code
   - Test touch interactions for iOS/tablet use
   - Consider different screen sizes

### Step 4: Verify Service Availability

1. **Check if the service exists** in AgValoniaGPS.Services:
   ```bash
   ls Shared/AgValoniaGPS.Services/
   ```

2. **Compare service methods** between AgOpenGPS.Core and AgValoniaGPS.Services:
   - Are all required methods present?
   - Are method signatures identical?
   - Are any input/output types different?

3. **Check DI registration** in both platforms:
   - `Platforms/AgValoniaGPS.Desktop/DependencyInjection/ServiceCollectionExtensions.cs`
   - `Platforms/AgValoniaGPS.iOS/DependencyInjection/ServiceCollectionExtensions.cs`

### Step 5: Create Implementation Plan

Create a markdown file in `/docs/` documenting:

```markdown
# Feature: [Feature Name]

## WinForms Reference
- File: `GPS/Forms/FormXxx.cs`
- Handler: `btnFeature_Click`
- Service calls: `ServiceName.MethodName(params)`

## State Variables Required
- `_isFeatureEnabled` (bool)
- `_featureCounter` (int)
- etc.

## Service Dependencies
- `IServiceName` - method1(), method2()

## Implementation Steps
1. [ ] Add state properties to MainViewModel
2. [ ] Add command for button
3. [ ] Wire up service calls
4. [ ] Add UI bindings
5. [ ] Test

## Input Building
Document how to build input objects for service methods:
- What boundaries/polygons are needed?
- What coordinate systems?
- What delegates/callbacks?

## Expected Behavior
- When button clicked: ...
- Visual feedback: ...
- State changes: ...
```

### Step 6: Implement in AgValoniaGPS

1. **ViewModel changes** (`MainViewModel.cs`):
   - Add backing fields for state
   - Add public properties with PropertyChanged
   - Add ICommand for the action
   - Inject required services
   - Implement command handler

2. **View changes** (if needed):
   - Add button bindings
   - Add visibility bindings
   - Add any new UI elements

3. **Service wiring**:
   - Build input objects correctly (this is often where bugs occur!)
   - Call service methods
   - Handle output/results
   - Update UI state

### Step 7: Build and Test

1. **Build**:
   ```bash
   dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
   ```

2. **Run**:
   ```bash
   dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
   ```

3. **Test scenarios**:
   - Happy path
   - Edge cases (no boundary, no track, etc.)
   - State transitions
   - Visual feedback

4. **Check console output** for debug messages

### Step 8: Debug Common Issues

#### Service returns unexpected results
- **Check input building**: Are boundaries/polygons in correct format?
- **Check delegates**: Are callback functions returning correct values?
- **Compare with WinForms**: Add logging to see what WinForms passes

#### UI not updating
- **Check PropertyChanged**: Is it being raised?
- **Check bindings**: Are property names correct?
- **Check thread**: Is update on UI thread?

#### Service method not found
- **Check DI registration**: Is service registered?
- **Check interface**: Does interface have the method?

## Common Patterns

### Building Boundary Inputs

WinForms often passes boundaries in specific formats. Check:
- `List<Vec3>` with headings calculated
- Outer boundary vs inner boundaries (holes)
- Clockwise vs counter-clockwise winding

### Point-in-Polygon Delegates

Many services use `Func<Vec3, int>` delegates for boundary testing:
- Return `0` = point is valid/inside working area
- Return `!= 0` = point is outside/in turn area/invalid

### State Machine Patterns

Features like YouTurn have phases:
1. Idle
2. Triggered (path created)
3. Active (following path)
4. Complete (reached end)

Track state carefully and handle transitions.

## MVVM Best Practices

Strictly adhere to the MVVM pattern throughout the codebase:

### View (XAML)
- **DO**: Bind to ViewModel properties and commands
- **DO**: Use `DataTemplate` for dynamic content
- **DO**: Use Styles for visual consistency
- **DON'T**: Put business logic in code-behind
- **DON'T**: Directly manipulate ViewModel from code-behind (except initialization)

### ViewModel
- **DO**: Expose properties with `INotifyPropertyChanged` (use ReactiveUI's `RaiseAndSetIfChanged`)
- **DO**: Use `ICommand` (RelayCommand) for user actions
- **DO**: Inject services via constructor (DI)
- **DO**: Keep ViewModels testable (no direct UI references)
- **DON'T**: Reference Views or UI controls
- **DON'T**: Use `MessageBox` or dialogs directly (use `IDialogService`)

### Model/Services
- **DO**: Keep business logic in services
- **DO**: Define interfaces for all services
- **DO**: Register services in DI container
- **DON'T**: Put UI logic in services

### Data Binding Patterns
```xml
<!-- Property binding -->
<TextBlock Text="{Binding PropertyName}"/>

<!-- Command binding -->
<Button Command="{Binding DoSomethingCommand}"/>

<!-- Two-way binding for inputs -->
<TextBox Text="{Binding InputValue, Mode=TwoWay}"/>

<!-- Binding with converter -->
<Image Source="{Binding ImagePath, Converter={x:Static conv:StringToImageConverter.Instance}}"/>

<!-- Collection binding with template -->
<ItemsControl ItemsSource="{Binding Items}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}"/>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### Separation of Concerns
```
User Action → View (Button) → ViewModel (Command) → Service (Logic) → Model (Data)
                    ↓                    ↓
              UI Binding          Property Changed
                    ↓                    ↓
               View Updates      ViewModel Updates
```

## Example: YouTurn Implementation

See `/docs/AB_PANELS_IMPLEMENTATION.md` for a detailed example of implementing YouTurn functionality, including:
- WinForms reference code
- Service method signatures
- Input building (especially `IsPointInsideTurnArea` delegate)
- State management
