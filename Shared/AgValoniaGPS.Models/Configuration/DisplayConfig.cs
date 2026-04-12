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
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Display and UI configuration.
/// Replaces: display parts of AppSettings, DisplaySettingsService state
/// </summary>
public class DisplayConfig : ObservableObject
{
    // Map display
    private bool _gridVisible = true;
    public bool GridVisible
    {
        get => _gridVisible;
        set => SetProperty(ref _gridVisible, value);
    }

    private bool _compassVisible = true;
    public bool CompassVisible
    {
        get => _compassVisible;
        set => SetProperty(ref _compassVisible, value);
    }

    private bool _speedVisible = true;
    public bool SpeedVisible
    {
        get => _speedVisible;
        set => SetProperty(ref _speedVisible, value);
    }

    // Camera
    private double _cameraZoom = 100.0;
    public double CameraZoom
    {
        get => _cameraZoom;
        set => SetProperty(ref _cameraZoom, value);
    }

    private double _cameraPitch = -60.0;
    public double CameraPitch
    {
        get => _cameraPitch;
        set => SetProperty(ref _cameraPitch, Math.Clamp(value, -90, -20));
    }

    private bool _is2DMode;
    public bool Is2DMode
    {
        get => _is2DMode;
        set => SetProperty(ref _is2DMode, value);
    }

    private bool _isNorthUp = false;
    public bool IsNorthUp
    {
        get => _isNorthUp;
        set => SetProperty(ref _isNorthUp, value);
    }

    private bool _isDayMode = true;
    public bool IsDayMode
    {
        get => _isDayMode;
        set => SetProperty(ref _isDayMode, value);
    }

    // Window (Desktop only, ignored on iOS)
    private double _windowWidth = 1200;
    public double WindowWidth
    {
        get => _windowWidth;
        set => SetProperty(ref _windowWidth, value);
    }

    private double _windowHeight = 800;
    public double WindowHeight
    {
        get => _windowHeight;
        set => SetProperty(ref _windowHeight, value);
    }

    private double _windowX = 100;
    public double WindowX
    {
        get => _windowX;
        set => SetProperty(ref _windowX, value);
    }

    private double _windowY = 100;
    public double WindowY
    {
        get => _windowY;
        set => SetProperty(ref _windowY, value);
    }

    private bool _windowMaximized;
    public bool WindowMaximized
    {
        get => _windowMaximized;
        set => SetProperty(ref _windowMaximized, value);
    }

    // Panel positions
    private double _simulatorPanelX = double.NaN;
    public double SimulatorPanelX
    {
        get => _simulatorPanelX;
        set => SetProperty(ref _simulatorPanelX, value);
    }

    private double _simulatorPanelY = double.NaN;
    public double SimulatorPanelY
    {
        get => _simulatorPanelY;
        set => SetProperty(ref _simulatorPanelY, value);
    }

    private bool _simulatorPanelVisible;
    public bool SimulatorPanelVisible
    {
        get => _simulatorPanelVisible;
        set => SetProperty(ref _simulatorPanelVisible, value);
    }

    // Display Options (toggle buttons)
    private bool _polygonsVisible = true;
    public bool PolygonsVisible
    {
        get => _polygonsVisible;
        set => SetProperty(ref _polygonsVisible, value);
    }

    private bool _speedometerVisible = true;
    public bool SpeedometerVisible
    {
        get => _speedometerVisible;
        set => SetProperty(ref _speedometerVisible, value);
    }

    private bool _keyboardEnabled;
    public bool KeyboardEnabled
    {
        get => _keyboardEnabled;
        set => SetProperty(ref _keyboardEnabled, value);
    }

    private bool _headlandDistanceVisible = true;
    public bool HeadlandDistanceVisible
    {
        get => _headlandDistanceVisible;
        set => SetProperty(ref _headlandDistanceVisible, value);
    }

    private bool _autoDayNight = true;
    public bool AutoDayNight
    {
        get => _autoDayNight;
        set => SetProperty(ref _autoDayNight, value);
    }

