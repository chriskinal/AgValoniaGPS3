// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.Headland;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Services.YouTurn;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.Services.Pipeline;

/// <summary>
/// Orchestrates the GPS processing pipeline on a background thread.
/// Subscribes to <see cref="IGpsService.GpsDataUpdated"/>, runs all heavy computation
/// off the UI thread, and fires <see cref="CycleCompleted"/> with an immutable result snapshot.
/// </summary>
public sealed class GpsPipelineService : IGpsPipelineService
{
    // ── Dependencies ────────────────────────────────────────────────────
    private readonly IGpsService _gpsService;
    private readonly IToolPositionService _toolPositionService;
    private readonly ITrackGuidanceService _trackGuidanceService;
    private readonly ISectionControlService _sectionControlService;
    private readonly ICoverageMapService _coverageMapService;
    private readonly IAutoSteerService _autoSteerService;
    private readonly YouTurnGuidanceService _youTurnGuidanceService;
    private readonly IAudioService _audioService;
    private readonly ILogger<GpsPipelineService> _logger;
    private readonly ApplicationState _appState;

    // ── Events ──────────────────────────────────────────────────────────
    public event Action<GpsCycleResult>? CycleCompleted;

    // ── Re-entrancy guard ───────────────────────────────────────────────
    private int _processing; // 0 = idle, 1 = processing

    // ── Subscription tracking ───────────────────────────────────────────
    private bool _isStarted;

    // ── Operational state (written from UI thread, read from background) ─
    private readonly object _stateLock = new();

    private bool _autoSteerEngaged;
    private Models.Track.Track? _activeTrack;
    private int _passNumber;
    private double _nudgeOffset;
    private bool _isTrackOnBoundary;

    private bool _youTurnEnabled;
    private List<Vec3>? _headlandLine;
    private Boundary? _boundary;
    private double _driftE;
    private double _driftN;

    private bool _isYouTurnTriggered;
    private bool _isInYouTurn;
    private List<Vec3>? _youTurnPath;

    // ── Pipeline-owned guidance state (only touched on background thread) ─
    private Models.Track.TrackGuidanceState? _trackGuidanceState;
    private double _simulatorSteerAngle;

    // ── Headland proximity ──────────────────────────────────────────────
    private readonly HeadlandDetectionService _headlandDetector = new();

    // ── RTK quality tracking ────────────────────────────────────────────
    private int _previousFixQuality;

    // ── Curve limit warning throttling ──────────────────────────────────
    private DateTime _lastCurveLimitWarning = DateTime.MinValue;
    private double? _lastHeadlandDistance;
    private bool _lastHeadlandWarning;
    private int _lastWarnedPathsAway = int.MinValue;

    // ── Logging throttle ────────────────────────────────────────────────
    private int _cycleCounter;

