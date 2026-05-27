// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
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
using System.Linq;

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.Pipeline;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.YouTurn;

using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.Services.YouTurn;

/// <summary>
/// High-level orchestration around <see cref="CreateTurn(YouTurnCreationInput)"/>:
/// builds the input, validates the generated path, and drops to a simple geometric
/// fallback when the primary algorithm produces a spiral or fails outright. This
/// used to live inline in MainViewModel.YouTurn.cs.
/// </summary>
public partial class YouTurnCreationService
{
    /// <summary>
    /// Result of <see cref="CreateTurnPath"/>. <see cref="Path"/> is null when preconditions
    /// aren't met (no headland line, no boundary, invalid input). <see cref="UsedFallback"/>
    /// is true when the primary Dubins-based algorithm returned a spiral or failed outright
    /// and the simple geometric fallback was substituted.
    /// </summary>
    public readonly record struct TurnPathResult(List<Vec3>? Path, bool UsedFallback);

    /// <summary>
    /// Build a complete U-turn path for the current vehicle state.
    /// The returned path is smoothed per <see cref="GuidanceConfig.UTurnSmoothing"/>.
    /// </summary>
    public TurnPathResult CreateTurnPath(
        Position currentPosition,
        Models.Track.Track selectedTrack,
        double headingRadians,
        double abHeading,
        Boundary? boundary,
        IReadOnlyList<Vec3>? headlandLine,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        int uTurnSkipRows,
        double headlandCalculatedWidth,
        double headlandDistance)
    {
        if (headlandLine == null)
            return new TurnPathResult(null, false);

        bool turnLeft = turn.IsTurnLeft;

        _logger.LogDebug("[YouTurn] Creating turn: direction={Dir}, sameWay={SameWay}, pathsAway={Away}",
            turnLeft ? "LEFT" : "RIGHT", guidance.IsHeadingSameWay, guidance.HowManyPathsAway);

        var input = BuildCreationInput(
            currentPosition, selectedTrack, headingRadians, abHeading, turnLeft,
            boundary, headlandLine, guidance, turn, uTurnSkipRows, headlandCalculatedWidth);

        if (input == null)
        {
            _logger.LogWarning("[YouTurn] Failed to build creation input - no boundary available?");
            return new TurnPathResult(null, false);
        }

        var output = CreateTurn(input);
        var config = ConfigurationStore.Instance;

        if (output.Success && output.TurnPath != null && output.TurnPath.Count > 10)
        {
            var path = output.TurnPath;

            // Spiral / pretzel guard. The original check summed |ΔHeading| per step and
            // rejected anything > 1.5π (270°) — but a legit Dubins RLR/LRL arc-arc-arc
            // solution can accumulate 300°+ of absolute turning while ending at the correct
            // 180° reversal. That false-rejected valid paths (#289 F2) and dropped the run
            // to the less-optimal simple fallback.
            //
            // New guard:
            //   - Primary check: start-to-end heading delta should be ~π (180°) for a U-turn.
            //   - Secondary safety: cumulative |ΔHeading| capped at 4π (720°) to still catch
            //     actual infinite-loop spirals, without rejecting loopy-but-valid paths.
            double netHeadingChange = path[^1].Heading - path[0].Heading;
            while (netHeadingChange > Math.PI) netHeadingChange -= 2 * Math.PI;
            while (netHeadingChange < -Math.PI) netHeadingChange += 2 * Math.PI;

            double totalHeadingChange = 0;
            for (int i = 1; i < path.Count; i++)
            {
                double delta = path[i].Heading - path[i - 1].Heading;
                while (delta > Math.PI) delta -= 2 * Math.PI;
                while (delta < -Math.PI) delta += 2 * Math.PI;
                totalHeadingChange += Math.Abs(delta);
            }

            // ~π expected for U-turn; tolerate ±30° slack to account for approach angle.
            bool netIsUTurnLike = Math.Abs(Math.Abs(netHeadingChange) - Math.PI) < Math.PI / 6.0;
            bool cumulativeRunaway = totalHeadingChange > Math.PI * 4.0;

            if (!netIsUTurnLike || cumulativeRunaway)
            {
                _logger.LogWarning("[YouTurn] Service path rejected: net={Net:F0}° cumulative={Cum:F0}° - using simple fallback",
                    netHeadingChange * 180 / Math.PI, totalHeadingChange * 180 / Math.PI);
                var fallback = SimpleFallback(currentPosition, abHeading, turnLeft, boundary,
                    guidance, turn, uTurnSkipRows, headlandDistance);
                return new TurnPathResult(fallback.Count > 10 ? fallback : null, UsedFallback: true);
            }

            TurnPathSmoothing.Smooth(path, config.Guidance.UTurnSmoothing);
            _logger.LogDebug("[YouTurn] Path created with {Count} points, net={Net:F0}° cumulative={Cum:F0}°",
                path.Count, netHeadingChange * 180 / Math.PI, totalHeadingChange * 180 / Math.PI);
            return new TurnPathResult(path, UsedFallback: false);
        }

        _logger.LogWarning("[YouTurn] Service failed: {Reason} - using simple fallback",
            output.FailureReason ?? "unknown");
        var fallbackPath = SimpleFallback(currentPosition, abHeading, turnLeft, boundary,
            guidance, turn, uTurnSkipRows, headlandDistance);
        return new TurnPathResult(fallbackPath.Count > 10 ? fallbackPath : null, UsedFallback: true);
    }

