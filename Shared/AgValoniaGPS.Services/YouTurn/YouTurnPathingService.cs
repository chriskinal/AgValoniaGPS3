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
using AgValoniaGPS.Models.Track;

using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.Services.YouTurn;

/// <summary>
/// Owns the pass / next-track book-keeping for the YouTurn state machine:
/// - choosing the offset to the next pass (normal / skip / snake modes),
/// - building the offset track that represents the next pass,
/// - validating that the next pass fits inside the cultivated area,
/// - building and advancing the pre-computed snake-pattern sequence,
/// - computing skip distances around already-worked passes.
///
/// The service mutates the provided <see cref="YouTurnState"/> and
/// <see cref="GuidanceState"/> but never touches UI services — map updates
/// stay on the ViewModel side.
/// </summary>
public sealed class YouTurnPathingService
{
    private readonly ILogger<YouTurnPathingService> _logger;

    public YouTurnPathingService(ILogger<YouTurnPathingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compute the next track offset perpendicular to the current line and stash
    /// it on <see cref="YouTurnState.NextTrack"/> / <see cref="YouTurnState.NextTrackTurnOffset"/>.
    /// </summary>
    public void ComputeNextTrack(
        Models.Track.Track referenceTrack,
        double abHeading,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        int uTurnSkipRows,
        bool isSkipWorkedMode,
        Models.Track.Track? selectedTrack)
    {
        if (referenceTrack.Points.Count < 2) return;

        var refPointA = referenceTrack.Points[0];
        var refPointB = referenceTrack.Points[referenceTrack.Points.Count - 1];
        var config = ConfigurationStore.Instance;

        // Offset direction via XOR of turn direction and travel direction:
        //   turnLeft=true,  sameWay=true  -> negative
        //   turnLeft=true,  sameWay=false -> positive
        //   turnLeft=false, sameWay=true  -> positive
        //   turnLeft=false, sameWay=false -> negative
        int pathsToMove = uTurnSkipRows + 1; // skip=0 moves 1 pass, skip=1 moves 2, etc.
        bool positiveOffset = turn.IsTurnLeft ^ guidance.IsHeadingSameWay;

        // Skip-worked mode: find next unworked path instead of fixed skip.
        if (isSkipWorkedMode && selectedTrack != null)
        {
            pathsToMove = GetNextUnworkedPathSkip(selectedTrack, guidance.HowManyPathsAway, positiveOffset, pathsToMove);
        }

        int offsetChange = positiveOffset ? pathsToMove : -pathsToMove;
        int nextPathsAway = guidance.HowManyPathsAway + offsetChange;

        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        double nextDistAway = widthMinusOverlap * nextPathsAway;

        // Authoritative perpendicular width for the U-turn arc — direction lives in IsTurnLeft.
        turn.NextTrackTurnOffset = Math.Abs(pathsToMove * widthMinusOverlap);

        Models.Track.Track nextTrack;
        if (referenceTrack.Points.Count == 2)
        {
            double perpAngle = abHeading + Math.PI / 2;
            double offsetEasting = Math.Sin(perpAngle) * nextDistAway;
            double offsetNorthing = Math.Cos(perpAngle) * nextDistAway;

            nextTrack = Models.Track.Track.FromABLine(
                $"Path {nextPathsAway}",
                new Vec3(refPointA.Easting + offsetEasting, refPointA.Northing + offsetNorthing, abHeading),
                new Vec3(refPointB.Easting + offsetEasting, refPointB.Northing + offsetNorthing, abHeading));
        }
        else
        {
            var offsetPoints = CurveProcessing.CreateOffsetCurve(referenceTrack.Points, nextDistAway);
            nextTrack = Models.Track.Track.FromCurve($"Path {nextPathsAway}", offsetPoints, referenceTrack.IsClosed);
        }
        nextTrack.IsActive = false;
        turn.NextTrack = nextTrack;

        _logger.LogDebug("[YouTurn] Turn {Dir}, heading {Way} way",
            turn.IsTurnLeft ? "LEFT" : "RIGHT",
            guidance.IsHeadingSameWay ? "SAME" : "OPPOSITE");
        _logger.LogDebug("[YouTurn] Offset {Sign}: path {Cur} -> {Next} ({Dist:F1}m)",
            positiveOffset ? "positive" : "negative",
            guidance.HowManyPathsAway, nextPathsAway, nextDistAway);
    }

    /// <summary>
    /// True if *any portion* of the next pass line (as determined by the normal turn rule —
    /// turnLeft XOR sameWay — with no skip-worked adjustment) crosses the cultivated area.
    /// </summary>
    /// <remarks>
    /// Fixes #289 F1. The previous implementation tested only the midpoint of the AB segment
    /// offset perpendicular — for non-rectangular fields the AB midpoint is not the center of
    /// every offset pass's usable span, so a midpoint can fall outside the polygon even when
    /// the offset line still crosses the field somewhere along its 2000 m rendered extent.
    /// That false-negative stopped U-turns early, leaving unworked strips on the far side of
    /// non-rectangular fields. New implementation walks the full offset line at fixed spacing
    /// and returns true if any sample lies inside the cultivated area.
    /// </remarks>
    /// <summary>
    /// Check if the next pass line (in either direction) falls inside the cultivated area.
    /// Returns (isInside, positiveDirection) where positiveDirection indicates which
    /// offset direction has cultivated area.
    /// </summary>
    public (bool isInside, bool positiveDirection) WouldNextLineBeInsideBoundary(
        Models.Track.Track currentTrack,
        double abHeading,
        GuidanceWorkingState guidance,
        Boundary? boundary,
        IReadOnlyList<Vec3>? headlandLine,
        int uTurnSkipRows)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
            return (true, false);
        if (currentTrack.Points.Count < 2)
            return (true, false);

        var config = ConfigurationStore.Instance;
        int pathsToMove = uTurnSkipRows + 1;
        int nextPathsAwayNeg = guidance.HowManyPathsAway - pathsToMove;
        int nextPathsAwayPos = guidance.HowManyPathsAway + pathsToMove;

        bool negInside = CheckOffsetLineInsideCultivated(currentTrack, abHeading, nextPathsAwayNeg,
                boundary, headlandLine, config);
        bool posInside = CheckOffsetLineInsideCultivated(currentTrack, abHeading, nextPathsAwayPos,
                boundary, headlandLine, config);

        if (negInside && posInside)
        {
            // Both directions work — prefer the one that continues advancing
            // (same direction as the current pass offset from 0).
            // If on pass 0, prefer negative (original convention).
            bool preferPositive = guidance.HowManyPathsAway > 0;
            return (true, preferPositive);
        }
        if (negInside) return (true, false);
        if (posInside) return (true, true);
        return (false, false);
    }

