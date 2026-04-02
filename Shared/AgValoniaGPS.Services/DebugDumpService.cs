// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.Logging;

namespace AgValoniaGPS.Services;

/// <summary>
/// Creates a debug dump zip containing logs, settings, field data, and runtime state.
/// For bug reporting and diagnostics.
/// </summary>
public class DebugDumpService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    /// <summary>
    /// Create a debug dump zip file containing all diagnostic information.
    /// Returns the path to the created zip file.
    /// </summary>
    public static string CreateDump(
        ISettingsService settingsService,
        ApplicationState appState,
        string? additionalNotes = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var dumpDir = Path.Combine(Path.GetTempPath(), "AgValoniaGPS", "dumps");
        Directory.CreateDirectory(dumpDir);
        var zipPath = Path.Combine(dumpDir, $"debug_dump_{timestamp}.zip");

        using var zipStream = File.Create(zipPath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // 1. System info
        AddTextEntry(archive, "system_info.txt", BuildSystemInfo());

        // 2. App settings
        try
        {
            var settingsJson = JsonSerializer.Serialize(settingsService.Settings, JsonOptions);
            AddTextEntry(archive, "appsettings.json", settingsJson);
        }
        catch (Exception ex)
        {
            AddTextEntry(archive, "appsettings_error.txt", ex.ToString());
        }

        // 3. Configuration store snapshot
        try
        {
            var configSnapshot = BuildConfigSnapshot();
            AddTextEntry(archive, "configuration.json", configSnapshot);
        }
        catch (Exception ex)
        {
            AddTextEntry(archive, "configuration_error.txt", ex.ToString());
        }

        // 4. Runtime state snapshot
        try
        {
            var stateSnapshot = BuildStateSnapshot(appState);
            AddTextEntry(archive, "runtime_state.json", stateSnapshot);
        }
        catch (Exception ex)
        {
            AddTextEntry(archive, "state_error.txt", ex.ToString());
        }

        // 5. Log entries
        try
        {
            var logs = BuildLogDump();
            AddTextEntry(archive, "logs.txt", logs);
        }
        catch (Exception ex)
        {
            AddTextEntry(archive, "logs_error.txt", ex.ToString());
        }

        // 6. Current field files (if a field is open)
        try
        {
            var fieldDir = appState.Field.ActiveField?.DirectoryPath;
            if (!string.IsNullOrEmpty(fieldDir) && Directory.Exists(fieldDir))
            {
                foreach (var file in Directory.GetFiles(fieldDir))
                {
                    var fileName = Path.GetFileName(file);
                    // Skip large binary files
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".bin" && new FileInfo(file).Length > 5_000_000)
                        continue;

                    var entry = archive.CreateEntry($"field/{fileName}");
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(file);
                    fileStream.CopyTo(entryStream);
                }
            }
        }
        catch (Exception ex)
        {
            AddTextEntry(archive, "field_error.txt", ex.ToString());
        }

        // 7. Current vehicle profile
        try
        {
            var profileName = ConfigurationStore.Instance.ActiveProfileName;
            if (!string.IsNullOrEmpty(profileName))
            {
                AddTextEntry(archive, "active_profile_name.txt", profileName);
            }
        }
        catch { }

        // 8. User notes
        if (!string.IsNullOrEmpty(additionalNotes))
        {
            AddTextEntry(archive, "user_notes.txt", additionalNotes);
        }

        return zipPath;
    }

    private static string BuildSystemInfo()
    {
        var sb = new StringBuilder();

        // App version + git hash from AssemblyInformationalVersion
        var assembly = typeof(DebugDumpService).Assembly;
        var infoVersion = assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
        var fileVersion = assembly
            .GetCustomAttribute<System.Reflection.AssemblyFileVersionAttribute>()
            ?.Version ?? "unknown";

        sb.AppendLine($"App Version: {fileVersion}");
        sb.AppendLine($"Build Info: {infoVersion}");
        sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"Runtime: {Environment.Version}");
        sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
        sb.AppendLine($"Machine: {Environment.MachineName}");
        sb.AppendLine($"Processors: {Environment.ProcessorCount}");
        sb.AppendLine($"Working Set: {Environment.WorkingSet / 1024 / 1024}MB");
        return sb.ToString();
    }

    private static string BuildConfigSnapshot()
    {
        var store = ConfigurationStore.Instance;
        var snapshot = new
        {
            Vehicle = new
            {
                store.Vehicle.Name,
                store.Vehicle.Wheelbase,
                store.Vehicle.TrackWidth,
                store.Vehicle.AntennaHeight,
                store.Vehicle.AntennaPivot,
                store.Vehicle.AntennaOffset,
                store.Vehicle.MaxSteerAngle
            },
            Tool = new
            {
                store.Tool.Width,
                store.Tool.Overlap,
                store.Tool.Offset,
                store.Tool.HitchLength,
                store.Tool.TrailingHitchLength,
                store.Tool.IsToolTrailing,
                store.Tool.IsToolRearFixed,
                store.Tool.IsToolTBT,
                store.Tool.IsMultiColoredSections
            },
            Guidance = new
            {
                store.Guidance.GoalPointLookAheadHold,
                store.Guidance.StanleyHeadingErrorGain,
                store.Guidance.StanleyDistanceErrorGain,
                store.Guidance.UTurnRadius
            },
            NumSections = store.NumSections,
            IsMetric = store.IsMetric,
            ActiveProfile = store.ActiveProfileName
        };
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static string BuildStateSnapshot(ApplicationState state)
    {
        var snapshot = new
        {
            Vehicle = new
            {
                state.Vehicle.Latitude,
                state.Vehicle.Longitude,
                state.Vehicle.Easting,
                state.Vehicle.Northing,
                state.Vehicle.Heading,
                Speed = state.Vehicle.Speed,
                state.Vehicle.FixQuality,
                state.Vehicle.SatelliteCount,
                state.Vehicle.Hdop
            },
            Field = new
            {
                ActiveField = state.Field.ActiveField?.Name ?? "None",
                HasBoundary = state.Field.CurrentBoundary?.IsValid ?? false,
                HasHeadland = state.Field.HasHeadland,
                HeadlandDistance = state.Field.HeadlandDistance,
                TrackCount = state.Field.Tracks.Count,
                ActiveTrack = state.Field.ActiveTrack?.Name,
                OriginLat = state.Field.OriginLatitude,
                OriginLon = state.Field.OriginLongitude
            },
            Guidance = new
            {
                state.Guidance.CrossTrackError,
                state.Guidance.SteerAngle,
                state.Guidance.HeadingError
            },
            UI = new
            {
                ActiveDialog = state.UI.ActiveDialog.ToString(),
                state.UI.IsSimulatorPanelVisible,
                state.UI.IsBoundaryPanelVisible
            }
        };
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static string BuildLogDump()
    {
        var sb = new StringBuilder();
        var entries = LogStore.Instance.GetSnapshot();
        foreach (var entry in entries)
        {
            sb.AppendLine($"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Category}: {entry.Message}");
        }
        return sb.ToString();
    }

    private static void AddTextEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(content);
    }
}
