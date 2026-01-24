namespace AgOpenGPS.Core.Models.GPS;

/// <summary>
/// Represents a geographic position with latitude, longitude, and altitude
/// </summary>
public record Position
{
    /// <summary>
    /// Latitude in decimal degrees
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Longitude in decimal degrees
    /// </summary>
    public double Longitude { get; init; }

    /// <summary>
    /// Altitude in meters above sea level
    /// </summary>
    public double Altitude { get; init; }

    /// <summary>
    /// UTM (Universal Transverse Mercator) Easting coordinate
    /// </summary>
    public double Easting { get; init; }

    /// <summary>
    /// UTM (Universal Transverse Mercator) Northing coordinate
    /// </summary>
    public double Northing { get; init; }

    /// <summary>
    /// UTM Zone number
    /// </summary>
    public int Zone { get; init; }

    /// <summary>
    /// UTM Hemisphere letter
    /// </summary>
    public char Hemisphere { get; init; }

    /// <summary>
    /// Heading in degrees (0-360)
    /// </summary>
    public double Heading { get; init; }

    /// <summary>
    /// Speed in meters per second
    /// </summary>
    public double Speed { get; init; }
}