    private bool CheckOffsetLineInsideCultivated(
        Models.Track.Track currentTrack, double abHeading, int nextPathsAway,
        Boundary? boundary, IReadOnlyList<Vec3>? headlandLine,
        ConfigurationStore config)
    {

        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        double nextDistAway = widthMinusOverlap * nextPathsAway;

        var pointA = currentTrack.Points[0];
        var pointB = currentTrack.Points[currentTrack.Points.Count - 1];
        double perpAngle = abHeading + Math.PI / 2;

        // Sample the offset pass line along the AB direction across the full rendered extent
        // (pass lines are drawn 2000 m beyond A and B in each direction — see
        // DrawingContextMapControl.DrawSingleTrack). Spacing 5 m gives ~800 samples across a
        // 4 km line — trivial cost, and won't miss a field whose narrowest usable span is > 5 m.
        const double SampleSpacing = 5.0;
        const double LineExtent = 2000.0;
        double abDx = pointB.Easting - pointA.Easting;
        double abDy = pointB.Northing - pointA.Northing;
        double abLen = Math.Sqrt(abDx * abDx + abDy * abDy);
        if (abLen < 0.01) return IsPointInsideCultivatedArea(
            (pointA.Easting + pointB.Easting) / 2 + Math.Sin(perpAngle) * nextDistAway,
            (pointA.Northing + pointB.Northing) / 2 + Math.Cos(perpAngle) * nextDistAway,
            boundary, headlandLine);

        double abNx = abDx / abLen;
        double abNy = abDy / abLen;

        double midEasting = (pointA.Easting + pointB.Easting) / 2 + Math.Sin(perpAngle) * nextDistAway;
        double midNorthing = (pointA.Northing + pointB.Northing) / 2 + Math.Cos(perpAngle) * nextDistAway;

        double totalLength = abLen + 2 * LineExtent;
        int sampleCount = (int)(totalLength / SampleSpacing);
        double startOffset = -(totalLength / 2.0);

        for (int i = 0; i <= sampleCount; i++)
        {
            double t = startOffset + i * SampleSpacing;
            double e = midEasting + abNx * t;
            double n = midNorthing + abNy * t;
            if (IsPointInsideCultivatedArea(e, n, boundary, headlandLine))
            {
                _logger.LogDebug("[NextTrack] nextPath={Next}, sample at t={T:F0}m hit cultivated area",
                    nextPathsAway, t);
                return true;
            }
        }

        _logger.LogDebug("[NextTrack] nextPath={Next}, no sample along {N} samples of offset line hit cultivated area",
            nextPathsAway, sampleCount + 1);
        return false;
    }

