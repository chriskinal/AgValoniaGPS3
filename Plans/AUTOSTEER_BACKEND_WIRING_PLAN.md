# AutoSteer Config Panel - Backend Wiring Plan

## Current State

### UI Complete
- 9-tab configuration panel built
- Numeric input dialogs working
- Toggle buttons with visual feedback
- Test mode UI with steer controls

### Models Complete
- `AutoSteerConfig` - All settings with reactive properties
- `PgnBuilder.BuildSteerSettingsPgn()` - PGN 252 builder
- `PgnBuilder.BuildSteerConfigPgn()` - PGN 251 builder

### Partially Wired
- `SendAndSaveCommand` - Sends PGN 251/252, but needs validation
- `_udpService` injected but minimally used

## What Needs Wiring

### 1. Receive PGN 253 (Steer Data FROM Module)

The steering module sends PGN 253 with actual sensor data:

```
Byte 0-1: Header (0x80, 0x81)
Byte 2:   Source (module ID)
Byte 3:   PGN (0xFD = 253)
Byte 4:   Length (8)
Byte 5-6: Actual steer angle * 100 (signed int16)
Byte 7-8: Heading from IMU * 16 (unsigned int16)
Byte 9:   Roll from IMU (signed byte, degrees)
Byte 10:  Switch status byte
          bit 0: Steer switch
          bit 1: Work switch
          bit 2: Remote steer button
          bit 3-7: Reserved
Byte 11:  PWM display (0-255)
Byte 12:  Reserved
Byte 13:  CRC
```

**Tasks:**
- [x] Add PGN 253 parser to `PgnBuilder.TryParseSteerData()`
- [x] Create `SteerModuleData` record for parsed data
- [x] Expose `ActualSteerAngle` on `AutoSteerConfigViewModel`
- [x] Expose `PwmDisplay` for the PWM bar visualization
- [ ] Expose `SwitchStatus` for steer/work switch indicators
- [x] Update status bar in config panel with live values

### 2. Profile Save/Load Integration

**Current:** Settings stored in `ConfigurationStore.AutoSteer`
**Need:** Persist to disk with vehicle profile

**Tasks:**
- [ ] Ensure `AutoSteerConfig` serializes with profile (JSON)
- [ ] Add `LoadProfile` event handling to reload UI when profile changes
- [ ] Mark profile as "dirty" when settings change (`Config.HasUnsavedChanges`)
- [ ] `SendAndSaveCommand` should:
  1. Send PGN 251 + 252 to module
  2. Save profile to disk
  3. Clear dirty flag

### 3. Zero WAS Calibration (Tab 2)

**Current:** ✅ Implemented
**Implementation:** Uses smoothed actual angle from PGN 253 to calculate offset correction.

**Formula:**
- newOffset = currentOffset + (currentAngle × countsPerDegree)
- This makes the current reading become zero

**Tasks:**
- [x] Use smoothed actual angle from PGN 253 (no separate protocol needed)
- [x] Implement `ZeroWasCommand` to calculate and apply offset correction
- [x] Send updated PGN 252 to module immediately

### 4. Test Mode Steer Commands

**Current:** Steer left/right/zero buttons exist but only adjust local values
**Need:** Send steer commands to module in free drive mode

