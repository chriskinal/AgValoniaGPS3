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

        // View mode commands
        ToggleGridCommand = new RelayCommand(() =>
        {
            IsGridOn = !IsGridOn;
        });

        ToggleDayNightCommand = new RelayCommand(() =>
        {
            IsDayMode = !IsDayMode;
        });

        Toggle2D3DCommand = new RelayCommand(() =>
        {
            Is2DMode = !Is2DMode;
        });

        ToggleNorthUpCommand = new RelayCommand(() =>
        {
            IsNorthUp = !IsNorthUp;
        });

        // Camera controls
        IncreaseCameraPitchCommand = new RelayCommand(() =>
        {
            CameraPitch += 5.0;
        });

        DecreaseCameraPitchCommand = new RelayCommand(() =>
        {
            CameraPitch -= 5.0;
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
}
