using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;
using AgOpenGPS.Core.Services.Guidance;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public class CRecPathPt
    {
        public double easting { get; set; }
        public double northing { get; set; }
        public double heading { get; set; }
        public double speed { get; set; }
        public bool autoBtnState { get; set; }

        //constructor
        public CRecPathPt(double _easting, double _northing, double _heading, double _speed,
                            bool _autoBtnState)
        {
            easting = _easting;
            northing = _northing;
            heading = _heading;
            speed = _speed;
            autoBtnState = _autoBtnState;
        }

        public GeoCoord AsGeoCoord => new GeoCoord(northing, easting);
    }

    public class CRecordedPath
    {
        private static readonly CurvePurePursuitGuidanceService _coreCurvePurePursuitService = new CurvePurePursuitGuidanceService();

        //constructor
        public CRecordedPath(FormGPS _f)
        {
            mf = _f;
        }

        //pointers to mainform controls
        private readonly FormGPS mf;

        //the recorded path from driving around
        public List<CRecPathPt> recList = new List<CRecPathPt>();

        //the dubins path to get there
        public List<CRecPathPt> shuttleDubinsList = new List<CRecPathPt>();

        public int shuttleListCount;

        //list of vec3 points of Dubins shortest path between 2 points - To be converted to RecPt
        public List<vec3> shortestDubinsList = new List<vec3>();

        //generated reference line
        public double distanceFromCurrentLinePivot;
        private int A, B, C;

        public int currentPositonIndex;

        //pure pursuit values
        public vec3 pivotAxlePosRP = new vec3(0, 0, 0);

        public vec3 homePos = new vec3();
        public vec2 goalPointRP = new vec2(0, 0);
        public double steerAngleRP, rEastRP, rNorthRP, ppRadiusRP;
        public vec2 radiusPointRP = new vec2(0, 0);

        public bool isEndOfTheRecLine, isRecordOn;
        public bool isDrivingRecordedPath, isFollowingDubinsToPath, isFollowingRecPath, isFollowingDubinsHome;

        public double pivotDistanceError, pivotDistanceErrorLast, pivotDerivative;

        //derivative counters
        private int counter2;

        public double inty;
        public double pivotErrorTotal;

        public int resumeState;

        private int starPathIndx = 0;

        public bool StartDrivingRecordedPath()
        {
            //create the dubins path based on start and goal to start of recorded path
            A = B = C = 0;

            if (recList.Count < 5) return false;

            //save a copy of where we started.
            homePos = mf.pivotAxlePos;

            // Try to find the nearest point of the recordet path in relation to the current position:
            double distance = double.MaxValue;
            int idx = 0;
            int i = 0;

            if (resumeState == 0) //start at the start
            {
                currentPositonIndex = 0;
                idx = 0;
                starPathIndx = 0;
            }
            else if (resumeState == 1) //resume from where stopped mid path
            {
                if (currentPositonIndex + 5 > recList.Count)
                {
                    currentPositonIndex = 0;
                    idx = 0;
                    starPathIndx = 0;
                }
                else
                {
                    idx = starPathIndx = currentPositonIndex;
                }
            }
            else //find closest point
            {
                foreach (CRecPathPt pt in recList)
                {
                    double temp = ((pt.easting - homePos.easting) * (pt.easting - homePos.easting))
                        + ((pt.northing - homePos.northing) * (pt.northing - homePos.northing));

                    if (temp < distance)
                    {
                        distance = temp;
                        idx = i;
                    }
                    i++;
                }

                //scootch down the line a bit
                if (idx + 5 < recList.Count) idx += 5;
                else idx = recList.Count - 1;

                starPathIndx = currentPositonIndex = idx;
            }

            //the goal is the first point of path, the start is the current position
            vec3 goal = new vec3(recList[idx].easting, recList[idx].northing, recList[idx].heading);

            //get the dubins for approach to recorded path
            GetDubinsPath(goal);
            shuttleListCount = shuttleDubinsList.Count;

            //has a valid dubins path been created?
            if (shuttleListCount == 0) return false;

            //starPathIndx = idxFieldSelected;

            //technically all good if we get here so set all the flags
            isFollowingDubinsHome = false;
            isFollowingRecPath = false;
            isFollowingDubinsToPath = true;
            isEndOfTheRecLine = false;
            //currentPositonIndex = 0;
            isDrivingRecordedPath = true;
            return true;
        }

        public bool trig;
        public double north;
        public int pathCount = 0;

        public void UpdatePosition()
        {
            if (isFollowingDubinsToPath)
            {
                //set a speed of 10 kmh
                mf.sim.stepDistance = shuttleDubinsList[C].speed / 50;

                pivotAxlePosRP = mf.pivotAxlePos;

                //StanleyDubinsPath(shuttleListCount);
                PurePursuitDubins(shuttleListCount);

                //check if close to recorded path
                int cnt = shuttleDubinsList.Count;
                pathCount = cnt - B;
                if (pathCount < 8)
                {
                    double distSqr = glm.DistanceSquared(pivotAxlePosRP.northing, pivotAxlePosRP.easting, recList[starPathIndx].northing, recList[starPathIndx].easting);
                    if (distSqr < 2)
                    {
                        isFollowingRecPath = true;
                        isFollowingDubinsToPath = false;
                        shuttleDubinsList.Clear();
                        shortestDubinsList.Clear();
                        C = starPathIndx;
                        A = C + 3;
                        B = A + 1;
                    }
                }
            }

            if (isFollowingRecPath)
            {
                pivotAxlePosRP = mf.pivotAxlePos;

                //StanleyRecPath(recListCount);
                PurePursuitRecPath(recList.Count);

                //if end of the line then stop
                if (!isEndOfTheRecLine)
                {
                    mf.sim.stepDistance = recList[C].speed / 34.86;
                    north = recList[C].northing;

                    pathCount = recList.Count - C;

                    //section control - only if different click the button
                    bool autoBtn = (mf.autoBtnState == btnStates.Auto);
                    trig = autoBtn;
                    if (autoBtn != recList[C].autoBtnState) mf.btnSectionMasterAuto.PerformClick();
                }
                else
                {
                    StopDrivingRecordedPath();
                    return;

                    //create the dubins path based on start and goal to start trip home
                    //GetDubinsPath(homePos);
                    //shuttleListCount = shuttleDubinsList.Count;

                    ////its too small
                    //if (shuttleListCount < 3)
                    //{
                    //    StopDrivingRecordedPath();
                    //    return;
                    //}

                    ////set all the flags
                    //isFollowingDubinsHome = true;
                    //A = B = C = 0;
                    //isFollowingRecPath = false;
                    //isFollowingDubinsToPath = false;
                    //isEndOfTheRecLine = false;
                }
            }

            if (isFollowingDubinsHome)
            {
                int cnt = shuttleDubinsList.Count;
                pathCount = cnt - B;
                if (pathCount < 3)
                {
                    StopDrivingRecordedPath();
                    return;
                }

                mf.sim.stepDistance = shuttleDubinsList[C].speed / 35;
                pivotAxlePosRP = mf.pivotAxlePos;

                //StanleyDubinsPath(shuttleListCount);
                PurePursuitDubins(shuttleListCount);
            }
        }

        public void StopDrivingRecordedPath()
        {
            isFollowingDubinsHome = false;
            isFollowingRecPath = false;
            isFollowingDubinsToPath = false;
            shuttleDubinsList.Clear();
            shortestDubinsList.Clear();
            mf.sim.stepDistance = 0;
            isDrivingRecordedPath = false;
            mf.btnPathGoStop.Image = Properties.Resources.boundaryPlay;
            mf.btnPathRecordStop.Enabled = true;
            mf.btnPickPath.Enabled = true;
            mf.btnResumePath.Enabled = true;
        }

        private void GetDubinsPath(vec3 goal)
        {
            CDubins.turningRadius = mf.yt.youTurnRadius * 1.2;
            CDubins dubPath = new CDubins();

            // current psition
            pivotAxlePosRP = mf.pivotAxlePos;

            //bump it forward
            vec3 pt2 = new vec3
            {
                easting = pivotAxlePosRP.easting + (Math.Sin(pivotAxlePosRP.heading) * 3),
                northing = pivotAxlePosRP.northing + (Math.Cos(pivotAxlePosRP.heading) * 3),
                heading = pivotAxlePosRP.heading
            };

            //get the dubins path vec3 point coordinates of turn
            shortestDubinsList.Clear();
            shuttleDubinsList.Clear();

            shortestDubinsList = dubPath.GenerateDubins(pt2, goal);

            //if Dubins returns 0 elements, there is an unavoidable blockage in the way.
            if (shortestDubinsList.Count > 0)
            {
                shortestDubinsList.Insert(0, mf.pivotAxlePos);

                //transfer point list to recPath class point style
                for (int i = 0; i < shortestDubinsList.Count; i++)
                {
                    CRecPathPt pt = new CRecPathPt(shortestDubinsList[i].easting, shortestDubinsList[i].northing, shortestDubinsList[i].heading, 9.0, false);
                    shuttleDubinsList.Add(pt);
                }
                return;
            }
        }

        private void PurePursuitRecPath(int ptCount)
        {
            // Optimized segment finding for recorded path playback
            // Only search forward from current position (much faster than global search)
            double minDistA = 9999999999;
            double dist;

            int top = currentPositonIndex + 5;
            if (top > ptCount) top = ptCount;

            for (int t = currentPositonIndex; t < top; t++)
            {
                dist = ((pivotAxlePosRP.easting - recList[t].easting) * (pivotAxlePosRP.easting - recList[t].easting))
                                + ((pivotAxlePosRP.northing - recList[t].northing) * (pivotAxlePosRP.northing - recList[t].northing));
                if (dist < minDistA)
                {
                    minDistA = dist;
                    A = t;
                }
            }

            C = A;
            B = A + 1;

            if (B == ptCount)
            {
                // End of line detection
                A--;
                B--;
                isEndOfTheRecLine = true;
            }

            currentPositonIndex = A;

            // Delegate Pure Pursuit calculation to Core
            // Convert recorded path points to Core format
            var corePathPoints = new List<Vec3>(recList.Count);
            for (int i = 0; i < recList.Count; i++)
            {
                corePathPoints.Add(new Vec3(recList[i].easting, recList[i].northing, recList[i].heading));
            }

            var input = new CurvePurePursuitGuidanceInput
            {
                PivotPosition = new Vec3(pivotAxlePosRP.easting, pivotAxlePosRP.northing, pivotAxlePosRP.heading),
                CurvePoints = corePathPoints,
                CurrentLocationIndex = A,
                FindGlobalNearestPoint = false, // Use local search since we already found A
                IsHeadingSameWay = true,
                TrackMode = AgOpenGPS.Core.Models.Guidance.TrackMode.Curve, // Recorded path behaves like a curve
                Wheelbase = mf.vehicle.VehicleConfig.Wheelbase,
                MaxSteerAngle = mf.vehicle.maxSteerAngle,
                PurePursuitIntegralGain = mf.vehicle.purePursuitIntegralGain,
                GoalPointDistance = mf.vehicle.UpdateGoalPointDistance(),
                SideHillCompFactor = 0, // No IMU in recorded path
                FixHeading = mf.fixHeading,
                AvgSpeed = mf.avgSpeed,
                IsReverse = mf.isReverse,
                IsAutoSteerOn = isFollowingRecPath,
                IsYouTurnTriggered = false, // No YouTurn during recorded path playback
                ImuRoll = 88888, // No IMU
                PreviousIntegral = inty,
                PreviousPivotDistanceError = pivotDistanceError,
                PreviousPivotDistanceErrorLast = pivotDistanceErrorLast,
                PreviousCounter = counter2
            };

            var output = _coreCurvePurePursuitService.CalculateGuidanceCurve(input);

            // Unpack results
            distanceFromCurrentLinePivot = output.DistanceFromCurrentLinePivot;
            steerAngleRP = output.SteerAngle;
            goalPointRP.easting = output.GoalPoint.Easting;
            goalPointRP.northing = output.GoalPoint.Northing;
            rEastRP = output.REast;
            rNorthRP = output.RNorth;
            ppRadiusRP = output.RadiusPoint != null ?
                Math.Sqrt((output.RadiusPoint.Easting - pivotAxlePosRP.easting) * (output.RadiusPoint.Easting - pivotAxlePosRP.easting) +
                          (output.RadiusPoint.Northing - pivotAxlePosRP.northing) * (output.RadiusPoint.Northing - pivotAxlePosRP.northing)) : 0;

            // Update state for next iteration
            inty = output.Integral;
            pivotDistanceError = output.PivotDistanceError;
            pivotDistanceErrorLast = output.PivotDistanceErrorLast;
            pivotDerivative = output.PivotDerivative;
            counter2 = output.Counter;

            // UI integration
            mf.vehicle.modeActualXTE = output.ModeActualXTE;
            mf.guidanceLineDistanceOff = output.GuidanceLineDistanceOff;
            mf.guidanceLineSteerAngle = output.GuidanceLineSteerAngle;
        }

        private void PurePursuitDubins(int ptCount)
        {
            // Delegate Pure Pursuit calculation to Core
            // Convert Dubins path points to Core format
            var coreDubinsPoints = new List<Vec3>(shuttleDubinsList.Count);
            for (int i = 0; i < shuttleDubinsList.Count; i++)
            {
                coreDubinsPoints.Add(new Vec3(shuttleDubinsList[i].easting, shuttleDubinsList[i].northing, shuttleDubinsList[i].heading));
            }

            var input = new CurvePurePursuitGuidanceInput
            {
                PivotPosition = new Vec3(pivotAxlePosRP.easting, pivotAxlePosRP.northing, pivotAxlePosRP.heading),
                CurvePoints = coreDubinsPoints,
                CurrentLocationIndex = 0,
                FindGlobalNearestPoint = true, // Search entire Dubins path for closest 2 points
                IsHeadingSameWay = true,
                TrackMode = AgOpenGPS.Core.Models.Guidance.TrackMode.Curve,
                Wheelbase = mf.vehicle.VehicleConfig.Wheelbase,
                MaxSteerAngle = mf.vehicle.maxSteerAngle,
                PurePursuitIntegralGain = mf.vehicle.purePursuitIntegralGain,
                GoalPointDistance = mf.vehicle.UpdateGoalPointDistance(),
                SideHillCompFactor = 0, // No IMU in recorded path
                FixHeading = mf.fixHeading,
                AvgSpeed = mf.avgSpeed,
                IsReverse = mf.isReverse,
                IsAutoSteerOn = mf.isBtnAutoSteerOn,
                IsYouTurnTriggered = mf.yt.isYouTurnTriggered,
                ImuRoll = 88888, // No IMU
                PreviousIntegral = inty,
                PreviousPivotDistanceError = pivotDistanceError,
                PreviousPivotDistanceErrorLast = pivotDistanceErrorLast,
                PreviousCounter = counter2
            };

            var output = _coreCurvePurePursuitService.CalculateGuidanceCurve(input);

            // Update A, B, C for WinForms state tracking
            A = output.CurrentLocationIndex;
            B = A + 1;
            if (B >= shuttleDubinsList.Count) B = shuttleDubinsList.Count - 1;
            C = A;

            // Unpack results
            distanceFromCurrentLinePivot = output.DistanceFromCurrentLinePivot;
            steerAngleRP = output.SteerAngle;
            goalPointRP.easting = output.GoalPoint.Easting;
            goalPointRP.northing = output.GoalPoint.Northing;
            rEastRP = output.REast;
            rNorthRP = output.RNorth;
            ppRadiusRP = output.RadiusPoint != null ?
                Math.Sqrt((output.RadiusPoint.Easting - pivotAxlePosRP.easting) * (output.RadiusPoint.Easting - pivotAxlePosRP.easting) +
                          (output.RadiusPoint.Northing - pivotAxlePosRP.northing) * (output.RadiusPoint.Northing - pivotAxlePosRP.northing)) : 0;

            // Radius point clamping (Dubins-specific)
            if (ppRadiusRP < -500) ppRadiusRP = -500;
            if (ppRadiusRP > 500) ppRadiusRP = 500;

            radiusPointRP.easting = pivotAxlePosRP.easting + (ppRadiusRP * Math.Cos(GeometryMath.twoPI - mf.fixHeading + output.Integral));
            radiusPointRP.northing = pivotAxlePosRP.northing + (ppRadiusRP * Math.Sin(GeometryMath.twoPI - mf.fixHeading + output.Integral));

            // Update state for next iteration
            inty = output.Integral;
            pivotDistanceError = output.PivotDistanceError;
            pivotDistanceErrorLast = output.PivotDistanceErrorLast;
            pivotDerivative = output.PivotDerivative;
            counter2 = output.Counter;

            // UI integration
            mf.guidanceLineDistanceOff = output.GuidanceLineDistanceOff;
            mf.guidanceLineSteerAngle = output.GuidanceLineSteerAngle;
        }

        public void DrawRecordedLine()
        {
            int ptCount = recList.Count;
            if (ptCount < 1) return;
            GL.LineWidth(1);
            GL.Color3(0.98f, 0.92f, 0.460f);
            GL.Begin(PrimitiveType.LineStrip);
            for (int h = 0; h < ptCount; h++) GL.Vertex3(recList[h].easting, recList[h].northing, 0);
            GL.End();

            if (!isRecordOn)
            {
                //Draw lookahead Point
                GL.PointSize(16.0f);
                GL.Begin(PrimitiveType.Points);

                //GL.Color(1.0f, 1.0f, 0.25f);
                //GL.Vertex(rEast, rNorth, 0.0);

                GL.Color3(1.0f, 0.5f, 0.95f);
                GL.Vertex3(recList[currentPositonIndex].easting, recList[currentPositonIndex].northing, 0);
                GL.End();
                GL.PointSize(1.0f);
            }
        }

        public void DrawDubins()
        {
            if (shuttleDubinsList.Count > 1)
            {
                //GL.LineWidth(2);
                GL.PointSize(2);
                GL.Color3(0.298f, 0.96f, 0.2960f);
                GL.Begin(PrimitiveType.Points);
                for (int h = 0; h < shuttleDubinsList.Count; h++)
                    GL.Vertex3(shuttleDubinsList[h].easting, shuttleDubinsList[h].northing, 0);
                GL.End();
            }
        }
    }
}

