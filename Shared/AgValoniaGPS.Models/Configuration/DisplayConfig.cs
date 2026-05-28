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
using AgValoniaGPS.Models;

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

    // Auto-close dialogs/menus after this many seconds of no interaction with
    // the open surface (matches AgOpenGPS). 0 disables. Behavior preference.
    /// <summary>Default countdown when the idle auto-close is enabled.</summary>
    public const double DefaultDialogAutoCloseSeconds = 7.0;

    // Off by default — idle auto-close is opt-in via the Screen & Alerts toggle.
    private double _dialogAutoCloseSeconds;
    public double DialogAutoCloseSeconds
    {
        get => _dialogAutoCloseSeconds;
        set
        {
            if (SetProperty(ref _dialogAutoCloseSeconds, value))
                OnPropertyChanged(nameof(DialogAutoCloseEnabled));
        }
    }

    /// <summary>
    /// Display-option view of <see cref="DialogAutoCloseSeconds"/>: on = the
    /// default countdown, off = 0 (never auto-close). Bound by the Screen &amp;
    /// Alerts "Auto-close menus" toggle.
    /// </summary>
    public bool DialogAutoCloseEnabled
    {
        get => _dialogAutoCloseSeconds > 0;
        set => DialogAutoCloseSeconds = value ? DefaultDialogAutoCloseSeconds : 0.0;
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

    // NOTE: Camera view (Zoom/Pitch/Mode), day/night current value, 2D/north-up
    // orientation, window geometry, and panel positions are persistent
    // application STATE ("where the app was"), not config. They moved to
    // PersistentAppState (appstate.json). Only display PREFERENCES remain here.

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

    private bool _fieldTextureMoveable;
    /// <summary>
    /// When true, the ground texture is rendered as world-tiled bitmaps
    /// so it visibly scrolls under the tractor as the camera pans. When
    /// false (default), the texture is rendered as a single stretched
    /// bitmap centered on the camera — FPS-stable but visually static.
    /// </summary>
    public bool FieldTextureMoveable
    {
        get => _fieldTextureMoveable;
        set => SetProperty(ref _fieldTextureMoveable, value);
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
    private double _displayResolutionMultiplier = GetDefaultResolutionMultiplier();
    public double DisplayResolutionMultiplier
    {
        get => _displayResolutionMultiplier;
        set => SetProperty(ref _displayResolutionMultiplier, Math.Clamp(value, 1.0, 6.0));
    }

    // Default High (1.5) on mobile, Ultra (1.0) on desktop. iPad Pro 2nd gen
    // measured ~40 FPS at High with AA on — well above the 24 FPS floor —
    // and visual quality is close enough to Ultra that it's the right
    // first-launch default for tablets. Users can dial up to Ultra in
    // Display config when running on faster hardware.
    private static double GetDefaultResolutionMultiplier() =>
        OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() ? 1.5 : 1.0;
}
