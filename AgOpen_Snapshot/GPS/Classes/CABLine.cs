using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Guidance;
using AgOpenGPS.Core.Services.Guidance;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for AB line guidance.
    /// Delegates Pure Pursuit calculations to Core PurePursuitGuidanceService.
    /// Stanley guidance already delegates to Core StanleyGuidanceService.
    /// </summary>
    public class CABLine
    {
        private static readonly PurePursuitGuidanceService _corePurePursuitService = new PurePursuitGuidanceService();

        public double abHeading, abLength;

        public bool isABValid;

        //the current AB guidance line
        public vec3 currentLinePtA = new vec3(0.0, 0.0, 0.0);
        public vec3 currentLinePtB = new vec3(0.0, 1.0, 0.0);

        public double distanceFromCurrentLinePivot;
        public double distanceFromRefLine;

        //pure pursuit values
        public vec2 goalPointAB = new vec2(0, 0);

        public int howManyPathsAway, lastHowManyPathsAway;
        public bool isMakingABLine;
        public bool isHeadingSameWay = true, lastIsHeadingSameWay;

        //public bool isOnTramLine;
        //public int tramBasedOn;
        public double ppRadiusAB;

        public vec2 radiusPointAB = new vec2(0, 0);
        public double rEastAB, rNorthAB;

        public double snapDistance, lastSecond = 0;
        public double steerAngleAB;
        public int lineWidth, numGuideLines;

        //design
        public vec2 desPtA = new vec2(0.2, 0.15);
        public vec2 desPtB = new vec2(0.3, 0.3);

        public vec2 desLineEndA = new vec2(0.0, 0.0);
        public vec2 desLineEndB = new vec2(999997, 1.0);

        public double desHeading = 0;

        public string desName = "";

        //autosteer errors
        public double pivotDistanceError, pivotDistanceErrorLast, pivotDerivative;

        //derivative counters
        private int counter2;

        public double inty;
        public double pivotErrorTotal;

        //Color tramColor = Color.YellowGreen;

        //pointers to mainform controls
        private readonly FormGPS mf;

        public CABLine(FormGPS _f)
        {
            //constructor
            mf = _f;
            //isOnTramLine = true;
            lineWidth = Properties.Settings.Default.setDisplay_lineWidth;
            abLength = 2000;
            numGuideLines = Properties.Settings.Default.setAS_numGuideLines;
        }

        public void BuildCurrentABLineList(vec3 pivot)
        {
            if (mf.trk.gArr.Count < mf.trk.idx || mf.trk.idx < 0) return;

            CTrk track = mf.trk.gArr[mf.trk.idx];

            if (!isABValid || ((mf.secondsSinceStart - lastSecond) > 0.66 && (!mf.isBtnAutoSteerOn || mf.mc.steerSwitchHigh)))
            {
                lastSecond = mf.secondsSinceStart;

                double dx, dy;

                abHeading = track.heading;

                track.endPtA.easting = track.ptA.easting - (Math.Sin(abHeading) * abLength);
                track.endPtA.northing = track.ptA.northing - (Math.Cos(abHeading) * abLength);

                track.endPtB.easting = track.ptB.easting + (Math.Sin(abHeading) * abLength);
                track.endPtB.northing = track.ptB.northing + (Math.Cos(abHeading) * abLength);

                //move the ABLine over based on the overlap amount set in
                double widthMinusOverlap = mf.tool.width - mf.tool.overlap;

                //x2-x1
                dx = track.endPtB.easting - track.endPtA.easting;
                //z2-z1
                dy = track.endPtB.northing - track.endPtA.northing;

                distanceFromRefLine = ((dy * mf.guidanceLookPos.easting) - (dx * mf.guidanceLookPos.northing) + (track.endPtB.easting
                                        * track.endPtA.northing) - (track.endPtB.northing * track.endPtA.easting))
                                            / Math.Sqrt((dy * dy) + (dx * dx));

                distanceFromRefLine -= (0.5 * widthMinusOverlap);

                isHeadingSameWay = Math.PI - Math.Abs(Math.Abs(pivot.heading - abHeading) - Math.PI) < glm.PIBy2;

                //if (mf.yt.isYouTurnTriggered && !mf.yt.isGoingStraightThrough) isHeadingSameWay = !isHeadingSameWay;

                //Which ABLine is the vehicle on, negative is left and positive is right side

                double RefDist = (distanceFromRefLine + (isHeadingSameWay ? mf.tool.offset : -mf.tool.offset) - track.nudgeDistance) / widthMinusOverlap;

                if (RefDist < 0) howManyPathsAway = (int)(RefDist - 0.5);
                else howManyPathsAway = (int)(RefDist + 0.5);
            }

            if (!isABValid || howManyPathsAway != lastHowManyPathsAway || (isHeadingSameWay != lastIsHeadingSameWay && mf.tool.offset != 0))
            {
                isABValid = true;
                lastHowManyPathsAway = howManyPathsAway;
                lastIsHeadingSameWay = isHeadingSameWay;

                double widthMinusOverlap = mf.tool.width - mf.tool.overlap;

                double distAway = widthMinusOverlap * howManyPathsAway + (isHeadingSameWay ? -mf.tool.offset : mf.tool.offset) + track.nudgeDistance;

                distAway += (0.5 * widthMinusOverlap);

                //move the curline as well. 
                vec2 nudgePtA = new vec2(track.ptA);
                vec2 nudgePtB = new vec2(track.ptB);

                //depending which way you are going, the offset can be either side
                vec2 point1 = new vec2((Math.Cos(-abHeading) * distAway) + nudgePtA.easting, (Math.Sin(-abHeading) * distAway) + nudgePtA.northing);

                vec2 point2 = new vec2((Math.Cos(-abHeading) * distAway) + nudgePtB.easting, (Math.Sin(-abHeading) * distAway) + nudgePtB.northing);

                //create the new line extent points for current ABLine based on original heading of AB line
                currentLinePtA.easting = point1.easting - (Math.Sin(abHeading) * abLength);
                currentLinePtA.northing = point1.northing - (Math.Cos(abHeading) * abLength);

                currentLinePtB.easting = point2.easting + (Math.Sin(abHeading) * abLength);
                currentLinePtB.northing = point2.northing + (Math.Cos(abHeading) * abLength);

                currentLinePtA.heading = abHeading;
                currentLinePtB.heading = abHeading;
            }
        }

        public void GetCurrentABLine(vec3 pivot, vec3 steer)
        {
            double dx, dy;

            //Check uturn first
            if (mf.yt.isYouTurnTriggered && mf.yt.DistanceFromYouTurnLine())//do the pure pursuit from youTurn
            {
                //now substitute what it thinks are AB line values with auto turn values
                steerAngleAB = mf.yt.steerAngleYT;
                distanceFromCurrentLinePivot = mf.yt.distanceFromCurrentLine;

                goalPointAB = mf.yt.goalPointYT;
                radiusPointAB.easting = mf.yt.radiusPointYT.easting;
                radiusPointAB.northing = mf.yt.radiusPointYT.northing;
                ppRadiusAB = mf.yt.ppRadiusYT;

                mf.vehicle.modeTimeCounter = 0;
                mf.vehicle.modeActualXTE = (distanceFromCurrentLinePivot);
            }

            //Stanley
            else if (mf.isStanleyUsed)
                mf.gyd.StanleyGuidanceABLine(currentLinePtA, currentLinePtB, pivot, steer);

            //Pure Pursuit - delegate to Core
            else
            {
                // Create input DTO from current state
                var input = new PurePursuitGuidanceInput
                {
                    PivotPosition = new Vec3(pivot.easting, pivot.northing, pivot.heading),
                    CurrentLinePtA = new Vec3(currentLinePtA.easting, currentLinePtA.northing, currentLinePtA.heading),
                    CurrentLinePtB = new Vec3(currentLinePtB.easting, currentLinePtB.northing, currentLinePtB.heading),
                    ABHeading = abHeading,
                    IsHeadingSameWay = isHeadingSameWay,
                    Wheelbase = mf.vehicle.VehicleConfig.Wheelbase,
                    MaxSteerAngle = mf.vehicle.maxSteerAngle,
                    PurePursuitIntegralGain = mf.vehicle.purePursuitIntegralGain,
                    GoalPointDistance = mf.vehicle.UpdateGoalPointDistance(),
                    SideHillCompFactor = mf.gyd.sideHillCompFactor,
                    FixHeading = mf.fixHeading,
                    AvgSpeed = mf.avgSpeed,
                    IsReverse = mf.isReverse,
                    IsAutoSteerOn = mf.isBtnAutoSteerOn,
                    ImuRoll = mf.ahrs.imuRoll,
                    PreviousIntegral = inty,
                    PreviousPivotDistanceError = pivotDistanceError,
                    PreviousPivotDistanceErrorLast = pivotDistanceErrorLast,
                    PreviousCounter = counter2
                };

                // Delegate to Core service
                var output = _corePurePursuitService.CalculateGuidanceABLine(input);

                // Unpack output DTO back to WinForms fields
                steerAngleAB = output.SteerAngle;
                distanceFromCurrentLinePivot = output.DistanceFromCurrentLinePivot;
                goalPointAB.easting = output.GoalPoint.Easting;
                goalPointAB.northing = output.GoalPoint.Northing;
                radiusPointAB.easting = output.RadiusPoint.Easting;
                radiusPointAB.northing = output.RadiusPoint.Northing;
                ppRadiusAB = output.PurePursuitRadius;
                rEastAB = output.REast;
                rNorthAB = output.RNorth;

                // Update state for next iteration
                inty = output.Integral;
                pivotDistanceError = output.PivotDistanceError;
                pivotDistanceErrorLast = output.PivotDistanceErrorLast;
                pivotDerivative = output.PivotDerivative;
                counter2 = output.Counter;

                // Mode tracking
                mf.vehicle.modeActualXTE = output.ModeActualXTE;
                mf.vehicle.modeActualHeadingError = output.ModeActualHeadingError;

                // Transmission values
                mf.guidanceLineDistanceOff = output.GuidanceLineDistanceOff;
                mf.guidanceLineSteerAngle = output.GuidanceLineSteerAngle;
            }

            //mf.setAngVel = 0.277777 * mf.avgSpeed * (Math.Tan(glm.toRadians(steerAngleAB))) / mf.vehicle.wheelbase;
            //mf.setAngVel = glm.toDegrees(mf.setAngVel);
        }

        public void DrawABLineNew()
        {
            //ABLine currently being designed
            GL.LineWidth(lineWidth);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(0.95f, 0.70f, 0.50f);
            GL.Vertex3(desLineEndA.easting, desLineEndA.northing, 0.0);
            GL.Vertex3(desLineEndB.easting, desLineEndB.northing, 0.0);
            GL.End();

            GL.Color3(0.2f, 0.950f, 0.20f);
            mf.font.DrawText3D(desPtA.easting, desPtA.northing, "&A", mf.camHeading);
            mf.font.DrawText3D(desPtB.easting, desPtB.northing, "&B", mf.camHeading);
        }

        public void DrawABLines()
        {
            //Draw AB Points
            GL.PointSize(8.0f);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0.0f, 0.90f, 0.95f);
            GL.Vertex3(mf.trk.gArr[mf.trk.idx].ptB.easting, mf.trk.gArr[mf.trk.idx].ptB.northing, 0.0);
            GL.Color3(0.95f, 0.0f, 0.0f);
            GL.Vertex3(mf.trk.gArr[mf.trk.idx].ptA.easting, mf.trk.gArr[mf.trk.idx].ptA.northing, 0.0);
            //GL.Color3(0.00990f, 0.990f, 0.095f);
            //GL.Vertex3(mf.bnd.iE, mf.bnd.iN, 0.0);
            GL.End();

            if (!isMakingABLine)
            {
                mf.font.DrawText3D(mf.trk.gArr[mf.trk.idx].ptA.easting, mf.trk.gArr[mf.trk.idx].ptA.northing, "&A", mf.camHeading);
                mf.font.DrawText3D(mf.trk.gArr[mf.trk.idx].ptB.easting, mf.trk.gArr[mf.trk.idx].ptB.northing, "&B", mf.camHeading);
            }

            GL.PointSize(1.0f);

            //Draw reference AB line
            GL.LineWidth(4);
            GL.Enable(EnableCap.LineStipple);
            GL.LineStipple(1, 0x0F00);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(0.930f, 0.2f, 0.2f);
            GL.Vertex3(mf.trk.gArr[mf.trk.idx].endPtA.easting, mf.trk.gArr[mf.trk.idx].endPtA.northing, 0);
            GL.Vertex3(mf.trk.gArr[mf.trk.idx].endPtB.easting, mf.trk.gArr[mf.trk.idx].endPtB.northing, 0);
            GL.End();
            GL.Disable(EnableCap.LineStipple);

            double widthMinusOverlap = mf.tool.width - mf.tool.overlap;
            double shadowOffset = isHeadingSameWay ? mf.tool.offset : -mf.tool.offset;
            double sinHR = Math.Sin(abHeading + glm.PIBy2) * (widthMinusOverlap * 0.5 + shadowOffset);
            double cosHR = Math.Cos(abHeading + glm.PIBy2) * (widthMinusOverlap * 0.5 + shadowOffset);
            double sinHL = Math.Sin(abHeading + glm.PIBy2) * (widthMinusOverlap * 0.5 - shadowOffset);
            double cosHL = Math.Cos(abHeading + glm.PIBy2) * (widthMinusOverlap * 0.5 - shadowOffset);

            //shadow
            GL.Color4(0.5, 0.5, 0.5, 0.2);
            GL.Begin(PrimitiveType.TriangleFan);
            {
                GL.Vertex3(currentLinePtA.easting - sinHL, currentLinePtA.northing - cosHL, 0);
                GL.Vertex3(currentLinePtA.easting + sinHR, currentLinePtA.northing + cosHR, 0);
                GL.Vertex3(currentLinePtB.easting + sinHR, currentLinePtB.northing + cosHR, 0);
                GL.Vertex3(currentLinePtB.easting - sinHL, currentLinePtB.northing - cosHR, 0);
            }
            GL.End();

            //shadow lines
            GL.Color4(0.55, 0.55, 0.55, 0.2);
            GL.LineWidth(1);
            GL.Begin(PrimitiveType.LineLoop);
            {
                GL.Vertex3(currentLinePtA.easting - sinHL, currentLinePtA.northing - cosHL, 0);
                GL.Vertex3(currentLinePtA.easting + sinHR, currentLinePtA.northing + cosHR, 0);
                GL.Vertex3(currentLinePtB.easting + sinHR, currentLinePtB.northing + cosHR, 0);
                GL.Vertex3(currentLinePtB.easting - sinHL, currentLinePtB.northing - cosHR, 0);
            }
            GL.End();

            //draw current AB Line
            GL.LineWidth(lineWidth * 3);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(0, 0, 0);
            GL.Vertex3(currentLinePtA.easting, currentLinePtA.northing, 0.0);
            GL.Vertex3(currentLinePtB.easting, currentLinePtB.northing, 0.0);
            GL.End();

            //draw current AB Line
            GL.LineWidth(lineWidth);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(0.95f, 0.20f, 0.950f);
            GL.Vertex3(currentLinePtA.easting, currentLinePtA.northing, 0.0);
            GL.Vertex3(currentLinePtB.easting, currentLinePtB.northing, 0.0);
            GL.End();

            if (mf.isSideGuideLines && mf.camera.camSetDistance > mf.tool.width * -400)
            {
                //get the tool offset and width
                double toolOffset = mf.tool.offset * 2;
                double toolWidth = mf.tool.width - mf.tool.overlap;
                double cosHeading = Math.Cos(-abHeading);
                double sinHeading = Math.Sin(-abHeading);

                GL.Color4(0, 0, 0, 0.5);

                GL.LineWidth(lineWidth * 3);

                GL.Begin(PrimitiveType.Lines);

                //if (toolOffset == 0)
                {
                    for (int i = 1; i <= numGuideLines; i++)
                    {
                        GL.Vertex3((cosHeading * (toolWidth * i)) + currentLinePtA.easting, (sinHeading * (toolWidth * i)) + currentLinePtA.northing, 0);
                        GL.Vertex3((cosHeading * (toolWidth * i)) + currentLinePtB.easting, (sinHeading * (toolWidth * i)) + currentLinePtB.northing, 0);

                        GL.Vertex3((cosHeading * (-toolWidth * i)) + currentLinePtA.easting, (sinHeading * (-toolWidth * i)) + currentLinePtA.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth * i)) + currentLinePtB.easting, (sinHeading * (-toolWidth * i)) + currentLinePtB.northing, 0);
                    }
                    GL.End();
                    //GL.Enable(EnableCap.LineStipple);
                    //GL.LineStipple(1, 0x000F);

                    GL.Color4(0.19907f, 0.6f, 0.19750f, 0.6f);
                    GL.LineWidth(lineWidth);
                    GL.Begin(PrimitiveType.Lines);

                    for (int i = 1; i <= numGuideLines; i++)
                    {
                        GL.Vertex3((cosHeading * (toolWidth * i)) + currentLinePtA.easting, (sinHeading * (toolWidth * i)) + currentLinePtA.northing, 0);
                        GL.Vertex3((cosHeading * (toolWidth * i)) + currentLinePtB.easting, (sinHeading * (toolWidth * i)) + currentLinePtB.northing, 0);

                        GL.Vertex3((cosHeading * (-toolWidth * i)) + currentLinePtA.easting, (sinHeading * (-toolWidth * i)) + currentLinePtA.northing, 0);
                        GL.Vertex3((cosHeading * (-toolWidth * i)) + currentLinePtB.easting, (sinHeading * (-toolWidth * i)) + currentLinePtB.northing, 0);
                    }
                    GL.End();


                }
                //else
                //{
                //    if (isHeadingSameWay)
                //    {
                //        GL.Vertex3((cosHeading * (toolWidth + toolOffset)) + currentLinePtA.easting, (sinHeading * (toolWidth + toolOffset)) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * (toolWidth + toolOffset)) + currentLinePtB.easting, (sinHeading * (toolWidth + toolOffset)) + currentLinePtB.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth + toolOffset)) + currentLinePtA.easting, (sinHeading * (-toolWidth + toolOffset)) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth + toolOffset)) + currentLinePtB.easting, (sinHeading * (-toolWidth + toolOffset)) + currentLinePtB.northing, 0);

                //        toolWidth *= 2;
                //        GL.Vertex3((cosHeading * toolWidth) + currentLinePtA.easting, (sinHeading * toolWidth) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * toolWidth) + currentLinePtB.easting, (sinHeading * toolWidth) + currentLinePtB.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth)) + currentLinePtA.easting, (sinHeading * (-toolWidth)) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth)) + currentLinePtB.easting, (sinHeading * (-toolWidth)) + currentLinePtB.northing, 0);
                //    }
                //    else
                //    {
                //        GL.Vertex3((cosHeading * (toolWidth - toolOffset)) + currentLinePtA.easting, (sinHeading * (toolWidth - toolOffset)) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * (toolWidth - toolOffset)) + currentLinePtB.easting, (sinHeading * (toolWidth - toolOffset)) + currentLinePtB.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth - toolOffset)) + currentLinePtA.easting, (sinHeading * (-toolWidth - toolOffset)) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth - toolOffset)) + currentLinePtB.easting, (sinHeading * (-toolWidth - toolOffset)) + currentLinePtB.northing, 0);

                //        toolWidth *= 2;
                //        GL.Vertex3((cosHeading * toolWidth) + currentLinePtA.easting, (sinHeading * toolWidth) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * toolWidth) + currentLinePtB.easting, (sinHeading * toolWidth) + currentLinePtB.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth)) + currentLinePtA.easting, (sinHeading * (-toolWidth)) + currentLinePtA.northing, 0);
                //        GL.Vertex3((cosHeading * (-toolWidth)) + currentLinePtB.easting, (sinHeading * (-toolWidth)) + currentLinePtB.northing, 0);
                //    }
                //    GL.End();

                //}
                GL.Disable(EnableCap.LineStipple);
            }

            if (!mf.isStanleyUsed && mf.camera.camSetDistance > -200)
            {
                ////Draw lookahead Point
                //GL.PointSize(16.0f);
                //GL.Begin(PrimitiveType.Points);
                //GL.Color3(1.0f, 1.0f, 0.0f);
                //GL.Vertex3(goalPointAB.easting, goalPointAB.northing, 0.0);
                ////GL.Vertex3(mf.gyd.rEastSteer, mf.gyd.rNorthSteer, 0.0);
                ////GL.Vertex3(mf.gyd.rEastPivot, mf.gyd.rNorthPivot, 0.0);
                //GL.End();
                //GL.PointSize(1.0f);

                //if (ppRadiusAB < 50 && ppRadiusAB > -50)
                //{
                //    const int numSegments = 200;
                //    double theta = glm.twoPI / numSegments;
                //    double c = Math.Cos(theta);//precalculate the sine and cosine
                //    double s = Math.Sin(theta);
                //    //double x = ppRadiusAB;//we start at angle = 0
                //    double x = 0;//we start at angle = 0
                //    double y = 0;

                //    GL.LineWidth(2);
                //    GL.Color3(0.53f, 0.530f, 0.950f);
                //    GL.Begin(PrimitiveType.Lines);
                //    for (int ii = 0; ii < numSegments - 15; ii++)
                //    {
                //        //glVertex2f(x + cx, y + cy);//output vertex
                //        GL.Vertex3(x + radiusPointAB.easting, y + radiusPointAB.northing, 0);//output vertex
                //        double t = x;//apply the rotation matrix
                //        x = (c * x) - (s * y);
                //        y = (s * t) + (c * y);
                //    }
                //    GL.End();
                //}
            }

            mf.yt.DrawYouTurn();

            GL.PointSize(1.0f);
            GL.LineWidth(1);
        }

        public void BuildTram()
        {
            if (mf.tram.generateMode != 1)
            {
                mf.tram.BuildTramBnd();
            }
            else
            {
                mf.tram.tramBndOuterArr?.Clear();
                mf.tram.tramBndInnerArr?.Clear();
            }

            mf.tram.tramList?.Clear();
            mf.tram.tramArr?.Clear();

            if (mf.tram.generateMode == 2) return;

            List<vec2> tramRef = new List<vec2>();

            bool isBndExist = mf.bnd.bndList.Count != 0;

            abHeading = mf.trk.gArr[mf.trk.idx].heading;

            double hsin = Math.Sin(abHeading);
            double hcos = Math.Cos(abHeading);

            double len = glm.Distance(mf.trk.gArr[mf.trk.idx].endPtA, mf.trk.gArr[mf.trk.idx].endPtB);
            //divide up the AB line into segments
            vec2 P1 = new vec2();
            for (int i = 0; i < (int)len; i += 4)
            {
                P1.easting = (hsin * i) + mf.trk.gArr[mf.trk.idx].endPtA.easting;
                P1.northing = (hcos * i) + mf.trk.gArr[mf.trk.idx].endPtA.northing;
                tramRef.Add(P1);
            }

            //create list of list of points of triangle strip of AB Highlight
            double headingCalc = abHeading + glm.PIBy2;

            hsin = Math.Sin(headingCalc);
            hcos = Math.Cos(headingCalc);

            mf.tram.tramList?.Clear();
            mf.tram.tramArr?.Clear();

            //no boundary starts on first pass
            int cntr = 0;
            if (isBndExist)
            {
                if (mf.tram.generateMode == 1)
                    cntr = 0;
                else
                    cntr = 1;
            }

            double widd;
            for (int i = cntr; i < mf.tram.passes; i++)
            {
                mf.tram.tramArr = new List<vec2>
                {
                    Capacity = 128
                };

                mf.tram.tramList.Add(mf.tram.tramArr);

                widd = (mf.tram.tramWidth * 0.5) - mf.tram.halfWheelTrack;
                widd += (mf.tram.tramWidth * i);

                for (int j = 0; j < tramRef.Count; j++)
                {
                    P1.easting = hsin * widd + tramRef[j].easting;
                    P1.northing = (hcos * widd) + tramRef[j].northing;

                    if (!isBndExist || mf.bnd.bndList[0].fenceLineEar.IsPointInPolygon(P1))
                    {
                        mf.tram.tramArr.Add(P1);
                    }
                }
            }

            for (int i = cntr; i < mf.tram.passes; i++)
            {
                mf.tram.tramArr = new List<vec2>
                {
                    Capacity = 128
                };

                mf.tram.tramList.Add(mf.tram.tramArr);

                widd = (mf.tram.tramWidth * 0.5) + mf.tram.halfWheelTrack;
                widd += (mf.tram.tramWidth * i);

                for (int j = 0; j < tramRef.Count; j++)
                {
                    P1.easting = (hsin * widd) + tramRef[j].easting;
                    P1.northing = (hcos * widd) + tramRef[j].northing;

                    if (!isBndExist || mf.bnd.bndList[0].fenceLineEar.IsPointInPolygon(P1))
                    {
                        mf.tram.tramArr.Add(P1);
                    }
                }
            }

            tramRef?.Clear();
            //outside tram

            if (mf.bnd.bndList.Count == 0 || mf.tram.passes != 0)
            {
                //return;
            }
        }
    }
}