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

/* Special thanks to erik.nordeus@gmail.com for his core dubins code originally written
 * in Unity. I converted this to work as a class in C# from that Unity C Script
 *  http://www.habrador.com/about/
 *
 * Migrated to AgOpenGPS.Core for shared use between WinForms and Avalonia UIs.
 */

namespace AgValoniaGPS.Services.PathPlanning
{
    //To keep track of the different paths when debugging
    public enum DubinsPathType
    { RSR, LSL, RSL, LSR, RLR, LRL }

    /// <summary>
    /// Core Dubins path planning service.
    /// Generates shortest paths between two points with specified headings and minimum turning radius.
    /// </summary>
    public class DubinsPathService
    {
        //How far we are driving each update, the accuracy will improve if we lower the driveDistance
        public const double DriveDistance = 0.05;

        //The radius the vehicle can turn 360 degrees with (set via property)
        public double TurningRadius { get; set; }

        //Position, Heading is in radians
        private Vec2 startPos, goalPos;
        private double startHeading, goalHeading;

        private List<DubinsPathData> pathDataList = new List<DubinsPathData>();
        private readonly List<Vec3> dubinsShortestPathList = new List<Vec3>();

        public DubinsPathService(double turningRadius)
        {
            TurningRadius = turningRadius;
        }

        //takes 2 points and headings to create a path - returns list of vec3 points and headings
        public List<Vec3> GeneratePath(Vec3 start, Vec3 goal)
        {
            //positions and heading
            startPos.Easting = start.Easting;
            startPos.Northing = start.Northing;
            startHeading = start.Heading;

            goalPos.Easting = goal.Easting;
            goalPos.Northing = goal.Northing;
            goalHeading = goal.Heading;

            //Get all valid Dubins paths
            pathDataList = GetAllDubinsPaths();

            //clear out existing path of vec3 points
            dubinsShortestPathList.Clear();

            //int pathsCnt = pathDataList.Count;
            if (pathDataList.Count > 0)
            {
                int cnt = pathDataList[0].PathCoordinates.Count;
                if (cnt > 1)
                {
                    //calculate the heading for each point
                    for (int i = 0; i < cnt - 1; i += 5)
                    {
                        Vec3 pt = new Vec3(pathDataList[0].PathCoordinates[i].Easting, pathDataList[0].PathCoordinates[i].Northing, 0)
                        {
                            Heading = Math.Atan2(pathDataList[0].PathCoordinates[i + 1].Easting - pathDataList[0].PathCoordinates[i].Easting,
                            pathDataList[0].PathCoordinates[i + 1].Northing - pathDataList[0].PathCoordinates[i].Northing)
                        };
                        dubinsShortestPathList.Add(pt);
                    }
                }
            }
            return dubinsShortestPathList;
        }

        //The 4 different circles we have that sits to the left/right of the start/goal
        private Vec2 startLeftCircle, startRightCircle, goalLeftCircle, goalRightCircle;

        //Get all valid Dubins paths sorted from shortest to longest
        private List<DubinsPathData> GetAllDubinsPaths()
        {
            //Reset the list with all Dubins paths
            pathDataList.Clear();

            //Position the circles that are to the left/right of the cars
            PositionLeftRightCircles();

            //Find the length of each path with tangent coordinates
            CalculateDubinsPathsLengths();

            //If we have paths
            if (pathDataList.Count > 0)
            {
                //Sort the list with paths so the shortest path is first
                pathDataList.Sort((x, y) => x.TotalLength.CompareTo(y.TotalLength));

                //Generate the final coordinates of the path from tangent points and segment lengths
                GeneratePathCoordinates();
            }

            //No paths could be found
            else
            {
                pathDataList.Clear();
            }

            //return either empty or the actual list.
            return pathDataList;
        }

