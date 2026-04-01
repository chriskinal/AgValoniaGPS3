// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using TrackModel = AgValoniaGPS.Models.Track.Track;

namespace AgValoniaGPS.Services.Track;

/// <summary>
/// Finds the closest guidance track to the vehicle position.
/// Matches legacy AgOpenGPS CTrack.FindClosestRefTrack() behavior.
/// </summary>
public static class AutoTrackSelectionService
{
    private const double MAX_HEADING_DIFF = 1.0; // ~57 degrees in radians
    private const double MIN_HEADING_DIFF = 2.14; // ~123 degrees (allows opposite direction)

    /// <summary>
    /// Find the closest visible, heading-aligned track to the vehicle.
    /// </summary>
    /// <param name="tracks">Available tracks</param>
    /// <param name="position">Vehicle position (easting, northing)</param>
    /// <param name="headingRadians">Vehicle heading in radians</param>
    /// <returns>The closest track, or null if none is aligned</returns>
    public static TrackModel? FindClosestTrack(
        IReadOnlyList<TrackModel> tracks,
        Vec2 position,
        double headingRadians)
    {
        if (tracks.Count == 0) return null;

        TrackModel? closest = null;
        double minDistance = double.MaxValue;

        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (!track.IsVisible || track.Points.Count < 2)
                continue;

            // Heading alignment check for AB lines (curves skip this)
            if (track.IsABLine)
            {
                double trackHeading = Math.Atan2(
                    track.Points[1].Easting - track.Points[0].Easting,
                    track.Points[1].Northing - track.Points[0].Northing);

                double diff = Math.Abs(headingRadians - trackHeading);
                // Normalize to [0, PI]
                while (diff > Math.PI) diff -= Math.PI * 2;
                diff = Math.Abs(diff);

                // Must be within ~57 degrees or within ~57 degrees of opposite
                if (diff > MAX_HEADING_DIFF && diff < MIN_HEADING_DIFF)
                    continue;
            }

            double distance = CalculateDistanceToTrack(track, position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = track;
            }
        }

        return closest;
    }

    /// <summary>
    /// Calculate perpendicular distance from position to track.
    /// AB lines: distance to infinite line through A and B.
    /// Curves: minimum distance to any segment.
    /// </summary>
    private static double CalculateDistanceToTrack(TrackModel track, Vec2 position)
    {
        if (track.IsABLine && track.Points.Count == 2)
        {
            return PerpendicularDistanceToLine(
                position,
                track.Points[0].Easting, track.Points[0].Northing,
                track.Points[1].Easting, track.Points[1].Northing);
        }

        // Curve: find minimum distance to any segment
        double minDist = double.MaxValue;
        for (int i = 0; i < track.Points.Count - 1; i++)
        {
            double dist = DistanceToSegment(
                position,
                track.Points[i].Easting, track.Points[i].Northing,
                track.Points[i + 1].Easting, track.Points[i + 1].Northing);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    private static double PerpendicularDistanceToLine(
        Vec2 point, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-10) return GeometryMath.Distance(point, new Vec2(ax, ay));

        // Perpendicular distance to infinite line
        return Math.Abs(dy * point.Easting - dx * point.Northing + bx * ay - by * ax) / Math.Sqrt(lenSq);
    }

    private static double DistanceToSegment(
        Vec2 point, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-10) return GeometryMath.Distance(point, new Vec2(ax, ay));

        double t = Math.Clamp(((point.Easting - ax) * dx + (point.Northing - ay) * dy) / lenSq, 0, 1);
        double closestX = ax + t * dx;
        double closestY = ay + t * dy;
        double ddx = point.Easting - closestX;
        double ddy = point.Northing - closestY;
        return Math.Sqrt(ddx * ddx + ddy * ddy);
    }
}
