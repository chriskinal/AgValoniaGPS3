# Contributing to AgValoniaGPS

Thank you for your interest in contributing to AgValoniaGPS! This document lists features that need implementation and provides guidance for contributors.

## Getting Started

1. Fork the repository
2. Clone your fork locally
3. Build the project following instructions in `CLAUDE.md`
4. Pick a feature from the list below
5. Create a feature branch and implement
6. Submit a pull request

## Architecture Overview

- **Shared code** (~92%): Located in `Shared/` folder
  - `AgValoniaGPS.Models/` - Data models
  - `AgValoniaGPS.Services/` - Business logic
  - `AgValoniaGPS.ViewModels/` - MVVM ViewModels (ReactiveUI)
  - `AgValoniaGPS.Views/` - Shared UI controls, panels, dialogs

- **Platform code** (~8%): Located in `Platforms/`
  - `AgValoniaGPS.Desktop/` - Windows/macOS/Linux
  - `AgValoniaGPS.iOS/` - iOS/iPadOS

## Features Needing Implementation

### Difficulty: Easy

These features are good starting points for new contributors.

#### About Dialog
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 80)
- **Task**: Create an About dialog showing app version, credits, and links
- **Skills needed**: XAML, basic ViewModel

#### Help Documentation
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 79)
- **Task**: Create help dialog or link to online documentation
- **Skills needed**: XAML, basic ViewModel

#### Section Colors Configuration
- **Button location**: Configuration Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/ConfigurationPanel.axaml` (Line 78)
- **Task**: Create dialog to customize section/coverage display colors
- **Skills needed**: XAML, color picker, settings persistence

#### App Colors / Theme
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 59)
- **Task**: Create dialog to customize application color scheme/theme
- **Skills needed**: XAML, Avalonia theming, settings persistence

---

### Difficulty: Medium

These features require more understanding of the codebase.

#### Log Viewer
- **Button location**: Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/ToolsPanel.axaml` (Line 61)
- **Task**: Create a log viewer to display application diagnostic logs
- **Skills needed**: XAML, logging infrastructure, virtualized lists

#### Flag By Lat/Lon
- **Button location**: Field Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FieldToolsPanel.axaml` (Line 105)
- **Task**: Allow user to place a flag/marker at specific GPS coordinates
- **Skills needed**: XAML, coordinate conversion, map integration

#### Recorded Path Display
- **Button location**: Field Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FieldToolsPanel.axaml` (Line 114)
- **Task**: Display the GPS path/trail that has been recorded
- **Skills needed**: XAML, GPS data, DrawingContext rendering

#### Import Tracks
- **Button location**: Field Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FieldToolsPanel.axaml` (Line 121)
- **Task**: Import track/guidance line data from external files (KML, shapefile, etc.)
- **Skills needed**: File parsing, coordinate systems, Track model

#### Hotkeys Configuration
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 60)
- **Task**: Create dialog to view and configure keyboard shortcuts
- **Skills needed**: XAML, key binding system, settings persistence

#### App Directories
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 58)
- **Task**: Show dialog displaying data directory locations with option to open in file manager
- **Skills needed**: XAML, platform file system APIs

#### View All Settings
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 57)
- **Task**: Create comprehensive settings viewer/editor
- **Skills needed**: XAML, reflection or settings enumeration

#### Reset All Settings
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 52)
- **Task**: Implement settings reset with confirmation dialog
- **Skills needed**: Settings persistence, confirmation dialogs

---

### Difficulty: Hard

These features require deep understanding of agricultural guidance or complex implementations.

#### Steer Chart
- **Button location**: Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/ToolsPanel.axaml` (Line 69)
- **Task**: Real-time chart showing steering angle commands vs actual response
- **Skills needed**: Charting library or custom rendering, real-time data, autosteer system

#### Heading Chart
- **Button location**: Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/ToolsPanel.axaml` (Line 75)
- **Task**: Real-time chart showing heading data over time
- **Skills needed**: Charting library or custom rendering, GPS data

#### XTE Chart (Cross-Track Error)
- **Button location**: Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/ToolsPanel.axaml` (Line 83)
- **Task**: Real-time chart showing cross-track error history
- **Skills needed**: Charting library or custom rendering, guidance system

#### Roll Correction
- **Button location**: Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/ToolsPanel.axaml` (Line 89)
- **Task**: Interface for configuring GPS antenna roll/tilt correction
- **Skills needed**: IMU/GPS concepts, coordinate transforms

#### Tram Lines
- **Button location**: Field Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FieldToolsPanel.axaml` (Line 82)
- **Task**: Display and manage tram line (tramline) patterns for controlled traffic farming
- **Skills needed**: Agricultural concepts, track system, rendering

#### Tram Lines Builder
- **Button location**: Field Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FieldToolsPanel.axaml` (Line 89)
- **Task**: Create interface to build/edit tram line patterns
- **Skills needed**: Agricultural concepts, complex UI, track generation

#### Offset Fix
- **Button location**: Job Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/JobMenuPanel.axaml` (Line 137)
- **Task**: Apply offset correction to shift all field tracks
- **Skills needed**: Coordinate transforms, track system

#### AutoSteer Setup Wizard
- **Button location**: AutoSteer Config dialog
- **File**: `Shared/AgValoniaGPS.ViewModels/AutoSteerConfigViewModel.cs` (Line 945)
- **Task**: Step-by-step wizard for configuring autosteer system
- **Skills needed**: Autosteer concepts, multi-step wizard UI, UDP communication

#### Hardware Pin Configuration Upload
- **Button location**: Configuration dialog
- **File**: `Shared/AgValoniaGPS.ViewModels/ConfigurationViewModel.cs` (Line 1488)
- **Task**: Read current pin configuration from machine module via UDP
- **Skills needed**: UDP protocol, AgOpenGPS hardware protocol

#### Send Machine Configuration
- **Button location**: Configuration dialog
- **File**: `Shared/AgValoniaGPS.ViewModels/ConfigurationViewModel.cs` (Line 1494)
- **Task**: Send configuration to machine module via UDP
- **Skills needed**: UDP protocol, AgOpenGPS hardware protocol

---

### Not Prioritized

These features may not be needed or have lower priority.

#### Language/Localization
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 50)
- **Note**: Requires localization infrastructure to be set up first

#### Kiosk Mode
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 51)
- **Note**: May not be needed for this application

---

## How to Implement a Button Feature

1. **Find the button** in the AXAML file listed above

2. **Add a Command binding** to the button:
   ```xml
   <Button Content="My Feature" Command="{Binding MyFeatureCommand}" />
   ```

3. **Create the command** in `MainViewModel.cs` or appropriate ViewModel:
   ```csharp
   public ReactiveCommand<Unit, Unit> MyFeatureCommand { get; }

   // In constructor:
   MyFeatureCommand = ReactiveCommand.Create(ExecuteMyFeature);

   private void ExecuteMyFeature()
   {
       // Implementation here
   }
   ```

4. **For dialogs**, follow the existing pattern:
   - Add `IsMyDialogVisible` property to ViewModel
   - Create dialog panel in `Shared/AgValoniaGPS.Views/Controls/Dialogs/`
   - Add dialog to both `MainWindow.axaml` and `MainView.axaml`

## Questions?

Open an issue on GitHub or reach out to the maintainers.
