using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.YouTurn;
using AgValoniaGPS.Services.PathPlanning;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgValoniaGPS.Services.YouTurn
{
    /// <summary>
    /// Service for creating U-turn paths.
    /// Generates turn geometry based on guidance lines, boundaries, and vehicle configuration.
    /// </summary>
    public class YouTurnCreationService
    {
        private const double TWO_PI = Math.PI * 2.0;
        private const double PI_BY_2 = Math.PI / 2.0;

        // Reusable service for Dubins path generation (radius updated per turn)
        private readonly DubinsPathService _dubinsService = new DubinsPathService(1.0);
        private readonly ILogger<YouTurnCreationService> _logger;

        public YouTurnCreationService(ILogger<YouTurnCreationService> logger)
        {
            _logger = logger;
        }

        // Current input being processed (for helper methods)
        private YouTurnCreationInput _currentInput;

        // Working variables during turn creation
        private List<Vec3> ytList = new List<Vec3>();
        private List<Vec3> ytList2 = new List<Vec3>();
        private List<Vec3> nextCurve = new List<Vec3>();
        private TurnClosePoint closestTurnPt = new TurnClosePoint();
        private TurnClosePoint inClosestTurnPt = new TurnClosePoint();
        private TurnClosePoint outClosestTurnPt = new TurnClosePoint();
        private TurnClosePoint startOfTurnPt = new TurnClosePoint();
        private List<TurnClosePoint> turnClosestList = new List<TurnClosePoint>();

        private double pointSpacing;
        private double iE, iN; // Line intersection results
        private int semiCircleIndex = -1;
        private bool isOutOfBounds = false;
        private bool isOutSameCurve = false;
        private int youTurnPhase = 0;

        /// <summary>
        /// Create a U-turn path.
        /// </summary>
        public YouTurnCreationOutput CreateTurn(YouTurnCreationInput input)
        {
            var output = new YouTurnCreationOutput();

            // Store input for helper methods
            _currentInput = input;

            // Validate input
            if (input.MakeUTurnCounter < 4)
            {
                output.Success = false;
                output.FailureReason = "Turn creation throttled - wait 1.5 seconds between turns";
                youTurnPhase = 0;
                return output;
            }

            // Initialize working state
            ytList.Clear();
            ytList2.Clear();
            nextCurve.Clear();
            turnClosestList.Clear();
            youTurnPhase = 0;
            isOutOfBounds = false;
            isOutSameCurve = false;

            // Use pre-calculated TurnOffset if provided, otherwise calculate from RowSkipsWidth
            double turnOffset;
            if (input.TurnOffset > 0)
            {
                // Use the pre-calculated offset (matches cyan next-track line exactly)
                turnOffset = input.TurnOffset + (input.IsTurnLeft ? -input.ToolOffset * 2.0 : input.ToolOffset * 2.0);
                _logger.LogDebug("Using pre-calculated TurnOffset: {InputOffset}m -> turnOffset={TurnOffset}m", input.TurnOffset, turnOffset);
            }
            else
            {
                // Fallback: calculate from RowSkipsWidth (skip=0 means 1 width, skip=1 means 2 widths, etc.)
                turnOffset = (input.ToolWidth - input.ToolOverlap) * (input.RowSkipsWidth + 1)
                    + (input.IsTurnLeft ? -input.ToolOffset * 2.0 : input.ToolOffset * 2.0);
                _logger.LogDebug("Fallback: TurnOffset was {InputOffset}m, calculated turnOffset={TurnOffset}m from RowSkipsWidth={RowSkipsWidth}", input.TurnOffset, turnOffset, input.RowSkipsWidth);
            }
            pointSpacing = input.TurnRadius * 0.1;

            bool success = false;

            // Route to appropriate turn creation method based on type and geometry
            if (input.GuidanceType == GuidanceLineType.Curve)
            {
                success = CreateCurveTurn(input, turnOffset);
            }
            else // AB Line
            {
                success = CreateABTurn(input, turnOffset);
            }

            // Package output
            if (success && ytList.Count > 0)
            {
                output.Success = true;
                output.TurnPath = new List<Vec3>(ytList);
                output.IsOutSameCurve = isOutSameCurve;
                output.IsGoingStraightThrough = IsGoingStraightThrough();
                output.IsOutOfBounds = isOutOfBounds;
                output.DistancePivotToTurnLine = Distance(ytList[0], input.PivotPosition);
                output.InClosestTurnPoint = inClosestTurnPt.ClosePt;
                output.OutClosestTurnPoint = outClosestTurnPt.ClosePt;
            }
            else
            {
                output.Success = false;
                output.FailureReason = "Turn creation failed - path invalid or out of bounds";
                output.IsOutOfBounds = true;
            }

            return output;
        }

        #region Curve Turn Creation

        private bool CreateCurveTurn(YouTurnCreationInput input, double turnOffset)
        {
            if (input.TurnType == YouTurnType.AlbinStyle)
            {
                // Omega (narrow) or Wide turn based on offset
                if (turnOffset > (input.TurnRadius * 2.0))
                {
                    return CreateCurveWideTurn(input, turnOffset);
                }
                else
                {
                    return CreateCurveOmegaTurn(input, turnOffset);
                }
            }
            else // KStyle
            {
                return CreateKStyleTurnCurve(input, turnOffset);
            }
        }

        private bool CreateCurveOmegaTurn(YouTurnCreationInput input, double turnOffset)
        {
            // Keep from making turns constantly - wait 1.5 seconds
            if (input.MakeUTurnCounter < 4)
            {
                youTurnPhase = 0;
                return true;
            }

            // Check for valid track mode
            if (input.TrackMode == 64 || input.TrackMode == 32) // waterPivot or bndCurve
            {
                youTurnPhase = 11; // Ignore
                return false;
            }

            switch (youTurnPhase)
            {
                case 0: // Find the crossing points
                    if (!FindCurveTurnPoint(input, false))
                    {
                        FailCreate();
                        return false;
                    }

                    // Save a copy
                    inClosestTurnPt = new TurnClosePoint(closestTurnPt);
                    ytList?.Clear();

                    int count = input.IsHeadingSameWay ? -1 : 1;
                    int curveIndex = inClosestTurnPt.CurveIndex;

                    isOutOfBounds = true;
                    int stopIfWayOut = 0;
                    double head = 0;

                    while (isOutOfBounds)
                    {
                        stopIfWayOut++;
                        isOutOfBounds = false;

                        // Creates half a circle starting at the crossing point
                        ytList.Clear();
                        curveIndex += count;

                        Vec3 currentPos = input.GuidancePoints[curveIndex];

                        if (!input.IsHeadingSameWay) currentPos.Heading += Math.PI;
                        if (currentPos.Heading >= TWO_PI) currentPos.Heading -= TWO_PI;
                        head = currentPos.Heading;

                        _dubinsService.TurningRadius = input.TurnRadius;

                        // Now we go the other way to turn round
                        double invertHead = currentPos.Heading - Math.PI;
                        if (invertHead <= -Math.PI) invertHead += TWO_PI;
                        if (invertHead >= Math.PI) invertHead -= TWO_PI;

                        Vec3 goal = new Vec3();

                        // Neat trick to not have to add pi/2
                        if (input.IsTurnLeft)
                        {
                            goal.Easting = input.GuidancePoints[curveIndex - count].Easting + (Math.Cos(-invertHead) * turnOffset);
                            goal.Northing = input.GuidancePoints[curveIndex - count].Northing + (Math.Sin(-invertHead) * turnOffset);
                        }
                        else
                        {
                            goal.Easting = input.GuidancePoints[curveIndex - count].Easting - (Math.Cos(-invertHead) * turnOffset);
                            goal.Northing = input.GuidancePoints[curveIndex - count].Northing - (Math.Sin(-invertHead) * turnOffset);
                        }

                        goal.Heading = invertHead;

                        // Generate the turn points
                        ytList = _dubinsService.GeneratePath(currentPos, goal);
                        if (ytList.Count == 0)
                        {
                            FailCreate();
                            return false;
                        }

                        if (stopIfWayOut == 300 || curveIndex < 1 || curveIndex > (input.GuidancePoints.Count - 2))
                        {
                            // For some reason it doesn't go inside boundary
                            FailCreate();
                            return false;
                        }

                        for (int i = 0; i < ytList.Count; i++)
                        {
                            if (input.IsPointInsideTurnArea(ytList[i]) != 0)
                            {
                                isOutOfBounds = true;
                                break;
                            }
                        }
                    }
                    inClosestTurnPt.CurveIndex = curveIndex;

                    // Too many points from Dubins - so cut
                    double distance;
                    int cnt = ytList.Count;
                    for (int i = 1; i < cnt - 2; i++)
                    {
                        distance = DistanceSquared(ytList[i], ytList[i + 1]);
                        if (distance < pointSpacing)
                        {
                            ytList.RemoveAt(i + 1);
                            i--;
                            cnt = ytList.Count;
                        }
                    }

                    // Move the turn to exact at the turnline
                    ytList = MoveTurnInsideTurnLine(input, ytList, head, false, false);
                    if (ytList.Count == 0)
                    {
                        FailCreate();
                        return false;
                    }

                    youTurnPhase = 1;
                    break;

                case 1:
                    // Build the next line to add sequencelines
                    double widthMinusOverlap = input.ToolWidth - input.ToolOverlap;

                    double distAway = widthMinusOverlap * (input.HowManyPathsAway +
                        ((input.IsTurnLeft ^ input.IsHeadingSameWay) ? input.RowSkipsWidth : -input.RowSkipsWidth))
                        + (input.IsHeadingSameWay ? input.ToolOffset : -input.ToolOffset) + input.NudgeDistance;

                    distAway += (0.5 * widthMinusOverlap);

                    // Create the next line
                    nextCurve = BuildNewOffsetCurveList(input, distAway);

                    // Get the index of the last yt point
                    double dis = double.MaxValue;
                    if (nextCurve.Count > 1)
                    {
                        for (int i = 1; i < nextCurve.Count; i++)
                        {
                            double newdis = Distance(nextCurve[i], ytList[ytList.Count - 1]);
                            if (newdis < dis)
                            {
                                dis = newdis;
                                if (input.IsHeadingSameWay) outClosestTurnPt.CurveIndex = i - 1;
                                else outClosestTurnPt.CurveIndex = i;
                            }
                        }

                        if (outClosestTurnPt.CurveIndex >= 0)
                        {
                            var outPt = outClosestTurnPt;
                            outPt.ClosePt = nextCurve[outClosestTurnPt.CurveIndex];
                            outClosestTurnPt = outPt;

                            var inPt = inClosestTurnPt;
                            inPt.ClosePt = input.GuidancePoints[inClosestTurnPt.CurveIndex];
                            inClosestTurnPt = inPt;

                            if (!AddCurveSequenceLines(input)) return false;
                        }
                    }

                    // Fill in the gaps
                    double distanc;
                    int cnt4 = ytList.Count;
                    for (int i = 1; i < cnt4 - 2; i++)
                    {
                        int j = i + 1;
                        if (j == cnt4 - 1) continue;
                        distanc = DistanceSquared(ytList[i], ytList[j]);
                        if (distanc > 1)
                        {
                            Vec3 pointB = new Vec3((ytList[i].Easting + ytList[j].Easting) / 2.0,
                                (ytList[i].Northing + ytList[j].Northing) / 2.0, ytList[i].Heading);

                            ytList.Insert(j, pointB);
                            cnt4 = ytList.Count;
                            i--;
                        }
                    }

                    // Calculate the new points headings based on fore and aft of point - smoother turns
                    cnt4 = ytList.Count;
                    Vec3[] arr = new Vec3[cnt4];
                    cnt4 -= 2;
                    ytList.CopyTo(arr);
                    ytList.Clear();

                    for (int i = 2; i < cnt4; i++)
                    {
                        Vec3 pt3 = arr[i];
                        pt3.Heading = Math.Atan2(arr[i + 1].Easting - arr[i - 1].Easting,
                            arr[i + 1].Northing - arr[i - 1].Northing);
                        if (pt3.Heading < 0) pt3.Heading += TWO_PI;
                        ytList.Add(pt3);
                    }

                    // Check too close
                    if (Distance(ytList[0], input.PivotPosition) < 3)
                    {
                        FailCreate();
                        return false;
                    }

                    isOutOfBounds = false;
                    youTurnPhase = 10;
                    return true;
            }
            return true;
        }

        private bool CreateCurveWideTurn(YouTurnCreationInput input, double turnOffset)
        {
            // Keep from making turns constantly
            if (input.MakeUTurnCounter < 4)
            {
                youTurnPhase = 0;
                return true;
            }

            // Check for valid track mode
            if (input.TrackMode == 64 || input.TrackMode == 32) // waterPivot or bndCurve
            {
                youTurnPhase = 11; // Ignore
                return false;
            }

            double head = 0;
            int count = input.IsHeadingSameWay ? -1 : 1;

            switch (youTurnPhase)
            {
                case 0:
                    // Create first semicircle
                    if (!FindCurveTurnPoint(input, false))
                    {
                        if (input.TrackMode == 32 || input.TrackMode == 64) // waterPivot or bndCurve
                            youTurnPhase = 11; // Ignore
                        else
                            FailCreate();
                        return false;
                    }

                    inClosestTurnPt = new TurnClosePoint(closestTurnPt);
                    startOfTurnPt = new TurnClosePoint(inClosestTurnPt);

                    int stopIfWayOut = 0;
                    isOutOfBounds = true;

                    while (isOutOfBounds)
                    {
                        isOutOfBounds = false;
                        stopIfWayOut++;

                        Vec3 currentPos = input.GuidancePoints[inClosestTurnPt.CurveIndex];

                        head = currentPos.Heading;
                        if (!input.IsHeadingSameWay) head += Math.PI;
                        if (head > TWO_PI) head -= TWO_PI;
                        currentPos.Heading = head;

                        // Creates half a circle starting at the crossing point
                        ytList.Clear();
                        ytList.Add(currentPos);

                        // Taken from Dubins
                        while (Math.Abs(head - currentPos.Heading) < Math.PI)
                        {
                            // Update the position
                            currentPos.Easting += pointSpacing * Math.Sin(currentPos.Heading);
                            currentPos.Northing += pointSpacing * Math.Cos(currentPos.Heading);

                            // Which way are we turning?
                            double turnParameter = input.IsTurnLeft ? -1.0 : 1.0;

                            // Update the heading
                            currentPos.Heading += (pointSpacing / input.TurnRadius) * turnParameter;

                            // Add the new coordinate
                            ytList.Add(currentPos);
                        }

                        int cnt4 = ytList.Count;
                        if (cnt4 == 0)
                        {
                            FailCreate();
                            return false;
                        }

                        // Are we out of bounds?
                        for (int j = 0; j < cnt4; j += 2)
                        {
                            if (input.IsPointInsideTurnArea(ytList[j]) != 0)
                            {
                                isOutOfBounds = true;
                                break;
                            }
                        }

                        // First check if not out of bounds
                        if (!isOutOfBounds)
                        {
                            ytList = MoveTurnInsideTurnLine(input, ytList, head, true, false);
                            if (ytList.Count == 0)
                            {
                                FailCreate();
                                return false;
                            }
                            youTurnPhase = 1;
                            return true;
                        }

                        if (stopIfWayOut == 300 || inClosestTurnPt.CurveIndex < 1 || inClosestTurnPt.CurveIndex > (input.GuidancePoints.Count - 2))
                        {
                            FailCreate();
                            return false;
                        }

                        // Keep moving infield till pattern is all inside
                        inClosestTurnPt.CurveIndex = inClosestTurnPt.CurveIndex + count;
                        var closePt = inClosestTurnPt.ClosePt;
                        closePt = input.GuidancePoints[inClosestTurnPt.CurveIndex];
                        inClosestTurnPt.ClosePt = closePt;

                        // Set the flag to Critical stop machine
                        if (Distance(ytList[0], input.PivotPosition) < 3)
                        {
                            FailCreate();
                            return false;
                        }
                    }

                    return false;

                case 1:
                    // Build the next line
                    double widthMinusOverlap = input.ToolWidth - input.ToolOverlap;
                    double distAway = widthMinusOverlap * (input.HowManyPathsAway + ((input.IsTurnLeft ^ input.IsHeadingSameWay) ? input.RowSkipsWidth : -input.RowSkipsWidth))
                        + (input.IsHeadingSameWay ? input.ToolOffset : -input.ToolOffset) + input.NudgeDistance;
                    distAway += (0.5 * widthMinusOverlap);

                    nextCurve = BuildNewOffsetCurveList(input, distAway);

                    // Going with or against boundary?
                    bool isTurnLineSameWay = true;
                    double headingDifference = Math.Abs(inClosestTurnPt.TurnLineHeading - ytList[ytList.Count - 1].Heading);
                    if (headingDifference > PI_BY_2 && headingDifference < 3 * PI_BY_2) isTurnLineSameWay = false;

                    if (!FindCurveOutTurnPoint(nextCurve, startOfTurnPt, isTurnLineSameWay))
                    {
                        FailCreate();
                        return false;
                    }
                    outClosestTurnPt = new TurnClosePoint(closestTurnPt);

                    // Move the turn inside of turnline
                    isOutOfBounds = true;
                    while (isOutOfBounds)
                    {
                        isOutOfBounds = false;
                        Vec3 currentPos = nextCurve[outClosestTurnPt.CurveIndex];

                        head = currentPos.Heading;
                        if ((!input.IsHeadingSameWay && !isOutSameCurve) || (input.IsHeadingSameWay && isOutSameCurve)) head += Math.PI;
                        if (head > TWO_PI) head -= TWO_PI;
                        currentPos.Heading = head;

                        ytList2.Clear();
                        ytList2.Add(currentPos);

                        while (Math.Abs(head - currentPos.Heading) < Math.PI)
                        {
                            currentPos.Easting += pointSpacing * Math.Sin(currentPos.Heading);
                            currentPos.Northing += pointSpacing * Math.Cos(currentPos.Heading);
                            double turnParameter = input.IsTurnLeft ? 1.0 : -1.0;
                            currentPos.Heading += (pointSpacing / input.TurnRadius) * turnParameter;
                            ytList2.Add(currentPos);
                        }

                        int cnt3 = ytList2.Count;
                        if (cnt3 == 0)
                        {
                            FailCreate();
                            return false;
                        }

                        for (int j = 0; j < cnt3; j += 2)
                        {
                            if (input.IsPointInsideTurnArea(ytList2[j]) != 0)
                            {
                                isOutOfBounds = true;
                                break;
                            }
                        }

                        if (!isOutOfBounds)
                        {
                            ytList2 = MoveTurnInsideTurnLine(input, ytList2, head, true, true);
                            if (ytList2.Count == 0)
                            {
                                FailCreate();
                                return false;
                            }
                            youTurnPhase = 2;
                            return true;
                        }

                        if (outClosestTurnPt.CurveIndex < 1 || outClosestTurnPt.CurveIndex > (nextCurve.Count - 2))
                        {
                            FailCreate();
                            return false;
                        }

                        if (!isOutSameCurve) outClosestTurnPt.CurveIndex = outClosestTurnPt.CurveIndex + count;
                        else outClosestTurnPt.CurveIndex = outClosestTurnPt.CurveIndex - count;

                        var outPt = outClosestTurnPt.ClosePt;
                        outPt = nextCurve[outClosestTurnPt.CurveIndex];
                        outClosestTurnPt.ClosePt = outPt;
                    }
                    return false;

                case 2:
                    // Bind the two turns together
                    int cnt1 = ytList.Count;
                    int cnt2 = ytList2.Count;

                    bool isFirstTurnLineSameWay = true;
                    double firstHeadingDifference = Math.Abs(inClosestTurnPt.TurnLineHeading - ytList[ytList.Count - 1].Heading);
                    if (firstHeadingDifference > PI_BY_2 && firstHeadingDifference < 3 * PI_BY_2) isFirstTurnLineSameWay = false;

                    FindInnerTurnPoints(ytList[cnt1 - 1], ytList[0].Heading, inClosestTurnPt, isFirstTurnLineSameWay);
                    TurnClosePoint startClosestTurnPt = new TurnClosePoint(closestTurnPt);

                    FindInnerTurnPoints(ytList2[cnt2 - 1], ytList2[0].Heading + Math.PI, outClosestTurnPt, !isFirstTurnLineSameWay);
                    TurnClosePoint goalClosestTurnPt = new TurnClosePoint(closestTurnPt);

                    if (startClosestTurnPt.TurnLineNum != goalClosestTurnPt.TurnLineNum)
                    {
                        FailCreate();
                        return false;
                    }

                    if (startClosestTurnPt.TurnLineIndex == goalClosestTurnPt.TurnLineIndex)
                    {
                        for (int a = 0; a < cnt2; cnt2--)
                        {
                            ytList.Add(ytList2[cnt2 - 1]);
                        }
                    }
                    else
                    {
                        Vec3 tPoint = new Vec3();
                        int turnCount = input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points.Count;
                        int loops = Math.Abs(startClosestTurnPt.TurnLineIndex - goalClosestTurnPt.TurnLineIndex);

                        if (loops > (input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points.Count / 2))
                        {
                            if (startClosestTurnPt.TurnLineIndex < goalClosestTurnPt.TurnLineIndex)
                                loops = (turnCount - goalClosestTurnPt.TurnLineIndex) + startClosestTurnPt.TurnLineIndex;
                            else
                                loops = (turnCount - startClosestTurnPt.TurnLineIndex) + goalClosestTurnPt.TurnLineIndex;
                        }

                        if (isFirstTurnLineSameWay)
                        {
                            for (int i = 0; i < loops; i++)
                            {
                                if ((startClosestTurnPt.TurnLineIndex + 1) >= turnCount) startClosestTurnPt.TurnLineIndex = -1;
                                tPoint = input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points[startClosestTurnPt.TurnLineIndex + 1];
                                startClosestTurnPt.TurnLineIndex++;
                                if (startClosestTurnPt.TurnLineIndex >= turnCount)
                                    startClosestTurnPt.TurnLineIndex = 0;
                                ytList.Add(tPoint);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < loops; i++)
                            {
                                tPoint = input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points[startClosestTurnPt.TurnLineIndex];
                                startClosestTurnPt.TurnLineIndex--;
                                if (startClosestTurnPt.TurnLineIndex == -1)
                                    startClosestTurnPt.TurnLineIndex = turnCount - 1;
                                ytList.Add(tPoint);
                            }
                        }

                        for (int a = 0; a < cnt2; cnt2--)
                        {
                            ytList.Add(ytList2[cnt2 - 1]);
                        }
                    }

                    if (!AddCurveSequenceLines(input)) return false;

                    double distance;
                    int cnt = ytList.Count;
                    for (int i = 1; i < cnt - 2; i++)
                    {
                        int j = i + 1;
                        if (j == cnt - 1) continue;
                        distance = DistanceSquared(ytList[i], ytList[j]);
                        if (distance > 1)
                        {
                            Vec3 pointB = new Vec3((ytList[i].Easting + ytList[j].Easting) / 2.0,
                                (ytList[i].Northing + ytList[j].Northing) / 2.0, ytList[i].Heading);
                            ytList.Insert(j, pointB);
                            cnt = ytList.Count;
                            i--;
                        }
                    }

                    cnt = ytList.Count;
                    Vec3[] arr = new Vec3[cnt];
                    cnt -= 2;
                    ytList.CopyTo(arr);
                    ytList.Clear();

                    for (int i = 2; i < cnt; i++)
                    {
                        Vec3 pt3 = arr[i];
                        pt3.Heading = Math.Atan2(arr[i + 1].Easting - arr[i - 1].Easting,
                            arr[i + 1].Northing - arr[i - 1].Northing);
                        if (pt3.Heading < 0) pt3.Heading += TWO_PI;
                        ytList.Add(pt3);
                    }

                    if (Distance(ytList[0], input.PivotPosition) < 3)
                    {
                        FailCreate();
                        return false;
                    }

                    isOutOfBounds = false;
                    youTurnPhase = 10;
                    ytList2.Clear();
                    return true;
            }

            return true;
        }

        private bool CreateKStyleTurnCurve(YouTurnCreationInput input, double turnOffset)
        {
            double pointSpacing = input.TurnRadius * 0.1;

            int turnIndex = input.IsPointInsideTurnArea(input.PivotPosition);
            if (input.MakeUTurnCounter < 4 || turnIndex != 0)
            {
                youTurnPhase = 0;
                return true;
            }

            if (!FindCurveTurnPoint(input, true))
            {
                FailCreate();
                return false;
            }

            // Save a copy
            inClosestTurnPt = new TurnClosePoint(closestTurnPt);

            ytList.Clear();

            int count = input.IsHeadingSameWay ? -1 : 1;
            int curveIndex = inClosestTurnPt.CurveIndex + count;

            bool pointOutOfBnd = true;
            int stopIfWayOut = 0;

            double head = 0;

            while (pointOutOfBnd)
            {
                stopIfWayOut++;
                pointOutOfBnd = false;

                // Creates half a circle starting at the crossing point
                ytList.Clear();
                if (curveIndex >= input.GuidancePoints.Count || curveIndex < 0)
                {
                    FailCreate();
                    return false;
                }
                Vec3 currentPos = input.GuidancePoints[curveIndex];

                curveIndex += count;

                if (!input.IsHeadingSameWay) currentPos.Heading += Math.PI;
                if (currentPos.Heading >= TWO_PI) currentPos.Heading -= TWO_PI;

                ytList.Add(currentPos);

                while (Math.Abs(ytList[0].Heading - currentPos.Heading) < 2.2)
                {
                    // Update the position of the car
                    currentPos.Easting += pointSpacing * Math.Sin(currentPos.Heading);
                    currentPos.Northing += pointSpacing * Math.Cos(currentPos.Heading);

                    // Which way are we turning?
                    double turnParameter = input.IsTurnLeft ? -1.0 : 1.0;

                    // Update the heading
                    currentPos.Heading += (pointSpacing / input.TurnRadius) * turnParameter;

                    // Add the new coordinate to the path
                    ytList.Add(currentPos);
                }

                for (int i = 0; i < ytList.Count; i++)
                {
                    if (input.IsPointInsideTurnArea(ytList[i]) != 0)
                    {
                        pointOutOfBnd = true;
                        break;
                    }
                }
            }

            // Move out
            head = ytList[0].Heading;
            double cosHead = Math.Cos(head) * 0.1;
            double sinHead = Math.Sin(head) * 0.1;
            Vec3[] arr2 = new Vec3[ytList.Count];
            ytList.CopyTo(arr2);
            ytList.Clear();

            // Step 2 move the turn inside with steps of 0.1 meter
            int j = 0;
            pointOutOfBnd = false;

            while (!pointOutOfBnd)
            {
                stopIfWayOut++;
                pointOutOfBnd = false;

                for (int i = 0; i < arr2.Length; i++)
                {
                    arr2[i].Easting += sinHead;
                    arr2[i].Northing += cosHead;
                }

                for (j = 0; j < arr2.Length; j++)
                {
                    int bob = input.IsPointInsideTurnArea(arr2[j]);
                    if (bob != 0)
                    {
                        pointOutOfBnd = true;
                        break;
                    }
                }

                if (stopIfWayOut == 300 || Distance(arr2[0], input.PivotPosition) < 6)
                {
                    // For some reason it doesn't go inside boundary, return empty list
                    return false;
                }
            }

            ytList.AddRange(arr2);

            // Add start extension from curve points
            curveIndex -= count;

            // Now we go the other way to turn round
            head = ytList[0].Heading;
            head -= Math.PI;
            if (head < -Math.PI) head += TWO_PI;
            if (head > Math.PI) head -= TWO_PI;

            if (head >= TWO_PI) head -= TWO_PI;
            else if (head < 0) head += TWO_PI;

            // Add the tail to first turn
            head = ytList[ytList.Count - 1].Heading;

            Vec3 pt = new Vec3();
            for (int i = 1; i <= (int)(3 * turnOffset); i++)
            {
                pt.Easting = ytList[ytList.Count - 1].Easting + (Math.Sin(head) * 0.5);
                pt.Northing = ytList[ytList.Count - 1].Northing + (Math.Cos(head) * 0.5);
                pt.Heading = 0;
                ytList.Add(pt);
            }

            // Leading in line of turn
            for (int i = 0; i < 4; i++)
            {
                ytList.Insert(0, input.GuidancePoints[curveIndex + i * count]);
            }

            // Fill in the gaps
            double distance;

            int cnt = ytList.Count;
            for (int i = 1; i < cnt - 2; i++)
            {
                j = i + 1;
                if (j == cnt - 1) continue;
                distance = DistanceSquared(ytList[i], ytList[j]);
                if (distance > 1)
                {
                    Vec3 pointB = new Vec3((ytList[i].Easting + ytList[j].Easting) / 2.0,
                        (ytList[i].Northing + ytList[j].Northing) / 2.0, ytList[i].Heading);

                    ytList.Insert(j, pointB);
                    cnt = ytList.Count;
                    i--;
                }
            }

            // Calculate line headings
            Vec3[] arr = new Vec3[ytList.Count];
            ytList.CopyTo(arr);
            ytList.Clear();

            for (int i = 0; i < arr.Length - 1; i++)
            {
                arr[i].Heading = Math.Atan2(arr[i + 1].Easting - arr[i].Easting, arr[i + 1].Northing - arr[i].Northing);
                if (arr[i].Heading < 0) arr[i].Heading += TWO_PI;
                ytList.Add(arr[i]);
            }

            isOutOfBounds = false;
            youTurnPhase = 10;

            return true;
        }

        #endregion

        #region AB Turn Creation

        private bool CreateABTurn(YouTurnCreationInput input, double turnOffset)
        {
            if (input.TurnType == YouTurnType.AlbinStyle)
            {
                // Always use OmegaTurn - it uses Dubins paths which handle any turnOffset
                // The wide turn multi-phase approach doesn't work with single-call pattern
                return CreateABOmegaTurn(input, turnOffset);
            }
            else // KStyle
            {
                return CreateKStyleTurnAB(input, turnOffset);
            }
        }

        private bool CreateABOmegaTurn(YouTurnCreationInput input, double turnOffset)
        {
            // Keep from making turns constantly
            if (input.MakeUTurnCounter < 4)
            {
                youTurnPhase = 0;
                return true;
            }

            double head = input.ABHeading;
            if (!input.IsHeadingSameWay) head += Math.PI;
            if (head >= TWO_PI) head -= TWO_PI;

            // Phase 0: Find turn point and create Dubins path
            // How far are we from any turn boundary
            Vec3 onPurePoint = new Vec3(input.ABReferencePoint.Easting, input.ABReferencePoint.Northing, head);
            FindABTurnPoint(input, onPurePoint);

            // Or did we lose the turnLine
            if (closestTurnPt.TurnLineIndex == -1)
            {
                _logger.LogDebug("FindABTurnPoint failed: no intersection found. RefPoint=({E}, {N}), head={Head}°, boundaryLines={Count}",
                    onPurePoint.Easting, onPurePoint.Northing, head * 180 / Math.PI, input.BoundaryTurnLines.Count);
                FailCreate();
                return false;
            }

            inClosestTurnPt = new TurnClosePoint(closestTurnPt);

            _dubinsService.TurningRadius = input.TurnRadius;

            Vec3 start = inClosestTurnPt.ClosePt;
            start.Heading = head;

            Vec3 goal = start;

            // Now we go the other way to turn round
            double invertedHead = head - Math.PI;
            if (invertedHead < 0) invertedHead += TWO_PI;
            if (invertedHead > TWO_PI) invertedHead -= TWO_PI;

            if (input.IsTurnLeft)
            {
                goal.Easting = goal.Easting + (Math.Cos(-invertedHead) * turnOffset);
                goal.Northing = goal.Northing + (Math.Sin(-invertedHead) * turnOffset);
            }
            else
            {
                goal.Easting = goal.Easting - (Math.Cos(-invertedHead) * turnOffset);
                goal.Northing = goal.Northing - (Math.Sin(-invertedHead) * turnOffset);
            }

            goal.Heading = invertedHead;

            // Check if start and goal are in valid locations
            int goalResult = input.IsPointInsideTurnArea(goal);

            _logger.LogDebug("OmegaTurn: turnOffset={TurnOffset}m, skipRows={SkipRows}, goalResult={GoalResult}", turnOffset, input.RowSkipsWidth, goalResult);
            _logger.LogDebug("Goal position: E={Easting}, N={Northing}", goal.Easting, goal.Northing);

            // If goal is outside boundary (-1), log it but proceed.
            // The goal being near the edge is OK - we'll check the arc path later.
            if (goalResult == -1)
            {
                _logger.LogDebug("Goal at turnOffset={TurnOffset}m is near/outside boundary edge", turnOffset);
            }

            // Generate the turn points
            ytList = _dubinsService.GeneratePath(start, goal);
            isOutOfBounds = true;

            if (ytList.Count == 0)
            {
                FailCreate();
                return false;
            }

            // Clean up closely spaced points
            double distance;
            int cnt = ytList.Count;
            for (int i = 1; i < cnt - 2; i++)
            {
                distance = DistanceSquared(ytList[i], ytList[i + 1]);
                if (distance < pointSpacing)
                {
                    ytList.RemoveAt(i + 1);
                    i--;
                    cnt = ytList.Count;
                }
            }

            // Phase 1: Move turn inside boundary and add sequence lines
            ytList = MoveTurnInsideTurnLine(input, ytList, head, false, false);

            if (ytList.Count == 0)
            {
                FailCreate();
                return false;
            }

            isOutOfBounds = false;
            youTurnPhase = 10;

            // Add the entry and exit legs
            if (!AddABSequenceLines(input))
            {
                return false;
            }

            return true;
        }

        private bool CreateABWideTurn(YouTurnCreationInput input, double turnOffset)
        {
            // Keep from making turns constantly
            if (input.MakeUTurnCounter < 4)
            {
                youTurnPhase = 0;
                return true;
            }

            double head = input.ABHeading;
            if (!input.IsHeadingSameWay) head += Math.PI;
            if (head >= TWO_PI) head -= TWO_PI;

            switch (youTurnPhase)
            {
                case 0:
                    // Grab the pure pursuit point right on ABLine
                    Vec3 onPurePoint = new Vec3(input.ABReferencePoint.Easting, input.ABReferencePoint.Northing, 0);

                    // How far are we from any turn boundary
                    FindABTurnPoint(input, onPurePoint);

                    // Save a copy for first point
                    inClosestTurnPt = new TurnClosePoint(closestTurnPt);
                    startOfTurnPt = new TurnClosePoint(closestTurnPt);

                    // Already no turnline
                    if (inClosestTurnPt.TurnLineIndex == -1)
                    {
                        FailCreate();
                        return false;
                    }

                    // Creates half a circle starting at the crossing point
                    ytList.Clear();
                    Vec3 currentPos = new Vec3(inClosestTurnPt.ClosePt.Easting, inClosestTurnPt.ClosePt.Northing, head);
                    ytList.Add(currentPos);

                    // Taken from Dubins
                    while (Math.Abs(head - currentPos.Heading) < Math.PI)
                    {
                        // Update the position of the car
                        currentPos.Easting += pointSpacing * Math.Sin(currentPos.Heading);
                        currentPos.Northing += pointSpacing * Math.Cos(currentPos.Heading);

                        // Which way are we turning?
                        double turnParameter = input.IsTurnLeft ? -1.0 : 1.0;

                        // Update the heading
                        currentPos.Heading += (pointSpacing / input.TurnRadius) * turnParameter;

                        // Add the new coordinate to the path
                        ytList.Add(currentPos);
                    }

                    // Move the half circle to tangent the turnline
                    isOutOfBounds = true;
                    ytList = MoveTurnInsideTurnLine(input, ytList, head, true, false);

                    // If it couldn't be done this will trigger
                    if (ytList.Count == 0)
                    {
                        FailCreate();
                        return false;
                    }

                    youTurnPhase = 1;
                    return true;

                case 1:
                    // Build the next line to add sequencelines
                    // Use the pre-calculated turnOffset instead of recalculating from RowSkipsWidth
                    double widthMinusOverlap = input.ToolWidth - input.ToolOverlap;

                    // distAway = offset from reference line to next track center
                    // turnOffset is the perpendicular distance we need to move
                    double distAway = widthMinusOverlap * input.HowManyPathsAway
                        + ((input.IsTurnLeft ^ input.IsHeadingSameWay) ? -turnOffset : turnOffset)
                        + (input.IsHeadingSameWay ? input.ToolOffset : -input.ToolOffset) + input.NudgeDistance;

                    distAway += (0.5 * widthMinusOverlap);
                    _logger.LogDebug("WideTurn case 1: turnOffset={TurnOffset}m, distAway={DistAway}m", turnOffset, distAway);

                    nextCurve = BuildNewOffsetCurveList(input, distAway);

                    // Going with or against boundary?
                    bool isTurnLineSameWay = true;
                    double headingDifference = Math.Abs(startOfTurnPt.ClosePt.Heading - ytList[ytList.Count - 1].Heading);
                    if (headingDifference > PI_BY_2 && headingDifference < 3 * PI_BY_2) isTurnLineSameWay = false;

                    if (!FindABOutTurnPoint(nextCurve, inClosestTurnPt, isTurnLineSameWay))
                    {
                        // Error
                        FailCreate();
                        return false;
                    }
                    outClosestTurnPt = new TurnClosePoint(closestTurnPt);

                    Vec3 pointPos = new Vec3(outClosestTurnPt.ClosePt.Easting, outClosestTurnPt.ClosePt.Northing, 0);
                    double headie;
                    if (!isOutSameCurve)
                    {
                        headie = head;
                    }
                    else
                    {
                        headie = head + Math.PI;
                        if (headie >= TWO_PI) headie -= TWO_PI;
                    }
                    pointPos.Heading = headie;

                    // Step 3 create half circle in new list
                    ytList2.Clear();
                    ytList2.Add(pointPos);

                    // Taken from Dubins
                    while (Math.Abs(headie - pointPos.Heading) < Math.PI)
                    {
                        // Update the position of the car
                        pointPos.Easting += pointSpacing * Math.Sin(pointPos.Heading);
                        pointPos.Northing += pointSpacing * Math.Cos(pointPos.Heading);

                        // Which way are we turning?
                        double turnParameter = input.IsTurnLeft ? 1.0 : -1.0;

                        // Update the heading
                        pointPos.Heading += (pointSpacing / input.TurnRadius) * turnParameter;

                        // Add the new coordinate to the path
                        ytList2.Add(pointPos);
                    }

                    // Move the half circle to tangent the turnline
                    isOutOfBounds = true;
                    ytList2 = MoveTurnInsideTurnLine(input, ytList2, headie, true, true);

                    if (ytList2.Count == 0)
                    {
                        FailCreate();
                        return false;
                    }

                    youTurnPhase = 2;
                    return true;

                case 2:
                    int cnt1 = ytList.Count;
                    int cnt2 = ytList2.Count;

                    // Find if the turn goes same way as turnline heading
                    bool isFirstTurnLineSameWay = true;
                    double firstHeadingDifference = Math.Abs(inClosestTurnPt.TurnLineHeading - ytList[ytList.Count - 1].Heading);

                    if (firstHeadingDifference > PI_BY_2 && firstHeadingDifference < 3 * PI_BY_2) isFirstTurnLineSameWay = false;

                    // Finds out start and goal point along the turnline
                    FindInnerTurnPoints(ytList[cnt1 - 1], ytList[0].Heading, inClosestTurnPt, isFirstTurnLineSameWay);
                    TurnClosePoint startClosestTurnPt = new TurnClosePoint(closestTurnPt);

                    FindInnerTurnPoints(ytList2[cnt2 - 1], ytList2[0].Heading + Math.PI, outClosestTurnPt, !isFirstTurnLineSameWay);
                    TurnClosePoint goalClosestTurnPt = new TurnClosePoint(closestTurnPt);

                    // We have 2 different turnLine crossings
                    if (startClosestTurnPt.TurnLineNum != goalClosestTurnPt.TurnLineNum)
                    {
                        FailCreate();
                        return false;
                    }

                    // Segment index is the "A" of the segment. segmentIndex+1 would be the "B"
                    // Is in and out on same segment? so only 1 segment
                    if (startClosestTurnPt.TurnLineIndex == goalClosestTurnPt.TurnLineIndex)
                    {
                        for (int a = 0; a < cnt2; cnt2--)
                        {
                            ytList.Add(ytList2[cnt2 - 1]);
                        }
                    }
                    else
                    {
                        // Multiple segments
                        Vec3 tPoint = new Vec3();
                        int turnCount = input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points.Count;

                        // How many points from turnline do we add
                        int loops = Math.Abs(startClosestTurnPt.TurnLineIndex - goalClosestTurnPt.TurnLineIndex);

                        // Are we crossing a border?
                        if (loops > (input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points.Count / 2))
                        {
                            if (startClosestTurnPt.TurnLineIndex < goalClosestTurnPt.TurnLineIndex)
                            {
                                loops = (turnCount - goalClosestTurnPt.TurnLineIndex) + startClosestTurnPt.TurnLineIndex;
                            }
                            else
                            {
                                loops = (turnCount - startClosestTurnPt.TurnLineIndex) + goalClosestTurnPt.TurnLineIndex;
                            }
                        }

                        // CountExit up - start with B which is next A
                        if (isFirstTurnLineSameWay)
                        {
                            for (int i = 0; i < loops; i++)
                            {
                                if ((startClosestTurnPt.TurnLineIndex + 1) >= turnCount) startClosestTurnPt.TurnLineIndex = -1;

                                tPoint = input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points[startClosestTurnPt.TurnLineIndex + 1];
                                startClosestTurnPt.TurnLineIndex++;
                                if (startClosestTurnPt.TurnLineIndex >= turnCount)
                                    startClosestTurnPt.TurnLineIndex = 0;
                                ytList.Add(tPoint);
                            }
                        }
                        else // CountExit down = start with A
                        {
                            for (int i = 0; i < loops; i++)
                            {
                                tPoint = input.BoundaryTurnLines[startClosestTurnPt.TurnLineNum].Points[startClosestTurnPt.TurnLineIndex];
                                startClosestTurnPt.TurnLineIndex--;
                                if (startClosestTurnPt.TurnLineIndex == -1)
                                    startClosestTurnPt.TurnLineIndex = turnCount - 1;
                                ytList.Add(tPoint);
                            }
                        }

                        // Add the out from ytList2
                        for (int a = 0; a < cnt2; cnt2--)
                        {
                            ytList.Add(ytList2[cnt2 - 1]);
                        }
                    }

                    // Fill in the gaps
                    double distance;

                    int cnt = ytList.Count;
                    for (int i = 1; i < cnt - 2; i++)
                    {
                        int j = i + 1;
                        if (j == cnt - 1) continue;
                        distance = DistanceSquared(ytList[i], ytList[j]);
                        if (distance > 1)
                        {
                            Vec3 pointB = new Vec3((ytList[i].Easting + ytList[j].Easting) / 2.0,
                                (ytList[i].Northing + ytList[j].Northing) / 2.0, ytList[i].Heading);

                            ytList.Insert(j, pointB);
                            cnt = ytList.Count;
                            i--;
                        }
                    }

                    // Calculate the new points headings based on fore and aft of point - smoother turns
                    cnt = ytList.Count;
                    Vec3[] arr = new Vec3[cnt];
                    cnt -= 2;
                    ytList.CopyTo(arr);
                    ytList.Clear();

                    for (int i = 2; i < cnt; i++)
                    {
                        Vec3 pt3 = arr[i];
                        pt3.Heading = Math.Atan2(arr[i + 1].Easting - arr[i - 1].Easting,
                            arr[i + 1].Northing - arr[i - 1].Northing);
                        if (pt3.Heading < 0) pt3.Heading += TWO_PI;
                        ytList.Add(pt3);
                    }

                    // Check too close
                    if (Distance(ytList[0], input.PivotPosition) < 3)
                    {
                        FailCreate();
                        return false;
                    }

                    // Are we continuing the same way?
                    isOutOfBounds = false;
                    youTurnPhase = 10;
                    ytList2.Clear();

                    if (!AddABSequenceLines(input)) return false;

                    return true;
            }

            // Just in case
            return true;
        }

        private bool CreateKStyleTurnAB(YouTurnCreationInput input, double turnOffset)
        {
            double pointSpacing = input.TurnRadius * 0.1;

            int turnIndex = input.IsPointInsideTurnArea(input.PivotPosition);
            if (input.MakeUTurnCounter < 4 || turnIndex != 0)
            {
                youTurnPhase = 0;
                return true;
            }

            // Grab the pure pursuit point right on ABLine
            Vec3 onPurePoint = new Vec3(input.ABReferencePoint.Easting, input.ABReferencePoint.Northing, 0);

            // How far are we from any turn boundary
            FindABTurnPoint(input, onPurePoint);

            // Save a copy for first point
            inClosestTurnPt = new TurnClosePoint(closestTurnPt);

            // Already no turnline
            if (inClosestTurnPt.TurnLineIndex == -1) return false;

            double head = input.ABHeading;

            if (!input.IsHeadingSameWay) head += Math.PI;
            if (head >= TWO_PI) head -= TWO_PI;

            // Distance to turnline from where we are
            double turnDiagDistance = Distance(input.PivotPosition, closestTurnPt.ClosePt);

            // Moves the point to the crossing with the turnline
            double rEastYT = input.ABReferencePoint.Easting + (Math.Sin(head) * turnDiagDistance);
            double rNorthYT = input.ABReferencePoint.Northing + (Math.Cos(head) * turnDiagDistance);

            // Creates half a circle starting at the crossing point
            ytList.Clear();
            Vec3 currentPos = new Vec3(rEastYT, rNorthYT, head);
            ytList.Add(currentPos);

            // Make semi circle - not quite
            while (Math.Abs(head - currentPos.Heading) < 2.2)
            {
                // Update the position of the car
                currentPos.Easting += pointSpacing * Math.Sin(currentPos.Heading);
                currentPos.Northing += pointSpacing * Math.Cos(currentPos.Heading);

                // Which way are we turning?
                double turnParameter = 1.0;

                if (input.IsTurnLeft) turnParameter = -1.0;

                // Update the heading
                currentPos.Heading += (pointSpacing / input.TurnRadius) * turnParameter;

                // Add the new coordinate to the path
                ytList.Add(currentPos);
            }

            // Move the half circle to tangent the turnline
            ytList = MoveTurnInsideTurnLine(input, ytList, head, false, false);

            // If it couldn't be done this will trigger
            if (ytList.Count < 5 || semiCircleIndex == -1)
            {
                FailCreate();
                return false;
            }

            // Grab the vehicle widths and offsets: skip=0 means 1 width, skip=1 means 2 widths, etc.
            double turnOffsetCalc = (input.ToolWidth - input.ToolOverlap) * (input.RowSkipsWidth + 1) + (input.IsTurnLeft ? input.ToolOffset : -input.ToolOffset);

            // Add the tail to first turn
            int count = ytList.Count;
            head = ytList[count - 1].Heading;

            Vec3 pt = new Vec3();
            for (int i = 1; i <= (int)(3 * turnOffsetCalc); i++)
            {
                pt.Easting = ytList[count - 1].Easting + (Math.Sin(head) * i * 0.5);
                pt.Northing = ytList[count - 1].Northing + (Math.Cos(head) * i * 0.5);
                pt.Heading = 0;
                ytList.Add(pt);
            }

            // Leading in line of turn
            head = input.ABHeading;
            if (input.IsHeadingSameWay) head += Math.PI;
            if (head >= TWO_PI) head -= TWO_PI;

            for (int a = 0; a < 8; a++)
            {
                pt.Easting = ytList[0].Easting + (Math.Sin(head) * 0.511);
                pt.Northing = ytList[0].Northing + (Math.Cos(head) * 0.511);
                pt.Heading = ytList[0].Heading;
                ytList.Insert(0, pt);
            }

            // Calculate line headings
            Vec3[] arr = new Vec3[ytList.Count];
            ytList.CopyTo(arr);
            ytList.Clear();

            // Headings of line one
            for (int i = 0; i < arr.Length - 1; i++)
            {
                arr[i].Heading = Math.Atan2(arr[i + 1].Easting - arr[i].Easting, arr[i + 1].Northing - arr[i].Northing);
                if (arr[i].Heading < 0) arr[i].Heading += TWO_PI;
                ytList.Add(arr[i]);
            }

            isOutOfBounds = false;
            youTurnPhase = 10;

            return true;
        }

        #endregion

        #region Helper Methods - Turn Point Finding

        private bool FindCurveTurnPoint(YouTurnCreationInput input, bool useAlternateHeading)
        {
            // Find closest AB Curve point that will cross and go out of bounds
            int count = input.IsHeadingSameWay ? 1 : -1;
            int turnNum = 99;
            int j;

            closestTurnPt = new TurnClosePoint();

            bool loop = input.TrackMode == 32 || input.TrackMode == 64; // bndCurve or waterPivot

            for (j = input.CurrentLocationIndex; j > 0 && j < input.GuidancePoints.Count; j += count)
            {
                if (j < 0)
                {
                    if (loop)
                    {
                        loop = false;
                        j = input.GuidancePoints.Count;
                        continue;
                    }
                    break;
                }
                else if (j >= input.GuidancePoints.Count)
                {
                    if (loop)
                    {
                        loop = false;
                        j = -1;
                        continue;
                    }
                    break;
                }

                int turnIndex = input.IsPointInsideTurnArea(input.GuidancePoints[j]);
                if (turnIndex != 0)
                {
                    closestTurnPt.CurveIndex = j - count;
                    closestTurnPt.TurnLineNum = turnIndex;
                    turnNum = turnIndex;
                    break;
                }
            }

            if (turnNum < 0)
            {
                closestTurnPt.TurnLineNum = 0;
                turnNum = 0;
            }
            else if (turnNum == 99)
            {
                // Curve does not cross a boundary
                return false;
            }

            if (closestTurnPt.CurveIndex == -1)
            {
                return false;
            }

            // Find exact intersection with turn line (including closing segment)
            var turnLinePoints = input.BoundaryTurnLines[turnNum].Points;
            for (int i = 0; i < turnLinePoints.Count; i++)
            {
                int nextI = (i + 1) % turnLinePoints.Count;  // Wrap around for closing segment
                int res = GetLineIntersection(
                        turnLinePoints[i].Easting,
                        turnLinePoints[i].Northing,
                        turnLinePoints[nextI].Easting,
                        turnLinePoints[nextI].Northing,

                        input.GuidancePoints[closestTurnPt.CurveIndex].Easting,
                        input.GuidancePoints[closestTurnPt.CurveIndex].Northing,
                        input.GuidancePoints[closestTurnPt.CurveIndex + count].Easting,
                        input.GuidancePoints[closestTurnPt.CurveIndex + count].Northing,

                         ref iE, ref iN);

                if (res == 1)
                {
                    var closePt = closestTurnPt.ClosePt;
                    closePt.Easting = iE;
                    closePt.Northing = iN;

                    if (useAlternateHeading)
                    {
                        double hed = Math.Atan2(turnLinePoints[nextI].Easting - turnLinePoints[i].Easting,
                            turnLinePoints[nextI].Northing - turnLinePoints[i].Northing);
                        if (hed < 0) hed += TWO_PI;
                        closePt.Heading = hed;
                        closestTurnPt.ClosePt = closePt;
                        closestTurnPt.TurnLineIndex = i;
                    }
                    else
                    {
                        closePt.Heading = input.GuidancePoints[closestTurnPt.CurveIndex].Heading;
                        closestTurnPt.ClosePt = closePt;
                        closestTurnPt.TurnLineIndex = i;
                        closestTurnPt.TurnLineNum = turnNum;
                        closestTurnPt.TurnLineHeading = turnLinePoints[i].Heading;
                        if (!input.IsHeadingSameWay && closestTurnPt.CurveIndex > 0) closestTurnPt.CurveIndex--;
                    }
                    break;
                }
            }

            return closestTurnPt.TurnLineIndex != -1 && closestTurnPt.CurveIndex != -1;
        }

        private void FindABTurnPoint(YouTurnCreationInput input, Vec3 fromPt)
        {
            double eP = fromPt.Easting;
            double nP = fromPt.Northing;
            double eAB, nAB;
            turnClosestList?.Clear();

            if (input.IsHeadingSameWay)
            {
                // Point B direction
                eAB = input.ABReferencePoint.Easting + Math.Sin(input.ABHeading) * 1000;
                nAB = input.ABReferencePoint.Northing + Math.Cos(input.ABHeading) * 1000;
            }
            else
            {
                // Point A direction
                eAB = input.ABReferencePoint.Easting - Math.Sin(input.ABHeading) * 1000;
                nAB = input.ABReferencePoint.Northing - Math.Cos(input.ABHeading) * 1000;
            }

            turnClosestList.Clear();

            for (int j = 0; j < input.BoundaryTurnLines.Count; j++)
            {
                var pts = input.BoundaryTurnLines[j].Points;
                // Check all segments including the closing segment from last point back to first
                for (int i = 0; i < pts.Count; i++)
                {
                    int nextI = (i + 1) % pts.Count;  // Wrap around to close polygon
                    int res = GetLineIntersection(
                        pts[i].Easting,
                        pts[i].Northing,
                        pts[nextI].Easting,
                        pts[nextI].Northing,
                        eP, nP, eAB, nAB, ref iE, ref iN
                    );

                    if (res == 1)
                    {
                        var cClose = new TurnClosePoint();
                        var closePt = cClose.ClosePt;
                        closePt.Easting = iE;
                        closePt.Northing = iN;

                        double hed = Math.Atan2(pts[nextI].Easting - pts[i].Easting,
                            pts[nextI].Northing - pts[i].Northing);
                        if (hed < 0) hed += TWO_PI;
                        closePt.Heading = hed;
                        cClose.ClosePt = closePt;
                        cClose.TurnLineNum = j;
                        cClose.TurnLineIndex = i;

                        turnClosestList.Add(new TurnClosePoint(cClose));
                    }
                }
            }

            // Determine closest point
            double minDistance = double.MaxValue;

            if (turnClosestList.Count > 0)
            {
                for (int i = 0; i < turnClosestList.Count; i++)
                {
                    double dist = ((fromPt.Easting - turnClosestList[i].ClosePt.Easting) * (fromPt.Easting - turnClosestList[i].ClosePt.Easting))
                                    + ((fromPt.Northing - turnClosestList[i].ClosePt.Northing) * (fromPt.Northing - turnClosestList[i].ClosePt.Northing));

                    if (minDistance >= dist)
                    {
                        minDistance = dist;
                        closestTurnPt = new TurnClosePoint(turnClosestList[i]);
                    }
                }
            }
        }

        private List<Vec3> MoveTurnInsideTurnLine(YouTurnCreationInput input, List<Vec3> uTurnList, double head, bool deleteSecondHalf, bool invertHeading)
        {
            // Step 1 make array out of the list so that we can modify the position
            double cosHead = Math.Cos(head);
            double sinHead = Math.Sin(head);
            int cnt = uTurnList.Count;
            Vec3[] arr2 = new Vec3[cnt];
            uTurnList.CopyTo(arr2);
            uTurnList.Clear();

            semiCircleIndex = -1;
            // Step 2 move the turn inside with steps of 1 meter
            bool pointOutOfBnd = isOutOfBounds;
            int j = 0;
            int stopIfWayOut = 0;
            while (pointOutOfBnd)
            {
                stopIfWayOut++;
                pointOutOfBnd = false;

                for (int i = 0; i < cnt; i++)
                {
                    arr2[i].Easting -= sinHead;
                    arr2[i].Northing -= cosHead;
                }

                for (; j < cnt; j += 1)
                {
                    int result = input.IsPointInsideTurnArea(arr2[j]);
                    if (result != 0)
                    {
                        pointOutOfBnd = true;
                        if (j > 0) j--;
                        break;
                    }
                }

                double distToVehicle = Distance(arr2[0], input.PivotPosition);
                if (stopIfWayOut == 1000 || distToVehicle < 3)
                {
                    // For some reason it doesn't go inside boundary, return empty list
                    return uTurnList;
                }
            }

            // Step 3, we are now inside turnline, move the turn forward until it hits the turnfence in steps of 0.1 meters
            while (!pointOutOfBnd)
            {
                for (int i = 0; i < cnt; i++)
                {
                    arr2[i].Easting += (sinHead * 0.1);
                    arr2[i].Northing += (cosHead * 0.1);
                }

                for (int a = 0; a < cnt; a++)
                {
                    if (input.IsPointInsideTurnArea(arr2[a]) != 0)
                    {
                        semiCircleIndex = a;
                        pointOutOfBnd = true;
                        break;
                    }
                }
            }

            // Step 4, Should we delete the points after the one that is outside? and where the points made in the wrong direction?
            for (int i = 0; i < cnt; i++)
            {
                if (i == semiCircleIndex && deleteSecondHalf)
                    break;
                if (invertHeading) arr2[i].Heading += Math.PI;
                if (arr2[i].Heading >= TWO_PI) arr2[i].Heading -= TWO_PI;
                else if (arr2[i].Heading < 0) arr2[i].Heading += TWO_PI;
                uTurnList.Add(arr2[i]);
            }

            // We have successfully moved the turn inside
            isOutOfBounds = false;

            return uTurnList;
        }

        private bool AddCurveSequenceLines(YouTurnCreationInput input)
        {
            // Calculate leg length - use direct LegLength if set, otherwise calculate
            double legLength;
            if (input.LegLength > 0)
            {
                legLength = input.LegLength;
            }
            else
            {
                legLength = input.HeadlandWidth * input.YouTurnLegExtensionMultiplier;

                double minLegLength = input.TurnRadius * 2.0;
                if (legLength < minLegLength) legLength = minLegLength;
            }

            // For curves, we add points from the curve itself
            // Estimate how many curve points we need based on leg length and typical curve point spacing (~1m)
            int numLegPoints = (int)(legLength / 1.0);
            if (numLegPoints < 10) numLegPoints = 10;

            bool sameWay = input.IsHeadingSameWay;
            int a = sameWay ? -1 : 1;

            // Add entry leg points from the curve
            for (int i = 0; i < numLegPoints; i++)
            {
                if (inClosestTurnPt.CurveIndex < 2 || inClosestTurnPt.CurveIndex > input.GuidancePoints.Count - 3)
                {
                    break;  // Stop if we run out of curve points
                }
                ytList.Insert(0, input.GuidancePoints[inClosestTurnPt.CurveIndex]);
                inClosestTurnPt.CurveIndex += a;
            }

            if (isOutSameCurve) sameWay = !sameWay;
            a = sameWay ? -1 : 1;

            // Add exit leg points from the next curve
            for (int i = 0; i < numLegPoints; i++)
            {
                if (outClosestTurnPt.CurveIndex < 2 || outClosestTurnPt.CurveIndex > nextCurve.Count - 3)
                {
                    break;  // Stop if we run out of curve points
                }
                ytList.Add(nextCurve[outClosestTurnPt.CurveIndex]);
                outClosestTurnPt.CurveIndex += a;
            }

            return true;
        }

        private bool AddABSequenceLines(YouTurnCreationInput input)
        {
            double inhead = input.ABHeading;
            if (!input.IsHeadingSameWay) inhead += Math.PI;
            if (inhead > TWO_PI) inhead -= TWO_PI;

            // After a U-turn, exit direction is opposite to entry (180° turn)
            // Unless isOutSameCurve, in which case we continue in same direction
            double outhead = inhead + Math.PI;  // Default: opposite direction after U-turn
            if (isOutSameCurve) outhead = inhead;  // Same direction if exiting same curve
            if (outhead > TWO_PI) outhead -= TWO_PI;
            if (outhead < 0) outhead += TWO_PI;

            // Calculate leg length - use direct LegLength if set, otherwise calculate
            double legLength;
            if (input.LegLength > 0)
            {
                // User specified leg length directly (UTurnExtension setting)
                legLength = input.LegLength;
            }
            else
            {
                // Fallback: calculate from headland width
                legLength = input.HeadlandWidth * input.YouTurnLegExtensionMultiplier;

                // Ensure minimum leg length based on turn diameter
                double minLegLength = input.TurnRadius * 2.0;
                if (legLength < minLegLength) legLength = minLegLength;
            }

            // Use 1 meter spacing for leg points
            double legPointSpacing = 1.0;
            int numLegPoints = (int)(legLength / legPointSpacing);
            if (numLegPoints < 10) numLegPoints = 10;  // Minimum 10 points (10 meters)

            // Add entry leg (before the turn arc)
            // Store the arc start point before we add entry leg points
            Vec3 arcStart = ytList[0];
            for (int a = 0; a < numLegPoints; a++)
            {
                Vec3 pt = new Vec3
                {
                    Easting = ytList[0].Easting - (Math.Sin(inhead) * legPointSpacing),
                    Northing = ytList[0].Northing - (Math.Cos(inhead) * legPointSpacing),
                    Heading = inhead
                };
                ytList.Insert(0, pt);
            }

            // The entry START point is now ytList[0] - this is in the cultivated area
            Vec3 entryStart = ytList[0];

            // Calculate the turn offset (perpendicular distance to next track)
            // skip=0 means 1 width (adjacent), skip=1 means 2 widths, etc.
            double turnOffset = (input.ToolWidth - input.ToolOverlap) * (input.RowSkipsWidth + 1)
                + (input.IsTurnLeft ? -input.ToolOffset * 2.0 : input.ToolOffset * 2.0);

            // Calculate perpendicular angle based on TRAVEL heading and turn direction
            // The turn goes perpendicular to travel direction (left or right based on IsTurnLeft)
            double perpAngle = inhead + (input.IsTurnLeft ? -PI_BY_2 : PI_BY_2);
            if (perpAngle < 0) perpAngle += TWO_PI;
            if (perpAngle > TWO_PI) perpAngle -= TWO_PI;

            // The EXIT END point should be in the cultivated area on the NEXT track
            // It's the entry start point offset perpendicular by the turn offset
            double exitEndE = entryStart.Easting + Math.Sin(perpAngle) * turnOffset;
            double exitEndN = entryStart.Northing + Math.Cos(perpAngle) * turnOffset;
            Vec3 exitEnd = new Vec3 { Easting = exitEndE, Northing = exitEndN, Heading = outhead };

            int count = ytList.Count;
            Vec3 arcEnd = ytList[count - 1];  // Last point of arc

            // Generate exit leg points going straight from arc end in outhead direction
            // This ensures exit leg is parallel to entry leg
            for (int i = 1; i <= numLegPoints; i++)
            {
                Vec3 pt = new Vec3
                {
                    Easting = arcEnd.Easting + Math.Sin(outhead) * i * legPointSpacing,
                    Northing = arcEnd.Northing + Math.Cos(outhead) * i * legPointSpacing,
                    Heading = outhead
                };
                ytList.Add(pt);
            }

            double distancePivotToTurnLine;
            count = ytList.Count;
            for (int i = 0; i < count; i += 2)
            {
                distancePivotToTurnLine = DistanceSquared(ytList[i], input.PivotPosition);
                if (distancePivotToTurnLine <= 3)
                {
                    FailCreate();
                    return false;
                }
            }

            return true;
        }

        private List<Vec3> BuildNewOffsetCurveList(YouTurnCreationInput input, double distAway)
        {
            // This is a placeholder for building offset curves
            // In full implementation, would offset the guidance curve by the specified distance
            // For now, return copy of original guidance points
            return new List<Vec3>(input.GuidancePoints);
        }

        private void FindInnerTurnPoints(Vec3 fromPt, double heading, TurnClosePoint turnPt, bool isSameWay)
        {
            double sin = Math.Sin(heading);
            double cos = Math.Cos(heading);
            closestTurnPt = new TurnClosePoint();

            int bndNum = turnPt.TurnLineNum;
            if (bndNum < 0 || bndNum >= _currentInput.BoundaryTurnLines.Count) return;

            var turnLine = _currentInput.BoundaryTurnLines[bndNum].Points;

            if (!isSameWay)
            {
                for (int i = turnLine.Count - 1; i >= 0; i--)
                {
                    int res = GetLineIntersection(
                        fromPt.Easting, fromPt.Northing,
                        fromPt.Easting + (sin * 1000), fromPt.Northing + (cos * 1000),
                        turnLine[i].Easting, turnLine[i].Northing,
                        i == 0 ? turnLine[turnLine.Count - 1].Easting : turnLine[i - 1].Easting,
                        i == 0 ? turnLine[turnLine.Count - 1].Northing : turnLine[i - 1].Northing,
                        ref iE, ref iN);

                    if (res == 1)
                    {
                        var closePt = closestTurnPt.ClosePt;
                        closePt.Easting = iE;
                        closePt.Northing = iN;
                        closestTurnPt.ClosePt = closePt;
                        closestTurnPt.TurnLineNum = bndNum;
                        closestTurnPt.TurnLineIndex = i == 0 ? turnLine.Count - 1 : i - 1;
                        return;
                    }
                }
            }
            else
            {
                // Iterate through all segments including the closing segment
                for (int i = 0; i < turnLine.Count; i++)
                {
                    int nextI = (i + 1) % turnLine.Count;  // Wrap around for closing segment
                    int res = GetLineIntersection(
                        fromPt.Easting, fromPt.Northing,
                        fromPt.Easting + (sin * 1000), fromPt.Northing + (cos * 1000),
                        turnLine[i].Easting, turnLine[i].Northing,
                        turnLine[nextI].Easting, turnLine[nextI].Northing,
                        ref iE, ref iN);

                    if (res == 1)
                    {
                        var closePt = closestTurnPt.ClosePt;
                        closePt.Easting = iE;
                        closePt.Northing = iN;
                        closestTurnPt.ClosePt = closePt;
                        closestTurnPt.TurnLineNum = bndNum;
                        closestTurnPt.TurnLineIndex = i;
                        return;
                    }
                }
            }
        }

        private bool FindCurveOutTurnPoint(List<Vec3> nextCurve, TurnClosePoint startPt, bool isTurnLineSameWay)
        {
            int count = _currentInput.IsHeadingSameWay ? 1 : -1;
            int turnNum = 99;

            closestTurnPt = new TurnClosePoint();

            for (int j = _currentInput.CurrentLocationIndex; j > 0 && j < nextCurve.Count; j += count)
            {
                if (j < 0 || j >= nextCurve.Count) break;

                int turnIndex = _currentInput.IsPointInsideTurnArea(nextCurve[j]);
                if (turnIndex != 0)
                {
                    closestTurnPt.CurveIndex = j - count;
                    closestTurnPt.TurnLineNum = turnIndex;
                    turnNum = turnIndex;
                    break;
                }
            }

            if (turnNum < 0)
            {
                closestTurnPt.TurnLineNum = 0;
                turnNum = 0;
            }
            else if (turnNum == 99)
            {
                return false;
            }

            if (closestTurnPt.CurveIndex == -1)
            {
                return false;
            }

            // Find exact intersection (including closing segment)
            if (turnNum >= _currentInput.BoundaryTurnLines.Count) return false;

            var turnLinePoints = _currentInput.BoundaryTurnLines[turnNum].Points;
            for (int i = 0; i < turnLinePoints.Count; i++)
            {
                int nextI = (i + 1) % turnLinePoints.Count;  // Wrap around for closing segment
                int res = GetLineIntersection(
                    turnLinePoints[i].Easting,
                    turnLinePoints[i].Northing,
                    turnLinePoints[nextI].Easting,
                    turnLinePoints[nextI].Northing,
                    nextCurve[closestTurnPt.CurveIndex].Easting,
                    nextCurve[closestTurnPt.CurveIndex].Northing,
                    nextCurve[closestTurnPt.CurveIndex + count].Easting,
                    nextCurve[closestTurnPt.CurveIndex + count].Northing,
                    ref iE, ref iN);

                if (res == 1)
                {
                    var closePt = closestTurnPt.ClosePt;
                    closePt.Easting = iE;
                    closePt.Northing = iN;
                    closePt.Heading = nextCurve[closestTurnPt.CurveIndex].Heading;
                    closestTurnPt.ClosePt = closePt;
                    closestTurnPt.TurnLineIndex = i;
                    closestTurnPt.TurnLineNum = turnNum;
                    closestTurnPt.TurnLineHeading = turnLinePoints[i].Heading;

                    // Check if we're going out the same curve
                    if (closestTurnPt.TurnLineNum == startPt.TurnLineNum &&
                        Math.Abs(closestTurnPt.TurnLineIndex - startPt.TurnLineIndex) < 3)
                    {
                        isOutSameCurve = true;
                    }
                    break;
                }
            }

            return closestTurnPt.TurnLineIndex != -1 && closestTurnPt.CurveIndex != -1;
        }

        private bool FindABOutTurnPoint(List<Vec3> nextCurve, TurnClosePoint inPt, bool isTurnLineSameWay)
        {
            int a = isTurnLineSameWay ? 1 : -1;
            int turnLineIndex = inPt.TurnLineIndex;
            int turnLineNum = inPt.TurnLineNum;
            int stopTurnLineIndex = inPt.TurnLineIndex - a;

            if (stopTurnLineIndex < 0) stopTurnLineIndex = _currentInput.BoundaryTurnLines[turnLineNum].Points.Count - 3;
            if (stopTurnLineIndex > _currentInput.BoundaryTurnLines[turnLineNum].Points.Count - 1) turnLineIndex = 3;

            for (; turnLineIndex != stopTurnLineIndex; turnLineIndex += a)
            {
                if (turnLineIndex < 0) turnLineIndex = _currentInput.BoundaryTurnLines[turnLineNum].Points.Count - 2;
                if (turnLineIndex > _currentInput.BoundaryTurnLines[turnLineNum].Points.Count - 2) turnLineIndex = 0;

                if (nextCurve.Count > 1)
                {
                    int res = GetLineIntersection(
                        _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex].Easting,
                        _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex].Northing,
                        _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex + 1].Easting,
                        _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex + 1].Northing,
                        nextCurve[0].Easting,
                        nextCurve[0].Northing,
                        nextCurve[1].Easting,
                        nextCurve[1].Northing,
                        ref iE, ref iN);

                    if (res == 1)
                    {
                        closestTurnPt = new TurnClosePoint();
                        var closePt = closestTurnPt.ClosePt;
                        closePt.Easting = iE;
                        closePt.Northing = iN;
                        closePt.Heading = _currentInput.ABHeading;
                        closestTurnPt.ClosePt = closePt;
                        closestTurnPt.TurnLineIndex = turnLineIndex;
                        closestTurnPt.CurveIndex = -1;
                        closestTurnPt.TurnLineHeading = _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex].Heading;
                        closestTurnPt.TurnLineNum = turnLineNum;
                        return true;
                    }
                }

                // Calculate AB line endpoints for intersection
                double abHead = _currentInput.ABHeading;
                Vec3 ptA = new Vec3(_currentInput.ABReferencePoint.Easting - Math.Sin(abHead) * 2000,
                                    _currentInput.ABReferencePoint.Northing - Math.Cos(abHead) * 2000, 0);
                Vec3 ptB = new Vec3(_currentInput.ABReferencePoint.Easting + Math.Sin(abHead) * 2000,
                                    _currentInput.ABReferencePoint.Northing + Math.Cos(abHead) * 2000, 0);

                int res2 = GetLineIntersection(
                    _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex].Easting,
                    _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex].Northing,
                    _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex + 1].Easting,
                    _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex + 1].Northing,
                    ptA.Easting,
                    ptA.Northing,
                    ptB.Easting,
                    ptB.Northing,
                    ref iE, ref iN);

                if (res2 == 1)
                {
                    double hed;
                    if (_currentInput.IsHeadingSameWay)
                        hed = Math.Atan2(_currentInput.ABReferencePoint.Easting - iE, _currentInput.ABReferencePoint.Northing - iN);
                    else
                        hed = Math.Atan2(iE - _currentInput.ABReferencePoint.Easting, iN - _currentInput.ABReferencePoint.Northing);

                    if (hed < 0) hed += TWO_PI;
                    hed = Math.Round(hed, 3);
                    double hedAB = Math.Round(_currentInput.ABHeading, 3);

                    if (hed == hedAB)
                    {
                        return false; // Hitting the curve behind us
                    }
                    else if (turnLineIndex == inPt.TurnLineIndex)
                    {
                        // Do nothing - hitting the curve at the same place as in
                    }
                    else
                    {
                        closestTurnPt = new TurnClosePoint();
                        var closePt = closestTurnPt.ClosePt;
                        closePt.Easting = iE;
                        closePt.Northing = iN;
                        closePt.Heading = _currentInput.ABHeading;
                        closestTurnPt.ClosePt = closePt;
                        closestTurnPt.TurnLineIndex = turnLineIndex;
                        closestTurnPt.CurveIndex = -1;
                        closestTurnPt.TurnLineHeading = _currentInput.BoundaryTurnLines[turnLineNum].Points[turnLineIndex].Heading;
                        closestTurnPt.TurnLineNum = turnLineNum;
                        isOutSameCurve = true;
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region Helper Methods - Utilities

        /// <summary>
        /// Calculate line intersection between two line segments.
        /// Returns 1 if lines intersect, 0 otherwise.
        /// </summary>
        private int GetLineIntersection(double p0x, double p0y, double p1x, double p1y,
                double p2x, double p2y, double p3x, double p3y, ref double iEast, ref double iNorth)
        {
            double s1x = p1x - p0x;
            double s1y = p1y - p0y;

            double s2x = p3x - p2x;
            double s2y = p3y - p2y;

            double s = (-s1y * (p0x - p2x) + s1x * (p0y - p2y)) / (-s2x * s1y + s1x * s2y);

            if (s >= 0 && s <= 1)
            {
                // Check other side
                double t = (s2x * (p0y - p2y) - s2y * (p0x - p2x)) / (-s2x * s1y + s1x * s2y);
                if (t >= 0 && t <= 1)
                {
                    // Collision detected
                    iEast = p0x + (t * s1x);
                    iNorth = p0y + (t * s1y);
                    return 1;
                }
            }

            return 0; // No collision
        }

        private static double Distance(Vec3 a, Vec3 b)
        {
            double dx = a.Easting - b.Easting;
            double dz = a.Northing - b.Northing;
            return Math.Sqrt((dx * dx) + (dz * dz));
        }

        private static double DistanceSquared(Vec3 a, Vec3 b)
        {
            double dx = a.Easting - b.Easting;
            double dz = a.Northing - b.Northing;
            return (dx * dx) + (dz * dz);
        }

        private bool IsGoingStraightThrough()
        {
            if (ytList.Count < 3) return false;
            return Math.PI - Math.Abs(Math.Abs(ytList[ytList.Count - 2].Heading - ytList[1].Heading) - Math.PI) < PI_BY_2;
        }

        private void FailCreate()
        {
            isOutOfBounds = true;
            youTurnPhase = 11;
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Represents a close point on a turn line.
        /// </summary>
        private class TurnClosePoint
        {
            public Vec3 ClosePt { get; set; } = new Vec3();
            public int TurnLineNum { get; set; } = -1;
            public int TurnLineIndex { get; set; } = -1;
            public double TurnLineHeading { get; set; } = -1;
            public int CurveIndex { get; set; } = -1;

            public TurnClosePoint() { }

            public TurnClosePoint(TurnClosePoint other)
            {
                ClosePt = other.ClosePt;
                TurnLineNum = other.TurnLineNum;
                TurnLineIndex = other.TurnLineIndex;
                TurnLineHeading = other.TurnLineHeading;
                CurveIndex = other.CurveIndex;
            }
        }

        #endregion
    }
}
