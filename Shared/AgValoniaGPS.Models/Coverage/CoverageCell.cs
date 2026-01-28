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

namespace AgValoniaGPS.Models.Coverage;

/// <summary>
/// Represents a single cell in the visual coverage grid.
/// Used for fast rendering - each cell is a simple rectangle.
/// </summary>
public readonly struct CoverageCell
{
    /// <summary>
    /// World X coordinate of cell center (easting)
    /// </summary>
    public double CenterX { get; init; }

    /// <summary>
    /// World Y coordinate of cell center (northing)
    /// </summary>
    public double CenterY { get; init; }

    /// <summary>
    /// Section index (0-15) that painted this cell, or 255 for default color
    /// </summary>
    public byte SectionIndex { get; init; }

    /// <summary>
    /// Color of this cell (RGB packed into Vec3 format like CoverageColor)
    /// </summary>
    public CoverageColor Color { get; init; }

    public CoverageCell(double centerX, double centerY, byte sectionIndex, CoverageColor color)
    {
        CenterX = centerX;
        CenterY = centerY;
        SectionIndex = sectionIndex;
        Color = color;
    }
}

/// <summary>
/// Parameters for querying visible coverage cells.
/// </summary>
public readonly struct CoverageViewport
{
    public double MinX { get; init; }
    public double MaxX { get; init; }
    public double MinY { get; init; }
    public double MaxY { get; init; }

    public CoverageViewport(double minX, double maxX, double minY, double maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }
}
