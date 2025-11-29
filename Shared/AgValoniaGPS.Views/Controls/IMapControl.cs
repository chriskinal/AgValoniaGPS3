using System.Collections.Generic;
using Avalonia;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Interface for map rendering controls - allows both OpenGL and SkiaSharp implementations
/// </summary>
public interface IMapControl
{
    // Camera/View control
    void Toggle3DMode();
    void Set3DMode(bool is3D);
    bool Is3DMode { get; }
    void SetPitch(double deltaRadians);
    void PanTo(double x, double y);
    void SetPitchAbsolute(double pitchRadians);
    void Pan(double deltaX, double deltaY);
    void Zoom(double factor);
    double GetZoom();
    void SetCamera(double x, double y, double zoom, double rotation);
    void Rotate(double deltaRadians);

    // Mouse interaction
    void StartPan(Point position);
    void StartRotate(Point position);
    void UpdateMouse(Point position);
    void EndPanRotate();

    // Content
    void SetBoundary(Boundary? boundary);
    void SetVehiclePosition(double x, double y, double heading);
    void SetGridVisible(bool visible);
    void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points);
    void ClearRecordingPoints();
    void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY);
    void ClearBackground();

    // Boundary recording indicator
    void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0);

    // Grid visibility property
    bool IsGridVisible { get; set; }
}
