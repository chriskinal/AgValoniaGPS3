# AB Panels Implementation Plan

## Overview

This document captures the implementation plan for three AB-related panels in AgValoniaGPS3, based on research of AgOpenGPS source code and user-provided screenshots.

## Panel Structure

The AB Options flyout in BottomNavigationPanel contains 3 main buttons (top to bottom):
1. **Tracks** (ABTracks.png) - Opens TracksDialogPanel
2. **Quick AB** (ABTrackAPlus.png) - Opens QuickABSelectorPanel
3. **Draw AB** (ABDraw.png) - Opens DrawABDialogPanel

---

## 1. TracksDialogPanel (Track Management)

### Purpose
Manage saved AB lines/curves - view, select, delete, and perform operations on tracks.

### UI Layout (from screenshot)
- List view showing saved tracks with columns: Name, Type (Line/Curve), etc.
- Toolbar buttons:
  - Add new track
  - Delete selected track
  - Swap A/B points
  - Copy/duplicate track
  - Close

### Key Features
- Display all saved tracks for current field
- Select a track to make it active
- Delete tracks
- Swap A and B points (reverses direction)

### ViewModel Properties Needed
```csharp
// Visibility
bool IsTracksDialogVisible

// Data
ObservableCollection<ABLine> SavedTracks
ABLine? SelectedTrack

// Commands
ICommand ShowTracksDialogCommand
ICommand CloseTracksDialogCommand
ICommand SelectTrackCommand
ICommand DeleteTrackCommand
ICommand SwapABPointsCommand
```

---

## 2. QuickABSelectorPanel (Mode Selector)

### Purpose
A compact mode selector panel (NOT a data entry form) that lets the user choose how to create an AB line by driving.

### UI Layout (from screenshot - 4 buttons in a row)
```
[ A+ Line ] [ Drive A-B ] [ Cancel ] [ Curve ]
```

### Modes Explained

1. **A+ Line** (Heading-based)
   - Sets Point A at current position
   - Creates line using current heading
   - No need to drive to Point B
   - Instant creation based on current direction

2. **Drive A-B** (Two-point straight line)
   - Tap to set Point A at current position
   - Drive to end of desired line
   - Tap again to set Point B
   - Creates straight line between A and B

3. **Curve** (Drive path recording)
   - Tap to start recording
   - Drive the curved path
   - Tap again to stop recording
   - Creates curve line following driven path

4. **Cancel**
   - Cancels current recording mode
   - Returns to normal operation

### State Machine
```
Normal -> A+ Mode (instant creation, back to Normal)
Normal -> Drive A-B Mode -> Recording A -> Tap -> Recording B -> Tap -> Line Created -> Normal
Normal -> Curve Mode -> Recording -> Tap -> Curve Created -> Normal
Any Recording Mode -> Cancel -> Normal
```

### ViewModel Properties Needed
```csharp
// Visibility
bool IsQuickABSelectorVisible

// State
ABCreationMode CurrentABCreationMode  // None, APlusLine, DriveAB, Curve
bool IsRecordingABLine
Position? RecordedPointA
List<Position> RecordedCurvePoints

// Commands
ICommand ShowQuickABSelectorCommand
ICommand CloseQuickABSelectorCommand
ICommand StartAPlusLineCommand      // Instant creation
ICommand StartDriveABCommand        // Begin A-B recording
ICommand StartCurveRecordingCommand // Begin curve recording
ICommand CancelABRecordingCommand   // Cancel any mode
ICommand SetABPointCommand          // Called when user taps during recording
```

### ABCreationMode Enum
```csharp
public enum ABCreationMode
{
    None,
    APlusLine,      // Creates line from current position + heading
    DriveAB,        // Recording A then B points
    Curve           // Recording curve path
}
```

---

## 3. DrawABDialogPanel (Map Drawing)

### Purpose
Allow user to draw/place AB line points directly on the map by tapping, rather than driving.

### UI Layout
- Instructions text
- Map interaction mode (tap to place points)
- Point A marker
- Point B marker
- Confirm/Cancel buttons

### Key Features
- Tap on map to set Point A
- Tap again to set Point B
- Preview line on map
- Confirm or cancel

### ViewModel Properties Needed
```csharp
// Visibility
bool IsDrawABDialogVisible

// State
bool IsDrawingABLine
Position? DrawnPointA
Position? DrawnPointB

// Commands
ICommand ShowDrawABDialogCommand
ICommand CloseDrawABDialogCommand
ICommand ConfirmDrawnABLineCommand
ICommand CancelDrawnABLineCommand
ICommand ClearDrawnPointsCommand
```

---

## Implementation Order

1. **Update BottomNavigationPanel flyout** - Replace current icons with correct ones:
   - Top: ABTracks.png → ShowTracksDialogCommand (existing ShowABTracksDialogCommand)
   - Middle: ABTrackAPlus.png → ShowQuickABSelectorCommand
   - Bottom: ABDraw.png → ShowDrawABDialogCommand

2. **Create QuickABSelectorPanel** - Simple 4-button mode selector
   - Most commonly used feature
   - Simplest to implement (just activates recording modes)

3. **Create TracksDialogPanel** - Track management list
   - Uses existing ABLine model
   - List with CRUD operations

4. **Create DrawABDialogPanel** - Map tap-to-place
   - Requires map interaction integration
   - More complex due to map coordinate handling

---

## Files to Create/Modify

### New Files
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/TracksDialogPanel.axaml`
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/TracksDialogPanel.axaml.cs`
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/QuickABSelectorPanel.axaml`
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/QuickABSelectorPanel.axaml.cs`
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/DrawABDialogPanel.axaml`
- `Shared/AgValoniaGPS.Views/Controls/Dialogs/DrawABDialogPanel.axaml.cs`

### Modified Files
- `Shared/AgValoniaGPS.Views/Controls/Panels/BottomNavigationPanel.axaml` - Update flyout icons
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` - Add properties and commands
- `Shared/AgValoniaGPS.Models/ABCreationMode.cs` - New enum (if needed)
- `Platforms/AgValoniaGPS.Desktop/Views/MainWindow.axaml` - Add dialog panels
- `Platforms/AgValoniaGPS.iOS/Views/MainView.axaml` - Add dialog panels

### Icon Files Added
- `ABDraw.png` - Copied from AgOpenGPS
- `ABTrackAPlus.png` - Copied from AgOpenGPS (renamed from ABTrackA+.png)

---

## Current Progress

- [x] Copied ABDraw.png icon
- [x] Copied ABTrackAPlus.png icon
- [x] Update BottomNavigationPanel flyout with correct icons
- [x] Create TracksDialogPanel
- [x] Create QuickABSelectorPanel
- [x] Create DrawABDialogPanel
- [x] Add ViewModel properties and commands
- [x] Add dialogs to MainWindow.axaml
- [x] Add dialogs to MainView.axaml (iOS)
- [x] Test implementation (builds successfully)