    private int _dayStartHour = 6;
    public int DayStartHour
    {
        get => _dayStartHour;
        set => SetProperty(ref _dayStartHour, Math.Clamp(value, 0, 23));
    }

    private int _nightStartHour = 20;
    public int NightStartHour
    {
        get => _nightStartHour;
        set => SetProperty(ref _nightStartHour, Math.Clamp(value, 0, 23));
    }

    private bool _svennArrowVisible;
    public bool SvennArrowVisible
    {
        get => _svennArrowVisible;
        set => SetProperty(ref _svennArrowVisible, value);
    }

    private bool _startFullscreen;
    public bool StartFullscreen
    {
        get => _startFullscreen;
        set => SetProperty(ref _startFullscreen, value);
    }

    private bool _elevationLogEnabled;
    public bool ElevationLogEnabled
    {
        get => _elevationLogEnabled;
        set => SetProperty(ref _elevationLogEnabled, value);
    }

    private bool _fieldTextureVisible = true;
    public bool FieldTextureVisible
    {
        get => _fieldTextureVisible;
        set => SetProperty(ref _fieldTextureVisible, value);
    }

    private bool _extraGuidelines;
    public bool ExtraGuidelines
    {
        get => _extraGuidelines;
        set => SetProperty(ref _extraGuidelines, value);
    }

    private int _extraGuidelinesCount = 10;
    public int ExtraGuidelinesCount
    {
        get => _extraGuidelinesCount;
        set => SetProperty(ref _extraGuidelinesCount, Math.Clamp(value, 1, 50));
    }

    private bool _lineSmoothEnabled = true;
    public bool LineSmoothEnabled
    {
        get => _lineSmoothEnabled;
        set => SetProperty(ref _lineSmoothEnabled, value);
    }

    private bool _directionMarkersVisible;
    public bool DirectionMarkersVisible
    {
        get => _directionMarkersVisible;
        set => SetProperty(ref _directionMarkersVisible, value);
    }

    private bool _sectionLinesVisible = true;
    public bool SectionLinesVisible
    {
        get => _sectionLinesVisible;
        set => SetProperty(ref _sectionLinesVisible, value);
    }

    // Screen Buttons (visibility of UI buttons)
    private bool _uTurnButtonVisible = true;
    public bool UTurnButtonVisible
    {
        get => _uTurnButtonVisible;
        set => SetProperty(ref _uTurnButtonVisible, value);
    }

    private bool _lateralButtonVisible = true;
    public bool LateralButtonVisible
    {
        get => _lateralButtonVisible;
        set => SetProperty(ref _lateralButtonVisible, value);
    }

    // Sounds
    private bool _autoSteerSound = true;
    public bool AutoSteerSound
    {
        get => _autoSteerSound;
        set => SetProperty(ref _autoSteerSound, value);
    }

    private bool _uTurnSound = true;
    public bool UTurnSound
    {
        get => _uTurnSound;
        set => SetProperty(ref _uTurnSound, value);
    }

    private bool _hydraulicSound = true;
    public bool HydraulicSound
    {
        get => _hydraulicSound;
        set => SetProperty(ref _hydraulicSound, value);
    }

    private bool _sectionsSound = true;
    public bool SectionsSound
    {
        get => _sectionsSound;
        set => SetProperty(ref _sectionsSound, value);
    }

    // Hardware Messages
    private bool _hardwareMessagesEnabled = true;
    public bool HardwareMessagesEnabled
    {
        get => _hardwareMessagesEnabled;
        set => SetProperty(ref _hardwareMessagesEnabled, value);
    }

    // Coverage display resolution multiplier
    // 1.0 = Ultra, 1.5 = High, 2.5 = Medium, 4.0 = Low, 6.0 = Minimum
    // Applied to coverage bitmap cell size — detection always stays at 0.1m
    private double _displayResolutionMultiplier = 1.0;
    public double DisplayResolutionMultiplier
    {
        get => _displayResolutionMultiplier;
        set => SetProperty(ref _displayResolutionMultiplier, Math.Clamp(value, 1.0, 6.0));
    }
}
