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

using AgValoniaGPS.Models;

namespace AgValoniaGPS.iOS.Controls;

/// <summary>
/// Interface for platform-specific map control on iOS.
/// This is a stub interface - full implementation pending.
/// </summary>
public interface IMapControl
{
    void Toggle3DMode();
    void Set3DMode(bool is3D);
    bool Is3DMode { get; }
    void SetPitch(double deltaRadians);
    void Pan(double deltaX, double deltaY);
    void PanTo(double x, double y);
    void Zoom(double factor);
    void SetBoundary(Boundary? boundary);
    void SetVehiclePosition(double easting, double northing, double headingRadians);
    bool IsGridVisible { get; set; }
}
