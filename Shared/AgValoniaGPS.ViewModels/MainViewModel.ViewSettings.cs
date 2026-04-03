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
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Services;
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
    private bool _isSteerChartPanelVisible;
    private bool _isHeadingChartPanelVisible;
    private bool _isXTEChartPanelVisible;

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

    public bool IsSteerChartPanelVisible
    {
        get => _isSteerChartPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isSteerChartPanelVisible, value);
    }

    public bool IsHeadingChartPanelVisible
    {
        get => _isHeadingChartPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isHeadingChartPanelVisible, value);
    }

    public bool IsXTEChartPanelVisible
    {
        get => _isXTEChartPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _isXTEChartPanelVisible, value);
    }

    #endregion

    #region Clock

    private string _currentTime = "";
    public string CurrentTime
    {
        get => _currentTime;
        private set => this.RaiseAndSetIfChanged(ref _currentTime, value);
    }

    private void InitializeClock()
    {
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        var clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        clockTimer.Start();
    }

    #endregion

    #region Camera Mode

    private CameraMode _cameraMode = CameraMode.NorthUp;
    private CameraMode _previousCameraMode = CameraMode.NorthUp;
    public CameraMode CameraMode
    {
        get => _cameraMode;
        set
        {
            var old = _cameraMode;
            this.RaiseAndSetIfChanged(ref _cameraMode, value);
            if (old != value)
            {
                this.RaisePropertyChanged(nameof(CameraModeLabel));
                ApplyCameraMode();
            }
        }
    }

    public string CameraModeLabel => _cameraMode switch
    {
        CameraMode.NorthUp => "N",
        CameraMode.HeadingUp => "H",
        CameraMode.Free => "C",  // "Center" -- tap to recenter on vehicle
        _ => "?"
    };

    private void ApplyCameraMode()
    {
        // Set camera follow mode directly on map control: 0=NorthUp, 1=HeadingUp, 2=Free
        int mapMode = _cameraMode switch
        {
            CameraMode.NorthUp => 0,
            CameraMode.HeadingUp => 1,
            CameraMode.Free => 2,
            _ => 0
        };
        var camPos = _mapService.GetCameraCenter();
        Console.WriteLine($"[Camera] ApplyCameraMode: {_cameraMode} (mapMode={mapMode}) cam=({camPos.X:F1},{camPos.Y:F1}) vehicle=({Easting:F1},{Northing:F1})");
        _mapService.SetCameraFollowMode(mapMode);

        // When switching FROM Free to a follow mode, immediately center on vehicle
        if (_cameraMode != CameraMode.Free)
        {
            _mapService.PanTo(Easting, Northing);
            Console.WriteLine($"[Camera] Recentered to ({Easting:F1},{Northing:F1})");
        }

        IsNorthUp = _cameraMode == CameraMode.NorthUp;
    }

    /// <summary>
    /// Called when user manually pans the map -- enters Free mode.
    /// </summary>
    public void OnUserPan()
    {
        if (_cameraMode != CameraMode.Free)
        {
            _previousCameraMode = _cameraMode;
            Console.WriteLine($"[Camera] OnUserPan: {_cameraMode} -> Free (prev={_previousCameraMode})");
            CameraMode = CameraMode.Free; // Use property setter to trigger ApplyCameraMode
        }
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
            _mapService.SetDayMode(value);
            ApplyThemeVariant(value);
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
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(BrightnessDisplay));
        }
    }

    public string BrightnessDisplay => _displaySettings.IsBrightnessSupported
        ? $"{_displaySettings.Brightness}%"
        : "??";

    #endregion

    #region Auto Day/Night

    private DispatcherTimer? _autoDayNightTimer;

    private void InitializeAutoDayNight()
    {
        CheckAutoDayNight();
        _autoDayNightTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _autoDayNightTimer.Tick += (_, _) => CheckAutoDayNight();
        _autoDayNightTimer.Start();
    }

    /// <summary>
    /// Switch day/night mode automatically based on solar position.
    /// Uses GPS-based sunrise/sunset when available, falls back to configured hours.
    /// GPS-based only when AutoDayNight is enabled.
    /// </summary>
    private void CheckAutoDayNight()
    {
        var display = ConfigurationStore.Instance.Display;
        if (!display.AutoDayNight) return;
        
        bool shouldBeDay = false;
        // Try GPS-based solar calculation when we have a valid position
        if (_gpsService.IsGpsDataOk() && display.AutoDayNight)
        {
            shouldBeDay = SolarCalculator.IsDay(Latitude, Longitude, DateTime.UtcNow);
        }
        else
        {
            // Fallback to configurable hours
            int hour = DateTime.Now.Hour;
            int dayStart = display.DayStartHour;
            int nightStart = display.NightStartHour;

            if (dayStart < nightStart)
                shouldBeDay = hour >= dayStart && hour < nightStart;
            else
                // Handles wrap-around (e.g. day=22, night=6 for night-shift work)
                shouldBeDay = hour >= dayStart || hour < nightStart;
        }
        if (IsDayMode != shouldBeDay)
        {
            IsDayMode = shouldBeDay;
            _mapService.SetDayMode(shouldBeDay);
        }
    }

    #endregion

    #region ConfigurationStore Display Forwarding

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
