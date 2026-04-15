// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgValoniaGPS.Models.Base;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing headland segment offset computation,
/// headland polygon building from segments, and related geometry helpers.
/// </summary>
public partial class MainViewModel
{
    /// <summary>
    /// Compute offset points for a headland segment by offsetting boundary points inward.
    /// </summary>
    public void ComputeSegmentOffset(Models.Headland.HeadlandSegment segment)
    {
        if (segment.BoundaryPoints.Count < 2)
        {
            segment.OffsetPoints.Clear();
            return;
        }

        double offset = segment.Offset;

        // For closed polygons (Boundary type or closed curves), use Clipper2 for proper
        // concave handling instead of edge-based offset
        bool isClosed = segment.BoundaryPoints.Count >= 3 &&
            System.Math.Pow(segment.BoundaryPoints[0].Easting - segment.BoundaryPoints[^1].Easting, 2) +
            System.Math.Pow(segment.BoundaryPoints[0].Northing - segment.BoundaryPoints[^1].Northing, 2) < 25.0;

        if ((segment.Type == Models.Headland.HeadlandSegmentType.Boundary || isClosed) && segment.BoundaryPoints.Count >= 3)
        {
            var clipperOffset = new Services.Geometry.PolygonOffsetService();
            var vec2Pts = segment.BoundaryPoints.Select(p => new Vec2(p.Easting, p.Northing)).ToList();
            var clipperResult = clipperOffset.CreateInwardOffset(vec2Pts, offset);
            if (clipperResult != null && clipperResult.Count >= 3)
            {
                segment.OffsetPoints = clipperResult.Select(p => new Vec3(p.Easting, p.Northing, 0)).ToList();
                return;
            }
        }

        // Determine offset direction (inward toward field center)
        double sign = 1.0;
        if (segment.Type == Models.Headland.HeadlandSegmentType.Boundary && segment.BoundaryPoints.Count >= 3)
        {
            // Closed polygon: use winding order
            double signedArea = 0;
            for (int j = 0; j < segment.BoundaryPoints.Count; j++)
            {
                var p1 = segment.BoundaryPoints[j];
                var p2 = segment.BoundaryPoints[(j + 1) % segment.BoundaryPoints.Count];
                signedArea += (p2.Easting - p1.Easting) * (p2.Northing + p1.Northing);
            }
            sign = signedArea > 0 ? 1.0 : -1.0;
        }
        else if (segment.BoundaryPoints.Count >= 2)
        {
            // Open segment: determine which side faces the field center
            var boundary = _currentBoundary?.OuterBoundary;
            if (boundary?.Points != null && boundary.Points.Count >= 3)
            {
                // Calculate boundary centroid
                double cx = 0, cy = 0;
                foreach (var bp in boundary.Points) { cx += bp.Easting; cy += bp.Northing; }
                cx /= boundary.Points.Count;
                cy /= boundary.Points.Count;

                // Test offset direction at midpoint
                int mid = segment.BoundaryPoints.Count / 2;
                var midPt = segment.BoundaryPoints[mid];
                var prevPt = mid > 0 ? segment.BoundaryPoints[mid - 1] : segment.BoundaryPoints[mid];
                var nextPt = mid < segment.BoundaryPoints.Count - 1 ? segment.BoundaryPoints[mid + 1] : segment.BoundaryPoints[mid];

                double dx = nextPt.Easting - prevPt.Easting;
                double dy = nextPt.Northing - prevPt.Northing;
                double len = System.Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.001)
                {
                    double nx = dy / len, ny = -dx / len;
                    // Check if offset toward centroid or away
                    double testE = midPt.Easting + nx;
                    double testN = midPt.Northing + ny;
                    double distToCenterOrig = System.Math.Pow(midPt.Easting - cx, 2) + System.Math.Pow(midPt.Northing - cy, 2);
                    double distToCenterTest = System.Math.Pow(testE - cx, 2) + System.Math.Pow(testN - cy, 2);
                    sign = distToCenterTest < distToCenterOrig ? 1.0 : -1.0;
                }
            }
        }

        var result = new List<Vec3>();

        // Edge-based offset: shift each edge by offset distance, then intersect consecutive edges
        // This handles curves correctly (constant distance from boundary)
        var pts = segment.BoundaryPoints;
        int ptCount = pts.Count;

