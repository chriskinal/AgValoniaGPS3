using System;
using System.Collections.Generic;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Services.Interfaces;

/// <summary>
/// Event args for boundary recording state changes
/// </summary>
public class BoundaryRecordingStateChangedEventArgs : EventArgs
{
    public BoundaryRecordingState State { get; }
    public int PointCount { get; }
    public double AreaHectares { get; }

    public BoundaryRecordingStateChangedEventArgs(BoundaryRecordingState state, int pointCount, double areaHectares)
    {
        State = state;
        PointCount = pointCount;
        AreaHectares = areaHectares;
    }
}

/// <summary>
/// Event args for when a boundary point is added
/// </summary>
public class BoundaryPointAddedEventArgs : EventArgs
{
    public BoundaryPoint Point { get; }
    public int TotalPoints { get; }
    public double AreaHectares { get; }

    public BoundaryPointAddedEventArgs(BoundaryPoint point, int totalPoints, double areaHectares)
    {
        Point = point;
        TotalPoints = totalPoints;
        AreaHectares = areaHectares;
    }
}

/// <summary>
/// Recording state for boundary
/// </summary>
public enum BoundaryRecordingState
{
    Idle,
    Recording,
    Paused
}

/// <summary>
/// Boundary type being recorded
/// </summary>
public enum BoundaryType
{
    Outer,
    Inner
}

/// <summary>
/// Service for recording and managing boundary polygons during GPS driving
/// </summary>
public interface IBoundaryRecordingService
{
    /// <summary>
    /// Current recording state
    /// </summary>
    BoundaryRecordingState State { get; }

    /// <summary>
    /// Whether recording is currently active (not paused, not idle)
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Type of boundary being recorded (outer or inner)
    /// </summary>
    BoundaryType CurrentBoundaryType { get; }

    /// <summary>
    /// Current number of recorded points
    /// </summary>
    int PointCount { get; }

    /// <summary>
    /// Current area in hectares (calculated from recorded points)
    /// </summary>
    double AreaHectares { get; }

    /// <summary>
    /// Minimum distance between points in meters (filtering)
    /// </summary>
    double MinPointSpacing { get; set; }

    /// <summary>
    /// Get a copy of the current recorded points
    /// </summary>
    IReadOnlyList<BoundaryPoint> RecordedPoints { get; }

    /// <summary>
    /// Start recording a new boundary
    /// </summary>
    /// <param name="boundaryType">Type of boundary to record</param>
    void StartRecording(BoundaryType boundaryType = BoundaryType.Outer);

    /// <summary>
    /// Pause recording (can resume)
    /// </summary>
    void PauseRecording();

    /// <summary>
    /// Resume recording after pause
    /// </summary>
    void ResumeRecording();

    /// <summary>
    /// Stop recording and finalize the boundary
    /// </summary>
    /// <returns>The recorded boundary polygon, or null if not enough points</returns>
    BoundaryPolygon? StopRecording();

    /// <summary>
    /// Cancel recording and discard all points
    /// </summary>
    void CancelRecording();

    /// <summary>
    /// Add a point at the current GPS position
    /// Called automatically by GPS service when recording
    /// </summary>
    /// <param name="easting">Easting in local coordinates</param>
    /// <param name="northing">Northing in local coordinates</param>
    /// <param name="heading">Heading in radians</param>
    void AddPoint(double easting, double northing, double heading);

    /// <summary>
    /// Manually add a point regardless of recording state.
    /// Used for "Add Point Manually" button when paused.
    /// </summary>
    /// <param name="easting">Easting in local coordinates</param>
    /// <param name="northing">Northing in local coordinates</param>
    /// <param name="heading">Heading in radians</param>
    void AddPointManual(double easting, double northing, double heading);

    /// <summary>
    /// Remove the last recorded point (undo)
    /// </summary>
    /// <returns>True if a point was removed</returns>
    bool RemoveLastPoint();

    /// <summary>
    /// Clear all recorded points without stopping recording
    /// </summary>
    void ClearPoints();

    /// <summary>
    /// Event fired when recording state changes
    /// </summary>
    event EventHandler<BoundaryRecordingStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event fired when a point is added
    /// </summary>
    event EventHandler<BoundaryPointAddedEventArgs>? PointAdded;
}
