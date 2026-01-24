using OpenTK.Graphics.OpenGL;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Models.Track;
using AgOpenGPS.Core.Services.Track;

namespace AgOpenGPS
{
    public enum TrackMode { None = 0, AB = 2, Curve = 4, bndTrackOuter = 8, bndTrackInner = 16, bndCurve = 32, waterPivot = 64 };//, Heading, Circle, Spiral

    public class CTrack
    {
        private static readonly TrackNudgingService _coreTrackNudgingService = new TrackNudgingService();

        //pointers to mainform controls
        private readonly FormGPS mf;

        public List<CTrk> gArr = new List<CTrk>();

        public int idx, autoTrack3SecTimer;

        public bool isAutoTrack = false, isAutoSnapToPivot = false, isAutoSnapped;

        public CTrack(FormGPS _f)
        {
            //constructor
            mf = _f;
            idx = -1;
        }

        public int FindClosestRefTrack(vec3 pivot)
        {
            if (idx < 0 || gArr.Count == 0) return -1;

            //only 1 track
            if (gArr.Count == 1) return idx;

            int trak = -1;
            int cntr = 0;

            //Count visible
            for (int i = 0; i < gArr.Count; i++)
            {
                if (gArr[i].isVisible)
                {
                    cntr++;
                    trak = i;
                }
            }

            //only 1 track visible of the group
            if (cntr == 1) return trak;

            //no visible tracks
            if (cntr == 0) return -1;

            //determine if any aligned reasonably close
            bool[] isAlignedArr = new bool[gArr.Count];
            for (int i = 0; i < gArr.Count; i++)
            {
                if (gArr[i].mode == TrackMode.Curve) isAlignedArr[i] = true;
                else
                {
                    double diff = Math.PI - Math.Abs(Math.Abs(pivot.heading - gArr[i].heading) - Math.PI);
                    if (diff < 1 || diff > 2.14)
                        isAlignedArr[i] = true;
                    else
                        isAlignedArr[i] = false;
                }
            }

            double minDistA = double.MaxValue;
            double dist;

            vec2 endPtA, endPtB;

            for (int i = 0; i < gArr.Count; i++)
            {
                if (!isAlignedArr[i]) continue;
                if (!gArr[i].isVisible) continue;

                if (gArr[i].mode == TrackMode.AB)
                {
                    double abHeading = mf.trk.gArr[i].heading;

                    endPtA.easting = mf.trk.gArr[i].ptA.easting - (Math.Sin(abHeading) * 2000);
                    endPtA.northing = mf.trk.gArr[i].ptA.northing - (Math.Cos(abHeading) * 2000);

                    endPtB.easting = mf.trk.gArr[i].ptB.easting + (Math.Sin(abHeading) * 2000);
                    endPtB.northing = mf.trk.gArr[i].ptB.northing + (Math.Cos(abHeading) * 2000);

                    //x2-x1
                    double dx = endPtB.easting - endPtA.easting;
                    //z2-z1
                    double dy = endPtB.northing - endPtA.northing;

                    dist = ((dy * mf.steerAxlePos.easting) - (dx * mf.steerAxlePos.northing) + (endPtB.easting
                                            * endPtA.northing) - (endPtB.northing * endPtA.easting))
                                                / Math.Sqrt((dy * dy) + (dx * dx));

                    dist *= dist;

                    if (dist < minDistA)
                    {
                        minDistA = dist;
                        trak = i;
                    }
                }
                else
                {
                    for (int j = 0; j < gArr[i].curvePts.Count; j++)
                    {

                        dist = glm.DistanceSquared(gArr[i].curvePts[j], pivot);

                        if (dist < minDistA)
                        {
                            minDistA = dist;
                            trak = i;
                        }
                    }
                }
            }

            return trak;
        }

        public void NudgeTrack(double dist)
        {
            if (idx > -1)
            {
                if (gArr[idx].mode == TrackMode.AB)
                {
                    mf.ABLine.isABValid = false;
                    gArr[idx].nudgeDistance += mf.ABLine.isHeadingSameWay ? dist : -dist;
                }
                else
                {
                    mf.curve.isCurveValid = false;
                    gArr[idx].nudgeDistance += mf.curve.isHeadingSameWay ? dist : -dist;

                }

                //if (gArr[idx].nudgeDistance > 0.5 * mf.tool.width) gArr[idx].nudgeDistance -= mf.tool.width;
                //else if (gArr[idx].nudgeDistance < -0.5 * mf.tool.width) gArr[idx].nudgeDistance += mf.tool.width;
            }
        }

