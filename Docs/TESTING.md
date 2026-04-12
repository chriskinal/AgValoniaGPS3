# Testing Guide

Comprehensive guide to the AgValoniaGPS test suite: what exists, how to run it, how to write new tests, and when tests are required.

**Last Updated:** April 2026

---

## Quick Reference

```bash
# Run all tests (317 total)
dotnet test Tests/AgValoniaGPS.Models.Tests/
dotnet test Tests/AgValoniaGPS.Services.Tests/
dotnet test Tests/AgValoniaGPS.UI.Tests/

# Run with coverage
dotnet test Tests/AgValoniaGPS.Services.Tests/ --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "TrackGuidanceServiceTests"

# Run single test
dotnet test Tests/AgValoniaGPS.Services.Tests/ --filter "PurePursuit_VehicleLeftOfLine_SteersRight"
```

**Current counts:** 72 model + 150 service + 95 UI = 317 tests
**Coverage:** ~6% models, ~20% services (critical paths covered, large gaps elsewhere)

---

## Test Projects

### AgValoniaGPS.Models.Tests (72 tests)

Pure unit tests for geometry, coordinate conversion, and math utilities. No mocking needed - these test pure functions.

| File | Tests | What it covers |
|------|-------|---------------|
| `GeometryMathTests.cs` | 37 | Angle conversion, distance, point-in-polygon, ray intersection, Catmull-Rom splines, unit constants |
| `GeoConversionTests.cs` | 21 | WGS84 to/from local coordinates, heading calculation, area computation |
| `BoundaryUtilsTests.cs` | 2 | Boundary polygon utilities |
| `CurveUtilsTests.cs` | 2 | Curve spline utilities |
| `GeoCalculationsTests.cs` | 3 | Geographic distance and area calculations |

**When to add tests here:** Any new method in `GeometryMath`, coordinate conversion, or pure data model logic.

### AgValoniaGPS.Services.Tests (150 tests)

Service-level tests that verify business logic. Some use mocks (NSubstitute), some are integration tests with real service instances.

| File | Tests | What it covers |
|------|-------|---------------|
| `TrackGuidanceServiceTests.cs` | 20 | Pure Pursuit and Stanley algorithms, heading scenarios, multi-frame state, edge cases |
| `NmeaParserServiceTests.cs` | 16 | NMEA sentence parsing, checksum, lat/lon, fix quality, speed conversion |
| `FileIOTests.cs` | 16 | Track/boundary/settings file round-trips, heading accuracy |
| `GeoJsonFieldServiceTests.cs` | 13 | GeoJSON field format round-trips, boundary/track preservation |
| `SectionControlServiceTests.cs` | 12 | Section on/off logic, speed cutoff, master state, bitmask generation |
| `SwathOrderingTests.cs` | 11 | Boustrophedon, snake, spiral swath patterns |
| `YouTurnGuidanceServiceTests.cs` | 11 | U-turn path following, steer angle clamping, completion detection |
| `ChartDataServiceTests.cs` | 19 | Chart data lifecycle, time windows, series management |
| `ProfileJsonServiceTests.cs` | 11 | Vehicle profile JSON serialization for all config types |
| `DisplayTogglePersistenceTests.cs` | 8 | AppSettings JSON round-trip, backward compat, reactive properties |
| `GpsServiceTests.cs` | 7 | GPS connection state, timeout detection, recovery |
| `FieldSaveLoadVerificationTests.cs` | 6 | End-to-end field I/O, legacy format support |
| `GuidancePipelineIntegrationTests.cs` | 5 | Closed-loop guidance: NMEA -> guidance -> steer angle convergence |

**When to add tests here:** Any new service, algorithm, file format, or configuration persistence change.

### AgValoniaGPS.UI.Tests (95 tests)

Headless Avalonia UI tests. Uses `[AvaloniaTest]` attribute for tests that need a rendering context, plain `[Test]` for ViewModel-only tests.

