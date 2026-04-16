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
/// Track mode matching WinForms TrackMode enum for file compatibility.
/// Integer values must be preserved for file format backwards compatibility.
/// </summary>
public enum TrackMode
{
    None = 0,
    AB = 2,
    Curve = 4,
    BndTrackOuter = 8,
    BndTrackInner = 16,
    BndCurve = 32,
    WaterPivot = 64,
    RecordedPath = 128,
    Contour = 256
}
