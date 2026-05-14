// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Services.Profile;

/// <summary>
/// v2 vehicle profile serializer. Persists Vehicle config + Guidance +
/// YouTurn + General. Tool config + Sections live in the parallel
/// <see cref="ToolProfileJsonService"/> per the AgOpenGPS 6.8.2-style
/// vehicle/tool split (#346).
/// </summary>
public static class VehicleProfileJsonService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static bool Exists(string vehiclesDirectory, string profileName)
        => File.Exists(GetJsonPath(vehiclesDirectory, profileName));

    public static void Save(string vehiclesDirectory, string profileName, ConfigurationStore store)
    {
        if (!Directory.Exists(vehiclesDirectory))
            Directory.CreateDirectory(vehiclesDirectory);

        var dto = ToDto(store);
        var json = JsonSerializer.Serialize(dto, Options);
        File.WriteAllText(GetJsonPath(vehiclesDirectory, profileName), json);
    }

    public static bool Load(string vehiclesDirectory, string profileName, ConfigurationStore store)
    {
        var path = GetJsonPath(vehiclesDirectory, profileName);
        if (!File.Exists(path))
            return false;

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<VehicleProfileDto>(json, Options);
        if (dto == null || dto.FormatVersion < 2)
            return false; // pre-v2 file; legacy reader handles it

        ApplyDtoToStore(dto, profileName, path, store);
        return true;
    }

    private static string GetJsonPath(string vehiclesDirectory, string profileName)
        => Path.Combine(vehiclesDirectory, $"{profileName}.json");

    // ── Store → DTO ────────────────────────────────────────────────────

    internal static VehicleProfileDto ToDto(ConfigurationStore store) => new()
    {
        FormatVersion = 2,
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
            // IsMetric intentionally omitted: device-/user-scoped, lives
            // in AppSettings now. Kept as a nullable field on the DTO so
            // pre-migration profiles still parse; new writes elide it.
            IsMetric = null,
            IsSimulatorOn = store.Simulator.Enabled,
            SimLatitude = store.Simulator.Latitude,
            SimLongitude = store.Simulator.Longitude,
        },
    };

    // ── DTO → Store ────────────────────────────────────────────────────

    internal static void ApplyDtoToStore(VehicleProfileDto dto, string profileName, string filePath, ConfigurationStore store)
    {
        // Vehicle
        store.Vehicle.Name = profileName;
        store.Vehicle.AntennaHeight = dto.Vehicle?.AntennaHeight ?? 3.0;
        store.Vehicle.AntennaPivot = dto.Vehicle?.AntennaPivot ?? 0.0;
        store.Vehicle.AntennaOffset = dto.Vehicle?.AntennaOffset ?? 0.0;
        store.Vehicle.Wheelbase = dto.Vehicle?.Wheelbase ?? 2.5;
        store.Vehicle.TrackWidth = dto.Vehicle?.TrackWidth ?? 1.8;
        store.Vehicle.Type = (VehicleType)(dto.Vehicle?.Type ?? 0);
        store.Vehicle.MaxSteerAngle = dto.Vehicle?.MaxSteerAngle ?? 35.0;
        store.Vehicle.MaxAngularVelocity = dto.Vehicle?.MaxAngularVelocity ?? 35.0;

        // Guidance
        store.Guidance.IsPurePursuit = dto.Guidance?.IsPurePursuit ?? true;
        store.Guidance.GoalPointLookAheadHold = dto.Guidance?.GoalPointLookAheadHold ?? 4.0;
        store.Guidance.GoalPointLookAheadMult = dto.Guidance?.GoalPointLookAheadMult ?? 1.4;
        store.Guidance.GoalPointAcquireFactor = dto.Guidance?.GoalPointAcquireFactor ?? 1.5;
        store.Guidance.StanleyDistanceErrorGain = dto.Guidance?.StanleyDistanceErrorGain ?? 0.8;
        store.Guidance.StanleyHeadingErrorGain = dto.Guidance?.StanleyHeadingErrorGain ?? 1.0;
        store.Guidance.StanleyIntegralGainAB = dto.Guidance?.StanleyIntegralGainAB ?? 0.0;
        store.Guidance.PurePursuitIntegralGain = dto.Guidance?.PurePursuitIntegralGain ?? 0.0;
        store.Guidance.UTurnCompensation = dto.Guidance?.UTurnCompensation ?? 1.0;
        store.Guidance.MinLookAheadDistance = dto.Guidance?.MinLookAheadDistance ?? 2.0;
        store.Guidance.StanleyIntegralDistanceAwayTriggerAB = dto.Guidance?.StanleyIntegralDistanceAwayTriggerAB ?? 0.3;
        store.Guidance.DeadZoneHeading = dto.Guidance?.DeadZoneHeading ?? 0.5;
        store.Guidance.DeadZoneDelay = dto.Guidance?.DeadZoneDelay ?? 10;
        store.Guidance.TramPasses = dto.Guidance?.TramPasses ?? 3;
        store.Guidance.TramDisplay = dto.Guidance?.TramDisplay ?? true;
        store.Guidance.TramLine = dto.Guidance?.TramLine ?? 1;
        store.Guidance.HydLiftLookAheadDistanceLeft = dto.Guidance?.HydLiftLookAheadDistanceLeft ?? 1.0;
        store.Guidance.HydLiftLookAheadDistanceRight = dto.Guidance?.HydLiftLookAheadDistanceRight ?? 1.0;

        // YouTurn (mirrored onto Guidance.UTurn* fields per current store layout)
        store.Guidance.UTurnRadius = dto.YouTurn?.TurnRadius ?? 8.0;
        store.Guidance.UTurnExtension = dto.YouTurn?.ExtensionLength ?? 20.0;
        store.Guidance.UTurnDistanceFromBoundary = dto.YouTurn?.DistanceFromBoundary ?? 2.0;
        store.Guidance.UTurnSkipWidth = dto.YouTurn?.SkipWidth ?? 1;
        store.Guidance.UTurnStyle = dto.YouTurn?.Style ?? 0;
        store.Guidance.UTurnSmoothing = dto.YouTurn?.Smoothing ?? 14;

        // General — IsMetric used to live here; it now lives in AppSettings.
        // Apply only if the legacy field is present in the file;
        // ReconcileIsMetricAfterProfileLoad post-load decides whether the
        // value sticks (one-shot migration) or AppSettings overrides it
        // (post-migration).
        if (dto.General?.IsMetric is bool legacyIsMetric)
            store.IsMetric = legacyIsMetric;

        // Profile metadata
        store.ActiveVehicleProfileName = profileName;
        store.ActiveVehicleProfilePath = filePath;
    }

    // ── DTOs ───────────────────────────────────────────────────────────

    internal class VehicleProfileDto
    {
        public int FormatVersion { get; set; }
        public VehicleDto? Vehicle { get; set; }
        public GuidanceDto? Guidance { get; set; }
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
        /// <summary>
        /// Nullable so writers can elide it for new profiles and readers
        /// can detect "field absent" vs "field present with value false".
        /// On load, a non-null value seeds AppSettings via the one-shot
        /// migration in
        /// <see cref="IConfigurationService.ReconcileIsMetricAfterProfileLoad"/>.
        /// </summary>
        public bool? IsMetric { get; set; }
        public bool IsSimulatorOn { get; set; }
        public double SimLatitude { get; set; }
        public double SimLongitude { get; set; }
    }
}
