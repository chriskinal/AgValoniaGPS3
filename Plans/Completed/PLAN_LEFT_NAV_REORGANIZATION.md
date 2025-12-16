# Left Navigation Panel Reorganization Plan

## Overview

This document outlines the reorganization of the left navigation panel menus to improve organization, reduce redundancy, and create a more logical grouping of functionality.

## Current Structure

The left navigation panel currently has these main buttons (top to bottom):
1. **File Menu (Hamburger)** - Application settings and general functions
2. **View Settings** - Display/view options
3. **Tools** - Miscellaneous tools
4. **Configuration (Gear)** - Vehicle and app configuration
5. **Job Menu** - Field operations (new/open/close)
6. **Field Tools** - Boundary, headland, tram lines
7. **AutoSteer Config** - (No panel implemented)
8. **Data I/O** - Import/export functions

---

## Detailed Changes by Menu

### 1. File Menu (Hamburger) → Rename to "Application Settings"

**Current Items:**
- New Profile
- Load Profile
- Language
- Simulator
- Enter Sim Coords
- Data I/O
- Kiosk Mode
- Reset ALL
- About
- AgShare API
- Help

**Changes:**

| Item | Action | Reason |
|------|--------|--------|
| New Profile | **REMOVE** | Move to Vehicle Config (Gear) panel |
| Load Profile | **REMOVE** | Move to Vehicle Config (Gear) panel |
| Language | Keep | OK |
| Simulator | Keep | OK |
| Enter Sim Coords | **REMOVE** | Move to Simulator Panel |
| Data I/O | **REMOVE** | Has dedicated button on nav bar |
| Kiosk Mode | Keep | OK |
| Reset ALL | **CLARIFY** | Rename to clarify what gets reset |
| About | Keep | OK |
| AgShare API | Keep | OK |
| Help | Keep | OK |
| View All Settings | **ADD** | New item |
| App Directories | **ADD** | New item |
| App Colors | **ADD** | New item |
| Hotkeys | **ADD** | New item |

**New Structure:**
```
Application Settings
├── Language
├── Kiosk Mode
├── Reset All Settings (renamed)
├── ─────────────────
├── View All Settings (NEW)
├── App Directories (NEW)
├── App Colors (NEW)
├── Hotkeys (NEW)
├── ─────────────────
├── Simulator
├── ─────────────────
├── AgShare API
├── Help
└── About
```

---

### 2. Tools Menu

**Current Items:**
- Steer Wizard
- Steer Chart
- Heading Chart
- XTE Chart
- Roll Correction
- Boundary Tool
- Smooth AB Curve
- Delete Contours
- Offset Fix
- Log Viewer

**Changes:**

| Item | Action | Reason |
|------|--------|--------|
| Steer Wizard | Keep | Wizards category |
| Steer Chart | Keep | Charts category |
| Heading Chart | Keep | Charts category |
| XTE Chart | Keep | Charts category |
| Roll Correction | Keep (for now) | Will move to AutoSteer Config when that panel is implemented |
| Boundary Tool | **REMOVE** | Already in Field Tools |
| Smooth AB Curve | **REMOVE** | Move to Track Options |
| Delete Contours | **REMOVE** | Move to Track Options |
| Offset Fix | **REMOVE** | Move to Job Menu |
| Log Viewer | Keep | OK |

**New Structure:**
```
Tools
├── Wizards
│   └── Steer Wizard
├── Charts
│   ├── Steer Chart
│   ├── Heading Chart
│   └── XTE Chart
├── Roll Correction (temporary - will move to AutoSteer Config later)
└── Log Viewer
```

---

### 3. Vehicle Configuration (Gear Menu)

**Current Items:**
- Configuration
- Auto Steer
- View All Settings
- Directories
- GPS Data
- Colors
- Multi-Section Colors
- HotKeys

**Changes:**

