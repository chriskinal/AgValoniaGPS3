using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.AgShare;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Services.AgShare
{
    /// <summary>
    /// Service for uploading field data to AgShare cloud service.
    /// Handles coordinate conversion from local NE to WGS84 and payload generation.
    /// </summary>
    public class AgShareUploaderService
    {
        /// <summary>
        /// Upload a field snapshot to AgShare
        /// </summary>
        public async Task<(bool success, string message, Guid fieldId)> UploadFieldAsync(
            FieldSnapshotInput input,
            AgShareClient client,
            string fieldDirectory = null)
        {
            try
            {
                if (input.Boundaries == null || input.Boundaries.Count == 0)
                    return (false, "No boundaries to upload", Guid.Empty);

                // Create local plane for coordinate conversion
                LocalPlane plane = new LocalPlane(input.Origin, new SharedFieldProperties());

                // Convert outer boundary
                List<CoordinateDto> outer = ConvertBoundary(input.Boundaries[0], plane);
                if (outer == null || outer.Count < 3)
                    return (false, "Invalid outer boundary", Guid.Empty);

                // Convert holes (inner boundaries)
                List<List<CoordinateDto>> holes = new List<List<CoordinateDto>>();
                for (int i = 1; i < input.Boundaries.Count; i++)
                {
                    List<CoordinateDto> hole = ConvertBoundary(input.Boundaries[i], plane);
                    if (hole.Count >= 4) holes.Add(hole);
                }

                // Convert AB lines
                List<AbLineUploadDto> abLines = ConvertAbLines(input.Tracks, plane);

                // Determine field ID
                Guid fieldId = input.FieldId ?? Guid.NewGuid();

                // Check if field exists and get visibility setting
                bool isPublic = input.IsPublic;
                try
                {
                    string json = await client.DownloadFieldAsync(fieldId);
                    AgShareFieldDto field = JsonConvert.DeserializeObject<AgShareFieldDto>(json);
                    if (field != null) isPublic = field.IsPublic;
                }
                catch (Exception)
                {
                    // Field doesn't exist yet or download failed - use input isPublic value
                }

                // Build upload payload
                var boundary = new
                {
                    outer = outer,
                    holes = holes
                };

                var payload = new
                {
                    name = input.FieldName,
                    isPublic = isPublic,
                    origin = new { latitude = input.Origin.Latitude, longitude = input.Origin.Longitude },
                    boundary = boundary,
                    abLines = abLines,
                    convergence = input.Convergence,
                    sourceId = (string)null
                };

                // Upload to AgShare
                var uploadResult = await client.UploadFieldAsync(fieldId, payload);

                // Save field ID to disk if upload succeeded and directory provided
                if (uploadResult.ok && !string.IsNullOrEmpty(fieldDirectory))
                {
                    if (!Directory.Exists(fieldDirectory))
                        Directory.CreateDirectory(fieldDirectory);

                    string txtPath = Path.Combine(fieldDirectory, "agshare.txt");
                    File.WriteAllText(txtPath, fieldId.ToString());
                }

                return (uploadResult.ok, uploadResult.message, fieldId);
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}", Guid.Empty);
            }
        }

        /// <summary>
        /// Convert local NE boundary to WGS84 coordinates
        /// </summary>
        private List<CoordinateDto> ConvertBoundary(List<Vec3> localFence, LocalPlane converter)
        {
            List<CoordinateDto> coords = new List<CoordinateDto>();
            for (int i = 0; i < localFence.Count; i++)
            {
                GeoCoord geo = new GeoCoord(localFence[i].Northing, localFence[i].Easting);
                Wgs84 wgs = converter.ConvertGeoCoordToWgs84(geo);
                coords.Add(new CoordinateDto { Latitude = wgs.Latitude, Longitude = wgs.Longitude });
            }

            // Ensure boundary is closed
            if (coords.Count > 1)
            {
                CoordinateDto first = coords[0];
                CoordinateDto last = coords[coords.Count - 1];
                if (first.Latitude != last.Latitude || first.Longitude != last.Longitude)
                {
                    coords.Add(first);
                }
            }

            return coords;
        }

        /// <summary>
        /// Convert track lines from local NE to WGS84 format
        /// </summary>
        private List<AbLineUploadDto> ConvertAbLines(List<TrackLineInput> tracks, LocalPlane converter)
        {
            List<AbLineUploadDto> result = new List<AbLineUploadDto>();

            foreach (TrackLineInput track in tracks)
            {
                if (track.Mode == TrackMode.AB)
                {
                    GeoCoord a = new GeoCoord(track.PtA.Northing, track.PtA.Easting);
                    GeoCoord b = new GeoCoord(track.PtB.Northing, track.PtB.Easting);
                    Wgs84 wgsA = converter.ConvertGeoCoordToWgs84(a);
                    Wgs84 wgsB = converter.ConvertGeoCoordToWgs84(b);

                    result.Add(new AbLineUploadDto
                    {
                        Name = track.Name,
                        Type = "AB",
                        Coords = new List<CoordinateDto>
                        {
                            new CoordinateDto { Latitude = wgsA.Latitude, Longitude = wgsA.Longitude },
                            new CoordinateDto { Latitude = wgsB.Latitude, Longitude = wgsB.Longitude }
                        }
                    });
                }
                else if (track.Mode == TrackMode.Curve && track.CurvePoints.Count >= 2)
                {
                    List<CoordinateDto> coords = new List<CoordinateDto>();
                    foreach (Vec3 pt in track.CurvePoints)
                    {
                        GeoCoord geo = new GeoCoord(pt.Northing, pt.Easting);
                        Wgs84 wgs = converter.ConvertGeoCoordToWgs84(geo);
                        coords.Add(new CoordinateDto { Latitude = wgs.Latitude, Longitude = wgs.Longitude });
                    }

                    result.Add(new AbLineUploadDto
                    {
                        Name = track.Name,
                        Type = "Curve",
                        Coords = coords
                    });
                }
            }

            return result;
        }
    }
}
