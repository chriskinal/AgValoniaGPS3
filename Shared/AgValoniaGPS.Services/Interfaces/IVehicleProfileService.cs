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

using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for managing vehicle profiles (tractor + implement configurations).
/// Loads/saves directly to/from ConfigurationStore, bypassing intermediate DTOs.
/// Supports both JSON (primary) and legacy AgOpenGPS XML (import only) formats.
/// </summary>
public interface IVehicleProfileService
{
    /// <summary>
    /// Gets the directory where vehicle profiles are stored
    /// </summary>
    string VehiclesDirectory { get; }

    /// <summary>
    /// Gets a list of available vehicle profile names
    /// </summary>
    List<string> GetAvailableProfiles();

    /// <summary>
    /// Loads a vehicle profile by name directly into the ConfigurationStore.
    /// Tries JSON first, falls back to legacy XML.
    /// </summary>
    /// <param name="profileName">Profile name (filename without extension)</param>
    /// <param name="store">ConfigurationStore to populate</param>
    /// <returns>True if loaded successfully</returns>
    bool Load(string profileName, ConfigurationStore store);

    /// <summary>
    /// Saves the current ConfigurationStore state as a JSON profile.
    /// </summary>
    /// <param name="profileName">Profile name</param>
    /// <param name="store">ConfigurationStore to save from</param>
    void Save(string profileName, ConfigurationStore store);

    /// <summary>
    /// Creates a new profile with default values and saves it.
    /// Resets the given ConfigurationStore to defaults.
    /// </summary>
    /// <param name="profileName">Name for the new profile</param>
    /// <param name="store">ConfigurationStore to reset to defaults</param>
    void CreateDefaultProfile(string profileName, ConfigurationStore store);
}
