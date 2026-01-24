using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Services
{
    /// <summary>
    /// Serializes and deserializes HeadlandLine to/from AgOpenGPS file format
    /// </summary>
    public static class HeadlandLineSerializer
    {
        /// <summary>
        /// Load headland paths from Headlines.txt file
        /// </summary>
        public static HeadlandLine Load(string fieldDirectory)
        {
            var result = new HeadlandLine();
            var path = Path.Combine(fieldDirectory, "Headlines.txt");

            if (!File.Exists(path))
                return result;

            using (var reader = new StreamReader(path))
            {
                reader.ReadLine(); // optional header "$HeadLines"

                while (!reader.EndOfStream)
                {
                    var hp = LoadPath(reader);
                    if (hp != null && hp.TrackPoints.Count > 3)
                    {
                        result.Tracks.Add(hp);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Save headland line to Headlines.txt file
        /// </summary>
        public static void Save(string fieldDirectory, HeadlandLine headlandLine)
        {
            var filename = Path.Combine(fieldDirectory, "Headlines.txt");

            using (var writer = new StreamWriter(filename, false))
            {
                writer.WriteLine("$HeadLines");

                if (headlandLine == null || headlandLine.Tracks.Count == 0)
                    return;

                foreach (var track in headlandLine.Tracks)
                {
                    SavePath(writer, track);
                }
            }
        }

        private static HeadlandPath LoadPath(StreamReader reader)
        {
            var hp = new HeadlandPath();

            // Read name
            hp.Name = reader.ReadLine() ?? string.Empty;

            // Read moveDistance
            var line = reader.ReadLine();
            if (line == null) return null;
            hp.MoveDistance = double.Parse(line, CultureInfo.InvariantCulture);

            // Read mode
            line = reader.ReadLine();
            if (line == null) return null;
            hp.Mode = int.Parse(line, CultureInfo.InvariantCulture);

            // Read a_point
            line = reader.ReadLine();
            if (line == null) return null;
            hp.APointIndex = int.Parse(line, CultureInfo.InvariantCulture);

            // Read number of points
            line = reader.ReadLine();
            if (line == null) return null;
            int numPoints = int.Parse(line, CultureInfo.InvariantCulture);

            // Read track points
            for (int i = 0; i < numPoints && !reader.EndOfStream; i++)
            {
                var words = (reader.ReadLine() ?? string.Empty).Split(',');
                if (words.Length < 3) continue;

                if (double.TryParse(words[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double easting) &&
                    double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double northing) &&
                    double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double heading))
                {
                    hp.TrackPoints.Add(new Vec3(easting, northing, heading));
                }
            }

            return hp;
        }

        private static void SavePath(StreamWriter writer, HeadlandPath path)
        {
            writer.WriteLine(path.Name);
            writer.WriteLine(path.MoveDistance.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(path.Mode.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(path.APointIndex.ToString(CultureInfo.InvariantCulture));

            var pts = path.TrackPoints ?? new List<Vec3>();
            writer.WriteLine(pts.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var p in pts)
            {
                writer.WriteLine($"{FormatDouble(p.Easting, 3)} , {FormatDouble(p.Northing, 3)} , {FormatDouble(p.Heading, 5)}");
            }
        }

        private static string FormatDouble(double value, int decimalPlaces)
        {
            return value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
        }
    }
}