        //Position the left and right circles that are to the left/right of the target and the car
        private void PositionLeftRightCircles()
        {
            //Goal pos
            goalRightCircle = DubinsMath.GetRightCircleCenterPos(goalPos, goalHeading, TurningRadius);
            goalLeftCircle = DubinsMath.GetLeftCircleCenterPos(goalPos, goalHeading, TurningRadius);

            //Start pos
            startRightCircle = DubinsMath.GetRightCircleCenterPos(startPos, startHeading, TurningRadius);
            startLeftCircle = DubinsMath.GetLeftCircleCenterPos(startPos, startHeading, TurningRadius);
        }

        //Calculate the path lengths of all Dubins paths by using tangent points
        private void CalculateDubinsPathsLengths()
        {
            //RSR ****RSR and LSL is only working if the circles don't have the same position
            if (startRightCircle.Easting != goalRightCircle.Easting && startRightCircle.Northing != goalRightCircle.Northing)
            {
                Get_RSR_Length();
            }

            //LSL
            if (startLeftCircle.Easting != goalLeftCircle.Easting && startLeftCircle.Northing != goalLeftCircle.Northing)
            {
                Get_LSL_Length();
            }

            //RSL and LSR is only working of the circles don't intersect
            double comparisonSqr = TurningRadius * 2.0 * TurningRadius * 2.0;

            //RSL
            if ((startRightCircle - goalLeftCircle).GetLengthSquared() > comparisonSqr)
            {
                Get_RSL_Length();
            }

            //LSR
            if ((startLeftCircle - goalRightCircle).GetLengthSquared() > comparisonSqr)
            {
                Get_LSR_Length();
            }

            //With the LRL and RLR paths, the distance between the circles have to be less than 4 * r
            comparisonSqr = 4.0 * TurningRadius * 4.0 * TurningRadius;

            //RLR
            if ((startRightCircle - goalRightCircle).GetLengthSquared() < comparisonSqr)
            {
                Get_RLR_Length();
            }

            //LRL
            if ((startLeftCircle - goalLeftCircle).GetLengthSquared() < comparisonSqr)
            {
                Get_LRL_Length();
            }
        }

        //RSR
        private void Get_RSR_Length()
        {
            //Find both tangent positons
            DubinsMath.LSLorRSR(startRightCircle, goalRightCircle, false, TurningRadius, out Vec2 startTangent, out Vec2 goalTangent);

            //Calculate lengths
            double length1 = DubinsMath.GetArcLength(startRightCircle, startPos, startTangent, false, TurningRadius);
            double length2 = (startTangent - goalTangent).GetLength();
            double length3 = DubinsMath.GetArcLength(goalRightCircle, goalTangent, goalPos, false, TurningRadius);

            //Save the data
            DubinsPathData pathData = new DubinsPathData(length1, length2, length3, startTangent, goalTangent, DubinsPathType.RSR)
            {
                //We also need this data to simplify when generating the final path
                Segment2Turning = false
            };

            //RSR
            pathData.SetIfTurningRight(true, false, true);

            //Add the path to the collection of all paths
            pathDataList.Add(pathData);
        }

        //LSL
        private void Get_LSL_Length()
        {
            //Find both tangent positions
            DubinsMath.LSLorRSR(startLeftCircle, goalLeftCircle, true, TurningRadius, out Vec2 startTangent, out Vec2 goalTangent);

            //Calculate lengths
            double length1 = DubinsMath.GetArcLength(startLeftCircle, startPos, startTangent, true, TurningRadius);
            double length2 = (startTangent - goalTangent).GetLength();
            double length3 = DubinsMath.GetArcLength(goalLeftCircle, goalTangent, goalPos, true, TurningRadius);

            //Save the data
            DubinsPathData pathData = new DubinsPathData(length1, length2, length3, startTangent, goalTangent, DubinsPathType.LSL)
            {
                //We also need this data to simplify when generating the final path
                Segment2Turning = false
            };

            //LSL
            pathData.SetIfTurningRight(false, false, false);

            //Add the path to the collection of all paths
            pathDataList.Add(pathData);
        }

