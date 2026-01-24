using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Services.PathPlanning;

/* Special thanks to erik.nordeus@gmail.com for his core dubins code originally written
 * in Unity. I converted this to work as a class in C# from that Unity C Script
 *  http://www.habrador.com/about/
 */

namespace AgOpenGPS
{
    //To keep track of the different paths when debugging
    public enum PathType
    { RSR, LSL, RSL, LSR, RLR, LRL }

    /// <summary>
    /// WinForms wrapper for Dubins path planning.
    /// Delegates all path generation to Core DubinsPathService.
    /// </summary>
    public class CDubins
    {
        //How far we are driving each update, the accuracy will improve if we lower the driveDistance
        public static readonly double driveDistance = DubinsPathService.DriveDistance;

        //The radius the car can turn 360 degrees with
        public static double turningRadius
        {
            get => _coreService.TurningRadius;
            set => _coreService.TurningRadius = value;
        }

        private static readonly DubinsPathService _coreService = new DubinsPathService(Properties.Settings.Default.set_youTurnRadius);

        //takes 2 points and headings to create a path - returns list of vec3 points and headings
        public List<vec3> GenerateDubins(vec3 _start, vec3 _goal)
        {
            // Convert WinForms vec3 to Core Vec3 (implicit conversion available)
            Vec3 coreStart = new Vec3(_start.easting, _start.northing, _start.heading);
            Vec3 coreGoal = new Vec3(_goal.easting, _goal.northing, _goal.heading);

            // Delegate to Core service
            List<Vec3> corePath = _coreService.GeneratePath(coreStart, coreGoal);

            // Convert back to WinForms vec3
            List<vec3> winformsPath = new List<vec3>(corePath.Count);
            foreach (Vec3 corePoint in corePath)
            {
                vec3 winformsPoint = new vec3(corePoint.Easting, corePoint.Northing, corePoint.Heading);
                winformsPath.Add(winformsPoint);
            }

            return winformsPath;
        }
    }

    //Takes care of all standardized methods related the generating of Dubins paths
    public static class DubinsMath
    {
        //Calculate center positions of the Right circle
        public static vec2 GetRightCircleCenterPos(vec2 circlePos, double heading)
        {
            Vec2 corePos = new Vec2(circlePos.easting, circlePos.northing);
            Vec2 coreResult = Core.Services.PathPlanning.DubinsMath.GetRightCircleCenterPos(corePos, heading, CDubins.turningRadius);
            return new vec2(coreResult.Easting, coreResult.Northing);
        }

        //Calculate center positions of the Left circle
        public static vec2 GetLeftCircleCenterPos(vec2 circlePos, double heading)
        {
            Vec2 corePos = new Vec2(circlePos.easting, circlePos.northing);
            Vec2 coreResult = Core.Services.PathPlanning.DubinsMath.GetLeftCircleCenterPos(corePos, heading, CDubins.turningRadius);
            return new vec2(coreResult.Easting, coreResult.Northing);
        }

        //
        // Calculate the start and end positions of the tangent lines
        //

        //Outer tangent (LSL and RSR)
        public static void LSLorRSR(vec2 startCircle, vec2 goalCircle, bool isBottom,
                                        out vec2 startTangent, out vec2 goalTangent)
        {
            Vec2 coreStart = new Vec2(startCircle.easting, startCircle.northing);
            Vec2 coreGoal = new Vec2(goalCircle.easting, goalCircle.northing);

            Core.Services.PathPlanning.DubinsMath.LSLorRSR(coreStart, coreGoal, isBottom, CDubins.turningRadius,
                out Vec2 coreStartTangent, out Vec2 coreGoalTangent);

            startTangent = new vec2(coreStartTangent.Easting, coreStartTangent.Northing);
            goalTangent = new vec2(coreGoalTangent.Easting, coreGoalTangent.Northing);
        }

        //Inner tangent (RSL and LSR)
        public static void RSLorLSR(
            vec2 startCircle,
            vec2 goalCircle,
            bool isBottom,
            out vec2 startTangent,
            out vec2 goalTangent)
        {
            Vec2 coreStart = new Vec2(startCircle.easting, startCircle.northing);
            Vec2 coreGoal = new Vec2(goalCircle.easting, goalCircle.northing);

            Core.Services.PathPlanning.DubinsMath.RSLorLSR(coreStart, coreGoal, isBottom, CDubins.turningRadius,
                out Vec2 coreStartTangent, out Vec2 coreGoalTangent);

            startTangent = new vec2(coreStartTangent.Easting, coreStartTangent.Northing);
            goalTangent = new vec2(coreGoalTangent.Easting, coreGoalTangent.Northing);
        }

