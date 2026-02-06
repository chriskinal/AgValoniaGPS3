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
using ReactiveUI;
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

    // Track when we last warned about curve limits (to avoid spam)
    private DateTime _lastCurveLimitWarning = DateTime.MinValue;
    private int _lastWarnedPathsAway = int.MinValue;

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
    /// Unified approach: An AB line is just a curve with 2 points.
    /// Uses _howManyPathsAway to calculate which parallel offset to follow.
    /// </summary>
    private void CalculateAutoSteerGuidance(AgValoniaGPS.Models.Position currentPosition)
    {
        var track = SelectedTrack;
        if (track == null || track.Points.Count < 2) return;

        // Convert heading from degrees to radians for the algorithm
        double headingRadians = currentPosition.Heading * Math.PI / 180.0;

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

        // Calculate parallel offset based on _howManyPathsAway
        // Same logic for both AB lines and curves
        double widthMinusOverlap = ConfigStore.ActualToolWidth - Tool.Overlap;
        double distAway = widthMinusOverlap * _howManyPathsAway;

        if (_youTurnCounter % 30 == 0)
        {
            _logger.LogDebug("AutoSteer: Following track '{Name}' ({Count} points), path {Path}, offset {Offset:F1}m",
                track.Name, track.Points.Count, _howManyPathsAway, distAway);
        }

        Track currentTrack;
        if (Math.Abs(distAway) < 0.01)
        {
            // No offset needed - use original track
            currentTrack = track;
        }
        else
        {
            // Create offset track using the clean offset function that handles self-intersections.
            // When offsetting inward on tight curves, the offset can fold back on itself.
            // CreateOffsetCurveWithInfo detects and removes these self-intersecting portions.
            var (offsetPoints, percentRemoved) = CurveProcessing.CreateOffsetCurveWithInfo(track.Points, distAway);

            // Warn user if significant portion of curve was removed due to tight radius
            if (percentRemoved > 10 && _howManyPathsAway != _lastWarnedPathsAway)
            {
                // Only warn once per path and at most every 10 seconds
                if ((DateTime.Now - _lastCurveLimitWarning).TotalSeconds > 10)
                {
                    _lastCurveLimitWarning = DateTime.Now;
                    _lastWarnedPathsAway = _howManyPathsAway;

                    double minRadius = CurveProcessing.CalculateMinRadiusOfCurvature(track.Points);
                    int maxPasses = CurveProcessing.CalculateMaxInwardPasses(track.Points, widthMinusOverlap);

                    StatusMessage = $"Curve too tight! {percentRemoved:F0}% removed. Max ~{maxPasses} inward passes (min radius: {minRadius:F1}m)";
                    _logger.LogWarning("Curve offset limit: {Percent:F1}% of points removed at path {Path}. Min radius: {Radius:F1}m, max passes: {Max}",
                        percentRemoved, _howManyPathsAway, minRadius, maxPasses);
                }
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

        // Update the map visualization
        _mapService.SetActiveTrack(currentTrack);

        // IMPORTANT: Calculate isHeadingSameWay using the OFFSET track we're actually following,
        // not the original track. For curves, the nearest segment can be at different indices
        // on the original vs offset track, causing incorrect heading direction.
        double trackHeading = FindNearestSegmentHeading(currentTrack.Points, currentPosition.Easting, currentPosition.Northing);

        // Determine if vehicle is heading the same way as the track
        double headingDiff = headingRadians - trackHeading;
        while (headingDiff > Math.PI) headingDiff -= 2 * Math.PI;
        while (headingDiff < -Math.PI) headingDiff += 2 * Math.PI;
        bool isHeadingSameWay = Math.Abs(headingDiff) < Math.PI / 2;

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
    /// Find the heading of the nearest segment to a point.
    /// For a 2-point track (AB line), returns the constant A→B heading.
    /// For curves, returns the heading of the segment closest to the point.
    /// </summary>
    private static double FindNearestSegmentHeading(List<Vec3> points, double easting, double northing)
    {
        if (points.Count < 2) return 0;

        // For 2-point tracks, there's only one segment - use its heading directly
        // This prevents the "nearest point jumping" issue for AB lines
        if (points.Count == 2)
        {
            return points[0].Heading;
        }

        // For curves, find the segment with minimum perpendicular distance
        double minDist = double.MaxValue;
        int nearestSegmentIdx = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];

            // Calculate perpendicular distance to segment
            double segDx = p2.Easting - p1.Easting;
            double segDy = p2.Northing - p1.Northing;
            double segLenSq = segDx * segDx + segDy * segDy;

            double dist;
            if (segLenSq < 0.0001)
            {
                // Degenerate segment - use point distance
                dist = Math.Sqrt((easting - p1.Easting) * (easting - p1.Easting) +
                                 (northing - p1.Northing) * (northing - p1.Northing));
            }
            else
            {
                // Project point onto segment line
                double t = ((easting - p1.Easting) * segDx + (northing - p1.Northing) * segDy) / segLenSq;
                t = Math.Clamp(t, 0, 1);

                double projE = p1.Easting + t * segDx;
                double projN = p1.Northing + t * segDy;

                dist = Math.Sqrt((easting - projE) * (easting - projE) +
                                 (northing - projN) * (northing - projN));
            }

            if (dist < minDist)
            {
                minDist = dist;
                nearestSegmentIdx = i;
            }
        }

        // Return the heading of the start point of the nearest segment
        return points[nearestSegmentIdx].Heading;
    }

    #endregion
}