        if (ptCount == 2)
        {
            // Simple line: just shift both points by the perpendicular
            double dx = pts[1].Easting - pts[0].Easting;
            double dy = pts[1].Northing - pts[0].Northing;
            double len = System.Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) len = 1;
            double nx = sign * dy / len * offset;
            double ny = sign * -dx / len * offset;
            result.Add(new Vec3(pts[0].Easting + nx, pts[0].Northing + ny, pts[0].Heading));
            result.Add(new Vec3(pts[1].Easting + nx, pts[1].Northing + ny, pts[1].Heading));
        }
        else
        {
            // Build offset edges (each edge shifted by offset along its normal)
            var offEdges = new List<(double ax, double ay, double bx, double by)>();
            for (int i = 0; i < ptCount - 1; i++)
            {
                double dx = pts[i + 1].Easting - pts[i].Easting;
                double dy = pts[i + 1].Northing - pts[i].Northing;
                double len = System.Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.001) continue;
                double nx = sign * dy / len * offset;
                double ny = sign * -dx / len * offset;
                offEdges.Add((pts[i].Easting + nx, pts[i].Northing + ny,
                              pts[i + 1].Easting + nx, pts[i + 1].Northing + ny));
            }

            if (offEdges.Count == 0) { segment.OffsetPoints = result; return; }

            // First offset point
            result.Add(new Vec3(offEdges[0].ax, offEdges[0].ay, pts[0].Heading));

            // Intersect consecutive offset edges to find interior offset points
            for (int i = 0; i < offEdges.Count - 1; i++)
            {
                var e1 = offEdges[i];
                var e2 = offEdges[i + 1];

                if (LineLineIntersection(e1.ax, e1.ay, e1.bx, e1.by,
                                         e2.ax, e2.ay, e2.bx, e2.by,
                                         out double ix, out double iy))
                {
                    result.Add(new Vec3(ix, iy, pts[i + 1].Heading));
                }
                else
                {
                    // Parallel edges - use endpoint of first edge
                    result.Add(new Vec3(e1.bx, e1.by, pts[i + 1].Heading));
                }
            }

            // Last offset point
            var last = offEdges[^1];
            result.Add(new Vec3(last.bx, last.by, pts[^1].Heading));
        }

        // Remove self-intersections (inverted fillets when offset > corner radius)
        // Safe for all types since edge-based offset produces accurate distances
        segment.OffsetPoints = result.Count > 3 ? RemoveSelfIntersections(result) : result;
    }

    /// <summary>
    /// Remove self-intersecting loops from an offset polygon.
    /// When the offset is larger than a convex feature (like a fillet), the offset
    /// polygon inverts and crosses itself. This detects those crossings and removes
    /// the inverted loops.
    /// </summary>
    private static List<Vec3> RemoveSelfIntersections(List<Vec3> points)
    {
        if (points.Count < 4) return points;

        var clean = new List<Vec3> { points[0] };
        int i = 0;

        while (i < points.Count - 1)
        {
            // Check if any later edge crosses the current edge
            var a1 = points[i];
            var a2 = points[i + 1];

            int skipTo = -1;
            Vec3 intersectPt = default;

            // Check against all non-adjacent edges ahead
            for (int j = i + 2; j < points.Count - 1; j++)
            {
                var b1 = points[j];
                var b2 = points[j + 1];

                if (LineSegmentIntersection(
                    a1.Easting, a1.Northing, a2.Easting, a2.Northing,
                    b1.Easting, b1.Northing, b2.Easting, b2.Northing,
                    out double t, out double u))
                {
                    if (t > 0.01 && t < 0.99 && u > 0.01 && u < 0.99)
                    {
                        // Self-intersection found - skip the loop
                        double ix = a1.Easting + t * (a2.Easting - a1.Easting);
                        double iy = a1.Northing + t * (a2.Northing - a1.Northing);
                        intersectPt = new Vec3(ix, iy, a1.Heading);
                        skipTo = j + 1;
                        break; // Take first intersection
                    }
                }
            }

            if (skipTo >= 0)
            {
                // Add intersection point and skip the loop
                clean.Add(intersectPt);
                i = skipTo;
            }
            else
            {
                // No intersection, keep the next point
                i++;
                if (i < points.Count)
                    clean.Add(points[i]);
            }
        }

        return clean;
    }

    /// <summary>
    /// Build the headland polygon from segments. If a single Boundary segment exists,
    /// use its offset points directly. Otherwise, concatenate all segment offset points
    /// and check if they form a loop.
    /// </summary>
    public void BuildHeadlandFromSegments()
    {
        if (HeadlandSegments.Count == 0)
        {
            // Default: headland = boundary
            var boundary = _currentBoundary?.OuterBoundary;
            if (boundary?.Points != null && boundary.Points.Count >= 3)
            {
                var bndPoints = new List<Vec3>();
                foreach (var pt in boundary.Points)
                    bndPoints.Add(new Vec3(pt.Easting, pt.Northing, pt.Heading));
                bndPoints.Add(new Vec3(boundary.Points[0].Easting, boundary.Points[0].Northing, boundary.Points[0].Heading));

                _currentHeadlandLine = bndPoints;
                CurrentHeadlandLine = bndPoints;
                State.Field.HeadlandLine = bndPoints;
                HasHeadland = true;
                IsHeadlandOn = true;
                _mapService.SetHeadlandLine(bndPoints);
                _mapService.SetHeadlandVisible(true);
            }
            else
            {
                HasHeadland = false;
                IsHeadlandOn = false;
                _currentHeadlandLine = null;
                CurrentHeadlandLine = null;
                State.Field.HeadlandLine = null;
                _mapService.SetHeadlandVisible(false);
            }
            OnPropertyChanged(nameof(HeadlandStatusText));
            OnPropertyChanged(nameof(CurrentHeadlandLineForPreview));
            return;
        }

        // Start with headland = boundary
        var bnd = _currentBoundary?.OuterBoundary;
        if (bnd?.Points == null || bnd.Points.Count < 3)
        {
            StatusMessage = "No boundary for headland";
            return;
        }

        var headland = new List<Vec3>();
        foreach (var pt in bnd.Points)
            headland.Add(new Vec3(pt.Easting, pt.Northing, pt.Heading));
        headland.Add(new Vec3(bnd.Points[0].Easting, bnd.Points[0].Northing, bnd.Points[0].Heading));

        int cutsApplied = 0;

        // Build all offset lines with extensions
        // Track list of segments per chain (merging combines multiple segments)
        var offsetLines = new List<(List<Models.Headland.HeadlandSegment> segs, List<Vec3> line)>();
        foreach (var seg in HeadlandSegments)
        {
            if (seg.OffsetPoints.Count < 2) continue;

            // Detect closed-loop offset (boundary covers full polygon)
            // If first and last boundary points are the same, the offset is a closed polygon
            double bndCloseDist = seg.BoundaryPoints.Count >= 3
                ? System.Math.Sqrt(
                    System.Math.Pow(seg.BoundaryPoints[0].Easting - seg.BoundaryPoints[^1].Easting, 2) +
                    System.Math.Pow(seg.BoundaryPoints[0].Northing - seg.BoundaryPoints[^1].Northing, 2))
                : double.MaxValue;

            if (bndCloseDist < 5.0 && seg.OffsetPoints.Count >= 4)
            {
                // Closed boundary segment - use offset directly as headland polygon
                var closedPoly = new List<Vec3>(seg.OffsetPoints);
                closedPoly.Add(closedPoly[0]); // close the loop

                // Use centroid-based selection (same as closed loop handling)
                double cx = 0, cy = 0;
                var bndPts = bnd.Points;
                foreach (var bp in bndPts) { cx += bp.Easting; cy += bp.Northing; }
                cx /= bndPts.Count; cy /= bndPts.Count;

                bool containsCentroid = IsPointInPolygon(cx, cy, closedPoly);
                if (containsCentroid)
                {
                    headland = closedPoly;
                    seg.IsEffective = true;
                    cutsApplied++;
                    _logger.LogDebug($"[Headland] Closed boundary offset '{seg.Name}' used as headland ({closedPoly.Count} pts)");
                    continue;
                }
            }

            var ol = BuildOffsetLineWithExtensions(seg);
            offsetLines.Add((new List<Models.Headland.HeadlandSegment> { seg }, ol));
        }

        // Pre-check: which offset lines already individually reach the boundary on both ends
        // These should NOT be merged with other lines (merging could break them)
        var reachesBothEnds = new HashSet<int>();
        for (int i = 0; i < offsetLines.Count; i++)
        {
            var line = offsetLines[i].line;
            if (line.Count < 2) continue;
            int halfCk = System.Math.Max(2, line.Count / 2);

            bool startReaches = false;
            for (int oi = 0; oi < System.Math.Min(halfCk, line.Count - 1) && !startReaches; oi++)
                startReaches = FindLineHeadlandIntersection(line[oi], line[oi + 1], headland, out _) >= 0;

            bool endReaches = false;
            for (int oi = line.Count - 1; oi >= System.Math.Max(1, line.Count - halfCk) && !endReaches; oi--)
                endReaches = FindLineHeadlandIntersection(line[oi - 1], line[oi], headland, out _) >= 0;

            if (startReaches && endReaches) reachesBothEnds.Add(i);
        }

        // Try to merge chained offset lines that intersect each other
        // This handles the case where two lines each only touch boundary on one side
        // but connect to each other on the other side
        // Skip lines that already reach both boundary ends
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int a = 0; a < offsetLines.Count && !merged; a++)
            {
                if (reachesBothEnds.Contains(a)) continue;
                for (int b = a + 1; b < offsetLines.Count && !merged; b++)
                {
                    if (reachesBothEnds.Contains(b)) continue;
                    // Check if end of A intersects any segment of B
                    var lineA = offsetLines[a].line;
                    var lineB = offsetLines[b].line;

                    // Check segment pairs between A's ends and B for intersection
                    // Search from A's end backward to prefer extending the chain (not cutting it short)
                    Vec3 intersectPt = default;
                    bool found = false;
                    int aSegIdx = -1;
                    for (int ai = lineA.Count - 2; ai >= 0 && !found; ai--)
                    {
                        for (int bi = 0; bi < lineB.Count - 1 && !found; bi++)
                        {
                            if (LineSegmentIntersection(
                                lineA[ai].Easting, lineA[ai].Northing, lineA[ai + 1].Easting, lineA[ai + 1].Northing,
                                lineB[bi].Easting, lineB[bi].Northing, lineB[bi + 1].Easting, lineB[bi + 1].Northing,
                                out double t, out double u))
                            {
                                if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
                                {
                                    intersectPt = new Vec3(
                                        lineB[bi].Easting + u * (lineB[bi + 1].Easting - lineB[bi].Easting),
                                        lineB[bi].Northing + u * (lineB[bi + 1].Northing - lineB[bi].Northing), 0);
                                    found = true;
                                    aSegIdx = ai;
                                }
                            }
                        }
                    }

                    if (found)
                    {
                        // Trim A at intersection: keep A[0..aSegIdx] + intersectPt
                        var trimmedA = new List<Vec3>();
                        for (int k = 0; k <= aSegIdx; k++)
                            trimmedA.Add(lineA[k]);
                        trimmedA.Add(intersectPt);

                        // Find which segment of B contains the intersection and trim B too
                        int bSegIdx = -1;
                        for (int bi = 0; bi < lineB.Count - 1; bi++)
                        {
                            if (LineSegmentIntersection(
                                lineA[aSegIdx].Easting, lineA[aSegIdx].Northing, lineA[aSegIdx + 1].Easting, lineA[aSegIdx + 1].Northing,
                                lineB[bi].Easting, lineB[bi].Northing, lineB[bi + 1].Easting, lineB[bi + 1].Northing,
                                out double t2, out double u2))
                            {
                                if (t2 >= 0 && t2 <= 1 && u2 >= 0 && u2 <= 1)
                                {
                                    bSegIdx = bi;
                                    break;
                                }
                            }
                        }

                        // Determine which end of B to connect from and trim at intersection
                        double dBStart = System.Math.Pow(lineB[0].Easting - intersectPt.Easting, 2) + System.Math.Pow(lineB[0].Northing - intersectPt.Northing, 2);
                        double dBEnd = System.Math.Pow(lineB[^1].Easting - intersectPt.Easting, 2) + System.Math.Pow(lineB[^1].Northing - intersectPt.Northing, 2);

                        var combined = new List<Vec3>(trimmedA);
                        if (dBStart < dBEnd)
                        {
                            // B goes forward from intersection: trim B start, keep bSegIdx+1..end
                            if (bSegIdx >= 0)
                                for (int k = bSegIdx + 1; k < lineB.Count; k++)
                                    combined.Add(lineB[k]);
                            else
                                combined.AddRange(lineB);
                        }
                        else
                        {
                            // B goes backward from intersection: trim B end, keep 0..bSegIdx reversed
                            if (bSegIdx >= 0)
                                for (int k = bSegIdx; k >= 0; k--)
                                    combined.Add(lineB[k]);
                            else
                            {
                                var rev = new List<Vec3>(lineB);
                                rev.Reverse();
                                combined.AddRange(rev);
                            }
                        }

                        var mergedSegs = new List<Models.Headland.HeadlandSegment>(offsetLines[a].segs);
                        mergedSegs.AddRange(offsetLines[b].segs);
                        offsetLines[a] = (mergedSegs, combined);
                        offsetLines.RemoveAt(b);
                        merged = true;
                        _logger.LogDebug($"[Headland] Merged offset lines: {mergedSegs.Count} segments combined");
                    }
                }
            }
        }

        // Check for closed loops: merged chains whose start and end meet
        // These can divide the polygon without touching the boundary
        // First try to close near-miss loops by finding where end segments intersect start segments
        for (int ci = 0; ci < offsetLines.Count; ci++)
        {
            var (closedSegs, closedLine) = offsetLines[ci];
            if (closedLine.Count < 6 || closedSegs.Count < 2) continue; // Need multiple merged segments

            double loopDist = System.Math.Sqrt(
                System.Math.Pow(closedLine[0].Easting - closedLine[^1].Easting, 2) +
                System.Math.Pow(closedLine[0].Northing - closedLine[^1].Northing, 2));

            if (loopDist >= 5.0)
            {
                // Ends don't meet directly - check if end segments intersect start segments
                // This handles the case where extensions overshoot past the closing corner
                int searchRange = System.Math.Max(3, closedLine.Count / 2);
                Vec3 closeIntersectPt = default;
                int startSegClose = -1, endSegClose = -1;

                for (int si = 0; si < System.Math.Min(searchRange, closedLine.Count / 2) && startSegClose < 0; si++)
                {
                    int endSearchStart = System.Math.Max(closedLine.Count / 2, si + 2);
                    for (int ei = endSearchStart; ei < closedLine.Count - 1 && startSegClose < 0; ei++)
                    {
                        if (LineSegmentIntersection(
                            closedLine[si].Easting, closedLine[si].Northing, closedLine[si + 1].Easting, closedLine[si + 1].Northing,
                            closedLine[ei].Easting, closedLine[ei].Northing, closedLine[ei + 1].Easting, closedLine[ei + 1].Northing,
                            out double t, out double u))
                        {
                            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
                            {
                                closeIntersectPt = new Vec3(
                                    closedLine[si].Easting + t * (closedLine[si + 1].Easting - closedLine[si].Easting),
                                    closedLine[si].Northing + t * (closedLine[si + 1].Northing - closedLine[si].Northing), 0);
                                startSegClose = si;
                                endSegClose = ei;
                            }
                        }
                    }
                }

                if (startSegClose >= 0)
                {
                    // Trim both ends at the intersection to close the loop
                    var trimmed = new List<Vec3> { closeIntersectPt };
                    for (int k = startSegClose + 1; k <= endSegClose; k++)
                        trimmed.Add(closedLine[k]);
                    trimmed.Add(closeIntersectPt);

                    offsetLines[ci] = (closedSegs, trimmed);
                    closedLine = trimmed;
                    loopDist = 0; // Now it's a closed loop
                    _logger.LogDebug($"[Headland] Closed loop by trimming extensions at intersection ({closeIntersectPt.Easting:F1},{closeIntersectPt.Northing:F1})");
                }
            }

            if (loopDist < 5.0) // Close enough to form a loop
            {
                // This is a closed loop - use it directly as a divider
                var loopPoly = new List<Vec3>(closedLine);
                loopPoly.Add(loopPoly[0]); // close

                // The headland is the boundary MINUS the loop area
                // Keep the part containing the centroid
                double cx = 0, cy = 0;
                var bndPts = bnd.Points;
                foreach (var bp in bndPts) { cx += bp.Easting; cy += bp.Northing; }
                cx /= bndPts.Count; cy /= bndPts.Count;

                bool loopContainsCentroid = IsPointInPolygon(cx, cy, loopPoly);

                if (!loopContainsCentroid)
                {
                    // Loop is outside the centroid - headland is unchanged
                    // (the loop is in the headland area, not the working area)
                    foreach (var cs in closedSegs) cs.IsEffective = true;
                    _logger.LogDebug($"[Headland] Closed loop found (outside centroid) - headland unchanged");
                }
                else
                {
                    // Loop contains centroid - the loop IS the headland
                    headland = loopPoly;
                    foreach (var cs in closedSegs) cs.IsEffective = true;
                    cutsApplied++;
                    _logger.LogDebug($"[Headland] Closed loop contains centroid - used as headland");
                }

                offsetLines.RemoveAt(ci);
                ci--;
            }
        }

        // Process each offset line (possibly merged chains)
        foreach (var (segs, offsetLine) in offsetLines)
        {
            var seg = segs[0]; // Primary segment for logging

            // Find intersection of offset line with headland polygon
            // Search from each end, limited to half the line to avoid finding the wrong end
            int halfCount = System.Math.Max(2, offsetLine.Count / 2);

            int startIntersectIdx = -1;
            Vec3 startIntersectPt = default;
            int startOffsetSegIdx = -1; // Which offset line segment the start intersection is on
            for (int oi = 0; oi < System.Math.Min(halfCount, offsetLine.Count - 1) && startIntersectIdx < 0; oi++)
            {
                startIntersectIdx = FindLineHeadlandIntersection(offsetLine[oi], offsetLine[oi + 1], headland, out startIntersectPt);
                if (startIntersectIdx >= 0) startOffsetSegIdx = oi;
            }

            int endIntersectIdx = -1;
            Vec3 endIntersectPt = default;
            int endOffsetSegIdx = -1; // Which offset line segment the end intersection is on
            int endStart = System.Math.Max(1, offsetLine.Count - halfCount);
            // Search from end backward, finding the FARTHEST intersection from segment start
            // (opposite of start search which finds closest). This ensures start and end
            // find different intersections even when checking the same offset segment.
            for (int oi = offsetLine.Count - 1; oi >= endStart && endIntersectIdx < 0; oi--)
            {
                endIntersectIdx = FindLineHeadlandIntersectionFarthest(offsetLine[oi - 1], offsetLine[oi], headland, out endIntersectPt);
                if (endIntersectIdx >= 0) endOffsetSegIdx = oi - 1;
            }

            _logger.LogDebug($"[Headland] Segment '{seg.Name}': start intersect={startIntersectIdx}, end intersect={endIntersectIdx}, offsetLine pts={offsetLine.Count}, headland pts={headland.Count}");

            // Check that both ends intersect at different locations
            // Allow same headland segment if intersection points are far apart
            bool intersectionsFarEnough = false;
            if (startIntersectIdx >= 0 && endIntersectIdx >= 0)
            {
                if (startIntersectIdx != endIntersectIdx)
                {
                    intersectionsFarEnough = true;
                }
                else
                {
                    double ptDist = System.Math.Sqrt(
                        System.Math.Pow(startIntersectPt.Easting - endIntersectPt.Easting, 2) +
                        System.Math.Pow(startIntersectPt.Northing - endIntersectPt.Northing, 2));
                    intersectionsFarEnough = ptDist > 1.0; // At least 1m apart
                }
            }
            foreach (var s in segs) s.IsEffective = intersectionsFarEnough;

            if (intersectionsFarEnough)
            {
                // Both ends intersect - split the polygon into two halves
                int count = headland.Count - 1; // exclude closing duplicate

                // The dividing line goes from startIntersectPt along the offset line to endIntersectPt
                // Include all offset line interior points between the two intersection segments
                var divLine = new List<Vec3> { startIntersectPt };
                if (startOffsetSegIdx >= 0 && endOffsetSegIdx >= 0 && endOffsetSegIdx > startOffsetSegIdx)
                {
                    // Add offset line points between the two intersection segments
                    for (int j = startOffsetSegIdx + 1; j <= endOffsetSegIdx; j++)
                        divLine.Add(offsetLine[j]);
                }
                divLine.Add(endIntersectPt);

                var divLineReverse = new List<Vec3>(divLine);
                divLineReverse.Reverse();

                var pathA = new List<Vec3>();
                var pathB = new List<Vec3>();
                int idx;

                if (startIntersectIdx == endIntersectIdx)
                {
                    // Both intersections on the same headland segment
                    // pathA: just the direct connection between the two points
                    pathA.Add(startIntersectPt);
                    pathA.Add(endIntersectPt);

                    // pathB: walk the entire headland polygon
                    pathB.Add(endIntersectPt);
                    idx = (endIntersectIdx + 1) % count;
                    for (int step = 0; step < count; step++)
                    {
                        pathB.Add(headland[idx]);
                        idx = (idx + 1) % count;
                    }
                    pathB.Add(startIntersectPt);
                }
                else
                {
                    // Path A: start at startIntersectPt, walk headland forward to endIntersectPt
                    idx = (startIntersectIdx + 1) % count;
                    pathA.Add(startIntersectPt);
                    while (idx != (endIntersectIdx + 1) % count)
                    {
                        pathA.Add(headland[idx]);
                        idx = (idx + 1) % count;
                        if (pathA.Count > count + 2) break;
                    }
                    pathA.Add(endIntersectPt);

                    // Path B: start at endIntersectPt, walk headland forward to startIntersectPt
                    idx = (endIntersectIdx + 1) % count;
                    pathB.Add(endIntersectPt);
                    while (idx != (startIntersectIdx + 1) % count)
                    {
                        pathB.Add(headland[idx]);
                        idx = (idx + 1) % count;
                        if (pathB.Count > count + 2) break;
                    }
                    pathB.Add(startIntersectPt);
                }

                // Complete each polygon by adding the dividing line
                // pathA + divLineReverse forms polygon A
                // pathB + divLine forms polygon B
                var polyA = new List<Vec3>(pathA);
                polyA.AddRange(divLineReverse);
                var polyB = new List<Vec3>(pathB);
                polyB.AddRange(divLine);

                // Pick the polygon that contains the field centroid (= working area)
                double cx = 0, cy = 0;
                var bndPts = bnd.Points;
                foreach (var bp in bndPts) { cx += bp.Easting; cy += bp.Northing; }
                cx /= bndPts.Count; cy /= bndPts.Count;

                bool aContains = IsPointInPolygon(cx, cy, polyA);
                bool bContains = IsPointInPolygon(cx, cy, polyB);

                _logger.LogDebug($"[Headland] PathA: {polyA.Count} pts, PathB: {polyB.Count} pts, centroid: ({cx:F1},{cy:F1}), aContains={aContains}, bContains={bContains}");

                List<Vec3> chosen;
                if (aContains && !bContains) chosen = polyA;
                else if (bContains && !aContains) chosen = polyB;
                else chosen = System.Math.Abs(CalculateSignedArea(polyA)) >= System.Math.Abs(CalculateSignedArea(polyB)) ? polyA : polyB;
                if (chosen.Count > 0)
                {
                    // Remove consecutive duplicate points
                    for (int d = chosen.Count - 1; d > 0; d--)
                    {
                        double ddx = chosen[d].Easting - chosen[d-1].Easting;
                        double ddy = chosen[d].Northing - chosen[d-1].Northing;
                        if (ddx * ddx + ddy * ddy < 0.01) chosen.RemoveAt(d);
                    }
                    chosen.Add(chosen[0]); // close loop
                }

                headland = chosen;
                cutsApplied++;
            }
        }

        // Apply headland
        _currentHeadlandLine = headland;
        CurrentHeadlandLine = headland;
        State.Field.HeadlandLine = headland;
        HasHeadland = true;
        IsHeadlandOn = true;
        _mapService.SetHeadlandLine(headland);
        _mapService.SetHeadlandVisible(true);

        OnPropertyChanged(nameof(HeadlandStatusText));
        OnPropertyChanged(nameof(CurrentHeadlandLineForPreview));
        // Log headland points for debugging
        for (int p = 0; p < headland.Count; p++)
            _logger.LogDebug($"[Headland] Point {p}: E={headland[p].Easting:F1} N={headland[p].Northing:F1}");

        StatusMessage = cutsApplied > 0
            ? $"Headland modified ({cutsApplied} cuts, {headland.Count} points)"
            : $"Headland = boundary ({headland.Count} points, no offset lines intersect)";

        SaveHeadlandSegments();
    }

    private static List<Vec3> BuildOffsetLineWithExtensions(Models.Headland.HeadlandSegment seg)
    {
        var line = new List<Vec3>();
        if (seg.StartExtension > 0 && seg.OffsetPoints.Count >= 2)
        {
            var s0 = seg.OffsetPoints[0]; var s1 = seg.OffsetPoints[1];
            double sdx = s0.Easting - s1.Easting, sdy = s0.Northing - s1.Northing;
            double slen = System.Math.Sqrt(sdx * sdx + sdy * sdy);
            if (slen > 0.01)
                line.Add(new Vec3(s0.Easting + sdx / slen * seg.StartExtension, s0.Northing + sdy / slen * seg.StartExtension, s0.Heading));
        }
        line.AddRange(seg.OffsetPoints);
        if (seg.EndExtension > 0 && seg.OffsetPoints.Count >= 2)
        {
            var e0 = seg.OffsetPoints[^2]; var e1 = seg.OffsetPoints[^1];
            double edx = e1.Easting - e0.Easting, edy = e1.Northing - e0.Northing;
            double elen = System.Math.Sqrt(edx * edx + edy * edy);
            if (elen > 0.01)
                line.Add(new Vec3(e1.Easting + edx / elen * seg.EndExtension, e1.Northing + edy / elen * seg.EndExtension, e1.Heading));
        }
        return line;
    }

    /// <summary>Intersect two infinite lines. Returns false if parallel.</summary>
    private static bool LineLineIntersection(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy,
        out double ix, out double iy)
    {
        double denom = (bx - ax) * (dy - cy) - (by - ay) * (dx - cx);
        ix = iy = 0;
        if (System.Math.Abs(denom) < 1e-10) return false;
        double t = ((cx - ax) * (dy - cy) - (cy - ay) * (dx - cx)) / denom;
        ix = ax + t * (bx - ax);
        iy = ay + t * (by - ay);
        return true;
    }

    private static bool IsPointInPolygon(double px, double py, List<Vec3> polygon)
    {
        bool inside = false;
        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            double yi = polygon[i].Northing, yj = polygon[j].Northing;
            double xi = polygon[i].Easting, xj = polygon[j].Easting;
            if (((yi > py) != (yj > py)) && (px < (xj - xi) * (py - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }

    private static double CalculateSignedArea(List<Vec3> polygon)
    {
        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            area += (p2.Easting - p1.Easting) * (p2.Northing + p1.Northing);
        }
        return area / 2.0;
    }

    /// <summary>
    /// Find where a line segment (from lineStart toward lineDir) intersects the headland polygon.
    /// Returns the index of the headland segment where intersection occurs, or -1 if no intersection.
    /// </summary>
    private static int FindLineHeadlandIntersection(Vec3 lineStart, Vec3 lineDir, List<Vec3> headland, out Vec3 intersectionPoint)
    {
        double bestDist = double.MaxValue;
        int bestIdx = -1;
        intersectionPoint = default;

        for (int i = 0; i < headland.Count - 1; i++)
        {
            var p1 = headland[i];
            var p2 = headland[i + 1];

            if (LineSegmentIntersection(
                lineStart.Easting, lineStart.Northing, lineDir.Easting, lineDir.Northing,
                p1.Easting, p1.Northing, p2.Easting, p2.Northing,
                out double t, out double u))
            {
                if (t >= 0 && t <= 1) // Segment from lineStart to lineDir (extension tip)
                {
                    // Distance from lineStart to intersection
                    double dx = lineDir.Easting - lineStart.Easting;
                    double dy = lineDir.Northing - lineStart.Northing;
                    double dist = System.Math.Sqrt(dx * dx + dy * dy) * t;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                        // Compute actual intersection point on the headland segment
                        var hp1 = headland[i];
                        var hp2 = headland[i + 1];
                        intersectionPoint = new Vec3(
                            hp1.Easting + u * (hp2.Easting - hp1.Easting),
                            hp1.Northing + u * (hp2.Northing - hp1.Northing),
                            hp1.Heading);
                    }
                }
            }
        }

        return bestIdx;
    }

    /// <summary>Same as FindLineHeadlandIntersection but returns the FARTHEST intersection from lineStart.</summary>
    private static int FindLineHeadlandIntersectionFarthest(Vec3 lineStart, Vec3 lineDir, List<Vec3> headland, out Vec3 intersectionPoint)
    {
        double bestDist = -1;
        int bestIdx = -1;
        intersectionPoint = default;

        for (int i = 0; i < headland.Count - 1; i++)
        {
            var p1 = headland[i];
            var p2 = headland[i + 1];

            if (LineSegmentIntersection(
                lineStart.Easting, lineStart.Northing, lineDir.Easting, lineDir.Northing,
                p1.Easting, p1.Northing, p2.Easting, p2.Northing,
                out double t, out double u))
            {
                if (t >= 0 && t <= 1)
                {
                    double dx = lineDir.Easting - lineStart.Easting;
                    double dy = lineDir.Northing - lineStart.Northing;
                    double dist = System.Math.Sqrt(dx * dx + dy * dy) * t;
                    if (dist > bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                        intersectionPoint = new Vec3(
                            headland[i].Easting + u * (headland[i + 1].Easting - headland[i].Easting),
                            headland[i].Northing + u * (headland[i + 1].Northing - headland[i].Northing),
                            headland[i].Heading);
                    }
                }
            }
        }

        return bestIdx;
    }

    private static bool LineSegmentIntersection(
        double ax, double ay, double bx, double by,
        double cx, double cy, double dx, double dy,
        out double t, out double u)
    {
        double denom = (bx - ax) * (dy - cy) - (by - ay) * (dx - cx);
        t = u = 0;
        if (System.Math.Abs(denom) < 1e-10) return false;

        t = ((cx - ax) * (dy - cy) - (cy - ay) * (dx - cx)) / denom;
        u = ((cx - ax) * (by - ay) - (cy - ay) * (bx - ax)) / denom;

        return u >= 0 && u <= 1; // u is the parameter on the headland segment
    }

    private void SaveHeadlandSegments()
    {
        if (!IsFieldOpen || string.IsNullOrEmpty(CurrentFieldName)) return;
        try
        {
            var fieldsDir = _settingsService.Settings.FieldsDirectory;
            if (string.IsNullOrEmpty(fieldsDir))
                fieldsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AgValoniaGPS", "Fields");
            var fieldPath = Path.Combine(fieldsDir, CurrentFieldName);
            Services.Headland.HeadlandSegmentFileService.Save(fieldPath, HeadlandSegments);
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug($"[Headland] Failed to save headland segments: {ex.Message}");
        }
    }

}
