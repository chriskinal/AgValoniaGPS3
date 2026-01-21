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
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Services.Track;

/// <summary>
/// Unified track guidance service.
/// Handles both Pure Pursuit and Stanley algorithms.
/// Works with both AB lines (2 points) and curves (N points).
///
/// Key insight: An AB line is just a curve with 2 points.
/// The same algorithms work for both - only goal point calculation differs.
/// </summary>
public class TrackGuidanceService : ITrackGuidanceService
{
    private const double PIBy2 = Math.PI / 2.0;
    private const double TwoPI = Math.PI * 2.0;

    /// <summary>
    /// Calculate steering guidance for a track.
    /// </summary>
    /// <param name="input">Guidance input parameters</param>
    /// <returns>Guidance output with steering angle and state</returns>
    public TrackGuidanceOutput CalculateGuidance(TrackGuidanceInput input)
    {
        var output = new TrackGuidanceOutput
        {
            GoalPoint = new Vec2(),
            RadiusPoint = new Vec2(),
            ClosestPointPivot = new Vec2(),
            ClosestPointSteer = new Vec2(),
            FindGlobalNearest = input.FindGlobalNearest,
            CurrentLocationIndex = input.CurrentLocationIndex,
            State = new TrackGuidanceState()
        };

        // Validate input
        if (input.Track?.Points == null || input.Track.Points.Count < 2)
        {
            output.DistanceFromLinePivot = 32000;
            output.GuidanceLineDistanceOff = 32000;
            return output;
        }

        var points = input.Track.Points;
        bool isClosed = input.Track.IsClosed;
        bool isABLine = input.Track.IsABLine;

        // Determine effective heading direction
        bool reverseHeading = input.IsReverse ? !input.IsHeadingSameWay : input.IsHeadingSameWay;

        // Find the nearest segment (A, B indices)
        int indexA, indexB;
        if (isABLine)
        {
            // AB line is simple - always segment 0-1
            indexA = 0;
            indexB = 1;
        }
        else
        {
            // Multi-point track - find nearest segment
            (indexA, indexB) = FindNearestSegment(
                input.PivotPosition,
                points,
                isClosed,
                input.CurrentLocationIndex,
                input.FindGlobalNearest,
                input.GoalPointDistance,
                reverseHeading);

            output.CurrentLocationIndex = indexA;
            output.FindGlobalNearest = false;
        }

        // Validate indices
        if (indexA < 0 || indexB < 0 || indexA >= points.Count || indexB >= points.Count)
        {
            output.DistanceFromLinePivot = 32000;
            output.GuidanceLineDistanceOff = 32000;
            return output;
        }

        Vec3 ptA = points[indexA];
        Vec3 ptB = points[indexB];

        // Calculate segment direction
        double dx = ptB.Easting - ptA.Easting;
        double dz = ptB.Northing - ptA.Northing;

        if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
        {
            output.DistanceFromLinePivot = 32000;
            output.GuidanceLineDistanceOff = 32000;
            return output;
        }

        // Calculate cross-track error (distance from line)
        output.DistanceFromLinePivot = CalculateCrossTrackError(
            input.PivotPosition, ptA, ptB, dx, dz);

        // Calculate closest point on segment
        var (closestEast, closestNorth) = CalculateClosestPoint(
            input.PivotPosition, ptA, dx, dz);
        output.ClosestPointPivot = new Vec2(closestEast, closestNorth);
        output.ManualUturnHeading = ptA.Heading;

        // Apply algorithm-specific calculations
        if (input.UseStanley)
        {
            CalculateStanleyGuidance(input, output, points, indexA, indexB, ptA, ptB, dx, dz);
        }
        else
        {
            CalculatePurePursuitGuidance(input, output, points, indexA, indexB, ptA, ptB, dx, dz, reverseHeading, isABLine, isClosed);
        }

        return output;
    }