//private void StanleyDubinsPath(int ptCount)
//{
//    //distanceFromCurrentLine = 9999;
//    //find the closest 2 points to current fix
//    double minDistA = 9999999999;
//    for (int t = 0; t < ptCount; t++)
//    {
//        double dist = ((pivotAxlePosRP.easting - shuttleDubinsList[t].easting) * (pivotAxlePosRP.easting - shuttleDubinsList[t].easting))
//                        + ((pivotAxlePosRP.northing - shuttleDubinsList[t].northing) * (pivotAxlePosRP.northing - shuttleDubinsList[t].northing));
//        if (dist < minDistA)
//        {
//            minDistA = dist;
//            A = t;
//        }
//    }

//    //save the closest point
//    C = A;
//    //next point is the next in list
//    B = A + 1;
//    if (B == ptCount) { A--; B--; }                //don't go past the end of the list - "end of the line" trigger

//    //get the distance from currently active AB line
//    //x2-x1
//    double dx = shuttleDubinsList[B].easting - shuttleDubinsList[A].easting;
//    //z2-z1
//    double dz = shuttleDubinsList[B].northing - shuttleDubinsList[A].northing;

//    if (Math.Abs(dx) < Double.Epsilon && Math.Abs(dz) < Double.Epsilon) return;

//    //abHeading = Math.Atan2(dz, dx);
//    abHeading = shuttleDubinsList[A].heading;

