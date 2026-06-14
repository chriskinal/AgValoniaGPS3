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


using AgValoniaGPS.Models.State;
using CommunityToolkit.Mvvm.Input;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Configuration dialog commands - profiles, settings dialogs.
/// </summary>
public partial class MainViewModel
{
    private void InitializeConfigurationCommands()
    {
        // Split Vehicle / Tool configuration dialogs — chained sub-dialogs of the
        // picker. A single ConfigurationViewModel backs both (they edit the same live
        // store). Pushed onto the chain so Back returns to the picker.
        ShowVehicleConfigDialogCommand = new RelayCommand(() =>
        {
            EnsureConfigurationViewModel();
            PushChainDialog(DialogType.VehicleConfig);
        });

        ShowToolConfigDialogCommand = new RelayCommand(() =>
        {
            EnsureConfigurationViewModel();
            PushChainDialog(DialogType.ToolConfig);
        });

        // Load Vehicle / Tool picker (#346) — the hub, opened directly from the gear
        // icon as the root of the config chain. Its "Configure Vehicle" / "Configure
        // Tool" buttons push the split dialogs onto the chain.
        ShowLoadVehicleToolDialogCommand = new RelayCommand(() =>
        {
            LoadVehicleToolDialogVm = new LoadVehicleToolDialogViewModel(
                _configurationService,
                onClose: () => State.UI.CloseDialog(),
                confirm: (msg, action) => ShowConfirmationDialog("Confirm", msg, action),
                confirmChoice: (msg, confirmLabel, cancelLabel, action) =>
                    ShowConfirmationDialog("Profile Damaged", msg, confirmLabel, cancelLabel, action),
                onConfigureVehicle: () => ShowVehicleConfigDialogCommand!.Execute(null),
                onConfigureTool: () => ShowToolConfigDialogCommand!.Execute(null));
            OpenChainDialog(DialogType.LoadVehicleTool);
        });

        CancelLoadVehicleToolDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // AutoSteer Configuration Panel
        ShowAutoSteerConfigCommand = new RelayCommand(() =>
        {
            AutoSteerConfigViewModel ??= new AutoSteerConfigViewModel(
                _configurationService,
                _udpService,
                _autoSteerService,
                ShowSteerWizard,
                () => ShowSmartWasCommand?.Execute(null));
            AutoSteerConfigViewModel.IsPanelVisible = true;
        });

        // Smart WAS calibration dialog
        ShowSmartWasCommand = new RelayCommand(() =>
        {
            SmartWasViewModel ??= new SmartWasViewModel(
                _smartWasService,
                _configurationService,
                _udpService,
                _autoSteerService,
                _dispatcher);
            State.UI.ShowDialog(DialogType.SmartWas);
        });

        CloseSmartWasDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });
    }

    /// <summary>
    /// Lazily construct the shared ConfigurationViewModel that backs both the
    /// Vehicle and Tool dialogs, wiring CloseRequested (Apply/Cancel) to hide
    /// whichever split dialog is open.
    /// </summary>
    private void EnsureConfigurationViewModel()
    {
        if (ConfigurationViewModel != null)
            return;

        ConfigurationViewModel = new ConfigurationViewModel(_configurationService);
        // Apply/Cancel inside the config dialog dismiss the whole config chain back
        // to the map (the picker has already applied the profile selection).
        ConfigurationViewModel.CloseRequested += (s, e) => CloseChain();
    }
}