    /// <summary>
    /// Calculate Pure Pursuit steering guidance.
    /// </summary>
    private void CalculatePurePursuitGuidance(
        TrackGuidanceInput input,
        TrackGuidanceOutput output,
        List<Vec3> points,
        int indexA, int indexB,
        Vec3 ptA, Vec3 ptB,
        double dx, double dz,
        bool reverseHeading,
        bool isABLine,
        bool isClosed)
    {
        // Calculate integral term
        CalculateIntegralTerm(input, output);

        // Calculate goal point
        if (isABLine)
        {
            // AB line: project goal point along infinite line
            double lineHeading = Math.Atan2(dx, dz);
            if (input.IsReverse ^ input.IsHeadingSameWay)
            {
                output.GoalPoint = new Vec2(
                    output.ClosestPointPivot.Easting + Math.Sin(lineHeading) * input.GoalPointDistance,
                    output.ClosestPointPivot.Northing + Math.Cos(lineHeading) * input.GoalPointDistance);
            }
            else
            {
                output.GoalPoint = new Vec2(
                    output.ClosestPointPivot.Easting - Math.Sin(lineHeading) * input.GoalPointDistance,
                    output.ClosestPointPivot.Northing - Math.Cos(lineHeading) * input.GoalPointDistance);
            }
        }
        else
        {
            // Multi-point curve: walk along points to find goal
            output.GoalPoint = CalculateCurveGoalPoint(
                points, indexA, indexB,
                output.ClosestPointPivot,
                input.GoalPointDistance,
                reverseHeading,
                isClosed);

            // Check for end of track
            if (!isClosed && input.IsAutoSteerOn && !input.IsReverse)
            {
                var endPoint = input.IsHeadingSameWay ? points[^1] : points[0];
                if (GeometryMath.Distance(output.GoalPoint, endPoint) < 0.5)
                {
                    output.IsAtEndOfTrack = true;
                }
            }
        }

        // Calculate Pure Pursuit steering
        double goalPointDistSq = GeometryMath.DistanceSquared(
            output.GoalPoint.Northing, output.GoalPoint.Easting,
            input.PivotPosition.Northing, input.PivotPosition.Easting);

        double localHeading;
        if (reverseHeading)
            localHeading = TwoPI - input.FixHeading + output.State.Integral;
        else
            localHeading = TwoPI - input.FixHeading - output.State.Integral;

        // Pure Pursuit radius
        double lateralOffset = (output.GoalPoint.Easting - input.PivotPosition.Easting) * Math.Cos(localHeading)
            + (output.GoalPoint.Northing - input.PivotPosition.Northing) * Math.Sin(localHeading);

        output.PurePursuitRadius = goalPointDistSq / (2 * lateralOffset);

        // Steer angle from Pure Pursuit formula
        output.SteerAngle = GeometryMath.ToDegrees(
            Math.Atan(2 * lateralOffset * input.Wheelbase / goalPointDistSq));

        // Side hill compensation
        if (input.ImuRoll != 88888)
            output.SteerAngle += input.ImuRoll * -input.SideHillCompFactor;

        // Limit steer angle
        output.SteerAngle = Math.Clamp(output.SteerAngle, -input.MaxSteerAngle, input.MaxSteerAngle);

        // Limit radius for display
        output.PurePursuitRadius = Math.Clamp(output.PurePursuitRadius, -500, 500);

        // Calculate radius point for visualization
        output.RadiusPoint = new Vec2(
            input.PivotPosition.Easting + output.PurePursuitRadius * Math.Cos(localHeading),
            input.PivotPosition.Northing + output.PurePursuitRadius * Math.Sin(localHeading));

        // Adjust sign based on heading direction
        if (!input.IsHeadingSameWay)
            output.DistanceFromLinePivot *= -1.0;

        output.CrossTrackError = output.DistanceFromLinePivot;

        // Calculate heading error
        double lineHeadingRef = ptA.Heading;
        double steerHeadingError = input.PivotPosition.Heading - lineHeadingRef;
        steerHeadingError = NormalizeHeadingError(steerHeadingError);
        output.HeadingErrorDegrees = GeometryMath.ToDegrees(steerHeadingError);

        // Prepare transmission values
        output.GuidanceLineDistanceOff = (short)Math.Round(output.DistanceFromLinePivot * 1000.0, MidpointRounding.AwayFromZero);
        output.GuidanceLineSteerAngle = (short)(output.SteerAngle * 100);
    }

