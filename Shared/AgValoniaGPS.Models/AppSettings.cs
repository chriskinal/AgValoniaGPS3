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
using System.Collections.Generic;

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
        public bool StartFullscreen { get; set; } = false;
        public bool SvennArrowVisible { get; set; } = false;
        public bool KeyboardEnabled { get; set; } = false;
        public bool HeadlandDistanceVisible { get; set; } = true;
        public bool ExtraGuidelines { get; set; } = false;
        public int ExtraGuidelinesCount { get; set; } = 10;
        public bool FieldTextureVisible { get; set; } = false;
        public bool AutoSteerSound { get; set; } = true;
        public bool UTurnSound { get; set; } = true;
        public bool HydraulicSound { get; set; } = true;
        public bool SectionsSound { get; set; } = true;

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

        // Localization
        public string Language { get; set; } = "en";

        // UI state
        public bool GridVisible { get; set; } = true;
        public bool CompassVisible { get; set; } = true;
        public bool SpeedVisible { get; set; } = true;
        public bool ElevationLogEnabled { get; set; } = false;

        // Camera settings
        public double CameraZoom { get; set; } = 100.0;
        public double CameraPitch { get; set; } = -60.0;

        // NTRIP settings
        public string NtripCasterIp { get; set; } = string.Empty;
        public int NtripCasterPort { get; set; } = 2101;
        public string NtripMountPoint { get; set; } = string.Empty;
        public string NtripUsername { get; set; } = string.Empty;
        public string NtripPassword { get; set; } = string.Empty;
        public bool NtripAutoConnect { get; set; } = false;

        // Simulator settings
        public bool SimulatorEnabled { get; set; } = true;
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

        // Hotkey bindings (empty = use defaults)
        public Dictionary<string, string> HotkeyBindings { get; set; } = new();

        /// <summary>
        /// Validate and clamp all settings to valid ranges.
        /// Returns list of fields that were corrected.
        /// </summary>
        public List<string> ValidateAndFix()
        {
            var defaults = new AppSettings();
            var fixes = new List<string>();

            // Simulator coordinates
            if (SimulatorLatitude < -90 || SimulatorLatitude > 90)
            {
                fixes.Add($"SimulatorLatitude was {SimulatorLatitude}, reset to {defaults.SimulatorLatitude}");
                SimulatorLatitude = defaults.SimulatorLatitude;
            }
            if (SimulatorLongitude < -180 || SimulatorLongitude > 180)
            {
                fixes.Add($"SimulatorLongitude was {SimulatorLongitude}, reset to {defaults.SimulatorLongitude}");
                SimulatorLongitude = defaults.SimulatorLongitude;
            }

            // Window dimensions
            if (WindowWidth < 100 || WindowWidth > 10000)
            {
                fixes.Add($"WindowWidth was {WindowWidth}, reset to {defaults.WindowWidth}");
                WindowWidth = defaults.WindowWidth;
            }
            if (WindowHeight < 100 || WindowHeight > 10000)
            {
                fixes.Add($"WindowHeight was {WindowHeight}, reset to {defaults.WindowHeight}");
                WindowHeight = defaults.WindowHeight;
            }

            // Camera
            if (CameraZoom < 1 || CameraZoom > 10000)
            {
                fixes.Add($"CameraZoom was {CameraZoom}, reset to {defaults.CameraZoom}");
                CameraZoom = defaults.CameraZoom;
            }

            // GPS update rate
            if (GpsUpdateRate < 1 || GpsUpdateRate > 100)
            {
                fixes.Add($"GpsUpdateRate was {GpsUpdateRate}, reset to {defaults.GpsUpdateRate}");
                GpsUpdateRate = defaults.GpsUpdateRate;
            }

            // NTRIP port
            if (NtripCasterPort < 1 || NtripCasterPort > 65535)
            {
                fixes.Add($"NtripCasterPort was {NtripCasterPort}, reset to {defaults.NtripCasterPort}");
                NtripCasterPort = defaults.NtripCasterPort;
            }

            return fixes;
        }
    }
}
