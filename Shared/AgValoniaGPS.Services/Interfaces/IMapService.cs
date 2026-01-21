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
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service interface for map rendering and camera control.
/// Platform-specific implementations wrap the actual map control.
/// No Avalonia dependencies - uses primitives only.
/// </summary>
public interface IMapService
{
    // Camera/View control
    void Toggle3DMode();
    void Set3DMode(bool is3D);
    bool Is3DMode { get; }

    void SetPitch(double deltaRadians);
    void SetPitchAbsolute(double pitchRadians);
    double Pitch { get; }

    void Pan(double deltaX, double deltaY);
    void PanTo(double x, double y);

    void Zoom(double factor);
    double ZoomLevel { get; }

    void Rotate(double deltaRadians);
    void SetRotation(double radians);
    double Rotation { get; }

    void SetCamera(double x, double y, double zoom, double rotation);

    // Mouse/touch interaction (position in screen coordinates)
    void StartPan(double x, double y);
    void StartRotate(double x, double y);
    void UpdatePointer(double x, double y);
    void EndInteraction();

    // Content
    void SetBoundary(Boundary? boundary);
    void SetVehiclePosition(double easting, double northing, double headingRadians);

    // Grid
    bool IsGridVisible { get; set; }

    // Boundary recording visualization
    void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points);
    void ClearRecordingPoints();
    void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0);

    // Background imagery
    void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY);
    void ClearBackground();

    // Headland visualization
    void SetHeadlandLine(IReadOnlyList<Vec3>? headlandPoints);
    void SetHeadlandPreview(IReadOnlyList<Vec2>? previewPoints);
    void SetHeadlandVisible(bool visible);

    // YouTurn path visualization
    void SetYouTurnPath(IReadOnlyList<(double Easting, double Northing)>? turnPath);

    // Track visualization for U-turns
    void SetNextTrack(AgValoniaGPS.Models.Track.Track? track);
    void SetIsInYouTurn(bool isInTurn);

    // Active Track for guidance
    void SetActiveTrack(AgValoniaGPS.Models.Track.Track? track);
}
