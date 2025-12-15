using System.Collections.Generic;
using System.Linq;
using AgValoniaGPS.Models.Base;
using Clipper2Lib;

namespace AgValoniaGPS.Services.Geometry;

/// <summary>
/// Service for creating offset polygons using Clipper2 library.
/// Used for headland generation from field boundaries.
/// </summary>
public class PolygonOffsetService : IPolygonOffsetService
{
    // Clipper2 uses integer coordinates, so we scale by this factor for precision
    private const double Scale = 1000.0;

    /// <summary>
    /// Create an inward offset polygon from a boundary.
    /// Offsets ALL points inward to preserve curve shape, then cleans up with Clipper.
    /// </summary>
    /// <param name="boundaryPoints">Outer boundary points</param>
    /// <param name="offsetDistance">Inward offset distance in meters (positive = inward/shrink)</param>
    /// <param name="joinType">How to handle corners (not used)</param>
    /// <returns>Offset polygon points, or null if offset collapses the polygon</returns>
    public List<Vec2>? CreateInwardOffset(List<Vec2> boundaryPoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        if (boundaryPoints == null || boundaryPoints.Count < 3)
            return null;

        if (offsetDistance <= 0)
            return new List<Vec2>(boundaryPoints);

        int n = boundaryPoints.Count;

        // Determine winding direction
        double signedArea = CalculateSignedArea(boundaryPoints);
        bool isCCW = signedArea > 0;
        double sign = isCCW ? 1.0 : -1.0;

        // Step 1: Move EVERY point inward along its local perpendicular
        var offsetPoints = new List<Vec2>(n);

        for (int i = 0; i < n; i++)
        {
            var prev = boundaryPoints[(i - 1 + n) % n];
            var curr = boundaryPoints[i];
            var next = boundaryPoints[(i + 1) % n];

            // Calculate perpendicular direction at this point
            // Use the average of the perpendiculars of the two adjacent edges
            double dx1 = curr.Easting - prev.Easting;
            double dy1 = curr.Northing - prev.Northing;
            double len1 = System.Math.Sqrt(dx1 * dx1 + dy1 * dy1);

            double dx2 = next.Easting - curr.Easting;
            double dy2 = next.Northing - curr.Northing;
            double len2 = System.Math.Sqrt(dx2 * dx2 + dy2 * dy2);

            // Normalize edge directions
            if (len1 > 0.001) { dx1 /= len1; dy1 /= len1; }
            if (len2 > 0.001) { dx2 /= len2; dy2 /= len2; }

            // Left perpendiculars: (-dy, dx)
            // Average of the two perpendiculars gives the inward direction
            double perpX = (-dy1 + -dy2) / 2.0;
            double perpY = (dx1 + dx2) / 2.0;
            double perpLen = System.Math.Sqrt(perpX * perpX + perpY * perpY);

            if (perpLen < 0.001)
            {
                // Fallback for collinear points
                perpX = -dy1;
                perpY = dx1;
                perpLen = 1.0;
            }
            else
            {
                perpX /= perpLen;
                perpY /= perpLen;
            }

            // Apply sign based on winding direction
            double inwardX = perpX * sign;
            double inwardY = perpY * sign;

            // Move point inward
            double newX = curr.Easting + inwardX * offsetDistance;
            double newY = curr.Northing + inwardY * offsetDistance;
            offsetPoints.Add(new Vec2(newX, newY));
        }

        // Step 2: Use Clipper to clean up any self-intersections
        var cleaned = CleanupSelfIntersections(offsetPoints);

        if (cleaned == null || cleaned.Count < 3)
            return offsetPoints;

        // Step 3: Round sharp corners that resulted from the cleanup
        var rounded = RoundSharpCorners(cleaned, offsetDistance * 0.8);

        // Step 4: Ensure polygon is properly closed (no gaps)
        var closed = EnsurePolygonClosed(rounded);

        return closed;
    }

