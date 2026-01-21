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

using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Tool;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services;

/// <summary>
/// Service for managing vehicle profiles in AgOpenGPS XML format
/// </summary>
public class VehicleProfileService : IVehicleProfileService
{
    private readonly ILogger<VehicleProfileService> _logger;
    private VehicleProfile? _activeProfile;

    public string VehiclesDirectory { get; }

    public VehicleProfile? ActiveProfile => _activeProfile;

    public event EventHandler<VehicleProfile?>? ActiveProfileChanged;

    public VehicleProfileService(ILogger<VehicleProfileService> logger)
    {
        _logger = logger;
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        VehiclesDirectory = Path.Combine(documentsPath, "AgValoniaGPS", "Vehicles");

        // Ensure directory exists
        if (!Directory.Exists(VehiclesDirectory))
        {
            Directory.CreateDirectory(VehiclesDirectory);
        }
    }

    public List<string> GetAvailableProfiles()
    {
        if (!Directory.Exists(VehiclesDirectory))
            return new List<string>();

        return Directory.GetFiles(VehiclesDirectory, "*.XML")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToList();
    }

    public VehicleProfile? Load(string profileName)
    {
        var filePath = Path.Combine(VehiclesDirectory, $"{profileName}.XML");
        if (!File.Exists(filePath))
            return null;

        try
        {
            var doc = XDocument.Load(filePath);
            var settings = ParseSettings(doc);

            var profile = new VehicleProfile
            {
                Name = profileName,
                FilePath = filePath,
                Vehicle = ParseVehicleConfiguration(settings),
                Tool = ParseToolConfiguration(settings),
                YouTurn = ParseYouTurnConfiguration(settings),
                SectionPositions = ParseSectionPositions(settings),
                NumSections = GetInt(settings, "setVehicle_numSections", 1),
                IsMetric = GetBool(settings, "setMenu_isMetric", false),
                IsPurePursuit = GetBool(settings, "setMenu_isPureOn", true),
                IsSimulatorOn = GetBool(settings, "setMenu_isSimulatorOn", true),
                SimLatitude = GetDouble(settings, "setGPS_SimLatitude", 32.5904315166667),
                SimLongitude = GetDouble(settings, "setGPS_SimLongitude", -87.1804217333333)
            };

            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle profile '{ProfileName}'", profileName);
            return null;
        }
    }

    public void Save(VehicleProfile profile)
    {
        var filePath = string.IsNullOrEmpty(profile.FilePath)
            ? Path.Combine(VehiclesDirectory, $"{profile.Name}.XML")
            : profile.FilePath;

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("configuration",
                new XElement("userSettings",
                    new XElement("AgOpenGPS.Properties.Settings",
                        CreateVehicleSettings(profile),
                        CreateToolSettings(profile),
                        CreateYouTurnSettings(profile),
                        CreateSectionSettings(profile),
                        CreateGeneralSettings(profile)
                    )
                )
            )
        );

