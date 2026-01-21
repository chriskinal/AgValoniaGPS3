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

using System.Text.Json;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models.Ntrip;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for managing NTRIP profiles stored as JSON files
/// </summary>
public class NtripProfileService : INtripProfileService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<NtripProfileService> _logger;
    private readonly List<NtripProfile> _profiles = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ProfilesDirectory { get; }

    public IReadOnlyList<NtripProfile> Profiles => _profiles.AsReadOnly();

    public NtripProfile? DefaultProfile => _profiles.FirstOrDefault(p => p.IsDefault);

    public event EventHandler? ProfilesChanged;

    public NtripProfileService(ISettingsService settingsService, ILogger<NtripProfileService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(documentsPath))
            documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrEmpty(documentsPath))
            documentsPath = Environment.CurrentDirectory;

        ProfilesDirectory = Path.Combine(documentsPath, "AgValoniaGPS", "NtripProfiles");

        if (!Directory.Exists(ProfilesDirectory))
        {
            Directory.CreateDirectory(ProfilesDirectory);
        }
    }

    public NtripProfile? GetProfileForField(string fieldDirectoryName)
    {
        // First look for a profile that has this field in its associations
        var fieldProfile = _profiles.FirstOrDefault(p =>
            p.AssociatedFields.Contains(fieldDirectoryName, StringComparer.OrdinalIgnoreCase));

        if (fieldProfile != null)
            return fieldProfile;

        // Fall back to default profile
        return DefaultProfile;
    }

    public async Task LoadProfilesAsync()
    {
        _profiles.Clear();

        if (!Directory.Exists(ProfilesDirectory))
        {
            Directory.CreateDirectory(ProfilesDirectory);
        }

        var jsonFiles = Directory.GetFiles(ProfilesDirectory, "*.json");
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var profile = JsonSerializer.Deserialize<NtripProfile>(json, _jsonOptions);
                if (profile != null)
                {
                    profile.FilePath = file;
                    _profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading NTRIP profile from '{File}'", file);
            }
        }

        // Migrate legacy settings if no profiles exist
        if (_profiles.Count == 0)
        {
            await MigrateLegacySettingsAsync();
        }

        // Sort by name
        _profiles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Migrates legacy NTRIP settings from appsettings.json to a default profile
    /// </summary>
    private async Task MigrateLegacySettingsAsync()
    {
        var settings = _settingsService.Settings;

        // Only migrate if there are legacy settings
        if (string.IsNullOrEmpty(settings.NtripCasterIp))
        {
            _logger.LogDebug("No legacy settings to migrate");
            return;
        }

        _logger.LogInformation("Migrating legacy NTRIP settings to default profile...");

        var defaultProfile = new NtripProfile
        {
            Name = "Default",
            CasterHost = settings.NtripCasterIp,
            CasterPort = settings.NtripCasterPort,
            MountPoint = settings.NtripMountPoint,
            Username = settings.NtripUsername,
            Password = settings.NtripPassword,
            AutoConnectOnFieldLoad = settings.NtripAutoConnect,
            IsDefault = true
        };

        await SaveProfileAsync(defaultProfile);
        _logger.LogInformation("Created default profile from legacy settings: {Host}:{Port}/{MountPoint}",
            defaultProfile.CasterHost, defaultProfile.CasterPort, defaultProfile.MountPoint);
    }

    public async Task SaveProfileAsync(NtripProfile profile)
    {
        // Ensure directory exists
        if (!Directory.Exists(ProfilesDirectory))
        {
            Directory.CreateDirectory(ProfilesDirectory);
        }

        // Generate file path if not set
        if (string.IsNullOrEmpty(profile.FilePath))
        {
            var safeFileName = SanitizeFileName(profile.Name);
            profile.FilePath = Path.Combine(ProfilesDirectory, $"{safeFileName}.json");
        }

        // If this profile is being set as default, clear default on others
        if (profile.IsDefault)
        {
            foreach (var p in _profiles.Where(p => p.Id != profile.Id && p.IsDefault))
            {
                p.IsDefault = false;
                await SaveProfileToFileAsync(p);
            }
        }

        await SaveProfileToFileAsync(profile);

        // Update in-memory list
        var existing = _profiles.FindIndex(p => p.Id == profile.Id);
        if (existing >= 0)
        {
            _profiles[existing] = profile;
        }
        else
        {
            _profiles.Add(profile);
            _profiles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null)
            return;

        // Delete file
        if (!string.IsNullOrEmpty(profile.FilePath) && File.Exists(profile.FilePath))
        {
            await Task.Run(() => File.Delete(profile.FilePath));
        }

        // Remove from in-memory list
        _profiles.Remove(profile);

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetDefaultProfileAsync(string? profileId)
    {
        foreach (var profile in _profiles)
        {
            var shouldBeDefault = profile.Id == profileId;
            if (profile.IsDefault != shouldBeDefault)
            {
                profile.IsDefault = shouldBeDefault;
                await SaveProfileToFileAsync(profile);
            }
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public NtripProfile CreateNewProfile(string name)
    {
        return new NtripProfile
        {
            Name = name,
            CasterPort = 2101,
            AutoConnectOnFieldLoad = true
        };
    }

    public IReadOnlyList<string> GetAvailableFields()
    {
        var fieldsDirectory = _settingsService.Settings.FieldsDirectory;
        if (string.IsNullOrEmpty(fieldsDirectory) || !Directory.Exists(fieldsDirectory))
            return Array.Empty<string>();

        return Directory.GetDirectories(fieldsDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name)
            .ToList()!;
    }

    private async Task SaveProfileToFileAsync(NtripProfile profile)
    {
        var json = JsonSerializer.Serialize(profile, _jsonOptions);
        await File.WriteAllTextAsync(profile.FilePath, json);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());
        return string.IsNullOrEmpty(sanitized) ? "Profile" : sanitized;
    }
}
