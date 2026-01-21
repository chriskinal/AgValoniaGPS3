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

namespace AgValoniaGPS.Services.Interfaces
{
    /// <summary>
    /// Service for managing display and navigation settings
    /// </summary>
    public interface IDisplaySettingsService
    {
        // Grid display
        bool IsGridOn { get; set; }
        event EventHandler<bool>? GridVisibilityChanged;

        // Day/Night mode
        bool IsDayMode { get; set; }
        event EventHandler<bool>? DayNightModeChanged;

        // Camera settings
        double CameraPitch { get; set; }
        bool Is2DMode { get; set; }
        bool IsNorthUp { get; set; }
        event EventHandler<double>? CameraPitchChanged;
        event EventHandler<bool>? ViewModeChanged;

        // Brightness control
        int Brightness { get; set; }
        bool IsBrightnessSupported { get; }
        event EventHandler<int>? BrightnessChanged;

        // Methods
        void IncreaseCameraPitch();
        void DecreaseCameraPitch();
        void IncreaseBrightness();
        void DecreaseBrightness();
        void ToggleGrid();
        void ToggleDayNight();
        void Toggle2D3D();
        void ToggleNorthUp();

        // Settings persistence
        void LoadSettings();
        void SaveSettings();
    }
}
