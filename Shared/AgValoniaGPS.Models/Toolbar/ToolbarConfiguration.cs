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

using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.Toolbar;

public class ToolbarConfiguration : ObservableObject
{
    public List<ShortcutLayout> ShortcutLayouts { get; set; } = new();

    private string? _activeLayoutName;
    public string? ActiveLayoutName
    {
        get => _activeLayoutName;
        set => SetProperty(ref _activeLayoutName, value);
    }

    // Auto-hide settings
    private bool _autoHideToolbars;
    public bool AutoHideToolbars
    {
        get => _autoHideToolbars;
        set => SetProperty(ref _autoHideToolbars, value);
    }

    private double _autoHideSpeedThreshold = 1.5;
    public double AutoHideSpeedThreshold
    {
        get => _autoHideSpeedThreshold;
        set => SetProperty(ref _autoHideSpeedThreshold, value);
    }

    private double _autoHideOpacity = 0.15;
    public double AutoHideOpacity
    {
        get => _autoHideOpacity;
        set => SetProperty(ref _autoHideOpacity, value);
    }

    private double _autoHideTimeout = 5.0;
    public double AutoHideTimeout
    {
        get => _autoHideTimeout;
        set => SetProperty(ref _autoHideTimeout, value);
    }

    public static ShortcutLayout CreateDefaultLayout()
    {
        return new ShortcutLayout
        {
            Name = "Default",
            Shortcuts = new List<ToolbarShortcut>
            {
                new() { ButtonId = "ToggleSectionOverlay" },
                new() { ButtonId = "ToggleAutoSteer" },
                new() { ButtonId = "ToggleContourMode" },
                new() { ButtonId = "ToggleSectionMaster" },
                new() { ButtonId = "ToggleManualMode" },
                new() { ButtonId = "ToggleHeadland" },
                new() { ButtonId = "SnapLeft" },
                new() { ButtonId = "SnapRight" },
            }
        };
    }
}
