# Config Panel to Service Wiring Plan

## Objective
Ensure every setting in the configuration panel actually affects the underlying service behavior. This plan addresses issues found in the config settings audit.

---

## Phase 1: Antenna Position Correction (High Priority)

### Problem
GPS position represents antenna location, but guidance needs vehicle pivot point position. The antenna offset settings are stored but never applied.

### Settings to Wire
- `Vehicle.AntennaPivot` - Distance from antenna to pivot point (along vehicle centerline)
- `Vehicle.AntennaOffset` - Lateral offset of antenna from centerline
- `Vehicle.AntennaHeight` - Height of antenna (for terrain compensation)

### Implementation

#### Option A: Transform in GpsService (Recommended)
```csharp
// In GpsService.UpdateGpsData() or new method TransformToPivot()
public Position TransformAntennaToPivot(GpsData gpsData)
{
    var vehicle = ConfigurationStore.Instance.Vehicle;
    double heading = gpsData.Heading * Math.PI / 180.0;

    // Transform antenna position to pivot position
    // Pivot is behind antenna by AntennaPivot distance
    double pivotEasting = gpsData.Easting - Math.Sin(heading) * vehicle.AntennaPivot;
    double pivotNorthing = gpsData.Northing - Math.Cos(heading) * vehicle.AntennaPivot;

    // Apply lateral offset if antenna is not on centerline
    if (Math.Abs(vehicle.AntennaOffset) > 0.001)
    {
        double perpHeading = heading + Math.PI / 2;
        pivotEasting -= Math.Sin(perpHeading) * vehicle.AntennaOffset;
        pivotNorthing -= Math.Cos(perpHeading) * vehicle.AntennaOffset;
    }

    return new Position
    {
        Easting = pivotEasting,
        Northing = pivotNorthing,
        Heading = gpsData.Heading,
        Speed = gpsData.Speed,
        // ... other fields
    };
}
```

#### Option B: Transform in MainViewModel
Apply transformation where GPS data is consumed in `CalculateAutoSteerGuidance()`.

### Files to Modify
- `Shared/AgValoniaGPS.Services/GpsService.cs` - Add transformation method
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` - Use transformed position
- `Shared/AgValoniaGPS.Services/Interfaces/IGpsService.cs` - Add interface method

### Testing
1. Set AntennaPivot to 2.0m, verify vehicle icon moves relative to GPS position
2. Set AntennaOffset to 0.5m, verify lateral shift
3. Verify guidance XTE changes with antenna settings

---

## Phase 2: Tool Width Correction (High Priority) - ✅ COMPLETE

### Problem
U-turn calculations were using `Vehicle.TrackWidth` instead of `Tool.Width`.

### Fix Applied
`MainViewModel.cs` line 1453 changed from:
```csharp
// BEFORE:
double toolWidth = Vehicle.TrackWidth;

// AFTER:
double toolWidth = Tool.Width;
```

### Files Modified
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` - Line 1453 fixed

### Testing
1. Set Tool.Width to different value than Vehicle.TrackWidth
2. Verify U-turn arc diameter changes with Tool.Width setting
3. Verify headland offset uses correct width

---

## Phase 3: Hitch/Tool Position Calculation (Medium Priority)

### Problem
Tool position relative to vehicle is not calculated. Hitch lengths are stored but unused.

### Settings to Wire
- `Tool.HitchLength` - Distance from pivot to hitch point
- `Tool.TrailingHitchLength` - For trailing implements
- `Tool.TankTrailingHitchLength` - For tank trailer configurations
- `Tool.IsToolTrailing`, `IsToolTBT`, `IsToolRearFixed`, `IsToolFrontFixed` - Tool type flags

### Implementation

