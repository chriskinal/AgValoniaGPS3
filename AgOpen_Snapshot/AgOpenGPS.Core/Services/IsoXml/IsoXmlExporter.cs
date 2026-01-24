using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.IsoXml;
using Dev4Agriculture.ISO11783.ISOXML.TaskFile;
using Dev4Agriculture.ISO11783.ISOXML;

namespace AgOpenGPS.Core.Services.IsoXml
{
    /// <summary>
    /// Core service for exporting field data to ISO 11783 XML format.
    /// Supports both Version 3 and Version 4 of the ISO standard.
    /// </summary>
    public class IsoXmlExporter
    {
        public enum IsoXmlVersion { V3, V4 }

        /// <summary>
        /// Export field data to ISO 11783 TaskData XML file
        /// </summary>
        /// <param name="directoryName">Output directory for TASKDATA.XML</param>
        /// <param name="designator">Field name/designator</param>
        /// <param name="area">Field area in square meters</param>
        /// <param name="boundaries">List of boundaries (outer + holes)</param>
        /// <param name="headlandLines">List of headland lines per boundary</param>
        /// <param name="guidanceLines">List of guidance lines (AB lines and curves)</param>
        /// <param name="localPlane">Coordinate conversion plane</param>
        /// <param name="version">ISO XML version (V3 or V4)</param>
        /// <param name="softwareVersion">Software version string</param>
        public static void Export(
            string directoryName,
            string designator,
            int area,
            List<IsoXmlBoundary> boundaries,
            List<List<Vec3>> headlandLines,
            List<IsoXmlTrack> guidanceLines,
            LocalPlane localPlane,
            IsoXmlVersion version,
            string softwareVersion)
        {
            if (!Enum.IsDefined(typeof(IsoXmlVersion), version))
                throw new ArgumentOutOfRangeException(nameof(version), version, "Invalid version");

            var isoxml = ISOXML.Create(directoryName);

            SetFileInformation(isoxml, version, softwareVersion);
            AddPartfield(isoxml, designator, area, boundaries, headlandLines, guidanceLines, localPlane, version);

            isoxml.Save();
        }

        private static void SetFileInformation(ISOXML isoxml, IsoXmlVersion version, string softwareVersion)
        {
            isoxml.DataTransferOrigin = ISO11783TaskDataFileDataTransferOrigin.FMIS;
            isoxml.ManagementSoftwareManufacturer = "AgOpenGPS";
            isoxml.ManagementSoftwareVersion = softwareVersion;

            switch (version)
            {
                case IsoXmlVersion.V3:
                    isoxml.VersionMajor = ISO11783TaskDataFileVersionMajor.Version3;
                    isoxml.VersionMinor = ISO11783TaskDataFileVersionMinor.Item3;
                    break;

                case IsoXmlVersion.V4:
                    isoxml.VersionMajor = ISO11783TaskDataFileVersionMajor.Version4;
                    isoxml.VersionMinor = ISO11783TaskDataFileVersionMinor.Item2;
                    break;
            }
        }

        private static void AddPartfield(
            ISOXML isoxml,
            string designator,
            int area,
            List<IsoXmlBoundary> boundaries,
            List<List<Vec3>> headlandLines,
            List<IsoXmlTrack> guidanceLines,
            LocalPlane localPlane,
            IsoXmlVersion version)
        {
            var partfield = new ISOPartfield();
            isoxml.IdTable.AddObjectAndAssignIdIfNone(partfield);
            partfield.PartfieldDesignator = designator;
            partfield.PartfieldArea = (ulong)area;

            AddBoundaries(partfield, boundaries, localPlane);
            AddHeadlands(partfield, boundaries, headlandLines, localPlane);
            AddTracks(isoxml, partfield, guidanceLines, localPlane, version);

            isoxml.Data.Partfield.Add(partfield);
        }

        private static void AddBoundaries(ISOPartfield partfield, List<IsoXmlBoundary> boundaries, LocalPlane localPlane)
        {
            for (int i = 0; i < boundaries.Count; i++)
            {
                var polygon = new ISOPolygon
                {
                    PolygonType = i == 0 ? ISOPolygonType.PartfieldBoundary : ISOPolygonType.Obstacle
                };

                var lineString = new ISOLineString
                {
                    LineStringType = ISOLineStringType.PolygonExterior
                };

                foreach (Vec3 v3 in boundaries[i].FenceLine)
                {
                    GeoCoord geoCoord = new GeoCoord(v3.Northing, v3.Easting);
                    Wgs84 latLon = localPlane.ConvertGeoCoordToWgs84(geoCoord);
                    lineString.Point.Add(new ISOPoint
                    {
                        PointType = ISOPointType.other,
                        PointNorth = (decimal)latLon.Latitude,
                        PointEast = (decimal)latLon.Longitude
                    });
                }

                polygon.LineString.Add(lineString);
                partfield.PolygonnonTreatmentZoneonly.Add(polygon);
            }
        }

        private static void AddHeadlands(ISOPartfield partfield, List<IsoXmlBoundary> boundaries, List<List<Vec3>> headlandLines, LocalPlane localPlane)
        {
            // Match headland lines to boundaries (assume same count)
            for (int i = 0; i < Math.Min(boundaries.Count, headlandLines.Count); i++)
            {
                if (headlandLines[i].Count < 1) continue;

                var polygon = new ISOPolygon
                {
                    PolygonType = ISOPolygonType.Headland
                };

                var lineString = new ISOLineString
                {
                    LineStringType = ISOLineStringType.PolygonExterior
                };

                foreach (Vec3 v3 in headlandLines[i])
                {
                    GeoCoord geoCoord = new GeoCoord(v3.Northing, v3.Easting);
                    Wgs84 latLon = localPlane.ConvertGeoCoordToWgs84(geoCoord);
                    lineString.Point.Add(new ISOPoint
                    {
                        PointType = ISOPointType.other,
                        PointNorth = (decimal)latLon.Latitude,
                        PointEast = (decimal)latLon.Longitude
                    });
                }

                polygon.LineString.Add(lineString);
                partfield.PolygonnonTreatmentZoneonly.Add(polygon);
            }
        }

