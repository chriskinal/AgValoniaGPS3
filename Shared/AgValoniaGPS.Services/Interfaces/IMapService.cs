using System.Collections.Generic;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;

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

    // AB Line visualization for U-turns
    void SetNextABLine(ABLine? abLine);
    void SetIsInYouTurn(bool isInTurn);

    // Active AB Line for guidance
    void SetActiveABLine(ABLine? abLine);
}
