using System;
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.AgShare
{
    /// <summary>
    /// Input data for creating a field snapshot for upload to AgShare.
    /// </summary>
    public class FieldSnapshotInput
    {
        /// <summary>
        /// Field name to display
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Field ID (GUID) - will be generated if not exists
        /// </summary>
        public Guid? FieldId { get; set; }

        /// <summary>
        /// Origin point in WGS84 coordinates
        /// </summary>
        public Wgs84 Origin { get; set; }

        /// <summary>
        /// Convergence angle (usually 0)
        /// </summary>
        public double Convergence { get; set; }

        /// <summary>
        /// List of boundaries in local NE coordinates
        /// First boundary is outer, rest are holes
        /// </summary>
        public List<List<Vec3>> Boundaries { get; set; } = new List<List<Vec3>>();

        /// <summary>
        /// List of AB lines/curves in local coordinates
        /// </summary>
        public List<TrackLineInput> Tracks { get; set; } = new List<TrackLineInput>();

        /// <summary>
        /// Whether the field should be public on AgShare
        /// </summary>
        public bool IsPublic { get; set; }
    }

    /// <summary>
    /// Track line (AB line or curve) input for upload
    /// </summary>
    public class TrackLineInput
    {
        /// <summary>
        /// Track name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Track mode (AB or Curve) - uses main TrackMode enum from AgValoniaGPS.Models
        /// </summary>
        public Models.TrackMode Mode { get; set; }

        /// <summary>
        /// Point A (for AB lines)
        /// </summary>
        public Vec3 PtA { get; set; }

        /// <summary>
        /// Point B (for AB lines)
        /// </summary>
        public Vec3 PtB { get; set; }

        /// <summary>
        /// Curve points (for curves)
        /// </summary>
        public List<Vec3> CurvePoints { get; set; } = new List<Vec3>();
    }
}
