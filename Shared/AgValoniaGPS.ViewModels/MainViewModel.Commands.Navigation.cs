using System;
using System.Windows.Input;
using AgValoniaGPS.Models.State;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing navigation and view settings command initialization.
/// </summary>
public partial class MainViewModel
{
    private void InitializeNavigationCommands()
    {
        // Use simple RelayCommand to avoid ReactiveCommand threading issues
        // Use property setters instead of service methods to ensure PropertyChanged fires
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

        IncreaseCameraPitchCommand = new RelayCommand(() =>
        {
            CameraPitch += 5.0;
        });

        DecreaseCameraPitchCommand = new RelayCommand(() =>
        {
            CameraPitch -= 5.0;
        });

        IncreaseBrightnessCommand = new RelayCommand(() =>
        {
            Brightness += 5; // Match the step from DisplaySettingsService
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

        // Right Navigation Panel Commands
        ToggleContourModeCommand = new RelayCommand(() =>
        {
            IsContourModeOn = !IsContourModeOn;
            StatusMessage = IsContourModeOn ? "Contour Mode: ON" : "Contour Mode: OFF";
        });

        ToggleManualModeCommand = new RelayCommand(() =>
        {
            IsManualSectionMode = !IsManualSectionMode;
            StatusMessage = IsManualSectionMode ? "Manual Section Mode: ON" : "Auto Section Mode";
        });

        ToggleSectionMasterCommand = new RelayCommand(() =>
        {
            IsSectionMasterOn = !IsSectionMasterOn;
            StatusMessage = IsSectionMasterOn ? "Section Master: ON" : "Section Master: OFF";
        });

        ToggleYouTurnCommand = new RelayCommand(() =>
        {
            IsYouTurnEnabled = !IsYouTurnEnabled;
            StatusMessage = IsYouTurnEnabled ? "U-Turn Auto: ON" : "U-Turn Auto: OFF";
        });

        ToggleAutoSteerCommand = new RelayCommand(() =>
        {
            IsAutoSteerEngaged = !IsAutoSteerEngaged;
            StatusMessage = IsAutoSteerEngaged ? "AutoSteer: ENGAGED" : "AutoSteer: Disengaged";
        });

        // Map Commands
        Toggle3DModeCommand = new RelayCommand(() =>
        {
            Is2DMode = !Is2DMode;
        });

        ZoomInCommand = new RelayCommand(() =>
        {
            ZoomInRequested?.Invoke();
        });

        ZoomOutCommand = new RelayCommand(() =>
        {
            ZoomOutRequested?.Invoke();
        });
    }
}
