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

namespace AgValoniaGPS.Models.Ntrip;

/// <summary>
/// NTRIP profile containing caster settings and field associations.
/// Profiles are stored as JSON files in Documents/AgValoniaGPS/NtripProfiles/.
/// </summary>
public class NtripProfile
{
    /// <summary>
    /// Unique identifier for this profile (GUID)
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User-friendly name for this profile
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// NTRIP caster host address
    /// </summary>
    public string CasterHost { get; set; } = string.Empty;

    /// <summary>
    /// NTRIP caster port (typically 2101)
    /// </summary>
    public int CasterPort { get; set; } = 2101;

    /// <summary>
    /// NTRIP mount point
    /// </summary>
    public string MountPoint { get; set; } = string.Empty;

    /// <summary>
    /// Username for NTRIP authentication (optional)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password for NTRIP authentication (optional)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// List of field directory names associated with this profile.
    /// When a field in this list is loaded, this profile will be used.
    /// </summary>
    public List<string> AssociatedFields { get; set; } = new();

    /// <summary>
    /// If true, automatically connect to NTRIP when an associated field is loaded
    /// </summary>
    public bool AutoConnectOnFieldLoad { get; set; } = true;

    /// <summary>
    /// If true, this is the default profile used for fields without a specific association
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Full path to the profile file (set by service after loading)
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}