        //Get the RLR or LRL tangent points
        public static void GetRLRorLRLTangents(
            vec2 startCircle,
            vec2 goalCircle,
            bool isLRL,
            out vec2 startTangent,
            out vec2 goalTangent,
            out vec2 middleCircle)
        {
            Vec2 coreStart = new Vec2(startCircle.easting, startCircle.northing);
            Vec2 coreGoal = new Vec2(goalCircle.easting, goalCircle.northing);

            Core.Services.PathPlanning.DubinsMath.GetRLRorLRLTangents(
                coreStart, coreGoal, isLRL, CDubins.turningRadius,
                out Vec2 coreStartTangent, out Vec2 coreGoalTangent, out Vec2 coreMiddleCircle);

            startTangent = new vec2(coreStartTangent.Easting, coreStartTangent.Northing);
            goalTangent = new vec2(coreGoalTangent.Easting, coreGoalTangent.Northing);
            middleCircle = new vec2(coreMiddleCircle.Easting, coreMiddleCircle.Northing);
        }

        //Calculate the length of an circle arc depending on which direction we are driving
        public static double GetArcLength(
            vec2 circleCenterPos,
            vec2 startPos,
            vec2 goalPos,
            bool isLeftCircle)
        {
            Vec2 coreCircleCenter = new Vec2(circleCenterPos.easting, circleCenterPos.northing);
            Vec2 coreStart = new Vec2(startPos.easting, startPos.northing);
            Vec2 coreGoal = new Vec2(goalPos.easting, goalPos.northing);

            return Core.Services.PathPlanning.DubinsMath.GetArcLength(
                coreCircleCenter, coreStart, coreGoal, isLeftCircle, CDubins.turningRadius);
        }

        //Loops through segments of a path and add new coordinates to the final path
        public static void AddCoordinatesToPath(
            ref vec2 currentPos,
            ref double theta,
            List<vec2> finalPath,
            int segments,
            bool isTurning,
            bool isTurningRight)
        {
            Vec2 corePos = new Vec2(currentPos.easting, currentPos.northing);
            List<Vec2> corePath = new List<Vec2>();

            // Copy existing finalPath to Core format
            foreach (vec2 v in finalPath)
            {
                corePath.Add(new Vec2(v.easting, v.northing));
            }

            Core.Services.PathPlanning.DubinsMath.AddCoordinatesToPath(
                ref corePos, ref theta, corePath, segments, isTurning, isTurningRight,
                CDubins.driveDistance, CDubins.turningRadius);

            // Update currentPos
            currentPos.easting = corePos.Easting;
            currentPos.northing = corePos.Northing;

            // Copy new points back to finalPath
            for (int i = finalPath.Count; i < corePath.Count; i++)
            {
                finalPath.Add(new vec2(corePath[i].Easting, corePath[i].Northing));
            }
        }
    }

    //Will hold data related to one Dubins path so we can sort them
    public class OneDubinsPath
    {
        //The total length of this path
        public double totalLength;

        //Need the individual path lengths for debugging and to find the final path
        public double length1, length2, length3;

        //The 2 tangent points we need to connect the lines and curves
        public vec2 tangent1, tangent2;

        //The type, such as RSL
        public PathType pathType;

        //The coordinates of the final path
        public List<vec2> pathCoordinates;

        //Are we turning or driving straight in segment 2?
        public bool segment2Turning;

        //Are we turning right in the particular segment?
        public bool segment1TurningRight, segment2TurningRight, segment3TurningRight;

        public OneDubinsPath(double length1, double length2, double length3, vec2 tangent1, vec2 tangent2, PathType pathType)
        {
            //Calculate the total length of this path
            this.totalLength = length1 + length2 + length3;

            this.length1 = length1;
            this.length2 = length2;
            this.length3 = length3;

            this.tangent1 = tangent1;
            this.tangent2 = tangent2;

            this.pathType = pathType;
        }

        //Are we turning right in any of the segments?
        public void SetIfTurningRight(bool segment1TurningRight, bool segment2TurningRight, bool segment3TurningRight)
        {
            this.segment1TurningRight = segment1TurningRight;
            this.segment2TurningRight = segment2TurningRight;
            this.segment3TurningRight = segment3TurningRight;
        }
    }
}
