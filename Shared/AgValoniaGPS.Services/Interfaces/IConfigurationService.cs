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

    #region Profile Management

    /// <summary>
    /// Gets a list of available profile names
    /// </summary>
    IReadOnlyList<string> GetAvailableProfiles();

    /// <summary>
    /// Loads a profile by name into the ConfigurationStore
    /// </summary>
    /// <param name="name">Profile name</param>
    /// <returns>True if loaded successfully</returns>
    bool LoadProfile(string name);

    /// <summary>
    /// Saves the current ConfigurationStore to the specified profile
    /// </summary>
    /// <param name="name">Profile name</param>
    void SaveProfile(string name);

    /// <summary>
    /// Creates a new profile with default values
    /// </summary>
    /// <param name="name">Profile name</param>
    void CreateProfile(string name);

    /// <summary>
    /// Deletes a profile
    /// </summary>
    /// <param name="name">Profile name</param>
    /// <returns>True if deleted successfully</returns>
    bool DeleteProfile(string name);

    /// <summary>
    /// Reloads the current profile, discarding unsaved changes
    /// </summary>
    void ReloadCurrentProfile();

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
