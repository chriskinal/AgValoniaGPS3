using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Base;

namespace AgOpenGPS.Core.Services
{
    /// <summary>
    /// Core tramline generation service.
    /// Generates inner and outer tramline offset paths from boundary fence lines.
    /// </summary>
    public class TramlineService : ITramlineService
    {
        private const double PIBy2 = Math.PI / 2.0;
        private const double MinSpacingSquared = 2.0; // Minimum distance squared between consecutive points

        /// <summary>
        /// Generate inner tramline offset from boundary fence line.
        /// Inner tramline is offset inward by (tramWidth * 0.5) + halfWheelTrack.
        /// </summary>
        /// <param name="fenceLine">Boundary fence line points with headings</param>
        /// <param name="tramWidth">Width of tram passes</param>
        /// <param name="halfWheelTrack">Half of vehicle wheel track width</param>
        /// <returns>List of inner tramline points</returns>
        public List<Vec2> GenerateInnerTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
        {
            double offset = (tramWidth * 0.5) + halfWheelTrack;
            return GenerateTramlineOffset(fenceLine, offset);
        }

        /// <summary>
        /// Generate outer tramline offset from boundary fence line.
        /// Outer tramline is offset inward by (tramWidth * 0.5) - halfWheelTrack.
        /// </summary>
        /// <param name="fenceLine">Boundary fence line points with headings</param>
        /// <param name="tramWidth">Width of tram passes</param>
        /// <param name="halfWheelTrack">Half of vehicle wheel track width</param>
        /// <returns>List of outer tramline points</returns>
        public List<Vec2> GenerateOuterTramline(List<Vec3> fenceLine, double tramWidth, double halfWheelTrack)
        {
            double offset = (tramWidth * 0.5) - halfWheelTrack;
            return GenerateTramlineOffset(fenceLine, offset);
        }

        /// <summary>
        /// Core algorithm to generate tramline offset from boundary fence line.
        /// Algorithm:
        /// 1. For each fence point, calculate perpendicular offset point
        /// 2. Check if offset point is far enough from all fence points (collision detection)
        /// 3. Check if offset point is far enough from previous added point (spacing)
        /// 4. Add point if both checks pass
        /// </summary>
        /// <param name="fenceLine">Boundary fence line points with headings</param>
        /// <param name="offset">Offset distance from fence line</param>
        /// <returns>List of tramline points</returns>
        private List<Vec2> GenerateTramlineOffset(List<Vec3> fenceLine, double offset)
        {
            if (fenceLine == null || fenceLine.Count == 0)
            {
                return new List<Vec2>();
            }

            List<Vec2> tramline = new List<Vec2>();
            int ptCount = fenceLine.Count;
            double distSq = offset * offset * 0.999; // Distance threshold for collision detection

            // Process each fence point
            for (int i = 0; i < ptCount; i++)
            {
                // Calculate perpendicular offset point
                // Offset is perpendicular to heading (heading + PI/2)
                Vec3 fencePoint = fenceLine[i];
                Vec2 offsetPoint = new Vec2(
                    fencePoint.Easting - (Math.Sin(PIBy2 + fencePoint.Heading) * offset),
                    fencePoint.Northing - (Math.Cos(PIBy2 + fencePoint.Heading) * offset)
                );

                // Check if offset point collides with fence line
                bool shouldAdd = true;
                for (int j = 0; j < ptCount; j++)
                {
                    double distanceSquared = DistanceSquared(
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
                        double spacingSquared =
                            (offsetPoint.Easting - lastPoint.Easting) * (offsetPoint.Easting - lastPoint.Easting) +
                            (offsetPoint.Northing - lastPoint.Northing) * (offsetPoint.Northing - lastPoint.Northing);

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

        /// <summary>
        /// Calculate squared distance between two points.
        /// Using squared distance avoids expensive sqrt() calculation.
        /// </summary>
        private double DistanceSquared(double northing1, double easting1, double northing2, double easting2)
        {
            double dNorth = northing1 - northing2;
            double dEast = easting1 - easting2;
            return (dNorth * dNorth) + (dEast * dEast);
        }
    }
}
