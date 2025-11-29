using System.Collections.Generic;
using AgValoniaGPS.Models;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.iOS.Services;

/// <summary>
/// iOS-specific map service - stub implementation
/// </summary>
public class MapService : IMapService
{
    private bool _is3DMode;
    private double _pitch;
    private double _zoomLevel = 1.0;
    private double _rotation;
    private bool _isGridVisible;

    public bool Is3DMode => _is3DMode;
    public double Pitch => _pitch;
    public double ZoomLevel => _zoomLevel;
    public double Rotation => _rotation;
    public bool IsGridVisible
    {
        get => _isGridVisible;
        set => _isGridVisible = value;
    }

    public void Toggle3DMode() => _is3DMode = !_is3DMode;
    public void Set3DMode(bool is3D) => _is3DMode = is3D;

    public void SetPitch(double deltaRadians) => _pitch += deltaRadians;
    public void SetPitchAbsolute(double pitchRadians) => _pitch = pitchRadians;

    public void Pan(double deltaX, double deltaY) { }
    public void PanTo(double x, double y) { }

    public void Zoom(double factor) => _zoomLevel *= factor;

    public void Rotate(double deltaRadians) => _rotation += deltaRadians;
    public void SetRotation(double radians) => _rotation = radians;

    public void SetCamera(double x, double y, double zoom, double rotation)
    {
        _zoomLevel = zoom;
        _rotation = rotation;
    }

    public void StartPan(double x, double y) { }
    public void StartRotate(double x, double y) { }
    public void UpdatePointer(double x, double y) { }
    public void EndInteraction() { }

    public void SetBoundary(Boundary? boundary) { }
    public void SetVehiclePosition(double easting, double northing, double headingRadians) { }

    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points) { }
    public void ClearRecordingPoints() { }
    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0) { }

    public void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY) { }
    public void ClearBackground() { }
}
