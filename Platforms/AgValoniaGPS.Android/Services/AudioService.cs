// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using AgValoniaGPS.Services.Audio;
using Android.Media;

namespace AgValoniaGPS.Android.Services;

/// <summary>
/// Android audio service using Android.Media.MediaPlayer.
/// </summary>
public class AudioService : AudioServiceBase
{
    private MediaPlayer? _mediaPlayer;

    protected override void PlayFile(string filePath)
    {
        _mediaPlayer?.Release();
        _mediaPlayer = null;

        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.SetDataSource(filePath);
        _mediaPlayer.Prepare();
        _mediaPlayer.Start();
        _mediaPlayer.Completion += (s, e) =>
        {
            _mediaPlayer?.Release();
            _mediaPlayer = null;
        };
    }
}
