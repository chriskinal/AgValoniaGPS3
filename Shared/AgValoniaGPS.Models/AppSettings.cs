using System;

namespace AgValoniaGPS.Models
{
    /// <summary>
    /// Application settings that are persisted between sessions
    /// </summary>
    public class AppSettings
    {
        // Window settings
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double WindowX { get; set; } = 100;
        public double WindowY { get; set; } = 100;
        public bool WindowMaximized { get; set; } = false;

        // Panel positions
        public double SimulatorPanelX { get; set; } = double.NaN; // NaN means not set
        public double SimulatorPanelY { get; set; } = double.NaN;
        public bool SimulatorPanelVisible { get; set; } = false;

        // UI state
        public bool GridVisible { get; set; } = true;
        public bool CompassVisible { get; set; } = true;
        public bool SpeedVisible { get; set; } = true;

        // Camera settings
        public double CameraZoom { get; set; } = 100.0;
        public double CameraPitch { get; set; } = 0.0;

        // NTRIP settings
        public string NtripCasterIp { get; set; } = string.Empty;
        public int NtripCasterPort { get; set; } = 2101;
        public string NtripMountPoint { get; set; } = string.Empty;
        public string NtripUsername { get; set; } = string.Empty;
        public string NtripPassword { get; set; } = string.Empty;
        public bool NtripAutoConnect { get; set; } = false;

        // Simulator settings
        public bool SimulatorEnabled { get; set; } = false;
        public double SimulatorLatitude { get; set; } = 40.7128;
        public double SimulatorLongitude { get; set; } = -74.0060;
        public double SimulatorSpeed { get; set; } = 0.0;
        public double SimulatorSteerAngle { get; set; } = 0.0;

        // GPS settings
        public int GpsUpdateRate { get; set; } = 10; // Hz
        public bool UseRtk { get; set; } = true;

        // Field management
        public string FieldsDirectory { get; set; } = string.Empty; // Will default to Documents/AgValoniaGPS/Fields
        public string CurrentFieldName { get; set; } = string.Empty; // Currently open field
        public string LastOpenedField { get; set; } = string.Empty; // Last field that was opened

        // First run
        public bool IsFirstRun { get; set; } = true;
        public DateTime LastRunDate { get; set; } = DateTime.MinValue;

        // AgShare settings
        public string AgShareServer { get; set; } = "https://agshare.agopengps.com";
        public string AgShareApiKey { get; set; } = string.Empty;
        public bool AgShareEnabled { get; set; } = false;

        // Vehicle profile settings
        public string LastUsedVehicleProfile { get; set; } = string.Empty;
    }
}
