using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Track;

/// <summary>
/// Unified output data from track guidance calculations.
/// Works with both Pure Pursuit and Stanley algorithms.
/// </summary>
public class TrackGuidanceOutput
{
    /// <summary>
    /// Calculated steer angle in degrees.
    /// Positive = right, Negative = left.
    /// </summary>
    public double SteerAngle { get; set; }

    /// <summary>
    /// Distance from guidance line in millimeters.
    /// For UDP transmission to steering hardware.
    /// </summary>
    public short GuidanceLineDistanceOff { get; set; }

    /// <summary>
    /// Steer angle * 100 for UDP transmission.
    /// </summary>
    public short GuidanceLineSteerAngle { get; set; }

    /// <summary>
    /// Cross-track error (distance from line) in meters.
    /// Positive = right of line, Negative = left of line.
    /// </summary>
    public double CrossTrackError { get; set; }

    /// <summary>
    /// Pivot distance from guidance line in meters.
    /// </summary>
    public double DistanceFromLinePivot { get; set; }

    /// <summary>
    /// Steer axle distance from guidance line in meters (Stanley).
    /// </summary>
    public double DistanceFromLineSteer { get; set; }

    /// <summary>
    /// Look-ahead goal point for Pure Pursuit visualization.
    /// </summary>
    public Vec2 GoalPoint { get; set; }

    /// <summary>
    /// Closest point on the guidance line (pivot reference).
    /// </summary>
    public Vec2 ClosestPointPivot { get; set; }

    /// <summary>
    /// Closest point on the guidance line (steer reference for Stanley).
    /// </summary>
    public Vec2 ClosestPointSteer { get; set; }

    /// <summary>
    /// Center point of turning radius (Pure Pursuit visualization).
    /// </summary>
    public Vec2 RadiusPoint { get; set; }

    /// <summary>
    /// Pure Pursuit turning radius in meters.
    /// </summary>
    public double PurePursuitRadius { get; set; }

    /// <summary>
    /// Heading error in degrees for display.
    /// </summary>
    public double HeadingErrorDegrees { get; set; }

    /// <summary>
    /// Heading at closest point on line (for manual U-turn).
    /// </summary>
    public double ManualUturnHeading { get; set; }

    /// <summary>
    /// Updated location index in track points.
    /// Use this as CurrentLocationIndex for next calculation.
    /// </summary>
    public int CurrentLocationIndex { get; set; }

    /// <summary>
    /// Whether to search globally on next iteration.
    /// Usually false unless at end of track or U-turn triggered.
    /// </summary>
    public bool FindGlobalNearest { get; set; }

    /// <summary>
    /// Whether vehicle has reached end of track.
    /// Only applicable for finite curves.
    /// </summary>
    public bool IsAtEndOfTrack { get; set; }

    /// <summary>
    /// Updated state for next iteration.
    /// Pass this as PreviousState on next call.
    /// </summary>
    public TrackGuidanceState State { get; set; } = new();
}
