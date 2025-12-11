using AgValoniaGPS.Models.Tool;

namespace AgValoniaGPS.Models;

/// <summary>
/// Complete vehicle profile containing all configuration settings
/// Compatible with AgOpenGPS vehicle XML format
/// </summary>
public class VehicleProfile
{
    /// <summary>
    /// Profile name (filename without .XML extension)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the profile file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Vehicle physical configuration (wheelbase, antenna, steering limits)
    /// </summary>
    public VehicleConfiguration Vehicle { get; set; } = new();

    /// <summary>
    /// Tool/implement configuration (width, sections, hitch)
    /// </summary>
    public ToolConfiguration Tool { get; set; } = new();

    /// <summary>
    /// YouTurn configuration (turn radius, extension, boundary distance)
    /// </summary>
    public YouTurnConfiguration YouTurn { get; set; } = new();

    /// <summary>
    /// Section positions (edge positions for each section in meters)
    /// Index 0 = leftmost edge, increasing indices move right
    /// </summary>
    public double[] SectionPositions { get; set; } = new double[17];

    /// <summary>
    /// Number of sections configured
    /// </summary>
    public int NumSections { get; set; } = 1;

    /// <summary>
    /// Whether metric units are used
    /// </summary>
    public bool IsMetric { get; set; } = false;

    /// <summary>
    /// Whether Pure Pursuit steering is used (vs Stanley)
    /// </summary>
    public bool IsPurePursuit { get; set; } = true;

    /// <summary>
    /// Simulator mode enabled
    /// </summary>
    public bool IsSimulatorOn { get; set; } = true;

    /// <summary>
    /// Simulator latitude
    /// </summary>
    public double SimLatitude { get; set; } = 32.5904315166667;

    /// <summary>
    /// Simulator longitude
    /// </summary>
    public double SimLongitude { get; set; } = -87.1804217333333;
}