#### New Service: ToolPositionService
```csharp
public interface IToolPositionService
{
    /// <summary>
    /// Calculate tool center position based on vehicle position and tool configuration
    /// </summary>
    Vec3 CalculateToolPosition(Vec3 vehiclePivot, double vehicleHeading);

    /// <summary>
    /// Calculate individual section positions for coverage mapping
    /// </summary>
    List<Vec3> CalculateSectionPositions(Vec3 vehiclePivot, double vehicleHeading);
}
```

#### Logic for Different Tool Types
```csharp
public Vec3 CalculateToolPosition(Vec3 vehiclePivot, double vehicleHeading)
{
    var tool = ConfigurationStore.Instance.Tool;

    if (tool.IsToolFrontFixed)
    {
        // Tool is ahead of pivot
        return OffsetPosition(vehiclePivot, vehicleHeading, tool.HitchLength);
    }
    else if (tool.IsToolRearFixed)
    {
        // Tool is behind pivot (negative hitch length)
        return OffsetPosition(vehiclePivot, vehicleHeading, tool.HitchLength);
    }
    else if (tool.IsToolTrailing)
    {
        // Trailing implement - needs heading calculation based on turn history
        // More complex - implement trailing geometry
        return CalculateTrailingToolPosition(vehiclePivot, vehicleHeading);
    }
    else if (tool.IsToolTBT)
    {
        // Tow-Between-Tractor - even more complex geometry
        return CalculateTBTToolPosition(vehiclePivot, vehicleHeading);
    }

    return vehiclePivot; // Default: tool at pivot
}
```

### Files to Create
- `Shared/AgValoniaGPS.Services/Tool/ToolPositionService.cs`
- `Shared/AgValoniaGPS.Services/Interfaces/IToolPositionService.cs`

### Files to Modify
- `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` - Use tool position for coverage
- DI registration files

---

## Phase 4: Section Control Service (Medium Priority)

### Problem
Section control settings exist but no service implements the logic.

### Settings to Wire
- `Config.NumSections` - Number of active sections
- `Tool.LookAheadOnSetting` - Time-based look ahead for section ON
- `Tool.LookAheadOffSetting` - Time-based look ahead for section OFF
- `Tool.TurnOffDelay` - Delay before turning sections off
- `Tool.SectionWidths[]` - Individual section widths
- `Tool.IsSectionOffWhenOut` - Turn off sections outside boundary

### New Service: SectionControlService
See `Plans/MISSING_SERVICES.md` for full specification.

---

## Phase 5: Verify Remaining Settings

### Hardware Config
- [ ] Verify GPS port settings used in serial communication
- [ ] Verify AHRS settings affect IMU processing
- [ ] Verify steering PID values sent to hardware

### Display Config
- [ ] Verify IsMetric affects all unit displays
- [ ] Verify map color settings used in DrawingContextMapControl
- [ ] Verify grid/overlay visibility settings work

### Tram Config
- [ ] Verify tram line settings used (or document as not implemented)

### Additional Options
- [ ] Verify sound settings affect audio playback
- [ ] Verify button visibility settings work
- [ ] Verify auto-steer settings affect guidance

### Sources Config
- [x] NTRIP settings verified working
- [ ] Verify UDP port settings used in UdpCommunicationService
- [ ] Verify GPS source selection works

---

## Implementation Order

1. **Week 1: Critical Fixes**
   - Fix Tool.Width usage (Phase 2) - 30 min
   - Implement antenna position transform (Phase 1) - 2-3 hours

2. **Week 2: Tool Position**
   - Create ToolPositionService (Phase 3) - 4-6 hours
   - Wire to coverage mapping

3. **Week 3: Section Control**
   - Create SectionControlService (Phase 4) - 8-12 hours
   - Wire all section settings

4. **Ongoing: Verification**
   - Test each remaining setting (Phase 5)
   - Fix any disconnects found

---

## Verification Checklist

For each setting, verify:
- [ ] UI control updates ConfigurationStore property
- [ ] Service reads from ConfigurationStore (not local copy)
- [ ] Changing setting visibly affects behavior
- [ ] Setting persists across app restart
