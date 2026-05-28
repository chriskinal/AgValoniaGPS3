// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Profile;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for managing the unified configuration store.
/// Bridges between ConfigurationStore and persistence services.
/// VehicleProfileService loads/saves directly to/from ConfigurationStore.
/// </summary>
public class ConfigurationService(
    IVehicleProfileService profileService,
    IToolProfileService toolProfileService,
    ISettingsService settingsService) : IConfigurationService
{
    public ConfigurationStore Store => ConfigurationStore.Instance;

    public string ProfilesDirectory => profileService.VehiclesDirectory;
    public string ToolsDirectory => toolProfileService.ToolsDirectory;

    public event EventHandler<string>? ProfileLoaded;
    public event EventHandler<string>? ProfileSaved;

    /// <summary>
    /// Recovery summary from the most recent <see cref="LoadProfiles"/>, or
    /// <c>null</c> if nothing was damaged. The startup recovery prompt reads
    /// this once; the runtime picker drives its own prompt via
    /// <see cref="ProbeProfiles"/> instead.
    /// </summary>
    public ProfileLoadProbe? LastRecovery { get; private set; }

    #region Profile Management

    public IReadOnlyList<string> GetAvailableProfiles()
    {
        return profileService.GetAvailableProfiles();
    }

    public IReadOnlyList<string> GetAvailableToolProfiles()
    {
        return toolProfileService.GetAvailableProfiles();
    }

    /// <summary>
    /// Convenience: load a vehicle and a tool profile that share the same
    /// name. The legacy single-profile callers and the same-name save
    /// pattern both flow through here.
    /// </summary>
    /// <summary>
    /// Load a vehicle profile and (independently) a tool profile. The
    /// picker dialog (#346) calls this with mismatched names.
    /// </summary>
    /// <summary>
    /// Read-only corruption probe of the v2 files behind a vehicle+tool pair,
    /// without mutating the store. The runtime picker calls this to decide
    /// whether to prompt before committing to a switch.
    /// </summary>
    public ProfileLoadProbe ProbeProfiles(string vehicleName, string toolName)
    {
        return new ProfileLoadProbe
        {
            Files = new[]
            {
                VehicleProfileJsonService.Probe(profileService.VehiclesDirectory, vehicleName),
                ToolProfileJsonService.Probe(toolProfileService.ToolsDirectory, toolName),
            },
        };
    }

    public bool LoadProfiles(string vehicleName, string toolName)
    {
        // Capture corruption status up-front (no mutation, no quarantine) so we
        // can record it for the startup recovery prompt after the load.
        var probe = ProbeProfiles(vehicleName, toolName);

        // profileService.Load transparently recovers from the .bak
        // last-known-good copy when the primary v2 file is damaged.
        if (!profileService.Load(vehicleName, Store))
        {
            LastRecovery = probe.NeedsPrompt ? probe : null;
            return false;
        }

        // Tool side is best-effort: a matching tool file may not exist yet
        // (pre-#346 user, no migration run, fresh install with no tool yet).
        // Failure leaves the tool/sections sub-store as the v1 reader (or
        // CreateDefaultProfile path) populated it.
        toolProfileService.Load(toolName, Store);

        LoadAutoSteerConfig(vehicleName);

        // IsMetric source-of-truth lives in AppSettings. A legacy profile
        // may have written into Store.IsMetric above; either it's a fresh
        // value (migrate to AppSettings) or AppSettings is authoritative
        // and the profile's value is overridden.
        ReconcileIsMetricAfterProfileLoad();

        Store.HasUnsavedChanges = false;

        LastRecovery = probe.NeedsPrompt ? probe : null;

        // Heal forward: if a primary file was damaged but recovered from its
        // backup, rewrite a fresh, healthy primary from the now-correct store
        // so the user isn't re-prompted on every subsequent load. (The damaged
        // primary was quarantined during the recovering read.)
        if (probe.AnyRecovered)
        {
            SaveProfiles(vehicleName, toolName);
        }

        // Persist the active pair so the next startup restores the same
        // combo instead of falling through to LoadProfile(name) and pairing
        // the vehicle name with itself as the tool name.
        settingsService.Settings.LastUsedVehicleProfile = Store.ActiveVehicleProfileName;
        settingsService.Settings.LastUsedToolProfile = Store.ActiveToolProfileName;
        settingsService.Save();

        Store.OnProfileLoaded();
        ProfileLoaded?.Invoke(this, vehicleName);
        return true;
    }

    public void SaveProfiles(string vehicleName, string toolName)
    {
        profileService.Save(vehicleName, Store);
        toolProfileService.Save(toolName, Store);
        SaveAutoSteerConfig(vehicleName);
        Store.HasUnsavedChanges = false;
        Store.OnProfileSaved();
        ProfileSaved?.Invoke(this, vehicleName);
    }

    public void CreateProfile(string name)
    {
        profileService.CreateDefaultProfile(name, Store);
        toolProfileService.CreateDefaultProfile(name, Store);
        Store.HasUnsavedChanges = false;
    }

    /// <summary>
    /// Rename a vehicle profile. If the renamed profile is the active one,
    /// the store's ActiveVehicleProfileName/Path and AppSettings.LastUsedVehicleProfile
    /// follow the rename so the active pointer survives. Returns false on
    /// collision / missing source. Throws on I/O failure.
    /// </summary>
    public bool RenameVehicleProfile(string oldName, string newName)
    {
        if (!profileService.Rename(oldName, newName))
            return false;

        if (string.Equals(Store.ActiveVehicleProfileName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            Store.ActiveVehicleProfileName = newName;
            Store.ActiveVehicleProfilePath = Path.Combine(profileService.VehiclesDirectory, $"{newName}.json");
            settingsService.Settings.LastUsedVehicleProfile = newName;
            settingsService.Save();
        }
        return true;
    }

    /// <summary>
    /// Rename a tool profile (see RenameVehicleProfile for active-pointer behavior).
    /// </summary>
    public bool RenameToolProfile(string oldName, string newName)
    {
        if (!toolProfileService.Rename(oldName, newName))
            return false;

        if (string.Equals(Store.ActiveToolProfileName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            Store.ActiveToolProfileName = newName;
            Store.ActiveToolProfilePath = Path.Combine(toolProfileService.ToolsDirectory, $"{newName}.json");
            settingsService.Settings.LastUsedToolProfile = newName;
            settingsService.Save();
        }
        return true;
    }

    /// <summary>
    /// Delete a vehicle profile. The active vehicle profile cannot be
    /// deleted — returns false.
    /// </summary>
    public bool DeleteVehicleProfile(string name)
    {
        if (string.Equals(Store.ActiveVehicleProfileName, name, StringComparison.OrdinalIgnoreCase))
            return false;
        return profileService.Delete(name);
    }

    /// <summary>
    /// Delete a tool profile. The active tool profile cannot be deleted.
    /// </summary>
    public bool DeleteToolProfile(string name)
    {
        if (string.Equals(Store.ActiveToolProfileName, name, StringComparison.OrdinalIgnoreCase))
            return false;
        return toolProfileService.Delete(name);
    }

    public bool DeleteProfile(string name)
    {
        bool deleted = false;
        try
        {
            // Delete v2 vehicle JSON
            var jsonPath = Path.Combine(ProfilesDirectory, $"{name}.json");
            if (File.Exists(jsonPath)) { File.Delete(jsonPath); deleted = true; }

            // Delete v2 tool JSON (paired by name; rare to delete vehicle and
            // not its same-name tool — the picker dialog manages independent
            // delete for mismatched names).
            var toolPath = Path.Combine(ToolsDirectory, $"{name}.json");
            if (File.Exists(toolPath)) { File.Delete(toolPath); deleted = true; }

            // Legacy AOG XML (vehicle side only) and AutoSteer sidecar.
            var xmlPath = Path.Combine(ProfilesDirectory, $"{name}.XML");
            if (File.Exists(xmlPath)) { File.Delete(xmlPath); deleted = true; }

            var autoSteerPath = Path.Combine(ProfilesDirectory, $"{name}.AutoSteer.json");
            if (File.Exists(autoSteerPath)) File.Delete(autoSteerPath);
        }
        catch
        {
            return false;
        }
        return deleted;
    }

    /// <summary>
    /// One-time v1 → v2 split migration (#346). Triggered when the Tools/
    /// directory is empty but the Vehicles/ directory holds JSON profiles
    /// that lack <c>formatVersion: 2</c>. For each pre-v2 file, reads the
    /// combined v1 profile, writes the split v2 vehicle file (overwrite)
    /// and a same-named v2 tool file. Also pairs up
    /// AppSettings.LastUsedToolProfile = LastUsedVehicleProfile so the
    /// active pairing is preserved across the migration.
    /// </summary>
    /// <returns>true if any file was migrated.</returns>
    public bool MigrateV1ProfilesIfNeeded()
    {
        var existingTools = toolProfileService.GetAvailableProfiles();
        if (existingTools.Count > 0)
            return false; // assumed migrated

        var vehicleNames = profileService.GetAvailableProfiles();
        bool migrated = false;

        foreach (var name in vehicleNames)
        {
            var jsonPath = Path.Combine(profileService.VehiclesDirectory, $"{name}.json");
            if (!File.Exists(jsonPath))
                continue; // XML-only legacy profile — leaves it for next save to migrate

            var version = PeekFormatVersion(jsonPath);
            if (version >= 2)
                continue; // already split

            // Read v1 into an isolated temp store so we don't perturb the
            // app singleton during startup before the proper LoadProfile.
            var tempStore = new ConfigurationStore();
            if (!ProfileJsonServiceV1.Load(profileService.VehiclesDirectory, name, tempStore))
                continue;

            try
            {
                VehicleProfileJsonService.Save(profileService.VehiclesDirectory, name, tempStore);
                toolProfileService.Save(name, tempStore);
                migrated = true;
            }
            catch (Exception)
            {
                // Best-effort migration — leave the v1 file in place if
                // either side fails so the user can retry / file a bug.
            }
        }

        if (migrated && string.IsNullOrEmpty(settingsService.Settings.LastUsedToolProfile))
        {
            settingsService.Settings.LastUsedToolProfile = settingsService.Settings.LastUsedVehicleProfile;
            settingsService.Save();
        }

        return migrated;
    }

    private static int? PeekFormatVersion(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            // JSON serializer used camelCase, so the property is "formatVersion".
            if (doc.RootElement.TryGetProperty("formatVersion", out var v) && v.ValueKind == JsonValueKind.Number)
                return v.GetInt32();
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void ReloadCurrentProfile()
    {
        if (!string.IsNullOrEmpty(Store.ActiveVehicleProfileName))
        {
            LoadProfiles(
                Store.ActiveVehicleProfileName,
                string.IsNullOrEmpty(Store.ActiveToolProfileName)
                    ? Store.ActiveVehicleProfileName
                    : Store.ActiveToolProfileName);
        }
    }

    #endregion

    #region App Settings Management

    public void LoadAppSettings()
    {
        settingsService.Load();
        ApplyAppSettingsToStore(settingsService.Settings);
    }

    public void SaveAppSettings()
    {
        ApplyStoreToAppSettings(settingsService.Settings);
        settingsService.Save();
    }

    /// <summary>
    /// Reconcile the device-scoped <see cref="AppSettings.IsMetric"/> with
    /// whatever value a vehicle profile just wrote into the store. Called
    /// after a profile load so the source-of-truth move from
    /// vehicle-profile to AppSettings holds:
    ///
    ///  - If migration has not yet happened, the profile's value seeds
    ///    AppSettings (one-shot legacy fallback) and the migration latch
    ///    flips so subsequent profile loads can't drag the unit
    ///    preference around.
    ///  - If migration has already happened, AppSettings is authoritative
    ///    and re-asserted onto the store, overriding whatever the profile
    ///    file carried.
    ///
    /// Either way, on return the store and AppSettings agree.
    /// </summary>
    public void ReconcileIsMetricAfterProfileLoad()
    {
        var settings = settingsService.Settings;
        if (!settings.HasMigratedIsMetric)
        {
            settings.IsMetric = Store.IsMetric;
            settings.HasMigratedIsMetric = true;
            settingsService.Save();
        }
        else
        {
            Store.IsMetric = settings.IsMetric;
        }
    }

    #endregion

    #region AppSettings <-> Store Mapping

    /// <summary>
    /// Applies AppSettings to the ConfigurationStore
    /// </summary>
    private void ApplyAppSettingsToStore(AppSettings settings)
    {
        var store = Store;

        // Display config (preferences only — window/camera/day-night/panel
        // STATE moved to PersistentAppState).
        store.Display.StartFullscreen = settings.StartFullscreen;
        store.Display.SvennArrowVisible = settings.SvennArrowVisible;
        store.Display.KeyboardEnabled = settings.KeyboardEnabled;
        store.Display.HeadlandDistanceVisible = settings.HeadlandDistanceVisible;
        store.Display.ExtraGuidelines = settings.ExtraGuidelines;
        store.Display.ExtraGuidelinesCount = settings.ExtraGuidelinesCount;
        store.Display.FieldTextureVisible = settings.FieldTextureVisible;
        store.Display.FieldTextureMoveable = settings.FieldTextureMoveable;
        store.IsMetric = settings.IsMetric;
        store.Display.AutoSteerSound = settings.AutoSteerSound;
        store.Display.UTurnSound = settings.UTurnSound;
        store.Display.HydraulicSound = settings.HydraulicSound;
        store.Display.SectionsSound = settings.SectionsSound;
        store.Display.GridVisible = settings.GridVisible;
        store.Display.CompassVisible = settings.CompassVisible;
        store.Display.SpeedVisible = settings.SpeedVisible;
        store.Display.ElevationLogEnabled = settings.ElevationLogEnabled;
        store.Display.PolygonsVisible = settings.PolygonsVisible;
        store.Display.SpeedometerVisible = settings.SpeedometerVisible;
        store.Display.LineSmoothEnabled = settings.LineSmoothEnabled;
        store.Display.DirectionMarkersVisible = settings.DirectionMarkersVisible;
        store.Display.SectionLinesVisible = settings.SectionLinesVisible;
        store.Display.UTurnButtonVisible = settings.UTurnButtonVisible;
        store.Display.LateralButtonVisible = settings.LateralButtonVisible;
        store.Display.HardwareMessagesEnabled = settings.HardwareMessagesEnabled;
        store.Display.DayStartHour = settings.DayStartHour;
        store.Display.NightStartHour = settings.NightStartHour;
        store.Display.DialogAutoCloseSeconds = settings.DialogAutoCloseSeconds;
        store.Display.DisplayResolutionMultiplier = settings.DisplayResolutionMultiplier;
        store.Display.AutoDayNight = settings.AutoDayNight;

        // Connection config
        store.Connections.NtripCasterHost = settings.NtripCasterIp;
        store.Connections.NtripCasterPort = settings.NtripCasterPort;
        store.Connections.NtripMountPoint = settings.NtripMountPoint;
        store.Connections.NtripUsername = settings.NtripUsername;
        store.Connections.NtripPassword = settings.NtripPassword;
        store.Connections.NtripAutoConnect = settings.NtripAutoConnect;
        store.Connections.AgShareServer = settings.AgShareServer;
        store.Connections.AgShareApiKey = settings.AgShareApiKey;
        store.Connections.AgShareEnabled = settings.AgShareEnabled;
        store.Connections.GpsUpdateRate = settings.GpsUpdateRate;
        store.Connections.UseRtk = settings.UseRtk;
        store.Connections.IsGpsConfigured = settings.IsGpsConfigured;
        store.Connections.IsImuConfigured = settings.IsImuConfigured;
        store.Connections.IsAutoSteerConfigured = settings.IsAutoSteerConfigured;
        store.Connections.IsMachineConfigured = settings.IsMachineConfigured;

        // Hotkey bindings
        if (settings.HotkeyBindings.Count > 0)
        {
            store.Hotkeys.LoadFromDictionary(settings.HotkeyBindings);
        }

        // Simulator config — only the Enabled preference is config. The last
        // simulator POSITION is persistent state (PersistentAppState).
        store.Simulator.Enabled = settings.SimulatorEnabled;
    }

    /// <summary>
    /// Applies ConfigurationStore to AppSettings
    /// </summary>
    private void ApplyStoreToAppSettings(AppSettings settings)
    {
        var store = Store;

        // Display config (preferences only — window/camera/day-night/panel
        // STATE is persisted by PersistentStateService).
        settings.StartFullscreen = store.Display.StartFullscreen;
        settings.SvennArrowVisible = store.Display.SvennArrowVisible;
        settings.KeyboardEnabled = store.Display.KeyboardEnabled;
        settings.HeadlandDistanceVisible = store.Display.HeadlandDistanceVisible;
        settings.ExtraGuidelines = store.Display.ExtraGuidelines;
        settings.ExtraGuidelinesCount = store.Display.ExtraGuidelinesCount;
        settings.FieldTextureVisible = store.Display.FieldTextureVisible;
        settings.FieldTextureMoveable = store.Display.FieldTextureMoveable;
        settings.IsMetric = store.IsMetric;
        settings.AutoSteerSound = store.Display.AutoSteerSound;
        settings.UTurnSound = store.Display.UTurnSound;
        settings.HydraulicSound = store.Display.HydraulicSound;
        settings.SectionsSound = store.Display.SectionsSound;
        settings.GridVisible = store.Display.GridVisible;
        settings.CompassVisible = store.Display.CompassVisible;
        settings.SpeedVisible = store.Display.SpeedVisible;
        settings.ElevationLogEnabled = store.Display.ElevationLogEnabled;
        settings.PolygonsVisible = store.Display.PolygonsVisible;
        settings.SpeedometerVisible = store.Display.SpeedometerVisible;
        settings.LineSmoothEnabled = store.Display.LineSmoothEnabled;
        settings.DirectionMarkersVisible = store.Display.DirectionMarkersVisible;
        settings.SectionLinesVisible = store.Display.SectionLinesVisible;
        settings.UTurnButtonVisible = store.Display.UTurnButtonVisible;
        settings.LateralButtonVisible = store.Display.LateralButtonVisible;
        settings.HardwareMessagesEnabled = store.Display.HardwareMessagesEnabled;
        settings.DayStartHour = store.Display.DayStartHour;
        settings.NightStartHour = store.Display.NightStartHour;
        settings.DialogAutoCloseSeconds = store.Display.DialogAutoCloseSeconds;
        settings.DisplayResolutionMultiplier = store.Display.DisplayResolutionMultiplier;
        settings.AutoDayNight = store.Display.AutoDayNight;

        // Connection config
        settings.NtripCasterIp = store.Connections.NtripCasterHost;
        settings.NtripCasterPort = store.Connections.NtripCasterPort;
        settings.NtripMountPoint = store.Connections.NtripMountPoint;
        settings.NtripUsername = store.Connections.NtripUsername;
        settings.NtripPassword = store.Connections.NtripPassword;
        settings.NtripAutoConnect = store.Connections.NtripAutoConnect;
        settings.AgShareServer = store.Connections.AgShareServer;
        settings.AgShareApiKey = store.Connections.AgShareApiKey;
        settings.AgShareEnabled = store.Connections.AgShareEnabled;
        settings.GpsUpdateRate = store.Connections.GpsUpdateRate;
        settings.UseRtk = store.Connections.UseRtk;
        settings.IsGpsConfigured = store.Connections.IsGpsConfigured;
        settings.IsImuConfigured = store.Connections.IsImuConfigured;
        settings.IsAutoSteerConfigured = store.Connections.IsAutoSteerConfigured;
        settings.IsMachineConfigured = store.Connections.IsMachineConfigured;

        // Simulator config — only the Enabled preference (position is state).
        settings.SimulatorEnabled = store.Simulator.Enabled;

        // Hotkey bindings
        settings.HotkeyBindings = store.Hotkeys.ToDictionary();

        // Active profile pair (#346)
        settings.LastUsedVehicleProfile = store.ActiveVehicleProfileName;
        settings.LastUsedToolProfile = store.ActiveToolProfileName;
    }

    #endregion

    #region AutoSteer Config Persistence

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Gets the path to the AutoSteer config JSON file for a profile.
    /// Stored as ProfileName.AutoSteer.json alongside the profile.
    /// </summary>
    private string GetAutoSteerConfigPath(string profileName)
    {
        return Path.Combine(ProfilesDirectory, $"{profileName}.AutoSteer.json");
    }

    /// <summary>
    /// Save AutoSteerConfig to JSON file alongside the profile.
    /// </summary>
    private void SaveAutoSteerConfig(string profileName)
    {
        try
        {
            var path = GetAutoSteerConfigPath(profileName);
            var dto = Store.AutoSteer.ToDto();
            var json = JsonSerializer.Serialize(dto, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save AutoSteer config: {ex.Message}");
        }
    }

    /// <summary>
    /// Load AutoSteerConfig from JSON file. If not found, keeps defaults.
    /// </summary>
    private void LoadAutoSteerConfig(string profileName)
    {
        try
        {
            var path = GetAutoSteerConfigPath(profileName);
            if (!File.Exists(path))
            {
                // No AutoSteer config file - reset to defaults
                Store.AutoSteer.ResetToDefaults();
                return;
            }

            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<AutoSteerConfigDto>(json);
            if (dto != null)
            {
                Store.AutoSteer.ApplyFromDto(dto);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load AutoSteer config: {ex.Message}");
            // On error, keep current values (or reset to defaults)
        }
    }

    #endregion
}
