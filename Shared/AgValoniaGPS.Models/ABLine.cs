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

namespace AgValoniaGPS.Models;

/// <summary>
/// Mode for creating AB lines/curves
/// </summary>
public enum ABCreationMode
{
    None,           // Not creating an AB line
    DriveAB,        // Drive from A to B - uses current position when tapping
    DrawAB,         // Draw on map - tap to place 2 points for straight line
    APlusLine,      // Create from current position + heading
    Curve,          // Record curve while driving
    DrawCurve       // Draw on map - tap to place multiple points for curve
}

/// <summary>
/// Track mode matching WinForms TrackMode enum for file compatibility
/// </summary>
public enum TrackMode
{
    None = 0,
    AB = 2,
    Curve = 4,
    BndTrackOuter = 8,
    BndTrackInner = 16,
    BndCurve = 32,
    WaterPivot = 64
}

/// <summary>
/// Which point is being set in AB creation
/// </summary>
public enum ABPointStep
{
    None,
    SettingPointA,
    SettingPointB
}

/// <summary>
/// Represents an AB guidance line for field operations.
/// Compatible with AgOpenGPS TrackLines.txt format.
/// </summary>
[System.Obsolete("Use AgValoniaGPS.Models.Track.Track instead. ABLine is retained for file I/O compatibility only.")]
public class ABLine
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Starting point of the AB line (Point A) - Easting/Northing coordinates
    /// </summary>
    public Position PointA { get; set; } = new();

    /// <summary>
    /// Ending point of the AB line (Point B) - Easting/Northing coordinates
    /// </summary>
    public Position PointB { get; set; } = new();

    /// <summary>
    /// Heading angle in degrees (internal use).
    /// Note: File format stores in radians.
    /// </summary>
    public double Heading { get; set; }

    /// <summary>
    /// Track mode (AB line, Curve, boundary track, etc.)
    /// Replaces the IsCurve boolean for WinForms compatibility.
    /// </summary>
    public TrackMode Mode { get; set; } = TrackMode.AB;

    /// <summary>
    /// Whether this is a curve line (as opposed to straight).
    /// Derived from Mode for backwards compatibility.
    /// </summary>
    public bool IsCurve => Mode == TrackMode.Curve || Mode == TrackMode.BndCurve;

    /// <summary>
    /// Whether this AB line is currently active for guidance
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether this track is visible on the map.
    /// Used by WinForms TrackLines.txt format.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Nudge offset distance in meters.
    /// Used for shifting the track left/right.
    /// </summary>
    public double NudgeDistance { get; set; }

    /// <summary>
    /// Curve points for curve-type tracks.
    /// Each point contains Easting, Northing, and Heading (in radians).
    /// </summary>
    public List<Vec3> CurvePoints { get; set; } = new();
}