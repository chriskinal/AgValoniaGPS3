using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.IsoXml;
using AgOpenGPS.Core.Models.Guidance;

namespace AgOpenGPS.Core.Services.IsoXml
{
    /// <summary>
    /// Core service for parsing ISO XML data related to agricultural fields.
    /// All coordinate conversion and extraction of boundaries, headlands, and guidance lines.
    /// </summary>
    public static class IsoXmlParserHelpers
    {
        // Extract WGS84 origin from PLN or GGP lines
        public static bool TryExtractOrigin(XmlNodeList fieldParts, out Wgs84 origin)
        {
            double latSum = 0, lonSum = 0;
            int count = 0;

            foreach (XmlNode node in fieldParts)
            {
                if (node.Name == "PLN" && node.Attributes["A"]?.Value == "1")
                {
                    AccumulateCoordinates(node, ref latSum, ref lonSum, ref count);
                }
            }

            if (count == 0)
            {
                foreach (XmlNode node in fieldParts)
                {
                    if (node.Name == "GGP")
                    {
                        var lsg = node.SelectSingleNode("GPN/LSG");
                        if (lsg != null) AccumulateCoordinates(lsg.ParentNode, ref latSum, ref lonSum, ref count);
                    }
                }
            }

            if (count == 0)
            {
                origin = new Wgs84();
                return false;
            }

            origin = new Wgs84(latSum / count, lonSum / count);
            return true;
        }

        // Parse PLN boundaries into IsoXmlBoundary objects
        public static List<IsoXmlBoundary> ParseBoundaries(XmlNodeList fieldParts, LocalPlane localPlane)
        {
            List<IsoXmlBoundary> boundaries = new List<IsoXmlBoundary>();
            bool outerBuilt = false;

            foreach (XmlNode node in fieldParts)
            {
                if (node.Name != "PLN") continue;
                string type = node.Attributes["A"]?.Value;

                if ((type == "1" || type == "9") && !outerBuilt)
                {
                    if (node.SelectSingleNode("LSG[@A='1']") is XmlNode lsg)
                    {
                        boundaries.Add(ParseBoundaryFromLSG(lsg, localPlane));
                        outerBuilt = true;
                    }
                }
                else if (type == "3" || type == "4" || type == "6")
                {
                    if (node.SelectSingleNode("LSG[@A='1']") is XmlNode lsg)
                    {
                        boundaries.Add(ParseBoundaryFromLSG(lsg, localPlane));
                    }
                }
            }

            return boundaries;
        }