    /// <summary>
    /// Calculate Stanley steering guidance.
    /// </summary>
    private void CalculateStanleyGuidance(
        TrackGuidanceInput input,
        TrackGuidanceOutput output,
        List<Vec3> points,
        int indexA, int indexB,
        Vec3 ptA, Vec3 ptB,
        double dx, double dz)
    {
        // Apply integral offset to create virtual offset line
        double integral = input.PreviousState?.Integral ?? 0;
        Vec3 steerA = new Vec3(
            ptA.Easting + Math.Sin(ptA.Heading + PIBy2) * integral,
            ptA.Northing + Math.Cos(ptA.Heading + PIBy2) * integral,
            ptA.Heading);
        Vec3 steerB = new Vec3(
            ptB.Easting + Math.Sin(ptB.Heading + PIBy2) * integral,
            ptB.Northing + Math.Cos(ptB.Heading + PIBy2) * integral,
            ptB.Heading);

        double dxSteer = steerB.Easting - steerA.Easting;
        double dzSteer = steerB.Northing - steerA.Northing;

        if (Math.Abs(dxSteer) < double.Epsilon && Math.Abs(dzSteer) < double.Epsilon)
        {
            output.DistanceFromLineSteer = 32000;
            output.GuidanceLineDistanceOff = 32000;
            return;
        }

        // Calculate steer axle distance from offset line
        output.DistanceFromLineSteer = CalculateCrossTrackError(
            input.SteerPosition, steerA, steerB, dxSteer, dzSteer);

        // Calculate closest point on line to steer position
        var (steerClosestEast, steerClosestNorth) = CalculateClosestPoint(
            input.SteerPosition, steerA, dxSteer, dzSteer);
        output.ClosestPointSteer = new Vec2(steerClosestEast, steerClosestNorth);

        // Calculate heading error
        double steerErr = Math.Atan2(
            steerClosestEast - output.ClosestPointPivot.Easting,
            steerClosestNorth - output.ClosestPointPivot.Northing);
        double steerHeadingError = input.SteerPosition.Heading - steerErr;
        steerHeadingError = NormalizeHeadingError(steerHeadingError);

        if (!input.IsHeadingSameWay)
        {
            output.DistanceFromLinePivot *= -1.0;
            output.DistanceFromLineSteer *= -1.0;
        }

        output.CrossTrackError = output.DistanceFromLinePivot;
        output.HeadingErrorDegrees = GeometryMath.ToDegrees(steerHeadingError);

        // Stanley integral calculation
        CalculateStanleyIntegral(input, output);

        // Stanley steering formula
        double speedMs = input.AvgSpeed * 0.27778; // km/h to m/s
        if (speedMs < 0.1) speedMs = 0.1;

        // Heading component
        double headingComponent = steerHeadingError * input.StanleyHeadingErrorGain;

        // Cross-track error component
        double xteComponent = Math.Atan(
            input.StanleyDistanceErrorGain * output.DistanceFromLineSteer / speedMs);

        // Stanley uses inverted angle (matches original AOG implementation)
        output.SteerAngle = GeometryMath.ToDegrees((headingComponent + xteComponent) * -1.0);

        // Side hill compensation
        if (input.ImuRoll != 88888)
            output.SteerAngle += input.ImuRoll * -input.SideHillCompFactor;

        // Limit steer angle
        output.SteerAngle = Math.Clamp(output.SteerAngle, -input.MaxSteerAngle, input.MaxSteerAngle);

        // Prepare transmission values
        output.GuidanceLineDistanceOff = (short)Math.Round(output.DistanceFromLinePivot * 1000.0, MidpointRounding.AwayFromZero);
        output.GuidanceLineSteerAngle = (short)(output.SteerAngle * 100);
    }