//    //how far from current AB Line is fix
//    distanceFromCurrentLinePivot = ((dz * pivotAxlePosRP.easting) - (dx * pivotAxlePosRP
//        .northing) + (shuttleDubinsList[B].easting
//                * shuttleDubinsList[A].northing) - (shuttleDubinsList[B].northing * shuttleDubinsList[A].easting))
//                    / Math.Sqrt((dz * dz) + (dx * dx));

//    //are we on the right side or not
//    isOnRightSideCurrentLine = distanceFromCurrentLinePivot > 0;

//    // calc point on ABLine closest to current position
//    double U = (((pivotAxlePosRP.easting - shuttleDubinsList[A].easting) * dx)
//                + ((pivotAxlePosRP.northing - shuttleDubinsList[A].northing) * dz))
//                / ((dx * dx) + (dz * dz));

//    rEastRP = shuttleDubinsList[A].easting + (U * dx);
//    rNorthRP = shuttleDubinsList[A].northing + (U * dz);

//    //the first part of stanley is to extract heading error
//    double abFixHeadingDelta = (pivotAxlePosRP.heading - abHeading);

//    //Fix the circular error - get it from -Pi/2 to Pi/2
//    if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
//    else if (abFixHeadingDelta < Math.PI) abFixHeadingDelta += Math.PI;
//    if (abFixHeadingDelta > glm.PIBy2) abFixHeadingDelta -= Math.PI;
//    else if (abFixHeadingDelta < -glm.PIBy2) abFixHeadingDelta += Math.PI;

