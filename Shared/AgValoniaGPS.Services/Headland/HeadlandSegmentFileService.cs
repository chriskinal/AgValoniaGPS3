// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;

namespace AgValoniaGPS.Services.Headland;

/// <summary>
/// Save/load headland segments to HeadlandSegments.json in field directory.
/// </summary>
public static class HeadlandSegmentFileService
{
    private const string FileName = "HeadlandSegments.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public static void Save(string fieldDirectory, IReadOnlyList<HeadlandSegment> segments)
    {
        var data = new List<SegmentDto>();
        foreach (var seg in segments)
        {
            data.Add(new SegmentDto
            {
                Name = seg.Name,
                Type = seg.Type.ToString(),
                Offset = seg.Offset,
                BoundaryIndex = seg.BoundaryIndex,
                BoundaryStartIndex = seg.BoundaryStartIndex,
                BoundaryEndIndex = seg.BoundaryEndIndex,
                BoundaryPoints = ConvertPoints(seg.BoundaryPoints),
                OffsetPoints = ConvertPoints(seg.OffsetPoints)
            });
        }

        var json = JsonSerializer.Serialize(data, _options);
        File.WriteAllText(Path.Combine(fieldDirectory, FileName), json);
    }

    public static List<HeadlandSegment> Load(string fieldDirectory)
    {
        var filePath = Path.Combine(fieldDirectory, FileName);
        if (!File.Exists(filePath))
            return new List<HeadlandSegment>();

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<List<SegmentDto>>(json);
            if (data == null) return new List<HeadlandSegment>();

            var result = new List<HeadlandSegment>();
            foreach (var dto in data)
            {
                var seg = new HeadlandSegment
                {
                    Name = dto.Name ?? "",
                    Type = Enum.TryParse<HeadlandSegmentType>(dto.Type, out var t) ? t : HeadlandSegmentType.Line,
                    Offset = dto.Offset,
                    BoundaryIndex = dto.BoundaryIndex,
                    BoundaryStartIndex = dto.BoundaryStartIndex,
                    BoundaryEndIndex = dto.BoundaryEndIndex,
                    BoundaryPoints = ConvertFromDto(dto.BoundaryPoints),
                    OffsetPoints = ConvertFromDto(dto.OffsetPoints)
                };
                result.Add(seg);
            }
            return result;
        }
        catch
        {
            return new List<HeadlandSegment>();
        }
    }

    private static List<double[]> ConvertPoints(List<Vec3> points)
    {
        var result = new List<double[]>();
        foreach (var p in points)
            result.Add(new[] { p.Easting, p.Northing, p.Heading });
        return result;
    }

    private static List<Vec3> ConvertFromDto(List<double[]>? points)
    {
        var result = new List<Vec3>();
        if (points == null) return result;
        foreach (var p in points)
        {
            if (p.Length >= 3)
                result.Add(new Vec3(p[0], p[1], p[2]));
        }
        return result;
    }

    private class SegmentDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public double Offset { get; set; }
        public int BoundaryIndex { get; set; }
        public int BoundaryStartIndex { get; set; }
        public int BoundaryEndIndex { get; set; }
        public List<double[]>? BoundaryPoints { get; set; }
        public List<double[]>? OffsetPoints { get; set; }
    }
}
