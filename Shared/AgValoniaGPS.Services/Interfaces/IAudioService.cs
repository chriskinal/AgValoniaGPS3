// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Sound effects matching legacy AgOpenGPS CSound.
/// </summary>
public enum SoundEffect
{
    BoundaryAlarm,
    UTurnTooClose,
    AutoSteerOn,
    AutoSteerOff,
    HydraulicLiftUp,
    HydraulicLiftDown,
    RtkLost,
    RtkRecovered,
    SectionOn,
    SectionOff,
    Headland
}

/// <summary>
/// Cross-platform audio playback service for alert sounds.
/// </summary>
public interface IAudioService
{
    /// <summary>
    /// Play a sound effect. Non-blocking, fire-and-forget.
    /// </summary>
    void Play(SoundEffect sound);

    /// <summary>
    /// Whether audio is globally enabled.
    /// </summary>
    bool IsEnabled { get; set; }
}
