# AutoSteer Configuration Panel Implementation Plan

## Implementation Checklist

### Phase 1: Model & Store
- [x] Create `AutoSteerConfig.cs` model with all properties
- [x] Add `AutoSteer` property to `ConfigurationStore.cs`
- [ ] Add serialization support for profile save/load

### Phase 2: ViewModel
- [x] Create `AutoSteerConfigViewModel.cs`
- [x] Implement edit commands for all parameters
- [x] Implement test mode (free drive) logic
- [x] Implement Send+Save command

### Phase 3: Panel UI - Compact Mode
- [x] Create `AutoSteerConfigPanel.axaml` main panel structure
- [x] Tab 1: Pure Pursuit settings (response slider, integral, curve graphic)
- [x] Tab 2: Sensor settings (WAS zero, counts/degree, ackermann, max angle)
- [x] Tab 3: Timing settings (deadzone, speed factor, acquire factor)
- [x] Tab 4: Gain settings (proportional, max PWM, min PWM)
- [x] Status bar (Set, Actual, Error displays)
- [x] Expand button to switch to full mode

### Phase 4: Panel UI - Full Mode (Right Side Tabs)
- [x] Tab 5: Turn Sensors (encoder, pressure, current toggles)
- [x] Tab 6: Hardware Config (invert toggles, dropdowns)
- [x] Tab 7: Algorithm (U-turn comp, sidehill, Stanley/Pure toggle)
- [x] Tab 8: Speed Limits (manual turns, min/max speed)
- [x] Tab 9: Display Settings (line width, nudge, lightbar)

### Phase 5: Test Mode Panel
- [x] Free Drive toggle button with tractor icon
- [x] Steer left/right buttons
- [x] PWM display
- [x] Steer offset control
- [x] REC button for diameter measurement
- [x] Steer angle and diameter displays

### Phase 6: PGN Communication
- [x] Add `BuildSteerSettingsPgn()` (PGN 252) to PgnBuilder
- [x] Add `BuildSteerConfigPgn()` (PGN 251) to PgnBuilder
- [ ] Handle incoming PGN 253 for actual steer angle display
- [x] Wire Send+Save to broadcast PGNs to modules

### Phase 7: Integration
- [x] Add `IsAutoSteerConfigPanelVisible` to MainViewModel
- [x] Add panel to MainWindow.axaml (via DialogOverlayHost)
- [x] Add panel to MainView.axaml (iOS) (via DialogOverlayHost)
- [x] Add button to LeftNavigationPanel to open panel
- [ ] Test with simulator
- [ ] Test with hardware module

---

## Overview

Implement a dual-mode (Compact/Full-Size) AutoSteer configuration panel following AgOpenGPS patterns. The panel configures steering control parameters that are sent to hardware modules via PGN 252/251.

## Panel Modes

### Compact Mode (Default)
- 4 icon tabs for core steering parameters
- Expand button (blue arrow) to switch to full mode
- Status bar: Set angle, Actual angle, Error

### Full-Size Mode
- Original 4 tabs on left side
- 5 additional tabs on right side for advanced settings
- Test mode panel in lower-left (free drive mode)
- Action buttons: Wizard, Reset, Send+Save, Close

## Tab Structure

### Left Side (Compact + Full)

**Tab 1: Pure Pursuit / Stanley** (vehicle icon)
- Algorithm label (Pure Pursuit / Stanley)
- Steer Response slider (Fast 1-10 Slow) - maps to `goalPointLookAheadHold`
- Response curve graphic
- Integral slider (0-100) - maps to `purePursuitIntegralGain` or `stanleyIntegralGain`

**Tab 2: Steering Sensor** (steering wheel icon)
- WAS Zero button + display
- Counts Per Degree slider (0.01-1.0)
- Ackermann slider (0-200, default 100)
- Max Steer Angle slider (10-90, default 45)

**Tab 3: Deadzone/Timing** (speedometer icon)
- Deadzone: Heading (0.0-5.0), On-Delay (0-50)
- Speed Factor slider (0.5-3.0)
- Acquire Factor slider (0.5-1.0)
- Display: Distance, Acquire, Hold values

**Tab 4: Gain** (gauge icon)
- Proportional Gain slider (1-100, default 10)
- Max Limit (PWM) slider (50-255, default 235)
- Minimum to Move slider (1-50, default 5)

### Right Side (Full Mode Only)

**Tab 5: Turn Sensors** (steering wheel + sensors icon)
- Turn Sensor toggle button
- Pressure Turn toggle button
- Current Turn toggle button

**Tab 6: Hardware Config** (relay icon)
- Danfoss toggle
- Invert WAS toggle
- Invert Motor Direction toggle
- Invert Relays toggle
- Motor Driver Type dropdown (IBT2, Cytron)
- AD Converter dropdown (Single, Differential)
- IMU Axis Swap dropdown (X, Y)
- External Enable dropdown (None, Switch, Button)