| Item | Action | Reason |
|------|--------|--------|
| Configuration | Keep | Main vehicle config |
| Auto Steer | **REMOVE** | Has its own button on Left Nav Panel |
| View All Settings | **REMOVE** | Move to Application Settings |
| Directories | **REMOVE** | Move to Application Settings |
| GPS Data | **REMOVE** | Not needed in this panel |
| Colors | **REMOVE** | Move to Application Settings as "App Colors" |
| Multi-Section Colors | Keep | Vehicle-specific (section colors) |
| HotKeys | **REMOVE** | Move to Application Settings |
| New Profile | **ADD** | Moved from File Menu |
| Load Profile | **ADD** | Moved from File Menu |

**New Structure:**
```
Vehicle Configuration
├── New Profile (moved from File Menu)
├── Load Profile (moved from File Menu)
├── ─────────────────
├── Configuration (vehicle settings)
└── Section Colors (renamed from Multi-Section Colors)
```

---

### 4. Job Menu

**Current Items:**
- ISO-XML
- Close
- From KML
- Drive In
- From Existing
- Open
- New From Default
- Resume
- AgShare Download
- AgShare Upload

**Changes:**

| Item | Action | Reason |
|------|--------|--------|
| All existing items | Keep | OK |
| Offset Fix | **ADD** | Moved from Tools |

**New Structure:**
```
Job Menu (Start New Field)
├── New From Default
├── From Existing
├── From KML
├── ISO-XML
├── ─────────────────
├── Open
├── Resume
├── Close
├── ─────────────────
├── Drive In
├── Offset Fix (moved from Tools)
├── ─────────────────
├── AgShare Download
└── AgShare Upload
```

---

### 5. Field Tools

**No changes required.** Keep as is.

**Current Structure:**
```
Field Tools
├── Boundary
├── Headland
├── Headland Builder
├── Tram Lines
├── Tram Lines Builder
├── Delete Applied Area
├── Flag By Lat Lon
├── Recorded Path
└── Import Tracks
```

---

### 6. AutoSteer Config

**Current State:** Button exists but no panel implemented.

**Action:** Create new `AutoSteerConfigPanel` with original AgOpenGPS autosteer functions.

**New Structure:**
```
AutoSteer Configuration
├── Steer Settings
├── Stanley/Pure Pursuit Settings
├── Roll Correction (moved from Tools)
├── Gain Settings
├── PID Settings
└── IMU Settings
```

*Note: Specific items TBD based on original AgOpenGPS autosteer configuration options.*

---

### 7. Simulator Panel

**Current Items:**
- Simulator Enabled checkbox
- Reset / Reset Steering buttons
- Steering controls (L/R buttons, slider)
- Speed slider
- Stop button
- Reverse Direction button

**Changes:**

| Item | Action | Reason |
|------|--------|--------|
| All existing items | Keep | OK |
| Enter Sim Coords | **ADD** | Button to open SimCoordsDialog (moved from File Menu) |

**New Structure:**
```
GPS Simulator
├── Simulator Enabled
├── Enter Sim Coords (NEW - moved from File Menu)
├── ─────────────────
├── Reset / Reset Steering
├── Steering Controls
├── Speed Controls
└── Reverse Direction
```

---

### 8. Track Options (Rename from "AB Line Options")

**Note:** This panel may be accessed from the bottom nav bar or right panel. Need to verify location.

**Changes:**

| Item | Action | Reason |
|------|--------|--------|
| Existing items | Keep | OK |
| Smooth AB Curve | **ADD** | Moved from Tools |
| Delete Contours | **ADD** | Moved from Tools |

---

## Implementation Tasks

### Phase 1: File Menu Reorganization
- [ ] Remove "New Profile" button
- [ ] Remove "Load Profile" button
- [ ] Remove "Enter Sim Coords" button
- [ ] Remove "Data I/O" button
- [ ] Add "View All Settings" button
- [ ] Add "App Directories" button
- [ ] Add "App Colors" button
- [ ] Add "Hotkeys" button
- [ ] Rename "Reset ALL" to clarify scope (e.g., "Reset All Settings")
- [ ] Reorder items per new structure

