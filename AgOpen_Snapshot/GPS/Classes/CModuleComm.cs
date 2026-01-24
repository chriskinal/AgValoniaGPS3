using AgOpenGPS.Core.Models.Communication;
using AgOpenGPS.Core.Services;
using AgOpenGPS.Core.Translations;

namespace AgOpenGPS
{
    /// <summary>
    /// WinForms wrapper for module communication.
    /// Delegates switch logic to Core ModuleCommunicationService and handles UI button clicks via events.
    /// </summary>
    public class CModuleComm
    {
        //copy of the mainform address
        private readonly FormGPS mf;
        private readonly ModuleCommunicationService _coreService;

        #region Properties delegated to Core

        //Critical Safety Properties
        public bool isOutOfBounds
        {
            get => _coreService.IsOutOfBounds;
            set => _coreService.IsOutOfBounds = value;
        }

        // ---- Section control switches to AOG  ---------------------------------------------------------
        //PGN - 32736 - 127.249 0x7FF9
        public byte[] ss => _coreService.SectionControlBytes;
        public byte[] ssP => _coreService.PreviousSectionControlBytes;

        // Section control indices (constants from Core)
        public int swHeader = ModuleCommunicationService.SW_HEADER;
        public int swMain = ModuleCommunicationService.SW_MAIN;
        public int swAutoGr0 = ModuleCommunicationService.SW_AUTO_GR0;
        public int swAutoGr1 = ModuleCommunicationService.SW_AUTO_GR1;
        public int swNumSections = ModuleCommunicationService.SW_NUM_SECTIONS;
        public int swOnGr0 = ModuleCommunicationService.SW_ON_GR0;
        public int swOffGr0 = ModuleCommunicationService.SW_OFF_GR0;
        public int swOnGr1 = ModuleCommunicationService.SW_ON_GR1;
        public int swOffGr1 = ModuleCommunicationService.SW_OFF_GR1;

        public int pwmDisplay
        {
            get => _coreService.PwmDisplay;
            set => _coreService.PwmDisplay = value;
        }

        public double actualSteerAngleDegrees
        {
            get => _coreService.ActualSteerAngleDegrees;
            set => _coreService.ActualSteerAngleDegrees = value;
        }

        public int actualSteerAngleChart
        {
            get => _coreService.ActualSteerAngleChart;
            set => _coreService.ActualSteerAngleChart = value;
        }

        public int sensorData
        {
            get => _coreService.SensorData;
            set => _coreService.SensorData = value;
        }

        //for the workswitch
        public bool isWorkSwitchActiveLow
        {
            get => _coreService.IsWorkSwitchActiveLow;
            set => _coreService.IsWorkSwitchActiveLow = value;
        }

        public bool isRemoteWorkSystemOn
        {
            get => _coreService.IsRemoteWorkSystemOn;
            set => _coreService.IsRemoteWorkSystemOn = value;
        }

        public bool isWorkSwitchEnabled
        {
            get => _coreService.IsWorkSwitchEnabled;
            set => _coreService.IsWorkSwitchEnabled = value;
        }

        public bool isWorkSwitchManualSections
        {
            get => _coreService.IsWorkSwitchManualSections;
            set => _coreService.IsWorkSwitchManualSections = value;
        }

        public bool isSteerWorkSwitchManualSections
        {
            get => _coreService.IsSteerWorkSwitchManualSections;
            set => _coreService.IsSteerWorkSwitchManualSections = value;
        }

        public bool isSteerWorkSwitchEnabled
        {
            get => _coreService.IsSteerWorkSwitchEnabled;
            set => _coreService.IsSteerWorkSwitchEnabled = value;
        }

        public bool workSwitchHigh
        {
            get => _coreService.WorkSwitchHigh;
            set => _coreService.WorkSwitchHigh = value;
        }

        public bool steerSwitchHigh
        {
            get => _coreService.SteerSwitchHigh;
            set => _coreService.SteerSwitchHigh = value;
        }

        // Note: oldWorkSwitchHigh, oldSteerSwitchHigh, oldSteerSwitchRemote are now internal to Core
        public bool oldWorkSwitchHigh, oldSteerSwitchHigh, oldSteerSwitchRemote;

        #endregion

        //constructor
        public CModuleComm(FormGPS _f)
        {
            mf = _f;

            // Create Core service
            _coreService = new ModuleCommunicationService();

            // Subscribe to Core events
            _coreService.AutoSteerToggleRequested += OnAutoSteerToggleRequested;
            _coreService.SectionMasterToggleRequested += OnSectionMasterToggleRequested;

            //WorkSwitch logic
            isRemoteWorkSystemOn = false;

            //does a low, grounded out, mean on
            isWorkSwitchActiveLow = true;
        }

        /// <summary>
        /// Handle AutoSteer toggle request from Core service
        /// </summary>
        private void OnAutoSteerToggleRequested(object sender, AutoSteerToggleEventArgs e)
        {
            mf.btnAutoSteer.PerformClick();
        }

        /// <summary>
        /// Handle section master button toggle request from Core service
        /// </summary>
        private void OnSectionMasterToggleRequested(object sender, SectionMasterToggleEventArgs e)
        {
            if (e.Button == SectionMasterToggleEventArgs.SectionMasterButton.Auto)
            {
                mf.btnSectionMasterAuto.PerformClick();
            }
            else // Manual
            {
                mf.btnSectionMasterManual.PerformClick();
            }
        }

        //Called from "OpenGL.Designer.cs" when required
        public void CheckWorkAndSteerSwitch()
        {
            // Build current UI state
            var currentState = new ModuleSwitchState
            {
                IsAutoSteerOn = mf.isBtnAutoSteerOn,
                IsAutoSteerAuto = mf.ahrs.isAutoSteerAuto,
                AutoButtonState = (int)mf.autoBtnState,
                ManualButtonState = (int)mf.manualBtnState
            };

            // Delegate to Core service - it will raise events if button clicks are needed
            _coreService.CheckSwitches(currentState);
        }
    }
}