        //RSL
        private void Get_RSL_Length()
        {
            //Find both tangent positions
            DubinsMath.RSLorLSR(startRightCircle, goalLeftCircle, false, TurningRadius, out Vec2 startTangent, out Vec2 goalTangent);

            //Calculate lengths
            double length1 = DubinsMath.GetArcLength(startRightCircle, startPos, startTangent, false, TurningRadius);
            double length2 = (startTangent - goalTangent).GetLength();
            double length3 = DubinsMath.GetArcLength(goalLeftCircle, goalTangent, goalPos, true, TurningRadius);

            //Save the data
            DubinsPathData pathData = new DubinsPathData(length1, length2, length3, startTangent, goalTangent, DubinsPathType.RSL)
            {
                //We also need this data to simplify when generating the final path
                Segment2Turning = false
            };

            //RSL
            pathData.SetIfTurningRight(true, false, false);

            //Add the path to the collection of all paths
            pathDataList.Add(pathData);
        }

        //LSR
        private void Get_LSR_Length()
        {
            //Find both tangent positions
            DubinsMath.RSLorLSR(startLeftCircle, goalRightCircle, true, TurningRadius, out Vec2 startTangent, out Vec2 goalTangent);

            //Calculate lengths
            double length1 = DubinsMath.GetArcLength(startLeftCircle, startPos, startTangent, true, TurningRadius);
            double length2 = (startTangent - goalTangent).GetLength();
            double length3 = DubinsMath.GetArcLength(goalRightCircle, goalTangent, goalPos, false, TurningRadius);

            //Save the data
            DubinsPathData pathData = new DubinsPathData(length1, length2, length3, startTangent, goalTangent, DubinsPathType.LSR)
            {
                //We also need this data to simplify when generating the final path
                Segment2Turning = false
            };

            //LSR
            pathData.SetIfTurningRight(false, false, true);

            //Add the path to the collection of all paths
            pathDataList.Add(pathData);
        }

        //RLR - Find both tangent positions and the position of the 3rd circle
        private void Get_RLR_Length()
        {
            DubinsMath.GetRLRorLRLTangents(
                startRightCircle,
                goalRightCircle,
                false,
                TurningRadius,
                out Vec2 startTangent,
                out Vec2 goalTangent,
                out Vec2 middleCircle);

            //Calculate lengths
            double length1 = DubinsMath.GetArcLength(startRightCircle, startPos, startTangent, false, TurningRadius);
            double length2 = DubinsMath.GetArcLength(middleCircle, startTangent, goalTangent, true, TurningRadius);
            double length3 = DubinsMath.GetArcLength(goalRightCircle, goalTangent, goalPos, false, TurningRadius);

            //Save the data
            DubinsPathData pathData = new DubinsPathData(length1, length2, length3, startTangent, goalTangent, DubinsPathType.RLR)
            {
                //We also need this data to simplify when generating the final path
                Segment2Turning = true
            };

            //RLR
            pathData.SetIfTurningRight(true, false, true);

            //Add the path to the collection of all paths
            pathDataList.Add(pathData);
        }

        //LRL - Find both tangent positions and the position of the 3rd circle
        private void Get_LRL_Length()
        {
            DubinsMath.GetRLRorLRLTangents(
                startLeftCircle,
                goalLeftCircle,
                true,
                TurningRadius,
                out Vec2 startTangent,
                out Vec2 goalTangent,
                out Vec2 middleCircle);

            //Calculate the total length of this path
            double length1 = DubinsMath.GetArcLength(startLeftCircle, startPos, startTangent, true, TurningRadius);
            double length2 = DubinsMath.GetArcLength(middleCircle, startTangent, goalTangent, false, TurningRadius);
            double length3 = DubinsMath.GetArcLength(goalLeftCircle, goalTangent, goalPos, true, TurningRadius);

            //Save the data
            DubinsPathData pathData = new DubinsPathData(length1, length2, length3, startTangent, goalTangent, DubinsPathType.LRL)
            {
                //We also need this data to simplify when generating the final path
                Segment2Turning = true
            };

            //LRL
            pathData.SetIfTurningRight(false, true, false);

            //Add the path to the collection of all paths
            pathDataList.Add(pathData);
        }

