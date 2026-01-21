// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using AgValoniaGPS.Services.Interfaces;
using AgValoniaGPS.Models.Communication;
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Services
{
    /// <summary>
    /// Core module communication service.
    /// Handles work switch and steer switch logic, raising events when UI actions are needed.
    /// Replaces FormGPS.PerformClick() calls with clean event-based architecture.
    /// Reads switch configuration from ConfigurationStore.Instance.Tool.
    /// </summary>
    public class ModuleCommunicationService : IModuleCommunicationService
    {
        // Access config from ConfigurationStore
        private static ToolConfig Tool => ConfigurationStore.Instance.Tool;
        private static MachineConfig Machine => ConfigurationStore.Instance.Machine;

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

        // Work switch configuration - read from ConfigurationStore
        public bool IsWorkSwitchActiveLow => Tool.IsWorkSwitchActiveLow;
        public bool IsRemoteWorkSystemOn { get; set; } // Runtime state, not persisted
        public bool IsWorkSwitchEnabled => Tool.IsWorkSwitchEnabled;
        public bool IsWorkSwitchManualSections => Tool.IsWorkSwitchManualSections;
        public bool IsSteerWorkSwitchManualSections => Tool.IsSteerSwitchManualSections;
        public bool IsSteerWorkSwitchEnabled => Tool.IsSteerSwitchEnabled;

        // Machine config accessors for hydraulic control
        public bool HydraulicLiftEnabled => Machine.HydraulicLiftEnabled;
        public int RaiseTime => Machine.RaiseTime;
        public int LowerTime => Machine.LowerTime;
        public double LookAhead => Machine.LookAhead;
        public bool InvertRelay => Machine.InvertRelay;

        // User values (custom data sent to machine module)
        public int User1Value => Machine.User1Value;
        public int User2Value => Machine.User2Value;
        public int User3Value => Machine.User3Value;
        public int User4Value => Machine.User4Value;

        // AHRS config accessor
        private static AhrsConfig Ahrs => ConfigurationStore.Instance.Ahrs;
        public bool AlarmStopsAutoSteer => Ahrs.AlarmStopsAutoSteer;

        // Switch states (runtime, from hardware)
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
