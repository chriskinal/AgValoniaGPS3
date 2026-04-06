// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using TrackModel = AgValoniaGPS.Models.Track.Track;

namespace AgValoniaGPS.Services;

/// <summary>
/// Loads and saves RecPath.txt files (legacy AgOpenGPS format).
/// Format: header "$RecPath", point count, then CSV lines:
///   easting,northing,heading,speed,autoBtnState
/// </summary>
public static class RecPathFileService
{
    /// <summary>
    /// Load RecPath.txt from a field directory as a Track (for map display).
    /// </summary>
    public static TrackModel? LoadRecPath(string fieldDirectory)
    {
        var points = LoadRecPathPoints(fieldDirectory, "RecPath.txt");
        if (points == null || points.Count < 2) return null;

        var vec3List = points.Select(p => new Vec3(p.Easting, p.Northing, p.Heading)).ToList();
        return TrackModel.FromRecordedPath("Recorded Path", vec3List);
    }

    /// <summary>
    /// Load RecPath points with full data (speed, autoBtnState).
    /// </summary>
    public static List<RecPathPoint>? LoadRecPathPoints(string fieldDirectory, string fileName = "RecPath.txt")
    {
        var path = Path.Combine(fieldDirectory, fileName);
        return LoadRecPathPointsFromFile(path);
    }

    /// <summary>
    /// Load RecPath points from an arbitrary file path.
    /// </summary>
    public static List<RecPathPoint>? LoadRecPathPointsFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var points = new List<RecPathPoint>();

        using var reader = new StreamReader(filePath);

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
                double speed = 0.0;
                bool autoBtnState = false;

                if (words.Length >= 4)
                    double.TryParse(words[3], NumberStyles.Float, CultureInfo.InvariantCulture, out speed);
                if (words.Length >= 5)
                    bool.TryParse(words[4], out autoBtnState);

                points.Add(new RecPathPoint(easting, northing, heading, speed, autoBtnState));
            }
        }

        return points.Count >= 2 ? points : null;
    }

    /// <summary>
    /// Save recorded path points to RecPath.txt in the field directory.
    /// </summary>
    public static void SaveRecPath(string fieldDirectory, List<RecPathPoint> points)
    {
        SaveRecPathToFile(Path.Combine(fieldDirectory, "RecPath.txt"), points);
    }

    /// <summary>
    /// Save recorded path points from a Track (legacy compat, no speed/autoBtnState).
    /// </summary>
    public static void SaveRecPath(string fieldDirectory, TrackModel track)
    {
        var points = track.Points.Select(p => new RecPathPoint(p.Easting, p.Northing, p.Heading, 0.0, false)).ToList();
        SaveRecPath(fieldDirectory, points);
    }

    /// <summary>
    /// Save recorded path points to an arbitrary file path.
    /// </summary>
    public static void SaveRecPathToFile(string filePath, List<RecPathPoint> points)
    {
        using var writer = new StreamWriter(filePath, false);
        writer.WriteLine("$RecPath");
        writer.WriteLine(points.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var pt in points)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},{3:F1},{4}",
                pt.Easting, pt.Northing, pt.Heading, pt.Speed, pt.AutoBtnState));
        }
    }

    /// <summary>
    /// List all .rec files in a field directory.
    /// </summary>
    public static List<string> ListRecFiles(string fieldDirectory)
    {
        if (!Directory.Exists(fieldDirectory)) return new List<string>();
        return Directory.GetFiles(fieldDirectory, "*.rec")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// Delete a .rec file from a field directory.
    /// </summary>
    public static bool DeleteRecFile(string fieldDirectory, string fileName)
    {
        var path = Path.Combine(fieldDirectory, fileName);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }
}