    /// <summary>
    /// Round sharp corners in a polygon by replacing them with arc segments.
    /// Then apply light Chaikin smoothing to remaining angular areas.
    /// </summary>
    private List<Vec2> RoundSharpCorners(List<Vec2> polygon, double cornerRadius)
    {
        if (polygon.Count < 3 || cornerRadius <= 0)
            return polygon;

        var result = new List<Vec2>();
        int n = polygon.Count;

        // Threshold for what we consider a "sharp" corner (in radians)
        // ~20 degrees = catch corners that need rounding
        const double sharpAngleThreshold = 20.0 * System.Math.PI / 180.0;

        for (int i = 0; i < n; i++)
        {
            var prev = polygon[(i - 1 + n) % n];
            var curr = polygon[i];
            var next = polygon[(i + 1) % n];

            // Calculate vectors
            double v1x = curr.Easting - prev.Easting;
            double v1y = curr.Northing - prev.Northing;
            double v2x = next.Easting - curr.Easting;
            double v2y = next.Northing - curr.Northing;

            double len1 = System.Math.Sqrt(v1x * v1x + v1y * v1y);
            double len2 = System.Math.Sqrt(v2x * v2x + v2y * v2y);

            if (len1 < 0.001 || len2 < 0.001)
            {
                result.Add(curr);
                continue;
            }

            // Normalize
            v1x /= len1; v1y /= len1;
            v2x /= len2; v2y /= len2;

            // Calculate turn angle using cross product (gives signed angle)
            double cross = v1x * v2y - v1y * v2x;
            double dot = v1x * v2x + v1y * v2y;
            double angle = System.Math.Abs(System.Math.Atan2(cross, dot));

            // If corner is sharp enough, add arc
            if (angle > sharpAngleThreshold)
            {
                // Calculate how far back from corner to start the arc
                double halfAngle = angle / 2.0;
                double tangentLength = cornerRadius / System.Math.Tan(halfAngle);

                // Clamp tangent length to not exceed half the edge lengths
                tangentLength = System.Math.Min(tangentLength, len1 * 0.45);
                tangentLength = System.Math.Min(tangentLength, len2 * 0.45);

                // Arc start point (on incoming edge)
                double arcStartX = curr.Easting - v1x * tangentLength;
                double arcStartY = curr.Northing - v1y * tangentLength;

                // Arc end point (on outgoing edge)
                double arcEndX = curr.Easting + v2x * tangentLength;
                double arcEndY = curr.Northing + v2y * tangentLength;

                // Calculate arc center
                double bisectX = -v1x + v2x;
                double bisectY = -v1y + v2y;
                double bisectLen = System.Math.Sqrt(bisectX * bisectX + bisectY * bisectY);

                if (bisectLen < 0.001)
                {
                    result.Add(curr);
                    continue;
                }

                bisectX /= bisectLen;
                bisectY /= bisectLen;

                // Distance from corner to arc center
                double actualRadius = tangentLength * System.Math.Tan(halfAngle);
                double distToCenter = actualRadius / System.Math.Sin(halfAngle);

                // Arc center - on the inside of the turn
                double centerX = curr.Easting + bisectX * distToCenter;
                double centerY = curr.Northing + bisectY * distToCenter;

                // Generate arc points
                double startAngle = System.Math.Atan2(arcStartY - centerY, arcStartX - centerX);
                double endAngle = System.Math.Atan2(arcEndY - centerY, arcEndX - centerX);

                // Determine sweep direction (should go the short way)
                double sweep = endAngle - startAngle;
                while (sweep > System.Math.PI) sweep -= 2 * System.Math.PI;
                while (sweep < -System.Math.PI) sweep += 2 * System.Math.PI;

                // Number of arc segments based on angle
                int numSegments = System.Math.Max(3, (int)(System.Math.Abs(sweep) / (15.0 * System.Math.PI / 180.0)));

                for (int j = 0; j <= numSegments; j++)
                {
                    double t = (double)j / numSegments;
                    double a = startAngle + t * sweep;
                    double x = centerX + actualRadius * System.Math.Cos(a);
                    double y = centerY + actualRadius * System.Math.Sin(a);
                    result.Add(new Vec2(x, y));
                }
            }
            else
            {
                // Not sharp, just add the point
                result.Add(curr);
            }
        }

        return result;
    }