        public void NudgeDistanceReset()
        {
            if (idx > -1 && gArr.Count > 0)
            {
                if (gArr[idx].mode == TrackMode.AB)
                {
                    mf.ABLine.isABValid = false;
                }
                else
                {
                    mf.curve.isCurveValid = false;
                }

                gArr[idx].nudgeDistance = 0;
            }
        }

        public void SnapToPivot()
        {
            if (idx > -1)
            {
                NudgeTrack(gArr[idx].mode == TrackMode.AB ? mf.ABLine.distanceFromCurrentLinePivot : mf.curve.distanceFromCurrentLinePivot);
            }
        }

        public void NudgeRefTrack(double dist)
        {
            if (idx > -1)
            {
                if (gArr[idx].mode == TrackMode.AB)
                {
                    mf.ABLine.isABValid = false;
                    NudgeRefABLine(mf.ABLine.isHeadingSameWay ? dist : -dist);
                }
                else
                {
                    mf.curve.isCurveValid = false;
                    NudgeRefCurve(mf.curve.isHeadingSameWay ? dist : -dist);
                }
            }
        }

        public void NudgeRefABLine(double dist)
        {
            // Delegate to Core service for AB line nudging calculation
            var input = new ABLineNudgeInput
            {
                PointA = new Vec2 { Easting = gArr[idx].ptA.easting, Northing = gArr[idx].ptA.northing },
                PointB = new Vec2 { Easting = gArr[idx].ptB.easting, Northing = gArr[idx].ptB.northing },
                Heading = gArr[idx].heading,
                Distance = dist
            };

            var output = _coreTrackNudgingService.NudgeABLine(input);

            gArr[idx].ptA.easting = output.NewPointA.Easting;
            gArr[idx].ptA.northing = output.NewPointA.Northing;
            gArr[idx].ptB.easting = output.NewPointB.Easting;
            gArr[idx].ptB.northing = output.NewPointB.Northing;
        }

        public void NudgeRefCurve(double distAway)
        {
            mf.curve.isCurveValid = false;

            // Delegate to Core service for curve nudging calculation
            var coreCurvePoints = new List<Vec3>(gArr[idx].curvePts.Count);
            for (int i = 0; i < gArr[idx].curvePts.Count; i++)
            {
                coreCurvePoints.Add(new Vec3(
                    gArr[idx].curvePts[i].easting,
                    gArr[idx].curvePts[i].northing,
                    gArr[idx].curvePts[i].heading));
            }

            var input = new CurveNudgeInput
            {
                CurvePoints = coreCurvePoints,
                Distance = distAway
            };

            var output = _coreTrackNudgingService.NudgeCurve(input);

            // Replace curve points with nudged result
            gArr[idx].curvePts.Clear();

            foreach (var item in output.NewCurvePoints)
            {
                gArr[idx].curvePts.Add(new vec3(item.Easting, item.Northing, item.Heading));
            }
        }
    }

    public class CTrk
    {
        public List<vec3> curvePts = new List<vec3>();
        public double heading;
        public string name;
        public bool isVisible;
        public vec2 ptA;
        public vec2 ptB;
        public vec2 endPtA;
        public vec2 endPtB;
        public TrackMode mode;
        public double nudgeDistance;
        public HashSet<int> workedTracks = new HashSet<int>();

        public CTrk()
        {
            curvePts = new List<vec3>();
            heading = 3;
            name = "New Track";
            isVisible = true;
            ptA = new vec2();
            ptB = new vec2();
            endPtA = new vec2();
            endPtB = new vec2();
            mode = TrackMode.None;
            nudgeDistance = 0;
        }

        public CTrk(CTrk _trk)
        {
            curvePts = new List<vec3>(_trk.curvePts);
            heading = _trk.heading;
            name = _trk.name;
            isVisible = _trk.isVisible;
            ptA = _trk.ptA;
            ptB = _trk.ptB;
            endPtA = new vec2();
            endPtB = new vec2();
            mode = _trk.mode;
            nudgeDistance = _trk.nudgeDistance;
        }
    }
}
