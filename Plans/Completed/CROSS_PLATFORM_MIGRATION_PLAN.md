# Cross-Platform Migration Plan

## Decision: Fresh Start in AgValoniaGPS3

Instead of refactoring the tangled AgValoniaGPS2 project in place, we'll create a clean new project `AgValoniaGPS3` that:
1. Takes only the working, known-good code from AgValoniaGPS2
2. Has NO reference to SourceCode/AgOpenGPS.Core
3. Is designed from the start for 95%/5% cross-platform split

### Why Fresh Start?
- AgValoniaGPS2 has tangled references to SourceCode/AgOpenGPS.Core
- Failed iOS attempt left confusing state
- Clean break is safer and easier to reason about
- Can always reference AgValoniaGPS2 for working code

---

## What We're Taking from AgValoniaGPS2 (Known Working)

### From AgValoniaGPS.Models/ (models that work)
- GpsData.cs, Position.cs
- VehicleConfiguration.cs, Vehicle.cs
- BackgroundImage.cs
- Other domain models (no AgOpenGPS.Core dependencies)

### From AgValoniaGPS.Services/ (services that work WITHOUT AgOpenGPS.Core refs)
- UdpCommunicationService.cs + IUdpCommunicationService.cs
- NtripClientService.cs + INtripClientService.cs
- NmeaParserService.cs
- SettingsService.cs + ISettingsService.cs
- FieldService.cs + IFieldService.cs
- GuidanceService.cs + IGuidanceService.cs
- BoundaryRecordingService.cs + IBoundaryRecordingService.cs
- BoundaryFileService.cs, FieldPlaneFileService.cs, BackgroundImageFileService.cs
- DisplaySettingsService.cs + IDisplaySettingsService.cs

### From AgValoniaGPS.ViewModels/
- MainViewModel.cs (bindings for GPS, modules, etc.)

### From AgValoniaGPS.Desktop/ (platform-specific)
- Views/MainWindow.axaml + .cs
- Views/DataIODialog.axaml + .cs
- Controls/OpenGLMapControl.cs
- Converters/
- DependencyInjection/ServiceCollectionExtensions.cs (will need cleanup)
- Assets/

---

## New Project Structure: AgValoniaGPS3

```
/Users/chris/Code/AgValoniaGPS3/
├── AgValoniaGPS.sln
├── CLAUDE.md
│
├── Shared/ (95% - cross-platform)
│   ├── AgValoniaGPS.Models/        # Data models only
│   ├── AgValoniaGPS.Services/      # Business logic services
│   └── AgValoniaGPS.ViewModels/    # MVVM ViewModels
│
└── Platforms/ (5% - platform-specific)
    ├── AgValoniaGPS.Desktop/       # Windows/macOS/Linux desktop
    │   ├── App.axaml + .cs
    │   ├── Views/
    │   ├── Controls/OpenGLMapControl.cs
    │   ├── Converters/
    │   └── DependencyInjection/
    │
    └── AgValoniaGPS.iOS/           # iOS
        ├── App.axaml + .cs
        ├── Views/iOSMainView.axaml + .cs
        ├── Controls/ (SkiaMapControl future)
        ├── Converters/
        └── DependencyInjection/
```

---

## Implementation Phases

### Phase 1: Create Project Structure
```bash
mkdir -p /Users/chris/Code/AgValoniaGPS3
cd /Users/chris/Code/AgValoniaGPS3

# Create shared projects
mkdir -p Shared/AgValoniaGPS.Models
mkdir -p Shared/AgValoniaGPS.Services/Interfaces
mkdir -p Shared/AgValoniaGPS.ViewModels

# Create platform projects
mkdir -p Platforms/AgValoniaGPS.Desktop/Views
mkdir -p Platforms/AgValoniaGPS.Desktop/Controls
mkdir -p Platforms/AgValoniaGPS.Desktop/Converters
mkdir -p Platforms/AgValoniaGPS.Desktop/DependencyInjection
mkdir -p Platforms/AgValoniaGPS.Desktop/Assets

mkdir -p Platforms/AgValoniaGPS.iOS/Views
mkdir -p Platforms/AgValoniaGPS.iOS/Converters
mkdir -p Platforms/AgValoniaGPS.iOS/DependencyInjection
```

### Phase 2: Create .csproj Files (No SourceCode References!)

**Shared/AgValoniaGPS.Models/AgValoniaGPS.Models.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**Shared/AgValoniaGPS.Services/AgValoniaGPS.Services.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AgValoniaGPS.Models\AgValoniaGPS.Models.csproj" />
  </ItemGroup>
</Project>
```

**Shared/AgValoniaGPS.ViewModels/AgValoniaGPS.ViewModels.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ReactiveUI" Version="20.1.1" />
    <PackageReference Include="ReactiveUI.Fody" Version="19.5.41" />
    <ProjectReference Include="..\AgValoniaGPS.Models\AgValoniaGPS.Models.csproj" />
    <ProjectReference Include="..\AgValoniaGPS.Services\AgValoniaGPS.Services.csproj" />
  </ItemGroup>
</Project>
```

**Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.9" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.9" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.9" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.3.6" />
    <PackageReference Include="ReactiveUI" Version="20.1.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageReference Include="Silk.NET.OpenGL" Version="2.22.0" />
    <PackageReference Include="StbImageSharp" Version="2.30.15" />
    <PackageReference Include="System.IO.Ports" Version="9.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Shared\AgValoniaGPS.Models\AgValoniaGPS.Models.csproj" />
    <ProjectReference Include="..\..\Shared\AgValoniaGPS.Services\AgValoniaGPS.Services.csproj" />
    <ProjectReference Include="..\..\Shared\AgValoniaGPS.ViewModels\AgValoniaGPS.ViewModels.csproj" />
  </ItemGroup>
