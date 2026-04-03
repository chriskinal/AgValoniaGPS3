// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AgValoniaGPS.Models.Base;
using TrackModel = AgValoniaGPS.Models.Track.Track;

namespace AgValoniaGPS.Services;

/// <summary>
/// Loads and saves RecPath.txt files (legacy AgOpenGPS format).
/// Format: header "$RecPath", point count, then CSV lines:
///   easting,northing,heading,speed,autoBtnState
/// </summary>
public static class RecPathFileService
{
    public static TrackModel? LoadRecPath(string fieldDirectory)
    {
        var path = Path.Combine(fieldDirectory, "RecPath.txt");
        if (!File.Exists(path)) return null;

        var points = new List<Vec3>();

        using var reader = new StreamReader(path);

        // First line: "$RecPath" header or point count
        var line1 = reader.ReadLine()?.Trim();
        if (line1 == null) return null;

        string? countLine;
        if (line1.StartsWith("$"))
            countLine = reader.ReadLine()?.Trim();
        else
            countLine = line1;

        if (countLine == null || !int.TryParse(countLine, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int numPoints))
            return null;

        if (numPoints == 0) return null;

        for (int i = 0; i < numPoints && !reader.EndOfStream; i++)
        {
            var words = (reader.ReadLine() ?? string.Empty).Split(',');
            if (words.Length < 3) continue;

            if (double.TryParse(words[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double easting) &&
                double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double northing) &&
                double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double heading))
            {
                points.Add(new Vec3(easting, northing, heading));
            }
        }

        if (points.Count < 2) return null;

        return TrackModel.FromRecordedPath("Recorded Path", points);
    }

    public static void SaveRecPath(string fieldDirectory, TrackModel track)
    {
        var path = Path.Combine(fieldDirectory, "RecPath.txt");

        using var writer = new StreamWriter(path, false);
        writer.WriteLine("$RecPath");
        writer.WriteLine(track.Points.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var pt in track.Points)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},0.0,False", pt.Easting, pt.Northing, pt.Heading));
        }
    }
}
