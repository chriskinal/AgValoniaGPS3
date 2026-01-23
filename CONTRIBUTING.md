<!--
AgValoniaGPS
Copyright (C) 2024-2025 AgValoniaGPS Contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
-->

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

#### App Colors / Theme
- **Button location**: File Menu Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` (Line 59)
- **Task**: Create dialog to customize application color scheme/theme
- **Skills needed**: XAML, Avalonia theming, settings persistence

#### Nudge AB Left/Right
- **Button location**: Bottom Navigation Panel (AB Line Options section)
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Lines 271-278)
- **Task**: Implement NudgeLeftCommand and NudgeRightCommand to shift track by standard offset
- **Skills needed**: Track model, geometry

#### Fine Nudge AB Left/Right
- **Button location**: Bottom Navigation Panel (AB Line Options section)
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Lines 283-290)
- **Task**: Implement FineNudgeLeftCommand and FineNudgeRightCommand for small track adjustments
- **Skills needed**: Track model, geometry

#### Snap to Left/Right Track
- **Button location**: Bottom Navigation Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Lines 192-203)
- **Task**: Implement SnapLeftCommand and SnapRightCommand to jump to adjacent tracks
- **Skills needed**: Track model, guidance system

#### Snap to Pivot
- **Button location**: Bottom Navigation Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Line 206)
- **Task**: Implement SnapToPivotCommand to snap to pivot point
- **Skills needed**: Track model, geometry

#### U-Turn Skip Rows On/Off
- **Button location**: Bottom Navigation Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Line 142)
- **Task**: Implement ToggleUTurnSkipRowsCommand to enable/disable row skipping during U-turns
- **Skills needed**: U-turn system, guidance

#### Reset Tool Heading
- **Button location**: Bottom Navigation Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Line 157)
- **Task**: Implement ResetToolHeadingCommand to reset implement heading to vehicle heading
- **Skills needed**: Vehicle/tool geometry

#### Display Options - Wire Up Settings
- **Location**: Configuration Dialog → Display Options tab
- **File**: `Shared/AgValoniaGPS.Views/Controls/Dialogs/Configuration/DisplayConfigTab.axaml`
- **Task**: Wire up display toggles to actually affect rendering/UI. Settings toggle but have no effect:
  - Polygons, Speedometer, Keyboard, Headland Distance display
  - Auto Day/Night brightness, Svenn Arrow, Start Fullscreen
  - Elevation Log, Field Texture, Grid, Extra Guidelines
  - Line Smooth, Direction Markers, Section Lines
  - Metric/Imperial units
- **Skills needed**: DrawingContext rendering, MainViewModel integration

#### Additional Options - Wire Up Settings
- **Location**: Configuration Dialog → Additional Options tab
- **File**: `Shared/AgValoniaGPS.Views/Controls/Dialogs/Configuration/AdditionalOptionsConfigTab.axaml`
- **Task**: Wire up additional options to actually affect the application:
  - Screen Buttons: U-Turn button visibility, Lateral button visibility
  - Sounds: Auto Steer, U-Turn, Hydraulic, Sections sounds
  - Hardware Messages toggle
- **Skills needed**: Audio playback, UI visibility bindings

#### View Settings Panel - Wire Up Settings
- **Location**: View Settings Panel (opened from left nav)
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/ViewSettingsPanel.axaml`
- **Task**: Wire up view settings to actually affect rendering. Only Grid works currently:
  - Day/Night mode (IsDayMode not used)
  - 2D/3D toggle (Is2DMode not used)
  - North Up/Track Up (IsNorthUp not used)
  - Camera Tilt Up/Down (CameraPitch not used)
  - Brightness +/- (IsBrightnessSupported returns false)
- **Skills needed**: DrawingContext rendering, camera transforms

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

#### Draw AB Line on Map
- **Button location**: Bottom Navigation Panel (AB Line Options section)
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Line 239)
- **Task**: Implement ShowDrawABDialogCommand to allow drawing curved or multipoint lines on map
- **Skills needed**: Map interaction, Track model, dialog system

#### Smooth AB Curve
- **Button location**: Bottom Navigation Panel (AB Line Options section)
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Line 254)
- **Task**: Implement SmoothABLineCommand to smooth/simplify curve points
- **Skills needed**: Geometry algorithms, Track model

#### Delete Contours
- **Button location**: Bottom Navigation Panel (AB Line Options section)
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Line 261)
- **Task**: Implement DeleteContoursCommand to remove contour reference lines
- **Skills needed**: Track model, contour system

#### Contour Mode On/Off
- **Button location**: Right Navigation Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/RightNavigationPanel.axaml` (Line 95)
- **Task**: Implement ToggleContourModeCommand to toggle contour guidance mode
- **Skills needed**: Guidance system, contour tracking

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

#### Headland Builder
- **Button location**: Field Tools Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/FieldToolsPanel.axaml` (Line 87)
- **Task**: Implement ShowHeadlandBuilderCommand to create/edit headland zones interactively
- **Skills needed**: Boundary geometry, polygon operations, map interaction

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

#### Section Mapping Colors
- **Button location**: Bottom Navigation Panel
- **File**: `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` (Line 151)
- **Note**: May be redundant with App Colors/Theme configuration

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
