// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

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
    public double Easting { get; init; }
    public double Northing { get; init; }
    public double Heading { get; init; }
    public double Speed { get; init; }
    public double RollDegrees { get; init; }
    public int SatelliteCount { get; init; }
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

    // Guidance
    public double SteerAngle { get; init; }
    public double CrossTrackError { get; init; }
    public double GoalPointEasting { get; init; }
    public double GoalPointNorthing { get; init; }
    public bool HasGuidance { get; init; }

    // Display tracks (computed by pipeline, displayed by view)
    public Track.Track? DisplayTrack { get; init; }  // The offset track being followed
    public Track.Track? BaseTrack { get; init; }      // The reference track (when offset != 0)

    // Pass detection (auto-detect nearest pass when autosteer not engaged)
    public int? NearestPassNumber { get; init; }

    // Autosteer
    public bool IsAutoSteerEngaged { get; init; }
    public bool AutoSteerDisengagedThisCycle { get; init; }
    public string? DisengageReason { get; init; }

    // YouTurn
    public bool IsInYouTurn { get; init; }
    public bool YouTurnTriggered { get; init; }
    public bool YouTurnCompleted { get; init; }

    // Section states (compact — individual section properties updated from this)
    public bool[]? SectionStates { get; init; }
    public int[]? SectionColorCodes { get; init; }

    // Headland proximity
    public double? HeadlandProximityDistance { get; init; }
    public bool HeadlandProximityWarning { get; init; }

    // Status
    public string? StatusMessage { get; init; }
}
