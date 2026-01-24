using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.IO
{
    /// <summary>
    /// WinForms wrapper for FileIoUtils from AgOpenGPS.Core.
    /// Delegates to Core implementation using implicit type conversions.
    /// </summary>
    public static class FileIoUtils
    {
        /// <summary>
        /// Formats a double value to a specific number of decimal places using invariant culture
        /// </summary>
        public static string FormatDouble(double value, int decimals)
        {
            return Core.Utilities.FileIoUtils.FormatDouble(value, decimals);
        }

        /// <summary>
        /// Safely parses an integer from a string, returning 0 if parsing fails
        /// </summary>
        public static int ParseIntSafe(string line)
        {
            return Core.Utilities.FileIoUtils.ParseIntSafe(line);
        }

        /// <summary>
        /// Reads a block of Vec3 points from a StreamReader in CSV format
        /// </summary>
        public static List<vec3> ReadVec3Block(StreamReader r, int count)
        {
            // Use Core implementation
            var coreResult = Core.Utilities.FileIoUtils.ReadVec3Block(r, count);

            // Convert back to WinForms vec3
            return coreResult.Select(p => (vec3)p).ToList();
        }
    }
}
