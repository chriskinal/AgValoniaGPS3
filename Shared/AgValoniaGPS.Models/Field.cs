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

using System;
using System.Collections.Generic;

namespace AgValoniaGPS.Models;

/// <summary>
/// Represents an agricultural field with boundaries, AB lines, and metadata
/// Matches AgOpenGPS Field.txt format
/// </summary>
public class Field
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field directory path (full path to field folder)
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Field origin point (WGS84 coordinates)
    /// All local coordinates are relative to this point
    /// </summary>
    public Position Origin { get; set; } = new Position();

    /// <summary>
    /// Convergence angle in degrees
    /// </summary>
    public double Convergence { get; set; }

    /// <summary>
    /// X offset in meters
    /// </summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// Y offset in meters
    /// </summary>
    public double OffsetY { get; set; }

    /// <summary>
    /// Field boundary (outer + inner boundaries)
    /// </summary>
    public Boundary? Boundary { get; set; }

    /// <summary>
    /// Background image (satellite photo)
    /// </summary>
    public BackgroundImage? BackgroundImage { get; set; }

    public List<ABLine> ABLines { get; set; } = new();

    /// <summary>
    /// Total area in hectares (calculated from boundary)
    /// </summary>
    public double TotalArea => Boundary?.AreaHectares ?? 0;

    /// <summary>
    /// Area worked in hectares
    /// </summary>
    public double WorkedArea { get; set; }

    /// <summary>
    /// Date when field was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Date when field was last modified
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Center position of the field (calculated from boundaries)
    /// </summary>
    public Position? CenterPosition { get; set; }
}