// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Services.Audio;
using NetCoreAudio;

namespace AgValoniaGPS.Desktop.Services;

/// <summary>
/// Desktop audio service using NetCoreAudio (Windows, macOS, Linux).
/// </summary>
public class AudioService : AudioServiceBase
{
    private readonly Player _player = new();

    protected override void PlayFile(string filePath)
    {
        // Fire and forget - don't await, don't block
        _ = _player.Play(filePath);
    }
}