**Tab 7: Steering Algorithm** (steering wheel icon)
- U-Turn Compensation slider (Out 0-100 In)
- Side Hill per Degree Compensation slider (0-1.0)
- Stanley/Pure toggle button
- Steer while reverse toggle button

**Tab 8: Speed Limits** (bell icon)
- Manual Turns toggle + speed display
- Min Speed button (km/h)
- Max Speed button (km/h)

**Tab 9: Display Settings** (gear icon)
- Line Width (pixels)
- Nudge Distance (cm)
- Next Guidance Line (seconds)
- Cm -> Pixel scale
- Guidance Bar: Lightbar toggle, Steering Bar toggle, On/Off toggle

### Bottom Section (Full Mode)

**Test Mode Panel (Lower Left)**
- Free Drive toggle button (tractor icon)
- Left steering button
- Right steering button
- PWM display
- Steer offset control (center/+5)
- REC button for diameter measurement
- Steer Angle display
- Diameter display

**Action Buttons (Lower Right)**
- Wizard button
- Reset button
- Send + Save button
- Close (checkmark) button

## Files to Create/Modify

### New Files

1. **Model: `AutoSteerConfig.cs`**
   - Location: `Shared/AgValoniaGPS.Models/Configuration/AutoSteerConfig.cs`
   - Properties for all steering parameters
   - Extends ReactiveObject for change notification

2. **Panel: `AutoSteerConfigPanel.axaml/.cs`**
   - Location: `Shared/AgValoniaGPS.Views/Controls/Dialogs/AutoSteerConfigPanel.axaml`
   - Dual-mode overlay panel (not in ConfigurationDialog)
   - TabControl with icon tabs

3. **Sub-tabs (6 files)**:
   - `AutoSteerPurePursuitTab.axaml`
   - `AutoSteerSensorTab.axaml`
   - `AutoSteerTimingTab.axaml`
   - `AutoSteerGainTab.axaml`
   - `AutoSteerHardwareTab.axaml`
   - `AutoSteerDisplayTab.axaml`

4. **ViewModel: `AutoSteerConfigViewModel.cs`**
   - Location: `Shared/AgValoniaGPS.ViewModels/AutoSteerConfigViewModel.cs`
   - Edit commands for all parameters
   - Test mode logic
   - Save/Send commands

### Files to Modify

1. **ConfigurationStore.cs**
   - Add `AutoSteer` property of type `AutoSteerConfig`

2. **MainViewModel.cs**
   - Add `IsAutoSteerConfigPanelVisible` property
   - Add `ShowAutoSteerConfigCommand`
   - Wire up Send PGN functionality

3. **MainWindow.axaml / MainView.axaml**
   - Add `<dialogs:AutoSteerConfigPanel/>` overlay

4. **PgnBuilder.cs**
   - Add `BuildSteerSettingsPgn()` for PGN 252
   - Add `BuildSteerConfigPgn()` for PGN 251

## AutoSteerConfig Model

```csharp
public class AutoSteerConfig : ReactiveObject
{
    // Tab 1: Pure Pursuit / Stanley
    public double SteerResponseHold { get; set; } = 3.0;  // goalPointLookAheadHold
    public double IntegralGain { get; set; } = 0.0;       // 0-1.0
    public bool IsStanleyMode { get; set; } = false;

    // Tab 2: Sensor
    public int WasOffset { get; set; } = 0;               // WAS zero offset
    public double CountsPerDegree { get; set; } = 0.1;    // 0.01-1.0
    public int Ackermann { get; set; } = 100;             // 0-200
    public int MaxSteerAngle { get; set; } = 45;          // 10-90 degrees

    // Tab 3: Timing
    public double DeadzoneHeading { get; set; } = 0.1;    // degrees
    public int DeadzoneDelay { get; set; } = 5;           // counts
    public double SpeedFactor { get; set; } = 1.0;        // goalPointLookAheadMult
    public double AcquireFactor { get; set; } = 0.9;      // goalPointAcquireFactor

    // Tab 4: Gain
    public int ProportionalGain { get; set; } = 10;       // 1-100
    public int MaxPwm { get; set; } = 235;                // High PWM limit
    public int MinPwm { get; set; } = 5;                  // Min to move

    // Tab 5: Turn Sensors
    public bool TurnSensorEnabled { get; set; } = false;
    public bool PressureSensorEnabled { get; set; } = false;
    public bool CurrentSensorEnabled { get; set; } = false;

    // Tab 6: Hardware
    public bool DanfossEnabled { get; set; } = false;
    public bool InvertWas { get; set; } = false;
    public bool InvertMotor { get; set; } = false;
    public bool InvertRelays { get; set; } = false;
    public int MotorDriver { get; set; } = 0;             // 0=IBT2, 1=Cytron
    public int AdConverter { get; set; } = 0;             // 0=Differential, 1=Single
    public int ImuAxisSwap { get; set; } = 0;             // 0=X, 1=Y
    public int ExternalEnable { get; set; } = 0;          // 0=None, 1=Switch, 2=Button

    // Tab 7: Algorithm
    public double UTurnCompensation { get; set; } = 0.0;  // -100 to 100
    public double SideHillCompensation { get; set; } = 0.0;
    public bool SteerInReverse { get; set; } = false;

    // Tab 8: Speed Limits
    public bool ManualTurnsEnabled { get; set; } = false;
    public double ManualTurnsSpeed { get; set; } = 12.0;  // km/h
    public double MinSteerSpeed { get; set; } = 0.0;      // km/h
    public double MaxSteerSpeed { get; set; } = 15.0;     // km/h

    // Tab 9: Display
    public int LineWidth { get; set; } = 2;               // pixels
    public int NudgeDistance { get; set; } = 20;          // cm
    public double NextGuidanceTime { get; set; } = 1.5;   // seconds
    public int CmPerPixel { get; set; } = 5;
    public bool LightbarEnabled { get; set; } = true;
    public bool SteerBarEnabled { get; set; } = false;
    public bool GuidanceBarOn { get; set; } = true;

    // PGN 251 bitfield helpers
    public byte GetSetting0Byte() { ... }
    public byte GetSetting1Byte() { ... }
}
```

