using System.Collections.Generic;
using System.Linq;
using AgOpenGPS.Core.Services;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.IO
{
    /// <summary>
    /// WinForms wrapper for HeadlandLineSerializer from AgOpenGPS.Core
    /// Delegates all file I/O to Core serializer to prove Core services work
    /// </summary>
    public static class HeadlinesFiles
    {
        /// <summary>
        /// Load headland paths from file using Core serializer
        /// Wraps Core HeadlandPath objects in WinForms CHeadPath wrappers
        /// </summary>
        public static List<CHeadPath> Load(string fieldDirectory)
        {
            // Use Core serializer to load from file
            var coreHeadlandLine = HeadlandLineSerializer.Load(fieldDirectory);

            // Wrap Core tracks in WinForms wrappers
            return coreHeadlandLine.Tracks
                .Select(coreTrack => new CHeadPath(coreTrack))
                .ToList();
        }

        /// <summary>
        /// Save headland paths to file using Core serializer
        /// Extracts Core HeadlandPath objects from WinForms CHeadPath wrappers
        /// </summary>
        public static void Save(string fieldDirectory, IReadOnlyList<CHeadPath> headPaths)
        {
            if (headPaths == null || headPaths.Count == 0)
            {
                // Save empty headland line
                HeadlandLineSerializer.Save(fieldDirectory, new HeadlandLine());
                return;
            }

            // Create Core HeadlandLine from WinForms wrappers
            var coreHeadlandLine = new HeadlandLine
            {
                Tracks = headPaths.Select(hp => hp.CorePath).ToList()
            };

            // Use Core serializer to save to file
            HeadlandLineSerializer.Save(fieldDirectory, coreHeadlandLine);
        }
    }
}
