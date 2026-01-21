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
using ReactiveUI;

namespace AgValoniaGPS.Models.Configuration;

/// <summary>
/// Display and UI configuration.
/// Replaces: display parts of AppSettings, DisplaySettingsService state
/// </summary>
public class DisplayConfig : ReactiveObject
{
    // Map display
    private bool _gridVisible = true;
    public bool GridVisible
    {
        get => _gridVisible;
        set => this.RaiseAndSetIfChanged(ref _gridVisible, value);
    }

    private bool _compassVisible = true;
    public bool CompassVisible
    {
        get => _compassVisible;
        set => this.RaiseAndSetIfChanged(ref _compassVisible, value);
    }

    private bool _speedVisible = true;
    public bool SpeedVisible
    {
        get => _speedVisible;
        set => this.RaiseAndSetIfChanged(ref _speedVisible, value);
    }

    // Camera
    private double _cameraZoom = 100.0;
    public double CameraZoom
    {
        get => _cameraZoom;
        set => this.RaiseAndSetIfChanged(ref _cameraZoom, value);
    }

    private double _cameraPitch = -62.0;
    public double CameraPitch
    {
        get => _cameraPitch;
        set => this.RaiseAndSetIfChanged(ref _cameraPitch, Math.Clamp(value, -90, -10));
    }

    private bool _is2DMode;
    public bool Is2DMode
    {
        get => _is2DMode;
        set => this.RaiseAndSetIfChanged(ref _is2DMode, value);
    }

    private bool _isNorthUp = true;
    public bool IsNorthUp
    {
        get => _isNorthUp;
        set => this.RaiseAndSetIfChanged(ref _isNorthUp, value);
    }

    private bool _isDayMode = true;
    public bool IsDayMode
    {
        get => _isDayMode;
        set => this.RaiseAndSetIfChanged(ref _isDayMode, value);
    }

    // Window (Desktop only, ignored on iOS)
    private double _windowWidth = 1200;
    public double WindowWidth
    {
        get => _windowWidth;
        set => this.RaiseAndSetIfChanged(ref _windowWidth, value);
    }

    private double _windowHeight = 800;
    public double WindowHeight
    {
        get => _windowHeight;
        set => this.RaiseAndSetIfChanged(ref _windowHeight, value);
    }

    private double _windowX = 100;
    public double WindowX
    {
        get => _windowX;
        set => this.RaiseAndSetIfChanged(ref _windowX, value);
    }

    private double _windowY = 100;
    public double WindowY
    {
        get => _windowY;
        set => this.RaiseAndSetIfChanged(ref _windowY, value);
    }

    private bool _windowMaximized;
    public bool WindowMaximized
    {
        get => _windowMaximized;
        set => this.RaiseAndSetIfChanged(ref _windowMaximized, value);
    }

    // Panel positions
    private double _simulatorPanelX = double.NaN;
    public double SimulatorPanelX
    {
        get => _simulatorPanelX;
        set => this.RaiseAndSetIfChanged(ref _simulatorPanelX, value);
    }

    private double _simulatorPanelY = double.NaN;
    public double SimulatorPanelY
    {
        get => _simulatorPanelY;
        set => this.RaiseAndSetIfChanged(ref _simulatorPanelY, value);
    }

    private bool _simulatorPanelVisible;
    public bool SimulatorPanelVisible
    {
        get => _simulatorPanelVisible;
        set => this.RaiseAndSetIfChanged(ref _simulatorPanelVisible, value);
    }

    // Left Navigation Panel position
    private double _leftNavPanelX = double.NaN;
    public double LeftNavPanelX
    {
        get => _leftNavPanelX;
        set => this.RaiseAndSetIfChanged(ref _leftNavPanelX, value);
    }

    private double _leftNavPanelY = double.NaN;
    public double LeftNavPanelY
    {
        get => _leftNavPanelY;
        set => this.RaiseAndSetIfChanged(ref _leftNavPanelY, value);
    }

    // Right Navigation Panel position
    private double _rightNavPanelX = double.NaN;
    public double RightNavPanelX
    {
        get => _rightNavPanelX;
        set => this.RaiseAndSetIfChanged(ref _rightNavPanelX, value);
    }

    private double _rightNavPanelY = double.NaN;
    public double RightNavPanelY
    {
        get => _rightNavPanelY;
        set => this.RaiseAndSetIfChanged(ref _rightNavPanelY, value);
    }

    // Bottom Navigation Panel position
    private double _bottomNavPanelX = double.NaN;
    public double BottomNavPanelX
    {
        get => _bottomNavPanelX;
        set => this.RaiseAndSetIfChanged(ref _bottomNavPanelX, value);
    }

    private double _bottomNavPanelY = double.NaN;
    public double BottomNavPanelY
    {
        get => _bottomNavPanelY;
        set => this.RaiseAndSetIfChanged(ref _bottomNavPanelY, value);
    }