    /// <summary>
    /// Ensure polygon is properly closed with no gaps between consecutive points.
    /// </summary>
    private List<Vec2> EnsurePolygonClosed(List<Vec2> polygon)
    {
        if (polygon.Count < 3)
            return polygon;

        var result = new List<Vec2>(polygon.Count);

        // Maximum allowed gap between consecutive points (in meters)
        const double maxGap = 2.0;

        for (int i = 0; i < polygon.Count; i++)
        {
            var curr = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];

            result.Add(curr);

            // Check distance to next point
            double dx = next.Easting - curr.Easting;
            double dy = next.Northing - curr.Northing;
            double dist = System.Math.Sqrt(dx * dx + dy * dy);

            // If gap is too large, add interpolated points to close it
            if (dist > maxGap)
            {
                int numInterpolated = (int)(dist / maxGap);
                for (int j = 1; j <= numInterpolated; j++)
                {
                    double t = (double)j / (numInterpolated + 1);
                    double x = curr.Easting + t * dx;
                    double y = curr.Northing + t * dy;
                    result.Add(new Vec2(x, y));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Use Clipper's inflate then deflate trick to round angular corners.
    /// Expanding with round join adds arc at corners, deflating shrinks back.
    /// </summary>
    private List<Vec2> InflateDeflateTrick(List<Vec2> polygon, double amount)
    {
        if (polygon.Count < 3 || amount <= 0)
            return polygon;

        // Convert to Clipper path
        var path = new Path64(polygon.Count);
        foreach (var pt in polygon)
        {
            path.Add(new Point64((long)(pt.Easting * Scale), (long)(pt.Northing * Scale)));
        }

        // Step 1: Inflate with round join (adds rounded corners)
        var clipperOffset = new ClipperOffset();
        clipperOffset.ArcTolerance = Scale * 0.1; // Small arc tolerance for smooth curves

        clipperOffset.AddPath(path, JoinType.Round, EndType.Polygon);

        var inflated = new Paths64();
        clipperOffset.Execute(amount * Scale, inflated);

        if (inflated.Count == 0 || inflated[0].Count < 3)
            return polygon;

        // Step 2: Deflate back to original size
        clipperOffset.Clear();
        clipperOffset.AddPath(inflated[0], JoinType.Round, EndType.Polygon);

        var deflated = new Paths64();
        clipperOffset.Execute(-amount * Scale, deflated);

        if (deflated.Count == 0 || deflated[0].Count < 3)
            return polygon;

        // Convert back to Vec2
        var result = new List<Vec2>(deflated[0].Count);
        foreach (var pt in deflated[0])
        {
            result.Add(new Vec2(pt.X / Scale, pt.Y / Scale));
        }

        return result;
    }

    /// <summary>
    /// Smooth the boundary before offset to remove GPS noise from curves.
    /// Resamples at even intervals to create cleaner corner geometry.
    /// </summary>
    private List<Vec2> SmoothBoundaryForOffset(List<Vec2> boundary)
    {
        if (boundary.Count < 10)
            return boundary;

        // Step 1: Resample at even spacing (removes GPS jitter)
        double targetSpacing = 2.0; // 2m between points
        var resampled = ResamplePolygon(boundary, targetSpacing);

        // Step 2: Apply moving average to smooth coordinates
        const int smoothWindow = 2;
        int n = resampled.Count;
        var smoothed = new List<Vec2>(n);

        for (int i = 0; i < n; i++)
        {
            double sumX = 0, sumY = 0;
            int count = 0;

            for (int j = -smoothWindow; j <= smoothWindow; j++)
            {
                int idx = (i + j + n) % n;
                sumX += resampled[idx].Easting;
                sumY += resampled[idx].Northing;
                count++;
            }

            smoothed.Add(new Vec2(sumX / count, sumY / count));
        }

        return smoothed;
    }

    /// <summary>
    /// Apply one iteration of Chaikin corner-cutting smoothing.
    /// </summary>
    private List<Vec2> ChaikinSmoothOnce(List<Vec2> polygon)
    {
        if (polygon.Count < 3)
            return polygon;

        var result = new List<Vec2>(polygon.Count * 2);

        for (int i = 0; i < polygon.Count; i++)
        {
            var p0 = polygon[i];
            var p1 = polygon[(i + 1) % polygon.Count];

            // Chaikin uses 1/4 and 3/4 points along each edge
            double q0x = 0.75 * p0.Easting + 0.25 * p1.Easting;
            double q0y = 0.75 * p0.Northing + 0.25 * p1.Northing;
            double q1x = 0.25 * p0.Easting + 0.75 * p1.Easting;
            double q1y = 0.25 * p0.Northing + 0.75 * p1.Northing;

            result.Add(new Vec2(q0x, q0y));
            result.Add(new Vec2(q1x, q1y));
        }

        return result;
    }

    /// <summary>
    /// Add arc points at sharp corners to smooth them.
    /// For deflation, Clipper doesn't add arcs, so we do it manually.
    /// </summary>
    private List<Vec2> AddCornerArcs(List<Vec2> polygon, double radius)
    {
        if (polygon.Count < 3)
            return polygon;

        var result = new List<Vec2>();
        int n = polygon.Count;
        double minAngleForArc = 20.0 * System.Math.PI / 180.0; // 20 degrees

        for (int i = 0; i < n; i++)
        {
            var prev = polygon[(i - 1 + n) % n];
            var curr = polygon[i];
            var next = polygon[(i + 1) % n];

            // Calculate vectors
            double v1x = curr.Easting - prev.Easting;
            double v1y = curr.Northing - prev.Northing;
            double v2x = next.Easting - curr.Easting;
            double v2y = next.Northing - curr.Northing;

            // Normalize
            double len1 = System.Math.Sqrt(v1x * v1x + v1y * v1y);
            double len2 = System.Math.Sqrt(v2x * v2x + v2y * v2y);

            if (len1 < 0.001 || len2 < 0.001)
            {
                result.Add(curr);
                continue;
            }

            v1x /= len1; v1y /= len1;
            v2x /= len2; v2y /= len2;

            // Calculate angle between vectors
            double dot = v1x * v2x + v1y * v2y;
            dot = System.Math.Clamp(dot, -1.0, 1.0);
            double angle = System.Math.Acos(dot);

            // If corner is sharp enough, add arc points
            if (angle > minAngleForArc)
            {
                // Calculate arc center and insert arc points
                // The arc should be tangent to both edges at distance 'radius' from corner

                // Bisector direction (points inward for convex corner)
                double bisectX = v1x + v2x;
                double bisectY = v1y + v2y;
                double bisectLen = System.Math.Sqrt(bisectX * bisectX + bisectY * bisectY);

                if (bisectLen > 0.001)
                {
                    bisectX /= bisectLen;
                    bisectY /= bisectLen;

                    // Distance from corner to arc center
                    double halfAngle = angle / 2.0;
                    double distToCenter = radius / System.Math.Sin(halfAngle);

                    // Arc center
                    double centerX = curr.Easting - bisectX * distToCenter;
                    double centerY = curr.Northing - bisectY * distToCenter;

                    // Start and end angles for arc
                    double startAngle = System.Math.Atan2(-v1y, -v1x); // perpendicular to incoming edge
                    double endAngle = System.Math.Atan2(v2y, v2x); // perpendicular to outgoing edge

                    // Adjust angles to go the short way
                    double sweep = endAngle - startAngle;
                    while (sweep > System.Math.PI) sweep -= 2 * System.Math.PI;
                    while (sweep < -System.Math.PI) sweep += 2 * System.Math.PI;

                    // Number of arc segments based on angle
                    int arcSegments = System.Math.Max(3, (int)(System.Math.Abs(sweep) / (10.0 * System.Math.PI / 180.0)));

                    for (int j = 0; j <= arcSegments; j++)
                    {
                        double t = (double)j / arcSegments;
                        double a = startAngle + t * sweep;
                        double x = centerX + radius * System.Math.Cos(a);
                        double y = centerY + radius * System.Math.Sin(a);
                        result.Add(new Vec2(x, y));
                    }
                }
                else
                {
                    result.Add(curr);
                }
            }
            else
            {
                // Corner is not sharp, just add the point
                result.Add(curr);
            }
        }

        return result;
    }

    /// <summary>
    /// Insert arc points between two offset edge endpoints to create a smooth corner.
    /// </summary>
    private void InsertArcPoints(List<Vec2> points, Vec2 arcStart, Vec2 arcEnd, Vec2 center, double radius, bool isCCW)
    {
        // Calculate angles from center to start and end points
        double startAngle = System.Math.Atan2(arcStart.Northing - center.Northing, arcStart.Easting - center.Easting);
        double endAngle = System.Math.Atan2(arcEnd.Northing - center.Northing, arcEnd.Easting - center.Easting);

        // Calculate sweep - go the LONG way (creates rounded corner that cuts inside)
        double sweep = endAngle - startAngle;

        // Normalize to [-PI, PI] first
        while (sweep > System.Math.PI) sweep -= 2 * System.Math.PI;
        while (sweep < -System.Math.PI) sweep += 2 * System.Math.PI;

        // Now flip to the LONG way (opposite direction)
        sweep = sweep > 0 ? sweep - 2 * System.Math.PI : sweep + 2 * System.Math.PI;

        // Calculate number of arc segments based on arc length
        double arcLength = System.Math.Abs(sweep) * radius;
        int numSegments = System.Math.Max(3, (int)(arcLength / 0.5)); // ~0.5m spacing
        numSegments = System.Math.Min(numSegments, 20); // Cap at 20 segments

        // Add arc points (including start, excluding end since next edge will add it)
        for (int j = 0; j <= numSegments; j++)
        {
            double t = (double)j / numSegments;
            double angle = startAngle + t * sweep;
            double x = center.Easting + radius * System.Math.Cos(angle);
            double y = center.Northing + radius * System.Math.Sin(angle);
            points.Add(new Vec2(x, y));
        }
    }

    /// <summary>
    /// Compute intersection point of two infinite lines defined by two points each.
    /// </summary>
    private Vec2? LineLineIntersection(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2)
    {
        double d1x = a2.Easting - a1.Easting;
        double d1y = a2.Northing - a1.Northing;
        double d2x = b2.Easting - b1.Easting;
        double d2y = b2.Northing - b1.Northing;

        double cross = d1x * d2y - d1y * d2x;

        if (System.Math.Abs(cross) < 1e-10)
        {
            // Lines are parallel
            return null;
        }

        double dx = b1.Easting - a1.Easting;
        double dy = b1.Northing - a1.Northing;

        double t = (dx * d2y - dy * d2x) / cross;

        return new Vec2(
            a1.Easting + t * d1x,
            a1.Northing + t * d1y);
    }

    /// <summary>
    /// Use Clipper to clean up a self-intersecting polygon.
    /// </summary>
    private List<Vec2>? CleanupWithClipper(List<Vec2> points)
    {
        if (points.Count < 3) return null;

        var path = new Path64(points.Count);
        foreach (var pt in points)
        {
            path.Add(new Point64((long)(pt.Easting * Scale), (long)(pt.Northing * Scale)));
        }

        var clipper = new Clipper64();
        clipper.AddSubject(path);

        var solution = new Paths64();
        clipper.Execute(ClipType.Union, FillRule.NonZero, solution);

        if (solution.Count > 0 && solution[0].Count >= 3)
        {
            // Find the largest polygon
            var largest = solution[0];
            double largestArea = System.Math.Abs(Clipper.Area(solution[0]));
            for (int i = 1; i < solution.Count; i++)
            {
                double a = System.Math.Abs(Clipper.Area(solution[i]));
                if (a > largestArea)
                {
                    largestArea = a;
                    largest = solution[i];
                }
            }

            var result = new List<Vec2>(largest.Count);
            foreach (var pt in largest)
            {
                result.Add(new Vec2(pt.X / Scale, pt.Y / Scale));
            }
            return result;
        }

        return null;
    }

    /// <summary>
    /// Check if a point is inside a polygon using ray casting algorithm.
    /// </summary>
    private bool IsPointInPolygon(Vec2 point, List<Vec2> polygon)
    {
        bool inside = false;
        int n = polygon.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if ((pi.Northing > point.Northing) != (pj.Northing > point.Northing) &&
                point.Easting < (pj.Easting - pi.Easting) * (point.Northing - pi.Northing) / (pj.Northing - pi.Northing) + pi.Easting)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Calculate the signed area of a polygon.
    /// Positive = counter-clockwise, Negative = clockwise
    /// </summary>
    private double CalculateSignedArea(List<Vec2> points)
    {
        double area = 0;
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += points[i].Easting * points[j].Northing;
            area -= points[j].Easting * points[i].Northing;
        }
        return area / 2.0;
    }

    /// <summary>
    /// Use Clipper2 to remove self-intersections from an offset polygon.
    /// </summary>
    private List<Vec2>? CleanupSelfIntersections(List<Vec2> points)
    {
        if (points.Count < 3) return null;

        // Convert to Clipper path
        var path = new Path64(points.Count);
        foreach (var pt in points)
        {
            path.Add(new Point64((long)(pt.Easting * Scale), (long)(pt.Northing * Scale)));
        }

        // Use Union operation to clean up self-intersections
        var clipper = new Clipper64();
        clipper.AddSubject(path);

        var solution = new Paths64();
        clipper.Execute(ClipType.Union, FillRule.NonZero, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
            return null;

        // Find the largest polygon (by area) - that's our main result
        Path64 largest = solution[0];
        double largestArea = System.Math.Abs(Clipper.Area(solution[0]));
        for (int i = 1; i < solution.Count; i++)
        {
            double a = System.Math.Abs(Clipper.Area(solution[i]));
            if (a > largestArea)
            {
                largestArea = a;
                largest = solution[i];
            }
        }

        // Convert back to Vec2
        var result = new List<Vec2>(largest.Count);
        foreach (var pt in largest)
        {
            result.Add(new Vec2(pt.X / Scale, pt.Y / Scale));
        }

        return result;
    }

    /// <summary>
    /// Create an outward offset polygon from a boundary.
    /// Used for inner boundaries (islands) where headland goes outward.
    /// </summary>
    /// <param name="boundaryPoints">Inner boundary points</param>
    /// <param name="offsetDistance">Outward offset distance in meters</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>Offset polygon points</returns>
    public List<Vec2>? CreateOutwardOffset(List<Vec2> boundaryPoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        if (boundaryPoints == null || boundaryPoints.Count < 3)
            return null;

        if (offsetDistance <= 0)
            return new List<Vec2>(boundaryPoints);

        var path = new Path64(boundaryPoints.Count);
        foreach (var pt in boundaryPoints)
        {
            path.Add(new Point64((long)(pt.Easting * Scale), (long)(pt.Northing * Scale)));
        }

        var clipperOffset = new ClipperOffset();

        // Set arc tolerance for smooth curves (smaller = smoother)
        clipperOffset.ArcTolerance = 25;

        JoinType clipperJoinType = joinType switch
        {
            OffsetJoinType.Miter => JoinType.Miter,
            OffsetJoinType.Square => JoinType.Square,
            _ => JoinType.Round
        };

        clipperOffset.AddPath(path, clipperJoinType, EndType.Polygon);

        var solution = new Paths64();
        // Positive offset = expand/outward
        clipperOffset.Execute(offsetDistance * Scale, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
            return null;

        var result = new List<Vec2>(solution[0].Count);
        foreach (var pt in solution[0])
        {
            result.Add(new Vec2(pt.X / Scale, pt.Y / Scale));
        }

        return result;
    }

    /// <summary>
    /// Create multiple concentric offset polygons (for multi-pass headlands).
    /// </summary>
    /// <param name="boundaryPoints">Outer boundary points</param>
    /// <param name="offsetDistance">Distance per pass in meters</param>
    /// <param name="passes">Number of passes</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>List of offset polygons from outermost to innermost</returns>
    public List<List<Vec2>> CreateMultiPassOffset(List<Vec2> boundaryPoints, double offsetDistance, int passes, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        var result = new List<List<Vec2>>();

        if (boundaryPoints == null || boundaryPoints.Count < 3 || passes <= 0)
            return result;

        var currentBoundary = boundaryPoints;

        for (int i = 0; i < passes; i++)
        {
            var offset = CreateInwardOffset(currentBoundary, offsetDistance, joinType);
            if (offset == null || offset.Count < 3)
                break;

            result.Add(offset);
            currentBoundary = offset;
        }

        return result;
    }

    /// <summary>
    /// Create an offset of an open polyline (not closed polygon).
    /// Used for headland segments from boundary clips.
    /// </summary>
    /// <param name="linePoints">Open polyline points</param>
    /// <param name="offsetDistance">Offset distance in meters (positive = left of travel direction)</param>
    /// <param name="joinType">How to handle corners</param>
    /// <returns>Offset line points, or null if offset fails</returns>
    public List<Vec2>? CreateLineOffset(List<Vec2> linePoints, double offsetDistance, OffsetJoinType joinType = OffsetJoinType.Round)
    {
        if (linePoints == null || linePoints.Count < 2)
            return null;

        if (offsetDistance == 0)
            return new List<Vec2>(linePoints);

        // Convert to Clipper2 path (scaled to integers)
        var path = new Path64(linePoints.Count);
        foreach (var pt in linePoints)
        {
            path.Add(new Point64((long)(pt.Easting * Scale), (long)(pt.Northing * Scale)));
        }

        // Create offset for open path
        var clipperOffset = new ClipperOffset();

        // Set arc tolerance for smooth curves (smaller = smoother)
        clipperOffset.ArcTolerance = 25;

        JoinType clipperJoinType = joinType switch
        {
            OffsetJoinType.Miter => JoinType.Miter,
            OffsetJoinType.Square => JoinType.Square,
            _ => JoinType.Round
        };

        // EndType.Round for open path with rounded ends
        clipperOffset.AddPath(path, clipperJoinType, EndType.Round);

        var solution = new Paths64();
        clipperOffset.Execute(offsetDistance * Scale, solution);

        if (solution.Count == 0 || solution[0].Count < 2)
            return null;

        // The result is a closed polygon around the line
        // We need to extract just one side - find the points closest to our original offset direction
        var result = new List<Vec2>(solution[0].Count);
        foreach (var pt in solution[0])
        {
            result.Add(new Vec2(pt.X / Scale, pt.Y / Scale));
        }

        // For a simple line offset, extract the relevant half of the buffer polygon
        return ExtractOffsetSide(linePoints, result, offsetDistance);
    }

    /// <summary>
    /// Apply Chaikin's corner cutting algorithm to smooth a closed polygon.
    /// This algorithm cuts corners without overshoot, staying within the convex hull.
    /// </summary>
    /// <param name="polygon">Input polygon points</param>
    /// <param name="iterations">Number of smoothing iterations (more = smoother but smaller)</param>
    /// <returns>Smoothed polygon</returns>
    private List<Vec2> ChaikinSmooth(List<Vec2> polygon, int iterations)
    {
        if (polygon.Count < 3 || iterations < 1)
            return polygon;

        var current = polygon;

        for (int iter = 0; iter < iterations; iter++)
        {
            var next = new List<Vec2>(current.Count * 2);

            for (int i = 0; i < current.Count; i++)
            {
                var p0 = current[i];
                var p1 = current[(i + 1) % current.Count];

                // Chaikin uses 1/4 and 3/4 points along each edge
                double q0x = 0.75 * p0.Easting + 0.25 * p1.Easting;
                double q0y = 0.75 * p0.Northing + 0.25 * p1.Northing;
                double q1x = 0.25 * p0.Easting + 0.75 * p1.Easting;
                double q1y = 0.25 * p0.Northing + 0.75 * p1.Northing;

                next.Add(new Vec2(q0x, q0y));
                next.Add(new Vec2(q1x, q1y));
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// Densify a polygon by adding interpolated points where segments are too long.
    /// This ensures curves are well-approximated with many short segments.
    /// </summary>
    /// <param name="polygon">Input polygon points</param>
    /// <param name="maxSpacing">Maximum allowed distance between consecutive points</param>
    /// <returns>Densified polygon with additional interpolated points</returns>
    private List<Vec2> DensifyPolygon(List<Vec2> polygon, double maxSpacing)
    {
        if (polygon.Count < 3 || maxSpacing <= 0)
            return polygon;

        var result = new List<Vec2>();

        for (int i = 0; i < polygon.Count; i++)
        {
            var p0 = polygon[i];
            var p1 = polygon[(i + 1) % polygon.Count];

            // Always add the current point
            result.Add(p0);

            // Calculate segment length
            double dx = p1.Easting - p0.Easting;
            double dy = p1.Northing - p0.Northing;
            double segmentLength = System.Math.Sqrt(dx * dx + dy * dy);

            // Add interpolated points if segment is too long
            if (segmentLength > maxSpacing)
            {
                int subdivisions = (int)System.Math.Ceiling(segmentLength / maxSpacing);
                for (int j = 1; j < subdivisions; j++)
                {
                    double t = (double)j / subdivisions;
                    double x = p0.Easting + t * dx;
                    double y = p0.Northing + t * dy;
                    result.Add(new Vec2(x, y));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Calculate the perimeter of a closed polygon
    /// </summary>
    private double CalculatePolygonPerimeter(List<Vec2> polygon)
    {
        double perimeter = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            perimeter += System.Math.Sqrt(
                (p2.Easting - p1.Easting) * (p2.Easting - p1.Easting) +
                (p2.Northing - p1.Northing) * (p2.Northing - p1.Northing));
        }
        return perimeter;
    }

    /// <summary>
    /// Resample a closed polygon to have evenly spaced points.
    /// This smooths out angular artifacts from offset operations.
    /// </summary>
    private List<Vec2> ResamplePolygon(List<Vec2> polygon, double targetSpacing)
    {
        if (polygon.Count < 3 || targetSpacing <= 0)
            return polygon;

        // Build cumulative distance array for the closed polygon
        var cumDist = new double[polygon.Count + 1];
        cumDist[0] = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            double dx = p2.Easting - p1.Easting;
            double dy = p2.Northing - p1.Northing;
            cumDist[i + 1] = cumDist[i] + System.Math.Sqrt(dx * dx + dy * dy);
        }

        double totalLength = cumDist[polygon.Count];
        int numPoints = System.Math.Max(3, (int)System.Math.Round(totalLength / targetSpacing));
        double actualSpacing = totalLength / numPoints;

        var result = new List<Vec2>(numPoints);

        for (int i = 0; i < numPoints; i++)
        {
            double targetDist = i * actualSpacing;

            // Find which segment this distance falls into
            int segIdx = 0;
            for (int j = 1; j <= polygon.Count; j++)
            {
                if (cumDist[j] >= targetDist)
                {
                    segIdx = j - 1;
                    break;
                }
            }

            // Interpolate within the segment
            var p1 = polygon[segIdx];
            var p2 = polygon[(segIdx + 1) % polygon.Count];
            double segStart = cumDist[segIdx];
            double segEnd = cumDist[segIdx + 1];
            double segLength = segEnd - segStart;

            double t = segLength > 1e-10 ? (targetDist - segStart) / segLength : 0;
            t = System.Math.Clamp(t, 0, 1);

            double x = p1.Easting + t * (p2.Easting - p1.Easting);
            double y = p1.Northing + t * (p2.Northing - p1.Northing);
            result.Add(new Vec2(x, y));
        }

        return result;
    }

    /// <summary>
    /// Extract one side from a buffer polygon around a line
    /// </summary>
    private List<Vec2>? ExtractOffsetSide(List<Vec2> originalLine, List<Vec2> bufferPolygon, double offsetDistance)
    {
        if (bufferPolygon.Count < 4)
            return bufferPolygon;

        // Find the points in the buffer that are on the offset side
        // by checking which points are approximately offsetDistance away from the original line
        var result = new List<Vec2>();
        double targetDist = System.Math.Abs(offsetDistance);
        double tolerance = targetDist * 0.5; // Allow some variation

        foreach (var bufferPt in bufferPolygon)
        {
            // Find minimum distance to original line
            double minDist = double.MaxValue;
            for (int i = 0; i < originalLine.Count - 1; i++)
            {
                double dist = PointToSegmentDistance(bufferPt, originalLine[i], originalLine[i + 1]);
                minDist = System.Math.Min(minDist, dist);
            }

            // Keep points that are roughly the right distance away
            if (System.Math.Abs(minDist - targetDist) < tolerance)
            {
                result.Add(bufferPt);
            }
        }

        // If we got enough points, sort them along the original line direction
        if (result.Count >= 2)
        {
            // Sort by projection onto original line direction
            var lineDir = new Vec2(
                originalLine[originalLine.Count - 1].Easting - originalLine[0].Easting,
                originalLine[originalLine.Count - 1].Northing - originalLine[0].Northing);
            double len = System.Math.Sqrt(lineDir.Easting * lineDir.Easting + lineDir.Northing * lineDir.Northing);
            if (len > 0)
            {
                lineDir = new Vec2(lineDir.Easting / len, lineDir.Northing / len);
                result.Sort((a, b) =>
                {
                    double projA = a.Easting * lineDir.Easting + a.Northing * lineDir.Northing;
                    double projB = b.Easting * lineDir.Easting + b.Northing * lineDir.Northing;
                    return projA.CompareTo(projB);
                });
            }
            return result;
        }

        // Fallback: just return first half of buffer (rough approximation)
        return bufferPolygon.Take(bufferPolygon.Count / 2).ToList();
    }

    /// <summary>
    /// Calculate distance from a point to a line segment
    /// </summary>
    private double PointToSegmentDistance(Vec2 point, Vec2 segA, Vec2 segB)
    {
        double dx = segB.Easting - segA.Easting;
        double dy = segB.Northing - segA.Northing;
        double lenSq = dx * dx + dy * dy;

        if (lenSq < 1e-10)
            return System.Math.Sqrt(
                (point.Easting - segA.Easting) * (point.Easting - segA.Easting) +
                (point.Northing - segA.Northing) * (point.Northing - segA.Northing));

        double t = System.Math.Max(0, System.Math.Min(1,
            ((point.Easting - segA.Easting) * dx + (point.Northing - segA.Northing) * dy) / lenSq));

        double projX = segA.Easting + t * dx;
        double projY = segA.Northing + t * dy;

        return System.Math.Sqrt(
            (point.Easting - projX) * (point.Easting - projX) +
            (point.Northing - projY) * (point.Northing - projY));
    }

    /// <summary>
    /// Simplify a closed polygon using Douglas-Peucker algorithm.
    /// Reduces point count while preserving shape within tolerance.
    /// </summary>
    private List<Vec2> DouglasPeuckerSimplify(List<Vec2> points, double tolerance)
    {
        if (points.Count <= 4)
            return new List<Vec2>(points);

        // For closed polygon, we need to handle the wrap-around
        // Find the point farthest from the line between first and last
        int n = points.Count;

        // Use iterative approach to avoid stack overflow on large polygons
        var result = new List<Vec2>();
        var keepFlags = new bool[n];

        // Always keep first and last points
        keepFlags[0] = true;
        keepFlags[n - 1] = true;

        // Stack of ranges to process
        var stack = new Stack<(int start, int end)>();
        stack.Push((0, n - 1));

        while (stack.Count > 0)
        {
            var (start, end) = stack.Pop();

            if (end - start < 2)
                continue;

            // Find point with maximum distance from line segment
            double maxDist = 0;
            int maxIdx = start;

            var lineStart = points[start];
            var lineEnd = points[end];

            for (int i = start + 1; i < end; i++)
            {
                double dist = PerpendicularDistance(points[i], lineStart, lineEnd);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    maxIdx = i;
                }
            }

            // If max distance exceeds tolerance, keep that point and recurse
            if (maxDist > tolerance)
            {
                keepFlags[maxIdx] = true;
                stack.Push((start, maxIdx));
                stack.Push((maxIdx, end));
            }
        }

        // Build result from flagged points
        for (int i = 0; i < n; i++)
        {
            if (keepFlags[i])
                result.Add(points[i]);
        }

        // Ensure we have at least 3 points for a valid polygon
        if (result.Count < 3)
            return new List<Vec2>(points);

        return result;
    }

    /// <summary>
    /// Calculate perpendicular distance from point to line segment.
    /// </summary>
    private double PerpendicularDistance(Vec2 point, Vec2 lineStart, Vec2 lineEnd)
    {
        double dx = lineEnd.Easting - lineStart.Easting;
        double dy = lineEnd.Northing - lineStart.Northing;
        double lenSq = dx * dx + dy * dy;

        if (lenSq < 1e-10)
        {
            // Line segment is a point
            return System.Math.Sqrt(
                (point.Easting - lineStart.Easting) * (point.Easting - lineStart.Easting) +
                (point.Northing - lineStart.Northing) * (point.Northing - lineStart.Northing));
        }

        // Calculate perpendicular distance using cross product
        double cross = System.Math.Abs(
            (point.Easting - lineStart.Easting) * dy -
            (point.Northing - lineStart.Northing) * dx);

        return cross / System.Math.Sqrt(lenSq);
    }

    /// <summary>
    /// Calculate headings for each point in the polygon based on adjacent points.
    /// </summary>
    /// <param name="points">Polygon points</param>
    /// <returns>Points with headings as Vec3 (X, Y, Heading in radians)</returns>
    public List<Vec3> CalculatePointHeadings(List<Vec2> points)
    {
        var result = new List<Vec3>(points.Count);

        if (points == null || points.Count < 3)
            return result;

        for (int i = 0; i < points.Count; i++)
        {
            var prev = points[(i - 1 + points.Count) % points.Count];
            var next = points[(i + 1) % points.Count];

            // Calculate heading from direction vector between prev and next
            double dx = next.Easting - prev.Easting;
            double dy = next.Northing - prev.Northing;
            double heading = System.Math.Atan2(dx, dy);

            result.Add(new Vec3(points[i].Easting, points[i].Northing, heading));
        }

        return result;
    }
}

/// <summary>
/// Corner join types for polygon offset
/// </summary>
public enum OffsetJoinType
{
    /// <summary>Round corners (smooth)</summary>
    Round,
    /// <summary>Miter corners (sharp, extended)</summary>
    Miter,
    /// <summary>Square corners (flat)</summary>
    Square
}
