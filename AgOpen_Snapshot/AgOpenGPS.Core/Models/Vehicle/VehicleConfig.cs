namespace AgOpenGPS.Core.Models
{
    public enum VehicleType
    {
        Tractor = 0,
        Harvester = 1,
        Articulated = 2
    }

    /// <summary>
    /// Core model for vehicle configuration and runtime state
    /// </summary>
    public class VehicleConfig
    {
        // Vehicle type and appearance
        public VehicleType Type { get; set; }
        public bool IsImage { get; set; }
        public ColorRgb Color { get; set; }
        public double Opacity { get; set; }

        // Antenna configuration
        public double AntennaHeight { get; set; }
        public double AntennaPivot { get; set; }
        public double AntennaOffset { get; set; }

        // Vehicle dimensions
        public double Wheelbase { get; set; }
        public double TrackWidth { get; set; }

        // Dead zone configuration
        public int DeadZoneHeading { get; set; }
        public int DeadZoneDelay { get; set; }
        public int DeadZoneDelayCounter { get; set; }
        public bool IsInDeadZone { get; set; }

        // Speed configuration
        public double SlowSpeedCutoff { get; set; } = 0;
        public double FunctionSpeedLimit { get; set; }

        // Goal point lookahead settings
        public double GoalPointLookAheadHold { get; set; }
        public double GoalPointLookAheadMult { get; set; }
        public double GoalPointAcquireFactor { get; set; }
        public double UturnCompensation { get; set; }

        // Stanley controller gains
        public double StanleyDistanceErrorGain { get; set; }
        public double StanleyHeadingErrorGain { get; set; }
        public double StanleyIntegralGainAB { get; set; }

        // Pure pursuit controller
        public double PurePursuitIntegralGain { get; set; }

        // Steering limits
        public double MaxSteerAngle { get; set; }
        public double MaxSteerSpeed { get; set; }
        public double MinSteerSpeed { get; set; }
        public double MaxAngularVelocity { get; set; }

        // Hydraulic lift configuration
        public bool IsHydLiftOn { get; set; }
        public double HydLiftLookAheadTime { get; set; }
        public double HydLiftLookAheadDistanceLeft { get; set; }
        public double HydLiftLookAheadDistanceRight { get; set; }

        // Free drive mode
        public bool IsInFreeDriveMode { get; set; }
        public double DriveFreeSteerAngle { get; set; } = 0;

        // Mode tracking
        public double ModeXTE { get; set; }
        public double ModeActualXTE { get; set; } = 0;
        public double ModeActualHeadingError { get; set; } = 0;
        public int ModeTime { get; set; } = 0;
        public int ModeTimeCounter { get; set; } = 0;

        // Goal distance
        public double GoalDistance { get; set; } = 0;
    }
}