    /// <summary>
    /// Calculate the integral term for Pure Pursuit.
    /// </summary>
    private void CalculateIntegralTerm(TrackGuidanceInput input, TrackGuidanceOutput output)
    {
        var state = output.State;
        var prev = input.PreviousState ?? new TrackGuidanceState();

        if (input.PurePursuitIntegralGain != 0 && !input.IsReverse)
        {
            state.PivotDistanceError = output.DistanceFromLinePivot * 0.2 + prev.PivotDistanceError * 0.8;
            state.Counter = prev.Counter + 1;

            if (state.Counter > 4)
            {
                state.PivotDerivative = state.PivotDistanceError - prev.PivotDistanceErrorLast;
                state.PivotDistanceErrorLast = state.PivotDistanceError;
                state.Counter = 0;
                state.PivotDerivative *= 2;
            }
            else
            {
                state.PivotDerivative = 0;
                state.PivotDistanceErrorLast = prev.PivotDistanceErrorLast;
            }

            // Integral update conditions
            if (input.IsAutoSteerOn && input.AvgSpeed > 2.5 &&
                Math.Abs(state.PivotDerivative) < 0.1 && !input.IsYouTurnTriggered)
            {
                if ((prev.Integral < 0 && output.DistanceFromLinePivot < 0) ||
                    (prev.Integral > 0 && output.DistanceFromLinePivot > 0))
                {
                    // Rapidly decrease integral when crossing line wrong way
                    state.Integral = prev.Integral + state.PivotDistanceError * input.PurePursuitIntegralGain * -0.04;
                }
                else
                {
                    if (Math.Abs(output.DistanceFromLinePivot) > 0.02)
                    {
                        state.Integral = prev.Integral + state.PivotDistanceError * input.PurePursuitIntegralGain * -0.02;
                        state.Integral = Math.Clamp(state.Integral, -0.2, 0.2);
                    }
                    else
                    {
                        state.Integral = prev.Integral;
                    }
                }
            }
            else
            {
                state.Integral = prev.Integral * 0.95;
            }
        }
        else
        {
            state.Integral = 0;
            state.PivotDistanceError = 0;
            state.PivotDistanceErrorLast = prev.PivotDistanceErrorLast;
            state.Counter = prev.Counter;
            state.PivotDerivative = 0;
        }
    }

    /// <summary>
    /// Calculate the integral term for Stanley.
    /// </summary>
    private void CalculateStanleyIntegral(TrackGuidanceInput input, TrackGuidanceOutput output)
    {
        var state = output.State;
        var prev = input.PreviousState ?? new TrackGuidanceState();

        if (input.StanleyIntegralGain != 0 && !input.IsReverse)
        {
            state.PivotDistanceError = output.DistanceFromLinePivot * 0.2 + prev.PivotDistanceError * 0.8;
            state.Counter = prev.Counter + 1;

            if (state.Counter > 4)
            {
                state.PivotDerivative = state.PivotDistanceError - prev.PivotDistanceErrorLast;
                state.PivotDistanceErrorLast = state.PivotDistanceError;
                state.Counter = 0;
            }
            else
            {
                state.PivotDerivative = 0;
                state.PivotDistanceErrorLast = prev.PivotDistanceErrorLast;
            }

            // Integral update
            if (input.IsAutoSteerOn && input.AvgSpeed > 2.5 && Math.Abs(state.PivotDerivative) < 0.1)
            {
                if ((prev.Integral < 0 && output.DistanceFromLinePivot < 0) ||
                    (prev.Integral > 0 && output.DistanceFromLinePivot > 0))
                {
                    state.Integral = prev.Integral + state.PivotDistanceError * input.StanleyIntegralGain * -0.06;
                }
                else
                {
                    if (Math.Abs(output.DistanceFromLinePivot) > 0.02)
                    {
                        state.Integral = prev.Integral + state.PivotDistanceError * input.StanleyIntegralGain * -0.02;
                        state.Integral = Math.Clamp(state.Integral, -2.0, 2.0);
                    }
                    else
                    {
                        state.Integral = prev.Integral;
                    }
                }
            }
            else
            {
                state.Integral = prev.Integral * 0.97;
            }
        }
        else
        {
            state.Integral = 0;
            state.PivotDistanceError = 0;
            state.PivotDistanceErrorLast = prev.PivotDistanceErrorLast;
            state.Counter = prev.Counter;
        }
    }

