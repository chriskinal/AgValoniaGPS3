using System;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.Communication;

namespace AgOpenGPS.Core.Services
{
    /// <summary>
    /// Core module communication service.
    /// Handles work switch and steer switch logic, raising events when UI actions are needed.
    /// Replaces FormGPS.PerformClick() calls with clean event-based architecture.
    /// </summary>
    public class ModuleCommunicationService : IModuleCommunicationService
    {
        // Section control data
        public byte[] SectionControlBytes { get; } = new byte[9];
        public byte[] PreviousSectionControlBytes { get; } = new byte[9];

        // Section control indices (constants matching original)
        public const int SW_HEADER = 0;
        public const int SW_MAIN = 1;
        public const int SW_AUTO_GR0 = 2;
        public const int SW_AUTO_GR1 = 3;
        public const int SW_NUM_SECTIONS = 4;
        public const int SW_ON_GR0 = 5;
        public const int SW_OFF_GR0 = 6;
        public const int SW_ON_GR1 = 7;
        public const int SW_OFF_GR1 = 8;

        // Steering data
        public int PwmDisplay { get; set; }
        public double ActualSteerAngleDegrees { get; set; }
        public int ActualSteerAngleChart { get; set; }
        public int SensorData { get; set; } = -1;

        // Safety
        public bool IsOutOfBounds { get; set; } = true;

        // Work switch configuration
        public bool IsWorkSwitchActiveLow { get; set; } = true;
        public bool IsRemoteWorkSystemOn { get; set; }
        public bool IsWorkSwitchEnabled { get; set; }
        public bool IsWorkSwitchManualSections { get; set; }
        public bool IsSteerWorkSwitchManualSections { get; set; }
        public bool IsSteerWorkSwitchEnabled { get; set; }

        // Switch states
        public bool WorkSwitchHigh { get; set; }
        public bool SteerSwitchHigh { get; set; }

        // Previous switch states (for change detection)
        private bool _oldWorkSwitchHigh;
        private bool _oldSteerSwitchHigh;
        private bool _oldSteerSwitchRemote;

        // Events raised when UI actions are needed
        public event EventHandler<AutoSteerToggleEventArgs>? AutoSteerToggleRequested;
        public event EventHandler<SectionMasterToggleEventArgs>? SectionMasterToggleRequested;

        /// <summary>
        /// Check work and steer switch states, raising events when UI actions are needed.
        /// This replaces the direct PerformClick() calls in original CModuleComm.
        /// </summary>
        /// <param name="currentState">Current UI state (button states, AutoSteer status)</param>
        public void CheckSwitches(ModuleSwitchState currentState)
        {
            // AutoSteerAuto button enable logic (Ray Bear inspired code)
            if (currentState.IsAutoSteerAuto && SteerSwitchHigh != _oldSteerSwitchRemote)
            {
                _oldSteerSwitchRemote = SteerSwitchHigh;

                // Steer switch is active low
                if (SteerSwitchHigh == currentState.IsAutoSteerOn)
                {
                    // Request AutoSteer toggle
                    AutoSteerToggleRequested?.Invoke(this, new AutoSteerToggleEventArgs());
                }
            }

            // Remote work system logic
            if (IsRemoteWorkSystemOn)
            {
                CheckWorkSwitch(currentState);
                CheckSteerWorkSwitch(currentState);
            }
        }

        /// <summary>
        /// Check work switch state and request section master button toggles
        /// </summary>
        private void CheckWorkSwitch(ModuleSwitchState currentState)
        {
            if (!IsWorkSwitchEnabled || _oldWorkSwitchHigh == WorkSwitchHigh)
                return;

            _oldWorkSwitchHigh = WorkSwitchHigh;

            if (WorkSwitchHigh != IsWorkSwitchActiveLow)
            {
                // Work switch is active - turn sections on
                if (IsWorkSwitchManualSections)
                {
                    if (currentState.ManualButtonState != ButtonStates.On)
                    {
                        SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                        {
                            Button = SectionMasterToggleEventArgs.SectionMasterButton.Manual
                        });
                    }
                }
                else
                {
                    if (currentState.AutoButtonState != ButtonStates.Auto)
                    {
                        SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                        {
                            Button = SectionMasterToggleEventArgs.SectionMasterButton.Auto
                        });
                    }
                }
            }
            else
            {
                // Work switch is inactive - turn sections off
                // Check both buttons and turn off whichever is on
                if (currentState.AutoButtonState != ButtonStates.Off)
                {
                    SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                    {
                        Button = SectionMasterToggleEventArgs.SectionMasterButton.Auto
                    });
                }
                if (currentState.ManualButtonState != ButtonStates.Off)
                {
                    SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                    {
                        Button = SectionMasterToggleEventArgs.SectionMasterButton.Manual
                    });
                }
            }
        }

        /// <summary>
        /// Check steer work switch state and request section master button toggles
        /// </summary>
        private void CheckSteerWorkSwitch(ModuleSwitchState currentState)
        {
            if (!IsSteerWorkSwitchEnabled || _oldSteerSwitchHigh == SteerSwitchHigh)
                return;

            _oldSteerSwitchHigh = SteerSwitchHigh;

            if ((currentState.IsAutoSteerOn && currentState.IsAutoSteerAuto)
                || (!currentState.IsAutoSteerAuto && !SteerSwitchHigh))
            {
                // Steer work switch is active - turn sections on
                if (IsSteerWorkSwitchManualSections)
                {
                    if (currentState.ManualButtonState != ButtonStates.On)
                    {
                        SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                        {
                            Button = SectionMasterToggleEventArgs.SectionMasterButton.Manual
                        });
                    }
                }
                else
                {
                    if (currentState.AutoButtonState != ButtonStates.Auto)
                    {
                        SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                        {
                            Button = SectionMasterToggleEventArgs.SectionMasterButton.Auto
                        });
                    }
                }
            }
            else
            {
                // Steer work switch is inactive - turn sections off
                // Check both buttons and turn off whichever is on
                if (currentState.AutoButtonState != ButtonStates.Off)
                {
                    SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                    {
                        Button = SectionMasterToggleEventArgs.SectionMasterButton.Auto
                    });
                }
                if (currentState.ManualButtonState != ButtonStates.Off)
                {
                    SectionMasterToggleRequested?.Invoke(this, new SectionMasterToggleEventArgs
                    {
                        Button = SectionMasterToggleEventArgs.SectionMasterButton.Manual
                    });
                }
            }
        }
    }
}
