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
    public bool LoadProfiles(string vehicleName, string toolName)
    {
        if (!profileService.Load(vehicleName, Store))
            return false;

        // Tool side is best-effort: a matching tool file may not exist yet
        // (pre-#346 user, no migration run, fresh install with no tool yet).
        // Failure leaves the tool/sections sub-store as the v1 reader (or
        // CreateDefaultProfile path) populated it.
        toolProfileService.Load(toolName, Store);

        LoadAutoSteerConfig(vehicleName);
        Store.HasUnsavedChanges = false;

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

    #endregion

    #region AppSettings <-> Store Mapping

    /// <summary>
    /// Applies AppSettings to the ConfigurationStore
    /// </summary>
    private void ApplyAppSettingsToStore(AppSettings settings)
    {
        var store = Store;

        // Display config
        store.Display.WindowWidth = settings.WindowWidth;
        store.Display.WindowHeight = settings.WindowHeight;
        store.Display.WindowX = settings.WindowX;
        store.Display.WindowY = settings.WindowY;
        store.Display.WindowMaximized = settings.WindowMaximized;
        store.Display.StartFullscreen = settings.StartFullscreen;
        store.Display.SvennArrowVisible = settings.SvennArrowVisible;
        store.Display.KeyboardEnabled = settings.KeyboardEnabled;
        store.Display.HeadlandDistanceVisible = settings.HeadlandDistanceVisible;
        store.Display.ExtraGuidelines = settings.ExtraGuidelines;
        store.Display.ExtraGuidelinesCount = settings.ExtraGuidelinesCount;
        store.Display.FieldTextureVisible = settings.FieldTextureVisible;
        store.Display.FieldTextureMoveable = settings.FieldTextureMoveable;
        store.Display.AutoSteerSound = settings.AutoSteerSound;
        store.Display.UTurnSound = settings.UTurnSound;
        store.Display.HydraulicSound = settings.HydraulicSound;
        store.Display.SectionsSound = settings.SectionsSound;
        store.Display.SimulatorPanelX = settings.SimulatorPanelX;
        store.Display.SimulatorPanelY = settings.SimulatorPanelY;
        store.Display.SimulatorPanelVisible = settings.SimulatorPanelVisible;
        store.Display.GridVisible = settings.GridVisible;
        store.Display.CompassVisible = settings.CompassVisible;
        store.Display.SpeedVisible = settings.SpeedVisible;
        store.Display.ElevationLogEnabled = settings.ElevationLogEnabled;
        store.Display.CameraZoom = settings.CameraZoom;
        store.Display.CameraPitch = settings.CameraPitch;
        store.Display.CameraMode = settings.CameraMode;
        store.Display.DisplayResolutionMultiplier = settings.DisplayResolutionMultiplier;
        store.Display.AutoDayNight = settings.AutoDayNight;
        store.Display.IsDayMode = settings.IsDayMode;

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

        // Hotkey bindings
        if (settings.HotkeyBindings.Count > 0)
        {
            store.Hotkeys.LoadFromDictionary(settings.HotkeyBindings);
        }

        // Simulator config - always restore from settings
        store.Simulator.Enabled = settings.SimulatorEnabled;
        store.Simulator.Latitude = settings.SimulatorLatitude;
        store.Simulator.Longitude = settings.SimulatorLongitude;
        store.Simulator.Speed = settings.SimulatorSpeed;
        store.Simulator.SteerAngle = settings.SimulatorSteerAngle;
    }

    /// <summary>
    /// Applies ConfigurationStore to AppSettings
    /// </summary>
    private void ApplyStoreToAppSettings(AppSettings settings)
    {
        var store = Store;

        // Display config
        settings.WindowWidth = store.Display.WindowWidth;
        settings.WindowHeight = store.Display.WindowHeight;
        settings.WindowX = store.Display.WindowX;
        settings.WindowY = store.Display.WindowY;
        settings.WindowMaximized = store.Display.WindowMaximized;
        settings.StartFullscreen = store.Display.StartFullscreen;
        settings.SvennArrowVisible = store.Display.SvennArrowVisible;
        settings.KeyboardEnabled = store.Display.KeyboardEnabled;
        settings.HeadlandDistanceVisible = store.Display.HeadlandDistanceVisible;
        settings.ExtraGuidelines = store.Display.ExtraGuidelines;
        settings.ExtraGuidelinesCount = store.Display.ExtraGuidelinesCount;
        settings.FieldTextureVisible = store.Display.FieldTextureVisible;
        settings.FieldTextureMoveable = store.Display.FieldTextureMoveable;
        settings.AutoSteerSound = store.Display.AutoSteerSound;
        settings.UTurnSound = store.Display.UTurnSound;
        settings.HydraulicSound = store.Display.HydraulicSound;
        settings.SectionsSound = store.Display.SectionsSound;
        settings.SimulatorPanelX = store.Display.SimulatorPanelX;
        settings.SimulatorPanelY = store.Display.SimulatorPanelY;
        settings.SimulatorPanelVisible = store.Display.SimulatorPanelVisible;
        settings.GridVisible = store.Display.GridVisible;
        settings.CompassVisible = store.Display.CompassVisible;
        settings.SpeedVisible = store.Display.SpeedVisible;
        settings.ElevationLogEnabled = store.Display.ElevationLogEnabled;
        settings.CameraZoom = store.Display.CameraZoom;
        settings.CameraPitch = store.Display.CameraPitch;
        settings.CameraMode = store.Display.CameraMode;
        settings.DisplayResolutionMultiplier = store.Display.DisplayResolutionMultiplier;
        settings.AutoDayNight = store.Display.AutoDayNight;
        settings.IsDayMode = store.Display.IsDayMode;

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

        // Simulator config
        settings.SimulatorEnabled = store.Simulator.Enabled;
        settings.SimulatorLatitude = store.Simulator.Latitude;
        settings.SimulatorLongitude = store.Simulator.Longitude;
        settings.SimulatorSpeed = store.Simulator.Speed;
        settings.SimulatorSteerAngle = store.Simulator.SteerAngle;

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
