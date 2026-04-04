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

using System.Reactive;
using Avalonia;
using Avalonia.Styling;
using ReactiveUI;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Navigation panel commands - view toggles, camera controls, brightness.
/// </summary>
public partial class MainViewModel
{
    private void InitializeNavigationCommands()
    {
        // Panel toggle commands
        ToggleViewSettingsPanelCommand = ReactiveCommand.Create(() =>
        {
            IsViewSettingsPanelVisible = !IsViewSettingsPanelVisible;
        });

        ToggleFileMenuPanelCommand = ReactiveCommand.Create(() =>
        {
            IsFileMenuPanelVisible = !IsFileMenuPanelVisible;
        });

        ToggleToolsPanelCommand = ReactiveCommand.Create(() =>
        {
            IsToolsPanelVisible = !IsToolsPanelVisible;
        });

        ToggleConfigurationPanelCommand = ReactiveCommand.Create(() =>
        {
            IsConfigurationPanelVisible = !IsConfigurationPanelVisible;
        });

        ToggleJobMenuPanelCommand = ReactiveCommand.Create(() =>
        {
            IsJobMenuPanelVisible = !IsJobMenuPanelVisible;
        });

        ToggleFieldToolsPanelCommand = ReactiveCommand.Create(() =>
        {
            IsFieldToolsPanelVisible = !IsFieldToolsPanelVisible;
        });

        ToggleAutoTrackCommand = ReactiveCommand.Create(() =>
        {
            IsAutoTrackEnabled = !IsAutoTrackEnabled;
            StatusMessage = IsAutoTrackEnabled ? "Auto track select ON" : "Auto track select OFF";
        });

        // View mode commands
        ToggleGridCommand = ReactiveCommand.Create(() =>
        {
            IsGridOn = !IsGridOn;
        });

        ToggleDayNightCommand = ReactiveCommand.Create(() =>
        {
            IsDayMode = !IsDayMode;
            _mapService.SetDayMode(IsDayMode);
            ApplyThemeVariant(IsDayMode);
        });

        Toggle2D3DCommand = ReactiveCommand.Create(() =>
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

        ToggleNorthUpCommand = ReactiveCommand.Create(() =>
        {
            IsNorthUp = !IsNorthUp;
            _mapService.SetNorthUp(IsNorthUp);
        });

        ToggleCameraModeCommand = ReactiveCommand.Create(() =>
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
        IncreaseCameraPitchCommand = ReactiveCommand.Create(() =>
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

        DecreaseCameraPitchCommand = ReactiveCommand.Create(() =>
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
        IncreaseBrightnessCommand = ReactiveCommand.Create(() =>
        {
            Brightness += 5;
        });

        DecreaseBrightnessCommand = ReactiveCommand.Create(() =>
        {
            Brightness -= 5;
        });

        // iOS Sheet toggle commands
        ToggleFileMenuCommand = ReactiveCommand.Create(() =>
        {
            IsFileMenuVisible = !IsFileMenuVisible;
        });

        ToggleFieldToolsCommand = ReactiveCommand.Create(() =>
        {
            IsFieldToolsVisible = !IsFieldToolsVisible;
        });

        ToggleSettingsCommand = ReactiveCommand.Create(() =>
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
