using System;
using AgOpenGPS.Core.Interfaces.Services;
using AgOpenGPS.Core.Models.GPS;

namespace AgOpenGPS.Core.Services
{
    /// <summary>
    /// Implementation of GPS service for data processing
    /// </summary>
    public class GpsService : IGpsService
    {
        public event EventHandler<GpsData>? GpsDataUpdated;

        public GpsData CurrentData { get; private set; } = new();

        public bool IsConnected { get; private set; }

        private DateTime _lastGpsDataReceived = DateTime.MinValue;
        private DateTime _lastImuDataReceived = DateTime.MinValue;
        private const int GPS_TIMEOUT_MS = 300; // 10Hz data = 100ms cycle, allow 300ms
        private const int IMU_TIMEOUT_MS = 300; // 10Hz data = 100ms cycle, allow 300ms

        public void Start()
        {
            IsConnected = true;
        }

        public void Stop()
        {
            IsConnected = false;
        }

        public void ProcessNmeaSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return;

            // This is called by NmeaParserService after parsing
            // Just trigger event notification
            GpsDataUpdated?.Invoke(this, CurrentData);
        }

        /// <summary>
        /// Update GPS data directly (called by NMEA parser)
        /// </summary>
        public void UpdateGpsData(GpsData newData)
        {
            CurrentData = newData;
            _lastGpsDataReceived = DateTime.Now;
            IsConnected = newData.IsValid;
            GpsDataUpdated?.Invoke(this, CurrentData);
        }

        /// <summary>
        /// Update IMU data timestamp (called when IMU data received)
        /// </summary>
        public void UpdateImuData()
        {
            _lastImuDataReceived = DateTime.Now;
        }

        /// <summary>
        /// Check if GPS data is flowing (10Hz expected)
        /// </summary>
        public bool IsGpsDataOk()
        {
            return (DateTime.Now - _lastGpsDataReceived).TotalMilliseconds < GPS_TIMEOUT_MS;
        }

        /// <summary>
        /// Check if IMU data is flowing (10Hz expected)
        /// </summary>
        public bool IsImuDataOk()
        {
            return (DateTime.Now - _lastImuDataReceived).TotalMilliseconds < IMU_TIMEOUT_MS;
        }
    }
}
