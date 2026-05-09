// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Tram;

/// <summary>
/// Service for managing tram lines for controlled traffic farming (CTF).
/// Tram lines are permanent wheel tracks that reduce soil compaction by
/// concentrating wheel traffic to the same paths.
/// </summary>
public class TramLineService(
    ITramLineOffsetService offsetService,
    ILogger<TramLineService> logger) : ITramLineService
{
    private readonly List<Vec2> _outerBoundaryTrack = new();
    private readonly List<Vec2> _innerBoundaryTrack = new();
    private readonly List<List<Vec2>> _parallelTramLines = new();
    private readonly List<List<Vec2>> _boundaryExtraLines = new();
    private List<Vec3>? _boundaryFence;

    private bool _isLeftManualOn;
    private bool _isRightManualOn;

    public IReadOnlyList<Vec2> OuterBoundaryTrack => _outerBoundaryTrack;
    public IReadOnlyList<Vec2> InnerBoundaryTrack => _innerBoundaryTrack;
    public IReadOnlyList<IReadOnlyList<Vec2>> ParallelTramLines => _parallelTramLines;
    public IReadOnlyList<IReadOnlyList<Vec2>> BoundaryExtraLines => _boundaryExtraLines;

    public bool HasTramLines =>
        _outerBoundaryTrack.Count > 0 ||
        _innerBoundaryTrack.Count > 0 ||
        _parallelTramLines.Count > 0 ||
        _boundaryExtraLines.Count > 0;

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

    /// <summary>
    /// Set boundary fence for clipping parallel tram lines.
    /// Points outside the fence are excluded.
    /// </summary>
    public void SetBoundaryFence(IReadOnlyList<Vec3>? fence)
    {
        _boundaryFence = fence?.ToList();
    }

    /// <summary>
    /// Generate boundary tram tracks from a fence line (headland or outer boundary).
    /// Track mode: first pass outer wheel at boundary, inner wheel inward.
    /// Edge mode: first pass centered at tramWidth/2 from boundary.
    /// </summary>
    public void GenerateBoundaryTramTracks(IReadOnlyList<Vec3> fenceLine, int passCount = 1,
        Models.Tram.TramSystemMode mode = Models.Tram.TramSystemMode.Edge,
        double tramWidthOverride = 0)
    {
        if (fenceLine == null || fenceLine.Count < 3)
            return;

        var config = ConfigurationStore.Instance;
        double tramWidth = tramWidthOverride > 0 ? tramWidthOverride : config.Tram.TramWidth;
        double halfWheelTrack = config.Vehicle.TrackWidth / 2.0;

        // Ensure fence line is closed (last point = first point)
        var fenceLineList = fenceLine.ToList();
        double closeDist = Math.Pow(fenceLineList[0].Easting - fenceLineList[^1].Easting, 2) +
                           Math.Pow(fenceLineList[0].Northing - fenceLineList[^1].Northing, 2);
        if (closeDist > 1.0)
            fenceLineList.Add(fenceLineList[0]);

        if (passCount < 1) passCount = 1;

        for (int pass = 0; pass < passCount; pass++)
        {
            // Track mode: vehicle center at boundary, wheels straddle it
            // Edge mode: pass center at tramWidth/2 + tramWidth*pass
            double passCenter = mode == Models.Tram.TramSystemMode.TrackLine
                ? tramWidth * pass
                : tramWidth * 0.5 + tramWidth * pass;

            double outerOffset = passCenter - halfWheelTrack;
            double innerOffset = passCenter + halfWheelTrack;

            // If outer offset is at or outside boundary, use boundary polygon directly
            var outerPoints = outerOffset > 0.1
                ? offsetService.GenerateClipperOffsetPublic(fenceLineList, outerOffset)
                : fenceLineList.Select(p => new Vec2(p.Easting, p.Northing)).ToList();
            if (outerPoints.Count > 2)
                outerPoints.Add(outerPoints[0]);

            var innerPoints = offsetService.GenerateClipperOffsetPublic(fenceLineList, innerOffset);
            if (innerPoints.Count > 2)
                innerPoints.Add(innerPoints[0]);

            if (pass == 0)
            {
                _outerBoundaryTrack.Clear();
                _outerBoundaryTrack.AddRange(outerPoints);
                _innerBoundaryTrack.Clear();
                _innerBoundaryTrack.AddRange(innerPoints);
            }
            else
            {
                // Extra boundary passes separate from track parallel lines
                if (outerPoints.Count > 1) _boundaryExtraLines.Add(outerPoints);
                if (innerPoints.Count > 1) _boundaryExtraLines.Add(innerPoints);
            }
        }

        TramLinesUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generate tram lines for a TramSystem with full options:
    /// direction (left/right/symmetric), mode (track line/edge), pass count.
    /// </summary>
    public List<List<Vec2>> GenerateForSystem(
        Models.Tram.TramSystem system,
        Models.Track.Track referenceTrack,
        double fieldWidth)
    {
        if (referenceTrack == null || referenceTrack.Points.Count < 2)
            return new List<List<Vec2>>();

        var config = ConfigurationStore.Instance;
        double tramWidth = system.TramWidth;
        double halfWheelTrack = config.Vehicle.TrackWidth / 2.0;
        double systemOffset = system.Offset;
        var result = new List<List<Vec2>>();

        int numLines = system.PassCount > 0
            ? system.PassCount
            : (int)(fieldWidth / tramWidth) + 2;

        List<Vec3>? fenceLine = _boundaryFence;

        for (int i = 0; i < numLines; i++)
        {
            // Track mode: vehicle drives on reference, so pass 0 is at offset 0
            // Edge mode: reference is between passes, so pass 0 is at tramWidth/2
            double baseOffset = system.Mode == Models.Tram.TramSystemMode.TrackLine
                ? (tramWidth * i) + systemOffset
                : (tramWidth * 0.5) + (tramWidth * i) + systemOffset;

            // Generate based on direction
            bool doPositive = system.Direction is Models.Tram.TramDirection.Symmetric
                or Models.Tram.TramDirection.Right;
            bool doNegative = system.Direction is Models.Tram.TramDirection.Symmetric
                or Models.Tram.TramDirection.Left;

            if (doPositive)
                AddPassLines(result, referenceTrack, baseOffset, halfWheelTrack, fenceLine);
            // Skip negative i=0 only for Symmetric Track mode (would duplicate at 0/-0)
            bool skipNegZero = i == 0
                && system.Mode == Models.Tram.TramSystemMode.TrackLine
                && system.Direction == Models.Tram.TramDirection.Symmetric;
            if (doNegative && !skipNegZero)
                AddPassLines(result, referenceTrack, -baseOffset, halfWheelTrack, fenceLine);
        }

        return result;
    }

    private void AddPassLines(
        List<List<Vec2>> result,
        Models.Track.Track track,
        double centerOffset,
        double halfWheelTrack,
        List<Vec3>? fence)
    {
        // Both Track and Edge modes generate two wheel tracks per pass
        foreach (var seg in OffsetTrackLaterallySegmented(track, centerOffset - halfWheelTrack, fence))
            if (seg.Count > 1) result.Add(seg);
        foreach (var seg in OffsetTrackLaterallySegmented(track, centerOffset + halfWheelTrack, fence))
            if (seg.Count > 1) result.Add(seg);
    }

    /// <summary>
    /// Generate parallel tram lines from a guidance track.
    /// Each tram pass produces two lines: inner and outer wheel tracks.
    /// Lines are clipped to the boundary fence.
    /// </summary>
    public void GenerateParallelTramLines(Models.Track.Track referenceTrack, double fieldWidth)
    {
        if (referenceTrack == null || referenceTrack.Points.Count < 2)
            return;

        var config = ConfigurationStore.Instance;
        double tramWidth = config.Tram.TramWidth;
        int passes = config.Tram.Passes;
        double halfWheelTrack = config.Vehicle.TrackWidth / 2.0;

        _parallelTramLines.Clear();

        // Tram line pairs spaced by tramWidth (sprayer boom width)
        // Each pair offset: (tramWidth * 0.5) +/- halfWheelTrack + (tramWidth * i)
        // First pair is near the reference line (at tramWidth/2 on each side)
        int numLines = (int)(fieldWidth / tramWidth) + 2;
        int startPass = config.Tram.StartPass;
        List<Vec3>? fenceLine = _boundaryFence;

        for (int i = startPass; i < numLines + startPass; i++)
        {
            double baseOffset = (tramWidth * 0.5) + (tramWidth * i);

            // Positive side: outer and inner wheel tracks (split at boundary crossings)
            foreach (var seg in OffsetTrackLaterallySegmented(referenceTrack, baseOffset - halfWheelTrack, fenceLine))
                if (seg.Count > 1) _parallelTramLines.Add(seg);
            foreach (var seg in OffsetTrackLaterallySegmented(referenceTrack, baseOffset + halfWheelTrack, fenceLine))
                if (seg.Count > 1) _parallelTramLines.Add(seg);

            // Negative side (mirror)
            foreach (var seg in OffsetTrackLaterallySegmented(referenceTrack, -(baseOffset - halfWheelTrack), fenceLine))
                if (seg.Count > 1) _parallelTramLines.Add(seg);
            foreach (var seg in OffsetTrackLaterallySegmented(referenceTrack, -(baseOffset + halfWheelTrack), fenceLine))
                if (seg.Count > 1) _parallelTramLines.Add(seg);
        }

        TramLinesUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Offset a track laterally by a given distance, clipping to boundary.
    /// AB lines (2 points) are densified to 2m spacing before offsetting.
    /// Returns multiple segments when the line exits and re-enters the boundary.
    /// </summary>
    private List<List<Vec2>> OffsetTrackLaterallySegmented(Models.Track.Track track, double offset, List<Vec3>? fence)
    {
        var segments = new List<List<Vec2>>();

        // Densify AB lines: convert 2 points to many points along the line
        var points = track.Points;
        if (points.Count == 2)
        {
            points = DensifyLine(points[0], points[1], 2.0);
        }

        // Build all offset points first
        var allPoints = new List<(Vec2 point, bool inside)>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];
            double perpHeading = point.Heading + Math.PI / 2.0;
            var offsetPoint = new Vec2(
                point.Easting + Math.Sin(perpHeading) * offset,
                point.Northing + Math.Cos(perpHeading) * offset
            );
            bool isInside = fence == null || IsPointInFence(offsetPoint, fence);
            allPoints.Add((offsetPoint, isInside));
        }

        // Split into segments at boundary crossings
        var current = new List<Vec2>();
        for (int i = 0; i < allPoints.Count; i++)
        {
            var (pt, inside) = allPoints[i];

            if (inside)
            {
                // Add boundary intersection when entering from outside
                if (current.Count == 0 && i > 0 && !allPoints[i - 1].inside && fence != null)
                {
                    var crossing = FindBoundaryCrossing(allPoints[i - 1].point, pt, fence);
                    if (crossing.HasValue) current.Add(crossing.Value);
                }
                current.Add(pt);
            }
            else
            {
                // Add boundary intersection when exiting to outside
                if (current.Count > 0 && fence != null)
                {
                    var crossing = FindBoundaryCrossing(current[^1], pt, fence);
                    if (crossing.HasValue) current.Add(crossing.Value);

                    if (current.Count > 1)
                        segments.Add(current);
                    current = new List<Vec2>();
                }
            }
        }

        if (current.Count > 1)
            segments.Add(current);

        return segments;
    }

    /// <summary>
    /// Legacy single-segment version for backward compatibility.
    /// </summary>
    private List<Vec2> OffsetTrackLaterally(Models.Track.Track track, double offset, List<Vec3>? fence = null)
    {
        if (fence == null)
        {
            // No fence: return single line without segmentation
            var points = track.Points;
            if (points.Count == 2)
                points = DensifyLine(points[0], points[1], 2.0);

            var result = new List<Vec2>();
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                double perpHeading = point.Heading + Math.PI / 2.0;
                result.Add(new Vec2(
                    point.Easting + Math.Sin(perpHeading) * offset,
                    point.Northing + Math.Cos(perpHeading) * offset));
            }
            return result;
        }

        // With fence: use segmented version, return longest segment
        var segments = OffsetTrackLaterallySegmented(track, offset, fence);
        if (segments.Count == 0) return new List<Vec2>();
        return segments.OrderByDescending(s => s.Count).First();
    }

    /// <summary>
    /// Find where a line segment crosses the boundary polygon.
    /// Returns the intersection point closest to 'from'.
    /// </summary>
    private static Vec2? FindBoundaryCrossing(Vec2 from, Vec2 to, List<Vec3> fence)
    {
        double bestT = double.MaxValue;
        Vec2? best = null;

        double dx = to.Easting - from.Easting;
        double dy = to.Northing - from.Northing;

        for (int i = 0, j = fence.Count - 1; i < fence.Count; j = i++)
        {
            double ex = fence[i].Easting - fence[j].Easting;
            double ey = fence[i].Northing - fence[j].Northing;
            double fx = fence[j].Easting - from.Easting;
            double fy = fence[j].Northing - from.Northing;

            double denom = dx * ey - dy * ex;
            if (Math.Abs(denom) < 1e-12) continue;

            double t = (fx * ey - fy * ex) / denom;
            double u = (fx * dy - fy * dx) / denom;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1 && t < bestT)
            {
                bestT = t;
                best = new Vec2(from.Easting + dx * t, from.Northing + dy * t);
            }
        }

        return best;
    }

    /// <summary>
    /// Convert a 2-point AB line to dense points at the given spacing.
    /// Extends the line well past both ends to cover the full field.
    /// </summary>
    private static List<Vec3> DensifyLine(Vec3 a, Vec3 b, double spacing)
    {
        double dx = b.Easting - a.Easting;
        double dy = b.Northing - a.Northing;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.01) return new List<Vec3> { a, b };

        double heading = Math.Atan2(dx, dy);
        double sinH = Math.Sin(heading);
        double cosH = Math.Cos(heading);

        // Extend line 500m past each end
        double ext = 500;
        double totalLen = len + 2 * ext;
        int numPts = (int)(totalLen / spacing) + 1;

        var result = new List<Vec3>(numPts);
        double startE = a.Easting - sinH * ext;
        double startN = a.Northing - cosH * ext;

        for (int i = 0; i < numPts; i++)
        {
            double d = i * spacing;
            result.Add(new Vec3(startE + sinH * d, startN + cosH * d, heading));
        }

        return result;
    }

    /// <summary>
    /// Ray casting point-in-polygon test for boundary clipping.
    /// </summary>
    private static bool IsPointInFence(Vec2 point, List<Vec3> fence)
    {
        bool inside = false;
        int count = fence.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            double yi = fence[i].Northing, yj = fence[j].Northing;
            double xi = fence[i].Easting, xj = fence[j].Easting;
            if (((yi > point.Northing) != (yj > point.Northing)) &&
                (point.Easting < (xj - xi) * (point.Northing - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
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

        // Check boundary extra passes
        foreach (var tramLine in _boundaryExtraLines)
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

        // Check boundary extra passes
        foreach (var tramLine in _boundaryExtraLines)
        {
            distSq = DistanceToPolylineSquared(tramLine, position);
            if (distSq < minDistSq) minDistSq = distSq;
        }

        return minDistSq < double.MaxValue ? Math.Sqrt(minDistSq) : double.MaxValue;
    }

    /// <summary>
    /// Detect which wheels are on tram lines.
    /// Returns a byte: bit 0 = right wheel, bit 1 = left wheel.
    /// </summary>
    public byte DetectTramWheels(Vec3 vehiclePosition, double vehicleHeading, double tolerance)
    {
        var config = ConfigurationStore.Instance;
        double halfTrack = config.Vehicle.TrackWidth / 2.0;

        // Calculate left and right wheel positions
        double perpHeading = vehicleHeading + Math.PI / 2.0;
        double sinPerp = Math.Sin(perpHeading);
        double cosPerp = Math.Cos(perpHeading);

        var rightWheel = new Vec3(
            vehiclePosition.Easting + sinPerp * halfTrack,
            vehiclePosition.Northing + cosPerp * halfTrack,
            vehicleHeading);
        var leftWheel = new Vec3(
            vehiclePosition.Easting - sinPerp * halfTrack,
            vehiclePosition.Northing - cosPerp * halfTrack,
            vehicleHeading);

        byte result = 0;

        bool rightOn = IsOnTramLine(rightWheel, tolerance) || _isRightManualOn;
        bool leftOn = IsOnTramLine(leftWheel, tolerance) || _isLeftManualOn;

        if (rightOn) result |= 1;
        if (leftOn) result |= 2;

        return result;
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
    /// Add a single tram line to the parallel lines collection.
    /// Used when generating per-system lines.
    /// </summary>
    public void AddTramLine(List<Vec2> points)
    {
        if (points != null && points.Count > 1)
            _parallelTramLines.Add(points);
    }

    /// <summary>
    /// Clear all tram lines
    /// </summary>
    public void Clear()
    {
        _outerBoundaryTrack.Clear();
        _innerBoundaryTrack.Clear();
        _parallelTramLines.Clear();
        _boundaryExtraLines.Clear();
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
            logger.LogError(ex, "Failed to save tram lines");
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
                    int count = int.Parse(line.Split(',')[1], CultureInfo.InvariantCulture);
                    ReadPoints(reader, _outerBoundaryTrack, count);
                }
                else if (line.StartsWith("$InnerTrack,"))
                {
                    int count = int.Parse(line.Split(',')[1], CultureInfo.InvariantCulture);
                    ReadPoints(reader, _innerBoundaryTrack, count);
                }
                else if (line.StartsWith("$TramLines,"))
                {
                    int lineCount = int.Parse(line.Split(',')[1], CultureInfo.InvariantCulture);
                    for (int i = 0; i < lineCount; i++)
                    {
                        line = reader.ReadLine();
                        if (line != null && line.StartsWith("$Line,"))
                        {
                            int pointCount = int.Parse(line.Split(',')[1], CultureInfo.InvariantCulture);
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
            logger.LogError(ex, "Failed to load tram lines");
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
