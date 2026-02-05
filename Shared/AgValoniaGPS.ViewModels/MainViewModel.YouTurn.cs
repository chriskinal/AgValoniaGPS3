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
using System.Linq;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing YouTurn (U-turn) logic.
/// Handles automatic U-turn path creation, guidance, and execution.
/// </summary>
public partial class MainViewModel
{
    #region YouTurn Fields

    // YouTurn state
    private bool _isYouTurnTriggered;
    private bool _isInYouTurn; // True when executing the U-turn
    private List<Vec3>? _youTurnPath;
    private int _youTurnCounter;
    private double _distanceToHeadland;
    private bool _isHeadingSameWay;
    private bool _isTurnLeft; // Direction of the current/pending U-turn
    private bool _wasHeadingSameWayAtTurnStart; // Heading direction when turn was created (for offset calc)
    private bool _lastTurnWasLeft; // Track last turn direction to alternate
    private Track? _nextTrack; // The next track to switch to after U-turn completes

    /// <summary>
    /// Pre-calculated perpendicular offset to next track (always positive, in meters).
    /// This is the authoritative value for U-turn arc width - use this instead of recalculating.
    /// </summary>
    public double NextTrackTurnOffset { get; private set; }

    private int _howManyPathsAway; // Which parallel offset line we're on (like AgOpenGPS)

    // Zone tracking - single state for tractor location
    public enum TractorZone { OutsideBoundary = 0, InHeadland = 1, InCultivatedArea = 2 }
    private TractorZone _currentZone = TractorZone.OutsideBoundary;

    // Debug: expose zone for UI display
    public TractorZone CurrentZone => _currentZone;
    public string CurrentZoneDisplay => _currentZone switch
    {
        TractorZone.OutsideBoundary => "Outside",
        TractorZone.InHeadland => "Headland",
        TractorZone.InCultivatedArea => "Cultivated",
        _ => "Unknown"
    };

    #endregion

    #region YouTurn Properties

    private bool _isYouTurnEnabled; // YouTurn auto U-turn feature

    public bool IsYouTurnEnabled
    {
        get => _isYouTurnEnabled;
        set => SetProperty(ref _isYouTurnEnabled, value);
    }

    private int _uTurnSkipRows;

    /// <summary>
    /// Number of rows to skip during U-turn (0-9)
    /// </summary>
    public int UTurnSkipRows
    {
        get => _uTurnSkipRows;
        set => SetProperty(ref _uTurnSkipRows, Math.Max(0, Math.Min(9, value)));
    }

    private bool _isUTurnSkipRowsEnabled;

    /// <summary>
    /// When true, U-turn skip rows feature is enabled
    /// </summary>
    public bool IsUTurnSkipRowsEnabled
    {
        get => _isUTurnSkipRowsEnabled;
        set => SetProperty(ref _isUTurnSkipRowsEnabled, value);
    }

    #endregion

    #region YouTurn Methods

    /// <summary>
    /// Clear all U-turn state - called when closing a field.
    /// </summary>
    public void ClearYouTurnState()
    {
        _youTurnPath = null;
        _nextTrack = null;
        _isYouTurnTriggered = false;
        _isInYouTurn = false;
        _youTurnCounter = 0;
        _currentZone = TractorZone.OutsideBoundary;

        _mapService.SetYouTurnPath(null);
        _mapService.SetNextTrack(null);
        _mapService.SetIsInYouTurn(false);

        State.YouTurn.IsTriggered = false;
        State.YouTurn.IsExecuting = false;
        State.YouTurn.TurnPath = null;
    }

    /// <summary>
    /// Manually trigger a left U-turn. Used for tracks along boundaries where
    /// automatic headland detection doesn't work.
    /// </summary>
    public void TriggerManualYouTurnLeft()
    {
        TriggerManualYouTurn(turnLeft: true);
    }

    /// <summary>
    /// Manually trigger a right U-turn. Used for tracks along boundaries where
    /// automatic headland detection doesn't work.
    /// </summary>
    public void TriggerManualYouTurnRight()
    {
        TriggerManualYouTurn(turnLeft: false);
    }

    /// <summary>
    /// Trigger a manual U-turn in the specified direction.
    /// Creates the turn path immediately without waiting for headland detection.
    /// </summary>
    private void TriggerManualYouTurn(bool turnLeft)
    {
        // Must have autosteer engaged and a track selected
        if (!IsAutoSteerEngaged || SelectedTrack == null)
        {
            StatusMessage = "Enable autosteer first";
            return;
        }

        // Don't create a new turn if already in one
        if (_isInYouTurn || _youTurnPath != null)
        {
            StatusMessage = "U-turn already in progress";
            return;
        }

        var track = SelectedTrack;
        if (track.Points.Count < 2)
        {
            StatusMessage = "Invalid track";
            return;
        }

        // Get current position from GPS
        var currentPosition = new AgValoniaGPS.Models.Position
        {
            Easting = Easting,
            Northing = Northing,
            Heading = Heading
        };

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

        // Calculate track heading
        var trackPointA = track.Points[0];
        var trackPointB = track.Points[track.Points.Count - 1];
        double abDx = trackPointB.Easting - trackPointA.Easting;
        double abDy = trackPointB.Northing - trackPointA.Northing;
        double abHeading = Math.Atan2(abDx, abDy);

        // Determine if vehicle is heading same way as AB line
        double headingDiff = headingRadians - abHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        _isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Set turn direction and save heading state for offset calculation
        _isTurnLeft = turnLeft;
        _wasHeadingSameWayAtTurnStart = _isHeadingSameWay;

        _logger.LogDebug($"[ManualYouTurn] Triggering {(turnLeft ? "LEFT" : "RIGHT")} turn, isHeadingSameWay={_isHeadingSameWay}");

        // Compute next track and create turn path
        ComputeNextTrack(track, abHeading);
        CreateYouTurnPath(currentPosition, headingRadians, abHeading);

        if (_youTurnPath != null && _youTurnPath.Count > 2)
        {
            // Immediately trigger the turn (don't wait for proximity to start point)
            State.YouTurn.IsTriggered = true;
            State.YouTurn.IsExecuting = true;
            _isYouTurnTriggered = true;
            _isInYouTurn = true;
            StatusMessage = $"Manual {(turnLeft ? "left" : "right")} U-turn started";
        }
        else
        {
            StatusMessage = "Failed to create U-turn path";
        }
    }

    #endregion

    #region YouTurn Processing

    /// <summary>
    /// Process YouTurn - check distance to headland, create turn path if needed, trigger turn.
    /// </summary>
    private void ProcessYouTurn(AgValoniaGPS.Models.Position currentPosition)
    {
        var track = SelectedTrack;
        if (track == null || track.Points.Count < 2 || _currentHeadlandLine == null) return;

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;
        bool isCurve = track.Points.Count > 2;

        double abHeading;
        if (isCurve)
        {
            // For curves, find the nearest point and use its local heading
            double minDistSq = double.MaxValue;
            int nearestIdx = 0;
            for (int i = 0; i < track.Points.Count; i++)
            {
                double dx = track.Points[i].Easting - currentPosition.Easting;
                double dy = track.Points[i].Northing - currentPosition.Northing;
                double distSq = dx * dx + dy * dy;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearestIdx = i;
                }
            }
            abHeading = track.Points[nearestIdx].Heading;
            _logger.LogDebug($"[YouTurn] Curve mode: nearest index={nearestIdx}, localHeading={abHeading * 180 / Math.PI:F1}°");
        }
        else
        {
            // For AB lines, calculate heading from first to last point
            var trackPointA = track.Points[0];
            var trackPointB = track.Points[1];
            double abDx = trackPointB.Easting - trackPointA.Easting;
            double abDy = trackPointB.Northing - trackPointA.Northing;
            abHeading = Math.Atan2(abDx, abDy);
            _logger.LogDebug($"[YouTurn] AB Line: abHeading={abHeading * 180 / Math.PI:F1}°");
        }