    public GpsPipelineService(
        IGpsService gpsService,
        IToolPositionService toolPositionService,
        ITrackGuidanceService trackGuidanceService,
        ISectionControlService sectionControlService,
        ICoverageMapService coverageMapService,
        IAutoSteerService autoSteerService,
        YouTurnGuidanceService youTurnGuidanceService,
        IAudioService audioService,
        ILogger<GpsPipelineService> logger,
        ApplicationState appState)
    {
        _gpsService = gpsService;
        _toolPositionService = toolPositionService;
        _trackGuidanceService = trackGuidanceService;
        _sectionControlService = sectionControlService;
        _coverageMapService = coverageMapService;
        _autoSteerService = autoSteerService;
        _youTurnGuidanceService = youTurnGuidanceService;
        _audioService = audioService;
        _logger = logger;
        _appState = appState;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════════════════

    public void Start()
    {
        if (_isStarted) return;
        _isStarted = true;
        _gpsService.GpsDataUpdated += OnGpsDataUpdated;
        _logger.LogInformation("GpsPipelineService started");
    }

    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;
        _gpsService.GpsDataUpdated -= OnGpsDataUpdated;
        _logger.LogInformation("GpsPipelineService stopped");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Operational state setters (called from UI thread)
    // ══════════════════════════════════════════════════════════════════════

    public void SetAutoSteerEngaged(bool engaged)
    {
        lock (_stateLock)
        {
            _autoSteerEngaged = engaged;
            // Reset guidance state when disengaging so we do global search on next engage
            if (!engaged) _trackGuidanceState = null;
        }
    }

    public void SetActiveTrack(Models.Track.Track? track, int passNumber, double nudgeOffset, bool isOnBoundary)
    {
        lock (_stateLock)
        {
            _activeTrack = track;
            _passNumber = passNumber;
            _nudgeOffset = nudgeOffset;
            _isTrackOnBoundary = isOnBoundary;
            // Reset guidance state when track changes so we do a global search
            _trackGuidanceState = null;
        }
    }

    public void SetYouTurnEnabled(bool enabled)
    {
        lock (_stateLock) _youTurnEnabled = enabled;
    }

    public void SetHeadlandLine(IReadOnlyList<Vec3>? headlandLine)
    {
        lock (_stateLock)
        {
            // Copy to our own list so caller can mutate theirs safely
            _headlandLine = headlandLine != null ? new List<Vec3>(headlandLine) : null;
        }
    }

    public void SetBoundary(Boundary? boundary)
    {
        lock (_stateLock) _boundary = boundary;
    }

    public void SetDriftCompensation(double driftE, double driftN)
    {
        lock (_stateLock) { _driftE = driftE; _driftN = driftN; }
    }

    public void SetYouTurnState(bool isTriggered, bool isInYouTurn, List<Vec3>? youTurnPath)
    {
        lock (_stateLock)
        {
            _isYouTurnTriggered = isTriggered;
            _isInYouTurn = isInYouTurn;
            _youTurnPath = youTurnPath != null ? new List<Vec3>(youTurnPath) : null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Read-back properties
    // ══════════════════════════════════════════════════════════════════════

    public bool IsAutoSteerEngaged { get { lock (_stateLock) return _autoSteerEngaged; } }
    public bool IsInYouTurn { get { lock (_stateLock) return _isInYouTurn; } }
    public double SimulatorSteerAngle => Volatile.Read(ref _simulatorSteerAngle);

    // ══════════════════════════════════════════════════════════════════════
    // GPS event handler
    // ══════════════════════════════════════════════════════════════════════

    private void OnGpsDataUpdated(object? sender, GpsData data)
    {
        // Skip if previous cycle is still running (back-pressure)
        if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0)
            return;

        Task.Run(() =>
        {
            try
            {
                ProcessCycle(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GpsPipelineService.ProcessCycle failed");
            }
            finally
            {
                Volatile.Write(ref _processing, 0);
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    // Core cycle — runs on background thread
    // ══════════════════════════════════════════════════════════════════════

    private void ProcessCycle(GpsData data)
    {
        _cycleCounter++;
        var config = ConfigurationStore.Instance;

        // ── Snapshot operational state under lock ────────────────────────
        bool autoSteerEngaged;
        Models.Track.Track? track;
        int passNumber;
        double nudgeOffset;
        bool isTrackOnBoundary;
        bool youTurnEnabled;
        List<Vec3>? headlandLine;
        Boundary? boundary;
        double driftE, driftN;
        bool isYouTurnTriggered;
        bool isInYouTurn;
        List<Vec3>? youTurnPath;

        lock (_stateLock)
        {
            autoSteerEngaged = _autoSteerEngaged;
            track = _activeTrack;
            passNumber = _passNumber;
            nudgeOffset = _nudgeOffset;
            isTrackOnBoundary = _isTrackOnBoundary;
            youTurnEnabled = _youTurnEnabled;
            headlandLine = _headlandLine;
            boundary = _boundary;
            driftE = _driftE;
            driftN = _driftN;
            isYouTurnTriggered = _isYouTurnTriggered;
            isInYouTurn = _isInYouTurn;
            youTurnPath = _youTurnPath;
        }

        var pos = data.CurrentPosition;
        bool hasTrack = track != null && track.Points.Count >= 2;

        // ── (1) Coordinate conversion for real GPS path ─────────────────
        double posEasting = pos.Easting;
        double posNorthing = pos.Northing;

        if (Math.Abs(posEasting) < 0.001 && Math.Abs(posNorthing) < 0.001
            && Math.Abs(pos.Latitude) > 0.001)
        {
            var localPlane = _appState.Field.LocalPlane;
            if (localPlane != null)
            {
                var geoCoord = localPlane.ConvertWgs84ToGeoCoord(
                    new Wgs84(pos.Latitude, pos.Longitude));
                posEasting = geoCoord.Easting;
                posNorthing = geoCoord.Northing;
            }
        }

        // ── (2) Apply drift compensation ────────────────────────────────
        double driftedEasting = posEasting + driftE;
        double driftedNorthing = posNorthing + driftN;
        double headingRad = pos.Heading * Math.PI / 180.0;

        // ── (3) Tool position ───────────────────────────────────────────
        _toolPositionService.Update(
            new Vec3(driftedEasting, driftedNorthing, headingRad),
            headingRad);

        var toolPos = _toolPositionService.ToolPosition;
        var hitchPos = _toolPositionService.HitchPosition;
        double toolHeading = _toolPositionService.ToolHeading;
        bool isToolReady = _toolPositionService.IsToolPositionReady;
        double toolWidth = config.ActualToolWidth;

        // Map positions are now sent via GpsCycleResult → ViewModel → MapRenderState.
        // The pipeline does NOT call _mapService directly.

        // ── (5) Boundary check — auto-disengage if outside ──────────────
        bool autoSteerDisengaged = false;
        string? disengageReason = null;

        bool skipBoundaryCheck = (isTrackOnBoundary && passNumber == 0) || isInYouTurn;
        if (autoSteerEngaged && !skipBoundaryCheck
            && !IsPointInsideBoundary(boundary, driftedEasting, driftedNorthing))
        {
            autoSteerEngaged = false;
            autoSteerDisengaged = true;
            disengageReason = "AutoSteer disengaged - outside boundary";
            lock (_stateLock) _autoSteerEngaged = false;
        }

        // ── (6) Guidance calculation ────────────────────────────────────
        double steerAngle = 0;
        double crossTrackError = 0;
        double goalE = 0, goalN = 0;
        bool hasGuidance = false;
        bool youTurnCompleted = false;
        string? statusMessage = null;
        Models.Track.Track? displayTrack = null;
        Models.Track.Track? baseTrack = null;

        // Always compute the display track when we have a track (for map visualization)
        if (hasTrack)
        {
            var config2 = ConfigurationStore.Instance;
            double widthMinusOverlap = config2.ActualToolWidth - config2.Tool.Overlap;
            double distAway = widthMinusOverlap * passNumber + nudgeOffset;

            if (Math.Abs(distAway) < 0.01)
            {
                displayTrack = track;
            }
            else
            {
                var offsetPoints = CurveProcessing.CreateOffsetCurve(track!.Points, distAway);
                displayTrack = new Models.Track.Track
                {
                    Name = $"{track.Name} (path {passNumber})",
                    Points = offsetPoints,
                    Type = track.Type,
                    IsVisible = true,
                    IsActive = true
                };
                baseTrack = track;
            }
        }

        if (autoSteerEngaged && hasTrack)
        {
            if (isYouTurnTriggered && youTurnPath != null && youTurnPath.Count > 0)
            {
                // YouTurn guidance — steer along turn path
                var ytResult = CalculateYouTurnGuidance(pos, youTurnPath);
                if (ytResult != null)
                {
                    steerAngle = ytResult.Value.steerAngle;
                    crossTrackError = ytResult.Value.xte;
                    youTurnCompleted = ytResult.Value.turnComplete;
                    hasGuidance = !youTurnCompleted;
                }
            }
            else
            {
                // Normal track guidance
                var guidanceResult = CalculateTrackGuidance(
                    pos, track!, passNumber, nudgeOffset, driftedEasting, driftedNorthing, headingRad,
                    isYouTurnTriggered);
                if (guidanceResult != null)
                {
                    steerAngle = guidanceResult.Value.steerAngle;
                    crossTrackError = guidanceResult.Value.xte;
                    goalE = guidanceResult.Value.goalE;
                    goalN = guidanceResult.Value.goalN;
                    hasGuidance = true;
                    statusMessage = guidanceResult.Value.statusMessage;
                }
            }

            if (hasGuidance)
            {
                Volatile.Write(ref _simulatorSteerAngle, steerAngle);
                _autoSteerService.UpdateGuidanceResults(steerAngle, crossTrackError);
            }
        }
        int? detectedNearestPass = null;
        if (!autoSteerEngaged && hasTrack)
        {
            // Display-only: auto-detect nearest pass and update visualization
            var (nearestPass, nearestDisplayTrack) = UpdateDisplayTrack(
                pos, track!, passNumber, nudgeOffset, driftedEasting, driftedNorthing);
            detectedNearestPass = nearestPass;
            if (nearestDisplayTrack != null)
            {
                displayTrack = nearestDisplayTrack;
                if (nearestPass != 0) baseTrack = track;
            }
        }

        // ── (7) Section control ─────────────────────────────────────────
        _sectionControlService.Update(toolPos, toolHeading, headingRad, pos.Speed);

        // ── (8) Coverage painting ───────────────────────────────────────
        var sectionStates = _sectionControlService.SectionStates;
        int numSections = _sectionControlService.NumSections;
        bool anyCoverage = false;

        for (int i = 0; i < numSections; i++)
        {
            var sec = sectionStates[i];
            if (sec.IsMappingOn)
            {
                var (left, right) = _toolPositionService.GetSectionEdgePositions(
                    sec.PositionLeft, sec.PositionRight);
                _coverageMapService.AddCoveragePoint(i,
                    new Vec2(left.Easting, left.Northing),
                    new Vec2(right.Easting, right.Northing));
                anyCoverage = true;
            }
        }

        if (anyCoverage)
            _coverageMapService.FlushCoverageUpdate();

        // ── (9) AutoSteer pipeline (PGN/latency) ────────────────────────
        _autoSteerService.ProcessSimulatedPosition(
            pos.Latitude, pos.Longitude, pos.Altitude,
            pos.Heading, pos.Speed, data.FixQuality,
            data.SatellitesInUse, data.Hdop,
            driftedEasting, driftedNorthing);

        // ── (10) Headland proximity ─────────────────────────────────────
        double? headlandDist = null;
        bool headlandWarning = false;
        ComputeHeadlandProximity(headlandLine, _toolPositionService.ToolPivotPosition,
            out headlandDist, out headlandWarning);

        // Hold last valid distance so the HUD doesn't disappear in gaps
        // (e.g., between crossing the headland line and entering the U-turn arc)
        if (headlandDist != null)
        {
            _lastHeadlandDistance = headlandDist;
            _lastHeadlandWarning = headlandWarning;
        }
        else if (_lastHeadlandDistance != null && headlandLine != null)
        {
            headlandDist = _lastHeadlandDistance;
            headlandWarning = _lastHeadlandWarning;
        }

        // ── (11) RTK quality sounds ─────────────────────────────────────
        CheckRtkQualityChange(data.FixQuality);

        // ── (12) Build section state arrays for result ──────────────────
        bool[]? secStatesArr = null;
        int[]? secColorCodes = null;
        if (numSections > 0)
        {
            secStatesArr = new bool[numSections];
            secColorCodes = new int[numSections];
            for (int i = 0; i < numSections; i++)
            {
                secStatesArr[i] = sectionStates[i].IsOn;
                secColorCodes[i] = GetSectionColorCode(sectionStates[i]);
            }
        }

        // ── (13) Build immutable result ─────────────────────────────────
        var result = new GpsCycleResult
        {
            // GPS position
            Latitude = pos.Latitude,
            Longitude = pos.Longitude,
            Easting = driftedEasting,
            Northing = driftedNorthing,
            Heading = pos.Heading,
            Speed = pos.Speed,
            RollDegrees = SensorState.Instance.ImuRoll,
            SatelliteCount = data.SatellitesInUse,
            FixQuality = data.FixQuality,
            GpsValid = data.IsValid,

            // Tool position
            ToolEasting = toolPos.Easting,
            ToolNorthing = toolPos.Northing,
            ToolHeadingRadians = toolHeading,
            ToolWidth = toolWidth,
            HitchEasting = hitchPos.Easting,
            HitchNorthing = hitchPos.Northing,
            IsToolPositionReady = isToolReady,

            // Guidance
            SteerAngle = steerAngle,
            CrossTrackError = crossTrackError,
            GoalPointEasting = goalE,
            GoalPointNorthing = goalN,
            HasGuidance = hasGuidance,
            DisplayTrack = displayTrack,
            BaseTrack = baseTrack,
            NearestPassNumber = detectedNearestPass,

            // Autosteer
            IsAutoSteerEngaged = autoSteerEngaged,
            AutoSteerDisengagedThisCycle = autoSteerDisengaged,
            DisengageReason = disengageReason,

            // YouTurn
            IsInYouTurn = isInYouTurn,
            YouTurnTriggered = isYouTurnTriggered,
            YouTurnCompleted = youTurnCompleted,

            // Sections
            SectionStates = secStatesArr,
            SectionColorCodes = secColorCodes,

            // Headland proximity
            HeadlandProximityDistance = headlandDist,
            HeadlandProximityWarning = headlandWarning,

            // Status
            StatusMessage = statusMessage
        };

        CycleCompleted?.Invoke(result);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Guidance helpers (run on background thread, no Avalonia types)
    // ══════════════════════════════════════════════════════════════════════

    private (double steerAngle, double xte, double goalE, double goalN, string? statusMessage)?
        CalculateTrackGuidance(
            Position currentPosition, Models.Track.Track track, int passNumber, double nudgeOffset,
            double driftedEasting, double driftedNorthing, double headingRad,
            bool isYouTurnTriggered)
    {
        var config = ConfigurationStore.Instance;

        // Calculate dynamic look-ahead
        double speedKmh = currentPosition.Speed * 3.6;
        double lookAhead = config.Guidance.GoalPointLookAheadHold;
        if (speedKmh > 1)
        {
            lookAhead = Math.Max(
                config.Guidance.MinLookAheadDistance,
                config.Guidance.GoalPointLookAheadHold + (speedKmh * config.Guidance.GoalPointLookAheadMult * 0.1));
        }

        // Steer axle position
        double steerE = driftedEasting + Math.Sin(headingRad) * config.Vehicle.Wheelbase;
        double steerN = driftedNorthing + Math.Cos(headingRad) * config.Vehicle.Wheelbase;

        // Calculate parallel offset
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        double distAway = widthMinusOverlap * passNumber + nudgeOffset;

        Models.Track.Track currentTrack;
        string? statusMessage = null;

        if (Math.Abs(distAway) < 0.01)
        {
            currentTrack = track;
        }
        else
        {
            var (offsetPoints, percentRemoved) = CurveProcessing.CreateOffsetCurveWithInfo(track.Points, distAway);

            // Warn on tight curves
            if (percentRemoved > 10 && passNumber != _lastWarnedPathsAway)
            {
                if ((DateTime.Now - _lastCurveLimitWarning).TotalSeconds > 10)
                {
                    _lastCurveLimitWarning = DateTime.Now;
                    _lastWarnedPathsAway = passNumber;

                    double minRadius = CurveProcessing.CalculateMinRadiusOfCurvature(track.Points);
                    int maxPasses = CurveProcessing.CalculateMaxInwardPasses(track.Points, widthMinusOverlap);
                    statusMessage = $"Curve too tight! {percentRemoved:F0}% removed. Max ~{maxPasses} inward passes (min radius: {minRadius:F1}m)";
                }
            }

            currentTrack = new Models.Track.Track
            {
                Name = $"{track.Name} (path {passNumber})",
                Points = offsetPoints,
                Type = track.Type,
                IsVisible = true,
                IsActive = true
            };
        }

        // Calculate heading alignment using the offset track we're actually following
        double trackHeading = FindNearestSegmentHeading(currentTrack.Points, driftedEasting, driftedNorthing);
        double headingDiff = headingRad - trackHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        bool isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Build guidance input
        var input = new Models.Track.TrackGuidanceInput
        {
            Track = currentTrack,
            PivotPosition = new Vec3(driftedEasting, driftedNorthing, headingRad),
            SteerPosition = new Vec3(steerE, steerN, headingRad),
            UseStanley = false,
            IsHeadingSameWay = isHeadingSameWay,
            Wheelbase = config.Vehicle.Wheelbase,
            MaxSteerAngle = config.Vehicle.MaxSteerAngle,
            GoalPointDistance = lookAhead,
            SideHillCompFactor = 0,
            PurePursuitIntegralGain = config.Guidance.PurePursuitIntegralGain,
            FixHeading = headingRad,
            AvgSpeed = speedKmh,
            IsReverse = false,
            IsAutoSteerOn = true,
            IsYouTurnTriggered = isYouTurnTriggered,
            ImuRoll = 88888,
            PreviousState = _trackGuidanceState,
            FindGlobalNearest = _trackGuidanceState == null,
            CurrentLocationIndex = _trackGuidanceState?.CurrentLocationIndex ?? 0
        };

        var output = _trackGuidanceService.CalculateGuidance(input);

        // Store state for next iteration
        _trackGuidanceState = output.State;
        if (_trackGuidanceState != null)
            _trackGuidanceState.CurrentLocationIndex = output.CurrentLocationIndex;

        return (output.SteerAngle, output.CrossTrackError, output.GoalPoint.Easting, output.GoalPoint.Northing,
                statusMessage);
    }

    /// <summary>
    /// Display-only track update: finds nearest pass and updates map, without steering.
    /// Returns the detected nearest pass number and the offset display track.
    /// </summary>
    private (int nearestPass, Models.Track.Track? displayTrack) UpdateDisplayTrack(
        Position pos, Models.Track.Track track, int passNumber, double nudgeOffset,
        double driftedEasting, double driftedNorthing)
    {
        var config = ConfigurationStore.Instance;
        double widthMinusOverlap = config.ActualToolWidth - config.Tool.Overlap;
        if (widthMinusOverlap < 0.1) widthMinusOverlap = 1.0;

        double perpDist = CalculatePerpendicularDistance(track, driftedEasting, driftedNorthing);
        int nearestPass = (int)Math.Round(perpDist / widthMinusOverlap);
        double distAway = widthMinusOverlap * nearestPass + nudgeOffset;

        Models.Track.Track? resultTrack = null;
        if (Math.Abs(distAway) < 0.01)
        {
            // On the base track — show reference directly
            resultTrack = track;
        }
        else
        {
            var (offsetPoints, _) = CurveProcessing.CreateOffsetCurveWithInfo(track.Points, distAway);
            resultTrack = new Models.Track.Track
            {
                Name = $"{track.Name} (pass {nearestPass})",
                Points = offsetPoints,
                Type = track.Type,
                IsVisible = true,
                IsActive = true
            };
        }

        // Update XTE display
        double xte = perpDist - (nearestPass * widthMinusOverlap);
        if (track.Points.Count >= 2)
        {
            var a = track.Points[0];
            var b = track.Points[track.Points.Count - 1];
            double trackHeading = Math.Atan2(b.Easting - a.Easting, b.Northing - a.Northing);
            double vehicleHeading = pos.Heading * Math.PI / 180.0;
            double headingDiff = Math.Abs(vehicleHeading - trackHeading);
            if (headingDiff > Math.PI) headingDiff = 2 * Math.PI - headingDiff;
            if (headingDiff > Math.PI / 2) xte = -xte;
        }

        _autoSteerService.UpdateGuidanceResults(0, xte);
        return (nearestPass, resultTrack);
    }

    /// <summary>
    /// Calculate YouTurn path-following guidance. Returns null if no path.
    /// </summary>
    private (double steerAngle, double xte, bool turnComplete)?
        CalculateYouTurnGuidance(Position currentPosition, List<Vec3> turnPath)
    {
        if (turnPath.Count == 0) return null;

        var config = ConfigurationStore.Instance;
        double headingRad = currentPosition.Heading * Math.PI / 180.0;
        double speedKmh = currentPosition.Speed * 3.6;

        var input = new YouTurnGuidanceInput
        {
            TurnPath = turnPath,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRad),
            SteerPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRad),
            Wheelbase = config.Vehicle.Wheelbase,
            MaxSteerAngle = config.Vehicle.MaxSteerAngle,
            UseStanley = false,
            GoalPointDistance = config.Guidance.GoalPointLookAheadHold,
            UTurnCompensation = config.Guidance.UTurnCompensation,
            FixHeading = headingRad,
            AvgSpeed = speedKmh,
            IsReverse = false,
            UTurnStyle = 0
        };

        var output = _youTurnGuidanceService.CalculateGuidance(input);

        if (output.IsTurnComplete)
            return (0, 0, true);

        return (output.SteerAngle, output.DistanceFromCurrentLine, false);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Geometry helpers (pure math, no UI)
    // ══════════════════════════════════════════════════════════════════════

    private static bool IsPointInsideBoundary(Boundary? boundary, double easting, double northing)
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

    private static double FindNearestSegmentHeading(List<Vec3> points, double easting, double northing)
    {
        if (points.Count < 2) return 0;
        if (points.Count == 2) return points[0].Heading;

        double minDist = double.MaxValue;
        int nearestIdx = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];

            double segDx = p2.Easting - p1.Easting;
            double segDy = p2.Northing - p1.Northing;
            double segLenSq = segDx * segDx + segDy * segDy;

            double dist;
            if (segLenSq < 0.0001)
            {
                dist = Math.Sqrt((easting - p1.Easting) * (easting - p1.Easting) +
                                 (northing - p1.Northing) * (northing - p1.Northing));
            }
            else
            {
                double t = Math.Clamp(
                    ((easting - p1.Easting) * segDx + (northing - p1.Northing) * segDy) / segLenSq,
                    0, 1);
                double projE = p1.Easting + t * segDx;
                double projN = p1.Northing + t * segDy;
                dist = Math.Sqrt((easting - projE) * (easting - projE) +
                                 (northing - projN) * (northing - projN));
            }

            if (dist < minDist)
            {
                minDist = dist;
                nearestIdx = i;
            }
        }

        return points[nearestIdx].Heading;
    }

    private static double CalculatePerpendicularDistance(Models.Track.Track track, double easting, double northing)
    {
        if (track.Points.Count == 2)
        {
            var a = track.Points[0];
            var b = track.Points[1];
            double dx = b.Easting - a.Easting;
            double dy = b.Northing - a.Northing;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01) return 0;
            return ((easting - a.Easting) * dy - (northing - a.Northing) * dx) / len;
        }
        else
        {
            double minDist = double.MaxValue;
            double signedDist = 0;
            for (int i = 0; i < track.Points.Count - 1; i++)
            {
                var a = track.Points[i];
                var b = track.Points[i + 1];
                double dx = b.Easting - a.Easting;
                double dy = b.Northing - a.Northing;
                double segLen = Math.Sqrt(dx * dx + dy * dy);
                if (segLen < 0.01) continue;
                double t = Math.Clamp(
                    ((easting - a.Easting) * dx + (northing - a.Northing) * dy) / (segLen * segLen),
                    0, 1);
                double projE = a.Easting + t * dx;
                double projN = a.Northing + t * dy;
                double dist = Math.Sqrt((easting - projE) * (easting - projE) + (northing - projN) * (northing - projN));
                if (dist < minDist)
                {
                    minDist = dist;
                    signedDist = ((easting - a.Easting) * dy - (northing - a.Northing) * dx) / segLen;
                }
            }
            return signedDist;
        }
    }

    private void ComputeHeadlandProximity(
        List<Vec3>? headlandLine, Vec3 toolPivot,
        out double? distance, out bool warning)
    {
        distance = null;
        warning = false;

        if (headlandLine == null || headlandLine.Count < 3)
            return;

        var input = new Models.Headland.HeadlandDetectionInput
        {
            IsHeadlandOn = true,
            VehiclePosition = toolPivot,
            Boundaries = new List<Models.Headland.BoundaryData>
            {
                new Models.Headland.BoundaryData
                {
                    HeadlandLine = new List<Vec3>(headlandLine)
                }
            }
        };

        var output = _headlandDetector.DetectHeadland(input);
        distance = output.HeadlandDistance;
        warning = output.ShouldTriggerWarning;
    }

    private void CheckRtkQualityChange(int fixQuality)
    {
        if (fixQuality != _previousFixQuality)
        {
            bool wasRtk = _previousFixQuality >= 4;
            bool isRtk = fixQuality >= 4;
            if (wasRtk && !isRtk)
                _audioService.Play(SoundEffect.RtkLost);
            else if (!wasRtk && isRtk)
                _audioService.Play(SoundEffect.RtkRecovered);
            _previousFixQuality = fixQuality;
        }
    }

    private static int GetSectionColorCode(SectionControlState state)
    {
        return state.ButtonState switch
        {
            SectionButtonState.Off => 0,
            SectionButtonState.On => 1,
            SectionButtonState.Auto => 2,
            _ => 0
        };
    }
}
