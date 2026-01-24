using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using AgLibrary.Logging;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.AgShare;
using AgOpenGPS.Core.Services.AgShare;
using AgOpenGPS.Forms;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for AgShare upload functionality.
    /// Delegates to Core AgShareUploaderService.
    /// </summary>
    public class CAgShareUploader
    {
        private static readonly AgShareUploaderService _uploaderService = new AgShareUploaderService();

        /// <summary>
        /// Create a snapshot from the current GPS session to upload
        /// </summary>
        public static FieldSnapshot CreateSnapshot(FormGPS gps)
        {
            string dir = Path.Combine(RegistrySettings.fieldsDirectory, gps.currentFieldDirectory);
            string idPath = Path.Combine(dir, "agshare.txt");

            Guid fieldId;
            if (File.Exists(idPath))
            {
                string raw = File.ReadAllText(idPath).Trim();
                fieldId = Guid.Parse(raw);
            }
            else
            {
                fieldId = Guid.NewGuid();
            }

            List<List<vec3>> boundaries = new List<List<vec3>>();
            foreach (var b in gps.bnd.bndList)
            {
                boundaries.Add(b.fenceLine.ToList());
            }

            List<CTrk> tracks = gps.trk.gArr.ToList();

            Wgs84 origin = gps.AppModel.LocalPlane.Origin;
            LocalPlane plane = new LocalPlane(origin, new SharedFieldProperties());

            FieldSnapshot snapshot = new FieldSnapshot
            {
                FieldName = gps.displayFieldName,
                FieldDirectory = dir,
                FieldId = fieldId,
                OriginLat = origin.Latitude,
                OriginLon = origin.Longitude,
                Convergence = 0,
                Boundaries = boundaries,
                Tracks = tracks,
                Converter = plane
            };
            return snapshot;
        }

        /// <summary>
        /// Upload snapshot to AgShare using boundary with holes
        /// </summary>
        public static async Task UploadAsync(FieldSnapshot snapshot, AgShareClient client, FormGPS gps)
        {
            try
            {
                // Convert WinForms FieldSnapshot to Core FieldSnapshotInput
                var input = new FieldSnapshotInput
                {
                    FieldName = snapshot.FieldName,
                    FieldId = snapshot.FieldId,
                    Origin = new Wgs84(snapshot.OriginLat, snapshot.OriginLon),
                    Convergence = snapshot.Convergence,
                    Boundaries = snapshot.Boundaries.Select(b => b.Select(v => (Vec3)v).ToList()).ToList(),
                    Tracks = snapshot.Tracks.Select(t => new TrackLineInput
                    {
                        Name = t.name,
                        Mode = (Core.Models.AgShare.TrackMode)(int)t.mode, // Cast via int
                        PtA = new Vec3(t.ptA.easting, t.ptA.northing, 0),
                        PtB = new Vec3(t.ptB.easting, t.ptB.northing, 0),
                        CurvePoints = t.curvePts.Select(v => (Vec3)v).ToList()
                    }).ToList(),
                    IsPublic = false
                };

                // Delegate to Core service
                var result = await _uploaderService.UploadFieldAsync(
                    input,
                    client.GetCoreClient(),
                    snapshot.FieldDirectory
                );

                if (result.success)
                {
                    Log.EventWriter($"Field uploaded to AgShare: {snapshot.FieldName} ({result.fieldId})");
                }
                else
                {
                    Log.EventWriter($"Failed to upload field to AgShare: {result.message}");
                }
            }
            catch (Exception ex)
            {
                Log.EventWriter("Error uploading field to AgShare: " + ex.Message);
            }
        }
    }
}