    // ── Input builder ───────────────────────────────────────────────────

    private YouTurnCreationInput? BuildCreationInput(
        Position currentPosition,
        Models.Track.Track track,
        double headingRadians,
        double abHeading,
        bool turnLeft,
        Boundary? boundary,
        IReadOnlyList<Vec3> headlandLine,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        int uTurnSkipRows,
        double headlandCalculatedWidth)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            _logger.LogDebug("[YouTurn] No valid outer boundary available");
            return null;
        }

        var config = ConfigurationStore.Instance;
        double toolWidth = config.ActualToolWidth;
        double totalHeadlandWidth = headlandCalculatedWidth;

        var outerPoints = boundary.OuterBoundary.Points
            .Select(p => new Vec2(p.Easting, p.Northing))
            .ToList();

        // Turn boundary controls where the outermost point of the turn can reach.
        //  > 0: turn stays that far inside the field
        //  = 0: turn may touch the outer boundary
        //  < 0: turn may extend past the outer boundary
        double distanceFromBoundary = config.Guidance.UTurnDistanceFromBoundary;
        List<Vec2>? turnBoundaryVec2;
        if (distanceFromBoundary > 0.1)
            turnBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, distanceFromBoundary);
        else if (distanceFromBoundary < -0.1)
            turnBoundaryVec2 = _polygonOffsetService.CreateOutwardOffset(outerPoints, -distanceFromBoundary);
        else
            turnBoundaryVec2 = outerPoints;

        if (turnBoundaryVec2 == null || turnBoundaryVec2.Count < 3)
        {
            _logger.LogDebug("[YouTurn] Offset failed, using outer boundary directly");
            turnBoundaryVec2 = outerPoints;
        }
        var turnBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(turnBoundaryVec2);

        var headlandBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, totalHeadlandWidth);
        if (headlandBoundaryVec2 == null || headlandBoundaryVec2.Count < 3)
        {
            _logger.LogDebug("[YouTurn] Failed to create headland boundary");
            return null;
        }
        var headlandBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(headlandBoundaryVec2);

        var boundaryTurnLines = new List<BoundaryTurnLine>
        {
            new BoundaryTurnLine { Points = turnBoundaryVec3, BoundaryIndex = 0 }
        };

        double headlandWidthForTurn = Math.Max(totalHeadlandWidth - toolWidth, toolWidth);

        Func<Vec3, int> isPointInsideTurnArea = point =>
            GeometryMath.IsPointInPolygon(turnBoundaryVec3, point) ? 0 : 1;

        var pivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians);

        var input = new YouTurnCreationInput
        {
            TurnType = YouTurnType.AlbinStyle,
            IsTurnLeft = turnLeft,
            GuidanceType = GuidanceLineType.ABLine,
            BoundaryTurnLines = boundaryTurnLines,
            IsPointInsideTurnArea = isPointInsideTurnArea,

            // Curves: use the heading at the point where the current offset track crosses the headland
            // (not the vehicle's local heading).
            ABHeading = track.Points.Count > 2
                ? FindTrackHeadingAtHeadland(track, pivotPosition, headlandLine, guidance)
                : abHeading,
            ABReferencePoint = CalculateCurrentTrackReferencePoint(track, abHeading, pivotPosition, headlandLine, guidance),
            IsHeadingSameWay = guidance.IsHeadingSameWay,

            PivotPosition = pivotPosition,
            ToolWidth = toolWidth,
            ToolOverlap = config.Tool.Overlap,
            ToolOffset = config.Tool.Offset,
            TurnRadius = config.Guidance.UTurnRadius,

            // Matches the cyan next-track line exactly.
            TurnOffset = turn.NextTrackTurnOffset,
            RowSkipsWidth = uTurnSkipRows,
            TurnStartOffset = 0,
            HowManyPathsAway = guidance.HowManyPathsAway,
            // Fine lateral nudge: the driven guidance line is offset by
            // (HowManyPathsAway * widthMinusOverlap + NudgeOffset) (see
            // GpsPipelineService distAway). The turn's exit/target-line math
            // already adds input.NudgeDistance — feed it the live nudge so the
            // turn tracks the nudged line instead of the un-nudged base track.
            NudgeDistance = guidance.NudgeOffset,
            TrackMode = 0,

            MakeUTurnCounter = turn.YouTurnCounter + 10,

            LegLength = config.Guidance.UTurnExtension,
            YouTurnLegExtensionMultiplier = 2.5,
            HeadlandWidth = headlandWidthForTurn,
        };

        _logger.LogDebug("[YouTurn] Input built: toolWidth={W:F1}m, totalHeadland={TH:F1}m, headlandWidthForTurn={HWT:F1}m, turnBoundaryPts={TB}, headlandPts={HP}",
            toolWidth, totalHeadlandWidth, headlandWidthForTurn, turnBoundaryVec3.Count, headlandBoundaryVec3.Count);

        return input;
    }

    // ── Reference-point helpers ─────────────────────────────────────────

    /// <summary>
    /// For curves: walk the current offset track from the nearest vehicle segment in the travel direction
    /// and return the heading at the first segment that crosses the headland. Falls back to the nearest-point
    /// heading when no crossing is found.
    /// </summary>
    private double FindTrackHeadingAtHeadland(
        Models.Track.Track track,
        Vec3 vehiclePos,
        IReadOnlyList<Vec3> headlandLine,
        GuidanceWorkingState guidance)
    {
        if (headlandLine.Count < 3) return track.Points[0].Heading;

        var config = ConfigurationStore.Instance;
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        // Match the driven line: pass offset + fine nudge (see GpsPipelineService distAway).
        double offsetDistance = guidance.HowManyPathsAway * widthMinusOverlap + guidance.NudgeOffset;

        List<Vec3> searchPoints = Math.Abs(offsetDistance) < 0.01
            ? track.Points
            : CurveProcessing.CreateOffsetCurve(track.Points, offsetDistance);

        int nearestIdx = 0;
        double minDistSq = double.MaxValue;
        for (int i = 0; i < searchPoints.Count; i++)
        {
            double dx = searchPoints[i].Easting - vehiclePos.Easting;
            double dy = searchPoints[i].Northing - vehiclePos.Northing;
            double distSq = dx * dx + dy * dy;
            if (distSq < minDistSq) { minDistSq = distSq; nearestIdx = i; }
        }

        int step = guidance.IsHeadingSameWay ? 1 : -1;
        int endIdx = guidance.IsHeadingSameWay ? searchPoints.Count - 1 : 0;

        for (int i = nearestIdx; i != endIdx; i += step)
        {
            var p1 = searchPoints[i];
            var p2 = searchPoints[i + step];

            for (int j = 0; j < headlandLine.Count; j++)
            {
                var h1 = headlandLine[j];
                var h2 = headlandLine[(j + 1) % headlandLine.Count];

                if (GeometryMath.SegmentsIntersect(
                        p1.Easting, p1.Northing, p2.Easting, p2.Northing,
                        h1.Easting, h1.Northing, h2.Easting, h2.Northing))
                {
                    _logger.LogDebug("[YouTurn] Found headland intersection on offset track (path {Path}) at index {Idx}, heading={Deg:F1}°",
                        guidance.HowManyPathsAway, i, p1.Heading * 180 / Math.PI);
                    return p1.Heading;
                }
            }
        }

        return searchPoints[nearestIdx].Heading;
    }

    /// <summary>
    /// Reference point for the primary Dubins turn: where the current offset track (not the base
    /// track) crosses the headland, ahead of the vehicle. Falls back to projecting the vehicle
    /// position onto the offset track.
    /// </summary>
    private Vec2 CalculateCurrentTrackReferencePoint(
        Models.Track.Track track,
        double abHeading,
        Vec3 vehiclePosition,
        IReadOnlyList<Vec3> headlandLine,
        GuidanceWorkingState guidance)
    {
        if (track.Points.Count < 2)
            return new Vec2(vehiclePosition.Easting, vehiclePosition.Northing);

        var config = ConfigurationStore.Instance;
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        // Match the driven line: pass offset + fine nudge (see GpsPipelineService distAway).
        double offsetDistance = guidance.HowManyPathsAway * widthMinusOverlap + guidance.NudgeOffset;

        Models.Track.Track currentOffsetTrack;
        if (Math.Abs(offsetDistance) < 0.01)
        {
            currentOffsetTrack = track;
        }
        else
        {
            var offsetPoints = CurveProcessing.CreateOffsetCurve(track.Points, offsetDistance);
            currentOffsetTrack = new Models.Track.Track
            {
                Name = $"Current path {guidance.HowManyPathsAway}",
                Points = offsetPoints,
                Type = track.Type,
                IsVisible = false,
                IsActive = false,
            };
        }

        var intersection = FindTrackHeadlandIntersectionAhead(currentOffsetTrack, vehiclePosition, headlandLine, guidance.IsHeadingSameWay);
        if (intersection.HasValue)
        {
            _logger.LogDebug("[YouTurn] Reference point: offset track (path {Path}) crosses headland at ({E:F1},{N:F1})",
                guidance.HowManyPathsAway, intersection.Value.Easting, intersection.Value.Northing);
            return intersection.Value;
        }

        // Fallback: project vehicle onto segment A-B of the offset track, clamped to [0,1].
        var ptA = currentOffsetTrack.Points[0];
        var ptB = currentOffsetTrack.Points[currentOffsetTrack.Points.Count - 1];
        double abE = ptB.Easting - ptA.Easting;
        double abN = ptB.Northing - ptA.Northing;
        double abLengthSq = abE * abE + abN * abN;

        double avE = vehiclePosition.Easting - ptA.Easting;
        double avN = vehiclePosition.Northing - ptA.Northing;
        double t = Math.Max(0, Math.Min(1, (avE * abE + avN * abN) / abLengthSq));

        _logger.LogDebug("[YouTurn] Reference point: fallback to vehicle projection on offset track, path={Path}",
            guidance.HowManyPathsAway);

        return new Vec2(ptA.Easting + t * abE, ptA.Northing + t * abN);
    }

    internal static Vec2? FindTrackHeadlandIntersectionAhead(
        Models.Track.Track track,
        Vec3 vehiclePos,
        IReadOnlyList<Vec3> headlandLine,
        bool headingSameWay)
    {
        if (headlandLine.Count < 3) return null;
        if (track.Points.Count < 2) return null;

        if (track.Points.Count > 2)
        {
            int nearestIdx = 0;
            double minDistSq = double.MaxValue;
            for (int i = 0; i < track.Points.Count; i++)
            {
                double pdx = track.Points[i].Easting - vehiclePos.Easting;
                double pdy = track.Points[i].Northing - vehiclePos.Northing;
                double distSq = pdx * pdx + pdy * pdy;
                if (distSq < minDistSq) { minDistSq = distSq; nearestIdx = i; }
            }

            int step = headingSameWay ? 1 : -1;
            int endIdx = headingSameWay ? track.Points.Count - 1 : 0;

            for (int i = nearestIdx; i != endIdx; i += step)
            {
                var p1 = track.Points[i];
                var p2 = track.Points[i + step];

                for (int j = 0; j < headlandLine.Count; j++)
                {
                    var h1 = headlandLine[j];
                    var h2 = headlandLine[(j + 1) % headlandLine.Count];

                    var intersection = GeometryMath.TryGetSegmentIntersection(
                        p1.Easting, p1.Northing, p2.Easting, p2.Northing,
                        h1.Easting, h1.Northing, h2.Easting, h2.Northing);
                    if (intersection.HasValue) return intersection;
                }
            }
            return null;
        }

        // AB line: intersect the AB line itself (extended past both endpoints)
        // with the headland; pick the intersection closest to the vehicle.
        //
        // The intersection MUST be a point on the AB line — anything else
        // produces a perpendicular-offset reference point, which cascades into
        // FindABTurnPoint and the U-turn arc landing off the cyan NextTrack
        // line by the same offset (issue #354). The previous implementation
        // intersected a *vehicle-anchored* line with the headland, which only
        // landed on AB when the vehicle happened to be on AB exactly — and
        // produced the order-dependent misalignment when the vehicle was
        // perpendicular-offset (e.g. activation before the auto-pass-detect
        // had time to update HowManyPathsAway from a stale 0).
        var ptA = track.Points[0];
        var ptB = track.Points[1];

        double dx = ptB.Easting - ptA.Easting;
        double dy = ptB.Northing - ptA.Northing;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) return null;

        double dirE = dx / len;
        double dirN = dy / len;
        double extendedStartE = ptA.Easting - dirE * 1000;
        double extendedStartN = ptA.Northing - dirN * 1000;
        double extendedEndE = ptB.Easting + dirE * 1000;
        double extendedEndN = ptB.Northing + dirN * 1000;

        Vec2? closestIntersection = null;
        double closestDist = double.MaxValue;

        for (int j = 0; j < headlandLine.Count; j++)
        {
            var h1 = headlandLine[j];
            var h2 = headlandLine[(j + 1) % headlandLine.Count];

            var intersection = GeometryMath.TryGetSegmentIntersection(
                extendedStartE, extendedStartN, extendedEndE, extendedEndN,
                h1.Easting, h1.Northing, h2.Easting, h2.Northing);

            if (intersection.HasValue)
            {
                double dxi = intersection.Value.Easting - vehiclePos.Easting;
                double dyi = intersection.Value.Northing - vehiclePos.Northing;
                double distSq = dxi * dxi + dyi * dyi;
                if (distSq < closestDist)
                {
                    closestDist = distSq;
                    closestIntersection = intersection;
                }
            }
        }

        return closestIntersection;
    }

    // ── Simple geometric fallback ───────────────────────────────────────

    /// <summary>
    /// Straight-in, semicircle, straight-out U-turn. Used when the primary Dubins-based creation
    /// returns a spiral or fails entirely.
    /// </summary>
    private List<Vec3> SimpleFallback(
        Position currentPosition,
        double abHeading,
        bool turnLeft,
        Boundary? boundary,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        int uTurnSkipRows,
        double headlandDistance)
    {
        var path = new List<Vec3>();
        var config = ConfigurationStore.Instance;

        const double pointSpacing = 0.5;
        double turnOffset = turn.NextTrackTurnOffset;

        if (turnOffset < 0.1)
        {
            double trackWidth = config.ActualToolWidth - config.Tool.Overlap;
            turnOffset = trackWidth * (uTurnSkipRows + 1);
            _logger.LogDebug("[YouTurn] Using fallback turnOffset calculation: {Off:F2}m", turnOffset);
        }

        double turnRadius = config.Guidance.UTurnRadius;
        double geometricMinRadius = turnOffset / 2.0;
        if (turnRadius < geometricMinRadius) turnRadius = geometricMinRadius;
        const double minTurnRadius = 4.0;
        if (turnRadius < minTurnRadius) turnRadius = minTurnRadius;

        double travelHeading = abHeading;
        if (!guidance.IsHeadingSameWay)
        {
            travelHeading += Math.PI;
            if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
        }

        double exitHeading = travelHeading + Math.PI;
        if (exitHeading >= Math.PI * 2) exitHeading -= Math.PI * 2;

        double perpAngle = turnLeft ? (travelHeading - Math.PI / 2) : (travelHeading + Math.PI / 2);

        double distToHeadland = turn.DistanceToHeadland;
        double headlandBoundaryEasting = currentPosition.Easting + Math.Sin(travelHeading) * distToHeadland;
        double headlandBoundaryNorthing = currentPosition.Northing + Math.Cos(travelHeading) * distToHeadland;

        double distanceFromBoundary = config.Guidance.UTurnDistanceFromBoundary;

        // #289 F4: Compute the actual distance from the headland crossing to the outer
        // boundary along the travel heading, instead of assuming headlandDistance (the
        // scalar headland-zone width). On non-rectangular fields the available forward
        // room varies along the headland — near a corner it's tighter than the zone
        // width, along a long straight edge it's closer to the zone width. The previous
        // formula `headlandLegLength = headlandDistance - turnRadius - distanceFromBoundary`
        // produced a tight arc when used as a fallback for a turn that the Dubins service
        // would have placed deeper. Using the actual ray distance makes the fallback's
        // apex placement match the service's intent regardless of field shape.
        double rayToOuter = double.MaxValue;
        if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid)
        {
            rayToOuter = RaycastDistanceToPolygon(
                headlandBoundaryEasting, headlandBoundaryNorthing,
                travelHeading, boundary.OuterBoundary.Points);
        }
        double availableHeadlandSpan = rayToOuter < double.MaxValue
            ? rayToOuter
            : headlandDistance; // fallback to legacy scalar when raycast fails

        double headlandLegLength = availableHeadlandSpan - turnRadius - distanceFromBoundary;
        if (headlandLegLength < 0) headlandLegLength = 0;
        double fieldLegLength = config.Guidance.UTurnExtension;

        _logger.LogDebug("[YouTurn] Simple path: turnOffset={Off:F1}m, turnRadius={Rad:F1}m", turnOffset, turnRadius);
        _logger.LogDebug("[YouTurn] headlandDist(scalar)={HD:F1}m rayToOuter={RO:F1}m, headlandLegLength={HLL:F1}m",
            headlandDistance, rayToOuter, headlandLegLength);

        double entryStartE = headlandBoundaryEasting - Math.Sin(travelHeading) * fieldLegLength;
        double entryStartN = headlandBoundaryNorthing - Math.Cos(travelHeading) * fieldLegLength;

        double arcStartE = headlandBoundaryEasting + Math.Sin(travelHeading) * headlandLegLength;
        double arcStartN = headlandBoundaryNorthing + Math.Cos(travelHeading) * headlandLegLength;

        double arcCenterE = arcStartE + Math.Sin(perpAngle) * turnRadius;
        double arcCenterN = arcStartN + Math.Cos(perpAngle) * turnRadius;

        double exitEndE = entryStartE + Math.Sin(perpAngle) * turnOffset;
        double exitEndN = entryStartN + Math.Cos(perpAngle) * turnOffset;

        // Arc apex boundary check removed — the arc is meant to extend into the headland zone.
        // Validate only that the exit end lands back inside the field boundary.
        if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid
            && !boundary.OuterBoundary.IsPointInside(exitEndE, exitEndN))
        {
            _logger.LogDebug("[YouTurn] Exit end is outside boundary - not creating U-turn");
            return path;
        }

        double totalEntryLength = fieldLegLength + headlandLegLength;
        int totalEntryPoints = (int)(totalEntryLength / pointSpacing);

        for (int i = 0; i <= totalEntryPoints; i++)
        {
            double dist = i * pointSpacing;
            path.Add(new Vec3
            {
                Easting = entryStartE + Math.Sin(travelHeading) * dist,
                Northing = entryStartN + Math.Cos(travelHeading) * dist,
                Heading = travelHeading,
            });
        }

        int arcPoints = Math.Max((int)(Math.PI * turnRadius / pointSpacing), 20);
        double startAngle = Math.Atan2(arcStartE - arcCenterE, arcStartN - arcCenterN);

        for (int i = 1; i <= arcPoints; i++)
        {
            double t = (double)i / arcPoints;
            double sweepAngle = turnLeft ? (-Math.PI * t) : (Math.PI * t);
            double currentAngle = startAngle + sweepAngle;

            double tangentHeading = currentAngle + (turnLeft ? -Math.PI / 2 : Math.PI / 2);
            if (tangentHeading < 0) tangentHeading += Math.PI * 2;
            if (tangentHeading >= Math.PI * 2) tangentHeading -= Math.PI * 2;

            path.Add(new Vec3
            {
                Easting = arcCenterE + Math.Sin(currentAngle) * turnRadius,
                Northing = arcCenterN + Math.Cos(currentAngle) * turnRadius,
                Heading = tangentHeading,
            });
        }

        // Connect the arc exit to the start of the outbound straight leg, then build the leg itself.
        var lastArcPoint = path[path.Count - 1];
        double actualArcEndE = lastArcPoint.Easting;
        double actualArcEndN = lastArcPoint.Northing;

        double exitStartE = arcStartE + Math.Sin(perpAngle) * turnOffset;
        double exitStartN = arcStartN + Math.Cos(perpAngle) * turnOffset;

        double arcToExitDist = Math.Sqrt(
            (exitStartE - actualArcEndE) * (exitStartE - actualArcEndE) +
            (exitStartN - actualArcEndN) * (exitStartN - actualArcEndN));
        if (arcToExitDist > pointSpacing)
        {
            int connectPoints = (int)(arcToExitDist / pointSpacing);
            for (int i = 1; i <= connectPoints; i++)
            {
                double t = (double)i / (connectPoints + 1);
                path.Add(new Vec3
                {
                    Easting = actualArcEndE + (exitStartE - actualArcEndE) * t,
                    Northing = actualArcEndN + (exitStartN - actualArcEndN) * t,
                    Heading = exitHeading,
                });
            }
        }

        int totalExitPoints = (int)(totalEntryLength / pointSpacing);
        for (int i = 1; i <= totalExitPoints; i++)
        {
            double dist = i * pointSpacing;
            path.Add(new Vec3
            {
                Easting = exitStartE + Math.Sin(exitHeading) * dist,
                Northing = exitStartN + Math.Cos(exitHeading) * dist,
                Heading = exitHeading,
            });
        }

        _logger.LogDebug("[YouTurn] Simple fallback path has {Count} points", path.Count);

        TurnPathSmoothing.Smooth(path, config.Guidance.UTurnSmoothing);

        return path;
    }

    /// <summary>
    /// Build an immediate U-turn path anchored at the tractor's current position — no
    /// entry/exit straight legs, no headland traversal. Just a semicircle from the
    /// tractor to the next parallel pass, executed the moment it's rendered (#260).
    ///
    /// The arc radius is fixed at <c>offset / 2</c> so the endpoint lands exactly on
    /// the next parallel pass with reversed heading. The configured
    /// <c>Guidance.UTurnRadius</c> is ignored because any larger radius would overshoot
    /// the target pass and would need a connector leg to bridge the gap — which would
    /// defeat the "no legs, immediate" requirement.
    /// </summary>
    public List<Vec3> CreateManualArcPath(
        Position currentPosition,
        double abHeading,
        bool turnLeft,
        Boundary? boundary,
        GuidanceWorkingState guidance,
        int uTurnSkipRows)
    {
        var path = new List<Vec3>();
        var config = ConfigurationStore.Instance;

        const double pointSpacing = 0.5;
        double trackWidth = config.ActualToolWidth - config.Tool.Overlap;
        double turnOffset = trackWidth * (uTurnSkipRows + 1);
        if (turnOffset < 0.1)
        {
            _logger.LogWarning("[ManualYouTurn] Tool width is zero — cannot compute offset");
            return path;
        }

        double turnRadius = turnOffset / 2.0;

        // Travel direction as the tractor is currently heading along the AB line.
        double travelHeading = abHeading;
        if (!guidance.IsHeadingSameWay)
        {
            travelHeading += Math.PI;
            if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
        }

        double arcStartE = currentPosition.Easting;
        double arcStartN = currentPosition.Northing;

        double perpAngle = turnLeft ? (travelHeading - Math.PI / 2) : (travelHeading + Math.PI / 2);

        double arcCenterE = arcStartE + Math.Sin(perpAngle) * turnRadius;
        double arcCenterN = arcStartN + Math.Cos(perpAngle) * turnRadius;

        double arcEndE = arcStartE + Math.Sin(perpAngle) * (2 * turnRadius);
        double arcEndN = arcStartN + Math.Cos(perpAngle) * (2 * turnRadius);

        // Refuse to plot a turn whose endpoint lands outside the field.
        if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid
            && !boundary.OuterBoundary.IsPointInside(arcEndE, arcEndN))
        {
            _logger.LogDebug("[ManualYouTurn] Arc end ({E:F1},{N:F1}) outside boundary — refusing",
                arcEndE, arcEndN);
            return path;
        }

        double startAngle = Math.Atan2(arcStartE - arcCenterE, arcStartN - arcCenterN);
        int arcPoints = Math.Max((int)(Math.PI * turnRadius / pointSpacing), 20);

        // Include the start point so guidance has a valid t=0 sample at the tractor.
        path.Add(new Vec3
        {
            Easting = arcStartE,
            Northing = arcStartN,
            Heading = travelHeading,
        });

        for (int i = 1; i <= arcPoints; i++)
        {
            double t = (double)i / arcPoints;
            double sweepAngle = turnLeft ? (-Math.PI * t) : (Math.PI * t);
            double currentAngle = startAngle + sweepAngle;

            double tangentHeading = currentAngle + (turnLeft ? -Math.PI / 2 : Math.PI / 2);
            if (tangentHeading < 0) tangentHeading += Math.PI * 2;
            if (tangentHeading >= Math.PI * 2) tangentHeading -= Math.PI * 2;

            path.Add(new Vec3
            {
                Easting = arcCenterE + Math.Sin(currentAngle) * turnRadius,
                Northing = arcCenterN + Math.Cos(currentAngle) * turnRadius,
                Heading = tangentHeading,
            });
        }

        TurnPathSmoothing.Smooth(path, config.Guidance.UTurnSmoothing);

        _logger.LogDebug("[ManualYouTurn] Arc path: {Count} points, radius={Rad:F1}m, offset={Off:F1}m, turnLeft={Left}",
            path.Count, turnRadius, turnOffset, turnLeft);

        return path;
    }

    /// <summary>
    /// Distance from (startE, startN) along direction `heading` to the first intersection
    /// with a closed polygon. Returns double.MaxValue if the ray never hits. Used by
    /// SimpleFallback to measure the actual forward distance from the headland crossing to
    /// the outer boundary on non-rectangular fields (#289 F4).
    /// </summary>
    private static double RaycastDistanceToPolygon(
        double startE, double startN, double heading, IReadOnlyList<BoundaryPoint> polygon)
    {
        if (polygon.Count < 3) return double.MaxValue;

        double dirE = Math.Sin(heading);
        double dirN = Math.Cos(heading);
        double minDist = double.MaxValue;
        int n = polygon.Count;

        for (int i = 0; i < n; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % n];

            double edgeE = p2.Easting - p1.Easting;
            double edgeN = p2.Northing - p1.Northing;
            double toP1E = p1.Easting - startE;
            double toP1N = p1.Northing - startN;

            double cross = dirE * edgeN - dirN * edgeE;
            if (Math.Abs(cross) < 1e-10) continue;

            double t = (toP1E * edgeN - toP1N * edgeE) / cross;
            double u = (toP1E * dirN - toP1N * dirE) / cross;

            if (t > 0 && u >= 0 && u <= 1 && t < minDist)
                minDist = t;
        }

        return minDist;
    }
}
