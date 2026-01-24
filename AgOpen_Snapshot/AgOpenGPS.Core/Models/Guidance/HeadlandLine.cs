using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Models.Guidance
{
    /// <summary>
    /// Headland guidance line data model containing multiple tracked paths
    /// </summary>
    public class HeadlandLine
    {
        /// <summary>
        /// Collection of headland paths/tracks
        /// </summary>
        public List<HeadlandPath> Tracks { get; set; } = new List<HeadlandPath>();

        /// <summary>
        /// Current track index
        /// </summary>
        public int CurrentIndex { get; set; }

        /// <summary>
        /// Desired line points for display/averaging
        /// </summary>
        public List<Vec3> DesiredPoints { get; set; } = new List<Vec3>();

        public HeadlandLine()
        {
        }
    }

    /// <summary>
    /// Individual headland path/track data
    /// </summary>
    public class HeadlandPath
    {
        /// <summary>
        /// Track points (position + heading)
        /// </summary>
        public List<Vec3> TrackPoints { get; set; } = new List<Vec3>();

        /// <summary>
        /// Name/identifier for this path
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Movement distance for this path
        /// </summary>
        public double MoveDistance { get; set; }

        /// <summary>
        /// Operating mode for this path
        /// </summary>
        public int Mode { get; set; }

        /// <summary>
        /// A-point reference index
        /// </summary>
        public int APointIndex { get; set; }

        public HeadlandPath()
        {
        }
    }
}
