using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Utilities
{
    /// <summary>
    /// File I/O utility functions for formatting and parsing data
    /// </summary>
    public static class FileIoUtils
    {
        /// <summary>
        /// Formats a double value to a specific number of decimal places using invariant culture.
        /// Useful for ensuring consistent file output across different regional settings.
        /// </summary>
        /// <param name="value">The value to format</param>
        /// <param name="decimals">Number of decimal places</param>
        /// <returns>Formatted string representation</returns>
        public static string FormatDouble(double value, int decimals)
        {
            return Math.Round(value, decimals).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Safely parses an integer from a string, returning 0 if parsing fails.
        /// Uses invariant culture for consistent parsing.
        /// </summary>
        /// <param name="line">The string to parse</param>
        /// <returns>Parsed integer value, or 0 if parsing fails</returns>
        public static int ParseIntSafe(string line)
        {
            if (!string.IsNullOrWhiteSpace(line) &&
                int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                return v;
            }
            return 0;
        }

        /// <summary>
        /// Reads a block of Vec3 points from a StreamReader in CSV format.
        /// Expected format: easting,northing,heading per line.
        /// Skips lines that don't have valid numeric data.
        /// </summary>
        /// <param name="reader">StreamReader to read from</param>
        /// <param name="count">Number of points to read</param>
        /// <returns>List of Vec3 points read from the stream</returns>
        public static List<Vec3> ReadVec3Block(StreamReader reader, int count)
        {
            var list = new List<Vec3>(count > 0 ? count : 0);
            for (int i = 0; i < count && !reader.EndOfStream; i++)
            {
                var words = (reader.ReadLine() ?? string.Empty).Split(',');
                if (words.Length < 3) continue;

                if (double.TryParse(words[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double e) &&
                    double.TryParse(words[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double n) &&
                    double.TryParse(words[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
                {
                    list.Add(new Vec3(e, n, h));
                }
            }
            return list;
        }
    }
}
