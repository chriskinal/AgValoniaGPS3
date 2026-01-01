# .NET Best Practices Implementation Plan

## Overview
Modernize the codebase to follow Microsoft's recommended .NET and MVVM best practices. All changes are cross-platform compatible.

## Status: COMPLETE (Phases 1, 2, 3 & 5 Done, Phase 4 Skipped)

---

## Phase 1: Structured Logging with Microsoft.Extensions.Logging

### Why
- 180+ `Console.WriteLine` calls scattered throughout codebase
- No log levels (debug vs info vs error)
- No structured data (can't query/filter logs)
- Console output not suitable for production

### Implementation Steps

#### 1.1 Add NuGet Package
Add to `Shared/AgValoniaGPS.Services/AgValoniaGPS.Services.csproj`:
```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
```

Add to platform projects for concrete providers:
```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="9.0.0" />
```

#### 1.2 Update DI Registration (each platform)
```csharp
// In ServiceCollectionExtensions.cs
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
    builder.SetMinimumLevel(LogLevel.Debug); // Or configure per environment
});
```

#### 1.3 Inject ILogger<T> into Services
```csharp
// Before
public class YouTurnCreationService : IYouTurnCreationService
{
    public YouTurnCreationService() { }

    public void CreateTurn()
    {
        Console.WriteLine($"[YouTurn] Creating turn...");
    }
}

// After
public class YouTurnCreationService : IYouTurnCreationService
{
    private readonly ILogger<YouTurnCreationService> _logger;

    public YouTurnCreationService(ILogger<YouTurnCreationService> logger)
    {
        _logger = logger;
    }

    public void CreateTurn()
    {
        _logger.LogDebug("Creating turn...");
    }
}
```

#### 1.4 Log Level Guidelines
| Level | Use For |
|-------|---------|
| `LogTrace` | Verbose diagnostic info (GPS coordinates every frame) |
| `LogDebug` | Development debugging (turn calculations, state changes) |
| `LogInformation` | Normal operations (field loaded, NTRIP connected) |
| `LogWarning` | Recoverable issues (GPS signal lost, reconnecting) |
| `LogError` | Errors with exceptions |
| `LogCritical` | Application crashes |

#### 1.5 Replace Console.WriteLine Calls
Use find/replace patterns:
| Find Pattern | Replace With |
|--------------|--------------|
| `Console.WriteLine($"[YouTurn]` | `_logger.LogDebug(` |
| `Console.WriteLine($"[GPS]` | `_logger.LogDebug(` |
| `Console.WriteLine($"Error:` | `_logger.LogError(` |

#### 1.6 Files to Modify
- All services in `Shared/AgValoniaGPS.Services/`
- `MainViewModel.cs` (inject logger)
- Platform DI setup files

### Estimated Effort: 2-3 hours

---

## Phase 2: Convert async void to async Task

### Why
- `async void` methods can't be awaited
- Exceptions in `async void` crash the app (unobserved)
- Makes testing difficult

### Current Occurrences
```
MainViewModel.cs: InitializeAsync(), StartHelloTimer()
BoundaryMapDialogPanel.axaml.cs: BtnSave_Click()
AgShareSettingsDialogPanel.axaml.cs: TestConnection_Click()
AgShareDownloadDialogPanel.axaml.cs: Refresh_Click(), DownloadAll_Click(), Download_Click()
AgShareUploadDialogPanel.axaml.cs: Upload_Click()
AlphanumericKeyboardPanel.axaml.cs: OnPasteClick()
```

### Implementation Steps

#### 2.1 Event Handlers (Keep async void but wrap)
Event handlers must remain `async void` but should have try/catch:
```csharp
// Before
private async void BtnSave_Click(object? sender, RoutedEventArgs e)
{
    await SaveAsync();
}

// After
private async void BtnSave_Click(object? sender, RoutedEventArgs e)
{
    try
    {
        await SaveAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Save failed");
        // Show error to user
    }
}
```

#### 2.2 Non-Event Methods (Change to async Task)
```csharp
// Before
private async void InitializeAsync()
{
    await LoadDataAsync();
}

// Call site
InitializeAsync(); // Fire and forget, exceptions lost

// After
private async Task InitializeAsync()
{
    await LoadDataAsync();
}

// Call site - option 1: await in constructor via helper
public MainViewModel()
{
    _ = InitializeAsync(); // Still fire-and-forget but explicit
}

// Call site - option 2: use IAsyncInitialization pattern
```

#### 2.3 Files to Modify
| File | Method | Action |
|------|--------|--------|
| `MainViewModel.cs` | `InitializeAsync` | Change to `async Task`, add error handling |
| `MainViewModel.cs` | `StartHelloTimer` | Change to `async Task` |
| `BoundaryMapDialogPanel.axaml.cs` | `BtnSave_Click` | Add try/catch |
| `AgShareSettingsDialogPanel.axaml.cs` | `TestConnection_Click` | Add try/catch |
| `AgShareDownloadDialogPanel.axaml.cs` | `Refresh_Click` | Add try/catch |
| `AgShareDownloadDialogPanel.axaml.cs` | `DownloadAll_Click` | Add try/catch |
| `AgShareDownloadDialogPanel.axaml.cs` | `Download_Click` | Add try/catch |
| `AgShareUploadDialogPanel.axaml.cs` | `Upload_Click` | Add try/catch |
| `AlphanumericKeyboardPanel.axaml.cs` | `OnPasteClick` | Add try/catch |

### Estimated Effort: 1 hour

---

## Phase 3: Records for Immutable Data Transfer Objects

### Why
- Less boilerplate code
- Built-in equality, hashing, deconstruction
- Immutable by default (safer)
- Better for data that shouldn't change after creation

### Candidates for Conversion

#### Good Candidates (Immutable DTOs)
| Current Class | Convert To |
|---------------|------------|
| `FieldSelectionItem` | `record FieldSelectionItem` |
| `CoverageColor` | `record struct CoverageColor` |
| `YouTurnCreationOutput` | `record YouTurnCreationOutput` |
| `CurveNudgeOutput` | `record CurveNudgeOutput` |
| `FieldSnapshotInput` | `record FieldSnapshotInput` |
| `GpsPosition` | `record struct GpsPosition` |

#### Not Good Candidates (Need Mutability)
- `NtripProfile` - edited by user
- `Track` - modified during recording
- `SectionControlState` - frequently updated
- ViewModels - reactive properties

### Implementation Example
```csharp
// Before (27 lines)
public class FieldSelectionItem
{
    public string Name { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public double Distance { get; set; }
    public double Area { get; set; }
    public string NtripProfileName { get; set; } = string.Empty;
}

// After (1 line)
public record FieldSelectionItem(
    string Name,
    string DirectoryPath,
    double Distance,
    double Area,
    string NtripProfileName);

// Usage remains the same
var item = new FieldSelectionItem("Field1", "/path", 0, 10.5, "Default");

// With-expressions for "modification"
var updated = item with { Area = 12.0 };
```

### Implementation Steps

#### 3.1 Identify All DTOs
Search for classes that:
- Have only properties (no methods beyond simple logic)
- Are created, passed around, but not modified
- Don't implement INotifyPropertyChanged

#### 3.2 Convert One at a Time
1. Change `class` to `record`
2. Convert properties to primary constructor parameters
3. Fix any compilation errors (usually assignment to properties)
4. Test functionality

#### 3.3 Use `record struct` for Small Value Types
```csharp
// For small, frequently-created types (reduces allocations)
public readonly record struct CoverageColor(byte R, byte G, byte B);
public readonly record struct Vec2(double Easting, double Northing);
```

### Estimated Effort: 2-3 hours

---

## Phase 4: IOptions<T> Pattern for Configuration

### Why
- Strongly-typed configuration
- Validation at startup
- Reloadable configuration
- Clear separation of configuration from code

### Current State
- `ConfigurationStore.Instance` singleton
- Manual JSON loading in services
- Settings scattered across multiple files

### Implementation Steps

#### 4.1 Define Options Classes
```csharp
// New file: Shared/AgValoniaGPS.Models/Configuration/NtripOptions.cs
public class NtripOptions
{
    public const string SectionName = "Ntrip";

    public string DefaultCasterHost { get; set; } = "rtk2go.com";
    public int DefaultCasterPort { get; set; } = 2101;
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    public bool AutoReconnect { get; set; } = true;
}

// New file: Shared/AgValoniaGPS.Models/Configuration/GpsOptions.cs
public class GpsOptions
{
    public const string SectionName = "Gps";

    public int UpdateRateHz { get; set; } = 10;
    public double SlowSpeedCutoff { get; set; } = 0.5;
}
```

#### 4.2 Register in DI
```csharp
// In ServiceCollectionExtensions.cs
services.Configure<NtripOptions>(configuration.GetSection(NtripOptions.SectionName));
services.Configure<GpsOptions>(configuration.GetSection(GpsOptions.SectionName));
```

#### 4.3 Inject into Services
```csharp
// Before
public class NtripClientService : INtripClientService
{
    public NtripClientService()
    {
        var timeout = 10; // Magic number
    }
}

// After
public class NtripClientService : INtripClientService
{
    private readonly NtripOptions _options;

    public NtripClientService(IOptions<NtripOptions> options)
    {
        _options = options.Value;
        var timeout = _options.ConnectionTimeoutSeconds;
    }
}
```

#### 4.4 Configuration File (appsettings.json)
```json
{
  "Ntrip": {
    "DefaultCasterHost": "rtk2go.com",
    "DefaultCasterPort": 2101,
    "ConnectionTimeoutSeconds": 10,
    "AutoReconnect": true
  },
  "Gps": {
    "UpdateRateHz": 10,
    "SlowSpeedCutoff": 0.5
  }
}
```

### Note
This is a larger refactor that may not be worth the effort given the existing `ConfigurationStore` pattern works. Consider for new configuration areas only.

### Estimated Effort: 3-4 hours (if full conversion)

---

## Phase 5: Primary Constructors (C# 12)

### Why
- Reduces constructor boilerplate
- Fields automatically captured from parameters
- Cleaner code for DI-heavy classes

### Implementation Steps

#### 5.1 Convert Services with Simple DI
```csharp
// Before (12 lines)
public class SectionControlService : ISectionControlService
{
    private readonly IToolPositionService _toolPositionService;
    private readonly ICoverageMapService _coverageMapService;
    private readonly ApplicationState _state;

    public SectionControlService(
        IToolPositionService toolPositionService,
        ICoverageMapService coverageMapService,
        ApplicationState state)
    {
        _toolPositionService = toolPositionService;
        _coverageMapService = coverageMapService;
        _state = state;
    }
}

// After (4 lines)
public class SectionControlService(
    IToolPositionService toolPositionService,
    ICoverageMapService coverageMapService,
    ApplicationState state) : ISectionControlService
{
    // Use toolPositionService, coverageMapService, state directly
}
```

#### 5.2 Candidates for Conversion
Services with constructor-only DI injection:
- `SectionControlService`
- `CoverageMapService`
- `TrackGuidanceService`
- `YouTurnCreationService`
- `YouTurnGuidanceService`
- `FieldService`
- `NtripProfileService`
- `GpsService`

#### 5.3 When NOT to Use
- Classes with complex constructor logic
- Classes that need to transform/validate parameters
- ViewModels (MVVM Toolkit may conflict)

### Estimated Effort: 1-2 hours

---

## Implementation Order (Recommended)

| Priority | Phase | Effort | Impact |
|----------|-------|--------|--------|
| 1 | Phase 2: async void fixes | 1 hour | High (prevents crashes) |
| 2 | Phase 1: Structured Logging | 2-3 hours | High (debugging/production) |
| 3 | Phase 5: Primary Constructors | 1-2 hours | Medium (code clarity) |
| 4 | Phase 3: Records | 2-3 hours | Medium (code reduction) |
| 5 | Phase 4: IOptions | 3-4 hours | Low (existing pattern works) |

---

## Checklist

### Phase 1: Structured Logging ✅
- [x] Add logging NuGet packages
- [x] Configure logging in DI (all platforms)
- [x] Add ILogger<T> to services
- [x] Replace Console.WriteLine calls (180+ → 0)
- [x] Test log output

### Phase 2: async void Fixes ✅
- [x] Add try/catch to event handlers
- [x] Convert non-event async void to async Task
- [x] Test error handling

### Phase 3: Records ✅
- [x] Identify DTO candidates
- [x] Convert FieldSelectionItem to record
- [x] Convert CoverageColor to readonly record struct
- [x] Convert ABLineNudgeOutput, CurveNudgeOutput, SectionHeadlandStatus to records
- [x] Update consuming services to use record constructors
- [x] Test functionality (build passes)

### Phase 4: IOptions (Skipped)
Existing `ConfigurationStore` pattern works well for current needs.

### Phase 5: Primary Constructors ✅
- [x] Convert simple services (HeadlandBuilderService, CoverageMapService, ConfigurationService, TramLineService)
- [x] Test DI still works
- [x] Build passes on all platforms

---

## References

- [Microsoft Logging Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Async/Await Best Practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [Records Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Options Pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Primary Constructors](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#primary-constructors)
