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
    private bool _hasCompletedFirstTurn; // Track if we've done at least one turn
    private Track? _nextTrack; // The next track to switch to after U-turn completes

    /// <summary>
    /// Pre-calculated perpendicular offset to next track (always positive, in meters).
    /// This is the authoritative value for U-turn arc width - use this instead of recalculating.
    /// </summary>
    public double NextTrackTurnOffset { get; private set; }

    private int _howManyPathsAway; // Which parallel offset line we're on (like AgOpenGPS)
    private Vec2? _lastTurnCompletionPosition; // Position where last U-turn completed - used to prevent immediate re-triggering

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

    #region YouTurn Processing

    /// <summary>
    /// Process YouTurn - check distance to headland, create turn path if needed, trigger turn.
    /// </summary>
    private void ProcessYouTurn(AgValoniaGPS.Models.Position currentPosition)
    {
        var track = SelectedTrack;
        if (track == null || track.Points.Count < 2 || _currentHeadlandLine == null) return;

        var trackPointA = track.Points[0];
        var trackPointB = track.Points[track.Points.Count - 1];

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

        // Calculate track heading to determine direction
        double abDx = trackPointB.Easting - trackPointA.Easting;
        double abDy = trackPointB.Northing - trackPointA.Northing;
        double abHeading = Math.Atan2(abDx, abDy);

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
        if (isAlignedWithABLine)
        {
            // IMPORTANT: Calculate distance using the travel heading (AB heading adjusted for direction),
            // not the vehicle heading. This ensures the raycast direction matches the path construction
            // direction, preventing arc positioning errors when vehicle heading differs from AB heading.
            double travelHeading = abHeading;
            if (!_isHeadingSameWay)
            {
                travelHeading += Math.PI;
                if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
            }
            _distanceToHeadland = CalculateDistanceToHeadland(currentPosition, travelHeading);
        }
        else
        {
            _distanceToHeadland = double.MaxValue;  // Don't detect headland if not aligned
        }

        // Create U-turn path when approaching the headland ahead
        // The raycast already looks in the direction we're heading, so it finds the headland in front
        // We only need to check if we're within a reasonable trigger distance (not too close, not too far)
        double minDistanceToCreate = 30.0;  // meters - don't create if we're already too close (in the turn zone)

        // The headland must be ahead of us (raycast found something) and not too close
        // AND we must be aligned with the AB line (not mid-turn)
        bool headlandAhead = _distanceToHeadland > minDistanceToCreate &&
                             _distanceToHeadland < double.MaxValue &&
                             isAlignedWithABLine;

        // Debug: Log status periodically
        if (_youTurnPath == null && !_isInYouTurn && _youTurnCounter % 60 == 0)
        {
            _logger.LogDebug($"[YouTurn] Status: distToHeadland={_distanceToHeadland:F1}m, headlandAhead={headlandAhead}, aligned={isAlignedWithABLine}, counter={_youTurnCounter}");
        }

        if (_youTurnPath == null && _youTurnCounter >= 4 && !_isInYouTurn && headlandAhead)
        {
            // First check if a U-turn would put us outside the boundary
            if (WouldNextLineBeInsideBoundary(track, abHeading))
            {
                _logger.LogDebug($"[YouTurn] Creating turn path - dist ahead: {_distanceToHeadland:F1}m");
                // Determine turn direction BEFORE computing next track
                // Same direction = turn left, opposite = turn right (for zig-zag pattern)
                _isTurnLeft = _isHeadingSameWay;
                _wasHeadingSameWayAtTurnStart = _isHeadingSameWay;
                // Compute the next track offset BEFORE creating the path (so NextTrackTurnOffset is set)
                ComputeNextTrack(track, abHeading);
                CreateYouTurnPath(currentPosition, headingRadians, abHeading);
            }
            else
            {
                _logger.LogDebug("[YouTurn] Next line would be outside boundary - stopping U-turns");
                StatusMessage = "End of field reached";
            }
        }
        // If we have a valid path and distance is close, trigger the turn
        else if (_youTurnPath != null && _youTurnPath.Count > 2 && !_isYouTurnTriggered && !_isInYouTurn)
        {
            // Calculate distance to turn start point
            double distToTurnStart = Math.Sqrt(
                Math.Pow(currentPosition.Easting - _youTurnPath[0].Easting, 2) +
                Math.Pow(currentPosition.Northing - _youTurnPath[0].Northing, 2));

            // Trigger when within 2 meters of turn start
            if (distToTurnStart <= 2.0)
            {
                // Update centralized state
                State.YouTurn.IsTriggered = true;
                State.YouTurn.IsExecuting = true;

                _isYouTurnTriggered = true;
                _isInYouTurn = true;
                StatusMessage = "YouTurn triggered!";
                _logger.LogDebug($"[YouTurn] Triggered at distance {distToTurnStart:F2}m from turn start");
                // Note: ComputeNextTrack was already called when the path was created
            }
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

        // Calculate where the next line would be (use config skip width)
        // skipWidth=0 means adjacent passes, skipWidth=1 means skip 1 row, etc.
        int rowSkipWidth = Guidance.UTurnSkipWidth;
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

        // Calculate the perpendicular direction (90 degrees from AB heading)
        // Positive offset is to the LEFT of the AB line (when looking from A to B)
        double perpAngle = abHeading + Math.PI / 2;
        double offsetEasting = Math.Sin(perpAngle) * nextDistAway;
        double offsetNorthing = Math.Cos(perpAngle) * nextDistAway;

        // Create the next track for visualization (relative to reference track)
        _nextTrack = Track.FromABLine(
            $"Path {nextPathsAway}",
            new Vec3(refPointA.Easting + offsetEasting, refPointA.Northing + offsetNorthing, abHeading),
            new Vec3(refPointB.Easting + offsetEasting, refPointB.Northing + offsetNorthing, abHeading));
        _nextTrack.IsActive = false;

        _logger.LogDebug($"[YouTurn] Turn {(_isTurnLeft ? "LEFT" : "RIGHT")}, heading {(_isHeadingSameWay ? "SAME" : "OPPOSITE")} way");
        _logger.LogDebug($"[YouTurn] Offset {(positiveOffset ? "positive" : "negative")}: path {_howManyPathsAway} -> {nextPathsAway} ({nextDistAway:F1}m)");

        // Update map visualization
        _mapService.SetNextTrack(_nextTrack);
        _mapService.SetIsInYouTurn(true);
    }

    /// <summary>
    /// Complete the U-turn: switch to the next line and reset state.
    /// </summary>
    private void CompleteYouTurn()
    {
        // Save the turn completion position (end of turn path) to prevent immediate re-triggering
        if (_youTurnPath != null && _youTurnPath.Count > 0)
        {
            var endPoint = _youTurnPath[_youTurnPath.Count - 1];
            _lastTurnCompletionPosition = new Vec2(endPoint.Easting, endPoint.Northing);
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
        _hasCompletedFirstTurn = true;

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

        // Update map visualization - clear the old turn path and next line
        // The active line will be updated by UpdateActiveLineVisualization in CalculateAutoSteerGuidance
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

        try
        {
            // Build the YouTurnCreationInput with proper boundary wiring
            var input = BuildYouTurnCreationInput(currentPosition, headingRadians, abHeading, turnLeft);
            if (input == null)
            {
                _logger.LogDebug($"[YouTurn] Failed to build creation input - using simple fallback");
                var earlyFallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
                if (earlyFallbackPath != null && earlyFallbackPath.Count > 10)
                {
                    State.YouTurn.TurnPath = earlyFallbackPath;
                    _youTurnPath = earlyFallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
                return;
            }

            // Use the YouTurnCreationService to create the path
            var output = _youTurnCreationService.CreateTurn(input);

            if (output.Success && output.TurnPath != null && output.TurnPath.Count > 10)
            {
                // User controls how far the turn extends via distanceFromBoundary setting
                // Don't reject paths that go past the outer boundary - that may be intentional
                var path = output.TurnPath;

                // Apply smoothing passes from config (1-50)
                int smoothingPasses = Guidance.UTurnSmoothing;
                if (smoothingPasses > 1 && path.Count > 4)
                {
                    for (int pass = 0; pass < smoothingPasses; pass++)
                    {
                        // Smooth interior points only (preserve start and end)
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
                    _logger.LogDebug($"[YouTurn] Applied {smoothingPasses} smoothing passes to service path");
                }

                State.YouTurn.TurnPath = path;
                _youTurnPath = path;
                _youTurnCounter = 0;
                StatusMessage = $"YouTurn path created ({path.Count} points)";
                _logger.LogDebug($"[YouTurn] Service path created with {path.Count} points, distToTurnLine={output.DistancePivotToTurnLine:F1}m");

                // Update map to show the turn path
                _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                return; // Path is valid, we're done
            }
            else
            {
                _logger.LogDebug($"[YouTurn] Service creation failed: {output.FailureReason ?? "unknown"}, using simple fallback");
            }

            // Fall back to simple geometric approach (with boundary checking built in)
            var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
            if (fallbackPath != null && fallbackPath.Count > 10)
            {
                State.YouTurn.TurnPath = fallbackPath;
                _youTurnPath = fallbackPath;
                _youTurnCounter = 0;
                _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
            }
            else
            {
                // No valid path - clear any existing
                _youTurnPath = null;
                _mapService.SetYouTurnPath(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"[YouTurn] Exception creating path: {ex.Message}");
            // Fall back to simple geometric approach
            try
            {
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    State.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
            }
            catch { }
        }
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
            ABHeading = abHeading,
            // Calculate reference point on the CURRENT track (not the original AB line)
            // Offset PointA perpendicular to the AB heading by howManyPathsAway * trackWidth
            ABReferencePoint = CalculateCurrentTrackReferencePoint(track, toolWidth, abHeading),
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
    /// Calculate a reference point on the current track (offset from the original AB line).
    /// The track number is determined by _howManyPathsAway.
    /// </summary>
    private Vec2 CalculateCurrentTrackReferencePoint(Track track, double toolWidth, double abHeading)
    {
        if (track.Points.Count == 0)
            return new Vec2(0, 0);

        // Start with the first point on the original track
        double baseEasting = track.Points[0].Easting;
        double baseNorthing = track.Points[0].Northing;

        // Calculate perpendicular offset to get to the current track
        // The perpendicular direction is 90° from the AB heading
        double perpAngle = abHeading + Math.PI / 2.0;

        // The offset distance is howManyPathsAway * toolWidth
        double offsetDistance = _howManyPathsAway * toolWidth;

        // Apply the offset perpendicular to the AB line
        double offsetEasting = baseEasting + Math.Sin(perpAngle) * offsetDistance;
        double offsetNorthing = baseNorthing + Math.Cos(perpAngle) * offsetDistance;

        _logger.LogDebug($"[YouTurn] Reference point: howManyPathsAway={_howManyPathsAway}, offset={offsetDistance:F2}m, perpAngle={perpAngle * 180 / Math.PI:F1}°");

        return new Vec2(offsetEasting, offsetNorthing);
    }

    /// <summary>
    /// Create a simple U-turn path directly using geometry.
    /// This creates a SYMMETRICAL U-turn by calculating exact endpoint positions first,
    /// then building the path to connect them.
    /// </summary>
    private List<Vec3> CreateSimpleUTurnPath(AgValoniaGPS.Models.Position currentPosition, double headingRadians, double abHeading, bool turnLeft)
    {
        var path = new List<Vec3>();

        // Parameters - use ConfigurationStore values
        double pointSpacing = 0.5; // meters between path points
        int rowSkipWidth = Guidance.UTurnSkipWidth; // From config (0 = adjacent, 1 = skip 1 row, etc.)
        double trackWidth = ConfigStore.ActualToolWidth - Tool.Overlap; // Implement width minus overlap
        double turnOffset = trackWidth * (rowSkipWidth + 1); // Perpendicular distance to next track

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

        // Leg lengths - use config values
        // The arc extends turnRadius beyond the arc start (toward the outer boundary)
        // So: arc_top_position = headlandLegLength + turnRadius
        // We want arc_top to be at HeadlandDistance - distanceFromBoundary
        // Therefore: headlandLegLength = HeadlandDistance - turnRadius - distanceFromBoundary
        // Negative distanceFromBoundary pushes arc PAST the outer boundary (useful for trailing implements)
        double distanceFromBoundary = Guidance.UTurnDistanceFromBoundary;
        double headlandLegLength = HeadlandDistance - turnRadius - distanceFromBoundary;

        // How far path extends into cultivated area (entry/exit legs) - use UTurnExtension from config
        double fieldLegLength = Guidance.UTurnExtension;

        _logger.LogDebug($"[YouTurn] HeadlandBoundary: E={headlandBoundaryEasting:F1}, N={headlandBoundaryNorthing:F1}");
        _logger.LogDebug($"[YouTurn] HeadlandDistance={HeadlandDistance:F1}m, headlandLegLength={headlandLegLength:F1}m, turnRadius={turnRadius:F1}m, turnOffset={turnOffset:F1}m");
        _logger.LogDebug($"[YouTurn] Arc will extend to {headlandLegLength + turnRadius:F1}m past headland boundary (headland zone is {HeadlandDistance:F1}m)");

        // ============================================
        // CALCULATE KEY WAYPOINTS IN ABSOLUTE COORDINATES
        // ============================================
        // The U-turn connects two parallel AB lines separated by turnOffset.
        // Entry start and exit end must BOTH be in the cultivated area (outside headland).
        // The arc happens deep in the headland.

        // STEP 1: Calculate the ENTRY START position (green marker)
        // This is on the CURRENT track, fieldLegLength BEHIND the headland boundary
        double entryStartE = headlandBoundaryEasting - Math.Sin(travelHeading) * fieldLegLength;
        double entryStartN = headlandBoundaryNorthing - Math.Cos(travelHeading) * fieldLegLength;

        // STEP 2: Calculate the ARC START position
        // This is on the CURRENT track, deep in the headland
        double arcStartE = headlandBoundaryEasting + Math.Sin(travelHeading) * headlandLegLength;
        double arcStartN = headlandBoundaryNorthing + Math.Cos(travelHeading) * headlandLegLength;

        // STEP 3: Calculate the ARC CENTER (center of semicircle)
        // Perpendicular from arc start by turnRadius
        double arcCenterE = arcStartE + Math.Sin(perpAngle) * turnRadius;
        double arcCenterN = arcStartN + Math.Cos(perpAngle) * turnRadius;

        // STEP 4: Calculate the ARC END position
        // Arc end is where the semicircle ends: diameter = 2 * turnRadius from arcStart
        // (This may differ from turnOffset when turnRadius is clamped to minTurnRadius)
        double arcDiameter = 2.0 * turnRadius;
        double arcEndE = arcStartE + Math.Sin(perpAngle) * arcDiameter;
        double arcEndN = arcStartN + Math.Cos(perpAngle) * arcDiameter;

        // BOUNDARY CHECK: Verify the arc's apex (furthest point from field) is inside boundary
        // The arc apex is at the arc center + turnRadius in the travel direction
        double arcApexE = arcCenterE + Math.Sin(travelHeading) * turnRadius;
        double arcApexN = arcCenterN + Math.Cos(travelHeading) * turnRadius;

        if (!IsPointInsideBoundary(arcApexE, arcApexN))
        {
            _logger.LogDebug($"[YouTurn] Arc apex ({arcApexE:F1}, {arcApexN:F1}) is outside boundary - not creating U-turn");
            return path; // Return empty path - no valid U-turn possible
        }

        // STEP 5: Calculate the EXIT END position (red marker)
        // The exit end must be on the NEXT track, at the same distance from headland as entry start
        // Since perpAngle already points toward the next track (based on turnLeft and travelHeading),
        // we just need to offset by turnOffset in that direction
        double exitEndE = entryStartE + Math.Sin(perpAngle) * turnOffset;
        double exitEndN = entryStartN + Math.Cos(perpAngle) * turnOffset;

        // BOUNDARY CHECK: Verify the exit end (next track) is inside boundary
        if (!IsPointInsideBoundary(exitEndE, exitEndN))
        {
            _logger.LogDebug($"[YouTurn] Exit end ({exitEndE:F1}, {exitEndN:F1}) is outside boundary - not creating U-turn");
            return path; // Return empty path - next track is outside boundary
        }

        _logger.LogDebug($"[YouTurn] ExitEnd calc: entryStart({entryStartE:F1},{entryStartN:F1}) + perpAngle({perpAngle * 180 / Math.PI:F1}°) * {turnOffset:F1}m = ({exitEndE:F1},{exitEndN:F1})");
        _logger.LogDebug($"[YouTurn] perpAngle direction: turnLeft={turnLeft}, travelHeading={travelHeading * 180 / Math.PI:F1}°");

        _logger.LogDebug($"[YouTurn] turnOffset={turnOffset:F1}m, arcDiameter={arcDiameter:F1}m (2*turnRadius)");
        _logger.LogDebug($"[YouTurn] EntryStart (green): E={entryStartE:F1}, N={entryStartN:F1}");
        _logger.LogDebug($"[YouTurn] ExitEnd (red): E={exitEndE:F1}, N={exitEndN:F1} = entryStart + perpOffset({turnOffset:F1}m)");
        _logger.LogDebug($"[YouTurn] ArcStart: E={arcStartE:F1}, N={arcStartN:F1}");
        _logger.LogDebug($"[YouTurn] ArcEnd: E={arcEndE:F1}, N={arcEndN:F1} = arcStart + perpOffset({arcDiameter:F1}m)");

        // ============================================
        // BUILD PATH: Entry Leg
        // ============================================
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

        // ============================================
        // BUILD PATH: Semicircle Arc
        // ============================================
        // Generate arc points from arcStart to arcEnd around arcCenter
        int arcPoints = Math.Max((int)(Math.PI * turnRadius / pointSpacing), 20);

        for (int i = 1; i <= arcPoints; i++)
        {
            // Fraction around the arc (0 to 1)
            double t = (double)i / arcPoints;

            // Angle: start pointing back toward entry leg, sweep 180° toward exit leg
            // Start angle: direction from center to arcStart
            double startAngle = Math.Atan2(arcStartE - arcCenterE, arcStartN - arcCenterN);

            // Sweep direction in Easting/Northing coordinate system where:
            //   Easting = sin(angle), Northing = cos(angle)
            //   angle=0 is north, angle=π/2 is east, angle=π is south, angle=3π/2 is west
            // For left turn: arc center is to the left of travel direction
            //   We want to sweep AWAY from field (into headland), which means DECREASING angle
            // For right turn: arc center is to the right of travel direction
            //   We want to sweep AWAY from field (into headland), which means INCREASING angle
            double sweepAngle = turnLeft ? (-Math.PI * t) : (Math.PI * t);
            double currentAngle = startAngle + sweepAngle;

            // Point on arc
            double ptE = arcCenterE + Math.Sin(currentAngle) * turnRadius;
            double ptN = arcCenterN + Math.Cos(currentAngle) * turnRadius;

            // Heading is tangent to circle (perpendicular to radius)
            // For left turn (decreasing angle/clockwise), tangent is +90° from radius
            // For right turn (increasing angle/counter-clockwise), tangent is -90° from radius
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

        // ============================================
        // BUILD PATH: Exit Leg
        // ============================================
        // Exit leg must end at exitEnd (which is turnOffset perpendicular from entryStart)
        // This ensures the exit lands on the next track regardless of arc diameter
        var lastArcPoint = path[path.Count - 1];
        double actualArcEndE = lastArcPoint.Easting;
        double actualArcEndN = lastArcPoint.Northing;

        // Calculate exitStart: same distance into headland as arcEnd, but at turnOffset from entry track
        // This is where the exit leg begins (at turnOffset perpendicular offset)
        double exitStartE = arcStartE + Math.Sin(perpAngle) * turnOffset;
        double exitStartN = arcStartN + Math.Cos(perpAngle) * turnOffset;

        // If arc diameter differs from turnOffset, we need a connecting segment
        // from actualArcEnd to exitStart (perpendicular adjustment)
        double arcToExitDist = Math.Sqrt(Math.Pow(exitStartE - actualArcEndE, 2) + Math.Pow(exitStartN - actualArcEndN, 2));
        if (arcToExitDist > pointSpacing)
        {
            // Add connecting points from arc end to exit start
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

        // Now build exit leg from exitStart to exitEnd (going back into field)
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

        _logger.LogDebug($"[YouTurn] Path has {path.Count} points: {totalEntryPoints + 1} entry, {arcPoints} arc, {totalExitPoints} exit");
        _logger.LogDebug($"[YouTurn] Actual entry start: E={path[0].Easting:F1}, N={path[0].Northing:F1}");
        _logger.LogDebug($"[YouTurn] Actual exit end: E={path[path.Count - 1].Easting:F1}, N={path[path.Count - 1].Northing:F1}");

        // Apply smoothing passes from config (1-50)
        int smoothingPasses = Guidance.UTurnSmoothing;
        if (smoothingPasses > 1 && path.Count > 4)
        {
            for (int pass = 0; pass < smoothingPasses; pass++)
            {
                // Smooth interior points only (preserve start and end)
                for (int i = 2; i < path.Count - 2; i++)
                {
                    var prev = path[i - 1];
                    var curr = path[i];
                    var next = path[i + 1];

                    // Average position with neighbors
                    path[i] = new Vec3
                    {
                        Easting = (prev.Easting + curr.Easting + next.Easting) / 3.0,
                        Northing = (prev.Northing + curr.Northing + next.Northing) / 3.0,
                        Heading = curr.Heading // Preserve heading
                    };
                }
            }
            _logger.LogDebug($"[YouTurn] Applied {smoothingPasses} smoothing passes");
        }

        return path;
    }

    /// <summary>
    /// Check if a point is inside the headland boundary.
    /// </summary>
    private bool IsPointInsideHeadland(Vec3 point)
    {
        if (_currentHeadlandLine == null || _currentHeadlandLine.Count < 3)
            return false;

        // Use ray casting algorithm
        int n = _currentHeadlandLine.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = _currentHeadlandLine[i];
            var pj = _currentHeadlandLine[j];

            if (((pi.Northing > point.Northing) != (pj.Northing > point.Northing)) &&
                (point.Easting < (pj.Easting - pi.Easting) * (point.Northing - pi.Northing) / (pj.Northing - pi.Northing) + pi.Easting))
            {
                inside = !inside;
            }
        }

        return inside;
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