    /// <summary>
    /// Find the nearest segment on a multi-point track.
    /// </summary>
    private (int indexA, int indexB) FindNearestSegment(
        Vec3 position,
        List<Vec3> points,
        bool isClosed,
        int currentIndex,
        bool findGlobal,
        double searchDistance,
        bool reverseDirection)
    {
        int nearestIndex;

        if (findGlobal)
        {
            // Global search - check every 10th point for efficiency
            nearestIndex = FindNearestGlobalPoint(position, points, 10);
        }
        else
        {
            // Local search around current index
            nearestIndex = FindNearestLocalPoint(position, points, currentIndex, searchDistance, reverseDirection);
        }

        // Find the two closest points around the nearest
        int searchStart = Math.Max(0, nearestIndex - 8);
        int searchEnd = Math.Min(points.Count, nearestIndex + 8);

        double minDistA = double.MaxValue;
        double minDistB = double.MaxValue;
        int indexA = nearestIndex;
        int indexB = nearestIndex;

        for (int j = searchStart; j < searchEnd; j++)
        {
            double dist = GeometryMath.DistanceSquared(position, points[j]);
            if (dist < minDistA)
            {
                minDistB = minDistA;
                indexB = indexA;
                minDistA = dist;
                indexA = j;
            }
            else if (dist < minDistB)
            {
                minDistB = dist;
                indexB = j;
            }
        }

        // Ensure ascending order
        if (indexA > indexB)
            (indexA, indexB) = (indexB, indexA);

        // For closed loops, handle wrap-around
        if (isClosed && indexA == 0 && indexB == points.Count - 1)
        {
            // Check if we should use last-to-first segment
            if (!IsInRange(points[indexA], points[indexB], position))
            {
                // Use wrap-around segment
                indexA = points.Count - 1;
                indexB = 0;
            }
        }

        return (indexA, indexB);
    }

    /// <summary>
    /// Find nearest point globally (coarse search).
    /// </summary>
    private int FindNearestGlobalPoint(Vec3 position, List<Vec3> points, int increment = 1)
    {
        double minDist = double.MaxValue;
        int minIndex = 0;

        for (int i = 0; i < points.Count; i += increment)
        {
            double dist = GeometryMath.DistanceSquared(position, points[i]);
            if (dist < minDist)
            {
                minDist = dist;
                minIndex = i;
            }
        }

        return minIndex;
    }

    /// <summary>
    /// Find nearest point locally (around current index).
    /// </summary>
    private int FindNearestLocalPoint(
        Vec3 position,
        List<Vec3> points,
        int startIndex,
        double searchDistance,
        bool reverseDirection)
    {
        int count = points.Count;
        double minDist = GeometryMath.DistanceSquared(position, points[(startIndex + count) % count]);
        int minIndex = startIndex;

        int direction = reverseDirection ? 1 : -1;
        double distSoFar = 0;
        Vec3 prevPoint = points[startIndex];

        int offset = 1;
        while (offset < count)
        {
            int idx = (startIndex + offset * direction + count) % count;
            double dist = GeometryMath.DistanceSquared(position, points[idx]);

            if (dist < minDist)
            {
                minDist = dist;
                minIndex = idx;
            }

            distSoFar += GeometryMath.Distance(prevPoint, points[idx]);
            prevPoint = points[idx];
            offset++;

            if (distSoFar > searchDistance)
                break;
        }

        // Continue until distance starts growing
        while (offset < count)
        {
            int idx = (startIndex + offset * direction + count) % count;
            double dist = GeometryMath.DistanceSquared(position, points[idx]);

            if (dist < minDist)
            {
                minDist = dist;
                minIndex = idx;
            }
            else
            {
                break;
            }
            offset++;
        }

        return minIndex;
    }

