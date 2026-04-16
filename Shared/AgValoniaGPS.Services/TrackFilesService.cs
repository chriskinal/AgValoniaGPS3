// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using TrackModel = AgValoniaGPS.Models.Track.Track;

namespace AgValoniaGPS.Services
{
    /// <summary>
    /// Service for loading and saving track lines (AB lines and curves) to TrackLines.txt.
    /// Format matches AgOpenGPS WinForms for field compatibility.
    /// </summary>
    public static class TrackFilesService
    {
        private const string FileName = "TrackLines.txt";
        private const string Header = "$TrackLines";

        /// <summary>
        /// Map legacy TrackMode (file format) to TrackType (runtime model).
        /// </summary>
        private static TrackType MapTrackMode(TrackMode mode) => mode switch
        {
            TrackMode.AB => TrackType.ABLine,
            TrackMode.Curve => TrackType.Curve,
            TrackMode.BndTrackOuter => TrackType.BoundaryOuter,
            TrackMode.BndTrackInner => TrackType.BoundaryInner,
            TrackMode.BndCurve => TrackType.BoundaryCurve,
            TrackMode.WaterPivot => TrackType.WaterPivot,
            TrackMode.RecordedPath => TrackType.RecordedPath,
            TrackMode.Contour => TrackType.Contour,
            _ => TrackType.ABLine
        };

        /// <summary>
        /// Map TrackType (runtime model) to legacy TrackMode (file format).
        /// </summary>
        private static TrackMode MapTrackType(TrackType type) => type switch
        {
            TrackType.ABLine => TrackMode.AB,
            TrackType.Curve => TrackMode.Curve,
            TrackType.BoundaryOuter => TrackMode.BndTrackOuter,
            TrackType.BoundaryInner => TrackMode.BndTrackInner,
            TrackType.BoundaryCurve => TrackMode.BndCurve,
            TrackType.WaterPivot => TrackMode.WaterPivot,
            TrackType.RecordedPath => TrackMode.RecordedPath,
            TrackType.Contour => TrackMode.Contour,
            _ => TrackMode.AB
        };

