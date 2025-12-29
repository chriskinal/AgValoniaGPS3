using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.Geometry;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.YouTurn;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for U-turn/YouTurn functionality.
/// Handles U-turn path creation, triggering, guidance, and completion.
/// </summary>
public class YouTurnViewModel : ReactiveObject
{
    private readonly YouTurnCreationService _creationService;
    private readonly YouTurnGuidanceService _guidanceService;
    private readonly IMapService _mapService;
    private readonly IPolygonOffsetService _polygonOffsetService;
    private readonly IVehicleProfileService _vehicleProfileService;
    private readonly ApplicationState _appState;

    // Events for MainViewModel to subscribe to
    public event EventHandler<string>? StatusMessageChanged;
    public event EventHandler<double>? SteerAngleChanged;
    public event EventHandler? TurnCompleted;

    public YouTurnViewModel(
        YouTurnCreationService creationService,
        YouTurnGuidanceService guidanceService,
        IMapService mapService,
        IPolygonOffsetService polygonOffsetService,
        IVehicleProfileService vehicleProfileService,
        ApplicationState appState)
    {
        _creationService = creationService;
        _guidanceService = guidanceService;
        _mapService = mapService;
        _polygonOffsetService = polygonOffsetService;
        _vehicleProfileService = vehicleProfileService;
        _appState = appState;
    }

    #region Properties

    // Convenience accessors for ConfigurationStore
    private static VehicleConfig Vehicle => ConfigurationStore.Instance.Vehicle;
    private static GuidanceConfig Guidance => ConfigurationStore.Instance.Guidance;
    private static ToolConfig Tool => ConfigurationStore.Instance.Tool;
    private static ConfigurationStore ConfigStore => ConfigurationStore.Instance;

    private bool _isYouTurnEnabled;
    /// <summary>
    /// Whether YouTurn auto U-turn feature is enabled.
    /// </summary>
    public bool IsYouTurnEnabled
    {
        get => _isYouTurnEnabled;
        set => this.RaiseAndSetIfChanged(ref _isYouTurnEnabled, value);
    }

    private int _uTurnSkipRows;
    /// <summary>
    /// Number of rows to skip on U-turn (0-9).
    /// </summary>
    public int UTurnSkipRows
    {
        get => _uTurnSkipRows;
        set => this.RaiseAndSetIfChanged(ref _uTurnSkipRows, Math.Max(0, Math.Min(9, value)));
    }

    private bool _isUTurnSkipRowsEnabled;
    /// <summary>
    /// Whether U-turn row skipping is enabled.
    /// </summary>
    public bool IsUTurnSkipRowsEnabled
    {
        get => _isUTurnSkipRowsEnabled;
        set => this.RaiseAndSetIfChanged(ref _isUTurnSkipRowsEnabled, value);
    }

    /// <summary>
    /// Whether a U-turn has been triggered and is pending execution.
    /// </summary>
    public bool IsYouTurnTriggered => _isYouTurnTriggered;

    /// <summary>
    /// Whether currently executing a U-turn.
    /// </summary>
    public bool IsInYouTurn => _isInYouTurn;

    /// <summary>
    /// Current U-turn path (null if no turn active).
    /// </summary>
    public List<Vec3>? YouTurnPath => _youTurnPath;

    /// <summary>
    /// Which parallel offset line we're on (like AgOpenGPS howManyPathsAway).
    /// </summary>
    public int HowManyPathsAway => _howManyPathsAway;

    /// <summary>
    /// The next track to switch to after U-turn completes.
    /// </summary>
    public Track? NextTrack => _nextTrack;

    #endregion

    #region Internal State

    // Turn state
    private bool _isYouTurnTriggered;
    private bool _isInYouTurn;
    private List<Vec3>? _youTurnPath;
    private int _youTurnCounter;
    private double _distanceToHeadland;
    private bool _isHeadingSameWay;
    private bool _isTurnLeft;
    private bool _wasHeadingSameWayAtTurnStart;
    private bool _lastTurnWasLeft;
    private bool _hasCompletedFirstTurn;
    private Track? _nextTrack;
    private int _howManyPathsAway;
    private Vec2? _lastTurnCompletionPosition;

    #endregion

    #region Public Methods