## PGN Communication

### PGN 252 - Steer Settings (sent when Save clicked)
```
[0x80, 0x81, 0x7F, 0xFC, 0x08,
 gainP,           // Byte 5
 highPWM,         // Byte 6
 lowPWM,          // Byte 7 (highPWM / 3)
 minPWM,          // Byte 8
 countsPerDeg,    // Byte 9
 steerOffsetHi,   // Byte 10
 steerOffsetLo,   // Byte 11
 ackermanFix,     // Byte 12
 CRC]             // Byte 13
```

### PGN 251 - Steer Config (sent with PGN 252)
```
[0x80, 0x81, 0x7F, 0xFB, 0x05,
 set0,            // Byte 5 (bitfield: invertWAS, invertRelays, invertMotor, adConv, motorDrive, steerEnable)
 pulseCount,      // Byte 6
 minSpeed,        // Byte 7
 set1,            // Byte 8 (bitfield: danfoss, pressure, current, imuAxis)
 angVel,          // Byte 9
 CRC]             // Byte 10
```

## UI Layout Pattern

Use existing ConfigurationDialog patterns:
- Dark theme (#DD1E2A38, #DD253545, #DD2C3E50)
- ParamCard borders for grouping
- TappableValue buttons for numeric input
- Sliders with Fast/Slow or Min/Max labels
- Icon images for tabs (40x40)
- Status bar at bottom with Set/Act/Err displays

## Implementation Order

1. Create `AutoSteerConfig.cs` model
2. Add to `ConfigurationStore.cs`
3. Create `AutoSteerConfigViewModel.cs` with edit commands
4. Create main `AutoSteerConfigPanel.axaml` with compact mode tabs
5. Add expand/collapse logic for full mode
6. Create sub-tab content for each tab
7. Add PGN 252/251 builder methods
8. Wire up Send+Save to send PGNs
9. Add test mode (free drive) functionality
10. Add panel to MainWindow/MainView
11. Test with hardware module

## Key Design Decisions

1. **Standalone Panel**: Not embedded in ConfigurationDialog - opened via dedicated button in LeftNavigationPanel
2. **Dual-Mode**: Window expands horizontally for full mode (not vertical tabs)
3. **Sliders vs Tappable**: Use sliders for frequently-adjusted values (gain, response), tappable buttons for calibration values
4. **Status Bar**: Real-time display of Set angle, Actual angle, Error - requires incoming PGN 253 parsing
5. **Test Mode**: Free drive mode sends direct steer angle commands for calibration

## Critical Files Reference

- `/Users/chris/Code/AgValoniaGPS2/SourceCode/GPS/Forms/Settings/FormSteer.cs` - AgOpenGPS implementation
- `/Users/chris/Code/AgValoniaGPS3/Reference/PGN.md` - PGN specifications
- `/Users/chris/Code/AgValoniaGPS3/Shared/AgValoniaGPS.Views/Controls/Dialogs/Configuration/ConfigurationDialog.axaml` - Dialog pattern
- `/Users/chris/Code/AgValoniaGPS3/Shared/AgValoniaGPS.ViewModels/ConfigurationViewModel.cs` - Command pattern
- `/Users/chris/Code/AgValoniaGPS3/Shared/AgValoniaGPS.Services/AutoSteer/PgnBuilder.cs` - PGN construction
