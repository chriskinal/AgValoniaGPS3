using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using AgOpenGPS.Core.Models.Base;
using AgOpenGPS.Core.Services;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for tramline generation.
    /// Delegates tramline offset calculation to Core TramlineService.
    /// </summary>
    public class CTram
    {
        private readonly FormGPS mf;
        private readonly TramlineService _coreService;

        //the triangle strip of the outer tram highlight
        public List<vec2> tramBndOuterArr = new List<vec2>();

        public List<vec2> tramBndInnerArr = new List<vec2>();

        //tram settings
        //public double wheelTrack;
        public double tramWidth;

        public double halfWheelTrack, alpha;
        public int passes;
        public bool isOuter;

        public bool isLeftManualOn, isRightManualOn;


        //tramlines
        public List<vec2> tramArr = new List<vec2>();

        public List<List<vec2>> tramList = new List<List<vec2>>();

        // 0 off, 1 All, 2, Lines, 3 Outer
        public int displayMode, generateMode = 0;

        internal int controlByte;

        public CTram(FormGPS _f)
        {
            //constructor
            mf = _f;

            // Create Core tramline service
            _coreService = new TramlineService();

            tramWidth = Properties.Settings.Default.setTram_tramWidth;
            //halfTramWidth = (Math.Round((Properties.Settings.Default.setTram_tramWidth) / 2.0, 3));

            halfWheelTrack = Properties.Settings.Default.setVehicle_trackWidth * 0.5;

            IsTramOuterOrInner();

            passes = Properties.Settings.Default.setTram_passes;
            displayMode = 0;

            alpha = Properties.Settings.Default.setTram_alpha;
        }

        public void IsTramOuterOrInner()
        {
            isOuter = ((int)(tramWidth / mf.tool.width + 0.5)) % 2 == 0;
            if (Properties.Settings.Default.setTool_isTramOuterInverted) isOuter = !isOuter;
        }

        public void DrawTram()
        {
            if (mf.camera.camSetDistance > -500) GL.LineWidth(10);
            else GL.LineWidth(6);

            GL.Color4(0, 0, 0, alpha);

            if (mf.tram.displayMode == 1 || mf.tram.displayMode == 2)
            {
                if (tramList.Count > 0)
                {
                    for (int i = 0; i < tramList.Count; i++)
                    {
                        GL.Begin(PrimitiveType.LineStrip);
                        for (int h = 0; h < tramList[i].Count; h++)
                            GL.Vertex3(tramList[i][h].easting, tramList[i][h].northing, 0);
                        GL.End();
                    }
                }
            }

            if (mf.tram.displayMode == 1 || mf.tram.displayMode == 3)
            {
                if (tramBndOuterArr.Count > 0)
                {
                    GL.Begin(PrimitiveType.LineStrip);
                    for (int h = 0; h < tramBndOuterArr.Count; h++) GL.Vertex3(tramBndOuterArr[h].easting, tramBndOuterArr[h].northing, 0);
                    GL.End();
                    GL.Begin(PrimitiveType.LineStrip);
                    for (int h = 0; h < tramBndInnerArr.Count; h++) GL.Vertex3(tramBndInnerArr[h].easting, tramBndInnerArr[h].northing, 0);
                    GL.End();
                }
            }

            if (mf.camera.camSetDistance > -500) GL.LineWidth(4);
            else GL.LineWidth(2);

            GL.Color4(0.930f, 0.72f, 0.73530f, alpha);

            if (mf.tram.displayMode == 1 || mf.tram.displayMode == 2)
            {
                if (tramList.Count > 0)
                {
                    for (int i = 0; i < tramList.Count; i++)
                    {
                        GL.Begin(PrimitiveType.LineStrip);
                        for (int h = 0; h < tramList[i].Count; h++)
                            GL.Vertex3(tramList[i][h].easting, tramList[i][h].northing, 0);
                        GL.End();
                    }
                }
            }

            if (mf.tram.displayMode == 1 || mf.tram.displayMode == 3)
            {
                if (tramBndOuterArr.Count > 0)
                {
                    GL.Begin(PrimitiveType.LineStrip);
                    for (int h = 0; h < tramBndOuterArr.Count; h++) GL.Vertex3(tramBndOuterArr[h].easting, tramBndOuterArr[h].northing, 0);
                    GL.End();
                    GL.Begin(PrimitiveType.LineStrip);
                    for (int h = 0; h < tramBndInnerArr.Count; h++) GL.Vertex3(tramBndInnerArr[h].easting, tramBndInnerArr[h].northing, 0);
                    GL.End();
                }
            }
        }

        public void BuildTramBnd()
        {
            bool isBndExist = mf.bnd.bndList.Count != 0;

            if (isBndExist)
            {
                CreateBndOuterTramTrack();
                CreateBndInnerTramTrack();
            }
            else
            {
                tramBndOuterArr?.Clear();
                tramBndInnerArr?.Clear();
            }
        }

        private void CreateBndInnerTramTrack()
        {
            tramBndInnerArr?.Clear();

            // Convert WinForms fence line to Core format
            List<Vec3> coreFenceLine = new List<Vec3>(mf.bnd.bndList[0].fenceLine.Count);
            foreach (vec3 point in mf.bnd.bndList[0].fenceLine)
            {
                coreFenceLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            List<Vec2> coreInnerTramline = _coreService.GenerateInnerTramline(coreFenceLine, tramWidth, halfWheelTrack);

            // Convert back to WinForms format
            foreach (Vec2 corePoint in coreInnerTramline)
            {
                tramBndInnerArr.Add(new vec2(corePoint.Easting, corePoint.Northing));
            }
        }

        public void CreateBndOuterTramTrack()
        {
            tramBndOuterArr?.Clear();

            // Convert WinForms fence line to Core format
            List<Vec3> coreFenceLine = new List<Vec3>(mf.bnd.bndList[0].fenceLine.Count);
            foreach (vec3 point in mf.bnd.bndList[0].fenceLine)
            {
                coreFenceLine.Add(new Vec3(point.easting, point.northing, point.heading));
            }

            // Delegate to Core service
            List<Vec2> coreOuterTramline = _coreService.GenerateOuterTramline(coreFenceLine, tramWidth, halfWheelTrack);

            // Convert back to WinForms format
            foreach (Vec2 corePoint in coreOuterTramline)
            {
                tramBndOuterArr.Add(new vec2(corePoint.Easting, corePoint.Northing));
            }
        }
    }
}