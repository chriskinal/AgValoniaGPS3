namespace AgValoniaGPS.Models;

/// <summary>
/// Configuration settings for YouTurn (U-turn) path creation
/// </summary>
public class YouTurnConfiguration
{
    /// <summary>
    /// Radius of the U-turn arc in meters
    /// Maps to: set_youTurnRadius
    /// </summary>
    public double TurnRadius { get; set; } = 8.0;

    /// <summary>
    /// Extension length beyond headland boundary in meters
    /// Maps to: set_youTurnExtensionLength
    /// </summary>
    public double ExtensionLength { get; set; } = 20.0;

    /// <summary>
    /// Distance from boundary to start turn in meters
    /// Maps to: set_youTurnDistanceFromBoundary
    /// </summary>
    public double DistanceFromBoundary { get; set; } = 2.0;

    /// <summary>
    /// Skip width multiplier (how many passes to skip)
    /// Maps to: set_youSkipWidth
    /// </summary>
    public int SkipWidth { get; set; } = 1;

    /// <summary>
    /// U-turn style (0 = standard U-turn)
    /// Maps to: set_uTurnStyle
    /// </summary>
    public int Style { get; set; } = 0;

    /// <summary>
    /// Smoothing factor for U-turn path
    /// Maps to: setAS_uTurnSmoothing
    /// </summary>
    public int Smoothing { get; set; } = 14;

    /// <summary>
    /// Compensation factor for U-turn steering
    /// Maps to: setAS_uTurnCompensation
    /// </summary>
    public double UTurnCompensation { get; set; } = 1.0;
}
