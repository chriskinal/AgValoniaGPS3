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
using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Profile;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for managing vehicle profiles.
/// Loads/saves directly to/from ConfigurationStore using JSON format.
/// Supports legacy AgOpenGPS XML import.
/// </summary>
public class VehicleProfileService : IVehicleProfileService
{
    private readonly ILogger<VehicleProfileService> _logger;

    public string VehiclesDirectory { get; }

    public VehicleProfileService(ILogger<VehicleProfileService> logger)
        : this(logger, DefaultVehiclesDirectory())
    {
    }

    /// <summary>
    /// Test seam: lets a test subclass redirect VehiclesDirectory away
    /// from MyDocuments without touching env vars or filesystem mocks.
    /// </summary>
    protected VehicleProfileService(ILogger<VehicleProfileService> logger, string vehiclesDirectory)
    {
        _logger = logger;
        VehiclesDirectory = vehiclesDirectory;

        if (!Directory.Exists(VehiclesDirectory))
        {
            Directory.CreateDirectory(VehiclesDirectory);
        }
    }

    private static string DefaultVehiclesDirectory()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "AgValoniaGPS", "Vehicles");
    }

    public List<string> GetAvailableProfiles()
    {
        if (!Directory.Exists(VehiclesDirectory))
            return new List<string>();

        var xmlProfiles = Directory.GetFiles(VehiclesDirectory, "*.XML")
            .Select(f => Path.GetFileNameWithoutExtension(f));
        var jsonProfiles = Directory.GetFiles(VehiclesDirectory, "*.json")
            .Where(f => !f.EndsWith(".AutoSteer.json", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetFileNameWithoutExtension(f));

        return xmlProfiles.Concat(jsonProfiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }

    public bool Load(string profileName, ConfigurationStore store)
    {
        try
        {
            // Prefer v2 vehicle-only format (#346) — JSON with FormatVersion >= 2.
            if (VehicleProfileJsonService.Load(VehiclesDirectory, profileName, store))
                return true;

            // Fall back to v1 combined JSON. V1 hydrates the entire store
            // (Vehicle + Guidance + YouTurn + General + Tool + Sections); the
            // Tool side will be re-written to its own file on next save, and
            // the v1 → v2 migration in ConfigurationService handles the
            // proactive split for users who don't save first.
            if (ProfileJsonServiceV1.Load(VehiclesDirectory, profileName, store))
                return true;

            // Last-resort fall back to legacy AOG XML.
            var filePath = ResolveExistingFile(profileName, ".xml");
            if (filePath == null)
                return false;

            // Build a single dictionary by merging the primary file (legacy combined
            // or 6.8.2 VehicleSettings) with any sibling 6.8.2 split files. The new
            // format keeps the same <setting name="..."> keys as the old combined
            // file — they're just spread across multiple files — so a merged
            // dictionary feeds ApplyXmlSettingsToStore unchanged.
            var settings = ParseSettings(XDocument.Load(filePath));
            MergeSiblingIfPresent(profileName, ".tool.xml", settings);
            MergeSiblingIfPresent(profileName, ".env.xml", settings);

            ApplyXmlSettingsToStore(settings, profileName, filePath, store);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle profile '{ProfileName}'", profileName);
            return false;
        }
    }

    /// <summary>
    /// Case-insensitively resolve a {profileName}{suffix} file in the Vehicles
    /// directory. Returns the actual on-disk path or null if not found.
    /// Needed because the existing convention is .XML (uppercase) for the
    /// primary file but the 6.8.2 sibling files use .xml (lowercase).
    /// </summary>
    private string? ResolveExistingFile(string profileName, string suffix)
    {
        // Check exact match first (cheapest case on case-sensitive filesystems)
        var primary = Path.Combine(VehiclesDirectory, profileName + suffix);
        if (File.Exists(primary)) return primary;

        var upper = Path.Combine(VehiclesDirectory, profileName + suffix.ToUpperInvariant());
        if (File.Exists(upper)) return upper;

        // Fallback: enumerate the directory once and match case-insensitively
        if (!Directory.Exists(VehiclesDirectory)) return null;
        var target = profileName + suffix;
        foreach (var path in Directory.EnumerateFiles(VehiclesDirectory))
        {
            if (string.Equals(Path.GetFileName(path), target, StringComparison.OrdinalIgnoreCase))
                return path;
        }
        return null;
    }

    private void MergeSiblingIfPresent(string profileName, string suffix, Dictionary<string, string> dict)
    {
        var path = ResolveExistingFile(profileName, suffix);
        if (path == null) return;

        try
        {
            var siblingDoc = XDocument.Load(path);
            foreach (var kv in ParseSettings(siblingDoc))
            {
                // Sibling values take precedence on key collision; with the 6.8.2
                // split there shouldn't be any, but if there is the more-specific
                // file (tool/env) wins.
                dict[kv.Key] = kv.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse sibling settings file '{Path}'", path);
        }
    }

    public void Save(string profileName, ConfigurationStore store)
    {
        // v2 vehicle-only format (#346). Tool/Sections are persisted via
        // IToolProfileService — ConfigurationService coordinates both sides.
        VehicleProfileJsonService.Save(VehiclesDirectory, profileName, store);
    }

    public bool Rename(string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
            return false;

        var oldPath = Path.Combine(VehiclesDirectory, $"{oldName}.json");
        var newPath = Path.Combine(VehiclesDirectory, $"{newName}.json");
        if (!File.Exists(oldPath))
            return false;

        // Allow case-only rename on case-insensitive filesystems.
        bool caseOnly = string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(oldName, newName, StringComparison.Ordinal);
        if (!caseOnly && File.Exists(newPath) &&
            !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            return false;

        File.Move(oldPath, newPath, overwrite: caseOnly);
        return true;
    }

    public bool Delete(string profileName)
    {
        if (string.IsNullOrEmpty(profileName))
            return false;
        var path = Path.Combine(VehiclesDirectory, $"{profileName}.json");
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    public void CreateDefaultProfile(string profileName, ConfigurationStore store)
    {
        // Reset store to defaults for vehicle-profile-related fields
        store.Vehicle.Name = profileName;
        store.Vehicle.Type = VehicleType.Tractor;
        store.Vehicle.AntennaHeight = 3.0;
        store.Vehicle.AntennaPivot = 0.0;
        store.Vehicle.AntennaOffset = 0.0;
        store.Vehicle.Wheelbase = 2.5;
        store.Vehicle.TrackWidth = 1.8;
        store.Vehicle.MaxSteerAngle = 35.0;
        store.Vehicle.MaxAngularVelocity = 35.0;

        store.Guidance.IsPurePursuit = true;
        store.Guidance.GoalPointLookAheadHold = 4.0;
        store.Guidance.GoalPointLookAheadMult = 1.4;
        store.Guidance.GoalPointAcquireFactor = 1.5;
        store.Guidance.MinLookAheadDistance = 2.0;
        store.Guidance.StanleyDistanceErrorGain = 0.8;
        store.Guidance.StanleyHeadingErrorGain = 1.0;
        store.Guidance.StanleyIntegralGainAB = 0.0;
        store.Guidance.PurePursuitIntegralGain = 0.0;
        store.Guidance.UTurnCompensation = 1.0;
        store.Guidance.UTurnRadius = 8.0;
        store.Guidance.UTurnExtension = 20.0;
        store.Guidance.UTurnDistanceFromBoundary = 2.0;
        store.Guidance.UTurnSkipWidth = 1;
        store.Guidance.UTurnStyle = 0;
        store.Guidance.UTurnSmoothing = 14;

        store.Tool.Width = 6.0;
        store.Tool.Overlap = 0.0;
        store.Tool.Offset = 0.0;
        store.Tool.HitchLength = 1.8;
        store.Tool.TrailingHitchLength = 2.5;
        store.Tool.TankTrailingHitchLength = 3.0;
        store.Tool.TrailingToolToPivotLength = 0.0;
        store.Tool.IsToolTrailing = false;
        store.Tool.IsToolTBT = false;
        store.Tool.IsToolRearFixed = true;
        store.Tool.IsToolFrontFixed = false;
        store.Tool.LookAheadOnSetting = 0.0;
        store.Tool.LookAheadOffSetting = 0.0;
        store.Tool.TurnOffDelay = 0.0;
        store.Tool.MinCoverage = 100;
        store.Tool.IsMultiColoredSections = false;
        store.Tool.IsSectionOffWhenOut = true;
        store.Tool.IsHeadlandSectionControl = true;

        store.NumSections = 1;
        var sectionPositions = new double[17];
        sectionPositions[0] = -3.0;  // Left edge
        sectionPositions[1] = 3.0;   // Right edge
        store.SectionPositions = sectionPositions;

        // IsMetric used to be reset here; it now lives in AppSettings and
        // is unaffected by creating a new default vehicle profile.

        store.ActiveVehicleProfileName = profileName;
        store.ActiveVehicleProfilePath = Path.Combine(VehiclesDirectory, $"{profileName}.json");

        // Save the new default profile
        Save(profileName, store);
    }

    #region Legacy XML Parsing

    /// <summary>
    /// Applies parsed XML settings directly to ConfigurationStore.
    /// Used for one-way import from AgOpenGPS XML format.
    /// </summary>
    private void ApplyXmlSettingsToStore(Dictionary<string, string> settings, string profileName, string filePath, ConfigurationStore store)
    {
        // Vehicle config
        store.Vehicle.Name = profileName;
        store.Vehicle.Type = (VehicleType)GetInt(settings, "setVehicle_vehicleType", 0);
        store.Vehicle.AntennaHeight = GetDouble(settings, "setVehicle_antennaHeight", 3.0);
        store.Vehicle.AntennaPivot = GetDouble(settings, "setVehicle_antennaPivot", 0.0);
        store.Vehicle.AntennaOffset = GetDouble(settings, "setVehicle_antennaOffset", 0.0);
        store.Vehicle.Wheelbase = GetDouble(settings, "setVehicle_wheelbase", 2.5);
        store.Vehicle.TrackWidth = GetDouble(settings, "setVehicle_trackWidth", 1.8);
        store.Vehicle.MaxSteerAngle = GetDouble(settings, "setVehicle_maxSteerAngle", 35.0);
        store.Vehicle.MaxAngularVelocity = GetDouble(settings, "setVehicle_maxAngularVelocity", 35.0);

        // Guidance config
        store.Guidance.IsPurePursuit = GetBool(settings, "setMenu_isPureOn", true);
        store.Guidance.GoalPointLookAheadHold = GetDouble(settings, "setVehicle_goalPointLookAheadHold", 4.0);
        store.Guidance.GoalPointLookAheadMult = GetDouble(settings, "setVehicle_goalPointLookAheadMult", 1.4);
        store.Guidance.GoalPointAcquireFactor = GetDouble(settings, "setVehicle_goalPointAcquireFactor", 1.5);
        store.Guidance.StanleyDistanceErrorGain = GetDouble(settings, "stanleyDistanceErrorGain", 0.8);
        store.Guidance.StanleyHeadingErrorGain = GetDouble(settings, "stanleyHeadingErrorGain", 1.0);
        store.Guidance.StanleyIntegralGainAB = GetDouble(settings, "stanleyIntegralGainAB", 0.0);
        store.Guidance.PurePursuitIntegralGain = GetDouble(settings, "purePursuitIntegralGainAB", 0.0);
        store.Guidance.UTurnCompensation = GetDouble(settings, "setAS_uTurnCompensation", 1.0);

        // U-Turn settings
        store.Guidance.UTurnRadius = GetDouble(settings, "set_youTurnRadius", 8.0);
        store.Guidance.UTurnExtension = GetDouble(settings, "set_youTurnExtensionLength", 20.0);
        store.Guidance.UTurnDistanceFromBoundary = GetDouble(settings, "set_youTurnDistanceFromBoundary", 2.0);
        store.Guidance.UTurnSkipWidth = GetInt(settings, "set_youSkipWidth", 1);
        store.Guidance.UTurnStyle = GetInt(settings, "set_uTurnStyle", 0);
        store.Guidance.UTurnSmoothing = GetInt(settings, "setAS_uTurnSmoothing", 14);

        // Tool config
        store.Tool.Width = GetDouble(settings, "setVehicle_toolWidth", 6.0);
        store.Tool.Overlap = GetDouble(settings, "setVehicle_toolOverlap", 0.0);
        store.Tool.Offset = GetDouble(settings, "setVehicle_toolOffset", 0.0);
        store.Tool.HitchLength = GetDouble(settings, "setVehicle_hitchLength", 1.8);
        // Legacy AOG XML profiles often store TrailingHitchLength as a negative value due to
        // a historical sign convention. Migrate to "positive = behind hitch" by taking abs.
        store.Tool.TrailingHitchLength = Math.Abs(GetDouble(settings, "setTool_toolTrailingHitchLength", 2.5));
        store.Tool.TankTrailingHitchLength = GetDouble(settings, "setVehicle_tankTrailingHitchLength", 3.0);
        store.Tool.TrailingToolToPivotLength = GetDouble(settings, "setTool_trailingToolToPivotLength", 0.0);
        store.Tool.IsToolTrailing = GetBool(settings, "setTool_isToolTrailing", false);
        store.Tool.IsToolTBT = GetBool(settings, "setTool_isToolTBT", false);
        store.Tool.IsToolRearFixed = GetBool(settings, "setTool_isToolRearFixed", true);
        store.Tool.IsToolFrontFixed = GetBool(settings, "setTool_isToolFront", false);
        store.Tool.LookAheadOnSetting = GetDouble(settings, "setVehicle_toolLookAheadOn", 1.0);
        store.Tool.LookAheadOffSetting = GetDouble(settings, "setVehicle_toolLookAheadOff", 0.5);
        store.Tool.TurnOffDelay = GetDouble(settings, "setVehicle_toolOffDelay", 0.0);
        store.Tool.MinCoverage = GetInt(settings, "setVehicle_minCoverage", 100);
        store.Tool.IsMultiColoredSections = GetBool(settings, "setTool_isMultiColoredSections", false);
        store.Tool.IsSectionsNotZones = GetBool(settings, "setTool_isSectionsNotZones", true);
        store.Tool.IsSectionOffWhenOut = GetBool(settings, "setTool_isSectionOffWhenOut", true);
        store.Tool.IsHeadlandSectionControl = GetBool(settings, "setTool_isHeadlandSectionControl", true);

        // Section config
        store.NumSections = GetInt(settings, "setVehicle_numSections", 1);
        var sectionPositions = new double[17];
        for (int i = 0; i < 17; i++)
        {
            sectionPositions[i] = GetDouble(settings, $"setSection_position{i + 1}", 0.0);
        }
        store.SectionPositions = sectionPositions;

        // Display config — IsMetric used to live in the vehicle XML.
        // It now lives in AppSettings; apply the legacy XML value to the
        // store so the post-load ReconcileIsMetricAfterProfileLoad can
        // perform the one-shot migration (the same path the JSON profile
        // services use). Once migration has completed, AppSettings
        // overrides the XML value on subsequent loads.
        if (settings.ContainsKey("setMenu_isMetric"))
            store.IsMetric = GetBool(settings, "setMenu_isMetric", false);

        // Profile metadata
        store.ActiveVehicleProfileName = profileName;
        store.ActiveVehicleProfilePath = filePath;
    }

    private Dictionary<string, string> ParseSettings(XDocument doc)
    {
        return doc.Descendants("setting")
            .Where(s => s.Attribute("name") != null)
            .ToDictionary(
                s => s.Attribute("name")!.Value,
                s => s.Element("value")?.Value ?? ""
            );
    }

    private double GetDouble(Dictionary<string, string> settings, string key, double defaultValue)
    {
        if (settings.TryGetValue(key, out var value) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    private int GetInt(Dictionary<string, string> settings, string key, int defaultValue)
    {
        if (settings.TryGetValue(key, out var value) && int.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    private bool GetBool(Dictionary<string, string> settings, string key, bool defaultValue)
    {
        if (settings.TryGetValue(key, out var value))
        {
            return value.Equals("True", StringComparison.OrdinalIgnoreCase);
        }
        return defaultValue;
    }

    #endregion
}
