using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.IsoXml
{
    /// <summary>
    /// Track/guidance line mode types from ISO XML
    /// </summary>
    public enum IsoXmlTrackMode
    {
        None = 0,
        AB = 2,
        Curve = 4
    }

    /// <summary>
    /// Parsed boundary data from ISO XML file
    /// </summary>
    public class IsoXmlBoundary
    {
        /// <summary>
        /// List of boundary coordinates in local coordinate system (NE meters)
        /// </summary>
        public List<Vec3> FenceLine { get; set; } = new List<Vec3>(128);

        /// <summary>
        /// Area of boundary in square meters (calculated separately)
        /// </summary>
        public double Area { get; set; } = 0;

        /// <summary>
        /// Whether this is a drive-through boundary
        /// </summary>
        public bool IsDriveThru { get; set; } = false;
    }

    /// <summary>
    /// Parsed track/guidance line data from ISO XML file
    /// </summary>
    public class IsoXmlTrack
    {
        /// <summary>
        /// Track name
        /// </summary>
        public string Name { get; set; } = "New Track";

        /// <summary>
        /// Track mode (AB line, Curve, etc.)
        /// </summary>
        public IsoXmlTrackMode Mode { get; set; } = IsoXmlTrackMode.None;

        /// <summary>
        /// Track heading in radians
        /// </summary>
        public double Heading { get; set; } = 3;

        /// <summary>
        /// Point A (for AB lines) in local coordinates
        /// </summary>
        public Vec2 PtA { get; set; }

        /// <summary>
        /// Point B (for AB lines) in local coordinates
        /// </summary>
        public Vec2 PtB { get; set; }

        /// <summary>
        /// Curve points (for curve guidance) in local coordinates
        /// </summary>
        public List<Vec3> CurvePoints { get; set; } = new List<Vec3>();

        /// <summary>
        /// Visibility flag
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Nudge distance (lateral offset)
        /// </summary>
        public double NudgeDistance { get; set; } = 0;
    }

    /// <summary>
    /// Complete parsed field data from ISO XML
    /// </summary>
    public class IsoXmlField
    {
        /// <summary>
        /// Field origin in WGS84 coordinates
        /// </summary>
        public Wgs84 Origin { get; set; }

        /// <summary>
        /// Parsed boundaries (outer and holes)
        /// </summary>
        public List<IsoXmlBoundary> Boundaries { get; set; } = new List<IsoXmlBoundary>();

        /// <summary>
        /// Parsed headland line
        /// </summary>
        public List<Vec3> HeadlandLine { get; set; } = new List<Vec3>();

        /// <summary>
        /// Parsed guidance lines (AB lines and curves)
        /// </summary>
        public List<IsoXmlTrack> GuidanceLines { get; set; } = new List<IsoXmlTrack>();
    }
}
