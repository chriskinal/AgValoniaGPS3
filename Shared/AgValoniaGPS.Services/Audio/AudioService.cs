// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;
using NetCoreAudio;

namespace AgValoniaGPS.Services.Audio;

/// <summary>
/// Cross-platform audio service using NetCoreAudio.
/// Extracts embedded .wav files to temp directory and plays via platform player.
/// </summary>
public class AudioService : IAudioService
{
    private readonly Dictionary<SoundEffect, string> _soundPaths = new();
    private readonly Player _player = new();
    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public AudioService()
    {
        ExtractSoundFiles();
    }

    public void Play(SoundEffect sound)
    {
        if (!_isEnabled) return;

        // Check per-sound config toggles
        var display = ConfigurationStore.Instance.Display;
        switch (sound)
        {
            case SoundEffect.AutoSteerOn:
            case SoundEffect.AutoSteerOff:
                if (!display.AutoSteerSound) return;
                break;
            case SoundEffect.UTurnTooClose:
                if (!display.UTurnSound) return;
                break;
            case SoundEffect.HydraulicLiftUp:
            case SoundEffect.HydraulicLiftDown:
                if (!display.HydraulicSound) return;
                break;
            case SoundEffect.SectionOn:
            case SoundEffect.SectionOff:
                if (!display.SectionsSound) return;
                break;
        }

        if (_soundPaths.TryGetValue(sound, out var path) && File.Exists(path))
        {
            try
            {
                // Fire and forget - don't await, don't block
                _ = _player.Play(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] Play failed for {sound}: {ex.Message}");
            }
        }
    }

    private void ExtractSoundFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AgValoniaGPS", "Sounds");
        Directory.CreateDirectory(tempDir);

        var mapping = GetFileMapping();

        // Try to find sound files from the Assets directory or extract from assembly
        var assetsDir = FindAssetsDirectory();

        foreach (var (effect, fileName) in mapping)
        {
            var destPath = Path.Combine(tempDir, fileName);

            // Only extract if not already there
            if (!File.Exists(destPath))
            {
                var sourcePath = assetsDir != null
                    ? Path.Combine(assetsDir, "Sounds", fileName)
                    : null;

                if (sourcePath != null && File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destPath, true);
                }
            }

            if (File.Exists(destPath))
            {
                _soundPaths[effect] = destPath;
            }
        }

        System.Diagnostics.Debug.WriteLine($"[Audio] Loaded {_soundPaths.Count}/{mapping.Count} sound files from {tempDir}");
    }

    private static string? FindAssetsDirectory()
    {
        // Walk up from assembly location to find Assets/Sounds
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyDir == null) return null;

        // Check common relative paths
        string[] searchPaths =
        {
            Path.Combine(assemblyDir, "Assets"),
            Path.Combine(assemblyDir, "..", "Assets"),
            Path.Combine(assemblyDir, "..", "..", "Assets"),
        };

        foreach (var path in searchPaths)
        {
            var soundsDir = Path.Combine(path, "Sounds");
            if (Directory.Exists(soundsDir))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Set a custom directory containing .wav files. Called by platform startup
    /// after extracting embedded Avalonia resources.
    /// </summary>
    public void SetSoundDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;

        var mapping = GetFileMapping();
        foreach (var (effect, fileName) in mapping)
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path))
            {
                _soundPaths[effect] = path;
            }
        }

        System.Diagnostics.Debug.WriteLine($"[Audio] Reloaded from {directory}: {_soundPaths.Count} sounds");
    }

    private static Dictionary<SoundEffect, string> GetFileMapping() => new()
    {
        { SoundEffect.BoundaryAlarm, "Alarm10.wav" },
        { SoundEffect.UTurnTooClose, "Alarm10.wav" },
        { SoundEffect.AutoSteerOn, "SteerOn.wav" },
        { SoundEffect.AutoSteerOff, "SteerOff.wav" },
        { SoundEffect.HydraulicLiftUp, "HydUp.wav" },
        { SoundEffect.HydraulicLiftDown, "HydDown.wav" },
        { SoundEffect.RtkLost, "rtk_lost.wav" },
        { SoundEffect.RtkRecovered, "rtk_back.wav" },
        { SoundEffect.SectionOn, "SectionOn.wav" },
        { SoundEffect.SectionOff, "SectionOff.wav" },
        { SoundEffect.Headland, "Headland.wav" },
    };
}
