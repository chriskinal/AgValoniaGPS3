# Machine Control Configuration Tab Plan

## Overview

Add a Machine Control configuration tab to the Configuration dialog with two sub-tabs:
1. **Machine Module** - Hydraulic lift configuration and user values
2. **Pin Config** - 24-pin relay assignment grid

## Screenshots Analysis

### Tab 1: Machine Module
- **Header**: "Machine Module"
- **Section**: "Hydraulic Lift Config"
- **Left area**:
  - Enable toggle button (power icon)
  - Raise Time value with graphic (ConMa_LiftRaiseTime.png) + "Plant Pop" label
  - Look Ahead value (ConV_GuidanceLookAhead.png available)
  - Lower Time value with graphic (ConMa_LiftLowerTime.png)
  - Invert Relay toggle button (ConSt_InvertRelay.png)
- **Right area**:
  - User 1-4 numeric values (custom user-defined values)
- **Bottom**: "Send + Save" button

### Tab 2: Pin Configuration
- **Grid**: 24 pins in 5x5 layout (last row has 4 pins)
- **Each pin**: Label + dropdown selector
- **Dropdown options**: Section 1-16, Hyd Up, Hyd Down, Tram L, Tram R, Geo Stop, "-" (none)
- **Bottom buttons**: Reset, Upload, Send + Save

---

## Implementation Plan

### Phase 1: Create MachineConfig Model

**File**: `Shared/AgValoniaGPS.Models/Configuration/MachineConfig.cs`

```csharp
public class MachineConfig : ReactiveObject
{
    // Hydraulic Lift Settings
    private bool _hydraulicLiftEnabled;
    private int _raiseTime = 4;           // seconds
    private double _lookAhead = 2.0;      // seconds
    private int _lowerTime = 2;           // seconds
    private bool _invertRelay;

    // User Custom Values (User 1-4)
    private int _user1Value = 1;
    private int _user2Value = 2;
    private int _user3Value = 3;
    private int _user4Value = 4;

    // Pin Assignments (24 pins, stored as enum or int)
    private int[] _pinAssignments = new int[24];
}
```

**Pin Assignment Enum**:
```csharp
public enum PinFunction
{
    None = 0,
    Section1 = 1,
    Section2 = 2,
    // ... Section3-16
    HydUp = 17,
    HydDown = 18,
    TramLeft = 19,
    TramRight = 20,
    GeoStop = 21
}
```

### Phase 2: Add to ConfigurationStore

**File**: `Shared/AgValoniaGPS.Models/Configuration/ConfigurationStore.cs`

Add:
```csharp
public MachineConfig Machine { get; } = new();
```

### Phase 3: Create Tab Structure

**Files to create**:
```
Shared/AgValoniaGPS.Views/Controls/Dialogs/Configuration/
├── MachineControlConfigTab.axaml       # Parent tab with sub-tabs
├── MachineControlConfigTab.axaml.cs
└── MachineSubTabs/
    ├── MachineModuleSubTab.axaml       # Hydraulic lift config
    ├── MachineModuleSubTab.axaml.cs
    ├── PinConfigSubTab.axaml           # 24-pin grid
    └── PinConfigSubTab.axaml.cs
```

### Phase 4: MachineModuleSubTab Layout

```
┌─────────────────────────────────────────────────────────────────┐
│ Hydraulic Lift Config                              │ User 1 [1] │
├─────────────────────────────────────────────────────│ User 2 [2] │
│ ┌────────┐  ┌────────┐ ┌────────────────┐         │ User 3 [3] │
│ │ Enable │  │ Raise  │ │   [Graphic]    │         │ User 4 [4] │
│ │  (⏻)   │  │ Time   │ │   Raise Time   │         └────────────┘
│ └────────┘  │  [4]   │ │                │
│             │PlantPop│ └────────────────┘
│ ┌────────┐  ├────────┤ ┌────────────────┐
│ │  Look  │  │ Lower  │ │   [Graphic]    │
│ │ Ahead  │  │ Time   │ │   Lower Time   │
│ │ [2.0]  │  │  [2]   │ │                │
│ └────────┘  └────────┘ └────────────────┘
│ ┌─────────────────────┐
│ │ Invert Relay [img]  │         [Send + Save]
│ └─────────────────────┘
└─────────────────────────────────────────────────────────────────┘
```