        //
        // Generate the final path from the tangent points
        //

        //When we have found the tangent points and lengths of each path we need to get the individual coordinates
        //of the entire path so we can travel along the path
        private void GeneratePathCoordinates()
        {
            for (int i = 0; i < pathDataList.Count; i++)
            {
                GetTotalPath(pathDataList[i]);
            }
        }

        //Find the coordinates of the entire path from the 2 tangents and length of each segment
        private void GetTotalPath(DubinsPathData pathData)
        {
            //Store the waypoints of the final path here
            List<Vec2> finalPath = new List<Vec2>();

            //Start position of the car
            Vec2 currentPos = startPos;
            //Start heading of the car
            double theta = startHeading;

            //We always have to add the first position manually
            finalPath.Add(currentPos);

            //How many line segments can we fit into this part of the path

            //First
            int segments = (int)Math.Floor(pathData.Length1 / DriveDistance);

            DubinsMath.AddCoordinatesToPath(
                ref currentPos,
                ref theta,
                finalPath,
                segments,
                true,
                pathData.Segment1TurningRight,
                DriveDistance,
                TurningRadius);

            //Second
            segments = (int)Math.Floor(pathData.Length2 / DriveDistance);

            DubinsMath.AddCoordinatesToPath(
                ref currentPos,
                ref theta,
                finalPath,
                segments,
                pathData.Segment2Turning,
                pathData.Segment2TurningRight,
                DriveDistance,
                TurningRadius);

            //Third
            segments = (int)Math.Floor(pathData.Length3 / DriveDistance);

            DubinsMath.AddCoordinatesToPath(
                ref currentPos,
                ref theta,
                finalPath,
                segments,
                true,
                pathData.Segment3TurningRight,
                DriveDistance,
                TurningRadius);

            //Add the final goal coordinate
            finalPath.Add(new Vec2(goalPos.Easting, goalPos.Northing));

            //Save the final path in the path data
            pathData.PathCoordinates = finalPath;
        }
    }

    //Takes care of all standardized methods related the generating of Dubins paths
    public static class DubinsMath
    {
        //Calculate center positions of the Right circle
        public static Vec2 GetRightCircleCenterPos(Vec2 circlePos, double heading, double turningRadius)
        {
            const double PIBy2 = Math.PI / 2.0;
            Vec2 rightCirclePos = new Vec2(0, 0)
            {
                //The circle is 90 degrees (pi/2 radians) to the right of the car's heading
                Easting = circlePos.Easting + (turningRadius * Math.Sin(heading + PIBy2)),
                Northing = circlePos.Northing + (turningRadius * Math.Cos(heading + PIBy2))
            };
            return rightCirclePos;
        }

        //Calculate center positions of the Left circle
        public static Vec2 GetLeftCircleCenterPos(Vec2 circlePos, double heading, double turningRadius)
        {
            const double PIBy2 = Math.PI / 2.0;
            Vec2 leftCirclePos = new Vec2(0, 0)
            {
                //The circle is 90 degrees (pi/2 radians) to the left of the car's heading
                Easting = circlePos.Easting + (turningRadius * Math.Sin(heading - PIBy2)),
                Northing = circlePos.Northing + (turningRadius * Math.Cos(heading - PIBy2))
            };
            return leftCirclePos;
        }

        //
        // Calculate the start and end positions of the tangent lines
        //

