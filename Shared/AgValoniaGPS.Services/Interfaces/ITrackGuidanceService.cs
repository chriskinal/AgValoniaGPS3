using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Unified track guidance service interface.
/// Handles both Pure Pursuit and Stanley algorithms.
/// Works with both AB lines (2 points) and curves (N points).
/// </summary>
public interface ITrackGuidanceService
{
    /// <summary>
    /// Calculate steering guidance for a track.
    /// </summary>
    /// <param name="input">Guidance input parameters including track, position, and configuration</param>
    /// <returns>Guidance output with steering angle, cross-track error, and updated state</returns>
    TrackGuidanceOutput CalculateGuidance(TrackGuidanceInput input);
}