### Phase 2: Tools Menu Cleanup
- [ ] Remove "Boundary Tool" button
- [ ] Remove "Smooth AB Curve" button
- [ ] Remove "Delete Contours" button
- [ ] Remove "Offset Fix" button
- [ ] Keep "Roll Correction" (will move to AutoSteer Config in future)
- [ ] Reorganize remaining items (Wizards, Charts, Roll Correction, Log Viewer)

### Phase 3: Vehicle Configuration Updates
- [ ] Add "New Profile" button
- [ ] Add "Load Profile" button
- [ ] Remove "Auto Steer" button (has own button on left nav)
- [ ] Remove "View All Settings" button
- [ ] Remove "Directories" button
- [ ] Remove "GPS Data" button
- [ ] Remove "Colors" button (general app colors)
- [ ] Remove "HotKeys" button
- [ ] Rename "Multi-Section Colors" to "Section Colors"
- [ ] Reorder items per new structure

### Phase 4: Job Menu Updates
- [ ] Add "Offset Fix" button (from Tools)
- [ ] Reorder items for logical grouping

### Phase 5: Simulator Panel Updates
- [ ] Add "Enter Sim Coords" button to SimulatorPanel

### Phase 6: AutoSteer Config Panel (DEFERRED)

**Note:** The AutoSteer Config panel is complex and requires a separate planning session. For this reorganization, we will:
- [ ] Leave the AutoSteer Config button as-is (non-functional placeholder)
- [ ] Create a separate task/issue for AutoSteer Config panel implementation
- [ ] Roll Correction will remain in Tools panel until AutoSteer Config is implemented

### Phase 7: Track Options Updates
- [ ] Locate existing Track/AB Line Options panel
- [ ] Add "Smooth AB Curve" button
- [ ] Add "Delete Contours" button
- [ ] Rename panel to "Track Options" if needed

---

## Files to Modify

| File | Changes |
|------|---------|
| `FileMenuPanel.axaml` | Remove items, add new items, reorder |
| `ToolsPanel.axaml` | Remove 5 items, reorganize layout |
| `ConfigurationPanel.axaml` | Add/remove items, reorder |
| `JobMenuPanel.axaml` | Add Offset Fix, reorder |
| `SimulatorPanel.axaml` | Add Enter Sim Coords button |
| `AutoSteerConfigPanel.axaml` | **NEW FILE** |
| `AutoSteerConfigPanel.axaml.cs` | **NEW FILE** |
| `LeftNavigationPanel.axaml` | Add AutoSteerConfigPanel reference |
| `MainViewModel.cs` | Add commands/properties for new functionality |
| Track Options panel (TBD) | Add Smooth AB Curve, Delete Contours |

---

## Questions and Answers

1. **"Reset ALL"** - What exactly does this reset?
   - **Answer:** Need to research in original AgOpenGPS codebase.

2. **AutoSteer Config** - What specific AgOpenGPS autosteer functions should be included?
   - **Answer:** This is a very complex panel in the original AgOpenGPS. Recommend deferring to a separate planning session. For now, leave the button as-is (non-functional) and create a separate task for AutoSteer Config panel implementation.

3. **Track Options location** - Is this in the bottom panel, right panel, or a dialog?
   - **Answer:** Stays in the bottom panel. Included in this plan because Smooth AB Curve and Delete Contours need to move there.

4. **App Colors vs Section Colors** - Confirm distinction between general app colors and section-specific colors.
   - **Answer:**
     - **App Colors:** Colors for the map area background when there is no PNG loaded. Also includes sky color for 3D map view (not yet implemented).
     - **Section Colors:** The colors painted on the map to indicate which sections have been worked/applied.
