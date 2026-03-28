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
using AgValoniaGPS.Models.Configuration;
using Avalonia.Threading;
using ReactiveUI;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing View Settings and Panel Visibility.
/// Manages UI state for panels, display settings, and camera/brightness controls.
/// </summary>
public partial class MainViewModel
{
    #region Panel Visibility Fields

    private bool _isViewSettingsPanelVisible;
    private bool _isFileMenuPanelVisible;
    private bool _isToolsPanelVisible;
    private bool _isConfigurationPanelVisible;
    private bool _isJobMenuPanelVisible;
    private bool _isFieldToolsPanelVisible;
    private bool _isSimulatorPanelVisible;

    #endregion

    #region Panel Visibility Properties

    public bool IsViewSettingsPanelVisible
    {
        get => _isViewSettingsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isViewSettingsPanelVisible, value);
    }

    public bool IsFileMenuPanelVisible
    {
        get => _isFileMenuPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isFileMenuPanelVisible, value);
    }

    public bool IsToolsPanelVisible
    {
        get => _isToolsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isToolsPanelVisible, value);
    }

    public bool IsConfigurationPanelVisible
    {
        get => _isConfigurationPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isConfigurationPanelVisible, value);
    }

    public bool IsJobMenuPanelVisible
    {
        get => _isJobMenuPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isJobMenuPanelVisible, value);
    }

    public bool IsFieldToolsPanelVisible
    {
        get => _isFieldToolsPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isFieldToolsPanelVisible, value);
    }

    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isSimulatorPanelVisible, value);
    }

    #endregion

    #region Display Settings Properties

    // Navigation settings properties (forwarded from service)
    public bool IsGridOn
    {
        get => _displaySettings.IsGridOn;
        set
        {
            _displaySettings.IsGridOn = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsDayMode
    {
        get => _displaySettings.IsDayMode;
        set
        {
            _displaySettings.IsDayMode = value;
            this.RaisePropertyChanged();
        }
    }

    public double CameraPitch
    {
        get => _displaySettings.CameraPitch;
        set
        {
            _displaySettings.CameraPitch = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Is2DMode));
        }
    }

    public bool Is2DMode
    {
        get => _displaySettings.Is2DMode;
        set
        {
            _displaySettings.Is2DMode = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsNorthUp
    {
        get => _displaySettings.IsNorthUp;
        set
        {
            _displaySettings.IsNorthUp = value;
            this.RaisePropertyChanged();
        }
    }

    public int Brightness
    {
        get => _displaySettings.Brightness;
        set
        {
            _displaySettings.Brightness = value;
            _mapService.SetBrightness(value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(BrightnessDisplay));
        }
    }

    public string BrightnessDisplay => $"{_displaySettings.Brightness}%";

    #endregion

    #region Auto Day/Night

    private DispatcherTimer? _autoDayNightTimer;

    private void InitializeAutoDayNight()
    {
        _autoDayNightTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _autoDayNightTimer.Tick += (_, _) => CheckAutoDayNight();
        _autoDayNightTimer.Start();
    }

    /// <summary>
    /// Switch day/night mode automatically based on local time.
    /// Day: 6:00 AM to 8:00 PM, Night: 8:00 PM to 6:00 AM.
    /// Only applies when AutoDayNight is enabled in DisplayConfig.
    /// </summary>
    private void CheckAutoDayNight()
    {
        if (!ConfigurationStore.Instance.Display.AutoDayNight) return;

        int hour = DateTime.Now.Hour;
        bool shouldBeDay = hour >= 6 && hour < 20;

        if (IsDayMode != shouldBeDay)
        {
            IsDayMode = shouldBeDay;
            _mapService.SetDayMode(shouldBeDay);
        }
    }

    #endregion

    #region ConfigurationStore Display Forwarding

    /// <summary>Whether speedometer display is visible (from DisplayConfig).</summary>
    public bool IsSpeedometerVisible =>
        ConfigurationStore.Instance.Display.SpeedometerVisible;

    /// <summary>Whether headland distance overlay is visible (from DisplayConfig).</summary>
    public bool IsHeadlandDistanceVisible =>
        ConfigurationStore.Instance.Display.HeadlandDistanceVisible;

    /// <summary>
    /// UTurn button visible when track available AND config allows it.
    /// </summary>
    public bool IsUTurnButtonVisible =>
        IsAutoSteerAvailable && ConfigurationStore.Instance.Display.UTurnButtonVisible;

    /// <summary>
    /// Notify IsUTurnButtonVisible when IsAutoSteerAvailable changes.
    /// Called from MainViewModel.Guidance.cs when track state changes.
    /// </summary>
    private void RaiseUTurnButtonVisibleChanged()
    {
        this.RaisePropertyChanged(nameof(IsUTurnButtonVisible));
    }

    #endregion
}