        // Parse Headland if available
        public static List<Vec3> ParseHeadland(XmlNodeList fieldParts, LocalPlane localPlane)
        {
            foreach (XmlNode node in fieldParts)
            {
                if (node.Name == "PLN" && node.Attributes["A"]?.Value == "10")
                {
                    if (node.SelectSingleNode("LSG[@A='1']") is XmlNode lsg)
                    {
                        var list = new List<Vec3>();
                        foreach (XmlNode pnt in lsg.SelectNodes("PNT"))
                        {
                            if (double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                                double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                            {
                                GeoCoord geo = localPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
                                list.Add(new Vec3(geo.Northing, geo.Easting, 0));
                            }
                        }

                        CalculateHeadings(list);
                        return list;
                    }
                }
            }

            return new List<Vec3>();
        }

        // Parse all valid guidance lines
        public static List<IsoXmlTrack> ParseAllGuidanceLines(XmlNodeList fieldParts, LocalPlane localPlane)
        {
            List<IsoXmlTrack> tracks = new List<IsoXmlTrack>();

            foreach (XmlNode node in fieldParts)
            {
                if (node.Name == "GGP")
                {
                    var trk = ParseGGPNode(node, localPlane);
                    if (trk != null) tracks.Add(trk);
                }
                else if (node.Name == "LSG" && node.Attributes["A"]?.Value == "5")
                {
                    var trk = ParseLSGNode(node, localPlane);
                    if (trk != null) tracks.Add(trk);
                }
            }

            return tracks;
        }

        public static IsoXmlBoundary ParseBoundaryFromLSG(XmlNode lsg, LocalPlane localPlane)
        {
            var boundary = new IsoXmlBoundary();

            foreach (XmlNode pnt in lsg.SelectNodes("PNT"))
            {
                if (double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                {
                    GeoCoord geo = localPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
                    boundary.FenceLine.Add(new Vec3(geo.Northing, geo.Easting, 0));
                }
            }

            return boundary;
        }

        // Parse GGP → GPN → LSG line
        private static IsoXmlTrack ParseGGPNode(XmlNode node, LocalPlane localPlane)
        {
            var gpn = node.SelectSingleNode("GPN");
            var lsg = gpn?.SelectSingleNode("LSG[@A='5']");
            if (gpn == null || lsg == null) return null;

            string lineType = gpn.Attributes["C"]?.Value;
            string name = gpn.Attributes["B"]?.Value ?? node.Attributes["B"]?.Value ?? "Unnamed";

            if (lineType == "1") return ParseABLine(lsg, name, localPlane);
            else if (lineType == "3") return ParseCurveLine(lsg, name, localPlane);

            return null;
        }

        // Parse LSG line directly
        private static IsoXmlTrack ParseLSGNode(XmlNode lsg, LocalPlane localPlane)
        {
            string name = lsg.Attributes["B"]?.Value ?? "Unnamed";
            int count = lsg.SelectNodes("PNT").Count;

            if (count == 2) return ParseABLine(lsg, name, localPlane);
            else if (count > 2) return ParseCurveLine(lsg, name, localPlane);

            return null;
        }

        // Parse AB line into IsoXmlTrack
        private static IsoXmlTrack ParseABLine(XmlNode lsg, string name, LocalPlane localPlane)
        {
            var points = lsg.SelectNodes("PNT");
            if (points.Count < 2) return null;

            var ptA = ParseVec2(points[0], localPlane);
            var ptB = ParseVec2(points[1], localPlane);

            double heading = Math.Atan2(ptB.Easting - ptA.Easting, ptB.Northing - ptA.Northing);
            if (heading < 0) heading += Math.PI * 2;

            return new IsoXmlTrack
            {
                Heading = heading,
                Mode = IsoXmlTrackMode.AB,
                PtA = ptA,
                PtB = ptB,
                Name = name.Trim()
            };
        }

        // Parse Curve line into IsoXmlTrack
        private static IsoXmlTrack ParseCurveLine(XmlNode lsg, string name, LocalPlane localPlane)
        {
            var points = lsg.SelectNodes("PNT");
            if (points == null || points.Count <= 2) return null;

            // Build raw list from ISOXML
            var desList = new List<Vec3>();
            foreach (XmlNode pnt in points)
            {
                var geo = ParseGeoCoord(pnt, localPlane);
                desList.Add(new Vec3(geo.Northing, geo.Easting, 0));
            }

            // Keep originals for ptA/ptB
            var originalFirst = desList[0];
            var originalLast = desList[desList.Count - 1];

            // Extend ends before preprocessing
            double extendMeters = 100.0;
            bool keepOriginalAB = false;
            desList = ExtendEnds(desList, extendMeters);

            // Preprocess curve (smooth and simplify)
            desList = CurveProcessing.Preprocess(desList, 1.6, 0.5);
            if (desList == null || desList.Count < 2) return null;

            double avgHeading = ComputeAverageHeading(desList);

            // Decide ptA/ptB (extended vs original)
            Vec2 ptA, ptB;
            if (keepOriginalAB)
            {
                ptA = new Vec2(originalFirst.Easting, originalFirst.Northing);
                ptB = new Vec2(originalLast.Easting, originalLast.Northing);
            }
            else
            {
                ptA = new Vec2(desList[0].Easting, desList[0].Northing);
                ptB = new Vec2(desList[desList.Count - 1].Easting, desList[desList.Count - 1].Northing);
            }

            // Build track
            var track = new IsoXmlTrack
            {
                Heading = avgHeading,
                Mode = IsoXmlTrackMode.Curve,
                PtA = ptA,
                PtB = ptB,
                Name = string.IsNullOrWhiteSpace(name) ? "Curve_" + DateTime.Now.ToString("HHmmss") : name
            };

            // Copy processed curve points and calculate headings
            track.CurvePoints = new List<Vec3>(desList);
            CalculateHeadings(track.CurvePoints);

            return track;
        }

        // Helper methods

        private static void AccumulateCoordinates(XmlNode parent, ref double latSum, ref double lonSum, ref int count)
        {
            foreach (XmlNode pnt in parent.SelectNodes(".//PNT"))
            {
                if (double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                {
                    latSum += lat;
                    lonSum += lon;
                    count++;
                }
            }
        }

        private static Vec2 ParseVec2(XmlNode pnt, LocalPlane localPlane)
        {
            double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat);
            double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon);
            GeoCoord geo = localPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
            return new Vec2(geo.Easting, geo.Northing);
        }

        private static GeoCoord ParseGeoCoord(XmlNode pnt, LocalPlane localPlane)
        {
            double.TryParse(pnt.Attributes["C"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat);
            double.TryParse(pnt.Attributes["D"]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon);
            return localPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon));
        }

