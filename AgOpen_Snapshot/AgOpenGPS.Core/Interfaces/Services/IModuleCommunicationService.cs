using System;
using AgOpenGPS.Core.Models.Communication;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for handling module communication switch logic.
    /// Raises events when UI actions are needed instead of directly triggering button clicks.
    /// </summary>
    public interface IModuleCommunicationService
    {
        // Section control data
        byte[] SectionControlBytes { get; }
        byte[] PreviousSectionControlBytes { get; }

        // Steering data
        int PwmDisplay { get; set; }
        double ActualSteerAngleDegrees { get; set; }
        int ActualSteerAngleChart { get; set; }
        int SensorData { get; set; }

        // Safety
        bool IsOutOfBounds { get; set; }

        // Work switch configuration
        bool IsWorkSwitchActiveLow { get; set; }
        bool IsRemoteWorkSystemOn { get; set; }
        bool IsWorkSwitchEnabled { get; set; }
        bool IsWorkSwitchManualSections { get; set; }
        bool IsSteerWorkSwitchManualSections { get; set; }
        bool IsSteerWorkSwitchEnabled { get; set; }

        // Switch states
        bool WorkSwitchHigh { get; set; }
        bool SteerSwitchHigh { get; set; }

        // Events
        event EventHandler<AutoSteerToggleEventArgs>? AutoSteerToggleRequested;
        event EventHandler<SectionMasterToggleEventArgs>? SectionMasterToggleRequested;

        /// <summary>
        /// Check work and steer switch states, raising events when UI actions are needed.
        /// </summary>
        void CheckSwitches(ModuleSwitchState currentState);
    }
}
