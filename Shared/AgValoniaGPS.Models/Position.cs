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
/// Represents a geographic position with latitude, longitude, and altitude
/// </summary>
public record Position
{
    /// <summary>
    /// Latitude in decimal degrees
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Longitude in decimal degrees
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Altitude in meters above sea level
    /// </summary>
    public double Altitude { get; init; }

    /// <summary>
    /// UTM (Universal Transverse Mercator) Easting coordinate
    /// </summary>
    public double Easting { get; init; }

    /// <summary>
    /// UTM (Universal Transverse Mercator) Northing coordinate
    /// </summary>
    public double Northing { get; init; }

    /// <summary>
    /// UTM Zone number
    /// </summary>
    public int Zone { get; init; }

    /// <summary>
    /// UTM Hemisphere letter
    /// </summary>
    public char Hemisphere { get; init; }

    /// <summary>
    /// Heading in degrees (0-360)
    /// </summary>
    public double Heading { get; init; }

    /// <summary>
    /// Speed in meters per second
    /// </summary>
    public double Speed { get; init; }
}