</Project>
```

**Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-ios</TargetFramework>
    <RuntimeIdentifier>iossimulator-arm64</RuntimeIdentifier>
    <Nullable>enable</Nullable>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.9" />
    <PackageReference Include="Avalonia.iOS" Version="11.3.9" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.9" />
    <PackageReference Include="ReactiveUI" Version="20.1.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Shared\AgValoniaGPS.Models\AgValoniaGPS.Models.csproj" />
    <ProjectReference Include="..\..\Shared\AgValoniaGPS.Services\AgValoniaGPS.Services.csproj" />
    <ProjectReference Include="..\..\Shared\AgValoniaGPS.ViewModels\AgValoniaGPS.ViewModels.csproj" />
  </ItemGroup>
</Project>
```

### Phase 3: Copy Models (Clean, No Dependencies)
Copy from AgValoniaGPS2/AgValoniaGPS/AgValoniaGPS.Models/ to AgValoniaGPS3/Shared/AgValoniaGPS.Models/

Files to copy:
- GpsData.cs
- Position.cs
- VehicleConfiguration.cs
- Vehicle.cs
- BackgroundImage.cs
- Other models that don't reference AgOpenGPS.Core

### Phase 4: Copy Services (Remove AgOpenGPS.Core References)
Copy from AgValoniaGPS2/AgValoniaGPS/AgValoniaGPS.Services/ to AgValoniaGPS3/Shared/AgValoniaGPS.Services/

Key services needed for working desktop app:
- Interfaces/IUdpCommunicationService.cs
- Interfaces/INtripClientService.cs
- Interfaces/ISettingsService.cs
- Interfaces/IGuidanceService.cs
- Interfaces/IFieldService.cs
- Interfaces/IBoundaryRecordingService.cs
- Interfaces/IDisplaySettingsService.cs
- UdpCommunicationService.cs
- NtripClientService.cs
- NmeaParserService.cs
- SettingsService.cs
- GuidanceService.cs
- FieldService.cs
- BoundaryRecordingService.cs
- DisplaySettingsService.cs

**Important**: Remove any `using AgOpenGPS.Core.*` statements and ensure all types come from AgValoniaGPS.Models

### Phase 5: Copy ViewModels
Copy MainViewModel.cs from AgValoniaGPS2 to AgValoniaGPS3/Shared/AgValoniaGPS.ViewModels/

Update imports to use AgValoniaGPS.Services.Interfaces (not AgOpenGPS.Core)

### Phase 6: Copy Desktop Platform Code
Copy from AgValoniaGPS2/AgValoniaGPS/AgValoniaGPS.Desktop/:
- Program.cs
- App.axaml + App.axaml.cs
- Views/MainWindow.axaml + .cs
- Views/DataIODialog.axaml + .cs
- Controls/OpenGLMapControl.cs
- Converters/*.cs
- Assets/

Update DependencyInjection/ServiceCollectionExtensions.cs to:
- Remove all AgOpenGPS.Core references
- Use AgValoniaGPS.Services.Interfaces for all services

### Phase 7: Build and Test Desktop
```bash
cd /Users/chris/Code/AgValoniaGPS3
dotnet build Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
dotnet run --project Platforms/AgValoniaGPS.Desktop/AgValoniaGPS.Desktop.csproj
```

Verify:
- [ ] App launches
- [ ] GPS status displays
- [ ] Module status displays
- [ ] NTRIP dialog works
- [ ] Map renders with vehicle

### Phase 8: Create iOS Platform Code
Create minimal iOS entry point:
- Program.cs (iOS main)
- App.axaml + App.axaml.cs (ISingleViewApplicationLifetime)
- Views/iOSMainView.axaml + .cs (simplified UI)
- Converters/ (copy from Desktop)
- DependencyInjection/ServiceCollectionExtensions.cs (same as Desktop but iOS namespace)

### Phase 9: Build and Test iOS
```bash
cd /Users/chris/Code/AgValoniaGPS3
dotnet build Platforms/AgValoniaGPS.iOS/AgValoniaGPS.iOS.csproj

# Install on simulator
open -a Simulator
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/AgValoniaGPS.iOS.app
xcrun simctl launch booted com.agopengps.agvaloniagps
```

Verify:
- [ ] App launches on simulator
- [ ] UI displays correctly
- [ ] Bindings work

### Phase 10: Commit and Document
- Create git repo
- Write CLAUDE.md
- Commit clean working state

---

## Key Rules for AgValoniaGPS3

1. **NO references to SourceCode/AgOpenGPS.Core** - ever
2. **Shared/ projects have NO Avalonia dependencies** - pure .NET
3. **Platform/ projects contain ALL UI code** - Views, Controls, Converters
4. **Services use interfaces** - platform projects wire up DI
5. **ViewModels use ReactiveUI** - shared across platforms

---

## Rollback Plan

AgValoniaGPS2 remains untouched at:
- `/Users/chris/Code/AgValoniaGPS2/`
- Branch: feature/skiasharp-mobile
- Commit: ede90f8

If AgValoniaGPS3 fails, we still have the original working Desktop app in AgValoniaGPS2.