        doc.Save(filePath);
        profile.FilePath = filePath;
    }

    public bool SetActiveProfile(string profileName)
    {
        var profile = Load(profileName);
        if (profile == null)
            return false;

        _activeProfile = profile;
        ActiveProfileChanged?.Invoke(this, profile);
        return true;
    }

    public VehicleProfile CreateDefaultProfile(string profileName)
    {
        var profile = new VehicleProfile
        {
            Name = profileName,
            FilePath = Path.Combine(VehiclesDirectory, $"{profileName}.XML"),
            Vehicle = new VehicleConfiguration(),
            Tool = new ToolConfiguration
            {
                Width = 6.0,
                NumOfSections = 1
            },
            YouTurn = new YouTurnConfiguration(),
            SectionPositions = new double[17],
            NumSections = 1,
            IsMetric = false,
            IsPurePursuit = true,
            IsSimulatorOn = true
        };

        // Set default section positions for single section
        profile.SectionPositions[0] = -3.0;  // Left edge
        profile.SectionPositions[1] = 3.0;   // Right edge

        Save(profile);
        return profile;
    }

    #region Parsing Helpers

    private Dictionary<string, string> ParseSettings(XDocument doc)
    {
        return doc.Descendants("setting")
            .Where(s => s.Attribute("name") != null)
            .ToDictionary(
                s => s.Attribute("name")!.Value,
                s => s.Element("value")?.Value ?? ""
            );
    }

    private VehicleConfiguration ParseVehicleConfiguration(Dictionary<string, string> settings)
    {
        return new VehicleConfiguration
        {
            AntennaHeight = GetDouble(settings, "setVehicle_antennaHeight", 3.0),
            AntennaPivot = GetDouble(settings, "setVehicle_antennaPivot", 0.0),
            AntennaOffset = GetDouble(settings, "setVehicle_antennaOffset", 0.0),
            Wheelbase = GetDouble(settings, "setVehicle_wheelbase", 2.5),
            TrackWidth = GetDouble(settings, "setVehicle_trackWidth", 1.8),
            Type = (VehicleType)GetInt(settings, "setVehicle_vehicleType", 0),
            MaxSteerAngle = GetDouble(settings, "setVehicle_maxSteerAngle", 35.0),
            MaxAngularVelocity = GetDouble(settings, "setVehicle_maxAngularVelocity", 35.0),
            GoalPointLookAheadHold = GetDouble(settings, "setVehicle_goalPointLookAheadHold", 4.0),
            GoalPointLookAheadMult = GetDouble(settings, "setVehicle_goalPointLookAheadMult", 1.4),
            GoalPointAcquireFactor = GetDouble(settings, "setVehicle_goalPointAcquireFactor", 1.5),
            StanleyDistanceErrorGain = GetDouble(settings, "stanleyDistanceErrorGain", 0.8),
            StanleyHeadingErrorGain = GetDouble(settings, "stanleyHeadingErrorGain", 1.0),
            StanleyIntegralGainAB = GetDouble(settings, "stanleyIntegralGainAB", 0.0),
            PurePursuitIntegralGain = GetDouble(settings, "purePursuitIntegralGainAB", 0.0),
            UTurnCompensation = GetDouble(settings, "setAS_uTurnCompensation", 1.0)
        };
    }

    private ToolConfiguration ParseToolConfiguration(Dictionary<string, string> settings)
    {
        return new ToolConfiguration
        {
            Width = GetDouble(settings, "setVehicle_toolWidth", 6.0),
            Overlap = GetDouble(settings, "setVehicle_toolOverlap", 0.0),
            Offset = GetDouble(settings, "setVehicle_toolOffset", 0.0),
            HitchLength = GetDouble(settings, "setVehicle_hitchLength", -1.8),
            TrailingHitchLength = GetDouble(settings, "setTool_toolTrailingHitchLength", -2.5),
            TankTrailingHitchLength = GetDouble(settings, "setVehicle_tankTrailingHitchLength", 3.0),
            TrailingToolToPivotLength = GetDouble(settings, "setTool_trailingToolToPivotLength", 0.0),
            IsToolTrailing = GetBool(settings, "setTool_isToolTrailing", false),
            IsToolTBT = GetBool(settings, "setTool_isToolTBT", false),
            IsToolRearFixed = GetBool(settings, "setTool_isToolRearFixed", true),
            IsToolFrontFixed = GetBool(settings, "setTool_isToolFront", false),
            NumOfSections = GetInt(settings, "setVehicle_numSections", 1),
            MinCoverage = GetInt(settings, "setVehicle_minCoverage", 100),
            IsSectionsNotZones = GetBool(settings, "setTool_isSectionsNotZones", true),
            IsSectionOffWhenOut = GetBool(settings, "setTool_isSectionOffWhenOut", true),
            IsHeadlandSectionControl = GetBool(settings, "setTool_isHeadlandSectionControl", true),
            LookAheadOnSetting = GetDouble(settings, "setVehicle_toolLookAheadOn", 1.0),
            LookAheadOffSetting = GetDouble(settings, "setVehicle_toolLookAheadOff", 0.5),
            TurnOffDelay = GetDouble(settings, "setVehicle_toolOffDelay", 0.0)
        };
    }

    private YouTurnConfiguration ParseYouTurnConfiguration(Dictionary<string, string> settings)
    {
        return new YouTurnConfiguration
        {
            TurnRadius = GetDouble(settings, "set_youTurnRadius", 8.0),
            ExtensionLength = GetDouble(settings, "set_youTurnExtensionLength", 20.0),
            DistanceFromBoundary = GetDouble(settings, "set_youTurnDistanceFromBoundary", 2.0),
            SkipWidth = GetInt(settings, "set_youSkipWidth", 1),
            Style = GetInt(settings, "set_uTurnStyle", 0),
            Smoothing = GetInt(settings, "setAS_uTurnSmoothing", 14),
            UTurnCompensation = GetDouble(settings, "setAS_uTurnCompensation", 1.0)
        };
    }

    private double[] ParseSectionPositions(Dictionary<string, string> settings)
    {
        var positions = new double[17];
        for (int i = 0; i < 17; i++)
        {
            positions[i] = GetDouble(settings, $"setSection_position{i + 1}", 0.0);
        }
        return positions;
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

    #region Save Helpers

    private IEnumerable<XElement> CreateVehicleSettings(VehicleProfile profile)
    {
        var v = profile.Vehicle;
        yield return CreateSetting("setVehicle_antennaHeight", v.AntennaHeight);
        yield return CreateSetting("setVehicle_antennaPivot", v.AntennaPivot);
        yield return CreateSetting("setVehicle_antennaOffset", v.AntennaOffset);
        yield return CreateSetting("setVehicle_wheelbase", v.Wheelbase);
        yield return CreateSetting("setVehicle_trackWidth", v.TrackWidth);
        yield return CreateSetting("setVehicle_vehicleType", (int)v.Type);
        yield return CreateSetting("setVehicle_maxSteerAngle", v.MaxSteerAngle);
        yield return CreateSetting("setVehicle_maxAngularVelocity", v.MaxAngularVelocity);
        yield return CreateSetting("setVehicle_goalPointLookAheadHold", v.GoalPointLookAheadHold);
        yield return CreateSetting("setVehicle_goalPointLookAheadMult", v.GoalPointLookAheadMult);
        yield return CreateSetting("setVehicle_goalPointAcquireFactor", v.GoalPointAcquireFactor);
        yield return CreateSetting("stanleyDistanceErrorGain", v.StanleyDistanceErrorGain);
        yield return CreateSetting("stanleyHeadingErrorGain", v.StanleyHeadingErrorGain);
        yield return CreateSetting("stanleyIntegralGainAB", v.StanleyIntegralGainAB);
        yield return CreateSetting("purePursuitIntegralGainAB", v.PurePursuitIntegralGain);
        yield return CreateSetting("setAS_uTurnCompensation", v.UTurnCompensation);
    }

    private IEnumerable<XElement> CreateToolSettings(VehicleProfile profile)
    {
        var t = profile.Tool;
        yield return CreateSetting("setVehicle_toolWidth", t.Width);
        yield return CreateSetting("setVehicle_toolOverlap", t.Overlap);
        yield return CreateSetting("setVehicle_toolOffset", t.Offset);
        yield return CreateSetting("setVehicle_hitchLength", t.HitchLength);
        yield return CreateSetting("setTool_toolTrailingHitchLength", t.TrailingHitchLength);
        yield return CreateSetting("setVehicle_tankTrailingHitchLength", t.TankTrailingHitchLength);
        yield return CreateSetting("setTool_trailingToolToPivotLength", t.TrailingToolToPivotLength);
        yield return CreateSetting("setTool_isToolTrailing", t.IsToolTrailing);
        yield return CreateSetting("setTool_isToolTBT", t.IsToolTBT);
        yield return CreateSetting("setTool_isToolRearFixed", t.IsToolRearFixed);
        yield return CreateSetting("setTool_isToolFront", t.IsToolFrontFixed);
        yield return CreateSetting("setVehicle_numSections", t.NumOfSections);
        yield return CreateSetting("setVehicle_minCoverage", t.MinCoverage);
        yield return CreateSetting("setTool_isSectionsNotZones", t.IsSectionsNotZones);
        yield return CreateSetting("setTool_isSectionOffWhenOut", t.IsSectionOffWhenOut);
        yield return CreateSetting("setTool_isHeadlandSectionControl", t.IsHeadlandSectionControl);
        yield return CreateSetting("setVehicle_toolLookAheadOn", t.LookAheadOnSetting);
        yield return CreateSetting("setVehicle_toolLookAheadOff", t.LookAheadOffSetting);
        yield return CreateSetting("setVehicle_toolOffDelay", t.TurnOffDelay);
    }

    private IEnumerable<XElement> CreateYouTurnSettings(VehicleProfile profile)
    {
        var y = profile.YouTurn;
        yield return CreateSetting("set_youTurnRadius", y.TurnRadius);
        yield return CreateSetting("set_youTurnExtensionLength", y.ExtensionLength);
        yield return CreateSetting("set_youTurnDistanceFromBoundary", y.DistanceFromBoundary);
        yield return CreateSetting("set_youSkipWidth", y.SkipWidth);
        yield return CreateSetting("set_uTurnStyle", y.Style);
        yield return CreateSetting("setAS_uTurnSmoothing", y.Smoothing);
    }

    private IEnumerable<XElement> CreateSectionSettings(VehicleProfile profile)
    {
        for (int i = 0; i < 17; i++)
        {
            yield return CreateSetting($"setSection_position{i + 1}", profile.SectionPositions[i]);
        }
    }

    private IEnumerable<XElement> CreateGeneralSettings(VehicleProfile profile)
    {
        yield return CreateSetting("setMenu_isMetric", profile.IsMetric);
        yield return CreateSetting("setMenu_isPureOn", profile.IsPurePursuit);
        yield return CreateSetting("setMenu_isSimulatorOn", profile.IsSimulatorOn);
        yield return CreateSetting("setGPS_SimLatitude", profile.SimLatitude);
        yield return CreateSetting("setGPS_SimLongitude", profile.SimLongitude);
    }

    private XElement CreateSetting(string name, object value)
    {
        string stringValue = value switch
        {
            double d => d.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "True" : "False",
            _ => value.ToString() ?? ""
        };

        return new XElement("setting",
            new XAttribute("name", name),
            new XAttribute("serializeAs", "String"),
            new XElement("value", stringValue)
        );
    }

    #endregion
}
