// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
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
        string? additionalNotes = null,
        byte[]? screenshotPng = null,
        string? outputDirectory = null,
        string filePrefix = "debug_dump",
        IReadOnlyList<string>? userAttachments = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var dumpDir = outputDirectory
            ?? Path.Combine(Path.GetTempPath(), "AgValoniaGPS", "dumps");
        Directory.CreateDirectory(dumpDir);
        var zipPath = Path.Combine(dumpDir, $"{filePrefix}_{timestamp}.zip");

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

        // 5b. GPS data recorder (last 60 seconds of position/guidance/YouTurn)
        try
        {
            var gpsCsv = Logging.GpsDataRecorder.Instance.ExportCsv();
            if (gpsCsv.Length > 50) // more than just the header
                AddTextEntry(archive, "gps_data_log.csv", gpsCsv);
        }
        catch (Exception ex)
        {
            AddTextEntry(archive, "gps_data_log_error.txt", ex.ToString());
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
            var profileName = ConfigurationStore.Instance.ActiveVehicleProfileName;
            if (!string.IsNullOrEmpty(profileName))
            {
                AddTextEntry(archive, "active_profile_name.txt", profileName);
            }
        }
        catch { }

        // 8. Screenshot
        if (screenshotPng != null && screenshotPng.Length > 0)
        {
            try
            {
                var entry = archive.CreateEntry("screenshot.png");
                using var entryStream = entry.Open();
                entryStream.Write(screenshotPng, 0, screenshotPng.Length);
            }
            catch (Exception ex)
            {
                AddTextEntry(archive, "screenshot_error.txt", ex.ToString());
            }
        }

        // 9. User notes
        if (!string.IsNullOrEmpty(additionalNotes))
        {
            AddTextEntry(archive, "user_notes.txt", additionalNotes);
        }

        // 10. User-attached files (screenshots, videos, logs, etc.)
        if (userAttachments != null)
        {
            foreach (var filePath in userAttachments)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var fileName = Path.GetFileName(filePath);
                        var entry = archive.CreateEntry($"attachments/{fileName}");
                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(filePath);
                        fileStream.CopyTo(entryStream);
                    }
                }
                catch (Exception ex)
                {
                    AddTextEntry(archive, $"attachments/{Path.GetFileName(filePath)}_error.txt", ex.ToString());
                }
            }
        }

        return zipPath;
    }

    /// <summary>
    /// Append user-supplied notes and attachments to an existing dump zip,
    /// then move it to its final location with a title-based filename.
    /// Used by the Bug Report flow so the state-snapshot zip can be created
    /// the moment the operator presses the Bug Report button (capturing app
    /// state at the time the bug occurred), and only the user-visible parts
    /// are appended after they finish typing in the dialog.
    /// </summary>
    /// <param name="sourceZipPath">Path to the existing dump zip (typically
    /// from <see cref="CreateDump"/> with no notes/attachments). Deleted on
    /// success; left in place if any step throws.</param>
    /// <param name="outputDirectory">Final destination folder. Created if
    /// missing.</param>
    /// <param name="filePrefix">Final file's prefix; the timestamp suffix
    /// is added here, not taken from the source zip's name.</param>
    /// <param name="notes">Optional user-typed notes (becomes
    /// <c>user_notes.txt</c> inside the zip).</param>
    /// <param name="userAttachments">Optional list of file paths to attach
    /// under <c>attachments/</c> inside the zip.</param>
    /// <returns>The final zip path.</returns>
    public static string FinalizeBugReport(
        string sourceZipPath,
        string outputDirectory,
        string filePrefix,
        string? notes,
        IReadOnlyList<string>? userAttachments)
    {
        Directory.CreateDirectory(outputDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var finalPath = Path.Combine(outputDirectory, $"{filePrefix}_{timestamp}.zip");

        File.Copy(sourceZipPath, finalPath, overwrite: false);

        using (var zipStream = File.Open(finalPath, FileMode.Open, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Update))
        {
            if (!string.IsNullOrEmpty(notes))
                AddTextEntry(archive, "user_notes.txt", notes);

            if (userAttachments != null)
            {
                foreach (var filePath in userAttachments)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            var fileName = Path.GetFileName(filePath);
                            var entry = archive.CreateEntry($"attachments/{fileName}");
                            using var entryStream = entry.Open();
                            using var fileStream = File.OpenRead(filePath);
                            fileStream.CopyTo(entryStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddTextEntry(archive, $"attachments/{Path.GetFileName(filePath)}_error.txt", ex.ToString());
                    }
                }
            }
        }

        try { File.Delete(sourceZipPath); } catch { /* best-effort temp cleanup */ }
        return finalPath;
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

        // Diagnostic dump is consumed by us / shared in bug reports —
        // pin every interpolation to InvariantCulture so timestamps and
        // numbers are unambiguous across reporters' locales.
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine(inv, $"App Version: {fileVersion}");
        sb.AppendLine(inv, $"Build Info: {infoVersion}");
        sb.AppendLine(inv, $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(inv, $"OS: {Environment.OSVersion}");
        sb.AppendLine(inv, $"Runtime: {Environment.Version}");
        sb.AppendLine(inv, $"64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine(inv, $"64-bit Process: {Environment.Is64BitProcess}");
        sb.AppendLine(inv, $"Machine: {Environment.MachineName}");
        sb.AppendLine(inv, $"Processors: {Environment.ProcessorCount}");
        sb.AppendLine(inv, $"Working Set: {Environment.WorkingSet / 1024 / 1024}MB");
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
            ActiveProfile = store.ActiveVehicleProfileName
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
                OriginLon = state.Field.OriginLongitude,
                DriftEasting = state.Field.DriftEasting,
                DriftNorthing = state.Field.DriftNorthing
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
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Category}: {entry.Message}");
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
