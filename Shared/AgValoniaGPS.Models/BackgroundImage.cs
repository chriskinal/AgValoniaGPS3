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

namespace AgValoniaGPS.Models;

/// <summary>
/// Represents a georeferenced background image (satellite/aerial photo)
/// Metadata from BackPic.Txt, actual image in BackPic.png
/// </summary>
public class BackgroundImage
{
    /// <summary>
    /// Whether the background image is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Maximum easting (right edge) in meters
    /// </summary>
    public double MaxEasting { get; set; }

    /// <summary>
    /// Minimum easting (left edge) in meters
    /// </summary>
    public double MinEasting { get; set; }

    /// <summary>
    /// Maximum northing (top edge) in meters
    /// </summary>
    public double MaxNorthing { get; set; }

    /// <summary>
    /// Minimum northing (bottom edge) in meters
    /// </summary>
    public double MinNorthing { get; set; }

    /// <summary>
    /// Width in meters
    /// </summary>
    public double Width => MaxEasting - MinEasting;

    /// <summary>
    /// Height in meters
    /// </summary>
    public double Height => MaxNorthing - MinNorthing;

    /// <summary>
    /// Path to the image file (BackPic.png)
    /// </summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Check if bounds are valid
    /// </summary>
    public bool IsValid => Width > 0 && Height > 0;
}
