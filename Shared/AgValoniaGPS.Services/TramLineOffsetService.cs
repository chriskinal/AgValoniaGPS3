using System;
using System.Collections.Generic;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Services;

/// <summary>
/// Core tramline offset generation service.
/// Generates inner and outer tramline offset paths from boundary fence lines.
/// Used internally by TramLineService.
/// </summary>
public class TramLineOffsetService : ITramLineOffsetService
{
    private const double PIBy2 = Math.PI / 2.0;
    private const double MinSpacingSquared = 2.0; // Minimum distance squared between consecutive points

    /// <summary>
    /// Generate inner tramline offset from boundary fence line.
    /// Inner tramline is offset inward by (tramWidth * 0.5) + halfWheelTrack.
    /// </summary>
    public List<Vec2> GenerateInnerTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
    {
        double offset = (tramWidth * 0.5) + halfWheelTrack;
        return GenerateTramlineOffset(fenceLine, offset);
    }

    /// <summary>
    /// Generate outer tramline offset from boundary fence line.
    /// Outer tramline is offset inward by (tramWidth * 0.5) - halfWheelTrack.
    /// </summary>
    public List<Vec2> GenerateOuterTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
    {
        double offset = (tramWidth * 0.5) - halfWheelTrack;
        return GenerateTramlineOffset(fenceLine, offset);
    }

    /// <summary>
    /// Core algorithm to generate tramline offset from boundary fence line.
    /// </summary>
    private List<Vec2> GenerateTramlineOffset(List<Vec3> fenceLine, double offset)
    {
        if (fenceLine == null || fenceLine.Count == 0)
        {
            return new List<Vec2>();
        }

        var tramline = new List<Vec2>();
        int ptCount = fenceLine.Count;
        double distSq = offset * offset * 0.999; // Distance threshold for collision detection

        // Process each fence point
        for (int i = 0; i < ptCount; i++)
        {
            // Calculate perpendicular offset point
            Vec3 fencePoint = fenceLine[i];
            var offsetPoint = new Vec2(
                fencePoint.Easting - (Math.Sin(PIBy2 + fencePoint.Heading) * offset),
                fencePoint.Northing - (Math.Cos(PIBy2 + fencePoint.Heading) * offset)
            );

            // Check if offset point collides with fence line
            bool shouldAdd = true;
            for (int j = 0; j < ptCount; j++)
            {
                double distanceSquared = GeometryMath.DistanceSquared(
                    offsetPoint.Northing, offsetPoint.Easting,
                    fenceLine[j].Northing, fenceLine[j].Easting);

                if (distanceSquared < distSq)
                {
                    shouldAdd = false;
                    break;
                }
            }

            // If no collision, check spacing from last added point
            if (shouldAdd)
            {
                if (tramline.Count > 0)
                {
                    Vec2 lastPoint = tramline[tramline.Count - 1];
                    double spacingSquared = GeometryMath.DistanceSquared(offsetPoint, lastPoint);

                    // Only add if far enough from last point
                    if (spacingSquared > MinSpacingSquared)
                    {
                        tramline.Add(offsetPoint);
                    }
                }
                else
                {
                    // First point, always add
                    tramline.Add(offsetPoint);
                }
            }
        }

        return tramline;
    }
}
