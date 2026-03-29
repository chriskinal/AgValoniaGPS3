namespace AgValoniaGPS.Models;

/// <summary>
/// Camera follow modes for the map view.
/// </summary>
public enum CameraMode
{
    /// <summary>Camera centered on vehicle, map oriented north-up.</summary>
    NorthUp,

    /// <summary>Camera centered on vehicle, map rotates so vehicle faces up.</summary>
    HeadingUp,

    /// <summary>User has panned/rotated manually, camera does not follow vehicle.</summary>
    Free
}
