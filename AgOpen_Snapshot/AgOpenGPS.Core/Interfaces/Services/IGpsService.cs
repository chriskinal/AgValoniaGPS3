using System;
using AgOpenGPS.Core.Models.GPS;

namespace AgOpenGPS.Core.Interfaces.Services
{
    /// <summary>
    /// Service for GPS data processing and management
    /// </summary>
    public interface IGpsService
    {
        /// <summary>
        /// Event fired when new GPS data is received
        /// </summary>
        event EventHandler<GpsData>? GpsDataUpdated;

        /// <summary>
        /// Current GPS data
        /// </summary>
        GpsData CurrentData { get; }

        /// <summary>
        /// Whether GPS is currently connected and receiving data
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Start GPS service
        /// </summary>
        void Start();

        /// <summary>
        /// Stop GPS service
        /// </summary>
        void Stop();

        /// <summary>
        /// Process NMEA sentence
        /// </summary>
        void ProcessNmeaSentence(string sentence);

        /// <summary>
        /// Update GPS data directly (called by NMEA parser)
        /// </summary>
        void UpdateGpsData(GpsData newData);

        /// <summary>
        /// Update IMU data timestamp (called when IMU data received)
        /// </summary>
        void UpdateImuData();

        /// <summary>
        /// Check if GPS data is flowing (10Hz expected)
        /// </summary>
        bool IsGpsDataOk();

        /// <summary>
        /// Check if IMU data is flowing (10Hz expected)
        /// </summary>
        bool IsImuDataOk();
    }
}
