# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
AgOpenGPS is a precision agriculture guidance software written in C# (.NET Framework 4.8) that provides GPS guidance, field mapping, and section control for agricultural equipment. The project consists of two main applications: AgIO (communication hub) and AgOpenGPS (main application).

## Build Commands

```bash
# Restore dependencies
dotnet restore --runtime win-x64 ./SourceCode/AgOpenGPS.sln

# Build solution
dotnet build --no-restore ./SourceCode/AgOpenGPS.sln

# Run all tests
dotnet test --no-restore --no-build ./SourceCode/AgOpenGPS.sln

# Run specific test project
dotnet test ./SourceCode/AgLibrary.Tests/AgLibrary.Tests.csproj
dotnet test ./SourceCode/AgOpenGPS.Core.Tests/AgOpenGPS.Core.Tests.csproj

# Publish (creates AgOpenGPS folder with all applications)
dotnet publish ./SourceCode/AgOpenGPS.sln
```

## Architecture

### Core Structure
- **MVP Pattern**: AgOpenGPS.Core implements Model-View-Presenter pattern with dependency injection
- **ApplicationCore**: Main composition root at `AgOpenGPS.Core/ApplicationCore.cs`
- **Separation of Concerns**: Models, ViewModels, Presenters, and Interfaces are cleanly separated

### Key Components
- **GPS/**: Main Windows Forms application with OpenGL graphics rendering
- **AgIO/**: Communication hub for hardware interfaces
- **AgOpenGPS.Core/**: Business logic library using MVP pattern
- **AgLibrary/**: Shared utilities and settings management
- **AgOpenGPS.WpfApp/**: WPF version of the application

### Main Entry Points
- GPS Application: `GPS/Program.cs`
- Core Logic: `AgOpenGPS.Core/ApplicationCore.cs`
- Settings: `AgLibrary/Settings/` namespace

## Development Workflow

### Version Control
- **Main development branch**: `develop` (submit PRs here)
- **Stable branch**: `master`
- **Version management**: GitVersion handles semantic versioning automatically
- **Version file**: Manual patch increments in `./sys/version.h` when fixing bugs

### Testing
- **Framework**: NUnit 4.3.2
- **Test pattern**: AAA (Arrange, Act, Assert) with Assert.That syntax
- **Test projects**: AgLibrary.Tests, AgOpenGPS.Core.Tests

### Key Technologies
- .NET Framework 4.8 (Windows-only)
- Windows Forms (main UI) and WPF (newer components)
- OpenTK.GLControl for OpenGL graphics
- SQLite for data storage
- NMEA protocol for GPS communication

## Common Development Tasks

### Running the Application
1. Set GPS project as startup project in Visual Studio
2. Build and run (F5)

### Adding New Features
- Business logic goes in AgOpenGPS.Core
- Shared utilities in AgLibrary
- UI components in GPS (Windows Forms) or AgOpenGPS.WpfApp (WPF)

### Debugging Hardware Communication
- AgIO handles all hardware communication
- Check AgIO logs for connection issues
- ModSim project provides hardware simulation

### Working with Translations
- Uses Weblate for internationalization
- Resource files (.resx) contain UI strings
- Located in each project's Properties folder