        // Determine if vehicle is heading the same way as the AB line
        double headingDiff = headingRadians - abHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        _isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Check if vehicle is aligned with AB line (not mid-turn)
        // We need to be within ~20 degrees of the AB line direction (either forward or reverse)
        // Math.Abs(headingDiff) < PI/2 means heading same way, > PI/2 means opposite
        // We want to check alignment to either direction of the AB line
        double alignmentTolerance = Math.PI / 9;  // ~20 degrees
        bool alignedForward = Math.Abs(headingDiff) < alignmentTolerance;
        bool alignedReverse = Math.Abs(headingDiff) > (Math.PI - alignmentTolerance);
        bool isAlignedWithABLine = alignedForward || alignedReverse;

        // Only calculate distance to headland when aligned with the AB line
        // This prevents creating turns while mid-turn when heading changes rapidly
        double travelHeading = abHeading;
        if (!_isHeadingSameWay)
        {
            travelHeading += Math.PI;
            if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
        }

        if (isAlignedWithABLine)
        {
            // Calculate distance to headland using raycast
            // Only triggers automatic U-turns when there's a headland line defined
            _distanceToHeadland = CalculateDistanceToHeadland(currentPosition, travelHeading);
        }
        else
        {
            _distanceToHeadland = double.MaxValue;  // Don't detect headland if not aligned
        }

        // Update zone tracking
        _currentZone = DetermineCurrentZone(currentPosition.Easting, currentPosition.Northing);

        bool isInCultivatedArea = _currentZone == TractorZone.InCultivatedArea;
        bool isInHeadlandZone = _currentZone == TractorZone.InHeadland;

        // AgOpenGPS creates turns while in CULTIVATED AREA approaching headland
        // The turn path has a leg that extends back into the cultivated area
        // Distance-based creation window: 10-60m from headland
        double minDistanceToCreate = 10.0;
        double maxDistanceToCreate = 60.0;
        bool headlandInRange = _distanceToHeadland > minDistanceToCreate &&
                               _distanceToHeadland < maxDistanceToCreate;

        // Debug logging
        if (_youTurnPath == null && !_isInYouTurn && _distanceToHeadland < 100)
        {
            _logger.LogDebug($"[YouTurn] Zone={_currentZone}, dist={_distanceToHeadland:F1}m, aligned={isAlignedWithABLine}, inRange={headlandInRange}");
        }

        // TURN CREATION: In cultivated area, approaching headland, aligned with track
        if (_youTurnPath == null && !_isInYouTurn && isInCultivatedArea && headlandInRange && isAlignedWithABLine)
        {
            bool nextLineInside = WouldNextLineBeInsideBoundary(track, abHeading);
            _logger.LogDebug($"[YouTurn] Creating turn? nextLineInside={nextLineInside}");
            if (nextLineInside)
            {
                _logger.LogDebug($"[YouTurn] Creating turn path at {_distanceToHeadland:F1}m from headland");
                _isTurnLeft = _isHeadingSameWay;
                _wasHeadingSameWayAtTurnStart = _isHeadingSameWay;
                ComputeNextTrack(track, abHeading);
                CreateYouTurnPath(currentPosition, headingRadians, abHeading);
            }
            else
            {
                _logger.LogDebug("[YouTurn] Next line would be outside boundary - stopping U-turns");
                StatusMessage = "End of field reached";
            }
        }
        // TURN TRIGGER: When path is ready, trigger when close to turn start point
        else if (_youTurnPath != null && _youTurnPath.Count > 2 && !_isYouTurnTriggered && !_isInYouTurn)
        {
            var turnStart = _youTurnPath[0];
            double distToTurnStart = Math.Sqrt(
                Math.Pow(currentPosition.Easting - turnStart.Easting, 2) +
                Math.Pow(currentPosition.Northing - turnStart.Northing, 2));

            // Trigger when within 2 meters of turn start
            if (distToTurnStart <= 2.0)
            {
                State.YouTurn.IsTriggered = true;
                State.YouTurn.IsExecuting = true;
                _isYouTurnTriggered = true;
                _isInYouTurn = true;
                StatusMessage = "YouTurn triggered!";
                _logger.LogDebug($"[YouTurn] Triggered at {distToTurnStart:F2}m from turn start");
            }
        }
        // RESET: If entered headland with untriggered turn, reset (drove past turn start)
        else if (_youTurnPath != null && !_isYouTurnTriggered && isInHeadlandZone)
        {
            _logger.LogDebug("[YouTurn] Entered headland without triggering - resetting turn");
            _youTurnPath = null;
            _nextTrack = null;
            _mapService.SetYouTurnPath(null);
            _mapService.SetNextTrack(null);
        }