        private static List<Vec3> ExtendEnds(List<Vec3> pts, double extendMeters)
        {
            if (pts == null || pts.Count < 2 || extendMeters <= 0) return pts;

            var list = new List<Vec3>(pts);

            // Extend before the first point (backwards along first segment)
            var first = list[0];
            var second = list[1];
            double dxF = first.Easting - second.Easting;
            double dyF = first.Northing - second.Northing;
            double lenF = Math.Sqrt(dxF * dxF + dyF * dyF);
            if (lenF > 1e-6)
            {
                dxF /= lenF; dyF /= lenF;
                list.Insert(0, new Vec3(
                    first.Northing + dyF * extendMeters,
                    first.Easting + dxF * extendMeters,
                    0
                ));
            }

            // Extend after the last point (forwards along last segment)
            var last = list[list.Count - 1];
            var beforeLast = list[list.Count - 2];
            double dxL = last.Easting - beforeLast.Easting;
            double dyL = last.Northing - beforeLast.Northing;
            double lenL = Math.Sqrt(dxL * dxL + dyL * dyL);
            if (lenL > 1e-6)
            {
                dxL /= lenL; dyL /= lenL;
                list.Add(new Vec3(
                    last.Northing + dyL * extendMeters,
                    last.Easting + dxL * extendMeters,
                    0
                ));
            }

            return list;
        }

        private static double ComputeAverageHeading(List<Vec3> pts)
        {
            if (pts == null || pts.Count < 2) return 0;

            double sumX = 0, sumY = 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                double dx = pts[i + 1].Easting - pts[i].Easting;
                double dy = pts[i + 1].Northing - pts[i].Northing;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 1e-6)
                {
                    sumX += dx / len;
                    sumY += dy / len;
                }
            }

            return Math.Atan2(sumX, sumY);
        }

        private static void CalculateHeadings(List<Vec3> list)
        {
            if (list == null || list.Count < 2) return;

            for (int i = 0; i < list.Count - 1; i++)
            {
                double heading = Math.Atan2(list[i + 1].Easting - list[i].Easting, list[i + 1].Northing - list[i].Northing);
                if (heading < 0) heading += Math.PI * 2;
                list[i] = new Vec3(list[i].Northing, list[i].Easting, heading);
            }

            // Last point gets same heading as second-to-last
            if (list.Count >= 2)
            {
                list[list.Count - 1] = new Vec3(
                    list[list.Count - 1].Northing,
                    list[list.Count - 1].Easting,
                    list[list.Count - 2].Heading
                );
            }
        }
    }
}
