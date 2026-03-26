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

using AgValoniaGPS.Models.Configuration;
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
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(BrightnessDisplay));
        }
    }

    public string BrightnessDisplay => _displaySettings.IsBrightnessSupported
        ? $"{_displaySettings.Brightness}%"
        : "??";

    #endregion

    #region Display Config Forwarding

    /// <summary>
    /// Handles PropertyChanged from DisplayConfig (fired when Configuration dialog toggles
    /// are changed). Syncs the display settings service and raises property changed notifications
    /// so the rendering layer (MainWindow/MapControl) can react.
    /// </summary>
    private void OnDisplayConfigChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var display = ConfigStore.Display;

        switch (e.PropertyName)
        {
            case nameof(DisplayConfig.GridVisible):
                // Sync DisplaySettingsService (which fires GridVisibilityChanged)
                _displaySettings.IsGridOn = display.GridVisible;
                this.RaisePropertyChanged(nameof(IsGridOn));
                break;

            case nameof(DisplayConfig.IsDayMode):
                _displaySettings.IsDayMode = display.IsDayMode;
                this.RaisePropertyChanged(nameof(IsDayMode));
                break;

            case nameof(DisplayConfig.Is2DMode):
                _displaySettings.Is2DMode = display.Is2DMode;
                this.RaisePropertyChanged(nameof(Is2DMode));
                break;

            case nameof(DisplayConfig.IsNorthUp):
                _displaySettings.IsNorthUp = display.IsNorthUp;
                this.RaisePropertyChanged(nameof(IsNorthUp));
                break;

            case nameof(DisplayConfig.CameraPitch):
                _displaySettings.CameraPitch = display.CameraPitch;
                this.RaisePropertyChanged(nameof(CameraPitch));
                break;

            // Display toggle properties - raise so platform code can forward to MapControl
            case nameof(DisplayConfig.StartFullscreen):
                this.RaisePropertyChanged(nameof(DisplayStartFullscreen));
                break;

            // All remaining display toggle properties use a naming convention:
            // "Display" prefix + property name. Platform code (MainWindow) reacts to these.
            case nameof(DisplayConfig.PolygonsVisible):
            case nameof(DisplayConfig.SpeedometerVisible):
            case nameof(DisplayConfig.KeyboardEnabled):
            case nameof(DisplayConfig.HeadlandDistanceVisible):
            case nameof(DisplayConfig.AutoDayNight):
            case nameof(DisplayConfig.SvennArrowVisible):
            case nameof(DisplayConfig.ElevationLogEnabled):
            case nameof(DisplayConfig.FieldTextureVisible):
            case nameof(DisplayConfig.ExtraGuidelines):
            case nameof(DisplayConfig.ExtraGuidelinesCount):
            case nameof(DisplayConfig.LineSmoothEnabled):
            case nameof(DisplayConfig.DirectionMarkersVisible):
            case nameof(DisplayConfig.SectionLinesVisible):
                this.RaisePropertyChanged("Display" + e.PropertyName);
                break;
        }
    }

    // Display config convenience properties (read from ConfigurationStore.Display)
    // These are raised by OnDisplayConfigChanged when the Configuration dialog changes them.
    public bool DisplayPolygonsVisible => ConfigStore.Display.PolygonsVisible;
    public bool DisplaySpeedometerVisible => ConfigStore.Display.SpeedometerVisible;
    public bool DisplayKeyboardEnabled => ConfigStore.Display.KeyboardEnabled;
    public bool DisplayHeadlandDistanceVisible => ConfigStore.Display.HeadlandDistanceVisible;
    public bool DisplayAutoDayNight => ConfigStore.Display.AutoDayNight;
    public bool DisplaySvennArrowVisible => ConfigStore.Display.SvennArrowVisible;
    public bool DisplayStartFullscreen => ConfigStore.Display.StartFullscreen;
    public bool DisplayElevationLogEnabled => ConfigStore.Display.ElevationLogEnabled;
    public bool DisplayFieldTextureVisible => ConfigStore.Display.FieldTextureVisible;
    public bool DisplayExtraGuidelines => ConfigStore.Display.ExtraGuidelines;
    public int DisplayExtraGuidelinesCount => ConfigStore.Display.ExtraGuidelinesCount;
    public bool DisplayLineSmoothEnabled => ConfigStore.Display.LineSmoothEnabled;
    public bool DisplayDirectionMarkersVisible => ConfigStore.Display.DirectionMarkersVisible;
    public bool DisplaySectionLinesVisible => ConfigStore.Display.SectionLinesVisible;

    #endregion
}
