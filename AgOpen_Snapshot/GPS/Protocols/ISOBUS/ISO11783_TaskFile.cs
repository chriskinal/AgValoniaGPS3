using System.Collections.Generic;
using System.Linq;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.IsoXml;
using AgOpenGPS.Core.Services.IsoXml;
using System;

namespace AgOpenGPS.Protocols.ISOBUS
{
    /// <summary>
    /// WinForms adapter for exporting field data to ISO 11783 XML format.
    /// Converts WinForms types to Core types and delegates to Core IsoXmlExporter.
    /// </summary>
    public class ISO11783_TaskFile
    {
        public enum Version { V3, V4 }

        public static void Export(
            string directoryName,
            string designator,
            int area,
            List<CBoundaryList> bndList,
            LocalPlane localPlane,
            CTrack trk,
            Version version)
        {
            if (!Enum.IsDefined(typeof(Version), version))
                throw new ArgumentOutOfRangeException(nameof(version), version, "Invalid version");

            // Convert WinForms types to Core types
            var coreBoundaries = ConvertBoundaries(bndList);
            var coreHeadlandLines = ConvertHeadlandLines(bndList);
            var coreGuidanceLines = ConvertGuidanceLines(trk);
            var coreVersion = version == Version.V3 ? IsoXmlExporter.IsoXmlVersion.V3 : IsoXmlExporter.IsoXmlVersion.V4;

            // Delegate to Core exporter
            IsoXmlExporter.Export(
                directoryName,
                designator,
                area,
                coreBoundaries,
                coreHeadlandLines,
                coreGuidanceLines,
                localPlane,
                coreVersion,
                Program.Version
            );
        }

        // Convert WinForms boundaries to Core boundaries
        private static List<IsoXmlBoundary> ConvertBoundaries(List<CBoundaryList> bndList)
        {
            var coreBoundaries = new List<IsoXmlBoundary>();

            foreach (var bnd in bndList)
            {
                var coreBoundary = new IsoXmlBoundary
                {
                    Area = bnd.area,
                    IsDriveThru = bnd.isDriveThru
                };

                // Use fenceLineEar if available (vec2), otherwise use fenceLine (vec3)
                if (bnd.fenceLineEar.Count > 0)
                {
                    foreach (vec2 v2 in bnd.fenceLineEar)
                    {
                        coreBoundary.FenceLine.Add(new Vec3(v2.northing, v2.easting, 0));
                    }
                }
                else
                {
                    foreach (vec3 v3 in bnd.fenceLine)
                    {
                        coreBoundary.FenceLine.Add(v3);  // Implicit conversion
                    }
                }

                coreBoundaries.Add(coreBoundary);
            }

            return coreBoundaries;
        }

        // Convert WinForms headland lines to Core headland lines
        private static List<List<Vec3>> ConvertHeadlandLines(List<CBoundaryList> bndList)
        {
            var coreHeadlandLines = new List<List<Vec3>>();

            foreach (var bnd in bndList)
            {
                var coreHeadland = new List<Vec3>();
                foreach (var v3 in bnd.hdLine)
                {
                    coreHeadland.Add(v3);  // Implicit conversion from vec3 to Vec3
                }
                coreHeadlandLines.Add(coreHeadland);
            }

            return coreHeadlandLines;
        }

        // Convert WinForms guidance lines to Core guidance lines
        private static List<IsoXmlTrack> ConvertGuidanceLines(CTrack trk)
        {
            var coreGuidanceLines = new List<IsoXmlTrack>();

            if (trk?.gArr == null) return coreGuidanceLines;

            foreach (var track in trk.gArr)
            {
                if (track.mode != TrackMode.AB && track.mode != TrackMode.Curve) continue;

                var coreTrack = new IsoXmlTrack
                {
                    Name = track.name,
                    Heading = track.heading,
                    Mode = (IsoXmlTrackMode)(int)track.mode,  // Enum cast
                    IsVisible = track.isVisible,
                    NudgeDistance = track.nudgeDistance,
                    PtA = track.ptA,  // Implicit conversion from vec2 to Vec2
                    PtB = track.ptB   // Implicit conversion from vec2 to Vec2
                };

                // Convert curve points
                foreach (var pt in track.curvePts)
                {
                    coreTrack.CurvePoints.Add(pt);  // Implicit conversion from vec3 to Vec3
                }

                coreGuidanceLines.Add(coreTrack);
            }

            return coreGuidanceLines;
        }
    }
}
