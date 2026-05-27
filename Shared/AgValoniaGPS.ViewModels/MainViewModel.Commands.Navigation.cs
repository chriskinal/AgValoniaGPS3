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
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Navigation panel commands - view toggles, camera controls.
/// </summary>
public partial class MainViewModel
{
    private void InitializeNavigationCommands()
    {
        // Panel toggle commands. Nav fly-outs are mutually exclusive: opening
        // one closes the others so they never stack ("too many windows").
        ToggleFileMenuPanelCommand = new RelayCommand(() =>
        {
            bool willOpen = !IsFileMenuPanelVisible;
            CloseAllMenuPanels();
            IsFileMenuPanelVisible = willOpen;
            RestartDialogAutoCloseTimer();
        });

        ToggleToolsPanelCommand = new RelayCommand(() =>
        {
            bool willOpen = !IsToolsPanelVisible;
            CloseAllMenuPanels();
            IsToolsPanelVisible = willOpen;
            RestartDialogAutoCloseTimer();
        });

        ToggleConfigurationPanelCommand = new RelayCommand(() =>
        {
            bool willOpen = !IsConfigurationPanelVisible;
            CloseAllMenuPanels();
            IsConfigurationPanelVisible = willOpen;
            RestartDialogAutoCloseTimer();
        });

        ToggleFieldOperationsPanelCommand = new RelayCommand(() =>
        {
            bool willOpen = !IsFieldOperationsPanelVisible;
            CloseAllMenuPanels();
            IsFieldOperationsPanelVisible = willOpen;
            RestartDialogAutoCloseTimer();
        });

        ToggleFieldToolsPanelCommand = new RelayCommand(() =>
        {
            bool willOpen = !IsFieldToolsPanelVisible;
            CloseAllMenuPanels();
            IsFieldToolsPanelVisible = willOpen;
            RestartDialogAutoCloseTimer();
        });

        ToggleAutoTrackCommand = new RelayCommand(() =>
        {
            IsAutoTrackEnabled = !IsAutoTrackEnabled;
            StatusMessage = IsAutoTrackEnabled ? "Auto track select ON" : "Auto track select OFF";
        });

        // View mode commands
        ToggleGridCommand = new RelayCommand(() =>
        {
            IsGridOn = !IsGridOn;
        });

        ToggleDayNightCommand = new RelayCommand(() =>
        {
            IsDayMode = !IsDayMode;
            _mapService.SetDayMode(IsDayMode);
            ApplyThemeVariant(IsDayMode);
            // Disable auto day/night when user manually toggles theme
            ConfigurationStore.Instance.Display.AutoDayNight = false;
        });

        Toggle2D3DCommand = new RelayCommand(() =>
        {
            if (Is2DMode)
            {
                // Switching to 3D: restore last 3D pitch
                Is2DMode = false;
                CameraPitch = _last3DPitch;
                _mapService.Set3DMode(true);
            }
            else
            {
                // Switching to 2D: overhead view
                Is2DMode = true;
                CameraPitch = -90.0;
                _mapService.Set3DMode(false);
            }
        });

        ToggleNorthUpCommand = new RelayCommand(() =>
        {
            IsNorthUp = !IsNorthUp;
            _mapService.SetNorthUp(IsNorthUp);
        });

        ToggleCameraModeCommand = new RelayCommand(() =>
        {
            var oldMode = CameraMode;
            // Explicit 4-state cycle: H -> N -> M -> C -> H
            CameraMode = CameraMode switch
            {
                Models.CameraMode.HeadingUp => Models.CameraMode.NorthUp,
                Models.CameraMode.NorthUp => Models.CameraMode.Map,
                Models.CameraMode.Map => Models.CameraMode.Free,
                Models.CameraMode.Free => Models.CameraMode.HeadingUp,
                _ => Models.CameraMode.Map
            };
            Console.WriteLine($"[Compass] {oldMode} -> {CameraMode}");
        });

        // Camera controls - tilt transitions between 2D and 3D automatically
        // "Tilt Down" = look more toward horizon = more 3D (pitch increases toward -10)
        // "Tilt Up" = look more overhead = more 2D (pitch decreases toward -90)
        IncreaseCameraPitchCommand = new RelayCommand(() =>
        {
            double newPitch = CameraPitch - 5.0;
            if (newPitch <= -90.0)
            {
                Is2DMode = true;
                CameraPitch = -90.0;
                _mapService.Set3DMode(false);
            }
            else
            {
                CameraPitch = newPitch;
            }
        });

        DecreaseCameraPitchCommand = new RelayCommand(() =>
        {
            if (Is2DMode)
            {
                Is2DMode = false;
                CameraPitch = -85.0;
                _mapService.Set3DMode(true);
            }
            else
            {
                CameraPitch += 5.0;
            }
        });

        // Display resolution: cycle Ultra → High → Medium → Low → Min → Ultra
        CycleDisplayResolutionCommand = new RelayCommand(() =>
        {
            var display = ConfigStore.Display;
            display.DisplayResolutionMultiplier = display.DisplayResolutionMultiplier switch
            {
                < 1.25 => 1.5,  // Ultra → High
                < 2.0  => 2.5,  // High → Medium
                < 3.25 => 4.0,  // Medium → Low
                < 5.0  => 6.0,  // Low → Min
                _ => 1.0,       // Min → Ultra
            };
            OnPropertyChanged(nameof(DisplayResolutionLabel));
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
    }

    /// <summary>
    /// Closes every nav fly-out menu. Used to keep the fly-outs mutually
    /// exclusive (opening one closes the rest) and to "collapse behind you"
    /// when a dialog opens over them — see the DialogChanged hook wired in
    /// the MainViewModel constructor.
    /// </summary>
    private void CloseAllMenuPanels()
    {
        IsFileMenuPanelVisible = false;
        IsToolsPanelVisible = false;
        IsConfigurationPanelVisible = false;
        IsFieldOperationsPanelVisible = false;
        IsFieldToolsPanelVisible = false;
    }

    private bool IsAnyMenuPanelOpen =>
        IsFileMenuPanelVisible || IsToolsPanelVisible ||
        IsConfigurationPanelVisible || IsFieldOperationsPanelVisible || IsFieldToolsPanelVisible;

    private Avalonia.Threading.DispatcherTimer? _dialogAutoCloseTimer;

    // Dialogs exempt from idle auto-close: those needing an explicit decision,
    // and every dialog with text/coordinate entry — a 7s pause (e.g. looking up
    // sim GPS coords) must not silently discard in-progress input. Auto-close
    // still applies to the nav fly-outs and the glanceable settings/info dialogs.
    private static readonly System.Collections.Generic.HashSet<Models.State.DialogType> _autoCloseExemptDialogs = new()
    {
        // Explicit decision required
        Models.State.DialogType.Confirmation,
        Models.State.DialogType.Error,
        // Text / coordinate / data entry
        Models.State.DialogType.NumericInput,
        Models.State.DialogType.NtripProfileEditor,
        Models.State.DialogType.SimCoords,
        Models.State.DialogType.FlagByLatLon,
        Models.State.DialogType.FlagList,
        Models.State.DialogType.NewField,
        Models.State.DialogType.FromExistingField,
        Models.State.DialogType.KmlImport,
        Models.State.DialogType.IsoXmlImport,
        Models.State.DialogType.LoadVehicleTool,
        Models.State.DialogType.RecordedPath,
        Models.State.DialogType.StartWorkSession,
        Models.State.DialogType.FieldBuilder,
        Models.State.DialogType.BugReport,
    };

    /// <summary>
    /// (Re)starts the auto-close countdown for the open dialog/menu. Called when
    /// a surface opens and on each interaction with it (see NotifyDialogInteraction).
    /// After ConfigStore.Display.DialogAutoCloseSeconds of no interaction the open
    /// surface closes, returning focus to the map. 0 disables; exempt dialogs and
    /// the busy overlay never auto-close.
    /// </summary>
    public void RestartDialogAutoCloseTimer()
    {
        _dialogAutoCloseTimer?.Stop();

        double seconds = ConfigStore.Display.DialogAutoCloseSeconds;
        if (seconds <= 0) return;

        var dialog = State.UI.ActiveDialog;
        bool dialogAutoCloseable = dialog != Models.State.DialogType.None
            && !_autoCloseExemptDialogs.Contains(dialog);

        if (!dialogAutoCloseable && !IsAnyMenuPanelOpen) return;

        _dialogAutoCloseTimer ??= CreateDialogAutoCloseTimer();
        _dialogAutoCloseTimer.Interval = TimeSpan.FromSeconds(seconds);
        _dialogAutoCloseTimer.Start();
    }

    private Avalonia.Threading.DispatcherTimer CreateDialogAutoCloseTimer()
    {
        var timer = new Avalonia.Threading.DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            _dialogAutoCloseTimer?.Stop();
            var dialog = State.UI.ActiveDialog;
            if (dialog != Models.State.DialogType.None && !_autoCloseExemptDialogs.Contains(dialog))
                State.UI.CloseDialog();
            else if (IsAnyMenuPanelOpen)
                CloseAllMenuPanels();
        };
        return timer;
    }

    /// <summary>
    /// Views call this when the user interacts with the open dialog/menu so the
    /// auto-close countdown resets (only dialog interaction counts, not the map).
    /// </summary>
    public void NotifyDialogInteraction() => RestartDialogAutoCloseTimer();

    /// <summary>
    /// Applies the Avalonia theme variant based on day/night mode.
    /// Day mode = Light theme, Night mode = Dark theme.
    /// </summary>
    internal static void ApplyThemeVariant(bool isDayMode)
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = isDayMode
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }
    }
}
