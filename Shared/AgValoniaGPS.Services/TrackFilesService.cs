using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;

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
        /// Load tracks from TrackLines.txt file.
        /// </summary>
        /// <param name="fieldDirectory">Path to the field directory</param>
        /// <returns>List of ABLine objects</returns>
        public static List<ABLine> Load(string fieldDirectory)
        {
            if (string.IsNullOrWhiteSpace(fieldDirectory))
                throw new ArgumentNullException(nameof(fieldDirectory));

            var result = new List<ABLine>();
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

                    // Build ABLine - convert heading from radians to degrees
                    var track = new ABLine
                    {
                        Name = name,
                        Heading = headingRadians * 180.0 / Math.PI,  // Convert to degrees
                        PointA = new Position { Easting = aEasting, Northing = aNorthing },
                        PointB = new Position { Easting = bEasting, Northing = bNorthing },
                        NudgeDistance = nudgeDistance,
                        Mode = mode,
                        IsVisible = isVisible,
                        CurvePoints = curvePoints
                    };

                    result.Add(track);
                }
            }

            return result;
        }

        /// <summary>
        /// Save tracks to TrackLines.txt file. Overwrites existing file.
        /// </summary>
        /// <param name="fieldDirectory">Path to the field directory</param>
        /// <param name="tracks">List of ABLine objects to save</param>
        public static void Save(string fieldDirectory, IReadOnlyList<ABLine> tracks)
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

                    // Heading (convert from degrees to radians for file)
                    var headingRadians = track.Heading * Math.PI / 180.0;
                    writer.WriteLine(headingRadians.ToString(CultureInfo.InvariantCulture));

                    // Point A (easting,northing)
                    writer.WriteLine($"{FormatDouble(track.PointA.Easting, 3)},{FormatDouble(track.PointA.Northing, 3)}");

                    // Point B (easting,northing)
                    writer.WriteLine($"{FormatDouble(track.PointB.Easting, 3)},{FormatDouble(track.PointB.Northing, 3)}");

                    // Nudge distance
                    writer.WriteLine(track.NudgeDistance.ToString(CultureInfo.InvariantCulture));

                    // Mode (as integer)
                    writer.WriteLine(((int)track.Mode).ToString(CultureInfo.InvariantCulture));

                    // Visibility
                    writer.WriteLine(track.IsVisible.ToString());

                    // Curve points
                    var pts = track.CurvePoints ?? new List<Vec3>();
                    writer.WriteLine(pts.Count.ToString(CultureInfo.InvariantCulture));

                    foreach (var p in pts)
                    {
                        writer.WriteLine($"{FormatDouble(p.Easting, 3)},{FormatDouble(p.Northing, 3)},{FormatDouble(p.Heading, 5)}");
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

        /// <summary>
        /// Load tracks from TrackLines.txt file as unified Track objects.
        /// </summary>
        /// <param name="fieldDirectory">Path to the field directory</param>
        /// <returns>List of Track objects</returns>
        public static List<Models.Track.Track> LoadTracks(string fieldDirectory)
        {
            var abLines = Load(fieldDirectory);
            return abLines.Select(Models.Track.Track.FromABLine).ToList();
        }

        /// <summary>
        /// Save unified Track objects to TrackLines.txt file.
        /// </summary>
        /// <param name="fieldDirectory">Path to the field directory</param>
        /// <param name="tracks">List of Track objects to save</param>
        public static void SaveTracks(string fieldDirectory, IReadOnlyList<Models.Track.Track> tracks)
        {
            if (tracks == null || tracks.Count == 0)
            {
                Save(fieldDirectory, Array.Empty<ABLine>());
                return;
            }

            var abLines = tracks.Select(t => t.ToABLine()).ToList();
            Save(fieldDirectory, abLines);
        }

        private static string FormatDouble(double value, int decimalPlaces)
        {
            return value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
        }
    }
}
