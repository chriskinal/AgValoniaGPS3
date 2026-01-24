namespace AgOpenGPS.Core.Models.Sensors
{
    /// <summary>
    /// Core model for IMU/AHRS (Attitude and Heading Reference System) sensor configuration and runtime data
    /// </summary>
    public class AhrsConfiguration
    {
        // Runtime sensor values
        public double ImuHeading { get; set; } = 99999;
        public double ImuRoll { get; set; } = 0;
        public double ImuPitch { get; set; } = 0;
        public double ImuYawRate { get; set; } = 0;
        public short AngularVelocity { get; set; }

        // Configuration values
        public double RollZero { get; set; }
        public double RollFilter { get; set; }
        public double FusionWeight { get; set; }
        public bool IsAutoSteerAuto { get; set; } = true;
        public double ForwardCompensation { get; set; }
        public double ReverseCompensation { get; set; }
        public bool IsRollInvert { get; set; }
        public bool IsReverseOn { get; set; }
        public bool IsDualAsIMU { get; set; }
        public bool AutoSwitchDualFixOn { get; set; }
        public double AutoSwitchDualFixSpeed { get; set; }
    }
}