| File | Tests | What it covers |
|------|-------|---------------|
| `BottomPanelHeadlessTests.cs` | 12 | Bottom nav panel buttons, snap/nudge commands with headless rendering |
| `P1CommandTests.cs` | 12 | U-turn, cycle AB lines, flag placement, headland extend/shrink |
| `TrackNudgeAndSnapTests.cs` | 11 | Nudge left/right, fine nudge, snap to pivot, accumulating nudges |
| `TrackManagementScreenshotTests.cs` | 11 | Track dialog, recorded paths, import, delete confirmation |
| `QuickWinScreenshotTests.cs` | 10 | Theme switching, log viewer, flag dialog, about dialog screenshots |
| `UIStateDialogTests.cs` | 8 | Dialog state machine: show/close, visibility, mutual exclusivity |
| `ScreenshotCaptureTests.cs` | 4 | Display toggle screenshots (grid, day/night, north-up) |
| `DisplayWiringScreenshots.cs` | 4 | Before/after screenshots for display feature toggles |
| `AppDirectoriesDialogTests.cs` | 4 | App directories dialog visibility and path population |
| `ResetAllSettingsTests.cs` | 4 | Reset settings: confirmation, service calls, cancellation |
| `ChartScreenshotTests.cs` | 3 | Steer/heading/XTE chart rendering with empty and populated data |
| `ResetToolHeadingTests.cs` | 3 | Tool heading sync with vehicle heading |

**When to add tests here:** Any new dialog, panel, command, or UI state change.

---

## Test Infrastructure

### MainViewModelBuilder

Builder pattern for creating a fully-mocked `MainViewModel` for testing:

```csharp
var vm = new MainViewModelBuilder().Build();

// Access mocked services for verification
var builder = new MainViewModelBuilder();
var vm = builder.Build();
builder.SettingsService.Received(1).Save();
```

All services are mocked via NSubstitute with sensible defaults (temp paths, empty collections).

### TestApp (Avalonia Headless)

Provides a headless Avalonia application context for UI tests:

```csharp
[AvaloniaTest]  // Runs in headless Avalonia context
public void MyUiTest()
{
    var window = new Window { Content = new MyPanel() };
    window.Show();
    // Assert visual state...
}
```

Configured with Fluent theme (dark mode) and shared resources matching the production app.

### TestSettingsService (Integration Tests)

Isolated settings service for integration tests that redirects all I/O to a temp directory:

```csharp
var settings = new TestSettingsService();
// All reads/writes go to temp dir, never touches user's Documents
```

### Integration Test Harness

`Tests/AgValoniaGPS.IntegrationTests/Program.cs` provides 14+ multi-step scenarios (app startup, field loading, simulator driving, coverage painting, etc.) that can run with a real window or headless.

---

## When to Write Tests

### Required (must have tests)

- **New algorithm or calculation** - Guidance, geometry, coordinate conversion
  - Test in `AgValoniaGPS.Models.Tests` or `AgValoniaGPS.Services.Tests`
  - Cover: normal case, edge cases, boundary values

- **New file format or I/O** - Any new file read/write
  - Test round-trip: write -> read -> assert equal
  - Test backward compatibility: can read old format files
  - Test missing/corrupt data handling

- **New AppSettings property** - Any persisted setting
  - Test JSON round-trip serialization
  - Test default value
  - Test backward compat (old settings file missing the property)
  - See `DisplayTogglePersistenceTests.cs` as template

- **Bug fix** - Reproduce the bug in a test first, then fix
  - Prevents regression

### Recommended (should have tests)

- **New ViewModel command** - Test execution and state changes
  - Use `MainViewModelBuilder` to create isolated ViewModel
  - Assert state transitions and service calls

- **New dialog** - Test visibility state
  - Test `ShowDialog()` / `CloseDialog()` via `UIState`
  - See `UIStateDialogTests.cs` as template

- **New service** - Test public API
  - Mock dependencies via NSubstitute
  - Test state lifecycle (start, process, stop)
  - See `SectionControlServiceTests.cs` as template

### Optional (nice to have)

- **Screenshot tests** - Visual verification of UI changes
  - Use `[AvaloniaTest]` with headless window
  - Capture before/after for toggle features
  - See `DisplayWiringScreenshots.cs` as template