        //Outer tangent (LSL and RSR)
        public static void LSLorRSR(Vec2 startCircle, Vec2 goalCircle, bool isBottom, double turningRadius,
                                        out Vec2 startTangent, out Vec2 goalTangent)
        {
            const double PIBy2 = Math.PI / 2.0;
            //The angle to the first tangent coordinate is always 90 degrees if the both circles have the same radius
            double theta = PIBy2;

            //Need to modify theta if the circles are not on the same height (z)
            theta += Math.Atan2(goalCircle.Northing - startCircle.Northing, goalCircle.Easting - startCircle.Easting);

            //Add pi to get the "bottom" coordinate which is on the opposite side (180 degrees = pi)
            if (isBottom) theta += Math.PI;

            //The coordinates of the first tangent points
            double xT1 = startCircle.Easting + (turningRadius * Math.Cos(theta));
            double zT1 = startCircle.Northing + (turningRadius * Math.Sin(theta));

            //To get the second coordinate we need a direction
            //This direction is the same as the direction between the center pos of the circles
            Vec2 dirVec = goalCircle - startCircle;

            double xT2 = xT1 + dirVec.Easting;
            double zT2 = zT1 + dirVec.Northing;

            //The final coordinates of the tangent lines
            startTangent = new Vec2(xT1, zT1);
            goalTangent = new Vec2(xT2, zT2);
        }

        //Inner tangent (RSL and LSR)
        public static void RSLorLSR(
            Vec2 startCircle,
            Vec2 goalCircle,
            bool isBottom,
            double turningRadius,
            out Vec2 startTangent,
            out Vec2 goalTangent)
        {
            //Find the distance between the circles
            double D = (startCircle - goalCircle).GetLength();

            //If the circles have the same radius we can use cosine and not the law of cosines
            //to calculate the angle to the first tangent coordinate
            double theta = Math.Acos((2 * turningRadius) / D);

            //If the circles is LSR, then the first tangent pos is on the other side of the center line
            if (isBottom) theta *= -1.0;

            //Need to modify theta if the circles are not on the same height
            theta += Math.Atan2(goalCircle.Northing - startCircle.Northing, goalCircle.Easting - startCircle.Easting);

            //The coordinates of the first tangent point
            double xT1 = startCircle.Easting + (turningRadius * Math.Cos(theta));
            double zT1 = startCircle.Northing + (turningRadius * Math.Sin(theta));

            //To get the second tangent coordinate we need the direction of the tangent
            //To get the direction we move up 2 circle radius and end up at this coordinate
            double xT1_tmp = startCircle.Easting + (2.0 * turningRadius * Math.Cos(theta));
            double zT1_tmp = startCircle.Northing + (2.0 * turningRadius * Math.Sin(theta));

            //The direction is between the new coordinate and the center of the target circle
            Vec2 dirVec = goalCircle - new Vec2(xT1_tmp, zT1_tmp);

            //The coordinates of the second tangent point is the
            double xT2 = xT1 + dirVec.Easting;
            double zT2 = zT1 + dirVec.Northing;

            //The final coordinates of the tangent lines
            startTangent = new Vec2(xT1, zT1);
            goalTangent = new Vec2(xT2, zT2);
        }