### Phase 5: PinConfigSubTab Layout

```
┌───────────────────────────────────────────────────────────────────┐
│  Pin 1       Pin 2       Pin 3       Pin 4       Pin 5            │
│ [Section 1▼][Section 2▼][Section 3▼][Section 4▼][Section 5▼]     │
│                                                                    │
│  Pin 6       Pin 7       Pin 8       Pin 9       Pin 10           │
│ [Section 6▼][   -    ▼][   -    ▼][   -    ▼][   -    ▼]         │
│                                                                    │
│  Pin 11      Pin 12      Pin 13      Pin 14      Pin 15           │
│ [   -    ▼][   -    ▼][   -    ▼][   -    ▼][   -    ▼]         │
│                                                                    │
│  Pin 16      Pin 17      Pin 18      Pin 19      Pin 20           │
│ [   -    ▼][   -    ▼][   -    ▼][   -    ▼][   -    ▼]         │
│                                                                    │
│  Pin 21      Pin 22      Pin 23      Pin 24                       │
│ [   -    ▼][   -    ▼][   -    ▼][   -    ▼]                     │
│                                                                    │
│    [Reset]     [Upload]                      [Send + Save]        │
└───────────────────────────────────────────────────────────────────┘
```

### Phase 6: Add Commands to ConfigurationViewModel

```csharp
// Machine Module Commands
public ICommand ToggleHydraulicLiftCommand { get; }
public ICommand EditRaiseTimeCommand { get; }
public ICommand EditLookAheadCommand { get; }
public ICommand EditLowerTimeCommand { get; }
public ICommand ToggleInvertRelayCommand { get; }
public ICommand EditUser1Command { get; }
public ICommand EditUser2Command { get; }
public ICommand EditUser3Command { get; }
public ICommand EditUser4Command { get; }

// Pin Config Commands
public ICommand ResetPinConfigCommand { get; }
public ICommand UploadPinConfigCommand { get; }
public ICommand SendAndSaveMachineConfigCommand { get; }

// Property for Machine accessor
public MachineConfig Machine => Config.Machine;
```

### Phase 7: Add Tab to ConfigurationDialog

**File**: `Shared/AgValoniaGPS.Views/Controls/Dialogs/ConfigurationDialog.axaml`

Add new TabItem after U-Turn tab (before Data Sources):
```xml
<!-- Machine Control Tab -->
<TabItem ToolTip.Tip="Machine Control">
    <TabItem.Header>
        <Image Source="avares://AgValoniaGPS.Views/Assets/Icons/Config/ConS_ModulesMachine.png"
               Width="40" Height="40" Stretch="Uniform"/>
    </TabItem.Header>
    <config:MachineControlConfigTab/>
</TabItem>
```

---

## Available Assets

| Asset | Purpose |
|-------|---------|
| ConS_ModulesMachine.png | Tab icon |
| ConMa_LiftRaiseTime.png | Raise time graphic |
| ConMa_LiftLowerTime.png | Lower time graphic |
| ConV_GuidanceLookAhead.png | Look ahead graphic |
| ConSt_InvertRelay.png | Invert relay toggle |

---

## Pin Function Options (Dropdown)

| Value | Display Text |
|-------|-------------|
| 0 | - |
| 1-16 | Section 1-16 |
| 17 | Hyd Up |
| 18 | Hyd Down |
| 19 | Tram Left |
| 20 | Tram Right |
| 21 | Geo Stop |

---

## Implementation Order

1. Create `MachineConfig.cs` model with all properties
2. Create `PinFunction` enum
3. Add `Machine` property to `ConfigurationStore`
4. Create `MachineControlConfigTab.axaml/cs` (parent with sub-tabs)
5. Create `MachineModuleSubTab.axaml/cs`
6. Create `PinConfigSubTab.axaml/cs`
7. Add commands to `ConfigurationViewModel`
8. Add tab to `ConfigurationDialog.axaml`
9. Build and test

---

## Notes

- "Send + Save" button sends configuration to hardware module via UDP and saves locally
- Pin assignments default: Pins 1-6 = Section 1-6, rest = None
- User values 1-4 are custom configurable values sent to machine module
- Look Ahead is in seconds (how far ahead to predict implement position)
- Raise/Lower Time in seconds (hydraulic timing)
