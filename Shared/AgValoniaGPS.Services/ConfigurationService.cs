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

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for managing the unified configuration store.
/// Bridges between ConfigurationStore and persistence services.
/// VehicleProfileService loads/saves directly to/from ConfigurationStore.
/// </summary>
public class ConfigurationService(
    IVehicleProfileService profileService,
    ISettingsService settingsService) : IConfigurationService
{
    public ConfigurationStore Store => ConfigurationStore.Instance;

    public string ProfilesDirectory => profileService.VehiclesDirectory;

    public event EventHandler<string>? ProfileLoaded;
    public event EventHandler<string>? ProfileSaved;

    #region Profile Management

    public IReadOnlyList<string> GetAvailableProfiles()
    {
        return profileService.GetAvailableProfiles();
    }

    public bool LoadProfile(string name)
    {
        if (!profileService.Load(name, Store))
            return false;

        LoadAutoSteerConfig(name);
        Store.HasUnsavedChanges = false;
        Store.OnProfileLoaded();
        ProfileLoaded?.Invoke(this, name);
        return true;
    }

    public void SaveProfile(string name)
    {
        profileService.Save(name, Store);
        SaveAutoSteerConfig(name);
        Store.HasUnsavedChanges = false;
        Store.OnProfileSaved();
        ProfileSaved?.Invoke(this, name);
    }

    public void CreateProfile(string name)
    {
        profileService.CreateDefaultProfile(name, Store);
        Store.HasUnsavedChanges = false;
    }

    public bool DeleteProfile(string name)
    {
        bool deleted = false;
        try
        {
            // Delete JSON profile (primary format)
            var jsonPath = Path.Combine(ProfilesDirectory, $"{name}.json");
            if (File.Exists(jsonPath)) { File.Delete(jsonPath); deleted = true; }

            // Also clean up legacy XML and AutoSteer JSON if they exist
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

    public void ReloadCurrentProfile()
    {
        if (!string.IsNullOrEmpty(Store.ActiveProfileName))
        {
            LoadProfile(Store.ActiveProfileName);
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

        // Active profile
        settings.LastUsedVehicleProfile = store.ActiveProfileName;
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
