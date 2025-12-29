using System;
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Service for managing tram lines for controlled traffic farming (CTF).
/// Tram lines are permanent wheel tracks that reduce soil compaction.
/// </summary>
public interface ITramLineService
{
    /// <summary>
    /// Outer boundary wheel track (follows field boundary)
    /// </summary>
    IReadOnlyList<Vec2> OuterBoundaryTrack { get; }

    /// <summary>
    /// Inner boundary wheel track (follows field boundary, offset inward)
    /// </summary>
    IReadOnlyList<Vec2> InnerBoundaryTrack { get; }

    /// <summary>
    /// Parallel tram lines generated from guidance tracks
    /// </summary>
    IReadOnlyList<IReadOnlyList<Vec2>> ParallelTramLines { get; }

    /// <summary>
    /// Whether any tram lines exist
    /// </summary>
    bool HasTramLines { get; }

    /// <summary>
    /// Generate boundary tram tracks from a fence line (headland or outer boundary)
    /// </summary>
    /// <param name="fenceLine">Boundary fence line points with headings</param>
    void GenerateBoundaryTramTracks(IReadOnlyList<Vec3> fenceLine);

    /// <summary>
    /// Generate parallel tram lines from a guidance track
    /// </summary>
    /// <param name="referenceTrack">Reference guidance track</param>
    /// <param name="fieldWidth">Total field width to generate tram lines across</param>
    void GenerateParallelTramLines(Models.Track.Track referenceTrack, double fieldWidth);

    /// <summary>
    /// Add a tram line at the current position (for manual recording)
    /// </summary>
    /// <param name="points">Points defining the tram line</param>
    void AddTramLine(IReadOnlyList<Vec2> points);

    /// <summary>
    /// Check if a position is on or near a tram line
    /// </summary>
    /// <param name="position">Position to check</param>
    /// <param name="tolerance">Distance tolerance in meters</param>
    /// <returns>True if position is on a tram line</returns>
    bool IsOnTramLine(Vec3 position, double tolerance);

    /// <summary>
    /// Get distance to the nearest tram line
    /// </summary>
    /// <param name="position">Position to check</param>
    /// <returns>Distance in meters, or double.MaxValue if no tram lines</returns>
    double DistanceToNearestTramLine(Vec3 position);

    /// <summary>
    /// Left wheel manual override - force recording left wheel track
    /// </summary>
    bool IsLeftManualOn { get; set; }

    /// <summary>
    /// Right wheel manual override - force recording right wheel track
    /// </summary>
    bool IsRightManualOn { get; set; }

    /// <summary>
    /// Clear all tram lines
    /// </summary>
    void Clear();

    /// <summary>
    /// Save tram lines to field directory
    /// </summary>
    /// <param name="fieldDirectory">Path to field directory</param>
    void SaveToFile(string fieldDirectory);

    /// <summary>
    /// Load tram lines from field directory
    /// </summary>
    /// <param name="fieldDirectory">Path to field directory</param>
    void LoadFromFile(string fieldDirectory);

    /// <summary>
    /// Event fired when tram lines are updated
    /// </summary>
    event EventHandler? TramLinesUpdated;
}

/// <summary>
/// Low-level service for generating tramline offset paths from boundary fence lines.
/// Used internally by ITramLineService.
/// </summary>
public interface ITramLineOffsetService
{
    /// <summary>
    /// Generate inner tramline offset from boundary fence line.
    /// Inner tramline is offset inward by (tramWidth * 0.5) + halfWheelTrack.
    /// </summary>
    /// <param name="fenceLine">Boundary fence line points with headings</param>
    /// <param name="tramWidth">Width of tram passes</param>
    /// <param name="halfWheelTrack">Half of vehicle wheel track width</param>
    /// <returns>List of inner tramline points</returns>
    List<Vec2> GenerateInnerTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack);

    /// <summary>
    /// Generate outer tramline offset from boundary fence line.
    /// Outer tramline is offset inward by (tramWidth * 0.5) - halfWheelTrack.
    /// </summary>
    /// <param name="fenceLine">Boundary fence line points with headings</param>
    /// <param name="tramWidth">Width of tram passes</param>
    /// <param name="halfWheelTrack">Half of vehicle wheel track width</param>
    /// <returns>List of outer tramline points</returns>
    List<Vec2> GenerateOuterTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack);
}