    // Section Control Panel position
    private double _sectionPanelX = double.NaN;
    public double SectionPanelX
    {
        get => _sectionPanelX;
        set => this.RaiseAndSetIfChanged(ref _sectionPanelX, value);
    }

    private double _sectionPanelY = double.NaN;
    public double SectionPanelY
    {
        get => _sectionPanelY;
        set => this.RaiseAndSetIfChanged(ref _sectionPanelY, value);
    }

    // Display Options (toggle buttons)
    private bool _polygonsVisible = true;
    public bool PolygonsVisible
    {
        get => _polygonsVisible;
        set => this.RaiseAndSetIfChanged(ref _polygonsVisible, value);
    }

    private bool _speedometerVisible = true;
    public bool SpeedometerVisible
    {
        get => _speedometerVisible;
        set => this.RaiseAndSetIfChanged(ref _speedometerVisible, value);
    }

    private bool _keyboardEnabled;
    public bool KeyboardEnabled
    {
        get => _keyboardEnabled;
        set => this.RaiseAndSetIfChanged(ref _keyboardEnabled, value);
    }

    private bool _headlandDistanceVisible = true;
    public bool HeadlandDistanceVisible
    {
        get => _headlandDistanceVisible;
        set => this.RaiseAndSetIfChanged(ref _headlandDistanceVisible, value);
    }

    private bool _autoDayNight;
    public bool AutoDayNight
    {
        get => _autoDayNight;
        set => this.RaiseAndSetIfChanged(ref _autoDayNight, value);
    }

    private bool _svennArrowVisible;
    public bool SvennArrowVisible
    {
        get => _svennArrowVisible;
        set => this.RaiseAndSetIfChanged(ref _svennArrowVisible, value);
    }

    private bool _startFullscreen;
    public bool StartFullscreen
    {
        get => _startFullscreen;
        set => this.RaiseAndSetIfChanged(ref _startFullscreen, value);
    }

    private bool _elevationLogEnabled;
    public bool ElevationLogEnabled
    {
        get => _elevationLogEnabled;
        set => this.RaiseAndSetIfChanged(ref _elevationLogEnabled, value);
    }

    private bool _fieldTextureVisible;
    public bool FieldTextureVisible
    {
        get => _fieldTextureVisible;
        set => this.RaiseAndSetIfChanged(ref _fieldTextureVisible, value);
    }

    private bool _extraGuidelines;
    public bool ExtraGuidelines
    {
        get => _extraGuidelines;
        set => this.RaiseAndSetIfChanged(ref _extraGuidelines, value);
    }

    private int _extraGuidelinesCount = 10;
    public int ExtraGuidelinesCount
    {
        get => _extraGuidelinesCount;
        set => this.RaiseAndSetIfChanged(ref _extraGuidelinesCount, Math.Clamp(value, 1, 50));
    }

    private bool _lineSmoothEnabled = true;
    public bool LineSmoothEnabled
    {
        get => _lineSmoothEnabled;
        set => this.RaiseAndSetIfChanged(ref _lineSmoothEnabled, value);
    }

    private bool _directionMarkersVisible;
    public bool DirectionMarkersVisible
    {
        get => _directionMarkersVisible;
        set => this.RaiseAndSetIfChanged(ref _directionMarkersVisible, value);
    }

    private bool _sectionLinesVisible = true;
    public bool SectionLinesVisible
    {
        get => _sectionLinesVisible;
        set => this.RaiseAndSetIfChanged(ref _sectionLinesVisible, value);
    }

    // Screen Buttons (visibility of UI buttons)
    private bool _uTurnButtonVisible = true;
    public bool UTurnButtonVisible
    {
        get => _uTurnButtonVisible;
        set => this.RaiseAndSetIfChanged(ref _uTurnButtonVisible, value);
    }

    private bool _lateralButtonVisible = true;
    public bool LateralButtonVisible
    {
        get => _lateralButtonVisible;
        set => this.RaiseAndSetIfChanged(ref _lateralButtonVisible, value);
    }

    // Sounds
    private bool _autoSteerSound = true;
    public bool AutoSteerSound
    {
        get => _autoSteerSound;
        set => this.RaiseAndSetIfChanged(ref _autoSteerSound, value);
    }

    private bool _uTurnSound = true;
    public bool UTurnSound
    {
        get => _uTurnSound;
        set => this.RaiseAndSetIfChanged(ref _uTurnSound, value);
    }

    private bool _hydraulicSound = true;
    public bool HydraulicSound
    {
        get => _hydraulicSound;
        set => this.RaiseAndSetIfChanged(ref _hydraulicSound, value);
    }

    private bool _sectionsSound = true;
    public bool SectionsSound
    {
        get => _sectionsSound;
        set => this.RaiseAndSetIfChanged(ref _sectionsSound, value);
    }

    // Hardware Messages
    private bool _hardwareMessagesEnabled = true;
    public bool HardwareMessagesEnabled
    {
        get => _hardwareMessagesEnabled;
        set => this.RaiseAndSetIfChanged(ref _hardwareMessagesEnabled, value);
    }
}