    /// <summary>
    /// Calculate goal point by walking along curve points.
    /// </summary>
    private Vec2 CalculateCurveGoalPoint(
        List<Vec3> points,
        int indexA, int indexB,
        Vec2 startPoint,
        double goalDistance,
        bool reverseDirection,
        bool isClosed)
    {
        int direction = reverseDirection ? 1 : -1;
        Vec3 current = new Vec3(startPoint.Easting, startPoint.Northing, 0);
        double distSoFar = 0;
        int count = points.Count;

        int startIdx = reverseDirection ? indexB : indexA;

        for (int i = startIdx; ; )
        {
            double segDist = GeometryMath.Distance(current, points[i]);

            if (distSoFar + segDist > goalDistance)
            {
                // Interpolate goal point
                double ratio = (goalDistance - distSoFar) / segDist;
                return new Vec2(
                    (1 - ratio) * current.Easting + ratio * points[i].Easting,
                    (1 - ratio) * current.Northing + ratio * points[i].Northing);
            }

            distSoFar += segDist;
            current = points[i];

            // Move to next point
            i += direction;

            if (isClosed)
            {
                // Wrap around for closed loops
                i = (i + count) % count;
            }
            else
            {
                // Stop at ends for open curves
                if (i < 0 || i >= count)
                {
                    return new Vec2(current.Easting, current.Northing);
                }
            }
        }
    }

    /// <summary>
    /// Calculate cross-track error (signed perpendicular distance from line).
    /// </summary>
    private double CalculateCrossTrackError(Vec3 point, Vec3 lineA, Vec3 lineB, double dx, double dz)
    {
        return ((dz * point.Easting) - (dx * point.Northing)
            + (lineB.Easting * lineA.Northing)
            - (lineB.Northing * lineA.Easting))
            / Math.Sqrt(dz * dz + dx * dx);
    }

    /// <summary>
    /// Calculate closest point on line segment.
    /// </summary>
    private (double east, double north) CalculateClosestPoint(Vec3 point, Vec3 lineA, double dx, double dz)
    {
        double u = ((point.Easting - lineA.Easting) * dx + (point.Northing - lineA.Northing) * dz)
            / (dx * dx + dz * dz);

        return (lineA.Easting + u * dx, lineA.Northing + u * dz);
    }

    /// <summary>
    /// Check if a point projects within a line segment.
    /// </summary>
    private bool IsInRange(Vec3 ptA, Vec3 ptB, Vec3 point)
    {
        double dx = ptB.Easting - ptA.Easting;
        double dz = ptB.Northing - ptA.Northing;

        if (Math.Abs(dx) < double.Epsilon && Math.Abs(dz) < double.Epsilon)
            return false;

        double t = ((point.Easting - ptA.Easting) * dx + (point.Northing - ptA.Northing) * dz)
            / (dx * dx + dz * dz);

        return t >= 0 && t <= 1;
    }

    /// <summary>
    /// Normalize heading error to [-PI, PI] range.
    /// </summary>
    private double NormalizeHeadingError(double error)
    {
        if (error > Math.PI)
            error -= Math.PI;
        else if (error < -Math.PI)
            error += Math.PI;

        if (error > PIBy2)
            error -= Math.PI;
        else if (error < -PIBy2)
            error += Math.PI;

        return error;
    }
}