    /// <summary>
    /// Build the snake-pattern sequence for the cultivated area and rotate it so the
    /// tractor's current pass comes first. Stored on the state object.
    /// </summary>
    public void BuildSnakeSequence(
        Models.Track.Track referenceTrack,
        double abHeading,
        GuidanceWorkingState guidance,
        YouTurnWorkingState turn,
        Boundary? boundary,
        IReadOnlyList<Vec3>? headlandLine)
    {
        var config = ConfigurationStore.Instance;
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        if (widthMinusOverlap < 0.5) return;

        // Walk outwards perpendicular to the reference track to find the pass-number range
        // that fits inside the cultivated area.
        var pointA = referenceTrack.Points[0];
        var pointB = referenceTrack.Points[referenceTrack.Points.Count - 1];
        double perpAngle = abHeading + Math.PI / 2;
        double midE = (pointA.Easting + pointB.Easting) / 2;
        double midN = (pointA.Northing + pointB.Northing) / 2;

        int minPath = 0, maxPath = 0;

        for (int p = 0; p <= 200; p++)
        {
            double offsetDist = widthMinusOverlap * p;
            double testE = midE + Math.Sin(perpAngle) * offsetDist;
            double testN = midN + Math.Cos(perpAngle) * offsetDist;
            if (!IsPointInsideCultivatedArea(testE, testN, boundary, headlandLine)) break;
            maxPath = p;
        }

        for (int p = -1; p >= -200; p--)
        {
            double offsetDist = widthMinusOverlap * p;
            double testE = midE + Math.Sin(perpAngle) * offsetDist;
            double testN = midN + Math.Cos(perpAngle) * offsetDist;
            if (!IsPointInsideCultivatedArea(testE, testN, boundary, headlandLine)) break;
            minPath = p;
        }

        var fullSequence = AgValoniaGPS.Services.Track.SwathOrderingService.GeneratePathSequence(
            minPath, maxPath, AgValoniaGPS.Services.Track.SwathPattern.Snake);

        // Rotate so the sequence starts at the current pass — the tractor may start anywhere in the field.
        int currentIdx = fullSequence.IndexOf(guidance.HowManyPathsAway);
        turn.SnakeSequence = currentIdx > 0
            ? fullSequence.Skip(currentIdx).Concat(fullSequence.Take(currentIdx)).ToList()
            : fullSequence;
        turn.SnakeIndex = 0;

        _logger.LogDebug("[YouTurn] Built snake sequence (rotated from path {Cur}): [{Seq}]",
            guidance.HowManyPathsAway, string.Join(", ", turn.SnakeSequence));
    }

    /// <summary>
    /// Peek the next path number in the snake sequence, or null if the sequence is exhausted.
    /// </summary>
    public int? GetNextSnakePath(YouTurnWorkingState turn)
    {
        if (turn.SnakeSequence == null || turn.SnakeIndex < 0) return null;

        int nextIndex = turn.SnakeIndex + 1;
        if (nextIndex >= turn.SnakeSequence.Count) return null;

        return turn.SnakeSequence[nextIndex];
    }

    /// <summary>
    /// Advance the snake sequence cursor after a pass has been completed.
    /// </summary>
    public void AdvanceSnakeSequence(YouTurnWorkingState turn)
    {
        if (turn.SnakeSequence != null && turn.SnakeIndex < turn.SnakeSequence.Count - 1)
        {
            turn.SnakeIndex++;
            _logger.LogDebug("[YouTurn] Snake advanced to index {Idx}: path {Path}",
                turn.SnakeIndex, turn.SnakeSequence[turn.SnakeIndex]);
        }
    }

    /// <summary>
    /// Mirrors AgOpenGPS's GetNextNotWorkedTrack: start at the normal goal lane and walk outward
    /// until an unworked pass is found. Returns the actual skip distance (always positive).
    /// </summary>
    public int GetNextUnworkedPathSkip(Models.Track.Track? selectedTrack, int currentPath, bool positiveDirection, int initialSkip)
    {
        if (selectedTrack == null) return initialSkip;

        int goalLane = currentPath + (positiveDirection ? initialSkip : -initialSkip);
        int iterations = 0;

        while (selectedTrack.IsPathWorked(goalLane) && iterations < 100)
        {
            if (positiveDirection) goalLane++; else goalLane--;
            iterations++;
        }

        int actualSkip = Math.Abs(goalLane - currentPath);
        if (actualSkip < 1) actualSkip = initialSkip;

        _logger.LogDebug("[YouTurn] SkipWorked: initial skip {Initial}, actual skip {Actual} (goal lane {Goal}, {It} iterations)",
            initialSkip, actualSkip, goalLane, iterations);
        return actualSkip;
    }

    // Headland line takes precedence; fall back to the outer boundary when no headland is defined.
    private static bool IsPointInsideCultivatedArea(
        double easting, double northing,
        Boundary? boundary,
        IReadOnlyList<Vec3>? headlandLine)
    {
        if (headlandLine != null && headlandLine.Count >= 3)
            return GeometryMath.IsPointInPolygon(headlandLine, new Vec2(easting, northing));

        if (boundary?.OuterBoundary != null && boundary.OuterBoundary.IsValid)
            return boundary.OuterBoundary.IsPointInside(easting, northing);

        return true;
    }
}