**Tasks:**
- [ ] Define test mode PGN or reuse existing steer command format
- [ ] `SteerLeftCommand` - Send negative steer angle increment
- [ ] `SteerRightCommand` - Send positive steer angle increment
- [ ] `ZeroSteerAngleCommand` - Send zero steer angle
- [ ] `ToggleFreeDriveCommand` - Enable/disable motor free drive mode
- [ ] Rate limit commands (don't flood UDP)

### 5. Diameter Measurement (Tab 2)

**Current:** Rec button has TODO for diameter calculation
**Need:** Record turning circle to auto-calculate counts per degree

**Flow:**
1. User clicks "Rec" to start recording
2. User drives in a full circle (360°)
3. Track WAS counts through the turn
4. Calculate: countsPerDegree = totalCounts / 360
5. Set `AutoSteer.CountsPerDegree = calculated`

**Tasks:**
- [ ] Add recording state to ViewModel
- [ ] Accumulate WAS counts from PGN 253 during recording
- [ ] Track heading change (need IMU data or GPS heading)
- [ ] Calculate and suggest counts per degree value
- [ ] Allow user to accept or reject calculated value

### 6. Reset to Defaults

**Current:** ✅ Implemented

**Tasks:**
- [x] Define default values for all AutoSteerConfig properties (in field initializers)
- [x] Add `ResetToDefaults()` method on `AutoSteerConfig`
- [x] Show confirmation dialog before reset
- [x] Send updated PGN 251/252 after reset

### 7. Real-time Status Display

**Current:** ✅ Implemented with smoothing and throttling

**Tasks:**
- [x] Subscribe to PGN 253 data stream (on panel open)
- [ ] Update `SetSteerAngle` from guidance calculation (future integration)
- [x] Update `ActualSteerAngle` from PGN 253 with EMA smoothing
- [x] Update `PwmDisplay` from PGN 253
- [x] Calculate `SteerError = |SetAngle - ActualAngle|`
- [x] Update at 10Hz with fixed-width columns for stable layout

### 8. Sensor Readings (Tab 5)

**Current:** Pressure/current progress bars are placeholders
**Need:** Live sensor values from module

The module may send additional sensor data (pressure, current) in:
- Extended PGN 253 data
- Separate sensor PGN

**Tasks:**
- [ ] Determine if module sends pressure/current data
- [ ] If yes, parse and expose on ViewModel
- [ ] Update progress bars with actual readings
- [ ] Compare against trip point thresholds

## PGN Communication Summary

| PGN | Direction | Purpose | Status |
|-----|-----------|---------|--------|
| 251 | PC → Module | Steer Config (hardware settings) | ✅ Builder done |
| 252 | PC → Module | Steer Settings (calibration) | ✅ Builder done |
| 253 | Module → PC | Steer Data (actual angles, switches) | ✅ Parser done |
| 254 | PC → Module | AutoSteer Data (speed, status, XTE) | ✅ Builder done |

## Implementation Order

### Phase 1: Core Communication (High Priority)
1. ✅ Parse PGN 253 incoming data
2. ✅ Wire `ActualSteerAngle`, `PwmDisplay` to status bar
3. ✅ Validate `SendAndSaveCommand` sends both PGNs correctly
4. ✅ Add display smoothing and throttling (10Hz updates, EMA filter)

### Phase 2: Calibration Tools (Medium Priority)
5. ✅ Implement `ZeroWasCommand` using PGN 253 data
6. Diameter recording (deferred - requires GPS tracking)
7. ✅ Implement `ResetToDefaultsCommand` with confirmation dialog

### Phase 3: Test Mode (Medium Priority)
8. Wire test mode steer commands to send to module
9. Add free drive mode toggle
10. Rate limit test commands

### Phase 4: Advanced Features (Lower Priority)
11. Pressure/current sensor displays
12. Turn sensor counts display
13. Setup wizard integration

## Testing Strategy

1. **Simulator Testing** - Use UDP loopback to verify PGN building/parsing
2. **Hardware Testing** - Connect to actual steer module
3. **Round-trip Verification** - Send settings, read back from module status

## Files to Modify

| File | Changes |
|------|---------|
| `UdpCommunicationService.cs` | Add PGN 253 parsing, expose events |
| `AutoSteerConfigViewModel.cs` | Subscribe to module data, wire commands |
| `PgnBuilder.cs` | Add PGN 253 parser (or separate `PgnParser.cs`) |
| `AutoSteerConfig.cs` | Add `ResetToDefaults()` method |
| `AutoSteerConfigPanel.axaml` | Bind status bar to live values |

## Open Questions

1. **Test Mode Protocol** - Does the module have a specific test/free-drive mode command?
2. **Sensor Data** - Does PGN 253 include pressure/current, or is there a separate sensor PGN?
3. **Bidirectional Sync** - Should we read settings back from module on panel open to verify sync?
4. **Error Handling** - What happens if module doesn't respond to PGN 251/252?
