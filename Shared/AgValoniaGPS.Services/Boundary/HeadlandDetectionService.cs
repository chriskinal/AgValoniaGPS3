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

using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Headland;
using System;
using System.Collections.Generic;

namespace AgValoniaGPS.Services.Headland
{
    /// <summary>
    /// Service for detecting tool position relative to headland boundaries.
    /// Supports section control, proximity warnings, and look-ahead detection.
    /// </summary>
    public class HeadlandDetectionService
    {
        /// <summary>
        /// Perform headland detection for tool sections and proximity.
        /// </summary>
        public HeadlandDetectionOutput DetectHeadland(HeadlandDetectionInput input)
        {
            var output = new HeadlandDetectionOutput();

            if (input.Boundaries.Count == 0 || input.Boundaries[0].HeadlandLine.Count == 0)
            {
                return output;
            }

            // Detect section corner positions
            DetectToolCorners(input, output);

            // Detect look-ahead positions for section control
            DetectToolLookOnPoints(input, output);

            // Calculate proximity to headland boundary
            CalculateHeadlandProximity(input, output);

            return output;
        }

        /// <summary>
        /// Check if a point is inside the headland area (accounting for holes).
        /// </summary>
        public bool IsPointInsideHeadArea(Vec2 point, List<BoundaryData> boundaries)
        {
            if (boundaries.Count == 0 || boundaries[0].HeadlandLine.Count == 0)
            {
                return false;
            }

            // If inside outer boundary, then potentially in headland
            if (GeometryMath.IsPointInPolygon(boundaries[0].HeadlandLine, new Vec3(point.Easting, point.Northing, 0)))
            {
                // Check if point is inside any holes (drive-through boundaries)
                for (int i = 1; i < boundaries.Count; i++)
                {
                    if (GeometryMath.IsPointInPolygon(boundaries[i].HeadlandLine, new Vec3(point.Easting, point.Northing, 0)))
                    {
                        return false; // Inside a hole, not in headland
                    }
                }
                return true;
            }
            return false;
        }

        private void DetectToolCorners(HeadlandDetectionInput input, HeadlandDetectionOutput output)
        {
            if (input.Sections.Count == 0)
            {
                return;
            }

            bool isLeftInWk, isRightInWk = true;

            for (int j = 0; j < input.Sections.Count; j++)
            {
                // For first section, check left point. For others, reuse previous right point check
                isLeftInWk = j == 0
                    ? IsPointInsideHeadArea(input.Sections[j].LeftPoint, input.Boundaries)
                    : isRightInWk;

                isRightInWk = IsPointInsideHeadArea(input.Sections[j].RightPoint, input.Boundaries);

                // Save left side (first section only)
                if (j == 0)
                {
                    output.IsLeftSideInHeadland = !isLeftInWk;
                }

                // Section is in headland if both corners are NOT in work area
                var status = new SectionHeadlandStatus(!isLeftInWk && !isRightInWk, IsLookOnInHeadland: false);
                output.SectionStatus.Add(status);
            }

            // Save right side (last section)
            output.IsRightSideInHeadland = !isRightInWk;

            // Tool is in headland if both outer points are in headland
            output.IsToolOuterPointsInHeadland = output.IsLeftSideInHeadland && output.IsRightSideInHeadland;
        }

        private void DetectToolLookOnPoints(HeadlandDetectionInput input, HeadlandDetectionOutput output)
        {
            if (input.Sections.Count == 0 || output.SectionStatus.Count != input.Sections.Count)
            {
                return;
            }

            bool isLookRightIn = false;

            Vec3 toolFix = input.VehiclePosition;
            double sinAB = Math.Sin(toolFix.Heading);
            double cosAB = Math.Cos(toolFix.Heading);

            // Generate look-ahead points for finding closest point
            double pos = 0;
            double mOn = (input.LookAhead.LookAheadDistanceOnPixelsRight - input.LookAhead.LookAheadDistanceOnPixelsLeft)
                         / input.LookAhead.TotalWidth;

            for (int j = 0; j < input.Sections.Count; j++)
            {
                // For first section, check left look-ahead point. For others, reuse previous right point check
                bool isLookLeftIn = j == 0
                    ? IsPointInsideHeadArea(new Vec2(
                        input.Sections[j].LeftPoint.Easting + (sinAB * input.LookAhead.LookAheadDistanceOnPixelsLeft * 0.1),
                        input.Sections[j].LeftPoint.Northing + (cosAB * input.LookAhead.LookAheadDistanceOnPixelsLeft * 0.1)),
                        input.Boundaries)
                    : isLookRightIn;

                pos += input.Sections[j].SectionWidth;
                double endHeight = (input.LookAhead.LookAheadDistanceOnPixelsLeft + (mOn * pos)) * 0.1;

                isLookRightIn = IsPointInsideHeadArea(new Vec2(
                    input.Sections[j].RightPoint.Easting + (sinAB * endHeight),
                    input.Sections[j].RightPoint.Northing + (cosAB * endHeight)),
                    input.Boundaries);

                // Look-ahead is in headland if both look points are NOT in work area
                output.SectionStatus[j] = output.SectionStatus[j] with { IsLookOnInHeadland = !isLookLeftIn && !isLookRightIn };
            }
        }

        private void CalculateHeadlandProximity(HeadlandDetectionInput input, HeadlandDetectionOutput output)
        {
            if (!input.IsHeadlandOn || input.Boundaries.Count == 0 || input.Boundaries[0].HeadlandLine.Count < 2)
            {
                output.HeadlandNearestPoint = null;
                output.HeadlandDistance = null;
                output.ShouldTriggerWarning = false;
                return;
            }

            Vec3 vehiclePos = input.VehiclePosition;

            // Find nearest point on headland boundary
            Vec2? nearest = GeometryMath.RaycastToPolygon(vehiclePos, input.Boundaries[0].HeadlandLine);
            if (!nearest.HasValue)
            {
                output.HeadlandNearestPoint = null;
                output.HeadlandDistance = null;
                output.ShouldTriggerWarning = false;
                return;
            }

            Vec2 nearestVal = nearest.Value;
            double distance = GeometryMath.Distance(vehiclePos.ToVec2(), nearestVal);

            output.HeadlandNearestPoint = nearestVal;
            output.HeadlandDistance = distance;

            // Check if vehicle is inside the headland
            bool isInside = GeometryMath.IsPointInPolygon(input.Boundaries[0].HeadlandLine, vehiclePos);

            // Calculate angle to nearest point
            double dx = nearestVal.Easting - vehiclePos.Easting;
            double dy = nearestVal.Northing - vehiclePos.Northing;
            double angleToPolygon = Math.Atan2(dx, dy);
            double headingDiff = GeometryMath.AngleDiff(vehiclePos.Heading, angleToPolygon);
            bool headingOk = headingDiff < GeometryMath.ToRadians(60);

            // Warning logic: trigger if heading toward boundary and within threshold
            output.ShouldTriggerWarning =
                (isInside && headingOk && distance < 20.0) ||
                (!isInside && headingOk && distance < 5.0);
        }
    }
}