        //Get the RLR or LRL tangent points
        public static void GetRLRorLRLTangents(
            Vec2 startCircle,
            Vec2 goalCircle,
            bool isLRL,
            double turningRadius,
            out Vec2 startTangent,
            out Vec2 goalTangent,
            out Vec2 middleCircle)
        {
            //The distance between the circles
            double D = (startCircle - goalCircle).GetLength();

            //The angle between the goal and the new 3rd circle we create with the law of cosines
            double theta = Math.Acos(D / (4.0 * turningRadius));

            //But we need to modify the angle theta if the circles are not on the same line
            Vec2 V1 = goalCircle - startCircle;

            //Different depending on if we calculate LRL or RLR
            if (isLRL)
                theta = Math.Atan2(V1.Northing, V1.Easting) + theta;
            else
                theta = Math.Atan2(V1.Northing, V1.Easting) - theta;

            //Calculate the position of the third circle
            double x = startCircle.Easting + (2 * turningRadius * Math.Cos(theta));
            double z = startCircle.Northing + (2 * turningRadius * Math.Sin(theta));
            middleCircle = new Vec2(x, z);

            //Calculate the tangent points
            Vec2 V2 = (startCircle - middleCircle).Normalize();
            Vec2 V3 = (goalCircle - middleCircle).Normalize();
            V2 *= turningRadius;
            V3 *= turningRadius;

            startTangent = middleCircle + V2;
            goalTangent = middleCircle + V3;
        }

        //Calculate the length of an circle arc depending on which direction we are driving
        public static double GetArcLength(
            Vec2 circleCenterPos,
            Vec2 startPos,
            Vec2 goalPos,
            bool isLeftCircle,
            double turningRadius)
        {
            Vec2 V1 = startPos - circleCenterPos;
            Vec2 V2 = goalPos - circleCenterPos;

            double theta = Math.Atan2(V2.Northing, V2.Easting) - Math.Atan2(V1.Northing, V1.Easting);
            if (theta < 0.0 && isLeftCircle) theta += 2.0 * Math.PI;
            else if (theta > 0 && !isLeftCircle) theta -= 2.0 * Math.PI;
            return Math.Abs(theta * turningRadius);
        }

        //Loops through segments of a path and add new coordinates to the final path
        public static void AddCoordinatesToPath(
            ref Vec2 currentPos,
            ref double theta,
            List<Vec2> finalPath,
            int segments,
            bool isTurning,
            bool isTurningRight,
            double driveDistance,
            double turningRadius)
        {
            for (int i = 0; i <= segments; i++)
            {
                //Update the position of the car
                currentPos.Easting += driveDistance * Math.Sin(theta);
                currentPos.Northing += driveDistance * Math.Cos(theta);

                //Don't update the heading if we are driving straight
                if (isTurning)
                {
                    //Which way are we turning?
                    double turnParameter = 1.0;

                    if (!isTurningRight) turnParameter = -1.0;

                    //Update the heading
                    theta += (driveDistance / turningRadius) * turnParameter;
                }

                //Add the new coordinate to the path
                finalPath.Add(currentPos);
            }
        }
    }

    //Will hold data related to one Dubins path so we can sort them
    public class DubinsPathData
    {
        //The total length of this path
        public double TotalLength;

        //Need the individual path lengths for debugging and to find the final path
        public double Length1, Length2, Length3;

        //The 2 tangent points we need to connect the lines and curves
        public Vec2 Tangent1, Tangent2;

        //The type, such as RSL
        public DubinsPathType PathType;

        //The coordinates of the final path
        public List<Vec2> PathCoordinates;

        //Are we turning or driving straight in segment 2?
        public bool Segment2Turning;

        //Are we turning right in the particular segment?
        public bool Segment1TurningRight, Segment2TurningRight, Segment3TurningRight;

        public DubinsPathData(double length1, double length2, double length3, Vec2 tangent1, Vec2 tangent2, DubinsPathType pathType)
        {
            //Calculate the total length of this path
            this.TotalLength = length1 + length2 + length3;

            this.Length1 = length1;
            this.Length2 = length2;
            this.Length3 = length3;

            this.Tangent1 = tangent1;
            this.Tangent2 = tangent2;

            this.PathType = pathType;
        }

        //Are we turning right in any of the segments?
        public void SetIfTurningRight(bool segment1TurningRight, bool segment2TurningRight, bool segment3TurningRight)
        {
            this.Segment1TurningRight = segment1TurningRight;
            this.Segment2TurningRight = segment2TurningRight;
            this.Segment3TurningRight = segment3TurningRight;
        }
    }
}
