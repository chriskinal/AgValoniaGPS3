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
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Services.Profile;

/// <summary>
/// Saves and loads vehicle profiles as structured JSON, replacing the flat AgOpenGPS XML format.
/// Serializes directly from/to ConfigurationStore sub-configs.
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
    /// Save the current ConfigurationStore state as a JSON profile.
    /// </summary>
    public static void Save(string vehiclesDirectory, string profileName, ConfigurationStore store)
    {
        if (!Directory.Exists(vehiclesDirectory))
            Directory.CreateDirectory(vehiclesDirectory);

        var dto = ToDto(profileName, store);
        var json = JsonSerializer.Serialize(dto, Options);
        File.WriteAllText(GetJsonPath(vehiclesDirectory, profileName), json);
    }

    /// <summary>
    /// Load a JSON profile directly into a ConfigurationStore.
    /// Returns true if the file was found and loaded successfully.
    /// </summary>
    public static bool Load(string vehiclesDirectory, string profileName, ConfigurationStore store)
    {
        var path = GetJsonPath(vehiclesDirectory, profileName);
        if (!File.Exists(path))
            return false;

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<ProfileDto>(json, Options);
        if (dto == null)
            return false;

        ApplyDtoToStore(dto, profileName, path, store);
        return true;
    }

    private static string GetJsonPath(string vehiclesDirectory, string profileName)
    {
        return Path.Combine(vehiclesDirectory, $"{profileName}.json");
    }

    // ---------------------------------------------------------------
    // ConfigurationStore -> DTO mapping
    // ---------------------------------------------------------------

    private static ProfileDto ToDto(string profileName, ConfigurationStore store)
    {
        // Section widths (cm) are the runtime source of truth — Tool.SectionWidths
        // is what the UI edits and what SectionControlService.RecalculateSectionPositions
        // consumes. Persist the live widths array, sized to the actual section count
        // so a 6-section tool doesn't carry along 10 trailing 100 cm defaults.
        int numSections = Math.Max(1, store.NumSections);
        var sectionWidths = new double[numSections];
        for (int i = 0; i < numSections; i++)
            sectionWidths[i] = store.Tool.GetSectionWidth(i);

        // Positions (m, NumSections+1 boundary points, centered on tool with Offset)
        // are derived from widths so existing readers (and earlier app builds that
        // only consume Positions) still get a consistent geometry. Without this
        // derivation the on-disk Positions array drifts away from the live widths
        // every time the user edits a section. (#section-width-persistence)
        double totalMeters = 0;
        for (int i = 0; i < numSections; i++)
            totalMeters += sectionWidths[i] / 100.0;
        var sectionPositions = new double[numSections + 1];
        sectionPositions[0] = -totalMeters / 2.0 + store.Tool.Offset;
        for (int i = 0; i < numSections; i++)
            sectionPositions[i + 1] = sectionPositions[i] + sectionWidths[i] / 100.0;

        return new ProfileDto
        {
            FormatVersion = 1,
            Vehicle = new VehicleDto
            {
                AntennaHeight = store.Vehicle.AntennaHeight,
                AntennaPivot = store.Vehicle.AntennaPivot,
                AntennaOffset = store.Vehicle.AntennaOffset,
                Wheelbase = store.Vehicle.Wheelbase,
                TrackWidth = store.Vehicle.TrackWidth,
                Type = (int)store.Vehicle.Type,
                MaxSteerAngle = store.Vehicle.MaxSteerAngle,
                MaxAngularVelocity = store.Vehicle.MaxAngularVelocity,
            },
            Guidance = new GuidanceDto
            {
                GoalPointLookAheadHold = store.Guidance.GoalPointLookAheadHold,
                GoalPointLookAheadMult = store.Guidance.GoalPointLookAheadMult,
                GoalPointAcquireFactor = store.Guidance.GoalPointAcquireFactor,
                StanleyDistanceErrorGain = store.Guidance.StanleyDistanceErrorGain,
                StanleyHeadingErrorGain = store.Guidance.StanleyHeadingErrorGain,
                StanleyIntegralGainAB = store.Guidance.StanleyIntegralGainAB,
                PurePursuitIntegralGain = store.Guidance.PurePursuitIntegralGain,
                IsPurePursuit = store.Guidance.IsPurePursuit,
                UTurnCompensation = store.Guidance.UTurnCompensation,
                // #343: previously not persisted.
                MinLookAheadDistance = store.Guidance.MinLookAheadDistance,
                StanleyIntegralDistanceAwayTriggerAB = store.Guidance.StanleyIntegralDistanceAwayTriggerAB,
                DeadZoneHeading = store.Guidance.DeadZoneHeading,
                DeadZoneDelay = store.Guidance.DeadZoneDelay,
                TramPasses = store.Guidance.TramPasses,
                TramDisplay = store.Guidance.TramDisplay,
                TramLine = store.Guidance.TramLine,
                HydLiftLookAheadDistanceLeft = store.Guidance.HydLiftLookAheadDistanceLeft,
                HydLiftLookAheadDistanceRight = store.Guidance.HydLiftLookAheadDistanceRight,
            },
            Tool = new ToolDto
            {
                Width = store.Tool.Width,
                Overlap = store.Tool.Overlap,
                Offset = store.Tool.Offset,
                HitchLength = store.Tool.HitchLength,
                TrailingHitchLength = store.Tool.TrailingHitchLength,
                TankTrailingHitchLength = store.Tool.TankTrailingHitchLength,
                TrailingToolToPivotLength = store.Tool.TrailingToolToPivotLength,
                IsToolTrailing = store.Tool.IsToolTrailing,
                IsToolTBT = store.Tool.IsToolTBT,
                IsToolRearFixed = store.Tool.IsToolRearFixed,
                IsToolFrontFixed = store.Tool.IsToolFrontFixed,
                MinCoverage = store.Tool.MinCoverage,
                IsMultiColoredSections = store.Tool.IsMultiColoredSections,
                IsSectionsNotZones = store.Tool.IsSectionsNotZones,
                IsSectionOffWhenOut = store.Tool.IsSectionOffWhenOut,
                IsHeadlandSectionControl = store.Tool.IsHeadlandSectionControl,
                LookAheadOn = store.Tool.LookAheadOnSetting,
                LookAheadOff = store.Tool.LookAheadOffSetting,
                TurnOffDelay = store.Tool.TurnOffDelay,
                // #343: previously not persisted — UI edits silently dropped on save.
                DefaultSectionWidth = store.Tool.DefaultSectionWidth,
                SlowSpeedCutoff = store.Tool.SlowSpeedCutoff,
                CoverageMargin = store.Tool.CoverageMargin,
                Zones = store.Tool.Zones,
                ZoneRanges = (int[])store.Tool.ZoneRanges.Clone(),
                IsWorkSwitchEnabled = store.Tool.IsWorkSwitchEnabled,
                IsWorkSwitchActiveLow = store.Tool.IsWorkSwitchActiveLow,
                IsWorkSwitchManualSections = store.Tool.IsWorkSwitchManualSections,
                IsSteerSwitchEnabled = store.Tool.IsSteerSwitchEnabled,
                IsSteerSwitchManualSections = store.Tool.IsSteerSwitchManualSections,
                SectionColors = (uint[])store.Tool.SectionColors.Clone(),
            },
            Sections = new SectionsDto
            {
                Count = numSections,
                Positions = sectionPositions,
                Widths = sectionWidths,
            },
            YouTurn = new YouTurnDto
            {
                TurnRadius = store.Guidance.UTurnRadius,
                ExtensionLength = store.Guidance.UTurnExtension,
                DistanceFromBoundary = store.Guidance.UTurnDistanceFromBoundary,
                SkipWidth = store.Guidance.UTurnSkipWidth,
                Style = store.Guidance.UTurnStyle,
                Smoothing = store.Guidance.UTurnSmoothing,
            },
            General = new GeneralDto
            {
                IsMetric = store.IsMetric,
                IsSimulatorOn = store.Simulator.Enabled,
                SimLatitude = store.Simulator.Latitude,
                SimLongitude = store.Simulator.Longitude,
            },
        };
    }

    // ---------------------------------------------------------------
    // DTO -> ConfigurationStore mapping
    // ---------------------------------------------------------------

    private static void ApplyDtoToStore(ProfileDto dto, string profileName, string filePath, ConfigurationStore store)
    {
        // Vehicle config
        store.Vehicle.Name = profileName;
        store.Vehicle.AntennaHeight = dto.Vehicle?.AntennaHeight ?? 3.0;
        store.Vehicle.AntennaPivot = dto.Vehicle?.AntennaPivot ?? 0.0;
        store.Vehicle.AntennaOffset = dto.Vehicle?.AntennaOffset ?? 0.0;
        store.Vehicle.Wheelbase = dto.Vehicle?.Wheelbase ?? 2.5;
        store.Vehicle.TrackWidth = dto.Vehicle?.TrackWidth ?? 1.8;
        store.Vehicle.Type = (VehicleType)(dto.Vehicle?.Type ?? 0);
        store.Vehicle.MaxSteerAngle = dto.Vehicle?.MaxSteerAngle ?? 35.0;
        store.Vehicle.MaxAngularVelocity = dto.Vehicle?.MaxAngularVelocity ?? 35.0;

        // Guidance config
        store.Guidance.IsPurePursuit = dto.Guidance?.IsPurePursuit ?? true;
        store.Guidance.GoalPointLookAheadHold = dto.Guidance?.GoalPointLookAheadHold ?? 4.0;
        store.Guidance.GoalPointLookAheadMult = dto.Guidance?.GoalPointLookAheadMult ?? 1.4;
        store.Guidance.GoalPointAcquireFactor = dto.Guidance?.GoalPointAcquireFactor ?? 1.5;
        store.Guidance.StanleyDistanceErrorGain = dto.Guidance?.StanleyDistanceErrorGain ?? 0.8;
        store.Guidance.StanleyHeadingErrorGain = dto.Guidance?.StanleyHeadingErrorGain ?? 1.0;
        store.Guidance.StanleyIntegralGainAB = dto.Guidance?.StanleyIntegralGainAB ?? 0.0;
        store.Guidance.PurePursuitIntegralGain = dto.Guidance?.PurePursuitIntegralGain ?? 0.0;
        store.Guidance.UTurnCompensation = dto.Guidance?.UTurnCompensation ?? 1.0;
        // #343: previously not persisted — defaults match GuidanceConfig initializers.
        store.Guidance.MinLookAheadDistance = dto.Guidance?.MinLookAheadDistance ?? 2.0;
        store.Guidance.StanleyIntegralDistanceAwayTriggerAB = dto.Guidance?.StanleyIntegralDistanceAwayTriggerAB ?? 0.3;
        store.Guidance.DeadZoneHeading = dto.Guidance?.DeadZoneHeading ?? 0.5;
        store.Guidance.DeadZoneDelay = dto.Guidance?.DeadZoneDelay ?? 10;
        store.Guidance.TramPasses = dto.Guidance?.TramPasses ?? 3;
        store.Guidance.TramDisplay = dto.Guidance?.TramDisplay ?? true;
        store.Guidance.TramLine = dto.Guidance?.TramLine ?? 1;
        store.Guidance.HydLiftLookAheadDistanceLeft = dto.Guidance?.HydLiftLookAheadDistanceLeft ?? 1.0;
        store.Guidance.HydLiftLookAheadDistanceRight = dto.Guidance?.HydLiftLookAheadDistanceRight ?? 1.0;

        // U-Turn settings
        store.Guidance.UTurnRadius = dto.YouTurn?.TurnRadius ?? 8.0;
        store.Guidance.UTurnExtension = dto.YouTurn?.ExtensionLength ?? 20.0;
        store.Guidance.UTurnDistanceFromBoundary = dto.YouTurn?.DistanceFromBoundary ?? 2.0;
        store.Guidance.UTurnSkipWidth = dto.YouTurn?.SkipWidth ?? 1;
        store.Guidance.UTurnStyle = dto.YouTurn?.Style ?? 0;
        store.Guidance.UTurnSmoothing = dto.YouTurn?.Smoothing ?? 14;

        // Tool config
        store.Tool.Width = dto.Tool?.Width ?? 6.0;
        store.Tool.Overlap = dto.Tool?.Overlap ?? 0.0;
        store.Tool.Offset = dto.Tool?.Offset ?? 0.0;
        store.Tool.HitchLength = dto.Tool?.HitchLength ?? 1.8;
        // Legacy profiles may have negative TrailingHitchLength from the old default;
        // migrate to "positive = behind hitch" by taking abs.
        store.Tool.TrailingHitchLength = Math.Abs(dto.Tool?.TrailingHitchLength ?? 2.5);
        store.Tool.TankTrailingHitchLength = dto.Tool?.TankTrailingHitchLength ?? 3.0;
        store.Tool.TrailingToolToPivotLength = dto.Tool?.TrailingToolToPivotLength ?? 0.0;
        store.Tool.IsToolTrailing = dto.Tool?.IsToolTrailing ?? false;
        store.Tool.IsToolTBT = dto.Tool?.IsToolTBT ?? false;
        store.Tool.IsToolRearFixed = dto.Tool?.IsToolRearFixed ?? true;
        store.Tool.IsToolFrontFixed = dto.Tool?.IsToolFrontFixed ?? false;
        store.Tool.LookAheadOnSetting = dto.Tool?.LookAheadOn ?? 0.0;
        store.Tool.LookAheadOffSetting = dto.Tool?.LookAheadOff ?? 0.0;
        store.Tool.TurnOffDelay = dto.Tool?.TurnOffDelay ?? 0.0;
        store.Tool.MinCoverage = dto.Tool?.MinCoverage ?? 100;
        store.Tool.IsMultiColoredSections = dto.Tool?.IsMultiColoredSections ?? false;
        // Was written but not loaded — see #343.
        store.Tool.IsSectionsNotZones = dto.Tool?.IsSectionsNotZones ?? true;
        store.Tool.IsSectionOffWhenOut = dto.Tool?.IsSectionOffWhenOut ?? true;
        store.Tool.IsHeadlandSectionControl = dto.Tool?.IsHeadlandSectionControl ?? true;
        // #343: previously not persisted — defaults match the in-memory ToolConfig
        // initializers so loading an older profile (no field present) yields the
        // same behavior as an unmodified store.
        store.Tool.DefaultSectionWidth = dto.Tool?.DefaultSectionWidth ?? 100.0;
        store.Tool.SlowSpeedCutoff = dto.Tool?.SlowSpeedCutoff ?? 0.5;
        store.Tool.CoverageMargin = dto.Tool?.CoverageMargin ?? 5.0;
        store.Tool.Zones = dto.Tool?.Zones ?? 2;
        if (dto.Tool?.ZoneRanges != null && dto.Tool.ZoneRanges.Length == 9)
            store.Tool.ZoneRanges = (int[])dto.Tool.ZoneRanges.Clone();
        store.Tool.IsWorkSwitchEnabled = dto.Tool?.IsWorkSwitchEnabled ?? false;
        store.Tool.IsWorkSwitchActiveLow = dto.Tool?.IsWorkSwitchActiveLow ?? false;
        store.Tool.IsWorkSwitchManualSections = dto.Tool?.IsWorkSwitchManualSections ?? false;
        store.Tool.IsSteerSwitchEnabled = dto.Tool?.IsSteerSwitchEnabled ?? false;
        store.Tool.IsSteerSwitchManualSections = dto.Tool?.IsSteerSwitchManualSections ?? false;
        if (dto.Tool?.SectionColors != null && dto.Tool.SectionColors.Length == 16)
            store.Tool.SectionColors = (uint[])dto.Tool.SectionColors.Clone();

        // Section config — set NumSections first so width-derivation knows how
        // many sections to populate.
        store.NumSections = dto.Sections?.Count ?? 1;
        var sectionPositions = new double[17];
        if (dto.Sections?.Positions != null)
            Array.Copy(dto.Sections.Positions, sectionPositions, Math.Min(dto.Sections.Positions.Length, 17));
        store.SectionPositions = sectionPositions;

        // Restore Tool.SectionWidths — the runtime source of truth that the
        // section UI edits and SectionControlService.RecalculateSectionPositions
        // consumes. Prefer the explicit Widths array (new format). Fall back
        // to deriving from Positions for older profiles that pre-date the
        // Widths field — guard against the bogus profile shape we've seen
        // in the wild (Count=16 with only 7 valid positions; trailing zeros
        // would otherwise produce widths = [..., -91 cm, 0, 0, 0]).
        // (#section-width-persistence)
        int restoredNum = Math.Max(1, store.NumSections);
        if (dto.Sections?.Widths != null && dto.Sections.Widths.Length >= restoredNum)
        {
            for (int i = 0; i < restoredNum; i++)
                store.Tool.SetSectionWidth(i, dto.Sections.Widths[i]);
        }
        else if (dto.Sections?.Positions != null && dto.Sections.Positions.Length >= restoredNum + 1)
        {
            for (int i = 0; i < restoredNum; i++)
            {
                double widthM = dto.Sections.Positions[i + 1] - dto.Sections.Positions[i];
                if (widthM > 0)
                    store.Tool.SetSectionWidth(i, widthM * 100.0);
            }
        }

        // Display config
        store.IsMetric = dto.General?.IsMetric ?? false;

        // Profile metadata
        store.ActiveProfileName = profileName;
        store.ActiveProfilePath = filePath;
    }

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
        // #343: nullable so older profiles take ApplyDtoToStore defaults.
        public double? MinLookAheadDistance { get; set; }
        public double? StanleyIntegralDistanceAwayTriggerAB { get; set; }
        public double? DeadZoneHeading { get; set; }
        public int? DeadZoneDelay { get; set; }
        public int? TramPasses { get; set; }
        public bool? TramDisplay { get; set; }
        public int? TramLine { get; set; }
        public double? HydLiftLookAheadDistanceLeft { get; set; }
        public double? HydLiftLookAheadDistanceRight { get; set; }
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
        // #343: nullable so older profiles (no field present) take the
        // ApplyDtoToStore default and don't get reset to "0".
        public double? DefaultSectionWidth { get; set; }
        public double? SlowSpeedCutoff { get; set; }
        public double? CoverageMargin { get; set; }
        public int? Zones { get; set; }
        public int[]? ZoneRanges { get; set; }
        public bool? IsWorkSwitchEnabled { get; set; }
        public bool? IsWorkSwitchActiveLow { get; set; }
        public bool? IsWorkSwitchManualSections { get; set; }
        public bool? IsSteerSwitchEnabled { get; set; }
        public bool? IsSteerSwitchManualSections { get; set; }
        public uint[]? SectionColors { get; set; }
    }

    internal class SectionsDto
    {
        public int Count { get; set; }
        public double[] Positions { get; set; } = Array.Empty<double>();
        // cm per section, length = Count. Authoritative; Positions is derived
        // for backward compat. Nullable so older profiles that lack the field
        // fall through to position-based derivation in ApplyDtoToStore.
        public double[]? Widths { get; set; }
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
