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
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Tool;

namespace AgValoniaGPS.Services.Profile;

/// <summary>
/// Saves and loads vehicle profiles as structured JSON, replacing the flat AgOpenGPS XML format.
/// Key improvement: dynamic section array (no 17-section hard limit).
/// </summary>
public static class ProfileJsonService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Check whether a JSON profile exists for the given name.
    /// </summary>
    public static bool Exists(string vehiclesDirectory, string profileName)
    {
        return File.Exists(GetJsonPath(vehiclesDirectory, profileName));
    }

    /// <summary>
    /// Save a VehicleProfile as JSON.
    /// </summary>
    public static void Save(string vehiclesDirectory, VehicleProfile profile)
    {
        if (!Directory.Exists(vehiclesDirectory))
            Directory.CreateDirectory(vehiclesDirectory);

        var dto = ToDto(profile);
        var json = JsonSerializer.Serialize(dto, Options);
        File.WriteAllText(GetJsonPath(vehiclesDirectory, profile.Name), json);
    }

    /// <summary>
    /// Load a VehicleProfile from a JSON file. Returns null if the file does not exist.
    /// </summary>
    public static VehicleProfile? Load(string vehiclesDirectory, string profileName)
    {
        var path = GetJsonPath(vehiclesDirectory, profileName);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<ProfileDto>(json, Options);
        if (dto == null)
            return null;

        return FromDto(dto, profileName, path);
    }

    private static string GetJsonPath(string vehiclesDirectory, string profileName)
    {
        return Path.Combine(vehiclesDirectory, $"{profileName}.json");
    }

    // ---------------------------------------------------------------
    // DTO <-> VehicleProfile mapping
    // ---------------------------------------------------------------

    private static ProfileDto ToDto(VehicleProfile p)
    {
        // Trim trailing zeros from section positions
        int usedPositions = p.NumSections + 1;
        var sectionPositions = new double[usedPositions];
        Array.Copy(p.SectionPositions, sectionPositions, Math.Min(usedPositions, p.SectionPositions.Length));

        return new ProfileDto
        {
            FormatVersion = 1,
            Vehicle = new VehicleDto
            {
                AntennaHeight = p.Vehicle.AntennaHeight,
                AntennaPivot = p.Vehicle.AntennaPivot,
                AntennaOffset = p.Vehicle.AntennaOffset,
                Wheelbase = p.Vehicle.Wheelbase,
                TrackWidth = p.Vehicle.TrackWidth,
                Type = (int)p.Vehicle.Type,
                MaxSteerAngle = p.Vehicle.MaxSteerAngle,
                MaxAngularVelocity = p.Vehicle.MaxAngularVelocity,
            },
            Guidance = new GuidanceDto
            {
                GoalPointLookAheadHold = p.Vehicle.GoalPointLookAheadHold,
                GoalPointLookAheadMult = p.Vehicle.GoalPointLookAheadMult,
                GoalPointAcquireFactor = p.Vehicle.GoalPointAcquireFactor,
                StanleyDistanceErrorGain = p.Vehicle.StanleyDistanceErrorGain,
                StanleyHeadingErrorGain = p.Vehicle.StanleyHeadingErrorGain,
                StanleyIntegralGainAB = p.Vehicle.StanleyIntegralGainAB,
                PurePursuitIntegralGain = p.Vehicle.PurePursuitIntegralGain,
                IsPurePursuit = p.IsPurePursuit,
                UTurnCompensation = p.Vehicle.UTurnCompensation,
            },
            Tool = new ToolDto
            {
                Width = p.Tool.Width,
                Overlap = p.Tool.Overlap,
                Offset = p.Tool.Offset,
                HitchLength = p.Tool.HitchLength,
                TrailingHitchLength = p.Tool.TrailingHitchLength,
                TankTrailingHitchLength = p.Tool.TankTrailingHitchLength,
                TrailingToolToPivotLength = p.Tool.TrailingToolToPivotLength,
                IsToolTrailing = p.Tool.IsToolTrailing,
                IsToolTBT = p.Tool.IsToolTBT,
                IsToolRearFixed = p.Tool.IsToolRearFixed,
                IsToolFrontFixed = p.Tool.IsToolFrontFixed,
                MinCoverage = p.Tool.MinCoverage,
                IsMultiColoredSections = p.Tool.IsMultiColoredSections,
                IsSectionsNotZones = p.Tool.IsSectionsNotZones,
                IsSectionOffWhenOut = p.Tool.IsSectionOffWhenOut,
                IsHeadlandSectionControl = p.Tool.IsHeadlandSectionControl,
                LookAheadOn = p.Tool.LookAheadOnSetting,
                LookAheadOff = p.Tool.LookAheadOffSetting,
                TurnOffDelay = p.Tool.TurnOffDelay,
            },
            Sections = new SectionsDto
            {
                Count = p.NumSections,
                Positions = sectionPositions,
            },
            YouTurn = new YouTurnDto
            {
                TurnRadius = p.YouTurn.TurnRadius,
                ExtensionLength = p.YouTurn.ExtensionLength,
                DistanceFromBoundary = p.YouTurn.DistanceFromBoundary,
                SkipWidth = p.YouTurn.SkipWidth,
                Style = p.YouTurn.Style,
                Smoothing = p.YouTurn.Smoothing,
            },
            General = new GeneralDto
            {
                IsMetric = p.IsMetric,
                IsSimulatorOn = p.IsSimulatorOn,
                SimLatitude = p.SimLatitude,
                SimLongitude = p.SimLongitude,
            },
        };
    }

