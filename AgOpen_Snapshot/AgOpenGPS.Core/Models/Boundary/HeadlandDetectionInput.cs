using AgOpenGPS.Core.Models.Base;
using System.Collections.Generic;

namespace AgOpenGPS.Core.Models.Headland
{
    /// <summary>
    /// Input data for headland detection operations.
    /// </summary>
    public class HeadlandDetectionInput
    {
        /// <summary>
        /// List of boundaries with headland lines.
        /// First boundary is the outer boundary, rest are holes.
        /// </summary>
        public List<BoundaryData> Boundaries { get; set; } = new List<BoundaryData>();

        /// <summary>
        /// Vehicle/tool pivot position for proximity calculations.
        /// </summary>
        public Vec3 VehiclePosition { get; set; }

        /// <summary>
        /// Section corner points for detection.
        /// </summary>
        public List<SectionCornerData> Sections { get; set; } = new List<SectionCornerData>();

        /// <summary>
        /// Look-ahead distance configuration for section control.
        /// </summary>
        public LookAheadConfig LookAhead { get; set; } = new LookAheadConfig();

        /// <summary>
        /// Whether headland is active.
        /// </summary>
        public bool IsHeadlandOn { get; set; }
    }

    /// <summary>
    /// Boundary data with headland line.
    /// </summary>
    public class BoundaryData
    {
        /// <summary>
        /// Headland line points.
        /// </summary>
        public List<Vec3> HeadlandLine { get; set; } = new List<Vec3>();

        /// <summary>
        /// Whether this is a drive-through boundary (hole).
        /// </summary>
        public bool IsDriveThru { get; set; }
    }

    /// <summary>
    /// Section corner point data.
    /// </summary>
    public class SectionCornerData
    {
        /// <summary>
        /// Left corner point of section.
        /// </summary>
        public Vec2 LeftPoint { get; set; }

        /// <summary>
        /// Right corner point of section.
        /// </summary>
        public Vec2 RightPoint { get; set; }

        /// <summary>
        /// Section width in meters.
        /// </summary>
        public double SectionWidth { get; set; }
    }

    /// <summary>
    /// Look-ahead configuration for headland detection.
    /// </summary>
    public class LookAheadConfig
    {
        /// <summary>
        /// Look-ahead distance on left side in pixels.
        /// </summary>
        public double LookAheadDistanceOnPixelsLeft { get; set; }

        /// <summary>
        /// Look-ahead distance on right side in pixels.
        /// </summary>
        public double LookAheadDistanceOnPixelsRight { get; set; }

        /// <summary>
        /// Total tool width in meters.
        /// </summary>
        public double TotalWidth { get; set; }
    }
}