//    //normally set to 1, less then unity gives less heading error.
//    abFixHeadingDelta *= mf.vehicle.stanleyHeadingErrorGain;
//    if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
//    if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

//    //the non linear distance error part of stanley
//    steerAngleRP = Math.Atan((distanceFromCurrentLinePivot * mf.vehicle.stanleyDistanceErrorGain) / ((mf.pn.speed * 0.277777) + 1));

//    //clamp it to max 42 degrees
//    if (steerAngleRP > 0.74) steerAngleRP = 0.74;
//    if (steerAngleRP < -0.74) steerAngleRP = -0.74;

//    //add them up and clamp to max in vehicle settings
//    steerAngleRP = glm.toDegrees((steerAngleRP + abFixHeadingDelta) * -1.0);
//    if (steerAngleRP < -mf.vehicle.maxSteerAngle) steerAngleRP = -mf.vehicle.maxSteerAngle;
//    if (steerAngleRP > mf.vehicle.maxSteerAngle) steerAngleRP = mf.vehicle.maxSteerAngle;

//    //Convert to millimeters and round properly to above/below .5
//    distanceFromCurrentLinePivot = Math.Round(distanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);

//    //every guidance method dumps into these that are used and sent everywhere, last one wins
//    mf.guidanceLineDistanceOff = mf.distanceDisplaySteer = (Int16)distanceFromCurrentLinePivot;
//    mf.guidanceLineSteerAngle = (Int16)(steerAngleRP * 100);
//}
//private void StanleyRecPath(int ptCount)
//{
//    //find the closest 2 points to current fix
//    double minDistA = 9999999999;

