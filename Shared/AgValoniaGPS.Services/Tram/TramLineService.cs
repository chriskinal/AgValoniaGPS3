using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Tram;

/// <summary>
/// Service for managing tram lines for controlled traffic farming (CTF).
/// Tram lines are permanent wheel tracks that reduce soil compaction by
/// concentrating wheel traffic to the same paths.
/// </summary>
public class TramLineService : ITramLineService
{
    private readonly ITramLineOffsetService _offsetService;

    private readonly List<Vec2> _outerBoundaryTrack = new();
    private readonly List<Vec2> _innerBoundaryTrack = new();
    private readonly List<List<Vec2>> _parallelTramLines = new();

    private bool _isLeftManualOn;
    private bool _isRightManualOn;

    public IReadOnlyList<Vec2> OuterBoundaryTrack => _outerBoundaryTrack;
    public IReadOnlyList<Vec2> InnerBoundaryTrack => _innerBoundaryTrack;
    public IReadOnlyList<IReadOnlyList<Vec2>> ParallelTramLines => _parallelTramLines;

    public bool HasTramLines =>
        _outerBoundaryTrack.Count > 0 ||
        _innerBoundaryTrack.Count > 0 ||
        _parallelTramLines.Count > 0;

    public bool IsLeftManualOn
    {
        get => _isLeftManualOn;
        set => _isLeftManualOn = value;
    }

    public bool IsRightManualOn
    {
        get => _isRightManualOn;
        set => _isRightManualOn = value;
    }

    public event EventHandler? TramLinesUpdated;

    public TramLineService(ITramLineOffsetService offsetService)
    {
        _offsetService = offsetService;
    }

