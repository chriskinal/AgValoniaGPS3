// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AgValoniaGPS.Models.Tram;

namespace AgValoniaGPS.Services.Tram;

/// <summary>
/// Save/load tram systems to TramSystems.json in field directory.
/// </summary>
public static class TramSystemFileService
{
    private const string FileName = "TramSystems.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public static void Save(string fieldDirectory, IReadOnlyList<TramSystem> systems)
    {
        var data = new List<TramSystemDto>();
        foreach (var sys in systems)
        {
            data.Add(new TramSystemDto
            {
                Name = sys.Name,
                ReferenceTrackName = sys.ReferenceTrackName,
                ReferenceBoundaryIndex = sys.ReferenceBoundaryIndex,
                TramWidth = sys.TramWidth,
                Mode = sys.Mode.ToString(),
                Offset = sys.Offset,
                Direction = sys.Direction.ToString(),
                PassCount = sys.PassCount,
                IsEnabled = sys.IsEnabled
            });
        }

        var json = JsonSerializer.Serialize(data, _options);
        File.WriteAllText(Path.Combine(fieldDirectory, FileName), json);
    }

    public static List<TramSystem> Load(string fieldDirectory)
    {
        var filePath = Path.Combine(fieldDirectory, FileName);
        if (!File.Exists(filePath))
            return new List<TramSystem>();

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<List<TramSystemDto>>(json);
            if (data == null) return new List<TramSystem>();

            var result = new List<TramSystem>();
            foreach (var dto in data)
            {
                result.Add(new TramSystem
                {
                    Name = dto.Name ?? "",
                    ReferenceTrackName = dto.ReferenceTrackName,
                    ReferenceBoundaryIndex = dto.ReferenceBoundaryIndex,
                    TramWidth = dto.TramWidth,
                    Mode = Enum.TryParse<TramSystemMode>(dto.Mode, out var m) ? m : TramSystemMode.TrackLine,
                    Offset = dto.Offset,
                    Direction = Enum.TryParse<TramDirection>(dto.Direction, out var d) ? d : TramDirection.Symmetric,
                    PassCount = dto.PassCount,
                    IsEnabled = dto.IsEnabled
                });
            }
            return result;
        }
        catch
        {
            return new List<TramSystem>();
        }
    }

    private class TramSystemDto
    {
        public string? Name { get; set; }
        public string? ReferenceTrackName { get; set; }
        public int ReferenceBoundaryIndex { get; set; } = -1;
        public double TramWidth { get; set; } = 24.0;
        public string? Mode { get; set; }
        public double Offset { get; set; }
        public string? Direction { get; set; }
        public int PassCount { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
