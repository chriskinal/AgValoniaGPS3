using System.Collections.Generic;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models;
using AgValoniaGPS.Desktop.Controls;
using AgValoniaGPS.Views.Controls;

namespace AgValoniaGPS.Desktop.Services;

/// <summary>
/// Desktop implementation of IMapService.
/// Wraps the platform-specific map control (OpenGL, Skia, or DrawingContext).
/// </summary>
public class MapService : IMapService
{
    private ISharedMapControl? _mapControl;

    /// <summary>
    /// Set the underlying map control. Must be called after the control is created.
    /// </summary>
    public void SetMapControl(ISharedMapControl mapControl)
    {
        _mapControl = mapControl;
    }

    private ISharedMapControl GetMapControl()
    {
        if (_mapControl == null)
            throw new System.InvalidOperationException("Map control not set. Call SetMapControl first.");
        return _mapControl;
    }

    public void Toggle3DMode() => GetMapControl().Toggle3DMode();

    public void Set3DMode(bool is3D) => GetMapControl().Set3DMode(is3D);

    public bool Is3DMode => _mapControl != null && !GetMapControl().IsGridVisible; // TODO: Add Is3DMode to IMapControl

    public void SetPitch(double deltaRadians) => GetMapControl().SetPitch(deltaRadians);

    public void SetPitchAbsolute(double pitchRadians) => GetMapControl().SetPitchAbsolute(pitchRadians);

    public double Pitch => 0; // TODO: Add Pitch property to IMapControl

    public void Pan(double deltaX, double deltaY) => GetMapControl().Pan(deltaX, deltaY);

    public void PanTo(double x, double y) => GetMapControl().Pan(x, y);

    public void Zoom(double factor) => GetMapControl().Zoom(factor);

    public double ZoomLevel => GetMapControl().GetZoom();

    public void Rotate(double deltaRadians) => GetMapControl().Rotate(deltaRadians);

    public void SetRotation(double radians) => GetMapControl().Rotate(radians);

    public double Rotation => 0; // TODO: Add Rotation property to IMapControl

    public void SetCamera(double x, double y, double zoom, double rotation) =>
        GetMapControl().SetCamera(x, y, zoom, rotation);

    public void StartPan(double x, double y) =>
        GetMapControl().StartPan(new Avalonia.Point(x, y));

    public void StartRotate(double x, double y) =>
        GetMapControl().StartRotate(new Avalonia.Point(x, y));

    public void UpdatePointer(double x, double y) =>
        GetMapControl().UpdateMouse(new Avalonia.Point(x, y));

    public void EndInteraction() => GetMapControl().EndPanRotate();

    public void SetBoundary(Boundary? boundary) => GetMapControl().SetBoundary(boundary);

    public void SetVehiclePosition(double easting, double northing, double headingRadians) =>
        GetMapControl().SetVehiclePosition(easting, northing, headingRadians);

    public bool IsGridVisible
    {
        get => _mapControl?.IsGridVisible ?? false;
        set
        {
            if (_mapControl != null)
                _mapControl.IsGridVisible = value;
        }
    }

    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points) =>
        GetMapControl().SetRecordingPoints(points);

    public void ClearRecordingPoints() => GetMapControl().ClearRecordingPoints();

    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0) =>
        GetMapControl().SetBoundaryOffsetIndicator(show, offsetMeters);

    public void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY) =>
        GetMapControl().SetBackgroundImage(imagePath, minX, maxY, maxX, minY);

    public void ClearBackground() => GetMapControl().ClearBackground();
}