//    //set the search range close to current position
//    int top = currentPositonIndex + 5;
//    if (top > ptCount) top = ptCount;

//    double dist;
//    for (int t = currentPositonIndex; t < top; t++)
//    {
//        dist = ((pivotAxlePosRP.easting - recList[t].easting) * (pivotAxlePosRP.easting - recList[t].easting))
//                        + ((pivotAxlePosRP.northing - recList[t].northing) * (pivotAxlePosRP.northing - recList[t].northing));
//        if (dist < minDistA)
//        {
//            minDistA = dist;
//            A = t;
//        }
//    }

//    //Save the closest point
//    C = A;

//    //next point is the next in list
//    B = A + 1;
//    if (B == ptCount)
//    {
//        //don't go past the end of the list - "end of the line" trigger
//        A--;
//        B--;
//        isEndOfTheRecLine = true;
//    }

//    //save current position
//    currentPositonIndex = A;

//    //get the distance from currently active AB line
//    double dx = recList[B].easting - recList[A].easting;
//    double dz = recList[B].northing - recList[A].northing;

//    if (Math.Abs(dx) < Double.Epsilon && Math.Abs(dz) < Double.Epsilon) return;

//    abHeading = Math.Atan2(dx, dz);
//    //abHeading = recList[A].heading;

