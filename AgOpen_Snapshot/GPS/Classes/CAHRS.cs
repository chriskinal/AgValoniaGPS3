using AgOpenGPS.Core.Models.Sensors;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for AhrsConfiguration from AgOpenGPS.Core
    /// Delegates all operations to Core AhrsConfiguration instance
    /// </summary>
    public class CAHRS
    {
        private readonly AhrsConfiguration _core;

        /// <summary>
        /// Roll and heading from the IMU - delegates to Core
        /// </summary>
        public double imuHeading
        {
            get => _core.ImuHeading;
            set => _core.ImuHeading = value;
        }

        public double imuRoll
        {
            get => _core.ImuRoll;
            set => _core.ImuRoll = value;
        }

        public double imuPitch
        {
            get => _core.ImuPitch;
            set => _core.ImuPitch = value;
        }

        public double imuYawRate
        {
            get => _core.ImuYawRate;
            set => _core.ImuYawRate = value;
        }

        public System.Int16 angVel
        {
            get => _core.AngularVelocity;
            set => _core.AngularVelocity = value;
        }

        /// <summary>
        /// Actual value in degrees - delegates to Core
        /// </summary>
        public double rollZero
        {
            get => _core.RollZero;
            set => _core.RollZero = value;
        }

        /// <summary>
        /// Roll Filter Value - delegates to Core
        /// </summary>
        public double rollFilter
        {
            get => _core.RollFilter;
            set => _core.RollFilter = value;
        }

        /// <summary>
        /// Configuration flags - delegate to Core
        /// </summary>
        public bool isAutoSteerAuto
        {
            get => _core.IsAutoSteerAuto;
            set => _core.IsAutoSteerAuto = value;
        }

        public bool isRollInvert
        {
            get => _core.IsRollInvert;
            set => _core.IsRollInvert = value;
        }

        public bool isDualAsIMU
        {
            get => _core.IsDualAsIMU;
            set => _core.IsDualAsIMU = value;
        }

        public bool isReverseOn
        {
            get => _core.IsReverseOn;
            set => _core.IsReverseOn = value;
        }

        /// <summary>
        /// AutoswitchDualFix configuration - delegates to Core
        /// </summary>
        public bool autoSwitchDualFixOn
        {
            get => _core.AutoSwitchDualFixOn;
            set => _core.AutoSwitchDualFixOn = value;
        }

        public double autoSwitchDualFixSpeed
        {
            get => _core.AutoSwitchDualFixSpeed;
            set => _core.AutoSwitchDualFixSpeed = value;
        }

        /// <summary>
        /// The factor for fusion of GPS and IMU - delegates to Core
        /// </summary>
        public double forwardComp
        {
            get => _core.ForwardCompensation;
            set => _core.ForwardCompensation = value;
        }

        public double reverseComp
        {
            get => _core.ReverseCompensation;
            set => _core.ReverseCompensation = value;
        }

        public double fusionWeight
        {
            get => _core.FusionWeight;
            set => _core.FusionWeight = value;
        }

        /// <summary>
        /// Constructor - initializes Core instance and loads configuration from Settings
        /// </summary>
        public CAHRS()
        {
            _core = new AhrsConfiguration();

            // Load configuration from WinForms Settings
            _core.RollZero = Properties.Settings.Default.setIMU_rollZero;
            _core.RollFilter = Properties.Settings.Default.setIMU_rollFilter;
            _core.FusionWeight = Properties.Settings.Default.setIMU_fusionWeight2;
            _core.IsAutoSteerAuto = true;
            _core.ForwardCompensation = Properties.Settings.Default.setGPS_forwardComp;
            _core.ReverseCompensation = Properties.Settings.Default.setGPS_reverseComp;
            _core.IsRollInvert = Properties.Settings.Default.setIMU_invertRoll;
            _core.IsReverseOn = Properties.Settings.Default.setIMU_isReverseOn;
            _core.AutoSwitchDualFixOn = Properties.Settings.Default.setAutoSwitchDualFixOn;
            _core.AutoSwitchDualFixSpeed = Properties.Settings.Default.setAutoSwitchDualFixSpeed;
        }

        /// <summary>
        /// Get the underlying Core AhrsConfiguration instance
        /// </summary>
        public AhrsConfiguration CoreAhrsConfiguration => _core;
    }
}
