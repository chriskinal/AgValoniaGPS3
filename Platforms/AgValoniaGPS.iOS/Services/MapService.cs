using System.Collections.Generic;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
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

    // AB Line visualization for U-turns
    public void SetNextABLine(ABLine? abLine)
    {
        _mapControl?.SetNextABLine(abLine);
    }

    public void SetIsInYouTurn(bool isInTurn)
    {
        _mapControl?.SetIsInYouTurn(isInTurn);
    }

    // Active AB Line for guidance
    public void SetActiveABLine(ABLine? abLine)
    {
        _mapControl?.SetActiveABLine(abLine);
    }
}
