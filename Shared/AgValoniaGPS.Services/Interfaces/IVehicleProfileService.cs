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

using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for managing vehicle profiles (tractor + implement configurations)
/// Compatible with AgOpenGPS vehicle XML format
/// </summary>
public interface IVehicleProfileService
{
    /// <summary>
    /// Gets the directory where vehicle profiles are stored
    /// </summary>
    string VehiclesDirectory { get; }

    /// <summary>
    /// Gets the currently active vehicle profile
    /// </summary>
    VehicleProfile? ActiveProfile { get; }

    /// <summary>
    /// Gets a list of available vehicle profile names (filenames without .XML extension)
    /// </summary>
    /// <returns>List of profile names</returns>
    List<string> GetAvailableProfiles();

    /// <summary>
    /// Loads a vehicle profile by name
    /// </summary>
    /// <param name="profileName">Profile name (filename without .XML extension)</param>
    /// <returns>The loaded vehicle profile, or null if not found</returns>
    VehicleProfile? Load(string profileName);

    /// <summary>
    /// Saves a vehicle profile to disk
    /// </summary>
    /// <param name="profile">The profile to save</param>
    void Save(VehicleProfile profile);

    /// <summary>
    /// Sets the active profile by name
    /// </summary>
    /// <param name="profileName">Profile name to activate</param>
    /// <returns>True if profile was loaded and activated successfully</returns>
    bool SetActiveProfile(string profileName);

    /// <summary>
    /// Creates a new default profile with the given name
    /// </summary>
    /// <param name="profileName">Name for the new profile</param>
    /// <returns>The newly created profile</returns>
    VehicleProfile CreateDefaultProfile(string profileName);

    /// <summary>
    /// Event fired when the active profile changes
    /// </summary>
    event EventHandler<VehicleProfile?>? ActiveProfileChanged;
}