//    //how far from current AB Line is fix
//    distanceFromCurrentLinePivot =
//        ((dz * pivotAxlePosRP.easting) - (dx * pivotAxlePosRP.northing) + (recList[B].easting
//                * recList[A].northing) - (recList[B].northing * recList[A].easting))
//                    / Math.Sqrt((dz * dz) + (dx * dx));

//    //are we on the right side or not
//    isOnRightSideCurrentLine = distanceFromCurrentLinePivot > 0;

//    // calc point on ABLine closest to current position
//    double U = (((pivotAxlePosRP.easting - recList[A].easting) * dx)
//                + ((pivotAxlePosRP.northing - recList[A].northing) * dz))
//                / ((dx * dx) + (dz * dz));

//    rEastRP = recList[A].easting + (U * dx);
//    rNorthRP = recList[A].northing + (U * dz);

//    //the first part of stanley is to extract heading error
//    double abFixHeadingDelta = (pivotAxlePosRP.heading - abHeading);

//    //Fix the circular error - get it from -Pi/2 to Pi/2
//    if (abFixHeadingDelta > Math.PI) abFixHeadingDelta -= Math.PI;
//    else if (abFixHeadingDelta < Math.PI) abFixHeadingDelta += Math.PI;
//    if (abFixHeadingDelta > glm.PIBy2) abFixHeadingDelta -= Math.PI;
//    else if (abFixHeadingDelta < -glm.PIBy2) abFixHeadingDelta += Math.PI;

