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
    /// Uses _howManyPathsAway to dynamically calculate which parallel line to follow.
    /// </summary>
    private void CalculateAutoSteerGuidance(AgValoniaGPS.Models.Position currentPosition)
    {
        var track = SelectedTrack;
        if (track == null) return;

        // Convert heading from degrees to radians for the algorithm
        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

        // Calculate AB line heading (from the original/reference AB line)
        double abDx = track.PointB.Easting - track.PointA.Easting;
        double abDy = track.PointB.Northing - track.PointA.Northing;
        double abHeading = Math.Atan2(abDx, abDy); // Note: atan2(dx, dy) for north-based heading

        // Determine if vehicle is heading the same way as the AB line
        double headingDiff = headingRadians - abHeading;
        // Normalize to -PI to PI
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        bool isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

        // Calculate the perpendicular offset distance based on howManyPathsAway
        // This is the key insight from AgOpenGPS - the guidance line is dynamically calculated
        double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap; // Implement width minus overlap
        double distAway = widthMinusOverlap * _howManyPathsAway;

        // Calculate the perpendicular direction (90 degrees from AB heading)
        double perpAngle = abHeading + Math.PI / 2; // Always use same perpendicular reference
        double offsetEasting = Math.Sin(perpAngle) * distAway;
        double offsetNorthing = Math.Cos(perpAngle) * distAway;

        // Calculate the current guidance line points (offset from reference AB line)
        double currentPtAEasting = track.PointA.Easting + offsetEasting;
        double currentPtANorthing = track.PointA.Northing + offsetNorthing;
        double currentPtBEasting = track.PointB.Easting + offsetEasting;
        double currentPtBNorthing = track.PointB.Northing + offsetNorthing;

        // Debug: log which offset we're following every second (30 frames at 30fps)
        if (_youTurnCounter % 30 == 0)
        {
            _logger.LogDebug("AutoSteer: Following path {Path}, offset {Offset:F1}m, heading {Heading}", _howManyPathsAway, distAway, isHeadingSameWay ? "same" : "opposite");
            _logger.LogDebug("AutoSteer: Current line A({Ax:F1},{Ay:F1}) B({Bx:F1},{By:F1})", currentPtAEasting, currentPtANorthing, currentPtBEasting, currentPtBNorthing);
        }

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

        // Create a Track from the dynamically calculated line
        var currentTrack = Models.Track.Track.FromABLine(
            "CurrentGuidance",
            new Vec3(currentPtAEasting, currentPtANorthing, abHeading),
            new Vec3(currentPtBEasting, currentPtBNorthing, abHeading));

        // Calculate steer axle position (ahead of pivot by wheelbase)
        double steerEasting = currentPosition.Easting + Math.Sin(headingRadians) * Vehicle.Wheelbase;
        double steerNorthing = currentPosition.Northing + Math.Cos(headingRadians) * Vehicle.Wheelbase;

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
            FindGlobalNearest = _trackGuidanceState == null // Global search on first iteration
        };

        // Calculate guidance using unified service
        var output = _trackGuidanceService.CalculateGuidance(input);

        // Store state for next iteration
        _trackGuidanceState = output.State;

        // Update centralized guidance state
        State.Guidance.UpdateFromGuidance(output);

        // Apply calculated steering to simulator
        SimulatorSteerAngle = output.SteerAngle;

        // Update cross-track error for display (convert from meters to cm) - legacy property
        CrossTrackError = output.CrossTrackError * 100;

        // Update the map to show the current guidance line
        UpdateActiveLineVisualization(currentPtAEasting, currentPtANorthing, currentPtBEasting, currentPtBNorthing);
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