        // Check if U-turn is complete (vehicle reached end of turn path)
        if (_isInYouTurn && _youTurnPath != null && _youTurnPath.Count > 2)
        {
            var startPoint = _youTurnPath[0];
            var endPoint = _youTurnPath[_youTurnPath.Count - 1];

            double distToTurnStart = Math.Sqrt(
                Math.Pow(currentPosition.Easting - startPoint.Easting, 2) +
                Math.Pow(currentPosition.Northing - startPoint.Northing, 2));
            double distToTurnEnd = Math.Sqrt(
                Math.Pow(currentPosition.Easting - endPoint.Easting, 2) +
                Math.Pow(currentPosition.Northing - endPoint.Northing, 2));

            // Complete turn when:
            // 1. Within 2 meters of turn end, AND
            // 2. Closer to end than to start (prevents immediate completion when start/end are close)
            // 3. At least 5 meters from start (ensures we've actually traveled into the turn)
            if (distToTurnEnd <= 2.0 && distToTurnEnd < distToTurnStart && distToTurnStart > 5.0)
            {
                CompleteYouTurn();
            }
        }
    }

    /// <summary>
    /// Check if the next track (after a U-turn) would be inside the field boundary.
    /// </summary>
    private bool WouldNextLineBeInsideBoundary(Track currentTrack, double abHeading)
    {
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
            return true; // No boundary, assume OK

        if (currentTrack.Points.Count < 2)
            return true; // Invalid track, assume OK

        var pointA = currentTrack.Points[0];
        var pointB = currentTrack.Points[currentTrack.Points.Count - 1];

        // Calculate where the next line would be (use runtime skip rows property)
        // skipWidth=0 means adjacent passes, skipWidth=1 means skip 1 row, etc.
        int rowSkipWidth = UTurnSkipRows; // Use runtime property (matches ComputeNextTrack)
        double actualWidth = ConfigStore.ActualToolWidth;
        double overlap = Tool.Overlap;
        double offsetDistance = (rowSkipWidth + 1) * (actualWidth - overlap);
        _logger.LogDebug($"[NextTrack] ActualToolWidth={actualWidth:F2}m, Overlap={overlap:F2}m, SkipWidth={rowSkipWidth}, OffsetDistance={offsetDistance:F2}m");

        // Perpendicular offset direction
        double perpAngle = abHeading + (_isHeadingSameWay ? -Math.PI / 2 : Math.PI / 2);
        double offsetEasting = Math.Sin(perpAngle) * offsetDistance;
        double offsetNorthing = Math.Cos(perpAngle) * offsetDistance;

        // Check if midpoint of next line would be inside boundary
        double midEasting = (pointA.Easting + pointB.Easting) / 2 + offsetEasting;
        double midNorthing = (pointA.Northing + pointB.Northing) / 2 + offsetNorthing;

        return IsPointInsideBoundary(midEasting, midNorthing);
    }

    /// <summary>
    /// Check if a point is inside the outer boundary.
    /// </summary>
    private bool IsPointInsideBoundary(double easting, double northing)
    {
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
            return true;

        var points = _currentBoundary.OuterBoundary.Points;
        int n = points.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = points[i];
            var pj = points[j];

            if (((pi.Northing > northing) != (pj.Northing > northing)) &&
                (easting < (pj.Easting - pi.Easting) * (northing - pi.Northing) / (pj.Northing - pi.Northing) + pi.Easting))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Calculate the minimum distance from a point to the boundary polygon.
    /// </summary>
    private double DistanceToBoundary(double easting, double northing)
    {
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
            return double.MaxValue;

        var points = _currentBoundary.OuterBoundary.Points;
        double minDist = double.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % points.Count];

            // Distance from point to line segment
            double dist = PointToSegmentDistance(easting, northing, p1.Easting, p1.Northing, p2.Easting, p2.Northing);
            if (dist < minDist)
                minDist = dist;
        }

        return minDist;
    }

    /// <summary>
    /// Calculate the distance from a point to a line segment.
    /// </summary>
    private static double PointToSegmentDistance(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double lenSq = dx * dx + dy * dy;

        if (lenSq < 0.0001) // Degenerate segment
            return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));

        // Project point onto line, clamped to segment
        double t = Math.Max(0, Math.Min(1, ((px - x1) * dx + (py - y1) * dy) / lenSq));
        double projX = x1 + t * dx;
        double projY = y1 + t * dy;

        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }

    /// <summary>
    /// Check if a track runs along the boundary (high % of points near boundary).
    /// Returns true if the track should skip boundary disengage on first pass.
    /// </summary>
    private bool IsTrackOnBoundary(Track? track, double threshold = 5.0, double minOverlapPercent = 0.5)
    {
        if (track == null || track.Points.Count == 0)
            return false;

        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
            return false;

        int pointsNearBoundary = 0;
        foreach (var point in track.Points)
        {
            if (DistanceToBoundary(point.Easting, point.Northing) < threshold)
                pointsNearBoundary++;
        }

        double overlapPercent = (double)pointsNearBoundary / track.Points.Count;
        return overlapPercent >= minOverlapPercent;
    }

    /// <summary>
    /// Compute the next track offset perpendicular to the current line.
    /// </summary>
    private void ComputeNextTrack(Track referenceTrack, double abHeading)
    {
        if (referenceTrack.Points.Count < 2)
            return;

        var refPointA = referenceTrack.Points[0];
        var refPointB = referenceTrack.Points[referenceTrack.Points.Count - 1];

        // Determine offset direction based on turn direction and heading
        // XOR truth table:
        //   turnLeft=true,  sameWay=true  -> false -> negative offset
        //   turnLeft=true,  sameWay=false -> true  -> positive offset
        //   turnLeft=false, sameWay=true  -> true  -> positive offset
        //   turnLeft=false, sameWay=false -> false -> negative offset
        int rowSkipWidth = UTurnSkipRows;  // Use runtime property from bottom nav button (0 = adjacent, 1 = skip 1, etc.)
        int pathsToMove = rowSkipWidth + 1;  // skip=0 moves 1 path, skip=1 moves 2 paths, etc.

        // Calculate offset direction using XOR
        bool positiveOffset = _isTurnLeft ^ _isHeadingSameWay;
        int offsetChange = positiveOffset ? pathsToMove : -pathsToMove;
        int nextPathsAway = _howManyPathsAway + offsetChange;

        // Calculate the total offset for the next line
        double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
        double nextDistAway = widthMinusOverlap * nextPathsAway;

        // Save the perpendicular turn offset (always positive - direction handled by IsTurnLeft)
        NextTrackTurnOffset = Math.Abs(pathsToMove * widthMinusOverlap);

        // Check if this is an AB line (2 points) or a curve (>2 points)
        if (referenceTrack.Points.Count == 2)
        {
            // AB Line: Calculate perpendicular offset from track heading
            double perpAngle = abHeading + Math.PI / 2;
            double offsetEasting = Math.Sin(perpAngle) * nextDistAway;
            double offsetNorthing = Math.Cos(perpAngle) * nextDistAway;

            _nextTrack = Track.FromABLine(
                $"Path {nextPathsAway}",
                new Vec3(refPointA.Easting + offsetEasting, refPointA.Northing + offsetNorthing, abHeading),
                new Vec3(refPointB.Easting + offsetEasting, refPointB.Northing + offsetNorthing, abHeading));
        }
        else
        {
            // Curve: Create clean offset curve that handles self-intersections on tight curves
            var offsetPoints = CurveProcessing.CreateOffsetCurve(referenceTrack.Points, nextDistAway);

            _nextTrack = Track.FromCurve(
                $"Path {nextPathsAway}",
                offsetPoints,
                referenceTrack.IsClosed);
        }
        _nextTrack.IsActive = false;

        _logger.LogDebug($"[YouTurn] Turn {(_isTurnLeft ? "LEFT" : "RIGHT")}, heading {(_isHeadingSameWay ? "SAME" : "OPPOSITE")} way");
        _logger.LogDebug($"[YouTurn] Offset {(positiveOffset ? "positive" : "negative")}: path {_howManyPathsAway} -> {nextPathsAway} ({nextDistAway:F1}m)");
        _logger.LogDebug($"[YouTurn] Next track type: {(referenceTrack.Points.Count == 2 ? "AB Line" : $"Curve with {referenceTrack.Points.Count} points")}");

        // Update map visualization
        _mapService.SetNextTrack(_nextTrack);
        _mapService.SetIsInYouTurn(true);
    }

    /// <summary>
    /// Complete the U-turn: switch to the next line and reset state.
    /// </summary>
    private void CompleteYouTurn()
    {
        // Guard against double-calling (can be triggered from both ProcessYouTurn and CalculateYouTurnGuidance)
        if (!_isInYouTurn)
        {
            _logger.LogDebug("[YouTurn] CompleteYouTurn called but not in turn - ignoring");
            return;
        }

        // Following AgOpenGPS approach exactly:
        // Determine offset direction using XOR
        int rowSkipWidth = UTurnSkipRows;  // Use runtime property from bottom nav button (0 = adjacent, 1 = skip 1, etc.)
        int pathsToMove = rowSkipWidth + 1;  // skip=0 moves 1 path, skip=1 moves 2 paths, etc.

        // Calculate offset direction using XOR
        // IMPORTANT: Use _wasHeadingSameWayAtTurnStart (saved at turn creation), NOT _isHeadingSameWay
        // (which has now flipped because we completed a 180° turn)
        bool positiveOffset = _isTurnLeft ^ _wasHeadingSameWayAtTurnStart;
        int offsetChange = positiveOffset ? pathsToMove : -pathsToMove;
        _howManyPathsAway += offsetChange;

        _logger.LogDebug($"[YouTurn] Turn complete! Turn was {(_isTurnLeft ? "LEFT" : "RIGHT")}, heading WAS {(_wasHeadingSameWayAtTurnStart ? "SAME" : "OPPOSITE")} at start");
        _logger.LogDebug($"[YouTurn] Offset {(positiveOffset ? "positive" : "negative")} by {offsetChange}, now on path {_howManyPathsAway}");
        _logger.LogDebug($"[YouTurn] Total offset: {(ConfigStore.ActualToolWidth - Tool.Overlap) * _howManyPathsAway:F1}m from reference line");

        // Remember this turn direction for alternating pattern
        _lastTurnWasLeft = _isTurnLeft;

        // Update centralized state
        State.YouTurn.LastTurnWasLeft = _isTurnLeft;
        State.YouTurn.HasCompletedFirstTurn = true;
        State.YouTurn.IsTriggered = false;
        State.YouTurn.IsExecuting = false;
        State.YouTurn.TurnPath = null;

        // Clear the U-turn state
        _isYouTurnTriggered = false;
        _isInYouTurn = false;
        _youTurnPath = null;
        _nextTrack = null;
        _youTurnCounter = 10; // Keep high so next U-turn path is created when conditions are met

        // CRITICAL: Reset guidance state to force global search on new offset track
        // Without this, the guidance uses the old CurrentLocationIndex which points to
        // the wrong position on the new track, causing the tractor to loop back
        _trackGuidanceState = null;

        // Update map visualization - clear the old turn path and next line
        // The active track will be updated by CalculateAutoSteerGuidance
        _mapService.SetYouTurnPath(null);
        _mapService.SetNextTrack(null);
        _mapService.SetIsInYouTurn(false);

        StatusMessage = $"Following path {_howManyPathsAway} ({(ConfigStore.ActualToolWidth - Tool.Overlap) * Math.Abs(_howManyPathsAway):F1}m offset)";
    }

    /// <summary>
    /// Calculate distance from current position to the headland boundary in the direction of travel.
    /// </summary>
    private double CalculateDistanceToHeadland(AgValoniaGPS.Models.Position currentPosition, double headingRadians)
    {
        if (_currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
            return double.MaxValue;

        // Use a simple raycast approach
        double minDistance = double.MaxValue;
        Vec2 pos = new Vec2(currentPosition.Easting, currentPosition.Northing);
        Vec2 dir = new Vec2(Math.Sin(headingRadians), Math.Cos(headingRadians));

        int intersectionCount = 0;
        int n = _currentHeadlandLine.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = _currentHeadlandLine[i];
            var p2 = _currentHeadlandLine[(i + 1) % n];

            // Ray-segment intersection
            Vec2 edge = new Vec2(p2.Easting - p1.Easting, p2.Northing - p1.Northing);
            Vec2 toP1 = new Vec2(p1.Easting - pos.Easting, p1.Northing - pos.Northing);

            double cross = dir.Easting * edge.Northing - dir.Northing * edge.Easting;
            if (Math.Abs(cross) < 1e-10) continue; // Parallel

            double t = (toP1.Easting * edge.Northing - toP1.Northing * edge.Easting) / cross;
            double u = (toP1.Easting * dir.Northing - toP1.Northing * dir.Easting) / cross;

            if (t > 0 && u >= 0 && u <= 1)
            {
                intersectionCount++;
                if (t < minDistance)
                    minDistance = t;
            }
        }

        // Debug: Log periodically to see what's happening
        if (_youTurnCounter % 120 == 0)
        {
            double headingDeg = headingRadians * 180.0 / Math.PI;
            _logger.LogDebug($"[Headland] Raycast: pos=({pos.Easting:F1},{pos.Northing:F1}), heading={headingDeg:F0}°, intersections={intersectionCount}, minDist={minDistance:F1}m, isHeadingSameWay={_isHeadingSameWay}");
        }

        return minDistance;
    }

    /// <summary>
    /// Determine which zone the tractor is currently in based on boundary polygons.
    /// Uses ray casting algorithm without allocations.
    /// </summary>
    private TractorZone DetermineCurrentZone(double easting, double northing)
    {
        // Check if inside headland (cultivated area) first - most common case
        if (_currentHeadlandLine != null && _currentHeadlandLine.Count >= 3)
        {
            if (IsPointInPolygon(_currentHeadlandLine, easting, northing))
                return TractorZone.InCultivatedArea;
        }

        // Check if inside outer boundary (headland zone)
        if (_currentBoundary?.OuterBoundary != null && _currentBoundary.OuterBoundary.IsValid)
        {
            if (_currentBoundary.OuterBoundary.IsPointInside(easting, northing))
                return TractorZone.InHeadland;
        }

        // Outside everything
        return TractorZone.OutsideBoundary;
    }

    /// <summary>
    /// Ray casting point-in-polygon test for Vec3 list (headland line).
    /// </summary>
    private static bool IsPointInPolygon(List<Vec3> polygon, double easting, double northing)
    {
        int n = polygon.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if (((pi.Northing > northing) != (pj.Northing > northing)) &&
                (easting < (pj.Easting - pi.Easting) * (northing - pi.Northing) / (pj.Northing - pi.Northing) + pi.Easting))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    #endregion

    #region YouTurn Path Creation

    /// <summary>
    /// Create a YouTurn path when approaching headland.
    /// Uses a simplified direct approach that creates entry leg, semicircle, and exit leg.
    /// </summary>
    private void CreateYouTurnPath(AgValoniaGPS.Models.Position currentPosition, double headingRadians, double abHeading)
    {
        var track = SelectedTrack;
        if (track == null || _currentHeadlandLine == null) return;

        // Turn direction was already set before ComputeNextTrack was called
        bool turnLeft = _isTurnLeft;

        _logger.LogDebug($"[YouTurn] Creating turn with YouTurnCreationService: direction={(_isTurnLeft ? "LEFT" : "RIGHT")}, isHeadingSameWay={_isHeadingSameWay}, pathsAway={_howManyPathsAway}");

        // Build the YouTurnCreationInput with proper boundary wiring
        var input = BuildYouTurnCreationInput(currentPosition, headingRadians, abHeading, turnLeft);
        if (input == null)
        {
            _logger.LogWarning("[YouTurn] Failed to build creation input - no boundary available?");
            return;
        }

        // Use the YouTurnCreationService to create the path
        var output = _youTurnCreationService.CreateTurn(input);

        if (output.Success && output.TurnPath != null && output.TurnPath.Count > 10)
        {
            var path = output.TurnPath;

            // Check for spiral/pretzel pattern by measuring total heading change
            // A proper U-turn should have ~180° total heading change, not 360°+
            double totalHeadingChange = 0;
            for (int i = 1; i < path.Count; i++)
            {
                double delta = path[i].Heading - path[i - 1].Heading;
                // Normalize to -π to π
                while (delta > Math.PI) delta -= 2 * Math.PI;
                while (delta < -Math.PI) delta += 2 * Math.PI;
                totalHeadingChange += Math.Abs(delta);
            }

            // If total heading change exceeds 270° (π * 1.5), it's likely a spiral - use simple fallback
            if (totalHeadingChange > Math.PI * 1.5)
            {
                _logger.LogWarning($"[YouTurn] Service path has excessive heading change ({totalHeadingChange * 180 / Math.PI:F0}°) - using simple fallback");
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    State.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
                return;
            }

            // Apply smoothing passes from config (1-50)
            int smoothingPasses = Guidance.UTurnSmoothing;
            if (smoothingPasses > 1 && path.Count > 4)
            {
                for (int pass = 0; pass < smoothingPasses; pass++)
                {
                    for (int i = 2; i < path.Count - 2; i++)
                    {
                        var prev = path[i - 1];
                        var curr = path[i];
                        var next = path[i + 1];

                        path[i] = new Vec3
                        {
                            Easting = (prev.Easting + curr.Easting + next.Easting) / 3.0,
                            Northing = (prev.Northing + curr.Northing + next.Northing) / 3.0,
                            Heading = curr.Heading
                        };
                    }
                }
            }

            State.YouTurn.TurnPath = path;
            _youTurnPath = path;
            _youTurnCounter = 0;
            StatusMessage = $"YouTurn path created ({path.Count} points)";
            _logger.LogDebug($"[YouTurn] Path created with {path.Count} points");

            _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
        }
        else
        {
            _logger.LogWarning($"[YouTurn] Service failed: {output.FailureReason ?? "unknown"} - using simple fallback");
            var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
            if (fallbackPath != null && fallbackPath.Count > 10)
            {
                State.YouTurn.TurnPath = fallbackPath;
                _youTurnPath = fallbackPath;
                _youTurnCounter = 0;
                _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
            }
        }
    }

    /// <summary>
    /// Find the track heading at the point where the CURRENT OFFSET TRACK crosses the headland.
    /// Uses the offset track, not the original, to get accurate heading for curves.
    /// </summary>
    private double FindTrackHeadingAtHeadland(Track track, Vec3 vehiclePos, bool headingSameWay)
    {
        if (_currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
            return track.Points[0].Heading; // Fallback

        // Create the current offset track (same logic as guidance)
        double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
        double offsetDistance = _howManyPathsAway * widthMinusOverlap;

        List<Vec3> searchPoints;
        if (Math.Abs(offsetDistance) < 0.01)
        {
            searchPoints = track.Points;
        }
        else
        {
            // Create clean offset curve that handles self-intersections on tight curves
            searchPoints = CurveProcessing.CreateOffsetCurve(track.Points, offsetDistance);
        }

        // Find nearest point to vehicle on the offset track
        int nearestIdx = 0;
        double minDistSq = double.MaxValue;
        for (int i = 0; i < searchPoints.Count; i++)
        {
            double dx = searchPoints[i].Easting - vehiclePos.Easting;
            double dy = searchPoints[i].Northing - vehiclePos.Northing;
            double distSq = dx * dx + dy * dy;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                nearestIdx = i;
            }
        }

        // Search from nearest point in direction of travel
        int step = headingSameWay ? 1 : -1;
        int endIdx = headingSameWay ? searchPoints.Count - 1 : 0;

        for (int i = nearestIdx; i != endIdx; i += step)
        {
            var p1 = searchPoints[i];
            var p2 = searchPoints[i + step];

            // Check if this segment crosses the headland
            for (int j = 0; j < _currentHeadlandLine.Count; j++)
            {
                var h1 = _currentHeadlandLine[j];
                var h2 = _currentHeadlandLine[(j + 1) % _currentHeadlandLine.Count];

                if (SegmentsIntersect(p1.Easting, p1.Northing, p2.Easting, p2.Northing,
                                      h1.Easting, h1.Northing, h2.Easting, h2.Northing))
                {
                    // Found intersection - return the track heading at this segment
                    _logger.LogDebug($"[YouTurn] Found headland intersection on offset track (path {_howManyPathsAway}) at index {i}, heading={p1.Heading * 180 / Math.PI:F1}°");
                    return p1.Heading;
                }
            }
        }

        // No intersection found, use heading at nearest point
        return searchPoints[nearestIdx].Heading;
    }

    /// <summary>
    /// Check if two line segments intersect.
    /// </summary>
    private bool SegmentsIntersect(double x1, double y1, double x2, double y2,
                                   double x3, double y3, double x4, double y4)
    {
        double d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(d) < 1e-10) return false;

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / d;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / d;

        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    /// <summary>
    /// Build the YouTurnCreationInput with proper boundary wiring.
    ///
    /// The IsPointInsideTurnArea delegate must return:
    /// - 0 = point is in the FIELD (safe to drive, inside headland boundary)
    /// - != 0 = point is in the TURN AREA (headland zone, where turn arc should be)
    ///
    /// We set this up with:
    /// - turnAreaPolygons[0] = outer field boundary (outer limit)
    /// - turnAreaPolygons[1] = headland boundary (inner limit, marks the field)
    ///
    /// So points between outer and headland return 0 (in outer but not in inner = headland zone... wait, that's wrong)
    /// Actually TurnAreaService returns 0 if in outer and NOT in any inner.
    /// So we need to INVERT the logic or structure it differently.
    ///
    /// Simpler approach: Create a custom delegate that directly tests:
    /// - If point is OUTSIDE outer boundary -> return 1 (out of bounds)
    /// - If point is INSIDE headland boundary (in the field) -> return 0 (safe)
    /// - Otherwise (in headland zone) -> return 1 (turn area)
    /// </summary>
    private YouTurnCreationInput? BuildYouTurnCreationInput(
        AgValoniaGPS.Models.Position currentPosition,
        double headingRadians,
        double abHeading,
        bool turnLeft)
    {
        // Need boundary to create turn boundaries
        if (_currentBoundary?.OuterBoundary == null || !_currentBoundary.OuterBoundary.IsValid)
        {
            _logger.LogDebug($"[YouTurn] No valid outer boundary available");
            return null;
        }

        var track = SelectedTrack;
        if (track == null)
        {
            _logger.LogDebug($"[YouTurn] No track selected");
            return null;
        }

        // Tool/implement width from configuration (use actual width from sections)
        double toolWidth = ConfigStore.ActualToolWidth;

        // Total headland width from the headland multiplier setting
        double totalHeadlandWidth = HeadlandCalculatedWidth;

        // Create outer boundary Vec3 list
        var outerPoints = _currentBoundary.OuterBoundary.Points
            .Select(p => new Vec2(p.Easting, p.Northing))
            .ToList();
        var outerBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(outerPoints);

        // Create turn boundary: controls where the outermost point of the turn can reach
        // distanceFromBoundary = 0 means turn can reach the outer boundary
        // distanceFromBoundary > 0 means turn stays that far inside
        // distanceFromBoundary < 0 means turn can extend past the outer boundary
        double distanceFromBoundary = Guidance.UTurnDistanceFromBoundary;
        double turnBoundaryOffset = distanceFromBoundary;
        _logger.LogDebug($"[YouTurn] distanceFromBoundary={distanceFromBoundary:F1}m, turnBoundaryOffset={turnBoundaryOffset:F1}m");

        List<Vec2>? turnBoundaryVec2;
        if (turnBoundaryOffset > 0.1)
        {
            // Positive: offset inward
            turnBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, turnBoundaryOffset);
        }
        else if (turnBoundaryOffset < -0.1)
        {
            // Negative: offset outward (turn starts outside boundary)
            turnBoundaryVec2 = _polygonOffsetService.CreateOutwardOffset(outerPoints, -turnBoundaryOffset);
        }
        else
        {
            // Near zero: use outer boundary directly
            turnBoundaryVec2 = outerPoints;
        }
        if (turnBoundaryVec2 == null || turnBoundaryVec2.Count < 3)
        {
            _logger.LogDebug($"[YouTurn] Offset failed, using outer boundary directly");
            turnBoundaryVec2 = outerPoints;
        }
        var turnBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(turnBoundaryVec2);

        // Create headland boundary: outer boundary offset inward by total headland width
        // This marks the inner edge of the turn zone (where the field starts)
        var headlandBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, totalHeadlandWidth);
        if (headlandBoundaryVec2 == null || headlandBoundaryVec2.Count < 3)
        {
            _logger.LogDebug($"[YouTurn] Failed to create headland boundary");
            return null;
        }
        var headlandBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(headlandBoundaryVec2);

        // Create the BoundaryTurnLine for the target turn boundary (where turn tangents)
        var boundaryTurnLines = new List<BoundaryTurnLine>
        {
            new BoundaryTurnLine
            {
                Points = turnBoundaryVec3,
                BoundaryIndex = 0
            }
        };

        // HeadlandWidth = distance from headland boundary to turn boundary
        double headlandWidthForTurn = Math.Max(totalHeadlandWidth - toolWidth, toolWidth);

        // Create IsPointInsideTurnArea delegate
        // Returns: 0 = OK to place turn here, != 0 = out of allowed zone
        // The user controls how far into headland via distanceFromBoundary setting
        // We allow turns up to (or past) the configured boundary
        Func<Vec3, int> isPointInsideTurnArea = (point) =>
        {
            // Use the turn boundary (which accounts for distanceFromBoundary) as the limit
            // Points inside the turn boundary are OK (return 0)
            // Points outside the turn boundary are in the restricted zone (return 1)
            if (GeometryMath.IsPointInPolygon(turnBoundaryVec3, point))
            {
                return 0; // Inside allowed zone
            }

            // Point is outside the configured turn boundary
            return 1; // In restricted area
        };

        // Build the input
        var input = new YouTurnCreationInput
        {
            TurnType = YouTurnType.AlbinStyle,
            IsTurnLeft = turnLeft,
            GuidanceType = GuidanceLineType.ABLine,

            // Boundary data - the turn line the path should tangent
            BoundaryTurnLines = boundaryTurnLines,

            // Custom delegate for turn area testing
            IsPointInsideTurnArea = isPointInsideTurnArea,

            // AB line guidance data
            // For curves, use the track heading at the headland intersection, not at vehicle position
            ABHeading = track.Points.Count > 2
                ? FindTrackHeadingAtHeadland(track, new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians), _isHeadingSameWay)
                : abHeading,
            // Calculate reference point on the CURRENT track near the vehicle position
            // (not at the extended endpoints which could be kilometers away)
            ABReferencePoint = CalculateCurrentTrackReferencePoint(track, toolWidth, abHeading,
                new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians)),
            IsHeadingSameWay = _isHeadingSameWay,

            // Vehicle position and configuration
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            ToolWidth = toolWidth,
            ToolOverlap = Tool.Overlap,
            ToolOffset = Tool.Offset,
            TurnRadius = Guidance.UTurnRadius,

            // Turn parameters - use pre-calculated offset from ComputeNextTrack (matches cyan line exactly)
            TurnOffset = NextTrackTurnOffset,
            RowSkipsWidth = UTurnSkipRows, // Kept for fallback/logging
            TurnStartOffset = 0,
            HowManyPathsAway = _howManyPathsAway,
            NudgeDistance = 0.0,
            TrackMode = 0, // Standard mode

            // State machine
            MakeUTurnCounter = _youTurnCounter + 10, // Ensure we pass the throttle check

            // Leg length - use user's UTurnExtension setting directly
            LegLength = Guidance.UTurnExtension,
            YouTurnLegExtensionMultiplier = 2.5, // Fallback if LegLength not set
            HeadlandWidth = headlandWidthForTurn
        };

        _logger.LogDebug($"[YouTurn] Input built: toolWidth={toolWidth:F1}m, totalHeadland={totalHeadlandWidth:F1}m, headlandWidthForTurn={headlandWidthForTurn:F1}m, turnBoundaryPoints={turnBoundaryVec3.Count}, headlandPoints={headlandBoundaryVec3.Count}");

        return input;
    }

    /// <summary>
    /// Calculate the reference point where the CURRENT OFFSET TRACK crosses the headland, ahead of the vehicle.
    /// This is the starting point for the U-turn path.
    ///
    /// Key insight: We create the offset track first, then find where IT crosses the headland.
    /// This is correct for curves where the offset track crosses at a different position than the base track.
    /// </summary>
    private Vec2 CalculateCurrentTrackReferencePoint(Track track, double toolWidth, double abHeading, Vec3 vehiclePosition)
    {
        if (track.Points.Count < 2)
            return new Vec2(vehiclePosition.Easting, vehiclePosition.Northing);

        // First, create the current offset track (same logic as in guidance)
        double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
        double offsetDistance = _howManyPathsAway * widthMinusOverlap;

        Track currentOffsetTrack;
        if (Math.Abs(offsetDistance) < 0.01)
        {
            // No offset - use original track
            currentOffsetTrack = track;
        }
        else
        {
            // Create clean offset curve that handles self-intersections on tight curves
            var offsetPoints = CurveProcessing.CreateOffsetCurve(track.Points, offsetDistance);

            currentOffsetTrack = new Track
            {
                Name = $"Current path {_howManyPathsAway}",
                Points = offsetPoints,
                Type = track.Type,
                IsVisible = false,
                IsActive = false
            };
        }

        // Now find where the OFFSET TRACK crosses the headland (no additional offset needed)
        var intersection = FindTrackHeadlandIntersectionAhead(currentOffsetTrack, vehiclePosition, _isHeadingSameWay);
        if (intersection.HasValue)
        {
            _logger.LogDebug($"[YouTurn] Reference point: offset track (path {_howManyPathsAway}) crosses headland at ({intersection.Value.Easting:F1}, {intersection.Value.Northing:F1})");
            return intersection.Value;
        }

        // Fallback: project vehicle position onto the offset track
        var ptA = currentOffsetTrack.Points[0];
        var ptB = currentOffsetTrack.Points[currentOffsetTrack.Points.Count - 1];

        // Vector from A to B
        double abE = ptB.Easting - ptA.Easting;
        double abN = ptB.Northing - ptA.Northing;
        double abLengthSq = abE * abE + abN * abN;

        // Vector from A to vehicle
        double avE = vehiclePosition.Easting - ptA.Easting;
        double avN = vehiclePosition.Northing - ptA.Northing;

        // Project vehicle onto track: t = (AV · AB) / |AB|²
        double t = (avE * abE + avN * abN) / abLengthSq;
        t = Math.Max(0, Math.Min(1, t));

        // Calculate the projected point on the offset track
        double projEasting = ptA.Easting + t * abE;
        double projNorthing = ptA.Northing + t * abN;

        _logger.LogDebug($"[YouTurn] Reference point: fallback to vehicle projection on offset track, path={_howManyPathsAway}");

        return new Vec2(projEasting, projNorthing);
    }

    /// <summary>
    /// Find where the track crosses the headland ahead of the vehicle in the direction of travel.
    /// Returns the intersection point, or null if no intersection found ahead.
    /// </summary>
    private Vec2? FindTrackHeadlandIntersectionAhead(Track track, Vec3 vehiclePos, bool headingSameWay)
    {
        if (_currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
            return null;

        if (track.Points.Count < 2)
            return null;

        // For curves, search along track points from vehicle position in direction of travel
        if (track.Points.Count > 2)
        {
            // Find nearest point to vehicle
            int nearestIdx = 0;
            double minDistSq = double.MaxValue;
            for (int i = 0; i < track.Points.Count; i++)
            {
                double pdx = track.Points[i].Easting - vehiclePos.Easting;
                double pdy = track.Points[i].Northing - vehiclePos.Northing;
                double distSq = pdx * pdx + pdy * pdy;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearestIdx = i;
                }
            }

            // Search from nearest point in direction of travel
            int step = headingSameWay ? 1 : -1;
            int endIdx = headingSameWay ? track.Points.Count - 1 : 0;

            for (int i = nearestIdx; i != endIdx; i += step)
            {
                var p1 = track.Points[i];
                var p2 = track.Points[i + step];

                // Check if this segment crosses the headland
                for (int j = 0; j < _currentHeadlandLine.Count; j++)
                {
                    var h1 = _currentHeadlandLine[j];
                    var h2 = _currentHeadlandLine[(j + 1) % _currentHeadlandLine.Count];

                    var intersection = GetLineIntersection(
                        p1.Easting, p1.Northing, p2.Easting, p2.Northing,
                        h1.Easting, h1.Northing, h2.Easting, h2.Northing);

                    if (intersection.HasValue)
                    {
                        return intersection;
                    }
                }
            }
            return null;
        }

        // For AB lines, extend the line and find intersection
        var ptA = track.Points[0];
        var ptB = track.Points[1];

        // Determine which direction is "ahead" based on heading
        Vec3 startPoint, endPoint;
        if (headingSameWay)
        {
            startPoint = ptA;
            endPoint = ptB;
        }
        else
        {
            startPoint = ptB;
            endPoint = ptA;
        }

        // Extend the line far beyond the track endpoints
        double dx = endPoint.Easting - startPoint.Easting;
        double dy = endPoint.Northing - startPoint.Northing;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) return null;

        // Extend to 1000m past the endpoint (should cover any field)
        double extendedE = endPoint.Easting + (dx / len) * 1000;
        double extendedN = endPoint.Northing + (dy / len) * 1000;

        // Find where this extended line crosses the headland, starting from vehicle position
        Vec2? closestIntersection = null;
        double closestDist = double.MaxValue;

        for (int j = 0; j < _currentHeadlandLine.Count; j++)
        {
            var h1 = _currentHeadlandLine[j];
            var h2 = _currentHeadlandLine[(j + 1) % _currentHeadlandLine.Count];

            var intersection = GetLineIntersection(
                vehiclePos.Easting, vehiclePos.Northing, extendedE, extendedN,
                h1.Easting, h1.Northing, h2.Easting, h2.Northing);

            if (intersection.HasValue)
            {
                double distSq = (intersection.Value.Easting - vehiclePos.Easting) * (intersection.Value.Easting - vehiclePos.Easting) +
                               (intersection.Value.Northing - vehiclePos.Northing) * (intersection.Value.Northing - vehiclePos.Northing);
                if (distSq < closestDist)
                {
                    closestDist = distSq;
                    closestIntersection = intersection;
                }
            }
        }

        return closestIntersection;
    }

    /// <summary>
    /// Get the intersection point of two line segments, or null if they don't intersect.
    /// </summary>
    private Vec2? GetLineIntersection(double x1, double y1, double x2, double y2,
                                      double x3, double y3, double x4, double y4)
    {
        double d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(d) < 1e-10) return null;

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / d;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / d;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            double x = x1 + t * (x2 - x1);
            double y = y1 + t * (y2 - y1);
            return new Vec2(x, y);
        }

        return null;
    }

    /// <summary>
    /// Create a simple geometric U-turn path with entry leg, semicircle arc, and exit leg.
    /// This is used as a fallback when the YouTurnCreationService produces an invalid path.
    /// </summary>
    private List<Vec3> CreateSimpleUTurnPath(AgValoniaGPS.Models.Position currentPosition, double headingRadians, double abHeading, bool turnLeft)
    {
        var path = new List<Vec3>();

        // Parameters - use the pre-calculated NextTrackTurnOffset which matches the cyan "next track" line
        double pointSpacing = 0.5; // meters between path points
        double turnOffset = NextTrackTurnOffset; // Use pre-calculated offset to match cyan line exactly

        // Fallback if NextTrackTurnOffset wasn't set
        if (turnOffset < 0.1)
        {
            int rowSkipWidth = UTurnSkipRows;
            double trackWidth = ConfigStore.ActualToolWidth - Tool.Overlap;
            turnOffset = trackWidth * (rowSkipWidth + 1);
            _logger.LogDebug($"[YouTurn] Using fallback turnOffset calculation: {turnOffset:F2}m");
        }

        // Turn radius from config, with fallback calculation
        double turnRadius = Guidance.UTurnRadius;

        // If config radius is too small for the track offset, use geometric minimum
        double geometricMinRadius = turnOffset / 2.0;
        if (turnRadius < geometricMinRadius)
        {
            turnRadius = geometricMinRadius;
        }

        // Absolute minimum turn radius constraint
        double minTurnRadius = 4.0;
        if (turnRadius < minTurnRadius)
        {
            turnRadius = minTurnRadius;
        }

        // Get the heading we're traveling (adjusted for same/opposite to AB)
        double travelHeading = abHeading;
        if (!_isHeadingSameWay)
        {
            travelHeading += Math.PI;
            if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
        }

        // Exit heading is 180° opposite (going back toward field)
        double exitHeading = travelHeading + Math.PI;
        if (exitHeading >= Math.PI * 2) exitHeading -= Math.PI * 2;

        // Perpendicular direction (toward next track)
        double perpAngle = turnLeft ? (travelHeading - Math.PI / 2) : (travelHeading + Math.PI / 2);

        // Calculate the headland boundary point on CURRENT track
        double distToHeadland = _distanceToHeadland;
        double headlandBoundaryEasting = currentPosition.Easting + Math.Sin(travelHeading) * distToHeadland;
        double headlandBoundaryNorthing = currentPosition.Northing + Math.Cos(travelHeading) * distToHeadland;

        // Leg lengths
        double distanceFromBoundary = Guidance.UTurnDistanceFromBoundary;
        double headlandLegLength = HeadlandDistance - turnRadius - distanceFromBoundary;
        double fieldLegLength = Guidance.UTurnExtension;

        _logger.LogDebug($"[YouTurn] Simple path: turnOffset={turnOffset:F1}m, turnRadius={turnRadius:F1}m");
        _logger.LogDebug($"[YouTurn] HeadlandDistance={HeadlandDistance:F1}m, headlandLegLength={headlandLegLength:F1}m");

        // Calculate key waypoints
        double entryStartE = headlandBoundaryEasting - Math.Sin(travelHeading) * fieldLegLength;
        double entryStartN = headlandBoundaryNorthing - Math.Cos(travelHeading) * fieldLegLength;

        double arcStartE = headlandBoundaryEasting + Math.Sin(travelHeading) * headlandLegLength;
        double arcStartN = headlandBoundaryNorthing + Math.Cos(travelHeading) * headlandLegLength;

        double arcCenterE = arcStartE + Math.Sin(perpAngle) * turnRadius;
        double arcCenterN = arcStartN + Math.Cos(perpAngle) * turnRadius;

        double arcDiameter = 2.0 * turnRadius;

        // Note: Arc apex boundary check removed - it was too restrictive.
        // The arc extends into the headland zone which exists precisely for turns.
        // WouldNextLineBeInsideBoundary already validates the next track is valid.
        // The exit end check below ensures we end up on a valid track.

        double exitEndE = entryStartE + Math.Sin(perpAngle) * turnOffset;
        double exitEndN = entryStartN + Math.Cos(perpAngle) * turnOffset;

        if (!IsPointInsideBoundary(exitEndE, exitEndN))
        {
            _logger.LogDebug($"[YouTurn] Exit end is outside boundary - not creating U-turn");
            return path;
        }

        // Build entry leg
        double totalEntryLength = fieldLegLength + headlandLegLength;
        int totalEntryPoints = (int)(totalEntryLength / pointSpacing);

        for (int i = 0; i <= totalEntryPoints; i++)
        {
            double dist = i * pointSpacing;
            Vec3 pt = new Vec3
            {
                Easting = entryStartE + Math.Sin(travelHeading) * dist,
                Northing = entryStartN + Math.Cos(travelHeading) * dist,
                Heading = travelHeading
            };
            path.Add(pt);
        }

        // Build semicircle arc
        int arcPoints = Math.Max((int)(Math.PI * turnRadius / pointSpacing), 20);

        for (int i = 1; i <= arcPoints; i++)
        {
            double t = (double)i / arcPoints;
            double startAngle = Math.Atan2(arcStartE - arcCenterE, arcStartN - arcCenterN);
            double sweepAngle = turnLeft ? (-Math.PI * t) : (Math.PI * t);
            double currentAngle = startAngle + sweepAngle;

            double ptE = arcCenterE + Math.Sin(currentAngle) * turnRadius;
            double ptN = arcCenterN + Math.Cos(currentAngle) * turnRadius;

            double tangentHeading = currentAngle + (turnLeft ? -Math.PI / 2 : Math.PI / 2);
            if (tangentHeading < 0) tangentHeading += Math.PI * 2;
            if (tangentHeading >= Math.PI * 2) tangentHeading -= Math.PI * 2;

            Vec3 pt = new Vec3
            {
                Easting = ptE,
                Northing = ptN,
                Heading = tangentHeading
            };
            path.Add(pt);
        }

        // Build exit leg
        var lastArcPoint = path[path.Count - 1];
        double actualArcEndE = lastArcPoint.Easting;
        double actualArcEndN = lastArcPoint.Northing;

        double exitStartE = arcStartE + Math.Sin(perpAngle) * turnOffset;
        double exitStartN = arcStartN + Math.Cos(perpAngle) * turnOffset;

        double arcToExitDist = Math.Sqrt(Math.Pow(exitStartE - actualArcEndE, 2) + Math.Pow(exitStartN - actualArcEndN, 2));
        if (arcToExitDist > pointSpacing)
        {
            int connectPoints = (int)(arcToExitDist / pointSpacing);
            for (int i = 1; i <= connectPoints; i++)
            {
                double t = (double)i / (connectPoints + 1);
                Vec3 pt = new Vec3
                {
                    Easting = actualArcEndE + (exitStartE - actualArcEndE) * t,
                    Northing = actualArcEndN + (exitStartN - actualArcEndN) * t,
                    Heading = exitHeading
                };
                path.Add(pt);
            }
        }

        int totalExitPoints = (int)(totalEntryLength / pointSpacing);

        for (int i = 1; i <= totalExitPoints; i++)
        {
            double dist = i * pointSpacing;
            Vec3 pt = new Vec3
            {
                Easting = exitStartE + Math.Sin(exitHeading) * dist,
                Northing = exitStartN + Math.Cos(exitHeading) * dist,
                Heading = exitHeading
            };
            path.Add(pt);
        }

        _logger.LogDebug($"[YouTurn] Simple fallback path has {path.Count} points");

        // Apply smoothing passes from config
        int smoothingPasses = Guidance.UTurnSmoothing;
        if (smoothingPasses > 1 && path.Count > 4)
        {
            for (int pass = 0; pass < smoothingPasses; pass++)
            {
                for (int i = 2; i < path.Count - 2; i++)
                {
                    var prev = path[i - 1];
                    var curr = path[i];
                    var next = path[i + 1];

                    path[i] = new Vec3
                    {
                        Easting = (prev.Easting + curr.Easting + next.Easting) / 3.0,
                        Northing = (prev.Northing + curr.Northing + next.Northing) / 3.0,
                        Heading = curr.Heading
                    };
                }
            }
        }

        return path;
    }

    #endregion

    #region YouTurn Guidance

    /// <summary>
    /// Calculate steering guidance while following the YouTurn path.
    /// </summary>
    private void CalculateYouTurnGuidance(AgValoniaGPS.Models.Position currentPosition)
    {
        if (_youTurnPath == null || _youTurnPath.Count == 0) return;

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;
        double speed = currentPosition.Speed * 3.6; // km/h

        var input = new YouTurnGuidanceInput
        {
            TurnPath = _youTurnPath,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            SteerPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            Wheelbase = Vehicle.Wheelbase,
            MaxSteerAngle = Vehicle.MaxSteerAngle,
            UseStanley = false, // Use Pure Pursuit for smoother turns
            GoalPointDistance = Guidance.GoalPointLookAheadHold,
            UTurnCompensation = Guidance.UTurnCompensation,
            FixHeading = headingRadians,
            AvgSpeed = speed,
            IsReverse = false,
            UTurnStyle = 0 // Albin style
        };

        var output = _youTurnGuidanceService.CalculateGuidance(input);

        if (output.IsTurnComplete)
        {
            // Turn complete - switch to next line and reset state
            _logger.LogDebug("[YouTurn] Guidance detected turn complete, calling CompleteYouTurn");
            CompleteYouTurn();
        }
        else
        {
            // Apply steering from YouTurn guidance with compensation
            SimulatorSteerAngle = output.SteerAngle * Guidance.UTurnCompensation;

            // Update centralized guidance state
            State.Guidance.CrossTrackError = output.DistanceFromCurrentLine;
            State.Guidance.SteerAngle = output.SteerAngle;

            // Legacy property (for existing bindings - display in cm)
            CrossTrackError = output.DistanceFromCurrentLine * 100;
        }
    }

    #endregion
}