    /// <summary>
    /// Process YouTurn - check distance to headland, create turn path if needed, trigger turn.
    /// </summary>
    /// <param name="currentPosition">Current vehicle position</param>
    /// <param name="track">Currently selected track</param>
    /// <param name="headlandLine">Current headland boundary line</param>
    /// <param name="boundary">Current field boundary</param>
    /// <param name="headlandDistance">Headland zone width</param>
    /// <param name="headlandCalculatedWidth">Total calculated headland width</param>
    public void ProcessYouTurn(
        Position currentPosition,
        Track? track,
        List<Vec3>? headlandLine,
        Boundary? boundary,
        double headlandDistance,
        double headlandCalculatedWidth)
    {
        if (track == null || track.Points.Count < 2 || headlandLine == null) return;

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
        double alignmentTolerance = Math.PI / 9;  // ~20 degrees
        bool alignedForward = Math.Abs(headingDiff) < alignmentTolerance;
        bool alignedReverse = Math.Abs(headingDiff) > (Math.PI - alignmentTolerance);
        bool isAlignedWithABLine = alignedForward || alignedReverse;

        // Only calculate distance to headland when aligned with the AB line
        if (isAlignedWithABLine)
        {
            double travelHeading = abHeading;
            if (!_isHeadingSameWay)
            {
                travelHeading += Math.PI;
                if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
            }
            _distanceToHeadland = CalculateDistanceToHeadland(currentPosition, travelHeading, headlandLine);
        }
        else
        {
            _distanceToHeadland = double.MaxValue;
        }

        // Create U-turn path when approaching the headland ahead
        double minDistanceToCreate = 30.0;
        bool headlandAhead = _distanceToHeadland > minDistanceToCreate &&
                             _distanceToHeadland < double.MaxValue &&
                             isAlignedWithABLine;

        // Increment counter
        _youTurnCounter++;

        // Debug logging
        if (_youTurnPath == null && !_isInYouTurn && _youTurnCounter % 60 == 0)
        {
            Console.WriteLine($"[YouTurn] Status: distToHeadland={_distanceToHeadland:F1}m, headlandAhead={headlandAhead}, aligned={isAlignedWithABLine}, counter={_youTurnCounter}");
        }

        if (_youTurnPath == null && _youTurnCounter >= 4 && !_isInYouTurn && headlandAhead)
        {
            if (WouldNextLineBeInsideBoundary(track, abHeading, boundary))
            {
                Console.WriteLine($"[YouTurn] Creating turn path - dist ahead: {_distanceToHeadland:F1}m");
                CreateYouTurnPath(currentPosition, headingRadians, abHeading, track, headlandLine, boundary, headlandDistance, headlandCalculatedWidth);
            }
            else
            {
                Console.WriteLine("[YouTurn] Next line would be outside boundary - stopping U-turns");
                StatusMessageChanged?.Invoke(this, "End of field reached");
            }
        }
        else if (_youTurnPath != null && _youTurnPath.Count > 2 && !_isYouTurnTriggered && !_isInYouTurn)
        {
            double distToTurnStart = Math.Sqrt(
                Math.Pow(currentPosition.Easting - _youTurnPath[0].Easting, 2) +
                Math.Pow(currentPosition.Northing - _youTurnPath[0].Northing, 2));

            if (distToTurnStart <= 2.0)
            {
                _appState.YouTurn.IsTriggered = true;
                _appState.YouTurn.IsExecuting = true;

                _isYouTurnTriggered = true;
                _isInYouTurn = true;
                StatusMessageChanged?.Invoke(this, "YouTurn triggered!");
                Console.WriteLine($"[YouTurn] Triggered at distance {distToTurnStart:F2}m from turn start");

                ComputeNextTrack(track, abHeading);
            }
        }

        // Check if U-turn is complete
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

            if (distToTurnEnd <= 2.0 && distToTurnEnd < distToTurnStart && distToTurnStart > 5.0)
            {
                CompleteYouTurn();
            }
        }
    }

    /// <summary>
    /// Calculate steering guidance while following the YouTurn path.
    /// </summary>
    /// <returns>True if guidance was calculated, false if no turn active</returns>
    public bool CalculateYouTurnGuidance(Position currentPosition, out double crossTrackError)
    {
        crossTrackError = 0;
        if (_youTurnPath == null || _youTurnPath.Count == 0) return false;

        double headingRadians = currentPosition.Heading * Math.PI / 180.0;
        double speed = currentPosition.Speed * 3.6; // km/h

        var input = new YouTurnGuidanceInput
        {
            TurnPath = _youTurnPath,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            SteerPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            Wheelbase = Vehicle.Wheelbase,
            MaxSteerAngle = Vehicle.MaxSteerAngle,
            UseStanley = false,
            GoalPointDistance = Guidance.GoalPointLookAheadHold,
            UTurnCompensation = Guidance.UTurnCompensation,
            FixHeading = headingRadians,
            AvgSpeed = speed,
            IsReverse = false,
            UTurnStyle = 0
        };

        var output = _guidanceService.CalculateGuidance(input);

        if (output.IsTurnComplete)
        {
            Console.WriteLine("[YouTurn] Guidance detected turn complete, calling CompleteYouTurn");
            CompleteYouTurn();
            return false;
        }

        double steerAngle = output.SteerAngle * Guidance.UTurnCompensation;
        SteerAngleChanged?.Invoke(this, steerAngle);

        _appState.Guidance.CrossTrackError = output.DistanceFromCurrentLine;
        _appState.Guidance.SteerAngle = output.SteerAngle;

        crossTrackError = output.DistanceFromCurrentLine * 100;
        return true;
    }

    /// <summary>
    /// Check if currently in a YouTurn (for determining which guidance to use).
    /// </summary>
    public bool ShouldUseYouTurnGuidance()
    {
        return _isYouTurnTriggered && _youTurnPath != null && _youTurnPath.Count > 0;
    }

    /// <summary>
    /// Reset YouTurn state (call when field changes, etc.)
    /// </summary>
    public void Reset()
    {
        _isYouTurnTriggered = false;
        _isInYouTurn = false;
        _youTurnPath = null;
        _nextTrack = null;
        _youTurnCounter = 0;
        _howManyPathsAway = 0;
        _lastTurnCompletionPosition = null;
        _hasCompletedFirstTurn = false;
        _lastTurnWasLeft = false;

        _appState.YouTurn.IsTriggered = false;
        _appState.YouTurn.IsExecuting = false;
        _appState.YouTurn.TurnPath = null;
        _appState.YouTurn.HasCompletedFirstTurn = false;

        _mapService.SetYouTurnPath(null);
        _mapService.SetNextTrack(null);
        _mapService.SetIsInYouTurn(false);
    }

    #endregion

    #region Private Methods

    private double CalculateDistanceToHeadland(Position currentPosition, double headingRadians, List<Vec3> headlandLine)
    {
        if (headlandLine.Count < 3)
            return double.MaxValue;

        double minDistance = double.MaxValue;
        Vec2 pos = new Vec2(currentPosition.Easting, currentPosition.Northing);
        Vec2 dir = new Vec2(Math.Sin(headingRadians), Math.Cos(headingRadians));

        int intersectionCount = 0;
        int n = headlandLine.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = headlandLine[i];
            var p2 = headlandLine[(i + 1) % n];

            Vec2 edge = new Vec2(p2.Easting - p1.Easting, p2.Northing - p1.Northing);
            Vec2 toP1 = new Vec2(p1.Easting - pos.Easting, p1.Northing - pos.Northing);

            double cross = dir.Easting * edge.Northing - dir.Northing * edge.Easting;
            if (Math.Abs(cross) < 1e-10) continue;

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
            Console.WriteLine($"[Headland] Raycast: pos=({pos.Easting:F1},{pos.Northing:F1}), heading={headingDeg:F0}deg, intersections={intersectionCount}, minDist={minDistance:F1}m, isHeadingSameWay={_isHeadingSameWay}");
        }

        return minDistance;
    }

    private bool WouldNextLineBeInsideBoundary(Track currentTrack, double abHeading, Boundary? boundary)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
            return true;

        if (currentTrack.Points.Count < 2)
            return true;

        var pointA = currentTrack.Points[0];
        var pointB = currentTrack.Points[currentTrack.Points.Count - 1];

        // Calculate where the next line would be (use config skip width)
        int rowSkipWidth = Guidance.UTurnSkipWidth;
        double offsetDistance = rowSkipWidth * (Tool.Width - Tool.Overlap);

        double perpAngle = abHeading + (_isHeadingSameWay ? -Math.PI / 2 : Math.PI / 2);
        double offsetEasting = Math.Sin(perpAngle) * offsetDistance;
        double offsetNorthing = Math.Cos(perpAngle) * offsetDistance;

        double midEasting = (pointA.Easting + pointB.Easting) / 2 + offsetEasting;
        double midNorthing = (pointA.Northing + pointB.Northing) / 2 + offsetNorthing;

        return IsPointInsideBoundary(midEasting, midNorthing, boundary);
    }

    private bool IsPointInsideBoundary(double easting, double northing, Boundary? boundary)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
            return true;

        var points = boundary.OuterBoundary.Points;
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

    private void ComputeNextTrack(Track referenceTrack, double abHeading)
    {
        if (referenceTrack.Points.Count < 2)
            return;

        var refPointA = referenceTrack.Points[0];
        var refPointB = referenceTrack.Points[referenceTrack.Points.Count - 1];

        int rowSkipWidth = UTurnSkipRows;  // Use runtime property from bottom nav button

        bool positiveOffset = _isTurnLeft ^ _isHeadingSameWay;
        int offsetChange = positiveOffset ? rowSkipWidth : -rowSkipWidth;
        int nextPathsAway = _howManyPathsAway + offsetChange;

        // Calculate the total offset for the next line
        double widthMinusOverlap = Tool.Width - Tool.Overlap;
        double nextDistAway = widthMinusOverlap * nextPathsAway;

        double perpAngle = abHeading + Math.PI / 2;
        double offsetEasting = Math.Sin(perpAngle) * nextDistAway;
        double offsetNorthing = Math.Cos(perpAngle) * nextDistAway;

        _nextTrack = Track.FromABLine(
            $"Path {nextPathsAway}",
            new Vec3(refPointA.Easting + offsetEasting, refPointA.Northing + offsetNorthing, abHeading),
            new Vec3(refPointB.Easting + offsetEasting, refPointB.Northing + offsetNorthing, abHeading));
        _nextTrack.IsActive = false;

        Console.WriteLine($"[YouTurn] Turn {(_isTurnLeft ? "LEFT" : "RIGHT")}, heading {(_isHeadingSameWay ? "SAME" : "OPPOSITE")} way");
        Console.WriteLine($"[YouTurn] Offset {(positiveOffset ? "positive" : "negative")}: path {_howManyPathsAway} -> {nextPathsAway} ({nextDistAway:F1}m)");

        _mapService.SetNextTrack(_nextTrack);
        _mapService.SetIsInYouTurn(true);
    }

    private void CompleteYouTurn()
    {
        if (_youTurnPath != null && _youTurnPath.Count > 0)
        {
            var endPoint = _youTurnPath[_youTurnPath.Count - 1];
            _lastTurnCompletionPosition = new Vec2(endPoint.Easting, endPoint.Northing);
        }

        // Following AgOpenGPS approach exactly:
        // howManyPathsAway += (isTurnLeft ^ isHeadingSameWay) ? rowSkipsWidth : -rowSkipsWidth
        int rowSkipWidth = UTurnSkipRows;  // Use runtime property from bottom nav button

        // Calculate offset direction using XOR like AgOpenGPS
        // IMPORTANT: Use _wasHeadingSameWayAtTurnStart (saved at turn creation), NOT _isHeadingSameWay
        bool positiveOffset = _isTurnLeft ^ _wasHeadingSameWayAtTurnStart;
        int offsetChange = positiveOffset ? rowSkipWidth : -rowSkipWidth;
        _howManyPathsAway += offsetChange;

        Console.WriteLine($"[YouTurn] Turn complete! Turn was {(_isTurnLeft ? "LEFT" : "RIGHT")}, heading WAS {(_wasHeadingSameWayAtTurnStart ? "SAME" : "OPPOSITE")} at start");
        Console.WriteLine($"[YouTurn] Offset {(positiveOffset ? "positive" : "negative")} by {offsetChange}, now on path {_howManyPathsAway}");
        Console.WriteLine($"[YouTurn] Total offset: {(Tool.Width - Tool.Overlap) * _howManyPathsAway:F1}m from reference line");

        _lastTurnWasLeft = _isTurnLeft;
        _hasCompletedFirstTurn = true;

        _appState.YouTurn.LastTurnWasLeft = _isTurnLeft;
        _appState.YouTurn.HasCompletedFirstTurn = true;
        _appState.YouTurn.IsTriggered = false;
        _appState.YouTurn.IsExecuting = false;
        _appState.YouTurn.TurnPath = null;

        _isYouTurnTriggered = false;
        _isInYouTurn = false;
        _youTurnPath = null;
        _nextTrack = null;
        _youTurnCounter = 10;

        _mapService.SetYouTurnPath(null);
        _mapService.SetNextTrack(null);
        _mapService.SetIsInYouTurn(false);

        StatusMessageChanged?.Invoke(this, $"Following path {_howManyPathsAway} ({(Tool.Width - Tool.Overlap) * Math.Abs(_howManyPathsAway):F1}m offset)");
        TurnCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void CreateYouTurnPath(
        Position currentPosition,
        double headingRadians,
        double abHeading,
        Track track,
        List<Vec3> headlandLine,
        Boundary? boundary,
        double headlandDistance,
        double headlandCalculatedWidth)
    {
        bool turnLeft = _isHeadingSameWay;
        _isTurnLeft = turnLeft;
        _wasHeadingSameWayAtTurnStart = _isHeadingSameWay;

        Console.WriteLine($"[YouTurn] Creating turn with YouTurnCreationService: direction={(_isTurnLeft ? "LEFT" : "RIGHT")}, isHeadingSameWay={_isHeadingSameWay}, pathsAway={_howManyPathsAway}");

        try
        {
            var input = BuildYouTurnCreationInput(currentPosition, headingRadians, abHeading, turnLeft, track, boundary, headlandCalculatedWidth);
            if (input == null)
            {
                Console.WriteLine($"[YouTurn] Failed to build creation input - using simple fallback");
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft, headlandDistance);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    _appState.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
                return;
            }

            var output = _creationService.CreateTurn(input);

            if (output.Success && output.TurnPath != null && output.TurnPath.Count > 10)
            {
                _appState.YouTurn.TurnPath = output.TurnPath;
                _youTurnPath = output.TurnPath;
                _youTurnCounter = 0;
                StatusMessageChanged?.Invoke(this, $"YouTurn path created ({output.TurnPath.Count} points)");
                Console.WriteLine($"[YouTurn] Service path created with {output.TurnPath.Count} points, distToTurnLine={output.DistancePivotToTurnLine:F1}m");

                _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
            }
            else
            {
                Console.WriteLine($"[YouTurn] Service creation failed: {output.FailureReason ?? "unknown"}, using simple fallback");
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft, headlandDistance);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    _appState.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YouTurn] Exception creating path: {ex.Message}");
            try
            {
                var fallbackPath = CreateSimpleUTurnPath(currentPosition, headingRadians, abHeading, turnLeft, headlandDistance);
                if (fallbackPath != null && fallbackPath.Count > 10)
                {
                    _appState.YouTurn.TurnPath = fallbackPath;
                    _youTurnPath = fallbackPath;
                    _youTurnCounter = 0;
                    _mapService.SetYouTurnPath(_youTurnPath.Select(p => (p.Easting, p.Northing)).ToList());
                }
            }
            catch { }
        }
    }

    private YouTurnCreationInput? BuildYouTurnCreationInput(
        Position currentPosition,
        double headingRadians,
        double abHeading,
        bool turnLeft,
        Track track,
        Boundary? boundary,
        double headlandCalculatedWidth)
    {
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            Console.WriteLine($"[YouTurn] No valid outer boundary available");
            return null;
        }

        // Tool/implement width from configuration
        double toolWidth = Tool.Width;
        double totalHeadlandWidth = headlandCalculatedWidth;

        var outerPoints = boundary.OuterBoundary.Points
            .Select(p => new Vec2(p.Easting, p.Northing))
            .ToList();
        var outerBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(outerPoints);

        var turnBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, toolWidth);
        if (turnBoundaryVec2 == null || turnBoundaryVec2.Count < 3)
        {
            Console.WriteLine($"[YouTurn] Failed to create turn boundary (1 tool width offset)");
            return null;
        }
        var turnBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(turnBoundaryVec2);

        var headlandBoundaryVec2 = _polygonOffsetService.CreateInwardOffset(outerPoints, totalHeadlandWidth);
        if (headlandBoundaryVec2 == null || headlandBoundaryVec2.Count < 3)
        {
            Console.WriteLine($"[YouTurn] Failed to create headland boundary");
            return null;
        }
        var headlandBoundaryVec3 = _polygonOffsetService.CalculatePointHeadings(headlandBoundaryVec2);

        var boundaryTurnLines = new List<BoundaryTurnLine>
        {
            new BoundaryTurnLine
            {
                Points = turnBoundaryVec3,
                BoundaryIndex = 0
            }
        };

        double headlandWidthForTurn = Math.Max(totalHeadlandWidth - toolWidth, toolWidth);

        Func<Vec3, int> isPointInsideTurnArea = (point) =>
        {
            if (!GeometryMath.IsPointInPolygon(outerBoundaryVec3, point))
            {
                return -1;
            }

            if (GeometryMath.IsPointInPolygon(headlandBoundaryVec3, point))
            {
                return 0;
            }

            return 1;
        };

        var input = new YouTurnCreationInput
        {
            TurnType = YouTurnType.AlbinStyle,
            IsTurnLeft = turnLeft,
            GuidanceType = GuidanceLineType.ABLine,
            BoundaryTurnLines = boundaryTurnLines,
            IsPointInsideTurnArea = isPointInsideTurnArea,
            ABHeading = abHeading,
            ABReferencePoint = CalculateCurrentTrackReferencePoint(track, toolWidth, abHeading),
            IsHeadingSameWay = _isHeadingSameWay,
            // Vehicle position and configuration
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            ToolWidth = toolWidth,
            ToolOverlap = Tool.Overlap,
            ToolOffset = Tool.Offset,
            TurnRadius = Guidance.UTurnRadius,

            // Turn parameters - use runtime UTurnSkipRows (controlled by bottom nav button)
            RowSkipsWidth = UTurnSkipRows,
            TurnStartOffset = 0,
            HowManyPathsAway = _howManyPathsAway,
            NudgeDistance = 0.0,
            TrackMode = 0,
            MakeUTurnCounter = _youTurnCounter + 10,
            YouTurnLegExtensionMultiplier = 2.5,
            HeadlandWidth = headlandWidthForTurn
        };

        Console.WriteLine($"[YouTurn] Input built: toolWidth={toolWidth:F1}m, totalHeadland={totalHeadlandWidth:F1}m, headlandWidthForTurn={headlandWidthForTurn:F1}m, turnBoundaryPoints={turnBoundaryVec3.Count}, headlandPoints={headlandBoundaryVec3.Count}");

        return input;
    }

    private Vec2 CalculateCurrentTrackReferencePoint(Track track, double toolWidth, double abHeading)
    {
        if (track.Points.Count == 0)
            return new Vec2(0, 0);

        double baseEasting = track.Points[0].Easting;
        double baseNorthing = track.Points[0].Northing;

        double perpAngle = abHeading + Math.PI / 2.0;
        double offsetDistance = _howManyPathsAway * toolWidth;

        double offsetEasting = baseEasting + Math.Sin(perpAngle) * offsetDistance;
        double offsetNorthing = baseNorthing + Math.Cos(perpAngle) * offsetDistance;

        Console.WriteLine($"[YouTurn] Reference point: howManyPathsAway={_howManyPathsAway}, offset={offsetDistance:F2}m, perpAngle={perpAngle * 180 / Math.PI:F1}°");

        return new Vec2(offsetEasting, offsetNorthing);
    }

    private List<Vec3> CreateSimpleUTurnPath(Position currentPosition, double headingRadians, double abHeading, bool turnLeft, double headlandDistance)
    {
        var path = new List<Vec3>();

        // Parameters - use ConfigurationStore values
        double pointSpacing = 0.5; // meters between path points
        int rowSkipWidth = Guidance.UTurnSkipWidth; // From config (1 = no skip, 2 = skip 1 row, etc.)
        double trackWidth = Tool.Width - Tool.Overlap; // Implement width minus overlap
        double turnOffset = trackWidth * rowSkipWidth; // Perpendicular distance to next track

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

        double travelHeading = abHeading;
        if (!_isHeadingSameWay)
        {
            travelHeading += Math.PI;
            if (travelHeading >= Math.PI * 2) travelHeading -= Math.PI * 2;
        }

        double exitHeading = travelHeading + Math.PI;
        if (exitHeading >= Math.PI * 2) exitHeading -= Math.PI * 2;

        double perpAngle = turnLeft ? (travelHeading - Math.PI / 2) : (travelHeading + Math.PI / 2);

        double distToHeadland = _distanceToHeadland;
        double headlandBoundaryEasting = currentPosition.Easting + Math.Sin(travelHeading) * distToHeadland;
        double headlandBoundaryNorthing = currentPosition.Northing + Math.Cos(travelHeading) * distToHeadland;

        // Leg lengths - use config values
        // The arc extends turnRadius beyond the arc start (toward the outer boundary)
        // So: arc_top_position = headlandLegLength + turnRadius
        // We want arc_top to be at HeadlandDistance (at the outer boundary)
        // Therefore: headlandLegLength = HeadlandDistance - turnRadius
        // But ensure arc start is at least a small margin past the headland boundary
        double distanceFromBoundary = Guidance.UTurnDistanceFromBoundary;
        double headlandLegLength = Math.Max(headlandDistance - turnRadius - distanceFromBoundary, 2.0);

        // How far path extends into cultivated area (entry/exit legs) - use UTurnExtension from config
        double fieldLegLength = Guidance.UTurnExtension;

        Console.WriteLine($"[YouTurn] HeadlandBoundary: E={headlandBoundaryEasting:F1}, N={headlandBoundaryNorthing:F1}");
        Console.WriteLine($"[YouTurn] HeadlandDistance={headlandDistance:F1}m, headlandLegLength={headlandLegLength:F1}m, turnRadius={turnRadius:F1}m, turnOffset={turnOffset:F1}m");
        Console.WriteLine($"[YouTurn] Arc will extend to {headlandLegLength + turnRadius:F1}m past headland boundary (headland zone is {headlandDistance:F1}m)");

        // Calculate key waypoints
        double entryStartE = headlandBoundaryEasting - Math.Sin(travelHeading) * fieldLegLength;
        double entryStartN = headlandBoundaryNorthing - Math.Cos(travelHeading) * fieldLegLength;

        double arcStartE = headlandBoundaryEasting + Math.Sin(travelHeading) * headlandLegLength;
        double arcStartN = headlandBoundaryNorthing + Math.Cos(travelHeading) * headlandLegLength;

        double arcCenterE = arcStartE + Math.Sin(perpAngle) * turnRadius;
        double arcCenterN = arcStartN + Math.Cos(perpAngle) * turnRadius;

        double arcDiameter = 2.0 * turnRadius;
        double arcEndE = arcStartE + Math.Sin(perpAngle) * arcDiameter;
        double arcEndN = arcStartN + Math.Cos(perpAngle) * arcDiameter;

        double exitEndE = entryStartE + Math.Sin(perpAngle) * turnOffset;
        double exitEndN = entryStartN + Math.Cos(perpAngle) * turnOffset;

        Console.WriteLine($"[YouTurn] EntryStart (green): E={entryStartE:F1}, N={entryStartN:F1}");
        Console.WriteLine($"[YouTurn] ExitEnd (red): E={exitEndE:F1}, N={exitEndN:F1}");
        Console.WriteLine($"[YouTurn] ArcStart: E={arcStartE:F1}, N={arcStartN:F1}");
        Console.WriteLine($"[YouTurn] ArcEnd: E={arcEndE:F1}, N={arcEndN:F1}");

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
        // Exit leg goes straight from the ACTUAL last arc point in exitHeading direction
        // Use same length as entry leg to ensure symmetry
        var lastArcPoint = path[path.Count - 1];
        double actualArcEndE = lastArcPoint.Easting;
        double actualArcEndN = lastArcPoint.Northing;
        double actualExitHeading = lastArcPoint.Heading; // Use the tangent heading from arc end

        int totalExitPoints = (int)(totalEntryLength / pointSpacing);

        for (int i = 1; i <= totalExitPoints; i++)
        {
            double dist = i * pointSpacing;
            Vec3 pt = new Vec3
            {
                Easting = actualArcEndE + Math.Sin(actualExitHeading) * dist,
                Northing = actualArcEndN + Math.Cos(actualExitHeading) * dist,
                Heading = actualExitHeading
            };
            path.Add(pt);
        }

        Console.WriteLine($"[YouTurn] Path has {path.Count} points: {totalEntryPoints + 1} entry, {arcPoints} arc, {totalExitPoints} exit");
        Console.WriteLine($"[YouTurn] Actual entry start: E={path[0].Easting:F1}, N={path[0].Northing:F1}");
        Console.WriteLine($"[YouTurn] Actual exit end: E={path[path.Count - 1].Easting:F1}, N={path[path.Count - 1].Northing:F1}");

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
            Console.WriteLine($"[YouTurn] Applied {smoothingPasses} smoothing passes");
        }

        return path;
    }

    #endregion
}