        /// <summary>
        /// Load tracks from TrackLines.txt file as unified Track objects.
        /// </summary>
        /// <param name="fieldDirectory">Path to the field directory</param>
        /// <returns>List of Track objects</returns>
        public static List<TrackModel> Load(string fieldDirectory)
        {
            if (string.IsNullOrWhiteSpace(fieldDirectory))
                throw new ArgumentNullException(nameof(fieldDirectory));

            var result = new List<TrackModel>();
            var path = Path.Combine(fieldDirectory, FileName);

            if (!File.Exists(path))
                return result;

            using (var reader = new StreamReader(path))
            {
                // Require header
                var header = reader.ReadLine();
                if (header == null || !header.TrimStart().StartsWith("$", StringComparison.Ordinal))
                    throw new InvalidDataException("TrackLines.txt missing $ header.");

                while (!reader.EndOfStream)
                {
                    // --- Name ---
                    var name = reader.ReadLine();
                    if (name == null) break;
                    name = name.Trim();
                    if (name.Length == 0) continue;

                    // --- Heading (in radians) ---
                    var headingLine = reader.ReadLine();
                    if (headingLine == null) throw new InvalidDataException("Unexpected EOF after track name.");
                    var headingRadians = double.Parse(headingLine.Trim(), CultureInfo.InvariantCulture);

                    // --- A point (easting,northing) ---
                    var aLine = reader.ReadLine();
                    if (aLine == null) throw new InvalidDataException("Unexpected EOF reading point A.");
                    var aParts = aLine.Split(',');
                    var aEasting = double.Parse(aParts[0], CultureInfo.InvariantCulture);
                    var aNorthing = double.Parse(aParts[1], CultureInfo.InvariantCulture);

                    // --- B point (easting,northing) ---
                    var bLine = reader.ReadLine();
                    if (bLine == null) throw new InvalidDataException("Unexpected EOF reading point B.");
                    var bParts = bLine.Split(',');
                    var bEasting = double.Parse(bParts[0], CultureInfo.InvariantCulture);
                    var bNorthing = double.Parse(bParts[1], CultureInfo.InvariantCulture);

                    // --- Nudge ---
                    var nudgeLine = reader.ReadLine();
                    if (nudgeLine == null) throw new InvalidDataException("Unexpected EOF reading nudge.");
                    var nudgeDistance = double.Parse(nudgeLine.Trim(), CultureInfo.InvariantCulture);

                    // --- Mode ---
                    var modeLine = reader.ReadLine();
                    if (modeLine == null) throw new InvalidDataException("Unexpected EOF reading mode.");
                    var modeInt = int.Parse(modeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    var mode = (TrackMode)modeInt;

                    // --- Visibility ---
                    var visLine = reader.ReadLine();
                    if (visLine == null) throw new InvalidDataException("Unexpected EOF reading visibility.");
                    var isVisible = bool.Parse(visLine.Trim());

                    // --- Curve count ---
                    var countLine = reader.ReadLine();
                    if (countLine == null) throw new InvalidDataException("Unexpected EOF reading curve count.");
                    var curveCount = int.Parse(countLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);

                    // --- Curve points ---
                    var curvePoints = new List<Vec3>();
                    for (int i = 0; i < curveCount; i++)
                    {
                        var line = reader.ReadLine();
                        if (line == null) throw new InvalidDataException("Unexpected EOF in curve points.");
                        var parts = line.Split(',');
                        var easting = double.Parse(parts[0], CultureInfo.InvariantCulture);
                        var northing = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        var pointHeading = double.Parse(parts[2], CultureInfo.InvariantCulture);
                        curvePoints.Add(new Vec3(easting, northing, pointHeading));
                    }

                    // Build Track directly from file fields
                    var track = new TrackModel
                    {
                        Name = name,
                        Type = MapTrackMode(mode),
                        IsVisible = isVisible,
                        NudgeDistance = nudgeDistance,
                        IsClosed = mode == TrackMode.WaterPivot
                    };

                    // Use curve points if available, otherwise use A/B points
                    if (curvePoints.Count > 0)
                    {
                        track.Points = new List<Vec3>(curvePoints);
                    }
                    else
                    {
                        track.Points = new List<Vec3>
                        {
                            new Vec3(aEasting, aNorthing, headingRadians),
                            new Vec3(bEasting, bNorthing, headingRadians)
                        };
                    }

                    result.Add(track);
                }
            }

            return result;
        }

        /// <summary>
        /// Save tracks to TrackLines.txt file. Overwrites existing file.
        /// </summary>
        /// <param name="fieldDirectory">Path to the field directory</param>
        /// <param name="tracks">List of Track objects to save</param>
        public static void Save(string fieldDirectory, IReadOnlyList<TrackModel> tracks)
        {
            if (string.IsNullOrWhiteSpace(fieldDirectory))
                throw new ArgumentNullException(nameof(fieldDirectory));

            var filename = Path.Combine(fieldDirectory, FileName);

            using (var writer = new StreamWriter(filename, false))
            {
                writer.WriteLine(Header);

                if (tracks == null || tracks.Count == 0)
                    return;

                foreach (var track in tracks)
                {
                    // Name
                    writer.WriteLine(track.Name ?? string.Empty);

                    // Heading in radians (Track.Heading already returns radians)
                    writer.WriteLine(track.Heading.ToString(CultureInfo.InvariantCulture));

                    // Point A (easting,northing) - first point
                    if (track.Points.Count >= 2)
                    {
                        writer.WriteLine($"{FormatDouble(track.Points[0].Easting, 3)},{FormatDouble(track.Points[0].Northing, 3)}");
                        writer.WriteLine($"{FormatDouble(track.Points[^1].Easting, 3)},{FormatDouble(track.Points[^1].Northing, 3)}");
                    }
                    else
                    {
                        writer.WriteLine("0.000,0.000");
                        writer.WriteLine("0.000,0.000");
                    }

                    // Nudge distance
                    writer.WriteLine(track.NudgeDistance.ToString(CultureInfo.InvariantCulture));

                    // Mode (as integer, mapped from TrackType)
                    writer.WriteLine(((int)MapTrackType(track.Type)).ToString(CultureInfo.InvariantCulture));

                    // Visibility
                    writer.WriteLine(track.IsVisible.ToString());

                    // Curve points - for non-AB-line tracks, store all points
                    if (track.Points.Count > 2 || track.Type != TrackType.ABLine)
                    {
                        writer.WriteLine(track.Points.Count.ToString(CultureInfo.InvariantCulture));
                        foreach (var p in track.Points)
                        {
                            writer.WriteLine($"{FormatDouble(p.Easting, 3)},{FormatDouble(p.Northing, 3)},{FormatDouble(p.Heading, 5)}");
                        }
                    }
                    else
                    {
                        writer.WriteLine("0");
                    }
                }
            }
        }

        /// <summary>
        /// Check if a TrackLines.txt file exists in the field directory
        /// </summary>
        public static bool Exists(string fieldDirectory)
        {
            if (string.IsNullOrWhiteSpace(fieldDirectory))
                return false;

            return File.Exists(Path.Combine(fieldDirectory, FileName));
        }

        private static string FormatDouble(double value, int decimalPlaces)
        {
            return value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
        }
    }
}
