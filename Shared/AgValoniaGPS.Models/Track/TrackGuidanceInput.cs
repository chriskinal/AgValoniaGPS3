using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Track;

/// <summary>
/// Unified input data for track guidance calculations.
/// Works with both Pure Pursuit and Stanley algorithms.
/// Works with both AB lines (2 points) and curves (N points).
/// </summary>
public class TrackGuidanceInput
{
    /// <summary>
    /// The track to follow. Contains all points.
    /// For AB lines: 2 points. For curves: N points.
    /// </summary>
    public Track Track { get; set; } = null!;

    /// <summary>
    /// Vehicle pivot position (rear axle for tractors).
    /// Used for Pure Pursuit and cross-track error calculation.
    /// </summary>
    public Vec3 PivotPosition { get; set; }

    /// <summary>
    /// Vehicle steer axle position (front axle).
    /// Used for Stanley algorithm.
    /// </summary>
    public Vec3 SteerPosition { get; set; }

    /// <summary>
    /// Whether to use Stanley algorithm (true) or Pure Pursuit (false).
    /// </summary>
    public bool UseStanley { get; set; }

    /// <summary>
    /// Vehicle wheelbase in meters.
    /// </summary>
    public double Wheelbase { get; set; }

    /// <summary>
    /// Maximum steering angle in degrees.
    /// </summary>
    public double MaxSteerAngle { get; set; }

    /// <summary>
    /// Goal point look-ahead distance in meters.
    /// </summary>
    public double GoalPointDistance { get; set; }

    /// <summary>
    /// Sidehill compensation factor (0.0 = off, 1.0 = full).
    /// </summary>
    public double SideHillCompFactor { get; set; }

    /// <summary>
    /// Pure Pursuit integral gain.
    /// </summary>
    public double PurePursuitIntegralGain { get; set; }

    /// <summary>
    /// Stanley heading error gain.
    /// </summary>
    public double StanleyHeadingErrorGain { get; set; }

    /// <summary>
    /// Stanley distance error gain.
    /// </summary>
    public double StanleyDistanceErrorGain { get; set; }

    /// <summary>
    /// Stanley integral gain.
    /// </summary>
    public double StanleyIntegralGain { get; set; }

    /// <summary>
    /// Current vehicle heading from GPS fix in radians.
    /// </summary>
    public double FixHeading { get; set; }

    /// <summary>
    /// Average vehicle speed in km/h.
    /// </summary>
    public double AvgSpeed { get; set; }

    /// <summary>
    /// Whether the vehicle is traveling in reverse.
    /// </summary>
    public bool IsReverse { get; set; }

    /// <summary>
    /// Whether auto-steer is engaged.
    /// </summary>
    public bool IsAutoSteerOn { get; set; }

    /// <summary>
    /// Whether a U-turn is currently triggered.
    /// </summary>
    public bool IsYouTurnTriggered { get; set; }

    /// <summary>
    /// IMU roll angle in degrees. 88888 = invalid/no IMU.
    /// </summary>
    public double ImuRoll { get; set; }

    /// <summary>
    /// Whether vehicle is heading the same direction as the track.
    /// </summary>
    public bool IsHeadingSameWay { get; set; }

    /// <summary>
    /// Current location index in track points (for curve tracking optimization).
    /// Start at 0 for new track or after U-turn.
    /// </summary>
    public int CurrentLocationIndex { get; set; }

    /// <summary>
    /// Whether to search globally for nearest point.
    /// True when acquiring line or after U-turn.
    /// False during normal guidance (search locally around CurrentLocationIndex).
    /// </summary>
    public bool FindGlobalNearest { get; set; } = true;

    /// <summary>
    /// Previous guidance state for filtering and integration.
    /// </summary>
    public TrackGuidanceState? PreviousState { get; set; }
}