#pragma warning disable CS0612 // Type or member is obsolete (VehicleProfile/VehicleConfiguration/ToolConfiguration/YouTurnConfiguration)
    private static VehicleProfile FromDto(ProfileDto dto, string profileName, string filePath)
    {
        var sectionPositions = new double[17];
        if (dto.Sections?.Positions != null)
            Array.Copy(dto.Sections.Positions, sectionPositions, Math.Min(dto.Sections.Positions.Length, 17));

        return new VehicleProfile
        {
            Name = profileName,
            FilePath = filePath,
            Vehicle = new VehicleConfiguration
            {
                AntennaHeight = dto.Vehicle?.AntennaHeight ?? 3.0,
                AntennaPivot = dto.Vehicle?.AntennaPivot ?? 0.0,
                AntennaOffset = dto.Vehicle?.AntennaOffset ?? 0.0,
                Wheelbase = dto.Vehicle?.Wheelbase ?? 2.5,
                TrackWidth = dto.Vehicle?.TrackWidth ?? 1.8,
                Type = (VehicleType)(dto.Vehicle?.Type ?? 0),
                MaxSteerAngle = dto.Vehicle?.MaxSteerAngle ?? 35.0,
                MaxAngularVelocity = dto.Vehicle?.MaxAngularVelocity ?? 35.0,
                GoalPointLookAheadHold = dto.Guidance?.GoalPointLookAheadHold ?? 4.0,
                GoalPointLookAheadMult = dto.Guidance?.GoalPointLookAheadMult ?? 1.4,
                GoalPointAcquireFactor = dto.Guidance?.GoalPointAcquireFactor ?? 1.5,
                StanleyDistanceErrorGain = dto.Guidance?.StanleyDistanceErrorGain ?? 0.8,
                StanleyHeadingErrorGain = dto.Guidance?.StanleyHeadingErrorGain ?? 1.0,
                StanleyIntegralGainAB = dto.Guidance?.StanleyIntegralGainAB ?? 0.0,
                PurePursuitIntegralGain = dto.Guidance?.PurePursuitIntegralGain ?? 0.0,
                UTurnCompensation = dto.Guidance?.UTurnCompensation ?? 1.0,
            },
            Tool = new ToolConfiguration
            {
                Width = dto.Tool?.Width ?? 6.0,
                Overlap = dto.Tool?.Overlap ?? 0.0,
                Offset = dto.Tool?.Offset ?? 0.0,
                HitchLength = dto.Tool?.HitchLength ?? -1.8,
                TrailingHitchLength = dto.Tool?.TrailingHitchLength ?? -2.5,
                TankTrailingHitchLength = dto.Tool?.TankTrailingHitchLength ?? 3.0,
                TrailingToolToPivotLength = dto.Tool?.TrailingToolToPivotLength ?? 0.0,
                IsToolTrailing = dto.Tool?.IsToolTrailing ?? false,
                IsToolTBT = dto.Tool?.IsToolTBT ?? false,
                IsToolRearFixed = dto.Tool?.IsToolRearFixed ?? true,
                IsToolFrontFixed = dto.Tool?.IsToolFrontFixed ?? false,
                NumOfSections = dto.Sections?.Count ?? 1,
                MinCoverage = dto.Tool?.MinCoverage ?? 100,
                IsMultiColoredSections = dto.Tool?.IsMultiColoredSections ?? false,
                IsSectionsNotZones = dto.Tool?.IsSectionsNotZones ?? true,
                IsSectionOffWhenOut = dto.Tool?.IsSectionOffWhenOut ?? true,
                IsHeadlandSectionControl = dto.Tool?.IsHeadlandSectionControl ?? true,
                LookAheadOnSetting = dto.Tool?.LookAheadOn ?? 1.0,
                LookAheadOffSetting = dto.Tool?.LookAheadOff ?? 0.5,
                TurnOffDelay = dto.Tool?.TurnOffDelay ?? 0.0,
            },
            YouTurn = new YouTurnConfiguration
            {
                TurnRadius = dto.YouTurn?.TurnRadius ?? 8.0,
                ExtensionLength = dto.YouTurn?.ExtensionLength ?? 20.0,
                DistanceFromBoundary = dto.YouTurn?.DistanceFromBoundary ?? 2.0,
                SkipWidth = dto.YouTurn?.SkipWidth ?? 1,
                Style = dto.YouTurn?.Style ?? 0,
                Smoothing = dto.YouTurn?.Smoothing ?? 14,
                UTurnCompensation = dto.Guidance?.UTurnCompensation ?? 1.0,
            },
            SectionPositions = sectionPositions,
            NumSections = dto.Sections?.Count ?? 1,
            IsMetric = dto.General?.IsMetric ?? false,
            IsPurePursuit = dto.Guidance?.IsPurePursuit ?? true,
            IsSimulatorOn = dto.General?.IsSimulatorOn ?? true,
            SimLatitude = dto.General?.SimLatitude ?? 32.5904315166667,
            SimLongitude = dto.General?.SimLongitude ?? -87.1804217333333,
        };
    }
