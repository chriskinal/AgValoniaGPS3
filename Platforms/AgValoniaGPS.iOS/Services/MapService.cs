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
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Views.Controls;

namespace AgValoniaGPS.iOS.Services;

/// <summary>
/// iOS-specific map service - delegates to DrawingContextMapControl
/// </summary>
public class MapService : IMapService
{
    private ISharedMapControl? _mapControl;
    private bool _is3DMode;
    private double _pitch;
    private double _zoomLevel = 1.0;
    private double _rotation;
    private bool _isGridVisible;

    /// <summary>
    /// Register the map control to receive service calls
    /// </summary>
    public void RegisterMapControl(ISharedMapControl mapControl)
    {
        _mapControl = mapControl;
    }

    public bool Is3DMode => _mapControl?.Is3DMode ?? _is3DMode;
    public double Pitch => _pitch;
    public double ZoomLevel => _mapControl?.GetZoom() ?? _zoomLevel;
    public double Rotation => _rotation;
    public bool IsGridVisible
    {
        get => _mapControl?.IsGridVisible ?? _isGridVisible;
        set
        {
            _isGridVisible = value;
            if (_mapControl != null)
                _mapControl.IsGridVisible = value;
        }
    }

    public void Toggle3DMode()
    {
        _is3DMode = !_is3DMode;
        _mapControl?.Toggle3DMode();
    }

    public void Set3DMode(bool is3D)
    {
        _is3DMode = is3D;
        _mapControl?.Set3DMode(is3D);
    }

    public void SetPitch(double deltaRadians)
    {
        _pitch += deltaRadians;
        _mapControl?.SetPitch(deltaRadians);
    }

    public void SetPitchAbsolute(double pitchRadians)
    {
        _pitch = pitchRadians;
        _mapControl?.SetPitchAbsolute(pitchRadians);
    }

    public void Pan(double deltaX, double deltaY)
    {
        _mapControl?.Pan(deltaX, deltaY);
    }

    public void PanTo(double x, double y)
    {
        _mapControl?.PanTo(x, y);
    }

    public void Zoom(double factor)
    {
        _zoomLevel *= factor;
        _mapControl?.Zoom(factor);
    }

    public void Rotate(double deltaRadians)
    {
        _rotation += deltaRadians;
        _mapControl?.Rotate(deltaRadians);
    }

    public void SetRotation(double radians)
    {
        _rotation = radians;
        // Note: DrawingContextMapControl uses SetCamera for rotation
    }

    public void SetCamera(double x, double y, double zoom, double rotation)
    {
        _zoomLevel = zoom;
        _rotation = rotation;
        _mapControl?.SetCamera(x, y, zoom, rotation);
    }

    public void StartPan(double x, double y)
    {
        _mapControl?.StartPan(new Avalonia.Point(x, y));
    }

    public void StartRotate(double x, double y)
    {
        _mapControl?.StartRotate(new Avalonia.Point(x, y));
    }

    public void UpdatePointer(double x, double y)
    {
        _mapControl?.UpdateMouse(new Avalonia.Point(x, y));
    }

    public void EndInteraction()
    {
        _mapControl?.EndPanRotate();
    }

    public void SetBoundary(Boundary? boundary)
    {
        _mapControl?.SetBoundary(boundary);
    }

    public void SetVehiclePosition(double easting, double northing, double headingRadians)
    {
        _mapControl?.SetVehiclePosition(easting, northing, headingRadians);
    }

    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points)
    {
        _mapControl?.SetRecordingPoints(points);
    }

    public void ClearRecordingPoints()
    {
        _mapControl?.ClearRecordingPoints();
    }

    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0)
    {
        _mapControl?.SetBoundaryOffsetIndicator(show, offsetMeters);
    }

    public void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY)
    {
        _mapControl?.SetBackgroundImage(imagePath, minX, maxY, maxX, minY);
    }

    public void SetBackgroundImageWithMercator(string imagePath, double minX, double maxY, double maxX, double minY,
        double mercMinX, double mercMaxX, double mercMinY, double mercMaxY,
        double originLat, double originLon)
    {
        _mapControl?.SetBackgroundImageWithMercator(imagePath, minX, maxY, maxX, minY,
            mercMinX, mercMaxX, mercMinY, mercMaxY, originLat, originLon);
    }

    public void ClearBackground()
    {
        _mapControl?.ClearBackground();
    }

    // Headland visualization
    public void SetHeadlandLine(IReadOnlyList<Vec3>? headlandPoints)
    {
        _mapControl?.SetHeadlandLine(headlandPoints);
    }

    public void SetHeadlandPreview(IReadOnlyList<Vec2>? previewPoints)
    {
        _mapControl?.SetHeadlandPreview(previewPoints);
    }

    public void SetHeadlandVisible(bool visible)
    {
        _mapControl?.SetHeadlandVisible(visible);
    }

    // YouTurn path visualization
    public void SetYouTurnPath(IReadOnlyList<(double Easting, double Northing)>? turnPath)
    {
        _mapControl?.SetYouTurnPath(turnPath);
    }

    // Track visualization for U-turns
    public void SetNextTrack(AgValoniaGPS.Models.Track.Track? track)
    {
        _mapControl?.SetNextTrack(track);
    }

    public void SetIsInYouTurn(bool isInTurn)
    {
        _mapControl?.SetIsInYouTurn(isInTurn);
    }

    // Active Track for guidance
    public void SetActiveTrack(AgValoniaGPS.Models.Track.Track? track)
    {
        _mapControl?.SetActiveTrack(track);
    }

    // Coverage bitmap initialization on field load
    public void InitializeCoverageBitmapWithBounds(double minE, double maxE, double minN, double maxN)
    {
        _mapControl?.InitializeCoverageBitmapWithBounds(minE, maxE, minN, maxN);
    }
}
