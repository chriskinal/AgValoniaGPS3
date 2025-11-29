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
