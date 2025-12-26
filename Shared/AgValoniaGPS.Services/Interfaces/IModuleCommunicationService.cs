using System;
using AgValoniaGPS.Models.Communication;

namespace AgValoniaGPS.Services.Interfaces
{
    /// <summary>
    /// Service for handling module communication switch logic.
    /// Raises events when UI actions are needed instead of directly triggering button clicks.
    /// Configuration is read from ConfigurationStore (Tool and Machine configs).
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

        // Work switch configuration (read from ConfigurationStore.Tool)
        bool IsWorkSwitchActiveLow { get; }
        bool IsRemoteWorkSystemOn { get; set; } // Runtime state
        bool IsWorkSwitchEnabled { get; }
        bool IsWorkSwitchManualSections { get; }
        bool IsSteerWorkSwitchManualSections { get; }
        bool IsSteerWorkSwitchEnabled { get; }

        // Machine config (read from ConfigurationStore.Machine)
        bool HydraulicLiftEnabled { get; }
        int RaiseTime { get; }
        int LowerTime { get; }
        double LookAhead { get; }
        bool InvertRelay { get; }

        // User values (custom data sent to machine module)
        int User1Value { get; }
        int User2Value { get; }
        int User3Value { get; }
        int User4Value { get; }

        // AHRS config (read from ConfigurationStore.Ahrs)
        bool AlarmStopsAutoSteer { get; }

        // Switch states (runtime, from hardware)
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
