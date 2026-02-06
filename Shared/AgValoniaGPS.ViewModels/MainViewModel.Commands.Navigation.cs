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

        // View mode commands
        ToggleGridCommand = ReactiveCommand.Create(() =>
        {
            IsGridOn = !IsGridOn;
        });

        ToggleDayNightCommand = ReactiveCommand.Create(() =>
        {
            IsDayMode = !IsDayMode;
        });

        Toggle2D3DCommand = ReactiveCommand.Create(() =>
        {
            Is2DMode = !Is2DMode;
        });

        ToggleNorthUpCommand = ReactiveCommand.Create(() =>
        {
            IsNorthUp = !IsNorthUp;
        });

        // Camera controls
        IncreaseCameraPitchCommand = ReactiveCommand.Create(() =>
        {
            CameraPitch += 5.0;
        });

        DecreaseCameraPitchCommand = ReactiveCommand.Create(() =>
        {
            CameraPitch -= 5.0;
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
}