//    //normally set to 1, less then unity gives less heading error.
//    abFixHeadingDelta *= mf.vehicle.stanleyHeadingErrorGain;
//    if (abFixHeadingDelta > 0.74) abFixHeadingDelta = 0.74;
//    if (abFixHeadingDelta < -0.74) abFixHeadingDelta = -0.74;

//    //the non linear distance error part of stanley
//    steerAngleRP = Math.Atan((distanceFromCurrentLinePivot * mf.vehicle.stanleyDistanceErrorGain) / ((mf.pn.speed * 0.277777) + 1));

//    //clamp it to max 42 degrees
//    if (steerAngleRP > 0.74) steerAngleRP = 0.74;
//    if (steerAngleRP < -0.74) steerAngleRP = -0.74;

//    //add them up and clamp to max in vehicle settings
//    steerAngleRP = glm.toDegrees((steerAngleRP + abFixHeadingDelta) * -1.0);
//    if (steerAngleRP < -mf.vehicle.maxSteerAngle) steerAngleRP = -mf.vehicle.maxSteerAngle;
//    if (steerAngleRP > mf.vehicle.maxSteerAngle) steerAngleRP = mf.vehicle.maxSteerAngle;

//    //Convert to millimeters and round properly to above/below .5
//    distanceFromCurrentLinePivot = Math.Round(distanceFromCurrentLinePivot * 1000.0, MidpointRounding.AwayFromZero);

//    //every guidance method dumps into these that are used and sent everywhere, last one wins
//    mf.guidanceLineDistanceOff = mf.distanceDisplaySteer = (Int16)distanceFromCurrentLinePivot;
//    mf.guidanceLineSteerAngle = (Int16)(steerAngleRP * 100);
//}