    /// <summary>
    /// Generate boundary tram tracks from a fence line (headland or outer boundary)
    /// </summary>
    public void GenerateBoundaryTramTracks(IReadOnlyList<Vec3> fenceLine)
    {
        if (fenceLine == null || fenceLine.Count < 3)
            return;

        var config = ConfigurationStore.Instance;
        double tramWidth = config.Tram.TramWidth;
        double halfWheelTrack = config.Vehicle.TrackWidth / 2.0;

        // Convert to List<Vec3> for the offset service
        var fenceLineList = fenceLine.ToList();

        // Determine if we should use outer or inner based on invert setting
        bool isOuter = !config.Tram.IsOuterInverted;

        // Generate outer boundary track
        _outerBoundaryTrack.Clear();
        var outerPoints = _offsetService.GenerateOuterTramline(fenceLineList, tramWidth, halfWheelTrack);
        _outerBoundaryTrack.AddRange(outerPoints);

        // Generate inner boundary track
        _innerBoundaryTrack.Clear();
        var innerPoints = _offsetService.GenerateInnerTramline(fenceLineList, tramWidth, halfWheelTrack);
        _innerBoundaryTrack.AddRange(innerPoints);

        TramLinesUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generate parallel tram lines from a guidance track
    /// </summary>
    public void GenerateParallelTramLines(Models.Track.Track referenceTrack, double fieldWidth)
    {
        if (referenceTrack == null || referenceTrack.Points.Count < 2)
            return;

        var config = ConfigurationStore.Instance;
        double tramWidth = config.Tram.TramWidth;
        int passes = config.Tram.Passes;

        _parallelTramLines.Clear();

        // Calculate how many tram lines we need based on field width and passes
        double passWidth = config.Tool.Width * passes;
        int numLines = (int)(fieldWidth / passWidth) + 2;

        // Generate tram lines on both sides of the reference track
        for (int i = -numLines; i <= numLines; i++)
        {
            if (i == 0) continue; // Skip center line

            double offset = i * passWidth;
            var tramLine = OffsetTrackLaterally(referenceTrack, offset);

            if (tramLine.Count > 1)
            {
                _parallelTramLines.Add(tramLine);
            }
        }

        TramLinesUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Offset a track laterally by a given distance
    /// </summary>
    private List<Vec2> OffsetTrackLaterally(Models.Track.Track track, double offset)
    {
        var result = new List<Vec2>();

        for (int i = 0; i < track.Points.Count; i++)
        {
            var point = track.Points[i];
            double heading = point.Heading;

            // Offset perpendicular to heading
            double perpHeading = heading + Math.PI / 2.0;
            var offsetPoint = new Vec2(
                point.Easting + Math.Sin(perpHeading) * offset,
                point.Northing + Math.Cos(perpHeading) * offset
            );

            result.Add(offsetPoint);
        }

        return result;
    }

    /// <summary>
    /// Add a tram line at the current position (for manual recording)
    /// </summary>
    public void AddTramLine(IReadOnlyList<Vec2> points)
    {
        if (points == null || points.Count < 2)
            return;

        _parallelTramLines.Add(points.ToList());
        TramLinesUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Check if a position is on or near a tram line
    /// </summary>
    public bool IsOnTramLine(Vec3 position, double tolerance)
    {
        double distSq = tolerance * tolerance;

        // Check boundary tracks
        if (IsOnPolyline(_outerBoundaryTrack, position, distSq))
            return true;

        if (IsOnPolyline(_innerBoundaryTrack, position, distSq))
            return true;

        // Check parallel tram lines
        foreach (var tramLine in _parallelTramLines)
        {
            if (IsOnPolyline(tramLine, position, distSq))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a point is near a polyline
    /// </summary>
    private bool IsOnPolyline(List<Vec2> polyline, Vec3 position, double toleranceSquared)
    {
        if (polyline.Count < 2)
            return false;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            double distSq = DistanceToSegmentSquared(
                position.Easting, position.Northing,
                polyline[i].Easting, polyline[i].Northing,
                polyline[i + 1].Easting, polyline[i + 1].Northing);

            if (distSq <= toleranceSquared)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get distance to the nearest tram line
    /// </summary>
    public double DistanceToNearestTramLine(Vec3 position)
    {
        double minDistSq = double.MaxValue;

        // Check boundary tracks
        double distSq = DistanceToPolylineSquared(_outerBoundaryTrack, position);
        if (distSq < minDistSq) minDistSq = distSq;

        distSq = DistanceToPolylineSquared(_innerBoundaryTrack, position);
        if (distSq < minDistSq) minDistSq = distSq;

        // Check parallel tram lines
        foreach (var tramLine in _parallelTramLines)
        {
            distSq = DistanceToPolylineSquared(tramLine, position);
            if (distSq < minDistSq) minDistSq = distSq;
        }

        return minDistSq < double.MaxValue ? Math.Sqrt(minDistSq) : double.MaxValue;
    }

    /// <summary>
    /// Get squared distance from point to polyline
    /// </summary>
    private double DistanceToPolylineSquared(List<Vec2> polyline, Vec3 position)
    {
        if (polyline.Count < 2)
            return double.MaxValue;

        double minDistSq = double.MaxValue;

        for (int i = 0; i < polyline.Count - 1; i++)
        {
            double distSq = DistanceToSegmentSquared(
                position.Easting, position.Northing,
                polyline[i].Easting, polyline[i].Northing,
                polyline[i + 1].Easting, polyline[i + 1].Northing);

            if (distSq < minDistSq)
                minDistSq = distSq;
        }

        return minDistSq;
    }

    /// <summary>
    /// Calculate squared distance from point to line segment
    /// </summary>
    private double DistanceToSegmentSquared(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double lengthSq = dx * dx + dy * dy;

        if (lengthSq < 0.0001)
        {
            // Segment is a point
            return (px - ax) * (px - ax) + (py - ay) * (py - ay);
        }

        // Project point onto line, clamped to segment
        double t = Math.Max(0, Math.Min(1, ((px - ax) * dx + (py - ay) * dy) / lengthSq));

        double projX = ax + t * dx;
        double projY = ay + t * dy;

        return (px - projX) * (px - projX) + (py - projY) * (py - projY);
    }

    /// <summary>
    /// Clear all tram lines
    /// </summary>
    public void Clear()
    {
        _outerBoundaryTrack.Clear();
        _innerBoundaryTrack.Clear();
        _parallelTramLines.Clear();
        _isLeftManualOn = false;
        _isRightManualOn = false;

        TramLinesUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Save tram lines to field directory
    /// </summary>
    public void SaveToFile(string fieldDirectory)
    {
        if (string.IsNullOrEmpty(fieldDirectory))
            return;

        string filePath = Path.Combine(fieldDirectory, "TramLines.txt");

        try
        {
            using var writer = new StreamWriter(filePath);

            // Write outer boundary track
            writer.WriteLine($"$OuterTrack,{_outerBoundaryTrack.Count}");
            foreach (var point in _outerBoundaryTrack)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F4},{1:F4}", point.Easting, point.Northing));
            }

            // Write inner boundary track
            writer.WriteLine($"$InnerTrack,{_innerBoundaryTrack.Count}");
            foreach (var point in _innerBoundaryTrack)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F4},{1:F4}", point.Easting, point.Northing));
            }

            // Write parallel tram lines
            writer.WriteLine($"$TramLines,{_parallelTramLines.Count}");
            foreach (var tramLine in _parallelTramLines)
            {
                writer.WriteLine($"$Line,{tramLine.Count}");
                foreach (var point in tramLine)
                {
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0:F4},{1:F4}", point.Easting, point.Northing));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TramLineService] Failed to save tram lines: {ex.Message}");
        }
    }

    /// <summary>
    /// Load tram lines from field directory
    /// </summary>
    public void LoadFromFile(string fieldDirectory)
    {
        if (string.IsNullOrEmpty(fieldDirectory))
            return;

        string filePath = Path.Combine(fieldDirectory, "TramLines.txt");

        if (!File.Exists(filePath))
            return;

        try
        {
            Clear();

            using var reader = new StreamReader(filePath);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("$OuterTrack,"))
                {
                    int count = int.Parse(line.Split(',')[1]);
                    ReadPoints(reader, _outerBoundaryTrack, count);
                }
                else if (line.StartsWith("$InnerTrack,"))
                {
                    int count = int.Parse(line.Split(',')[1]);
                    ReadPoints(reader, _innerBoundaryTrack, count);
                }
                else if (line.StartsWith("$TramLines,"))
                {
                    int lineCount = int.Parse(line.Split(',')[1]);
                    for (int i = 0; i < lineCount; i++)
                    {
                        line = reader.ReadLine();
                        if (line != null && line.StartsWith("$Line,"))
                        {
                            int pointCount = int.Parse(line.Split(',')[1]);
                            var tramLine = new List<Vec2>();
                            ReadPoints(reader, tramLine, pointCount);
                            if (tramLine.Count > 0)
                            {
                                _parallelTramLines.Add(tramLine);
                            }
                        }
                    }
                }
            }

            if (HasTramLines)
            {
                TramLinesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TramLineService] Failed to load tram lines: {ex.Message}");
        }
    }

    /// <summary>
    /// Read points from file into a list
    /// </summary>
    private void ReadPoints(StreamReader reader, List<Vec2> points, int count)
    {
        for (int i = 0; i < count; i++)
        {
            string? line = reader.ReadLine();
            if (line == null) break;

            var parts = line.Split(',');
            if (parts.Length >= 2)
            {
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double easting) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double northing))
                {
                    points.Add(new Vec2(easting, northing));
                }
            }
        }
    }
}