#pragma warning restore CS0612

    // ---------------------------------------------------------------
    // DTOs -- structured JSON representation
    // ---------------------------------------------------------------

    internal class ProfileDto
    {
        public int FormatVersion { get; set; }
        public VehicleDto? Vehicle { get; set; }
        public GuidanceDto? Guidance { get; set; }
        public ToolDto? Tool { get; set; }
        public SectionsDto? Sections { get; set; }
        public YouTurnDto? YouTurn { get; set; }
        public GeneralDto? General { get; set; }
    }

    internal class VehicleDto
    {
        public double AntennaHeight { get; set; }
        public double AntennaPivot { get; set; }
        public double AntennaOffset { get; set; }
        public double Wheelbase { get; set; }
        public double TrackWidth { get; set; }
        public int Type { get; set; }
        public double MaxSteerAngle { get; set; }
        public double MaxAngularVelocity { get; set; }
    }

    internal class GuidanceDto
    {
        public double GoalPointLookAheadHold { get; set; }
        public double GoalPointLookAheadMult { get; set; }
        public double GoalPointAcquireFactor { get; set; }
        public double StanleyDistanceErrorGain { get; set; }
        public double StanleyHeadingErrorGain { get; set; }
        public double StanleyIntegralGainAB { get; set; }
        public double PurePursuitIntegralGain { get; set; }
        public bool IsPurePursuit { get; set; }
        public double UTurnCompensation { get; set; }
    }

    internal class ToolDto
    {
        public double Width { get; set; }
        public double Overlap { get; set; }
        public double Offset { get; set; }
        public double HitchLength { get; set; }
        public double TrailingHitchLength { get; set; }
        public double TankTrailingHitchLength { get; set; }
        public double TrailingToolToPivotLength { get; set; }
        public bool IsToolTrailing { get; set; }
        public bool IsToolTBT { get; set; }
        public bool IsToolRearFixed { get; set; }
        public bool IsToolFrontFixed { get; set; }
        public int MinCoverage { get; set; }
        public bool IsMultiColoredSections { get; set; }
        public bool IsSectionsNotZones { get; set; }
        public bool IsSectionOffWhenOut { get; set; }
        public bool IsHeadlandSectionControl { get; set; }
        public double LookAheadOn { get; set; }
        public double LookAheadOff { get; set; }
        public double TurnOffDelay { get; set; }
    }

    internal class SectionsDto
    {
        public int Count { get; set; }
        public double[] Positions { get; set; } = Array.Empty<double>();
    }

    internal class YouTurnDto
    {
        public double TurnRadius { get; set; }
        public double ExtensionLength { get; set; }
        public double DistanceFromBoundary { get; set; }
        public int SkipWidth { get; set; }
        public int Style { get; set; }
        public int Smoothing { get; set; }
    }

    internal class GeneralDto
    {
        public bool IsMetric { get; set; }
        public bool IsSimulatorOn { get; set; }
        public double SimLatitude { get; set; }
        public double SimLongitude { get; set; }
    }
}
