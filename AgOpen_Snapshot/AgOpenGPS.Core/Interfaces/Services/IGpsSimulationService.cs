using System;
using AgOpenGPS.Core.Models;
using AgOpenGPS.Core.Models.GPS;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Event args for GPS simulation updates
    /// </summary>
    public class GpsSimulationEventArgs : EventArgs
    {
        public SimulatedGpsData Data { get; }

        public GpsSimulationEventArgs(SimulatedGpsData data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Service for simulating GPS position updates without real GPS hardware.
    /// Useful for testing and development.
    /// </summary>
    public interface IGpsSimulationService
    {
        /// <summary>
        /// Raised when simulated GPS data is updated
        /// </summary>
        event EventHandler<GpsSimulationEventArgs>? GpsDataUpdated;

        /// <summary>
        /// Current simulated WGS84 position
        /// </summary>
        Wgs84 CurrentPosition { get; }

        /// <summary>
        /// Current simulated heading in radians
        /// </summary>
        double HeadingRadians { get; }

        /// <summary>
        /// Current step distance (meters per tick)
        /// </summary>
        double StepDistance { get; set; }

        /// <summary>
        /// Current steer angle input (degrees)
        /// </summary>
        double SteerAngle { get; set; }

        /// <summary>
        /// Averaged/smoothed steer angle (degrees)
        /// </summary>
        double SteerAngleAverage { get; }

        /// <summary>
        /// Whether accelerating forward
        /// </summary>
        bool IsAcceleratingForward { get; set; }

        /// <summary>
        /// Whether accelerating backward
        /// </summary>
        bool IsAcceleratingBackward { get; set; }

        /// <summary>
        /// Initialize simulation with starting position
        /// </summary>
        void Initialize(Wgs84 startPosition);

        /// <summary>
        /// Process one simulation tick with given steer angle.
        /// Updates position, heading, and all simulated GPS values.
        /// </summary>
        /// <param name="steerAngleDegrees">Current steer angle command in degrees</param>
        void Tick(double steerAngleDegrees);

        /// <summary>
        /// Reset simulation to initial state
        /// </summary>
        void Reset();

        /// <summary>
        /// Set the heading directly (in radians)
        /// </summary>
        /// <param name="headingRadians">New heading in radians</param>
        void SetHeading(double headingRadians);
    }
}
