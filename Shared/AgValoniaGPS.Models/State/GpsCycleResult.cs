// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Pipeline;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Immutable snapshot of one GPS processing cycle's results.
/// Produced by the service pipeline on a background thread.
/// Consumed by the ViewModel on the UI thread to update bound properties.
/// </summary>
public record GpsCycleResult
{
    // GPS position
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Altitude { get; init; }
    public double Easting { get; init; }
    public double Northing { get; init; }
    public double Heading { get; init; }
    public double Speed { get; init; }
    public double RollDegrees { get; init; }
    public int SatelliteCount { get; init; }
    public double Hdop { get; init; }
    public double DifferentialAge { get; init; }
    public int FixQuality { get; init; }
    public bool GpsValid { get; init; }

    // Tool position
    public double ToolEasting { get; init; }
    public double ToolNorthing { get; init; }
    public double ToolHeadingRadians { get; init; }
    public double ToolWidth { get; init; }
    public double HitchEasting { get; init; }
    public double HitchNorthing { get; init; }
    public bool IsToolPositionReady { get; init; }

    // Autosteer
    public bool IsAutoSteerEngaged { get; init; }
    public bool AutoSteerDisengagedThisCycle { get; init; }
    public string? DisengageReason { get; init; }

    // Per-cycle snapshots emitted by the cycle worker. Consumed on the UI
    // thread by ApplyGpsCycleResult to mirror onto State.* (PropertyChanged
    // boundary). YouTurn is non-null every cycle; Guidance is only emitted
    // when the YouTurn tick ran (otherwise the cycle's HowManyPathsAway
    // seed would fight the UI's NearestPassNumber auto-detect writer).
    public YouTurnSnapshot? YouTurn { get; init; }
    public GuidanceSnapshot? Guidance { get; init; }

    // Section states (compact — individual section properties updated from this)
    public bool[]? SectionStates { get; init; }
    public int[]? SectionColorCodes { get; init; }

    // Headland proximity
    public double? HeadlandProximityDistance { get; init; }
    public bool HeadlandProximityWarning { get; init; }

    /// <summary>
    /// Phase E: non-null on the single cycle where the cycle worker auto-creates
    /// a <see cref="LocalPlane"/> from the first GPS fix (no field open yet).
    /// <see cref="MainViewModel.ApplyGpsCycleResult"/> mirrors it onto
    /// <c>State.Field.LocalPlane</c> on the UI thread — the cycle keeps its own
    /// reference for coord conversion in the meantime. Null on every other cycle.
    /// </summary>
    public LocalPlane? FirstFixLocalPlane { get; init; }

    /// <summary>
    /// Non-null on the single cycle where the cycle worker re-anchors the
    /// temporary local plane because the live GPS source has moved beyond
    /// the configured threshold from the existing origin (no field loaded).
    /// The UI thread overwrites <c>State.Field.LocalPlane</c> and surfaces
    /// a status toast describing how far the source jumped.
    /// </summary>
    public LocalPlane? ReplacementLocalPlane { get; init; }

    /// <summary>
    /// Distance in kilometres between the previous origin and the live GPS
    /// position on the cycle that triggered <see cref="ReplacementLocalPlane"/>.
    /// </summary>
    public double ReplacementDistanceKm { get; init; }

    /// <summary>
    /// Non-null on the single cycle where the cycle worker first detects
    /// that the live GPS position is far from the loaded field origin.
    /// The UI thread disables autosteer and prompts the operator.
    /// </summary>
    public FarFromFieldWarning? FarFromFieldWarning { get; init; }

    // Status
    public string? StatusMessage { get; init; }
}

/// <summary>
/// Payload for the "GPS far from field" warning: how far the live position
/// is from the loaded field's local-plane origin, plus the offending lat/lon.
/// </summary>
public sealed record FarFromFieldWarning(
    double DistanceMeters, double Latitude, double Longitude);
