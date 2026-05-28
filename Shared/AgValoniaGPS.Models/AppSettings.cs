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
        // Window geometry (WindowWidth/Height/X/Y/Maximized) is persistent
        // application STATE, not config — it now lives in PersistentAppState
        // (appstate.json), not here.
        public bool StartFullscreen { get; set; } = false;
        public bool SvennArrowVisible { get; set; } = false;
        public bool KeyboardEnabled { get; set; } = false;
        public bool HeadlandDistanceVisible { get; set; } = true;
        public bool ExtraGuidelines { get; set; } = false;
        public int ExtraGuidelinesCount { get; set; } = 10;
        public bool FieldTextureVisible { get; set; } = true;
        public bool FieldTextureMoveable { get; set; } = false;

        /// <summary>
        /// Device-/user-scoped metric vs imperial preference. The source of
        /// truth lives here (in AppSettings); vehicle profiles must not
        /// dictate units. Default false (imperial) matches the legacy
        /// per-vehicle default before the migration.
        /// </summary>
        public bool IsMetric { get; set; } = false;

        /// <summary>
        /// One-shot migration latch: if false, the next vehicle-profile
        /// load that carries a legacy <c>General.IsMetric</c> field will
        /// copy that value into <see cref="IsMetric"/> and set this flag
        /// to true. Subsequent loads ignore the profile field — units are
        /// then a device preference, not vehicle-scoped.
        /// </summary>
        public bool HasMigratedIsMetric { get; set; } = false;
        public bool AutoSteerSound { get; set; } = true;
        public bool UTurnSound { get; set; } = true;
        public bool HydraulicSound { get; set; } = true;
        public bool SectionsSound { get; set; } = true;

        // Panel positions (SimulatorPanelX/Y/Visible) are persistent state →
        // PersistentAppState.

        // Localization
        public string Language { get; set; } = "en";

        // UI state
        public bool GridVisible { get; set; } = true;
        public bool CompassVisible { get; set; } = true;
        public bool SpeedVisible { get; set; } = true;
        public bool ElevationLogEnabled { get; set; } = false;

        // Display option toggles (preferences). Defaults mirror DisplayConfig.
        public bool PolygonsVisible { get; set; } = true;
        public bool SpeedometerVisible { get; set; } = true;
        public bool LineSmoothEnabled { get; set; } = true;
        public bool DirectionMarkersVisible { get; set; } = false;
        public bool SectionLinesVisible { get; set; } = true;
        public bool UTurnButtonVisible { get; set; } = true;
        public bool LateralButtonVisible { get; set; } = true;
        public bool HardwareMessagesEnabled { get; set; } = true;
        public int DayStartHour { get; set; } = 6;
        public int NightStartHour { get; set; } = 20;

        // Auto-close dialogs/menus after this many seconds of no interaction
        // (0 disables). Opt-in via the Screen & Alerts toggle; default off.
        public double DialogAutoCloseSeconds { get; set; }

        // On-map field-stats detail card. Toggled from the strip; default OFF.
        public bool FieldStatsOnMapVisible { get; set; } = false;

        // Camera view (CameraZoom/Pitch/Mode) and the current day/night value
        // (IsDayMode) are "where the app was" → persistent state, in
        // PersistentAppState. The AutoDayNight *preference* below stays config.

        // Coverage display resolution multiplier (1.0 = Ultra, 1.5 = High,
        // 2.5 = Medium, 4.0 = Low, 6.0 = Minimum). Default mirrors the
        // platform-aware default in DisplayConfig so a missing settings file
        // produces the same first-launch behavior as a fresh DisplayConfig.
        public double DisplayResolutionMultiplier { get; set; } =
            OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() ? 1.5 : 1.0;

        // Auto day/night switching by clock time (a preference → config). The
        // CURRENT day/night value (IsDayMode) is state → PersistentAppState.
        public bool AutoDayNight { get; set; } = true;

        // NTRIP settings
        public string NtripCasterIp { get; set; } = string.Empty;
        public int NtripCasterPort { get; set; } = 2101;
        public string NtripMountPoint { get; set; } = string.Empty;
        public string NtripUsername { get; set; } = string.Empty;
        public string NtripPassword { get; set; } = string.Empty;
        public bool NtripAutoConnect { get; set; } = false;

        // Simulator: whether the simulator is the GPS source is a preference
        // (config). The last simulator POSITION (lat/lon/speed/steer) is state
        // → PersistentAppState.
        public bool SimulatorEnabled { get; set; } = true;

        // GPS settings
        public int GpsUpdateRate { get; set; } = 10; // Hz
        public bool UseRtk { get; set; } = true;

        // Module presence — which modules the user expects to be present.
        // Drives the aggregate Module-status indicator in the top status strip.
        // Toggle UI ships with the Network panel (next commit); defaults match
        // the previous behavior of always expecting all four.
        public bool IsGpsConfigured { get; set; } = true;
        public bool IsImuConfigured { get; set; } = true;
        public bool IsAutoSteerConfigured { get; set; } = true;
        public bool IsMachineConfigured { get; set; } = true;

        // Field management. The Fields directory is a storage-location
        // preference (config); the open-field / last-field POINTER is state →
        // PersistentAppState (field DATA stays in field files).
        public string FieldsDirectory { get; set; } = string.Empty; // Will default to Documents/AgValoniaGPS/Fields

        // First-run / last-run are app-lifecycle state → PersistentAppState.

        // AgShare settings
        public string AgShareServer { get; set; } = "https://agshare.agopengps.com";
        public string AgShareApiKey { get; set; } = string.Empty;
        public bool AgShareEnabled { get; set; } = false;

        // Vehicle profile settings
        public string LastUsedVehicleProfile { get; set; } = string.Empty;
        public string LastUsedToolProfile { get; set; } = string.Empty;

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

            // Note: simulator-coordinate, window-dimension, and camera ranges
            // are now validated by PersistentAppState (the values moved there).

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
