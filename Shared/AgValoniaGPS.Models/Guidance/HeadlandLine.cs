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

using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Guidance
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
