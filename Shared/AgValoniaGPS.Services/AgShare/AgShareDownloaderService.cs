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
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.AgShare;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.AgShare
{
    /// <summary>
    /// Service for downloading field data from AgShare cloud service.
    /// </summary>
    public class AgShareDownloaderService
    {
        private readonly AgShareClient client;

        public AgShareDownloaderService(AgShareClient agShareClient)
        {
            client = agShareClient;
        }

        /// <summary>
        /// Downloads a field and saves it to disk
        /// </summary>
        public async Task<(bool success, string message)> DownloadAndSaveAsync(Guid fieldId, string fieldsDirectory)
        {
            try
            {
                string json = await client.DownloadFieldAsync(fieldId);
                var dto = JsonConvert.DeserializeObject<AgShareFieldDto>(json);
                var model = AgShareFieldParser.Parse(dto);
                string fieldDir = Path.Combine(fieldsDirectory, model.Name);
                FieldFileWriter.WriteAllFiles(model, fieldDir);
                return (true, "Download successful");
            }
            catch (Exception ex)
            {
                return (false, $"Download failed: {ex.GetType().Name} - {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a list of user-owned fields
        /// </summary>
        public async Task<List<AgShareGetOwnFieldDto>> GetOwnFieldsAsync()
        {
            return await client.GetOwnFieldsAsync();
        }

        /// <summary>
        /// Downloads a field DTO for preview only
        /// </summary>
        public async Task<AgShareFieldDto> DownloadFieldPreviewAsync(Guid fieldId)
        {
            string json = await client.DownloadFieldAsync(fieldId);
            return JsonConvert.DeserializeObject<AgShareFieldDto>(json);
        }

        /// <summary>
        /// Downloads all user fields with progress reporting
        /// </summary>
        public async Task<(int Downloaded, int Skipped)> DownloadAllAsync(
            string fieldsDirectory,
            bool forceOverwrite = false,
            IProgress<int> progress = null)
        {
            var fields = await GetOwnFieldsAsync();
            int skipped = 0, downloaded = 0;

            foreach (var field in fields)
            {
                string dir = Path.Combine(fieldsDirectory, field.Name);
                string agsharePath = Path.Combine(dir, "agshare.txt");

                bool alreadyExists = false;
                if (File.Exists(agsharePath))
                {
                    try
                    {
                        var id = File.ReadAllText(agsharePath).Trim();
                        alreadyExists = Guid.TryParse(id, out Guid guid) && guid == field.Id;
                    }
                    catch { }
                }

                if (alreadyExists && !forceOverwrite)
                {
                    skipped++;
                }
                else
                {
                    var preview = await DownloadFieldPreviewAsync(field.Id);
                    if (preview != null)
                    {
                        var model = AgShareFieldParser.Parse(preview);
                        FieldFileWriter.WriteAllFiles(model, dir);
                        downloaded++;
                    }
                }

                progress?.Report(downloaded + skipped);
            }

            return (downloaded, skipped);
        }
    }

    /// <summary>
    /// Utility class that writes a LocalFieldModel to standard AgOpenGPS-compatible files.
    /// </summary>
    public static class FieldFileWriter
    {
        /// <summary>
        /// Writes all files required for a field
        /// </summary>
        public static void WriteAllFiles(LocalFieldModel field, string fieldDir)
        {
            if (!Directory.Exists(fieldDir))
                Directory.CreateDirectory(fieldDir);

            WriteAgShareId(fieldDir, field.FieldId);
            WriteFieldTxt(fieldDir, field.Origin);
            WriteBoundaryTxt(fieldDir, field.Boundaries);
            WriteTrackLinesTxt(fieldDir, field.AbLines);
            WriteStaticFiles(fieldDir); // Flags, Headland
        }

        /// <summary>
        /// Writes agshare.txt with the field ID
        /// </summary>
        private static void WriteAgShareId(string fieldDir, Guid fieldId)
        {
            File.WriteAllText(Path.Combine(fieldDir, "agshare.txt"), fieldId.ToString());
        }

        /// <summary>
        /// Writes origin and metadata to Field.txt
        /// </summary>
        private static void WriteFieldTxt(string fieldDir, Wgs84 origin)
        {
            var fieldTxt = new List<string>
            {
                DateTime.Now.ToString("yyyy-MMM-dd hh:mm:ss tt", CultureInfo.InvariantCulture),
                "$FieldDir",
                "AgShare Downloaded",
                "$Offsets",
                "0,0",
                "Convergence",
                "0", // Always 0
                "StartFix",
                origin.Latitude.ToString(CultureInfo.InvariantCulture) + "," + origin.Longitude.ToString(CultureInfo.InvariantCulture)
            };

            File.WriteAllLines(Path.Combine(fieldDir, "Field.txt"), fieldTxt);
        }

        /// <summary>
        /// Writes outer and inner boundary rings to Boundary.txt
        /// </summary>
        private static void WriteBoundaryTxt(string fieldDir, List<List<LocalPoint>> boundaries)
        {
            if (boundaries == null || boundaries.Count == 0) return;

            var lines = new List<string> { "$Boundary" };

            for (int i = 0; i < boundaries.Count; i++)
            {
                var ring = boundaries[i];
                bool isHole = i != 0;

                lines.Add(isHole ? "True" : "False");
                lines.Add(ring.Count.ToString());

                var enriched = BoundaryUtils.WithHeadings(ConvertToVec3List(ring));

                foreach (var pt in enriched)
                {
                    lines.Add(
                        pt.Easting.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                        pt.Northing.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                        pt.Heading.ToString("0.#####", CultureInfo.InvariantCulture)
                    );
                }
            }

            File.WriteAllLines(Path.Combine(fieldDir, "Boundary.txt"), lines);
        }

        /// <summary>
        /// Writes AB-lines and optional curve points to TrackLines.txt
        /// </summary>
        private static void WriteTrackLinesTxt(string fieldDir, List<AbLineLocal> abLines)
        {
            var lines = new List<string> { "$TrackLines" };

            foreach (var ab in abLines)
            {
                lines.Add(ab.Name ?? "Unnamed");

                bool isCurve = ab.CurvePoints != null && ab.CurvePoints.Count > 1;

                LocalPoint ptA = ab.PtA;
                LocalPoint ptB = ab.PtB;
                double heading = ab.Heading;

                if (isCurve)
                {
                    ptA = ab.CurvePoints[0];
                    ptB = ab.CurvePoints[ab.CurvePoints.Count - 1];
                    heading = GeoConversion.HeadingFromPoints(
                        new Vec2(ptA.Easting, ptA.Northing),
                        new Vec2(ptB.Easting, ptB.Northing)
                    );
                }

                lines.Add(heading.ToString("0.###", CultureInfo.InvariantCulture));
                lines.Add(ptA.Easting.ToString("0.###", CultureInfo.InvariantCulture) + "," + ptA.Northing.ToString("0.###", CultureInfo.InvariantCulture));
                lines.Add(ptB.Easting.ToString("0.###", CultureInfo.InvariantCulture) + "," + ptB.Northing.ToString("0.###", CultureInfo.InvariantCulture));
                lines.Add("0"); // Nudge

                if (isCurve)
                {
                    lines.Add("4"); // Curve mode
                    lines.Add("True");
                    lines.Add(ab.CurvePoints.Count.ToString());

                    foreach (var pt in ab.CurvePoints)
                    {
                        lines.Add(
                            pt.Easting.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                            pt.Northing.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                            pt.Heading.ToString("0.#####", CultureInfo.InvariantCulture)
                        );
                    }
                }
                else
                {
                    lines.Add("2"); // AB mode
                    lines.Add("True");
                    lines.Add("0");
                }
            }

            File.WriteAllLines(Path.Combine(fieldDir, "TrackLines.txt"), lines);
        }

        /// <summary>
        /// Writes default placeholder files like Flags.txt and Headland.txt
        /// </summary>
        private static void WriteStaticFiles(string fieldDir)
        {
            File.WriteAllLines(Path.Combine(fieldDir, "Flags.txt"), new[] { "$Flags", "0" });
            File.WriteAllLines(Path.Combine(fieldDir, "Headland.txt"), new[] { "$Headland", "0" });
            File.WriteAllLines(Path.Combine(fieldDir, "Contour.txt"), new[] { "$Contour", "0" });
            File.WriteAllLines(Path.Combine(fieldDir, "Sections.txt"), new[] { "Sections", "0" });
        }

        /// <summary>
        /// Helper to convert LocalPoint list to Vec3 list
        /// </summary>
        private static List<Vec3> ConvertToVec3List(List<LocalPoint> points)
        {
            var result = new List<Vec3>();
            foreach (var pt in points)
            {
                result.Add((Vec3)pt);
            }
            return result;
        }
    }
}
