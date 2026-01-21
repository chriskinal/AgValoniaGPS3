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

using AgValoniaGPS.Models.Ntrip;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for managing NTRIP profiles (caster configurations with field associations)
/// Profiles are stored as JSON files in Documents/AgValoniaGPS/NtripProfiles/
/// </summary>
public interface INtripProfileService
{
    /// <summary>
    /// Gets the directory where NTRIP profiles are stored
    /// </summary>
    string ProfilesDirectory { get; }

    /// <summary>
    /// Gets all loaded NTRIP profiles
    /// </summary>
    IReadOnlyList<NtripProfile> Profiles { get; }

    /// <summary>
    /// Gets the default profile (used for fields without a specific association)
    /// </summary>
    NtripProfile? DefaultProfile { get; }

    /// <summary>
    /// Gets the profile for a specific field.
    /// Returns the field-specific profile if one exists, otherwise returns the default profile.
    /// </summary>
    /// <param name="fieldDirectoryName">The field's directory name</param>
    /// <returns>The profile to use for this field, or null if no profile applies</returns>
    NtripProfile? GetProfileForField(string fieldDirectoryName);

    /// <summary>
    /// Loads all profiles from disk
    /// </summary>
    Task LoadProfilesAsync();

    /// <summary>
    /// Saves a profile to disk. Creates new or updates existing.
    /// </summary>
    /// <param name="profile">The profile to save</param>
    Task SaveProfileAsync(NtripProfile profile);

    /// <summary>
    /// Deletes a profile from disk
    /// </summary>
    /// <param name="profileId">The ID of the profile to delete</param>
    Task DeleteProfileAsync(string profileId);

    /// <summary>
    /// Sets which profile is the default.
    /// Pass null to clear the default (no auto-connect for unassociated fields).
    /// </summary>
    /// <param name="profileId">The ID of the profile to set as default, or null</param>
    Task SetDefaultProfileAsync(string? profileId);

    /// <summary>
    /// Creates a new profile with default values
    /// </summary>
    /// <param name="name">Name for the new profile</param>
    /// <returns>The newly created profile (not yet saved)</returns>
    NtripProfile CreateNewProfile(string name);

    /// <summary>
    /// Gets the list of available fields that can be associated with profiles
    /// </summary>
    /// <returns>List of field directory names</returns>
    IReadOnlyList<string> GetAvailableFields();

    /// <summary>
    /// Event fired when profiles are loaded, saved, or deleted
    /// </summary>
    event EventHandler? ProfilesChanged;
}
