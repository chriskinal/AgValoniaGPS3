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

        // Navigation panel positions
        public double LeftNavPanelX { get; set; } = double.NaN;
        public double LeftNavPanelY { get; set; } = double.NaN;
        public double RightNavPanelX { get; set; } = double.NaN;
        public double RightNavPanelY { get; set; } = double.NaN;
        public double BottomNavPanelX { get; set; } = double.NaN;
        public double BottomNavPanelY { get; set; } = double.NaN;
        public double SectionPanelX { get; set; } = double.NaN;
        public double SectionPanelY { get; set; } = double.NaN;

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
