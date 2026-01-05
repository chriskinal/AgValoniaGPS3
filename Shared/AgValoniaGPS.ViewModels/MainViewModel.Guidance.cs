using System;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Guidance;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.AutoSteer;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing AutoSteer guidance calculation logic.
/// Handles Pure Pursuit/Stanley guidance algorithms and visualization updates.
/// </summary>
public partial class MainViewModel
{
    #region Guidance State

    // Track guidance state (carried between iterations)
    private TrackGuidanceState? _trackGuidanceState;

    #endregion

    #region AutoSteer Event Handlers

    private void OnAutoSteerStateUpdated(object? sender, VehicleStateSnapshot state)
    {
        // Update latency display from AutoSteer pipeline
        // This fires at 10Hz from the GPS receive path
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            GpsToPgnLatencyMs = state.TotalLatencyMs;
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => GpsToPgnLatencyMs = state.TotalLatencyMs);
        }
    }

    #endregion

    #region Guidance Calculation

    /// <summary>
    /// Calculate steering guidance using Pure Pursuit algorithm and apply to simulator.
    /// For AB lines: Uses _howManyPathsAway to dynamically calculate which parallel line to follow.
    /// For curves: Follows the curve directly (parallel offset curves are a future feature).
    /// </summary>
    private void CalculateAutoSteerGuidance(AgValoniaGPS.Models.Position currentPosition)
    {
        var track = SelectedTrack;
        if (track == null) return;

        // Convert heading from degrees to radians for the algorithm
        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

        // Calculate track heading (from first to last point)
        double trackDx = track.PointB.Easting - track.PointA.Easting;
        double trackDy = track.PointB.Northing - track.PointA.Northing;
        double trackHeading = Math.Atan2(trackDx, trackDy); // Note: atan2(dx, dy) for north-based heading

        // Determine if vehicle is heading the same way as the track
        double headingDiff = headingRadians - trackHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        bool isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Calculate dynamic look-ahead distance based on speed
        double speed = currentPosition.Speed * 3.6; // Convert m/s to km/h for look-ahead calc
        double lookAhead = Guidance.GoalPointLookAheadHold;
        if (speed > 1)
        {
            lookAhead = Math.Max(
                Guidance.MinLookAheadDistance,
                Guidance.GoalPointLookAheadHold + (speed * Guidance.GoalPointLookAheadMult * 0.1)
            );
        }

        // Calculate steer axle position (ahead of pivot by wheelbase)
        double steerEasting = currentPosition.Easting + Math.Sin(headingRadians) * Vehicle.Wheelbase;
        double steerNorthing = currentPosition.Northing + Math.Cos(headingRadians) * Vehicle.Wheelbase;

        Track currentTrack;

        // Check if this is an AB line (2 points) or a curve (>2 points)
        if (track.Points.Count == 2)
        {
            // AB Line: Calculate parallel offset based on _howManyPathsAway
            double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
            double distAway = widthMinusOverlap * _howManyPathsAway;

            double perpAngle = trackHeading + Math.PI / 2;
            double offsetEasting = Math.Sin(perpAngle) * distAway;
            double offsetNorthing = Math.Cos(perpAngle) * distAway;

            double currentPtAEasting = track.PointA.Easting + offsetEasting;
            double currentPtANorthing = track.PointA.Northing + offsetNorthing;
            double currentPtBEasting = track.PointB.Easting + offsetEasting;
            double currentPtBNorthing = track.PointB.Northing + offsetNorthing;

            if (_youTurnCounter % 30 == 0)
            {
                _logger.LogDebug("AutoSteer AB: Following path {Path}, offset {Offset:F1}m", _howManyPathsAway, distAway);
            }

            currentTrack = Track.FromABLine(
                "CurrentGuidance",
                new Vec3(currentPtAEasting, currentPtANorthing, trackHeading),
                new Vec3(currentPtBEasting, currentPtBNorthing, trackHeading));

            // Update the map to show the current AB line
            UpdateActiveLineVisualization(currentPtAEasting, currentPtANorthing, currentPtBEasting, currentPtBNorthing);
        }
        else
        {
            // Curve: Apply parallel offset based on _howManyPathsAway
            double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
            double distAway = widthMinusOverlap * _howManyPathsAway;

            if (_youTurnCounter % 30 == 0)
            {
                _logger.LogDebug("AutoSteer Curve: Following curve '{Name}' with {Count} points, path {Path}, offset {Offset:F1}m",
                    track.Name, track.Points.Count, _howManyPathsAway, distAway);
            }

            if (Math.Abs(distAway) < 0.01)
            {
                // No offset needed - use original curve
                currentTrack = track;
            }
            else
            {
                // Create offset curve by moving each point perpendicular to its local heading
                var offsetPoints = new List<Vec3>(track.Points.Count);
                foreach (var pt in track.Points)
                {
                    // Perpendicular is 90° from heading (left is positive offset)
                    double perpAngle = pt.Heading + Math.PI / 2;
                    double offsetE = pt.Easting + Math.Sin(perpAngle) * distAway;
                    double offsetN = pt.Northing + Math.Cos(perpAngle) * distAway;
                    offsetPoints.Add(new Vec3(offsetE, offsetN, pt.Heading));
                }

                currentTrack = new Track
                {
                    Name = $"{track.Name} (path {_howManyPathsAway})",
                    Points = offsetPoints,
                    Type = track.Type,
                    IsVisible = true,
                    IsActive = true
                };
            }

            // Update the map to show the current curve (offset or original)
            _mapService.SetActiveTrack(currentTrack);
        }

        // Build unified guidance input
        var input = new TrackGuidanceInput
        {
            Track = currentTrack,
            PivotPosition = new Vec3(currentPosition.Easting, currentPosition.Northing, headingRadians),
            SteerPosition = new Vec3(steerEasting, steerNorthing, headingRadians),
            UseStanley = false, // Use Pure Pursuit
            IsHeadingSameWay = isHeadingSameWay,

            // Vehicle configuration
            Wheelbase = Vehicle.Wheelbase,
            MaxSteerAngle = Vehicle.MaxSteerAngle,
            GoalPointDistance = lookAhead,
            SideHillCompFactor = 0, // No IMU roll compensation in simulator

            // Pure Pursuit gains
            PurePursuitIntegralGain = Guidance.PurePursuitIntegralGain,

            // Vehicle state
            FixHeading = headingRadians,
            AvgSpeed = speed,
            IsReverse = false,
            IsAutoSteerOn = true,
            IsYouTurnTriggered = _isYouTurnTriggered,

            // AHRS data (88888 = invalid/no IMU)
            ImuRoll = 88888,

            // Previous state for filtering/integration
            PreviousState = _trackGuidanceState,
            FindGlobalNearest = _trackGuidanceState == null, // Global search on first iteration
            CurrentLocationIndex = _trackGuidanceState?.CurrentLocationIndex ?? 0
        };

        // Calculate guidance using unified service
        var output = _trackGuidanceService.CalculateGuidance(input);

        // Store state for next iteration
        _trackGuidanceState = output.State;
        if (_trackGuidanceState != null)
        {
            _trackGuidanceState.CurrentLocationIndex = output.CurrentLocationIndex;
        }

        // Update centralized guidance state
        State.Guidance.UpdateFromGuidance(output);

        // Apply calculated steering to simulator
        SimulatorSteerAngle = output.SteerAngle;

        // Update cross-track error for display (convert from meters to cm) - legacy property
        CrossTrackError = output.CrossTrackError * 100;
    }

    /// <summary>
    /// Update the map visualization to show the current dynamically-calculated guidance line.
    /// </summary>
    private void UpdateActiveLineVisualization(double ptAEasting, double ptANorthing, double ptBEasting, double ptBNorthing)
    {
        // Create a temporary Track for visualization that represents the current offset line
        var currentGuidanceTrack = Track.FromABLine(
            "CurrentGuidance",
            new Vec3(ptAEasting, ptANorthing, 0),
            new Vec3(ptBEasting, ptBNorthing, 0));
        currentGuidanceTrack.IsActive = true;
        _mapService.SetActiveTrack(currentGuidanceTrack);
    }

    #endregion
}
