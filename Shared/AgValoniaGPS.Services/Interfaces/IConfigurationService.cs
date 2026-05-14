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
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for managing the unified configuration store.
/// Provides profile and app settings management with persistence.
/// Bridges to existing VehicleProfileService for AgOpenGPS XML compatibility.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the central configuration store
    /// </summary>
    ConfigurationStore Store { get; }

    /// <summary>
    /// Gets the directory where vehicle profiles are stored
    /// </summary>
    string ProfilesDirectory { get; }

    /// <summary>
    /// Gets the directory where tool profiles are stored (#346).
    /// </summary>
    string ToolsDirectory { get; }

    #region Profile Management

    /// <summary>
    /// Gets a list of available vehicle profile names.
    /// </summary>
    IReadOnlyList<string> GetAvailableProfiles();

    /// <summary>
    /// Gets a list of available tool profile names (#346).
    /// </summary>
    IReadOnlyList<string> GetAvailableToolProfiles();

    /// <summary>
    /// Loads a vehicle profile and a tool profile by name (#346 split).
    /// </summary>
    bool LoadProfiles(string vehicleName, string toolName);

    /// <summary>
    /// Saves the current ConfigurationStore to a vehicle profile and a
    /// tool profile (#346 split).
    /// </summary>
    void SaveProfiles(string vehicleName, string toolName);

    /// <summary>
    /// Creates a new profile with default values
    /// </summary>
    /// <param name="name">Profile name</param>
    void CreateProfile(string name);

    /// <summary>
    /// Deletes a profile (vehicle JSON, paired tool JSON if any, legacy XML, AutoSteer sidecar).
    /// </summary>
    bool DeleteProfile(string name);

    /// <summary>
    /// Renames the vehicle profile. Updates the active pointer if the
    /// renamed profile is the active one. Returns false on collision /
    /// missing source.
    /// </summary>
    bool RenameVehicleProfile(string oldName, string newName);

    /// <summary>
    /// Renames the tool profile (see RenameVehicleProfile).
    /// </summary>
    bool RenameToolProfile(string oldName, string newName);

    /// <summary>
    /// Deletes a vehicle profile. Returns false if the profile is active.
    /// </summary>
    bool DeleteVehicleProfile(string name);

    /// <summary>
    /// Deletes a tool profile. Returns false if the profile is active.
    /// </summary>
    bool DeleteToolProfile(string name);

    /// <summary>
    /// Reloads the current profile, discarding unsaved changes
    /// </summary>
    void ReloadCurrentProfile();

    /// <summary>
    /// One-time v1 → v2 split migration of any pre-#346 combined profiles.
    /// No-op if Tools/ is non-empty.
    /// </summary>
    bool MigrateV1ProfilesIfNeeded();

    #endregion

    #region App Settings Management

    /// <summary>
    /// Loads application settings (window position, NTRIP, etc.)
    /// </summary>
    void LoadAppSettings();

    /// <summary>
    /// Saves application settings
    /// </summary>
    void SaveAppSettings();

    /// <summary>
    /// Reconcile <see cref="AppSettings.IsMetric"/> with the value a
    /// vehicle profile just wrote into the store. One-shot migration on
    /// first call; thereafter AppSettings is authoritative and the
    /// profile's value is overridden.
    /// </summary>
    void ReconcileIsMetricAfterProfileLoad();

    #endregion

    #region Events

    /// <summary>
    /// Raised when a profile is loaded
    /// </summary>
    event EventHandler<string>? ProfileLoaded;

    /// <summary>
    /// Raised when a profile is saved
    /// </summary>
    event EventHandler<string>? ProfileSaved;

    #endregion
}
