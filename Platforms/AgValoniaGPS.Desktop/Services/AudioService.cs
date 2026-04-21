// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgValoniaGPS.Services.Audio;
using NetCoreAudio;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgValoniaGPS.Desktop.Services;

/// <summary>
/// Desktop audio service using NetCoreAudio (Windows, macOS, Linux).
/// </summary>
public class AudioService : AudioServiceBase
{
    private readonly SemaphoreSlim _playLock = new(1, 1);

    protected override void PlayFile(string filePath)
    {
        // Fire and forget, but serialize to avoid NetCoreAudio race conditions.
        _ = PlayFileSafeAsync(filePath);
    }

    private async Task PlayFileSafeAsync(string filePath)
    {
        await _playLock.WaitAsync();
        try
        {
            // Use a fresh player instance per playback to avoid internal NetCoreAudio
            // WindowsPlayer state corruption between rapid consecutive plays.
            var player = new Player();
            await player.Play(filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Audio] Desktop playback failed for '{filePath}': {ex}");
        }
        finally
        {
            _playLock.Release();
        }
    }
}