        private static void AddTracks(ISOXML isoxml, ISOPartfield partfield, List<IsoXmlTrack> tracks, LocalPlane localPlane, IsoXmlVersion version)
        {
            if (tracks == null) return;

            foreach (IsoXmlTrack track in tracks)
            {
                if (track.Mode != IsoXmlTrackMode.AB && track.Mode != IsoXmlTrackMode.Curve) continue;

                switch (version)
                {
                    case IsoXmlVersion.V3:
                        {
                            ISOLineString lineString = CreateLineString(track, localPlane, version);
                            lineString.LineStringDesignator = track.Name;
                            partfield.LineString.Add(lineString);
                        }
                        break;

                    case IsoXmlVersion.V4:
                        {
                            var guidanceGroup = new ISOGuidanceGroup
                            {
                                GuidanceGroupDesignator = track.Name
                            };
                            isoxml.IdTable.AddObjectAndAssignIdIfNone(guidanceGroup);

                            var guidancePattern = new ISOGuidancePattern
                            {
                                GuidancePatternId = guidanceGroup.GuidanceGroupId,
                                GuidancePatternPropagationDirection = ISOGuidancePatternPropagationDirection.Bothdirections,
                                GuidancePatternExtension = ISOGuidancePatternExtension.Frombothfirstandlastpoint,
                                GuidancePatternGNSSMethod = ISOGuidancePatternGNSSMethod.Desktopgenerateddata
                            };

                            ISOLineString lineString = CreateLineString(track, localPlane, version);

                            switch (track.Mode)
                            {
                                case IsoXmlTrackMode.AB:
                                    guidancePattern.GuidancePatternType = ISOGuidancePatternType.AB;
                                    break;

                                case IsoXmlTrackMode.Curve:
                                    guidancePattern.GuidancePatternType = ISOGuidancePatternType.Curve;
                                    break;

                                default:
                                    throw new InvalidOperationException("Track mode is invalid");
                            }

                            guidancePattern.LineString.Add(lineString);
                            guidanceGroup.GuidancePattern.Add(guidancePattern);
                            partfield.GuidanceGroup.Add(guidanceGroup);
                        }
                        break;
                }
            }
        }

        private static ISOLineString CreateLineString(IsoXmlTrack track, LocalPlane localPlane, IsoXmlVersion version)
        {
            switch (track.Mode)
            {
                case IsoXmlTrackMode.AB:
                    return CreateABLineString(track, localPlane, version);

                case IsoXmlTrackMode.Curve:
                    return CreateCurveLineString(track, localPlane, version);

                default:
                    throw new InvalidOperationException("Track mode is invalid");
            }
        }

        private static ISOLineString CreateABLineString(IsoXmlTrack track, LocalPlane localPlane, IsoXmlVersion version)
        {
            var lineString = new ISOLineString
            {
                LineStringType = ISOLineStringType.GuidancePattern
            };

            GeoCoord pointA = new GeoCoord(track.PtA.Northing, track.PtA.Easting);
            GeoDir heading = new GeoDir(track.Heading);
            Wgs84 latLon = localPlane.ConvertGeoCoordToWgs84(pointA - 1000.0 * heading);

            lineString.Point.Add(new ISOPoint
            {
                PointType = version == IsoXmlVersion.V4 ? ISOPointType.GuidanceReferenceA : ISOPointType.other,
                PointNorth = (decimal)latLon.Latitude,
                PointEast = (decimal)latLon.Longitude
            });

            latLon = localPlane.ConvertGeoCoordToWgs84(pointA + 1000.0 * heading);

            lineString.Point.Add(new ISOPoint
            {
                PointType = version == IsoXmlVersion.V4 ? ISOPointType.GuidanceReferenceB : ISOPointType.other,
                PointNorth = (decimal)latLon.Latitude,
                PointEast = (decimal)latLon.Longitude
            });

            return lineString;
        }

        private static ISOLineString CreateCurveLineString(IsoXmlTrack track, LocalPlane localPlane, IsoXmlVersion version)
        {
            var lineString = new ISOLineString
            {
                LineStringType = ISOLineStringType.GuidancePattern
            };

            for (int j = 0; j < track.CurvePoints.Count; j++)
            {
                Vec3 v3 = track.CurvePoints[j];
                GeoCoord geoCoord = new GeoCoord(v3.Northing, v3.Easting);
                Wgs84 latLon = localPlane.ConvertGeoCoordToWgs84(geoCoord);

                var point = new ISOPoint
                {
                    PointNorth = (decimal)latLon.Latitude,
                    PointEast = (decimal)latLon.Longitude
                };

                if (version == IsoXmlVersion.V4)
                {
                    if (j == 0)
                    {
                        point.PointType = ISOPointType.GuidanceReferenceA;
                    }
                    else if (j == track.CurvePoints.Count - 1)
                    {
                        point.PointType = ISOPointType.GuidanceReferenceB;
                    }
                    else
                    {
                        point.PointType = ISOPointType.GuidancePoint;
                    }
                }
                else
                {
                    point.PointType = ISOPointType.other;
                }

                lineString.Point.Add(point);
            }

            return lineString;
        }
    }
}
