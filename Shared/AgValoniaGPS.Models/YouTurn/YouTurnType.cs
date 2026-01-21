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

namespace AgValoniaGPS.Models.YouTurn
{
    /// <summary>
    /// Type of U-turn pattern to create.
    /// </summary>
    public enum YouTurnType
    {
        /// <summary>
        /// Omega or Wide turn (based on offset width).
        /// Uses Dubins paths for omega, semicircles for wide.
        /// </summary>
        AlbinStyle = 0,

        /// <summary>
        /// K-style turn.
        /// Creates a more squared-off turn pattern.
        /// </summary>
        KStyle = 1
    }

    /// <summary>
    /// Skip mode for determining next guidance line.
    /// </summary>
    public enum SkipMode
    {
        /// <summary>
        /// Normal skip - use configured skip width.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Alternate between different skip widths.
        /// </summary>
        Alternative = 1,

        /// <summary>
        /// Skip worked tracks - find next unworked track.
        /// </summary>
        IgnoreWorkedTracks = 2
    }

    /// <summary>
    /// Guidance line type for U-turn.
    /// </summary>
    public enum GuidanceLineType
    {
        /// <summary>
        /// AB straight line.
        /// </summary>
        ABLine = 0,

        /// <summary>
        /// Curved guidance line.
        /// </summary>
        Curve = 1
    }
}
