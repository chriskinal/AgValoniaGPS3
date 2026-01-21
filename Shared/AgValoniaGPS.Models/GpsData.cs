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

namespace AgValoniaGPS.Models;

/// <summary>
/// Represents GPS data received from receiver
/// </summary>
public class GpsData
{
    public Position CurrentPosition { get; set; } = new();

    /// <summary>
    /// GPS fix quality (0=invalid, 1=GPS fix, 2=DGPS fix, 4=RTK fixed, 5=RTK float)
    /// </summary>
    public int FixQuality { get; set; }

    /// <summary>
    /// Number of satellites in use
    /// </summary>
    public int SatellitesInUse { get; set; }

    /// <summary>
    /// Horizontal dilution of precision
    /// </summary>
    public double Hdop { get; set; }

    /// <summary>
    /// Age of differential corrections in seconds
    /// </summary>
    public double DifferentialAge { get; set; }

    /// <summary>
    /// Timestamp when data was received
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Whether GPS data is currently valid (can be overridden by parser for quality filtering)
    /// </summary>
    private bool? _isValidOverride;
    public bool IsValid
    {
        get => _isValidOverride ?? (FixQuality > 0 && SatellitesInUse >= 4);
        set => _isValidOverride = value;
    }
}