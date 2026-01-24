using AgOpenGPS.Core.Drawing;
using AgOpenGPS.Core.DrawLib;
using AgOpenGPS.Core.Models;
using OpenTK.Graphics.OpenGL;
using System;

//Please, if you use this, share the improvements

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for VehicleConfig from AgOpenGPS.Core
    /// Delegates configuration/state to Core, keeps OpenGL rendering in WinForms
    /// </summary>
    public class CVehicle
    {
        private readonly FormGPS mf;

        // Dead zone configuration - delegate to Core
        public int deadZoneHeading
        {
            get => VehicleConfig.DeadZoneHeading;
            set => VehicleConfig.DeadZoneHeading = value;
        }

        public int deadZoneDelay
        {
            get => VehicleConfig.DeadZoneDelay;
            set => VehicleConfig.DeadZoneDelay = value;
        }

        public int deadZoneDelayCounter
        {
            get => VehicleConfig.DeadZoneDelayCounter;
            set => VehicleConfig.DeadZoneDelayCounter = value;
        }

        public bool isInDeadZone
        {
            get => VehicleConfig.IsInDeadZone;
            set => VehicleConfig.IsInDeadZone = value;
        }

        // Speed configuration - delegate to Core
        public double slowSpeedCutoff
        {
            get => VehicleConfig.SlowSpeedCutoff;
            set => VehicleConfig.SlowSpeedCutoff = value;
        }

        public double functionSpeedLimit
        {
            get => VehicleConfig.FunctionSpeedLimit;
            set => VehicleConfig.FunctionSpeedLimit = value;
        }

        // Goal point lookahead - delegate to Core
        public double goalPointLookAheadHold
        {
            get => VehicleConfig.GoalPointLookAheadHold;
            set => VehicleConfig.GoalPointLookAheadHold = value;
        }

        public double goalPointLookAheadMult
        {
            get => VehicleConfig.GoalPointLookAheadMult;
            set => VehicleConfig.GoalPointLookAheadMult = value;
        }

        public double goalPointAcquireFactor
        {
            get => VehicleConfig.GoalPointAcquireFactor;
            set => VehicleConfig.GoalPointAcquireFactor = value;
        }

        public double uturnCompensation
        {
            get => VehicleConfig.UturnCompensation;
            set => VehicleConfig.UturnCompensation = value;
        }

        // Stanley controller - delegate to Core
        public double stanleyDistanceErrorGain
        {
            get => VehicleConfig.StanleyDistanceErrorGain;
            set => VehicleConfig.StanleyDistanceErrorGain = value;
        }

        public double stanleyHeadingErrorGain
        {
            get => VehicleConfig.StanleyHeadingErrorGain;
            set => VehicleConfig.StanleyHeadingErrorGain = value;
        }

        public double stanleyIntegralGainAB
        {
            get => VehicleConfig.StanleyIntegralGainAB;
            set => VehicleConfig.StanleyIntegralGainAB = value;
        }

        // Pure pursuit controller - delegate to Core
        public double purePursuitIntegralGain
        {
            get => VehicleConfig.PurePursuitIntegralGain;
            set => VehicleConfig.PurePursuitIntegralGain = value;
        }

        // Steering limits - delegate to Core
        public double maxSteerAngle
        {
            get => VehicleConfig.MaxSteerAngle;
            set => VehicleConfig.MaxSteerAngle = value;
        }

        public double maxSteerSpeed
        {
            get => VehicleConfig.MaxSteerSpeed;
            set => VehicleConfig.MaxSteerSpeed = value;
        }

        public double minSteerSpeed
        {
            get => VehicleConfig.MinSteerSpeed;
            set => VehicleConfig.MinSteerSpeed = value;
        }

        public double maxAngularVelocity
        {
            get => VehicleConfig.MaxAngularVelocity;
            set => VehicleConfig.MaxAngularVelocity = value;
        }

        // Hydraulic lift - delegate to Core
        public bool isHydLiftOn
        {
            get => VehicleConfig.IsHydLiftOn;
            set => VehicleConfig.IsHydLiftOn = value;
        }

        public double hydLiftLookAheadTime
        {
            get => VehicleConfig.HydLiftLookAheadTime;
            set => VehicleConfig.HydLiftLookAheadTime = value;
        }

        public double hydLiftLookAheadDistanceLeft
        {
            get => VehicleConfig.HydLiftLookAheadDistanceLeft;
            set => VehicleConfig.HydLiftLookAheadDistanceLeft = value;
        }

        public double hydLiftLookAheadDistanceRight
        {
            get => VehicleConfig.HydLiftLookAheadDistanceRight;
            set => VehicleConfig.HydLiftLookAheadDistanceRight = value;
        }

        // Free drive mode - delegate to Core
        public bool isInFreeDriveMode
        {
            get => VehicleConfig.IsInFreeDriveMode;
            set => VehicleConfig.IsInFreeDriveMode = value;
        }

        public double driveFreeSteerAngle
        {
            get => VehicleConfig.DriveFreeSteerAngle;
            set => VehicleConfig.DriveFreeSteerAngle = value;
        }

        // Mode tracking - delegate to Core
        public double modeXTE
        {
            get => VehicleConfig.ModeXTE;
            set => VehicleConfig.ModeXTE = value;
        }

        public double modeActualXTE
        {
            get => VehicleConfig.ModeActualXTE;
            set => VehicleConfig.ModeActualXTE = value;
        }

        public double modeActualHeadingError
        {
            get => VehicleConfig.ModeActualHeadingError;
            set => VehicleConfig.ModeActualHeadingError = value;
        }

        public int modeTime
        {
            get => VehicleConfig.ModeTime;
            set => VehicleConfig.ModeTime = value;
        }

        public int modeTimeCounter
        {
            get => VehicleConfig.ModeTimeCounter;
            set => VehicleConfig.ModeTimeCounter = value;
        }

        // Goal distance - delegate to Core
        public double goalDistance
        {
            get => VehicleConfig.GoalDistance;
            set => VehicleConfig.GoalDistance = value;
        }

        /// <summary>
        /// Core VehicleConfig instance - ALL state stored here
        /// </summary>
        public VehicleConfig VehicleConfig { get; }

        /// <summary>
        /// Constructor - initializes Core VehicleConfig and loads settings
        /// </summary>
        public CVehicle(FormGPS _f)
        {
            mf = _f;

            VehicleConfig = new VehicleConfig();

            // Load antenna configuration
            VehicleConfig.AntennaHeight = Properties.Settings.Default.setVehicle_antennaHeight;
            VehicleConfig.AntennaPivot = Properties.Settings.Default.setVehicle_antennaPivot;
            VehicleConfig.AntennaOffset = Properties.Settings.Default.setVehicle_antennaOffset;

            // Load vehicle dimensions
            VehicleConfig.Wheelbase = Properties.Settings.Default.setVehicle_wheelbase;
            VehicleConfig.TrackWidth = Properties.Settings.Default.setVehicle_trackWidth;

            // Load speed configuration
            VehicleConfig.SlowSpeedCutoff = Properties.Settings.Default.setVehicle_slowSpeedCutoff;
            VehicleConfig.FunctionSpeedLimit = Properties.Settings.Default.setAS_functionSpeedLimit;

            // Load goal point lookahead
            VehicleConfig.GoalPointLookAheadHold = Properties.Settings.Default.setVehicle_goalPointLookAheadHold;
            VehicleConfig.GoalPointLookAheadMult = Properties.Settings.Default.setVehicle_goalPointLookAheadMult;
            VehicleConfig.GoalPointAcquireFactor = Properties.Settings.Default.setVehicle_goalPointAcquireFactor;
            VehicleConfig.UturnCompensation = Properties.Settings.Default.setAS_uTurnCompensation;

            // Load Stanley controller gains
            VehicleConfig.StanleyDistanceErrorGain = Properties.Settings.Default.stanleyDistanceErrorGain;
            VehicleConfig.StanleyHeadingErrorGain = Properties.Settings.Default.stanleyHeadingErrorGain;
            VehicleConfig.StanleyIntegralGainAB = Properties.Settings.Default.stanleyIntegralGainAB;

            // Load pure pursuit controller
            VehicleConfig.PurePursuitIntegralGain = Properties.Settings.Default.purePursuitIntegralGainAB;

            // Load steering limits
            VehicleConfig.MaxAngularVelocity = Properties.Settings.Default.setVehicle_maxAngularVelocity;
            VehicleConfig.MaxSteerAngle = Properties.Settings.Default.setVehicle_maxSteerAngle;
            VehicleConfig.MaxSteerSpeed = Properties.Settings.Default.setAS_maxSteerSpeed;
            VehicleConfig.MinSteerSpeed = Properties.Settings.Default.setAS_minSteerSpeed;

            // Load hydraulic lift configuration
            VehicleConfig.IsHydLiftOn = false;
            VehicleConfig.HydLiftLookAheadTime = Properties.Settings.Default.setVehicle_hydraulicLiftLookAhead;

            // Load dead zone configuration
            VehicleConfig.DeadZoneHeading = Properties.Settings.Default.setAS_deadZoneHeading;
            VehicleConfig.DeadZoneDelay = Properties.Settings.Default.setAS_deadZoneDelay;

            // Load vehicle type
            VehicleConfig.Type = (VehicleType)Properties.Settings.Default.setVehicle_vehicleType;

            // Initialize free drive mode
            VehicleConfig.IsInFreeDriveMode = false;

            // Initialize mode tracking
            VehicleConfig.ModeXTE = 0.2;  // how far from line before it becomes Hold
            VehicleConfig.ModeTime = 1;    // how long before hold is activated
        }

        /// <summary>
        /// Calculate goal point distance based on speed and cross track error
        /// Business logic method - could potentially move to Core later
        /// </summary>
        public double UpdateGoalPointDistance()
        {
            double xTE = Math.Abs(modeActualXTE);
            double goalPointDistance = mf.avgSpeed * 0.05 * goalPointLookAheadMult;

            double LoekiAheadHold = goalPointLookAheadHold;
            double LoekiAheadAcquire = goalPointLookAheadHold * goalPointAcquireFactor;

            if (xTE <= 0.1)
            {
                goalPointDistance *= LoekiAheadHold;
                goalPointDistance += LoekiAheadHold;
            }

            else if (xTE > 0.1 && xTE < 0.4)
            {
                xTE -= 0.1;

                LoekiAheadHold = (1 - (xTE / 0.3)) * (LoekiAheadHold - LoekiAheadAcquire);
                LoekiAheadHold += LoekiAheadAcquire;

                goalPointDistance *= LoekiAheadHold;
                goalPointDistance += LoekiAheadHold;
            }
            else
            {
                goalPointDistance *= LoekiAheadAcquire;
                goalPointDistance += LoekiAheadAcquire;
            }

            if (goalPointDistance < 2) goalPointDistance = 2;
            goalDistance = goalPointDistance;

            return goalPointDistance;
        }

        // ========== OpenGL Rendering Methods (WinForms-specific) ==========

        public void DrawVehicle()
        {
            GL.Rotate(glm.toDegrees(-mf.fixHeading), 0.0, 0.0, 1.0);
            //mf.font.DrawText3D(0, 0, "&TGF");
            if (mf.isFirstHeadingSet && !mf.tool.isToolFrontFixed)
            {
                // Draw the rigid hitch
                XyCoord[] vertices;
                if (!mf.tool.isToolRearFixed)
                {
                    vertices = new XyCoord[] {
                        new XyCoord(0, mf.tool.hitchLength), new XyCoord(0, 0)
                    };
                }
                else
                {
                    vertices = new XyCoord[] {
                        new XyCoord(-0.35, mf.tool.hitchLength), new XyCoord(-0.35, 0),
                        new XyCoord( 0.35, mf.tool.hitchLength), new XyCoord( 0.35, 0)
                    };
                }
                LineStyle backgroundLineStyle = new LineStyle(4, Colors.Black);
                LineStyle foregroundLineStyle = new LineStyle(1, Colors.HitchRigidColor);
                LineStyle[] layerStyles = { backgroundLineStyle, foregroundLineStyle };
                GLW.DrawLinesPrimitiveLayered(layerStyles, vertices);
            }

            //draw the vehicle Body
            if (!mf.isFirstHeadingSet && mf.headingFromSource != "Dual")
            {
                GL.Color4(1, 1, 1, 0.75);
                mf.ScreenTextures.QuestionMark.Draw(new XyCoord(1.0, 5.0), new XyCoord(5.0, 1.0));
            }

            //3 vehicle types  tractor=0 harvestor=1 Articulated=2
            ColorRgba vehicleColor = new ColorRgba(VehicleConfig.Color, (float)VehicleConfig.Opacity);
            if (VehicleConfig.IsImage)
            {
                if (VehicleConfig.Type == VehicleType.Tractor)
                {
                    //vehicle body
                    GLW.SetColor(vehicleColor);

                    AckermannAngles(
                        -(mf.timerSim.Enabled ? mf.sim.steerangleAve : mf.mc.actualSteerAngleDegrees),
                        out double leftAckermann,
                        out double rightAckermann);
                    XyCoord tractorCenter = new XyCoord(0.0, 0.5 * VehicleConfig.Wheelbase);
                    mf.VehicleTextures.Tractor.DrawCentered(
                        tractorCenter,
                        new XyDelta(VehicleConfig.TrackWidth, -1.0 * VehicleConfig.Wheelbase));

                    //right wheel
                    GL.PushMatrix();
                    GL.Translate(0.5 * VehicleConfig.TrackWidth, VehicleConfig.Wheelbase, 0);
                    GL.Rotate(rightAckermann, 0, 0, 1);

                    XyDelta frontWheelDelta = new XyDelta(0.5 * VehicleConfig.TrackWidth, -0.75 * VehicleConfig.Wheelbase);
                    mf.VehicleTextures.FrontWheel.DrawCenteredAroundOrigin(frontWheelDelta);

                    GL.PopMatrix();

                    //Left Wheel
                    GL.PushMatrix();

                    GL.Translate(-VehicleConfig.TrackWidth * 0.5, VehicleConfig.Wheelbase, 0);
                    GL.Rotate(leftAckermann, 0, 0, 1);

                    mf.VehicleTextures.FrontWheel.DrawCenteredAroundOrigin(frontWheelDelta);

                    GL.PopMatrix();
                    //disable, straight color
                }
                else if (VehicleConfig.Type == VehicleType.Harvester)
                {
                    //vehicle body

                    AckermannAngles(
                        mf.timerSim.Enabled ? mf.sim.steerAngle : mf.mc.actualSteerAngleDegrees,
                        out double leftAckermannAngle,
                        out double rightAckermannAngle);
                    ColorRgba harvesterWheelColor = new ColorRgba(Colors.HarvesterWheelColor, (float)VehicleConfig.Opacity);
                    GLW.SetColor(harvesterWheelColor);
                    //right wheel
                    GL.PushMatrix();
                    GL.Translate(VehicleConfig.TrackWidth * 0.5, -VehicleConfig.Wheelbase, 0);
                    GL.Rotate(rightAckermannAngle, 0, 0, 1);
                    XyDelta forntWheelDelta = new XyDelta(0.25 * VehicleConfig.TrackWidth, 0.5 * VehicleConfig.Wheelbase);
                    mf.VehicleTextures.FrontWheel.DrawCenteredAroundOrigin(forntWheelDelta);
                    GL.PopMatrix();

                    //Left Wheel
                    GL.PushMatrix();
                    GL.Translate(-VehicleConfig.TrackWidth * 0.5, -VehicleConfig.Wheelbase, 0);
                    GL.Rotate(leftAckermannAngle, 0, 0, 1);
                    mf.VehicleTextures.FrontWheel.DrawCenteredAroundOrigin(forntWheelDelta);
                    GL.PopMatrix();

                    GLW.SetColor(vehicleColor);
                    mf.VehicleTextures.Harvester.DrawCenteredAroundOrigin(
                        new XyDelta(VehicleConfig.TrackWidth, -1.5 * VehicleConfig.Wheelbase));
                    //disable, straight color
                }
                else if (VehicleConfig.Type == VehicleType.Articulated)
                {
                    double modelSteerAngle = 0.5 * (mf.timerSim.Enabled ? mf.sim.steerAngle : mf.mc.actualSteerAngleDegrees);
                    GLW.SetColor(vehicleColor);

                    XyDelta articulated = new XyDelta(VehicleConfig.TrackWidth, -0.65 * VehicleConfig.Wheelbase);
                    GL.PushMatrix();
                    GL.Translate(0, -VehicleConfig.Wheelbase * 0.5, 0);
                    GL.Rotate(modelSteerAngle, 0, 0, 1);
                    mf.VehicleTextures.ArticulatedRear.DrawCenteredAroundOrigin(articulated);
                    GL.PopMatrix();

                    GL.PushMatrix();
                    GL.Translate(0, VehicleConfig.Wheelbase * 0.5, 0);
                    GL.Rotate(-modelSteerAngle, 0, 0, 1);
                    mf.VehicleTextures.ArticulatedFront.DrawCenteredAroundOrigin(articulated);
                    GL.PopMatrix();
                }
            }
            else
            {
                GL.Color4(1.2, 1.20, 0.0, VehicleConfig.Opacity);
                GL.Begin(PrimitiveType.TriangleFan);
                GL.Vertex3(0, VehicleConfig.AntennaPivot, -0.0);
                GL.Vertex3(1.0, -0, 0.0);
                GL.Color4(0.0, 1.20, 1.22, VehicleConfig.Opacity);
                GL.Vertex3(0, VehicleConfig.Wheelbase, 0.0);
                GL.Color4(1.220, 0.0, 1.2, VehicleConfig.Opacity);
                GL.Vertex3(-1.0, -0, 0.0);
                GL.Vertex3(1.0, -0, 0.0);
                GL.End();

                GL.LineWidth(3);
                GL.Color3(0.12, 0.12, 0.12);
                GL.Begin(PrimitiveType.LineLoop);
                {
                    GL.Vertex3(-1.0, 0, 0);
                    GL.Vertex3(1.0, 0, 0);
                    GL.Vertex3(0, VehicleConfig.Wheelbase, 0);
                }
                GL.End();
            }
            if (mf.camera.camSetDistance > -75 && mf.isFirstHeadingSet)
            {
                //draw the bright antenna dot
                PointStyle antennaBackgroundStyle = new PointStyle(16, Colors.Black);
                PointStyle antennaForegroundStyle = new PointStyle(10, Colors.AntennaColor);
                PointStyle[] layerStyles = { antennaBackgroundStyle, antennaForegroundStyle };
                GLW.DrawPointLayered(layerStyles, -VehicleConfig.AntennaOffset, VehicleConfig.AntennaPivot, 0.1);
            }

            if (mf.bnd.isBndBeingMade && mf.bnd.isDrawAtPivot)
            {
                if (mf.bnd.isDrawRightSide)
                {
                    GL.LineWidth(2);
                    GL.Color3(0.0, 1.270, 0.0);
                    GL.Begin(PrimitiveType.LineStrip);
                    {
                        GL.Vertex3(0.0, 0, 0);
                        GL.Color3(1.270, 1.220, 0.20);
                        GL.Vertex3(mf.bnd.createBndOffset, 0, 0);
                        GL.Vertex3(mf.bnd.createBndOffset * 0.75, 0.25, 0);
                    }
                    GL.End();
                }
                //draw on left side
                else
                {
                    GL.LineWidth(2);
                    GL.Color3(0.0, 1.270, 0.0);
                    GL.Begin(PrimitiveType.LineStrip);
                    {
                        GL.Vertex3(0.0, 0, 0);
                        GL.Color3(1.270, 1.220, 0.20);
                        GL.Vertex3(-mf.bnd.createBndOffset, 0, 0);
                        GL.Vertex3(-mf.bnd.createBndOffset * 0.75, 0.25, 0);
                    }
                    GL.End();
                }
            }

            //Svenn Arrow
            if (mf.isSvennArrowOn && mf.camera.camSetDistance > -1000)
            {
                //double offs = mf.curve.distanceFromCurrentLinePivot * 0.3;
                double svennDist = mf.camera.camSetDistance * -0.07;
                double svennWidth = svennDist * 0.22;
                LineStyle svenArrowLineStyle = new LineStyle(mf.ABLine.lineWidth, Colors.SvenArrowColor);
                GLW.SetLineStyle(svenArrowLineStyle);
                XyCoord[] vertices = {
                    new XyCoord(svennWidth, VehicleConfig.Wheelbase + svennDist),
                    new XyCoord(0, VehicleConfig.Wheelbase + svennWidth + 0.5 + svennDist),
                    new XyCoord(-svennWidth, VehicleConfig.Wheelbase + svennDist)
                };
                GLW.DrawLineStripPrimitive(vertices);
            }
            GL.LineWidth(1);
        }

        private void AckermannAngles(double wheelAngle, out double leftAckermannAngle, out double rightAckermannAngle)
        {
            leftAckermannAngle = wheelAngle;
            rightAckermannAngle = wheelAngle;
            if (wheelAngle > 0.0)
            {
                leftAckermannAngle *= 1.25;
            }
            else
            {
                rightAckermannAngle *= 1.25;
            }
        }

    }
}
