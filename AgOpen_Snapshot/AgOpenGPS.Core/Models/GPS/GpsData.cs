using System;

namespace AgOpenGPS.Core.Models.GPS;

/// <summary>
/// Represents GPS data received from receiver
/// </summary>
public class GpsData
{
    public Position CurrentPosition { get; set; } = new();

    /// <summary>
    /// GPS fix quality (0=invalid, 1=GPS fix, 2=DGPS fix, 4=RTK fixed, 5=RTK float)
    /// </summary>
    public int FixQuality { get; set; }

    /// <summary>
    /// Number of satellites in use
    /// </summary>
    public int SatellitesInUse { get; set; }

    /// <summary>
    /// Horizontal dilution of precision
    /// </summary>
    public double Hdop { get; set; }

    /// <summary>
    /// Age of differential corrections in seconds
    /// </summary>
    public double DifferentialAge { get; set; }

    /// <summary>
    /// Timestamp when data was received
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Whether GPS data is currently valid
    /// </summary>
    public bool IsValid => FixQuality > 0 && SatellitesInUse >= 4;
}