- **Integration scenarios** - Multi-step workflows
  - Add to `IntegrationTests/Program.cs`
  - Good for verifying end-to-end data flow

---

## Writing a New Test

### Model Test (pure function)

```csharp
[TestFixture]
public class MyMathTests
{
    [Test]
    public void MyFunction_GivenInput_ReturnsExpected()
    {
        var result = GeometryMath.MyFunction(input);
        Assert.That(result, Is.EqualTo(expected).Within(0.001));
    }
}
```

### Service Test (with mocks)

```csharp
[TestFixture]
public class MyServiceTests
{
    private MyService _service;
    private IDependency _dependency;

    [SetUp]
    public void SetUp()
    {
        _dependency = Substitute.For<IDependency>();
        _service = new MyService(_dependency);
    }

    [Test]
    public void Process_WhenCondition_DoesExpectedThing()
    {
        _dependency.GetValue().Returns(42);

        _service.Process();

        Assert.That(_service.Result, Is.EqualTo(42));
        _dependency.Received(1).GetValue();
    }
}
```

### ViewModel Test

```csharp
[TestFixture]
public class MyCommandTests
{
    [Test]
    public void MyCommand_WhenExecuted_UpdatesState()
    {
        var vm = new MainViewModelBuilder().Build();

        vm.MyCommand!.Execute(null);

        Assert.That(vm.State.SomeProperty, Is.EqualTo(expected));
    }
}
```

### AppSettings Persistence Test

```csharp
[Test]
public void AppSettings_MyProperty_SurvivesJsonRoundTrip()
{
    var options = new JsonSerializerOptions
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    var original = new AppSettings { MyProperty = true };

    var json = JsonSerializer.Serialize(original, options);
    var deserialized = JsonSerializer.Deserialize<AppSettings>(json, options);

    Assert.That(deserialized!.MyProperty, Is.True);
}
```

### Headless UI Test

```csharp
[AvaloniaTest]
public void MyPanel_WhenToggled_ChangesVisibility()
{
    var vm = new MainViewModelBuilder().Build();
    var window = new Window
    {
        Width = 800, Height = 600,
        Content = new MyPanel { DataContext = vm }
    };
    window.Show();

    vm.ToggleMyPanelCommand!.Execute(null);

    // Assert visual state via DataContext or find controls
    Assert.That(vm.State.UI.IsMyPanelVisible, Is.True);
}
```

---

## Coverage

Coverage collection is set up via `coverlet.collector` in all test projects.

```bash
# Collect coverage
dotnet test Tests/AgValoniaGPS.Services.Tests/ --collect:"XPlat Code Coverage"

# Output: TestResults/{guid}/coverage.cobertura.xml
```

**Current coverage (April 2026):**
- Models: ~6% line coverage
- Services: ~20% line coverage

Coverage is low overall but focused on critical paths:
- Guidance algorithms (Pure Pursuit, Stanley)
- NMEA parsing
- Section control logic
- File I/O round-trips
- Coordinate conversion

### Coverage Gaps (known)

- `DrawingContextMapControl` - No rendering tests (visual-only)
- `UdpCommunicationService` - Network I/O, tested via integration harness
- `NtripClientService` - Network I/O
- `ConfigurationViewModel` - Large, partially tested via UI tests
- `MainViewModel` partial files - Most commands tested, some gaps in GPS handling and section control orchestration
- Coverage painting pipeline - Tested in integration harness, not unit tests

---

## Conventions

- **Framework:** NUnit 4.3 with `Assert.That()` syntax
- **Mocking:** NSubstitute
- **Headless UI:** Avalonia.Headless.NUnit with `[AvaloniaTest]`
- **Naming:** `MethodName_Condition_ExpectedResult` or descriptive sentence
- **Parallelism:** Tests run in parallel by default. Use `[NonParallelizable]` for tests that modify `ConfigurationStore.Instance` or other static state
- **Screenshots:** Saved to `TestResults/` directory, useful for visual verification in CI
- **Tolerances:** Use `Is.EqualTo(x).Within(tolerance)` for floating-point comparisons
