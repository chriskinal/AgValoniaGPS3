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

using AgValoniaGPS.Models.Configuration;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Navigation panel commands - view toggles, camera controls, brightness.
/// </summary>
public partial class MainViewModel
{
    private void InitializeNavigationCommands()
    {
        // Panel toggle commands
        ToggleViewSettingsPanelCommand = new RelayCommand(() =>
        {
            IsViewSettingsPanelVisible = !IsViewSettingsPanelVisible;
        });

        ToggleFileMenuPanelCommand = new RelayCommand(() =>
        {
            IsFileMenuPanelVisible = !IsFileMenuPanelVisible;
        });

        ToggleToolsPanelCommand = new RelayCommand(() =>
        {
            IsToolsPanelVisible = !IsToolsPanelVisible;
        });

        ToggleConfigurationPanelCommand = new RelayCommand(() =>
        {
            IsConfigurationPanelVisible = !IsConfigurationPanelVisible;
        });

        ToggleJobMenuPanelCommand = new RelayCommand(() =>
        {
            IsJobMenuPanelVisible = !IsJobMenuPanelVisible;
        });

        ToggleFieldToolsPanelCommand = new RelayCommand(() =>
        {
            IsFieldToolsPanelVisible = !IsFieldToolsPanelVisible;
        });

        ToggleAutoTrackCommand = new RelayCommand(() =>
        {
            IsAutoTrackEnabled = !IsAutoTrackEnabled;
            StatusMessage = IsAutoTrackEnabled ? "Auto track select ON" : "Auto track select OFF";
        });

        // View mode commands
        ToggleGridCommand = new RelayCommand(() =>
        {
            IsGridOn = !IsGridOn;
        });

        ToggleDayNightCommand = new RelayCommand(() =>
        {
            IsDayMode = !IsDayMode;
            _mapService.SetDayMode(IsDayMode);
            ApplyThemeVariant(IsDayMode);
            // Disable auto day/night when user manually toggles theme
            ConfigurationStore.Instance.Display.AutoDayNight = false;
        });

        Toggle2D3DCommand = new RelayCommand(() =>
        {
            if (Is2DMode)
            {
                // Switching to 3D: restore last 3D pitch
                Is2DMode = false;
                CameraPitch = _last3DPitch;
                _mapService.Set3DMode(true);
            }
            else
            {
                // Switching to 2D: overhead view
                Is2DMode = true;
                CameraPitch = -90.0;
                _mapService.Set3DMode(false);
            }
        });

        ToggleNorthUpCommand = new RelayCommand(() =>
        {
            IsNorthUp = !IsNorthUp;
            _mapService.SetNorthUp(IsNorthUp);
        });

        ToggleCameraModeCommand = new RelayCommand(() =>
        {
            var oldMode = CameraMode;
            CameraMode = CameraMode switch
            {
                Models.CameraMode.Free => _previousCameraMode, // Return to previous mode
                Models.CameraMode.NorthUp => Models.CameraMode.HeadingUp,
                Models.CameraMode.HeadingUp => Models.CameraMode.NorthUp,
                _ => Models.CameraMode.NorthUp
            };
            Console.WriteLine($"[Compass] {oldMode} -> {CameraMode}");
        });

        // Camera controls - tilt transitions between 2D and 3D automatically
        // "Tilt Down" = look more toward horizon = more 3D (pitch increases toward -10)
        // "Tilt Up" = look more overhead = more 2D (pitch decreases toward -90)
        IncreaseCameraPitchCommand = new RelayCommand(() =>
        {
            double newPitch = CameraPitch - 5.0;
            if (newPitch <= -90.0)
            {
                Is2DMode = true;
                CameraPitch = -90.0;
                _mapService.Set3DMode(false);
            }
            else
            {
                CameraPitch = newPitch;
            }
        });

        DecreaseCameraPitchCommand = new RelayCommand(() =>
        {
            if (Is2DMode)
            {
                Is2DMode = false;
                CameraPitch = -85.0;
                _mapService.Set3DMode(true);
            }
            else
            {
                CameraPitch += 5.0;
            }
        });

        // Brightness controls
        IncreaseBrightnessCommand = new RelayCommand(() =>
        {
            Brightness += 5;
        });

        DecreaseBrightnessCommand = new RelayCommand(() =>
        {
            Brightness -= 5;
        });

        // Display resolution: cycle Ultra → High → Medium → Low → Min → Ultra
        CycleDisplayResolutionCommand = new RelayCommand(() =>
        {
            var display = ConfigStore.Display;
            display.DisplayResolutionMultiplier = display.DisplayResolutionMultiplier switch
            {
                < 1.25 => 1.5,  // Ultra → High
                < 2.0  => 2.5,  // High → Medium
                < 3.25 => 4.0,  // Medium → Low
                < 5.0  => 6.0,  // Low → Min
                _ => 1.0,       // Min → Ultra
            };
            OnPropertyChanged(nameof(DisplayResolutionLabel));
        });

        // iOS Sheet toggle commands
        ToggleFileMenuCommand = new RelayCommand(() =>
        {
            IsFileMenuVisible = !IsFileMenuVisible;
        });

        ToggleFieldToolsCommand = new RelayCommand(() =>
        {
            IsFieldToolsVisible = !IsFieldToolsVisible;
        });

        ToggleSettingsCommand = new RelayCommand(() =>
        {
            IsSettingsVisible = !IsSettingsVisible;
        });
    }

    /// <summary>
    /// Applies the Avalonia theme variant based on day/night mode.
    /// Day mode = Light theme, Night mode = Dark theme.
    /// </summary>
    internal static void ApplyThemeVariant(bool isDayMode)
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = isDayMode
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }
    }
}
