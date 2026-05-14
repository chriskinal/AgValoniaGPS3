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


using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// MainViewModel partial class containing View Settings and Panel Visibility.
/// Manages UI state for panels, display settings, and camera/display controls.
/// </summary>
public partial class MainViewModel
{
    #region Panel Visibility Fields

    private bool _isViewSettingsPanelVisible;
    private bool _isFileMenuPanelVisible;
    private bool _isToolsPanelVisible;
    private bool _isConfigurationPanelVisible;
    private bool _isFieldOperationsPanelVisible;
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
        set => SetProperty(ref _isViewSettingsPanelVisible, value);
    }

    public bool IsFileMenuPanelVisible
    {
        get => _isFileMenuPanelVisible;
        set => SetProperty(ref _isFileMenuPanelVisible, value);
    }

    public bool IsToolsPanelVisible
    {
        get => _isToolsPanelVisible;
        set => SetProperty(ref _isToolsPanelVisible, value);
    }

    public bool IsConfigurationPanelVisible
    {
        get => _isConfigurationPanelVisible;
        set => SetProperty(ref _isConfigurationPanelVisible, value);
    }

    public bool IsFieldOperationsPanelVisible
    {
        get => _isFieldOperationsPanelVisible;
        set => SetProperty(ref _isFieldOperationsPanelVisible, value);
    }

    public bool IsFieldToolsPanelVisible
    {
        get => _isFieldToolsPanelVisible;
        set => SetProperty(ref _isFieldToolsPanelVisible, value);
    }

    public bool IsSimulatorPanelVisible
    {
        get => _isSimulatorPanelVisible;
        set => SetProperty(ref _isSimulatorPanelVisible, value);
    }

    public bool IsSteerChartPanelVisible
    {
        get => _isSteerChartPanelVisible;
        set => SetProperty(ref _isSteerChartPanelVisible, value);
    }

    public bool IsHeadingChartPanelVisible
    {
        get => _isHeadingChartPanelVisible;
        set => SetProperty(ref _isHeadingChartPanelVisible, value);
    }

    public bool IsXTEChartPanelVisible
    {
        get => _isXTEChartPanelVisible;
        set => SetProperty(ref _isXTEChartPanelVisible, value);
    }

    #endregion

    #region Clock

    private string _currentTime = "";
    public string CurrentTime
    {
        get => _currentTime;
        private set => SetProperty(ref _currentTime, value);
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

    private CameraMode _cameraMode = CameraMode.Map;
    private CameraMode _previousCameraMode = CameraMode.Map;
    public CameraMode CameraMode
    {
        get => _cameraMode;
        set
        {
            var old = _cameraMode;
            SetProperty(ref _cameraMode, value);
            if (old != value)
            {
                OnPropertyChanged(nameof(CameraModeLabel));
                ApplyCameraMode();
            }
        }
    }

    public string CameraModeLabel => _cameraMode switch
    {
        CameraMode.NorthUp => "N",
        CameraMode.HeadingUp => "H",
        CameraMode.Map => "M",
        CameraMode.Free => "C",  // "Center" -- tap to recenter on vehicle
        _ => "?"
    };

    private void ApplyCameraMode()
    {
        // Set camera follow mode directly on map control: 0=NorthUp, 1=HeadingUp, 2=Free, 3=Map
        int mapMode = _cameraMode switch
        {
            CameraMode.NorthUp => 0,
            CameraMode.HeadingUp => 1,
            CameraMode.Free => 2,
            CameraMode.Map => 3,
            _ => 3
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

        // Persist last actively-chosen follow mode (Free is transient -- entered by
        // pan, not a mode the user explicitly picks to keep).
        if (_cameraMode != CameraMode.Free)
            ConfigStore.Display.CameraMode = _cameraMode;
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
            OnPropertyChanged();
        }
    }

    public bool IsDayMode
    {
        get => _displaySettings.IsDayMode;
        set
        {
            _displaySettings.IsDayMode = value;
            OnPropertyChanged();
            _mapService.SetDayMode(value);
            ApplyThemeVariant(value);
        }
    }

    private double _last3DPitch = -60.0;

    public double CameraPitch
    {
        get => _displaySettings.CameraPitch;
        set
        {
            _displaySettings.CameraPitch = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Is2DMode));
            OnPropertyChanged(nameof(CameraPitchDisplay));
            // Remember last 3D pitch for restoring when toggling back from 2D
            if (value > -89.0)
                _last3DPitch = value;
        }
    }

    public string CameraPitchDisplay
    {
        get
        {
            double pitch = _displaySettings.CameraPitch;
            if (pitch <= -89.0) return "2D (overhead)";
            // Convert: -90 = overhead, -10 = nearly horizontal
            // Show as tilt angle: 0 = overhead, 80 = horizontal
            double tilt = 90.0 + pitch;
            return $"Tilt: {tilt:F0} deg";
        }
    }

    public bool Is2DMode
    {
        get => _displaySettings.Is2DMode;
        set
        {
            _displaySettings.Is2DMode = value;
            OnPropertyChanged();
        }
    }

    public bool IsNorthUp
    {
        get => _displaySettings.IsNorthUp;
        set
        {
            _displaySettings.IsNorthUp = value;
            OnPropertyChanged();
        }
    }

    public string DisplayResolutionLabel => ConfigStore.Display.DisplayResolutionMultiplier switch
    {
        < 1.25 => "Ultra",
        < 2.0  => "High",
        < 3.25 => "Med",
        < 5.0  => "Low",
        _ => "Min",
    };

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
        OnPropertyChanged(nameof(IsUTurnButtonVisible));
    }

    #endregion
}
