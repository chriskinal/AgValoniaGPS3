# NTRIP Profiles Feature Plan

## Overview
Implement NTRIP profiles as an app-level service where users can:
- Create/edit/delete multiple NTRIP profiles
- Associate profiles with specific fields
- Auto-connect to the appropriate NTRIP caster when a field is loaded

## Status: COMPLETE

### Completed
- [x] Phase 0: Branch setup (`feature/ntrip-profiles`)
- [x] Phase 1: Data Model & Storage
- [x] Phase 2: Field Load Integration
- [x] Phase 3: UI - Profile List Dialog
- [x] Phase 4: UI - Profile Editor (basic)
- [x] Phase 5: Migration from legacy settings
- [x] Test Connection button in profile editor
- [x] Show NTRIP profile name in field selection dialog
- [x] Fix binding errors (FallbackValue/TargetNullValue)

---

## Architecture

### Data Model: NtripProfile
```csharp
public class NtripProfile
{
    public string Id { get; set; }                    // GUID
    public string Name { get; set; }                  // User-friendly name
    public string CasterHost { get; set; }
    public int CasterPort { get; set; }
    public string MountPoint { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public List<string> AssociatedFields { get; set; }  // Field directory names
    public bool AutoConnectOnFieldLoad { get; set; }
    public bool IsDefault { get; set; }               // Default for unassociated fields
}
```

### Storage
- **Location**: `Documents/AgValoniaGPS/NtripProfiles/`
- **Format**: JSON files, one per profile (`{ProfileName}.json`)

### Service Interface: INtripProfileService
```csharp
public interface INtripProfileService
{
    IReadOnlyList<NtripProfile> Profiles { get; }
    NtripProfile? DefaultProfile { get; }
    NtripProfile? GetProfileForField(string fieldDirectoryName);
    Task LoadProfilesAsync();
    Task SaveProfileAsync(NtripProfile profile);
    Task DeleteProfileAsync(string profileId);
    Task SetDefaultProfileAsync(string? profileId);
    NtripProfile CreateNewProfile(string name);
    IReadOnlyList<string> GetAvailableFields();
    event EventHandler ProfilesChanged;
}
```

---

## Implementation Details

### Field Load Integration
When a field is loaded (`MainViewModel.LoadField`):
1. Get profile for field via `GetProfileForField(fieldName)`
2. Returns field-specific profile if associated, otherwise default profile
3. If profile exists and `AutoConnectOnFieldLoad` is true:
   - Disconnect from current caster
   - Update UI with profile settings
   - Connect to new caster

### UI Components

#### NtripProfilesDialogPanel
- List of profiles showing name, caster host, mount point
- Default profile marked with star (*)
- Toolbar: Add, Edit, Delete, Set as Default
- Accessed from: Application Settings (hamburger menu) → NTRIP Profiles

#### NtripProfileEditorPanel
- Profile name
- Caster settings (host, port, mount point, username, password)
- Field associations (multi-select checkbox list)
- Options: Auto-connect on field load, Set as default
- Test Connection button (TODO)

#### Field Selection Enhancement (TODO)
- Show NTRIP profile name next to field name
- Fields without specific association show "Default"

### Migration
On first run, if no profiles exist but legacy NTRIP settings are present:
- Create a "Default" profile from legacy settings
- Mark it as the default profile
- Auto-connect behavior preserved

---

## Files Created/Modified

### New Files
| File | Purpose |
|------|---------|
| `Shared/AgValoniaGPS.Models/Ntrip/NtripProfile.cs` | Profile data model |
| `Shared/AgValoniaGPS.Models/Ntrip/FieldAssociationItem.cs` | UI helper for field selection |
| `Shared/AgValoniaGPS.Services/Interfaces/INtripProfileService.cs` | Service interface |
| `Shared/AgValoniaGPS.Services/NtripProfileService.cs` | Service implementation |
| `Shared/AgValoniaGPS.Views/Controls/Dialogs/NtripProfilesDialogPanel.axaml(.cs)` | Profile list dialog |
| `Shared/AgValoniaGPS.Views/Controls/Dialogs/NtripProfileEditorPanel.axaml(.cs)` | Profile editor dialog |

### Modified Files
| File | Changes |
|------|---------|
| `Shared/AgValoniaGPS.Models/State/UIState.cs` | Added NtripProfiles, NtripProfileEditor dialog types |
| `Shared/AgValoniaGPS.ViewModels/MainViewModel.cs` | Profile properties, commands, field load integration |
| `Shared/AgValoniaGPS.Views/Controls/DialogOverlayHost.axaml` | Registered new dialogs |
| `Shared/AgValoniaGPS.Views/Controls/Panels/FileMenuPanel.axaml` | Added NTRIP Profiles menu item |
| `Platforms/*/DependencyInjection/ServiceCollectionExtensions.cs` | Registered INtripProfileService |

---

## Design Decisions

1. **Profile Sharing**: Multiple fields can share the same NTRIP profile (e.g., all fields near same base station)
2. **Default Profile**: Fields without specific association use the default profile
3. **UI Location**: Hamburger menu (app-level configuration)
4. **Field Association**: From profile editor via multi-select checkbox list
5. **Storage Format**: JSON (simpler than XML, human-readable)
