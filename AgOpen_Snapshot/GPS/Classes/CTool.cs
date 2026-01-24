using AgOpenGPS.Core.Drawing;
using AgOpenGPS.Core.DrawLib;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.Tool;
using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for ToolConfiguration from AgOpenGPS.Core
    /// Delegates configuration/state to Core, keeps OpenGL rendering in WinForms
    /// </summary>
    public class CTool
    {
        private readonly FormGPS mf;
        private readonly ToolConfiguration _core;

        // Tool dimensions - delegate to Core
        public double width
        {
            get => _core.Width;
            set => _core.Width = value;
        }

        public double halfWidth
        {
            get => _core.HalfWidth;
            set => _core.HalfWidth = value;
        }

        public double contourWidth
        {
            get => _core.ContourWidth;
            set => _core.ContourWidth = value;
        }

        public double overlap
        {
            get => _core.Overlap;
            set => _core.Overlap = value;
        }

        public double offset
        {
            get => _core.Offset;
            set => _core.Offset = value;
        }

        // Position and speed - delegate to Core
        public double farLeftPosition
        {
            get => _core.FarLeftPosition;
            set => _core.FarLeftPosition = value;
        }

        public double farLeftSpeed
        {
            get => _core.FarLeftSpeed;
            set => _core.FarLeftSpeed = value;
        }

        public double farRightPosition
        {
            get => _core.FarRightPosition;
            set => _core.FarRightPosition = value;
        }

        public double farRightSpeed
        {
            get => _core.FarRightSpeed;
            set => _core.FarRightSpeed = value;
        }

        // Hitch configuration - delegate to Core
        public double trailingHitchLength
        {
            get => _core.TrailingHitchLength;
            set => _core.TrailingHitchLength = value;
        }

        public double tankTrailingHitchLength
        {
            get => _core.TankTrailingHitchLength;
            set => _core.TankTrailingHitchLength = value;
        }

        public double trailingToolToPivotLength
        {
            get => _core.TrailingToolToPivotLength;
            set => _core.TrailingToolToPivotLength = value;
        }

        public double hitchLength
        {
            get => _core.HitchLength;
            set => _core.HitchLength = value;
        }

        // Lookahead settings - delegate to Core
        public double lookAheadOffSetting
        {
            get => _core.LookAheadOffSetting;
            set => _core.LookAheadOffSetting = value;
        }

        public double lookAheadOnSetting
        {
            get => _core.LookAheadOnSetting;
            set => _core.LookAheadOnSetting = value;
        }

        public double turnOffDelay
        {
            get => _core.TurnOffDelay;
            set => _core.TurnOffDelay = value;
        }

        public double lookAheadDistanceOnPixelsLeft
        {
            get => _core.LookAheadDistanceOnPixelsLeft;
            set => _core.LookAheadDistanceOnPixelsLeft = value;
        }

        public double lookAheadDistanceOnPixelsRight
        {
            get => _core.LookAheadDistanceOnPixelsRight;
            set => _core.LookAheadDistanceOnPixelsRight = value;
        }

        public double lookAheadDistanceOffPixelsLeft
        {
            get => _core.LookAheadDistanceOffPixelsLeft;
            set => _core.LookAheadDistanceOffPixelsLeft = value;
        }

        public double lookAheadDistanceOffPixelsRight
        {
            get => _core.LookAheadDistanceOffPixelsRight;
            set => _core.LookAheadDistanceOffPixelsRight = value;
        }

        // Tool type flags - delegate to Core
        public bool isToolTrailing
        {
            get => _core.IsToolTrailing;
            set => _core.IsToolTrailing = value;
        }

        public bool isToolTBT
        {
            get => _core.IsToolTBT;
            set => _core.IsToolTBT = value;
        }

        public bool isToolRearFixed
        {
            get => _core.IsToolRearFixed;
            set => _core.IsToolRearFixed = value;
        }

        public bool isToolFrontFixed
        {
            get => _core.IsToolFrontFixed;
            set => _core.IsToolFrontFixed = value;
        }

        // Section configuration - delegate to Core
        public int numOfSections
        {
            get => _core.NumOfSections;
            set => _core.NumOfSections = value;
        }

        public int minCoverage
        {
            get => _core.MinCoverage;
            set => _core.MinCoverage = value;
        }

        public bool isMultiColoredSections
        {
            get => _core.IsMultiColoredSections;
            set => _core.IsMultiColoredSections = value;
        }

        public bool isSectionOffWhenOut
        {
            get => _core.IsSectionOffWhenOut;
            set => _core.IsSectionOffWhenOut = value;
        }

        public bool isSectionsNotZones
        {
            get => _core.IsSectionsNotZones;
            set => _core.IsSectionsNotZones = value;
        }

        // Headland detection - delegate to Core
        public bool isLeftSideInHeadland
        {
            get => _core.IsLeftSideInHeadland;
            set => _core.IsLeftSideInHeadland = value;
        }

        public bool isRightSideInHeadland
        {
            get => _core.IsRightSideInHeadland;
            set => _core.IsRightSideInHeadland = value;
        }

        // Read pixel parameters - delegate to Core
        public int rpXPosition
        {
            get => _core.RpXPosition;
            set => _core.RpXPosition = value;
        }

        public int rpWidth
        {
            get => _core.RpWidth;
            set => _core.RpWidth = value;
        }

        // Zone configuration - delegate to Core
        public int zones
        {
            get => _core.Zones;
            set => _core.Zones = value;
        }

        public int[] zoneRanges
        {
            get => _core.ZoneRanges;
            set => _core.ZoneRanges = value;
        }

        // Display settings - delegate to Core
        public bool isDisplayTramControl
        {
            get => _core.IsDisplayTramControl;
            set => _core.IsDisplayTramControl = value;
        }

        // Section colors - delegate to Core with System.Drawing.Color conversion
        public Color[] secColors
        {
            get
            {
                Color[] colors = new Color[16];
                for (int i = 0; i < 16; i++)
                {
                    colors[i] = (Color)_core.SectionColors[i];  // Explicit conversion from ColorRgb
                }
                return colors;
            }
            set
            {
                for (int i = 0; i < 16; i++)
                {
                    if (i < value.Length)
                    {
                        _core.SectionColors[i] = new ColorRgb(value[i].R, value[i].G, value[i].B);
                    }
                }
            }
        }

        // WinForms-specific rendering state (not in Core)
        private double textRotate;

        /// <summary>
        /// Constructor - initializes Core instance and loads configuration from Settings
        /// </summary>
        public CTool(FormGPS _f)
        {
            mf = _f;
            _core = new ToolConfiguration();

            // Load configuration from WinForms Settings into Core
            _core.TrailingToolToPivotLength = Properties.Settings.Default.setTool_trailingToolToPivotLength;
            _core.Width = Properties.Settings.Default.setVehicle_toolWidth;
            _core.Overlap = Properties.Settings.Default.setVehicle_toolOverlap;
            _core.Offset = Properties.Settings.Default.setVehicle_toolOffset;

            _core.TrailingHitchLength = Properties.Settings.Default.setTool_toolTrailingHitchLength;
            _core.TankTrailingHitchLength = Properties.Settings.Default.setVehicle_tankTrailingHitchLength;
            _core.HitchLength = Properties.Settings.Default.setVehicle_hitchLength;

            _core.IsToolRearFixed = Properties.Settings.Default.setTool_isToolRearFixed;
            _core.IsToolTrailing = Properties.Settings.Default.setTool_isToolTrailing;
            _core.IsToolTBT = Properties.Settings.Default.setTool_isToolTBT;
            _core.IsToolFrontFixed = Properties.Settings.Default.setTool_isToolFront;

            _core.LookAheadOnSetting = Properties.Settings.Default.setVehicle_toolLookAheadOn;
            _core.LookAheadOffSetting = Properties.Settings.Default.setVehicle_toolLookAheadOff;
            _core.TurnOffDelay = Properties.Settings.Default.setVehicle_toolOffDelay;

            _core.IsSectionOffWhenOut = Properties.Settings.Default.setTool_isSectionOffWhenOut;
            _core.IsSectionsNotZones = Properties.Settings.Default.setTool_isSectionsNotZones;

            if (_core.IsSectionsNotZones)
                _core.NumOfSections = Properties.Settings.Default.setVehicle_numSections;
            else
                _core.NumOfSections = Properties.Settings.Default.setTool_numSectionsMulti;

            _core.MinCoverage = Properties.Settings.Default.setVehicle_minCoverage;
            _core.IsMultiColoredSections = Properties.Settings.Default.setColor_isMultiColorSections;

            // Load section colors
            _core.SectionColors[0] = new ColorRgb(Properties.Settings.Default.setColor_sec01.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec01.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec01.CheckColorFor255().B);
            _core.SectionColors[1] = new ColorRgb(Properties.Settings.Default.setColor_sec02.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec02.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec02.CheckColorFor255().B);
            _core.SectionColors[2] = new ColorRgb(Properties.Settings.Default.setColor_sec03.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec03.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec03.CheckColorFor255().B);
            _core.SectionColors[3] = new ColorRgb(Properties.Settings.Default.setColor_sec04.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec04.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec04.CheckColorFor255().B);
            _core.SectionColors[4] = new ColorRgb(Properties.Settings.Default.setColor_sec05.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec05.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec05.CheckColorFor255().B);
            _core.SectionColors[5] = new ColorRgb(Properties.Settings.Default.setColor_sec06.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec06.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec06.CheckColorFor255().B);
            _core.SectionColors[6] = new ColorRgb(Properties.Settings.Default.setColor_sec07.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec07.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec07.CheckColorFor255().B);
            _core.SectionColors[7] = new ColorRgb(Properties.Settings.Default.setColor_sec08.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec08.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec08.CheckColorFor255().B);
            _core.SectionColors[8] = new ColorRgb(Properties.Settings.Default.setColor_sec09.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec09.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec09.CheckColorFor255().B);
            _core.SectionColors[9] = new ColorRgb(Properties.Settings.Default.setColor_sec10.CheckColorFor255().R,
                                                   Properties.Settings.Default.setColor_sec10.CheckColorFor255().G,
                                                   Properties.Settings.Default.setColor_sec10.CheckColorFor255().B);
            _core.SectionColors[10] = new ColorRgb(Properties.Settings.Default.setColor_sec11.CheckColorFor255().R,
                                                    Properties.Settings.Default.setColor_sec11.CheckColorFor255().G,
                                                    Properties.Settings.Default.setColor_sec11.CheckColorFor255().B);
            _core.SectionColors[11] = new ColorRgb(Properties.Settings.Default.setColor_sec12.CheckColorFor255().R,
                                                    Properties.Settings.Default.setColor_sec12.CheckColorFor255().G,
                                                    Properties.Settings.Default.setColor_sec12.CheckColorFor255().B);
            _core.SectionColors[12] = new ColorRgb(Properties.Settings.Default.setColor_sec13.CheckColorFor255().R,
                                                    Properties.Settings.Default.setColor_sec13.CheckColorFor255().G,
                                                    Properties.Settings.Default.setColor_sec13.CheckColorFor255().B);
            _core.SectionColors[13] = new ColorRgb(Properties.Settings.Default.setColor_sec14.CheckColorFor255().R,
                                                    Properties.Settings.Default.setColor_sec14.CheckColorFor255().G,
                                                    Properties.Settings.Default.setColor_sec14.CheckColorFor255().B);
            _core.SectionColors[14] = new ColorRgb(Properties.Settings.Default.setColor_sec15.CheckColorFor255().R,
                                                    Properties.Settings.Default.setColor_sec15.CheckColorFor255().G,
                                                    Properties.Settings.Default.setColor_sec15.CheckColorFor255().B);
            _core.SectionColors[15] = new ColorRgb(Properties.Settings.Default.setColor_sec16.CheckColorFor255().R,
                                                    Properties.Settings.Default.setColor_sec16.CheckColorFor255().G,
                                                    Properties.Settings.Default.setColor_sec16.CheckColorFor255().B);

            // Load zone configuration
            string[] words = Properties.Settings.Default.setTool_zones.Split(',');
            _core.Zones = int.Parse(words[0]);

            for (int i = 0; i < words.Length; i++)
            {
                _core.ZoneRanges[i] = int.Parse(words[i]);
            }

            _core.IsDisplayTramControl = Properties.Settings.Default.setTool_isDisplayTramControl;
        }

        /// <summary>
        /// Get the underlying Core ToolConfiguration instance
        /// </summary>
        public ToolConfiguration CoreToolConfiguration => _core;

        // ========== OpenGL Rendering Methods (WinForms-specific) ==========

        private void DrawHitch(double trailingTank)
        {
            XyCoord[] vertices = {
                new XyCoord(-0.57, trailingTank),
                new XyCoord(0.0, 0.0),
                new XyCoord(0.57, trailingTank)
            };
            LineStyle backgroundLineStyle = new LineStyle(6.0f, Colors.Black);
            LineStyle foregroundLineStyle = new LineStyle(1.0f, Colors.HitchColor);
            LineStyle[] lineStyles = { backgroundLineStyle, foregroundLineStyle };
            GLW.DrawLineLoopPrimitiveLayered(lineStyles, vertices);
        }

        private void DrawTrailingHitch(double trailingTool)
        {
            XyCoord[] vertices = {
                new XyCoord(-0.65 + mf.tool.offset, trailingTool),
                new XyCoord(0.0, 0.0),
                new XyCoord(0.65 + mf.tool.offset, trailingTool)
            };
            LineStyle backgroundLineStyle = new LineStyle(6.0f, Colors.Black);
            LineStyle foregroundLineStyle = new LineStyle(1.0f, Colors.HitchTrailingColor);
            LineStyle[] lineStyles = { backgroundLineStyle, foregroundLineStyle };
            GLW.DrawLineLoopPrimitiveLayered(lineStyles, vertices);
        }

        public void DrawTool()
        {
            //translate and rotate at pivot axle
            GL.Translate(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0);
            GL.PushMatrix();

            //translate down to the hitch pin
            GL.Translate(Math.Sin(mf.fixHeading) * (hitchLength),
                            Math.Cos(mf.fixHeading) * (hitchLength), 0);

            //settings doesn't change trailing hitch length if set to rigid, so do it here
            double trailingTank, trailingTool;
            if (isToolTrailing)
            {
                trailingTank = tankTrailingHitchLength;
                trailingTool = trailingHitchLength;
            }
            else { trailingTank = 0; trailingTool = 0; }

            // if there is a trailing tow between hitch
            if (isToolTBT && isToolTrailing)
            {
                //rotate to tank heading
                GL.Rotate(glm.toDegrees(-mf.tankPos.heading), 0.0, 0.0, 1.0);

                DrawHitch(trailingTank);

                GL.Color4(1, 1, 1, 0.75);
                XyCoord toolAxleCenter = new XyCoord(0.0, trailingTank);
                XyDelta deltaToU1V1 = new XyDelta(1.5, 1.0);
                mf.VehicleTextures.ToolAxle.DrawCentered(toolAxleCenter, deltaToU1V1);

                //move down the tank hitch, unwind, rotate to section heading
                GL.Translate(0.0, trailingTank, 0.0);
                GL.Rotate(glm.toDegrees(mf.tankPos.heading), 0.0, 0.0, 1.0);
            }
            GL.Rotate(glm.toDegrees(-mf.toolPivotPos.heading), 0.0, 0.0, 1.0);

            //draw the hitch if trailing
            if (isToolTrailing)
            {
                DrawTrailingHitch(trailingTool);

                if (Math.Abs(trailingToolToPivotLength) > 1 && mf.camera.camSetDistance > -100)
                {
                    textRotate += (mf.sim.stepDistance);
                    GL.Color4(1, 1, 1, 0.75);
                    XyCoord rightTire00 = new XyCoord(0.75 + offset, trailingTool + 0.51);
                    XyCoord rightTire11 = new XyCoord(1.4 + offset, trailingTool - 0.51);
                    XyCoord leftTire00 = new XyCoord(-0.75 + offset, trailingTool + 0.51);
                    XyCoord lefttTire11 = new XyCoord(-1.4 + offset, trailingTool - 0.51);
                    mf.VehicleTextures.Tire.Draw(rightTire00, rightTire11);
                    mf.VehicleTextures.Tire.Draw(leftTire00, lefttTire11);
                }
                trailingTool -= trailingToolToPivotLength;
            }

            if (mf.isJobStarted)
            {
                //look ahead lines
                GL.LineWidth(3);
                GL.Begin(PrimitiveType.Lines);

                //lookahead section on
                GL.Color3(0.20f, 0.7f, 0.2f);
                GL.Vertex3(mf.tool.farLeftPosition, (mf.tool.lookAheadDistanceOnPixelsLeft) * 0.1 + trailingTool, 0);
                GL.Vertex3(mf.tool.farRightPosition, (mf.tool.lookAheadDistanceOnPixelsRight) * 0.1 + trailingTool, 0);

                //lookahead section off
                GL.Color3(0.70f, 0.2f, 0.2f);
                GL.Vertex3(mf.tool.farLeftPosition, (mf.tool.lookAheadDistanceOffPixelsLeft) * 0.1 + trailingTool, 0);
                GL.Vertex3(mf.tool.farRightPosition, (mf.tool.lookAheadDistanceOffPixelsRight) * 0.1 + trailingTool, 0);

                if (mf.vehicle.isHydLiftOn)
                {
                    GL.Color3(0.70f, 0.2f, 0.72f);
                    GL.Vertex3(mf.section[0].positionLeft, (mf.vehicle.hydLiftLookAheadDistanceLeft * 0.1) + trailingTool, 0);
                    GL.Vertex3(mf.section[mf.tool.numOfSections - 1].positionRight, (mf.vehicle.hydLiftLookAheadDistanceRight * 0.1) + trailingTool, 0);
                }

                GL.End();
            }

            //draw the sections
            GL.LineWidth(2);

            double hite = mf.camera.camSetDistance / -250;
            if (hite > 4) hite = 4;
            if (hite < 1) hite = 1;

            //TooDoo
            //hite = 0.2;

            for (int j = 0; j < numOfSections; j++)
            {
                //if section is on, green, if off, red color
                if (mf.section[j].isSectionOn)
                {
                    if (mf.section[j].sectionBtnState == btnStates.Auto)
                    {
                        //GL.Color3(0.0f, 0.9f, 0.0f);
                        if (mf.section[j].isMappingOn) GL.Color3(0.0f, 0.95f, 0.0f);
                        else GL.Color3(0.970f, 0.30f, 0.970f);
                    }
                    else GL.Color3(0.97, 0.97, 0);
                }
                else
                {
                    if (!mf.section[j].isMappingOn) GL.Color3(0.950f, 0.2f, 0.2f);
                    else GL.Color3(0.00f, 0.250f, 0.97f);
                    //GL.Color3(0.7f, 0.2f, 0.2f);
                }

                double mid = (mf.section[j].positionRight - mf.section[j].positionLeft) / 2 + mf.section[j].positionLeft;
                XyCoord[] vertices = {
                    new XyCoord(mf.section[j].positionLeft, trailingTool),
                    new XyCoord(mf.section[j].positionLeft, trailingTool - hite),
                    new XyCoord(mid, trailingTool - hite * 1.5),
                    new XyCoord(mf.section[j].positionRight, trailingTool - hite),
                    new XyCoord(mf.section[j].positionRight, trailingTool),
                };
                GLW.DrawTriangleFanPrimitive(vertices);

                if (mf.camera.camSetDistance > -width * 200)
                {
                    GLW.SetColor(Colors.Black);
                    GLW.DrawLineLoopPrimitive(vertices);
                }
            }

            //zones
            if (!isSectionsNotZones && zones > 0 && mf.camera.camSetDistance > -150)
            {
                //GL.PointSize(8);

                GL.Begin(PrimitiveType.Lines);
                for (int i = 1; i < zones; i++)
                {
                    GL.Color3(0.5f, 0.80f, 0.950f);
                    GL.Vertex3(mf.section[zoneRanges[i]].positionLeft, trailingTool - 0.4, 0);
                    GL.Vertex3(mf.section[zoneRanges[i]].positionLeft, trailingTool + 0.2, 0);
                }

                GL.End();
            }

            //tram Dots
            if (isDisplayTramControl && mf.tram.displayMode != 0)
            {
                if (mf.camera.camSetDistance > -300)
                {
                    if (mf.camera.camSetDistance > -100)
                        GL.PointSize(12);
                    else GL.PointSize(8);

                    ColorRgb rightMarkerColor = ((mf.tram.controlByte) & 1) != 0 ? Colors.TramMarkerOnColor : Colors.Black;
                    ColorRgb leftMarkerColor = ((mf.tram.controlByte) & 2) != 0 ? Colors.TramMarkerOnColor : Colors.Black;
                    double rightX = mf.tram.isOuter ? farRightPosition - mf.tram.halfWheelTrack : mf.tram.halfWheelTrack;
                    double leftX = mf.tram.isOuter ? farLeftPosition + mf.tram.halfWheelTrack : -mf.tram.halfWheelTrack;
                    // section markers
                    GL.Begin(PrimitiveType.Points);
                    GLW.SetColor(rightMarkerColor);
                    GL.Vertex3(rightX, trailingTool, 0);
                    GLW.SetColor(leftMarkerColor);
                    GL.Vertex3(leftX, trailingTool, 0);
                    GL.End();
                }
            }

            GL.PopMatrix();
        }
    }
}
